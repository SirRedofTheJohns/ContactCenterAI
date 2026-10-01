# ADR-003 — LLM propone; software y fuente deciden

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** Una cancelación puede afectar negocio y no es reversible por borrar un mensaje. Intención no equivale a permiso/confirmación.

**Decisión.** CP-001 v1 determinista, oferta vigente ligada a principal/recurso/versión/epoch y control Confirmar autenticado. CancelReservation es comando interno, no tool de escritura del modelo. Fuente revalida antes de commit. Solo Completed fuente permite plantilla de éxito; Pending/Unknown se muestran explícitos.

**Alternativas.** Modelo decide elegibilidad: difícil de auditar y expuesto a injection. Chat «sí» interpretado por LLM como aprobación: ambiguo/replayable. Cancelar por tool libre con prompt «pide permiso»: control dependiente del modelo. Motor genérico de reglas: excesivo para una política.

**Consecuencias.** Un paso visible adicional y necesidad de UI específica. Canal real Genesys requiere confirmación equivalente probada; no se activa escritura si no existe. Revisar con nuevos tipos de transacción, excepciones, step-up o consentimiento de canal; nunca delegar permisos al LLM.

Referencias: FR06–08/14; INV01–04; AC07–16.

