using System.Net;
using Xunit;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// The SPA and the API share one origin in a deployed image, which puts static file serving
/// inside the authorization pipeline rather than beside it. That interaction has two failure
/// modes, both of which produce a working-looking app.
///
/// <para>
/// The first is the SPA shell. <c>MapFallbackToFile</c> carries no authorization metadata, so
/// the deny-by-default <c>FallbackPolicy</c> in <c>Program.cs</c> applies to it and it answers
/// 401 — but only on a hard refresh or a deep link, because <c>/</c> and every client-side
/// navigation are served by the static file middleware before authorization runs. Worse, the
/// axios interceptor never sees it: a document navigation is not an XHR, so the app does not
/// redirect to the login screen, it simply fails.
/// </para>
///
/// <para>
/// The second is the assets, and it is the easier one to introduce. The fallback's route pattern
/// is <c>{*path:nonfile}</c>, which deliberately does not match a path whose last segment has a
/// file extension — so <c>/assets/app.js</c> matches no endpoint at all, and the fallback policy
/// is applied to endpoint-less requests too. Move <c>UseStaticFiles</c> after
/// <c>UseAuthorization</c> and every script and stylesheet 401s behind an <c>index.html</c> that
/// still loads: a blank page whose console reads like a CORS or bundler problem.
/// </para>
///
/// <para>
/// Both are invisible to <c>vue-tsc</c>, to <c>vite build</c> and to every other test in this
/// project, and neither shows up until the app is hosted the way it is deployed.
/// </para>
///
/// <para>
/// The counterweight is <see cref="Serving_the_spa_does_not_make_the_api_anonymous"/>. Three
/// <c>AllowAnonymous</c> calls were added to make the above work, and an over-broad one would
/// silently unauthenticate the API — so the tests that assert things are reachable are only
/// safe in the presence of one that asserts what still is not.
/// </para>
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class StaticFileServingTests
{
    private readonly ApiFactory _factory;

    public StaticFileServingTests(ApiFactory factory) => _factory = factory;

    // ------------------------------------------------------------------
    // The shell and its assets
    // ------------------------------------------------------------------

    [Fact]
    public async Task The_spa_shell_is_served_at_the_root_without_a_token()
    {
        using var client = _factory.CreateIsolatedClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Asserting on the marker rather than just the status code: this is the test that pins
        // the stand-in web root as actually applied. If it is not, every other assertion here
        // degrades into "some 404 happened", so this one failing first is the useful signal.
        Assert.Contains(ApiFactory.ShellMarker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_deep_link_is_answered_with_the_shell_rather_than_a_challenge()
    {
        using var client = _factory.CreateIsolatedClient();

        // vue-router is in history mode, so this arrives as a real navigation rather than being
        // resolved client-side. Without AllowAnonymous on the file fallback it is a bare 401,
        // and the user sees a broken page instead of the login screen.
        var response = await client.GetAsync("/properties/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ApiFactory.ShellMarker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_static_asset_is_served_without_a_token()
    {
        using var client = _factory.CreateIsolatedClient();

        // The regression guard for middleware order. A path with a file extension matches no
        // endpoint, so if UseStaticFiles ever moves after UseAuthorization this returns 401
        // while the shell above keeps returning 200.
        var response = await client.GetAsync(ApiFactory.AssetPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Where the SPA fallback must not reach
    // ------------------------------------------------------------------

    [Fact]
    public async Task An_unmatched_api_path_returns_404_and_not_the_shell()
    {
        using var client = _factory.CreateIsolatedClient();

        var response = await client.GetAsync("/api/not-a-real-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The status code alone would not catch the defect: with no /api fallback registered the
        // file fallback answers this path 200 with index.html, and a caller expecting JSON gets
        // a parse error instead of a 404. Asserting the body is what distinguishes the two.
        Assert.DoesNotContain(ApiFactory.ShellMarker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_health_endpoint_answers_without_a_token()
    {
        using var client = _factory.CreateIsolatedClient();

        // The platform healthcheck has no credentials to present. If this ever requires a token,
        // every deploy fails its healthcheck and rolls back with nothing in the logs to say why.
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // What must still be refused
    // ------------------------------------------------------------------

    [Fact]
    public async Task The_openapi_document_is_not_served_outside_development()
    {
        using var client = _factory.CreateIsolatedClient();

        // MapOpenApi is registered only in Development, and this factory hosts the app as
        // Production. The document describes every route in the API, so it should not be
        // retrievable from a deployment — and docs/deployment.md turns on ASPNETCORE_ENVIRONMENT
        // being Production there for reasons that have nothing to do with this.
        var response = await client.GetAsync("/openapi/v1.json");

        // Deliberately not asserting a specific code. With no endpoint registered the request
        // falls through to the deny-by-default policy rather than to a 404, and which of the two
        // answers is an implementation detail. That it is not the document is the point.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Serving_the_spa_does_not_make_the_api_anonymous()
    {
        using var client = _factory.CreateIsolatedClient();

        // The three AllowAnonymous calls that make the SPA work are scoped to the shell, the
        // /api 404 and the healthcheck. An over-broad one — AllowAnonymous on the controllers,
        // or a fallback pattern that captured /api — would leave every landlord's portfolio
        // readable without a token, through a build and a UI that both look entirely fine.
        var response = await client.GetAsync("/api/RentalProperties");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unmatched_api_path_is_not_a_way_to_probe_for_endpoints()
    {
        using var client = _factory.CreateIsolatedClient();

        // The /api fallback is anonymous, so it must answer identically whether or not the path
        // corresponds to something real. A protected endpoint answers 401 and a missing one 404,
        // which is the correct pair: 404 here would confirm the endpoint does not exist, and 401
        // there says nothing about whether the resource does.
        var missing = await client.GetAsync("/api/no-such-controller");
        var existing = await client.GetAsync("/api/RentalProperties");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, existing.StatusCode);
    }
}
