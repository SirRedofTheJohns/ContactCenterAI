# RAG gobernado y knowledge assistant

**Entrega actual:** [demo local v0.6](../progress/demo-v0.6.md): SQLite, intención con IA real local o simulador explícito, contact center mock, evidence por temas, confirmación y recovery. Las secciones empresariales siguientes mantienen el objetivo y sus gates.


## Objetivo y fuente de autoridad

RAG responde preguntas desde conocimiento aprobado; no decide permisos ni ejecuta reglas. SQL contiene registro, texto, chunkId y versión activa. Qdrant contiene vectores/referencias y payload para filtrar. CP-001 v1 es configuración determinista compartida por preview/fuente; los documentos ES/EN explican esa versión, sin poder alterarla.

## Corpus inicial previsto

40 documentos lógicos con traducciones ES/EN: política de cancelación, preguntas de reserva, información de propiedades, guía de login, procedimiento de transferencia y procedimientos internos de agente. Solo temas dentro del MVP; información de pagos indica canal autorizado, nunca ofrece ejecución de pago. Los pares de traducción mantienen translationGroup y policyVersion; publicación de política exige revisión de equivalencia.

Metadata mínima: documentId, version, language, translationGroup, title, sourceType, classification, allowedRoles/scopes, tenantId, editorSubject, reviewerSubject, effectiveAtUtc, expiresAtUtc, contentHash, chunkingVersion, embeddingModel/version, indexGeneration y estado. Un documento público no se marca Agent-only y luego se intenta responder al cliente con él.

## Ingesta y publicación

Entrada limitada a Markdown curado <=256 KB desde editor autorizado. Sin crawling ni PDFs/uploads de clientes. Validar encoding, links permitidos, estructura y contenido sospechoso; revisión humana distinta del uploader. No afirmar que detección elimina prompt injection.

Draft → PendingReview → ApprovedForIndexing → Published. Worker divide por sección y encabezados, conservando sectionId, título y offsets. Punto de partida: 300–500 tokens/chunk y overlap hasta 50, sin cortar reglas/negaciones fuera de contexto; se calibra con dev set. Embeddings se generan solo para versiones revisadas. Construir generación completa staging, verificar count/hash y activar puntero SQL de forma atómica. Fallo deja versión anterior activa; reindex usa misma identidad/version y nueva generación.

Revocación actualiza SQL primero e invalida caches; retirar puntos del índice es asincrónico. La vigencia se comprueba en cada query, independientemente del job de expiración. Vectores son derivados sensibles y requieren los mismos límites de acceso/retención del corpus.

## Recuperación

1. Resolver tenant, public/internal scope, asignación y ACL desde principal servidor. No aceptar roles del texto/modelo.
2. Búsqueda dense multilingüe baseline; prefilter por tenant, published generation, language/scope y allowedRoles, devolviendo IDs/scores sin texto. Prefiltro reduce exposición, no es autoridad final.
3. SQL revalida versión, publicación, ACL, fecha y clasificación; carga texto solo de IDs permitidos. Puntos viejos, alterados o sin metadata se descartan.
4. Seleccionar hasta 5 chunks con diversidad de secciones y presupuesto; si menos de 5 permitidos, no completar con documentos prohibidos. Pregunta ambigua puede pedir aclaración.
5. Enviar evidencia delimitada como data, sin herramientas especiales o secretos derivados del documento. Propuestas de modelo pasan gateway común.
6. Citas solo de allowlist de evidencia usada. Resolver URLs mediante backend con la misma autorización. IDs inexistentes, secciones inventadas o contradicción detectada invalidan draft.

Qdrant soporta filtros sobre payload; el diseño conserva una comprobación SQL independiente por revocación y control de autoridad. [Documentación de payload](https://qdrant.tech/documentation/concepts/payload/).

## Grounding y abstención

Score vectorial no es probabilidad de corrección. Umbral de evidencia se calibra por modelo/corpus e idioma en dev set, se congela y se evalúa en holdout. Incluir casos sin respuesta, versiones viejas, reglas con negación y conflicto entre fuentes. Si evidencia insuficiente/contradictoria, abstenerse y ofrecer humano.

Verificación de citas combina checks deterministas de IDs/vigencia/ACL con revisión de soporte semántico durante evaluación. Un detector o LLM judge no garantiza entailment en producción. Para consecuencias transaccionales se usa plantilla con reasonCode y veredicto determinista, reduciendo riesgo de interpretación. Fuente fuente y política activa prevalecen sobre una explicación recuperada. Si documento CP contradice configuración, se bloquea respuesta de política y se abre revisión, sin cambiar reglas.

Dense-only es baseline. Hybrid lexical+dense y reranker son experimentos posteriores si Recall@5 o consultas por códigos fallan; comparar latencia/calidad antes de aceptar otra dependencia. No se añade un reranker por defecto. Invalidar cache por tenant/ACL/activeVersion/language y no cachear respuestas privadas entre usuarios.

## Feedback y corrección

Registrar answerId, citation IDs, bundle de versiones, rating y reasonCode; comentario sanitizado. Reviewer reproduce el fallo y decide cambiar corpus/prompt/chunking. Publicación y cambios pasan evals afectados y safety completa. No usar feedback para entrenamiento automático ni liberar documentos sin revisión. Las métricas distinguen falta de conocimiento, fallo retrieval, fallo grounding y respuesta correcta con mala UX.
