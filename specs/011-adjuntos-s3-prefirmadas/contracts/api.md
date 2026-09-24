# Contrato de la API: adjuntos directos (011)

**Fecha**: 2026-09-23 · Todas las rutas exigen sesión y cooperativa activa. Los errores usan el sobre
de siempre (`{ code, message, traceId, data? }`). Los enums entran por nombre o por número y salen como
número. Sólo viajan `PublicId` (Principio VI).

Cada ruta lleva el limitador de concurrencia `adjuntos` (R13): si se excede, **429**
`Attachments.Busy`, con el mensaje «Hay muchas transferencias en curso; intente de nuevo en unos
segundos».

## 1. Solicitar una subida

`POST /api/attachments/uploads` · permiso `Attachments.Upload`, más la regla del dueño (data-model) ·
comando `SolicitarSubidaDeAdjuntoCommand` (auditado)

```json
{
  "ownerEntityType": "AccountingDocument",
  "ownerEntityPublicId": "8f1c…",
  "fileName": "Factura 1234 – Ferretería Núñez.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 20481234,
  "sha256Base64": "n4bQgYhMfWWaL+qgxVrQFaO/TxsrC4Is0V1sFbDwCgg="
}
```

**201**

```json
{
  "attachmentPublicId": "c2d4…",
  "upload": {
    "url": "https://ingenia365-erp-attachments.s3.amazonaws.com/",
    "method": "POST",
    "fields": { "key": "pdn/3/2026/09/9a…bin", "Content-Type": "application/pdf", "x-amz-checksum-sha256": "n4bQ…", "policy": "…", "x-amz-algorithm": "AWS4-HMAC-SHA256", "x-amz-credential": "…", "x-amz-date": "…", "x-amz-security-token": "…", "x-amz-signature": "…" },
    "fileField": "file",
    "expiresAt": "2026-09-23T15:05:00Z"
  },
  "maxBytes": 26214400
}
```

