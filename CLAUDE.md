# SaveSphere

Single-owner, self-hosted personal "life operating system": .NET 10 modular monolith + PostgreSQL, Angular web, offline-first Expo/React Native Android. **Greenfield: no application code exists yet.** The PRD is the spec; nothing in it is implemented.

## Source of truth: the PRD

`docs/SaveSphere_PRD_V0.0.html` (1.7 MB, ~300 requirements). **Never read or grep the HTML directly**; use the lookup script (stdlib Python):

```
python scripts/prd.py index --phase 01 --priority P0   # list requirements (filters: --phase --module --priority)
python scripts/prd.py req M01-R01 ARC-01 SEC-05        # full text + acceptance criteria + prerequisites
python scripts/prd.py id phase-02                      # any section by HTML id
python scripts/prd.py search "outbox"                  # which requirements mention a term
```

Rules for working from the PRD:
- Before implementing anything, read its requirement(s) **and their prerequisites** (`req` shows them). Cite requirement IDs in commits, PRs, tests and ADRs.
- Classification matters: functional behavior, privacy policy, invariants and acceptance criteria are **normative**. Libraries and benchmarks are *proposals*; mockups are *illustrative*. A library "mentioned" in the PRD is not a mandatory dependency.
- Definition of done (PRD §13): acceptance criteria, domain invariants, authorization, offline behavior, error states, accessibility, redacted telemetry, export/deletion, docs and automated tests are all verified. **Never claim a feature is implemented/verified without a passing test that exercises its acceptance criterion.**
- Do not silently change scope, priority or acceptance criteria (GOV-01). If the PRD seems wrong or ambiguous, stop and ask. Deferred or blocked work must stay visible, not be dropped.
- Unresolved choices get an ADR in `docs/adr/` (copy `0000-template.md`) with options, acceptance tests and evidence *before* committing to a library/provider.

## PUBLIC REPOSITORY: hard rules

This repo is public on GitHub and stays public. Anything committed is world-readable and effectively permanent (history, forks, caches). Assume every file, commit message, PR, issue and CI log is published.

- **Never commit:** secrets, tokens, keys, certificates, connection strings, `.env` files, Cloudflare tunnel credentials, OAuth client secrets, real IPs/hostnames/LAN details, backups, DB dumps, recovery packages, uploaded files, or any real personal data (health, finance, journal, contacts, documents). Commit `.env.example` with placeholders only.
- **Fixtures, tests, seed data and docs use obviously fake data** (`example.com`, `user@example.invalid`, RFC 5737 IPs like `192.0.2.1`). Never paste real records, screenshots or logs from the owner's instance.
- **Config pattern:** secrets come from environment/secret files outside the repo (`.gitignore` covers `.env*`, `secrets/`, `*.pem`, `*.key`, `appsettings.*.local.json`). Never hardcode a fallback secret "for development".
- **Workflow:** solo project; the owner commits straight to `master`. No feature branches or pull requests unless asked.
- **Commits must appear 100% authored by the owner.** Never add `Co-Authored-By`, `Signed-off-by`, "Generated with Claude Code" or any other attribution/trailer to commits, PR text or files. Use the repo's configured git identity as-is. Only commit when asked; show the message for review first.
- **Stage files by name** (`git add path/...`), never `git add -A`/`.` without reviewing `git status`. Review `git diff --cached` before every commit.
- **Never `git push --all`, `--mirror` or `--tags` blindly**, and never push `refs/agents/*` or `refs/codex/*`: local tooling checkpoint refs exist in this clone and must not be published. Push the named branch only.
- **Never force-push or rewrite history** without explicit owner approval.
- Don't create GitHub issues/PRs/comments containing private details, and don't add third-party Actions/webhooks/deploy keys without asking. CI must not echo secrets.
- If something sensitive is committed or pushed: stop, tell the owner immediately, and treat the secret as compromised (rotate it first; deleting the commit is not enough).
- The PRD already published the proposed owner domain; do not add further real infrastructure identifiers beyond what it contains.

## Fixed architecture (do not deviate without an ADR + owner approval)

- Modular monolith, vertical slices. Modules own their commands, invariants and DB schemas; cross-module interaction goes through explicit interfaces, typed events and a transactional outbox, **never cross-module table writes**. A worker process may share assemblies; no microservices.
- PostgreSQL is the canonical store (schemas: identity, core, productivity, finance, health, knowledge, inventory, lifestyle, analytics, automation, integrations, sync, audit). Binaries live outside the DB in a filesystem-backed object store (content hash + metadata + ref counting).
- Android uses encrypted SQLite and one durable sync protocol (operation IDs, outbox, pull cursors). SignalR only signals "changes exist"; it is not the sync channel.
- Business rules live **only** in .NET. Do not duplicate canonical rules in TypeScript; do not share UI components between Angular and React Native. Share generated API contracts (`packages/api-contracts`), design tokens and pure utilities only.
- Privacy is structural (SEC-01): sensitivity, lock, AI eligibility, search visibility, export, etc. are independent policy dimensions; the most restrictive wins; derived data inherits restrictions. Nothing sensitive in logs, previews, counts or aggregates when denied. Manual domain workflows must work without AI or connectors.
- Never expose PostgreSQL, file storage, worker dashboard or host management publicly. Never embed secrets/service tokens in the APK or repo.

