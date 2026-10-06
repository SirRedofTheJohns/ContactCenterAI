# Probar el resort

[English](resort-demo.en.md) · [Arquitectura](resort-booking-v0.12.es.md) · [Pruebas y límites](progress/resort-v0.12.md)

Caribbean Horizon es un hotel inventado con una habitación Estándar, una Deluxe y una Suite. Los precios son 120, 180 y 280 USD por noche. La Suite admite cuatro huéspedes e incluye jacuzzi privado. No hay pagos ni conexión con un hotel real.

## Arranque

Con la preparación inicial de la demo terminada, abre Docker Desktop y ejecuta `eng/Start-Resort.cmd` en Windows, o `pwsh -File eng/Start-Resort.ps1`. El lanzador conserva las cuentas y los datos, inicia el servicio de acceso existente, abre la web y deja los servicios en segundo plano. Requiere .NET fijado en `global.json` y PowerShell 7. La [preparación general](es/primeros-pasos.md) explica esos requisitos para otra computadora.

Abre **http://127.0.0.1:7452/resort.html**. El calendario y los precios se pueden consultar sin entrar. Inicia sesión como `customer-a` o `customer-b`, clave `123456Aa!`. Son cuentas ficticias diferentes y cada una tiene sus propias reservas.

Para administrar mantenimiento y tarifas, usa `operations-admin` con la misma clave de demo. Ese rol no obtiene acceso a reservas privadas. Un bloqueo solo puede ocupar noches libres; no desplaza una reserva.

WhatsApp requiere la [configuración oficial de prueba](integrations/setup.es.md). El lanzador reactiva el host, pero no publica la web ni cambia permisos de Meta. El túnel expone **solo el host de canales (7454)**. Si una PC reiniciada recibe una URL nueva de túnel, se debe actualizar el callback de Meta y volver a verificarlo. No pegues una URL de la web, Keycloak o administración en ese túnel.

## Vincular WhatsApp

1. Entra en la página y pulsa **Generar código**.
2. Copia el comando `Vincular …` y envíalo al chat de prueba.
3. Vuelve a la página, pulsa **Actualizar estado** y **Confirmar vínculo**.

El código dura cinco minutos y se usa una vez. El vínculo pertenece a esa sesión de cliente: dura como máximo los quince minutos de la sesión, y deja de autorizar al salir. Puedes desvincularlo antes. Si vence, entra y vincula otra vez; el bot no deduce tu identidad por tu teléfono ni pide tu contraseña por chat.

## Recorrido de WhatsApp

Las fechas de ejemplo son de octubre de 2026. En otro momento elige fechas futuras que estén dentro de los próximos 90 días.

| Envía | Qué comprobar |
|---|---|
| `Qué incluye la Suite con jacuzzi` | Amenidades, capacidad y tarifa obtenidas del catálogo |
| `Dame las fechas disponibles para reservar` | Hasta cinco alternativas por categoría, para dos noches, consultadas en el inventario |
| `Fechas disponibles Suite 2026-10-25 a 2026-10-27 para 2 personas` | Opciones desde esa fecha, con duración de dos noches; no una garantía de stock |
| `Reservar Suite 2026-10-25 a 2026-10-27 para 2 personas` | Propuesta de 560 USD ficticios; todavía no hay reserva |
| `Confirmar <identificador de la propuesta>` | Reserva confirmada y código `STAY-…`; usa el identificador completo que devuelva el bot |
| `Mis reservas` | Solo las estadías de la cuenta vinculada |
| `Cambiar STAY-… Suite 2026-10-28 a 2026-10-30 para 2 personas` | Otra propuesta; las fechas anteriores siguen intactas hasta confirmar |
| `Cancelar STAY-…` | Propuesta de cancelación; no elimina la reserva al pedirla |
| `que sea en español`, `/es` o `/en` | Cambio de idioma persistente |
| `/human` | Cola humana simulada; el bot queda en pausa |

Confirma cada cambio o cancelación con el comando completo de su **nueva** propuesta. Un «sí» solo no ejecuta nada. La página también permite crear, cambiar y cancelar con un botón de confirmación.

La búsqueda propone hasta cinco estadías por categoría dentro de los siguientes treinta posibles días de entrada. Por defecto usa dos huéspedes y dos noches. Para una reserva, especifica categoría, fechas completas y huéspedes; el resumen de la propuesta muestra los valores que se ejecutarían. Fechas como «mañana» o «del 20 al 22» sin mes/año requieren aclaración. Esta es una demo de intención acotada, no conversación natural ilimitada.

## Reglas que puedes demostrar

- Una misma habitación debe estar libre todas las noches. La fecha de salida no ocupa esa noche.
- Una propuesta dura cinco minutos y no retiene la habitación. Se vuelven a comprobar disponibilidad, precio y estado al confirmar.
- Cambiar o cancelar sin penalidad requiere al menos 72 horas antes de la **llegada actual**. Mover fechas no evita esa regla.
- Si el destino está ocupado o en mantenimiento, el cambio falla y conserva la reserva anterior.
- Repetir una confirmación devuelve el mismo recibo y no duplica la reserva.
- Si no se puede comprobar el resultado de una solicitud, consulta **Mis reservas** antes de proponer otra. Una entrega incierta de WhatsApp no deshace una operación completada.

La demo original usa códigos `RES-…`; el resort nuevo usa `STAY-…` y un almacén separado. Conservamos sus datos e informes, sin combinar ambos inventarios. Telegram sigue con su adapter público; no se activó un bot real ni se agregaron reservas privadas a ese canal.

Para un video de portafolio: muestra la Suite y su tarifa, una fecha ocupada, una alternativa, la propuesta de 560 USD, la confirmación, el calendario actualizado, un cambio y una cancelación. Explica qué es real —transporte WhatsApp, login, persistencia y reglas— y qué es simulado —hotel, precios, intención de este perfil y cola humana—. No publiques tokens, códigos de vínculo, teléfonos ni logs privados.
