# ADR-0003: Test stack (xUnit v3, Testcontainers, real PostgreSQL)

- **Status:** accepted (owner decision 2026-11-25; acceptance evidence not yet recorded, see below)
- **Date:** 2026-11-25
- **Requirements:** QA-01, QA-03, ARC-01, ARC-02, NFR-07, M01-R22

## Context

PRD §13's definition of done and QA-01 require that no feature is claimed without a passing automated test exercising its acceptance criterion. Most Phase 01 invariants (outbox atomicity, migrations, schema ownership, health under database loss) are only meaningful against a real PostgreSQL, not an in-memory fake or SQLite. Tests must run the same way on the owner's Windows PC (Docker 29.x available) and on GitHub-hosted runners, without shared mutable state with the dev database.

The PRD names no test framework; this is a pending choice.

## Options considered

1. **xUnit v3 + Testcontainers for PostgreSQL.**
   - Pros: each test class/collection gets a throwaway PostgreSQL container; no manual setup; identical locally and in CI; dev data can never be touched by tests.
   - Cons: needs a running Docker daemon; container start adds seconds to the first test; image tag must stay aligned with the pinned runtime image.
2. **xUnit v3 + the compose PostgreSQL (or a CI service container).**
   - Pros: no container orchestration in test code; fast start.
   - Cons: tests share a server with dev unless carefully isolated; setup differs between local and CI; easy to pollute dev data.
3. **TUnit or another newer framework.**
   - Pros: modern, parallel-by-default.
   - Cons: smaller ecosystem and tooling; no requirement favors it.

## Acceptance tests / evidence

- `dotnet test` from a clean clone with only Docker running passes with no environment variables and no pre-existing database.
- The same suite passes in GitHub Actions on a hosted runner (Docker preinstalled) with no secrets.
- A test creates and drops databases from a template per test class, proving isolation: running the suite twice, or in parallel, yields the same results and leaves the dev compose database untouched.
- A drift test asserts the Testcontainers PostgreSQL image tag equals the tag pinned in the compose files (see ADR-0004).
- Integration tests reference the requirement ID they verify (trait/attribute such as `[Requirement("ARC-02")]`), enabling QA-01 traceability reports.
- Confirm that xUnit v3 test projects run under `dotnet test` on SDK 10.0.202 (Microsoft Testing Platform mode configured in `global.json` if required). If not, record the workaround here.

## Decision

- **xUnit v3** as the test framework, **Testcontainers (PostgreSQL module)** for integration tests against a real, pinned PostgreSQL 18 image.
- Layout: `tests/SaveSphere.UnitTests`, `tests/SaveSphere.IntegrationTests`, and `tests/SaveSphere.ArchitectureTests` (module-boundary rules from ADR-0001). Created as each slice needs them.
- Test data is obviously fake (CLAUDE.md public-repo rules); no real records, hostnames or secrets in fixtures. Container credentials are random per run.

## Consequences

- Easier: confident persistence/outbox tests, no local DB setup, parity between local and CI.
- Harder: Docker is a hard prerequisite for the full suite; slower than pure unit tests, so unit and integration projects stay separate and are runnable independently.
- Revisit: when the Android client lands, add a separate strategy for offline/sync fixtures shared with the server (ARC-02, QA-03); mobile tests will be covered by their own ADR.
- Evidence for the acceptance tests above is **not yet recorded**; it will be added here when the scaffold lands.
