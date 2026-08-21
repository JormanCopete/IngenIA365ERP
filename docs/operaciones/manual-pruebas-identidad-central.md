# Manual de Pruebas — Identidad Central (Feature 002)

> **Objetivo**: guía paso a paso, copy-paste, para probar end-to-end los flujos
> de la identidad central federada (US1 onboarding, US2 login, Phase 4b
> recuperación, US3 multi-empresa, US4 admin de empresa, US5 master admin).
>
> **Audiencia**: QA, dev senior validando integraciones, primer onboarding
> técnico al producto.
>
> **Pre-requisito**: branch `002-identidad-central-federada` checked out
> (no `main`). Stack docker accesible.

---

## 0. Glosario rápido

| Término | Significado |
|---------|-------------|
| **Identidad central** | El registro único del usuario en `ADM_CentralUsers` (BD `IngenIA365ERP_Admin`). Una persona = una identidad, sin importar cuántas cooperativas tenga acceso. |
| **Tenant** | Cooperativa-cliente del SaaS. Fila en `ADM_Tenants`. |
| **Membresía** | Relación N:N CentralUser ↔ Tenant (`ADM_TenantMemberships`). Estados: `Invited`, `Active`, `Suspended`, `Revoked`. |
| **Master admin** | Usuario con flag `IsGlobalMasterAdmin=true`. Opera sobre todos los tenants. |
| **Tenant admin** | Membresía con `IsTenantAdmin=true`. Opera solo en su empresa. |
| **JWT `purpose`** | Claim que acota qué endpoints acepta el token: `full`, `mfa-verify`, `mfa-enroll`, `tenant-select`, `password-reset`. |

---

## 1. Preparación del entorno

### 1.1 Levantar el stack

```powershell
# Working dir: D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\

# Levantar SQL Server, MongoDB, Redis y MailHog
docker compose up -d sqlserver mongodb redis mailhog
```

**Verificar**: `docker ps` debe mostrar los 4 contenedores en estado `Up`. La
UI de MailHog estará en `http://localhost:8025`.

