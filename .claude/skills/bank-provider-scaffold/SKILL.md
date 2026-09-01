---
name: bank-provider-scaffold
description: Implement the Enable Banking provider (EnableBankingProvider : IBankDataProvider) per docs/research/banking-data-integration.md's phase-1 plan. Invoke with /bank-provider-scaffold when ready to build real bank-feed import.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
---

Re-read `docs/research/banking-data-integration.md` first — it already made the vendor decision (Enable
Banking, phase 1 = own accounts only, no AISP license needed) and laid out the plan; this skill executes
that plan, it doesn't re-derive it.

1. `MoneyManager.Api/Services/Banking/IBankDataProvider.cs` is the contract to implement;
   `ManualBankDataProvider.cs` is the existing reference for how a provider plugs into the app (DI
   registration, what calls it).
2. Implement `EnableBankingProvider` scoped to **phase 1 only** (the user's own bank accounts, not a public
   TPP-as-a-service integration) — don't build phase 2/3 from the research doc (friendly users, real
   SaaS/AISP licensing); those need business/legal decisions, not just code.
3. Credentials/config: follow the existing `appsettings.json`/environment-variable pattern used by other
   external integrations (see how `EcbExchangeRateProvider` is configured) — never hardcode API keys. Check
   `CLAUDE.md`'s rule about third-party requests (the ECB fetch is documented as "the one sanctioned
   exception") — flag explicitly that this adds a second one rather than silently expanding the existing
   exception.
4. Add tests following the existing pattern for the `Banking` service folder.
5. Run `dotnet build` and `dotnet test` and confirm both pass before reporting done.
6. Report clearly if actually calling the Enable Banking API requires credentials/registration the user
   hasn't set up yet — don't fake success or stub around a missing credential silently.
