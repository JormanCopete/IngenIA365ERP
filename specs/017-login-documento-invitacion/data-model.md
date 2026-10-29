# Data model — 017

Todo es **aditivo**. Se agregan columnas que admiten vacío y tablas nuevas. Lo único que se retira es
el índice `UK_COR_People_TaxId`, y antes se crea el que lo reemplaza. Los nombres siguen la convención
de cada base: `ADM_*` en la administrativa y `COR_*`/`SEC_*` en cada cooperativa.

## Base administrativa (`IngenIA365ERP_Admin`)

### `ADM_CentralUsers` (cuenta central, ampliada)

| Columna | Tipo | Nulo | Nota |
|---|---|---|---|
| `DocumentType` | varchar(3) | sí | código de acceso: CC, CE, PPT, PEP, TI, PA |
| `DocumentNumber` | varchar(20) | sí | normalizado: `[0-9A-Z]`, sin puntos, espacios ni guiones |
| `DocumentUsername` | varchar(24) | sí | derivado por `UsuarioDeDocumento`; nunca se escribe a mano |
| `FirstName`, `OtherNames`, `LastName`, `SecondLastName` | varchar(60) | sí | |
| `EmailVerifiedAt` | timestamp | sí | auditoría; la regla del ingreso lee `EmailConfirmed` |
| `PhoneNumber` | *(existente)* | sí | se usa como celular |

Índices nuevos:

- `UX_ADM_CentralUsers_Document`: (`DocumentType`, `DocumentNumber`), único, filtrado a filas no
  borradas y con documento.
- `UX_ADM_CentralUsers_DocumentUsername`: único, mismo filtro.
- `UX_ADM_CentralUsers_NormalizedEmail`: único, filtrado a filas no borradas. La migración trae una
  guarda contra duplicados.

`UserName` y `NormalizedUserName` no cambian: siguen iguales al correo.

Estados (`Status`): no cambian (`Active`, `Pending`, `Disabled`). La identidad que se crea al invitar
nace `Pending` y sin clave.

### `ADM_Invitations` (ampliada)

| Columna | Tipo | Nota |
|---|---|---|
| `CentralUserId` | uuid, nulo | la identidad a la que ancla, existente o creada al invitar |
| `DocumentType`, `DocumentNumber` | varchar(3), varchar(20) | |
| `FirstName`, `OtherNames`, `LastName`, `SecondLastName`, `MobilePhone` | | lo digitado; la identidad existente no se sobrescribe |
| `RolePublicId`, `PositionPublicId` | uuid, nulo | de `SEC_Roles` y `COR_Positions` de la cooperativa |
| `DocumentAttempts` | int, 0 | |
| `MaxDocumentAttempts` | int | copia de la política al emitir |
| `TypedEmail` | varchar(256), nulo | lo que digitó el administrador, si difiere del destino (auditoría) |

`Email` y `NormalizedEmail` siguen siendo el **destino**: el correo de la identidad si ya existía.

`InvitationStatus` gana `UnderReview = 5`. Transiciones:

```
Pending ──aceptar (documento coincide)──▶ Accepted
Pending ──n intentos fallidos─────────▶ UnderReview ──corregir──▶ Superseded (+ nueva Pending)
Pending/UnderReview ──cancelar──────────▶ Revoked
Pending ──vence─────────────────────────▶ Expired
Pending ──reenviar/corregir─────────────▶ Superseded (+ nueva Pending)
```

### `ADM_TenantMemberships` (ampliada)

`MembershipStatus` gana `UnderReview = 4`, que no da acceso: no aparece en el selector ni deja
renovar. Columnas nuevas: `ReviewRequestedAt` y `ReviewReason` (varchar 200).

### `ADM_CentralUserLoginAttempts` (ampliada)

`Identifier` (varchar 256, lo normalizado) e `IdentifierKind` (0 correo, 1 usuario, 2 desconocido).
`NormalizedEmail` se sigue llenando cuando el texto resuelve a una cuenta.

### `ADM_TenantIdentityPolicies` (nueva, una por cooperativa, como `ADM_TenantMfaPolicies`)

| Columna | Tipo | Defecto |
|---|---|---|
| `TenantId` | uuid, único | |
| `AcceptedDocumentTypes` | varchar(40) | `CC,CE,PPT,PEP,TI,PA` |
| `DocumentRequiredFrom` | date, nulo | nulo = nunca bloquea |
| `MaxActivationAttempts` | int | 3 (mínimo 1, máximo 10) |

Una cooperativa sin fila usa los defectos.

### `ADM_DocumentHistory` (nueva, sólo inserción)

`CentralUserId`, `OldType`, `OldNumber`, `NewType`, `NewNumber`, `OldUsername`, `NewUsername`,
`Reason` (`Registration`, `Correction`, `Update`, `Merge`, `Migration`), `Notes`, `SupportReference`,
`SupportAttachmentPublicId` + `SupportTenantId` (si lo subió un administrador), `ActorCentralUserId`,
`ActorKind` (persona, administrador, maestro, proceso), `OccurredAt`.

### `ADM_IdentityCases` (nueva)

`Kind` (`DocumentTaken`, `Merge`, `DocumentMismatch`), `CentralUserId`, `OtherCentralUserId` (nulo),
`Status` (`Open`, `InProgress`, `Done`, `Dismissed`), `Detail` (json: cooperativas pendientes en una
fusión), `OpenedBy`, `ResolvedBy`, `ResolvedAt`, `Resolution`.

### `ADM_EmailChangeRequests` (nueva)

`CentralUserId`, `NewEmail`, `NewNormalizedEmail`, `CodeHash` (SHA-256), `ExpiresAt` (30 min),
`Attempts` (máximo 5), `Status` (`Pending`, `Confirmed`, `Expired`, `Cancelled`), `RequestedFromIp`,
`ByMaster` (bool) y `Reason` (si la hace el maestro).

## Cada base de cooperativa

### `COR_People` (ampliada)

| Columna | Tipo | Nota |
|---|---|---|
| `Username` | varchar(24), no nulo tras rellenar | derivado de `IdType` + `TaxId` normalizado (R2); `TaxId` no se reescribe |

- `UK_COR_People_Username` es único y **no filtra eliminadas**: un documento eliminado se restaura,
  no se duplica, como hoy. **Reemplaza** a `UK_COR_People_TaxId`.
- `IdType` admite un código nuevo, `PT` (PPT), en `CatalogosDePersona`, en el validador de
  `PersonInput` y en el mapeo DIAN (`PT → 48`).

### `SEC_Users` (ampliada)

| Columna | Tipo | Nota |
|---|---|---|
| `PositionId` | int, nulo, FK `COR_Positions` | cargo de la invitación (D-15) |
| `CentralUsername` | varchar(24), nulo | copia del usuario central, escrita al activar y al acuñar un token con esa cooperativa si cambió; para la diferencia de D-08 |

`PersonId` se escribe al activar: la persona de la cooperativa se busca por `Username`; si no existe,
se crea con los datos de la invitación por `PersonFactory`. `IdentificationNumber` (texto libre de hoy)
deja de escribirse y se conserva.

## Reglas transversales

- Ninguna tabla nueva referencia a la persona por correo, documento ni usuario; sólo por
  `CentralUserId` (Guid) o `PersonId` (int interno).
- Ningún `int Id` sale por la API: las rutas usan el `PublicId` de membresía o invitación y el Guid de
  la cuenta central, como hoy en `/api/saas/users/{centralUserPublicId}`.
