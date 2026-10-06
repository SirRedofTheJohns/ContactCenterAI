# ADR-025 — Live inventory and scoped resort booking through messaging

Status: **Proposed**, 2026-10-06. [ES proposal](../resort-booking-v0.12.es.md) · [EN proposal](../resort-booking-v0.12.en.md).

The owner requested a fictional hotel calendar, available/occupied dates, reservation creation, changes and cancellation through WhatsApp, then expanded the catalog to room categories, nightly prices and amenities such as a jacuzzi. v0.11 deliberately has no private reservation capability; a new baseline is required before implementation.

Propose three room units, one per Standard/Deluxe/Suite category. Catalog and nightly rates are versioned structured source data. Availability comes from live inventory, not RAG. Governed knowledge still explains policy. Money uses integer minor units, fixed currency and synthetic totals; no payment/refund capability.

Use source transactions and unique unit/night allocations for both bookings and maintenance. Proposals hold no stock, expire in five minutes and require explicit confirmation. Revalidate prices, capacity, ownership, policy and current inventory before commit. Reschedule allocates the target and releases the previous nights atomically. Receipts and stable command IDs handle replay and uncertainty.

Public catalog/search do not identify a customer. Private operations require dual proof: one-use code generated from an authenticated local web session, submitted from a signed/allowlisted channel, then confirmed in the same web session. Short-lived linkage is checked for every confirmation. Service authentication alone cannot grant a member role. No login/admin/internal API is exposed through the tunnel.

Use an isolated resort-v0.12 source/workflow profile with v2 contracts, preserving historical v1 databases, channel deliveries and evaluations. Distinguish STAY reservation IDs from legacy RES IDs. Never infer checkout dates for historical records that lack them.

Rejected alternatives: calendar text in vector storage (staleness); phone-to-member automatic mapping (no account proof); unconditional LLM writes (no business authority); cancel-then-rebook change (loses old booking on conflict); in-memory availability locks (not crash-safe); external calendar/payment services (unneeded cost/dependency).

Consequences: adds inventory, quotes, account-link lifecycle, admin calendar and recovery acceptance. General natural-language date parsing remains a measured gate. A remote/mobile login experience needs a separate HTTPS deployment design. This proposal does not weaken v0.11 or mark its existing live gates closed.

En español: el calendario será una fuente transaccional, las políticas siguen en conocimiento aprobado y el LLM solo propone. Tres categorías con precios ficticios. Vincular cuenta requiere sesión web y control del chat; el teléfono no otorga identidad. Reserva/cambio/cancelación requieren oferta, confirmación y recibo. El perfil nuevo preserva historia y mide sus propios resultados antes de publicarse como funcional.
