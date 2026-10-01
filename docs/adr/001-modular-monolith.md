# ADR-001 — Monolito modular y worker

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** Un desarrollador necesita demostrar confiabilidad sin desplegar una organización ficticia de microservicios. Cancelación y estado requieren atomicidad local; integración fuente sigue siendo distribuida.

**Decisión.** API/BFF .NET con módulos Domain/Application/Infrastructure y worker .NET separado para jobs durables. Un source/contact simulator externo. No broker/Redis/Kubernetes en v0.1.

**Alternativas.** Microservicio por módulo: más fallos/red/contratos sin escala justificada. Todo en requests API: pierde recuperación si cae/vence el request. Todo Python: reduce ajuste al requisito principal C#/.NET.

**Consecuencias.** Menos despliegues y transacciones SQL más simples; hay que cuidar límites de dependencias y leases entre API/worker. Worker no significa un servicio de negocio independiente. Revisar si equipos/escala o backlog medido exigen extracción de un módulo o broker.

Referencias: FR01/09/18; NFR03/12; C4 y backlog B02/B04/B07.

