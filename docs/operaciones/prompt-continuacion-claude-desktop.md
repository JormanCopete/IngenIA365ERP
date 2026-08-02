# Prompt para continuar en Claude Desktop

> Copiá **todo lo que está debajo del separador `---`** y pegalo como primer
> mensaje en Claude Desktop. Es autosuficiente: le da a Claude todo el
> contexto necesario para seguir sin ver el repo local.
>
> **Actualizado 2026-08-01**: la Fase 1 (feature 002) está CERRADA y
> mergeada a `develop` (PR #3). El trabajo actual es el feature 003 —
> Remates de Identidad Central (rama `003-identidad-remates-ui`).

---

Estoy trabajando en **IngenIA365ERP**, un ERP SaaS multi-tenant para
cooperativas colombianas (.NET 10, Clean Architecture, CQRS + MediatR,
Blazor Hybrid, SQL Server + MongoDB + Redis). La **Fase 1 — Identidad
Central Federada** (feature 002) ya está cerrada y mergeada a `develop`.
Ahora estoy en la rama `003-identidad-remates-ui` con el **feature 003 —
Remates de Identidad Central**: TenantSwitcher en el header con guardia de
cambios sin guardar, navegación por rol (cuenta / admin de empresa /
consola SaaS), recovery codes canjeables en el desafío MFA + regeneración,
sesión Web persistente ante F5 (sessionStorage solo-pestaña), adopción de
sesión al aceptar invitación sin re-login, consola de aprobaciones de MFA
reset con listado de pendientes, y la deuda de tests de integración del
002 (11 archivos sobre `CentralIdentityApiFixture` con Testcontainers).

El manual técnico de pruebas (15 pasos) ya se ejecutó **completo
end-to-end por curl**: US1 onboarding, US2 login central, Phase 4b
(password change/forgot/reset + MFA voluntario y forzado), US3
multi-empresa, US4 admin de empresa, US5 master admin y los background
jobs. La UI Web se probó parcialmente (login central, accept-invitation,
forgot-password). Guardá este contexto como fuente de verdad.

## Ubicación y stack

- Proyecto: `D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\`
- Rama activa: `002-identidad-central-federada`
- OS: Windows 11 + PowerShell + Git Bash + WSL2 Ubuntu
- Setup local **sin Docker** (Docker Compose disponible como alternativa):

| Servicio | Dónde corre | Puerto |
|---|---|---|
| SQL Server | Servicio Windows nativo | 1433 (Trusted_Connection) |
| MongoDB | Servicio Windows nativo | 27017 |
| Redis | WSL2 distro `Ubuntu` | 6379 |
| smtp4dev | `dotnet tool` global | 1025 SMTP, 8025 UI |
| API .NET 10 | `dotnet run` | 5100 |
| Web Blazor | `dotnet run --launch-profile http` | 5200 (login nuevo en `/security/login`) |

Comandos para arrancar el stack:

```powershell
# Redis
wsl -d Ubuntu -- sudo service redis-server start
# Si desde Windows da ConnectionReset aunque el PONG interno funcione:
# wsl --shutdown y volver a arrancar (relay de puertos WSL2 zombie)

# smtp4dev (en terminal aparte)
smtp4dev --smtpport 1025 --urls "http://localhost:8025"

# API (desde la raíz del repo)
cd "D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP"
dotnet run --project src/Presentation/IngenIA365ERP.API

# Web (perfil http — el cert self-signed de :7200 rompe automatización)
dotnet run --project src/Presentation/IngenIA365ERP.Web --launch-profile http
```

## Credenciales y datos del entorno (actualizados 2026-07-31)

| Actor | Datos |
|---|---|
| Master admin | `master@ingenia.dev` / `MasterDev2026!Strong` — Id `c3cc31c1-a855-43bb-aec8-c751cfff4526` |
| Ana (admin de ambas coops) | `ana.perez@coop.solidaria.test` / `Ana-After-Reset-2026` — Id `972d6f61-453c-4864-b7a9-db20df06fc0f` |
| Ana — MFA secret TOTP | `XVCTZVBYV7CTIHSVUVDWY6CO5SK2WEUB` (el login SIEMPRE pide TOTP) |
| Ana — recovery codes | `RYBK3-Y7MF3 NPF5W-CW32Y NT257-GFCJW 66RJQ-WFQYR KD78K-63C5V B2VMR-NYHT7 VJB4K-4WYPN 443W2-VQVMP 2KKBM-6BGH6 CHJ75-J693P` |
| Luis (miembro Solidaria) | `luis.martinez@coop.solidaria.test` / `Luis-Strong-Pwd-2026` — Id `619cc290-7c25-4ce3-96de-42a0f47781e6` — sin MFA |
| Carla (miembro Solidaria) | `carla.gomez@coop.solidaria.test` / `Carla-Strong-Pwd-2026` — Id `02b3c560-6cdb-490e-ad94-2a10bd1e6fe3` — sin MFA (el master se lo reseteó en la prueba 7.1) |
| Elena (creada por UI) | `elena.vega@coop.solidaria.test` / `Elena-Strong-Pwd-2026` — sin MFA |
| Gina (flujo gated) | `gina.torres@coop.solidaria.test` / `Gina-Strong-Pwd-2026` — MFA activo, secret `BAVZ5IJWACDNYQRWUR2FSHV6V4NSWVUZ` |
| Tenant 1 "Coop. Solidaria Dev" | `ea5aad63-f579-40b3-86b9-85d3bce6401d` — política MFA **ACTIVA** |
| Tenant 2 "Coop. del Pacifico Dev" | `da51829f-c5b2-45da-9145-215120d810b7` — política off — **default de Ana** |

> Luis/Carla/Elena sin MFA + política de Solidaria activa ⇒ login →
> `MfaEnrollmentRequired` → enrollment forzado (ya funciona).

## Flujo completado

| Bloque | Estado |
|---|---|
| US1 onboarding (invitación, preview, accept, replay 410) | ✅ |
| US2 login central (mono-tenant, lockout, FR-041) | ✅ |
| Phase 4b: change/forgot/reset password, MFA voluntario, política MFA + enforcement | ✅ |
| US3 multi-empresa: TenantSelection, select, switch, default | ✅ (+ auditoría Mongo) |
| US4 admin: miembros, suspender/reactivar, salvaguarda último admin, política MFA | ✅ |
| US5 master: force-MFA-reset con razón, validación | ✅ |
| Jobs: InvitationExpiry (~2 min) y PasswordResetCleanup (~5 min) | ✅ |
| UI: `/security/login` → MFA challenge, `/auth/accept-invitation`, `/auth/forgot-password` | ✅ |

## Bugs arreglados (NO revertir)

Sesión 2026-07-26: `AddCachingServices` habilitado; `AbortOnConnectFail=false`;
`IssueMasterOperationalAsync` en LoginCommandHandler; sección `Smtp` raíz con
host `127.0.0.1`; migraciones 18–23 + gaps 1–6 de esquema.

Sesión 2026-07-31:
1. **Seguridad**: `CentralRefreshSession` guarda `SecurityStamp` y el refresh
   lo compara — cambiar/resetear password o force-MFA-reset ahora invalida
   todos los refresh tokens (antes sobrevivían). Tests nuevos (6 casos).
2. `AcceptInvitationCommandHandler` persiste el refresh en Redis (antes
   devolvía un token muerto que fallaba al primer refresh).
3. Mapeo EF de `PasswordResetToken` registrado en `AdminDbContext` + Gap 7
   (`ADM_PasswordResetTokens.Id` BIGINT→INT) — arregla el 500 del forgot.
4. `TenantResolutionMiddleware`: exenciones `/api/profile/mfa/enroll|confirm`
   (el enrollment forzado era inalcanzable) + fallback solo-master que
   resuelve el tenant desde la ruta (el master no tiene claim de tenant).
5. Host Web registra los 6 clientes de identidad de Shared (el prerender
   daba 500 en todas las páginas Security).
6. Parsers de los clientes UI manejan **204 No Content** (antes mostraban
   `JsonException` cruda en pantalla).

## Pendientes

- ~~Cutover UI~~ → **HECHO (2026-08-01)**: `/login` sirve el login central
  (el viejo quedó en `/legacy-login`); el JWT central pasa a ser la sesión de
  la app (keys `auth_token`/`refresh_token` + notificación del auth state).
  Verificado E2E: login → MFA → dashboard sin rebote → Salir → login central.
- Secuelas del cutover: persistencia ante F5 (storage in-memory pierde
  sesión en reload); `PermissionGate` necesita claims/`GET /me` del tenant;
  el header muestra "Usuario" genérico; falta el TenantSwitcher (T091).
- Gaps formalizados en `26_Backfill_Gaps_Tenant.sql` + `26b_Backfill_Gaps_Admin.sql`;
  T127 (evidencia con capturas) y T118 (integration test con Testcontainers)
  **hechos**. T124 ejecutado: 30.000/30.000 OK a 100 RPS × 5 min, 0 fallos,
  p50=192ms, pero p95=2265ms > 800ms en la máquina dev — repetir en el VPS
  objetivo. Usuarios sintéticos `load.userNN@cooperativa.test` /
  `LoadTest-Pwd-2026` sembrados en la BD admin local.
- Hardening pendiente: fail-fast si `Keys/dev_private.pem` no resuelve (hoy
  cae en silencio a clave RSA aleatoria).
- Observaciones resueltas (2026-08-01): accept respeta MFA (challenge sin
  tokens cuando aplica, FR-003b/c); `otpauth://` con email; `GET /members`
  con email por miembro. En backlog: timing side-channel en forgot y la
  colección Mongo `audit_events_` (sufijo vacío) para eventos globales.

## Qué quiero que hagas ahora

**PEGÁ ACÁ ABAJO LO QUE QUERÉS QUE HAGA** (elegí uno o cambialo por lo que
necesites):

- [ ] Diseñar el cutover del login (puente JWT central ↔ auth del host Web).
- [ ] Portar los gaps 1–7 a `24_Backfill_Gaps.sql`.
- [ ] Armar la evidencia de release (T127) con screenshots.
- [ ] Otro: `<contá qué necesitás>`

Antes de tocar código, **pedime confirmación de qué archivos vas a leer y
qué vas a cambiar**. Después ejecutá. Si algún endpoint responde 500,
mostrame los primeros 10 renglones del stack para diagnosticar juntos —
no me devuelvas solo "falló, probá esto".
