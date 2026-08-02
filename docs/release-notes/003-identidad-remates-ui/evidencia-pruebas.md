# Evidencia de pruebas — Feature 003: Remates de Identidad Central

**Rama**: `003-identidad-remates-ui` · **Fecha**: 2026-08-01

## Resumen

Implementación completa de las 6 user stories (T001–T035) más la deuda de
tests de integración del feature 002 (T022, T036–T046). Verificación
automatizada en verde; el recorrido manual de UI (quickstart con Playwright)
queda pendiente de una sesión con la Web levantada.

## Suites automatizadas (2026-08-01)

| Suite | Resultado |
|---|---|
| `IngenIA365ERP.Domain.Tests` | 80/80 ✅ |
| `IngenIA365ERP.Application.Tests` | 205/205 ✅ (incluye 7 nuevos de US3, 4 de US6, 5 de T045, 5 de T046) |
| `IngenIA365ERP.Architecture.Tests` | 34/34 ✅ (obligó el validator de `ListMfaResetRequestsQuery`, principio VIII) |
| `IngenIA365ERP.API.IntegrationTests` — colección `Identity` | 11/11 ✅ (Testcontainers SQL 2022 + Mongo 7 + Redis 7) |
| Build solución | 0 errores |

## Tests de integración nuevos (deuda 002 + US3)

| Tarea | Archivo | Escenario verificado |
|---|---|---|
| T022 | `Security_RecoveryCodeRedeem.cs` | Canje one-shot de recovery code en mfa/verify; reuso rechazado con `Identity.MfaInvalid`; `recoveryCodesRemaining=9`. |
| T036 | `EndToEnd_InviteRegisterLogin.cs` | Master invita → preview → accept registro → login → membresía activa. |
| T037 | `Security_InvitationReplay.cs` | Replay del token de invitación → 410 `Invitation.AlreadyAccepted`. |
| T038 | `EndToEnd_LoginSingleTenant.cs` | Mono-tenant → autoSelected con claim `active_tenant_id` correcto en el JWT. |
| T039 | `EndToEnd_LoginGenericErrors.cs` | Email inexistente y password mala → misma respuesta genérica (FR-041). |
| T040 | `EndToEnd_LockoutProgression.cs` | 5 fallos → `Identity.Locked.Soft`; login correcto durante el bloqueo también rechazado. |
| T041 | `EndToEnd_MfaEnrollmentForced.cs` | Política MFA del tenant fuerza enrollment; confirm con TOTP real eleva a sesión operativa. |
| T042 | `EndToEnd_MultiTenantSwitch.cs` | 2 membresías → TenantSelection → select A → switch a B con claims de B. |
| T043 | `Security_TenantCrossover.cs` | Token de A rechazado sobre recursos de B (403 `Membership.Forbidden`, 422 `Membership.NotActive`); control positivo sobre A. |
| T044 | `Security_TenantAdminScope.cs` | Admin de A no administra miembros de B; sí los de A. |
| T045 | `PromoteToTenantAdminCommandHandlerTests.cs` (unit) | Promoción, idempotencia, invalidación de caché. |
| T046 | `UpdateTenantMfaPolicyCommandHandlerTests.cs` (unit) | Activar/desactivar política, invalidación por miembro, auditoría FR-003d. |

## Hallazgos y correcciones durante la verificación

1. **`IX_ADM_Tenants_Identifier` único no filtrado en la BD de test**: el
   `EnsureCreated` del shape legacy (ErpTenantInfo) crea un índice único sobre
   `Identifier` que rompe al registrar un segundo tenant con `Identifier`
   NULL. La BD real no tiene ese índice; la fixture ahora lo re-crea
   **filtrado** (`WHERE Identifier IS NOT NULL`). Solo afecta al entorno de
   test.
2. **`UK_ADM_Tenants_Subdomain`**: los tests que registran dos tenants ahora
   envían `subdomain` explícito (dos NULL chocan en SQL Server).

## Fallas pre-existentes (fuera de alcance del 003)

La corrida completa de `IngenIA365ERP.API.IntegrationTests` muestra ~27
fallas en suites de la Fase 0 (`Audit`, `Attachments`, `Notifications`,
`Auth`, `Security`, `Compliance` sobre `ApiTestFixture`). Ninguno de esos
archivos fue tocado por este feature (verificado con
`git diff develop...HEAD -- tests/`); son deuda ambiental previa.

## Pendiente

- **T047**: recorrido manual del `quickstart.md` (curl + Playwright sobre la
  Web en el perfil `http` puerto 5200) con capturas — requiere API + Web
  levantadas en una sesión interactiva.
