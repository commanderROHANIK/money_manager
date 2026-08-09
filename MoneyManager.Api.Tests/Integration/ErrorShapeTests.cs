using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// One error shape, across every controller that writes.
///
/// <para>
/// Before this, the API answered a rejected write in three different ways depending on which
/// controller you happened to hit — <c>{ message }</c> from some, a bare string from others, and
/// from most of them nothing at all, because the request was simply accepted. A form had no field
/// to render against because the response contained none.
/// </para>
///
/// <para>
/// The value of these tests is in the *sameness*. Any single one of them could be satisfied by a
/// controller that got lucky; together they say a caller can parse one envelope and be right
/// everywhere, which is the property the UI's error extractor is built on. That is why
/// <see cref="Every_write_endpoint_rejects_invalid_input_in_the_same_shape"/> walks four
/// controllers rather than testing one thoroughly.
/// </para>
///
/// <para>
/// The 409 case is deliberately separate. A duplicate rent payment is not a field error — nothing
/// about the request is malformed, the state just does not permit it — so it stays a 409 with a
/// <c>detail</c> and no <c>errors</c> map. Flattening it into a 400 would have been the easy way
/// to make "one shape" true, and it would have lost the distinction the UI needs to decide
/// between an inline message and a banner.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ErrorShapeTests
{
    private const string Password = "correct-horse-battery";
    private const string ProblemJson = "application/problem+json";

    private readonly ApiFactory _factory;

    public ErrorShapeTests(ApiFactory factory) => _factory = factory;

    public static TheoryData<string, object> InvalidWrites => new()
    {
        { "/api/BankAccounts", new { accountName = "", balance = -5m, bankName = "B", accountNumber = "1", accountType = "Current" } },
        { "/api/RentalProperties", new { propertyName = "", address = "" } },
        { "/api/Stocks", new { ticker = "", sharesOwned = -1, purchasePrice = -1m, currentPrice = 1m, purchaseDate = "2026-01-01" } },
        { "/api/Loans", new { loanName = "", loanAmount = -1m, remainingBalance = 0m, interestRate = 1m, dueDate = "2026-01-01", isPaidOff = false } },
    };

    [Theory]
    [MemberData(nameof(InvalidWrites))]
    public async Task Every_write_endpoint_rejects_invalid_input_in_the_same_shape(string url, object payload)
    {
        using var client = await AuthenticatedClientAsync("shape");

        var response = await client.PostAsJsonAsync(url, payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // RFC 7807: status and title always, errors for a validation failure. The UI's extractor
        // reads exactly these, so a controller answering a different shape breaks it silently.
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.True(body.RootElement.TryGetProperty("title", out _));
        Assert.True(body.RootElement.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateObject());
    }

    [Fact]
    public async Task A_request_breaking_three_rules_names_all_three_fields()
    {
        using var client = await AuthenticatedClientAsync("three-rules");

        var response = await client.PostAsJsonAsync("/api/BankAccounts", new
        {
            accountName = "",
            balance = -100m,
            bankName = "Test Bank",
            accountNumber = "123",
            accountType = "Current",
            currencyCode = "XYZ123",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorFieldsAsync(response);

        // All three at once, rather than making the user fix them one submit at a time.
        Assert.Contains("AccountName", errors);
        Assert.Contains("Balance", errors);
        Assert.Contains("CurrencyCode", errors);
    }

    [Fact]
    public async Task A_property_sold_before_it_was_purchased_names_the_date()
    {
        using var client = await AuthenticatedClientAsync("sold-early");

        var response = await client.PostAsJsonAsync("/api/RentalProperties", new
        {
            propertyName = "Backwards",
            address = "1 Reverse Street",
            purchaseDate = "2026-06-01",
            saleDate = "2026-01-01",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // A cross-field rule still has to name a field, or the message has nowhere to render.
        Assert.Contains("SaleDate", await ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task A_loan_owing_more_than_was_borrowed_names_the_balance()
    {
        using var client = await AuthenticatedClientAsync("overdrawn-loan");

        var response = await client.PostAsJsonAsync("/api/Loans", new
        {
            loanName = "Impossible",
            loanAmount = 1_000m,
            remainingBalance = 5_000m,
            interestRate = 3m,
            dueDate = "2030-01-01",
            isPaidOff = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("RemainingBalance", await ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task An_unsupported_currency_is_refused_rather_than_stored()
    {
        using var client = await AuthenticatedClientAsync("bad-currency");

        // The reason this one matters: every figure on a property is denominated in a currency
        // fixed at creation, and portfolio totals refuse to add unlike ones. A property stored as
        // "XYZ" could never be combined with anything again.
        var response = await client.PostAsJsonAsync("/api/RentalProperties", new
        {
            propertyName = "Nowhere",
            address = "1 Nowhere Road",
            currencyCode = "NOT-A-CURRENCY",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CurrencyCode", await ErrorFieldsAsync(response));
    }

    [Fact]
    public async Task A_state_conflict_is_a_problem_document_without_a_field()
    {
        using var client = await AuthenticatedClientAsync("conflict");
        var username = $"conflict-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, username)).StatusCode);

        var duplicate = await RegisterAsync(client, username);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ProblemJson, duplicate.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());

        // detail, and deliberately no errors map: there is no input to put this under.
        Assert.True(body.RootElement.TryGetProperty("detail", out var detail));
        Assert.False(string.IsNullOrWhiteSpace(detail.GetString()));
        Assert.False(body.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task A_valid_write_still_succeeds()
    {
        using var client = await AuthenticatedClientAsync("still-works");

        // The counterweight. Validation that rejects everything satisfies every test above.
        var response = await client.PostAsJsonAsync("/api/BankAccounts", new
        {
            accountName = "Current account",
            balance = 1_250.55m,
            bankName = "Test Bank",
            accountNumber = "NL00TEST0123456789",
            accountType = "Current",
            currencyCode = "EUR",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task<List<string>> ErrorFieldsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return body.RootElement
            .GetProperty("errors")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToList();
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string username) =>
        client.PostAsJsonAsync("/api/auth/register",
            new { username, email = $"{username}@example.test", password = Password });

    private async Task<HttpClient> AuthenticatedClientAsync(string prefix)
    {
        var client = _factory.CreateIsolatedClient();
        var username = $"{prefix}-{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.OK, (await RegisterAsync(client, username)).StatusCode);

        var loggedIn = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);

        using var body = JsonDocument.Parse(await loggedIn.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("token").GetString());

        return client;
    }
}
