namespace MoneyManager.Api.Models
{
    /// <summary>
    /// Bound from the "Features" configuration section: which sections of the product this
    /// deployment presents at all.
    ///
    /// <para>
    /// The MVP is rental property management. Bank accounts and stocks exist, are half-built, and
    /// would be the first thing a customer clicks — so they are switched off rather than deleted,
    /// because the work is real and comes back once the rental half is settled. A flag is the
    /// right shape for "not yet" and the wrong shape for "never".
    /// </para>
    ///
    /// <para>
    /// Deployment-wide rather than per-user, deliberately. The point is that the customer never
    /// sees the section — not that they can find a switch for it — and a per-user flag would need
    /// a column, a migration, and a UI to set it. If two customers ever want different sections,
    /// that is the moment to move this onto the user, not before.
    /// </para>
    ///
    /// <para>
    /// The defaults below are the MVP shape, so a deployment gets it with no configuration at all.
    /// appsettings.Development.json turns everything back on, because a fresh clone should show
    /// the whole application to whoever is working on it.
    /// </para>
    /// </summary>
    public class FeatureOptions
    {
        public const string SectionName = "Features";

        /// <summary>Bank accounts: balances, the totals widget, and the provider seam behind them.</summary>
        public bool Banking { get; set; }

        /// <summary>The stock holdings section.</summary>
        public bool Stocks { get; set; }

        /// <summary>
        /// Loans. On for the MVP: a mortgage is the financing side of "is this property
        /// underperforming", so hiding it does not make the answer smaller, it makes it wrong.
        /// It has a flag of its own anyway, so changing that judgement is one environment
        /// variable rather than a code change.
        /// </summary>
        public bool Loans { get; set; } = true;

        /// <summary>Upcoming events. On for the MVP: rent due dates are part of the rental story.</summary>
        public bool Events { get; set; } = true;

        /// <summary>
        /// Whether <paramref name="feature"/> is switched on for this deployment.
        ///
        /// <para>
        /// A <c>switch</c> over the enum rather than reflection or a dictionary, so adding a
        /// member without handling it here does not compile — the alternative fails at runtime by
        /// quietly reporting the new section as disabled, which looks exactly like the flag
        /// working.
        /// </para>
        /// </summary>
        public bool IsEnabled(Feature feature) => feature switch
        {
            Feature.Banking => Banking,
            Feature.Stocks => Stocks,
            Feature.Loans => Loans,
            Feature.Events => Events,
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, "Unhandled feature."),
        };
    }

    /// <summary>
    /// The sections that can be switched off. Named rather than stringly-typed so a gate cannot
    /// be attached to a feature that does not exist.
    /// </summary>
    public enum Feature
    {
        Banking,
        Stocks,
        Loans,
        Events,
    }
}
