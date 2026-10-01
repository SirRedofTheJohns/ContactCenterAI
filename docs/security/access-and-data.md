# Seguridad, identidad, acceso y datos

## Identidad y fronteras

El IdP autentica; el backend valida issuer, audience, firma, expiración y mapea subject a MemberRef. Claims de rol solo de emisor confiable y asignaciones servidor. No usar preguntas personales, email, número de reserva o voz del modelo como autenticación. Login/step-up sucede fuera del prompt. IdP local será compatible OIDC con cuentas sintéticas; la elección/versión de distribución se fija en el spike M1.

Para UI se adopta BFF/sesión y OIDC Authorization Code + PKCE, rotación de sesión al login, cierre/expiración y CSRF. Integraciones usan identidades de servicio de privilegios mínimos; nunca tokens del cliente en prompts. Flujo OAuth se contrasta con [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html).

## RBAC más autorización de recursos

| Capacidad | Anónimo | Customer | Agent | Supervisor | KnowledgeEditor | KnowledgeReviewer | OperationsAdmin |
|---|---|---|---|---|---|---|---|
| FAQ pública | Sí | Sí | Sí | Sí | Sí | Sí | Sí |
| Reserva / contexto privado | No | Propio | Asignación activa | Asignación o caso escalado asignado | No | No | No |
| Preview | No | Propio | Asignado | Asignado | No | No | No |
| Confirmar cancelación en v0.1 | No | Oferta propia | No | No | No | No | No |
| Solicitar handoff | Sesión propia | Propio | Asignado | Asignado | No | No | No |
| Conocimiento interno | No | No | Según ACL | Según ACL | Según ACL | Según ACL | Sin permiso implícito |
| Crear versión | No | No | No | No | Sí | No | No |
| Aprobar/publicar/revocar | No | No | No | No | No | Sí, distinto de creador | No |
| Métricas / audit sanitizado | No | No | Propio caso permitido | Casos asignados | No | Publicaciones propias | Operacional sanitizado |
| Kill switch / configuración técnica | No | No | No | No | No | No | Sí |
| Override reglas / leer todo | No | No | No | No | No | No | No |

Un principal puede tener más de un rol, pero separación creador/reviewer compara **subject**, no solo role. No basta obtener ambos roles. Identidad de servicio tiene permisos de transporte; no adquiere acceso de miembro. API fuente verifica delegación/ownership; un rol de sistema no autoriza cualquier reserva.

Filtros obligatorios: tenantId, principal, MemberRef o Assignment vigente, ownership epoch, estado y ACL de documento. No existen tools que puedan modificar estos filtros. Revocación de asignación/token invalida nuevos accesos; antes de una mutación se revalida. No guardar autorizaciones positivas indefinidamente en conversation context.

## Minimización y retención

| Dato | Ubicación permitida / uso |
|---|---|
| Subject y MemberRef opacos | SQL operativo y comprobación de permisos; modelo no recibe email/nombre por defecto |
| Reserva mínima | DTO al workflow/modelo con alias, propertyCode, estado e instantes; sin contrato completo |
| Tokens / secretos | Secret manager o entorno local excluido del repo; nunca SQL conversación/log/LLM |
| Entrada chat | Sanitización y detección de datos sensibles antes de almacenar/enviar; muestra aviso y canal seguro para pagos |
| Citas | IDs de doc/version/section; links al backend que revalida ACL |
| Audit | IDs opacos, códigos, versiones, timestamps y resultado; sin prompt/transcript libre |
| Trace / métricas | Metadatos técnicos y tiempos; sin cuerpos HTTP ni cardinalidad por miembro |
| Summary | Hechos sanitizados y versión/autor; acceso por asignación |

La demo conserva mensajes sanitizados 7 días, audit 30 y traces 7; no son plazos legales recomendados. Logs se construyen por allowlist de campos, con redacción de emergencia antes de exportar. No se registra PAN/CVV ni siquiera enmascarado como estrategia de almacenamiento; se excluye de prompts y memoria. Detectors no son perfectos: usar datos sintéticos y DTOs mínimos, además de pruebas canary y revisión.

En producción: cifrado en tránsito y reposo, backup cifrado, control de borrado, revisión de ubicación de datos, permisos fuente, subprocesadores y retención del proveedor. No declarar cumplimiento PCI o privacidad por tener TLS/RBAC.

## Proveedor de IA

La documentación de OpenAI dice que los datos de API no se usan para entrenamiento salvo opt-in y distingue retención de monitoreo y estado de aplicación. No equiparar «sin entrenamiento» con «sin retención». ZDR exige elegibilidad y configuración propias. [Data controls](https://developers.openai.com/api/docs/guides/your-data).

Decisión de proyecto: datos sintéticos, opt-in deshabilitado, minimizar prompt, estado conversacional local y solicitud sin almacenamiento proveedor cuando el endpoint lo permita. Antes de datos reales se exige aprobación contractual, región y revisión de retención por endpoint. Azure OpenAI es una posible implementación futura del port; no se presupone que sus condiciones sean idénticas.

## Controles técnicos de IA

Tratar entrada, documento, tool output y respuesta modelo como datos no confiables. Separar instrucciones de evidencia; allowlist de tools por estado; validación de schema y semántica; budget; sin URLs libres/SQL/shell; output HTML/Markdown escapado y links permitidos. Guardrails sirven como detección adicional: nunca sustituyen el ejecutor determinista. Política/reglas, ACL y confirmation ledger no dependen de prompts.

