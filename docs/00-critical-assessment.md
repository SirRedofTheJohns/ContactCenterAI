# Evaluación crítica del proyecto y ajuste a GBS

**Estado actual:** [demo local v0.8 y evidencia](progress/demo-v0.8.md). El plan v0.1 conservado abajo es la referencia empresarial; la implementación local y sus límites están documentados por separado.


Fecha: 2026-09-30. Base: descripción completa de AI Agent Developer aportada por el usuario en la conversación «Analizar proyecto técnico». No se verificó que la oferta continúe abierta. La descripción sirve como requisito de referencia; las recomendaciones de la conversación anterior se reevaluaron como hipótesis.

## Dictamen

El proyecto tiene buen ajuste al rol si entrega un flujo transaccional pequeño, verificable y operable. La propuesta inicial enumeraba muchas tecnologías, pero dejaba abiertos identidad, autorización sobre recursos, semántica de confirmación, resultados inciertos y transferencia de propiedad de la conversación. Un listado de herramientas y diagramas sin esos contratos sería una demo de chatbot, insuficiente para sostener una entrevista de ingeniería empresarial.

La recomendación es un **monolito modular con puertos/adapters y un worker durable**, no microservicios ni una red de agentes autónomos. El caso de cancelación es suficiente para mostrar casi todos los requisitos principales. El knowledge assistant interno y la recepción del handoff aportan una segunda perspectiva de contacto con empleados. Agent Assist en tiempo real, voz y pagos reales quedan como extensiones explícitas.

## Matriz de ajuste a la oferta

| Requisito de la vacante | Decisión v0.1 | Evidencia que deberá producirse | Brecha honesta |
|---|---|---|---|
| C#/.NET y REST | API, Domain, Application, adapters y worker .NET | Flujo API, reglas y pruebas de integración | Diseño; todavía sin código |
| SQL y sistemas de miembros/reservas | SQL Server; sistemas fuente separados en simulador | Constraints, transacciones y concurrencia | No CRM empresarial conectado |
| LLM, estado, tools y structured outputs | Un orquestador acotado; SQL autoritativo | Reanudación, propuestas inválidas y límites | Modelo exacto por evaluar |
| Reglas críticas separadas del razonamiento | Policy Engine + Source Adapter | Bordes de 72 h, propiedad, confirmación y revalidación | Políticas son ficticias |
| RAG interno, permisos, aprobación y feedback | ACL por documento/versión; publicación gobernada | Fuente permitida, expiración y reversión de índice | Sin repositorio empresarial real |
| Genesys Architect, Data Actions e integraciones | Port desacoplado; mock; spike de Digital Connector | Contratos y fallback, luego pruebas en tenant | Mocks no equivalen a experiencia en tenant |
| Transferencia con contexto y fallback | Ownership epoch, solicitud durable y acknowledgment | Nunca responder como IA tras handoff confirmado | Routing real pendiente |
| Atención ES/EN | Mismo workflow y corpus equivalente | Métricas por idioma y escenarios pareados | No acreditación de inglés profesional |
| Seguridad, PII, RBAC, secretos | OIDC, políticas por recurso, minimización y auditoría | Ataques directos/indirectos y aislamiento | No certificación PCI ni validación jurídica |
| Idempotencia, retries, timeout, recuperación | Inbox/outbox, claves semánticas y reconciliación | Efecto único frente a duplicados y timeout después del commit | Fuente real debe soportar comprobación |
| Observabilidad, soporte y costos | OTel, métricas acotadas y runbooks | Traza de extremo a extremo e incidente reproducido | SLOs son objetivos, no resultados |
| QA, evals y regresiones | C# para invariantes; Python para corpus/evals | Reporte versionado, ES/EN, fallos y latencia | Seed inicial no es benchmark final |
| Git, reviews y CI/CD | Gates de documentación, código, seguridad y despliegue | PR revisable y rollback de un release | Pipeline de producto posterior |
| Agent Assist, transcripción y after-call work | Resumen textual con hechos verificados en MVP; asistencia después | Resumen de cancelación / handoff | Sin voz, transcripción o realtime assist en v0.1 |
| Angular, Azure/OpenAI y Python | UI Angular posterior; port de LLM; Python offline | Vista mínima y comparación offline | No routing multi-provider en runtime |
| Experiencia profesional 3+ años y grado | No se infiere del repositorio | Explicar decisiones y experiencias reales | Un portafolio no sustituye esos requisitos |

