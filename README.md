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
