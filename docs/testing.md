# Testing

This repo uses a layered NUnit test suite (Unit / Integration / API / UI) with Allure reporting and coverage collection in CI.

## Test Project Structure

All tests live in `SocksShoppingStore.Tests`:

- `Unit/` — controller/service/model unit tests.
- `Integration/` — integration tests, typically via `WebApplicationFactory<Program>`.
- `API/` — API “smoke” tests for `/api/*` routes.
- `UI/` + `PageObjects/` — Selenium UI smoke tests.

## Test Settings

Tests read settings from (in order of precedence):

- Environment variables
- `SocksShoppingStore.Tests/appsettings.Test.json` (copied to the test output directory)

Supported keys:

- `BASE_URL` / `BaseUrl` — base URL for UI/API tests (e.g., `http://127.0.0.1:5123`)
- `RUN_UI_TESTS` / `RunUi` — enable UI tests (`1/true`)
- `IGNORE_CERT_ERRORS` / `IgnoreCertErrors` — allow insecure localhost only when explicitly enabled
- `USE_TEST_FACTORY` / `UseTestFactory` — run Integration/API against in-memory host via `WebApplicationFactory<Program>`

## Running Tests Locally

### Fast (no external server)

Integration/API tests can run without starting the app when `USE_TEST_FACTORY=1`.

- PowerShell:
  - `setx USE_TEST_FACTORY 1`
  - `dotnet test SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release --filter "TestCategory=Unit|TestCategory=Integration|TestCategory=API-Smoke"`

### UI Smoke (requires a running server)

UI tests require the application to be running and reachable by `BASE_URL`.

- Start the app:
  - `dotnet run --project SocksShoppingStore/SocksShoppingStore.csproj --launch-profile https`
- Enable UI tests:
  - `setx RUN_UI_TESTS 1`
- Run UI smoke:
  - `dotnet test SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release --filter "TestCategory=UI-Smoke"`

If you run the server over HTTP, set:

- `setx BASE_URL http://127.0.0.1:5123`
- `setx IGNORE_CERT_ERRORS 0`
- `setx USE_TEST_FACTORY 0`

## Categories and Conventions

Categories used across the suite:

- `Unit`
- `Integration`
- `API-Smoke`
- `UI-Smoke`

Allure conventions:

- Test classes declare suite labels (e.g., `[AllureSuite("Unit")]`, `[AllureSuite("UI")]`).
- Each test includes an English `[AllureDescription(...)]` describing What/Steps/Expected.
- UI failures attach a screenshot (see `SocksShoppingStore.Tests/BaseTest.cs`).

## CI Workflow

Workflow file: `.github/workflows/test-and-report.yml`.

### `dev` branch

- Runs fast suite: `Unit + Integration + API-Smoke`.
- Runs scoped coverage for Integration and API.
- Uploads Allure raw results and coverage artifacts.

### `main` branch and Pull Requests

- Starts the app in **HTTP** mode on `http://127.0.0.1:5123` with relaxed limits:
  - `FreeTier__Enabled=false`
  - `RateLimiting__GlobalPerMinute=1000`
  - `RateLimiting__ApiPerMinute=1000`
  - `HttpsRedirect__Enabled=false`
- Runs non-UI tests with coverage (`coverlet.runsettings`).
- Runs `UI-Smoke` separately (no coverage).
- Aggregates `**/allure-results` into a single Allure results folder.
- Generates:
  - Allure HTML report
  - Coverage HTML report (ReportGenerator)
- Publishes reports to GitHub Pages (branch `gh-pages`).

### Manual UI run

Workflow supports `workflow_dispatch` input `run_ui=true` to run UI smoke.

## Coverage Collection

Coverage runsettings files live at repo root:

- `coverlet.runsettings`
- `coverlet.integration.runsettings`
- `coverlet.api.runsettings`

In CI, coverage is generated for non-UI tests by default. There is an additional CI step that can instrument the running app with `coverlet.console` to collect “UI coverage”.

