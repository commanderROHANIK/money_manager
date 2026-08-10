using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    /// <summary>
    /// Which sections this deployment presents, so the SPA can build its navigation from the same
    /// answer the API enforces rather than from a second copy of the truth in the bundle.
    ///
    /// <para>
    /// A build-time constant in the front end would have been simpler and wrong: the bundle is
    /// built once by the Dockerfile and the flags are set per deployment, so the two would drift
    /// the first time an environment differed. Serving them means the navigation cannot disagree
    /// with what the endpoints do.
    /// </para>
    ///
    /// <para>
    /// Authenticated, like everything else. It is tempting to make this anonymous so the shell can
    /// read it before login, but the login screen has no navigation to build and adding
    /// <c>[AllowAnonymous]</c> is a security decision that would need to be argued rather than
    /// picked up as a convenience — CLAUDE.md is explicit about that. It also keeps the enabled
    /// sections from being something an anonymous caller can enumerate.
    /// </para>
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class FeaturesController : ControllerBase
    {
        private readonly FeatureOptions _features;

        public FeaturesController(IOptions<FeatureOptions> features) => _features = features.Value;

        [HttpGet]
        public ActionResult<FeaturesDto> Get() => FeaturesDto.From(_features);
    }

    /// <summary>
    /// Deliberately its own type rather than returning <see cref="FeatureOptions"/> directly, so
    /// that adding an option which is not a UI section — a limit, a provider name, a key — does
    /// not silently start being served to the browser.
    /// </summary>
    public record FeaturesDto(bool Banking, bool Stocks, bool Loans, bool Events)
    {
        public static FeaturesDto From(FeatureOptions options) =>
            new(options.Banking, options.Stocks, options.Loans, options.Events);
    }
}
