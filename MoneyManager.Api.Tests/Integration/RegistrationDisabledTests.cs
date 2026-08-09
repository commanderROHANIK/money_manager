using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MoneyManager.Api.Models;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// A deployment closes registration because its URL is public — preview environments have no
/// platform gate in front of them at all — and accounts are seeded there instead.
///
/// <para>
/// The rest of the integration suite runs with registration open, because most of
/// <see cref="AuthenticationTests"/> registers a user as its first step. So the closed behaviour
/// needs a host of its own. It is built with <c>WithWebHostBuilder</c> and a service override
/// rather than by changing configuration: <c>ApiFactory</c> configures the app through
/// process-wide environment variables, and a second factory racing to set those would leave the
/// two hosts pointed at each other's database. A later <c>Configure&lt;AuthOptions&gt;</c> wins
/// over the one bound from configuration, which needs nothing from the environment.
/// </para>
///
/// <para>
/// The second test is the one that would catch the damaging mistake. Closing registration must
/// not close the door on the accounts that already exist — a deployment where nobody can log in
/// is not a secure deployment, it is a broken one.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RegistrationDisabledTests
{
    private const string Password = "correct-horse-battery";

    private readonly ApiFactory _factory;

    public RegistrationDisabledTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Registration_is_not_found_when_it_is_disabled()
    {
        using var closed = CreateHostWithRegistrationDisabled();
        using var client = ClientFor(closed, "203.0.113.60");

        var username = $"turned-away-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { username, email = $"{username}@example.test", password = Password });

        // 404 rather than 403: a closed deployment should not confirm that the endpoint is
        // there. A 403 would say "this exists, you may not use it", which is a fact about the
        // deployment that an anonymous caller has no need of.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // And it really did not create the account — the endpoint refuses before it validates
        // or writes anything. Logging in with those credentials against the open host, which
        // shares the same database, finds nothing.
        using var openClient = _factory.CreateIsolatedClient();

        var login = await openClient.PostAsJsonAsync("/api/auth/login", new { username, password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_still_works_when_registration_is_disabled()
    {
        var username = $"already-here-{Guid.NewGuid():N}";

        // Registered through the open host, standing in for an account that was seeded or that
        // predates registration being closed.
        using var openClient = _factory.CreateIsolatedClient();

        var registered = await openClient.PostAsJsonAsync("/api/auth/register",
            new { username, email = $"{username}@example.test", password = Password });

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        using var closed = CreateHostWithRegistrationDisabled();
        using var client = ClientFor(closed, "203.0.113.61");

        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    /// <summary>
    /// Overrides the option after <c>Program.cs</c> has bound it from configuration. Options are
    /// applied in registration order, so this one wins.
    /// </summary>
    private WebApplicationFactory<Program> CreateHostWithRegistrationDisabled() =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Configure<AuthOptions>(options => options.AllowRegistration = false)));

    /// <summary>
    /// The auth rate limiter partitions on the client address, and these hosts share a process
    /// with the rest of the suite — so each caller here needs an address nothing else uses.
    /// </summary>
    private static HttpClient ClientFor(WebApplicationFactory<Program> factory, string address)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiFactory.ClientAddressHeader, address);

        return client;
    }
}