> **Ningún correo sale a internet durante estas pruebas, y así debe ser.** El
> destino por defecto en desarrollo es **smtp4dev** (`docker-compose.dev.yml`,
> misma bandeja en **http://localhost:8025**); MailHog, en el `docker-compose.yml`
> clásico, hace lo mismo. Ambos **capturan** el mensaje y **no lo entregan**: si
> te quedaste esperando el correo en tu buzón real, no hay nada roto — abrí
> `http://localhost:8025`. Enviar de verdad se documenta en
> [`correo-saliente.md`](correo-saliente.md).

### 1.2 Aplicar DDL admin (orden estricto)

```powershell
# Crear BD IngenIA365ERP_Admin si no existe
sqlcmd -S localhost -U sa -P "IngenIA365_Dev2026!" -C -No -Q "IF DB_ID('IngenIA365ERP_Admin') IS NULL CREATE DATABASE IngenIA365ERP_Admin"

# Aplicar scripts 15a → 15c → 15e (15d sólo en cutover; no para QA)
sqlcmd -S localhost -U sa -P "IngenIA365_Dev2026!" -C -No -d IngenIA365ERP_Admin -i database/schema/15a_Admin_CentralIdentity.sql
sqlcmd -S localhost -U sa -P "IngenIA365_Dev2026!" -C -No -d IngenIA365ERP_Admin -i database/schema/15b_Admin_Memberships_Invitations.sql
sqlcmd -S localhost -U sa -P "IngenIA365_Dev2026!" -C -No -d IngenIA365ERP_Admin -i database/schema/15c_Admin_MfaPolicy_LoginAttempts.sql
sqlcmd -S localhost -U sa -P "IngenIA365_Dev2026!" -C -No -d IngenIA365ERP_Admin -i database/schema/15e_Admin_PasswordResetTokens.sql
```

> **Alternativa EF**: si prefieres EF migrations, `dotnet ef database update
> --context AdminDbContext --project src/Infrastructure/IngenIA365ERP.Persistence
> --startup-project src/Presentation/IngenIA365ERP.API` aplica
> `InitialCentralIdentity`. Solo funciona si la BD `IngenIA365ERP_Admin` está
> virgen — sino hay conflicto con `ADM_Tenants` ya existente de Fase 0.

### 1.3 Bootstrap del audit log MongoDB

```powershell
$env:AUDIT_WRITER_PASSWORD='dev-writer'; $env:AUDIT_READER_PASSWORD='dev-reader'; dotnet run --project tools/IngenIA365ERP.DbMigrator -- audit-bootstrap --mongo-connection "mongodb://localhost:27017"
```

**Verificar (esperado tras 1ra corrida)**:
```
+ Roles:       audit_appendOnly, audit_readOnly
+ Users (new): audit_writer, audit_reader
Audit bootstrap completado. Cambios netos: 4
```

Re-ejecutándolo debería mostrar `Cambios netos: 0` salvo los users que se
"actualizan" siempre.

### 1.4 Seed master admin

Variables de entorno antes de arrancar la API por primera vez (define las
credenciales del master admin inicial):

```powershell
$env:MASTER_ADMIN_EMAIL = 'master@ingenia.dev'
$env:MASTER_ADMIN_PASSWORD = 'Master-Dev-2026-Long-Password'
```

> Si el seed legacy ya creó otro admin (`admin@solido.local`) y prefieres
> usarlo, ignora esto. El flujo central solo necesita un usuario con
> `IsGlobalMasterAdmin=true` en `ADM_CentralUsers`.

### 1.5 Arrancar la API

```powershell
dotnet run --project src/Presentation/IngenIA365ERP.API
```

Espera el log:

```
[INF] Now listening on: https://localhost:7200
[INF] Now listening on: http://localhost:5000
```

Verificaciones rápidas:

```powershell
# Health check
curl http://localhost:5000/health/live
# Esperado: 200 OK con { "status": "Healthy" }

# Swagger
# Abrir http://localhost:5000/swagger
```

### 1.6 Arrancar la Web (terminal aparte)

```powershell
dotnet run --project src/Presentation/IngenIA365ERP.Web
```

UI en `http://localhost:5200`.

---

## 2. US1 — Onboarding por invitación

### 2.1 Caso "master invita admin de empresa nueva"

#### Paso 1. Login del master admin

```powershell
$loginResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = $env:MASTER_ADMIN_EMAIL; password = $env:MASTER_ADMIN_PASSWORD } | ConvertTo-Json)

$loginResponse | ConvertTo-Json -Depth 5
$accessToken = $loginResponse.accessToken
```

**Esperado**: `challenge = "None"` + `accessToken` + `refreshToken`. Si tienes
MFA activado para el master, recibirás `challenge = "MfaRequired"` con
`challengeToken` y debes hacer:

```powershell
# Con MFA: continuar con /api/auth/mfa/verify
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/mfa/verify" `
  -Method POST -Headers @{ Authorization = "Bearer $($loginResponse.challengeToken)" } `
  -ContentType "application/json" `
  -Body (@{ code = "123456" } | ConvertTo-Json)
```

#### Paso 2. Master crea tenant + envía invitación admin (atomic)

```powershell
$tenantBody = @{
    name = "Coop. Solidaria Dev"
    schemaName = "tenant_coop_solidaria"
    nit = "900111222"
    legalName = "Cooperativa Solidaria SAS"
    contactEmail = "contacto@coop.solidaria.test"
    planType = "Basic"
    maxUsers = 50
    storageLimitMb = 5120
    firstAdminEmail = "ana.perez@coop.solidaria.test"
} | ConvertTo-Json

$tenantResp = Invoke-RestMethod -Uri "http://localhost:5000/api/saas/tenants/with-admin" `
  -Method POST -Headers @{ Authorization = "Bearer $accessToken" } `
  -ContentType "application/json" -Body $tenantBody

$tenantResp | ConvertTo-Json
```

**Esperado**: 200 OK con `tenantPublicId`, `invitationPublicId`,
`invitationExpiresAt`, `correoEnviado` y `motivoCorreoNoEnviado`.

> Mirá `correoEnviado` antes de seguir. El 200 confirma que la empresa y la
> invitación quedaron creadas — eso pasa igual con el SMTP caído —, pero
> **no** que el correo haya salido. Con `correoEnviado: false` no vas a
> encontrar nada en `http://localhost:8025`: `motivoCorreoNoEnviado` dice por
> qué, y la invitación se reenvía desde
> `/admin/tenants/{tenantPublicId}/invitaciones`.

#### Paso 3. Verificar correo en MailHog

Abre `http://localhost:8025`. Deberás ver UN correo:

- **To**: `ana.perez@coop.solidaria.test`
- **Subject**: `Invitación a Coop. Solidaria Dev — IngenIA365ERP`
- **Body**: enlace `http://localhost:5200/auth/accept-invitation?token=...`
  (la base sale de `IdentityEmail:BaseUrl`; si ves `https://localhost:7200`
  es que esa clave no está definida y rige el valor cableado por defecto)

Copia el `token=` de la URL del enlace. Lo necesitas para los siguientes pasos.

#### Paso 4. Preview de la invitación (sin consumir)

```powershell
$token = "<PEGAR_TOKEN_DEL_CORREO>"

Invoke-RestMethod -Uri "http://localhost:5000/api/invitations/$([uri]::EscapeDataString($token))/preview" `
  -Method GET | ConvertTo-Json
```

**Esperado**:
```json
{
  "tenantPublicId": "<guid>",
  "tenantName": "Coop. Solidaria Dev",
  "email": "ana.perez@coop.solidaria.test",
  "isExistingCentralUser": false,
  "inviteAsTenantAdmin": true,
  "expiresAt": "<iso>",
  "isValid": true,
  "errorCode": null
}
```

#### Paso 5. Ana acepta la invitación (rama "nueva identidad")

```powershell
$acceptBody = @{
    token = $token
    registration = @{ password = "Ana-Strong-Pwd-2026" }
} | ConvertTo-Json

$acceptResp = Invoke-RestMethod -Uri "http://localhost:5000/api/invitations/accept" `
  -Method POST -ContentType "application/json" -Body $acceptBody

$acceptResp | ConvertTo-Json
```

**Esperado**: 200 OK con `accessToken`, `refreshToken`, `centralUserId`,
`activeTenantPublicId` = el publicId del tenant invitante,
`activeTenantName = "Coop. Solidaria Dev"`.

Ana ya tiene JWT operativo con `tenant_admin = true` (porque la invitación
era admin) y puede operar contra el tenant inmediatamente.

#### Paso 6. Verificar token consumido (replay debe fallar)

```powershell
# Segunda llamada con el MISMO token debe rechazar
Invoke-RestMethod -Uri "http://localhost:5000/api/invitations/accept" `
  -Method POST -ContentType "application/json" -Body $acceptBody
```

**Esperado**: `410 Gone` con `{ "code": "Invitation.AlreadyAccepted", ... }`.

### 2.2 Caso "tenant admin invita miembro regular"

```powershell
$inviteAna = @{ email = "luis.martinez@coop.solidaria.test" } | ConvertTo-Json

# Ana (que ya tiene accessToken del paso anterior) invita a Luis
$anaToken = $acceptResp.accessToken
$tenantId = $acceptResp.activeTenantPublicId

Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/invitations" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" } `
  -ContentType "application/json" -Body $inviteAna
```

