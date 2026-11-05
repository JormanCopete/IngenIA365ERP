# Contrato de la API — 016

Todas las rutas exigen sesión con cooperativa resuelta y declaran su permiso (`.RequireAuthorization`). Entran y salen `PublicId`,
nunca `int Id`. Toda escritura lleva `Idempotency-Key` (`COR_OperationKeys`). Los errores van en el sobre de siempre
(`code`, `message`, `data`). Los enums entran por nombre o número y salen como número.

Prefijo: `/api/implementation`.

## 1. Perfiles de lectura

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /profiles?targetTemplate=` | `Implementation.Loads.Review` | lista con versión vigente y si está sellada |
| `GET /profiles/{id}` | ídem | perfil con todas sus versiones y mapeos |
| `GET /profiles/targets` | ídem | plantillas que aceptan perfil, con sus columnas (nombre, tipo, obligatoria, largo) |
| `POST /profiles` | `Implementation.Profiles.Manage` | crea con su versión 1 |
| `PUT /profiles/{id}/versions/current` | ídem | modifica la versión vigente si no está sellada; si lo está, crea la siguiente (`data.versionNumber`) |
| `POST /profiles/{id}/deactivate` · `/activate` | ídem | |
| `POST /profiles/{id}/preview` | `Implementation.Loads.Review` | cuerpo `{ attachmentPublicId, versionNumber? }`: primeras N filas convertidas, errores de lectura, avisos de codificación. No guarda |

Cuerpo de una versión:

```json
{
  "code": "TERCEROS", "name": "Terceros del sistema anterior",
  "targetTemplate": "core.people", "targetPart": null,
  "fileKind": "FixedWidth", "encoding": "windows-1252", "skipFirstLines": 2,
  "skipPatterns": [{ "kind": "StartsWith", "value": "TOTAL" }], "skipEmptyLines": true, "lineLength": 320,
  "mappings": [
    { "targetColumn": "tipoDocumento", "sourceKind": "Position", "start": 1, "length": 2,
      "equivalenceTableCode": "TIPODOC" },
    { "targetColumn": "documento", "sourceKind": "Position", "start": 3, "length": 15, "padChar": "0" },
    { "targetColumn": "fechaNacimiento", "sourceKind": "Position", "start": 120, "length": 8,
      "dateFormat": "yyyyMMdd", "emptyDateValues": "00000000" },
    { "targetColumn": "esProveedor", "sourceKind": "Constant", "constant": "no" }
  ]
}
```

Errores 422: `Import.Profile.MissingRequiredColumn` (`data.column`), `.DuplicateTarget`, `.UnknownTarget`, `.OverlappingPositions`,
`.InvalidFormat`, `.VersionSealed`, `.UnknownTargetTemplate`, `Catalogo.CodigoDuplicado`.

Respuesta de la vista previa:

```json
{
  "columns": ["tipoDocumento", "documento", "primerNombre", "..."],
  "rows": [{ "line": 3, "values": ["CC", "1144123456", "MARIA", "..."], "errors": [] }],
  "errors": [{ "line": 7, "column": "fechaNacimiento", "source": "pos. 120-127", "code": "Import.Profile.InvalidDate",
               "message": "«31022020» no es una fecha válida con el formato yyyyMMdd." }],
  "warnings": [{ "code": "Import.Profile.EncodingSuspect", "message": "12 líneas tienen caracteres no reconocidos…", "lines": [4, 9] }],
  "totalLines": 5321
}
```

## 2. Equivalencias

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /equivalence-tables` · `GET /equivalence-tables/{id}?search=&page=` | `Implementation.Loads.Review` | |
| `POST /equivalence-tables` · `PUT /equivalence-tables/{id}` | `Implementation.Profiles.Manage` | |
| `POST /equivalence-tables/{id}/rows` · `PUT …/rows/{rowId}` · `DELETE …/rows/{rowId}` | ídem | fila a fila (baja lógica) |
| `GET /equivalence-tables/{id}/template.xlsx` | `Implementation.Loads.Review` | plantilla `implementation.equivalences` |
| `POST /equivalence-tables/{id}/import?mode=review\|apply` | `Implementation.Profiles.Manage` | multipart, mecánica común |
| `POST /equivalence-tables/{id}/import-accounting?mode=review\|apply` | ídem | lee la hoja «Equivalencias» del formato de la contadora |

Avisos de la carga: `Import.Equivalence.TargetNotFound` (el código nuevo no existe en su catálogo). Errores:
`Import.Equivalence.Conflicting` (con la fila de cada uno).

## 3. Cargues de implantación

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `POST /loads/uploads` | `Implementation.Loads.Review` | autorización firmada de subida (la de la 011, dueño `ImplementationLoad`); valida tamaño contra `Implantacion.MaximoDeBytesPorArchivo` **antes** de subir |
| `POST /loads` | `Implementation.Loads.Review` (+ permiso de importar del destino si `mode = apply`) | crea el cargue y lo encola |
| `GET /loads?step=&status=&page=` | `Implementation.Dashboard.View` | |
| `GET /loads/{id}` | ídem | estado, avance, resultado |
| `POST /loads/{id}/cancel` | quien lo pidió o `Implementation.Profiles.Manage` | sólo `Queued` |
| `GET /loads/{id}/review.xlsx` | `Implementation.Loads.Review`; con datos personales, además el permiso de exportar datos personales del área (data-model §6) | libro de revisión con las columnas destino convertidas, `resultado` y `errores` |

