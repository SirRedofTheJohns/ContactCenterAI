# B02 — Foundation .NET y contratos

Fecha: 2026-09-30. Estado: **DONE — scope estructural**. El DoR se revisó antes de código y la evidencia final se obtuvo de una ejecución real.

Este documento conserva el snapshot de cierre B02. El contrato inicialmente planned y la ausencia de rutas fueron reemplazados por el candidato B03/B04; [estado actual](b03-b04-ingress.md). La regresión Foundation conserva 21 checks, ahora verifica HTTP 426 para ingress y mantiene 503 sin configuración. B02 histórico no acredita el repositorio nuevo.

## Definition of Ready revisada antes de implementación

| Gate | Evidencia y límite |
|---|---|
| Baseline | [Freeze CLOSED](../architecture-freeze-v0.1.md), B01 CLOSED con 19 checks y dos logins reales |
| Requisitos | FR01 como capacidad futura; NFR12 dependencias, NFR13 host reproducible, NFR03 prohíbe recepción falsa. B02 no satisface FR01/AC04 por sí solo |
| Componentes | [C4 API/worker/SQL](../architecture/overview.md); ninguna nueva unidad de negocio |
| Contrato previo | [Host OpenAPI](../contracts/host-v0.1.openapi.json) y [conversaciones planned](../contracts/conversations-v0.1.openapi.json). Solo health tiene handlers en B02 |
| Decisiones | ADR-001/002/010/011 Accepted; [ADR-012](../adr/012-foundation-and-readiness.md) fija readiness y composición |
| Amenazas | No datos de miembros, cookies ni LLM. Health sin secretos, stack ni connection strings. Sin rutas mutantes o aceptación durable; bind loopback local |
| Oráculos | Grafo de referencias prohibidas; host real live 200, ready 503, `/v1/conversations` ausente. Worker no tiene jobs ni puertos |
| Dependencias | Shared framework .NET 10.0.12 y SDK 10.0.401 verificados. No requiere acceso Docker del agente ni nuevos paquetes NuGet |
| Demo / rollback | Build/checks locales reproducibles; detener solo el proceso propio. Sin migraciones o efectos de negocio que revertir |

La retirada del certificado diagnóstico del perfil Windows original quedó no confirmada tras cerrar el terminal; el servidor fue detenido. Se conserva en [B01](b01-runtime-identity.md) como limpieza local pendiente, sin atribuir un fallo a SQL/OIDC o añadir una dependencia al host B02 HTTP loopback sin credenciales.

## Alcance implementable

Cinco proyectos principales y un ejecutable de checks de arquitectura. Domain declara idioma ES/EN del contrato; Application declara el port de readiness; Infrastructure informa que la DB operativa todavía no está configurada. API traduce a HTTP; Worker arranca y espera cancelación, sin polling de SQL ni ejecución de comandos.

El contrato de conversaciones describe el primer slice futuro y sus reglas de autenticación/idempotencia/versionado. Está marcado planned y no sirve como documentación de endpoints disponibles. Mensajes conservan 202 solo después de commit SQL. Consentimiento y cancelación siguen fuera del LLM según contratos congelados.

## Evidencia de cierre

Ejecución del agente: restore inicial generó seis locks; luego locked restore PASS sin repetir build o smoke. Release PASS con cero warnings/errors y warnings como errores. **21 checks PASS** verificaron fronteras de proyectos, referencias compiladas, referencias OpenAPI, contrato planned, host loopback, respuestas health, readiness 503 sin DB, Problem Details sanitizado, ausencia de sesión/ruta de negocio y parada del proceso propio. Worker arrancó y se detuvo sin jobs.

Reporte: [b02-evidence.json](b02-evidence.json). No hubo nuevas dependencias externas NuGet. OpenAPI recibió checks estructurales/local refs; no se declara validación completa de OAS. No se repitieron SQL, OIDC o logins B01.

B02 entrega estructura compilable, no conversación durable o autorización de miembros. B03/B04 requieren completar contratos de sesión/CSRF, acceso a recurso y persistencia antes de handlers. Esas capacidades, las mutaciones, RAG, métricas AI y Genesys siguen pendientes.
