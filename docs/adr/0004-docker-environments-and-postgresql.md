# ADR-0004: Containerized environments, dev/prod isolation, PostgreSQL 18 and migrations

- **Status:** accepted (owner decision 2026-11-25; acceptance evidence not yet recorded, see below)
- **Date:** 2026-11-25
- **Requirements:** NFR-07, NFR-10, SEC-10, M01-R22, ARC-08, ARC-12

## Context

Phase 01's exit criterion is "Deploy reproducibly", with dev/prod isolation and backup/recovery verification arriving in the same phase. The PRD requires that PostgreSQL, file storage and host management are never exposed publicly, and that secrets are never embedded in the repo (CLAUDE.md; this repository is public). The PRD lists host setup and PostgreSQL deployment details as *pending*. Cloudflare, backups and the home-server deployment are later steps in Phase 01; this ADR covers only the first step.

Decisions needed: how API and database are containerized, how dev and prod are kept apart, which PostgreSQL version, and how migrations run.

## Options considered

### A. Container scope
1. **Everything containerized from day 1** (API and PostgreSQL, with dev and prod override files and a multi-stage Dockerfile). More upfront work, but proves reproducible deployment early.
2. Compose for PostgreSQL only; API on host via `dotnet run`. Faster inner loop, but defers Dockerfile and compose problems to the deployment step.
3. Both compose files now, only dev run. Cheaper, but the prod file would be unproven.

### B. PostgreSQL version
1. **PostgreSQL 18 (exact minor tag pinned).** Native `uuidv7()` for time-ordered IDs, current major with the longest support window.
2. PostgreSQL 17. More battle-tested, but UUIDv7 would have to be generated in .NET.

### C. Migration execution
1. **Explicit migrator step**: a one-shot compose service/command applies migrations; the API refuses to start serving if the schema is behind (or reports not-ready).
2. Migrate on API start. Convenient, but races under multiple instances, gives the web-facing process DDL rights, and hides failures inside startup.

## Acceptance tests / evidence

- `docker compose --env-file .env up` (dev) from a clean clone with only `.env.example` copied to `.env` starts PostgreSQL, runs the migrator to completion, then starts the API; `/api/v1/health` returns 200.
- **Isolation (dev vs prod):** dev and prod use different compose project names, container names, networks, volumes and database names, and cannot start on the same host ports. A script/test asserts the rendered configs (`docker compose config`) share none of these.
- The PostgreSQL port is published only on `127.0.0.1` (asserted from the rendered config); no other service publishes a port in prod.
- No secret or real identifier is committed: `.env.example` has placeholders only; `git grep` for the credentials used by a local run finds nothing.
- Health endpoint returns 503 and the API reports not-ready when the database is stopped, and when migrations are pending.
- Prod compose is validated with `docker compose -f ... config` only; it is **not** run until the home-server step.
- Verify the PostgreSQL 18 image's data-directory/volume mount convention at scaffold time and record it here (it changed from earlier majors).
- `SELECT uuidv7()` succeeds in the pinned image (migration test).

## Decision

- **Everything containerized from the start:** multi-stage `Dockerfile` for the API (SDK image to runtime image, non-root user, read-only root filesystem where practical), `docker-compose.yml` (base), plus `docker-compose.dev.yml` and `docker-compose.prod.yml` overrides.
- **PostgreSQL 18**, official image pinned to an exact minor tag (the tag is recorded in the compose files and checked against the Testcontainers tag, see ADR-0003). Named volumes, host port bound to `127.0.0.1` only.
- **Isolation:** distinct compose project names (`savesphere-dev`, `savesphere-prod`), distinct volumes/networks/database names, and credentials supplied via gitignored `.env` or secret files outside the repo. No fallback default secrets anywhere.
- **Explicit migrator:** migrations run as a one-shot service (`depends_on: service_completed_successfully`) before the API. The API does not run DDL.
- **Prod compose** is written and linted now; it is exercised at the home-server step.

## Consequences

- Easier: reproducible bring-up on any machine with Docker; early detection of container/network problems; the same artifact path for dev, CI and prod.
- Harder: slower inner loop than running the API on the host (a `dotnet run`/`dotnet watch` path remains available against the dev database); more files to keep consistent.
- Open items deliberately left to later steps: a separate least-privilege runtime DB role versus the migrator's owner role (decide when owner auth is added), backup/restore and SEC-10 recovery package, Cloudflare tunnel, and TLS/secret provisioning on the home server.
- Evidence for the acceptance tests above is **not yet recorded**; it will be added here when the scaffold lands.
