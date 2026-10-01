# Architecture Freeze v0.9 — Cierre de presentación

Estado: CLOSED para diseño antes de implementación, 2026-10-01. [ADR-022](adr/022-retrieval-recall-and-final-delivery.md) Accepted. Conserva los freezes e informes anteriores.

DoR: fallo observable de selección en v0.8, calibración dev separada, cambio pequeño en elegibilidad de ranking/prompts, pruebas negativas y revalidación final. Ningún cambio en confirmación, comandos, sesiones, tenant, propiedad, publicación ni fuente de reservas. No añadir herramientas de escritura al LLM.

AC adicionales:

- Un pasaje con score 0.35–0.55 requiere elección cerrada del reranker; no se entrega por score solamente.
- El selector puede abstenerse; una elección inferior a 0.35 se rechaza. El modo sin reranker conserva 0.55 y margen.
- Revocación/cambio de versión durante la inferencia se vuelve a comprobar.
- Dataset adicional y hash se guardan antes de editar código; sus resultados no son revisión humana independiente.
- Regresión y evaluación adicional conservan todos los resultados y latencias; no reemplazan informes históricos.
- Arranque y revisión breve del producto con OIDC real; guardar evidencia nueva separada de la prueba anterior.
- El lanzador no declara disponibilidad con Keycloak apagado; puede iniciar solo su contenedor existente y previamente identificado por labels, sin descargas ni cambios de datos.
- La conexión SQL live usa .NET, cifrado y credencial restringida. Si falla, ningún cierre empresarial se declara por un fixture.

Rollback: conservar v0.8; restaurar umbral/prompt anterior si falla una regla de seguridad. No resetear las bases existentes. Operación Genesys continúa detrás de IContactCenterAdapter y con el mock local hasta disponer de tenant.
