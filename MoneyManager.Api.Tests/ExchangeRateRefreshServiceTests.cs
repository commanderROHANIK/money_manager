using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Currency;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The one rule the whole automatic-rate feature rests on: <b>a rate the user entered is never
/// overwritten by one that was fetched.</b>
///
/// <para>
/// Everything else about this feature is recoverable. Getting this wrong is not: a landlord who
/// recorded the rate their bank actually gave them on the day of a transfer, and later finds a
/// daily reference rate in its place, has had a decision quietly discarded — and nothing in the
/// output says so, because both rows look like a rate.
/// </para>
///
/// <para>
/// Against real SQLite through the real DbContext, like <see cref="TenantIsolationTests"/> and for
/// the same reason: the global query filter and the owner stamping are what make "the user's
/// rates" mean anything, and the in-memory provider does not enforce them.
/// </para>
/// </summary>
public sealed class ExchangeRateRefreshServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MoneyManagerDbContext> _options;

    private const int Alice = 1;
    private const int Bob = 2;

    private static readonly DateTime Published = new(2026, 8, 10);
    private static readonly DateTime Entered = new(2026, 7, 1);

    public ExchangeRateRefreshServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MoneyManagerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var setup = ContextFor(null);
        setup.Database.EnsureCreated();

        setup.Users.AddRange(
            new User { Id = Alice, Username = "alice", NormalizedUsername = "ALICE", Email = "a@e.com", NormalizedEmail = "A@E.COM" },
            new User { Id = Bob, Username = "bob", NormalizedUsername = "BOB", Email = "b@e.com", NormalizedEmail = "B@E.COM" });

        setup.SaveChanges();
    }

    [Fact]
    public async Task A_pair_with_no_row_is_fetched_and_stored_with_its_source()
    {
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));

        using var context = ContextFor(Alice);
        var written = await RefreshFor(context, Alice, provider);

        Assert.Equal(1, written);

        var stored = Assert.Single(await context.ExchangeRates.ToListAsync());

        Assert.Equal(398.5m, stored.Rate);
        Assert.Equal(Published, stored.AsOf);

        // Stored rather than inferred later. A row that cannot say where it came from turns every
        // disclosure built on it into a guess.
        Assert.Equal(ExchangeRateSource.Ecb, stored.Source);
    }

    [Fact]
    public async Task A_rate_the_user_entered_survives_a_refresh_untouched()
    {
        Seed(Alice, new ExchangeRate
        {
            BaseCurrency = "EUR",
            QuoteCurrency = "HUF",
            Rate = 400m,
            AsOf = Entered,
            Source = ExchangeRateSource.Manual,
        });

        var provider = ProviderReturning(("EUR", "HUF", 398.5m));

        using var context = ContextFor(Alice);
        var written = await RefreshFor(context, Alice, provider);

        Assert.Equal(0, written);

        var stored = Assert.Single(await context.ExchangeRates.ToListAsync());

        Assert.Equal(400m, stored.Rate);
        Assert.Equal(Entered, stored.AsOf);
        Assert.Equal(ExchangeRateSource.Manual, stored.Source);
    }

    [Fact]
    public async Task A_pair_the_user_entered_backwards_is_not_even_asked_about()
    {
        // HUF→EUR and EUR→HUF are the same fact, so having entered one is having spoken for both.
        // Asked and then discarded would be correct too; not asked is cheaper and says the same
        // thing about what the user meant.
        Seed(Alice, new ExchangeRate
        {
            BaseCurrency = "HUF",
            QuoteCurrency = "EUR",
            Rate = 0.0025m,
            AsOf = Entered,
            Source = ExchangeRateSource.Manual,
        });

        var provider = ProviderReturning(("EUR", "HUF", 398.5m));

        using var context = ContextFor(Alice);
        await RefreshFor(context, Alice, provider, held: ["HUF"]);

        Assert.Empty(provider.Requested);
    }

    [Fact]
    public async Task A_fetched_row_stored_the_other_way_round_is_updated_rather_than_duplicated()
    {
        // The base-currency change. A row was fetched while the user reported in EUR; they switch
        // to HUF and the next refresh asks the other way round.
        //
        // The unique index is on (UserId, Base, Quote), so it does not stop EUR→HUF and HUF→EUR
        // both existing — nothing does except this lookup. Two rows for one pair means the table
        // shows the pair twice, and the converter prefers whichever direction is asked for while
        // the other silently goes stale.
        Seed(Alice, new ExchangeRate
        {
            BaseCurrency = "EUR",
            QuoteCurrency = "HUF",
            Rate = 390m,
            AsOf = new DateTime(2026, 1, 5),
            Source = ExchangeRateSource.Ecb,
        });

        var provider = new StubProvider(
            [new ProvidedRate("HUF", "EUR", 0.0025m, Published, ExchangeRateSource.Ecb)]);

        using var context = ContextFor(Alice);

        // Reporting in HUF now, so the pair is asked about the other way round. Note this only
        // reaches the loop because the target changed — with baseCurrency still EUR, "EUR" would
        // be filtered out of `wanted` as the target itself and nothing would be fetched at all.
        await RefreshFor(context, Alice, provider, held: ["EUR"], baseCurrency: "HUF");

        var stored = Assert.Single(await context.ExchangeRates.ToListAsync());

        // Rewritten into the direction just fetched, rather than inverted into the stored one:
        // both say the same thing, and storing the figure the provider gave avoids a reciprocal.
        Assert.Equal("HUF", stored.BaseCurrency);
        Assert.Equal("EUR", stored.QuoteCurrency);
        Assert.Equal(0.0025m, stored.Rate);
        Assert.Equal(Published, stored.AsOf);
    }

    [Fact]
    public async Task A_manual_row_is_not_overwritten_by_a_fetch_of_the_opposite_direction()
    {
        // The same defect, aimed at the invariant rather than at tidiness. The pair filter above
        // means this provider is answering with something it was never asked for — which a mirror,
        // a future provider, or a response whose `base` field disagrees with the request could all
        // do. A lookup that only matched the fetched direction would see no row, insert one, and
        // leave a fetched rate shadowing a rate the user asserted.
        Seed(Alice, new ExchangeRate
        {
            BaseCurrency = "EUR",
            QuoteCurrency = "HUF",
            Rate = 400m,
            AsOf = Entered,
            Source = ExchangeRateSource.Manual,
        });

        var provider = new StubProvider(
            [new ProvidedRate("HUF", "EUR", 0.0025m, Published, ExchangeRateSource.Ecb)]);

        using var context = ContextFor(Alice);
        var written = await RefreshFor(context, Alice, provider, held: ["GBP"]);

        Assert.Equal(0, written);

        var stored = Assert.Single(await context.ExchangeRates.ToListAsync());

        Assert.Equal(400m, stored.Rate);
        Assert.Equal(ExchangeRateSource.Manual, stored.Source);
    }

    [Fact]
    public async Task A_previously_fetched_row_is_brought_up_to_date()
    {
        Seed(Alice, new ExchangeRate
        {
            BaseCurrency = "EUR",
            QuoteCurrency = "HUF",
            Rate = 390m,
            AsOf = new DateTime(2026, 1, 5),
            Source = ExchangeRateSource.Ecb,
        });

        var provider = ProviderReturning(("EUR", "HUF", 398.5m));

        using var context = ContextFor(Alice);
        var written = await RefreshFor(context, Alice, provider);

        Assert.Equal(1, written);

        var stored = Assert.Single(await context.ExchangeRates.ToListAsync());

        Assert.Equal(398.5m, stored.Rate);
        Assert.Equal(Published, stored.AsOf);
    }

    [Fact]
    public async Task A_provider_that_answers_with_nothing_leaves_the_stored_rates_alone()
    {
        Seed(Alice, new ExchangeRate
        {
            BaseCurrency = "EUR",
            QuoteCurrency = "HUF",
            Rate = 390m,
            AsOf = new DateTime(2026, 1, 5),
            Source = ExchangeRateSource.Ecb,
        });

        using var context = ContextFor(Alice);
        var written = await RefreshFor(context, Alice, new StubProvider([]));

        // An unreachable provider is an ordinary condition, and the right answer to it is the
        // rates already on record — not an empty table and not an exception up through a dashboard.
        Assert.Equal(0, written);

        var stored = Assert.Single(await context.ExchangeRates.ToListAsync());
        Assert.Equal(390m, stored.Rate);
    }

    [Fact]
    public async Task The_no_op_provider_writes_nothing_at_all()
    {
        // What a deployment with Features:AutomaticExchangeRates off actually gets. The behaviour
        // has to be indistinguishable from the app before rates were ever fetched.
        using var context = ContextFor(Alice);

        var written = await RefreshFor(context, Alice, new NoExchangeRateProvider());

        Assert.Equal(0, written);
        Assert.Empty(await context.ExchangeRates.ToListAsync());
    }

    [Fact]
    public async Task A_second_refresh_inside_the_window_does_not_ask_again()
    {
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));
        var cache = new MemoryCache(new MemoryCacheOptions());

        using var first = ContextFor(Alice);
        await RefreshFor(first, Alice, provider, cache: cache);

        using var second = ContextFor(Alice);
        await RefreshFor(second, Alice, provider, cache: cache);

        // The ECB publishes once a working day. Asking on every page load spends requests to learn
        // nothing, and turns one visitor into a steady stream of outbound calls.
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task Dropping_a_manual_rate_lets_the_pair_be_asked_about_again()
    {
        // The user's way of saying "stop using my rate, use whatever today's is" is to remove the
        // row — that is what automatic means here, a pair nobody has spoken for. Without this the
        // window says "already asked about your pairs" for up to six hours, so the row disappears
        // and nothing takes its place. From the outside that is indistinguishable from the whole
        // feature not working, which is precisely how it was reported.
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));
        var cache = new MemoryCache(new MemoryCacheOptions());

        Seed(Alice, new ExchangeRate
        {
            BaseCurrency = "EUR",
            QuoteCurrency = "HUF",
            Rate = 400m,
            AsOf = Entered,
            Source = ExchangeRateSource.Manual,
        });

        using (var first = ContextFor(Alice))
            await RefreshFor(first, Alice, provider, cache: cache);

        // Skipped, correctly: the pair was spoken for.
        using (var check = ContextFor(Alice))
            Assert.Equal(400m, (await check.ExchangeRates.SingleAsync()).Rate);

        using (var dropped = ContextFor(Alice))
        {
            dropped.ExchangeRates.RemoveRange(await dropped.ExchangeRates.ToListAsync());
            await dropped.SaveChangesAsync();

            ServiceFor(dropped, Alice, provider, cache).Invalidate("EUR");
        }

        using var after = ContextFor(Alice);
        await RefreshFor(after, Alice, provider, cache: cache);

        var filled = await after.ExchangeRates.SingleAsync();
        Assert.Equal(398.5m, filled.Rate);
        Assert.Equal(ExchangeRateSource.Ecb, filled.Source);
    }

    [Fact]
    public async Task Invalidating_one_users_window_leaves_anothers_alone()
    {
        // The key is per user. Invalidating on every write would hand one landlord's edit the
        // power to spend everybody else's fetch, which is the rate limit undone by accident.
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));
        var cache = new MemoryCache(new MemoryCacheOptions());

        using (var alice = ContextFor(Alice))
            await RefreshFor(alice, Alice, provider, cache: cache);

        using (var bob = ContextFor(Bob))
            await RefreshFor(bob, Bob, provider, cache: cache);

        Assert.Equal(2, provider.Calls);

        using (var alice = ContextFor(Alice))
            ServiceFor(alice, Alice, provider, cache).Invalidate("EUR");

        using (var bob = ContextFor(Bob))
            await RefreshFor(bob, Bob, provider, cache: cache);

        // Bob's window is untouched, so his refresh still answers from it.
        Assert.Equal(2, provider.Calls);

        using (var alice = ContextFor(Alice))
            await RefreshFor(alice, Alice, provider, cache: cache);

        Assert.Equal(3, provider.Calls);
    }

    [Fact]
    public async Task An_explicit_refresh_ignores_the_window()
    {
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));
        var cache = new MemoryCache(new MemoryCacheOptions());

        using var first = ContextFor(Alice);
        await RefreshFor(first, Alice, provider, cache: cache);

        using var second = ContextFor(Alice);
        await RefreshFor(second, Alice, provider, cache: cache, force: true);

        // Someone who pressed "refresh" is asking for today's number, and waiting out a window
        // they cannot see is not an answer to that.
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task An_explicit_refresh_still_has_a_floor_under_it()
    {
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));
        var cache = new MemoryCache(new MemoryCacheOptions());

        using var first = ContextFor(Alice);
        await RefreshFor(first, Alice, provider, cache: cache, force: true);

        using var second = ContextFor(Alice);
        await RefreshFor(second, Alice, provider, cache: cache, force: true);

        // Otherwise "ignore the window" is an authenticated caller's lever for driving unbounded
        // outbound traffic at the provider — and CLAUDE.md's precondition for having an outbound
        // call at all is that a cache limits it, which `force` would be a hole in.
        //
        // Nothing is lost by the floor: the endpoint answers with the table either way, and the
        // ECB publishes once a working day, so a second press this soon could not have returned a
        // different number.
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task One_users_window_does_not_silence_anothers_refresh()
    {
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));
        var cache = new MemoryCache(new MemoryCacheOptions());

        using var alice = ContextFor(Alice);
        await RefreshFor(alice, Alice, provider, cache: cache);

        using var bob = ContextFor(Bob);
        await RefreshFor(bob, Bob, provider, cache: cache);

        // Partitioned by user, like everything else here. A cache key shared across the whole
        // process would mean the first landlord to load a dashboard each morning decides whether
        // anyone else gets a rate.
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task Rates_fetched_for_one_user_are_not_visible_to_another()
    {
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));

        using var alice = ContextFor(Alice);
        await RefreshFor(alice, Alice, provider);

        using var bob = ContextFor(Bob);

        // Fetched rows are owned entities like any other. They arrive from outside the request,
        // which is exactly the shape of thing that gets written without an owner and then read by
        // everybody.
        Assert.Empty(await bob.ExchangeRates.ToListAsync());
    }

    [Fact]
    public async Task A_nonsense_quote_is_dropped_rather_than_stored()
    {
        // A provider answering with a zero, a negative, or a pair that is the same currency twice
        // is not a reason to write a row that cannot be inverted and cannot describe an exchange.
        var provider = new StubProvider(
        [
            new ProvidedRate("EUR", "HUF", 0m, Published, ExchangeRateSource.Ecb),
            new ProvidedRate("EUR", "GBP", -1m, Published, ExchangeRateSource.Ecb),
            new ProvidedRate("EUR", "EUR", 1m, Published, ExchangeRateSource.Ecb),
        ]);

        using var context = ContextFor(Alice);
        var written = await RefreshFor(context, Alice, provider);

        Assert.Equal(0, written);
        Assert.Empty(await context.ExchangeRates.ToListAsync());
    }

    // ------------------------------------------------------------------
    // Where the fetch is actually triggered from
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_rollup_fetches_rather_than_waiting_for_someone_to_open_settings()
    {
        // The defect this catches is a wiring one, and it is invisible to every test that calls
        // RefreshAsync directly: fetching used to happen only in ExchangeRatesController, so it
        // only ever ran for a user who had already opened Settings. A landlord with a HUF flat and
        // a EUR mortgage, who never went looking for a rate table, saw null totals and "Add the
        // rate in Settings" with the feature switched on and working perfectly.
        //
        // CurrencyRollupService is the choke point both rollups go through, so this is where being
        // up to date has to be arranged.
        var provider = ProviderReturning(("EUR", "HUF", 398.5m));

        using var context = ContextFor(Alice);

        var rollup = new CurrencyRollupService(
            context,
            new StubCurrentUser(Alice),
            ServiceFor(context, Alice, provider));

        var loaded = await rollup.LoadAsync();

        Assert.Equal(1, provider.Calls);

        // And the rate it fetched is in the converter it hands back — the refresh has to happen
        // before the rates are read, which is an ordering mistake that would leave this green on
        // the second request and wrong on the first.
        Assert.Equal(398.5m, loaded.Rates.Convert(1m, new CurrencyPair("EUR", "HUF")));
    }

    // ------------------------------------------------------------------

    private MoneyManagerDbContext ContextFor(int? userId) =>
        new(_options, new StubCurrentUser(userId));

    private void Seed(int userId, ExchangeRate rate)
    {
        using var context = ContextFor(userId);
        context.ExchangeRates.Add(rate);
        context.SaveChanges();
    }

    private static StubProvider ProviderReturning(params (string From, string To, decimal Rate)[] rates) =>
        new([.. rates.Select(r => new ProvidedRate(r.From, r.To, r.Rate, Published, ExchangeRateSource.Ecb))]);

    /// <summary>
    /// Builds the service over <paramref name="context"/>, running as the user that context was
    /// opened for. The two must agree: a service told it is Alice over a context filtered to Bob
    /// would write rows one of them cannot read.
    /// </summary>
    private static ExchangeRateRefreshService ServiceFor(
        MoneyManagerDbContext context,
        int userId,
        IExchangeRateProvider provider,
        IMemoryCache? cache = null) =>
        new(context,
            provider,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            new StubCurrentUser(userId),
            Options.Create(new ExchangeRateProviderOptions()),
            NullLogger<ExchangeRateRefreshService>.Instance);

    private static Task<int> RefreshFor(
        MoneyManagerDbContext context,
        int userId,
        IExchangeRateProvider provider,
        IMemoryCache? cache = null,
        string[]? held = null,
        bool force = false,
        string baseCurrency = "EUR") =>
        ServiceFor(context, userId, provider, cache)
            .RefreshAsync(baseCurrency, held ?? ["HUF", "GBP"], force);

    public void Dispose() => _connection.Dispose();

    private sealed class StubCurrentUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }

    /// <summary>
    /// Records what it was asked for as well as what it returned, because "did not ask" is a
    /// distinct outcome from "asked and ignored the answer" — and the manual-wins rule is stated
    /// in terms of the first.
    /// </summary>
    private sealed class StubProvider(IReadOnlyList<ProvidedRate> rates) : IExchangeRateProvider
    {
        public int Calls { get; private set; }

        public List<string> Requested { get; } = [];

        public Task<IReadOnlyList<ProvidedRate>> GetRatesAsync(
            string baseCurrency,
            IReadOnlyCollection<string> quoteCurrencies,
            CancellationToken cancellationToken = default)
        {
            Calls += 1;
            Requested.AddRange(quoteCurrencies);

            return Task.FromResult(rates);
        }
    }
}
