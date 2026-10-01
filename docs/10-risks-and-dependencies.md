# Riesgos, dependencias y preguntas resueltas

Riesgos aceptados para local-MVP se mantienen visibles; no se transforman en aprobaciones de producción. Responsables son roles previstos, no personas que hayan aceptado formalmente.

| ID | Riesgo / severidad | Mitigación y evidencia | Dueño / gate |
|---|---|---|---|
| R01 | Acceso a tenant Genesys inexistente / alta para integración real | Mock neutral; gate M6 y separar TenantValidated | Integration; bloquea M6, no M0–M5 |
| R02 | Fuente real no soporta idempotencia/reconciliación / crítica | A01 explícito, contract test; deshabilitar write si no hay outcome confiable | Backend/Source owner; bloquea mutación real |
| R03 | Reglas ficticias insuficientes / alta | Solo CP-001 v1 sin dinero; negocio real valida otra versión | Business; bloquea producción |
| R04 | Hallucination/grounding bilingüe / alta | Citas, abstención, templates críticas y eval por idioma | AI/QA; bloquea release si gate falla |
| R05 | Prompt injection / alta | Gateway, mínimos privilegios, corpus review, red team | Security/QA; riesgo residual aceptado con synthetic-only |
| R06 | Revocación/index desincronizado / alta | SQL autoridad, no texto de Qdrant sin recheck | Knowledge; contract test M3 |
| R07 | Concurrencia/crash/handoff mezcla actores / crítica | Command state + leases + epoch, failpoints | Backend/Integration; M2/M4 |
| R08 | Hardware/Docker/SQL compatible o licencia local / media | Spike M1, registrar perfil; alternative no cambia autoridad sin ADR | DevOps; prerequisite implementación |
| R09 | Provider/retention/model cambia / alta | Modelo fijado, version bundle, contratos y reevaluación | AI/Security; bloquea datos reales/live release afectado |
| R10 | Scope creep y portafolio inconcluso / alta | M1–M5 pequeños; extensiones explícitas fuera MVP | Owner/Architect; revisar cada milestone |
| R11 | CI de producto y compatibilidad de renderers destino / media | 12 diagramas renderizados en Mermaid Live v12; revisar renderer de publicación y ejecutar pipeline al implementar | QA/DevOps; reportar límite Sprint 0 |
| R12 | Políticas de comunicación/PCI/privacidad sin revisión / alta | Sin pagos ni mensajes proactivos; nueva baseline por canal | Security/Business; bloquea canal/datos reales |

## Preguntas decididas para v0.1

No se necesita preguntar por cloud, tenant o CRM para cerrar el diseño local. Se decide monolito modular, SQL Server, Qdrant, HTTP source mock, OIDC sintético, CP-001 v1, acción Confirmar en UI, worker durable y adapter neutral. UI Angular entra después del API. LLM/modelo exacto no cambia autoridad y se selecciona con spike/evals; si carece de capability necesaria, revisar ADR antes de integrar.

## Decisiones que requieren evidencia futura

- Genesys: modalidad disponible, auth exacta, esquema de eventos, identidad de miembro, routing y aceptación de agente.
- Source real: expectedVersion, command receipt, constraints y permisos delegados.
- Proveedor: modelo/version, schemas/embeddings, tarifa, región y retención efectiva.
- Producción: reglas de negocio, HA/RPO/RTO, control contractual/privacidad y operaciones de contacto.

Estas preguntas son gates explícitos de sus fases, no huecos ocultos del freeze local. RPO/RTO productivos no se prometen en una máquina; M1 probará backup/restore local y documentará tiempos medidos.

