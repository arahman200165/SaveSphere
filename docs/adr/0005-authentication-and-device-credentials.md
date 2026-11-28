# ADR-0005: Authentication, device credentials and recovery

- **Status:** accepted 2026-11-28 (Option 2) with the rules and gates in "Decision" below. The owner delegated the decision ("decide on a path to take and record it"); the owner may overturn it before the first identity code is merged.
- **Date:** 2026-11-25 (proposed), 2026-11-28 (decided)
- **Requirements:** M01-R01, SEC-05, SEC-01, SEC-04, SEC-10, NFR-10, ARC-08

## Context

SaveSphere is single-owner. The owner signs in on a personal browser, a personal Android device and a restricted work device. The PRD requires:

- **M01-R01-AC01:** bootstrap one owner, enroll a personal device and a restricted work device, and verify that the work credential cannot expand its server scope.
- **SEC-05 / SEC-05-AC01:** passkeys or MFA, an enrolled device and a server scope; a local username/PIN unlock whose verifier is distinct from any server token; rate limiting; backgrounding or timeout relocks; a PIN reset must never unlock unapproved server access. A local PIN is never remote server authentication.
- **SEC-10:** an emergency recovery package; encryption keys and recovery codes are never stored on the host or inside the same package.
- **SEC-01 / SEC-04:** effective permission is the intersection of record, domain, enrolled device/session, purpose and current unlock; the most restrictive wins; missing policy fails closed.
- **NFR-10:** no unresolved critical or high security findings at release; permission-boundary violations are blocked.

The PRD names ASP.NET Core Identity and OpenIddict as proposals and lists identity packages as a pending feasibility decision (CLAUDE.md "Open decisions"). Nothing is mandatory.

Facts checked on 2026-11-25 (not assumptions):

- The ASP.NET Core shared framework installed here (10.0.6) ships passkey APIs in Identity: `IdentityPasskeyOptions`, `MakePasskeyCreationOptionsAsync`, `MakePasskeyRequestOptionsAsync`, `IPasskeyHandler`, and `IUserPasskeyStore` in `Microsoft.Extensions.Identity.Core` 10.0.15.
- OpenIddict 7.9.0 (`OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore`) targets net10.0.
- Not yet verified (spike): passkey behavior on the owner's actual browsers and on Android Credential Manager, OpenIddict's fit for a native Android client with per-device revocation, and licence terms.

## Options considered

1. **ASP.NET Core Identity with passkeys; session cookie for the web and Identity's bearer/refresh tokens for Android; a custom `device` table for enrollment and scope.**
   - Pros: first-party, smallest dependency set, passkeys and MFA recovery codes built in, one user store.
   - Cons: Identity's bearer tokens have no built-in notion of per-device scope, rotation policy or revocation lists. We would write and own the device-scope and token-revocation logic ourselves, which is the part M01-R01-AC01 stresses.
2. **ASP.NET Core Identity (user store, passkeys, MFA) plus OpenIddict as the OAuth 2.0 / OIDC server.** Web and Android use authorization code with PKCE; scopes, audiences, refresh-token rotation and revocation come from OpenIddict; a device is an OpenIddict application/authorization linked to an enrollment record.
   - Pros: standard, reviewable token model; device-scoped, short-lived, revocable tokens; the "work credential cannot widen scope" rule is expressed as the scopes the authorization can ever grant.
   - Cons: more setup and surface area, a second large dependency, and an OIDC flow in a native Android app that must work offline-first (sync only needs a token when online).
3. **Hand-rolled minimal auth** (owner credential plus device tokens, no framework). Rejected for evaluation: the owner did not select it and it carries the most security risk against NFR-10.

Out of scope for this ADR: the local PIN/biometric unlock on Android and web (its verifier is client-side and never accepted by the server; designed in the SEC-05 epic once this ADR is decided) and external identity providers (the PRD requires manual workflows without connectors).

## Acceptance tests / evidence (spike, before deciding)

For each of options 1 and 2, in a throwaway branch or scratch project (not merged until decided), record results here:

