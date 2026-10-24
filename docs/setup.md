# Setup

## Requirements

- .NET SDK 8.0+

## Local Run

- From repo root: `dotnet run --project SocksShoppingStore`
- Open the printed HTTPS URL (e.g., `https://localhost:5001`)
- Health probe: `GET /healthz`

## Configuration

Edit `SocksShoppingStore/appsettings.json`:

- `FreeTier`: enable guard and allowlist paths
- `RateLimiting`: `GlobalPerMinute`, `ApiPerMinute`
- `Concurrency`: `MaxConcurrentRequests`
- `Status`: `AllowIPs` for `/_status`, `LatencyWindowSize`
- `Legal`: controller name and contact email

For development overrides, use `appsettings.Development.json`.

## Payments (Stripe Checkout)

Checkout uses Stripe Checkout in **test mode**.

Configure `SocksShoppingStore/appsettings.json` → `Stripe`:

- `PublishableKey` — `pk_test_*`
- `SecretKey` — **must** be `sk_test_*` (non-test keys are rejected)
- `WebhookSecret` — optional (only needed if you use Stripe webhooks locally)

If Stripe keys are missing/invalid, the checkout flow falls back to a local “finalize order” demo path.

## Localization

- Default culture: `en` (`en-US` for numbers)
- Supported UI cultures: `en`, `ru`
- Switch via cookie POST (`/Localization/Set`) or query `?culture=en|ru`

## SSL Note

ASP.NET dev cert is required for HTTPS. If missing, install with:

- `dotnet dev-certs https --trust`

## Test Settings (local and CI)

This repo centralizes test configuration to avoid hardcoding URLs and flags in test code.

- File: `SocksShoppingStore.Tests/appsettings.Test.json` is copied to test bin and can define:
  - `BaseUrl`: default UI/API base (e.g., `https://localhost:7068` locally)
  - `RunUi`: enable/disable Selenium UI tests by default
  - `IgnoreCertErrors`: allow insecure localhost only when needed (CI)
  - `UseTestFactory`: use `WebApplicationFactory<Program>` for Integration/API tests (recommended)

- Environment variables override JSON:
  - `BASE_URL` — overrides `BaseUrl`
  - `RUN_UI_TESTS` — `1/true` to enable UI tests
  - `IGNORE_CERT_ERRORS` — `1/true` to ignore TLS errors (CI only)
  - `USE_TEST_FACTORY` — `1/true` to run Integration/API via in-memory test host

## Current-Session Test Examples

Environment variables must reach the test process in the same session. In
PowerShell use `$env:NAME = 'value'`; `setx` only changes future shells and is
not used in the commands below.

### Fast tests without an external server

Bash:

```bash
USE_TEST_FACTORY=1 RUN_UI_TESTS=0 dotnet test \
  SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release \
  --filter "TestCategory=Unit|TestCategory=Integration|TestCategory=API-Smoke"
```

PowerShell:

```powershell
$env:USE_TEST_FACTORY = '1'
$env:RUN_UI_TESTS = '0'
dotnet test SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release --filter "TestCategory=Unit|TestCategory=Integration|TestCategory=API-Smoke"
```

### Explicit UI smoke

UI tests always require a running application at `BASE_URL`; the in-memory
factory is for Integration/API tests, not the Selenium browser. For HTTPS first
trust the development certificate with `dotnet dev-certs https --trust`, start
`dotnet run --project SocksShoppingStore/SocksShoppingStore.csproj --launch-profile https`
in the server session, then use its printed URL in the test session.

PowerShell test session (replace the URL with the running app's address):

```powershell
$env:BASE_URL = 'https://localhost:7068'
$env:RUN_UI_TESTS = '1'
$env:USE_TEST_FACTORY = '0'
$env:IGNORE_CERT_ERRORS = '0'
dotnet test SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release --filter "TestCategory=UI-Smoke"
```

For CI-style HTTP instead select `http://127.0.0.1:5123`, keep certificate-error
ignoring off, and start the app in that server session with the workflow's
relaxed demo settings:

```bash
FreeTier__Enabled=false RateLimiting__GlobalPerMinute=1000 \
  RateLimiting__ApiPerMinute=1000 HttpsRedirect__Enabled=false \
  dotnet run --project SocksShoppingStore/SocksShoppingStore.csproj \
  --urls http://127.0.0.1:5123 --no-launch-profile
```

Change only `BASE_URL` in the test session; retain `RUN_UI_TESTS=1`,
`USE_TEST_FACTORY=0` and `IGNORE_CERT_ERRORS=0`. Intentional insecure-localhost
certificate testing remains an explicit opt-in; do not silently enable it.
These are operator examples, not server/device actions performed by cleanup.
CI lane selection, coverage commands and conventions belong in [Testing](testing.md).
