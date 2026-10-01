# ContactCenterAI — entrega local v0.8

Estado: **DONE para la entrega funcional de portafolio descrita aquí**. Calidad empresarial y gates externos **OPEN**. Fecha 2026-10-01. Arquitectura antes del código: [freeze v0.7](../architecture-freeze-v0.7-retrieval-operation.md), [freeze v0.8](../architecture-freeze-v0.8-evidence-selection.md), [ADR-020](../adr/020-local-semantic-retrieval-and-operational-evidence.md) y [ADR-021](../adr/021-bounded-evidence-selection.md). Preservar v0.1/v0.4/v0.5/v0.6 y sus informes; no rehacer B01 ni resetear la demo.

## Resultado práctico

La demo tiene autenticación Keycloak real, historial persistente, reservas propias en una fuente HTTP C# separada, propuesta/confirmación visible, ledger/receipts/leases/recovery, handoff y contexto para agente asignado. La intención se interpreta con Qwen local. La pregunta original se recupera con embeddings BGE-M3 reales, índice coseno exacto C# y selección acotada entre candidatos autorizados. El texto final es un extracto publicado con cita, no una respuesta factual libre. El modelo no puede cancelar ni asignarse identidad/permisos. Contact center y hotel siguen siendo ficticios.

El corpus creció a 40 documentos lógicos/80 variantes ES/EN, con IDs/sección/versiones/hash/idioma/clasificación estables. Nuevas versiones siguen requiriendo editor y reviewer distintos. Ingesta por lotes fuera de transacción y activación atómica de generación; queries/lectura de citas revalidan publicación, vigencia, hash, tenant y ACL. SQLite es un índice local persistente con vectores reales; **no se presenta como Qdrant ni ANN**. Los documentos son cortos, una sección overview por documento, sin fragmentación artificial.

Modelo de embeddings: bge-m3:latest, digest 7907646426070047a77226ac3e684fbbe8410524f7b4a74d02837e43f2146bab, 1024 dimensiones. Descarga explícita autorizada desde registry Ollama (~1.2 GB), blobs/manifiesto verificados, solo .local/embedding. Proceso loopback separado 11435; Qwen permanece 11434. CPU, sin cloud, sin redirects/proxy, sin descarga automática. Antes de abrir se preparan ambos usos de Qwen; startup no se confunde con el deadline del producto.

## Evidencia y límites

| Comprobación | Resultado | Qué demuestra |
|---|---|---|
| Release .NET, 15 proyectos | 0 warnings / 0 errors | Compilación local; no publicación |
| [Safety suites](demo-v0.8-checks.json) | 226 comprobaciones PASS | Foundation 21, almacenamiento 17, ingress 33, fuente 22, workflow 25, assistant 36, IA wire 28, retrieval 35, operación 9. Corrida completa de 224 y verificación focalizada de 35 tras el ajuste final de tamaño; no se repitió toda la suite |
| [Intención real v0.6](../../evaluation/reports/demo-v0.6/report.md) | 40/40 intents, 38/40 propuestas | Dataset público; errores conservados |
| [Búsqueda v0.7](../../evaluation/reports/demo-v0.7/report.md) | 86% exacto / 96.875% recall@5 positivos | 200 casos, umbral previo; gate OPEN |
| [Selección v0.8](../../evaluation/reports/demo-v0.8/report.md) | 89% exacto / 100% abstención fuera de alcance | Regresión real de los mismos 200 casos conocidos; sin holdout independiente |
| Respuestas seleccionadas v0.8 | 178/178 IDs gold correctos | Precisión de selección en esta muestra, no juicio humano general; 22 abstenciones incluidas en errores |
| Latencia v0.8 | p50 2657.5 ms / p95 4376 ms | Recuperación+reranking secuencial CPU; excluye clasificación de intención y carga concurrente |
| [Producto en navegador](demo-v0.8-browser.json) | PASS; turno 6957.49 ms | Login real, pregunta nueva, cita y selección observada dentro de 8 s |
| [Backup/restore](demo-v0.7-restore.json) | PASS | Dos snapshots online; restore en otra carpeta, integridad/FK/conteos y receipts. No reemplazó DB activas ni prueba HA/distributed atomic backup |
| [Dependencias](demo-v0.7-dependencies.json) / [SBOM](demo-v0.7-sbom.cdx.json) | 40 paquetes: hashes canónicos recalculados, 0 advisories coincidentes en feed oficial consultado | 31 runtime; incluye tests/spikes. No certifica OS/Docker/Ollama/SDK ni cadena/revocación de firmas |
| [Workflow SHA-pinned](demo-v0.7-ci-pins.json) | Declarado y verificado contra refs oficiales | No existe corrida GitHub remota ni despliegue |

