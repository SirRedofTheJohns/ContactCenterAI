# ADR-004 — RAG autorizado y versiones publicadas

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** Embeddings pueden encontrar políticas viejas o internas. Score alto no indica permiso ni soporte factual.

**Decisión.** SQL registra texto/ACL/estado/versión; Qdrant es proyección de búsqueda. Prefilter de IDs y recheck SQL antes de cargar texto; revisión por otro principal y publicación atómica. Dense multilingüe baseline, citas resueltas por backend, abstención y feedback humano. CP-001 ejecutable es independiente del texto.

**Alternativas.** Solo metadata Qdrant: revocación desincronizada. PDFs sin approval/expiry: no cumple gobernanza. Hybrid/reranker inmediato: añade complejidad antes de medir baseline. RAG para decidir reglas: introduce nondeterminismo en transacciones.

**Consecuencias.** Dos stores y pipeline de versiones; reconstrucción del índice es posible. Checks de citas no garantizan entailment; medir con anotación. Revisar si recall/latencia del baseline falla o se agregan formatos/source repositories.

Referencias: FR10–13/20; AC01–03/21–24; diseño RAG.

