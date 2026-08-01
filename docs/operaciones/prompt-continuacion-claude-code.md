# Prompt para continuar en otra cuenta Claude Code

> Copiá **todo lo que está debajo del separador `---`** y pegalo como primer
> mensaje en la nueva sesión de Claude Code (después de abrir el proyecto
> `D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\`).
>
> A diferencia del prompt para Claude Desktop, este asume que el nuevo
> asistente tiene acceso directo al filesystem (Read/Edit/Write/Bash) y
> puede leer el repo. Le doy contexto de estado, no de código.
>
> **Actualizado 2026-07-31** tras la sesión que completó el manual técnico
> completo (15 pasos), arregló 6 bugs y probó la UI Web parcialmente.

---

Estoy retomando la **Fase 1 — Identidad Central Federada** del proyecto
**IngenIA365ERP** en la rama `002-identidad-central-federada`. El manual
técnico de pruebas ya se ejecutó **completo end-to-end por curl** (US1–US5,
Phase 4b, background jobs) y la UI Web se probó parcialmente. Antes de tocar
nada, **leé estos archivos en este orden**:

1. `CLAUDE.md` (raíz) — instrucciones del proyecto y stack.
2. `.specify/memory/constitution.md` — los 12 principios vinculantes.
3. `specs/002-identidad-central-federada/plan.md` + `spec.md` +
   `tasks.md` — el feature en curso.
4. `docs/operaciones/setup-local-pruebas.md` — cómo levantar el entorno
   local sin Docker. Incluye los **7 gaps de esquema** ya aplicados y la
   tabla de errores frecuentes (incluye el relay WSL2 zombie de Redis).
5. `docs/operaciones/manual-pruebas-identidad-central.md` — los 15 pasos
   técnicos. **Todos ejecutados OK** (2026-07-31).
6. `docs/operaciones/prompt-continuacion-claude-desktop.md` — estado
   exacto: credenciales, IDs, bugs arreglados, pendientes. **El más
   importante.**

## Estado rápido

- **Backend completo verificado end-to-end**: onboarding por invitación,
  login central, lockout, Phase 4b (password change/forgot/reset, MFA
  voluntario y forzado), US3 multi-empresa (selector, switch, default),
  US4 admin de empresa (miembros, suspensión, salvaguarda último admin,
  política MFA), US5 master admin (force-MFA-reset), jobs de expiración
  y limpieza. Auditoría Mongo verificada en cada bloque.
- **UI Web probada parcialmente** (Playwright, perfil http en 5200):
  login central sin combo (`/security/login`) → MFA challenge → verify OK;
  `/auth/accept-invitation` E2E (creó a Elena); `/auth/forgot-password`
  con mensaje genérico.

## Bugs arreglados (NO revertir)

Sesión 2026-07-26 (previa):
1. `API/Program.cs` — `AddCachingServices` habilitado.
2. `Caching/DependencyInjection.cs` — `AbortOnConnectFail=false`.
3. `LoginCommandHandler` — `IssueMasterOperationalAsync` para master sin tenants.
4. `appsettings.Development.json` — sección raíz `Smtp` host `127.0.0.1`.
5. Migraciones 18–23 + gaps 1–6 de esquema.

Sesión 2026-07-31 (esta):
1. **Refresh tokens ahora mueren al cambiar/resetear password o force-MFA-reset**:
   `CentralRefreshSession` guarda `SecurityStamp` y `RefreshTokenCommandHandler`
   lo compara (mismatch o null → familia invalidada). Poblado en los 6 puntos
   de emisión. Tests en `RefreshTokenCommandHandlerTests.cs` (6 casos).
2. `AcceptInvitationCommandHandler` ahora **persiste el refresh token** en el
   store (antes devolvía un token muerto).
3. `AdminDbContext.OnModelCreating` aplica `PasswordResetTokenConfiguration`
   (antes: `Invalid object name 'dbo.PasswordResetTokens'` → 500 en forgot) +
   **Gap 7** de esquema (`ADM_PasswordResetTokens.Id` BIGINT→INT).
4. `TenantResolutionMiddleware` — exenciones `/api/profile/mfa/enroll` y
   `/confirm` (el enrollment forzado era inalcanzable, FR-003b roto) +
   fallback solo-master que resuelve tenant desde la ruta `/api/tenants/{id}`
   (FR-039/FR-040a: el master no tiene membresías ni claim de tenant).
5. `Web/Program.cs` — registrados los 6 clientes de identidad de Shared
   (el prerender del host server daba 500 en todas las páginas Security).
6. `CentralAuthClient.ParseAsync` + `InvitationClient.ParseAsync` — manejo
   de **204 No Content** (antes: `JsonException` cruda en pantalla en
   forgot y en toda acción 204 por UI).

## Credenciales / IDs (actualizadas 2026-07-31)

| Actor | Datos |
|---|---|
| Master admin | `master@ingenia.dev` / `MasterDev2026!Strong` — Id `c3cc31c1-a855-43bb-aec8-c751cfff4526` |
| Ana (admin ambas coops) | `ana.perez@coop.solidaria.test` / `Ana-After-Reset-2026` — Id `972d6f61-453c-4864-b7a9-db20df06fc0f` |
| Ana — MFA secret TOTP | `XVCTZVBYV7CTIHSVUVDWY6CO5SK2WEUB` (login SIEMPRE pide TOTP) |
| Ana — recovery codes | `RYBK3-Y7MF3 NPF5W-CW32Y NT257-GFCJW 66RJQ-WFQYR KD78K-63C5V B2VMR-NYHT7 VJB4K-4WYPN 443W2-VQVMP 2KKBM-6BGH6 CHJ75-J693P` |
| Luis (miembro Solidaria) | `luis.martinez@coop.solidaria.test` / `Luis-Strong-Pwd-2026` — Id `619cc290-7c25-4ce3-96de-42a0f47781e6` — sin MFA |
| Carla (miembro Solidaria) | `carla.gomez@coop.solidaria.test` / `Carla-Strong-Pwd-2026` — Id `02b3c560-6cdb-490e-ad94-2a10bd1e6fe3` — sin MFA (force-reset del master en prueba 7.1) |
| Elena (miembro Solidaria, creada por UI) | `elena.vega@coop.solidaria.test` / `Elena-Strong-Pwd-2026` — sin MFA |
| Gina (miembro Solidaria, flujo gated) | `gina.torres@coop.solidaria.test` / `Gina-Strong-Pwd-2026` — MFA activo, secret `BAVZ5IJWACDNYQRWUR2FSHV6V4NSWVUZ` |
| Tenant 1 "Coop. Solidaria Dev" | `ea5aad63-f579-40b3-86b9-85d3bce6401d` — política MFA **ACTIVA** |
| Tenant 2 "Coop. del Pacifico Dev" | `da51829f-c5b2-45da-9145-215120d810b7` — política MFA off — **default de Ana** |
| Diego | NO existe — solo una invitación expirada (prueba del job 8.1) |

> Luis/Carla/Elena sin MFA + política de Solidaria activa ⇒ su login da
> `MfaEnrollmentRequired` y deben completar el enrollment forzado (ya
> funciona tras el fix del middleware).
> Para generar códigos TOTP sin celular: proyecto consola con `Otp.NET`
> (`Base32Encoding.ToBytes(secret)` + `new Totp(...)` — 20 líneas).

## Levantar el stack local

```powershell
# 1. Redis (WSL Ubuntu)
wsl -d Ubuntu -- sudo service redis-server start
wsl -d Ubuntu -- redis-cli ping   # esperar PONG
# Si desde Windows da ConnectionReset con PONG adentro: wsl --shutdown y reintentar (relay zombie)

# 2. smtp4dev (terminal aparte)
smtp4dev --smtpport 1025 --urls "http://localhost:8025"

# 3. API (raíz del repo)
dotnet run --project src/Presentation/IngenIA365ERP.API

# 4. Web Blazor — usar el perfil http para pruebas automatizadas
#    (el cert self-signed de https://localhost:7200 rompe Playwright)
dotnet run --project src/Presentation/IngenIA365ERP.Web --launch-profile http
# UI en http://localhost:5200 — login NUEVO en /security/login
```

## Pendientes (elegí uno para arrancar)

- ~~Cutover UI~~ → **HECHO (2026-08-01)**: `/login` sirve el login central
  (el viejo quedó en `/legacy-login`); `CentralAuthClient.AdoptSessionAsync`
  persiste el JWT central en las keys `auth_token`/`refresh_token` que leen
  `CustomAuthStateProvider` y `AuthBearerHandler`, y notifica el auth state.
  Verificado E2E: login → MFA → dashboard sin rebote → Salir → login central.
- **Secuelas del cutover** (siguientes iteraciones):
  - Persistencia ante F5: `ISecureStorage` Web/WASM es in-memory — un reload
    pierde la sesión (limitación pre-existente; nota en `Web/Program.cs`).
  - `PermissionGate`/permisos finos: el JWT central no trae claims `perm`
    del tenant — las páginas ERP que gateen por permiso necesitarán `GET /me`.
  - El header muestra "Usuario" genérico (no lee el claim `email`).
  - TenantSwitcher del header (T091) sigue sin existir.
- **Portar gaps 1–7 a `database/migration/24_Backfill_Gaps.sql`** formal.
- **Tests pendientes**: T118 (integration master-register-tenant),
  T127 (evidencia quickstart con screenshots), T124 (load test NBomber).
- **Observaciones resueltas (2026-08-01)**: accept-invitation ahora respeta
  MFA (challenge `MfaRequired`/`MfaEnrollmentRequired` sin tokens si aplica —
  FR-003b/c); `otpauth://` etiqueta con el email; `GET /members` devuelve
  `email` por miembro (batch vía `ICentralIdentityProvider.GetEmailsByIdsAsync`).
- **Observaciones en backlog** (decididas como no-urgentes): timing
  side-channel en forgot (185ms vs 6ms); eventos globales caen en la
  colección Mongo `audit_events_` (sufijo vacío).

## Reglas para esta sesión

1. **Antes de modificar código**, decime qué archivos vas a leer y qué
   pensás cambiar — espera mi OK. Bug fixes chicos (typos, imports,
   registros de una línea) podés ejecutarlos directo.
2. **Si un endpoint responde 500**, mostrame el stack completo del
   serilog (no solo el mensaje).
3. **No inventes credenciales/IDs** — usá los de la tabla o pedime los nuevos.
4. **Preferí `curl` sobre UI** salvo que la tarea sea de UI.
5. **Constitución del proyecto**: si el cambio roza multi-tenancy,
   auditoría, seguridad o CQRS, releé el principio relevante antes del diff.

## Qué quiero que hagas ahora

**REEMPLAZÁ ESTA LÍNEA POR LA TAREA CONCRETA**, por ejemplo:

- Diseñar y proponer el cutover del login (puente JWT central ↔ auth del host Web).
- Portar los gaps 1–7 a `24_Backfill_Gaps.sql`.
- Ejecutar T118/T127 y armar la evidencia de release.
- Otro: `<contá qué necesitás>`
