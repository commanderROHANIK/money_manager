namespace MoneyManager.Api.Services.Banking
{
    /// <summary>
    /// The default provider: there isn't one. Balances are whatever the owner typed in.
    ///
    /// <para>
    /// Registered so a fresh clone builds and runs with no configuration and — the part that
    /// matters — <b>no network access</b>. Every method refuses rather than returning an empty
    /// list, because empty is indistinguishable from "your bank has no accounts" and would let a
    /// caller quietly render nothing where it should have said the feature is not configured.
    /// </para>
    ///
    /// <para>
    /// It also keeps the seam honest while there is only one implementation. An interface whose
    /// sole implementation is the real vendor tends to grow that vendor's shape; one that has to
    /// accommodate a deliberate no-op stays a boundary.
    /// </para>
    /// </summary>
    public sealed class ManualBankDataProvider : IBankDataProvider
    {
        private const string NotConfigured =
            "No bank data provider is configured. Balances are entered manually; see " +
            "docs/research/banking-data-integration.md for what connecting one would involve.";

        public Task<IReadOnlyList<Aspsp>> GetBanksAsync(string country, CancellationToken ct) =>
            throw new NotSupportedException(NotConfigured);

        public Task<AuthStartResult> StartAuthAsync(string aspspId, string redirectUri, CancellationToken ct) =>
            throw new NotSupportedException(NotConfigured);

        public Task<BankSession> CompleteAuthAsync(string code, CancellationToken ct) =>
            throw new NotSupportedException(NotConfigured);

        public Task<IReadOnlyList<ProviderAccount>> GetAccountsAsync(string sessionId, CancellationToken ct) =>
            throw new NotSupportedException(NotConfigured);

        public Task<AccountBalances> GetBalancesAsync(string sessionId, string accountId, CancellationToken ct) =>
            throw new NotSupportedException(NotConfigured);
    }
}
