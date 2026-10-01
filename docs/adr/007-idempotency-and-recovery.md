# ADR-007 — Transporte at-least-once y estado Unknown

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** Un timeout no prueba que una transacción falló. Crash entre envío y receipt impide atomicidad middleware/fuente.

**Decisión.** Inbox/outbox SQL, intención/audit antes de envío, commandId estable, payloadHash, confirmación consumible, constraints/rowVersion y leases. Submitted recuperado consulta fuente. Unknown se reconcilia; reenviar solo con not-accepted definitivo y dedupe fuente. No exactly-once global ni retry ciego de POST. Handoff/outbound usa epoch y acknowledgment.

**Alternativas.** In-memory state o audit al final: pérdida/huecos por crash. Retry middleware por defecto para POST: doble efecto. Transacción distribuida: no disponible para fuente HTTP y complejidad innecesaria. Nueva key en cada retry: destruye dedupe.

**Consecuencias.** Ledger/job de recovery y estado visible incierto. Fuente debe satisfacer A01; si no, bloquear write real o resolver manualmente. Revisar por capacidad fuente o backlog que exija broker; mantener semántica y receipts.

Referencias: FR08–09/16/18; AC13–20; contratos. Para HTTP resilience usar política explícita por operación, no defaults de retries indiscriminados. [Microsoft HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience?tabs=dotnet-cli).

