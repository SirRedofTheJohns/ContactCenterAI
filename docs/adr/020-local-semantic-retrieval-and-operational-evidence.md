# ADR-020 — Recuperación semántica local y evidencia operativa

Estado: Accepted. Fecha: 2026-10-01. Amplía ADR-004/014/017/019 para la presentación; no reemplaza el objetivo empresarial Qdrant/SQL/Genesys.

Problema: diez secciones elegidas por un tema del clasificador no prueban recuperación semántica. Se necesita una demo bilingüe reproducible y evidencia de recuperación de datos, sin repetir los bloqueos de Docker.

Decisión: BGE-M3 por la API oficial `/api/embed` de Ollama, un servidor local separado en 127.0.0.1:11435, CPU, modelo/digest fijados, sin cloud ni redirects. Descarga explícita autorizada en esta sesión; pesos dentro de `.local/embedding`, excluidos del entregable. MIT según [model card oficial](https://huggingface.co/BAAI/bge-m3); 1024 dimensiones, español/inglés. [Contrato de embeddings](https://docs.ollama.com/api/embed): `truncate=false`; no se aceptan vectores vacíos, no finitos o con dimensión distinta. No se extraen embeddings de un modelo de chat.

Índice local: vectores reales persistidos en SQLite con búsqueda coseno exacta en C#, suficiente para 40 documentos lógicos/80 variantes. No ANN ni Qdrant simulado. Un documento breve equivale a una sección estable `overview`; no fragmentar artificialmente. Índice por generación atómica, hash de contenido, modelo y versión. Ingesta independiente de consultas, sin sostener una transacción mientras se llama al modelo. Reindexar versiones publicadas, no borradores. Autorizar candidatos antes del ranking y resolver otra vez antes de entregar; las citas guardadas se revalidan al leer. Publicar/revocar/expirar invalida evidencia aun si el índice tarda en actualizarse.

Consulta: usar el texto sanitizado original, idioma de conversación y sesión comprobada; el tema propuesto por el LLM no filtra ni manda en la recuperación. Top cinco, umbral inicial coseno 0.55 y margen 0.015 entre documentos diferentes; calibrar exclusivamente con el split dev antes del holdout. Falta de índice, timeout, evidencia insuficiente o autorización fallida produce abstención segura. Respuesta extractiva con cita, sin generación factual libre. Cuestiones financieras fuera de alcance requieren abstención, no asumir que cualquier precio es un pago permitido. Política CP-001 permanece una regla de dominio independiente del índice.

Operación: exportar actividades con lista cerrada de etiquetas/códigos y duración, sin texto, IDs de sesión ni claves; panel restringido a OperationsAdmin. Backup consistente mediante API SQLite, restauración siempre en directorio nuevo, prueba aislada de integridad/reinicio; no alterar la DB que contiene la demostración del usuario.

Evaluación: dataset nuevo de 200 casos (100 pares ES/EN), 60 dev/140 holdout congelado antes de ejecutar. Los autores conocen las preguntas: holdout separa calibración, no es una evaluación independiente o ciega. Reportar recall@5, acierto/abstención y latencia con inferencia real; conservar el resultado previo de 38/40. Fallos se publican, no se edita el holdout para mejorar números. Dos revisores humanos y pruebas tenant/carga empresarial permanecen pendientes.

Alternativas: Qdrant requiere otro runtime inaccesible aquí; permanece como adapter futuro. Hashes léxicos etiquetados como embeddings darían evidencia falsa. Un servicio Python en el camino del usuario quebraría la separación acordada; Python sigue siendo evaluación/data tooling.

Consecuencias: descarga adicional de ~1.2 GB, memoria/latencia CPU y dos procesos locales. Índice exacto no prueba escalabilidad empresarial. CI usa fixtures de vectores/HTTP, no descarga modelos. No cerrar B08/B09/B15 empresariales por este avance local.
