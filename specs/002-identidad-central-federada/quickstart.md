# Quickstart — Identidad Central con Autorización Federada por Empresa

**Branch**: `002-identidad-central-federada` | **Date**: 2026-05-30

Esta guía recorre los **5 user stories del spec** sobre un entorno de desarrollo limpio. Cubre los criterios de aceptación y los success criteria SC-001 a SC-012 de manera reproducible.

---

## Prerrequisitos

```bash
# 1. Branch y restore
git checkout 002-identidad-central-federada
dotnet restore IngenIA365ERP.sln

# 2. Levantar dependencias de infraestructura
docker compose up -d sqlserver mongodb redis mailhog

# 3. Aplicar migraciones de la fase
dotnet ef database update --project src/Infrastructure/IngenIA365ERP.Persistence --context AdminDbContext
sqlcmd -i database/schema/15a_Admin_CentralIdentity.sql
sqlcmd -i database/schema/15b_Admin_Memberships_Invitations.sql
sqlcmd -i database/schema/15c_Admin_MfaPolicy_LoginAttempts.sql
sqlcmd -i database/schema/15d_Tenant_Users_Refactor.sql
sqlcmd -i database/migration/16_Seed_Default_GlobalMasterAdmin.sql

# 4. Variables de entorno mínimas (.env.dev)
export MASTER_ADMIN_EMAIL="master@ingenia365.test"
export MASTER_ADMIN_PASSWORD="MasterDevPwd2026!Strong"
export EMAIL_SENDER_SMTP_HOST="localhost"
export EMAIL_SENDER_SMTP_PORT="1025"   # MailHog
export EMAIL_SENDER_FROM="no-reply@ingenia365.test"
export PWNED_PASSWORD_BASE_URL="https://api.pwnedpasswords.com"

# 5. Arrancar
dotnet run --project src/Presentation/IngenIA365ERP.API
# UI Blazor:  https://localhost:5001
# API:        https://localhost:5001/api
# MailHog:    http://localhost:8025
```

---

## US1 — Onboarding mediante invitación (P1)

Cubre acceptance scenarios 1-4 de US1, SC-004, SC-007, SC-008, SC-009, SC-010.

### Caso 1.1 — Usuario nuevo (correo no registrado)

```bash
# (a) Master admin invita a un nuevo administrador de empresa
curl -X POST https://localhost:5001/api/saas/invitations \
  -H "Authorization: Bearer $MASTER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "ana.perez@coopprueba.test",
    "tenantPublicId": "TENANT_PUBLIC_ID",
    "inviteAsTenantAdmin": true
  }'
# → 201 Created con invitationPublicId

# (b) Verificar correo en MailHog: http://localhost:8025
#    Debe llegar correo "Invitación a Coop. Prueba" con enlace
#    https://app.ingenia365.com/auth/accept-invitation?token=XXXX
# (en dev el dominio se mapea a localhost:5001)

# (c) Preview del token (UI lo hace al abrir el enlace)
curl https://localhost:5001/api/invitations/$TOKEN/preview
# → 200 OK { isValid: true, isExistingCentralUser: false, ... }

# (d) Aceptar (caso nuevo)
curl -X POST https://localhost:5001/api/invitations/accept \
  -H "Content-Type: application/json" \
  -d '{
    "token": "'$TOKEN'",
    "registration": {
      "password": "M1Cl4v3Seg|ra2026",
      "displayName": "Ana Pérez"
    }
  }'
# → 200 OK con accessToken (active_tenant_id ya seteado al tenant invitante)
```

Verificaciones SQL:

```sql
-- CentralUser fue creado
SELECT Id, NormalizedEmail, EmailConfirmed, Status FROM ADM_CentralUsers
WHERE NormalizedEmail = 'ANA.PEREZ@COOPPRUEBA.TEST';

-- TenantMembership Active con IsTenantAdmin = 1
SELECT Status, IsTenantAdmin FROM ADM_TenantMemberships
WHERE CentralUserId = '<id>' AND TenantId = '<tenant>';

-- Invitación marcada Accepted
SELECT Status, AcceptedAt FROM ADM_Invitations WHERE PublicId = '<inv>';

-- Auditoría
db.audit_events.find({ "eventType": { $in: ["Invitation.Issued.ByMaster", "Invitation.Accepted", "Membership.Activated"] } });
```

### Caso 1.2 — Email ya registrado por invitación previa

Otra empresa invita al mismo email. Al abrir el enlace, el preview devuelve `isExistingCentralUser: true`. La UI presenta el formulario de "iniciar sesión para confirmar". El POST `/accept` usa `existingCredentials.password`. Se reutiliza el `CentralUser` y se crea una **segunda** `TenantMembership`. Verificar:

```sql
SELECT COUNT(*) FROM ADM_TenantMemberships WHERE CentralUserId = '<id>';
-- Debe devolver 2.
```

### Caso 1.3 — Invitación expirada

