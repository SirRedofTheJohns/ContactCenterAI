# Mostrar ContactCenterAI en cinco minutos

Abre [la demo local](http://127.0.0.1:7452). **Abre Docker Desktop y haz doble clic en Abrir-Demo.cmd**. Conserva tus datos y la selección de IA. El lanzador reconoce el servicio de acceso y puede iniciar únicamente el contenedor Keycloak existente del proyecto; no crea contenedores ni descarga imágenes. Si Docker impide el acceso, inicia el grupo **contactcenterai-local** desde su pantalla. En este equipo ya están preparados Keycloak, el modelo de intención y el modelo bilingüe de búsqueda.

| Cuenta ficticia | Para qué sirve | Clave |
|---|---|---|
| customer-a | Conversación y reservas propias RES-001 / RES-002 | 123456Aa! |
| customer-b | Otra identidad: RES-003 / RES-004 | 123456Aa! |
| agent-assigned | Contexto de los casos transferidos a esta cuenta | 123456Aa! |
| agent-unassigned | Mostrar que el rol sin asignación no abre otro caso | 123456Aa! |
| operations-admin | Panel de actividad, índice y operaciones pendientes | 123456Aa! |
| knowledge-editor / knowledge-reviewer | Roles separados para revisión y publicación mediante API | 123456Aa! |

1. Como customer-a, pregunta **¿A qué hora puedo nadar en la piscina?**. La búsqueda encuentra el documento aprobado y muestra la cita; pulsa la cita para abrirla.
2. Pregunta **¿Qué establece CP-001?** y después pulsa **Mis reservas**.
3. Escribe **Cancelar RES-001**. Revisa la propuesta. La reserva sigue igual hasta pulsar **Confirmar cancelación ficticia**. **Conservar reserva** rechaza la propuesta.
4. El resultado solo dice cancelación confirmada después del comprobante de la fuente. Un resultado incierto se comprueba sin crear un comando nuevo.
5. Pulsa **Solicitar humano** y cambia a **agent-assigned** para mostrar el contexto. El contact center es un mock y el bot queda en pausa tras su aceptación.

Para inglés, elige English antes de crear una conversación nueva. Puedes preguntar “When can I swim in the pool?” o “What is the cancellation policy?”. “Sí/yes” en chat no confirma una cancelación.

Para el panel operativo, entra como **operations-admin**. Muestra documentos aprobados/indexados, cancelaciones confirmadas, resultados inciertos, revisión humana y actividad sin texto de conversaciones. Las otras cuentas no pueden consultar ese panel.

Para cambiar de cuenta pulsa **Salir**. Si Keycloak muestra el usuario anterior, usa la flecha junto a su nombre para elegir otro. La sesión dura quince minutos; si vence, vuelve a entrar. No se requieren certificados nuevos.

**RES-003 de B ya fue cancelada en una prueba anterior. RES-001 de A se conservó.** Las fechas envejecen: [Preparar-Demo.cmd](Preparar-Demo.cmd) archiva las bases anteriores y prepara otras con fechas nuevas. No se ejecutó sobre tu evidencia actual. [Guardar-Copia.cmd](Guardar-Copia.cmd) guarda dos snapshots y comprueba restauración en una carpeta aparte; nunca sustituye las bases activas.

[Usar-Simulador.cmd](Usar-Simulador.cmd) selecciona expresamente intención/lookup simulados; [Activar-IA-Local.cmd](Activar-IA-Local.cmd) vuelve a los modelos aprobados. Las respuestas históricas conservan su etiqueta de proveedor. Los lanzadores no cambian claves ni descargan modelos automáticamente.

**Qué mostrar como habilidad:** separación entre IA y autoridad de negocio, autenticación real, permisos por recurso, contratos, SQL, idempotencia, recuperación, evidencia versionada, evaluación bilingüe y observabilidad. **Qué explicar con honestidad:** la fuente y el contact center son ficticios; SQLite es la excepción local y la integración empresarial SQL/Genesys tiene gates propios. La UI es HTML/CSS/JS. Los informes incluyen errores y límites de calidad.

Lee [el estado completo](docs/progress/demo-v0.10.md) y [los resultados](evaluation/README.md) antes de una entrevista. El ZIP contiene proyecto/documentación, no los pesos ni tus bases/credenciales locales.


La versión actual es **v0.10**. En esta entrega se conservaron las reservas y no se confirmó otra cancelación. Las comprobaciones anteriores de confirmación, recovery, A/B y panel siguen disponibles. Para una entrevista, revisa la propuesta sin confirmarla si quieres conservar RES-001.
