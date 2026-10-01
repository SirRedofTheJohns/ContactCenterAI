# Requisitos funcionales

Prioridad MUST para el MVP local salvo indicación explícita. Cada requisito tiene criterio de aceptación en [casos de uso](05-use-cases-acceptance.md) y trazabilidad en [matriz](06-traceability.md).

| ID | Comportamiento requerido |
|---|---|
| FR01 | Mantener conversación, mensajes, idioma explícito es/en, version y ownership epoch en SQL; reanudar tras reinicio sin usar memoria del modelo como autoridad. |
| FR02 | Responder en el idioma elegido; preguntar si una entrada ambigua cambia idioma. Cambiar idioma no cambia permisos, reglas ni identidad. |
| FR03 | Permitir FAQ pública anónima; exigir OIDC y vínculo verificado principal-miembro antes de acceder a cuentas. Rechazar identity claims del texto. |
| FR04 | Autorizar cada lectura/mutación por rol, tenant y propiedad/asignación activa; denegar por defecto y evitar enumeración de reservas. |
| FR05 | Leer detalle mínimo de una reserva a través de Reservation Port; sistema fuente autoritativo, sin SQL generado por modelo. |
| FR06 | Calcular preview con CP-001 v1 y devolver oferta inmutable, caducidad, rowVersion, versión de política y consecuencias. |
| FR07 | Pedir confirmación explícita de una sola oferta visible; guardar el acto autenticado, consumirlo una vez y rechazar ofertas vencidas o alteradas. |
| FR08 | Ejecutar cancelación con commandId estable, reglas fuente y control de versión. Comunicar Completed solo con resultado fuente; timeout posterior al envío produce Unknown. |
| FR09 | Deduplicar eventos y comandos, responder consistentemente a repeticiones y reconciliar outcomes inciertos sin repetir efectos a ciegas. |
| FR10 | Recuperar solo conocimiento publicado, vigente y autorizado; revalidar metadatos SQL antes de entregar contenido al LLM o UI. |
| FR11 | Respuestas factuales de políticas incluyen citas verificables de versión/sección. Sin evidencia suficiente, abstenerse y ofrecer handoff; no inventar reglas. |
| FR12 | Ingesta curada con versionado, revisión por otro principal, publicación atómica, expiración, revocación y reconstrucción del índice. |
| FR13 | Registrar feedback sobre respuesta/fuente y enviarlo a revisión; no cambiar política, prompt o documento automáticamente. |
| FR14 | Aceptar proposals de tools con esquema cerrado; seleccionar allowlist según estado y permiso, validar argumentos y acotar pasos/tiempo/costo. |
| FR15 | Transferir por solicitud explícita, petición no soportada, fallo de identidad, evidencia insuficiente o riesgo; preservar hechos mínimos, estado y resultados pendientes. |
| FR16 | Mantener bot silencioso al solicitar handoff respecto a herramientas nuevas; confirmar transferencia solo tras acknowledgment. Procesar eventos duplicados/fuera de orden con control de epoch. |
| FR17 | Producir resumen ES/EN, categoría y disposition desde hechos verificados; marcar Unknown/Pending y edición humana. No guardar chain-of-thought. |
| FR18 | Auditar intención, autorización, versiones, confirmación, comando, resultado y fallo con IDs de correlación, sin PII libre. |
| FR19 | Exponer adapter contact center neutral con recibir, responder, solicitar handoff, contexto y eventos de aceptación/fallo; mocks de timeout, reorder y duplicates. |
| FR20 | Permitir empleado asignado recuperar contexto y buscar conocimiento interno; impedir acceso a otras conversaciones, drafts y documentos no permitidos. |
| FR21 | Aplicar kill switch para detener mutaciones y habilitar fallback humano manteniendo lectura segura y consulta de resultado. |
| FR22 | Exportar artefactos sanitizados de evaluación y versiones de modelo/prompt/tools/corpus/reglas; reproducir escenarios pareados ES/EN. |

