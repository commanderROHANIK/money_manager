using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The onboarding progress endpoint, over HTTP.
///
/// <para>
/// The claim under test is not "these booleans are correct" so much as "this is derived". A
/// stored <c>IsOnboarded</c> column would satisfy the first two tests here and fail the third,
/// which is exactly the defect the design set out to avoid: a landlord who deletes their only
/// property is back to having no properties, and a flag written once would go on insisting they
/// had finished.
/// </para>
///
/// <para>
/// The isolation test is the other half. This controller never mentions a user — it reads seven
/// sets and asks each whether anything is there — so it is only correct because the global query
/// filter scopes them. Two accounts against the same database is the cheapest way to say that out
/// loud, and it fails loudly if someone ever swaps one of those sets for an unfiltered query.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OnboardingEndpointTests
{
    private const string Password = "correct-horse-battery";

    private readonly ApiFactory _factory;

    public OnboardingEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_new_account_has_done_nothing()
    {
        using var client = await AuthenticatedClientAsync("onboarding-new");

        var progress = await FetchAsync(client);

        // Every step outstanding. This is the state nobody had ever seen before this feature:
        // every account so far was created by hand and seeded.
        Assert.False(progress.GetProperty("hasProperty").GetBoolean());
        Assert.False(progress.GetProperty("hasLease").GetBoolean());
        Assert.False(progress.GetProperty("hasTransaction").GetBoolean());
        Assert.False(progress.GetProperty("hasValuation").GetBoolean());
        Assert.False(progress.GetProperty("hasBankAccount").GetBoolean());
        Assert.False(progress.GetProperty("hasLoan").GetBoolean());
        Assert.False(progress.GetProperty("hasStock").GetBoolean());
    }

    [Fact]
    public async Task Adding_a_property_and_a_tenancy_ticks_their_steps()
    {
        using var client = await AuthenticatedClientAsync("onboarding-progress");

        var propertyId = await CreatePropertyAsync(client);

        var afterProperty = await FetchAsync(client);
        Assert.True(afterProperty.GetProperty("hasProperty").GetBoolean());
        Assert.False(afterProperty.GetProperty("hasLease").GetBoolean());

        var lease = await client.PostAsJsonAsync($"/api/RentalProperties/{propertyId}/leases",
            new
            {
                tenantName = "T. Tenant",
                startDate = DateTime.UtcNow.Date.AddMonths(-1).ToString("yyyy-MM-dd"),
                monthlyRent = 1_200m,
                rentDueDayOfMonth = 1,
            });

        Assert.Equal(HttpStatusCode.Created, lease.StatusCode);

        var afterLease = await FetchAsync(client);
        Assert.True(afterLease.GetProperty("hasProperty").GetBoolean());
        Assert.True(afterLease.GetProperty("hasLease").GetBoolean());

        // Nothing was written to say so. Both answers came from the rows themselves.
        Assert.False(afterLease.GetProperty("hasValuation").GetBoolean());
    }

    [Fact]
    public async Task Deleting_the_only_property_un_ticks_its_step()
    {
        using var client = await AuthenticatedClientAsync("onboarding-deleted");

        var propertyId = await CreatePropertyAsync(client);

        Assert.True((await FetchAsync(client)).GetProperty("hasProperty").GetBoolean());

        var deleted = await client.DeleteAsync($"/api/RentalProperties/{propertyId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // The test a stored flag fails. Progress is a conclusion about what exists, so removing
        // the thing has to remove the conclusion.
        Assert.False((await FetchAsync(client)).GetProperty("hasProperty").GetBoolean());
    }

    [Fact]
    public async Task One_landlord_s_portfolio_does_not_tick_another_s_steps()
    {
        using var established = await AuthenticatedClientAsync("onboarding-established");
        await CreatePropertyAsync(established);

        Assert.True((await FetchAsync(established)).GetProperty("hasProperty").GetBoolean());

        // A different account, against the same database. The endpoint names no user anywhere;
        // if this ever goes true, the query filter has been bypassed.
        using var newcomer = await AuthenticatedClientAsync("onboarding-newcomer");

        Assert.False((await FetchAsync(newcomer)).GetProperty("hasProperty").GetBoolean());
    }

    [Fact]
    public async Task Progress_requires_authentication()
    {
        using var client = _factory.CreateIsolatedClient();

        var response = await client.GetAsync("/api/Onboarding");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task<JsonElement> FetchAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/Onboarding");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Cloned: the JsonDocument backing it is disposed when this method returns.
        return body.RootElement.Clone();
    }

    private static async Task<int> CreatePropertyAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/RentalProperties",
            new { propertyName = "Onboarding test", address = "1 Ledger Way", currencyCode = "EUR" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());

        return body.RootElement.GetProperty("id").GetInt32();
    }

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
}
