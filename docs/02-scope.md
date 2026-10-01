# Alcance y supuestos v0.1

Refinamiento B05: [ADR-015](adr/015-source-simulator-demo.md) y [freeze v0.3](architecture-freeze-v0.3-source.md) separan proceso/DB fuente del middleware. La UI consulta reservas propias/elegibilidad. Confirmación/cancelación del producto e IA permanecen pendientes.

Nota de presentación: [ADR-014](adr/014-simple-local-demo.md) y [freeze v0.2](architecture-freeze-v0.2-demo.md) agregan una UI mínima, OIDC real y SQLite persistente en HTTP loopback con cuentas ficticias de clave común. Simplifican el arranque; no cambian reglas de reservas ni declaran implementado el objetivo completo de este documento.

## Incluido

- Chat web textual para cliente ES/EN y una vista mínima de empleado; sin transcripción.
- Un club ficticio, un tenant lógico, cuentas sintéticas de cliente/empleado/admin.
- FAQ pública y knowledge assistant interno con citas, ACL, versiones, aprobación, expiración, reindexación y feedback.
- Lectura de reservas propias o de una conversación asignada. Solo cancelación sin penalidad como mutación de negocio.
- Preview de cancelación, confirmación explícita y ejecutor durable con reconciliación.
- Resumen de interacción textual basado en hechos, categoría y disposition determinista.
- Handoff mock con solicitud, aceptación y fallo; control de propiedad de conversación.
- Puertos de LLM, reservas, identidad y contact center; simuladores de fuentes independientes.
- Diseño CI/CD, seguridad, métricas, alertas, runbooks y plan de evals Python.

## Fuera del MVP

Voz, SMS/WhatsApp reales, envíos proactivos, consentimiento multicanal, pagos/reembolsos reales, contratos empresariales, CRM real, overrides de política, multi-tenancy físico, multi-agent, Kubernetes, multi-provider failover, entrenamiento/fine-tuning y Agent Assist en streaming. Angular implementará solo las vistas necesarias después del flujo API. No se afirma cumplimiento PCI DSS; no se capturan PAN/CVV ni se procesa dinero.

## Extensiones planificadas

| Extensión | Dependencia previa | Motivo de postergación |
|---|---|---|
| Genesys real | Tenant, región, licencias, Architect y validación de wire protocol | No puede probarse con un mock |
| Agent Assist en tiempo real | Acceso a eventos y contexto autorizado de interacción | Añade latencia, UX y permisos específicos |
| Voz y transcripción | Canal, STT/TTS, privacidad y presupuesto | Distintos requisitos de tiempo/consentimiento |
| SMS/WhatsApp | Ledger de consentimiento y revocación, proveedor aprobado | La autorización transaccional no sustituye consentimiento de comunicación |
| Penalidades/pagos | Contrato de negocio, provider idempotente y revisión de seguridad | Aumenta riesgo y scope financiero |
| Multi-tenancy | Modelo de aislamiento y pruebas independientes | TenantId en el diseño no demuestra aislamiento productivo |

## Supuestos aceptados para la demo

| ID | Supuesto | Si cambia |
|---|---|---|
| A01 | Fuente simulada permite commandId, rowVersion y consulta de resultado | Suspender mutaciones automatizadas si no se puede reconciliar con certeza |
| A02 | Cancelación gratis si faltan al menos 72 horas; estado Confirmed; sin excepción | Nueva política versionada y ADR de impacto |
| A03 | Instantes UTC; zona IANA de propiedad para mostrar fechas | Actualizar fixtures de DST y formatos; regla siempre sobre instantes |
| A04 | Login OIDC local con cuentas sintéticas; no autenticar por chat | Validar step-up/claim mapping antes de identidad externa |
| A05 | Documentos Markdown curados, tamaños acotados, sin archivos arbitrarios | Agregar sandbox de parsers y malware scanning si se admiten PDFs/uploads |
| A06 | Un modelo y un embedding se fijan tras comparación offline | Recalibrar retrieval, costos y evals al cambiarlos |
| A07 | Adapter mock es siempre disponible sin credenciales externas | Acceso Genesys solo para M6 |
| A08 | Metas de carga sobre perfil de 4 vCPU / 16 GB y 10 conversaciones concurrentes | Rehacer mediciones al cambiar hardware/concurrencia |

## Política ficticia CP-001 v1

`eligible = authenticated AND resourceAuthorized AND reservation.status == Confirmed AND checkInUtc - nowUtc >= 72h`. Penalidad igual a cero. Exactamente 72 h es elegible; 71 h 59 m 59 s no lo es. Reservas ya canceladas no reciben una segunda cancelación. Se revalida en la fuente al ejecutar. La versión de política es inmutable y activa en SQL/configuración gobernada; RAG contiene su explicación equivalente en ES/EN.

Una reserva fuera de política ofrece handoff; no se presenta un supuesto supervisor capaz de hacer override. Nunca usar la hora del navegador para elegibilidad.

