# ADR-022 — Recall bilingüe y cierre local

Estado: Accepted, 2026-10-01. El usuario pidió completar los pendientes tras abrir Docker. No introduce autoridad del modelo ni modifica datos fuente.

La regresión v0.8 mostró abstenciones por un umbral coseno 0.55 aplicado antes de seleccionar evidencia. En desarrollo, preguntas generales relevantes tienen score 0.386–0.546 y el texto aprobado está entre los candidatos. Coseno es una medida de similitud, no una probabilidad de respuesta correcta.

Decisión: mantener 0.55/margen 0.015 para búsqueda sin reranker. Con el selector cerrado, admitir candidatos desde 0.35 y exigir elección explícita de una sección existente, autorizada y vigente. El umbral se calibra sobre los casos dev ya conocidos; toda comparación con los 200 casos históricos se etiqueta regresión. No modificar sus oráculos, no convertir abstenciones en aciertos ni sobrescribir v0.8. Mantener máximo cinco pasajes, contexto acotado, ocho segundos de producto, salida JSON cerrada, revalidación después de inferencia y texto final extractivo con cita.

El prompt v2 debe permitir respuestas generales sobre esta demo y reglas internas sin confundirlas con consultas en vivo, instrucciones o consejos médicos. Elegir 0 para una solicitud no sustentada, incluso cuando comparta palabras con un pasaje. Una regla que describe una derivación médica o una limitación operativa no autoriza al modelo a dar diagnóstico ni ejecutar acciones.

Validación previa a activar: suites deterministas, regresión real de los 200 casos, conjunto adicional congelado antes del cambio, preguntas fuera de alcance e intentos de inyección. Objetivo local ≥95% exacto y 100% abstención fuera de alcance. Reportar cada fallo y distinguir el conjunto adicional escrito por el mismo autor de una revisión humana independiente. El gate humano no puede cerrarse con autoevaluación.

SQL Server: comprobar la conexión .NET cifrada con login restringido antes de cambiar composición o migrar datos. Si el proveedor TLS de Windows impide esa conexión, conservar SQLite y registrar el bloqueo; no desactivar cifrado ni publicar una migración sin prueba. Docker se opera por UI únicamente para iniciar el stack existente. Los datos anteriores se conservan.

Arranque cotidiano: el lanzador comprobará también el issuer de Keycloak antes de decir que la demo ya está lista. Si está apagado, puede iniciar únicamente el contenedor existente cuyo nombre y labels Compose pertenecen a este proyecto. CLI local fija, sin crear contenedores, descargar imágenes, cambiar claves ni repetir B01. Una espera acotada comprueba el issuer exacto; un fallo deja un mensaje concreto. Esta llamada del lanzador usa el entorno normal de quien lo abre; no supone acceso al pipe Docker desde el perfil restringido del agente.

CI remota y Genesys real requieren destino autorizado y cuenta/tenant. Los mocks desacoplados y el workflow reproducible siguen siendo la entrega local cuando esos accesos no están disponibles.
