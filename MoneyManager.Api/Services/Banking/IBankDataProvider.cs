namespace MoneyManager.Api.Services.Banking
{
    /// <summary>A bank as a provider lists it, for the "choose your bank" step.</summary>
    public sealed record Aspsp(string Id, string Name, string Country, string? LogoUrl = null);

    /// <summary>Where to send the user to authorise, and the reference to resume with afterwards.</summary>
    public sealed record AuthStartResult(string AuthorizationUrl, string StateReference);

    /// <summary>
    /// An authorised connection. <paramref name="ExpiresAt"/> is the part that gets forgotten:
    /// consent is time-limited by regulation, so a session that worked last month is not
    /// evidence that it works today.
    /// </summary>
    public sealed record BankSession(string SessionId, DateTimeOffset ExpiresAt);

    /// <summary>One account behind a session, in this application's terms rather than a vendor's.</summary>
    public sealed record ProviderAccount(
        string AccountId,
        string? Name,
        string? Iban,
        string CurrencyCode);

    /// <summary>
    /// Balances for one account.
    ///
    /// <para>
    /// Two figures rather than one, deliberately. "Balance" is not a single number: the Berlin
    /// Group model returns an array of typed balances, and booked and available routinely differ
    /// by a pending card transaction. Collapsing them at the boundary would mean the application
    /// could never say which one it is showing. <paramref name="AsOf"/> is on the same footing —
    /// a balance without a timestamp cannot be told apart from a stale one.
    /// </para>
    /// </summary>
    public sealed record AccountBalances(
        decimal? Booked,
        decimal? Available,
        string CurrencyCode,
        DateTimeOffset AsOf);

    /// <summary>
    /// The seam between this application and whichever open-banking vendor is in use.
    ///
    /// <para>
    /// The reason it exists is in <c>docs/research/banking-data-integration.md</c>: the obvious
    /// answer to "which provider" was Nordigen, which was free, popular, acquired, and closed to
    /// new signups inside three years. The research doc's advice is to assume the current provider
    /// will be the next one to go. Nothing above this interface knows which vendor is in use, and
    /// the record types above are this application's shapes — a vendor's response is normalised at
    /// the boundary rather than leaking into the UI.
    /// </para>
    ///
    /// <para>
    /// The cost of the abstraction is about an hour. The cost of not having it, when the provider
    /// changes, is rewriting every caller.
    /// </para>
    /// </summary>
    public interface IBankDataProvider
    {
        Task<IReadOnlyList<Aspsp>> GetBanksAsync(string country, CancellationToken ct);

        Task<AuthStartResult> StartAuthAsync(string aspspId, string redirectUri, CancellationToken ct);

        Task<BankSession> CompleteAuthAsync(string code, CancellationToken ct);

        Task<IReadOnlyList<ProviderAccount>> GetAccountsAsync(string sessionId, CancellationToken ct);

        Task<AccountBalances> GetBalancesAsync(string sessionId, string accountId, CancellationToken ct);
    }
}
