using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The agenda endpoints, over HTTP.
///
/// <para>
/// <c>AgendaBuilderTests</c> covers the merge and the window with literals and
/// <c>AgendaServiceTests</c> covers the ack round trip against SQLite directly, but neither sends
/// a request — this is what proves the controller wiring itself: routing <c>/agenda</c> ahead of
/// <c>GetEvent</c>'s <c>{id}</c> template, and the tenant boundary as the caller outside the
/// process actually experiences it.
/// </para>
///
/// <para>
/// <b>Requires the migration this change ships without.</b> <c>AgendaAcknowledgement</c> is a new
/// entity with no migration behind it yet — see the PR description for the exact
/// <c>dotnet ef migrations add</c> command. Until that migration exists and is applied, every test
/// below fails with a 500 from a missing table, which is the expected, honest failure mode for
/// code that has not been given its schema yet, not a defect in the tests themselves.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class UpcomingEventsAgendaEndpointTests
{
    private const string Password = "correct-horse-battery";

    private readonly ApiFactory _factory;

    public UpcomingEventsAgendaEndpointTests(ApiFactory factory) => _factory = factory;

    private static DateTime InDays(int days) => DateTime.UtcNow.Date.AddDays(days);

    [Fact]
    public async Task A_manual_event_within_the_window_appears_on_the_agenda()
    {
        using var client = await AuthenticatedClientAsync("agenda-manual");
        await CreateManualEventAsync(client, "Boiler service", InDays(5));

        var entries = await ReadAgendaAsync(client, days: 30);

        Assert.Contains(entries.EnumerateArray(), e => e.GetProperty("title").GetString() == "Boiler service");
    }

    [Fact]
    public async Task A_manual_event_outside_the_window_is_left_out()
    {
        using var client = await AuthenticatedClientAsync("agenda-outside-window");
        await CreateManualEventAsync(client, "Far off renewal", InDays(90));

        var entries = await ReadAgendaAsync(client, days: 7);

        Assert.DoesNotContain(entries.EnumerateArray(), e => e.GetProperty("title").GetString() == "Far off renewal");
    }

    [Fact]
    public async Task Acknowledging_an_event_removes_it_from_a_later_call()
    {
        using var client = await AuthenticatedClientAsync("agenda-ack");
        var id = await CreateManualEventAsync(client, "Meter reading", InDays(3));

        var before = await ReadAgendaAsync(client, days: 30);
        var key = Assert.Single(before.EnumerateArray(), e => HasUpcomingEventId(e, id))
            .GetProperty("key").GetString();

        var ack = await client.PostAsync($"/api/UpcomingEvents/agenda/{key}/ack", content: null);
        Assert.Equal(HttpStatusCode.NoContent, ack.StatusCode);

        var after = await ReadAgendaAsync(client, days: 30);
        Assert.DoesNotContain(after.EnumerateArray(), e => HasUpcomingEventId(e, id));
    }

    [Fact]
    public async Task Acknowledging_an_unknown_key_is_a_no_op_rather_than_an_error()
    {
        using var client = await AuthenticatedClientAsync("agenda-ack-unknown");

        var ack = await client.PostAsync("/api/UpcomingEvents/agenda/manual:999999/ack", content: null);

        Assert.Equal(HttpStatusCode.NoContent, ack.StatusCode);
    }

    [Fact]
    public async Task A_negative_window_is_refused()
    {
        using var client = await AuthenticatedClientAsync("agenda-negative-days");

        var response = await client.GetAsync("/api/UpcomingEvents/agenda?days=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Another_users_agenda_has_none_of_the_callers_events()
    {
        using var owner = await AuthenticatedClientAsync("agenda-owner");
        using var stranger = await AuthenticatedClientAsync("agenda-stranger");

        await CreateManualEventAsync(owner, "Owner-only reminder", InDays(1));

        var strangersAgenda = await ReadAgendaAsync(stranger, days: 30);

        Assert.DoesNotContain(
            strangersAgenda.EnumerateArray(),
            e => e.GetProperty("title").GetString() == "Owner-only reminder");
    }

    [Fact]
    public async Task The_agenda_is_refused_without_a_token()
    {
        using var client = _factory.CreateIsolatedClient();

        var response = await client.GetAsync("/api/UpcomingEvents/agenda");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

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

    private static async Task<int> CreateManualEventAsync(HttpClient client, string title, DateTime eventDate)
    {
        var created = await client.PostAsJsonAsync("/api/UpcomingEvents", new
        {
            title,
            description = (string?)null,
            eventDate,
            isRecurring = false,
            isNotified = false,
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static bool HasUpcomingEventId(JsonElement entry, int id) =>
        entry.TryGetProperty("upcomingEventId", out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.GetInt32() == id;

    private static async Task<JsonElement> ReadAgendaAsync(HttpClient client, int days)
    {
        var response = await client.GetAsync($"/api/UpcomingEvents/agenda?days={days}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
