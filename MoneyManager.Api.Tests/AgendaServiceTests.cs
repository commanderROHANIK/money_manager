using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Agenda;
using MoneyManager.Api.Services.Rent;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// <see cref="AgendaService"/> against real SQLite, for the two guarantees
/// <see cref="AgendaBuilderTests"/> cannot check with literals: that an acknowledgement actually
/// persists across calls, and that both the agenda and the acknowledgement obey the tenant
/// boundary. <see cref="RentScheduleServiceTests"/> already covers the rent-due feed on its own;
/// this suite treats it as a black box and focuses on the merge and the ack round trip.
/// </summary>
public sealed class AgendaServiceTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 3, 15);

    private const int Alice = 1;
    private const int Bob = 2;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MoneyManagerDbContext> _options;

    public AgendaServiceTests()
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

    private MoneyManagerDbContext ContextFor(int? userId) => new(_options, new StubCurrentUser(userId));

    private static AgendaService ServiceFor(MoneyManagerDbContext context) =>
        new(context, new RentScheduleService(context));

    private int SeedOpenLoanFor(int userId, DateTime dueDate)
    {
        using var context = ContextFor(userId);
        var loan = new Loan
        {
            LoanName = "Mortgage",
            CurrencyCode = "EUR",
            DueDate = dueDate,
            MonthlyPayment = 850m,
            IsPaidOff = false,
        };
        context.Loans.Add(loan);
        context.SaveChanges();
        return loan.Id;
    }

    private int SeedManualEventFor(int userId, DateTime eventDate)
    {
        using var context = ContextFor(userId);
        var ev = new UpcomingEvent { Title = "Insurance renewal", EventDate = eventDate };
        context.UpcomingEvents.Add(ev);
        context.SaveChanges();
        return ev.Id;
    }

    [Fact]
    public async Task An_open_loan_due_soon_appears_on_the_agenda()
    {
        SeedOpenLoanFor(Alice, Today.AddDays(5));

        using var context = ContextFor(Alice);
        var entries = await ServiceFor(context).GetAgendaAsync(days: 30, today: Today);

        var only = Assert.Single(entries);
        Assert.Equal(AgendaSource.Loan, only.Source);
    }

    [Fact]
    public async Task Acknowledging_an_entry_removes_it_and_the_removal_survives_a_fresh_context()
    {
        var loanId = SeedOpenLoanFor(Alice, Today.AddDays(-90));
        var key = $"loan:{loanId}";

        using (var context = ContextFor(Alice))
        {
            var before = await ServiceFor(context).GetAgendaAsync(days: 30, today: Today);
            Assert.Single(before);

            await ServiceFor(context).AcknowledgeAsync(key);
        }

        // A fresh context — the point is that the acknowledgement is a stored row, not
        // something that only lived in the DbContext that wrote it.
        using var reader = ContextFor(Alice);
        var after = await ServiceFor(reader).GetAgendaAsync(days: 30, today: Today);

        Assert.Empty(after);
    }

    [Fact]
    public async Task Acknowledging_an_entry_twice_does_not_throw()
    {
        var loanId = SeedOpenLoanFor(Alice, Today.AddDays(-1));
        var key = $"loan:{loanId}";

        using var context = ContextFor(Alice);
        var service = ServiceFor(context);

        await service.AcknowledgeAsync(key);
        await service.AcknowledgeAsync(key); // must be a no-op, not a unique-index violation

        Assert.Single(context.AgendaAcknowledgements.ToList());
    }

    [Fact]
    public async Task A_manual_event_and_a_loan_merge_into_one_sorted_agenda()
    {
        SeedManualEventFor(Alice, Today.AddDays(2));
        SeedOpenLoanFor(Alice, Today.AddDays(10));

        using var context = ContextFor(Alice);
        var entries = await ServiceFor(context).GetAgendaAsync(days: 30, today: Today);

        Assert.Equal(new[] { AgendaSource.Manual, AgendaSource.Loan }, entries.Select(e => e.Source));
    }

    [Fact]
    public async Task The_agenda_never_reaches_across_the_tenant_boundary()
    {
        SeedOpenLoanFor(Alice, Today.AddDays(1));
        SeedManualEventFor(Bob, Today.AddDays(1));

        using var asAlice = ContextFor(Alice);
        var alicesAgenda = await ServiceFor(asAlice).GetAgendaAsync(days: 30, today: Today);

        var only = Assert.Single(alicesAgenda);
        Assert.Equal(AgendaSource.Loan, only.Source);
    }

    [Fact]
    public async Task Acknowledging_another_users_key_does_not_hide_it_from_the_owner()
    {
        var loanId = SeedOpenLoanFor(Alice, Today.AddDays(1));
        var key = $"loan:{loanId}";

        // Bob acknowledges a key that names Alice's loan. His own ack table gains a row, but it
        // has no bearing on what Alice's context reports — AcknowledgeAsync writes through the
        // same tenant-scoped context, so it can only ever create a row Bob owns.
        using (var asBob = ContextFor(Bob))
        {
            await ServiceFor(asBob).AcknowledgeAsync(key);
        }

        using var asAlice = ContextFor(Alice);
        var alicesAgenda = await ServiceFor(asAlice).GetAgendaAsync(days: 30, today: Today);

        Assert.Single(alicesAgenda);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubCurrentUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }
}
