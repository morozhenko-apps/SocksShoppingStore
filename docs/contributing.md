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

## Unused editor/model preset cleanup — 2026-10-24

Mode A. Owner explicitly authorized removing unnecessary IDE/model configuration
from previously processed repositories. Baseline `b0e788fd8976f9598ce9fa647a45a194449c1932`, clean local
dev. This supersedes earlier retention/model-adapter decisions, without reopening
product, test, security, data or release scope.

Remove only: `.github/agents/ORCHESTRATOR.md`, `.github/agents/SUB AGENT.md`.
No source/workflow/build consumer needs these presets. Useful rules stay in
`docs/contributing.md`; current entrypoint references are corrected. Old audit/rollout
path names remain historical evidence, not dangling runtime dependencies. Retired
model selections, forced delegation and obsolete test quotas are not copied into
another active preset. Deleted originals remain recoverable from baseline Git.

Scope excludes application/tests, CI workflows, dependencies, legal/public inputs,
provider/secret/data operations, ignored/local state, push/promotion or deployment.
No new Markdown policy owner or IDE/model dependency is introduced. Keep existing
useful ignores and tool-neutral contributor/run/debug instructions.

Milestones:
1. [x] Inspect presets, canonical owner rules, tracked consumers and clean state;
   preserve baseline hashes and original Markdown outside the checkout.
2. [x] Record removal rationale and retain neutral useful rules before deletion.
3. [x] Remove unreferenced presets and scoped metadata in a separate config commit.
4. [x] Validate exact changes/deletions, protected hashes, links, neutral rules and
   no runtime/CI references; update evidence in a separate docs handoff commit.

Rollback: normal inverse scoped commits or restore exact removed files from the
baseline. No runtime or data rollback is needed. Historical report claims apply
to their original checkpoints; do not treat them as fresh external verification.

## Tool-neutral engineering discipline

Communicate with the owner in Russian; code, comments, docs and commits are English.
Select one primary feature/refactor or testing mode. Analyze actual source, tools,
contracts and relevant owners; record scope, staged plan, rationale, risks and
rollback before implementation. Resolve routine choices autonomously; stop only
for material architecture/API/persistent-format/security/compatibility or agreed
scope decisions, or indispensable missing input. Preserve behavior in refactors.
Track milestones and exact evidence so another engineer can continue without oral
context. Keep documentation and executable/configuration commits separate.

Use explicit dependencies, minimal globals, predictable side effects and clear
contracts. Diagnose observed defects before fixing their cause; remove temporary
traces and add regression protection. Never weaken assertions, hide defects behind
flags, swallow failures or mask them with arbitrary sleeps/retries. Do not mock
the domain/subject; fake external boundaries with deterministic clocks/RNG/storage/
network. Tests use Given/When/Then, AAA and order-independent exact outcomes.

Testing requires every scoped artifact/branch/invariant, Positive and applicable
N1–N12 cells, boundaries, interactions, per-file coverage and mutation review before
completion. Do not replace complete planning with arbitrary scenario/test quotas.
Prefer unit/domain, then integration/contracts and critical E2E. Run required stable
checks once at the final gate; known-flaky cases separately and bounded targeted
reruns for diagnosis. Respect actual CI/release gates; no zero-flakiness claim from
repetition alone. Manage only one owned terminal/session at a time, close owned
processes after evidence and never interfere with user/other-agent sessions.
Continue after tool/context interruption only when supported, from repository
evidence; never invent background work. Report changed owners, exact commands and
exit statuses, stable/flaky/skipped results, commits, remaining limits and rollback.
No IDE preset, model preference or forced delegation is required by this policy.

Final evidence: the task-local processed-tooling validator passed all
428/428 scoped static checks for this repository: exact removed paths,
203 protected SHA-256 hashes, 23 local/inbound Markdown links, no new broken
links and no runtime/CI consumer of a removed preset. `git diff b0e788fd8976f9598ce9fa647a45a194449c1932
--check` exited 0. No app/runtime/build/test/audit/provider or release operation
was run or claimed. No ignored/local file was deleted; no push/promotion occurred.
The final docs handoff commits this evidence separately from template removal;
all owned commands exited with no active terminal/server. Restore deleted files
from the recorded baseline or revert the scoped commits to roll back.
