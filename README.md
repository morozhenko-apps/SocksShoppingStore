# SocksShoppingStore

A simple ASP.NET Core MVC demo app for browsing fun “job-themed” socks, adding them to a session-backed cart, and exploring basic production-friendly concerns (security headers, rate limiting, concurrency caps, metrics, localization).

- Quick tech overview: ASP.NET Core 8 MVC, Razor Views, in-memory product repository, session cart, EN/RU localization.
- Safety: CSP and secure headers, free-tier guard, global and API rate limits, simple concurrency limiter.
- Operability: health endpoint, lightweight request metrics, configuration via `appsettings.json`.

## Quick Start

- Prerequisites: .NET SDK 8.0+
- Run: `dotnet run --project SocksShoppingStore`
- Open: `https://localhost:5001` (or the URL from the console)
- Health: `GET /healthz` → `OK`

## Repository Map

- `SocksShoppingStore/Controllers` — MVC controllers (`Home`, `Products`, `ProductsApi`, `Cart`, `Legal`, `Localization`).
- `SocksShoppingStore/Middleware` — `SecurityHeaders`, `FreeTierGuard`, `ConcurrencyLimiter`.
- `SocksShoppingStore/Services` — `ProductCatalogLocalizer`, request `RateOptions`, basic `RequestMetrics`.
- `SocksShoppingStore/Data` — in-memory `ProductRepository`.
- `SocksShoppingStore/Models` — `Sock`, `ShoppingCart`, `CartItem`, `ErrorViewModel`.
- `SocksShoppingStore/Views` — Razor Views (UI), `wwwroot` for static assets.
- `SocksShoppingStore/Program.cs` — pipeline, DI, localization, sessions, rate limiting, metrics.
- `docs/` — maintained project documentation and the migrated legacy roadmap context.
- `.codex/` — ignored machine-local historical material, if present; not a required project input.

## Documentation

| Owner | Scope |
| --- | --- |
| [Agent guide](docs/SOCKS_AGENT_GUIDE.md) | Handoff, runtime invariants, routes and code entrypoints |
| [Overview](docs/overview.md) | Purpose, implemented scope, constraints and explicitly future roadmap |
| [Architecture](docs/architecture.md) | Pipeline, API/controllers, localization, guards, metrics and storage |
| [Setup](docs/setup.md) | Local run, configuration, test-mode Stripe and current-session test settings |
| [Testing](docs/testing.md) | Test categories, fast/UI commands, CI lanes, coverage and reporting |
| [Contributing](docs/contributing.md) | Change/review discipline and test selection |
| [Runner isolation record](docs/PUBLIC_ACTIONS_RUNNER_ISOLATION_2026-10-18.md) | Dated risk/decision and source-versus-live verification limits |

API, localization, security/limits and metrics are owned by Architecture and the
agent guide; separate api/products, internationalization, security-and-limits,
metrics and roadmap pages are not present in this checkout. Use the Overview's
future-scope section rather than treating a missing roadmap file as authority.
The Russian entrypoint is [README.ru.md](README.ru.md); the maintained detailed
guides are English, with no additional docs/*/*.ru.md copies in this tree.

<a id="local-scripts-windowspowershell"></a>

### Optional Machine-Local Helpers

The following historical PowerShell helpers are not shipped in this checkout:
`scripts/` is ignored machine-local tooling. Keep these examples only for an
operator who already has compatible helpers; standard tracked run/test commands
are in Setup and Testing. No helper script is required by the application or CI.

- Start app with health check (non-blocking):
  - `./scripts/start-app.ps1 -Url http://127.0.0.1:5123 -Configuration Release`
  - Logs: `.logs/app-local.log` (stdout), `.logs/app-local.err.log` (stderr), PID: `.logs/app.pid`
- Stop app (uses PID file, with fallback scan):
  - `./scripts/stop-app.ps1` (add `-Force` if needed)
- Run tests like CI:
  - Fast (dev): `./scripts/run-tests.ps1 -Suite dev-fast`
  - Full (main): `./scripts/run-tests.ps1 -Suite main-full -BaseUrl http://127.0.0.1:5123`
  - UI smoke: `./scripts/run-tests.ps1 -Suite ui-smoke -BaseUrl http://127.0.0.1:5123`
  - Results: `.logs/TestResults/<Suite>`, console log: `.logs/test-<Suite>.log`

Generated helper output under `.logs/` is local diagnostic material, not source
or a permanent test fixture. Preserve useful sanitized findings in the owning
guide before discarding a capture.

## License

See `LICENSE`.
## CI / Reports

- CI workflow: `.github/workflows/test-and-report.yml`.
- This public repository uses **GitHub-hosted Linux** for all pull requests,
  routed branch checks and release-tag jobs. RunnerRouter dispatches with
  `runner=hosted`; it never selects the owner's local self-hosted runners.
  The source-enforced guard is `ci/test_runner_routing.py`.
- Branch and pull-request runs execute only the fast Unit + Integration +
  API-Smoke gate. A newer run for the same branch or pull request cancels the
  obsolete run.
- Full release regression, release UI smoke, coverage/report publication and
  Azure deployment run only for an annotated `vX.Y.Z` tag whose commit is current
  `main` HEAD. Release-tag runs are not auto-cancelled.
- Explicit `workflow_dispatch` with `run_ui=true` can additionally run the
  separate hosted manual-UI job; that job does not publish reports or deploy.
- Recorded Allure report location: [GitHub Pages](https://rudExtremo.github.io/SocksShoppingStore).
  Its current publication is not reverified by documentation or local hygiene checks.

Badges:

![CI Dev](https://github.com/rudExtremo/SocksShoppingStore/actions/workflows/test-and-report.yml/badge.svg?branch=dev)


## Demo payment durability boundary

Checkout is test-only: the service rejects missing/non-sk_test_ secrets before the SDK adapter. PaymentSessionStore and session cart/order state are process-local and can be lost on restart, deployment or another instance. They are not durable payment receipts or fulfillment state. Real-commerce persistence/hosting/idempotency needs a separate product/architecture decision. Current guard and package provenance evidence is in [Testing](docs/testing.md#demo-checkout-and-dependency-provenance--2026-10-25).
