using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// End-to-end authentication: register, log in, and present the resulting token to the running
/// application over HTTP.
///
/// <para>
/// This is the suite that makes a <c>Microsoft.AspNetCore.Authentication.JwtBearer</c> bump
/// provable rather than hopeful. The failure mode it exists for is IDX14102 — a
/// <c>Microsoft.IdentityModel.Tokens</c> out of step with the
/// <c>Microsoft.IdentityModel.JsonWebTokens</c> that JwtBearer resolves, which makes the handler
/// call a <c>Base64UrlEncoder</c> overload that does not exist and rejects every token the app
/// itself just issued. The build stays green. Every other test in this project stays green,
/// because none of them sends a request. Only a real token going through the real handler
/// notices, and <see cref="A_token_from_login_is_accepted_by_the_running_application"/> is that
/// canary.
/// </para>
///
/// <para>
/// The positive and negative tests are load-bearing as a pair, and neither is worth much alone.
/// A handler that rejects everything passes all the 401 tests; a handler that validates nothing
/// passes the 200 tests. Only both together say that validation runs *and* discriminates.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthenticationTests
{
    private const string ValidPassword = "correct-horse-battery";

    private readonly ApiFactory _factory;

    public AuthenticationTests(ApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------
    // The token round-trip
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_token_from_login_is_accepted_by_the_running_application()
    {
        using var client = _factory.CreateIsolatedClient();
        var username = UniqueUsername("round-trip");

        var token = await RegisterAndLoginAsync(client, username);
        Authenticate(client, token);

        var response = await client.GetAsync("/api/auth/me");

        // Not Assert.True(IsSuccessStatusCode): when this breaks it is IDX14102 rejecting a
        // token the app minted seconds earlier, and the status code is the whole diagnosis.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(username, body.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task A_token_from_login_is_accepted_on_a_data_endpoint()
    {
        using var client = _factory.CreateIsolatedClient();

        Authenticate(client, await RegisterAndLoginAsync(client, UniqueUsername("data")));

        // /api/auth/me reads the principal directly; this goes the whole way through to a
        // DbContext scoped by ICurrentUser, so it also proves the "sub" claim survived the
        // handler's inbound claim mapping intact.
        var response = await client.GetAsync("/api/RentalProperties");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_token_carries_the_identity_of_the_user_who_logged_in()
    {
        using var client = _factory.CreateIsolatedClient();
        var username = UniqueUsername("identity");

        Authenticate(client, await RegisterAndLoginAsync(client, username));
        var created = await client.PostAsJsonAsync("/api/RentalProperties",
            new { propertyName = "Identity check", address = "1 Test Street" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Ownership is stamped from the token, never from the request body — so a property
        // created with this token has to come back to it.
        var listed = await client.GetAsync("/api/RentalProperties");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        using var body = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());

        Assert.Contains(body.RootElement.EnumerateArray(),
            property => property.GetProperty("propertyName").GetString() == "Identity check");
    }

    // ------------------------------------------------------------------
    // Registration and login
    // ------------------------------------------------------------------

    [Fact]
    public async Task Registration_rejects_a_password_below_the_minimum_length()
    {
        using var client = _factory.CreateIsolatedClient();

        var response = await RegisterAsync(client, UniqueUsername("short-password"), "sevench");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registration_rejects_a_username_that_is_already_taken()
    {
        using var client = _factory.CreateIsolatedClient();
        var username = UniqueUsername("duplicate");

        Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, username)).StatusCode);

        // Differs from the first registration in case only: the unique index is on the
        // normalized column, so this has to collide.
        var second = await RegisterAsync(client, username.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_is_case_insensitive_in_the_username()
    {
        using var client = _factory.CreateIsolatedClient();
        var username = UniqueUsername("case");

        await RegisterAsync(client, username);
        var response = await LoginAsync(client, username.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_a_wrong_password()
    {
        using var client = _factory.CreateIsolatedClient();
        var username = UniqueUsername("wrong-password");

        await RegisterAsync(client, username);
        var response = await LoginAsync(client, username, "not-the-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_an_unknown_username()
    {
        using var client = _factory.CreateIsolatedClient();

        var response = await LoginAsync(client, UniqueUsername("never-registered"));

        // Same status and the same body as a wrong password, deliberately: the response must
        // not tell an attacker whether the account exists.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // What the handler must refuse
    // ------------------------------------------------------------------

    [Fact]
    public async Task An_endpoint_with_no_authorize_attribute_is_still_protected()
    {
        using var client = _factory.CreateIsolatedClient();

        // AuthController carries neither [Authorize] nor [AllowAnonymous] on Me(), so this
        // request is refused by the fallback policy in Program.cs and nothing else. It is the
        // one endpoint in the app that demonstrates deny-by-default actually holding.
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_request_with_no_token_is_refused_by_a_data_endpoint()
    {
        using var client = _factory.CreateIsolatedClient();

        var response = await client.GetAsync("/api/RentalProperties");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_token_is_refused()
    {
        using var client = _factory.CreateIsolatedClient();
        Authenticate(client, "not-a-jwt");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_a_different_key_is_refused()
    {
        using var client = _factory.CreateIsolatedClient();
        Authenticate(client, TestTokens.Create(1, "forger",
            signingKey: "a-completely-different-signing-key-of-adequate-length-0123456789"));

        var response = await client.GetAsync("/api/auth/me");

        // ValidateIssuerSigningKey doing its job. If this ever returns 200, the signature is
        // decorative and anyone can mint themselves any account.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_from_a_different_issuer_is_refused()
    {
        using var client = _factory.CreateIsolatedClient();
        Authenticate(client, TestTokens.Create(1, "elsewhere", issuer: "some-other-identity-provider"));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_for_a_different_audience_is_refused()
    {
        using var client = _factory.CreateIsolatedClient();
        Authenticate(client, TestTokens.Create(1, "elsewhere", audience: "some-other-application"));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        using var client = _factory.CreateIsolatedClient();

        // Well past the 30-second ClockSkew configured in Program.cs.
        Authenticate(client, TestTokens.Create(1, "stale",
            issuedAt: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1)));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_valid_token_for_a_user_who_no_longer_exists_is_refused()
    {
        using var client = _factory.CreateIsolatedClient();

        // Correctly signed, correct issuer and audience, unexpired — and pointing at an id no
        // row has. The token is valid; the user is not. The distinction has to be enforced past
        // the handler, or a deleted account keeps its access until its token expires.
        Authenticate(client, TestTokens.Create(int.MaxValue, "ghost"));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Isolation, over HTTP
    // ------------------------------------------------------------------

    [Fact]
    public async Task One_users_token_cannot_read_another_users_property()
    {
        using var alice = _factory.CreateIsolatedClient();
        using var bob = _factory.CreateIsolatedClient();

        Authenticate(alice, await RegisterAndLoginAsync(alice, UniqueUsername("alice")));
        Authenticate(bob, await RegisterAndLoginAsync(bob, UniqueUsername("bob")));

        var created = await alice.PostAsJsonAsync("/api/RentalProperties",
            new { propertyName = "Alice's flat", address = "2 Private Road" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var propertyId = createdBody.RootElement.GetProperty("id").GetInt32();

        // TenantIsolationTests proves the query filter at the data layer. This proves the
        // filter is what an HTTP caller actually meets — 404, not 403, so the response does not
        // confirm the row exists either.
        var direct = await bob.GetAsync($"/api/RentalProperties/{propertyId}");
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);

        var listed = await bob.GetAsync("/api/RentalProperties");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        using var listedBody = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());

        Assert.DoesNotContain(listedBody.RootElement.EnumerateArray(),
            property => property.GetProperty("id").GetInt32() == propertyId);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string UniqueUsername(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string username, string password = ValidPassword) =>
        client.PostAsJsonAsync("/api/auth/register",
            new { username, email = $"{username}@example.test", password });

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string username, string password = ValidPassword) =>
        client.PostAsJsonAsync("/api/auth/login", new { username, password });

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string username)
    {
        var registered = await RegisterAsync(client, username);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        var loggedIn = await LoginAsync(client, username);
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);

        using var body = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("token").GetString();

        Assert.NotNull(token);
        Assert.Equal(3, token.Split('.').Length);

        return token;
    }
}
