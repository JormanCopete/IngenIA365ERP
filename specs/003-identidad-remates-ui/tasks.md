---

description: "Task list for Remates de Identidad Central — Switcher, Navegación y Recuperación"
---

# Tasks: Remates de Identidad Central (feature 003)

**Input**: Design documents from `/specs/003-identidad-remates-ui/`

**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Tests**: incluidos — la constitución exige tests por handler y el spec incorpora la deuda de integración del feature 002 (SC-107).

**Organization**: tareas agrupadas por user story (P1 → P3). MVP = US1 + US2 (los dos P1). Cero DDL y cero paquetes nuevos en todo el feature.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede correr en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: user story a la que pertenece (US1..US6)

## Path Conventions

- Application: `src/Core/IngenIA365ERP.Application/`
- Infra Identity: `src/Infrastructure/IngenIA365ERP.Identity/`
- API: `src/Presentation/IngenIA365ERP.API/`
- UI compartida: `src/Presentation/IngenIA365ERP.Shared/`
- Cliente WASM: `src/Presentation/IngenIA365ERP.Web.Client/`
- Tests: `tests/IngenIA365ERP.{Application,API.Integration}.Tests/`

---

## Phase 1: Setup

Sin tareas: el feature no agrega paquetes, configuración ni esquema
(plan.md > Technical Context). El entorno local es el del feature 002.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: la única pieza consumida por más de una historia.

- [X] T001 Agregar `GetMeAsync()` a `src/Presentation/IngenIA365ERP.Shared/Services/Security/CentralAuthClient.cs` (+ record `MeResponse` con email, isGlobalMasterAdmin, mfaEnabled, activeTenant {publicId, name}, availableTenants, defaultTenantPublicId, recoveryCodesRemaining?) consumiendo `GET /api/auth/me`; cachear por sesión en el client y limpiar el caché en `AdoptSessionAsync`/`LogoutAsync` para que el switch refresque la identidad. Consumidores: header (US5), sección de códigos (US3), detección "MFA ya activo" (TODO existente de MfaEnrollmentCentral).

**Checkpoint Foundational**: `GetMeAsync` disponible — US1..US6 pueden avanzar en paralelo.

---

## Phase 3: User Story 1 — Cambio de empresa desde el encabezado (Priority: P1) 🎯 MVP 1/2

**Goal**: TenantSwitcher siempre visible con la empresa activa; cambio sin re-login con recarga de contexto y guardia de cambios sin guardar (FR-101..FR-104).

**Independent Test**: quickstart.md § US1 (Ana multi-empresa cambia con y sin formulario sucio; Luis mono-empresa no ve selector).

- [X] T002 [US1] Crear `IFormDirtyStateService` (`Register/MarkDirty/MarkClean/HasDirtyForms/Clear`) + `InMemoryFormDirtyStateService` en `src/Presentation/IngenIA365ERP.Shared/Services/IFormDirtyStateService.cs` (research D-02)
- [X] T003 [P] [US1] Crear wrapper `DirtyTrackingEditForm.razor` en `src/Presentation/IngenIA365ERP.Shared/Components/DirtyTrackingEditForm.razor` (marca dirty en `OnFieldChanged`, clean en submit válido y en `Dispose`)
- [X] T004 [US1] Registrar `IFormDirtyStateService` como scoped en `src/Presentation/IngenIA365ERP.Web/Program.cs`, `src/Presentation/IngenIA365ERP.Web.Client/Program.cs` y el host MAUI (`src/Presentation/IngenIA365ERP.App/MauiProgram.cs` si registra servicios de Shared)
- [X] T005 [US1] Crear `TenantSwitcher.razor` en `src/Presentation/IngenIA365ERP.Shared/Components/TenantSwitcher.razor`: carga `TenantSessionClient.GetActiveTenantsAsync()` al autenticarse; muestra solo el nombre si hay 1 membresía y selector si hay >1; al elegir consulta `IFormDirtyStateService.HasDirtyForms` → diálogo de confirmación existente → `SwitchAsync` → navegar a `/` sin forceLoad; si el switch falla (`Membership.NotActive`), toast accionable y conserva la sesión actual (FR-104)
- [X] T006 [US1] Montar `TenantSwitcher` en el header de `src/Presentation/IngenIA365ERP.Shared/Layout/MainLayout.razor` (reemplaza el badge estático `@_tenantId`)
- [X] T007 [P] [US1] Migrar los formularios de identidad a `DirtyTrackingEditForm`: `src/Presentation/IngenIA365ERP.Shared/Pages/Security/ChangePassword.razor`, `MfaEnrollmentCentral.razor` y `DefaultTenantSetting.razor`

