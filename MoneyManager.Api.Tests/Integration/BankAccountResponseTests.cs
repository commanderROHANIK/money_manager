using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The bank account endpoints answer with a DTO rather than the entity.
///
/// <para>
/// This is guarding a change that has not happened yet, which is the only useful moment to guard
/// it. Connecting a bank means adding a consent token, an external account id, or a session
/// reference to <c>BankAccount</c> — and while the controller returns that class verbatim, the
/// person adding the column publishes it to every client without ever opening the controller.
/// Nobody adding a property thinks of themselves as changing an API response.
/// </para>
///
/// <para>
/// So the assertions are written as a whitelist: the response carries exactly these fields and
/// nothing else. A looser test — "it still has a balance" — would pass just as happily on the day
/// the token leaks.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class BankAccountResponseTests
{
    private const string Password = "correct-horse-battery";

    private static readonly string[] Expected =
    [
        "id", "accountName", "balance", "bankName", "accountNumber", "accountType", "currencyCode",
    ];

    private readonly ApiFactory _factory;

    public BankAccountResponseTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_account_comes_back_with_exactly_the_published_fields()
    {
        using var client = await AuthenticatedClientAsync();

        var created = await CreateAccountAsync(client);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var fields = body.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(Expected.Order(), fields.Order());
    }

    [Fact]
    public async Task The_owner_id_is_not_published()
    {
        using var client = await AuthenticatedClientAsync();
        await CreateAccountAsync(client);

        var response = await client.GetAsync("/api/BankAccounts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Ownership is stamped from the token and pinned on update, never taken from a payload.
        // Stating it in a response invites a client to start believing it, and there is nothing
        // a caller can do with the id of the account they are already authenticated as.
        foreach (var account in body.RootElement.EnumerateArray())
        {
            Assert.False(account.TryGetProperty("userId", out _));
        }
    }

    [Fact]
    public async Task Fetching_one_account_answers_in_the_same_shape_as_the_list()
    {
        using var client = await AuthenticatedClientAsync();

        var created = await CreateAccountAsync(client);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdBody.RootElement.GetProperty("id").GetInt32();

        var single = await client.GetAsync($"/api/BankAccounts/{id}");
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);

        using var body = JsonDocument.Parse(await single.Content.ReadAsStringAsync());
        var fields = body.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        // All three endpoints go through the same projection, so a field added to one is added to
        // all three — this asserts they have not drifted apart.
        Assert.Equal(Expected.Order(), fields.Order());
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Task<HttpResponseMessage> CreateAccountAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/BankAccounts", new
        {
            accountName = "Current account",
            balance = 1_200.50m,
            bankName = "Test Bank",
            accountNumber = "NL00TEST0123456789",
            accountType = "Current",
            currencyCode = "EUR",
        });

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _factory.CreateIsolatedClient();
        var username = $"bank-dto-{Guid.NewGuid():N}";

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
