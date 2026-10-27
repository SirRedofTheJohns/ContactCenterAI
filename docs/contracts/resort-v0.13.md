# Resort v0.13 contract: web chat and container profile

2026-10-27. Extends the [v0.12 baseline](resort-v0.12.md) without changing its schema, offer/confirm/receipt semantics, ownership rules or channel authentication. Frozen v0.12 invariants still apply.

## Shared conversation turn

`ResortConversation.RespondAsync` is the single turn logic used by the WhatsApp bridge and the web chat. It returns a `ResortTurn`: `code`, `text`, `mode`, `interpreted` request, `missing[]`, `assumptions[]`, and optionally `offer`, `stays`, `catalog`, `availability`, `receipt` and `suggestions`. The interpretation is untrusted. It never selects the actor, and it can only lead to `ResortStore.Offer`, which validates, prices and stores a five-minute proposal.

- Actor: the web uses the verified cookie session. The bridge uses only a confirmed link bound to a live session (v0.12). Text that names a member, account or role is ignored.
- Public without a session: catalog, availability, policy, out-of-scope and clarification. Private (own stays, create, change, cancel, confirm) requires a customer actor. Otherwise the turn returns `SIGN_IN_REQUIRED` (web), `CHANNEL_LINK_REQUIRED` (channel) or `VERIFIED_CUSTOMER_REQUIRED` (employee).
- Clarification instead of defaults. A create request needs a room, both dates and an explicit guest count; a hedged count ("2 or 3") counts as ambiguous. The turn returns `RESORT_DETAILS_REQUIRED` with `missing` among `type`, `dates`, `departure`, `guests` and `guests-exact`, plus an example whose dates are relative to today. A change without a stated guest count keeps the booking's own count instead of the parser's v0.12 default of 2. Change or cancel without a `STAY-` code returns `STAY_REFERENCE_REQUIRED`, listing only the actor's confirmed stays.
- Availability without full dates or guests states its assumptions in `assumptions`.
- `RESORT_POLICY` answers from the fixed demo rules. `RESORT_OUT_OF_SCOPE` declines without inventing content.
- Confirmation stays explicit and bound to an offer. In the web it is the offer's Confirm button calling `POST /v2/resort/offers/{id}/confirm`; a typed `Confirm <offer id>` is the same closed command. A bare "yes" returns `CONFIRM_REFERENCE_REQUIRED`. A lost response is recovered with `GET /v2/resort/operations/{id}` for the same offer ID.
- Behavior change shared with the channel: create requests without an explicit guest count now ask instead of assuming 2. `ResortLanguage.Parse` itself is unchanged, so the declared v0.12 parser examples still evaluate the same.

## HTTP

- `POST /v2/resort/chat` takes `{text, language}` (closed DTO, `es|en`, 1–2000 characters) and a single `Idempotency-Key` (1–128), which becomes the offer request key. It needs the same Origin and antiforgery checks as every v2 write. Text passes the existing sensitive-data guard (cards rejected, e-mail and phone masked). Chat text is not persisted; only a resulting structured offer is.
- `GET /v2/resort/chat/profile` returns the active `mode` and `providerId`, plus ES/EN examples with dates relative to today.
- The bridge key is optional. Without it the resort and chat work, `/internal/resort/channel` is not mapped, and the existing middleware rejects that path. A key shorter than 32 characters still fails startup.

## Interpreters

- `deterministic` (default, `resort-rules-v0.12`): the v0.12 parser plus guest/policy/reference helpers. No model, network or download.
- `local-llm` (`CCAI_RESORT_INTERPRETER=local-llm`): `OllamaResortInterpreter` uses the existing pinned `qwen3-vl` digest, raw wrapper, loopback endpoint, eight-second deadline and a closed six-field schema (`resort-intent-local-v1`). Output that is not exactly that schema is rejected, not repaired. Pin mismatch, timeout and transport failure each return their own code. There is no fallback to the deterministic interpreter and no automatic download. The channel bridge always stays deterministic.
- Endpoint (2026-10-27): `CCAI_OLLAMA_ENDPOINT` accepts only `http://127.0.0.1:11434`, or, in container mode only, `http://host.docker.internal:11434` (Docker Desktop forwards it to the host loopback). Any other value fails startup with `OLLAMA_ENDPOINT_INVALID`. The compose file selects the interpreter with `CCAI_DEMO_INTERPRETER` (default `deterministic`).
- Warm-up: in `local-llm` mode a startup service checks the pinned digest and loads the model once with the inference load options and no prompt. Its failure changes nothing; the eight-second answer deadline is unchanged.

## Container profile

`CCAI_DEMO_CONTAINER=true` (demo profile only):

- Kestrel listens on the container interface. Docker publishes 7452 and the sign-in port (18080 by default, `CCAI_DEMO_IDP_PORT`) only on the host loopback; that published binding is the outer network boundary. The in-process check now accepts loopback or private (RFC 1918 / ULA) peers. The Host must still be `127.0.0.1:7452`, and Origin and antiforgery are unchanged.
- `CCAI_OIDC_METADATA_ADDRESS` may point discovery, token and key retrieval at the compose network. It must be an `http` URL ending in the `contactcenterai-local` realm discovery path. `CCAI_OIDC_ISSUER` may move the browser-facing port. It must be `http://localhost:<port>/realms/contactcenterai-local`, otherwise startup fails with `OIDC_ISSUER_INVALID`. Tokens are validated against that issuer and audience. Identity bindings keep the logical v0.12 issuer, so the synthetic users resolve to the same members, and the CSP `form-action` follows the issuer origin.
- `/` redirects to `/resort.html` because the container has no legacy HTTP source (Simulator). The v0.10 assistant runs from source.

## Acceptance

`tests/ContactCenterAI.ResortChatChecks`: actual HTTP middleware, no bridge key. It covers visitor vs. customer vs. employee, clarification without offers, idempotent proposals, typed yes, cross-customer confirm/cancel denial, identity claims in text, change preserving guests, occupied change keeping the original, repeated confirm and receipt lookup returning one receipt, the model-output contract with fixtures, the explicit no-fallback errors and the metadata address restriction. `tests/ContactCenterAI.ResortChecks` continues to cover the v0.12 store and channel bridge.
