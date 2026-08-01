# Phase 0 — Research: Identidad Central con Autorización Federada por Empresa

**Branch**: `002-identidad-central-federada` | **Date**: 2026-05-30

Este documento resuelve los unknowns derivados de `plan.md > Technical Context` y registra las decisiones tomadas con sus alternativas evaluadas.

---

## D-01: Mecanismo de identidad central

**Decision**: usar **ASP.NET Core Identity 10** (paquetes `Microsoft.AspNetCore.Identity.EntityFrameworkCore`) como base de la implementación de `ICentralIdentityProvider`. Crear una clase `CentralUserIdentity : IdentityUser<Guid>` en `IngenIA365ERP.Identity.Models` que actúa como bridge entre el framework y la entidad de dominio `CentralUser` (POCO en `IngenIA365ERP.Domain.Entities.Admin`). El `UserManager<CentralUserIdentity>` y `SignInManager<CentralUserIdentity>` se inyectan únicamente en la clase `AspNetCoreIdentityProvider` (Infrastructure). Application y Domain solo conocen `ICentralIdentityProvider`.

**Rationale**:
- Lo pide explícitamente la decisión arquitectónica del prompt original ("ASP.NET Core Identity + JWT").
- Cubre out-of-the-box: hashing, lockout, MFA TOTP, security stamp, email confirmation, password reset, claims, roles.
- Migración futura a Entra External ID se hará sustituyendo la implementación de `ICentralIdentityProvider` por un adapter que llame a MSAL, sin tocar Application ni Domain.
- Compatible con EF Core 10 y SQL Server (lo que ya usa el proyecto).

**Alternatives considered**:
- **Implementación custom 100% (extender el dominio existente)**: rechazada — re-implementar hashing seguro, lockout, security stamp, recovery codes, etc., es costoso y propenso a errores. La constitución no prohíbe ASP.NET Identity y la Fase 0 ya lo lista como dependencia base.
- **OpenIddict / IdentityServer**: rechazada para v1 — añade complejidad de servidor OAuth2/OIDC completo que no se necesita (solo somos cliente JWT). Útil si se monetiza identidad como servicio a terceros, fuera del alcance.
- **Auth0 / Clerk / SaaS de identidad**: rechazada — el producto va sobre VPS propia, sin dependencia obligatoria de SaaS externo. El prompt menciona Entra como migración futura opcional; la implementación inicial debe ser self-hosted.

---

## D-02: Hashing de contraseña en ASP.NET Identity

**Decision**: sustituir el `IPasswordHasher<CentralUserIdentity>` default (PBKDF2 con HMAC-SHA256, 100.000 iteraciones) por **`BcryptPasswordHasher` custom con BCrypt.Net-Next cost 11**.

**Rationale**:
- La constitución del proyecto (sección "Estándares Técnicos Adicionales") exige BCrypt cost ≥ 11.
- Fase 0 ya estandarizó BCrypt cost 11 para el `User` per-tenant que ahora se reemplaza.
- ASP.NET Identity permite sustituir el hasher vía `services.AddScoped<IPasswordHasher<CentralUserIdentity>, BcryptPasswordHasher>()`.
- BCrypt es ampliamente auditado, resistente a hardware especializado, y soporta migración de cost (el hasher reconoce hashes generados con cost inferior y los rehashea al verificar).

**Alternatives considered**:
- **Argon2id**: técnicamente superior pero la madurez del ecosistema .NET es inferior (libsodium-net, Konscious.Security.Cryptography), introduce dependencia nativa, y rompe alineación con Fase 0.
- **Mantener PBKDF2 default**: rechazada — contradice la constitución vigente.

---

## D-03: Estructura del JWT y emisión

**Decision**: JWT RS256 emitido por `CentralJwtIssuer` (Infrastructure/Identity/Services). Claims:

