# Discussing the project in an interview

[Español](../es/portafolio.md)

Lead with a working example: “This is a bilingual C# assistant for fictional reservations. I wanted to show the workflow around AI: identity, permissions, explicit confirmation and recovery when an external service times out.”

| Vacancy skill | Evidence here | Boundary to explain |
|---|---|---|
| C#/.NET, REST and integration | Layered API, workers and separate HTTP source | Portfolio-scale implementation |
| Agents, prompting and tool calling | Closed intent schema and server-dispatched cases | Model never grants permissions or performs direct writes |
| RAG and embeddings | Real local vectors, evidence selection, publication/access checks | Small exact-search corpus; eight recorded quality errors |
| SQL and reliable operations | Real SQLite workflow, SQL Server schema/adapter target, durable receipts | Enterprise encrypted SQL runtime still open |
| Security and RBAC | Real OIDC, ownership and assigned-agent rules | Synthetic local environment |
| Genesys/contact-center integration | Provider-neutral handoff and fault-aware mock | No live tenant, voice, Agent Assist or post-call workflow |
| Testing, Git and CI/CD | Safety suites, evaluation reports, pinned workflow | Remote CI status must be checked after publication |
| Bilingual communication | ES/EN behavior and documentation | Independent language review pending |

Be ready to explain four choices: why confirmation is a UI action, why the reservation source owns the final result, why a timeout becomes Unknown, and why a WhatsApp sender or Telegram ID is not an authenticated member.

Useful files to open: [cancellation workflow](../../src/ContactCenterAI.Application/CancellationWorkflow.cs), [resource access](../../src/ContactCenterAI.Domain/ResourceAccess.cs), [source HTTP boundary](../../src/ContactCenterAI.Infrastructure/HttpReservationSource.cs), [channel architecture](../integrations/channels.en.md), and [evaluation limits](evaluation-and-limits.md).

Avoid claiming a complete Genesys product or production readiness. Explain what you built, what was tested and what you would verify next in a real tenant. AI-assisted development is part of this project's process; understand the code and tradeoffs before presenting it as your work.
