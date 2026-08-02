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

## T047 — Recorrido del quickstart (2026-08-02, API 5100 + Web 5200 perfil http)

### Por curl (backend real, BD de desarrollo)

- **US3**: `POST /api/profile/mfa/recovery-codes/regenerate` con password de
  Gina → 10 códigos; canje en `mfa/verify` con `useRecoveryCode=true` →
  `challenge=None`, `recoveryCodesRemaining=9`; reuso del MISMO código →
  422 `Identity.MfaInvalid`; un código del juego viejo tras regenerar → 422.
- **T017**: `GET /api/auth/me` de Gina → `mfaEnabled=true`,
  `recoveryCodesRemaining` correcto, empresa activa.
- **Mongo** (`IngenIA365ERP_Audit.audit_events_`): eventos
  `Profile.RecoveryCodesRegenerated`, `CentralUser.Mfa.RecoveryCodeUsed` y
  `CentralUser.Mfa.RecoveryCodeFailed` con el email de Gina.

### Por UI (Playwright — capturas en `capturas/`)

| Escenario | Resultado | Captura |
|---|---|---|
| US5.1 — link "¿Olvidaste tu contraseña?" en el login | ✅ | `us5-login-forgot-link.png` |
| US3 — toggle de recovery code en el desafío MFA | ✅ | `us3-mfa-challenge-toggle.png`, `us3-modo-recovery-code.png` |
| US3 — Gina entra al dashboard canjeando un código | ✅ | `us1-gina-mono-empresa-sin-selector.png` |
| US3 — `/profile/mfa` estado activo con contador (8) y regeneración con password (10 nuevos, una sola vez) | ✅ | `us3-profile-mfa-contador.png`, `us3-regeneracion-codigos.png` |
| US1/US2 — Ana: header con email real, switcher con sus 2 cooperativas, NavMenu con "Mi Cuenta" + "Mi Cooperativa" | ✅ | `us1-us2-dashboard-ana-switcher.png` |
| US1 — switch Pacífico → Solidaria sin re-login; claims del JWT verificados (`active_tenant_id`, `tenant_admin`) | ✅ | `us1-switch-solidaria.png` |
| US1 — Gina mono-empresa: badge sin selector | ✅ | `us1-gina-mono-empresa-sin-selector.png` |
| US2 — nav de Gina (no-admin) sin "Mi Cooperativa" ni consola SaaS | ✅ | `us6-consola-aprobaciones.png` |
| US4 — F5 restaura sesión + empresa activa (tokens en `sessionStorage` con prefijo `ingenia365:`) | ✅ tras 2 fixes (abajo) | `us4-f5-sesion-restaurada.png`, `us4-f5-switcher-restaurado.png` |
| US4/FR-114 — token adulterado → login limpio y storage purgado | ✅ | — |
| US6.1 — `/security/mfa-enrollment` y `/security/change-password-required` → 404 | ✅ | — |
| US6.2 — consola de aprobaciones carga con tabla de pendientes + formulario (sin GUIDs para aprobar) | ✅ tras fix DI (abajo) | `us6-consola-aprobaciones.png` |

### Bugs encontrados y corregidos durante T047

1. **F5 devolvía 302 → /login desde el servidor** (rompía todo US4): el
   `[Authorize]` de las páginas propagaba el challenge de la cookie en la
   petición HTTP inicial y el server redirigía antes de que el WASM hidratara
   la sesión. Fix: `.AllowAnonymous()` en `MapRazorComponents` (Web/Program.cs)
   — la guardia sigue en el cliente (`AuthorizeRouteView` + `RedirectToLogin`).
2. **Tras F5 el TenantSwitcher/GetMe no funcionaban**: `CentralAuthClient` es
   scoped y renacía sin token aunque `sessionStorage` lo tuviera. Fix:
   `TryRestoreSessionAsync()` (rehidratación lazy desde el storage, con `exp`
   del propio JWT) invocada desde `GetMeAsync` y el `TenantSwitcher`.
3. **`/security/mfa-reset-approvals` crasheaba el runtime WASM**: la página
   inyecta el `AuthClient` per-tenant de Fase 0 que nunca estuvo registrado en
   el DI de Web.Client ni del host Web. Fix: registrado en ambos.

### No cubierto en esta corrida de UI (cubierto por tests de integración)

- US5.2/5.3 — accept de invitación adoptando sesión (cubierto por
  `EndToEnd_InviteRegisterLogin` + `EndToEnd_MfaEnrollmentForced` y el flujo
  E2E del 002 con Elena); el recorrido por correo real requiere emitir
  invitaciones nuevas sobre la BD de desarrollo.
- US6.2 completo (crear solicitud + doble aprobación con datos per-tenant) —
  backend cubierto por `ListMfaResetRequestsQueryHandlerTests` y los
  handlers de Fase 0 ya probados.
