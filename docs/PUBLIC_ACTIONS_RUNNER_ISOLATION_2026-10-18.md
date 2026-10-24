# Public GitHub Actions runner safety

Date: 2026-10-18. Mode A: security and CI scheduling fix.
Source reviewed on 2026-10-24: hosted-only routing is implemented; actual PR/router
runner-assignment and green-CI acceptance are not verified by this local cleanup.

<a id="confirmed-issue-and-risk"></a>

## Historical Confirmed Issue And Risk

The following describes the original October 18 baseline, not the current
hosted-only source:

This repository is PUBLIC. The existing direct pull_request entry executes `quick` on the organization self-hosted Linux runner because the expression treats every non-workflow_dispatch event as self-hosted. The main-tag manual workflow also allows explicit local self-hosted selection. RunnerRouter's separate hosted-only route fix cannot protect a direct PR workflow; it only controls workflow_dispatch inputs. This is a source-confirmed unsafe routing boundary, not evidence that any untrusted PR actually executed on the owner machine. GitHub-hosted standard Linux Actions are free for public repositories.

## Planned correction

1. [x] Read owner repo docs, live workflow, routing-contract tests, repository visibility and branch divergence.
2. [x] Pin `quick` PR/branch and tagged release jobs directly to GitHub-hosted Ubuntu. Keep the RunnerRouter compatible `runner=hosted` dispatch input but remove the unsupported `self-hosted` option. Do not use the owner machine for any public-source job, including manual tag builds.
3. [x] Preserve current fast tests, tag-on-current-main validation, release non-cancellation, UI/report and Azure publication steps without changing tokens or permissions.
4. [x] Extend the existing Python CI routing contract with negative assertions against accidental self-hosted selection and run this in the quick job.
5. [ ] Observe green CI on dev; review the pre-existing dev/main divergence (eleven dev-only and five main-only commits at start) before any merge/release. No force push.
6. [ ] Verify actual GitHub runner assignment on PR and successful router dispatch; do not claim live safety purely from source checks.

## Rollback

Revert scoped dev workflow/test/docs commits. No runner-group, organization settings, webhook secrets or release tags are modified.

## Local Source Verification Boundary — 2026-10-24

The tracked workflow assigns quick, main-tests and manual-ui directly to
ubuntu-latest, and its dispatch selector accepts only hosted. The five existing
Python contracts check those source invariants, removed local selection and the
retained release/deployment shape. Legacy conditional self-hosted preparation
steps do not change these fixed hosted assignments; cleanup does not edit them.
Testing owns the actual local command/result after the final gate.

Checklist items 2–4 describe implemented source. Items 5–6 remain unchecked:
this cleanup does not observe green hosted jobs, real PR/router assignment or
reconcile branches for publication. The original eleven/five divergence counts
are historical baseline evidence, not a fresh branch-status assertion. No runner
settings, grants, secrets, tokens, workflow or provider state was changed.