## UI Stability Notes

- UI tests assume the cookie consent banner may appear and will click `#cookie-accept` if present.
- Header readiness is detected via `a[aria-label='Cart']`.
- Prefer stable, semantic selectors when changing UI; avoid brittle DOM-dependent selectors.

## Repository Hygiene — 2026-10-24

Status: PLANNED before implementation. Scope: local SocksShoppingStore cleanup
on the existing clean dev branch; no other repository or provider is included.
No application/test/CI behavior, API, payment, session, security, legal, dependency
or published product contract changes are planned. No push/main promotion,
release, deployment, app/device process or external message is authorized.

### Analysis and diagnostic provenance

The two tracked .logs snapshots were introduced in `28b9b6c`;
`tmp_viewimports.txt` entered Git in `0780cb5`. No code, tests, workflow or
tracked script consumes these exact paths. The five temporary Razor directives
are all present in the seven-directive live
`SocksShoppingStore/Views/_ViewImports.cshtml`, which remains unchanged.
The files total 69,602 bytes. Full original bytes remain recoverable from
immutable revision `18ea7a6ba1a4739d4d4ad0aa9aa5a0a7fefda864`; the minimized index below avoids copying
raw request/device/process output into new documentation or archives.

| Historical diagnostic path | Bytes | Lines | Original Git blob |
| --- | ---: | ---: | --- |
| `.logs/main.log` | 47694 | 566 | `61a5ac118ce2e18cd92379eb753f0ee86fa40e76` |
| `.logs/pages.txt` | 21693 | 309 | `53ec168d899e8157a3d72ad889d61d4ff5c8abbc` |
| `tmp_viewimports.txt` | 215 | 5 | `e126f47e425f01add0a1133f9ab645d4f20c3f22` |

The existing `.logs/` rule already matches both tracked snapshots, but ignores
do not remove indexed files. Add only root-anchored `/tmp_viewimports.txt` and
`/app.log`: the latter is stdout/stderr produced by the existing release
workflow. Preserve all existing ignore rules, including machine-local scripts;
do not add blanket *.txt/XML rules or delete any other ignored/local material.

### Milestones and preservation map

| Stage | State | Scope and preservation |
| --- | --- | --- |
| M1: analysis/plan | COMPLETE | Read guides, source TestSettings, workflow/routing contracts and imports; inventory exact files and current documentation gaps. |
| M2: selective cleanup | PLANNED | Delete exactly the three indexed diagnostics; add the two narrow ignores; retain live Razor imports and all application/test/CI/script/license/configuration inputs. |
| M3: documentation organization | PLANNED | Correct missing-document pointers and identify optional unshipped local helpers. Replace setx examples with current-process PowerShell/Bash variables. Reconcile fast/tagged/manual-UI lanes and declared categories with source, retaining all report, coverage, runtime, future-scope and acceptance information. Keep the Russian README, architecture, agent adapters and dependency licenses unchanged. |
| M4: final validation | PLANNED | Run the existing five Python CI routing contracts once; check exact deletions/blobs/bytes, live-import preservation, ignore controls, original anchors/local links and retained contracts; run git diff --check. Full .NET/UI/coverage/deployment gates are not required for this docs/ignore/unused-output-only scope. |

Documentation owners remain the five core guides; contributing instructions
and the dated runner-isolation record have distinct supporting roles. No new
parallel guide or canonical roadmap file is needed. Historical runner risk and
initial divergence are retained as dated evidence; current source-only checks
must not claim actual hosted PR/router assignment or production verification.
Correct obsolete examples without changing the underlying workflow or runtime.

Rollback: revert the selective diagnostic/ignore commit to restore exact files
and previous ignores; revert documentation commits independently. Git history
and existing clones retain original raw diagnostics; this is not a privacy purge
or a history rewrite. Capture no raw log content in replacement outputs.
