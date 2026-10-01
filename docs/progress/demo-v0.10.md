# Entrega de portafolio v0.10

La presentación local está implementada y sus gates locales están cerrados. [Guía de uso](../../DEMO.md). La disponibilidad final de este equipo se registra por separado en [estado del host](demo-v0.10-host.json) y [recorrido de navegador](demo-v0.10-browser.json); no deducir disponibilidad de una captura histórica.

## Qué se completó

- Documentación architecture-first: charter, alcance, FR/NFR, use cases/criterios, trazabilidad, C4/sequence/state/data model, amenazas, evaluación, ADRs y DoR. Freeze v0.1 y los incrementos v0.9/v0.10 cerrados antes de sus cambios.
- Backend C#/.NET, login real Keycloak Code+PKCE, sesiones persistentes, RBAC y ownership, fuente HTTP separada, dos SQLite y UI sencilla. Cuentas ficticias con clave común 123456Aa!.
- Propuesta/confirmación explícita, ledger durable, receipts idempotentes, leases y reconciliación tras resultado incierto. Sin autoridad de escritura para el modelo. Handoff y asignación en mock desacoplado de Genesys.
- Qwen/BGE-M3 reales fijados, índice denso exacto de 80 variantes publicadas, revisión distinta, ACL/expiry/revoke/citas, selector cerrado y alcance determinista antes de inferencia.
- Panel operativo, trazas sanitizadas y ensayo de backup/restore previamente observado. Launcher conservador sin borrar datos, cambiar claves ni descargar modelos.
- [Build final y nueve suites](demo-v0.10-checks.json): **249 PASS**, cero warnings/errores. Pruebas aisladas con clocks/fault injection; no equivalen a SQL/OIDC/Genesys live.
- [300 outputs reales iniciales](../../evaluation/reports/demo-v0.10/report.md), 96.5%/98%; [cierre compuesto](../../evaluation/reports/demo-v0.10/scope-replay/report.md), 97%/98%, abstención 60/60 OOD. Solo el guard de conjugaciones cambió; 13 checks actuales y 287 outputs reutilizados, no nueva corrida completa. Ocho errores publicados, incluido un documento equivocado sobre late checkout.
- CI reproducible declarada con acciones fijadas, locks/SBOM y revisión de paquetes previa preservada. Su comando pasó aquí; no hay ejecución GitHub.

## Límites abiertos, con motivo concreto

| Gate | Qué falta y por qué |
|---|---|
| SQL empresarial del producto | [Conexión cifrada bloqueada](demo-v0.10-sql-runtime.json) en este entorno: native indica falta de soporte de cifrado; diagnóstico managed llega a SChannel sin credenciales de security package. Docker iniciado no lo resolvió. Reproductor portable compilado; no se deshabilitó cifrado ni se migró la demo parcialmente. Requiere comprobar runtime Windows compatible, repo SQL completo y aceptación live antes de cambiar la composición. |
| Genesys real | Tenant/region/workflow y acceso autorizados aún no proporcionados. Adapter neutral y mocks terminados; ningún resultado tenant-validated. |
| CI externa | No hay remote GitHub ni repositorio destino indicado. Workflow terminado y comprobado localmente; falta subirlo al destino autorizado y observar su run. |
| Calidad independiente | Dos revisores bilingües y casos no conocidos. El cierre local numérico no sustituye esa aceptación. |
| Operación de producción | HTTPS desplegado, revocación global, retención efectiva, carga/HA/restore distribuido y políticas reales. Diseño y gates documentados; no son requisitos para abrir el portafolio sintético local. |

Los tickets empresariales B03/B04 y M6 permanecen abiertos en su alcance original. B01/B02 cerrados históricamente; no repetir logins diagnósticos ni certificados como requisito de presentación. SQL/Qdrant/Angular del diseño no se presentan como implementaciones actuales. Bases, pesos, secretos y logs excluidos del ZIP.

## Decisiones y evidencia

[ADR-022](../adr/022-retrieval-recall-and-final-delivery.md), [ADR-023](../adr/023-query-scope-before-semantic-inference.md), [freeze v0.9](../architecture-freeze-v0.9-final-delivery.md), [freeze v0.10](../architecture-freeze-v0.10-scope-closure.md), [secuencia de alcance](../diagrams/query-scope-retrieval.mmd), [contrato actual](../contracts/demo-v0.10.openapi.json). Resultados iniciales y entregas anteriores conservados.


## Recorrido final observado

Login real, historial conservado, nueva pregunta de piscina con su cita abierta, abstención ante «devolver mi dinero» y reservas de A sin mutación. Se renovó por el flujo normal una sesión que venció; no se cambiaron claves ni límites de sesión. El arranque frío mostró una espera insuficiente: se amplió la preparación a 45 segundos y el timeout de discovery a ocho, sin ampliar el deadline del chat. La API actual está activa y el lanzador reconoce el estado listo; no se repitió un arranque frío completo después del ajuste.

![Demo actual: reservas, cita y abstención](screenshots/final-demo-v0.10.png)
