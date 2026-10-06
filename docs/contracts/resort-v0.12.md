# Resort v0.12 executable contract baseline

2026-10-06. Engineering review before code. Policy RB-001, CP-001 cancellation and RS-001 reschedule; prices synthetic USD minor units. One property, three units, ninety-day horizon, one to fourteen nights. Dates ISO; half-open nights and property timezone America/Santo_Domingo.

## Implementation refinement

Use one **new** `.local/resort/resort.db` for the bounded resort source, quote/command journal and links. The C# source is an in-process module, not an additional remote source service. This simplifies a local demo and makes allocation/receipt commits atomic while preserving the historical HTTP v1 source and operational databases. Queued command is committed separately before execution; retry/recovery uses the same offer/command ID. SQLServer/remote source deployment is not claimed. This replaces the proposed separate v0.12 source/workflow databases; it does not weaken uniqueness, confirmation or historical preservation.

RoomType/unit/catalog/rate rows are a versioned seed. Allocation primary key `(unit,night)` and exclusive booking/block owner. Stay owner is server memberRef. Offer stores actor session, route, epoch, request ID, action, exact dates/type/guest count, source version, price snapshot, expiration, command ID and disposition. Confirmation commits Queued once. Source command transaction validates and writes booking/allocation/receipt together; same command returns its existing authoritative receipt. Rejected commands also have durable receipts. Maintenance has version checks and cannot displace a booked night. No hold, payments or partial reschedule.

## HTTP surface

The resort login form may request the fixed local `/resort.html` return page. Only that constant is honored; no caller-supplied redirect URL is accepted. Start-Resort may restart only a uniquely identified, owned local ChannelHost assembly. It does not widen the tunnel, change Meta permissions or erase the ledger.

All `/v2/resort` web writes require the existing origin+antiforgery checks and verified cookie actor. LocalOnly middleware and loopback binding remain. Public GET catalog and calendar expose room/date occupancy, never customer identity or booking identifiers. GET my-stays/offer/operation needs a current Customer actor. POST offers accepts `{action,stayId?,type?,arrival?,departure?,guests?}` and an Idempotency-Key; server chooses owner. POST offers/{id}/confirm accepts `{decision:"confirm"}`. Cross-session/account IDs return RESOURCE_NOT_FOUND. POST blocks and DELETE blocks/{id}?version=n need OperationsAdmin; writes are audited.

POST link-codes creates 128-bit one-use code (5 minutes) for the current verified actor/session; code response has no-store and is displayed only in the local authenticated UI. GET link-status shows pending route masked, not its full address. POST link-confirm activates that pending route in the **same** session, bounded by session expiration and 30 minutes. POST unlink invalidates link and unconsumed offers. Session validity is reread through IOperationalStore on each linked request; logout, role reduction, inactive principal and expiry fail closed.

`/internal/resort/channel` is **not** under the public channel host. It lives on the existing loopback web API and requires a separate 256-bit configured service key, fixed endpoint/channel values and bounded JSON input. DTO: `{route,epoch,endpoint,channel,text,eventKey,language}`; route is opaque namespace/HMAC key, not memberRef. Endpoint allowlist must match private configured test resources. Private identity is resolved from persisted verified link, never DTO roles. Client uses fixed loopback URL, no redirects, deadline. Source errors return stable codes without exception/payload logs. Internal key goes only to the two local services, never Meta. No CORS/admin/tunnel expansion.

## Language/parser boundary

Deterministic mode recognizes catalog/amenity/availability, own-reservation, booking/change/cancel, offer-bound confirm and linkage. ISO dates are accepted; explicit named-month ranges support ES/EN. Unknown/partial dates ask for ISO dates rather than guessing. No arbitrary SQL/URLs/tools; freeform LLM extraction is not a readiness shortcut. `/es`, `/en` and a small explicit language-alias set persist language. Link commands bypass LLM and are encrypted with the existing ledger key while waiting in the channel inbox; clear their text after completion. Exact offer IDs prevent a bare yes from confirming a stale proposal.

## Acceptance oracles / review closure

- Source concurrency: two different actor/session offers target same unit/nights; after concurrent confirms, one Completed, one Rejected, one reservation's allocations.
- Change overlap excludes own nights; occupied target preserves exact old row/version/allocations and produces rejected receipt. Cancel frees all nights atomically; boundary at 72h passes and 72h-1ms fails. Change evaluates **old** arrival cutoff.
- Rate/version changes, offer expiry, wrong actor/session/route/epoch, wrong payload for reused idempotency key, invalid capacity/date/horizon: zero writes. Duplicate confirmation returns existing receipt and never consumes capacity again.
- Durable Queued command is recovered by same ID after restart; committed receipt is returned if the response was lost. A source transaction rolls back on storage error; no success announcement without receipt.
- Maintenance collision with a reservation or existing block rolls back completely. Read-only calendar contains no owner/reservation identifiers. Nonadmin cannot block; member B cannot see/change member A.
- Link one-use/expiry/session mismatch, malicious code attempts, wrong endpoint, missing service auth, logged-out session and revoked role: no active authority. Code excluded from LLM/plain stored input/public logs. Failed API call does not silently use public response for a private operation.
- Handoff/epoch change prevents new confirm; v0.11 already-transporting race remains documented. Outbound receipt/cost restrictions unchanged.

Tests use controlled clocks, isolated SQLite, real concurrency and fake transport; full relevant regression before activation. UI has local customer/login/calendar/link/confirm controls; shows fictional prices and scenario. Feature flag `CCAI_RESORT_ENABLED=true` plus matching bridge key enables it; unset leaves v0.11. Rollback flag preserves new DB and outstanding commands. No legacy data reset. CI executes resort checks, parser checks and pertinent old suites; blocked remote runner remains a separate gate.

**DoR reviewed and complete for this local bounded implementation**: scope, topology, authority, schema invariants, wire surface, recovery, parser limits, negative oracles, UI and rollback specified. This is the agent's engineering review under the owner's implementation request, not independent security approval or live evidence.
