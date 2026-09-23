# Data Model: Adjuntos con subida y descarga directas al almacén (011)

**Fecha**: 2026-09-23 · **Spec**: [spec.md](spec.md) · **Research**: [research.md](research.md)

Un solo cambio de esquema: columnas nuevas en `COR_Attachments`, en la base de **cada cooperativa**.
Es aditivo, con valores por defecto que dejan las filas existentes exactamente como se comportan hoy.
No hay tablas nuevas.

## Entidad `Attachment` (`COR_Attachments`)

`Attachment : AuditableEntityLong`, con la baja lógica (`IsDeleted`, `DeletedAt`, `DeletedBy`) y el
filtro global de siempre (Principio VII).

| Columna | Tipo | Hoy | Cambio | Regla |
|---|---|---|---|---|
| `PublicId` | uuid | ✓ | — | lo único que viaja fuera (Principio VI) |
| `TenantId` | int | ✓ | — | cooperativa; también va en la clave del objeto |
| `OwnerEntityType` | varchar(100) | ✓ | — | `AccountingDocument`, `PilaGeneration`, `BankDisbursementFile`, `EmploymentTermination` |
| `OwnerEntityPublicId` | uuid | ✓ | — | el documento dueño |
| `FileName` | varchar(500) | ✓ | — | nombre original, tal cual (tildes, espacios) |
| `ContentType` | varchar(200) | ✓ | — | tipo declarado; debe estar en la lista blanca |
| `SizeBytes` | bigint | ✓ | — | en `Direct`, el **declarado** y firmado en la autorización; al confirmar debe coincidir con el del almacén |
| `Sha256Hex` | varchar(64) | ✓ | — | huella del contenido: la calcula el navegador (subidas) o el ERP (generados) |
| `StoragePath` | varchar(2000) | ✓ | — | referencia opaca sin el prefijo del ambiente: `{cooperativa}/{aaaa}/{mm}/{guid}.bin` |
| `StorageProvider` | varchar(50) | ✓ | — | `Local` / `S3` |
| `EncryptedDek` | varchar(1000), obligatorio | ✓ | — | sólo en `AppEncrypted`; en `Direct` va vacío (no se altera la columna) |
| **`Format`** | int | — | **nueva**, por defecto `1` | `1 = AppEncrypted` (formato anterior), `2 = Direct` |
| **`Status`** | int | — | **nueva**, por defecto `2` | `1 = Uploading`, `2 = Available`, `3 = Rejected`, `4 = Incomplete` |
| **`UploadExpiresAt`** | timestamp, nulo | — | **nueva** | vencimiento de la autorización de subida; nulo en generados y en filas anteriores |
| **`ConfirmedAt`** | timestamp, nulo | — | **nueva** | cuándo se confirmó o rechazó |
| **`ConfirmedBy`** | varchar(100), nulo | — | **nueva** | quién confirmó o rechazó |
| **`RejectionReason`** | varchar(300), nulo | — | **nueva** | motivo legible («el contenido no corresponde a un PDF») |

**Índices.** El existente `(TenantId, OwnerEntityType, OwnerEntityPublicId)` cubre la lista por dueño.
No hace falta otro: los `Uploading` vencidos se detectan dentro de esa misma lista.

## Enumeraciones (Domain)

```text
FormatoDeAdjunto  { AppEncrypted = 1, Direct = 2 }
EstadoDeAdjunto   { Uploading = 1, Available = 2, Rejected = 3, Incomplete = 4 }
```

Viajan como número y entran por nombre o por número, igual que el resto de la API
(`EnumPorNombreONumero`).

## Transiciones de estado

```text
                      autorización emitida
                               │
                               ▼
                         ┌───────────┐  confirmar: objeto presente,
                         │ Uploading │──── tamaño y firma coinciden ───▶ Available
                         └───────────┘
                           │       │
     confirmar: presente   │       │  confirmar: autorización vencida
     pero no coincide      │       │  y ningún objeto en el almacén
                           ▼       ▼
                     Rejected    Incomplete ── reintentar ──▶ Uploading (autorización nueva, misma fila)
             (objeto retirado:
              queda en la papelera)

  Cualquier estado ── borrar (persona con permiso y regla de conservación) ──▶ IsDeleted = true
                                                           (el objeto, si había, va a la papelera)
```

- **Generados por el ERP:** nacen `Available` y `Direct`, sin pasar por `Uploading`.
- **Filas anteriores:** quedan `Available` y `AppEncrypted`, y se siguen leyendo por la API.
- **`Uploading` sin vencer** no se confirma como `Incomplete`: la subida puede estar en curso.
- **Confirmar es idempotente.** Confirmar algo ya `Available` o `Rejected` devuelve el estado sin
  volver a validar.

## Reglas de conservación por dueño (`AdjuntosDeModulo`, ampliada)

| Dueño | Leer exige | Subir (personas) | Borrar |
|---|---|---|---|
| `AccountingDocument` | `Attachments.Download` + `Accounting.Vouchers.View` | `Attachments.Upload` + `Accounting.Vouchers.Create`; el comprobante tiene que existir en la cooperativa | `Attachments.Delete`, **sólo mientras el comprobante está en borrador** |
| `PilaGeneration` | `Payroll.Pila.View` | **nunca**: lo genera el módulo | nunca |
| `BankDisbursementFile` | `Payroll.Disbursement.View` | nunca | nunca |
| `EmploymentTermination` | `Payroll.Settlements.View` | nunca | nunca |
| cualquier otro | — | **no habilitado** en esta feature | — |

Las dos negativas de lectura —no existe y no te corresponde— responden igual:
`Generic.NotFound` (FR-020).

## Objeto en el almacén

| Propiedad | Valor |
|---|---|
| Bucket | `ingenia365-erp-attachments` (us-east-1), versionado |
| Clave | `{ambiente}/{cooperativa}/{aaaa}/{mm}/{guid}.bin`. El prefijo del ambiente **no** se guarda en la base |
| Cifrado | SSE-S3 (AES256) por defecto del bucket. En `Direct` no hay cifrado propio |
| Metadatos | `tenant`, `owner-type`, `owner-id`, `sha256`, `nombre` (codificado) |
| Borrado | `DeleteObject` deja una marca; la versión vive 90 días (`NoncurrentVersionExpiration`) y después se purga; la marca huérfana también (`ExpiredObjectDeleteMarker`) |

## Migración

Un **par** de migraciones EF, una por proveedor: `AdjuntosDirectos` (PostgreSQL y SQL Server).

- **Aditiva:** seis columnas nuevas. `Format` y `Status` son `NOT NULL` con valor por defecto (`1` y
  `2`); las otras cuatro admiten nulo.
- **No destructiva:** no borra ni altera columnas existentes. `Down` quita las seis columnas.
- **Se aplica** con el `DbMigrator` en cada base de cooperativa (Principio IV). La API no migra bases
  existentes al arrancar.
- **Sin DML:** los valores por defecto hacen todo el trabajo sobre las filas anteriores (Principio XII).