| Claim | Tipo | Origen |
|-------|------|--------|
| `sub` | Guid string | `CentralUser.Id` |
| `email` | string | `CentralUser.Email` |
| `is_global_master_admin` | bool | `CentralUser.IsGlobalMasterAdmin` |
| `active_tenant_id` | Guid string | TenantId seleccionado en esta sesión |
| `tenant_admin` | bool | `TenantMembership.IsTenantAdmin` para `active_tenant_id` |
| `mfa_verified` | bool | true si la sesión pasó MFA (o si el usuario no tiene MFA y ninguna empresa lo exige) |
| `iat`, `exp`, `nbf`, `jti` | estándar | Estándar JWT |

Access token TTL **30 min**; refresh token TTL **12 h** con rotation y family detection (mismo patrón que Fase 0). Cambio de empresa (`POST /api/sessions/switch-tenant`) re-emite **access + refresh** con el nuevo `active_tenant_id`. Cambio de contraseña invalida todos los refresh tokens del usuario (security stamp regenerated).

**Rationale**:
- Alineado con la decisión JWT existente de Fase 0 (mismas claves RS256, mismo issuer/audience).
- `active_tenant_id` en el token = tenant context auto-resoluble sin estado en servidor (consistente con principio IV pero sin header `X-Tenant-Id` manipulable).
- `mfa_verified` en el token permite enforcement por endpoint sin re-consultar el centro en cada request.

**Alternatives considered**:
- **Active tenant en cookie de sesión**: rechazada — el backend es API stateless; cookies suman complejidad sin valor (los Blazor clientes ya manejan refresh tokens).
- **Active tenant inferido del header en cada request**: rechazada — vuelve al modelo anterior y abre vector de ataque por header tampering.

---

## D-04: Validación contra contraseñas comprometidas (HaveIBeenPwned)

**Decision**: integrar **HaveIBeenPwned Pwned Passwords v3 API** vía k-anonymity (cliente envía solo los primeros 5 caracteres del hash SHA-1 de la contraseña; la API responde con todos los sufijos que matchean; el cliente compara localmente). Implementado en `PwnedPasswordService` (Infrastructure), llamado solo en `AcceptInvitationCommand` (registro) y `ChangePasswordCommand` (cambio). NEVER en `LoginCommand` (impacto en latencia y disponibilidad).

**Rationale**:
- API gratuita, sin API key, sin rate limit estricto (limitado por ToS razonable).
- K-anonymity garantiza que la contraseña nunca abandona el servidor del producto en plano.
- NIST SP 800-63B 5.1.1.2 recomienda explícitamente esta validación.

**Failure mode**: timeout 2 segundos en `HttpClient`. Si la API falla o tarda, log Warning con `Pwned.UnavailableFallback` y se permite la contraseña (fail-open). Justificación: la longitud mínima 12 ya ofrece base de seguridad; bloquear el registro/cambio por indisponibilidad de un servicio externo perjudica más al usuario que el riesgo marginal de aceptar una contraseña comprometida. Se monitorea la tasa de fallos para detectar degradación sistémica.

**Alternatives considered**:
- **Lista descargada offline de HIBP (~30 GB hash + count)**: rechazada para v1 — costoso en storage, requiere actualización periódica, y la API online cubre el caso de uso.
- **No validar contra listas externas**: rechazada — la spec exige FR-044 explícitamente.

---

## D-05: Token de invitación

**Decision**: 32 bytes aleatorios generados con `RandomNumberGenerator.GetBytes(32)` → codificados Base64Url (43 caracteres URL-safe). Solo el **hash SHA-256** del token se almacena en `ADM_Invitations.TokenHash`. La URL del correo es:
`https://app.ingenia365.com/auth/accept-invitation?token={base64url}`

Periodo de validez por defecto: **7 días naturales** desde la emisión (alineado con la spec). Configurable por master admin mediante `ADM_TenantSettings` en versiones futuras.

