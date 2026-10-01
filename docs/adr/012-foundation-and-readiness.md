# ADR-012 — Foundation y readiness explícita

Estado: Accepted — B02 local. Fecha: 2026-09-30. Complementa ADR-001/002/010/011; no cambia autoridad, acceso o reglas de Freeze v0.1.

**Contexto.** B01 verificó runtime, SQL/OIDC y dos logins reales. Se necesita una solución compilable que proteja dependencias antes de implementar persistencia, identidad productiva y negocio. Un host iniciado no prueba que pueda aceptar mensajes durables.

**Decisión.** Crear Domain, Application, Infrastructure, Api y Worker en .NET 10, con API/worker como raíces de composición. Domain sin HTTP, SDK o paquetes externos; Application solo referencia Domain. Worker usa el mismo shared framework ASP.NET Core para Generic Host, sin añadir paquete Hosting duplicado. SQL/EF y adapters se añaden en sus slices.

En B02 solo se implementan `/health/live` y `/health/ready`. Live indica proceso activo; Ready devuelve 503 `OPERATIONAL_STORE_NOT_CONFIGURED` hasta disponer de almacenamiento operativo real. No reutilizar la cuenta sa del spike como readiness productiva. Ninguna ruta `/v1` existe aún y el worker permanece inactivo, sin aceptar ni ejecutar trabajos.

OpenAPI 3.1.0 separa el contrato del host implementado del contrato preliminar de conversaciones, explícitamente `planned`. Antes de B03/B04 se completa revisión de identidad, CSRF, persistencia, errores y límites de cada handler. Se verifica el grafo de proyectos y referencias compiladas, además de liveness/readiness y ausencia de rutas de negocio en un proceso real.

**Alternativas.** Declarar Ready por arrancar o por conectar con sa ocultaría gates pendientes. Implementar respuestas 202 en memoria violaría NFR03. Generar todos los adapters futuros añadiría interfaces sin consumidores. Las tres alternativas se descartan.

**Consecuencias.** B02 entrega estructura y controles de dependencia, no satisface FR01 ni AC04. El 503 es el resultado esperado de una foundation sin DB operativa. B03/B04 habilitarán capacidades verificadas; los health checks remotos requerirán política de exposición propia.

Referencias: FR01, NFR03/12/13, AC30 como comportamiento futuro; [DoR B02](../progress/b02-foundation.md), [contratos](../contracts/api-and-tools.md).
