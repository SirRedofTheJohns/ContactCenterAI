# ADR-010 — Bundle de release, observabilidad y minimización

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** Cambiar documentos o prompts puede cambiar producto sin modificar código. Logs completos de prompts facilitan debug pero exponen datos.

**Decisión.** Release bundle versionado, gates CI por riesgo, OTel técnico y audit SQL transaccional separados, logs por allowlist, datos sintéticos y retención demo. CI con pins/locks/secret scanning. Rollback compatible sin revertir efectos fuente; kill switch detiene dispatch nuevo y recovery continúa.

**Alternativas.** Modelo/corpus fuera de versionado: irreproducible. Audit solo por traces: pérdida por sampling/export. Log completo de HTTP/prompts: fuga innecesaria. Aplicación como certificación PCI/privacidad: evidencia insuficiente.

**Consecuencias.** Mayor disciplina de release y métricas, menos comodidad al inspeccionar texto libre. Contratos proveedor/región/retención y HA/restore son gates antes de datos reales. Revisar por hosting/compliance real o nuevos canales.

Referencias: FR18/21–22; NFR02/10/16/18; AC28/30–32; runbooks.