**Checkpoint US1**: FR-019..FR-022 del 002 quedan con superficie completa (SC-101).

---

## Phase 4: User Story 2 — Navegación completa por rol (Priority: P1) 🎯 MVP 2/2

**Goal**: menú de cuenta para todo autenticado + secciones admin de empresa y master, decididas por claims del JWT (research D-06); cero pantallas huérfanas (FR-105..FR-107).

**Independent Test**: quickstart.md § US2 (Luis regular / Ana admin / master ven exactamente sus opciones; ninguna entrada rota).

- [X] T008 [US2] Extender `src/Presentation/IngenIA365ERP.Shared/Layout/NavMenu.razor` con las secciones por rol: **Cuenta** (Cambiar contraseña `/profile/password`, MFA `/profile/mfa`, Empresa por defecto `/profile/default-tenant`) para todo autenticado; **Mi cooperativa** (Miembros `/admin/tenant/{active_tenant_id}/members`, Política MFA `/admin/tenant/{active_tenant_id}/mfa-policy`) solo con claim `tenant_admin=true`; **Consola SaaS** (Registrar cooperativa `/saas/register-tenant`, Reset de MFA `/saas/force-mfa-reset`, Aprobaciones `/security/mfa-reset-approvals`) solo con `is_global_master_admin=true`; el `active_tenant_id` sale del `AuthenticationState`
- [X] T009 [US2] Agregar menú desplegable de cuenta en el header de `src/Presentation/IngenIA365ERP.Shared/Layout/MainLayout.razor` (mismos enlaces de Cuenta + Salir existente), visible solo autenticado

**Checkpoint US2 (MVP)**: SC-102 — toda pantalla activa alcanzable y por rol correcto.

---

## Phase 5: User Story 3 — Entrar con un código de recuperación (Priority: P2)

**Goal**: canje one-shot en el desafío MFA + contador de restantes + regeneración con confirmación de identidad (FR-108..FR-112; contracts/mfa-recovery-codes.md; research D-03/D-04).

**Independent Test**: quickstart.md § US3 con Gina (canje, reuso rechazado, regeneración, auditoría).

