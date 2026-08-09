using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MoneyManager.Api.Services.Rent;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The rent schedule endpoints, over HTTP.
///
/// <para>
/// <c>RentScheduleBuilderTests</c> covers the pure builder and <c>RentScheduleServiceTests</c>
/// covers the service, but neither sends a request — so nothing exercised the controller's own
/// behaviour: its status codes, and the ownership check that happens before the service is ever
/// reached. This is the debt PR #22 left behind.
/// </para>
///
/// <para>
/// Two of these matter more than the rest. The 409 is the only endpoint behaviour that depends
/// on prior state, and it exists because recording rent is a button — a double click, a retried
/// request or two devices on the same page must not book the rent twice. And the arrears list is
/// the one endpoint that reaches across every property the caller owns in a single call, so it
/// is where a tenant-isolation mistake would be widest: <c>TenantIsolationTests</c> proves the
/// query filter at the data layer, and this proves the endpoint actually inherits it through a
/// three-hop join.
/// </para>
///
/// <para>
/// Ownership failures assert <c>404</c> rather than <c>403</c> throughout. A 403 would confirm
/// the row exists, which is an ownership oracle: it tells a caller that some other landlord owns
/// property 7 even though it tells them nothing about the property itself.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RentScheduleEndpointTests
{
    private const string Password = "correct-horse-battery";
    private const decimal MonthlyRent = 1_000m;

    private readonly ApiFactory _factory;

    public RentScheduleEndpointTests(ApiFactory factory) => _factory = factory;

    /// <summary>First of the current month; every date in these tests is relative to it.</summary>
    private static DateTime FirstOfThisMonth
    {
        get
        {
            var today = DateTime.UtcNow.Date;
            return new DateTime(today.Year, today.Month, 1);
        }
    }

    /// <summary>A month that has certainly started and whose rent has certainly fallen due.</summary>
    private static DateTime LastMonth => FirstOfThisMonth.AddMonths(-1);

    // ------------------------------------------------------------------
    // Recording rent
    // ------------------------------------------------------------------

    [Fact]
    public async Task Recording_rent_settles_the_month_it_was_recorded_against()
    {
        using var client = await AuthenticatedClientAsync("record");
        var propertyId = await CreateLetPropertyAsync(client);
        var period = LastMonth.ToString("yyyy-MM");

        var recorded = await client.PostAsJsonAsync(
            $"/api/RentalProperties/{propertyId}/rent-schedule/{period}/record", new { });

        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);

        using var body = JsonDocument.Parse(await recorded.Content.ReadAsStringAsync());
        Assert.Equal((int)RentPeriodStatus.Paid, body.RootElement.GetProperty("status").GetInt32());

        // The round trip is the point: the schedule stores nothing, so a fresh GET can only
        // report this month as paid if the write landed in the ledger and was derived back out.
        var month = await FetchPeriodAsync(client, propertyId, period);

        Assert.Equal((int)RentPeriodStatus.Paid, month.GetProperty("status").GetInt32());
        Assert.Equal(MonthlyRent, month.GetProperty("receivedAmount").GetDecimal());
        Assert.NotEmpty(month.GetProperty("paymentIds").EnumerateArray());
    }

    [Fact]
    public async Task Recording_the_same_month_twice_is_refused()
    {
        using var client = await AuthenticatedClientAsync("double-click");
        var propertyId = await CreateLetPropertyAsync(client);
        var period = LastMonth.ToString("yyyy-MM");
        var url = $"/api/RentalProperties/{propertyId}/rent-schedule/{period}/record";

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url, new { })).StatusCode);

        var second = await client.PostAsJsonAsync(url, new { });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // And the refusal has to be real: a 409 that still wrote the row would double the
        // month's income and leave the ledger disagreeing with the button that produced it.
        var month = await FetchPeriodAsync(client, propertyId, period);

        Assert.Equal(MonthlyRent, month.GetProperty("receivedAmount").GetDecimal());
        Assert.Single(month.GetProperty("paymentIds").EnumerateArray());
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("august")]
    [InlineData("2026")]
    public async Task A_period_that_is_not_a_month_is_refused(string period)
    {
        using var client = await AuthenticatedClientAsync($"bad-period-{period}");
        var propertyId = await CreateLetPropertyAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/RentalProperties/{propertyId}/rent-schedule/{period}/record", new { });

        // 400 rather than an unhandled parse exception reaching the client as a 500.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_month_that_has_not_started_yet_is_refused()
    {
        using var client = await AuthenticatedClientAsync("future");
        var propertyId = await CreateLetPropertyAsync(client);
        var period = FirstOfThisMonth.AddMonths(2).ToString("yyyy-MM");

        var response = await client.PostAsJsonAsync(
            $"/api/RentalProperties/{propertyId}/rent-schedule/{period}/record", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Ownership
    // ------------------------------------------------------------------

    [Fact]
    public async Task Another_users_schedule_is_not_found()
    {
        using var owner = await AuthenticatedClientAsync("schedule-owner");
        using var stranger = await AuthenticatedClientAsync("schedule-stranger");

        var propertyId = await CreateLetPropertyAsync(owner);

        var response = await stranger.GetAsync($"/api/RentalProperties/{propertyId}/rent-schedule");

        // 404, not 403: the response must not confirm that this property id belongs to someone.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Recording_rent_against_another_users_property_is_not_found()
    {
        using var owner = await AuthenticatedClientAsync("record-owner");
        using var stranger = await AuthenticatedClientAsync("record-stranger");

        var propertyId = await CreateLetPropertyAsync(owner);
        var period = LastMonth.ToString("yyyy-MM");

        var response = await stranger.PostAsJsonAsync(
            $"/api/RentalProperties/{propertyId}/rent-schedule/{period}/record", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The write must not have happened either. Asked by the owner, the month is still unpaid.
        var month = await FetchPeriodAsync(owner, propertyId, period);

        Assert.Equal((int)RentPeriodStatus.Unpaid, month.GetProperty("status").GetInt32());
        Assert.Equal(0m, month.GetProperty("receivedAmount").GetDecimal());
    }

    [Fact]
    public async Task Arrears_contain_only_the_callers_own_properties()
    {
        using var owner = await AuthenticatedClientAsync("arrears-owner");
        using var stranger = await AuthenticatedClientAsync("arrears-stranger");

        // Let, and nothing ever recorded — so every month since it began is overdue.
        var propertyId = await CreateLetPropertyAsync(owner);

        var ownersArrears = await ReadArrearsAsync(owner);
        var strangersArrears = await ReadArrearsAsync(stranger);

        Assert.Contains(ownersArrears, id => id == propertyId);

        // The assertion that matters. This endpoint takes no id, so it is the one place a
        // caller could be handed another landlord's portfolio wholesale.
        Assert.DoesNotContain(strangersArrears, id => id == propertyId);
    }

    [Fact]
    public async Task Arrears_are_refused_without_a_token()
    {
        using var client = _factory.CreateIsolatedClient();

        // The endpoint carries no id, so an unauthenticated caller has nothing to scope to —
        // deny-by-default has to be what stops it rather than a missing lookup.
        var response = await client.GetAsync("/api/RentalProperties/rent-schedule/arrears");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<HttpClient> AuthenticatedClientAsync(string prefix)
    {
        var client = _factory.CreateIsolatedClient();
        var username = $"{prefix}-{Guid.NewGuid():N}";

        var registered = await client.PostAsJsonAsync("/api/auth/register",
            new { username, email = $"{username}@example.test", password = Password });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        var loggedIn = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);

        using var body = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>A property with a tenancy running since six months ago, rent due on the 1st.</summary>
    private static async Task<int> CreateLetPropertyAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/RentalProperties",
            new { propertyName = "Rent schedule test", address = "1 Ledger Way", currencyCode = "EUR" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var propertyId = body.RootElement.GetProperty("id").GetInt32();

        var lease = await client.PostAsJsonAsync($"/api/RentalProperties/{propertyId}/leases",
            new
            {
                tenantName = "T. Tenant",
                startDate = FirstOfThisMonth.AddMonths(-6).ToString("yyyy-MM-dd"),
                monthlyRent = MonthlyRent,
                rentDueDayOfMonth = 1,
            });

        Assert.Equal(HttpStatusCode.Created, lease.StatusCode);

        return propertyId;
    }

    /// <summary>The one month of the schedule under test, fetched fresh.</summary>
    private static async Task<JsonElement> FetchPeriodAsync(HttpClient client, int propertyId, string period)
    {
        var response = await client.GetAsync($"/api/RentalProperties/{propertyId}/rent-schedule");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var schedule = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var month = schedule.RootElement
            .GetProperty("periods")
            .EnumerateArray()
            .SingleOrDefault(p => p.GetProperty("period").GetString() == period);

        Assert.NotEqual(JsonValueKind.Undefined, month.ValueKind);

        // Cloned: the JsonDocument backing it is disposed when this method returns.
        return month.Clone();
    }

    private static async Task<List<int>> ReadArrearsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/RentalProperties/rent-schedule/arrears");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement
            .EnumerateArray()
            .Select(entry => entry.GetProperty("propertyId").GetInt32())
            .ToList();
    }
}
