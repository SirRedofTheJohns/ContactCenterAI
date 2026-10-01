# Entrega funcional de portafolio — v0.5

Estado: **DONE en el perfil local de presentación**. Diseño anterior al código: [freeze v0.4](../architecture-freeze-v0.4-transactions.md), [freeze v0.5](../architecture-freeze-v0.5-assistant.md), [ADR-016](../adr/016-local-durable-transactions.md) y [ADR-017](../adr/017-reproducible-assistant-and-contact-center-mock.md). Arquitectura empresarial v0.1 conserva sus gates propios.

## Resultado

Login real OIDC/Keycloak; conversaciones, jobs, ofertas y ledger en SQLite C# persistente. Fuente de reservas en otro proceso/DB. Chat ES/EN con proveedor de intención simulado, JSON cerrado, recuperación léxica por temas y respuestas extractivas citadas. Tools permitidas: FAQ, reservas propias, preview y handoff; cero tool de cancelación libre. UI confirma/rechaza, espera la fuente y muestra Pending/Completed/Unknown sin inferencia. Dispatcher usa leases, stable commandId, source dedupe y reconciliación.

Handoff neutral persistente → acknowledgment del mock → HumanOwned con epoch nuevo y asignación. La cuenta agent-assigned recupera contexto y conocimiento interno; agent-unassigned no recibe ese recurso. Bot queda en pausa; operaciones ya enviadas continúan reconciliando. Un Unknown de más de 60 s abre HumanReview local/audit, sin prometer un operador conectado.

Gobierno del corpus: diez secciones/fixtures ES/EN, clasificación, tenant, versión, hash y vigencia. Draft no publicado, reviewer distinto, activación atómica, revocación, expiry y cita directa autorizada. Política documental no cambia CP-001. Feedback crea PendingReview. Corpus inicial tiene metadata sintética revisada, no una auditoría humana real. Detección de marcadores sospechosos es una defensa adicional limitada.

## Evidencia ejecutada

| Suite | PASS | Ámbito real |
|---|---:|---|
| Foundation | 21 | Dependencias compiladas, host y health empresarial |
| Operational | 17 | C# SQLite, concurrencia, reabrir, sesión/ownership/idempotencia |
| Ingress | 33 | Pipeline HTTPS TestServer; tickets/store fixture aislados |
| Source | 22 | C# SQLite fuente, reglas, replay/concurrencia y Kestrel HTTP |
| B06/B07 | 25 | Ledger, confirmación, dos dispatchers, clocks, crash y respuesta perdida |
| Asistente/contact center | 36 | C# persistencia, ACL/citas/gobierno, adversarial, handoff y deadline/budget |
| API demo en ejecución | 18 | HTTP propio, recursos sin sesión, Host/Origin/CSRF y challenge IdP real |
| Evaluación simulada | 40 casos / 20 pares | Outputs C# reales del proveedor simulado, Python offline y manifest |

**154 checks C# + 18 smoke HTTP + 40 casos de intención**, con ámbitos distintos. [Transacciones](b06-b07-evidence.json), [asistente](b08-b12-evidence.json), [evaluación](../../evaluation/reports/demo-v0.5/report.md). No sumarlos como un benchmark lingüístico de LLM. Un dataset público de 20 casos por idioma arroja 20/20 en ambos y Wilson 95% [0.8389,1.0]; no prueba generalización.

La primera carrera de estrés tras ampliar el repositorio devolvió OperationalUnavailable. Se añadió espera asíncrona local para evitar ocupar request threads con SQLite busy waits; la autoridad entre procesos sigue siendo transacción/constraints. La suite final completa pasó. No se presenta como benchmark de cargas empresariales.

## Recorrido de navegador observado

Cliente B, conversación English: FAQ con cita KB-CANCELLATION-EN v1/overview, abrir fuente, chat Cancel RES-003 → oferta visible sin efecto, clic Confirmar → Pending → Completed con referencia source. Recargar conservó el resultado. Solicitar humano mostró HandoffPending y luego humano asignado/bot en pausa. Login agent-assigned mostró el caso, ledger Completed y guía interna permitida. Login Cliente A y nueva conversación Español respondió FAQ con cita ES; esta vista queda abierta. No se exportaron cookies, tokens, auth codes ni URLs de callback.

Capturas: [FAQ EN](screenshots/assistant-faq-en.jpg), [cancelación Completed](screenshots/cancellation-completed.jpg), [handoff](screenshots/handoff-owned.jpg), [contexto agente](screenshots/agent-context.jpg), [FAQ ES](screenshots/assistant-faq-es.jpg). [Registro del recorrido](demo-v0.5-browser.json).

## Límites y siguiente validación empresarial

- Proveedor de IA simulado: no inferencia LLM real, generación abierta, tokens/precios ni calidad holdout medida. Puerto permite sustituirlo después de evaluar un modelo real.
- Recuperación por temas/corpus pequeño: no Qdrant, embeddings ni Recall@5 dense medido.
- Contact center mock: contrato neutral y estados probados, ninguna llamada/compatibilidad tenant Genesys Cloud confirmada.
- Perfil SQLite/HTTP loopback: SQL Server empresarial, TLS real del producto y revocación global IdP continúan pendientes. La nota histórica de retiro del certificado B01 permanece en su reporte.
- UI HTML/CSS/JS: Angular sigue como ampliación, no se declara experiencia Angular probada por este código.
- ActivitySource/Meter y audit implementados; exporter OTel, dashboard, CI remoto, deployment/rollback operativo y holdout empresarial 200 casos no observados.

CI declarada en .github/workflows/demo-ci.yml, sin secretos y con contents:read; Verify-Demo.ps1 ejecutado localmente. No se publicó repositorio ni se ejecutó GitHub Actions. No hay afirmación de producción/compliance/empleabilidad garantizada. El ZIP excluye SDK, .env, databases, logs y claves. [Guion sencillo](../../DEMO.md).

[Inventario de dependencias](demo-v0.5-dependency-scope.json): las 31 versiones del runtime están cubiertas por el audit previo. Ocho versiones transitivas de tests/diagnósticos tienen alcance separado; no se afirma audit oficial ni advisory actualizado para ellas. El lanzador Preparar-Demo se inspeccionó con parser PowerShell sin errores; no se ejecutó sobre las bases de esta evidencia.
