# SaveSphere

A single-owner, self-hosted personal operating system: finance, planning, health, knowledge, inventory and more, with an offline-first Android app and a full web workspace. The product spec is [`docs/SaveSphere_PRD_V0.0.html`](docs/SaveSphere_PRD_V0.0.html).

## Stack

.NET 10 / ASP.NET Core modular monolith · PostgreSQL · Angular web · React Native + Expo Android (encrypted SQLite) · Docker Compose · Cloudflare Tunnel.

## Working with the spec

The PRD is large; query it instead of opening it:

```
python scripts/prd.py index --phase 01 --priority P0
python scripts/prd.py req M01-R01
```

Architecture decisions are recorded in [`docs/adr/`](docs/adr). Contributor/agent guidance lives in [`CLAUDE.md`](CLAUDE.md).

## Getting started (backend)

Prerequisites: Docker, and the .NET SDK pinned in [`global.json`](global.json) (10.0.202).

### Local environment

Secrets never live in the repo. Copy [`.env.example`](.env.example) to `.env` (development) or `.env.prod` (production);
Docker Compose reads the copy you create. Both copies are gitignored. The variables:

| Variable | Required | Notes |
|---|---|---|
| `POSTGRES_PASSWORD` | yes | Random, letters and digits only (it is embedded in a connection string). For example `openssl rand -hex 24`. There is no default: Compose refuses to start without it. |
| `POSTGRES_HOST_PORT` | no | Host port for PostgreSQL, bound to `127.0.0.1` only. Default `55432` (dev). |
| `API_HOST_PORT` | no | Host port for the API, bound to `127.0.0.1` only. Default `58080` (dev), `58180` (prod). |

### Run

```
docker compose -f docker-compose.yml -f docker-compose.dev.yml --env-file .env up --build
curl http://127.0.0.1:58080/api/v1/health
```

Start order is PostgreSQL (healthy), then the one-shot **migrator** (the only process that runs DDL), then the API.
Dev (`savesphere-dev`) and prod (`savesphere-prod`) use separate project names, volumes, networks, databases and
roles. The prod override is linted with `docker compose config` and has not been run yet.

### Test

```
dotnet build SaveSphere.slnx
dotnet test --solution SaveSphere.slnx
```

Integration tests start their own throwaway PostgreSQL container via Testcontainers, so Docker must be running.
They also check that [`packages/api-contracts/openapi.json`](packages/api-contracts/openapi.json) matches the API; after an
intentional API change, regenerate it with `UPDATE_OPENAPI=1 dotnet test --project tests/SaveSphere.IntegrationTests`
and commit the result.

Epic status and deferred work: [`docs/e01-status.md`](docs/e01-status.md).