```json
POST /api/implementation/loads
{
  "stepCode": "CARTERA", "targetTemplate": "lending.migration", "targetPart": "installments",
  "readProfilePublicId": "…", "versionNumber": null,
  "attachmentPublicId": "…", "mode": "Apply",
  "targetParameters": { "migrationLoadPublicId": "…", "replace": false, "reason": "" }
}
```

- 202: `{ "loadPublicId": "…", "status": "Queued" }`.
- 422: `Implementation.Load.TooLarge` (`data.maxBytes`, `data.maxLines`), `.ProfileTargetMismatch`, `.ProfileInactive`,
  `.AttachmentNotAvailable`, `Validation.Invalid`.
- 403 sin el permiso del destino.

`GET /loads/{id}`:

```json
{
  "loadPublicId": "…", "stepCode": "PERSONAS", "status": "Running", "mode": "Review",
  "totalLines": 50000, "processedLines": 21400,
  "profile": { "code": "TERCEROS", "versionNumber": 3 },
  "file": { "name": "terceros.txt", "sha256": "…" },
  "requestedBy": "Ana Pérez", "startedAt": "…", "finishedAt": null,
  "result": null, "orderWarning": null, "moduleLoadPublicId": null
}
```

`result` es el `ImportResultDto` de siempre (o, en auxiliares y apertura, su resultado adaptado a la misma forma), con los errores de
lectura juntos: `{ sheet, row (= línea original), column (= columna destino), source, code, message }`.

## 4. Tablero, cortes, cifras y diferencias

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /dashboard?cutoff=` | `Implementation.Dashboard.View` | pasos en orden, con estado, último cargue, totales y avisos |
| `GET/PUT /steps` | ver / `Implementation.ReferenceFigures.Manage` | orden y habilitación por cooperativa |
| `GET/POST/PUT /cutoffs` · `POST /cutoffs/{id}/make-current` | ídem | |
| `GET/PUT /cutoffs/{id}/reference-figures` | ídem | cifras por paso y total |
| `GET …/reference-figures/template.xlsx` · `POST …/reference-figures/import?mode=` | ídem | plantilla `implementation.reference-figures` |
| `POST /cutoffs/{id}/differences` | `Implementation.Differences.Accept` | `{ stepCode, totalCode, comparison, calculatedValue, expectedValue, reason }`; 409 `Implementation.Difference.Stale` si los valores ya no son los actuales |

Paso en el tablero:

```json
{
  "stepCode": "CARTERA", "name": "Carga de cartera", "order": 8, "status": "WithAcceptedDifferences",
  "orderWarning": "Se aplicó antes que «Asociados».",
  "lastLoad": { "loadPublicId": "…", "mode": "Apply", "by": "…", "at": "…", "file": "creditos.txt", "profileVersion": "CREDITOS v2" },
  "totals": [
    { "totalCode": "CAR_CAPITAL", "name": "Saldo de capital", "calculated": 1234567890.00,
      "reference": 1234500000.00, "referenceDifference": 67890.00,
      "accounting": 1234567890.00, "accountingDifference": 0.00,
      "state": "Accepted", "acceptedReason": "Redondeo del sistema anterior" }
  ],
  "link": "/cartera/migracion/…"
}
```

`state`: `NoReference` («sin cifra»), `Matches`, `Difference`, `Accepted`.

## 5. Configuración

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /configuration/export?includeFigures=true` | `Implementation.Configuration.Export` | JSON `schemaVersion: 1`; auditado `Implementation.Configuration.Exported` |
| `POST /configuration/import?mode=review\|apply` | ídem + `Implementation.Profiles.Manage` | todo o nada; resumen por elemento (crear, versión nueva, sin cambio) |

## 6. Invitaciones masivas (US7)

| Método y ruta | Permiso | Qué hace |
|---|---|---|
| `GET /api/admin/users/invitations/template.xlsx` | `Security.Users.ImportInvitations` | el formato con «Usuarios nuevos», «Cargos» (con el catálogo vigente) y «Listas» (tipos aceptados por la cooperativa, roles) |
| `POST /api/admin/users/invitations/import?mode=review\|apply` | `Security.Users.ImportInvitations` | multipart, mecánica común, síncrona |

Resultado: el `ImportResultDto` de siempre. Cada fila que sale lleva en `rows[].messages` **exactamente** el mensaje de la 017
(«Invitación enviada. Si la persona ya tiene cuenta, le llegará a su correo registrado.»), y en `apply` además `emailSent: false` si
el correo no salió. Extra:

```json
"extra": { "invitations": 42, "positionsCreated": 2, "positionsUpdated": 1, "alreadyMembers": 3, "emailsFailed": 0 }
```

Errores por celda: los códigos de la 017 (`Identity.Document.Mismatch`, `.Invalid`, `.TypeNotAccepted`, `Invitation.EmailTaken`,
`Invitation.RoleNotFound`, `Invitation.PositionNotFound`) más `Import.Key.Duplicate` (correo o documento repetido en el archivo) y
`Catalogo.CodigoInvalido` en «Cargos». Aviso: `Invitation.AlreadyMember` (fila «sin cambio»). Errores de archivo:
`Import.Invitations.TooManyRows` (`data.maxRows`). Si la confirmación del catálogo de cargos falla después de emitir, las invitaciones se cancelan y el resultado lo dice:
`Import.Invitations.Compensated` (ningún correo sale).

## 7. Cambios en rutas existentes

Ninguna ruta cambia de forma. Los comandos de apertura, auxiliares, partes de Cartera y partes de aportes aceptan un perfil sólo
desde el trabajo de fondo del cargue de implantación (§3); sus rutas multipart siguen iguales.