**Esperado**: 200 OK + correo en MailHog para Luis. La invitación queda con
`inviteAsTenantAdmin=false` (solo master puede crear admins).

### 2.3 Caso "revoco la invitación a Luis antes de que la acepte"

```powershell
$listResp = Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/members" `
  -Headers @{ Authorization = "Bearer $anaToken" }
# Identifica el invitationPublicId de la última respuesta o de la BD

# Suponiendo $invitationPublicId obtenido
Invoke-RestMethod -Uri "http://localhost:5000/api/invitations/$invitationPublicId" `
  -Method DELETE -Headers @{ Authorization = "Bearer $anaToken" }
# Esperado: 204 No Content
```

Luis no podrá aceptar — preview devolverá `errorCode: "Invitation.Revoked"`.

---

## 3. US2 — Login centralizado

### 3.1 Login mono-tenant (Ana)

```powershell
$loginAna = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = "ana.perez@coop.solidaria.test"; password = "Ana-Strong-Pwd-2026" } | ConvertTo-Json)

$loginAna.challenge
# Esperado: "None" porque Ana solo está en Coop. Solidaria
$loginAna.autoSelected
# Esperado: true
$loginAna.activeTenantName
# Esperado: "Coop. Solidaria Dev"
```

### 3.2 Lockout progresivo (5 fallos consecutivos)

```powershell
1..5 | ForEach-Object {
    try {
        Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
          -Method POST -ContentType "application/json" `
          -Body (@{ email = "ana.perez@coop.solidaria.test"; password = "wrong-$_" } | ConvertTo-Json)
    } catch {
        Write-Host "Intento $_ — Status: $($_.Exception.Response.StatusCode)"
    }
}

# 6º intento — debería disparar lockout 60s
try {
    Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
      -Method POST -ContentType "application/json" `
      -Body (@{ email = "ana.perez@coop.solidaria.test"; password = "even-wrong" } | ConvertTo-Json)
} catch {
    $resp = $_ | ConvertFrom-Json
    Write-Host "Locked: $($resp.code) — $($resp.message)"
}
```

**Esperado**:
- Intentos 1-5: `401 Identity.InvalidCredentials`.
- Intento 6+: `423 Identity.Locked.Soft` con mensaje "Cuenta bloqueada
  temporalmente. Reintenta en 60 segundos."

Espera 60s, intenta con password correcto:

```powershell
Start-Sleep -Seconds 65
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = "ana.perez@coop.solidaria.test"; password = "Ana-Strong-Pwd-2026" } | ConvertTo-Json)
# Esperado: éxito + contador reseteado
```

### 3.3 Mensaje genérico ante email inexistente (FR-041)

```powershell
try {
    Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
      -Method POST -ContentType "application/json" `
      -Body (@{ email = "nadie@ningun-lado.test"; password = "anything" } | ConvertTo-Json)
} catch {
    $resp = $_ | ConvertFrom-Json
    $resp | ConvertTo-Json
}
```

**Esperado**: `401 Identity.InvalidCredentials` con mensaje
"Credenciales inválidas." — **idéntico** al de password incorrecta. La
respuesta no debe revelar si el email existe.

### 3.4 NoActiveMembership

Crea un CentralUser sin membresías (ejecuta solo el accept de US1 sin
provisión de membership; o usa un usuario al que se le revocaron todas las
membresías). Login:

```powershell
$resp = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" -Method POST `
  -ContentType "application/json" `
  -Body (@{ email = "sin.membresias@test.local"; password = "..." } | ConvertTo-Json)
$resp.challenge
# Esperado: "NoActiveMembership"
$resp.accessToken
# Esperado: null
```

