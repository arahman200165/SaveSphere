# ADR-0005: Authentication, device credentials and recovery

- **Status:** proposed (options and acceptance tests defined; **no library is chosen until the spike evidence below is recorded and the owner decides**)
- **Date:** 2026-11-25
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

## Decision

Pending. Current leaning, to be confirmed or overturned by the evidence above: **option 2**, because per-device scoped and revocable tokens are central to M01-R01 and are better expressed by an established token server than by custom code; fall back to option 1 if the spike shows OpenIddict is disproportionate for one owner and two devices.

## Consequences

- Until this ADR is accepted, no identity code, no user tables and no authentication middleware are added. The API remains unauthenticated and must not be exposed outside loopback (ADR-0004 already binds ports to `127.0.0.1`; the Cloudflare tunnel is deferred until auth exists).
- Whichever option wins, the `identity` schema is owned by a new `SaveSphere.Modules.Identity` module (ADR-0001), emits typed events through the outbox, and its tests carry `[Requirement("M01-R01")]`, `[Requirement("SEC-05")]` and `[Requirement("SEC-01")]` traits.
- Recovery (SEC-10) and the local unlock design depend on this decision and follow it.
