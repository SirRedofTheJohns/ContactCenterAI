# Release notes — v0.13.1 (prepared, not published)

**Audience:** someone who downloads the project to try or review it. Everything in [v0.13](release-notes-v0.13.md) still applies.

## The promise

- **Main path:** the self-contained Docker demo in deterministic mode. Run `start-demo.cmd`, open http://127.0.0.1:7452 and book, change and cancel a fictional stay from the chat. Only Docker is needed: no model, no external account.
- **Checked platform:** Windows 11 with Docker Desktop. `demo.sh` for macOS/Linux is included but not validated.

## Local AI: read this first

- **A new installation cannot currently obtain the pinned model.** The project pins one exact build of `qwen3-vl` (digest `901cae73216286ea8c5aba8b46d307ff7188f737285ec500c795a12f05225d28`). As checked on 2026-10-27, the registry's `qwen3-vl:latest` points to a different build and does not serve the pinned one by digest. Pulling `latest` does not solve this: the app rejects other builds with `LOCAL_MODEL_PIN_MISMATCH`.
- **It was run only on the author's PC**, where the pinned build was already installed. The path was Docker app → `host.docker.internal` → Ollama on the host loopback.
- **Results on a 22-case development sample (ES/EN):** 21/22 before one prompt adjustment and 22/22 after it. The deterministic rules gave 19/22. The adjustment added to the prompt an example derived from one of the sample's cases, so these figures do not show generalization, production quality or overall accuracy.
- **Latency:** 3.5–6.8 s per message on CPU, close to the eight-second deadline.
- **Where the model is present,** `start-demo.cmd ai` turns it on. Without it, the chat reports an explicit error and never falls back to the rules.

## What changed since v0.13

- Optional local AI from the Docker demo (`start-demo.cmd ai` / `./demo.sh start-ai`), with a restricted model endpoint.
- Startup warm-up of the pinned model so the first message is not lost to a cold start.
- One prompt clarification for vague booking requests.
- Documentation of the above, including these limits.

## Not verified

- macOS, Linux, ARM, and other browsers beyond headless Chrome and Edge.
- Remote CI: runs do not start while the GitHub account is locked for billing.
- Screen reader and 200 % zoom.

## Licence

**Not chosen yet; this is a pending decision for the owner.** Until a licence is added, default copyright applies.

## Files that would be published

| File | Purpose |
|---|---|
| `ContactCenterAI-demo-v0.13.1-<commit>.zip` | The package: `git archive` of the release commit plus `START-HERE.txt`. It contains only tracked files, so no `.local`, `.env`, databases or caches |
| `SHA256SUMS` | Checksums of the packages in the release folder |
| This file | Release notes |

To check a download, run `certutil -hashfile <zip> SHA256` (Windows) or `shasum -a 256 <zip>` (macOS/Linux) and compare with `SHA256SUMS`.
