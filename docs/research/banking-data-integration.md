# Getting real bank data into Money Manager

**Status:** research, no code committed
**Date:** July 2026
**Scope:** how to replace the hand-entered `BankAccount.Balance` with a real balance pulled from the user's bank
**Assumed context:** Hungary / EU banks; personal use first, possible SaaS later; ASP.NET Core 9 backend

---

## 1. The short version

**Use [Enable Banking](https://enablebanking.com) for phase 1.** It is the only European
provider still offering genuine self-serve, free production access to your *own* accounts,
it covers every major Hungarian bank, and its free tier is not a trial — it is a permanent
mode called *Restricted Production*.

**Do not build against the provider directly.** Put a two-method interface in front of it
(`IBankDataProvider`). This is the single most important decision in this document, and
section 5 explains why: the previously obvious answer to this question — Nordigen — was
free, beloved, acquired, and then closed to new signups, all within three years. Assume
your provider will be the next one.

**Do not get an AISP licence.** Not now, and probably not ever. Section 4.2 shows the
numbers.

| Phase | What | Provider cost | Eng. effort |
|---|---|---|---|
| 1. Your own accounts | Enable Banking Restricted Production | €0 | ~3–5 days |
| 2. A handful of friendly users | Enable Banking Production (contract) | quoted, volume-based | ~2–4 days more |
| 3. Real SaaS | Enable Banking *TPP-as-a-Service*, or Salt Edge / Tink | contract + compliance | weeks, mostly legal |

The thing that will actually cost you time is not the API call. It is consent lifecycle,
token storage, and the fact that "balance" is not one number. Sections 3.4–3.6.

---

## 2. What we're actually trying to do

Today `BankAccount` (`MoneyManager.Api/Models/BankAccount.cs`) is a hand-typed record:

```csharp
public decimal Balance { get; set; }
public string BankName { get; set; }
public string AccountNumber { get; set; }
```

Somebody types their balance in and it is wrong the next day. The goal is a bank accounts
page whose numbers are true without anyone maintaining them.

Requirements, in the order they matter:

1. **Balance per account, refreshed automatically.** The whole ask, today.
2. **Transactions later.** Every provider that gives balances gives transactions from the
   same consent, so this is essentially free *if* the data model anticipates it. It is
   expensive if it doesn't. Section 9 accounts for this.
3. **Multi-currency.** Already a first-class concept in the codebase (`CurrencyCode`
   everywhere, portfolio totals refuse to add across currencies). Bank data arrives with
   its own currency and must not break that rule.
4. **Multi-tenant safety.** `IOwnedByUser` + global query filters. Bank credentials and
   tokens are the most sensitive data this app will ever hold; they must live inside that
   same isolation model, not beside it.

Explicit non-goals: payment initiation (moving money), card data, credit scoring.

---

## 3. Background: how bank data access actually works

Worth reading properly if this is new to you — most of the integration cost lives in
concepts here, not in code.

### 3.1 Why you can't just call your bank

There are exactly three ways software gets at bank data, and only one is a real option.

**Screen scraping** — log in as the user with their credentials and parse the HTML. This is
how everything worked before 2018. It is now largely illegal in the EU without a licence,
breaks whenever the bank redesigns, and requires storing the user's actual banking password.
**Do not do this.** It's mentioned only so you recognise it when a cheap provider is quietly
doing it (some non-EU aggregators still do).

**File import** — the bank exports a statement, you parse it. Legitimate, zero regulatory
burden, and genuinely useful as a fallback. Section 4.3.

**Open banking APIs** — the bank exposes a regulated API. This is the real answer in Europe,
and it exists because of a law.

### 3.2 PSD2, and the vocabulary you need

The EU's second Payment Services Directive (PSD2) forced every bank in the EEA to expose a
free API for third parties to read account data, with the customer's consent. Hungary's
banks are all in scope, supervised by the MNB.

The acronyms are unavoidable, so:

| Term | Meaning | Who that is here |
|---|---|---|
| **ASPSP** | The bank holding the account | OTP, K&H, Erste, MBH… |
| **PSU** | Payment Service User — the human | You, then your users |
| **TPP** | Third Party Provider — the app asking for data | Money Manager (in principle) |
| **AISP** | A TPP licensed to *read* account information | The licence you don't want |
| **PISP** | A TPP licensed to *initiate payments* | Not in scope |
| **SCA** | Strong Customer Authentication — 2FA at the bank | The redirect the user completes |
| **eIDAS cert** | Cryptographic certificate proving you're a licensed TPP | ~€358+/yr, section 4.2 |

The critical, counter-intuitive point: **PSD2 says the API must be free — but you must be
licensed to use it.** Free access, gated by a regulatory licence. That gate is why
aggregators exist and why this document recommends one.

### 3.3 What an aggregator actually sells you

An aggregator like Enable Banking is a licensed AISP that has already integrated ~2,500
banks. You get:

- **Their licence.** You operate under it instead of getting your own.
- **Their eIDAS certificates.** Managed, renewed, and not your problem.
- **One API instead of 2,500.** This is worth more than it sounds. PSD2 mandated the
  *outcome*, not the *format*. Every bank's API differs in auth flow, field names, quirks
  and outages. Section 7 has a Hungarian example that makes this concrete.
- **Coverage maintenance.** Banks break their APIs constantly. Somebody else fixes it.

You are not paying for HTTP calls. You are paying for a licence and a normalisation layer.

### 3.4 The consent flow

This shape is the same across every provider, so learn it once:

```
1. User clicks "Connect bank" in Money Manager
2. Backend asks provider to start an auth session for a chosen bank (ASPSP)
3. Provider returns a URL → browser redirects user to their bank
4. User logs in at the bank and does SCA (SMS / bank app / token)
   ─ the user's credentials never touch Money Manager ─
5. Bank redirects back to our callback with a code
6. Backend exchanges the code for a session, stores the session ID
7. Backend can now call GET /accounts and GET /accounts/{id}/balances
   until the consent expires
```

Two consequences for the app:

- **You need a public HTTPS callback URL.** Step 5 is a browser redirect from the bank.
  Localhost works for development with most providers, but production needs a real domain.
- **Connecting a bank is interactive; refreshing a balance is not.** Once the session
  exists, balance polling is a plain background job. Only the initial connect (and
  re-consent) needs the user present.

### 3.5 Consent expires. This is the thing people underestimate.

Under the amended PSD2 RTS, an AISP may access accounts without fresh SCA for **180 days**
(raised from 90 in 2022 — the EBA's most developer-friendly change to date). After that the
user must physically re-authenticate at their bank.

So the product has a recurring, unavoidable interruption: **roughly twice a year, every
connected bank asks the user to log in again.** If the UI does not handle this gracefully,
balances silently go stale and the app quietly starts lying — which for this codebase is a
philosophical violation, not just a bug (the README: *any figure that cannot be known is
shown as unknown rather than as zero*).

Design implications, and these are non-negotiable:

- Store `ConsentExpiresAt` and surface it in the UI *before* it lapses.
- A stale balance must render as stale, with its `as-of` timestamp — never as a current one.
- Re-consent is a normal, expected flow, not an error path.

(For reference: the UK kept a 90-day re-consent cycle. Not our problem, but it explains
conflicting advice online.)

### 3.6 "Balance" is not one number

The Berlin Group NextGenPSD2 standard — which essentially all Hungarian banks implement —
defines several balance types, and a bank may return any subset:

| Type | Meaning |
|---|---|
| `closingBooked` | Settled transactions only. The accountant's number. |
| `interimAvailable` | What you can actually spend right now — includes pending items, may include overdraft. |
| `expected` | Booked + pending. |
| `forwardAvailable` | Available at a future date. |
| `openingBooked` / `interimBooked` | Less commonly returned. |

**Recommendation:** display `interimAvailable` (it matches what the user sees in their
banking app, which is the number they will compare against and complain about), but persist
`closingBooked` too, because that is the one that reconciles against a transaction ledger
later.

Two field pitfalls: not every bank returns every type — you need a documented fallback
order, and a null when none is available. And an overdrawn account returns a *negative*
balance, which `decimal Balance` handles but any UI formatting assumption may not.

---

## 4. The four routes, with costs

### 4.1 Route A — Use an aggregator ✅ recommended

Covered above. Cost: €0 to start, contract pricing at scale. Effort: days.

### 4.2 Route B — Get your own AISP licence and talk to banks directly ❌

For completeness, and because it's the option beginners assume is "the proper way":

| Item | Cost |
|---|---|
| AISP registration (MNB) | 3–6 months, no minimum capital, but legal + compliance work |
| Professional indemnity insurance | Mandatory, ongoing, scales with usage |
| eIDAS QWAC certificate | from ~€358 + VAT/year |
| QSEAL signing | from ~€0.02 *per signature* — reportedly up to €240k/month for a TPP with 10k daily users |
| Integrating each bank individually | Weeks per bank. MBH alone is three APIs (section 7). |
| Ongoing breakage maintenance | Forever |

This is a business, not a feature. Rule it out and move on.

### 4.3 Route C — File import (CAMT.053 / MT940 / CSV) ⚠️ keep as fallback

Every Hungarian bank lets you download statements, typically as CSV or the ISO 20022
`camt.053` XML. Parsing these is boring, well-documented work with no regulatory burden and
no vendor at all.

- **Pro:** zero cost, zero lock-in, works for banks no aggregator covers, works forever.
- **Con:** manual, so balances are as fresh as the last upload. Doesn't meet the actual
  requirement.

**Where it earns its place:** as the escape hatch for an uncovered bank, and as the
historical backfill when a new connection only returns 90 days of transactions. Worth
building *eventually*, not now.

### 4.4 Route D — Unofficial APIs / scraping ❌

Reverse-engineering a bank's mobile app API. Illegal-ish, fragile, requires holding real
credentials, and would end the SaaS ambition immediately. No.

---

## 5. The vendor landscape, and the thing that changed in 2025

### 5.1 Why most advice you'll find online is wrong

If you google this, you will be told to use **Nordigen** — free open banking, beloved by
indie developers and self-hosted finance apps.

Here is what actually happened:

1. Nordigen offered a genuinely free AIS API and became the default for hobbyist finance apps.
2. **2022:** GoCardless acquired Nordigen.
3. **2023:** Rebranded to *GoCardless Bank Account Data*, free tier retained.
4. **July 2025:** New signups disabled. There is now a dedicated page at
   `bankaccountdata.gocardless.com/new-signups-disabled`.
5. **2026:** Closed to new customers and winding down. Existing accounts keep working.

I confirmed this independently rather than trusting a single source: GoCardless's own
signups-disabled page, a [December 2025 GitHub issue](https://github.com/adept/gocardless-to-csv/issues/4)
on a tool that broke because of it, and a [Firefly III issue](https://github.com/firefly-iii/firefly-iii/issues/10753)
where the maintainers accepted Enable Banking as the replacement integration for their
importer v2.0.0.

**Any tutorial, blog post, or LLM answer recommending Nordigen/GoCardless for a new project
is out of date.** Including, quite possibly, one you'll get if you ask this question again
somewhere else.

The lesson is not "GoCardless bad." It is that the free tier in this market is a
customer-acquisition strategy with a shelf life, and you should architect for its removal.

### 5.2 The providers

| Provider | Self-serve? | Free tier | Coverage | Pricing model | Verdict |
|---|---|---|---|---|---|
| **Enable Banking** | ✅ Yes | ✅ Restricted Production — free, own accounts, permanent | ~2,500 banks / 29 countries | Per connected account/month, quoted | **Recommended** |
| GoCardless BAD | ❌ Closed | — | Was excellent | — | Dead for new projects |
| **Salt Edge** | ❌ Sales | ❌ | 8,000+ globally, widest reach | Quoted, by API call volume | Phase 3 candidate |
| **Tink** (Visa) | ❌ Sales | ❌ | 6,000+ European | Enterprise | Strong, heavy for us |
| **TrueLayer** | Partly | ❌ | UK + EU, payments-led | Published tiers | Payments-first |
| **Yapily** | ❌ Sales | ❌ | Deep EU, raw/unnormalised | Enterprise | API-purist, no free entry |
| **Plaid** | Sandbox only | Sandbox only | US-strong, EU present | Opaque, sales-led | Wrong continent for us |
| Teller / SimpleFIN | ✅ | Partly | **US only** | $15/yr–free tiers | Irrelevant in EU — noted because they dominate search results |

The last row matters: much of the "cheap open banking API" content online is US-centric.
Teller and SimpleFIN are frequently recommended and are useless for Hungarian banks.

### 5.3 Enable Banking in detail

**Company.** Founded 2019, based in Espoo, Finland. Regulated AISP under Finnish FIN-FSA
supervision (an EU licence, passportable to Hungary). ISO/IEC 27001 certified. Named
Finland's 5th largest fintech in 2024.

**The free tier — how it really works.** Register an application, choose *production*
(not sandbox). It starts `Inactive`. You activate it by clicking "Activate by linking
accounts" and completing a real bank login. From then on the app is in **Restricted
Production**: real production data, real bank, no charge — but it can *only* read accounts
you explicitly whitelisted.

That constraint is exactly right for phase 1 and exactly wrong for phase 3, which is the
honest framing: it is not a free tier you can grow inside. It is a free tier you can *build*
inside, then pay to leave.

**API shape.**

- Auth: a JWT you sign yourself with RS256, using an RSA private key (`.pem`) downloaded at
  registration, with your application ID in the `kid` header. Not OAuth client-credentials —
  you mint the token locally per request. Cheap and dependency-free in .NET.
- `GET /aspsps` — list banks
- `POST /auth` → returns the bank redirect URL
- `POST /sessions` — exchange the callback code for a session
- `GET /sessions/{id}` — the accounts under this consent
- `GET /accounts/{id}/balances` — **the endpoint we need**
- `GET /accounts/{id}/transactions` — phase 2, same consent, no extra integration
- Sessions valid up to 180 days, per section 3.5.

**.NET support — validate this expectation now.** There is **no official .NET SDK and no
NuGet package.** The [samples repo](https://github.com/enablebanking/enablebanking-api-samples)
has a `cs_example` folder, and that is the extent of it. Their own tooling (CLI, eIDAS
broker) is Python.

This is fine, and I want to be clear about why rather than hand-wave it: the entire
integration is `HttpClient` + `System.IdentityModel.Tokens.Jwt` (already transitively in the
project via `Microsoft.AspNetCore.Authentication.JwtBearer`) + `System.Security.Cryptography.RSA`
for the PEM key, which .NET has had first-class since .NET 5. Signing an RS256 JWT is about
fifteen lines. **No new NuGet dependency is required.** An official SDK would save perhaps
half a day and add a dependency that lags the API; hand-rolling is genuinely the better
trade here.

**Risks, stated plainly.** Enable Banking reported €444k revenue in 2023. That is a small
company. The pattern that killed Nordigen — small indie-friendly provider, acquired,
free tier withdrawn — is a live risk here, arguably more so than with Visa-owned Tink.
This does not change the recommendation, because the mitigation is cheap: the abstraction
in section 9.3 makes a provider swap a few days' work rather than a rewrite. But it should
inform how much provider-specific logic you allow to leak into the domain. The answer is
none.

---

## 6. Regulation: what applies, and what's coming

**GDPR.** Bank data is personal data of the highest sensitivity. For phase 1 (your own
accounts) this is trivial. For SaaS it means a DPA with the provider, a lawful basis
(consent), a retention policy, and export/erasure. Budget for it; don't discover it.

**Token storage.** Session IDs and refresh tokens are bearer credentials to somebody's bank
account. They must be encrypted at rest, never logged, and never returned by an API
endpoint. Section 9 keeps them out of the entity the controller serialises — the current
`BankAccountsController.GetBankAccounts()` returns the EF entity directly, so any secret
added to `BankAccount` would be immediately published over HTTP. This is a real trap and
the design avoids it structurally.

**PSD3 / PSR — coming, not urgent.** Final texts expected in the Official Journal in H1
2026, entry into force ~2027 with an 18–21 month transition, so realistically **mid-to-late
2027**. It tightens API quality obligations on banks — broadly good for us — and is an
aggregator problem, not ours.

**FIDA — interesting, distant.** Extends open-banking-style access beyond payment accounts
to investments, pensions, insurance, mortgages and loans, under a new *FISP* category. Still
in trilogue as of April 2026; realistically operational **2029–2030**.

Note the strategic relevance: FIDA is eventually the regulated path to automatic *mortgage*
data — which for a rental-property app with a `Loan` entity is the highest-value future feed
in this whole document. Nothing to do about it now beyond knowing it's coming.

---

## 7. Hungary specifics

Enable Banking documents Hungary as a market and covers OTP, K&H, Raiffeisen, MBH and
several smaller institutions, plus Erste. Two documented quirks that will cost you time if
they surprise you:

**OTP Bank** — redirect flow where the user must first choose between *private* and
*business*, then pick an SCA method. OTP InternetBank/MobileBank is private-only; OTPdirekt
serves both. If the UI doesn't set expectations, users pick wrong and the connection fails
in a way that looks like your bug.

**MBH Bank** — formed by merging MKB, Takarékbank and Budapest Bank, and **still exposes
three separate open banking APIs**, one per predecessor institution, with entirely different
auth flows. A user with an MBH account must connect via the API matching their *original*
bank. This is the single best argument in this document for using an aggregator: it is
exactly the kind of mess you are paying somebody else to already know about.

Also worth connecting early because they're easy and well-behaved: **Revolut** and **Wise**.
Both are covered by aggregators as ordinary ASPSPs. Note that Wise's *personal API token*
does **not** expose balance retrieval for European accounts (only US/CA/AU/NZ/SG/MY), so go
through the aggregator rather than assuming a direct integration is simpler — it isn't.

---

## 8. Recommendation

**Phase 1 — now.** Enable Banking, Restricted Production. Connect your own accounts. €0.
Build the whole feature: connect flow, balance refresh, staleness display, re-consent.
Everything you learn here is what phase 2 needs; nothing is throwaway.

**Phase 2 — a few real users.** Move to Enable Banking Production under contract. The code
does not change. The commercial conversation and a DPA do.

**Phase 3 — real SaaS.** Two options, decide when you get there:
- **Enable Banking TPP-as-a-Service** — they operate as your regulated AISP/PISP, including
  eIDAS certificate management. Pricing is a platform subscription plus a per-PSU-consent
  fee. This is the low-effort path and keeps your code unchanged.
- **Re-tender** against Salt Edge and Tink, who have broader coverage and more corporate
  durability. This is why section 9.3 exists.

**Why Enable Banking over the alternatives, in one line each:** it is the only one you can
start today without a sales call; Salt Edge and Tink have better coverage but no free entry
and no self-serve; TrueLayer and Yapily are payments-led; Plaid is the wrong continent;
GoCardless is closed.

**What would change this recommendation:** if you knew today that this is going to be a real
multi-tenant product within six months, starting a Salt Edge or Tink conversation
immediately would be defensible, because you'd skip a migration. Given "personal now, SaaS
later," starting free and abstracted is the better expected-value play.

---

## 9. What this means for the codebase

Grounded in the conventions already established in the repo — global query filters,
`FirstOrDefaultAsync` over `FindAsync`, in-memory decimal aggregation, nullable metrics that
mean *cannot be known*.

### 9.1 Data model

Do **not** put provider fields on `BankAccount`. That entity is returned directly from
`BankAccountsController.GetBankAccounts()`, so anything on it is public. Add a sibling:

```csharp
public class BankConnection : IOwnedByUser   // picks up the tenant filter + SaveChanges stamping
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public string Provider { get; set; } = "EnableBanking";
    public string AspspName { get; set; } = string.Empty;   // "OTP Bank"
    public string AspspCountry { get; set; } = "HU";

    // Bearer credential to somebody's bank account. Encrypted at rest, never serialised.
    public string SessionIdEncrypted { get; set; } = string.Empty;

    public DateTimeOffset ConsentGrantedAt { get; set; }
    public DateTimeOffset ConsentExpiresAt { get; set; }     // ~180 days; drives the re-consent prompt
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
}
```

and on `BankAccount`, only what is safe to publish:

```csharp
public int? BankConnectionId { get; set; }        // null = manually maintained, as today
public string? ProviderAccountId { get; set; }
public DateTimeOffset? BalanceAsOf { get; set; }  // null = never synced → render as unknown
public decimal? AvailableBalance { get; set; }    // interimAvailable
// existing Balance stays as closingBooked / manual
```

`BalanceAsOf` is what lets the UI honour the README's rule. A synced balance with no
timestamp is a lie waiting to happen.

**Manual accounts must keep working.** `BankConnectionId == null` means the user types the
number, exactly as today. Some banks won't be covered, and this is also the migration path.

### 9.2 Encryption

ASP.NET Core's Data Protection API (`IDataProtector`) is built in and sufficient. Keys must
be persisted (`PersistKeysToFileSystem` or equivalent) or every restart invalidates every
stored session — a genuinely nasty failure mode to debug, because it looks like the bank
revoked consent.

### 9.3 The abstraction — the anti-lock-in bit

```csharp
public interface IBankDataProvider
{
    Task<IReadOnlyList<Aspsp>> GetBanksAsync(string country, CancellationToken ct);
    Task<AuthStartResult> StartAuthAsync(string aspspId, string redirectUri, CancellationToken ct);
    Task<BankSession> CompleteAuthAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<ProviderAccount>> GetAccountsAsync(string sessionId, CancellationToken ct);
    Task<AccountBalances> GetBalancesAsync(string sessionId, string accountId, CancellationToken ct);
}
```

Five methods. `EnableBankingProvider` implements it; registered in `Program.cs` beside the
existing `AddScoped` registrations, with `AddHttpClient`. Nothing above this interface knows
which vendor is in use, and `AccountBalances` is *our* shape — normalise the Berlin Group
balance-type array at the boundary, not in the UI.

The cost of this abstraction is about an hour. The cost of not having it, if Enable Banking
goes the way of Nordigen, is a rewrite.

### 9.4 Sync

A hosted `BackgroundService` on a timer (a few times daily — balances don't change fast
enough to justify more, and providers rate-limit). On failure, record `LastSyncError` and
leave the old balance with its old `BalanceAsOf` — never zero it. Nothing in the sync path
should throw into the request pipeline.

### 9.5 Effort estimate

| Task | Estimate |
|---|---|
| Enable Banking signup, app registration, key handling | 0.5 d |
| JWT RS256 signing + typed `HttpClient` | 0.5 d |
| Connect flow: bank picker, redirect, callback endpoint | 1 d |
| `BankConnection` entity, migration, encryption | 0.5 d |
| Balance sync service + normalisation | 1 d |
| UI: connect, staleness, re-consent, errors | 1–1.5 d |
| Tests (tenant isolation on the new entity is mandatory) | 0.5 d |
| **Total** | **~5 days** |

Realistically the bank-side flows are where surprises live — MBH's three APIs, OTP's
private/business split. Assume 6–7 days end to end for something you'd let another person use.

---

## 10. What I'm confident about, and what I'm not

**Verified against multiple independent sources:** GoCardless/Nordigen closure (their own
page, a broken-tool issue, Firefly III's migration decision); the 180-day SCA rule (EBA RTS
amendment); Enable Banking's Restricted Production mechanics (their docs, a third-party
integration guide, the Firefly III discussion); the absence of a .NET SDK (their GitHub org
— 11 repos, C# appears only as a sample); Hungarian coverage and the OTP/MBH quirks (Enable
Banking's Hungary market docs).

**Not verified — treat as unknown, not as estimated:**
- **Actual production pricing.** Enable Banking, Salt Edge and Tink all quote privately.
  I found no public per-account euro figure and am not going to invent one. Phase 2 starts
  with an email to `info@enablebanking.com`.
- **Real-world reliability of specific Hungarian bank connections.** Documented coverage and
  a working connection are different claims. The only way to close this is to connect one
  account, which is free and takes an afternoon.
- **Whether Restricted Production has an account-count cap.** Documented as "accounts you
  link yourself"; no explicit numeric limit found.

**Recommended next step:** register an Enable Banking application and connect one real
Hungarian account before any code is written. It costs nothing, and it converts the two
biggest unknowns above into facts.

---

## Sources

Provider & market
- [Enable Banking](https://enablebanking.com) — [API reference](https://enablebanking.com/docs/api/reference/), [quick start](https://enablebanking.com/docs/api/quick-start/), [linked accounts / restricted usage](https://enablebanking.com/docs/api/linked-accounts/), [Hungary market notes](https://enablebanking.com/docs/markets/hu/), [FAQ](https://enablebanking.com/docs/faq/)
- [enablebanking/enablebanking-api-samples](https://github.com/enablebanking/enablebanking-api-samples) — C# sample, Apache 2.0
- [Enable Banking GitHub org](https://github.com/enablebanking)
- [Enable Banking plans & pricing (api-evangelist mirror)](https://github.com/api-evangelist/enable-banking/blob/main/plans/enable-banking-plans-pricing.yml)
- [GoCardless Bank Account Data docs](https://developer.gocardless.com/bank-account-data/overview) and [new signups disabled](https://bankaccountdata.gocardless.com/new-signups-disabled)
- [Free & indie open banking APIs (2026)](https://www.openbankingtracker.com/guides/free-open-banking-apis)
- [Best open banking API providers for developers (2026)](https://www.openbankingtracker.com/blog/best-open-banking-api-providers-developers-2026)
- [Open banking APIs in Europe — provider comparison](https://www.openbankingtracker.com/open-banking-apis-europe)
- [Comparing European providers: Plaid, TrueLayer, Tink, GoCardless](https://dev.to/johnfrandsen/comparing-european-open-banking-api-providers-in-2026-plaid-truelayer-tink-gocardless-125c)
- [Salt Edge account information docs](https://docs.saltedge.com/account_information/v5/)
- [Banks in Hungary — open banking directory](https://www.openbankingtracker.com/providers/country/hu)

Ecosystem validation
- [gocardless-to-csv#4 — "New signups for Bank Account Data are currently disabled"](https://github.com/adept/gocardless-to-csv/issues/4) (Dec 2025)
- [firefly-iii#10753 — Add Enable Banking as alternative to GoCardless](https://github.com/firefly-iii/firefly-iii/issues/10753)
- [Actual Budget — GoCardless setup](https://actualbudget.org/docs/advanced/bank-sync/gocardless/)

Regulation
- [EBA final report on the RTS amendment (90→180 days)](https://www.eba.europa.eu/sites/default/files/document_library/Publications/Draft%20Technical%20Standards/2022/EBA-RTS-2022-03%20RTS%20on%20SCA&CSC/1029858/Final%20Report%20on%20the%20amendment%20of%20the%20RTS%20on%20SCA&CSC.pdf)
- [90 becomes 180: EBA makes key SCA change](https://www.vixio.com/insights/pc-90-becomes-180-eba-makes-key-sca-change)
- [Berlin Group NextGenPSD2](https://www.berlin-group.org/psd2-access-to-bank-accounts)
- [PSD3 & PSR readiness and timeline](https://www.openbankingtracker.com/guides/psd3-psr-readiness), [Morrison Foerster analysis](https://www.mofo.com/resources/insights/260430-psd3-and-the-payment-services-regulation-key-developments)
- [AISP licence requirements](https://crassula.io/guides/licenses/aisp/)
- [QWAC certificates for PSD2 — pricing](https://www.actalis.com/qwac-certificates), [the eIDAS challenge for TPPs](https://blog.saltedge.com/the-eidas-challenge-for-tpps-under-psd2/)

Other
- [Revolut Open Banking API](https://developer.revolut.com/docs/open-banking/open-banking-api)
- [Wise personal API tokens](https://docs.wise.com/guides/developer/auth-and-security/personal-api-token)
