# Release notes — v0.13.1 (local package, not published)

**Audience:** someone who downloads the project to try or review it. Everything in [v0.13](release-notes-v0.13.md) still applies.

## What changed

- **Optional local AI from the Docker demo.** `start-demo.cmd ai` (Windows) or `./demo.sh start-ai` makes the chat interpret messages with the project's pinned local model (`qwen3-vl`, digest `901cae73…`) through Ollama on your own computer. The app reaches Ollama through `host.docker.internal`, and Ollama keeps listening only on `127.0.0.1`.
- **Warm-up.** The model is loaded at startup, so the first message stays within the eight-second answer deadline.
- **No silent fallback.** Without the exact model, or if it is unreachable or slow, the chat says so with an explicit code and prepares nothing.

## Requirements for local AI

- Ollama listening on `127.0.0.1:11434`, with `qwen3-vl:latest` at digest `901cae73216286ea8c5aba8b46d307ff7188f737285ec500c795a12f05225d28` already installed (8.8B, Q4_K_M, 6.14 GB).
- About 6 GB of free RAM; it runs on CPU.
- Docker Desktop. A Linux Docker engine is untested.

## Known limits

- **The pinned model may not be downloadable any more.** The registry's `qwen3-vl:latest` now resolves to a different build. A fresh download is rejected with `LOCAL_MODEL_PIN_MISMATCH`.
- **Thin latency margin.** On the test computer each message took 3.5–6.8 s, close to the deadline; slower hardware can time out.
- **No quality guarantee.** Quality was checked only on a 22-case development sample (22/22 after one prompt round, 21/22 before; the rules scored 19/22). That is not a holdout.
- **The basic demo is unchanged.** It stays deterministic and needs only Docker.

## Verify the package

`SHA256SUMS` lists this package and any earlier one in the same folder. Use `certutil -hashfile <zip> SHA256` on Windows or `shasum -a 256 <zip>` on macOS/Linux.
