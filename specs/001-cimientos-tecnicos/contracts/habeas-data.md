# Contract — Habeas Data (Ley 1581/2012)

**Base path**: `/api/compliance/habeas-data`
**Auth**: authenticated + permissions del módulo Compliance.

---

## GET `/api/compliance/habeas-data/policies`

**Permission**: `Compliance.HabeasData.View`

Lista versiones publicadas de la política de tratamiento del tenant.

**Response 200**

```json
{
  "items": [
    {
      "publicId": "Guid",
      "versionLabel": "2.1.0",
      "title": "Política de Tratamiento de Datos Personales",
      "effectiveFrom": "2026-01-01T00:00:00Z",
      "effectiveTo": null,
      "sha256": "...",
      "publishedAt": "...",
      "publishedBy": "username"
    }
  ]
}
```

---

## GET `/api/compliance/habeas-data/policies/{publicId}`

Detalle con el texto markdown completo.

---

## POST `/api/compliance/habeas-data/policies`

**Permission**: `Compliance.HabeasData.Publish`

Publica una nueva versión. La versión anterior queda con `EffectiveTo = now` automáticamente.

**Request body**

```json
{
  "versionLabel": "2.1.0",
  "title": "string",
  "bodyMarkdown": "string",
  "effectiveFrom": "..."
}
```

**Errors**

- `Compliance.HabeasData.VersionLabelDuplicate`
- `Compliance.HabeasData.EffectiveFromInPast` — no se permite retroactivo (la versión nueva inicia desde el momento de la publicación, no antes).

---

## POST `/api/compliance/habeas-data/consents`

**Permission**: `Compliance.HabeasData.Record`

Registra una aceptación.

**Request body**

```json
{
  "personPublicId": "Guid",
  "policyVersionPublicId": "Guid",
  "evidenceText": "string (texto exacto del consentimiento mostrado al titular)",
  "captureChannel": "WebForm | InPerson | Phone | Document",
  "ip": "string? (si web)",
  "userAgent": "string?"
}
```

**Response 201**

```json
{ "publicId": "Guid", "status": "Accepted", "acceptedAtUtc": "..." }
```

**Errors**

- `Compliance.HabeasData.PersonNotFound`
- `Compliance.HabeasData.PolicyVersionNotActive`

---

## POST `/api/compliance/habeas-data/revocations`

**Permission**: `Compliance.HabeasData.Record`

Registra una revocación.

**Request body**

```json
{
  "personPublicId": "Guid",
  "reason": "string? (<=500)"
}
```

**Errors**

- `Compliance.HabeasData.NoActiveConsent` — no hay consentimiento activo que revocar.

**Side effects**: publica `HabeasDataRevokedEvent` en MediatR; emite notificación al área de cumplimiento configurada.

---

## GET `/api/compliance/habeas-data/persons/{personPublicId}/history`

**Permission**: `Compliance.HabeasData.View`

Historial completo de aceptaciones y revocaciones del titular.

**Response 200**

```json
{
  "person": { "publicId": "Guid", "name": "..." },
  "currentStatus": "Accepted | Revoked | None",
  "currentPolicyVersion": "2.1.0 | null",
  "events": [
    {
      "publicId": "Guid",
      "status": "Accepted",
      "policyVersionLabel": "2.1.0",
      "occurredAtUtc": "...",
      "ip": "...",
      "evidenceText": "..."
    }
  ]
}
```