UI: redirige a `/security/no-membership`.

---

## 4. Phase 4b — Profile & Recovery

### 4.1 Cambio de contraseña + invalidación de refresh tokens

Ana cambia su contraseña:

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/profile/password" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" } `
  -ContentType "application/json" `
  -Body (@{
      currentPassword = "Ana-Strong-Pwd-2026"
      newPassword = "Ana-Updated-2026-New"
  } | ConvertTo-Json)
# Esperado: 204 No Content
```

**Verificar en MailHog**: nuevo correo con asunto "Tu contraseña fue
cambiada" (incluye IP + UA del cambio).

**Verificar refresh invalidation**:
```powershell
# El refresh token anterior (loginAna.refreshToken) ya NO debe funcionar
try {
    Invoke-RestMethod -Uri "http://localhost:5000/api/auth/refresh" `
      -Method POST -ContentType "application/json" `
      -Body (@{ refreshToken = $loginAna.refreshToken } | ConvertTo-Json)
} catch {
    Write-Host "Refresh rejected: $($_.Exception.Response.StatusCode)"
}
# Esperado: 401 Identity.RefreshToken.Invalid
```

### 4.2 Forgot password (defensa anti-enumeración)

```powershell
# Email existente
$r1 = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/password/forgot" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = "ana.perez@coop.solidaria.test" } | ConvertTo-Json) -SkipHttpErrorCheck
$r1.StatusCode
# Esperado: 200 (Result.Success → traducido a 200; el spec dice 202 pero
# Carter mapea Result vacío a 204 No Content).

# Email INEXISTENTE — debe responder EXACTAMENTE igual
$r2 = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/password/forgot" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = "noexiste@test.test" } | ConvertTo-Json) -SkipHttpErrorCheck
$r2.StatusCode
# Esperado: idéntico al anterior (FR-041)
```

**Verificar MailHog**: solo llega 1 correo (al email existente). La respuesta
HTTP es indistinguible.

### 4.3 Reset password (consumir el token)

1. Toma el token del enlace que llegó a MailHog.
2. Aplica:

```powershell
$resetToken = "<TOKEN_DEL_ENLACE>"
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/password/reset" `
  -Method POST -ContentType "application/json" `
  -Body (@{ token = $resetToken; newPassword = "Ana-After-Reset-2026" } | ConvertTo-Json)
# Esperado: 204 No Content

# Segundo intento con el mismo token
try {
    Invoke-RestMethod -Uri "http://localhost:5000/api/auth/password/reset" `
      -Method POST -ContentType "application/json" `
      -Body (@{ token = $resetToken; newPassword = "another" } | ConvertTo-Json)
} catch {
    $err = $_ | ConvertFrom-Json
    $err.code
    # Esperado: "Profile.PasswordReset.AlreadyConsumed"
}
```

### 4.4 Activar MFA voluntario

```powershell
# Login fresco
$ana = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = "ana.perez@coop.solidaria.test"; password = "Ana-After-Reset-2026" } | ConvertTo-Json)
$anaToken = $ana.accessToken

# Comenzar enrollment
$mfa = Invoke-RestMethod -Uri "http://localhost:5000/api/profile/mfa/enroll" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" }

$mfa.secretBase32
$mfa.otpAuthUri
# Pega el secret en Google Authenticator / Authy / Microsoft Authenticator
# o decodifica el URI otpauth://totp/... como QR

# Confirmar con el primer código que muestre la app
Invoke-RestMethod -Uri "http://localhost:5000/api/profile/mfa/confirm" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" } `
  -ContentType "application/json" `
  -Body (@{ code = "123456" } | ConvertTo-Json)  # ← reemplaza con código real
# Esperado: 200 OK con recoveryCodes (10 códigos one-shot). GUARDA estos códigos.
```

Próximo login de Ana ya pasará por `MfaChallenge.razor`.

### 4.5 Activar política "MFA obligatorio" del tenant (admin de empresa)

```powershell
# Ana (admin) activa la política sobre Coop. Solidaria
Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/mfa-policy" `
  -Method PUT -Headers @{ Authorization = "Bearer $anaToken" } `
  -ContentType "application/json" `
  -Body (@{ isRequired = $true } | ConvertTo-Json)
