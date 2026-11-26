# SaveSphere threat model (Phase 01, v0.1)

Status: **draft for owner review.** This is a document, not evidence: a row marked *Implemented* points at the automated test that
exercises it; everything else is *Planned* or *Deferred* and must not be read as delivered (GOV-01, PRD §13).
The repository is public, so this model is deliberately generic: no real hostnames, IP addresses, account names or device details.

Requirements served: SEC-01, SEC-04, SEC-05, SEC-10, M01-R01, NFR-07, NFR-10. Related decisions: ADR-0002 (redaction), ADR-0004
(containers and exposure), ADR-0005 (authentication, proposed).

## 1. Scope and assumptions

- One owner, one home server running Docker Compose (API, migrator, PostgreSQL; later worker, file store, tunnel connector).
- Clients: a personal web browser, a personal Android phone, and a restricted work device (server scope narrower than the personal one).
- Remote access without VPN is planned through a Cloudflare tunnel (Phase 01, not built). Until then nothing is reachable off the host.
- The owner is trusted. Their devices can be lost, stolen, shared briefly or compromised; the home network and the internet are not trusted.
- Out of scope for v0.1: nation-state attackers, physical attacks on the server after disk unlock, and compromise of the owner's cloud accounts.

## 2. Assets

| ID | Asset | Why it matters |
|---|---|---|
| A1 | Records in PostgreSQL (finance, health, journal, knowledge, ...) | Highly personal; some domains private by default (SEC-04) |
| A2 | Uploaded binaries in the object store (planned) | Documents, images, recordings |
| A3 | Credentials: passkeys, device tokens, recovery codes, DB password | Gate everything else |
| A4 | Backups and the emergency recovery package (planned) | Contain A1 and A2 outside the server's protection |
| A5 | Audit and event log | Evidence of what happened; must not be silently altered |
| A6 | Source repository and CI (public) | Supply chain and accidental disclosure |
| A7 | Availability of the home server and data | RPO 1 h, RTO 8 h (NFR-07) |

## 3. Trust boundaries

```
 Internet --(B1)--> tunnel connector --> API container --(B4)--> PostgreSQL / file store
 Browser  --(B3)--> API                      |
 Android  --(B2)--> API (sync)               +--(B5)--> host: Docker, volumes, backups
 AI / connector providers (B6, later)        +--(B7)--> repo, CI, dependencies
```

## 4. STRIDE per boundary

Status values: **Implemented** (test named), **Planned** (designed, not built), **Deferred** (needs a later epic or decision).

### B1 Internet to the tunnel and API

| Threat | Mitigation | Requirement | Status |
|---|---|---|---|
| Spoofing: attacker calls the API as the owner | Passkeys/MFA, device-bound tokens (ADR-0005) | SEC-05, M01-R01 | Planned |
| Tampering/Info disclosure: plaintext traffic | TLS terminated at the tunnel edge; API trusts only the tunnel connector | NFR-10 | Deferred (tunnel not built) |
| Denial of service: request flooding, expensive endpoints | Rate limits on auth and sync, edge protection | NFR-01, SEC-05 | Planned |
| Info disclosure: services exposed by mistake | Only the API is published, on loopback; PostgreSQL publishes nothing in prod; every port binds `127.0.0.1` | NFR-10 | Implemented (`ComposeConfigTests`) |
| Info disclosure: error messages reveal internals | Generic Problem Details outside Development; coarse health states only | NFR-10 | Implemented (`ApiConventionTests`, `HealthTests`) |

### B2 Android device to server (sync) and B3 browser to API

| Threat | Mitigation | Requirement | Status |
|---|---|---|---|
| Spoofing: stolen or cloned device token | Per-device credentials, short lifetime, rotation, revocation | M01-R01, SEC-05 | Planned |
| Elevation: restricted work device widens its scope | Scope fixed at enrollment and enforced server-side on every request (intersection rule) | M01-R01-AC01, SEC-01 | Planned |
| Elevation: local PIN reset grants server access | PIN verifier is client-local and never accepted by the server | SEC-05-AC01 | Planned |
| Tampering: replayed or duplicated sync operations | Operation IDs, per-entity sync strategy, no blanket last-write-wins | ARC-02, QA-03 | Deferred (outbox and event log exist; sync protocol does not) |
| Repudiation: owner cannot tell what a device did | Audit log of auth and sync events | SEC-05 | Planned |
| Info disclosure: sensitive data in previews, counts or aggregates when locked or denied | Policy intersection, derived data inherits restrictions | SEC-01-AC01 | Deferred (policy engine not built) |
| Info disclosure: stolen unlocked phone | Encrypted local SQLite, relock on background/timeout | SEC-05 | Planned |