## Planned layout (PRD §09; create only as each epic needs it)

```
apps/backend/{SaveSphere.Api,SaveSphere.Worker,SaveSphere.SharedKernel,SaveSphere.Infrastructure,Modules/<Module>}
apps/web (Angular) · apps/mobile (Expo + Kotlin modules)
packages/{api-contracts,design-tokens,shared-typescript}
infrastructure/{docker,cloudflare,database,backups} · tests/ · docs/ · docker-compose.yml
```

## Conventions

- `SaveSphere.*` for .NET projects/namespaces · PascalCase types · camelCase JSON · snake_case PostgreSQL schemas/tables · kebab-case URL segments · API prefix `/api/v1`.
- Files: UTF-8, LF (`.gitattributes`/`.editorconfig` enforce). Don't commit generated or private data (see `.gitignore`).
- Prefer small, reviewable commits that map to one requirement/epic. Tests ship with the change.

## Current phase

Phase 01 / epic E01 "Bootstrap": the **walking skeleton is implemented** (see `docs/e01-status.md` for what is done, what has evidence, and what is deferred). Exit criteria and the 25 phase-01 requirements: `python scripts/prd.py id phase-01`. Remaining Phase 01 work (owner auth, Angular/Expo shells, Cloudflare, backups/SEC-10, design tokens, ink/editor spikes) is **not started**.

## Build, test, run

```
dotnet build SaveSphere.slnx                      # warnings are errors (ADR-0002)
dotnet test --solution SaveSphere.slnx            # xUnit v3 on Microsoft Testing Platform; Docker must be running
dotnet test --project tests/SaveSphere.UnitTests  # one project; add --filter-trait "Requirement=ARC-02" to select by requirement
UPDATE_OPENAPI=1 dotnet test --project tests/SaveSphere.IntegrationTests   # regenerate packages/api-contracts/openapi.json
docker compose -f docker-compose.yml -f docker-compose.dev.yml --env-file .env up --build   # needs POSTGRES_PASSWORD, see README
dotnet ef migrations add <Name> --project apps/backend/Modules/<Module> --startup-project apps/backend/SaveSphere.Migrator --output-dir Persistence/Migrations
```

- Layout now: `apps/backend/{SaveSphere.Api,SaveSphere.Migrator,SaveSphere.SharedKernel,SaveSphere.Infrastructure,Modules/Core}`, `tests/{UnitTests,ArchitectureTests,IntegrationTests,TestSupport}`, `packages/api-contracts/openapi.json` (committed, generated). Only the **Migrator** runs DDL; the API never does. No Worker project yet.
- Every test carries `[Requirement("ID")]`. New endpoints go in an `IEndpointModule` (mapped under `/api/v1`); new module = own project, own `DbContext`, own schema, and a migration history table in that schema (ADR-0001). Package versions only in `Directory.Packages.props`.
- Log redaction is a single chokepoint (`RedactingSink`); never log request bodies/queries, and wrap secrets in `Redacted<T>` or mark `[Sensitive]`.
- `.claude/settings.json` denies reading real env files (`.env`, `.env.local`, `.env.dev*`, `.env.prod*`, ...) but not `.env.example`. If you add a new env-file naming scheme, extend the deny list rather than relying on `.gitignore`.

## Open decisions to resolve (ADR) before building

- **Authentication.** The PRD proposes ASP.NET Core Identity/OpenIddict with passkeys/MFA and a recovery procedure (SEC-05, M01-R01, SEC-10) and lists identity packages as a *pending feasibility decision*. Decide via ADR before building.
- Host setup, sync framework, rich-text/ink approach, background scheduling, backup targets (all "pending" in the PRD). Decided so far: ADR-0001..0004 (SDK/EF Core, API shape and logging, test stack, Docker/PostgreSQL).

## Local environment (this Windows PC)

- Windows 11; Git Bash or PowerShell. Verified available: Docker 29.x, Python 3.13, Node 24.21.0, npm 11.
- .NET SDKs: 10.0.202 in `C:\Program Files\dotnet` (the one on PATH) and 10.0.402 in `%LOCALAPPDATA%\Microsoft\dotnet`. `global.json` pins 10.0.202 (ADR-0001). Host port 5432 is already used by another local PostgreSQL, so dev compose publishes 55432.
