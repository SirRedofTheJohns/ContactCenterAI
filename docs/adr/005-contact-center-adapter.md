# ADR-005 — Contrato neutral de contacto y Genesys por etapas

Estado: Accepted — contrato local v0.1; ruta wire Genesys pendiente de spike. Fecha: 2026-09-30.

**Contexto.** No hay tenant. Digital Connector, Data Actions y Open Messaging tienen responsabilidades diferentes; SDK no hace una transferencia universal.

**Decisión.** IContactCenterAdapter neutral: entrada, entrega, solicitud handoff, contexto y acknowledgment. Mock modela failures/disorder/duplicates. Digital Connector es candidato para bot digital; Architect maneja routing/handoff; Data Actions para operaciones cortas. Open Messaging solo si se conecta canal externo. Revisión tenant M6 antes de adapter real.

**Alternativas.** Acoplar Domain a SDK: rompe tests/local y reemplazo. Open Messaging como API universal de bot: confunde transporte/canal. Declarar mock equivalente a integración real: evidencia insuficiente. Implementar todas las modalidades: scope inmanejable.

**Consecuencias.** Portfolio local termina sin licencias, pero no acredita experiencia tenant. Wire fields/auth/routing pueden cambiar el diseño de adapter y requerir baseline v0.2. Revisar ante tenant/capabilities disponibles y antes de escribir código Genesys.

Referencias: FR15–16/19; AC17–20; guía de integración y gate M6.

