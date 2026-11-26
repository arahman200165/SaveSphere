# E01 Bootstrap: status

Walking skeleton of Phase 01 (`python scripts/prd.py id phase-01`). Written so that nothing unfinished can be read as
delivered (GOV-01, Phase 01 exit criteria). "Evidence" means an automated test or a recorded manual run; nothing here
is claimed verified without one.

## Delivered with evidence

| Area | What exists | Evidence |
|---|---|---|
| Monorepo and build | `global.json` (SDK 10.0.202), central package management, `.slnx`, warnings as errors | `BuildConventionTests`; the build itself failed on analyzer warnings during development |
| API shell | Minimal APIs under `/api/v1`, Problem Details, built-in OpenAPI, committed contract `packages/api-contracts/openapi.json` | `ApiConventionTests` (prefix, kebab-case, contract matches, problem+json, no internals in production) |
| Health | Liveness and readiness (database reachable, migrations current), coarse states only | `HealthTests` (200, 503 pending, 503 unreachable, no leakage, liveness independent) and a real compose run |
| PostgreSQL and migrations | PostgreSQL 18.7, EF Core, 13 reserved schemas, `core` tables, per-module history table, explicit migrator, idempotent and reversible migration | `MigrationTests` (apply, re-apply no-op, schemas, history location, rollback to zero and re-apply) |
| Typed IDs (ARC-01 groundwork) | `Id<TKind>` (UUIDv7, JSON string, EF `uuid` converter) | `IdTests`; database `uuidv7()` defaults in `MigrationTests` and `OutboxTests` |
| Record registry (ARC-01) | `core.record` (typed kind, owner, source, lifecycle, sensitivity, search visibility, timestamps), `core.record_link`, `core.record_external_ref` (namespaced external IDs, one canonical record each); `IRecordRegistry` in the shared kernel; DB-enforced value sets, immutable identity columns, terminal tombstone; events carry IDs and kinds only | `RecordRegistryTests` (ARC-01-AC01: distinct typed IDs and links, one canonical expense counted once across links and re-imports; atomic with outbox; fail-closed policy), `RecordKindAndExternalRefTests` |
| Outbox and event log (ARC-02 groundwork) | `core.outbox_message` and append-only `core.event_log` written with the domain change | `OutboxTests` (commit together, rollback together, failing insert rolls everything back, UPDATE/DELETE/TRUNCATE rejected) |
| Module boundaries (ARC-12) | One project and schema per module, no cross-module references | `ModuleBoundaryTests` |
| Redaction (NFR-10, SEC-01 for logs) | Central `RedactingSink`, `Redacted<T>`, `[Sensitive]`, name and pattern scrubbing, exception text scrubbed | `LogRedactionTests`, `RedactionThroughTheApiTests`, no password or role name in the API container logs during the compose run |
| Docker (reproducible local deploy) | Multi-stage Dockerfile, base + dev + prod compose, non-root read-only containers, loopback-only ports, postgres then migrator then api | `ComposeConfigTests`; manual `docker compose up --build --wait` (all healthy, migrator exit 0, stopping PostgreSQL gives 503 then recovery) |
| Test stack | xUnit v3 on Microsoft Testing Platform, Testcontainers PostgreSQL, requirement traits | The suite itself (132 tests passing at the time of writing) |

## Documents awaiting owner review (no automated evidence)

- `docs/threat-model.md` (draft v0.1): assets, trust boundaries and STRIDE tables with an honest implemented/planned/deferred status per row. Review it before the tunnel, owner auth or any real data is introduced.
- `docs/adr/0005-authentication-and-device-credentials.md` (**proposed**): compares ASP.NET Core Identity with passkeys against Identity plus OpenIddict, with spike acceptance tests. No library is chosen and no identity code exists.

## Written but not yet run or verified

- **CI** (`.github/workflows/ci.yml`): build and test job, and a compose bring-up job. It has never run on GitHub and has not
  been linted with actionlint (the attempt was blocked in this session). First run is the real test.
- **Prod compose** (`docker-compose.prod.yml`): renders and passes the isolation tests; never started.

## Deferred (not started, still required by Phase 01)

- Owner auth, device enrollment and scopes, lock (M01-R01, SEC-05, SEC-01 enforcement): blocked on deciding ADR-0005 after its spike.
- Angular and Expo shells, design tokens, generated TypeScript client from the committed OpenAPI.
- Cloudflare tunnel and remote access without VPN, Android enrollment.
- Backups, restore verification and the SEC-10 emergency recovery package (NFR-07 RPO/RTO measurement).
- ARC-01 follow-ups: the registry stores sensitivity and search visibility but nothing enforces them yet (SEC-01 policy engine, with owner auth); `owner_id` is an unconstrained UUID until the identity module defines the owner (no cross-schema FK by design); the registry is staged on `CoreDbContext`, so a second module needs the shared-transaction pattern below; the registry offers no count/query API on purpose, since counts must go through SEC-01 authorization.
- Outbox dispatcher and worker (ARC-09, Phase 06); cross-module shared-transaction pattern for the outbox when a second module lands.
- Canonical knowledge/ink/canvas schema, storage manifest, import manifest, ink/editor/import feasibility spikes (M09-R05, ARC-11, KN-10).
- Least-privilege runtime DB role versus the migrator's owner role (decide with owner auth).
