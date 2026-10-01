# Ejecutar la demo

[English](../en/getting-started.md)

## Requisitos

Windows, PowerShell 7.2+, Python 3.12, SDK .NET indicado en `global.json` (10.0.401), Docker Desktop con contenedores Linux y Git. Preparar paquetes e imágenes necesita Internet. La ruta inicial selecciona explícitamente **intención y búsqueda por tema simuladas**: no necesita modelos ni una cuenta de IA pagada. Sí usa Keycloak real local y dos bases SQLite reales.

En el equipo original basta abrir Docker Desktop y ejecutar `Abrir-Demo.cmd`. Un clon necesita la preparación de abajo. La ruta está documentada; la evidencia histórica se midió en el equipo original, no en todas las instalaciones Windows.

## Primera ejecución en otro equipo

Abre PowerShell 7 en la raíz del repositorio. No copies el `.env` ni las bases de otra persona.

```powershell
./eng/Initialize-LocalEnvironment.ps1
docker compose --env-file deploy/local/.env -f deploy/local/compose.yaml up -d keycloak
```

Se inicia **solo Keycloak**. Conserva `REVIEW_REQUIRED` para la licencia SQL salvo que quieras ejecutar SQL Server Developer por separado y hayas revisado sus términos. Espera a que responda el discovery: `http://localhost:8080/realms/contactcenterai-local/.well-known/openid-configuration`.

```powershell
python ./eng/Simplify-Demo.py
./eng/Start-Demo.ps1 -IntentProvider simulated
```

La herramienta prepara cuentas ficticias y callback local exacto; el producto sigue usando login de navegador. El lanzador compila y abre `http://127.0.0.1:7452`. Si discovery aún no responde, espera al contenedor y repite el paso siguiente una vez; no hace falta repetir diagnósticos de certificados.

Usuarios: `customer-a`, `customer-b`, `agent-assigned`, `agent-unassigned`, `operations-admin`, `knowledge-editor`, `knowledge-reviewer`. Clave: `123456Aa!`. Son fixtures públicos ficticios. Las claves de servicios son otras, se generan localmente y quedan en archivos ignorados.

## IA local

El perfil real necesita los digests exactos de Qwen y BGE-M3 registrados en [intención](../../src/ContactCenterAI.Infrastructure/OllamaIntentProvider.cs) y [embeddings](../../src/ContactCenterAI.Infrastructure/OllamaEmbeddingProvider.cs). Consulta [IA local](../progress/demo-v0.6.md) y [decisión de retrieval](../adr/020-local-semantic-retrieval-and-operational-evidence.md) antes de descargar. Los lanzadores comprueban archivos y pins; no son instaladores automáticos de Ollama y modelos para otra computadora.

En el equipo preparado, `Activar-IA-Local.cmd` selecciona IA real local y `Usar-Simulador.cmd` selecciona simulación. No existe cambio silencioso a la nube ni cobro por API de inferencia. Los dos modos no tienen la misma calidad. Mantén puertos en loopback y desactiva funciones cloud si instalas Ollama manualmente.

## Comprobación y recuperación

```powershell
./eng/Verify-Demo.ps1
```

Compila dependencias fijadas, ejecuta suites deterministas y evalúa fixtures simulados. No contacta Meta, Telegram, Genesys ni modelos pagados. GitHub Actions ejecuta el mismo comando en otro entorno limpio.

`Guardar-Copia.cmd` crea y comprueba copias sin reemplazar las bases actuales. `Preparar-Demo.cmd` archiva las anteriores y crea fechas ficticias nuevas; úsalo a propósito porque cambia el inventario de presentación. Ninguno hace falta para arreglar una sesión vencida.

| Síntoma | Paso siguiente |
|---|---|
| SDK no coincide | Instalar la versión de `global.json`; conservar locks |
| Login no disponible | Abrir Docker y comprobar contenedor Keycloak y discovery |
| Sesión vencida | Entrar otra vez; dura 15 minutos |
| Falta modelo o pin | Usar simulación o completar preparación de IA por separado |
| Propuesta rechazada | Revisar estado fuente, versión, elegibilidad y vencimiento |
| Error SQL/TLS | Pendiente empresarial; no bloquea la demo SQLite |

La mensajería tiene sus propios [pasos y condiciones](../integrations/channels.es.md). No expongas la demo web, Keycloak, bases ni puertos de modelos para habilitar un webhook.
