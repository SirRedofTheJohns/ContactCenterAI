# ContactCenterAI

[English](README.md) · [Preparación](docs/es/primeros-pasos.md) · [Cómo funciona](docs/es/guia-ingenieria.md) · [WhatsApp + Telegram](docs/integrations/channels.es.md)

Una demo pequeña de atención al cliente hecha en **C# / .NET 10**. Responde preguntas en español e inglés, consulta reservas ficticias, prepara una cancelación para que el cliente la revise y transfiere la conversación a una cola humana simulada.

Lo interesante está alrededor del modelo: el servidor comprueba los permisos, pide confirmación explícita, registra la operación y espera el comprobante del servicio de reservas. El modelo no puede cancelar una reserva por su cuenta.

![Demo local: reservas, cita aprobada y respuesta fuera de alcance](docs/progress/screenshots/final-demo-v0.10.png)

## Recorrido de cinco minutos

1. Pregunta a qué hora abre la piscina y abre el documento citado.
2. Entra con una cuenta ficticia y consulta sus reservas.
3. Solicita una cancelación. La reserva sigue igual hasta pulsar Confirmar.
4. Muestra una respuesta perdida: se comprueba el comando original y no se crea otra cancelación.
5. Solicita un humano. El bot queda en pausa y solo el agente asignado puede abrir el caso.

[Guion en español](DEMO.md) · [English demo script](docs/en/demo.md)

## Qué funciona hoy

| Área | Demo actual |
|---|---|
| Backend | ASP.NET Core y capas Application / Domain / Infrastructure; Python para evaluación offline |
| Identidad | Login real con Keycloak, Authorization Code + PKCE, sesiones persistentes, roles y permisos por recurso |
| Reservas | Servicio HTTP separado con otra base SQLite; inventario ficticio |
| Operaciones confiables | Propuestas que vencen, confirmación explícita, comandos persistentes, leases, comprobantes y reconciliación |
| IA local | Qwen para intención, BGE-M3 para embeddings y selección cerrada de evidencia; sin API pagada |
| Conocimiento | 40 documentos lógicos / 80 variantes ES/EN, revisión, permisos, vencimiento y citas verificadas |
| Atención humana | Interfaz independiente del proveedor y mock local; IDs y épocas rechazan aceptaciones viejas |
| Operación | Panel local sencillo, trazas sanitizadas y ensayo registrado de copia/restauración |
| Entrega | Dependencias fijadas, GitHub Actions y 249 comprobaciones deterministas en la base v0.10 |

WhatsApp y Telegram son la siguiente extensión. Su [arquitectura, contratos, riesgos y condiciones](docs/integrations/channels.es.md) se documentan antes del código. Validar el proveedor real es una comprobación aparte; un mock no demuestra esa conexión.

## Cómo leer los resultados

La [guía de evaluación](docs/es/evaluacion-y-limites.md) conserva los errores. Hay 300 respuestas originales de retrieval local. Una corrección puntual combina 13 comprobaciones actuales de alcance con 287 respuestas cuyo recorrido no cambió: 97% y 98% de acierto exacto en dos conjuntos conocidos, con abstención en los 60 casos fuera de alcance. **Quedan ocho errores de calidad.** No es una nueva corrida de 300 consultas, prueba de carga ni revisión independiente.

La demo usa SQLite y JavaScript sencillo. SQL Server, Qdrant, Angular y un tenant real de Genesys Cloud son objetivos empresariales pendientes. La conexión cifrada del producto a SQL sigue bloqueada en el Windows original. No se presenta como listo para producción.

## Por dónde empezar

- [Prepararlo en otra computadora](docs/es/primeros-pasos.md)
- [Entender piezas y flujos](docs/es/guia-ingenieria.md)
- [Relacionarlo con una vacante de AI Agent Developer](docs/es/portafolio.md)
- [Diseño de mensajería](docs/integrations/channels.es.md)
- [Requisitos, decisiones e historial](docs/README.md)
- [Avisos de terceros](THIRD_PARTY_NOTICES.md)

Todo es ficticio: hotel, cuentas, políticas y reservas. Los usuarios comparten `123456Aa!`; las claves de servicios se generan localmente y no se suben a Git. Tampoco se incluyen modelos, bases, copias ni logs.

Es un proyecto de aprendizaje y portafolio desarrollado con herramientas asistidas por IA. Las decisiones, pruebas y limitaciones están explicadas para poder reproducirlas y discutirlas en una entrevista.
