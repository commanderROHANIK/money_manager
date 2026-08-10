using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MoneyManager.Api.Models;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The MVP presents rental properties and nothing else. Bank accounts and stocks are switched off
/// rather than deleted, so the question these tests answer is whether "switched off" means the
/// section is gone or merely unlinked.
///
/// <para>
/// Unlinked would be the easy mistake and an invisible one: the navigation looks right, the
/// screenshots look right, and <c>/api/BankAccounts</c> keeps answering to anyone who types it.
/// Every test here is about the endpoints rather than the UI, because that is the half a
/// front-end change cannot be trusted to cover.
/// </para>
///
/// <para>
/// Hosts are built with <c>WithWebHostBuilder</c> and a service override rather than by setting
/// configuration, for the reason <see cref="RegistrationDisabledTests"/> gives: <c>ApiFactory</c>
/// configures the app through process-wide environment variables, and a second factory racing to
/// set those would leave the two hosts pointed at each other's database.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class FeatureGateTests
{
    private const string Password = "correct-horse-battery";

    /// <summary>The four sections that can be switched off, by the route each one is served at.</summary>
    public static TheoryData<string> GatedRoutes => new()
    {
        "/api/BankAccounts",
        "/api/Stocks",
        "/api/Loans",
        "/api/UpcomingEvents",
    };

    private readonly ApiFactory _factory;

    public FeatureGateTests(ApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------
    // Off means gone
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(GatedRoutes))]
    public async Task A_disabled_sections_endpoints_are_not_found(string route)
    {
        using var host = HostWith(AllOff);
        using var client = await AuthenticatedClientAsync(host, "203.0.113.70");

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(GatedRoutes))]
    public async Task A_disabled_sections_endpoints_are_reachable_again_when_it_is_switched_on(string route)
    {
        using var host = HostWith(AllOn);
        using var client = await AuthenticatedClientAsync(host, "203.0.113.71");

        var response = await client.GetAsync(route);

        // The flag decides whether the section exists, and nothing else about it. Flags-on has to
        // behave exactly as the app did before the gate existed, or turning one back on would
        // mean debugging a second code path rather than restoring a feature.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_disabled_section_is_indistinguishable_from_one_that_was_never_built()
    {
        using var host = HostWith(AllOff);
        using var client = await AuthenticatedClientAsync(host, "203.0.113.72");

        var disabled = await client.GetAsync("/api/BankAccounts");
        var neverExisted = await client.GetAsync("/api/no-such-controller");

        // This is the property worth having, and the reason the gate answers with a bare 404
        // rather than the ProblemDetails envelope the rest of the API uses. A body saying "this
        // feature is disabled" would tell a customer the product has a section they were not
        // shown — which is exactly what switching it off was meant to avoid. Same status, same
        // empty body, no inference available.
        Assert.Equal(neverExisted.StatusCode, disabled.StatusCode);
        Assert.Equal(
            await neverExisted.Content.ReadAsStringAsync(),
            await disabled.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_write_to_a_disabled_section_is_refused_before_the_body_is_read()
    {
        using var host = HostWith(AllOff);
        using var client = await AuthenticatedClientAsync(host, "203.0.113.73");

        // Invalid on purpose: an empty account name would be a 400 from the validation attributes
        // on the request record. Getting 404 instead proves the gate runs ahead of model binding,
        // so a disabled section does not answer questions about what it would have accepted.
        var response = await client.PostAsJsonAsync("/api/BankAccounts",
            new { accountName = "", balance = 10m, bankName = "B", accountNumber = "1", accountType = "Current" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_tell_which_sections_are_enabled()
    {
        using var host = HostWith(options =>
        {
            options.Banking = false;
            options.Loans = true;
        });

        using var client = ClientFor(host, "203.0.113.74");

        // The gate is a resource filter, so it runs inside the endpoint — after the authorization
        // middleware has already refused an anonymous request. Both answers stay 401, and the
        // enabled sections are not something you can enumerate without an account.
        var disabled = await client.GetAsync("/api/BankAccounts");
        var enabled = await client.GetAsync("/api/Loans");

        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, enabled.StatusCode);
    }

    // ------------------------------------------------------------------
    // What the flags must never reach
    // ------------------------------------------------------------------

    [Fact]
    public async Task The_rental_endpoints_are_unaffected_by_every_flag_being_off()
    {
        using var host = HostWith(AllOff);
        using var client = await AuthenticatedClientAsync(host, "203.0.113.75");

        // The MVP is these. A gate applied one controller too widely would ship a deployment
        // whose only remaining feature is the one it exists to sell.
        foreach (var route in new[] { "/api/RentalProperties", "/api/Settings" })
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // ------------------------------------------------------------------
    // What the SPA reads
    // ------------------------------------------------------------------

    [Fact]
    public async Task The_features_endpoint_reports_the_resolved_flags()
    {
        using var host = HostWith(options =>
        {
            options.Banking = false;
            options.Stocks = false;
            options.Loans = true;
            options.Events = true;
        });

        using var client = await AuthenticatedClientAsync(host, "203.0.113.76");

        var response = await client.GetAsync("/api/Features");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // The navigation is built from this, so it has to agree with what the endpoints above do.
        // A UI reading its own build-time copy of the flags is the drift this endpoint prevents.
        Assert.False(body.RootElement.GetProperty("banking").GetBoolean());
        Assert.False(body.RootElement.GetProperty("stocks").GetBoolean());
        Assert.True(body.RootElement.GetProperty("loans").GetBoolean());
        Assert.True(body.RootElement.GetProperty("events").GetBoolean());
    }

    [Fact]
    public async Task The_features_endpoint_requires_a_token()
    {
        using var client = _factory.CreateIsolatedClient();

        // Deliberately not [AllowAnonymous]. The login screen has no navigation to build, so the
        // convenience would buy nothing and would hand an anonymous caller the list of sections.
        var response = await client.GetAsync("/api/Features");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void The_shipped_defaults_are_the_mvp_shape()
    {
        // Not a restatement of FeatureOptions: this asserts that a deployment which configures
        // nothing gets rental-only, which is the behaviour the MVP depends on. A default flipped
        // to true while nobody was looking would ship the half-built sections to the customer,
        // and every other test here passes because it sets the flags explicitly.
        var defaults = new FeatureOptions();

        Assert.False(defaults.Banking);
        Assert.False(defaults.Stocks);
        Assert.True(defaults.Loans);
        Assert.True(defaults.Events);
    }

    // ------------------------------------------------------------------

    private static void AllOff(FeatureOptions options)
    {
        options.Banking = false;
        options.Stocks = false;
        options.Loans = false;
        options.Events = false;
    }

    private static void AllOn(FeatureOptions options)
    {
        options.Banking = true;
        options.Stocks = true;
        options.Loans = true;
        options.Events = true;
    }

    /// <summary>
    /// Overrides the options after <c>Program.cs</c> has bound them from configuration. Options
    /// are applied in registration order, so this one wins.
    /// </summary>
    private WebApplicationFactory<Program> HostWith(Action<FeatureOptions> configure) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.Configure(configure)));

    /// <summary>
    /// Registers through the shared open host and logs in against <paramref name="host"/>, which
    /// sees the same database. The auth rate limiter partitions on the client address and these
    /// hosts share a process with the rest of the suite, so each caller needs its own.
    /// </summary>
    private async Task<HttpClient> AuthenticatedClientAsync(WebApplicationFactory<Program> host, string address)
    {
        var username = $"features-{Guid.NewGuid():N}";

        using var registrar = _factory.CreateIsolatedClient();

        var registered = await registrar.PostAsJsonAsync("/api/auth/register",
            new { username, email = $"{username}@example.test", password = Password });

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        var client = ClientFor(host, address);

        var loggedIn = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);

        using var body = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("token").GetString());

        return client;
    }

    private static HttpClient ClientFor(WebApplicationFactory<Program> factory, string address)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiFactory.ClientAddressHeader, address);

        return client;
    }
}