```

Ahora Luis (sin MFA), si hace login, recibirá `challenge =
"MfaEnrollmentRequired"` y la UI lo redirigirá a `/auth/enroll-mfa-forced`.

---

## 5. US3 — Multi-empresa

### 5.1 Pre-setup

Repite §2.1 creando una segunda empresa **e invita a Ana** desde ese tenant:

```powershell
$master = <token master del paso 2.1.1>
$tenant2 = Invoke-RestMethod -Uri "http://localhost:5000/api/saas/tenants/with-admin" `
  -Method POST -Headers @{ Authorization = "Bearer $master" } `
  -ContentType "application/json" `
  -Body (@{
      name = "Coop. del Pacífico Dev"; schemaName = "tenant_pacifico"
      nit = "900333444"; legalName = "Coop. Pacífico SAS"
      contactEmail = "contact@pacifico.test"; planType = "Basic"
      maxUsers = 50; storageLimitMb = 5120
      firstAdminEmail = "ana.perez@coop.solidaria.test"  # Ana ya existe
  } | ConvertTo-Json)
```

Ana acepta esa nueva invitación con la rama `useActiveSession`:

```powershell
$secondToken = "<TOKEN_DE_LA_2DA_INVITACION>"
Invoke-RestMethod -Uri "http://localhost:5000/api/invitations/accept" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" } `
  -ContentType "application/json" `
  -Body (@{ token = $secondToken; useActiveSession = $true } | ConvertTo-Json)
```

Ahora Ana tiene **2 membresías activas**.

### 5.2 Login con TenantSelection

```powershell
$ana = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = "ana.perez@coop.solidaria.test"; password = "Ana-After-Reset-2026" } | ConvertTo-Json)

$ana.challenge
# Esperado: "TenantSelection"
$ana.activeTenants
# Esperado: array con las 2 cooperativas
```

UI: redirige a `/security/select-tenant` con los 2 tenants.

### 5.3 Seleccionar tenant con el challenge token

```powershell
$selResp = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/select-tenant" `
  -Method POST -Headers @{ Authorization = "Bearer $($ana.challengeToken)" } `
  -ContentType "application/json" `
  -Body (@{ tenantPublicId = $ana.activeTenants[0].tenantPublicId } | ConvertTo-Json)

$selResp.tenant
# Esperado: { tenantPublicId, tenantName }
```

### 5.4 Switch tenant en sesión activa

Ya autenticado en Coop. Solidaria, Ana se cambia a Coop. del Pacífico:

```powershell
$switch = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/switch-tenant" `
  -Method POST -Headers @{ Authorization = "Bearer $($selResp.accessToken)" } `
  -ContentType "application/json" `
  -Body (@{ tenantPublicId = $ana.activeTenants[1].tenantPublicId } | ConvertTo-Json)

$switch.tenant.tenantName
# Esperado: "Coop. del Pacífico Dev"
```

### 5.5 Default tenant para próximos logins

```powershell
# Ana fija Coop. del Pacífico como default
Invoke-RestMethod -Uri "http://localhost:5000/api/profile/default-tenant" `
  -Method PUT -Headers @{ Authorization = "Bearer $($switch.accessToken)" } `
  -ContentType "application/json" `
  -Body (@{ tenantPublicId = $ana.activeTenants[1].tenantPublicId } | ConvertTo-Json)
# Esperado: 204 No Content

# Próximo login:
$ana2 = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = "ana.perez@coop.solidaria.test"; password = "Ana-After-Reset-2026" } | ConvertTo-Json)

$ana2.challenge   # Esperado: "None"
$ana2.autoSelected  # Esperado: true
$ana2.activeTenantName  # Esperado: "Coop. del Pacífico Dev"
```

---

## 6. US4 — Admin de empresa

### 6.1 Listar miembros

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/members" `
  -Headers @{ Authorization = "Bearer $anaToken" } | ConvertTo-Json -Depth 5
```

### 6.2 Suspender → reactivar miembro

```powershell
$membershipId = "<PUBLIC_ID_DE_LA_MEMBRESIA_DE_LUIS>"

# Suspender
Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/members/$membershipId/suspend" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" }

# Luis intenta loguear → recibe challenge=NoActiveMembership o login fallido
# según si tenía otras membresías.

# Reactivar
Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/members/$membershipId/activate" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" }
```

### 6.3 Salvaguarda último admin

```powershell
# Ana es el único admin activo. Intenta degradarse:
try {
    Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/members/<ANA_MEMBERSHIP>/demote-admin" `
      -Method POST -Headers @{ Authorization = "Bearer $anaToken" }
} catch {
    $err = $_ | ConvertFrom-Json
    $err.code
    # Esperado: "Membership.SelfDemoteBlocked.LastAdmin"
}

# Solución: promover a Luis primero, luego degradar a Ana.
Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/members/<LUIS_MEMBERSHIP>/promote-admin" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" }

# Ahora sí puede degradarse Ana
Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/members/<ANA_MEMBERSHIP>/demote-admin" `
  -Method POST -Headers @{ Authorization = "Bearer $anaToken" }