Manipular `ExpiresAt` al pasado y reintentar accept → `410 Gone Invitation.Expired`. Auditoría registra `Invitation.Expired` cuando el job de limpieza corre (o cuando un cliente intenta abrirla).

### Caso 1.4 — Reuso de invitación aceptada

Reenviar el mismo token tras una aceptación exitosa → `410 Gone Invitation.AlreadyAccepted`.

**SC-009** verificado: cero invitaciones aceptadas más de una vez.

---

## US2 — Login centralizado sin combo box (P1)

Cubre acceptance scenarios 1-5 de US2, SC-001, SC-002, SC-006.

### Caso 2.1 — Usuario con 1 membresía activa: entrada directa

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"ana.perez@coopprueba.test","password":"M1Cl4v3Seg|ra2026"}'
# → 200 OK con autoSelected: true, accessToken, refreshToken
```

Verificar JWT: decode → `active_tenant_id` está poblado, `tenant_admin: true`.

UI Blazor (`https://localhost:5001`): al hacer login no aparece combo box ni selector — directo al dashboard del tenant. **SC-001** verificado visualmente.

Cronometrar: end-to-end < 5 s desde click submit hasta render del dashboard. **SC-002** verificado.

### Caso 2.2 — Usuario sin membresías activas

Suspender la membresía y reintentar login:

```bash
curl -X POST https://localhost:5001/api/tenants/$TENANT/members/$MEMBERSHIP/revoke \
  -H "Authorization: Bearer $MASTER_TOKEN" -d '{"reason":"test"}'

curl -X POST https://localhost:5001/api/auth/login -d '{...}'
# → 200 OK con challenge: "NoActiveMembership"
```

UI: pantalla `NoMembershipNotice.razor` con el mensaje "No tienes acceso a ninguna empresa. Solicita una invitación." Cero acceso al ERP. **FR-015** verificado.

### Caso 2.3 — Credenciales incorrectas

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -d '{"email":"ana.perez@coopprueba.test","password":"wrong"}'
# → 401 Unauthorized { "errorCode": "Identity.InvalidCredentials", "message": "Credenciales inválidas." }
```

El mismo mensaje genérico para email inexistente. **FR-041** verificado.

### Caso 2.4 — Login con MFA

Activar MFA en el perfil del usuario:

```bash
curl -X POST https://localhost:5001/api/profile/mfa/enroll -H "Authorization: Bearer $TOKEN"
# Retorna URL QR + secret

curl -X POST https://localhost:5001/api/profile/mfa/confirm \
  -H "Authorization: Bearer $TOKEN" -d '{"code":"123456"}'

# Logout y login otra vez
curl -X POST https://localhost:5001/api/auth/login -d '{...}'
# → 200 OK con challenge: "MfaRequired", challengeToken

curl -X POST https://localhost:5001/api/auth/mfa/verify \
  -d '{"challengeToken":"...","code":"654321"}'
# → 200 OK con accessToken/refreshToken
```

---

## US3 — Multi-empresa: selector y cambio en sesión (P2)

Cubre acceptance scenarios 1-5 de US3, SC-003, SC-005, SC-011.

### Setup

Aceptar invitaciones de tres tenants distintos para el mismo email → tres `TenantMembership` Active.

### Caso 3.1 — Selector al login

```bash
curl -X POST https://localhost:5001/api/auth/login -d '{...}'
# → 200 OK con activeTenants: [3 entries], autoSelected: false
```

UI: pantalla `SelectTenant.razor` lista exactamente las 3 empresas. **FR-014** verificado.

### Caso 3.2 — Cambio de empresa en sesión

Estando dentro de Tenant A:

```bash
curl -X POST https://localhost:5001/api/sessions/switch-tenant \
  -H "Authorization: Bearer $TOKEN_A" \
  -d '{"tenantPublicId":"TENANT_B_ID"}'
# → 200 OK con nuevo accessToken (active_tenant_id = TENANT_B_ID)
```

UI: clic en `TenantSwitcher.razor` del header → modal de confirmación si hay cambios sin guardar → cambio inmediato. Verificar que el menú, los permisos y los datos cargados ahora son del Tenant B. **FR-020, FR-022** verificados.

Cronometrar: < 3 s desde click hasta dashboard nuevo. **SC-003** verificado.

### Caso 3.3 — Empresa por defecto

```bash
curl -X PUT https://localhost:5001/api/profile/default-tenant \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"tenantPublicId":"TENANT_B_ID"}'
# → 204 No Content

