# SOCKS_AGENT_GUIDE

This document is a **single, high-signal reference** for coding agents working in this repository.

It focuses on:

- Project intent and constraints
- Code map and key flows
- Runtime guards and configuration
- Tests/CI expectations
- Practical “gotchas” that affect changes

Canonical docs live in `docs/`:

- `docs/overview.md`
- `docs/setup.md`
- `docs/architecture.md`
- `docs/testing.md`
- `docs/SOCKS_AGENT_GUIDE.md` (this file)

Contributor guidance is in `docs/contributing.md`; dated CI routing evidence and
its remaining live-assignment gate are in
`docs/PUBLIC_ACTIONS_RUNNER_ISOLATION_2026-10-18.md`. Current source/workflow
contracts take precedence over old operational snapshots.


---

## 1) What this project is

SocksShoppingStore is an ASP.NET Core 8 MVC demo app:

- A small product catalog (“job-themed” socks)
- A session-backed shopping cart with AJAX interactions
- A demo checkout flow that redirects to **Stripe Checkout** in **test mode**
- A set of production-friendly rails for free-tier hosting (CSP, rate limit, concurrency cap, optional FreeTier guard)
- A test suite (NUnit + Selenium UI tests) with Allure reporting and CI publishing

The repository is designed as a **QA/automation showcase**: the feature set is intentionally small, but the app includes “real-world” concerns that create useful test scenarios.

---

## 2) Quick run commands

### Run the app

- `dotnet run --project SocksShoppingStore`

Open the printed URL.

Health:

- `GET /healthz` → `OK`

### Run tests

[Testing](testing.md) owns the complete commands, environment precedence and
fast/tagged/manual-UI lanes. Bash quick references:

```bash
# Fast Unit/Integration/API suite; no external server or browser
USE_TEST_FACTORY=1 RUN_UI_TESTS=0 dotnet test \
  SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release \
  --filter "TestCategory=Unit|TestCategory=Integration|TestCategory=API-Smoke"

# Explicit UI smoke; first start the app at the selected BASE_URL
BASE_URL=http://127.0.0.1:5123 RUN_UI_TESTS=1 USE_TEST_FACTORY=0 \
  IGNORE_CERT_ERRORS=0 dotnet test \
  SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release \
  --filter "TestCategory=UI-Smoke"
```

For PowerShell use `$env:NAME = 'value'` in the current session as documented in
Setup. `setx` persists future-shell values and does not prepare the current test
process. Keep explicit UI/browser selection separate from the ordinary fast gate.

---

## 3) High-level architecture

### Project layout

- `SocksShoppingStore/` — ASP.NET Core MVC application
- `SocksShoppingStore.Tests/` — NUnit test project (Unit/Integration/API/UI)
- `.github/workflows/test-and-report.yml` — CI pipeline (tests + coverage + Allure + GitHub Pages)
- `docs/` — canonical documentation

### Request pipeline

The authoritative pipeline is in `SocksShoppingStore/Program.cs`:

- Optional HTTPS redirect (can be disabled via `HttpsRedirect:Enabled=false`)
- Static files with cache headers (7 days)
- Routing
- Request localization (`en`/`ru`) via query and cookie providers
- `FreeTierGuardMiddleware` (optional 503 gate)
- `SecurityHeadersMiddleware` (CSP with per-request nonce)
- Cookie policy defaults
- `ConcurrencyLimiterMiddleware` (fast-fail concurrency cap)
- ASP.NET Rate Limiter:
  - Global fixed-window per-IP limiter
  - Named `api` per-IP limiter
  - Friendly `429` body
- Session
- MVC routes
- Operational endpoints:
  - `/healthz`
  - `/robots.txt`
  - `/_status` (IP allowlist)

---

## 4) Entry points (HTTP)

### UI

- Catalog:
  - `GET /` (`HomeController.Index`)
- Product details:
  - `GET /Products/Details?id=<int>` (`ProductsController.Details`)
- Cart:
  - `GET /Cart` (`CartController.Index`)
- Checkout:
  - `GET /Checkout` (`CheckoutController.Index`)
  - `POST /Checkout` (validate + store draft in session)
  - `GET /Checkout/Review` (view draft)
  - `POST /Checkout/Confirm` (redirect to Stripe Checkout OR fallback finalize)
  - `GET /Checkout/ReturnSuccess` (finalize after success)
  - `GET /Checkout/PaymentFailed` (cancel)
  - `GET /Checkout/ThankYou` (display last order)

### JSON API

- Products:
  - `GET /api/products` (`ProductsApiController.GetAllProducts`)
  - `GET /api/products/{id}` (`ProductsApiController.GetProduct`)

### Operational

- Health:
  - `GET /healthz`
- Robots:
  - `GET /robots.txt`
- Status/metrics:
  - `GET /_status` (IP allowlist)

### Webhooks

- Stripe webhook:
  - `POST /webhooks/stripe` (`StripeWebhookController.Handle`) — optional; verifies Stripe signature if `Stripe:WebhookSecret` is configured

---

## 5) Core application flows

### 5.1 Catalog browsing

Controller: `HomeController.Index`.

Inputs:

- `q` — substring search in product name/description
- `sort` — `name_asc|name_desc|price_asc|price_desc`
- `minPrice` / `maxPrice`
- `page` / `pageSize`

Important:

- Products are localized based on `CultureInfo.CurrentUICulture`.

UI implementation:

- The catalog grid uses a **Load more** button.
- Client-side script is `SocksShoppingStore/wwwroot/js/site.js`:
  - Fetches `/api/products?page=N&pageSize=M&q=...&sort=...&minPrice=...&maxPrice=...`
  - Appends cards into `#catalog-grid`.

### 5.2 Products API: caching / ETag