1. Bootstrap exactly one owner; a second bootstrap attempt is rejected.
2. Enroll a personal device and a restricted work device. The work credential requests a broader scope (including by forging claims, replaying an old token and using a refresh token) and is refused every time (M01-R01-AC01).
3. Revoke one device: its access and refresh tokens stop working within a defined bound while the other device keeps working.
4. Passkey registration and sign-in succeed on the owner's desktop browser and on Android; password-less recovery uses MFA recovery codes, shown once and not stored in plaintext.
5. A server token cannot be derived from, or replaced by, the local PIN verifier; resetting the PIN grants no new server access (SEC-05-AC01).
6. Rate limiting and lockout on sign-in and token endpoints, with no account-existence leakage (NFR-10).
7. Every authentication event and token issuance is audit-logged without secrets or tokens in the log (existing redaction chokepoint, ADR-0002).
8. Effort estimate in files and lines for each option, and the list of packages added (supply-chain review, licences).
9. Authorization failures default to deny when device, scope or lock state is missing (SEC-04-AC01).

## Spike evidence (2026-11-25)

Two throwaway apps outside this repository (nothing identity-related is merged), driven by **one shared contract test suite** (xUnit v3, in-process TestServer) so both options face identical tests: 12 tests per option, 24/24 passing on three consecutive runs. Package versions: ASP.NET Core Identity 10.0.15, OpenIddict 7.9.0. Option 1 = Identity cookie for the owner, Identity bearer tokens for devices, custom `device` table with per-request lookup, custom refresh-token rotation. Option 2 = Identity cookie for the owner, OpenIddict (authorization code + PKCE, refresh tokens) with **one OpenIddict public client per device whose permitted scopes are that device's maximum scope**.

| # | Acceptance test | Option 1 | Option 2 | Notes |
|---|---|---|---|---|
| 1 | One owner; second bootstrap rejected; 8 concurrent bootstraps give exactly one | pass | pass | Both rely on a single-row table with `CHECK (id = 1)`, not on an application check. |
| 2 | Work device cannot widen scope: at enrollment, via refresh, forged JWT, tampered token, replayed refresh token | pass | pass | Opt 1: scope is read from the device row on every request, never from the token; rotation and replay detection are **custom code**. Opt 2: scope permissions, rotation and replay detection come from OpenIddict. |
| 3 | Revoke one device: access and refresh tokens die at once; other device unaffected | pass | pass | Both need a database lookup per request for immediate access-token revocation (Opt 2 only with `EnableTokenEntryValidation` and `EnableAuthorizationEntryValidation`). |
| 4 | Passkeys on desktop browser and Android; recovery codes | **not run** | **not run** | Needs the owner's real browsers and Android Credential Manager. Cannot be done headlessly; still required before deciding. |
| 5 | PIN-derived value is not a server credential | pass (weak) | pass (weak) | Only proves a PIN hash is refused as bearer, refresh token and password. The real property (verifier never sent to the server) is a client design rule for the SEC-05 epic. |
| 6 | Lockout; no account-existence leak | pass | pass | Identical status and body for unknown user, wrong password and locked account. **Finding:** five bad attempts lock the sole owner out, so an attacker can deny the owner sign-in. Needs a decision (passkey-first sign-in and per-IP throttling). Token-endpoint rate limiting was not tested. |
| 7 | Audit without secrets | pass | pass | Audit rows and all captured log output (Debug level) contain no password, access token or refresh token. Audit is **custom code in both options**; neither option provides it. |
| 8 | Effort and packages | see below | see below | |
| 9 | Default deny with missing device, scope or lock | pass | pass | Anonymous, junk token and an owner browser session with no enrolled device are all refused on device-scoped endpoints. "Lock" state is not modelled yet (SEC-05 epic). |

**Mutation checks (tests can fail):** removing OpenIddict's token/authorization entry validation and zero reuse leeway made T3 and the replay test fail in Option 2; reading scope from the token claim instead of the device row made T3 fail in Option 1. A flaky concurrency failure that appeared during the spike was traced to the spike's own shared SQLite connection, fixed, and re-run 15 times clean.

**Effort (rough, scratch code, non-blank non-comment lines):** Option 1 `Opt1App.cs` 192, Option 2 `Opt2App.cs` 172, each including shared owner bootstrap, login, audit and endpoints. The difference is not size but **who owns the security-critical logic**: in Option 1 we wrote and must keep correct the device-scope check, refresh rotation, replay detection and scope-narrowing on refresh (roughly 70 lines, each with a failure mode the tests had to catch); in Option 2 these are library behavior plus configuration.

