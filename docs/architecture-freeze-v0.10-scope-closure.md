# Architecture Freeze v0.10 — Alcance determinista

Estado: CLOSED para diseño previo al código, 2026-10-01. [ADR-023](adr/023-query-scope-before-semantic-inference.md) Accepted. Amplía ADR-022 tras detectar una regresión de alcance, sin cambiar autoridad ni datos.

DoR: dos selecciones incorrectas observadas en v0.9; consultas médicas/ejecución de pagos fuera de scope inicial; política en Application, invocada tras autorización y antes de inferencia; pruebas negativas y de falsas alarmas. No ampliar herramientas ni permisos, no alterar confirmación o fuente.

AC: dosis/diagnóstico, reembolso solicitado de dinero, secretos internos e instrucciones de ignorar reglas se abstienen con OUT_OF_SCOPE y cero inferencia. Preguntas informativas sobre alergias y Wi-Fi permanecen permitidas. Otra cuenta no puede usar el filtro para acceder al recurso: se rechaza primero. Tiempo de regex acotado, fracaso cerrado. Corpus y gold preservados. Evaluación v0.10 nueva separada; revisión humana y gates empresariales permanecen explícitos.

Ver [secuencia de alcance](diagrams/query-scope-retrieval.mmd). Rollback de la selección v0.9 si falla un invariante; conservar informes/DBs/modelos. No activar el candidato hasta pasar la nueva evaluación.

Ampliación previa a la corrección final: la corrida v0.10 detectó `devolver mi dinero`. Aprobar solo el ajuste de conjugaciones, verificaciones negativas/positivas y replay compuesto definido en ADR-023; preservar outputs y tiempos iniciales, identificar cada resultado reutilizado. No repetir inferencia de consultas no afectadas ni presentar el replay como una nueva medición completa de IA.
