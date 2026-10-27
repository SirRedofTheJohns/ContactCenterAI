# Release notes — v0.13 (local package, not published)

**Audience:** someone who downloads the project to try or review it.

## What you get

- A resort booking demo you can run with Docker alone: `start-demo.cmd` (Windows) or `./demo.sh start`, then http://127.0.0.1:7452.
- A web chat that answers about rooms, prices, availability and policy, and prepares bookings, changes and cancellations as proposals you confirm explicitly.
- Six guided scenarios on the page: complete booking, rejected change, repeated confirmation, another customer's booking, recovered result and ambiguous or out-of-scope requests.
- A collapsed technical panel showing what the server decided, and why, without secrets.

## Requirements

Docker Desktop or Docker Engine with Compose v2; Internet access the first time (images and NuGet packages); free ports 7452 and 18080 on `127.0.0.1` (18080 can be changed with `CCAI_DEMO_IDP_PORT`). Tested host: Windows 11 with Docker Desktop. See the progress note for what was executed. Other systems are expected to work but are not verified.

## Modes

Deterministic interpretation by default. The optional local AI interpreter needs a source checkout and the pinned model; it is not part of the package and its inference was not run for this release.

## Known limits

- Local demo only: HTTP on loopback, Keycloak in development mode, shared synthetic accounts.
- Seed bookings are relative to the day the demo was first created. `reset-demo` recreates them.
- The page always shows text in the selected language, but chat answers keep the language they were sent in.
- No licence has been chosen yet; default copyright applies until the owner adds one.
- Remote CI has no runs for this version (account billing lock).

## Verify the package

The package ships with a `SHA256SUMS` file. On Windows run `certutil -hashfile <zip> SHA256`; on macOS/Linux run `shasum -a 256 <zip>`. Compare the result with the file.
