using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// Hosts the real application in-process — the real <c>Program.cs</c> pipeline, the real
/// JwtBearer handler, the real authorization fallback policy, the real rate limiter — against a
/// throwaway SQLite file.
///
/// <para>
/// Nothing is stubbed out on the authentication path, because the defect this suite exists to
/// catch only appears when the genuine article runs. A <c>Microsoft.IdentityModel.Tokens</c>
/// older than the <c>Microsoft.IdentityModel.JsonWebTokens</c> that JwtBearer resolves makes the
/// handler call a <c>Base64UrlEncoder</c> overload that does not exist, and every token fails
/// validation with IDX14102 — through a completely green build, and through every other test in
/// this project, none of which sends an HTTP request. Substituting a test authentication handler
/// here would restore exactly that blind spot.
/// </para>
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "MoneyManager.Api.Tests";
    public const string Audience = "MoneyManager.Client.Tests";

    /// <summary>Comfortably over <c>JwtSettings.MinimumSecretKeyBytes</c>, which startup enforces.</summary>
    public const string SigningKey = "integration-test-signing-key-well-over-32-bytes-long-0123456789";

    /// <summary>
    /// Request header the startup filter below turns into <c>Connection.RemoteIpAddress</c>.
    /// TestServer leaves that null, and the auth rate limiter partitions on it — so without
    /// this every test in the process would share one 10-request-per-minute bucket and the
    /// suite would start failing on its own size rather than on a defect.
    /// </summary>
    public const string ClientAddressHeader = "X-Test-Client-Address";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"moneymanager-integration-{Guid.NewGuid():N}.db");

    private int _addressCounter;

    public ApiFactory()
    {
        // Program.cs reads JwtSettings and the connection string off builder.Configuration
        // *before* builder.Build(), so ConfigureAppConfiguration and UseSetting both arrive too
        // late to change them — those are applied while the host is being built. Environment
        // variables are picked up by CreateBuilder's default provider set, which is early
        // enough, and it is how the app documents supplying the signing key anyway (.env.example).
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", SigningKey);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", Issuer);
        Environment.SetEnvironmentVariable("JwtSettings__Audience", Audience);
        Environment.SetEnvironmentVariable("JwtSettings__ExpiryHours", "12");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", $"Data Source={_databasePath}");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter, ClientAddressStartupFilter>());

    /// <summary>
    /// A client whose requests carry a client address no other client has used, so one test can
    /// never spend another test's share of the auth rate limiter's per-address budget.
    /// </summary>
    public HttpClient CreateIsolatedClient()
    {
        var ordinal = Interlocked.Increment(ref _addressCounter);
        return CreateClientForAddress($"10.0.{ordinal / 256 % 256}.{ordinal % 256}");
    }

    /// <summary>For the rate-limit tests, which need two requests to share an address on purpose.</summary>
    public HttpClient CreateClientForAddress(string address)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ClientAddressHeader, address);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        // -wal and -shm are SQLite's sidecar files; leaving them behind would slowly fill the
        // temp directory of anyone running the suite repeatedly.
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            try
            {
                File.Delete(_databasePath + suffix);
            }
            catch (IOException)
            {
                // A file still held open is not worth failing an otherwise green suite over.
            }
        }
    }

    /// <summary>
    /// Runs ahead of everything configured in <c>Program.cs</c> — including UseRateLimiter —
    /// which is what lets it set the address the limiter partitions on.
    /// </summary>
    private sealed class ClientAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (HttpContext context, Func<Task> nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue(ClientAddressHeader, out var header)
                        && IPAddress.TryParse(header.ToString(), out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await nextMiddleware();
                });

                next(app);
            };
    }
}

/// <summary>
/// One factory for every integration class: the app is hosted once, and xUnit runs the classes
/// sharing it sequentially. Both matter — <see cref="ApiFactory"/> configures the app through
/// process-wide environment variables, and two factories racing to set them would each end up
/// running against the other's database file.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "integration";
}
