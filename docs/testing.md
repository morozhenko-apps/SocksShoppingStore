# Testing

This repo uses a layered NUnit test suite (Unit / Integration / API / UI) with Allure reporting and coverage collection in CI.

Documentation/source review: 2026-10-24. Commands and CI lanes below follow the
tracked TestSettings and workflow; this review does not prove a live UI run,
report publication, runner assignment or deployment. [Setup](setup.md) owns
server/configuration details and current-session PowerShell variables.


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

Run only Unit, Integration and API-Smoke through the in-memory host:

```bash
USE_TEST_FACTORY=1 RUN_UI_TESTS=0 dotnet test \
  SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release \
  --filter "TestCategory=Unit|TestCategory=Integration|TestCategory=API-Smoke"
```

PowerShell uses current-process variables, not `setx`:

```powershell
$env:USE_TEST_FACTORY = '1'
$env:RUN_UI_TESTS = '0'
dotnet test SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release --filter "TestCategory=Unit|TestCategory=Integration|TestCategory=API-Smoke"
```

### UI Smoke (requires a running server)

First start the application as described in Setup. In the test session select
its actual `BASE_URL`, set `RUN_UI_TESTS=1`, `USE_TEST_FACTORY=0` and, for trusted
HTTPS or ordinary HTTP, `IGNORE_CERT_ERRORS=0`:

```bash
BASE_URL=http://127.0.0.1:5123 RUN_UI_TESTS=1 USE_TEST_FACTORY=0 \
  IGNORE_CERT_ERRORS=0 dotnet test \
  SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release \
  --filter "TestCategory=UI-Smoke"
```

For HTTPS use the server's printed URL after trusting the development certificate.
For HTTP keep `HttpsRedirect__Enabled=false` on the server. See Setup for the
complete PowerShell test session and relaxed CI-style HTTP server configuration.
UI tests are explicit browser/server work, separate from the ordinary fast gate.
The declared `UI-Regression` category can be selected for an intentional local
regression run; it is not part of the branch/PR fast filter.

## Categories and Conventions

Categories used across the suite:

- `Unit`
- `Integration`
- `API-Smoke`
- `UI-Smoke`
- `UI-Regression` (explicit local regression selection)

Tests also carry cross-cutting Positive, Negative, Boundary and Security labels.
Directory placement alone does not choose a runtime category.

Allure conventions:

- Test classes declare suite labels (e.g., `[AllureSuite("Unit")]`, `[AllureSuite("UI")]`).
- Each test includes an English `[AllureDescription(...)]` describing What/Steps/Expected.
- UI failures attach a screenshot (see `SocksShoppingStore.Tests/BaseTest.cs`).

## CI Workflow

Source authority: `.github/workflows/test-and-report.yml`; the public-runner
selection contract is `ci/test_runner_routing.py`. All three jobs declare
`ubuntu-latest`; dispatch accepts only `runner=hosted`. No workflow edit or live
run is part of documentation cleanup.

<a id="dev-branch"></a>

### Branch And Pull Request Checks

The `quick` job runs for pull requests targeting main and for routed/manual
branch `workflow_dispatch`. RunnerRouter owns automatic branch scheduling; the
workflow has no direct push trigger. It runs the fast Unit + Integration +
API-Smoke filter with `USE_TEST_FACTORY=1` and `RUN_UI_TESTS=0`, plus the Python
runner contracts. Ordinary branch/PR jobs do not collect or publish reports,
start UI smoke or deploy. A newer run for the same branch/PR cancels the old one.

<a id="main-branch-and-pull-requests"></a>

### Tagged Release Regression And Publication

The `main-tests` job is selected only by tag `workflow_dispatch`, then checks
that `vX.Y.Z` is an annotated tag on current origin/main HEAD. Tagged runs are
not auto-cancelled. This job:

- starts the app over HTTP on `http://127.0.0.1:5123` with
  `FreeTier__Enabled=false`, both rate limits set to 1000 and
  `HttpsRedirect__Enabled=false`;
- runs non-UI tests through the factory with `coverlet.runsettings`;
- runs `UI-Smoke` separately against the server without test-process coverage;
- aggregates Allure results, generates Allure and ReportGenerator HTML, and
  publishes the report site to the `gh-pages` branch;
- publishes/deploys the application through the existing Azure step;
- includes the separate running-server `coverlet.console` UI instrumentation
  step and result collection. Its existence is not a fresh coverage measurement.

Exact commands, permissions, secrets and ordering remain in the unchanged
workflow. A documentation change does not establish that any job succeeded.

### Manual UI run

Explicit `workflow_dispatch` with `run_ui=true` selects the separate hosted
`manual-ui` job, including on a branch. It starts the app and runs UI-Smoke; it
does not publish reports or deploy to Azure. It may run alongside the selected
branch/release job; normal fast verification does not need this input.

