# Explicar el proyecto en una entrevista

[English](../en/portfolio.md)

Empieza con un ejemplo que funcione: «Es un asistente bilingüe en C# para reservas ficticias. Quise demostrar el flujo alrededor de la IA: identidad, permisos, confirmación explícita y recuperación cuando falla un servicio externo».

| Habilidad de la vacante | Evidencia | Límite que conviene explicar |
|---|---|---|
| C#/.NET, REST e integración | API por capas, workers y fuente HTTP separada | Implementación de portafolio |
| Agentes, prompting y tools | Esquema de intención cerrado y casos ejecutados por servidor | El modelo no concede permisos ni escribe directamente |
| RAG y embeddings | Vectores locales reales, selección y publicación con permisos | Corpus pequeño, búsqueda exacta y ocho errores registrados |
| SQL y operaciones confiables | SQLite real, diseño/adapter SQL Server y comprobantes | Runtime SQL cifrado empresarial pendiente |
| Seguridad y RBAC | OIDC real, propiedad y asignación de agentes | Entorno local ficticio |
| Genesys/contact center | Handoff neutral y mock con fallos | Sin tenant real, voz, Agent Assist ni post-call |
| Pruebas, Git y CI/CD | Suites, evaluación y workflow fijado | Comprobar CI remota después de publicar |
| Comunicación bilingüe | Comportamiento y guías ES/EN | Falta revisión lingüística independiente |

Debes poder explicar cuatro decisiones: por qué Confirmar es una acción de UI, por qué la fuente decide el resultado final, por qué un timeout produce Unknown y por qué teléfono/ID de Telegram no identifican a un miembro autenticado.

Archivos útiles: [flujo de cancelación](../../src/ContactCenterAI.Application/CancellationWorkflow.cs), [permisos](../../src/ContactCenterAI.Domain/ResourceAccess.cs), [cliente HTTP fuente](../../src/ContactCenterAI.Infrastructure/HttpReservationSource.cs), [arquitectura de canales](../integrations/channels.es.md) y [límites de evaluación](evaluacion-y-limites.md).

Evita presentarlo como producto Genesys completo o listo para producción. Explica qué construiste, cómo se probó y qué comprobarías en un tenant. El desarrollo asistido por IA forma parte del proceso; entiende el código y los compromisos antes de presentarlo como tu trabajo.
