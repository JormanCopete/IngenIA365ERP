# Implementation Plan: Remates de Identidad Central — Switcher, Navegación y Recuperación

**Branch**: `003-identidad-remates-ui` | **Date**: 2026-08-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-identidad-remates-ui/spec.md`

## Summary

Cierra la brecha de superficie del feature 002 sin tocar su comportamiento de
negocio: (1) **TenantSwitcher** en el encabezado que consume los servicios de
sesión ya existentes (`GetActiveTenantsAsync`/`SwitchAsync` + adopción de
sesión del cutover) con guardia de cambios sin guardar; (2) **navegación por
rol** en `MainLayout`/`NavMenu` leyendo los claims del JWT central; (3)
**canje de recovery codes** como alternativa al TOTP en el desafío MFA (los
códigos ya se persisten en `ADM_CentralUserTokens` vía ASP.NET Identity —
solo falta la vía de canje y la regeneración desde el perfil); (4)
**continuidad de sesión solo-pestaña** sustituyendo el storage in-memory del
cliente por `sessionStorage` del navegador; (5) **remates de entrada** (link
de recuperación en el login, adopción de sesión al aceptar invitación,
header con identidad real vía `GET /api/auth/me`); (6) **higiene**: retiro de
pantallas duplicadas/colgadas y listado de solicitudes de MFA reset para el
aprobador; (7) **deuda de tests de integración del 002** sobre la fixture
`CentralIdentityApiFixture` existente.

**Sin modelo de datos nuevo, sin migraciones SQL, sin endpoints destructivos**:
solo 3 adiciones de API (canje en `mfa/verify`, regeneración de códigos,
listado de reset requests) y trabajo de cliente Blazor.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0.5 (sin cambios).

**Primary Dependencies**: las existentes. Cero paquetes nuevos — el canje de
recovery codes usa `UserManager.RedeemTwoFactorRecoveryCodeAsync` /
`CountRecoveryCodesAsync` (ASP.NET Identity ya referenciado); el
`sessionStorage` usa `IJSRuntime` nativo de Blazor.

**Storage**: sin cambios de esquema. Recovery codes ya viven en
`ADM_CentralUserTokens` (store estándar de Identity, poblado por el
enrollment del 002). `SEC_MfaResetRequests` (Fase 0, per-tenant) solo gana
una query de lectura. Auditoría MongoDB recibe eventos nuevos.

**Testing**: `Application.Tests` (handlers/validators nuevos),
`API.IntegrationTests` sobre `CentralIdentityApiFixture` (los 11 tests de la
deuda 002 + canje de recovery codes), verificación E2E de UI con Playwright
(patrón de la evidencia T127).

**Target Platform**: los mismos del 002 (API .NET + Blazor Web/WASM; MAUI
comparte `Shared` y hereda el switcher y la navegación).

**Performance Goals**: SC-101 hereda SC-003 del 002 (switch < 3 s). El resto
sin objetivos nuevos de rendimiento.

**Constraints**:
- La decisión de Clarifications (sesión **solo-pestaña**) fija
  `sessionStorage` (se borra al cerrar la pestaña) — NO `localStorage`.
  Exposición XSS del token en `sessionStorage`: aceptada y documentada como
  trade-off del alcance elegido (mismo perfil de riesgo que una SPA
  estándar); mitigación heredada: access de 15 min + rotación de refresh.
- El canje de recovery code responde el MISMO error genérico que un TOTP
  incorrecto (FR-109) y es one-shot atómico (el token store de Identity
  reescribe la lista al canjear); el lockout progresivo del 002 sigue
  aplicando a los intentos fallidos del desafío.
- El listado de MFA reset corre bajo el tenant context del middleware
  existente (entidad per-tenant `SEC_MfaResetRequests`); detalle en
  research D-08.

**Scale/Scope**: ~6 páginas/componentes Blazor tocados o nuevos, 3 páginas
retiradas/absorbidas, 3 endpoints nuevos/extendidos, ~4 handlers/queries
nuevos, 11 tests de integración de deuda + los nuevos.

## Constitution Check

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | Spec 003 con checklist 16/16 y clarificación resuelta antes de este plan. |
| II   | Clean Architecture | PASS | El canje/regeneración de códigos entra por `ICentralIdentityProvider` (métodos nuevos); ningún handler toca `UserManager`. La UI consume los clients existentes. |
| III  | CQRS + MediatR | PASS | Nuevos: `RegenerateRecoveryCodesCommand` (+validator), `ListMfaResetRequestsQuery`; `MfaVerifyCommand` extendido conserva su validator. Endpoints Carter solo reenvían a `ISender`. |
| IV   | Multi-tenancy | PASS | Única query nueva a BD de tenant: `ListMfaResetRequestsQuery`, bajo el tenant context resuelto por el middleware existente. Cero cross-tenant. |
| V    | Person centralizada | PASS | No se toca `COR_People`. |
| VI   | PublicId externo | PASS | Sin entidades nuevas; el listado expone `PublicId` de `MfaResetRequest` (AuditableEntity). |
| VII  | Soft-delete + auditoría | PASS | Sin entidades nuevas; los flujos nuevos pasan por AuditBehavior. |
| VIII | Validación dual | PASS | Validators FluentValidation para los commands nuevos + validación de formato en las páginas (código de recuperación, confirmación de regeneración). |
| IX   | Errores visibles | PASS | Mensajes accionables en español; sesión inválida en storage degrada a login limpio (FR-114), nunca excepción cruda. |
| X    | SIPLA/SARLAFT | PASS | Eventos de auditoría nuevos: `CentralUser.Mfa.RecoveryCodeUsed`, `Profile.RecoveryCodesRegenerated`; el listado de reset requests es lectura. |
| XI   | Inmutabilidad contable | N/A | No toca movimientos contables. |
| XII  | Migraciones | PASS (vacuo) | Cero DDL en este feature. |

**Resultado**: 11/12 PASS + 1 N/A, sin excepciones nuevas.

## Project Structure

### Documentation (this feature)

```text
specs/003-identidad-remates-ui/
├── plan.md              # Este documento
├── spec.md              # Especificación (con clarificación encodeada)
├── research.md          # Phase 0: decisiones D-01..D-10
├── data-model.md        # Phase 1: sin entidades nuevas — mapa de lo reutilizado
├── quickstart.md        # Phase 1: verificación E2E por user story
├── contracts/
│   ├── mfa-recovery-codes.md    # Canje en verify + regeneración
│   └── mfa-reset-requests.md    # Listado para el aprobador
└── checklists/requirements.md   # 16/16
```

### Source Code (extensiones marcadas con ~, altas con +, retiros con −)

```text
src/
├── Core/IngenIA365ERP.Application/
│   ├── Common/Interfaces/Identity/
│   │   ~ ICentralIdentityProvider.cs        # + RedeemRecoveryCodeAsync, RegenerateRecoveryCodesAsync, CountRecoveryCodesAsync
│   ├── Identity/Auth/MfaVerify/
│   │   ~ MfaVerifyCommand.cs                # + UseRecoveryCode (bool); resultado + RecoveryCodesRemaining
│   │   ~ MfaVerifyCommandValidator.cs       # reglas por tipo de código
│   │   ~ MfaVerifyCommandHandler.cs         # rama de canje + contador restante + audit
│   ├── Identity/Profile/RegenerateRecoveryCodes/
│   │   + RegenerateRecoveryCodesCommand.cs  # + validator + handler (confirmación por password o TOTP)
│   └── Security/Auth/MfaReset/
│       + ListMfaResetRequestsQuery.cs       # + handler (por status, paginada, per-tenant)
├── Infrastructure/IngenIA365ERP.Identity/CentralIdentity/
│   ~ AspNetCoreIdentityProvider.cs          # implementa Redeem/Regenerate/Count vía UserManager
├── Presentation/IngenIA365ERP.API/
│   ~ Endpoints/CentralAuthModule.cs         # verify acepta la rama recovery (mismo endpoint)
│   ~ Endpoints/ProfileModule.cs             # + POST /api/profile/mfa/recovery-codes/regenerate [purpose=full]
│   ~ Endpoints/AuthEndpoints.cs             # + GET /api/auth/mfa/reset/requests (admin)
├── Presentation/IngenIA365ERP.Web.Client/
│   + Services/BrowserSessionSecureStorage.cs # ISecureStorage sobre sessionStorage (IJSRuntime)
│   ~ Program.cs                              # registro: sustituye WebAssemblySecureStorage
├── Presentation/IngenIA365ERP.Shared/
│   ├── Layout/
│   │   ~ MainLayout.razor                   # header: identidad real (email+empresa vía /me) + menú de cuenta + TenantSwitcher
│   │   ~ NavMenu.razor                      # secciones por rol (cuenta / admin empresa / master)
│   ├── Components/
│   │   + TenantSwitcher.razor               # visible si >1 membresía activa; usa TenantSessionClient
│   │   + DirtyTrackingEditForm.razor        # wrapper EditForm → IFormDirtyStateService
│   ├── Services/
│   │   + IFormDirtyStateService.cs          # + InMemoryFormDirtyStateService (scoped)
│   │   ~ Security/CentralAuthClient.cs      # + GetMeAsync (cachea identidad para el header)
│   └── Pages/Security/
│       ~ Login.razor                        # + link "¿Olvidaste tu contraseña?"
│       ~ MfaChallenge.razor                 # + toggle "usar código de recuperación" + aviso de restantes
│       ~ MfaEnrollmentCentral.razor         # + sección regenerar códigos (resuelve su TODO de /me)
│       ~ AcceptInvitation.razor             # adopta sesión (challenge=None → dashboard; MFA → paso correspondiente)
│       ~ MfaResetApprovals.razor            # listado de pendientes + aprobar/rechazar (absorbe MfaResetRequests)
│       − MfaEnrollment.razor                # retirar (duplicada de Central/Forced)
│       − ChangePasswordRequired.razor       # retirar (sin flujo que la use)
│       − MfaResetRequests.razor             # absorbida por la de aprobaciones
tests/
├── IngenIA365ERP.Application.Tests/         # MfaVerify (rama recovery), RegenerateRecoveryCodes, ListMfaResetRequests
└── IngenIA365ERP.API.IntegrationTests/Identity/  # deuda 002 (11 archivos) + Security_RecoveryCodeRedeem.cs
```

**Structure Decision**: todo el trabajo de UI vive en `IngenIA365ERP.Shared`
(lo heredan Web y MAUI). El almacenamiento solo-pestaña se implementa como
`ISecureStorage` nueva en `IngenIA365ERP.Web.Client`
(`BrowserSessionSecureStorage` sobre `sessionStorage` vía `IJSRuntime`),
reemplazando el registro de `WebAssemblySecureStorage`; el host server
conserva su storage in-memory (el prerender no persiste sesión) y MAUI
conserva su `ISecureStorage` nativo (ya persistente y seguro por SO).

## Phase 0 — Research

Resuelta en [research.md](./research.md): decisiones D-01..D-10 (switcher,
dirty-state, canje de códigos, sessionStorage, navegación por claims,
consumo de /me, adopción en accept, listado de reset requests, retiro de
páginas y estrategia de la deuda de tests).

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md): sin entidades nuevas; mapa de los
  almacenes reutilizados y de los eventos de auditoría nuevos.
- [contracts/](./contracts/): canje/regeneración de recovery codes y listado
  de solicitudes de MFA reset.
- [quickstart.md](./quickstart.md): verificación E2E por user story (curl +
  UI), reutilizando actores y credenciales del entorno local del 002.
- Agent context: bloque SPECKIT de `CLAUDE.md` apunta a este plan.

## Constitution Re-check (post Phase 1)

Sin cambios respecto de la tabla anterior: los contratos no introducen
entidades ni saltan el pipeline MediatR; el único acceso nuevo a BD de
tenant (`ListMfaResetRequestsQuery`) corre bajo el tenant context del
middleware. **11/12 PASS + 1 N/A.**

## Complexity Tracking

Sin violaciones que justificar. Única deuda consciente: el token en
`sessionStorage` es legible por JS de la propia página (riesgo XSS estándar
de SPA); se acepta por la decisión de Clarifications (alcance solo-pestaña)
y se compensa con la vida corta del access token y la rotación de refresh ya
existentes. Endurecimientos futuros (CSP estricta, cookies HttpOnly con
patrón BFF) quedan explícitamente fuera de alcance.
