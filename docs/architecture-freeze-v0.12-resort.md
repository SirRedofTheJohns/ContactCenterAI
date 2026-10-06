# Architecture Freeze v0.12 — Resort calendar and channel booking

Date: 2026-10-06. Status: **OPEN / design proposal. No productive implementation authorized by this document.** The user requested this expansion; this file records the engineering readiness review, not a separate user-permission requirement.

[Spanish packet](resort-booking-v0.12.es.md) · [English packet](resort-booking-v0.12.en.md) · [Proposed ADR-025](adr/025-resort-booking-calendar.md).

The prior public-channel freeze remains valid for the running FAQ. New private channel operations need a new baseline. Do not inject a reservation source into the current public responder before this review closes.

## Decisions proposed

- Three units/categories, versioned amenities and fictional USD nightly prices; live source inventory with unique unit/night allocation.
- Nights are a half-open date range in the property's timezone; exact occupancy and atomic changes, no payment side effects.
- Public availability; private booking only with web/channel dual proof and short-lived server-owned linkage.
- Five-minute quotes, explicit offer-bound confirmation, durable command/receipt, no stock hold or blind retry.
- OperationsAdmin for maintenance; no override of existing bookings. Separate new scenario/profile preserves historical v1 data and reports.
- C#/.NET remains runtime; Python evaluates offline. Simulator/local model modes and real WhatsApp transport are reported separately.

## Remaining review work before closing

1. Versioned neutral and HTTP contracts, schema constraints and error codes for inventory/quote/command/receipt/linkage.
2. Implementable session expiry/logout/revocation bridge and credential/CSRF/input-redaction design; prove no private HTTP route is tunneled.
3. Precise deterministic oracles for concurrent booking, overlapping reschedule, price/version changes, account linking and recovery.
4. Parser date/intent limits and source-fact response format; reconcile the new profile with historic cancellation/source interfaces.
5. UI calendar and confirmation interaction, feature flag, rollback and preservation/migration review.

No architecture completeness or live-ready claim until these checks close. The 2026-10-06 activity only prepares design and restores existing communications; it adds no source write capability to WhatsApp.

Estado: propuesta abierta. Antes de código productivo faltan contratos, esquema, diseño implementable de vinculación/revocación, casos negativos y revisión de impacto. Esto es trabajo técnico pendiente, no una solicitud de permiso adicional al dueño. El diseño conserva la demo anterior y los límites de autoridad del LLM.