**Packages added:** Option 1: none beyond Identity EF and the EF provider. Option 2: OpenIddict 7.9.0 meta and sub-packages (about 20 `OpenIddict.*` assemblies, including client packages pulled in by `OpenIddict.AspNetCore` that a pure server does not use) and Microsoft.IdentityModel 8.23.0. Licence of the OpenIddict packages: Apache-2.0 (from the package metadata).

**Option 2 pitfalls found (each is a silent security downgrade if missed):**
- `SetRefreshTokenReuseLeeway` defaults to 30 seconds: a replayed refresh token still works inside it. The spike sets it to zero.
- Without `EnableTokenEntryValidation` / `EnableAuthorizationEntryValidation`, a revoked device's access token keeps working until it expires.
- Refresh tokens are only issued when `offline_access` is requested and permitted.
- Access-token encryption was disabled in the spike to inspect and forge JWTs; keep the default (encrypted) in production.

## Verification on the deployment host (2026-11-25)

Second round, run on the Windows 11 home PC that will host the deployment, against real runtimes instead of the in-process test server: Kestrel on `localhost`, PostgreSQL 18.7 in Docker, desktop Chrome 157, and an Android 15 Google Play emulator (Chrome 124). Same two spike apps; passkeys, recovery codes, rate limiting and key persistence are ASP.NET Core / platform features shared by both options, so they do **not** discriminate between them.

