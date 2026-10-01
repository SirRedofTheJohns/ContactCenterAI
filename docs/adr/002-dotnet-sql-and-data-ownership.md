# ADR-002 — Backend .NET y autoridad SQL/fuente

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** La oferta exige C#/.NET, SQL e integraciones. Un dato fuente no debe convertirse en registro autoritativo del middleware.

**Decisión.** .NET 10 LTS / ASP.NET Core; EF Core y SQL Server para estado/knowledge/audit/inbox/outbox. Source DB separada con HTTP adapter. Python solo offline eval/data; Angular para UI mínima posterior. SDK/patch/imagen/edición se fijan tras spike de compatibilidad/licencia.

**Alternativas.** .NET 8 tiene horizonte de soporte corto para un proyecto nuevo al 2026-09-30. PostgreSQL es válido, pero SQL Server favorece la demostración del stack empresarial elegido; no se afirma que GBS use ese motor. SQLite simplifica setup pero no reproduce igual concurrency/constraints del objetivo. DB compartida fuente/API elimina el contrato y oculta fallos distribuidos.

**Consecuencias.** Mayor requisito de RAM/containers y licencia local por validar; se conserva una frontera real fuente/middleware. Revisar por restricción de hosting/hardware o incompatibilidad verificada. [Soporte oficial .NET](https://dotnet.microsoft.com/en-us/platform/support/policy).

Referencias: FR05/08/18; NFR12–13; modelo conceptual y B01/B05.