El cliente arma un `multipart/form-data` con **todos** los `fields`, en el orden recibido, y el archivo
**al final**, en `fileField`. Si el POST al almacén responde 204, el cliente llama a
[confirmar](#3-confirmar).

| Código | HTTP | Cuándo |
|---|---|---|
| `Validation.Attachments.MimeTypeNotAllowed` | 400 | el tipo no está en la lista blanca |
| `Validation.Attachments.FileTooLarge` | 400 | `sizeBytes` supera el máximo; `data: { maxBytes }` |
| `Validation.Attachments.FileEmpty` | 400 | `sizeBytes` = 0 |
| `Attachments.OwnerNotAllowed` | 422 | el tipo de dueño no admite subidas de personas: lo genera un módulo o no está habilitado |
| `Generic.NotFound` | 404 | el documento no existe en la cooperativa, **o** falta el permiso de escritura de su módulo (misma respuesta) |

Los `Validation.*` responden **400**: así los mapea `ErrorEnvelopeFilter` en toda la API. Esta tabla decía 422 hasta que la implementación lo contrastó con el filtro. Los tres los responde el **handler**, no el validador: un fallo del validador sale siempre como `Validation.Invalid`, y el código de la tabla no llegaría nunca al cliente (la misma convención que nómina). El validador sólo mira la forma: tipo de dueño e id presentes, nombre de hasta 500 caracteres y la huella como SHA-256 en base64 (44 caracteres).

## 2. Renovar la autorización de una subida incompleta

`POST /api/attachments/{attachmentPublicId}/upload-url` · `Attachments.Upload` · comando
`RenovarSubidaDeAdjuntoCommand`

Sólo sirve para `Uploading` con la autorización vencida, o para `Incomplete`. Emite una autorización
nueva **sobre la misma fila** y la deja otra vez en `Uploading`. La respuesta tiene la misma forma que
en §1. `Attachments.NotAvailable` (409) para cualquier otro estado.

## 3. Confirmar

`POST /api/attachments/{attachmentPublicId}/confirm` · `Attachments.Upload` · comando
`ConfirmarSubidaDeAdjuntoCommand` · **idempotente**

**200**

```json
{ "status": 2, "rejectionReason": null, "confirmedAt": "2026-09-23T15:01:12Z" }
```

| Resultado | Cuándo |
|---|---|
| `Available` (2) | el objeto está, el tamaño coincide y la firma corresponde al tipo |
| `Rejected` (3) | el objeto está pero no coincide; el objeto se retira (papelera) y `rejectionReason` dice por qué |
| `Incomplete` (4) | la autorización venció y no hay objeto |
| `Uploading` (1) | no hay objeto pero la autorización sigue vigente (la subida puede estar en curso) |

## 4. Listar los adjuntos de un documento

`GET /api/attachments/by-owner?ownerEntityType=…&ownerEntityPublicId=…` · `Attachments.Download`
más el permiso de lectura del módulo · consulta: **no escribe**

**200**

```json
[
  {
    "publicId": "c2d4…",
    "ownerEntityType": "AccountingDocument",
    "ownerEntityPublicId": "8f1c…",
    "fileName": "Factura 1234 – Ferretería Núñez.pdf",
    "contentType": "application/pdf",
    "sizeBytes": 20481234,
    "sha256Hex": "9f86d081…",
    "createdAt": "2026-09-23T15:00:01Z",
    "createdBy": "auxiliar@coop",
    "canDelete": true,
    "status": 2,
    "format": 2,
    "rejectionReason": null,
    "needsConfirmation": false
  }
]
```

- **`needsConfirmation`** es verdadero para `Uploading` con la autorización vencida. La pantalla llama a
  §3 para esos adjuntos (R5).
- **`canDelete`** aplica las reglas de conservación: por ejemplo, es falso si el comprobante ya está
  contabilizado. Sin permiso de lectura del módulo, la lista viene **vacía**, sin error.
- Los campos de siempre (`ownerEntityType`, `ownerEntityPublicId`, `sha256Hex`, `createdAt`, `createdBy`)
  se conservan con sus nombres; hasta la implementación este ejemplo los llamaba `uploadedAt/By`.

## 5. Pedir un enlace de descarga

`POST /api/attachments/{attachmentPublicId}/download-link` · `Attachments.Download` más la lectura del
módulo · comando `EmitirEnlaceDeDescargaCommand` (auditado, FR-046)

**200**, formato directo:

```json
{ "direct": true, "url": "https://ingenia365-erp-attachments.s3.amazonaws.com/pdn/3/…?X-Amz-Expires=60&response-content-disposition=attachment%3B%20filename%2A%3DUTF-8%27%27Factura%25201234…", "expiresAt": "2026-09-23T15:02:13Z" }
```

**200**, formato anterior (`AppEncrypted`): `{ "direct": false }`. El cliente baja por §6.

| Código | HTTP | Cuándo |
|---|---|---|
| `Generic.NotFound` | 404 | no existe, **o** no corresponde a quien pide (misma respuesta) |
| `Attachments.NotAvailable` | 409 | el estado no es `Available`; `data: { status }` |

## 6. Bajar un adjunto del formato anterior

`GET /api/attachments/{attachmentPublicId}` · igual que hoy: descifra, verifica la huella y responde
con el archivo, para `Format = AppEncrypted`. Para `Direct` responde 409
`Attachments.UseDownloadLink`.

## 7. Borrar

`DELETE /api/attachments/{attachmentPublicId}` · `Attachments.Delete` · comando
`DeleteAttachmentCommand` (auditado)

**204.** Retira el objeto (papelera de 90 días) y después da de baja la fila (R8).

| Código | HTTP | Cuándo |
|---|---|---|
| `Generic.NotFound` | 404 | no existe o no corresponde |
| `Attachments.OwnedByModule` | 422 | lo generó un módulo (como hoy) |
| `Attachments.OwnerLocked` | 422 | es soporte de un comprobante contabilizado; `data: { ownerEntityType }` |

## 8. Descartar un borrador con soportes (contabilidad)

`DELETE /api/accounting/documents/drafts/{id}?deleteAttachments=true` · `DiscardDraftCommand(PublicId,
DeleteAttachments)` · aplica también a los borradores de apertura.

| Código | HTTP | Cuándo |
|---|---|---|
| `Accounting.Document.HasAttachments` | 422 | tiene soportes y no se pidió `deleteAttachments=true`; `data: { count }` |

## 9. Rutas de descarga de un módulo

Aplican primero la regla de su módulo y después responden **igual que §5**:

- `POST /api/payroll/pila/{id}/download-link?acknowledgeDifference=true|false`
  (`Payroll.Pila.View`): conserva la exigencia de reconocer el descuadre. El `Content-Type` firmado
  lleva el `charset` de la generación.
- `POST /api/payroll/disbursements/{id}/download-link` (`Payroll.Disbursement.View`).

Las rutas actuales `GET …/{id}/file` quedan **sólo** para archivos `AppEncrypted`, y para los `Direct`
responden 409 `Attachments.UseDownloadLink`.

## 10. Sólo con `AttachmentStorage:Provider = Local` (desarrollo)

`POST /api/attachments/local-blob/{token}` (formulario, igual que el POST del almacén) y
`GET /api/attachments/local-blob/{token}`. **Anónimas**: el token es la autorización, como en S3. Es
un token de DataProtection que lleva la operación, la clave, el tipo, el tamaño, la huella y el
vencimiento, y que el almacén local rechaza si está alterado o vencido. **No se
registran** con otro proveedor **ni en Production**; lo fija una prueba. Las rutas sólo reenvían:
`POST` a `RecibirSubidaLocalCommand` y `GET` a `LeerBlobLocalQuery` (research R16).

## Retirada

`POST /api/attachments` (subida multipart **a través de la API**) se retira **sin alias**. Su único
cliente era el componente huérfano `AttachmentUploader`, y las subidas de personas pasan a §1–§3. Los
módulos siguen guardando sus archivos por `UploadAttachmentCommand`, que es interno y no tiene ruta.

## Códigos nuevos

`Attachments.OwnerNotAllowed`, `Attachments.OwnerLocked`, `Attachments.NotAvailable`,
`Attachments.UseDownloadLink`, `Attachments.Busy` y `Accounting.Document.HasAttachments`. Los de
validación, integridad, `BlobMissing` y `OwnedByModule` siguen como están.
