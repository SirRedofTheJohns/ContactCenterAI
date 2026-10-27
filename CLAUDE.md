# CLAUDE.md

## Propósito

ContactCenterAI es un proyecto de portafolio en C#/.NET: un resort ficticio (Caribbean Horizon) donde
reservar, cambiar y cancelar pasa por identidad verificada, propuesta, confirmación explícita,
revalidación, transacción y recibo.

Objetivo actual: que una persona ajena descargue el repositorio y complete la demo en su propia
máquina sin la PC, credenciales, bases de datos, túnel ni ayuda del autor. El perfil básico no debe
requerir Meta/WhatsApp, túneles, modelos descargados ni cuentas pagadas. Los perfiles adicionales
(IA local, canales reales) se identifican como tales.

Hay dos superficies distintas que no conviene mezclar:
- **Perfil original (v0.10, `/`)**: interfaz web de asistente con IA/RAG (modo simulado o modelo local
  fijado por digest).
- **Resort (v0.12, `/resort.html`)**: catálogo, calendario y formularios web. La conversación del
  resort existe hoy solo por WhatsApp y usa interpretación determinista (reglas y regex en
  `ResortLanguage`), no un modelo. Falta un chat web del resort.

## Instrucciones base

Lee y respeta [AGENTS.md](AGENTS.md): límites, invariantes de seguridad y reglas de evidencia. No se
duplican aquí. Si este archivo y AGENTS.md chocan, gana AGENTS.md salvo indicación expresa del dueño.

## Comandos

El último estado comprobado de cada comando está en la nota de progreso más reciente de
[docs/progress/](docs/progress/) (p. ej. `portfolio-2026-10-27.md`), no aquí.

- Requisitos: PowerShell 7.2+ (Windows trae 5.1; todos los `eng/*.ps1` exigen 7.2+), el .NET SDK
  exacto de `global.json` (`rollForward: disable`), Docker con contenedores Linux y Python 3.12.
  Node.js solo para las pruebas del frontend.
- Los scripts buscan primero un SDK en `../../work/runtimes/dotnet` (layout del equipo original) y,
  si no existe, usan `dotnet` del PATH; esa ruta no es un requisito. `Verify-Demo.ps1 -DotNetExe <ruta>`
  acepta un SDK en otra carpeta. Herramientas locales opcionales en `.local/` (ignorada por git):
  `.local/dotnet/dotnet.exe`, `.local/pwsh/pwsh.exe`.
- Verificación completa: `./eng/Verify-Demo.ps1 [-DotNetExe <ruta>]` (restore con lock y auditoría
  NuGet, build Release, suites deterministas C#, evaluadores Python).
- Pruebas del frontend del resort: `node --test "tests/web/*.test.mjs"` (runner integrado de Node,
  sin npm). Son evidencia aislada de UI, no pruebas de navegador ni de login.
- Primera vez:
  - `./eng/Initialize-LocalEnvironment.ps1` (crea `deploy/local/.env`, ignorado)
  - `docker compose --env-file deploy/local/.env -f deploy/local/compose.yaml up -d keycloak`
  - `python ./eng/Simplify-Demo.py` (reescribe archivos versionados, ver abajo)
- Resort: `./eng/Start-Resort.ps1` (o `eng\Start-Resort.cmd`) → http://127.0.0.1:7452/resort.html.
  Hoy también prepara la configuración de canales y reinicia el ChannelHost.
- Demo original (RAG): `./eng/Start-Demo.ps1 -IntentProvider simulated` (o `local-llm` con modelos fijados).
- Usuarios sintéticos públicos: `customer-a`, `customer-b`, `operations-admin`, entre otros; clave `123456Aa!`.

## Datos, secretos y evidencia

- Nunca commitear `.env`, `.local/`, bases SQLite, tokens, códigos de vínculo ni logs de proveedor.
- `Verify-Demo.ps1` y varias suites reescriben archivos versionados en `docs/progress/` y `evaluation/`;
  `Simplify-Demo.py` reescribe el realm de Keycloak y `docs/progress/demo-setup-evidence.json`. Antes
  de ejecutarlos, anotar `git status`. Después, conservar las salidas nuevas fuera de los reportes
  históricos y restaurar solo los archivos que la ejecución generó.
- No sobrescribir reportes históricos ni presentar evidencia antigua como ejecutada ahora. Etiquetar
  siempre: implementado / probado ahora (fecha, commit, comando) / histórico / simulado / pendiente.
- `-FreshData` y `Preparar-Demo.cmd` archivan las bases de la demo; no usarlos sin pedirlo.
- No relajar issuer, origin, loopback, antiforgery ni validaciones para que algo arranque. No
  desactivar NuGetAudit ni suprimir avisos: corregir la dependencia.

## Forma de trabajo

- Entregas pequeñas y verticales acordadas con el dueño: comportamiento, archivos, pruebas y criterio
  de aceptación. Proponer el alcance antes de implementar. (`docs/08-backlog.md` mezcla estados
  históricos; mientras no se actualice, el alcance de cada entrega se acuerda explícitamente.)
- Actualizar contrato/aceptación antes de cambiar comportamiento (regla de AGENTS.md).
- Cada entrega informa: diff, comandos ejecutados con su resultado real, límites y pasos de revisión
  manual, y deja una nota fechada en `docs/progress/`.
- Rama por entrega. Sin commit, push ni despliegue salvo petición del dueño.
- Sin frameworks ni infraestructura nueva sin justificación ligada al objetivo de la demo.

## Documentación relevante

- [README.md](README.md) y [docs/README.md](docs/README.md) (índice)
- Resort: [docs/resort-booking-v0.12.en.md](docs/resort-booking-v0.12.en.md),
  [docs/contracts/resort-v0.12.md](docs/contracts/resort-v0.12.md),
  [docs/architecture-freeze-v0.12-resort.md](docs/architecture-freeze-v0.12-resort.md), ADR-025 en [docs/adr/](docs/adr/)
- Canales: [docs/architecture-freeze-v0.11-channels.md](docs/architecture-freeze-v0.11-channels.md), ADR-024
- Arranque: [docs/en/getting-started.md](docs/en/getting-started.md), [docs/resort-demo.en.md](docs/resort-demo.en.md)
- Evaluación: [docs/en/evaluation-and-limits.md](docs/en/evaluation-and-limits.md), [evaluation/README.md](evaluation/README.md)
- Evidencia histórica: [docs/progress/](docs/progress/)

Algunas guías contradicen el estado real (p. ej. conteos de comprobaciones y CI remoto). Verificar
contra el código o una ejecución antes de citarlas.