# Esperado: 204
```

### 6.4 Política MFA obligatoria

```powershell
# Estado actual
Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/mfa-policy" `
  -Headers @{ Authorization = "Bearer $anaToken" }

# Activar
Invoke-RestMethod -Uri "http://localhost:5000/api/tenants/$tenantId/mfa-policy" `
  -Method PUT -Headers @{ Authorization = "Bearer $anaToken" } `
  -ContentType "application/json" `
  -Body (@{ isRequired = $true } | ConvertTo-Json)

# Ahora Luis (si no tiene MFA) intenta login y debe recibir
# challenge = "MfaEnrollmentRequired"
```

---

## 7. US5 — Master admin

### 7.1 Force MFA reset (recuperación operativa)

Caso: Luis perdió su teléfono. Llama al master por canal alterno y solicita
reset de MFA.

```powershell
$masterToken = "<MASTER_ACCESS_TOKEN>"
$luisCentralId = "<GUID_DE_CENTRAL_USER_DE_LUIS>"  # de la BD ADM_CentralUsers.Id

Invoke-RestMethod -Uri "http://localhost:5000/api/saas/users/$luisCentralId/force-mfa-reset" `
  -Method POST -Headers @{ Authorization = "Bearer $masterToken" } `
  -ContentType "application/json" `
  -Body (@{ reason = "Luis perdió el celular durante visita a sucursal Cali. Verificado por videollamada." } | ConvertTo-Json)