**Rationale**:
- 32 bytes = 256 bits de entropía — equivale a la fortaleza de una clave AES-256, criptográficamente seguro contra brute-force.
- Base64Url es seguro en URLs sin encoding adicional.
- Almacenar solo el hash protege contra leak de BD (un atacante con dump no puede aceptar invitaciones).

**Alternatives considered**:
- **JWT firmado como token de invitación**: rechazada — más pesado, requiere clave pública en la URL para verificar offline, y la rotación es más compleja. La opción simple (random + hash) es suficiente.
- **GUID v4 como token**: rechazada — solo ~122 bits de entropía, técnicamente suficiente pero menos defensivo a futuro.

---

## D-06: Concurrencia en aceptación de invitación

**Decision**: dos capas:
1. **Lock distribuido en Redis** (key `lock:invitation:{tokenHash}`, TTL 30s) al inicio del `AcceptInvitationCommandHandler`.
2. **UPDATE condicional con `RowVersion`** sobre `ADM_Invitations` al cambiar el estado a `Accepted`. Si `RowVersion` cambió → `DbUpdateConcurrencyException` → handler retorna `Invitation.AlreadyAccepted`.

**Rationale**:
- Lock previene el caso común (dos clicks rápidos del mismo usuario, dos pestañas) sin tocar BD.
- UPDATE condicional cubre el caso edge (Redis caído o lock expirado entre la lectura y el write).
- Alineado con el patrón optimistic concurrency ya establecido en Fase 0.

**Alternatives considered**:
- **Solo lock Redis**: rechazada — Redis puede fallar; UPDATE condicional es la defensa final.
- **Solo UPDATE condicional**: rechazada — sufre el problema "read uncommitted dirty" en escenarios de alta concurrencia; el lock reduce drásticamente la probabilidad de hit.

---

## D-07: Cache de membresías activas

**Decision**: cache en Redis bajo key `memberships:{centralUserId}` con TTL **60 segundos**. Valor: JSON con lista de `{ tenantId, tenantName, isTenantAdmin, isMfaRequired }`. Invalidación explícita publicando un evento `MembershipChanged(centralUserId)` desde los handlers que modifican `TenantMembership` (Activate/Suspend/Revoke/Promote/Demote) y `TenantMfaPolicy` (Update).

**Rationale**:
- En el flujo de login y cada vez que se valida el `active_tenant_id` de un JWT, hay que conocer las membresías. Round-trip a SQL en cada request es caro.
- TTL 60s es un compromiso aceptable: el efecto máximo de un cambio (revocación, suspensión) tarda como mucho 60 segundos en propagarse desde otros servidores que no recibieron el `MembershipChanged`.
- La invalidación explícita reduce ese delay a inmediato cuando el cambio ocurre en el mismo servidor.

**Alternatives considered**:
- **TTL 5 minutos**: rechazada — permite ventanas largas donde un usuario revocado sigue operando.
- **Sin cache**: rechazada — round-trip SQL en cada petición es costo inaceptable a 100 concurrent users.
- **In-memory cache local**: rechazada — multi-instancia (Docker scale) no compartiría invalidaciones, generando inconsistencias.

---

## D-08: Política MFA por tenant — enforcement multi-empresa

**Decision**: durante el flujo de login, el `LoginCommandHandler` consulta TODAS las membresías activas del usuario y mira en `ADM_TenantMfaPolicies` cuáles tienen `IsRequired = true`. Si AL MENOS UNA empresa exige MFA y el usuario aún no tiene MFA configurado → el handler retorna `Identity.MfaConfigurationRequired` con un challenge token temporal (5 min), redirigiendo al flujo de enrollment forzado antes de emitir el JWT.

**Rationale**:
- La credencial es central (única). Si un usuario tiene MFA "off" pero pertenece a una empresa que la exige, no puede haber estados inconsistentes (no se puede tener MFA solo para algunas empresas — FR-003c lo declara explícitamente).
- El enrollment forzado bloquea el acceso al ERP hasta que el usuario configure MFA, pero NO bloquea el login en sí — el usuario es identificado, solo no recibe JWT operativo hasta completar el setup.

