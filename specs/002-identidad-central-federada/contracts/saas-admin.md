# Contract: SaaS Admin (master admin global)

**Módulo Carter**: `SaasAdminModule.cs`
**Rutas**: `/api/saas/*`
**Autorización**: `[RequireMasterAdmin]` en todos los endpoints (`is_global_master_admin == true` en el JWT).

---

## GET /api/saas/tenants

Catálogo global de tenants. Solo visible para master admin (FR-038).

**Query params**: `search`, `subscriptionStatus`, `page`, `pageSize`.

**Response** `200 OK`:

```json
{
  "items": [
    {
      "tenantPublicId": "...",
      "tenantName": "Coop. Solidaria",
      "nit": "900123456-7",
      "subscriptionStatus": "Active",
      "subscriptionExpiresAt": "...",
      "activeMembersCount": 23,
      "mfaPolicyEnabled": false,
      "tenantAdminsCount": 2
    }
  ],
  "totalCount": 42
}
```

---

## POST /api/saas/tenants

Crea un nuevo tenant (FR-037).

**Request**:

```json
{
  "tenantName": "Coop. Nueva",
  "nit": "901234567-8",
  "subscriptionPlanCode": "PRO",
  "firstAdminEmail": "admin@coopnueva.coop"
}
```

**Response** `201 Created`:

```json
{
  "tenantPublicId": "...",
  "tenantName": "Coop. Nueva",
  "invitation": {
    "invitationPublicId": "...",
    "email": "admin@coopnueva.coop",
    "expiresAt": "..."
  }
}
```

**Side effects**:
- Crea el tenant en `ADM_Tenants` y aprovisiona su schema/BD por tenant (proceso ya existente en Fase 0).
- Emite automáticamente una invitación marcada `inviteAsTenantAdmin = true` para el email indicado.

**Auditoría**: `Tenant.Created` + `Invitation.Issued.ByMaster`.

---

## POST /api/saas/users/{centralUserPublicId}/force-mfa-reset

Recupera el MFA de un usuario (caso operativo cuando pierde su dispositivo TOTP). Procedimiento operativo justificado por la spec (Edge Case "Pérdida del segundo factor" + assumption "Recuperación de segundo factor").

**Request**: `{ "reason": "Solicitud telefónica verificada" }`.

**Response**: `204 No Content`.

**Side effects**:
- Limpia `MfaSecret` y pone `TwoFactorEnabled = false` en `ADM_CentralUsers`.
- Invalida todos los refresh tokens del usuario (regenerated security stamp).
- En el siguiente login, si alguna de las empresas del usuario tiene política "MFA obligatorio", se le forzará a re-configurarlo.

**Auditoría**: `CentralUser.MfaResetByMaster` con `actorUserId`, `targetUserId`, `reason`.