- [X] T010 [US3] Extender `ICentralIdentityProvider` en `src/Core/IngenIA365ERP.Application/Common/Interfaces/Identity/ICentralIdentityProvider.cs` con `RedeemRecoveryCodeAsync(Guid, string, CancellationToken) → bool`, `CountRecoveryCodesAsync(Guid, CancellationToken) → int` y `RegenerateRecoveryCodesAsync(Guid, CancellationToken) → IReadOnlyList<string>`
- [X] T011 [US3] Implementar los 3 métodos en `src/Infrastructure/IngenIA365ERP.Identity/CentralIdentity/AspNetCoreIdentityProvider.cs` vía `UserManager.RedeemTwoFactorRecoveryCodeAsync` / `CountRecoveryCodesAsync` / `GenerateNewTwoFactorRecoveryCodesAsync(user, 10)`
- [X] T012 [P] [US3] Agregar constantes `CentralUserMfaRecoveryCodeUsed`, `CentralUserMfaRecoveryCodeFailed`, `ProfileRecoveryCodesRegenerated` en `src/Core/IngenIA365ERP.Application/Common/Audit/AuditEventTypes.cs`
- [X] T013 [US3] Extender `MfaVerifyCommand` (`UseRecoveryCode` bool default false) + validator (rama recovery: 5–20 chars alfanumérico-guion; rama TOTP sin cambios) + handler en `src/Core/IngenIA365ERP.Application/Identity/Auth/MfaVerify/`: canje → mismos emisiones de tokens que hoy + `RecoveryCodesRemaining` en el resultado; fallo → `Identity.MfaInvalid` genérico + contador de lockout + evento `RecoveryCodeFailed`; éxito → evento `RecoveryCodeUsed`
- [X] T014 [US3] Propagar `recoveryCodesRemaining` en la respuesta del verify: result record del handler, `CentralAuthModule.cs` (API) y `LoginResponse` del cliente en `src/Presentation/IngenIA365ERP.Shared/Services/Security/CentralAuthClient.cs`
- [X] T015 [US3] Crear `RegenerateRecoveryCodesCommand` (+validator XOR `CurrentPassword`/`TotpCode`, +handler con confirmación de identidad, MFA activo requerido, evento de auditoría) en `src/Core/IngenIA365ERP.Application/Identity/Profile/RegenerateRecoveryCodes/`
- [X] T016 [US3] Mapear `POST /api/profile/mfa/recovery-codes/regenerate` `[RequirePurpose("full")]` en `src/Presentation/IngenIA365ERP.API/Endpoints/ProfileModule.cs` + método `RegenerateRecoveryCodesAsync` en `ProfileClient` (`src/Presentation/IngenIA365ERP.Shared/Services/Security/ProfileClient.cs`)
- [X] T017 [P] [US3] Extender `GetMeQuery`/handler (`src/Core/IngenIA365ERP.Application/Identity/Auth/Me/`) con `RecoveryCodesRemaining` (vía `CountRecoveryCodesAsync` cuando MFA activo)
- [X] T018 [US3] UI `src/Presentation/IngenIA365ERP.Shared/Pages/Security/MfaChallenge.razor`: enlace "Usar un código de recuperación" que alterna el input (placeholder y validación de formato) y envía `useRecoveryCode=true`; tras éxito muestra "te quedan N códigos" (banner destacado si N ≤ 3, crítico si N = 0)
- [X] T019 [US3] UI `src/Presentation/IngenIA365ERP.Shared/Pages/Security/MfaEnrollmentCentral.razor`: sección "Códigos de recuperación" (conteo desde `GetMeAsync`, botón regenerar con confirmación password/TOTP, muestra los 10 nuevos una sola vez); resuelve el TODO de detección "MFA ya activo" con `GetMeAsync`
- [X] T020 [P] [US3] Tests `tests/IngenIA365ERP.Application.Tests/Identity/Auth/MfaVerifyCommandHandlerTests.cs`: canje válido → tokens + remaining y evento; canje inválido → `Identity.MfaInvalid` + lockout++; TOTP sin cambios de comportamiento
- [X] T021 [P] [US3] Tests `tests/IngenIA365ERP.Application.Tests/Identity/Profile/RegenerateRecoveryCodesCommandHandlerTests.cs`: XOR de confirmación, password/TOTP inválidos, sin MFA → `Profile.Mfa.NotEnrolled`, éxito → 10 códigos + auditoría
- [ ] T022 [US3] Test de integración `tests/IngenIA365ERP.API.IntegrationTests/Identity/Security_RecoveryCodeRedeem.cs` sobre `CentralIdentityApiFixture`: enroll → canjear código → entra; reuso → 422; contador decrementa; regeneración invalida el juego viejo

**Checkpoint US3**: SC-103 — cero códigos reutilizables.

---

## Phase 6: User Story 4 — La sesión sobrevive a una recarga (Priority: P2)

**Goal**: `sessionStorage` solo-pestaña reemplaza el diccionario en memoria del cliente WASM (FR-113/FR-114; research D-05; alcance de Clarifications).

**Independent Test**: quickstart.md § US4 (F5 conserva sesión y empresa; logout limpia; token adulterado degrada a login limpio).

- [ ] T023 [US4] Crear `BrowserSessionSecureStorage : ISecureStorage` en `src/Presentation/IngenIA365ERP.Web.Client/Services/BrowserSessionSecureStorage.cs` sobre `window.sessionStorage` vía `IJSInProcessRuntime` (Set/Get/Remove/RemoveAll con try/catch → null si el storage no está disponible)
- [ ] T024 [US4] Sustituir el registro de `WebAssemblySecureStorage` por `BrowserSessionSecureStorage` en `src/Presentation/IngenIA365ERP.Web.Client/Program.cs` (mismo lifetime Singleton por los DelegatingHandlers; documentar en comentario que host server y MAUI no cambian)
- [ ] T025 [US4] Endurecer `src/Presentation/IngenIA365ERP.Shared/Services/CustomAuthStateProvider.cs`: un JWT ilegible/corrupto en storage (excepción de `JwtClaimsExtractor` o token expirado) degrada a estado anónimo y limpia las keys — nunca excepción cruda (FR-114)

**Checkpoint US4**: SC-104 — cero pérdidas de sesión por F5; cero artefactos tras logout.

---

## Phase 7: User Story 5 — Remates del flujo de entrada (Priority: P3)

**Goal**: link de recuperación en el login, adopción de sesión al aceptar invitación y header con identidad real (FR-115..FR-117; research D-07).

**Independent Test**: quickstart.md § US5.

