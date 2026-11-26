# ADR-0001: .NET SDK pin and data access (EF Core, per-module migrations)

- **Status:** accepted (owner decision 2026-11-25; acceptance evidence not yet recorded, see below)
- **Date:** 2026-11-25
- **Requirements:** ARC-01, ARC-02, ARC-08, ARC-12, NFR-07, M01-R22

## Context

The PRD fixes the architecture as a .NET 10 modular monolith on PostgreSQL where each module owns its commands, invariants and DB schema, and cross-module interaction never writes another module's tables (CLAUDE.md "Fixed architecture"). The PRD lists library versions and the data-access/migration approach as *pending feasibility decisions*; nothing in it makes a specific library mandatory.

Two .NET SDKs exist on the owner's PC: 10.0.202 (`C:\Program Files\dotnet`, on PATH) and 10.0.402 (`%LOCALAPPDATA%\Microsoft\dotnet`, not on PATH). Docker images and CI runners must resolve the same SDK as the local machine so builds are reproducible (Phase 01 exit criterion: "Deploy reproducibly").

We need to choose (a) the SDK to pin in `global.json` and (b) how modules define, migrate and query their schemas.

## Options considered

1. **SDK 10.0.202 + EF Core.**
   - Pros: already on PATH, no PATH ordering on this PC; trivially matched by an `mcr.microsoft.com/dotnet/sdk:10.0.2xx` image and `actions/setup-dotnet`. EF Core gives model-first schema ownership, one `DbContext` per module, and typed migrations with raw SQL escape hatches (`migrationBuilder.Sql`).
   - Cons: not the newest SDK; EF's snake_case and PostgreSQL mapping need explicit configuration.
2. **SDK 10.0.402 + EF Core.**
   - Pros: newest tooling.
   - Cons: requires manual PATH ordering locally; Docker/CI must be matched to a newer feature band; no PRD requirement needs it.
3. **SDK 10.0.202 + Dapper + hand-written SQL migrations (DbUp/Flyway style).**
   - Pros: total control of DDL and queries; strict schema ownership is explicit.
   - Cons: more boilerplate for every slice, no model/migration drift checks, two languages to keep in sync per change.
4. **Defer via a spike of options 1 and 3.**
   - Pros: evidence before choosing.
   - Cons: delays the walking skeleton; the owner already decided.

## Acceptance tests / evidence

All must pass before this ADR's evidence is recorded (scaffold epic E01):

- `dotnet --version` run in the repo root reports the pinned SDK (10.0.202), and `global.json` uses `rollForward: latestPatch` (not `latestMajor`).
- The Dockerfile builds with an SDK image of the same feature band; CI uses `actions/setup-dotnet` with `global-json-file`.
- Applying all migrations to an empty PostgreSQL succeeds, and a second apply is a no-op.
- Each module's `DbContext` records its migration history in a table inside its **own** schema (not `public`).
- An architecture test fails if a module project references another module's `DbContext` or entity types.
- `dotnet ef migrations script --idempotent` (or equivalent) produces reviewable SQL for every migration, supporting the rollback/recovery review (NFR-07).

## Decision

- Pin **SDK 10.0.202** in `global.json` with `rollForward: latestPatch`.
- Use **EF Core with Npgsql**, one `DbContext` per module, each with `MigrationsHistoryTable("__ef_migrations_history", "<module schema>")`.
- Tables, columns and schemas are snake_case (CLAUDE.md conventions). The mechanism (explicit mapping versus a naming-convention package) is chosen at scaffold time and recorded in the code, not as a mandatory dependency.
- Raw SQL is allowed inside migrations and for performance-critical or reporting queries, but never writes across module schemas.
- Migration 0001 is owned by the Core module and creates all 13 PRD schemas as empty, reserved namespaces. After that, each module's migrations only add objects inside the schema it owns.

## Consequences

- Easier: model-first slices, one reviewable migration chain per module, SDK/Docker/CI parity.
- Harder: EF-generated SQL must be reviewed for PostgreSQL-specific behavior (uuidv7 defaults, partial indexes, check constraints); a few modules may later need hand-tuned SQL.
- Revisit: SDK pin on each .NET servicing update (document the bump in a commit); migration ownership of reserved schemas if a module is extracted into its own repository (not planned).
- Evidence for the acceptance tests above is **not yet recorded**; it will be added here when the scaffold lands.