# Esperado: 204
```

**Efectos**:
- `ADM_CentralUsers.MfaSecret = NULL` y `TwoFactorEnabled = false` para Luis.
- `SecurityStamp` regenerado → sus refresh tokens activos quedan inválidos.
- Audit log con la razón (`CentralUser.MfaResetByMaster`).
- En su próximo login, si Coop. Solidaria tiene MFA policy activa, Luis irá
  a `/auth/enroll-mfa-forced`.

### 7.2 Razón obligatoria

```powershell
try {
    Invoke-RestMethod -Uri "http://localhost:5000/api/saas/users/$luisCentralId/force-mfa-reset" `
      -Method POST -Headers @{ Authorization = "Bearer $masterToken" } `
      -ContentType "application/json" `
      -Body (@{ reason = "test" } | ConvertTo-Json)
} catch {
    $err = $_ | ConvertFrom-Json
    $err.code
    # Esperado: "Validation.Invalid" (razón < 10 chars)
}
```

---

## 8. Background jobs (Phase 8)

### 8.1 InvitationExpiryJob

Estos jobs corren cada hora. Para forzar una expiración inmediata para
pruebas:

```sql
-- Crear invitación con ExpiresAt en el pasado
UPDATE ADM_Invitations
   SET ExpiresAt = DATEADD(MINUTE, -5, SYSUTCDATETIME())
 WHERE PublicId = '<invitation_publicid>'
   AND Status = 0;  -- 0 = Pending
```

Espera el siguiente tick del job (hasta 1h) o reinicia la API para que
arranque el job de inmediato (con un delay inicial de 2 min). Verifica:

```sql
SELECT PublicId, Status, ExpiresAt FROM ADM_Invitations WHERE PublicId = '<id>';
-- Esperado: Status = 2 (Expired)
```

Y en MongoDB `audit_events_*`:

```javascript
db.audit_events_template.find({ action: "Invitation.Expired" }).pretty()
```

### 8.2 PasswordResetTokenCleanupJob

Análogo. Para forzar:

```sql
UPDATE ADM_PasswordResetTokens
   SET ConsumedAt = DATEADD(DAY, -31, SYSUTCDATETIME())
 WHERE PublicId = '<token_publicid>';
```

Tras el siguiente tick (cada 6h), la fila desaparece de la tabla.

---

## 9. Verificaciones de audit log

Conéctate a Mongo:

```powershell
mongosh "mongodb://localhost:27017/IngenIA365ERP_Audit"
```

Queries útiles:

```javascript
// Logins por email
db.audit_events_template.find({ action: { $in: ["CentralUser.Login.Success", "CentralUser.Login.Failed"] } }).sort({ occurredAt: -1 }).limit(10)

// Eventos de un usuario específico
db.audit_events_template.find({ userId: "<centralUserId_hex>" }).sort({ occurredAt: -1 })

// Cambios de política MFA
db.audit_events_template.find({ action: /^TenantMfaPolicy\./ })

// Reset MFA por master
db.audit_events_template.find({ action: "CentralUser.MfaResetByMaster" })
```

---

## 10. JWT decoding

Inspecciona los claims de tus tokens:

1. Abre [https://jwt.io](https://jwt.io).
2. Pega el JWT.
3. Verifica:
   - `sub` = `centralUserId` (Guid sin guiones)
   - `email`
   - `is_global_master_admin`
   - `active_tenant_id` (sólo en `purpose=full`)
   - `tenant_admin` (sólo en `purpose=full`)
   - `purpose` ∈ {`full`, `mfa-verify`, `mfa-enroll`, `tenant-select`,
     `password-reset`}
   - `mfa_verified`
   - `exp`, `nbf`, `iat`, `jti`

---

## 11. Troubleshooting

| Síntoma | Causa probable | Solución |
|---------|----------------|----------|
| `401 Unauthorized` en login con credenciales correctas | `LoginAttemptCounter` lockout activo de pruebas previas | `redis-cli DEL login-locked:EMAIL` (uppercase del email) |
| `Tenant.NotFound` al consumir endpoints de tenant | Después de Chunk D, el middleware solo lee `active_tenant_id` del JWT | Verifica que el JWT tenga ese claim (no usar tokens emitidos antes de US2) |
| `Identity.WrongTokenPurpose` | Estás enviando un challenge token a un endpoint full (o viceversa) | Revisa el `purpose` claim del JWT y la ruta documentada |
| Plantillas de correo no se renderizan | `Templates/*.html` no copiado al output | Revisar `IngenIA365ERP.Storage.csproj` — debe tener `<None Update="Templates\*.html" CopyToOutputDirectory="PreserveNewest" />` |
| `Profile.Mfa.NoPendingEnrollment` al confirmar MFA | Pasaron > 10 min desde `BeginMfaEnrollment` | Reinicia con `POST /api/profile/mfa/enroll` |
| `Invitation.LockBusy` en accept | Dos clics simultáneos del enlace o lock zombie | Espera 30s y reintenta |
| No llega el correo a mi buzón real | Es lo esperado en local: smtp4dev/MailHog capturan y **no entregan** | Abrir la bandeja en `http://localhost:8025` |
| La bandeja de `http://localhost:8025` no recibe nada | SMTP host/puerto mal configurados | `Smtp__Host=localhost` + `Smtp__Port=1025` (la vieja `EmailSender__*` no la lee nadie) |
| El enlace del correo apunta a `localhost:7200` | Falta `IdentityEmail:BaseUrl` en la config del ambiente | Definir `IdentityEmail__BaseUrl` (dev: `http://localhost:5200`) |
| Jobs no procesan nada | API recién arrancada — delay inicial de 2 min en InvitationExpiry, 5 min en PasswordResetCleanup | Esperar o reiniciar para forzar el `delay + first tick` |
| `Membership.LastAdminProtected` al intentar revocar admin | Es la salvaguarda esperada (US4) | Promueve otro admin primero |
| Validation.Invalid en force-mfa-reset | Razón < 10 chars | Pasa una razón descriptiva (mínimo 10 chars) |

---

## 12. Quick reference de endpoints

| Endpoint | Método | Auth | Purpose JWT | Used by |
|----------|--------|------|-------------|---------|
| `/api/auth/login` | POST | Anon | — | Login form |
| `/api/auth/mfa/verify` | POST | Bearer | `mfa-verify` | MfaChallenge.razor |
| `/api/auth/refresh` | POST | Anon | — | Token rotation |
| `/api/auth/logout` | POST | Bearer | `full` | Logout button |
| `/api/auth/me` | GET | Bearer | `full` | Profile, TenantSwitcher |
| `/api/auth/password/forgot` | POST | Anon | — | ForgotPassword.razor |
| `/api/auth/password/reset` | POST | Anon | — | ResetPassword.razor |
| `/api/profile/mfa/enroll` | POST | Bearer | `full` o `mfa-enroll` | MfaEnrollmentCentral/Forced |
| `/api/profile/mfa/confirm` | POST | Bearer | `full` o `mfa-enroll` | idem |
| `/api/profile/mfa/disable` | POST | Bearer | `full` | MfaEnrollmentCentral |
| `/api/profile/password` | POST | Bearer | `full` | ChangePassword.razor |
| `/api/profile/default-tenant` | PUT | Bearer | `full` | DefaultTenantSetting.razor |
| `/api/sessions/active-tenants` | GET | Bearer | `full` | DefaultTenantSetting |
| `/api/sessions/select-tenant` | POST | Bearer | `tenant-select` o `full` | SelectTenant.razor |
| `/api/sessions/switch-tenant` | POST | Bearer | `full` | TenantSwitcher |
| `/api/invitations/{token}/preview` | GET | Anon | — | AcceptInvitation.razor |
| `/api/invitations/accept` | POST | Anon o Bearer | `full` (rama session) | idem |
| `/api/invitations/{publicId}` | DELETE | Bearer | `full` | Revoke action |
| `/api/tenants/{id}/invitations` | POST | Bearer | `full`+tenantAdmin | Tenant admin emite |
| `/api/saas/invitations` | POST | Bearer | `full`+master | Master emite |
| `/api/tenants/{id}/members` | GET | Bearer | `full`+admin | TenantMembers.razor |
| `/api/tenants/{id}/members/{publicId}/{action}` | POST | Bearer | `full`+admin | suspend/activate/revoke/promote-admin/demote-admin |
| `/api/tenants/{id}/mfa-policy` | GET/PUT | Bearer | `full`+admin | TenantMfaPolicyPage.razor |
| `/api/saas/tenants/with-admin` | POST | Bearer | `full`+master | MasterRegisterTenant.razor |
| `/api/saas/users/{id}/force-mfa-reset` | POST | Bearer | `full`+master | MasterMfaReset.razor |

---

## 13. Smoke test rápido (5 minutos)

Si solo tienes 5 min para verificar que el sistema está vivo:

```powershell
# 1. Health
curl http://localhost:5000/health/live

# 2. Login master
$m = Invoke-RestMethod -Uri "http://localhost:5000/api/auth/login" `
  -Method POST -ContentType "application/json" `
  -Body (@{ email = $env:MASTER_ADMIN_EMAIL; password = $env:MASTER_ADMIN_PASSWORD } | ConvertTo-Json)

# 3. Me
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/me" `
  -Headers @{ Authorization = "Bearer $($m.accessToken)" }

# 4. Refresh
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/refresh" `
  -Method POST -ContentType "application/json" `
  -Body (@{ refreshToken = $m.refreshToken } | ConvertTo-Json)

# 5. Logout
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/logout" `
  -Method POST -Headers @{ Authorization = "Bearer $($m.accessToken)" } `
  -ContentType "application/json" `
  -Body (@{ refreshToken = $m.refreshToken } | ConvertTo-Json)
```

Si los 5 pasos retornan 200/204 sin errores, el sistema está sano.

---

## 14. Limpieza para nueva sesión de pruebas

```sql
-- BORRAR INVITACIONES + MEMBERSHIPS + TENANTS DE PRUEBA (cuidado en prod!)
USE IngenIA365ERP_Admin;
DELETE FROM ADM_PasswordResetTokens;
DELETE FROM ADM_Invitations;
DELETE FROM ADM_TenantMemberships;
DELETE FROM ADM_TenantMfaPolicies;
DELETE FROM ADM_CentralUserLoginAttempts;
-- NO borrar ADM_CentralUsers ni ADM_Tenants si quieres conservar usuarios/tenants;
-- de lo contrario:
-- DELETE FROM ADM_Tenants WHERE Name LIKE '%Dev%';
-- (el master admin debe permanecer)
```

```powershell
# Resetear contadores Redis
redis-cli KEYS "login-locked:*" | redis-cli DEL
redis-cli KEYS "login-attempts:*" | redis-cli DEL
redis-cli KEYS "central-refresh:*" | redis-cli DEL
redis-cli KEYS "mfa-pending:*" | redis-cli DEL

# Limpiar audit log MongoDB (opcional)
mongosh --eval "db.getSiblingDB('IngenIA365ERP_Audit').audit_events_template.deleteMany({})"
```

---

## 15. Anexos

### 15.1 Crear usuarios sintéticos para load test

```sql
-- 50 usuarios load.user01..load.user50 con password "LoadTest-Pwd-2026"
-- (hash BCrypt cost 11 generado externamente)
DECLARE @i INT = 1;
WHILE @i <= 50
BEGIN
    INSERT INTO ADM_CentralUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                                  EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                                  TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
                                  IsGlobalMasterAdmin, Status, CreatedAt, IsDeleted)
    VALUES (NEWID(),
            CONCAT('load.user', RIGHT('0' + CAST(@i AS NVARCHAR(2)), 2), '@cooperativa.test'),
            UPPER(CONCAT('load.user', RIGHT('0' + CAST(@i AS NVARCHAR(2)), 2), '@cooperativa.test')),
            CONCAT('load.user', RIGHT('0' + CAST(@i AS NVARCHAR(2)), 2), '@cooperativa.test'),
            UPPER(CONCAT('load.user', RIGHT('0' + CAST(@i AS NVARCHAR(2)), 2), '@cooperativa.test')),
            1,
            '$2a$11$<HASH_BCRYPT_PRECOMPUTADO>',  -- LoadTest-Pwd-2026
            NEWID(), NEWID(),
            0, 0, 0, 0, 0,
            SYSUTCDATETIME(), 0);
    SET @i = @i + 1;
END
```

### 15.2 Decoder JWT en línea (sin internet)

```powershell
function Decode-Jwt {
    param([string]$Jwt)
    $parts = $Jwt.Split('.')
    $b64 = $parts[1].Replace('-', '+').Replace('_', '/')
    $b64 = $b64.PadRight((4 - ($b64.Length % 4)) % 4 + $b64.Length, '=')
    [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64)) | ConvertFrom-Json
}

Decode-Jwt $accessToken
```

---

**Fin del manual**. Reportar issues en `https://github.com/JormanCopete/IngenIA365ERP/issues`
con la etiqueta `feature-002-identidad`.
