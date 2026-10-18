# Public GitHub Actions runner safety

Date: 2026-10-18. Mode A: security and CI scheduling fix. Status: implementation in progress.

## Confirmed issue and risk

This repository is PUBLIC. The existing direct pull_request entry executes `quick` on the organization self-hosted Linux runner because the expression treats every non-workflow_dispatch event as self-hosted. The main-tag manual workflow also allows explicit local self-hosted selection. RunnerRouter's separate hosted-only route fix cannot protect a direct PR workflow; it only controls workflow_dispatch inputs. This is a source-confirmed unsafe routing boundary, not evidence that any untrusted PR actually executed on the owner machine. GitHub-hosted standard Linux Actions are free for public repositories.

## Planned correction

1. [x] Read owner repo docs, live workflow, routing-contract tests, repository visibility and branch divergence.
2. [ ] Pin `quick` PR/branch and tagged release jobs directly to GitHub-hosted Ubuntu. Keep the RunnerRouter compatible `runner=hosted` dispatch input but remove the unsupported `self-hosted` option. Do not use the owner machine for any public-source job, including manual tag builds.
3. [ ] Preserve current fast tests, tag-on-current-main validation, release non-cancellation, UI/report and Azure publication steps without changing tokens or permissions.
4. [ ] Extend the existing Python CI routing contract with negative assertions against accidental self-hosted selection and run this in the quick job.
5. [ ] Observe green CI on dev; review the pre-existing dev/main divergence (eleven dev-only and five main-only commits at start) before any merge/release. No force push.
6. [ ] Verify actual GitHub runner assignment on PR and successful router dispatch; do not claim live safety purely from source checks.

## Rollback

Revert scoped dev workflow/test/docs commits. No runner-group, organization settings, webhook secrets or release tags are modified.