- [ ] T026 [P] [US5] Agregar el enlace "¿Olvidaste tu contraseña?" → `/auth/forgot-password` en `src/Presentation/IngenIA365ERP.Shared/Pages/Security/Login.razor`
- [ ] T027 [US5] Exponer la adopción de sesión externa en `src/Presentation/IngenIA365ERP.Shared/Services/Security/CentralAuthClient.cs`: método público `AdoptChallengeToken(token)` (para que accept pueda encadenar al paso MFA) — `AdoptSessionAsync` ya es público desde el cutover; y en `InvitationClient.cs` exponer `challenge`/`challengeToken`/tokens del response de accept (DTO del 002 ya los envía)
- [ ] T028 [US5] Reescribir el post-éxito de `src/Presentation/IngenIA365ERP.Shared/Pages/Security/AcceptInvitation.razor` (resuelve sus 2 TODO): `challenge=None` → `AdoptSessionAsync` + navegar a `/` (SC-105); `MfaRequired` → `AdoptChallengeToken` + `/security/mfa-challenge`; `MfaEnrollmentRequired` → `AdoptChallengeToken` + `/auth/enroll-mfa-forced`
- [X] T029 [US5] Header de `src/Presentation/IngenIA365ERP.Shared/Layout/MainLayout.razor`: mostrar email y nombre de empresa activa reales vía `GetMeAsync` (T001), refrescando al evento `Authenticated` del client (login/switch/adopción)

**Checkpoint US5**: SC-105 — de clic en el correo al tablero sin re-login.

---

## Phase 8: User Story 6 — Higiene de pantallas y aprobación operable (Priority: P3)

**Goal**: retiro de páginas duplicadas/colgadas y listado de solicitudes de MFA reset para aprobar sin GUIDs (FR-118/FR-119; contracts/mfa-reset-requests.md; research D-08/D-09).

**Independent Test**: quickstart.md § US6.

- [ ] T030 [US6] Crear `ListMfaResetRequestsQuery` (status filter, paginada) + handler en `src/Core/IngenIA365ERP.Application/Security/Auth/MfaReset/ListMfaResetRequestsQuery.cs`: proyección con `PublicId`, afectado y solicitante resueltos (username/email del `User` per-tenant), motivo, fechas, estado, aprobaciones — bajo el tenant context (principio IV; nunca `int Id` afuera)
- [ ] T031 [US6] Mapear `GET /api/auth/mfa/reset/requests` (query params status/page/pageSize; guard de admin del tenant o master, el mismo de request/approve) en `src/Presentation/IngenIA365ERP.API/Endpoints/AuthEndpoints.cs`
- [ ] T032 [P] [US6] Agregar `ListMfaResetRequestsAsync` a `src/Presentation/IngenIA365ERP.Shared/Services/Security/AuthClient.cs`
- [ ] T033 [US6] Reescribir `src/Presentation/IngenIA365ERP.Shared/Pages/Security/MfaResetApprovals.razor`: tabla de pendientes (solicitante, afectado, motivo, fechas, expira) con acciones Aprobar/Rechazar sobre los endpoints existentes + formulario de alta de solicitud (absorbe `MfaResetRequests.razor`); estados resueltos desaparecen del filtro Pending
- [ ] T034 [US6] Eliminar `src/Presentation/IngenIA365ERP.Shared/Pages/Security/MfaEnrollment.razor`, `ChangePasswordRequired.razor` y `MfaResetRequests.razor` + toda referencia residual (grep por sus rutas `/security/mfa-enrollment`, `/security/change-password-required`, `/security/mfa-reset-requests`)
- [ ] T035 [P] [US6] Tests `tests/IngenIA365ERP.Application.Tests/Security/Auth/ListMfaResetRequestsQueryHandlerTests.cs`: filtro por status, paginación, proyección de nombres, aislamiento por tenant

**Checkpoint US6**: SC-106 — aprobar sin identificadores técnicos; cero páginas duplicadas (FR-118, SC-102).

---

## Phase 9: Deuda de verificación 002 + Polish

**Purpose**: SC-107 (deuda de integración del feature 002 en cero, sobre `CentralIdentityApiFixture` — research D-10) + cierre de evidencia.

