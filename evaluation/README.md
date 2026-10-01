# Evaluación reproducible

Las invariantes de negocio y la calidad del modelo se reportan por separado. Ningún resultado del modelo supera un fallo de autorización, confirmación o receipt.

| Evidencia | Qué ejecutó | Resultado y límite |
|---|---|---|
| [v0.5](reports/demo-v0.5/report.md) | Clasificador simulado, 40 casos | Baseline público sin inferencia |
| [v0.6](reports/demo-v0.6/report.md) | Qwen real, 40 casos de intención | 40 intents / 38 propuestas; no holdout |
| [v0.7](reports/demo-v0.7/report.md) | BGE-M3 real, 200 preguntas | 86% exacto; fallos conservados |
| [v0.8](reports/demo-v0.8/report.md) | BGE-M3 + selección Qwen, mismos 200 | 89%; regresión conocida |
| [v0.9](reports/demo-v0.9/report.md) | 200 + 100 preguntas, IA real | 95.5%/95%; fallos de alcance, candidato no activado |
| [v0.10 inicial](reports/demo-v0.10/report.md) | 300 consultas reales con selector v2 | 96.5%/98%; un reembolso ES fuera de alcance no reconocido |
| [v0.10 cierre compuesto](reports/demo-v0.10/scope-replay/report.md) | Guard C# actual + outputs medidos en caminos sin cambio | 97%/98%; 60/60 OOD abstenciones; ocho errores conservados |

El último cierre reutiliza 287 outputs sin cambio y ejecuta 13 checks actuales del filtro a través del repositorio autorizado, con cero inferencia. Corrige una selección mediante el reconocimiento de `devolver mi dinero`. No se cambió corpus, gold, modelo ni prompt entre ambas evidencias v0.10. No es una corrida nueva de 300 inferencias ni un benchmark de latencia. Los tiempos reales iniciales están en el informe superior; la corrección compuesta no publica tiempos agregados nuevos.

`rag-200-v1.jsonl` conserva 100 pares ES/EN y el split histórico 60 dev/140 holdout. Tras inspeccionar resultados esos nombres no significan casos desconocidos. `rag-additional-v1.jsonl` se congeló antes de v0.9 y proviene del mismo autor; la corrección de hotel se informó por su primera corrida. En v0.10 ambos conjuntos son regresión conocida. Revisión humana independiente y carga siguen abiertas.

Python puntúa outputs guardados, verifica hashes y publica errores; no usa red ni un judge. Para repetir inferencia actual con modelos ya preparados: `eng/Measure-FinalRetrieval.ps1`. Para reconstruir el cierre compuesto: ejecutar RetrievalChecks con `--ScopeReplay` y luego `scope_replay_eval.py`; exige las corridas reales iniciales intactas. `final_rag_eval.py --version 0.10` reconstruye el informe original. Los lanzadores no sobrescriben las corridas históricas v0.7/v0.8/v0.9 con código nuevo.

CI ejecuta [249 comprobaciones deterministas](../docs/progress/demo-v0.10-checks.json) y el evaluador de intención simulado; no necesita modelos ni descarga pesos. La corrida aislada RAG excluye clasificación de intención, login, conversaciones concurrentes y carga. El producto conserva su límite global de ocho segundos.

No editar gold ni ocultar fallos. No exportar thinking, cookies, claves o auth codes. Los pesos y DBs no están en el bundle.