Controller: `ProductsApiController`.

Behavior:

- Filtering/sorting/paging as above.
- Response sets:
  - `ETag` computed from JSON SHA-256
  - `Last-Modified` (a process-wide timestamp)
  - `Cache-Control: public, max-age=60`
  - `X-Total-Count` (before paging)
- Supports conditional requests:
  - `If-None-Match` and `If-Modified-Since` → `304`

### 5.3 Cart

Storage:

- Session key: `Cart` (serialized `ShoppingCart`)

Implementation notes:

- `ShoppingCart.RemoveItem` does **not** decrement below 1 (use `DeleteItem` to remove completely).
- `ShoppingCart.SetQuantity` deletes item if `quantity <= 0`.

Controller: `CartController`.

AJAX behavior:

- Add to cart links have `js-add-to-cart` and are intercepted by `wwwroot/js/site.js`.
- AJAX uses `Accept: application/json` to get a JSON response.
- JSON responses include cart summary and (if applicable) updated item subtotal.

ReturnUrl rules:

- `AddToCart` prefers explicit `returnUrl` if local.
- Else it uses safe `Referer` fallback (not from `/Cart`).

### 5.4 Checkout (Stripe test-mode)

Controller: `CheckoutController`.

Session keys:

- `OrderDraft` — `Order` created from checkout form + current cart
- `LastOrder` — finalized order shown on Thank You

Stripe session flow:

- `POST /Checkout/Confirm`:
  - Attempts to create a Stripe Checkout session using `StripeCheckoutService`.
  - If successful:
    - Saves draft in `PaymentSessionStore` keyed by Stripe session id
    - Redirects user to `session.Url`
  - If Stripe fails (missing key, invalid key, network, etc.):
    - Falls back to local finalize:
      - `LastOrder` set
      - `Cart` cleared
      - `OrderDraft` cleared
      - Redirect to `ThankYou`

Stripe constraints:

- `StripeCheckoutService` refuses non-test secret keys.
  - `Stripe:SecretKey` must start with `sk_test_`.

Webhook:

- `StripeWebhookController` validates signature with `Stripe:WebhookSecret`.
- It marks a session as completed in `PaymentSessionStore` on `checkout.session.completed`.

---

## 6) Localization

Supported UI cultures: `en`, `ru`.

Culture providers:

- Query string provider
- Cookie provider

Culture switch endpoint:

- `POST /Localization/Set` (writes `CookieRequestCultureProvider.DefaultCookieName`)

Product localization:

- Implemented via `Services/ProductCatalogLocalizer`.
- RU translations are a simple in-memory map by product id.

---

## 7) Runtime guards and operational limits

### Security headers / CSP

Middleware: `Middleware/SecurityHeadersMiddleware.cs`.

- Sets CSP with a per-request nonce and restricts scripts to `self` and `nonce-...`.
- **Avoid introducing inline scripts** unless they are wired to the nonce mechanism.

### FreeTier guard

Middleware: `Middleware/FreeTierGuardMiddleware.cs`.

- When enabled, can return `503` for most paths except allowlisted paths.
- Config in `SocksShoppingStore/appsettings.json` → `FreeTier`.

### Rate limiting

Configured in `Program.cs` via `AddRateLimiter`.

- Global per-IP fixed window (default config: `RateLimiting:GlobalPerMinute`)
- API per-IP fixed window (policy `api`, config: `RateLimiting:ApiPerMinute`)

### Concurrency limiting

Middleware: `Middleware/ConcurrencyLimiterMiddleware.cs`.

- In-process `SemaphoreSlim`.
- Fast fails with `429` when saturated.

### Metrics

- Collector: `RequestMetrics` (rolling latency window)
- Endpoint: `GET /_status`
  - Allowed IPs: `Status:AllowIPs`
  - Returns counts and p50/p95/p99 latency

---

## 8) Configuration reference (appsettings)

Main file: `SocksShoppingStore/appsettings.json`.

- `FreeTier`:
  - `Enabled`, `BlockAllTraffic`, `BlockOnAzure`, `AllowPaths`
- `RateLimiting`:
  - `GlobalPerMinute`, `ApiPerMinute`
- `Concurrency`:
  - `MaxConcurrentRequests`
- `Status`:
  - `AllowIPs`, `LatencyWindowSize`
- `Repository`:
  - `Provider`: `InMemory` or `Json`
  - `Json:Path`
- `Stripe`:
  - `PublishableKey`, `SecretKey`, `WebhookSecret`, `UseTestModeBanner`, `SuccessReturnPath`, `CancelReturnPath`
- `HttpsRedirect:Enabled`:
  - Set `false` to run CI-style HTTP without redirect

---

## 9) Tests and CI expectations

Authoritative references:

- `docs/testing.md`
- `.github/workflows/test-and-report.yml`

Key expectations for changes:

- If behavior changes, extend tests accordingly.
- UI changes should preserve stable selectors and keep UI smoke tests passing.
- Avoid introducing flakiness (prefer deterministic waits / explicit conditions).

---

## 10) Common pitfalls for agents

- **Do not add inline scripts** (CSP will block them). Prefer `wwwroot/js/site.js`.
- Remember that `/_status` is IP-allowlisted — tests should not assume public access.
- Checkout is a demo flow: Stripe is best-effort, and there is a local fallback by design.
- Session keys (`Cart`, `OrderDraft`, `LastOrder`) are the primary “state store”.
- CI runs the app in HTTP mode and relaxes limits; local dev often uses HTTPS.

---

## 11) When you change something

Minimum checklist:

- Update or add tests (Unit/Integration/API/UI as appropriate).
- If public behavior changes (routes, JSON contract, config keys), update canonical docs in `docs/`.

