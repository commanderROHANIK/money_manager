using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The auth rate limiter partitions on <c>Connection.RemoteIpAddress</c>. Deployed behind an
/// edge proxy that address is the proxy's for every request, so the partition key becomes a
/// constant and the limiter degrades into the single shared bucket its own comment in
/// <c>Program.cs</c> rules out — ten requests a minute for the entire deployment, across login
/// *and* the <c>/api/auth/me</c> the SPA calls on every page load.
///
/// <para>
/// Nothing about that failure is visible. The limiter still returns 429s, still looks
/// configured, still passes <see cref="AuthRateLimitTests"/> — because that suite sets
/// <c>Connection.RemoteIpAddress</c> directly through <c>ApiFactory</c>'s startup filter and
/// never sends an <c>X-Forwarded-For</c> header at all. <c>UseForwardedHeaders</c> could be
/// deleted, or registered without its options and silently do nothing, and every existing test
/// would stay green. These two are the only ones that would notice.
/// </para>
///
/// <para>
/// They are a pair and neither is worth much alone.
/// <see cref="Callers_sharing_a_forwarded_address_share_a_bucket"/> is what proves the header is
/// read at all: without the middleware those two callers have different connection addresses,
/// land in different buckets, and nothing throttles.
/// <see cref="The_partition_follows_the_forwarded_address_rather_than_the_connection"/> is what
/// proves it has actually displaced the connection address: without the middleware those two
/// share one connection address, land in one bucket, and the innocent caller is throttled for
/// the noisy one's traffic. Together they say the partition moved, and moved to the right thing.
/// </para>
///
/// <para>
/// What they deliberately do not prove is that the rightmost <c>X-Forwarded-For</c> entry is the
/// real client on any particular host. <c>KnownProxies</c> is cleared, so the middleware performs
/// no per-hop verification; <c>ForwardLimit = 1</c> means it trusts whatever the last appending
/// hop wrote. Whether that hop is the edge — rather than an internal router whose address is the
/// same for everybody, which would put us straight back in one bucket — is a property of the
/// deployment and can only be settled by watching the resolved address vary per client on a
/// running instance.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ForwardedHeadersTests
{
    /// <summary>Program.cs permits 10 per minute per partition; this run must trip it.</summary>
    private const int AttemptsPastTheLimit = 20;

    private const string ForwardedForHeader = "X-Forwarded-For";

    private readonly ApiFactory _factory;

    public ForwardedHeadersTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_partition_follows_the_forwarded_address_rather_than_the_connection()
    {
        // One proxy in front of two different people — which is what the deployed topology
        // looks like from Kestrel's side. Same connection address, different real clients.
        const string proxy = "203.0.113.40";

        using var noisy = ClientBehindProxy(proxy, forwardedFor: "198.51.100.10");
        using var innocent = ClientBehindProxy(proxy, forwardedFor: "198.51.100.11");

        var noisyStatuses = await AttemptLoginsAsync(noisy, AttemptsPastTheLimit);
        Assert.Contains(HttpStatusCode.TooManyRequests, noisyStatuses);

        // Unauthorized, not TooManyRequests: this caller gets a straight answer about their own
        // credentials. Without UseForwardedHeaders they share the proxy's address, share its
        // bucket, and are locked out of login by somebody else's password guessing.
        var innocentStatus = await AttemptLoginAsync(innocent);

        Assert.Equal(HttpStatusCode.Unauthorized, innocentStatus);
    }

    [Fact]
    public async Task Callers_sharing_a_forwarded_address_share_a_bucket()
    {
        // Deliberately different connection addresses, so the only thing these two have in
        // common is the forwarded one. If the header were being ignored they would be in
        // separate buckets and neither would ever see a 429.
        const string client = "198.51.100.20";

        using var first = ClientBehindProxy("203.0.113.50", forwardedFor: client);
        using var second = ClientBehindProxy("203.0.113.51", forwardedFor: client);

        var statuses = await AttemptLoginsAsync(first, AttemptsPastTheLimit);

        Assert.Equal(HttpStatusCode.Unauthorized, statuses[0]);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);

        // One attacker rotating through connections must not get a fresh budget for each. This
        // is the assertion that fails if UseForwardedHeaders is removed, or registered without
        // configuring ForwardedHeaders in its options, which makes it a no-op.
        var secondStatus = await AttemptLoginAsync(second);

        Assert.Equal(HttpStatusCode.TooManyRequests, secondStatus);
    }

    /// <summary>
    /// A client whose requests look like they crossed a proxy: <paramref name="connectionAddress"/>
    /// is what Kestrel sees, <paramref name="forwardedFor"/> is what the proxy appended.
    /// </summary>
    private HttpClient ClientBehindProxy(string connectionAddress, string forwardedFor)
    {
        var client = _factory.CreateClientForAddress(connectionAddress);
        client.DefaultRequestHeaders.Add(ForwardedForHeader, forwardedFor);

        return client;
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
            new { username = "nobody-behind-this-proxy", password = "nor-this-password" });

        return response.StatusCode;
    }
}
