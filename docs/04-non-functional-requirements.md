# Requisitos no funcionales y presupuestos

Estas son metas de diseño y gates futuros. No hay métricas de runtime medidas en Sprint 0. Perfil de referencia: 4 vCPU, 16 GB RAM, 10 conversaciones concurrentes, 1 tenant sintético, corpus de 40 documentos lógicos/80 traducciones, hasta 1.000 chunks. Proveedor LLM externo se mide separadamente; pruebas de infraestructura usan mocks con latencia controlada.

| ID | Atributo y criterio verificable |
|---|---|
| NFR01 | Seguridad: 0 mutaciones sin identidad, recurso autorizado, oferta válida y confirmación consumible en corpus obligatorio; 0 lectura de otra cuenta o documento prohibido. |
| NFR02 | Privacidad: prompts y outputs contienen DTO mínimo; 0 canary PII/secreto en logs/traces/datasets. No PAN/CVV. TLS fuera de loopback; secretos externos al repo. |
| NFR03 | Durabilidad: antes de HTTP 202 de evento/comando existe registro SQL committed. Reinicio en cada failpoint preserva comando o devuelve error sin reconocer recepción. |
| NFR04 | Idempotencia: 100 entregas del mismo commandId/eventId y 2 workers concurrentes causan como máximo un efecto fuente; payload distinto con misma clave devuelve 409. |
| NFR05 | Latencia local: p95 de ingestión durable <= 500 ms; preview determinista <= 1 s con fuente mock <= 100 ms; overhead sin LLM <= 1,5 s en perfil declarado. |
| NFR06 | Latencia IA objetivo: p95 de turno RAG <= 8 s y de preview conversacional <= 10 s; deadline de turno 12 s. Confirmación HTTP <= 500 ms devuelve 202; ejecución/reconciliación asincrónica. |
| NFR07 | Límites por turno: máximo 2 llamadas al modelo, 3 tools secuenciales, 8k tokens input y 1k output por llamada; sin bucles autónomos ilimitados. Al superar presupuesto, respuesta segura/handoff. |
| NFR08 | Resiliencia: lecturas con máximo 2 retries y jitter dentro del deadline; mutaciones sin retry por defecto. 429 respeta Retry-After. Circuit breaker abre a 5 fallos en ventana de 30 s, mínimo 5 muestras; prueba esos bordes. |
| NFR09 | Recuperación local: tras caída de worker, retomar leases en <= 30 s; detectar Unknown > 60 s y abrir caso humano <= 120 s. Ante fuente caída no se promete resolución en 120 s. |
| NFR10 | Observabilidad: 100% de comandos/mutaciones con audit record y correlación; traces del flujo feliz y fallos en demo. Métricas sin memberId/conversationId como etiquetas. |
| NFR11 | Calidad IA: gates por idioma, seguridad y negocio según plan; ninguna media agregada puede esconder fallos ES/EN. |
| NFR12 | Mantenibilidad: Domain no importa SDK/HTTP/LLM; Application depende de puertos; adapters traducen protocolos; contratos y configuración versionados. |
| NFR13 | Portabilidad: demo futura reproducible con runtime/containers fijados y datos sintéticos; camino mock sin API key ni tenant. Hardware y licencias se documentan. |
| NFR14 | Accesibilidad: confirmación muestra reserva, consecuencia, vencimiento y botones diferenciados; teclado/labels legibles; resultado Pending/Unknown claro en ambos idiomas. |
| NFR15 | Costo: límite local propuesto USD 5 por día de evals live y USD 0,05 por conversación de 10 turnos como objetivo. Aplicar contador de tokens y cálculo con tarifa versionada; bloquear nuevas llamadas al agotar presupuesto. Reconciliación y handoff continúan. |
| NFR16 | Retención demo: mensajes sanitizados 7 días, auditoría operacional 30 días, trazas 7 días; seed sintético versionado permanente. Borrado y backups deben seguir igual política antes de datos reales. |
| NFR17 | Disponibilidad: no se promete 99,9% con un equipo local. Se prueban degradación por fallo de LLM/index/fuente y recuperación por reinicio. SLO productivo exige HA y error budget propios. |
| NFR18 | Entrega: cambios de prompts/modelos/chunks/ACL/tools/reglas pasan regresión; release versiona bundle completo. Vulnerabilidades críticas/altas explotables y fallos de safety bloquean promoción. |

## Presupuesto de dependencias

Deadline de turno 12 s: autenticación/estado y validaciones <= 1 s; búsqueda <= 1 s; LLM/lecturas <= 8 s en conjunto; validación/render <= 1 s; margen <= 1 s. Los límites de llamadas individuales se ajustan al tiempo restante; sumar retries no amplía el deadline. HTTP source read timeout 2 s; dispatch mutación timeout 3 s, sin asumir rollback remoto. Ingestión/embedding y resumen post-interacción van al worker.

La base SQL no disponible implica fail closed para mutaciones y no reconocer recepción durable. Qdrant no disponible permite lectura/preview determinista y mensaje de conocimiento no disponible; LLM no disponible permite plantillas seguras y handoff. No se transforma un fallo en una respuesta inventada.

