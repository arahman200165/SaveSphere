# ADR-0002: API shape, OpenAPI, logging redaction and build conventions

- **Status:** accepted (owner decision 2026-11-25; evidence recorded below)
- **Date:** 2026-11-25
- **Requirements:** ARC-08, ARC-01, NFR-10, SEC-01, SEC-04, M01-R22

## Context

The API is one ASP.NET Core process hosting vertical-slice modules under the `/api/v1` prefix (CLAUDE.md conventions). The PRD requires typed contracts: .NET is the authoritative validator and OpenAPI/TypeScript clients are *generated* from it (ARC-08). Privacy is structural: nothing sensitive may appear in logs, previews, counts or aggregates when denied (SEC-01, SEC-04, NFR-10), so redaction must be a central, testable mechanism and not a per-call habit.

The PRD does not mandate any web, logging or OpenAPI library; each is a proposal evaluated here. Four cross-cutting choices are bundled because they are all decided once, at the scaffold, and constrain every later slice.

## Options considered

### A. Endpoint style
1. **Minimal APIs with a `MapGroup` per slice.** Fits vertical slices, no controller/MediatR dependency, simple module registration (`IModuleEndpoints`-style).
2. Controllers. Familiar, but heavier per-slice ceremony and implicit conventions.
3. Mediator library (MediatR or similar) plus endpoints. Adds a dependency (and licensing/maintenance concerns) for indirection the project does not need yet.

### B. OpenAPI generation
1. **`Microsoft.AspNetCore.OpenApi` (built in).** First-party, build-time document generation feeds `packages/api-contracts`.
2. Swashbuckle / NSwag. Larger feature set (client generation), but extra third-party dependency and overlapping generators.

### C. Logging
1. **Serilog with a central redaction filter.** Structured logs, enrichers, and a single place for destructuring policies and property-name filters.
2. Built-in `ILogger` plus a custom `ILoggerProvider`/filter. No dependency, but we rebuild structured sinks and enrichers ourselves.

### D. Build conventions
1. **`.slnx`, central package management (`Directory.Packages.props`), shared `Directory.Build.props`, `Nullable` on, analyzers on, warnings as errors.** One place for versions and strictness.
2. Per-project settings and a classic `.sln`. Less upfront work, but drift across ~10+ projects.

## Acceptance tests / evidence

- A test enumerates all registered endpoints and asserts each lives under `/api/v1` with kebab-case segments.
- The build emits `openapi.json` and a test fails if the committed contract changes without regenerating (consumed later by `packages/api-contracts`).
- **Redaction (NFR-10, SEC-01):** log a request containing a bearer token, a connection string, a password field and a field marked sensitive. The captured sink output contains none of those values. Request/response bodies and query strings are not logged by default.
- Unhandled exceptions return an RFC 9457 `application/problem+json` response with no stack trace or inner exception text outside Development.
- `dotnet build` fails on a new warning (verified by a deliberate unused-variable canary in a scratch branch/local check, not committed).
- Package versions are declared only in `Directory.Packages.props` (a test or script greps project files for `Version=` on `PackageReference`).

## Decision

- **Minimal APIs**, one endpoint group per slice, registered by each module; no controllers and no mediator library.
- **Built-in OpenAPI** document generation; no Swashbuckle.
- **Serilog** with: console JSON output, a global destructuring policy, a deny-list of property names (`password`, `token`, `secret`, `authorization`, `connectionString`, ...), a `Redacted<T>`/`[Sensitive]` marker type for domain values, and request logging that excludes bodies and query strings. The redaction filter is a shared infrastructure component with its own tests.
- **`.slnx` solution, central package management, warnings as errors**, nullable reference types and the .NET analyzers enabled solution-wide.
- Problem Details (RFC 9457) for errors.

## Consequences

- Easier: uniform module registration, one redaction chokepoint to audit, deterministic dependency versions.
- Harder: Serilog is a new third-party dependency to track; Minimal APIs need discipline for validation (a shared validation filter will be added with the first real endpoint). Warnings-as-errors can briefly block upgrades.
- Revisit: if client generation from built-in OpenAPI proves insufficient for the TypeScript contracts (ARC-08), reassess NSwag/Kiota in a new ADR when `packages/api-contracts` is built.

## Evidence recorded 2026-11-25

- Endpoint prefix and kebab-case: `ApiConventionTests`. Contract: the document is generated at test time from the running API and compared with the committed `packages/api-contracts/openapi.json` (regenerate with `UPDATE_OPENAPI=1`). **Deviation:** the ADR said "the build emits openapi.json"; a test-time comparison was used instead because it needs no build-time app launch and still fails on drift.
- Redaction: `LogRedactionTests` (unit) and `RedactionThroughTheApiTests` (end to end) prove fake secrets, connection strings, bearer tokens, `[Sensitive]` properties, `Redacted<T>` and exception text never reach a sink; the compose run showed no password or role name in the API logs. Request logging records the path only.
- Problem Details with no internals in Production: `ApiConventionTests`.
- Warnings as errors: the build failed on analyzer findings (CA1848, CA1861) while developing, so the setting is active; `BuildConventionTests` assert the flags. No dedicated canary was added.
- Central package versions: `BuildConventionTests`.
- Serilog is used with `preserveStaticLogger: true` and an explicit logger for request logging, so parallel hosts never share events.
