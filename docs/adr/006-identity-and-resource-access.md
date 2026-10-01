# ADR-006 — Identidad confiable y permisos de recurso

Estado: Accepted — local MVP v0.1. Fecha: 2026-09-30.

**Contexto.** Rol Agent o memberId escrito en un chat no autoriza acceso a cualquier cuenta. Tokens de máquina y miembro son identidades distintas.

**Decisión.** IdP OIDC sintético; BFF con cookie/CSRF y Authorization Code + PKCE. Subject→MemberRef servidor. RBAC junto a tenant/ownership/asignación y document ACL en cada operación. Editor/reviewer diferentes por subject. No override supervisor ni acceso universal OperationsAdmin.

**Alternativas.** Login por datos personales/preguntas del bot: suplantación y fuga. JWT emitido ad hoc sin IdP: burden de gestión no necesario. RBAC solamente: cross-account fácil. Tokens en localStorage/prompt: exposición innecesaria.

**Consecuencias.** IdP y mapping añaden setup; probar expiración/revocación y autorización del recurso fuente. Revisar por SSO corporativo/step-up, delegated access o nuevo canal; no aceptar claims externos sin validación.

Referencias: FR03–04/20; AC04–06/21–22/28; matriz de acceso.

