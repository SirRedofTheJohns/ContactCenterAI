# Catálogo de diagramas Mermaid

Fuentes canónicas `.mmd`, renderizables con Mermaid en GitHub/editor compatible. Las copias embebidas de los documentos deben coincidir con estas fuentes.

| Archivo | Vista |
|---|---|
| [c4-context.mmd](c4-context.mmd) | C4 L1: personas, sistema y dependencias externas |
| [c4-containers.mmd](c4-containers.mmd) | C4 L2: unidades ejecutables y almacenes |
| [c4-components.mmd](c4-components.mmd) | C4 L3: componentes del proceso API |
| [deployment-local.mmd](deployment-local.mmd) | Despliegue local propuesto y perfil sandbox separado |
| [sequence-rag.mmd](sequence-rag.mmd) | RAG autorizado con citas y abstención |
| [sequence-cancellation.mmd](sequence-cancellation.mmd) | Preview, confirmación y mutación durable |
| [sequence-reconciliation.mmd](sequence-reconciliation.mmd) | Timeout, crash y reconciliación fuente |
| [sequence-handoff.mmd](sequence-handoff.mmd) | Transferencia, acknowledgment y epoch |
| [sequence-ingestion.mmd](sequence-ingestion.mmd) | Gobierno, indexación y revocación |
| [sequence-session-ingress.mmd](sequence-session-ingress.mmd) | Code+PKCE, rotación, autorización y commit de conversaciones/mensajes |
| [state-conversation.mmd](state-conversation.mmd) | Máquina de estado de conversación |
| [state-command.mmd](state-command.mmd) | Máquina de estado del comando |
| [data-model.mmd](data-model.mmd) | Modelo conceptual; source entities externas |
| [demo-local.mmd](demo-local.mmd) | Perfil de presentación v0.2: login OIDC real y SQLite loopback |
| [source-demo.mmd](source-demo.mmd) | B05 de presentación: adapter HTTP, fuente y DB independientes |

Los C4 usan flowcharts rotulados por nivel para evitar dependencia de sintaxis C4 experimental. No se requieren archivos Figma/FigJam ni publicación externa.



| [sequence-demo-transactions.mmd](sequence-demo-transactions.mmd) | Implementación local de confirmación/recovery |
| [assistant-demo.mmd](assistant-demo.mmd) | Asistente simulado, evidence gateway y handoff |

| [local-ai-intent.mmd](local-ai-intent.mmd) | Inferencia local real y gateway determinista |


## Recuperación local medida

[Consulta semántica](local-semantic-retrieval.mmd), [ingesta](local-knowledge-ingestion.mmd) y [selección de evidencia](evidence-selection.mmd).


[Alcance determinista y recuperación autorizada v0.10](query-scope-retrieval.mmd).
