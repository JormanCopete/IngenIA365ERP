# Contract — Attachments

**Base path**: `/api/attachments`
**Auth**: authenticated.
**Authorization model**: cada operación verifica el permiso de View/Update/Delete sobre la entidad propietaria (`OwnerEntityType` + `OwnerEntityId`).

---

## POST `/api/attachments`

Sube un archivo cifrado.

**Permission**: depende de `ownerEntityType` — debe coincidir con el permiso de modificación de esa entidad (p. ej. `Users.Update` si se adjunta a un usuario).

**Content-Type**: `multipart/form-data`.

**Form fields**

| Field | Notes |
|---|---|
| ownerEntityType | string. Tipo de la entidad. |
| ownerEntityPublicId | Guid. |
| file | binary. Máximo configurado por tenant. |

**Response 201**

```json
{
  "publicId": "Guid",
  "originalFileName": "cedula.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 245678,
  "sha256": "hex",
  "uploadedAt": "..."
}
```

**Errors**

- `Attachment.UnsupportedType`
- `Attachment.SizeExceeded`
- `Attachment.OwnerNotFound`
- `Attachment.PermissionDenied`

**Side effects**: cifra con DEK nueva (AES-256-GCM), encripta DEK con KEK actual (DataProtection), escribe en `IBlobStore`, persiste `COM_Attachments`, audit log `Create`.

---

## GET `/api/attachments/{publicId}`

Descarga el archivo descifrado. Responde con `Content-Type` original y `Content-Disposition: attachment; filename="..."`.

**Permission**: View sobre `ownerEntityType`.

**Side effects**: descifra, recalcula SHA-256 y valida; audit log `Download`. Falla si el hash no coincide → `Attachment.IntegrityCheckFailed`.

**Errors**

- `Attachment.NotFound`
- `Attachment.PermissionDenied`
- `Attachment.IntegrityCheckFailed`

---

## GET `/api/attachments/{publicId}/metadata`

Solo metadatos, sin descargar el binario. Permiso View sobre owner.

---

## DELETE `/api/attachments/{publicId}`

**Permission**: Delete sobre owner.

Soft-delete: `Status = SoftDeleted`, el archivo cifrado permanece en `IBlobStore` para auditoría hasta que el job de purga lo retire según política de retención.

**Body** (opcional): `{ "reason": "string?" }`.

**Side effects**: audit log `Delete`.
