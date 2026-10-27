# Guion de demostración para entrevista

Duración: 8–10 minutos de demo y 5 de preguntas. Ten la demo iniciada antes (`start-demo.cmd`) y dos ventanas: una normal y otra privada, para tener a la vez una sesión de `customer-a` y otra de `customer-b`.

## 1. Problema en una frase (30 s)

"Un chat puede entender mal, repetir un mensaje o perder una respuesta. Quería demostrar cómo conectar una conversación con una operación de negocio sin que el texto pueda conceder permisos, duplicar una reserva o anunciar un éxito que no ocurrió."

## 2. Recorrido (6 min)

1. **Sin sesión:** "¿Qué incluye la Suite?" y luego una disponibilidad. *Mensaje: lo público no requiere identidad; los datos salen del inventario, no del modelo.*
2. **Pedir una reserva sin sesión:** el chat responde `SIGN_IN_REQUIRED`. Inicia sesión como `customer-a`; el mensaje escrito se conserva.
3. **Escenario 6:** "para 2 o 3 personas". *Mensaje: preguntar en vez de suponer; el valor por defecto antiguo del parser no se usa para decidir.*
4. **Escenario 1:** propuesta → mostrar habitación, fechas, total y vencimiento → Confirmar → calendario. Abre el **panel técnico**: interpretación, código, propuesta y recibo. *Mensaje: la interpretación no autoriza nada.*
5. **Escenario 3:** "Repetir confirmación" devuelve el mismo recibo. *Mensaje: idempotencia por ID de propuesta.*
6. **Escenario 4:** "Cancelar STAY-B0000001" como `customer-a` devuelve `RESOURCE_NOT_FOUND`. *Mensaje: conocer un código no es una credencial; la respuesta no revela si existe.*
7. **Escenario 5:** activa el laboratorio, confirma y luego pulsa "Consultar recibo". *Mensaje: diferencia entre una operación fallida y una con resultado incierto; se recupera leyendo, no repitiendo.*
8. **Escenario 2:** mover la reserva a noches ocupadas devuelve `DATES_OCCUPIED` y la original queda intacta.

## 3. Arquitectura (1–2 min)

Muestra el diagrama de la [guía](demo-guide.es.md#arquitectura): navegador → app → Keycloak, SQLite en volúmenes, puertos solo en loopback. Explica en una frase el problema OIDC en contenedores: el navegador y la app ven Keycloak con nombres distintos, así que la app descubre por `keycloak:8080` pero valida el issuer del navegador `localhost:18080`, un puerto configurable porque el 8080 suele estar ocupado.

## 4. Preguntas difíciles y respuestas cortas

- **"¿Dónde está la IA?"** "El modo por defecto es determinista y lo digo en la interfaz. Hay un adaptador para un modelo local con esquema cerrado de seis campos; si falla, devuelve un error explícito y no cambia en silencio a las reglas. No he medido su calidad en este chat y no uso cifras antiguas para venderlo."
- **"¿Qué pasa si dos personas confirman la misma noche?"** "La tabla de noches tiene clave única (habitación, noche) y la confirmación revalida dentro de la transacción: una gana y la otra recibe un rechazo con recibo. Hay una comprobación concurrente que lo verifica."
- **"¿Por qué una propuesta no reserva?"** "Para no bloquear inventario por mensajes que nadie confirma. A cambio, la confirmación puede fallar si alguien ocupó la noche; por eso se revalida."
- **"¿Por qué aceptas IPs privadas en modo contenedor?"** "Detrás del port-forward de Docker la IP remota es el gateway. La frontera pasa a ser el puerto publicado solo en 127.0.0.1; Host, Origin y CSRF no cambian, y el modo es explícito."
- **"¿Qué cambiarías para producción?"** "HTTPS y cookies seguras, secretos fuera de volúmenes, aislamiento por visitante, una base con réplicas si hiciera falta, observabilidad real y una evaluación medida del modelo antes de activarlo."
- **"¿Qué parte hiciste tú?"** Responde con honestidad: diseño de los límites, decisiones y revisión, con asistencia de IA en la implementación, como dice el README. Ofrece cambiar algo pequeño en vivo, por ejemplo un texto de aclaración, y ejecutar `node --test "tests/web/*.test.mjs"`.

## 5. Cierre (20 s)

"Lo que quería demostrar no es un chat que lo sabe todo, sino un sistema que no se equivoca de forma peligrosa: pregunta cuando falta información, no deja que el texto decida permisos y siempre puede explicar qué pasó con una operación."
