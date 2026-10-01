# Architecture Freeze v0.1

Fecha: 2026-09-30. Estado: **CLOSED — baseline de arquitectura del MVP local**.

## Alcance de la baseline

Demo local de ContactCenterAI, un club/tenant sintético, web chat ES/EN, RAG público/interno gobernado, reserva propia, cancelación sin penalidad CP-001 v1, confirmación UI autenticada, recovery durable y handoff mock. Backend C#/.NET, SQL autoritativo, Qdrant proyección y Python offline. Sin producción, datos reales, pagos/voz/SMS/WhatsApp ni tenant Genesys validado.

La evaluación técnica y las decisiones se toman con el mandato del usuario de actuar como arquitecto. No se inventa aceptación del propietario, QA, negocio, seguridad o terceros. Este registro cierra el diseño local tras checks; no representa autorización de producción.

## Decisiones congeladas

ADR-001 a ADR-010 Accepted para local-MVP. Invariantes INV01–08 obligatorios. FR01–22 y NFR01–18 son baseline; AC01–32 especifican oráculos. Contratos internos y adapter neutral v0.1; wire protocol Genesys/modelo exacto/imágenes patch se validan en spikes de sus slices sin ampliar autoridad.

Cambios de reglas, confirmación, acceso, efecto fuente, ownership o alcance requieren nuevo ADR y revisión de freeze v0.2. Cambio de patch compatible/fixture editorial se registra sin romper baseline; todo cambio de prompt/modelo/corpus también exige regresión según su impacto.

## Checklist de cierre

| Gate de arquitectura | Evidencia |
|---|---|
| Evaluación / Charter / Scope | docs/00–02 |
| FR / NFR / UC / AC / traceability | docs/03–06 |
| C4 / secuencias / estados / modelo de datos | architecture/ y diagrams/ |
| Seguridad / threat model / riesgos | security/ y docs/10 |
| RAG / tools / identity / transactions | architecture/rag.md, contracts/api-and-tools.md |
| Adapter Genesys / mocks / gate tenant | integrations/genesys-cloud.md |
| Evals ES/EN / seeds / oráculos | evaluation/ y docs/evaluation/ |
| ADRs / DoR / backlog / operación | adr/, docs/07–09 y operations/ |
| Consistencia y verificación de artefactos | [PASS de diseño](verification-v0.1.md) |

## Pendientes y bloqueantes por fase

No hay bloqueante conceptual identificado para el MVP local bajo A01–08. Antes de M1 se verifican runtime/SQL/OIDC y se formaliza OpenAPI del slice; antes de live AI modelo/retention/budget; antes de M6 tenant/wire/routing/identity; antes de datos reales negocio/seguridad/privacidad/HA. Estos gates no se declaran superados por escribir documentos.

Esta entrega finaliza Sprint 0 y se detiene antes de código productivo. Una fase posterior puede iniciar B01 siguiendo Definition of Ready. El sistema no es runnable ni sus métricas están demostradas.

