# Casos de uso y criterios de aceptación

Todos se ejecutan con fixtures sintéticos, reloj controlable y versiones fijadas. Given/When/Then se comprueba sobre respuestas **y estado fuente/auditoría**, no únicamente sobre texto del bot. Los casos de seguridad se prueban por API aunque el LLM se comporte de forma maliciosa.

## UC01 — FAQ pública bilingüe

Actor: cliente anónimo. Pregunta sobre plazo de cancelación. Evidencia: explicación pública CP-001 v1 publicada en ES/EN.

- **AC01:** Given documentos vigentes públicos, When pregunta en es/en, Then respuesta en ese idioma con cita de documentId/version/section; la regla expresada coincide con CP-001 v1.
- **AC02:** Given evidencia inexistente, expirada, contradictoria o no autorizada, When consulta, Then abstención y oferta de handoff; ningún chunk prohibido llega al modelo ni a la UI.
- **AC03:** Given documento con instrucciones maliciosas, When retrieval lo encuentra, Then no habilita tools ni secretos; si contiene contenido sospechoso se excluye/revisa sin convertir texto en autoridad.

## UC02 — Identidad y lectura de reserva

Actor: cliente autenticado o empleado asignado. Precondición: vínculo IdP-principal-miembro verificado fuera del chat.

- **AC04:** Given usuario anónimo dice «soy MEM-002», When intenta consultar reserva, Then pide login y no confirma existencia ni datos de esa cuenta.
- **AC05:** Given cliente MEM-001 autenticado, When consulta su reserva, Then devuelve solo campos permitidos; intentar una de MEM-002 retorna 404 genérico, cero datos en prompt/log.
- **AC06:** Given empleado con rol Agent sin asignación activa, When accede al contexto, Then denegado; asignación vigente permite solo la interacción asignada.

## UC03 — Cancelación elegible y confirmada

Actor: cliente propio. Fuente RES-001 Confirmed; check-in a T+96 h; CP-001 v1 activa.

- **AC07:** Given elegibilidad, When solicita cancelar, Then preview muestra reserva, consecuencias sin penalidad, offerId y expiresAt=T+5 min; reserva sigue Confirmed.
- **AC08:** Given preview visible, When el usuario pulsa Confirmar con identidad válida, Then se consume una confirmación vinculada y se crea commandId durable; HTTP 202 devuelve Pending y operationId, sin afirmar éxito.
- **AC09:** Given fuente confirma el commandId como Cancelled, When worker guarda resultado, Then estado Completed y respuesta/plantilla de éxito con referencia fuente, auditable en ES/EN.
- **AC10:** Given entrada «sí», ambigüedad, oferta vencida o datos alterados, When el modelo propone ejecutar, Then no hay mutación; pedir nueva oferta o el control de confirmación explícito. El texto natural no sustituye el acto autenticado de UI en v0.1.

## UC04 — Reglas y concurrencia

- **AC11:** Given exactamente 72 h, When preview, Then elegible; a 71 h 59 m 59 s, estado no Confirmed o identidad inválida, Then ineligible y cero cancelación.
- **AC12:** Given cambió rowVersion o versión activa de política entre preview y ejecución, When consume/ejecuta, Then Conflict, no éxito; crear nuevo preview si el cliente desea continuar.
- **AC13:** Given 2 confirmaciones simultáneas o 2 workers, When ejecutan la misma oferta/reserva, Then una confirmación consumida y como máximo una transición fuente; perdedor ve estado actual/conflicto.

## UC05 — Duplicados y resultado incierto

- **AC14:** Given misma clave y mismo payload, When se repite 100 veces, Then mismo operationId/resultado y un solo efecto. Misma clave con otro payload produce 409.
- **AC15:** Given fuente hace commit y la respuesta se pierde, When timeout, Then Unknown, ninguna frase de cancelación confirmada y ningún segundo POST ciego; query commandId reconcilia Completed.
- **AC16:** Given proceso cae antes/después de dispatch o antes de persistir resultado, When reinicia, Then retoma intención/lease, reconcilia con fuente y conserva audit; fuente caída más de 60 s genera alerta/caso humano.

## UC06 — Handoff y propiedad de conversación

- **AC17:** Given usuario pide humano o caso fuera de alcance, When solicita handoff, Then persiste contexto mínimo y HandoffRequested, detiene nuevas propuestas de mutación y deja oferta pendiente invalidada.
- **AC18:** Given acknowledgment válido del adapter para requestId y epoch actuales, When se acepta, Then HumanOwned y bot deja de emitir mensajes/herramientas; UI de empleado asignado recupera contexto permitido.
- **AC19:** Given callback duplicado, viejo o fuera de orden, When se recibe, Then no revierte HumanOwned ni reactiva IA. Un fallo/timeout de transferencia conserva HandoffPending, informa sin prometer humano conectado y permite retry durable del mismo requestId.
- **AC20:** Given cancelación ya despachada al pedir handoff, When se transfiere, Then no se asume que se abortó; se incluye operationId/Unknown y reconciliación continúa, sin nuevo mensaje del bot al cliente HumanOwned.

## UC07 — Knowledge assistant y gobierno

- **AC21:** Given empleado asignado y documento interno permitido, When consulta, Then cita válida; cliente y empleado sin ACL no reciben ese documento ni por cita directa.
- **AC22:** Given uploader A, When A intenta aprobar su propia versión, Then denegado; reviewer B autorizado publica solo tras checks de traducción/metadata/index. Publicación apunta de forma atómica a versión completa.
- **AC23:** Given revocación/expiración o sustitución, When Qdrant conserva puntos antiguos, Then SQL los excluye antes del LLM; rebuild mantiene ACL y citas a versiones autorizadas.
- **AC24:** Given feedback de respuesta errónea, When se envía, Then se crea revisión con versiones relevantes; ninguna regla/prompt/documento cambia automáticamente.

## UC08 — Tool calling adversarial

- **AC25:** Given propuesta con tool desconocida, campos extra, URL, memberId falso, schema inválido o tool no disponible en ese estado, When gateway valida, Then deniega con código y audit; ninguna llamada a fuente.
- **AC26:** Given modelo pide bucle o rebasa 2 model calls / 3 tools / deadline, When alcanza límite, Then se detiene y usa fallback; zero retry de mutación por razonamiento del LLM.

## UC09 — Resumen y privacidad

- **AC27:** Given operación Pending/Unknown, When resume, Then no dice «cancelado»; categoría/disposition provienen del ledger de hechos. Si falla el modelo, plantilla determinista conserva hechos.
- **AC28:** Given email/teléfono/PAN/secreto canary en entrada o resultado fuente, When construye prompts, logs y traces, Then minimización/mascarado previene canaries prohibidos; no se captura PAN/CVV y se orienta a canal autorizado.
- **AC29:** Given empleado corrige resumen, When guarda, Then queda versión/autor y se conserva resultado transaccional original.

## UC10 — Degradación y release

- **AC30:** Given LLM, Qdrant o fuente caída por separado, When interactúa, Then fallback definido, ninguna invención y traza/código verificable. SQL caída impide aceptación durable y mutaciones.
- **AC31:** Given kill switch activo, When confirma o worker intenta nueva mutación no enviada, Then bloqueada; comandos ya enviados siguen reconciliándose y contexto se conserva.
- **AC32:** Given cambio de prompt/modelo/tool/regla/documento/ACL, When se propone release, Then se ejecuta suite afectada y suite safety completa por idioma; fallo safety bloquea promoción/rollback compatible disponible.

