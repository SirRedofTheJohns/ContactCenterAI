# Run the demo

[Español](../es/primeros-pasos.md)

## Requirements

Windows, PowerShell 7.2+, Python 3.12, the .NET SDK version in `global.json` (10.0.401), Docker Desktop with Linux containers, and Git. Package and image setup needs Internet access. This first route uses the **explicit simulated intent/topic providers**, so it does not need model weights or a paid AI account. It still uses real local Keycloak and two real SQLite databases.

The original computer is already prepared: open Docker Desktop and run `Abrir-Demo.cmd`. A cloned repository needs the one-time preparation below. That clean-computer route is documented; historical evidence was measured on the original host, not on every Windows installation.

## First run on a new computer

Open PowerShell 7 in the repository root. Do not copy someone else's `.env` or databases.

```powershell
./eng/Initialize-LocalEnvironment.ps1
docker compose --env-file deploy/local/.env -f deploy/local/compose.yaml up -d keycloak
```

This starts **only Keycloak**. Leave SQL license acceptance as `REVIEW_REQUIRED` unless you separately intend to run SQL Server Developer and have reviewed its terms. Wait for Keycloak's realm discovery endpoint before the next step: `http://localhost:8080/realms/contactcenterai-local/.well-known/openid-configuration`.

```powershell
python ./eng/Simplify-Demo.py
./eng/Start-Demo.ps1 -IntentProvider simulated
```

The provisioning tool prepares the fictional accounts and exact local callback. It does not replace browser login with a password API. The launcher builds the demo and opens `http://127.0.0.1:7452`. If discovery is not ready, wait for the container to finish and try the next step once; there is no reason to repeat certificate diagnostics.

Users: `customer-a`, `customer-b`, `agent-assigned`, `agent-unassigned`, `operations-admin`, `knowledge-editor`, `knowledge-reviewer`. Password: `123456Aa!`. These are public synthetic fixtures. Generated service passwords are different and remain in ignored files.

## Local AI

The real AI profile requires the exact Qwen and BGE-M3 digests recorded in [the provider code](../../src/ContactCenterAI.Infrastructure/OllamaIntentProvider.cs) and [embedding code](../../src/ContactCenterAI.Infrastructure/OllamaEmbeddingProvider.cs). Read the [local AI runbook](../progress/demo-v0.6.md) and [retrieval decision](../adr/020-local-semantic-retrieval-and-operational-evidence.md) before downloading models. The prepared launchers check local runtime files and pins; they are not an automatic Ollama/model installer for another computer.

On the prepared host, `Activar-IA-Local.cmd` selects the real local profile. `Usar-Simulador.cmd` selects the simulator explicitly. There is no silent switch to a cloud provider, no API-token charge, and no claim that the two modes have equal answer quality. Keep local inference endpoints on loopback and disable cloud features in any manually installed Ollama runtime.

## Verification and recovery

```powershell
./eng/Verify-Demo.ps1
```

This builds locked dependencies, runs deterministic suites and evaluates the fixed simulated fixtures; it does not contact Meta, Telegram, Genesys or a paid model. GitHub Actions runs that command in a separate clean environment.

`Guardar-Copia.cmd` creates and checks copies without replacing the current databases. `Preparar-Demo.cmd` archives the previous demo databases and creates fresh synthetic dates; use it deliberately because it changes the presentation inventory. Do not run either just to fix an expired login.

## Troubleshooting

| Symptom | Next step |
|---|---|
| SDK mismatch | Install the exact version in `global.json`; do not silently change the lock |
| Login service unavailable | Open Docker and check the project Keycloak container and discovery URL |
| Session expired | Sign in again; sessions last 15 minutes |
| Model pin or file missing | Use simulated mode, or complete the separate local model setup |
| Reservation offer rejected | Refresh source/version; inspect eligibility and offer expiry |
| SQL/TLS error | This is an open enterprise gate, not a prerequisite for the SQLite demo |

The messaging extension has its own [setup and release gates](../integrations/channels.en.md). Never tunnel the web demo, Keycloak, database or model ports to make a webhook work.