| Question | Result |
|---|---|
| Contract suite on PostgreSQL | 32/32 pass for both options (the in-memory SQLite run: 26 pass, 6 PostgreSQL-only tests skipped). One PostgreSQL-only defect found in the spike itself: an unquoted `CHECK (Id = 1)` passed on SQLite and failed on PostgreSQL. 12 repeated runs of the concurrent-bootstrap race: 0 failures. |
| Passkeys, server side (desktop Chrome 157, virtual authenticator) | Registration (attestation verified), discoverable sign-in and signature counter 1 to 2 all work, for both options. |
| Passkeys, **real** authenticator (owner's desktop Chrome) | Registration (`attachment=platform`, transports `hybrid` + `internal`) and sign-in both succeeded. The page must be the focused window or Chrome rejects the call. |
| Passkeys, Android | **Not verified.** Chrome receives the server's options and shows the passkey UI. It requires a device screen lock (`isUserVerifyingPlatformAuthenticatorAvailable` is false without one) and a credential provider; with none, only "use a different phone or tablet" is offered. With a signed-in Google account that already has Google Password Manager data, creation requires restoring that data with the screen lock of the owner's other device, so the test was stopped. Completing it needs a throwaway Google account with no existing passkey data, or the owner's real phone. |
| Native Android OIDC (Option 2) | A minimal native app (Java, no extra libraries) on Android 15: browser authorize, redirect to a private-use scheme (`com.savesphere.spike:/oauth2redirect`) handled by an intent filter, PKCE code exchange, authorized API call and refresh all returned 200. The owner's browser session stood in for the sign-in page. Not tested: Custom Tabs, verified App Links, an Expo/React Native auth library, process death mid-flow, offline use. Option 1 needs no browser hand-off (one-time enrollment code), which is simpler on Android but is custom client code. |
| Restart behavior (both) | With persisted keys, access and refresh tokens survive a restart. Without a persistent key location (the read-only container default) every token dies on restart. Option 1 needs the ASP.NET Core data-protection key ring persisted; Option 2 needs the OpenIddict signing and encryption certificates persisted (plus the key ring for cookies). Compose must give the API a writable volume for them, and the spike stored them unencrypted on disk, so protecting them at rest and covering them in SEC-10 recovery is open work. |
| Session cookie behind a TLS-terminating proxy (both) | **Finding.** Over plain HTTP, which is what the origin sees behind Cloudflare Tunnel, Identity's session cookie is issued **without `Secure`**; over HTTPS it has `Secure; HttpOnly; SameSite=Lax`. Needs an explicit secure-cookie policy and forwarded-headers handling. The fix itself was not run. |
| Rate limiting (both) | The built-in ASP.NET Core limiter, applied per client and path, throttles sign-in, token and recovery endpoints (5 allowed, then 429 in the test). Lockout of the sole owner by an attacker remains a decision. |
| Recovery codes (both) | **Finding.** Identity generates ten codes that redeem exactly once, but stores them in plaintext in `AspNetUserTokens`, which fails the "not stored in plaintext" criterion of test 4. They are also tied to the two-factor sign-in flow, not to passkey-only recovery. A custom hashed recovery mechanism is needed whichever option is chosen (SEC-10). |

**Still open before accepting:** real Android passkey creation and sign-in; Custom Tabs or App Link redirect handling and an Expo auth library against Option 2; the secure-cookie fix; encryption of keys at rest; the owner lockout policy; hashed recovery codes.

**Spike reading, not a decision:** every tested criterion is met by both options, and the deployment-level items (key persistence, cookie policy, recovery codes, throttling, passkeys) cost the same either way. The remaining differences are who owns the token-security logic (Option 1: roughly 70 custom lines that must be kept correct; Option 2: library behavior plus configuration with known pitfalls), dependency size (Option 2 adds about 20 packages), and the Android sign-in model (Option 2: standard OIDC through the browser, now shown to work; Option 1: custom enrollment-code flow). The decision is the owner's.

## Decision

**Option 2: ASP.NET Core Identity (user store, passkeys) plus OpenIddict as the OAuth 2.0 / OIDC server.** Web and Android both use authorization code with PKCE. Option 1 is not pursued; Option 3 stays rejected.

### Why

Both options met every tested criterion, and passkeys, recovery codes, throttling and key persistence cost the same either way. The deciding factor is who owns the security-critical token logic (M01-R01-AC01, NFR-10). Option 1 means roughly 70 custom lines (device-scope check, refresh rotation, replay detection, scope narrowing), each with a failure mode the tests had to catch. Option 2 turns them into library behavior plus configuration. Its extra dependency size is real but bounded (see "Dependency posture"), and the Android authorization-code flow was shown to work end to end. The pitfalls found are all configuration, and the rules below pin them with tests so a silent downgrade fails the build.

### Rules every implementation must follow (each becomes a test with a `[Requirement(...)]` trait)

1. **One OpenIddict public client per enrolled device.** Its permitted scopes are that device's maximum scope and can never be widened by a token request; widening requires the owner to re-enroll.
2. **OpenIddict hardening is mandatory and asserted by tests:** refresh-token reuse leeway is zero; `EnableTokenEntryValidation` and `EnableAuthorizationEntryValidation` are on (revocation is immediate); `offline_access` is granted explicitly; access-token encryption stays at its default (on) in production, so the spike's disabled encryption must not be copied; access tokens are short-lived (target 10 minutes, tune with evidence).
3. **Server scope comes from the device record and the OpenIddict authorization, never from a client-supplied claim.** Missing device, scope or lock state denies (SEC-04-AC01).
4. **Single owner** is enforced by the database (single-row table with a quoted `CHECK ("id" = 1)`, verified on PostgreSQL, not only SQLite), not by an application check.
5. **Owner bootstrap** uses a one-time setup secret supplied through a secret file or environment variable outside the repo (never a fallback default, never logged). It is consumed by the first successful bootstrap and refuses a second owner even under concurrent requests.
6. **Sign-in is passkey-first. There is no password credential for the owner.** Recovery is by hashed recovery codes only.
7. **Recovery codes are custom, not Identity's:** ten codes of at least 80 bits of entropy, shown once, stored only as keyed hashes (HMAC-SHA-256 with a server-side key held with the secrets in rule 9, constant-time compare), single use, redemption audited and throttled, and issuing a new set invalidates the old set. Identity's two-factor recovery codes are not used because it stores them in plaintext. The SEC-10 emergency package is built on top of this and never holds the keys and the codes in the same package.
8. **Lockout policy: an attacker cannot lock the owner out.** Passkey assertions are not subject to attempt lockout (they cannot be guessed). Recovery-code and enrollment-code redemption use per-client and global exponential backoff capped at 15 minutes, never a permanent lock, with identical status and body for unknown, wrong and throttled attempts (no account-existence leak). The built-in ASP.NET Core rate limiter fronts the sign-in, token, recovery and enrollment endpoints.
9. **Key material:** OpenIddict signing and encryption keys and the ASP.NET Core data-protection key ring live on a persistent writable volume, protected at rest with a key-encryption secret or certificate supplied from outside the repo (a Docker secret in compose), never baked into an image or committed. Without persisted keys every token dies on restart, so a test restarts the API and asserts tokens survive. The rotation procedure and inclusion in SEC-10 recovery are documented when the volume is introduced.
10. **Secure cookie policy:** the owner session cookie is always `Secure; HttpOnly; SameSite=Lax`. `UseForwardedHeaders` trusts only the configured proxy (loopback and the compose network), never arbitrary `X-Forwarded-*` from the internet. Tests assert that `Secure` is set behind a TLS-terminating proxy and that forwarded headers from an untrusted source are ignored.
11. **Audit and redaction:** every authentication event, enrollment, token issuance, refusal and revocation is written to the `audit` schema by custom code (neither option provides this) and goes through the existing redaction chokepoint (ADR-0002). Tests assert that no password, token, code or key appears in audit rows or captured logs.
12. **The PIN is client-side only.** Its verifier is never accepted by the server and a PIN reset grants no server access (SEC-05-AC01). Designed in the SEC-05 epic.

### Dependency posture

Reference only the OpenIddict packages a server with EF Core stores and token validation needs (`OpenIddict.Server.AspNetCore`, `OpenIddict.Validation.AspNetCore`, `OpenIddict.EntityFrameworkCore`), not the `OpenIddict.AspNetCore` meta package, which pulls in client assemblies a pure server does not use. Versions live only in `Directory.Packages.props`, pinned exactly (7.9.0 at decision time, Apache-2.0). Re-check licence and advisories on every upgrade.

### Disposition of the items left open by the spike

| Open item | Disposition |
|---|---|
| Real Android passkey creation and sign-in | Passkeys are an ASP.NET Core Identity and platform feature shared by both options, so this does not discriminate between them and does not block the decision. It **is a release gate** for M01-R01-AC01 and SEC-05: it must pass on the owner's real phone (or a clean throwaway Google account) and be recorded in `docs/e01-status.md` before either requirement is called verified. |
| Custom Tabs / verified App Link and an Expo auth library | The server speaks standard OIDC, so this is a client choice. It is decided in the Expo shell epic by a short spike: `expo-auth-session` against `react-native-app-auth`, Custom Tabs, a verified HTTPS App Link redirect preferred and a private-use scheme as fallback, process death mid-flow, offline use. It does not block the server work. Until it is run, Android sign-in is not claimed to work. |
| Secure-cookie fix | Decided (rule 10); implemented and tested in the identity epic. |
| Encryption of keys at rest | Decided (rule 9); implemented and tested in the identity epic. |
| Owner lockout policy | Decided (rule 8). |
| Hashed recovery codes | Decided (rule 7). |
| Least-privilege runtime database role (deferred from E01) | Adopted: the API connects as a DML-only role and the migrator keeps the owner role. Introduced with the first identity migration. |

### Implementation order (identity epic; each step a small commit citing its requirements)

1. `SaveSphere.Modules.Identity` project, `identity` schema and history table, single-owner table and bootstrap endpoint (rules 4, 5). Tests: M01-R01.
2. Passkey registration and sign-in, hashed recovery codes, throttling (rules 6, 7, 8).
3. OpenIddict server, device enrollment and per-device scope (rules 1 to 3). Tests: M01-R01-AC01 (work credential cannot widen scope by request, forged token, replay or refresh).
4. Revocation bound, key persistence and rotation, secure cookie and forwarded headers (rules 2, 9, 10).
5. Audit, redaction assertions and the SEC-01 default-deny authorization middleware (rules 3, 11). Tests: SEC-04-AC01.
6. Update `docs/threat-model.md` rows from Planned to Implemented only where a passing test exists.

Gates that stay visible until run: Android passkey on a real device, the Expo OIDC spike, SEC-05 lock behavior, the SEC-10 recovery package, backup of the key volume.

## Consequences

- Identity code may now be added in the order above. Until steps 1 to 5 are merged with passing tests, the API remains unauthenticated and must not be exposed outside loopback (ADR-0004 already binds ports to `127.0.0.1`; the Cloudflare tunnel stays deferred until auth exists).
- Whichever option wins, the `identity` schema is owned by a new `SaveSphere.Modules.Identity` module (ADR-0001), emits typed events through the outbox, and its tests carry `[Requirement("M01-R01")]`, `[Requirement("SEC-05")]` and `[Requirement("SEC-01")]` traits.
- Recovery (SEC-10) and the local unlock design depend on this decision and follow it.
