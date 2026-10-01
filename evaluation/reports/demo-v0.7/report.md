# Evaluación de recuperación local v0.7

200 casos, 100 pares ES/EN. Corpus sintético de 40 documentos lógicos y 80 variantes. Inferencia real BGE-M3 y búsqueda coseno exacta C#. Umbral/margen fijados antes de ejecutar; sin ajuste posterior al holdout. Los autores conocen los casos: split de calibración separado, no evaluación ciega.

| Split | N | Acierto exacto | Recall@5 positivos | Abstención fuera de alcance | p95 ms |
|---|---:|---:|---:|---:|---:|
| overall | 200 | 86.0% | 96.9% | 95.0% | 49 |
| dev | 60 | 80.0% | 98.0% | 100.0% | 50 |
| holdout | 140 | 88.6% | 96.4% | 93.3% | 49 |
| es | 100 | 84.0% | 97.5% | 95.0% | 50 |
| en | 100 | 88.0% | 96.2% | 95.0% | 48 |

Gate local de calidad: **OPEN** (holdout ≥95% exacto y 100% abstención fuera de alcance).

Se mide recuperación aislada: no latencia total de chat, concurrencia, disponibilidad HA, transferencia Genesys ni calidad humana. No convertir abstención en acierto para preguntas que sí tienen documento relevante. El informe conserva todos los fallos; no se cambian preguntas o resultados para aprobar. Coste local sin factura cloud, pero no se midió consumo eléctrico.

## Fallos

- rag-003-es: esperado `KB-SERVICES-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-003-en: esperado `KB-SERVICES-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-004-es: esperado `KB-SERVICES-ES`, observado `KB-VISITORS-ES`; GROUNDED_EXTRACT.
- rag-004-en: esperado `KB-SERVICES-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-005-es: esperado `KB-IDENTITY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-013-es: esperado `KB-PARKING-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-019-es: esperado `KB-BREAKFAST-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-021-es: esperado `KB-WIFI-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-021-en: esperado `KB-WIFI-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-023-es: esperado `KB-AIRPORT-ES`, observado `KB-LUGGAGE-ES`; GROUNDED_EXTRACT.
- rag-023-en: esperado `KB-AIRPORT-EN`, observado `KB-LUGGAGE-EN`; GROUNDED_EXTRACT.
- rag-027-es: esperado `KB-LATE-CHECKOUT-ES`, observado `KB-EARLY-CHECKIN-ES`; GROUNDED_EXTRACT.
- rag-027-en: esperado `KB-LATE-CHECKOUT-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-030-es: esperado `KB-EARLY-CHECKIN-ES`, observado `KB-VISITORS-ES`; GROUNDED_EXTRACT.
- rag-045-en: esperado `KB-HOUSEKEEPING-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-050-es: esperado `KB-RESTAURANT-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-055-en: esperado `KB-RECEPTION-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-056-es: esperado `KB-RECEPTION-ES`, observado `KB-VISITORS-ES`; GROUNDED_EXTRACT.
- rag-056-en: esperado `KB-RECEPTION-EN`, observado `KB-VISITORS-EN`; GROUNDED_EXTRACT.
- rag-073-es: esperado `KB-PRIVACY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-073-en: esperado `KB-PRIVACY-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-074-es: esperado `KB-PRIVACY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-078-es: esperado `KB-INTERNAL-HANDOFF-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-078-en: esperado `KB-INTERNAL-HANDOFF-EN`, observado `KB-INVOICE-EN`; GROUNDED_EXTRACT.
- rag-080-es: esperado `KB-INTERNAL-SUMMARY-ES`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-080-en: esperado `KB-INTERNAL-SUMMARY-EN`, observado `None`; INSUFFICIENT_EVIDENCE.
- rag-092-es: esperado `None`, observado `KB-ACCESSIBILITY-ES`; GROUNDED_EXTRACT.
- rag-092-en: esperado `None`, observado `KB-ACCESSIBILITY-EN`; GROUNDED_EXTRACT.