- [ ] T036 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_InviteRegisterLogin.cs` (master invita → preview → accept rama nueva → login → membership activa + audit)
- [ ] T037 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/Security_InvitationReplay.cs` (accept → replay → 410 `Invitation.AlreadyAccepted`)
- [ ] T038 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_LoginSingleTenant.cs` (mono-tenant → JWT con `active_tenant_id` correcto)
- [ ] T039 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_LoginGenericErrors.cs` (email inexistente y password mala → misma respuesta, FR-041)
- [ ] T040 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_LockoutProgression.cs` (5 fallos → 423 60s; reset tras éxito)
- [ ] T041 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_MfaEnrollmentForced.cs` (política MFA → login → MfaEnrollmentRequired → enroll con challenge token → confirm → elevación)
- [ ] T042 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/EndToEnd_MultiTenantSwitch.cs` (2 tenants → selector → A → switch a B → claims de B)
- [ ] T043 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/Security_TenantCrossover.cs` (token con `active_tenant_id` sin membresía → rechazo)
- [ ] T044 [P] Test `tests/IngenIA365ERP.API.IntegrationTests/Identity/Security_TenantAdminScope.cs` (admin de A sobre miembros de B → 401/403)
- [ ] T045 [P] Test unit `tests/IngenIA365ERP.Application.Tests/Memberships/PromoteToTenantAdminCommandHandlerTests.cs` (promoción, idempotencia, notificación de caché)
- [ ] T046 [P] Test unit `tests/IngenIA365ERP.Application.Tests/Tenants/UpdateTenantMfaPolicyCommandHandlerTests.cs` (activar/desactivar, invalidación de caché por miembro, auditoría FR-003d)
- [ ] T047 Ejecutar quickstart.md completo (curl + Playwright) y archivar evidencia con capturas en `docs/release-notes/003-identidad-remates-ui/evidencia-pruebas.md`
- [ ] T048 Sweep final: `dotnet build` solución 0 errores + suites Domain/Application/Architecture/Integration completas en verde
- [ ] T049 [P] Actualizar `docs/operaciones/prompt-continuacion-claude-code.md`, `prompt-continuacion-claude-desktop.md` y `docs/INDICE-DOCUMENTACION.md` con el estado del feature 003

---

## Dependencies & Execution Order

- **Phase 2 (T001)** bloquea el header de US5 (T029) y la sección de códigos de US3 (T019); todo lo demás puede arrancar tras ella.
- **US1**: T002 → {T003, T004} → T005 → T006; T007 tras T003. Independiente del resto.
- **US2**: T008/T009 independientes entre sí y del resto (solo claims existentes).
- **US3**: T010 → T011 → T013 → T014; T012 paralelo; T015 → T016; T017 paralelo tras T010; UI T018 tras T014, T019 tras T016+T017+T001; tests T020/T021 con sus handlers; T022 al final de la historia.
- **US4**: T023 → T024; T025 paralelo. Independiente (mejora la UX de todas las demás pero ninguna depende de ella).
- **US5**: T026 paralelo; T027 → T028; T029 tras T001.
- **US6**: T030 → T031 → {T032, T033}; T034 tras T033; T035 con T030.
- **Phase 9**: T036–T046 todos paralelos (archivos distintos, misma fixture); T047 tras completar US1–US6; T048 al final; T049 paralelo con T048.

### Parallel Example — arranque con dos personas

```text
Dev A: T001 → US1 completa (T002..T007) → US4 (T023..T025)
Dev B: US2 (T008, T009) → US3 backend (T010..T017) → US3 UI (T018, T019)
Ambos: Phase 9 en paralelo (T036..T046 son [P])
```

## Implementation Strategy

1. **MVP primero**: Phase 2 + US1 + US2 (~9 tareas) — con eso el producto
   multi-empresa es operable y navegable de punta a punta. **STOP y validar**
   con quickstart § US1–US2.
2. **Seguridad**: US3 (recovery codes) + US4 (sesión) — cierran las dos
   promesas de seguridad/continuidad.
3. **Pulido**: US5 + US6.
4. **Cierre**: Phase 9 (deuda + evidencia + sweep) → PR a develop.

Checkpoints válidos de demo: post-US2 (MVP), post-US4, post-US6, cierre.

## Notes

- Cero DDL y cero paquetes nuevos: ninguna tarea toca `database/` ni los csproj.
- Todo command nuevo lleva validator hermano y pasa por los pipeline
  behaviors (constitución III); los fallos de canje alimentan el lockout
  existente.
- Los DTOs del listado de MFA reset exponen `PublicId` únicamente
  (constitución VI).
- La verificación E2E de UI reutiliza el patrón Playwright de la evidencia
  del 002 (`docs/release-notes/002-identidad-central-federada/`).
