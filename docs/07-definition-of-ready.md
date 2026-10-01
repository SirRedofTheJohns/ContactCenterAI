# Definition of Ready y Definition of Done

## Ready para un slice de implementación

Todos los puntos deben tener evidencia concreta; «lo veremos al programar» no satisface un requisito crítico.

- Architecture Freeze local está CLOSED y slice pertenece al scope aprobado.
- IDs FR/NFR/AC definidos, actores y happy path/fallos claros; política y límites sin ambigüedad.
- Componente en C4 y flujo/estado reflejados en diagramas; autoridad de cada dato explícita.
- Contrato/version/error codes/identity/resource checks/DTO mínimo definidos; antes de handlers se revisa OpenAPI de ese slice.
- ADR aplicable Accepted; si cambia una decisión, existe ADR nuevo y revisión de baseline.
- Amenazas afectadas y controles obligatorios identificados, incluyendo race/replay/Unknown cuando hay efectos.
- Fixtures/oráculo y estrategia de pruebas definidos; budget, métricas y logging sin PII especificados.
- Dependencia/spike de compatibilidad completado para ese slice (ej. OIDC/SQL en M1); mocks disponibles o tarea previa para producirlos.
- Criterio de demo/review, rollback/fallback y gate de release definido; ningún bloqueante de seguridad/contrato crítico abierto.

El freeze **no** hace automáticamente Ready a todos los tickets. M1 puede crear el esqueleto tras resolver runtime/SQL/IdP; M2 espera contratos source; M6 espera tenant. Ready es por slice, no permiso para saltarse dependencias.

## Done para implementación futura

Comportamiento cumple AC y controles fuente, pruebas apropiadas pasan, resultado/fallos documentados, traces/audit sanitizados verificados y diagramas/contratos actualizados. PR revisable, dependencias/pipeline sin bloqueantes y demo reproducible. Cambios de IA incluyen evals y versiones; mutaciones incluyen concurrency/failpoints. Limitaciones declaran MockValidated, LiveAIValidated o TenantValidated según evidencia real.

## Done para Architecture Freeze v0.1

Charter, alcance, FR/NFR, AC, C4, secuencias/estados/ERD, contratos, seguridad, threat model, RAG, integración, eval plan, ADRs, riesgos, DoR y backlog presentes. Trazabilidad cubre MUST y controles críticos. Revisión arquitectónica no deja bloqueantes **para la demo local**. Enlaces/diagramas/dataset revisados con alcance de verificación declarado. Registro de cierre distingue diseño de producción y no inventa firmas de stakeholders.

