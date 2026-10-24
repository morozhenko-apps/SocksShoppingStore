# Contributing

Thanks for your interest in improving SocksShoppingStore!

## Guidelines

- Language: code comments, commit messages, and PRs in English
- Style: concise, consistent naming, small focused changes
- Commits: Conventional Commits (e.g., `feat: add ru localization for products`)
- Tests: add tests when you introduce logic changes

## Development

- Clone and run locally with `dotnet run --project SocksShoppingStore`
- Keep `appsettings.json` defaults safe (limits, guard) for demos
- Prefer configuration over constants when adding toggles

## Pull Requests

- Describe the change, motivation, and risks
- Note any security or performance impact
- If UI, include screenshots or brief notes

## Testing Strategy and Execution

Use fast deterministic Unit/Integration/API checks for routine changes and keep
explicit browser/report/deployment work separate. [Testing](testing.md) owns the
executable filters and current workflow; [Setup](setup.md) owns configuration.
This guide does not create another CI or billing policy.

### Test Environments

| Concern | Local work | GitHub-hosted jobs |
| --- | --- | --- |
| Prerequisites | Explicit SDK, flags and selected server/browser | Workflow selects the host and installs prerequisites |
| Diagnostics | IDE/debugger and private local captures | Scoped workflow logs and report artifacts |
| Capacity and pricing | Owner machine and time budget | Provider/account policy; do not rely on copied minute-price or job-limit figures |
| Evidence | Record command, revision and result | Record exact job/source identity; source assertions alone are not a live run |

### Recommended Test Distribution

| Test Type                  | Recommended Primary Environment | Frequency                       |
| :------------------------- | :------------------------------ | :------------------------------ |
| **Unit Tests**             | GitHub Actions                  | On routed branch checks / supported PRs              |
| **Integration Tests**      | GitHub Actions                  | On routed branch checks / supported PRs              |
| **API Tests (Smoke)**      | GitHub Actions                  | On routed branch checks / supported PRs              |
| **UI Tests (Smoke)**       | Tagged release or explicit manual-UI job | On a release tag or deliberate `run_ui=true` dispatch |
| **Coverage/report publishing** | GitHub Actions release gate | On an annotated release tag   |
| **Azure deployment**       | GitHub Actions release gate     | On an annotated release tag     |
| **UI Tests (Full Regression)** | Local Machine               | Before major releases as needed |

### Running Tests Locally

Fast tests use the in-memory factory and do not require an external server:

```bash
USE_TEST_FACTORY=1 RUN_UI_TESTS=0 dotnet test \
  SocksShoppingStore.Tests/SocksShoppingStore.Tests.csproj -c Release \
  --filter "TestCategory=Unit|TestCategory=Integration|TestCategory=API-Smoke"
```

For intentional UI work first start the application and select its `BASE_URL`,
`RUN_UI_TESTS=1` and `USE_TEST_FACTORY=0` as described in Setup/Testing. Then select
`TestCategory=UI-Smoke` or the existing
`dotnet test --filter "TestCategory=UI-Regression"` explicitly.
An unfiltered `dotnet test` is a deliberate local all-category invocation, not
an equivalent of the ordinary PR job. A single category remains selectable with
`dotnet test --filter "TestCategory=Unit"`.

Scoped Unit/Integration and API-only coverage commands, all existing runsettings
and Allure naming/metadata/screenshot conventions are owned by
[Testing](testing.md#coverage-collection) and
[Categories and Conventions](testing.md#categories-and-conventions). The previous
`docs/test-conventions.md` pointer did not name a file in this checkout.
Adding UI-Smoke to a command labelled fast would require an external server and
browser; keep it separate. Use current-process PowerShell variables rather than
persistent `setx` examples when following Setup on Windows.
