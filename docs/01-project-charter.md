# Project Charter

## Problema y propósito

Los centros de contacto necesitan respuestas basadas en conocimiento vigente y acciones que respeten identidad, permisos y políticas, con continuidad al transferir a un humano. ContactCenterAI demostrará cómo diseñar y, en una fase posterior, construir ese flujo en C#/.NET. El proyecto también prepara al candidato para explicar decisiones, riesgos y evidencia en una entrevista de AI Agent Developer.

Producto ficticio: **Caribbean Horizon Vacation Club**. Datos, reservas, políticas y usuarios son sintéticos. GBS es una referencia de competencias, no cliente ni fuente de datos.

## Resultado del MVP

Un cliente ES/EN consulta una política, inicia sesión, ve una reserva propia, obtiene una oferta determinista de cancelación sin penalidad, la confirma y recibe un resultado verificado. Casos no permitidos o inciertos terminan en abstención o handoff. Un empleado asignado puede consultar conocimiento interno y recuperar el contexto de transferencia. El sistema conserva auditoría y permite reproducir fallos.

## Stakeholders y responsabilidades

| Rol del proyecto | Responsabilidad |
|---|---|
| Usuario / propietario del portafolio | Prioridad de alcance y aceptación posterior del producto |
| Arquitecto responsable en Sprint 0 | Evaluación crítica, baseline, decisiones y revisión de consistencia |
| Desarrollo | Implementar por slices con contratos y controles deterministas |
| QA / evaluador | Casos ES/EN, invariantes, regresiones y reporte de resultados |
| Operación de contacto, negocio, seguridad y DevOps | Roles modelados; deberán validar políticas e integración antes de datos reales |

Los últimos roles no representan personas que hayan firmado o revisado este proyecto. No se simula su aprobación.

## Criterios de éxito

- Architecture Freeze v0.1 cerrado para el alcance local, con requisitos, diagramas, amenazas, ADRs y trazabilidad.
- Posteriormente, demo reproducible desde un checkout limpio sin acceso a Genesys real, con datos sintéticos.
- Flujo ES/EN equivalente y acciones críticas con autorización, confirmación, idempotencia y resultado fuente.
- Evals reproducibles y seguridad obligatoria según [plan de evaluación](evaluation/plan.md); no publicar métricas inventadas.
- Explicar en inglés y español qué está implementado, simulado, medido y pendiente de validación.

## Restricciones

C#/.NET es el backend principal. Python se limita a evaluation/data tooling. SQL mantiene estado autoritativo. Adapter de contacto desacoplado, mocks locales y Mermaid versionado. No implementación productiva en Sprint 0. Un único operador puede demostrar el sistema; no se adopta una disponibilidad empresarial como hecho medido.

## Hitos y salida

| Hito | Salida verificable |
|---|---|
| M0 — Arquitectura | Baseline local v0.1 cerrado y paquete de documentación |
| M1 — Fundamentos | Autenticación, SQL, estado durable y simulador |
| M2 — Transacción | Cancelación sin LLM, confirmación vinculada, recovery y concurrencia |
| M3 — IA / RAG | Flujo bilingüe y documentos gobernados sin romper M2 |
| M4 — Contact center local | Handoff y contrato adapter con fallos simulados |
| M5 — Portafolio | UI mínima, evals, CI, trazas y guion reproducible |
| M6 — Integración real opcional | Sandbox Genesys, capacidades, seguridad y smoke tests evidenciados |

Orden orientativo: 6–8 semanas a tiempo parcial para M1–M5, sujeto a disponibilidad y experiencia del desarrollador. No es compromiso de entrega. M6 depende de acceso y licencias, por lo que no condiciona la demo local.

