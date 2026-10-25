# Architecture

This is a classic ASP.NET Core MVC app structured by responsibility:

- Controllers (`SocksShoppingStore/Controllers`): HTTP endpoints and UI orchestration
- Middleware (`SocksShoppingStore/Middleware`): cross‑cutting pipeline concerns
- Services (`SocksShoppingStore/Services`) and options (`SocksShoppingStore/Options`): core app helpers and configuration options
- Data (`SocksShoppingStore/Data`): in‑memory product source
- Models (`SocksShoppingStore/Models`): domain and view models
- Views (`SocksShoppingStore/Views`): Razor UI; `wwwroot` for static assets

## Request Pipeline (Program.cs)

- HTTPS redirection and static files with cache headers (7 days)
- Request localization (EN/RU) via query string and cookie providers
- Free‑tier guard middleware to avoid accidental costs/deploy exposure
- Security headers middleware (CSP, frame, sniffing, referrer, permissions)
- Cookie policy (Lax SameSite, HttpOnly, Secure based on environment)
- Concurrency limiter middleware (in‑process cap, fast‑fail on saturation)
- Rate limiter (global default + named `api` policy) with friendly 429 body
- Session and MVC routing
- Operational endpoints: `GET /healthz`, `GET /robots.txt`, `GET /_status`

## Controllers

- `HomeController`: catalog listing with optional `q` search; localized texts
- `ProductsController`: product details (`/Products/Details?id=`)
- `ProductsApiController` (`[Route("api/products")]`): returns all products as JSON; `[EnableRateLimiting("api")]`
- `CartController`: session‑backed cart (add/remove/delete/clear)
- `CheckoutController`: checkout form → review → Stripe Checkout redirect (test mode) → return pages
- `StripeWebhookController`: `POST /webhooks/stripe` to process Stripe events (optional)
- `LegalController`: static legal pages (TOS/Privacy)
- `LocalizationController`: POST culture switch with safe local redirect

## UI Notes

- Navbar search: compact dropdown near the language selector
- Catalog filters: Price (min–max), Sort (buttons with icons)
- Lazy load: a “Load more” button fetches `/api/products?page=N&pageSize=M` and appends cards client‑side (no infinite scroll)

## Middleware

- `SecurityHeadersMiddleware`: sets CSP with per‑request nonce, denies framing, `nosniff`, strict referrer and permissions
- `FreeTierGuardMiddleware`: if enabled and not allowlisted path, returns 503 (blocks on Azure or full site per config)
- `ConcurrencyLimiterMiddleware`: bounding concurrent requests with `SemaphoreSlim`, returns 429 when saturated

## Services and Data

- `ProductCatalogLocalizer`: replaces product names/descriptions per culture (EN default, RU mapped)
- `IProductRepository`: abstraction for product storage
- `LegacyProductRepository`: wraps legacy in-memory list (default)
- `JsonProductRepository`: optional JSON file storage (configurable path)
- `StripeCheckoutService`: creates Stripe Checkout session (guards against non-test secret keys)
- `PaymentSessionStore`: in-memory store for Stripe session → order draft status
- `RequestMetrics`: rolling latency window + counts; exposed via `/_status`

## Configuration

`SocksShoppingStore/appsettings.json` provides:

- `FreeTier`: feature toggle, Azure detection, allowlist paths
- `RateLimiting`: `GlobalPerMinute`, `ApiPerMinute`
- `Concurrency`: `MaxConcurrentRequests`
- `Status`: `AllowIPs`, `LatencyWindowSize`
- `Legal`: controller name and contact email
- `Repository`: provider `InMemory|Json` and JSON path
- `Stripe`: publishable/secret keys, webhook secret, and return paths
- `HttpsRedirect:Enabled`: allow disabling HTTPS redirect (used by CI HTTP runs)

Testing details live in `docs/testing.md`.

## Payment state scope and verification plan — 2026-10-25

Status: SOURCE/DEMO GUARD VERIFIED LOCALLY; current demo contract unchanged. Audit F08 identifies
process-local PaymentSessionStore state, not a demonstrated defect in this QA
showcase. The agent guide explicitly specifies Stripe test-mode checkout;
StripeCheckoutService already rejects missing/non-sk_test_ keys before creating
the SDK client. No live checkout or durable order-processing contract is promised.

Payment drafts/completion flags and session-backed cart/order state can disappear
on restart, deployment or another process. Do not copy this store as a durable
payment receipt, fulfillment ledger or multi-instance synchronization mechanism.
Real commerce/persistence would require its own owner-selected data, hosting,
migration, idempotency and recovery design. The initially offered SQLite choice
was premature; no owner response is needed to preserve the existing demo.

Plan: keep in-memory semantics and existing key guards; add a minimal injectable
SDK session-creation boundary so real guard behavior can be tested without any
Stripe network, including on a regression; cover missing/live/non-test/case/prefix
keys plus allowed test-key delegation and propagated provider failure. Run the
documented fast Unit/Integration/API-Smoke gate with the real .NET8 runtime and
UI/browser/external Stripe disabled. Record commands, results and rollback in
Testing. Keep docs/source commits separate. No database, schema, checkout policy,
key, provider, publication or production feature is added. Revert the scoped
testability commit for source rollback; documentation retains the demo limits.

The SDK boundary receives the same validated key snapshot even if mutable options
change during logging; the refactor must not introduce a check/use gap. A
deterministic logging interleaving regression covers that invariant.

Public-only restore also reproduced NU1603: declared Stripe.net42.30.0 does not
exist in the public feed and resolves approximately to43.0.0. Align the reference
to the exact already-effective43.0.0; compare resolved package identities before
and after to prove no package graph upgrade. Keep that provenance correction in
its own executable commit, then repeat only affected/final fast gates. Do not
silence warnings or move to another SDK generation.

The default transport still uses the existing Stripe client; only that external call is injectable. All missing/non-test checks use one key snapshot, before the adapter, and in-memory payment/session behavior is unchanged. The original F08 remains a documented demo limitation, not an accepted production persistence design.

## Deterministic promo clock follow-up — 2026-10-25

Mode A. Preserve the existing QA demo, tax/shipping rules and inclusive promo expiry boundary (active at exactly ExpiresUtc, expired only afterward). Inject .NET TimeProvider with TimeProvider.System as the backward-compatible default; register the default explicitly for application composition. Read time once per expiry check. Add table-driven real TotalsCalculator cases for active/inactive, no expiry, before/equal/after expiry, alternate offsets, repeated clock advance/rollback, and retain existing totals assertions. No payment storage, currency, external Stripe or API behavior change. Final gate: focused tests and existing complete Unit/Integration/API-Smoke group once; docs/source commits separate. Rollback is scoped source revert; no data migration.
