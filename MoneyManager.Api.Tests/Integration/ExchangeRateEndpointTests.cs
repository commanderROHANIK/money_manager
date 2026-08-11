using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The rate endpoints, hosted for real, with fetching switched off — which is how
/// <see cref="ApiFactory"/> configures every host in this suite, and therefore what a deployment
/// that has turned <c>Features:AutomaticExchangeRates</c> off actually gets.
///
/// <para>
/// That is the case worth hosting rather than unit-testing. Listing rates now runs a refresh
/// first, so the endpoint that used to be a plain query has acquired a provider, a cache and an
/// outbound call behind it. The promise being checked here is that none of that is visible: with
/// the no-op provider registered, <c>GET</c> answers exactly as it did before rates were ever
/// fetched, and the suite makes no network request in the process.
/// </para>
///
/// <para>
/// The happy path — a real provider answering with real rates — is covered against a stub in
/// <see cref="ExchangeRateRefreshServiceTests"/>. A test that reached
/// <c>api.frankfurter.dev</c> would fail on somebody else's outage and pass for reasons it could
/// not describe.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ExchangeRateEndpointTests
{
    private const string Password = "correct-horse-battery";

    private readonly ApiFactory _factory;

    public ExchangeRateEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Listing_rates_answers_with_the_rows_on_record_and_their_source()
    {
        using var client = await AuthenticatedClientAsync();

        var saved = await client.PutAsJsonAsync("/api/ExchangeRates/EUR/HUF",
            new { rate = 400m, asOf = "2026-07-01" });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var response = await client.GetAsync("/api/ExchangeRates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = Assert.Single(body.RootElement.EnumerateArray().ToList());

        Assert.Equal(400m, row.GetProperty("rate").GetDecimal());

        // 0 is Manual. Published because the UI's whole disclosure is built from it — a response
        // that carried the rate but not its provenance would leave the client to guess, and the
        // guess it would make is "the user entered this".
        Assert.Equal(0, row.GetProperty("source").GetInt32());
    }

    [Fact]
    public async Task A_refresh_leaves_an_entered_rate_exactly_as_entered()
    {
        using var client = await AuthenticatedClientAsync();

        await client.PutAsJsonAsync("/api/ExchangeRates/EUR/HUF", new { rate = 400m, asOf = "2026-07-01" });

        var refreshed = await client.PostAsync("/api/ExchangeRates/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        using var body = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var row = Assert.Single(body.RootElement.EnumerateArray().ToList());

        // With fetching off there is nothing to overwrite it with; with fetching on the service
        // skips the pair. Both roads lead here, which is the point — pressing refresh can never
        // cost the user a rate they asserted.
        Assert.Equal(400m, row.GetProperty("rate").GetDecimal());
        Assert.Equal(0, row.GetProperty("source").GetInt32());
    }

    [Fact]
    public async Task The_refresh_endpoint_requires_a_token()
    {
        using var client = _factory.CreateIsolatedClient();

        // It spends an outbound request and writes rows. Reachable anonymously, it would be a way
        // to make the deployment call somebody else's API on demand.
        var response = await client.PostAsync("/api/ExchangeRates/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _factory.CreateIsolatedClient();
        var username = $"fx-{Guid.NewGuid():N}";

        var registered = await client.PostAsJsonAsync("/api/auth/register",
            new { username, email = $"{username}@example.test", password = Password });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        var loggedIn = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);

        using var body = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("token").GetString());

        return client;
    }
}
