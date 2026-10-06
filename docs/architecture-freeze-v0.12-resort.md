# Architecture Freeze v0.12 — Resort calendar and channel booking

Date: 2026-10-06. Status: **CLOSED for bounded local implementation before productive code.** The owner explicitly requested implementation. [Executable contract and review](contracts/resort-v0.12.md) resolves the items below, including a co-located new resort SQLite source/journal, session-bound linking, internal service authentication, negative oracles and parser limits. This is an engineering review, not independent security approval. Live acceptance remains open.

[Spanish packet](resort-booking-v0.12.es.md) · [English packet](resort-booking-v0.12.en.md) · [Accepted ADR-025](adr/025-resort-booking-calendar.md).

The prior public-channel freeze remains valid for the running FAQ. New private channel operations need a new baseline. Do not inject a reservation source into the current public responder before this review closes.

## Accepted decisions

- Three units/categories, versioned amenities and fictional USD nightly prices; live source inventory with unique unit/night allocation.
- Nights are a half-open date range in the property's timezone; exact occupancy and atomic changes, no payment side effects.
- Public availability; private booking only with web/channel dual proof and short-lived server-owned linkage.
- Five-minute quotes, explicit offer-bound confirmation, durable command/receipt, no stock hold or blind retry.
- OperationsAdmin for maintenance; no override of existing bookings. Separate new scenario/profile preserves historical v1 data and reports.
- C#/.NET remains runtime; Python evaluates offline. Simulator/local model modes and real WhatsApp transport are reported separately.

## Reviewed items (resolved in the executable contract)

1. Versioned neutral and HTTP contracts, schema constraints and error codes for inventory/quote/command/receipt/linkage.
2. Implementable session expiry/logout/revocation bridge and credential/CSRF/input-redaction design; prove no private HTTP route is tunneled.
3. Precise deterministic oracles for concurrent booking, overlapping reschedule, price/version changes, account linking and recovery.
4. Parser date/intent limits and source-fact response format; reconcile the new profile with historic cancellation/source interfaces.
5. UI calendar and confirmation interaction, feature flag, rollback and preservation/migration review.

The earlier design/restoration activity added no private capability; implementation now proceeds under this closed local baseline. Do not claim live-ready until test and provider evidence exists.

Estado: CLOSED para implementar el alcance local; revisión de contratos, invariantes de esquema, vinculación/revocación, casos negativos e impacto registrada antes del código. Evidencia en vivo pendiente. El nuevo almacén resort agrupa fuente y journal; conserva la demo anterior y la autoridad determinista.