**Alternatives considered**:
- **Bloquear el acceso solo a las empresas que requieren MFA, permitiendo entrar a las que no**: rechazada — la spec dice explícitamente que la credencial es única; aplicar políticas diferenciadas por empresa contradice ese modelo y abre la puerta a "logueado con MFA débil en Empresa A, brincando a Empresa B que requería MFA".
- **Forzar MFA platform-wide siempre**: rechazada — el usuario eligió la opción B en Q3, no la D.

---

## D-09: Salvaguarda "último admin de empresa" (FR-040)

**Decision**: implementada en dos handlers:
- `DemoteFromTenantAdminCommandHandler`: antes de degradar a un admin, ejecuta
  `SELECT COUNT(*) FROM ADM_TenantMemberships WHERE TenantId = @t AND IsTenantAdmin = 1 AND Status = 'Active' AND Id != @affected`.
  Si el resultado es 0 → retornar `Membership.LastAdminProtected` sin escribir.
- `RevokeMembershipCommandHandler`: misma query previa al cambio de estado, ANTES de tocar la membresía.

Auditoría: el intento bloqueado se registra como `Membership.DemotionRejected.LastAdmin` con razón explícita.

**Rationale**:
- El check tiene que correr DENTRO de la misma transacción que el UPDATE para evitar TOCTOU (otro admin se degrada al mismo tiempo y ambas operaciones pasan el check).
- El master admin **NO se ve afectado por este check** cuando opera sobre un tenant (puede dejar un tenant sin admin, pero entonces él mismo asume responsabilidad — el guardrail aplica solo a admins de la misma empresa actuando contra sí mismos o sus pares).

**Decisión adicional**: cuando el último admin se autoexcluye (caso edge), el handler también lo rechaza explícitamente con `Membership.SelfRevokeBlocked.LastAdmin`.

**Alternatives considered**:
- **Permitir el último admin degradarse pero emitir alerta al master**: rechazada — deja al tenant en estado inoperante hasta que el master reaccione. La salvaguarda preventiva es estrictamente mejor.

---

## D-10: Proveedor de email v1

**Decision**: SMTP propio vía MailKit 4.x (ya presente en `IngenIA365ERP.Storage`). Se promueve la interfaz `IEmailSender` desde Storage al proyecto Application (`IngenIA365ERP.Application.Common.Abstractions.IEmailSender`) — Storage queda como una de varias implementaciones posibles. Plantillas HTML almacenadas en `IngenIA365ERP.Storage/Templates/` (en español):
- `InvitationEmail.html` — invitación con enlace + nombre de empresa + emisor.
- `PasswordResetEmail.html` — reset estándar (preparado para uso futuro de "olvidé mi contraseña").

Configuración SMTP en `appsettings.json` con sección `EmailSender:Smtp:{Host, Port, Username, Password, FromAddress, FromName, EnableSsl}`. Credenciales SMTP nunca en código.

**Rationale**:
- El producto se despliega en VPS, donde un MTA local o un relay SMTP autenticado es lo natural.
- Mantener `IEmailSender` desacoplada permite intercambiar a Azure Communication / SendGrid sin tocar handlers (la clarification Q5 dejó esto como decisión confirmada).
- MailKit es production-grade, soporta TLS, OAuth, reintentos.

**Alternatives considered**:
- **Azure Communication Services / SendGrid en v1**: rechazada por la clarification Q5 (solo SMTP en v1).
- **Mantener `IEmailSender` en Storage**: rechazada por principio II — si Application necesita inyectar la abstracción, debe definirla Application (no depender de Infrastructure).

---

## D-11: Bloqueo progresivo anti fuerza bruta

**Decision**: contador en Redis por `email-normalized` (NO por IP — evita falsos positivos por NAT y permite defensa contra atacante distribuido):
- 5 fallos consecutivos → bloqueo 1 min, código `Identity.Locked.Soft`.
- 10 fallos → 5 min.
- 15 fallos → 15 min.
- 20+ fallos → 60 min, requiere reset administrativo (master admin desde consola SaaS).

