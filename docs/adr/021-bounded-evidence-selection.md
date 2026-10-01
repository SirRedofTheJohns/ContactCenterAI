# ADR-021 — Selección acotada de evidencia

Estado: Accepted. 2026-10-01. Motivación medida: recuperación v0.7 obtiene 96.875% recall@5 pero 86% acierto final; hay confusiones entre llegada/salida y documentos de recepción/visitas. Conservar [informe original](../../evaluation/reports/demo-v0.7/report.md).

Decisión para v0.8: reranker local con el mismo Qwen fijado. Recibe exclusivamente pregunta sanitizada y hasta cinco candidatos ya autorizados (título y extracto de máximo 600 caracteres). Salida JSON cerrada `choice` = 0 (abstenerse) o posición existente. Nunca recibe identidad, roles, herramientas ni comandos. El índice coseno y umbral mínimo 0.55 permanecen; reemplazar margen numérico por selección de evidencia. Elegir un candidato por debajo del umbral produce abstención. El documento elegido se revalida por SQL antes de responder. El modelo no redacta hechos y no modifica CP-001.

Límite: una petición adicional por FAQ, contexto 2048/output 32 tokens, CPU, 5 segundos como máximo, dentro del deadline total existente de 8 segundos del producto. Error de schema, deadline, pin o envelope produce abstención/fallback; no sustituir por el primer candidato cuando falla. Ninguna nueva dependencia o descarga. Prompt/schema/versiones reproducibles; no exportar thinking.

Evaluación posterior se llama **regresión de 200 casos conocidos**. Ya se inspeccionó el holdout v0.7; reutilizarlo no demuestra generalización ciega. No cambiar preguntas ni ocultar resultados previos. La latencia reportada de recuperación+reranking no incluye clasificación del intent; navegador verifica un recorrido completo y los tests mantienen deadline global. Sin claim de load test ni evaluación independiente.

Tradeoff: mayor latencia/CPU y otra decisión estadística. Una selección errónea todavía puede devolver un texto irrelevante aunque auténtico; por eso calidad y autorización son gates distintos. Mantener abstención, cita visible y feedback/revisión. Diseño Qdrant empresarial y separación C#/Python sin cambios.
