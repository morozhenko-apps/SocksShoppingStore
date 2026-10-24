# Overview

SocksShoppingStore is a small ASP.NET Core 8 MVC demo app for browsing “job-themed” socks, adding them to a session-backed cart, and running a test-mode checkout flow (Stripe Checkout). The repo is intentionally designed as a QA/automation showcase: the app stays simple, but includes production-friendly concerns (security headers/CSP, rate limiting, concurrency cap, minimal metrics, localization) and a layered test suite with CI reporting.

<a id="canonical-documentation-5-files"></a>

## Documentation Owners

The five core guides retain distinct ownership:

- [Overview](overview.md) — purpose, constraints and explicitly future roadmap.
- [Setup](setup.md) — local run, configuration, Stripe and test environment.
- [Architecture](architecture.md) — code map, pipeline, endpoints and core flows.
- [Testing](testing.md) — commands, categories, CI lanes, coverage and evidence.
- [Agent guide](SOCKS_AGENT_GUIDE.md) — high-signal handoff and runtime invariants.

[Contributing](contributing.md) is contributor/review guidance; the
[runner-isolation record](PUBLIC_ACTIONS_RUNNER_ISOLATION_2026-10-18.md) is dated
risk/verification evidence. Neither is a replacement product contract. Keep the
Russian root README, component/dependency licenses and working Razor inputs in
their existing roles. A source check does not prove a live CI deployment.

## What’s Implemented Today

- Catalog browsing with `q` search, sorting, price filtering, and paging.
- Session-backed cart with AJAX “add to cart” and in-place quantity updates.
- Checkout form → review → Stripe Checkout redirect (test keys only) with a safe local fallback.
- Localization (EN/RU): UI strings via `.resx`; product name/description via an in-memory map.
- Safety and free-tier friendliness: CSP/security headers, rate limiting, concurrency cap, optional free-tier guard.
- Operability: `GET /healthz` and a small IP-allowlisted `GET /_status` metrics endpoint.

## Constraints / Safety Rails

- Stripe: the app refuses non-test keys (`sk_test_*`) to avoid accidental live charges.
- Free-tier guard: can block most traffic in certain deployments; see `FreeTier` options in `SocksShoppingStore/appsettings.json`.
- Rate limiting and concurrency caps are enabled by default; CI runs relax these to keep UI tests stable.

## Roadmap (Vision)

This roadmap is a guide for future expansion. Many items are not implemented yet. These
ideas are retained product context, not authorization to implement them during
repository cleanup.

### Principles

1. Efficiency by design (free-tier awareness).
2. Maximum testability (features chosen to create diverse test scenarios).
3. Automation first (CI is the center of the lifecycle).
4. Public demonstration (reports are automatically publishable and publicly accessible).

### Feature Expansion (high level)

- Phase 1: accounts, order history, user-generated content (reviews/ratings), wishlist.
- Phase 2: faceted search/filtering, promo codes, lightweight recommendations (mock).
- Phase 3: guest checkout, mock payment variations, mock order confirmation.

### QA / Testing Roadmap (high level)

- Richer Allure reports (steps/attachments/history/categories).
- Lightweight performance checks (e.g., k6) targeting key routes and APIs.
- Automated security scanning (e.g., OWASP ZAP baseline) integrated into CI.
- Practical AI use for QA tasks (e.g., structured test data generation, locator resilience research).

## Tech Debt / Known Gaps

- Catalog price validation UX (invalid inputs, min > max messaging).
- Cart decrement-to-zero behavior (currently clamps at `>=1` unless deleted).
  This is the current explicit cart contract, not a change made by cleanup.
- UI coverage collection in CI (initial wiring exists; iterate as needed).
  The workflow contains a tagged-release instrumentation step; actual collected
  coverage and successful publication require a matching run receipt.

## Contributing Guidelines (short)

- Prefer English for PRs and commit messages.
- Keep changes small and focused; update/extend tests with behavior changes.
- Use Conventional Commits style (e.g., `feat: ...`, `fix: ...`).