Los dos primeros casos de la regresión tuvieron timeout inicial; quedan en el informe. Preparación añadida al lanzador evita esperar a cargar el modelo durante la primera consulta; no se repitió la corrida para borrar esos fallos. Todos los errores restantes son abstenciones, incluidas preguntas relevantes. El gate numérico de calidad sigue OPEN: 89% general y 92.14% en el split histórico no alcanzan 95%. El nuevo prompt se diseñó después de inspeccionar los errores v0.7; llamar holdout al split repetido no lo vuelve ciego. Mantener un futuro set independiente y dos revisores humanos como pendientes.

El panel OperationsAdmin devuelve agregados por tenant y actividad W3C acotada; cliente/rol falsificado/sesión revocada se rechazan. Exporter con nombres y etiquetas cerrados, máximo 60 eventos en memoria/20 en panel y rotación de archivo ~1 MiB; no exporta texto/roles/session IDs/claves/thinking. Audit transaccional sigue separado. No OTLP collector distribuido, retención legal o alertas enviadas a servicios externos.

## Presentación y pendientes

[Guía de cinco minutos](../../DEMO.md), [contrato v0.8](../contracts/demo-v0.8.openapi.json), [entrevista](../09-portfolio-and-interview.md), [consulta](../diagrams/local-semantic-retrieval.mmd), [ingesta](../diagrams/local-knowledge-ingestion.mmd) y [selección](../diagrams/evidence-selection.mmd). Customer A conserva RES-001 y RES-002; RES-003 de B ya estaba cancelada por la prueba v0.5. No se inició otra cancelación ni se borraron conversaciones. Clave pública de las cuentas ficticias: 123456Aa!; claves privadas no se modificaron.

Gates empresariales: .NET/SQL Server y HTTPS de producto en el host autorizado, revocación global del IdP, retención/worker/restart empresarial, Qdrant/ANN, frontend Angular, ejecución CI cloud/deploy, carga/coste energético y calidad independiente. B16/B17 real Genesys requiere tenant y permisos; mocks son la entrega autorizada actual. No pedir al usuario repetir B01 para presentar. La retirada del antiguo certificado diagnóstico del perfil original sigue como nota histórica no confirmada; este trabajo no instaló ni retiró certificados.

Paquete de portafolio: fuente/documentos/contratos/diagramas/pruebas y evidencia sanitizada. Excluir .local, SDK, modelos, DB, backups, claves/config privada y logs. En otra máquina empezar con modo simulador y preparar dependencias explícitamente; esto no es un instalador empresarial.

## Estado del equipo al entregar

La compilación final del código vigente pasó con cero warnings/errors. El paquete fue revisado mediante CRC y búsqueda de valores privados. La sesión anterior del navegador es evidencia histórica, no una afirmación de disponibilidad actual. En la reanudación, Keycloak estaba apagado; Docker Desktop falló al arrancar por su socket temporal de OTel. La solicitud de escritura en la carpeta Docker/run, fuera del workspace, no concedió permisos. No se restableció Docker ni se borraron contenedores/bases. El código y las evidencias están entregados; reabrir la demo en este host queda bloqueado por ese arranque. Ver [estado del host](demo-v0.8-host.json).
