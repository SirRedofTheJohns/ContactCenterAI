# ADR-008 — IA acotada y port de proveedor

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** Un planner abierto puede inventar herramientas, extender costos y hacer difícil la reanudación. Proveedores/versiones cambian.

**Decisión.** Un workflow con máximo 2 model calls y 3 tools por turno. Model/schema validation + server semantics. Un provider en runtime con adapter; modelo y embeddings fijados después de eval comparativa offline. Stub determinista para control tests. State SQL, contexto mínimo, sin chain-of-thought almacenado ni secretos del modelo.

**Alternativas.** Multi-agent/planning frameworks como default: más variabilidad sin necesidad. Failover multi-provider en runtime: distinta calidad/contexto antes de medir. Alias modelo móvil sin regresión: impide atribución. Python servicio LLM paralelo: dispersa ownership/runtime.

**Consecuencias.** Menor autonomía y fallback al límite; intercambio de proveedor es posible pero exige tests/evals/retention review. Revisar por tareas nuevas que realmente requieran planning o resultados medidos insuficientes. Tool schema estricto es ayuda de formato, no permiso.

Referencias: FR14/22; NFR06–07/15; AC25–26; evaluación.