The [isolation record](PUBLIC_ACTIONS_RUNNER_ISOLATION_2026-10-18.md) retains the
historical risk and the distinction between current source and live runner proof.

## Coverage Collection

Coverage runsettings files live at repo root:

- `coverlet.runsettings`
- `coverlet.integration.runsettings`
- `coverlet.api.runsettings`

Coverage/report generation belongs to the tagged release lane, not ordinary
branch or PR checks. The additional `coverlet.console` step instruments the
running application for UI coverage; do not confuse it with covering the Selenium
test process. Local scoped collection remains available:

```bash
# Unit/Integration report with the existing settings
USE_TEST_FACTORY=1 RUN_UI_TESTS=0 dotnet test \
  SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release \
  --filter "TestCategory=Unit|TestCategory=Integration" --settings coverlet.runsettings

# API-only report
USE_TEST_FACTORY=1 RUN_UI_TESTS=0 dotnet test \
  SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release \
  --filter "TestCategory=API-Smoke" --settings coverlet.api.runsettings
```

Generated TestResults, coverage and Allure outputs remain local/CI artifacts
under the existing scoped ignores; preserve useful source/run/result summaries
rather than committing raw generated output. No blanket XML/text ignore is needed.

## UI Stability Notes

- UI tests assume the cookie consent banner may appear and will click `#cookie-accept` if present.
- Header readiness is detected via `a[aria-label='Cart']`.
- Prefer stable, semantic selectors when changing UI; avoid brittle DOM-dependent selectors.

## Repository Hygiene — 2026-10-24

Status: COMPLETE for the local cleanup scope; the plan was recorded before
implementation in `293a9e6`. Scope: local SocksShoppingStore cleanup
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
| M2: selective cleanup | COMPLETE (`17740f7`) | Delete exactly the three indexed diagnostics; add the two narrow ignores; retain live Razor imports and all application/test/CI/script/license/configuration inputs. |
| M3: documentation organization | COMPLETE | Correct missing-document pointers and identify optional unshipped local helpers. Replace setx examples with current-process PowerShell/Bash variables. Reconcile fast/tagged/manual-UI lanes and declared categories with source, retaining all report, coverage, runtime, future-scope and acceptance information. Keep the Russian README, architecture, agent adapters and dependency licenses unchanged. |
| M4: final validation | COMPLETE | All five existing Python CI routing contracts passed once; 95 hygiene/preservation checks passed; git diff --check passed. Exact deletions/blobs/bytes, live imports, ignore controls, original anchors/local links and retained contracts verified. Full .NET/UI/coverage/deployment gates are not required for this docs/ignore/unused-output-only scope. |

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

### Cleanup Validation Evidence

Executed on 2026-10-24:

- In the repository root, `python3 ci/test_runner_routing.py`: exit 0, all five
  existing source-routing contracts passed, with no failures or skips. This is
  the applicable fast STABLE source-contract suite, executed once after the
  planned implementation; it does not observe a real hosted runner assignment.
- From the local audit workspace,
  `python3 /home/morozhenko/Documents/Codex/2026-10-24/x20/work/socks-cleanup/validate_cleanup.py`:
  exit 0, 95 hygiene/preservation checks passed. Exact Git commands/statuses are
  recorded in that workspace's `outputs/SOCKS_CLEANUP_VALIDATION.json`.
- `git diff --check 18ea7a6ba1a4739d4d4ad0aa9aa5a0a7fefda864`: exit 0.
- Four generated-path controls match the intended ignores; ten text, nested,
  Razor, test, coverage-setting, CI and license controls remain outside them
  (`git check-ignore --no-index`, expected exits 0 and 1 respectively).
- The live `_ViewImports.cshtml` is byte-identical to the baseline and retains
  all five scratch-copy directives plus its two additional imports. No unique
  temporary directive was removed. Full diagnostic bytes match their retained
  immutable Git hashes; no raw logs were copied into replacement artifacts.
- Agent runtime/API/cart/payment/security/configuration contracts, setup
  contracts, implemented/future scope, Allure/UI conventions and the historical
  runner-risk/rollback record remain intact. Original heading anchors remain;
  no new broken local Markdown target/anchor was found. Corrected commands use
  the existing TestSettings precedence, category declarations and workflow.

The cleanup changes only seven existing English documentation files, two narrow
ignore entries and the three unused diagnostic files. Architecture, Russian
README, agent adapters, dependency licenses, runtime/configuration, tests,
workflows and coverage inputs are unchanged. No new product tests were added.
Full .NET/NUnit runtime, browser/UI, coverage, build, known-flaky, provider/CI,
publication and deployment gates were NOT RUN. PowerShell/server examples were
reviewed against source, not executed; no app process was started. No runtime
stability, current hosted quota/pricing, published report or production claim is
made. All work remains local on dev; no push, main promotion, tag, history
rewrite, external message or other-repository change was performed.
