# Presentación del proyecto

Material para mostrar ContactCenterAI. Reutiliza la demo Docker en modo determinista y los escenarios de la página. **No existe un video grabado**; abajo están las instrucciones para hacerlo. Para preguntas técnicas más largas, usa el [guion de entrevista](interview-script.md).

## Guion de 60–90 segundos

Prepara antes: la demo iniciada (`start-demo.cmd`), la sesión de `customer-a` abierta y la ventana a 1366×768.

| Tiempo | En pantalla | Qué decir |
|---|---|---|
| 0:00–0:12 | Página del resort, cabecera con "Modo determinista" | "Un chat puede malinterpretar un mensaje, repetirlo o perder una respuesta. Construí un resort ficticio para mostrar cómo una conversación llega a una reserva sin que el texto decida permisos ni duplique operaciones." |
| 0:12–0:35 | Chat: `Reservar Deluxe <fecha> a <fecha> para 2 personas` → aparece la propuesta | "El mensaje solo genera una propuesta: habitación, fechas, huéspedes, total y vencimiento. Todavía no se ha reservado nada." |
| 0:35–0:50 | Pulsar *Confirmar* → recibo y calendario ocupado | "Solo este botón ejecuta. El servidor revalida disponibilidad, precio y permisos en una transacción y guarda un recibo." |
| 0:50–1:10 | Pulsar *Repetir confirmación* → "Mismo recibo que antes"; luego escribir `Cancelar STAY-B0000001` → `RESOURCE_NOT_FOUND` | "Una garantía: si confirmo dos veces, recibo el mismo recibo y no hay reserva duplicada. Y conocer el código de otro cliente no me da acceso." |
| 1:10–1:25 | Abrir el panel técnico | "El panel muestra lo que el servidor decidió. Es C# con SQLite y login real con Keycloak, y se ejecuta con un solo comando en Docker." |

Cierre opcional (5 s): "Hay un modo de IA local opcional; lo probé en mi equipo, pero hoy el modelo fijado no se puede descargar de nuevo, y eso está documentado."

## Explicación de unos dos minutos para entrevista

**Qué construí (30 s).** Una demo de reservas para un resort ficticio. El cliente conversa en un chat web, recibe una propuesta y la confirma. Detrás hay un backend en C#/.NET con SQLite, login real con Keycloak (OIDC) y una demo empaquetada en Docker que otra persona puede ejecutar sin mi equipo.

**Decisiones importantes (60 s).**
1. **El texto propone y el servidor decide.** La identidad sale de la sesión, nunca del mensaje. Si alguien escribe "soy customer-b", no cambia nada.
2. **Propuesta y luego confirmación explícita.** La propuesta no reserva la habitación. Al confirmar, una transacción revalida todo y asigna cada noche una sola vez; una clave única impide la doble reserva.
3. **Idempotencia y recibos.** Confirmar otra vez devuelve el mismo recibo. Si se pierde una respuesta, se recupera consultando el recibo, sin volver a ejecutar.
4. **Preguntar en vez de suponer.** Si faltan fechas o el número de huéspedes es ambiguo, el chat pregunta.
5. **Docker sin debilitar controles.** El navegador y la aplicación ven Keycloak con nombres distintos. Lo resolví con configuración explícita, puertos solo en loopback y validación del issuer, sin quitar las protecciones existentes.

**Limitaciones (30 s).**
- El modo por defecto es determinista; su comprensión del lenguaje es limitada.
- La IA local se ejecutó en mi equipo con una muestra de desarrollo de 22 casos: 21/22 antes de un ajuste del prompt y 22/22 después. Ese ajuste incluyó un ejemplo derivado de uno de los casos, así que no es una medida de calidad general.
- Hoy el modelo fijado no se puede volver a descargar.
- Solo comprobé Windows con Docker Desktop.
- La CI remota no se ejecuta por un bloqueo de facturación de la cuenta.
- Es una demo local, no un sistema en producción.

## Cómo grabar el recorrido

1. **Preparar.** Ejecuta `reset-demo.cmd` y luego `start-demo.cmd`, para tener datos y fechas de hoy. Inicia sesión como `customer-a`, deja la ventana del navegador a 1366×768 y el zoom al 100–110 %. Cierra pestañas y notificaciones.
2. **Herramienta.** En Windows, Xbox Game Bar (`Win+Alt+R` para empezar y detener) o OBS Studio grabando solo la ventana del navegador.
3. **Contenido.** Sigue la tabla del guion de 60–90 segundos. Escribe los mensajes antes de pulsar *Enviar*, para que se lean.
4. **Revisar.** El video no debe mostrar contraseñas distintas de la clave pública de demo, tokens, el panel de Docker con secretos ni otras ventanas.
5. **Publicar.** Si lo subes, enlázalo desde el README e indica la fecha y el commit grabados. Hasta entonces, el README no menciona ningún video.

Capturas reales disponibles: [escritorio](progress/screenshots/resort-v0.13-desktop.png), [móvil](progress/screenshots/resort-v0.13-mobile.png), [propuesta con IA local](progress/screenshots/resort-v0.13-ai-proposal.png) y [error explícito sin modelo](progress/screenshots/resort-v0.13-ai-unavailable.png).
