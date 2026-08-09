namespace MoneyManager.Api.Models
{
    /// <summary>
    /// Bound from the "Auth" configuration section.
    /// </summary>
    public class AuthOptions
    {
        public const string SectionName = "Auth";

        /// <summary>
        /// Whether <c>POST /api/auth/register</c> is reachable.
        ///
        /// <para>
        /// Off unless configuration turns it on, because the two failure directions are not
        /// symmetric. A deployment that forgot to close registration is open to anyone who finds
        /// the URL — and preview environments have public URLs — while one that forgot to open it
        /// is merely inconvenient, and says so the first time anybody tries.
        /// </para>
        ///
        /// <para>
        /// appsettings.Development.json turns it on, so a fresh clone still runs with no setup.
        /// </para>
        /// </summary>
        public bool AllowRegistration { get; set; }
    }
}