## Correcciones concretas al diseño previo

1. **Identidad:** una reserva o un número de miembro escrito en el chat no autentica a nadie. El usuario pasa por un IdP local con cuentas sintéticas; el backend establece el vínculo principal-miembro. El paso futuro de identidad de Genesys requiere evidencia firmada y mapeo validado.
2. **RBAC:** un agente no puede leer cualquier cuenta porque tenga rol Agent. También necesita una asignación activa a la conversación; el cliente solo accede a recursos propios. Supervisor no obtiene override automático de políticas.
3. **Cancelación:** la confirmación se vincula a una oferta concreta, versión de reserva/política, principal, conversación y vencimiento. «Sí» ambiguo o una propuesta del modelo no es una autorización persistida.
4. **Estado incierto:** no se hace retry ciego de una cancelación. Si la fuente pudo hacer commit, se guarda Unknown y se consulta por operationId. Un rollback de SQL local no deshace un efecto remoto.
5. **RAG:** distancia vectorial no es probabilidad ni autorización. SQL valida permisos y vigencia antes de enviar chunks al modelo; la evidencia textual explica la regla, pero no modifica el Policy Engine.
6. **Genesys:** Open Messaging transporta un canal externo hacia Genesys; no es una interfaz genérica de bot para cualquier canal ya alojado allí. Data Actions ejecuta operaciones JSON acotadas. Digital Connector es el candidato para conversación bot, sujeto a capacidades del tenant. El SDK no elimina estas diferencias.
7. **Auditoría:** registrar después del efecto abre un hueco si el proceso cae. Se persiste intención, confirmación consumida y outbox antes del envío; luego se registra el resultado observado. Se auditan decisiones y códigos, no razonamiento privado del LLM.
8. **Complejidad:** una sola política sin penalidad, un solo club ficticio, chat textual y un tenant lógico. Sin motor de reglas genérico, Redis, Kafka, Kubernetes, multi-agent ni integración de pagos.

## Riesgos dominantes y costo de complejidad

Los riesgos principales son acciones sobre otra cuenta, falsas confirmaciones de éxito, dobles efectos por retry, fuga de documentos internos y bot/humano activos a la vez. Todos necesitan controles externos al modelo. Los riesgos de implementación más altos son integración Genesys real, calibración bilingüe de RAG y concurrencia ante eventos fuera de orden. Se reducen con contratos tempranos, fixtures y fault injection antes de incorporar una UI extensa.

El worker y dos almacenes añaden operación; se justifican por recuperación durable y búsqueda vectorial. SQL es la fuente de verdad y Qdrant se puede reconstruir. Separar cada módulo en un servicio añadiría coordinación distribuida sin aportar evidencia proporcional al alcance.

## Decisiones respaldadas por fuentes actuales

Se selecciona .NET 10 LTS por su horizonte de soporte para un proyecto nuevo; se fijarán patch/SDK y dependencias al comenzar la implementación. [Política oficial de .NET](https://dotnet.microsoft.com/en-us/platform/support/policy).

Genesys distingue Data Actions para servicios JSON y Digital Connector para bots asincrónicos. La disponibilidad y configuración concreta siguen requiriendo un tenant. [Data Actions](https://help.genesys.cloud/articles/about-web-services-data-actions-integration/), [Digital Connector](https://help.genesys.cloud/articles/genesys-digital-bot-connector-overview/).

La protección contra prompt injection usa controles en capas y validación del ejecutor; no se interpreta un filtro de palabras como garantía. [OWASP](https://cheatsheetseries.owasp.org/cheatsheets/LLM_Prompt_Injection_Prevention_Cheat_Sheet.html).

## Condiciones del dictamen

**Aprobado para el MVP local del portafolio** con las decisiones de este paquete. **No aprobado para producción o datos reales.** Esas fases requieren políticas de negocio reales, revisión de seguridad/privacidad, contratos del proveedor, sandbox Genesys, pruebas de recuperación y resultados medidos. El freeze acepta supuestos explícitos; no inventa validación de terceros.

