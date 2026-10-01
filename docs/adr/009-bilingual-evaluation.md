# ADR-009 — Evals ES/EN y safety por invariantes

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** Una demo feliz en un idioma no prueba calidad ni seguridad. Un judge puede valorar bien una respuesta que ejecutó una acción indebida.

**Decisión.** C# prueba determinismo/contratos; Python eval/data. 200 casos pareados, dev/holdout separados, oráculos fuente/ACL/estado para safety y humanos para soporte semántico. Gates por idioma, counts/intervalos y failure artifacts sanitizados. Safety cero violaciones; no cherry-picking ni scores inventados.

**Alternativas.** Solo unit tests: no mide lenguaje/retrieval. Solo LLM judge: no demuestra seguridad y sesga scores. Un promedio ES+EN: oculta degradación. Dataset publicado usado como holdout: fuga metodológica.

**Consecuencias.** Requiere anotar y mantener corpus/versiones; seeds son dev, no benchmark final. Revisar thresholds si negocio/idiomas/scope cambia mediante nuevo baseline, no para aprobar una corrida fallida.

Referencias: FR22; NFR11; AC32; plan de evals.