Cada login exitoso resetea el contador. Cada bloqueo se registra en `ADM_CentralUserLoginAttempts` (append-only) con timestamp, IP, User-Agent, resultado.

**Rationale**:
- Escalado progresivo desincentiva ataques sin penalizar al usuario legítimo que olvida la contraseña una vez.
- Contar por email evita que un atacante con IPs diversas evada el bloqueo.
- Reset administrativo a las 20+ fallos protege contra atacante persistente sin que el usuario legítimo quede bloqueado indefinidamente.

**Alternatives considered**:
- **CAPTCHA tras N fallos**: rechazada para v1 — añade dependencia externa (Cloudflare Turnstile, Google reCAPTCHA). Se puede agregar en v2 si los datos de producción muestran que el lockout no es suficiente.
- **Bloqueo por IP**: rechazada — falsos positivos masivos (oficinas con NAT compartida).

---

## D-12: Estrategia de migración (instalación nueva)

**Decision**: la spec declara explícitamente "Sin migración" — el script `15d_Tenant_Users_Refactor.sql` es **destructivo** sobre `SEC_Users` (drop de columnas: `PasswordHash`, `PasswordSalt`, `MfaSecret`, `MustChangePassword`, `FailedLoginAttempts`, `LockoutEndAt`, `IsEmailVerified`, `LegacyLogin`) y se documenta en su header como "**REQUIERE BD VIRGEN — irreversible sin restore**". El script `16_Seed_Default_GlobalMasterAdmin.sql` crea idempotentemente un master admin con email y contraseña inicial desde variables de entorno (`MASTER_ADMIN_EMAIL`, `MASTER_ADMIN_PASSWORD`), forzando cambio de contraseña en el primer login.

**Rationale**:
- Cumple la assumption "Sin migración de usuarios" del spec (instalación nueva, sin migración de datos legacy).
- El header de la migración con advertencia explícita protege contra ejecución accidental sobre BD productiva con datos.
- El seed idempotente del master admin permite bootstrap del sistema desde cero sin intervención manual de BD.

**Alternatives considered**:
- **Script de migración que mueva contraseñas legacy a CentralUser**: rechazada por la spec — "no generar scripts ni lógica de migración".
- **Bootstrap del master admin desde la API en el primer arranque**: rechazada — acopla código de producción a un caso de uso de instalación que solo se ejecuta una vez.

---

## Resumen de unknowns resueltos

| Unknown | Resolución |
|---------|-----------|
| Mecanismo de identidad central | ASP.NET Core Identity + custom Bcrypt hasher (D-01, D-02) |
| Estructura JWT + claims | RS256, claims `central_user_id` + `active_tenant_id` + `is_global_master_admin` + `tenant_admin` + `mfa_verified` (D-03) |
| Validación de contraseñas comprometidas | HaveIBeenPwned k-anonymity, solo en registro/cambio, fail-open (D-04) |
| Token de invitación | 32 bytes random + Base64Url + hash SHA-256 (D-05) |
| Concurrencia en aceptación | Lock Redis + UPDATE condicional con RowVersion (D-06) |
| Cache de membresías | Redis TTL 60s + invalidación explícita (D-07) |
| Enforcement MFA multi-empresa | Si AL MENOS UNA empresa lo exige → MFA global (D-08) |
| Salvaguarda último admin | Check transaccional pre-update en degrade/revoke handlers (D-09) |
| Email provider v1 | SMTP propio vía MailKit, `IEmailSender` promovida a Application (D-10) |
| Bloqueo progresivo | Por email-normalizado en Redis: 5→1min, 10→5min, 15→15min, 20+→60min (D-11) |
| Migración de datos | No hay — instalación nueva, master admin seed idempotente desde env vars (D-12) |

Cero unknowns restantes. Listo para Phase 1.
