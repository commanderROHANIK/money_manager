using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The auth endpoints are rate limited because a login form is the obvious place to try a list
/// of stolen passwords. What makes the limiter worth testing is not that it exists but *how* it
/// partitions: <c>AddFixedWindowLimiter</c>'s overload without a partition key hands one bucket
/// to the whole application, so the first caller to hit the limit locks everybody else out of
/// login. That version also passes a test that only checks "too many requests get a 429", which
/// is why <see cref="The_limit_is_partitioned_by_client_address"/> is the one that matters here.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthRateLimitTests
{
    /// <summary>Program.cs permits 10 per minute per address; a run of this length must trip it.</summary>
    private const int AttemptsPastTheLimit = 20;

    private readonly ApiFactory _factory;

    public AuthRateLimitTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Repeated_login_attempts_from_one_address_are_rate_limited()
    {
        // Addresses come from the TEST-NET-3 documentation range so they cannot be confused
        // with the 10.0.x.x pool CreateIsolatedClient hands out.
        using var client = _factory.CreateClientForAddress("203.0.113.10");

        var statuses = await AttemptLoginsAsync(client, AttemptsPastTheLimit);

        // The first attempt must be answered on its merits — a limiter that rejects from the
        // very first request is not a limiter, it is an outage.
        Assert.Equal(HttpStatusCode.Unauthorized, statuses[0]);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task The_limit_is_partitioned_by_client_address()
    {
        using var noisy = _factory.CreateClientForAddress("203.0.113.20");
        using var innocent = _factory.CreateClientForAddress("203.0.113.21");

        var noisyStatuses = await AttemptLoginsAsync(noisy, AttemptsPastTheLimit);
        Assert.Contains(HttpStatusCode.TooManyRequests, noisyStatuses);

        // A second caller arriving mid-attack still gets to log in — or in this case, still
        // gets a straight answer about their credentials rather than somebody else's 429.
        var innocentStatus = await AttemptLoginAsync(innocent);

        Assert.Equal(HttpStatusCode.Unauthorized, innocentStatus);
    }

    private static async Task<List<HttpStatusCode>> AttemptLoginsAsync(HttpClient client, int count)
    {
        var statuses = new List<HttpStatusCode>(count);

        for (var attempt = 0; attempt < count; attempt++)
        {
            statuses.Add(await AttemptLoginAsync(client));

            if (statuses[^1] == HttpStatusCode.TooManyRequests)
                break;
        }

        return statuses;
    }

    private static async Task<HttpStatusCode> AttemptLoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "nobody-by-this-name", password = "nor-this-password" });

        return response.StatusCode;
    }
}
