# Evaluación de recuperación local v0.8

200 casos, 100 pares ES/EN. Corpus sintético de 40 documentos lógicos y 80 variantes. Inferencia real BGE-M3 y búsqueda coseno exacta C#. Reranker Qwen añadido después de inspeccionar los errores v0.7. Esta corrida es regresión sobre casos conocidos; los nombres dev/holdout conservan el split histórico y no demuestran generalización. No se cambiaron las preguntas.

| Split | N | Acierto exacto | Recall@5 positivos | Abstención fuera de alcance | p95 ms |
|---|---:|---:|---:|---:|---:|
| overall | 200 | 89.0% | 95.6% | 100.0% | 4376 |
| dev | 60 | 81.7% | 94.0% | 100.0% | 4675 |
| holdout | 140 | 92.1% | 96.4% | 100.0% | 4376 |
| es | 100 | 86.0% | 96.2% | 100.0% | 4566 |
| en | 100 | 92.0% | 95.0% | 100.0% | 2886 |

Gate numérico local: **OPEN** (split histórico holdout ≥95% exacto y 100% abstención fuera de alcance). No cierra el gate de calidad empresarial/independiente.

Se mide recuperación aislada: no latencia total de chat, concurrencia, disponibilidad HA, transferencia Genesys ni calidad humana. No convertir abstención en acierto para preguntas que sí tienen documento relevante. El informe conserva todos los fallos; no se cambian preguntas o resultados para aprobar. Coste local sin factura cloud, pero no se midió consumo eléctrico.

## Fallos

- rag-001-es: esperado `KB-CANCELLATION-ES`, observado `None`; INFERENCE_FAILED.
- rag-001-en: esperado `KB-CANCELLATION-EN`, observado `None`; INFERENCE_FAILED.
- rag-003-es: esperado `KB-SERVICES-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-003-en: esperado `KB-SERVICES-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-004-es: esperado `KB-SERVICES-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-004-en: esperado `KB-SERVICES-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-005-es: esperado `KB-IDENTITY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-013-es: esperado `KB-PARKING-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-021-es: esperado `KB-WIFI-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-021-en: esperado `KB-WIFI-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-023-es: esperado `KB-AIRPORT-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-023-en: esperado `KB-AIRPORT-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-037-es: esperado `KB-QUIET-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-042-es: esperado `KB-FOOD-ALLERGY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-063-es: esperado `KB-KEYS-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-es: esperado `KB-PRIVACY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-en: esperado `KB-PRIVACY-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-074-es: esperado `KB-PRIVACY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-078-es: esperado `KB-INTERNAL-HANDOFF-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-078-en: esperado `KB-INTERNAL-HANDOFF-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-080-es: esperado `KB-INTERNAL-SUMMARY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-080-en: esperado `KB-INTERNAL-SUMMARY-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