### B4 API to PostgreSQL and the file store

| Threat | Mitigation | Requirement | Status |
|---|---|---|---|
| Elevation: web-facing process holds DDL rights | Only the one-shot migrator runs DDL; the API never does | ADR-0004 | Implemented by design (`MigrationTests` run the migrator path); a separate least-privilege DB role is Deferred |
| Tampering: module writes another module's tables | One schema per module, no cross-module references | ARC-12 | Implemented (`ModuleBoundaryTests`) |
| Tampering: rewriting history | Append-only event log enforced by trigger | ARC-02 | Implemented (`OutboxTests`) |
| Info disclosure: database reachable from the network | PostgreSQL not published in prod, loopback in dev | NFR-10 | Implemented (`ComposeConfigTests`) |
| Info disclosure: SQL injection | Parameterized EF Core access; raw SQL only in reviewed migrations | NFR-10 | Planned (needs a rule and a test when raw queries appear) |
| Info disclosure: uploaded files exposed | Files live outside the DB behind authorization, never served statically | NFR-10 | Deferred (store not built) |

### B5 Host, containers, volumes and backups

| Threat | Mitigation | Requirement | Status |
|---|---|---|---|
| Elevation: container escape or abuse | Non-root user, read-only root filesystem, all capabilities dropped, `no-new-privileges` | NFR-10 | Implemented (`ComposeConfigTests`) |
| Info disclosure: secrets in the repo, images or logs | `.gitignore` and deny rules, no fallback secrets, `.dockerignore`, redacting logger | NFR-10, SEC-01 | Implemented for config and logs (`ComposeConfigTests`, `LogRedactionTests`, `RedactionThroughTheApiTests`); repo scanning is Planned |
| Info disclosure: stolen backup or recovery package | Encrypted at rest, keys and recovery codes never stored on the host or in the same package | SEC-10 | Deferred |
| Tampering: corrupted or truncated backup | Manifest with hashes verified on restore | SEC-10-AC01, NFR-07 | Deferred |
| Denial of service: disk failure, ransomware on the host | Tested restores, off-host copies | NFR-07 | Deferred |
| Dev/prod cross-contamination | Separate project names, volumes, networks, databases and roles | ADR-0004 | Implemented (`ComposeConfigTests`) |

### B6 AI and connector providers (later phases)

| Threat | Mitigation | Requirement | Status |
|---|---|---|---|
| Info disclosure: private data sent to a provider | Per-record AI eligibility, most-restrictive-wins, local-only and raw-only constraints | SEC-01, SEC-02, AI-01 | Deferred |
| Elevation: prompt injection through retrieved content | Treat retrieved content as untrusted, typed tool validation | AI-03, AI-06 | Deferred |
| Manual workflows depend on AI | Every domain workflow must work without AI or connectors | M01-R22 | Planned |

### B7 Source repository, CI and dependencies

| Threat | Mitigation | Requirement | Status |
|---|---|---|---|
| Info disclosure: real data or identifiers committed to the public repo | CLAUDE.md rules, fake fixtures, review of every diff before commit | NFR-10 | Process control only |
| Tampering: malicious or vulnerable dependency | Central package versions, exact action SHAs, first-party Actions only | NFR-10 | Implemented for pinning (`BuildConventionTests`); vulnerability scanning is Planned |
| Tampering: CI abused to leak secrets | CI uses no secrets; throwaway credentials are generated and masked | NFR-10 | Written, not yet run on GitHub |

## 5. Top residual risks (as of v0.1)

1. **No authentication exists.** The API is only safe because it binds to loopback; do not run the tunnel until ADR-0005 is decided and built.
2. **No backups or recovery package.** A disk failure today loses all data (NFR-07 unmet).
3. **Single runtime DB role.** A compromised API process could alter schema; mitigated later by a least-privilege role.
4. **No policy engine.** SEC-01 enforcement is not built, so nothing sensitive may be stored yet.
5. **CI unverified.** Supply-chain controls in CI have not run.

## 6. Review and upkeep

- The owner reviews this document before the Cloudflare tunnel, owner auth or any real data is introduced.
- Update the status column in the same commit that adds the mitigation and its test.
- Add a row for every new trust boundary (for example the worker, the file store, each connector).