# Logout + login
curl -X POST https://localhost:5001/api/auth/login -d '{...}'
# → 200 OK con autoSelected: true, accessToken con active_tenant_id = TENANT_B_ID
```

**FR-017** verificado.

### Caso 3.4 — Default tenant inválido tras revocación

Revocar la membresía con TENANT_B y reintentar login → la preferencia se descarta silenciosamente, se muestra selector con los 2 restantes. **FR-018** verificado.

### Caso 3.5 — Tenant crossover (seguridad)

Modificar manualmente el `active_tenant_id` del JWT a un tenant donde el usuario NO tiene membresía → API responde `403 Forbidden Membership.NotActive`. **SC-005, SC-006** verificados.

---

## US4 — Admin de empresa: gestión de usuarios (P2)

Cubre acceptance scenarios 1-4 de US4.

### Caso 4.1 — Invitar regular member

Como admin del Tenant A:

```bash
curl -X POST https://localhost:5001/api/tenants/$TENANT_A/invitations \
  -H "Authorization: Bearer $TENANT_A_ADMIN_TOKEN" \
  -d '{"email":"nuevo@coop.test","inviteAsTenantAdmin":false}'
# → 201 Created
```

### Caso 4.2 — Intento de invitar a otra empresa

```bash
curl -X POST https://localhost:5001/api/tenants/$TENANT_B/invitations \
  -H "Authorization: Bearer $TENANT_A_ADMIN_TOKEN" \
  -d '{...}'
# → 403 Forbidden  (RequireTenantAdmin filter)
```

### Caso 4.3 — Intento de invitar como master admin

```bash
curl -X POST https://localhost:5001/api/saas/invitations \
  -H "Authorization: Bearer $TENANT_A_ADMIN_TOKEN" \
  -d '{...}'
# → 403 Forbidden  (RequireMasterAdmin filter)
```

### Caso 4.4 — Promote / demote / last admin guard

Como admin del Tenant A (que tiene 2 admins: A1 y A2):

```bash
# Demote A2
curl -X POST https://localhost:5001/api/tenants/$TENANT_A/members/$A2_MEMBERSHIP/demote-admin \
  -H "Authorization: Bearer $A1_TOKEN"
# → 204 No Content

# Ahora A1 es el único. Intento de auto-degrade
curl -X POST https://localhost:5001/api/tenants/$TENANT_A/members/$A1_MEMBERSHIP/demote-admin \
  -H "Authorization: Bearer $A1_TOKEN"
# → 403 Forbidden Membership.SelfDemoteBlocked.LastAdmin
```

**FR-040, FR-040a, SC-012** verificados.

---

## US5 — Gobierno master admin (P3)

Cubre acceptance scenarios 1-4 de US5.

### Caso 5.1 — Crear tenant + invitar primer admin

```bash
curl -X POST https://localhost:5001/api/saas/tenants \
  -H "Authorization: Bearer $MASTER_TOKEN" \
  -d '{
    "tenantName":"Coop. Nueva",
    "nit":"901234567-8",
    "subscriptionPlanCode":"PRO",
    "firstAdminEmail":"admin@coopnueva.test"
  }'
# → 201 Created con tenantPublicId + invitation
```

El destinatario recibe correo, acepta, queda como admin del nuevo tenant.

### Caso 5.2 — Listado de tenants

```bash
curl https://localhost:5001/api/saas/tenants -H "Authorization: Bearer $MASTER_TOKEN"
# → 200 OK con lista completa
```

### Caso 5.3 — Master fuerza reset MFA

```bash
curl -X POST https://localhost:5001/api/saas/users/$CENTRAL_USER/force-mfa-reset \
  -H "Authorization: Bearer $MASTER_TOKEN" \
  -d '{"reason":"Usuario perdió dispositivo TOTP"}'
# → 204 No Content
```

Auditoría registra `CentralUser.MfaResetByMaster` con razón.

---

## Verificaciones cruzadas de Success Criteria

| SC | Cómo se verifica en este quickstart |
|----|-------------------------------------|
| SC-001 | Caso 2.1 — UI no muestra combo box |
| SC-002 | Caso 2.1 — cronometraje < 5 s |
| SC-003 | Caso 3.2 — cronometraje < 3 s |
| SC-004 | Caso 1.1 — alta solo vía invitación |
| SC-005 | Caso 3.5 — sin cross-tenant data |
| SC-006 | Caso 3.5 — JWT manipulation rechazado |
| SC-007 | MongoDB `audit_events` queries en cada caso |
| SC-008 | Métrica de adopción medida tras lanzamiento (no en quickstart) |
| SC-009 | Caso 1.4 — replay rechazado |
| SC-010 | Caso 1.1 — cronometraje invitación → acceso |
| SC-011 | Caso 3.2 — auditoría de `Session.TenantSwitched` |
| SC-012 | Caso 4.4 — last admin guard |

---

## Smoke test automatizado

```bash
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~Identity"
# Debe pasar: EndToEnd_InviteRegisterLogin, EndToEnd_MultiTenantSwitch,
#             EndToEnd_MfaPolicyEnforcement, Security_TenantCrossover,
#             Security_InvitationReplay
```

```bash
dotnet test tests/IngenIA365ERP.Architecture.Tests
# Debe pasar: CentralIdentityArchTests, AdminDbContextArchTests
```

```bash
dotnet test tests/IngenIA365ERP.Load.Tests --filter "LoginThroughputScenario"
# NBomber: 100 login/seg × 5 min, p95 < 800 ms
```
