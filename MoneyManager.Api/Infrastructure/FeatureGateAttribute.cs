using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Infrastructure
{
    /// <summary>
    /// Makes every action on a controller answer 404 when its section is switched off in
    /// <see cref="FeatureOptions"/>.
    ///
    /// <para>
    /// Hiding a navigation link while the endpoints keep answering is not hiding the section, it
    /// is moving it one URL away. Applied here rather than as an <c>if</c> at the top of each
    /// action so that a method added later is covered by having been added to a gated controller,
    /// which is the only version of this that stays true as the code grows.
    /// </para>
    ///
    /// <para>
    /// 404 rather than 403, matching how <c>AuthController</c> answers when registration is
    /// closed: a deployment should not confirm that a section it has switched off is there. The
    /// body is empty for the same reason — that is byte for byte what the <c>/api/{**slug}</c>
    /// fallback in <c>Program.cs</c> answers for a route that was never registered, so a disabled
    /// feature is indistinguishable from one that was never built. It is the one place in the API
    /// that deliberately does not carry the shared <c>ProblemDetails</c> envelope, and the UI's
    /// error extractor already handles a bodiless failure.
    /// </para>
    ///
    /// <para>
    /// Getting that byte-for-byte took three attempts, and each wrong one is worth knowing about.
    /// <c>[ApiController]</c> installs an always-run result filter that fills any
    /// <c>IClientErrorActionResult</c> with a <c>ProblemDetails</c> body, and it runs even when a
    /// resource filter has short-circuited the pipeline — so <c>NotFoundResult</c> produced a
    /// described 404, and <c>StatusCodeResult</c> did too, because it is the class that
    /// <em>declares</em> that interface rather than a plainer sibling of it. <c>ContentResult</c>
    /// escaped the body but resolves a default <c>text/plain; charset=utf-8</c> even with a null
    /// <c>Content</c>, which left the two answers still distinguishable by header.
    /// </para>
    ///
    /// <para>
    /// The described-404-versus-empty-404 difference is not cosmetic: it is precisely the tell
    /// this gate exists to remove, because it says which paths are real routes. Both wrong
    /// versions of this were caught by
    /// <c>A_disabled_section_is_indistinguishable_from_one_that_was_never_built</c> comparing the
    /// two response bodies rather than only their status codes — which is the reason to write the
    /// assertion that way.
    /// </para>
    ///
    /// <para>
    /// A resource filter, so it runs before model binding — there is no reason to bind or
    /// validate a request body for a section that does not exist here. It runs after the
    /// authorization middleware, which is what keeps the anonymous answers unchanged: an
    /// unauthenticated caller still gets 401 on a gated route, and so learns nothing about which
    /// sections this deployment has enabled.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FeatureGateAttribute : Attribute, IResourceFilter
    {
        private readonly Feature _feature;

        public FeatureGateAttribute(Feature feature) => _feature = feature;

        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            // Resolved per request rather than injected: an attribute's constructor arguments
            // have to be compile-time constants, so there is nowhere for a dependency to arrive.
            // IOptions is a singleton over configuration read at startup, so this is a dictionary
            // lookup rather than any real work.
            var options = context.HttpContext.RequestServices
                .GetRequiredService<IOptions<FeatureOptions>>().Value;

            if (!options.IsEnabled(_feature))
            {
                // Status set on the response and an EmptyResult to close the pipeline, rather than
                // any of the results that carry a status themselves. Every one of those adds
                // something the unmatched-route fallback does not: NotFoundResult and
                // StatusCodeResult are IClientErrorActionResult, so [ApiController] fills them in
                // with a ProblemDetails body, and ContentResult resolves a default
                // "text/plain; charset=utf-8" even when its Content is null. EmptyResult's
                // executor does nothing at all, which is the only way to answer with exactly what
                // Results.NotFound() answers — a status and no headers of its own.
                context.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Result = new EmptyResult();
            }
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
            // Nothing to do on the way out.
        }
    }
}
