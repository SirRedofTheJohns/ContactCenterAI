# Threat model: STRIDE y riesgos específicos de IA

## Activos y fronteras

Activos: identidad, datos de reserva, comandos/confirmaciones, políticas, corpus/ACL, secretos, audit y propiedad de conversación. Fronteras: navegador→API; IdP→claims; integración→ingress; API/worker→SQL; modelo→gateway; corpus/vector→prompt; worker→fuente; backend→agente/Genesys; build→release.

Inputs de usuario, documentos, índices derivados, resultados fuente y proposals del modelo se validan; aprobación de documento no convierte su contenido en instrucciones. SQL es autoridad de estado/ACL, fuente de reserva de negocio, IdP de identidad. Un atacante puede controlar texto y argumentos, repetir solicitudes, conseguir un documento malicioso en review y provocar cortes/redelivery.

STRIDE: S suplantación, T alteración, R repudio, I divulgación, D denegación, E escalación. Impacto alto cuando permite acceso privado o mutación; se prioriza por impacto y exposición, no puntuación ficticia.

| ID / categoría | Ataque y frontera | Control requerido | Validación / responsable | Riesgo residual |
|---|---|---|---|---|
| TH01 S/E | Chat dice ser otro miembro / claim falso | IdP verificado, mapping servidor, recurso/tenant/asignación | AC04–06; Backend/Security | Cuenta IdP comprometida exige step-up productivo |
| TH02 E | Modelo inventa cancel tool/admin/SQL | Tool allowlist + schema + semantic checks; no write tool del LLM | AC25–26; Backend/QA | LLM puede generar texto incorrecto; no concede acceso |
| TH03 T/E | Prompt injection directo | Datos delimitados, ejecutor determinista, budgets | AC03/25; AI/QA | No garantía de erradicación semántica |
| TH04 T/I | Inyección indirecta / poisoning aprobado | Review distinto, chunks como data, ACL, citas y revoke SQL | AC03/22–23; Knowledge/Security | Reviewer puede pasar contenido sutil; revocación y evals |
| TH05 I | Cross-account / cross-tenant / cita directa | Ownership/asignación en cada endpoint y retrieval; cache scope | AC05/21/23; Backend | Aislamiento multi-tenant físico no cubierto |
| TH06 I | PII en prompt, summary, HTTP logs o métricas | DTO mínimo, allowlist log, redacción, synthetic-only | AC28; Security/Operations | Detectors imperfectos; datos reales requieren revisión |
| TH07 T/R | Cambiar oferta o confirmar desde otro usuario | Oferta inmutable vinculada, expiración, hash, acto UI + CSRF | AC08/10/12; Backend | Robo de sesión; cookie/step-up/rotation |
| TH08 T | Replay, doble click, workers concurrentes | Unique keys, offer consumption, source command dedupe/CAS | AC13–16; Backend | Fuente real sin receipt confiable bloquea automatización |
| TH09 T/R | Timeout después del commit; mentira de éxito | Submitted/Unknown durable, query commandId, plantilla final | AC15–16/27; Backend/Operations | Caída fuente deja resultado pendiente, requiere humano |
| TH10 S/T | Webhook falsificado, disorder o callback viejo | Auth específica, inbox hash, requestId+epoch, CAS | AC18–20; Integration/Security | Protocolo real aún debe probarse |
| TH11 D | Loop tools, spam, input gigante, costo ilimitado | Tamaño/cuota/deadline, max calls, rate limit y kill switch | AC26/30–31; AI/Operations | DDoS volumétrico productivo requiere edge protection |
| TH12 T/I | SSRF, URL de tool/cita, Markdown XSS | URLs fijas/allowlist, ningún arbitrary_http, escape/sanitize UI | AC25/28; Backend/UI | Nuevos formatos/uploads requieren revisión |
| TH13 R/T | Falta de auditoría / alteración por runtime | Audit transaccional con intención, append-only grants y correlation | AC08–09/16; Backend/Operations | Admin DB puede alterar; WORM/tamper evidence posterior |
| TH14 E/T | Uploader se autoaprueba / override supervisor | Separación por subject y RBAC; no override de negocio | AC22; Knowledge/Security | Compromiso de dos cuentas no resuelto por RBAC solo |
| TH15 T/E | Dependencia/build/workflow comprometido | Pins/locks, SBOM, mínimos permisos CI, review y secret scan | AC32; DevOps | Supply chain requiere mantenimiento continuo |
| TH16 I/E | Agente lee caso no asignado / admin omnipotente | Assignment vigente y ACL; OperationsAdmin sin permisos miembro | AC06/21; Backend/Security | Uso legítimo indebido requiere supervisión organizacional |
| TH17 T/I | Índice viejo conserva documento revocado | Payload filter + SQL recheck antes de texto; cache invalidation | AC02/23; Knowledge | Compromiso DB operativo cambia autoridad y exige respuesta incidente |
| TH18 T | Bot y humano actúan a la vez | HandoffPending stop, acknowledgment y ownership epoch; in-flight ledger | AC17–20; Integration | Delivery ya en red puede ser tardío: dedupe/epoch por destino |

## Seguridad comprobable y límites

La suite fuerza proposals maliciosas directamente al gateway, falsifica callbacks y simula races; no considera seguro al sistema solo porque el modelo respondió «no puedo». El indicador de ataque exitoso es exfiltración, bypass, efecto/lectura no autorizada o false-success, comprobado sobre estado y artefactos.

Mitigaciones de prompt injection se basan en defensa en capas y permisos externos al LLM. No se implementa regex como frontera de seguridad ni se guardan logs completos de prompts para «monitorear» a costa de privacidad. [OWASP prompt injection prevention](https://cheatsheetseries.owasp.org/cheatsheets/LLM_Prompt_Injection_Prevention_Cheat_Sheet.html).

## Respuesta a incidentes

Ante mutación sospechosa: kill switch, preservar audit sanitizado, detener dispatch nuevo, reconciliar comandos enviados, revocar credenciales afectadas y revisar ownership/asignación. Ante documento malicioso: revoke SQL primero, invalidar caches, quitar índice, identificar respuestas afectadas por citation IDs y reevaluar. Ante fuga: bloquear export, aplicar retención/revisión y no pegar datos sensibles en issues. Runbooks operacionales detallan pasos y criterios de recuperación.



## Control local v0.10 y riesgo residual

[ADR-023](../adr/023-query-scope-before-semantic-inference.md) incorpora rechazo acotado de solicitudes explícitas médicas, ejecución de reembolso, secretos, override y nombre propio de otra propiedad, después de autorizar el recurso y antes de inferencia. Sus regex tienen límite de tiempo y se abstienen al fallar. Es filtro de alcance, no frontera de identidad/seguridad ni detector completo de inyección: ACL, publicación, confirmación y receipts siguen siendo la autoridad. Se prueban conjugaciones ES y falsas alarmas Wi-Fi/alergias/política informativa; replay conocido preserva errores semánticos. Ocho fallos de calidad siguen abiertos, incluido un documento irrelevante: una cita válida no prueba pertinencia.
