# Contratos HTTP: Cartera financiera (convenciones, permisos y rutas de todo el módulo)

**Feature**: 013 | **Fecha**: 2026-10-05 | **Fuente de nombres**: `decisiones-transversales.md` (§2, T1..T102,
CH1..CH50, preguntas A1..I15 con su propuesta por defecto). Aquí no se inventa ningún nombre que allí tenga
uno. Lo que este contrato necesita y allí no está lo fija con las reglas T1–T4 y lo lista en §27; las
contradicciones que encontró se decidieron en `decisiones-transversales.md` §7 (C01..C90) y §28 remite a ellas. Los
códigos de error, enums y tablas son los de `data-model.md` (§9, §10), literales (C65, C71).

**Hermanos**: `contracts/interfaces.md` (puertos síncronos con otros módulos), `contracts/adaptadores-y-archivos.md`
(adaptadores externos, mensajes al deudor, archivos planos, layouts y documentos emitidos), `contracts/plantillas.md`
(columnas de cada plantilla) y los de hechos y mensajes de la bandeja y del lado contable. Este contrato cubre **sólo HTTP**: lo que llama una
pantalla de Cartera, de la plataforma o de otro módulo. Los cinco puertos que Cartera ofrece
(`IConsultasDeCartera`, `IReservasDeCartera`, `IConsultaDeObligaciones`, `IContratoDeObligaciones`,
`IDimensionesDeCartera`), el puerto `IContabilidadParaCartera` y los comandos de consumo **no tienen ruta**
(§26).

Resumen de lo que rige (detallado en §1):

- Prefijo **`/api/lending`** para la Cartera nueva; lo conservado se muda a `/api/savings` si se aprueba A10
  (§25). Plataforma en `/api/core`, `/api/saas`, `/api/compliance`; lado contable en
  `/api/accounting/lending` (enmienda E-01); informes en `/api/reports/lending`.
- Todo identificador que cruza es **`PublicId`** (`{id:guid}`).
- Toda escritura lleva **`Idempotency-Key`** (`IOperacionIdempotente`, `LosComandosDeCarteraLlevanClave`).
- Sobre `{ code, message, traceId, data? }`. Sin permiso o fuera de alcance responde **el mismo 404** que lo
  inexistente. Regla de negocio, **422** con `data`; validación de forma, **400** con los campos.
- Listas paginadas con `PagedResult`; exportar exige un **permiso propio** y se audita.
- Lectura con datos del deudor = permiso **`Read`**; `View` sólo para catálogos, parámetros, tableros y
  conteos sin persona (T60).

---

## 1. Convenciones

### 1.1 Grupos, filtros y lo que no tiene HTTP

- **Un archivo por área** en `Endpoints/Lending/*.cs`, todos bajo `app.MapGroup("/api/lending/…")` con
  `RequireAuthorization()`, `AddEndpointFilter<ErrorEnvelopeFilter>()`, `.RequirePermission(...)` en **cada**
  ruta (`LosEndpointsProtegidosExigenPermiso` entra por glob a `Endpoints/Lending/*.cs`; `Endpoints/Savings`
  queda exento por nombre hasta su feature) y el metadato de grupo
  `.AuditarIntentoDenegado(ModuloDeAuditoria.Lending)` (T67).
- **Los endpoints sólo reenvían** al `ISender` (Principio III). Cada comando tiene su `AbstractValidator`
  (Principio VIII); las reglas regulatorias (usura, límites, segregación, alcance, autorización) fallan en
  el handler como `Result.Failure`, para quedar auditadas como rechazo (T67).
- **Sin ruta** (lo vigila `LosComandosDeConsumoNoTienenRuta`): los consumidores de mensajes, la tanda del
  ciclo diario, la corrección por valor nacional, la ejecución del cierre, el cierre base, la réplica de
  valores nacionales y los métodos de los puertos en proceso (§26).
- **El alcance de Cartera** (oficina y línea, `IAlcanceDeCartera`) rige en todas las rutas de
  `/api/lending` y `/api/reports/lending`. No rige en los puertos que usan otros módulos (FR-004): esos se
  autorizan con el permiso del módulo que llama.

### 1.2 Sobre de error y estado HTTP

Una falla sale siempre como `{ code, message, traceId, data? }`. `ErrorEnvelopeFilter` infiere el estado del
código:

| Código | HTTP | Cuándo |
|---|---|---|
| `Validation.Invalid` | 400 | el validador rechazó el cuerpo; `data.errors: [{ field, code, message }]` («errores por campo»); queda auditado como `Lending.Request.Rejected` (campos y códigos, sin valores, T67) |
| `Request.BodyInvalid` | 400 | cuerpo ilegible (enum inexistente, Guid mal formado); el mensaje dice el valor y lo admitido |
| `Operation.KeyRequired` | 400 | escritura sin `Idempotency-Key` (§1.5) |
| `Archivo.Vacio`, `Archivo.ColumnaFaltante`, `Archivo.HojaFaltante`, `Archivo.Ilegible` | 400 | importaciones (§1.11) |
| `*.NotFound`, `Generic.NotFound` | 404 | no existe, **o** no hay permiso, **o** está fuera del alcance (§1.3) |
| `Concurrency.StaleRowVersion` | 409 | el borrador cambió desde que se leyó (solicitudes, novedades y lotes en borrador llevan `rowVersion`) |
| cualquier otro | 422 | regla de negocio; `data` trae lo necesario para corregir |

**422 con errores por campo**. Cuando la regla de negocio es de un campo (una tasa sobre la usura, un plazo
fuera del rango de la línea, una fila de una importación), `data` lleva además `errors: [{ field, code,
message, row?, column? }]` para que la pantalla marque el campo, igual que `POST /documents/validate` de la
009.

```json
{
  "code": "Lending.Usury.Exceeded",
  "message": "La tasa efectiva con cargos (31,42 % EA) supera la usura de consumo vigente el 2026-11-03 (29,87 % EA). Baje la tasa o los cargos de la línea.",
  "traceId": "00-7c1f…-01",
  "data": {
    "effectiveRate": 0.314200, "usuryRate": 0.298700, "modalityKey": "CONSUMO", "date": "2026-11-03",
    "nationalValuePublicId": "6f1b…", "errors": [{ "field": "currentRate", "code": "Lending.Usury.Exceeded", "message": "…" }]
  }
}
```

- `code` en inglés, `Área.Recurso.Motivo`, recurso en singular (`Lending.Loan.*` aunque la ruta sea
  `/loans`). `message` en español claro, dice qué hacer y nunca muestra el código (FR-013). `data` en
  camelCase.
- Un `2xx` con cuerpo devuelve el DTO; una acción sin valor responde 204; un POST que crea responde 201;
  una orden que encola responde **202** con la corrida (§16.4).
- Las familias de códigos son las de `decisiones-transversales.md` §2.17; los que este contrato agrega
  están en §27. Un código que emita `Application/Lending` y no figure aquí es un defecto del contrato.

### 1.3 Sin permiso o fuera de alcance: el mismo 404

- Ruta sin su permiso: 404 `Generic.NotFound`, igual que una ruta inexistente (FR-004, SC-015). Antes del
  404, el filtro registra `Lending.Access.Denied` (T67).
- **Fuera del alcance** (un crédito de una oficina que el usuario no tiene, una solicitud de una línea que
  no opera): el **mismo** 404 que el recurso inexistente (`Lending.Loan.NotFound`,
  `Lending.Application.NotFound`…), con el mismo mensaje, y el evento `Lending.Access.OutOfScope`. Nunca 403.
- **Alcance que falla cerrado** (T62): sin filas en `SEC_UserBranchAssignments` el usuario no ve ninguna
  oficina, salvo `Lending.Scope.AllOffices`; sin filas en `LND_UserCreditLineScopes` no opera ninguna línea,
  salvo `Lending.Scope.AllLines`. Quien decide o desembolsa también necesita alcance.
- **Las listas filtran en silencio**: lo que está fuera del alcance no aparece ni cuenta en `totalCount`.
- **Un identificador dentro del cuerpo** (la línea de una solicitud, el crédito que se recoge, la oficina)
  sigue la misma regla: fuera de alcance es inexistente.
- **Excepción, el nivel de decisión**: sin el permiso del nivel que le toca a una solicitud, la ruta de
  decisión responde el 404 de la solicitud (§4.3), como las aprobaciones de la 012.

### 1.4 Permisos que dependen del cuerpo y atribuciones operativas

- **El permiso es fijo por ruta**. Cuando un permiso adicional depende del cuerpo (votar por otro miembro,
  fecha valor antes de un cierre, una clave de parámetro con permiso propio) y el recurso ya es visible
  para el usuario, la respuesta es **422 `Lending.Permission.Required`** con `data: { permissionCodes[] }`,
  no 404: no revela nada y la pantalla necesita saber qué falta (precedente de la 012, §17.2.3). Las
  pantallas lo esconden con `PermissionGate` a partir de `GET /api/admin/permissions/mine`.
- **Atribuciones operativas con monto** (T63): `Lending.Disbursements.Execute`, `Lending.CreditLimits.Assign`,
  `Lending.ValidationFailures.Confirm` y `Lending.Payments.BackdateBeforeClose` admiten un monto máximo por
  rol en `SEC_PermissionAmountLimits` (§3.5.2). El límite efectivo es el mayor de los roles activos del
  usuario que conceden el permiso; un rol que lo concede sin fila no tiene límite. Excederlo responde 422
  `Lending.AmountLimit.Exceeded` con `data: { amount, maxAmount, currency, permissionCode }`.
- **Las decisiones de crédito no usan esos montos**: las enruta la matriz de atribuciones (§3.5, §4.3).

### 1.5 Idempotencia (FR-009, T21)

- **Cabecera** `Idempotency-Key: <uuid>` obligatoria en todo POST, PUT o DELETE que envía un comando de
  `Application/Lending`, de la plataforma invocada desde una pantalla de Cartera y de las rutas
  `/api/core`, `/api/compliance` y `/api/accounting/lending` de este contrato. La ruta la copia al
  `OperationKey` del comando (`IOperacionIdempotente`); `LosComandosDeCarteraLlevanClave` lo vigila.
- **Sin clave** van las consultas, también las que usan POST por el tamaño del cuerpo: simulaciones
  (`/simulations/schedule`, `…/schedule/pdf`, `/loans/{id}/payment-simulations`), vistas previas
  (`…/reversal-preview`, `…/disbursement-preview`, `/attribution-matrices/preview`, `/novelties/{id}/simulate`,
  `/disbursement-files/preview`, `/payroll-deduction-files/preview`, `/campaigns/{id}/preview`,
  `/message-templates/preview`), validaciones (`/regulatory/…/validate`), `/integrity/verify`,
  `/api/accounting/lending/rules/resolve`, `/api/accounting/lending/batches/preview` y los
  `download-link` (emiten un enlace firmado de 60 s y auditan; repetirlos emite otro).
- **Primera vez**: `IdempotencyBehavior` abre `TransaccionExplicita`, inserta `COR_OperationKeys` y guarda
  el resultado **sólo si hubo éxito**. Si falla, se revierte y la clave no queda.
- **Repetición** (doble clic, reintento de red, dos pestañas): mismo estado y cuerpo que la primera vez,
  cabecera `Idempotent-Replayed: true` y evento `Operation.Replayed`.
- **Misma clave, otro contenido**: 422 `Operation.KeyReused` con `data: { operation, firstUsedAt }`.
- **El cliente** genera el UUID al iniciar la operación (abrir el diálogo, cargar el borrador), lo conserva
  en todo reintento y lo renueva sólo tras un éxito o si cambia el contenido. Revisar una importación y
  aplicarla son dos operaciones: dos claves.
- **No usan `COR_OperationKeys`** (T21): el contrato de obligaciones y la reserva de número, que corren en
  la transacción de otro módulo, con clave de negocio e índice único en el documento de Cartera
  (`Lending.Contract.KeyReused`, `Lending.Reservation.KeyReused`); los mensajes recibidos
  (`LND_InboundMessages`) y los procesos (marca de agua). Ninguno tiene ruta.

### 1.6 Paginación y orden

Las listas que crecen (solicitudes, créditos, operaciones, transacciones, mensajes, documentos, gestiones,
alertas, corridas, líneas de un cierre) reciben `?page=&pageSize=` y responden `PagedResult { items, page,
pageSize, totalCount, totalPages }`: `page` desde 1, `pageSize` 20 por defecto, máximo 200 (un valor mayor
se recorta, no falla). Los catálogos chicos (líneas, órganos, etapas, convenios, plantillas, tipos de
alerta) responden un arreglo sin paginar. Cada lista declara su orden fijo; no hay `sort` libre. Los
informes y las exportaciones no paginan (§17).

### 1.7 Sólo `PublicId` y claves naturales (Principio VI)

- Todo identificador que entra o sale es el `PublicId` (Guid); las rutas usan `{id:guid}`. Los `Id`
  internos (`int`/`bigint`, también `SEC_Users.Id` y el `Id` de `COR_IntegrationMessages`) nunca cruzan: un
  usuario sale como `{ userPublicId, name }` y una persona como `{ personPublicId, fullName,
  documentType, documentNumber }` (sólo en respuestas de rutas `Read`).
- **Números de negocio** (`loanNumber`, número de operación, de acta, de documento) son texto para
  mostrar y buscar (`?number=`); ningún comando los recibe donde espera una entidad.
- **Códigos de catálogo** (`CodigoDeCatalogo`: líneas, órganos, etapas, convenios) son la nomenclatura de la
  cooperativa; no se editan (identifican la fila en plantillas y en la matriz contable, `CreditLineCode`).
  Un duplicado responde 422 `Catalogo.CodigoDuplicado`; la pantalla lo consulta antes con
  `GET /api/catalogos/{catalogo}/codigo/{codigo}` (§27 lista los nombres de catálogo).
- **Claves naturales admitidas**: el mes de un cierre y de un archivo (`{year}/{month}`), la central
  (`{bureauCode}`), la clave de parámetro (`{key}`), el tipo de alerta (`{typeCode}`), la parte de una
  carga (`{part}`), la plantilla (`{templateKey}`), el código de una plantilla de mensaje (`{code}`) y el
  código de verificación de un documento (`verify/{code}`).

### 1.8 Fechas (D y R), decimales, enums y moneda

- **Fechas de operación** `yyyy-MM-dd`, en **fecha local de Colombia** (`IDateTimeService.HoyLocal`, T9); la
  que se propone es la de hoy local. **Instantes** en UTC ISO-8601 con `Z`.
- **Las dos fechas de una liquidación** (FR-011, T24, C83): en `/loans/*`, `/risk/ratings`,
  `/api/core/national-values` (y, por la misma regla, `/parameters`), `date` es la fecha D a la que se liquida
  (pasada, hoy o futura; por defecto hoy) y **`recordedAsOf`** es el **instante** R hasta el que se toma la
  información registrada (por defecto, ahora); la respuesta lo devuelve con el mismo nombre. En informes y
  tablero `asOf` es la **fecha de corte**, como en la 012. Ningún parámetro `asOf` es un instante.
- **Decimales** exactos en JSON (número, nunca cadena): montos en pesos con 2 decimales (`Monto` 18,2);
  **tasas como fracción** con hasta 6 decimales (`TasaAnual` 9,6: `0.245000` es 24,5 % EA); las tasas
  equivalentes y diarias de la explicación salen con hasta 18 (`TasaEquivalente` 28,18); UVR con 4 (K6b). Un
  valor con más decimales de los admitidos es `Validation.Invalid`, nunca se redondea en silencio (T35).
- **Enums**: entran por nombre o por número y salen como número (`EnumPorNombreONumero`); aquí se escriben
  por nombre y los números son los de `data-model.md`. Los estados derivados (`status` de un cierre, de un
  documento emitido, de una promesa) y los textos de presentación (`severity`, `unit`) salen por nombre.
- **Moneda**: todo valor es `COP`. Un cuerpo con otra moneda responde 422 `Lending.Currency.NotSupported`.

### 1.9 Avisos, reconocimientos, motivo y acta

- **Avisos**: una respuesta exitosa puede traer `warnings: [{ code, message, data }]`; no detienen nada (p. ej.
  `Lending.Prevalidation.NoResponse` con la política «confirmar con pendiente», `Lending.Regulatory.LayoutSinCotejar`).
- **Reconocimiento**: cuando una operación exige aceptar un aviso, el primer intento responde 422 con el
  código del aviso y `data`, y el segundo lleva `acknowledgeWarnings: true` (o la bandera nombrada en la
  ruta) y, si la acción lo exige, el `reason`. Se audita lo aceptado.
- **Motivo**: los comandos `IConMotivo` (anular, cancelar, reversar, reabrir, liberar, levantar un
  bloqueo, cambiar un parámetro, aceptar diferencias) exigen `reason` no vacío de hasta 500 caracteres; sin
  él, 400 `Validation.Invalid`. Se copia al evento de auditoría.
- **Acta** (T64): todo lo que la spec dice «con acta» (decisiones colegiadas, vinculados, castigo, evaluación
  de cartera, validación de la política SARC, adopción o desistimiento de la pérdida esperada, aceptación de
  diferencias D17, reapertura de un cierre cuando la atribución lo exija) lleva `sessionItemPublicId`: el
  `PublicId` de un **punto de sesión cerrado** cuyo asunto y resultado coinciden. No hay campos de texto
  «referencia del acta». Un punto que no corresponde responde 422 `Lending.Session.ItemMismatch` (`data: {
  expectedSubject, itemSubject, itemOutcome }`).

### 1.10 Descargas y exportaciones

- **Archivos de Cartera** (desembolsos, descuentos, regulatorios, documentos emitidos, actas, informes de
  centrales): `POST …/{id}/download-link` emite un enlace firmado de 60 s al adjunto de módulo (no
  borrable, `AdjuntosDeModulo`, T81), que obliga a guardar con nombre y tipo. Se audita cada emisión.
- **Subidas** (documentos de la solicitud, soportes de garantías, certificaciones de pagaduría,
  constancias de aviso previo, consentimientos, evidencia de autorizaciones): por la ruta de la plataforma
  `POST /api/attachments/uploads` con el tipo de dueño de §2.24 de las transversales; el archivo nunca pasa
  por la API (feature 011). El comando de Cartera recibe el `attachmentPublicId` ya confirmado.
- **Exportar** un informe exige `Lending.Reports.Export` además de `Lending.Reports.View`
  (`RequirePermissionWhenExporting`), y las vistas con datos del deudor exigen además
  `Lending.Reports.ExportPersonalData` (FR-138). Cada exportación escribe `Lending.Report.Exported` con la
  cooperativa y las filas (§17).

### 1.11 Plantillas de importación

Mecánica única, la de la 012 sobre el ejecutor común (`RutasDePlantilla.MapPlantilla`, generalizado con la
definición y el permiso de datos personales por parámetro, T82): en cada ruta base `GET {base}/template.xlsx`
(con `?withData=true` es la exportación llena, con permiso de exportar) y `POST {base}/import?mode=review|apply`
(multipart, hasta 16 MB, `reason`, `Idempotency-Key`; `review` no guarda; `apply` es todo o nada y responde
422 `Import.Invalid` con `data.errors[] { row, column, code, message }`). Un código que ya existe **actualiza**
con las mismas reglas que el alta unitaria. **Ninguna plantilla crea personas**: un documento que no está en
`COR_People` es error de fila y columna. Las columnas viven en `contracts/plantillas.md`; el registro
de plantillas de Cartera es `GET /api/lending/templates` (§3.9).

### 1.12 Auditoría

- Todo comando lo registra `AuditBehavior` con módulo `Lending` (`Compliance`, `NationalValues` o `Savings`
  según el espacio de nombres, T67), actor (`IActorActual`), oficina (`IConOficina` o la oficina por
  defecto del usuario), origen (IP, User-Agent, `X-Canal`, `X-Dispositivo` de MAUI), `reason`, clave de
  idempotencia y, en cada `LoanOperation`, la **distribución completa** por cuota y concepto (T23). Un
  `Result.IsFailure` queda como `Rejected` con su código; un 400 de validación, como
  `Lending.Request.Rejected`.
- **Lecturas sensibles** (no pasan por un comando) las registra `LendingAuditEmitter` con lista fija:
  liquidación (`Lending.Payoff.Viewed`, con D, R y la huella del resultado), informe de centrales
  (`Lending.CreditBureauReport.Viewed`), reimpresión y descarga (`Lending.Document.Reprinted`), exportaciones
  (`Lending.Report.Exported`), actas y vinculados (`Lending.Session.Viewed`, `Lending.RelatedParty.Viewed`),
  verificación de un documento (`Lending.Document.Verified`) y la consulta de cupo de Comercial
  (`Lending.CreditStatus.Queried`, sin ruta). Lo fija `LasLecturasSensiblesDeCarteraSeAuditan`.
- El ingreso a cada pantalla lo registra `RegistroDeAccesos` con módulo `Navigation`.

### 1.13 Antes de activar y por entrega

- **Antes de la activación** (FR-142) sólo se puede: parametrizar (§3, §12.1), simular (§4.1), otorgar cupos con
  su evaluación y decisión (§4.3–§4.5, §5, §7), registrar autorizaciones y vinculados (§3.7, §22), migrar y
  activar (§19), y usar la plataforma (§21–§23). Cualquier otra ruta de `/api/lending` que escriba sobre
  créditos u originación responde 422 **`Lending.NotActivated`** («Cartera no activada»), y las de lectura
  de créditos responden listas vacías.
- **Por entrega**: el catálogo de permisos nace entero en K1 (§2.1), pero **una ruta nace con su entrega**
  (columna E). Antes, no existe: responde el 404 genérico. La columna E de cada tabla es la entrega en que
  la ruta se publica.

### 1.14 Errores comunes del módulo

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Lending.NotActivated` | 422 | operación que exige Cartera activada (§1.13) | |
| `Lending.Permission.Required` | 422 | falta un permiso que depende del cuerpo (§1.4) | `{ permissionCodes[] }` |
| `Lending.AmountLimit.Exceeded` | 422 | atribución operativa excedida (§1.4) | `{ amount, maxAmount, currency, permissionCode }` |
| `Lending.Segregation.Conflict` | 422 | la persona ya intervino en el expediente con una acción incompatible (T66) | `{ action, previousAction, previousAt }` |
| `Lending.Security.UserWithoutPerson` | 422 | quien radica, decide, vota o desembolsa no tiene `SEC_Users.PersonId` | `{ howToFix }` |
| `Lending.Office.WithoutAccountingBranch` | 422 | la oficina del crédito no está vinculada a una sucursal contable (T13, C9) | `{ officePublicId, officeName }` |
| `Lending.Prevalidation.NotPostable` · `.NoResponse` | 422 o aviso | validación previa contable con `Contabilidad.FaltaDeRegla = Bloquear` o política `Bloquear` (sólo en desembolso, novedad, condonación y castigo, T56) | `{ errors: [{ messageType, rule, whoFixes }] }` |
| `NationalValues.Missing` | 422 | falta el valor nacional de una fecha (usura, índice, festivos) | `{ code, key, date }` |
| `Lending.Currency.NotSupported` | 422 | moneda distinta de COP | |
| `Lending.<Recurso>.NotFound` | 404 | inexistente, sin permiso o fuera de alcance | |

---

## 2. Permisos

### 2.1 Catálogo `Lending.*` (T60)

Los siembra la API al arrancar (`LendingPermissionCatalogSeeder`, invocado desde `PhaseZeroSecuritySeeder`
**antes** de `BuiltInRolesSeeder`), nunca el DbMigrator. **Todo `Lending.*` se siembra entero en K1**, también
lo de K2 a K6b, para que las plantillas de rol no cambien de forma. Se borra el bloque `Lending` de
`IdentitySeedData` y su rol `LoanOfficer` (T89). La columna E es la entrega desde la que alguna ruta lo exige.

**Configuración**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.CreditLines.View` · `.Manage` | ver líneas, versiones, modalidades y equivalencias · crearlas, versionarlas, cerrarlas e importarlas | K1 |
| `Lending.Parameters.View` · `.Manage` | ver y registrar vigencias de parámetros del módulo `LND` (y ver la política SARC efectiva) | K1 |
| `Lending.Scoring.View` · `.Manage` | ver y versionar el bloque de puntaje de una línea (FR-037) | K1 |
| `Lending.Attributions.View` · `.Manage` | matriz de atribuciones y montos de las atribuciones operativas | K1 |
| `Lending.Segregation.Manage` | registrar vigencias de `Segregacion.Incompatibilidades` (permiso de la clave) | K1 |
| `Lending.GoverningBodies.View` · `.Manage` | órganos, reglas de quórum y mayoría, miembros | K1 |
| `Lending.RelatedParties.Read` · `.Manage` | ver (auditado) y mantener personas vinculadas | K1 |
| `Lending.RiskPolicy.Manage` · `.Validate` | registrar una versión de la política SARC · validarla con su punto de acta | K1 |
| `Lending.Scopes.Manage` | asignar líneas a usuarios | K1 |
| `Lending.Scope.AllOffices` · `.AllLines` | alcance total sin filas de asignación | K1 |
| `Lending.Templates.View` | registro y descarga de plantillas vacías de Cartera | K1 |
| `Lending.MessageTemplates.View` · `.Manage` | plantillas de mensaje al deudor y sus versiones | K1 |

**Originación**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.Simulations.Run` | simulador de plan de pagos (FR-034) | K1 |
| `Lending.Applications.Read` · `.Create` · `.Submit` · `.Analyze` · `.Cancel` | ver solicitudes · radicarlas y editarlas en borrador (con la preferencia inicial del deudor) · enviarlas a análisis · analizarlas y volver a correr validaciones · anularlas o registrar el desistimiento | K1 |
| `Lending.PrepaymentPreferences.Manage` | cambiar la preferencia del deudor para abonos a capital de un crédito o de un cupo rotativo (operación `PreferenceChange`, FR-035, D24; C85) | K1 |
| `Lending.Decisions.Analyst` · `.Coordinator` · `.Management` · `.Exceptions` | decidir en los niveles individuales de la matriz que los nombran; `Exceptions` es el nivel de excepción (puntaje bajo, alertas) | K1 |
| `Lending.CreditBureau.Query` · `.ReadReports` | pedir una consulta a centrales · ver su resultado (auditado) | K1 |
| `Lending.Sessions.Read` · `.Manage` · `.RecordVotes` · `.Vote` · `.Close` | ver sesiones y actas (auditado) · convocar, registrar asistencia y puntos · registrar votos de otros (secretaría) · votar el propio · cerrar el acta | K1 |
| `Lending.Guarantees.Read` · `.Manage` · `.Release` | ver garantías · registrarlas y cambiar su estado · liberarlas tras la firmeza | K1 |
| `Lending.PromissoryNotes.Manage` | crear, consultar, endosar y anular pagarés | K1 |
| `Lending.CreditLimits.Read` · `.Assign` | ver cupos, disponible y saldos de aportes cargados · otorgarlos, terminarlos y cargar saldos de aportes (atribución con monto) | K1 |
| `Lending.Blocks.Manage` | bloqueos y embargos manuales | K1 |
| `Lending.NumberReservations.Read` | ver reservas de número de Comercial | K1 |

**Crédito y pagos**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.Disbursements.Execute` · `.Void` | desembolsar y emitir un nuevo giro (atribución con monto) · anular un crédito pendiente de giro | K1 |
| `Lending.DisbursementOrders.Void` | anular una orden de giro emitida | K1 |
| `Lending.DisbursementFiles.Generate` | ver, generar, descargar y anular archivos de desembolso | K1 |
| `Lending.Loans.Read` | ver créditos, estado de cuenta, plan, operaciones y transacciones | K1 |
| `Lending.Payoffs.Read` | liquidación a una fecha (auditada) | K1 |
| `Lending.Payments.Simulate` · `.Reverse` · `.BackdateBeforeClose` | simular un pago · reversar un pago (no de Nómina) · fecha valor antes de un cierre (también por el contrato, D16; atribución con monto) | K1 |
| `Lending.ValidationFailures.Confirm` | confirmar por excepción una obligación con validación fallida (atribución con monto) | K1 |
| `Lending.Integrity.Verify` | verificar el libro contra la reconstrucción | K1 |

**Integración y procesos**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.Messages.Read` · `.Reprocess` · `.SendNotApplicable` | bandeja de mensajes de Cartera, recibos y hechos con su lista por crédito · reprocesar rechazados · enviar después lo que no pasó | K1 |
| `Lending.Processes.View` · `.Run` | ver corridas (sin datos del deudor) · ordenar o reintentar el ciclo diario | K1 |
| `Lending.Reconciliation.View` | conciliación contable y cuadre de una carga | K1 |
| `Lending.TestDoubles.Operate` | operar el recaudador y la tesorería de prueba (sólo fuera de producción) | K1 |

**Riesgo**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.Ratings.Read` · `.Override` | calificación informativa, cierres, reporte de cambios y modelo por segmento · calificación por análisis y su levantamiento | K1 |
| `Lending.MonthlyClose.Execute` · `.Reopen` | ordenar el cierre mensual · reabrir el último | K1 |
| `Lending.ImpairmentModel.Manage` | adopción o desistimiento de la pérdida esperada | K6a |
| `Lending.ArrearsHistory.Import` | carga complementaria del historial de mora | K6a |
| `Lending.PortfolioEvaluations.Prepare` · `.Apply` | preparar la evaluación de cartera · aplicarla con acta | K3 |

**Libranza**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.PayrollAgreements.View` · `.Manage` | pagadurías y convenios (sin documentos de personas) | K2 |
| `Lending.PayrollDeductionFiles.Read` · `.Generate` | ver archivos de descuento, sus líneas y los no aplicados · generarlos y anularlos | K2 |
| `Lending.PayrollNotApplied.Manage` | resolver un no aplicado (caja, cobranza, reintento) | K2 |
| `Lending.PayrollTransfers.Manage` | trasladar una libranza a otra pagaduría o a la forma alterna | K2 |

**Novedades**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.Novelties.Read` · `.Request` · `.Apply` | ver novedades · solicitarlas, simularlas y registrar consentimientos · aplicarlas | K3 |
| `Lending.Condonations.Request` · `.Apply` | solicitar una condonación · aplicarla | K3 |
| `Lending.WriteOffs.Prepare` · `.Apply` | armar el lote de castigo · aplicarlo con acta | K3 |
| `Lending.DebtorEvents.Manage` | fallecimiento, retiro de la cooperativa y embargo; reclamación al seguro | K3 |
| `Lending.Insolvency.Manage` | procesos de insolvencia | K3 |

**Mensajes al deudor y cobranza**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.DebtorMessages.Read` · `.Send` · `.Cancel` | ver la cola y su historia · enviar o programar · cancelar uno no enviado | K1 |
| `Lending.Campaigns.Manage` | campañas de mensajes | K4 |
| `Lending.Collections.Read` · `.Assign` · `.RecordActivity` | etapas, agenda, gestiones, promesas, acuerdos y jurídico · asignar y repartir · registrar gestiones | K4 |
| `Lending.CollectionStages.Manage` | versionar etapas de cobranza | K4 |
| `Lending.PaymentAgreements.Create` · `.Approve` | acuerdos de pago | K4 |
| `Lending.LegalProcesses.Manage` | cobro jurídico | K4 |
| `Lending.PriorNotices.Read` · `.Send` · `.RegisterEvidence` | avisos previos (lectura desde K1 por lo migrado) · enviarlos · registrar constancia manual | K1 (Read) / K4 |
| `Lending.CreditReportClaims.Read` · `.Manage` | reclamos y suplantación sobre el reporte (lectura desde K1) | K1 (Read) / K4 |

**Documentos e informes**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.Documents.Read` · `.Issue` · `.Reprint` · `.Void` · `.Verify` | ver documentos emitidos · emitir y enviar · descargar de nuevo el PDF guardado · anular · verificar un código | K1 |
| `Lending.Reports.View` · `.Export` · `.ExportPersonalData` | informes del centro · exportarlos · exportar los que traen datos del deudor | K1 |
| `Lending.Dashboard.View` | tablero | K5 |
| `Lending.RegulatoryFiles.Read` · `.MarkUploaded` | ver y descargar archivos SICSES y de centrales · marcar cargado con radicado | K5 |
| `Lending.RegulatoryReports.Generate` | validar y generar el formato de cartera del SICSES | K5 |
| `Lending.CreditBureauFiles.Generate` | validar y generar archivos para centrales | K5 |

**Migración, activación y alertas**

| Código | Qué habilita | E |
|---|---|---|
| `Lending.Migration.Load` · `.AcceptDifferences` | crear cargas, importar partes, aplicar, marcar ventas · aceptar diferencias con acta (D17) | K1 |
| `Lending.Activation.Execute` | ver la preparación, revisar cobros provisionales y activar | K1 |
| `Lending.Alerts.View` · `.Attend` · `.Manage` | bandeja de alertas de Cartera · atender · configurar tipos | K1 |

Total: **123 códigos `Lending.*`** (122 más `Lending.PrepaymentPreferences.Manage`, C85). Los `.View` que existen
son exactamente los de las tablas y ninguno devuelve datos del deudor (`LosCodigosViewDeCarteraNoTienenDatosPersonales`):
sus respuestas traen números, conteos, códigos y nombres de entidades jurídicas; de las personas que aparecen bajo un
`View` (miembros de órganos, contactos de pagaduría) sólo nombre, cargo y contacto institucional, nunca documento,
correo ni teléfono personal (C84).

### 2.2 Códigos de otros módulos

En `DomainPermissionCatalogSeeder` (o el seeder de su módulo), sembrados en K1:

| Código | Dónde |
|---|---|
| `Accounting.LendingRules.View` · `.Manage` | matriz, tipos de comprobante y completitud (§23) |
| `Accounting.LendingBatches.View` · `.Run` | lotes de Cartera (§23) |
| `Accounting.RegulatoryIndicators.View` · `.Manage` | patrimonio técnico y solvencia (§23.6) |
| `Core.IntegrationParticipants.View` · `.Manage` | registro de recaudadores y destinos (§21.1) |
| `Core.NationalValues.View` · `.Review` | copia de valores nacionales y conformidad (§21.3) |
| `Core.RegulatoryProfile.View` · `.Manage` | perfil regulatorio (§21.4) |
| `Core.ExternalChannels.View` · `.Manage` | canales externos (§21.2) |
| `Compliance.RestrictiveLists.View` · `.Load` · `.Release` | cargas de listas · cargarlas · ver y liberar coincidencias (§4.5, §22.1) |
| `Compliance.DataSubjectRequests.Read` · `.Register` · `.Respond` | solicitudes del titular (§22.3) |

Existentes que este contrato usa: `Core.People.Create` (alta con `PersonaDialog`),
`Compliance.HabeasData.RecordConsent` y `.ViewHistory` (autorizaciones, §22.2), `Security.Roles.Create`
(roles sugeridos, §2.5), `Attachments.Upload`/`.Download` (§1.10), `Accounting.Vouchers.View` (recibos,
§23.5), `Accounting.Periods.Close` (§24), `Payroll.Runs.View` (§24), `AuditLog.View`, `.Export`,
`.VerifyIntegrity`. El registro central de valores nacionales (`/api/saas/*`) no tiene código: lo protege
`RequireMasterAdminAttribute.Check` (maestro con segundo factor, §21.5).

### 2.3 Cómo se aplican

- **Crear, decidir, ejecutar y anular son acciones distintas** (segregación). Quien radica o solicita no
  decide ni vota sobre lo suyo (regla fija); los pares de `Segregacion.Incompatibilidades` (por defecto
  `Decidir × Desembolsar`) los evalúa `ReglasDeSegregacion` por `SEC_Users.Id` y por persona, nunca con el
  entero del token (`LaSegregacionNoUsaUserIdDelToken`). Cada acto escribe su `LND_CaseParticipations` por
  `RegistroDeIntervenciones` en la misma transacción.
- **Roles integrados** (T61): `Operator`, `ReadOnly` y `Auditor` reciben `*.View` por su glob y **nada** con
  datos del deudor; a `CompanyAdmin` se le excluyen de su `*` los `Lending.Decisions.*` y
  `Lending.Sessions.Vote` (administrar no es aprobar crédito).
- **Contador, auditor y revisor fiscal leen todo** (FR-004) por sus plantillas (§2.5), con
  `Scope.AllOffices` y `Scope.AllLines`.

### 2.4 Atribuciones operativas con monto

Rutas en §3.5.2. Admiten límite **sólo** `Lending.Disbursements.Execute` (valor del tramo),
`Lending.CreditLimits.Assign` (valor del cupo), `Lending.ValidationFailures.Confirm` (valor de la obligación)
y `Lending.Payments.BackdateBeforeClose` (valor del pago). `PermisosLimitables` admite códigos por módulo
(T65). Rige el límite vigente en la fecha de la operación.

### 2.5 Plantillas de rol (T61)

Plantillas en código (`PerfilesSugeridos`, módulo `Lending`), servidas por
`GET /api/admin/roles/templates?module=Lending` y creadas en bloque por `CreateSuggestedLendingRolesCommand`
(`POST /api/lending/activation/suggested-roles`, §19.4). Cada rol creado es de la cooperativa y se edita.
`Lending.*.Read` y `Lending.*.View` se expanden contra el catálogo sembrado al crear el rol.

| Plantilla | Actor | Permisos | Alcance y montos |
|---|---|---|---|
| `cartera.administrador` | Administrador de Cartera | `Lending.*` **salvo** `Lending.Decisions.*` y `Lending.Sessions.Vote` (C86) | todas las oficinas y líneas; sin límite |
| `cartera.asesor` | Asesor de crédito | `CreditLines.View` (respuesta del dueño, 2026-10-14: sólo consultar), `Simulations.Run`, `Applications.{Read,Create,Submit}`, `PrepaymentPreferences.Manage`, `CreditBureau.Query`, `Guarantees.{Read,Manage}`, `PromissoryNotes.Manage`, `CreditLimits.Read`, `Loans.Read`, `Payoffs.Read`, `Documents.{Read,Issue}`, `DebtorMessages.{Read,Send}`, `Core.People.Create`, `Compliance.HabeasData.RecordConsent` | sus oficinas y líneas |
| `cartera.analista` | Analista | `Applications.{Read,Analyze}`, `Decisions.Analyst`, `CreditBureau.{Query,ReadReports}`, `Guarantees.Read`, `CreditLimits.Read`, `Loans.Read`, `Payoffs.Read`, `Ratings.Read` | sus oficinas y líneas |
| `cartera.jefe` | Jefe de cartera | `Decisions.Coordinator`, `Applications.{Read,Cancel}`, `PrepaymentPreferences.Manage`, `CreditLimits.{Read,Assign}`, `Blocks.Manage`, `Disbursements.{Execute,Void}`, `DisbursementOrders.Void`, `DisbursementFiles.Generate`, `Loans.Read`, `Payoffs.Read`, `Payments.{Simulate,Reverse}`, `MonthlyClose.Execute`, `Ratings.Read`, `Novelties.{Read,Request}`, `WriteOffs.Prepare`, `Sessions.{Read,Manage}`, `Messages.*`, `Processes.*`, `Reconciliation.View`, `Migration.Load`, `PayrollNotApplied.Manage`, `Collections.{Read,Assign}`, `CollectionStages.Manage`, `Alerts.*`, `Reports.{View,Export}`, `Parameters.View`, `Templates.View` | todas las oficinas y líneas; límite en `Disbursements.Execute` y `CreditLimits.Assign` si la cooperativa lo fija |
| `cartera.comite-credito` | Secretaría del comité de crédito | `Sessions.{Read,Manage,RecordVotes,Close}`, `Applications.Read`, `RelatedParties.Read` | todas las oficinas y líneas |
| `cartera.comite-riesgos` | Comité de riesgos | `PortfolioEvaluations.Prepare`, `Ratings.Read`, `Sessions.{Read,Manage,RecordVotes,Close}` | todas |
| `cartera.gerente` | Gerente | `Decisions.{Management,Exceptions}`, `Condonations.Request`, `ValidationFailures.Confirm`, `Payments.BackdateBeforeClose`, `Applications.Read`, `Loans.Read`, `Payoffs.Read`, `Ratings.Read`, `Reports.View`, `Dashboard.View` | todas; límites operativos si la cooperativa los fija |
| `cartera.consejo` | Secretaría del consejo | `Sessions.{Read,Manage,RecordVotes,Close}`, `RelatedParties.Read`, `WriteOffs.Apply`, `PortfolioEvaluations.Apply`, `ImpairmentModel.Manage`, `RiskPolicy.Validate`, `Migration.AcceptDifferences` | todas |
| `cartera.gestor-cobranza` | Gestor de cobranza | `Collections.{Read,RecordActivity}`, `PaymentAgreements.Create`, `Loans.Read`, `Payoffs.Read`, `DebtorMessages.{Read,Send}`, `PriorNotices.Read` | sus oficinas |
| `cartera.oficial-cumplimiento` | Oficial de cumplimiento | `Compliance.RestrictiveLists.{View,Load,Release}`, `Compliance.DataSubjectRequests.*`, `Compliance.HabeasData.ViewHistory`, `RelatedParties.Read`, `CreditReportClaims.*`, `AuditLog.View` | todas |
| `cartera.contador` | Contador (sólo consulta y regulatorios) | `Lending.*.Read`, `Lending.*.View`, `Reports.*`, `Reconciliation.View`, `RegulatoryReports.Generate`, `CreditBureauFiles.Generate`, `RegulatoryFiles.MarkUploaded`, `Core.NationalValues.{View,Review}`, `Core.RegulatoryProfile.View` | todas; la matriz, los lotes y los indicadores (`Accounting.Lending*`, `Accounting.RegulatoryIndicators.*`) van en su rol contable |
| `cartera.auditor` | Auditor | `Lending.*.Read`, `Lending.*.View`, `Reports.{View,Export}`, `Integrity.Verify`, `AuditLog.{View,Export,VerifyIntegrity}` | todas, sin escritura |
| `cartera.revisor-fiscal` | Revisor fiscal | igual que `cartera.auditor` (las actas entran por `Sessions.Read`) | todas, sin escritura |

Los códigos sin prefijo de la tabla son `Lending.*`. `cartera.administrador` excluye `Lending.Decisions.*` y
`Lending.Sessions.Vote`, igual que el rol integrado `CompanyAdmin`: administrar no es aprobar crédito (C86).

---

**Cómo leer las tablas de rutas (§3 a §20).** Cada fila: `Ruta` relativa al prefijo de la sección · `Permiso`
(sin el prefijo `Lending.` cuando es de Cartera) · `Comando / consulta` · `Cuerpo → respuesta` resumidos ·
`Errores` propios (los comunes de §1.14 aplican siempre y no se repiten) · `E` entrega en que la ruta nace.
«Lista» es `PagedResult` salvo que se diga arreglo. Toda escritura lleva `Idempotency-Key` (§1.5).

## 3. Configuración — `/api/lending`

### 3.1 Líneas de crédito — `/credit-lines` (`CreditLinesEndpoints.cs`)

Una línea es `LND_CreditLines` (código de la cooperativa con `CodigoDeCatalogo`, nombre, destino, estado) más
sus versiones `LND_CreditLineVersions` con vigencia y fecha de registro (FR-019–FR-032). Modificar una línea es
**registrar una versión**; los créditos vivos conservan lo pactado (FR-032). Una línea con créditos no se
borra: se cierra a créditos nuevos.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&modality=&commercialOrigin=&includeClosed=` | CreditLines.View | `ListCreditLinesQuery` | → arreglo `CreditLineListItemDto { publicId, code, name, purpose, status, closedReason?, usuryBlockedSince?, modalityCode, subsegmentCodes[], commercialOrigin, currentVersion { publicId, validFrom, rateKind, currentRate?, indexCode?, margin?, usuryHeadroom } }` por código; sólo las líneas del alcance | | K1 |
| `GET /{id}?date=` | CreditLines.View | `GetCreditLineQuery` | → `CreditLineDto` con la versión vigente en `date` y el historial de versiones (`validFrom`, `registeredAt`, `registeredBy`, `reason`) | `Lending.CreditLine.NotFound` | K1 |
| `GET /{id}/versions/{versionId}` | CreditLines.View | `GetCreditLineVersionQuery` | → `CreditLineVersionDto` (abajo) | `.NotFound` | K1 |
| `POST /` | CreditLines.Manage | `CreateCreditLineCommand` | `{ code, name, purpose, allowsNonMembers, commercialOrigin, firstVersion: CreditLineVersionInput }` → 201 `CreditLineDto` | `Catalogo.CodigoDuplicado`, `Lending.CreditLine.ModalityUnknown`, `.NonMembersNotAllowed`, `.InterestTaxConceptMissing`, `Lending.Usury.Exceeded`, `Lending.Parameter.BelowLegalFloor` | K1 |
| `POST /{id}/versions` | CreditLines.Manage | `AddCreditLineVersionCommand` | `CreditLineVersionInput` con `validFrom` y `reason` → 201 `CreditLineVersionDto`. Cierra la versión anterior la víspera; si la nueva queda bajo la usura, limpia el bloqueo por usura (`usuryBlockedSince`) | los de `POST /`, `Lending.CreditLine.VersionOverlaps` (`data: { existingValidFrom }`), `.Closed` | K1 |
| `POST /{id}/close` · `/{id}/reopen` | CreditLines.Manage | `CloseCreditLineCommand` · `ReopenCreditLineCommand` | `{ reason }` → `CreditLineDto`. Cerrar no afecta los créditos vivos; reabrir revalida la usura del día | `Lending.CreditLine.AlreadyClosed`, `.NotClosed`, `Lending.Usury.Exceeded` | K1 |
| `DELETE /{id}` | CreditLines.Manage | `DeleteCreditLineCommand` | 204 sólo si nunca tuvo solicitud, crédito, cupo ni mensaje; borrado lógico | `Lending.CreditLine.HasLoans` (`data: { alternatives: ["Close"] }`) | K1 |
| `POST /{id}/usury-check` | CreditLines.View | `CheckCreditLineUsuryQuery` | `{ date?, amount?, termInstallments?, periodicity? }` → `{ effectiveRate, usuryRate, headroom, computableCharges[], explanation, passes }` (consulta, sin clave) | `NationalValues.Missing` | K1 |
| `GET /template.xlsx` · `POST /import?mode=` | CreditLines.View · CreditLines.Manage | `ImportCreditLinesCommand` | plantilla `lending.credit-lines` (hojas `Lineas`, `Tasas`, `Documentos`, `Seguros`, `Garantias`, `Equivalencias`) → `ImportResultDto` | `Import.Invalid` y los del alta | K1 |

`CreditLineVersionInput` (lo que sella cada versión; los nombres de bloque son los de `data-model.md`):

```json
{
  "validFrom": "2026-12-01", "reason": "Tasa aprobada por el consejo",
  "modalityCode": "CONSUMO", "subsegmentCode": "SinLibranza", "forEmployees": false,
  "rates": {
    "current": { "kind": "Fixed", "effectiveAnnual": 0.245000 },
    "variable": null,
    "default": { "kind": "RelativeToCurrent", "spread": 0.000000 },
    "bands": [{ "amountFrom": 0, "amountTo": 5000000, "termFrom": 1, "termTo": 24, "debtorKind": null, "forEmployees": null, "minSeniorityMonths": null, "effectiveAnnual": 0.260000 }]
  },
  "amounts": { "min": 500000, "max": 30000000 }, "terms": { "minInstallments": 1, "maxInstallments": 60 },
  "periodicities": ["Monthly", "Biweekly"], "paymentDayRule": { "kind": "FixedDay", "day": 5 },
  "amortization": { "systems": ["LevelPayment", "ConstantPrincipal"], "allowsExtraordinary": true, "grace": { "kind": "None" }, "allowsTranches": false },
  "dayCount": "Actual365", "rateEquivalence": "Compound",
  "rounding": { "unit": 1, "mode": "AwayFromZero", "moment": "PerInstallment" },
  "allocationOrder": "OldestInstallmentFirst", "conceptOrder": ["CollectionFee", "Insurance", "DefaultInterest", "CurrentInterest", "Principal"],
  "commercialNoteRecalculation": "ReduceInstallment", "advanceOptionFirst": "ExtraordinaryPrincipal",
  "paymentForms": ["Cashier", "OwnPayroll"], "defaultPaymentForm": "Cashier",
  "creditLimit": { "contributionsMultiple": 3, "maxIndebtedness": 40000000, "minContributions": 200000 },
  "requiresBureauQuery": true,
  "interestTaxConcept": { "taxed": false, "supportAttachmentPublicId": "9a2c…" },
  "documents": [{ "code": "CEDULA", "name": "Copia de cédula", "mandatory": true, "conditions": null }],
  "insurances": [{ "insurerPersonPublicId": "…", "base": "OutstandingBalance", "rate": 0.000450, "periodicity": "Monthly", "mandatory": true, "cooperativeRetainsShare": false }],
  "collateralRules": [{ "amountFrom": 10000001, "amountTo": null, "coDebtors": 1, "admittedKinds": ["CoDebtor", "Mortgage"], "minCoverage": 1.200000, "contributionsCount": true }]
}
```

- **Usura** (FR-021, T28): al crear, versionar, reabrir y en `usury-check`, `ControlDeUsura` compara la
  efectiva con todos los cargos computables contra la usura de la modalidad certificada que el catálogo
  vigente asigna. Excederla responde 422 `Lending.Usury.Exceeded` con la explicación; una usura nueva que la
  línea supera la bloquea sola (`usuryBlockedSince`, `LND_CreditLines.UsuryBlockedSince`; no es un
  `closedReason`) y alerta `Cartera.LineaCerradaPorUsura`.
- **Pisos y techos legales** (T42): un valor menos estricto que el límite nacional vigente responde 422
  `Lending.Parameter.BelowLegalFloor` con `data: { limitCode, limitValue, source }`.
- **Concepto tributario del interés** (D20, T44): una línea de origen comercial con tasa mayor que cero sin
  `interestTaxConcept` con soporte responde 422 `Lending.CreditLine.InterestTaxConceptMissing`.
- **No asociados** (FR-019): `allowsNonMembers` sólo si el perfil regulatorio vigente lo permite; si no,
  422 `Lending.CreditLine.NonMembersNotAllowed`.
- **Vivienda y UVR** (FR-024): una modalidad `VIVIENDA` responde 422 `Lending.CreditLine.ModalityNotAvailable`
  (`data: { availableFrom: "K6b" }`) hasta K6b.

### 3.2 Modalidades y equivalencias de Comercial

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /modalities?date=` | CreditLines.View | `ListModalitiesQuery` | → `{ documentPublicId, code, validFrom, sha256, verified, modalities: [{ code, name, subsegments[], sarcSegment, expectedLossSegment?, usuryKey?, admitsUvr, retiredFrom? }] }` del documento `CatalogoDeModalidades` vigente (T44); sólo lectura | `NationalValues.Missing` | K1 |
| `GET /commercial-term-equivalences` | CreditLines.View | `ListCommercialTermEquivalencesQuery` | → arreglo `{ commercialPeriodicity, lendingPeriodicity, commercialTermUnit, factor, validFrom }` (`LND_CommercialTermEquivalences`, FR-022) | | K1 |
| `PUT /commercial-term-equivalences` | CreditLines.Manage | `SetCommercialTermEquivalencesCommand` | `{ items[], validFrom, reason }` → el arreglo vigente. También por la hoja `Equivalencias` de `lending.credit-lines` | `Lending.CommercialTermEquivalence.Duplicate`, `.PeriodicityUnknown` | K1 |

Una periodicidad de Comercial sin equivalencia no es un error de esta ruta: es error del mensaje de venta
(evaluación `Rejected`, T17).

### 3.3 Puntaje de la línea — `/credit-lines/{id}/scoring`

El bloque de puntaje vive en la versión de la línea (FR-037). Esta ruta registra una versión nueva que copia
todo lo demás y cambia sólo el puntaje, con su permiso propio.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?date=` | Scoring.View | `GetCreditLineScoringQuery` | → `{ versionPublicId, validFrom, minimumScore, exceptionLevelCode, variables: [{ code, name, source, weight, ranges: [{ from, to, points }] }] }` | `Lending.CreditLine.NotFound` | K1 |
| `POST /` | Scoring.Manage | `SetCreditLineScoringCommand` | `{ validFrom, reason, minimumScore, exceptionLevelCode, variables[] }` → 201 `CreditLineVersionDto` | `Lending.Scoring.WeightsInvalid` (`data: { reason }`), `.VariableUnknown`, `Lending.CreditLine.VersionOverlaps`, `Lending.Attribution.LevelUnknown` | K1 |

Un puntaje bajo el mínimo nunca rechaza: sube la solicitud al nivel de excepción (FR-037, §4.3).

### 3.4 Parámetros — `/parameters` (`ParametersEndpoints.cs`)

Una ruta para todas las claves del módulo `LND` de `COR_ParameterVersions` (catálogo `ParametrosDeCartera`,
§2.20 de las transversales). Leer en el servidor es sólo `LectorDeParametros`; escribir, `AddParameterVersionCommand`
(el de la plataforma). Las claves no llevan el prefijo «Cartera.» (T43).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?key=&date=&recordedAsOf=` | Parameters.View | `ListParametersQuery` (módulo `LND`) | → arreglo `ParameterDto { module: "LND", key, description, type, allowedValues?, defaultValue, allowedScopes[], requiredPermission?, availableFrom, legalLimit?: { limitCode, value, direction, source }, current: { value, source: Version \| Default \| LegalLimit, validFrom?, validTo?, reason?, legalSource?, reviewPending, changedBy?, changedAt? }, overrides: [{ scopeKind, scope { publicId, code, name }, value, validFrom, validTo? }] }`. Con `recordedAsOf` (instante R) aplica la regla bitemporal de T38 | | K1 |
| `GET /{key}/history?scopeKind=&scopePublicId=` | Parameters.View | `GetParameterHistoryQuery` | → arreglo de vigencias, la más reciente primero | `Parameters.KeyNotFound` | K1 |
| `POST /{key}/versions` | Parameters.Manage (+ el de la clave) | `AddParameterVersionCommand` | `{ scopeKind: None \| CreditLine \| LendingFactType, scopePublicId?, value, validFrom, reason, legalSource? }` → 201 `{ versionPublicId, previousClosedOn? }` | `Parameters.ValueNotAllowed`, `.ScopeNotAllowed`, `.Overlaps`, `.PermissionRequired`, `Lending.Parameter.BelowLegalFloor`, `Lending.Parameter.NotYetAvailable` (`data: { availableFrom }`) | K1 |

- **Permiso de la clave**: `Segregacion.Incompatibilidades` exige además `Lending.Segregation.Manage`; sin
  él, 422 `Parameters.PermissionRequired` (`data: { permissionCode }`).
- **Entrega de la clave** (T43): una clave marcada K2, K4 o K6a no se ofrece ni se acepta hasta que
  `CatalogoDeCartera.EntregaVigente` la alcance (`CadaParametroRespetaLaEntregaDeSuModulo`).
- **Ámbitos**: `CreditLine = 6` (scope = línea, dentro del alcance) y `LendingFactType = 7` (scope = tipo de
  hecho de `LND_FactTypes`, el catálogo sembrado; `GET /fact-types` lo lista con `Parameters.View`).
- **Lo sellado no cambia**: el modo de paso se sella al emitir; las condiciones de un crédito, al
  desembolsar. Una vigencia nueva rige sólo hacia adelante.

### 3.5 Atribuciones

#### 3.5.1 Matriz de atribuciones — `/attribution-matrices` (T63)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?date=&includeHistory=` | Attributions.View | `ListAttributionMatricesQuery` | → arreglo `AttributionMatrixDto { publicId, version, validFrom, validTo?, reason, source, levels: [{ code, name, order, kind: Individual \| Collegiate, permissionCode?, governingBody?, consolidatedAmountCeiling? }], rules: [{ subject, conditions, minimumLevelCode }] }` | | K1 |
| `GET /{id}` | Attributions.View | `GetAttributionMatrixQuery` | → `AttributionMatrixDto` | `Lending.AttributionMatrix.NotFound` | K1 |
| `POST /` | Attributions.Manage | `AddAttributionMatrixVersionCommand` | `{ validFrom, reason, source, levels[], rules[] }` → 201 `AttributionMatrixDto`. Cierra la anterior la víspera; las solicitudes ya enrutadas conservan la versión sellada | `Lending.Attribution.MatrixInvalid` (`data: { reason }`: sin nivel colegiado final, órdenes no consecutivos, permiso de nivel fuera de `Lending.Decisions.*`), `.LevelUnknown`, `Lending.GoverningBody.NotFound` | K1 |
| `POST /preview` | Attributions.View | `PreviewAttributionRoutingQuery` | `{ subject, creditLinePublicId?, personPublicId?, amount, termInstallments?, collateralKinds?, score?, alerts?[], daysPastDue?, concept? }` → `{ effectiveLevel, reasons: [{ kind: AmountCeiling \| Rule \| RelatedParty \| Exception, detail }], consolidatedAmount, nonExceptionable[] }` (sin clave) | | K1 |

`subject` es una de las constantes `LendingDecisionSubjects` (§27): `CreditApplication`, `CreditLimit`,
`Novelty`, `Condonation`, `WriteOffBatch`, `PortfolioEvaluation`, `ImpairmentModelChange`,
`ActivationDifferences`, `ValidationFailureException`, `RiskPolicyVersion` (C52, C100). El consejo para
vinculados es forzado y no se configura (FR-040). Las `conditions` de una regla del sujeto `Novelty` admiten también
`noveltyMechanism` y `noveltyCause` (K3, T0879, bloque B2 del 2026-10-15; nulo = no importa; con otro sujeto,
`Lending.Attribution.MatrixInvalid` con `reason = RuleNoveltyCriteriaOutOfSubject`), y la vista previa recibe los mismos dos campos.

#### 3.5.2 Montos de las atribuciones operativas — `/amount-limits`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?rolePublicId=&permissionCode=&date=` | Attributions.View | `ListPermissionAmountLimitsQuery` (módulo `Lending`) | → arreglo `{ publicId, role { publicId, name }, permissionCode, maxAmount?, currency, validFrom, validTo?, reason, createdBy, createdAt }` | | K1 |
| `POST /` | Attributions.Manage | `SetPermissionAmountLimitCommand` | `{ rolePublicId, permissionCode, maxAmount, validFrom, reason }` → 201; `maxAmount: null` = sin límite | `Approvals.AmountLimit.NotLimitable` (`data: { limitable[] }`: los cuatro de §2.4), `.RoleLacksPermission`, `.Overlaps`; rol inexistente: 404 `Generic.NotFound` | K1 |

### 3.6 Órganos de decisión — `/governing-bodies` (T64)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?includeInactive=` | GoverningBodies.View | `ListGoverningBodiesQuery` | → arreglo `{ publicId, code, name, kind, decidesCredit, minutesPrefix, nextMinutesNumber, currentRules { quorum, ordinaryMajority, relatedPartyFraction, validFrom }, members, isActive }` | | K1 |
| `GET /{id}?date=` | GoverningBodies.View | `GetGoverningBodyQuery` | → el anterior + `members: [{ memberPublicId, person { personPublicId, fullName }, role: Chair \| Secretary \| Member \| Alternate, votes, validFrom, validTo?, appointmentSessionItemPublicId? }]` + historial de reglas | `Lending.GoverningBody.NotFound` | K1 |
| `POST /` | GoverningBodies.Manage | `CreateGoverningBodyCommand` | `{ code, name, kind: BoardOfDirectors \| OversightBoard \| CreditCommittee \| RiskCommittee \| Management \| Other, decidesCredit, minutesPrefix, rules }` → 201 | `Catalogo.CodigoDuplicado` | K1 |
| `POST /{id}/rules` | GoverningBodies.Manage | `AddGoverningBodyRulesVersionCommand` | `{ quorum, ordinaryMajority, relatedPartyFraction?, validFrom, reason }` → 201 | `Lending.GoverningBody.RulesInvalid`, `Lending.Parameter.BelowLegalFloor` (fracción de vinculados bajo 4/5) | K1 |
| `POST /{id}/members` | GoverningBodies.Manage | `AddGoverningBodyMemberCommand` | `{ personPublicId, position, votes, validFrom, appointmentSessionItemPublicId? }` → 201 | `Lending.GoverningBody.MemberDuplicate`, `Core.Person.NotFound` | K1 |
| `POST /{id}/members/{memberId}/end` | GoverningBodies.Manage | `EndGoverningBodyMemberCommand` | `{ validTo, reason }` → 204 | `Lending.GoverningBody.MemberNotFound` | K1 |
| `POST /{id}/deactivate` | GoverningBodies.Manage | `DeactivateGoverningBodyCommand` | `{ reason }` → 204 | `Lending.GoverningBody.InUse` (`data: { matrixVersion }`: un nivel vigente lo nombra) | K1 |

`kind` del órgano y `position` del miembro son enums nuevos (§27). Los miembros son personas del maestro
(no tienen que ser usuarios); la composición vigente a la fecha de la sesión decide el quórum (FR-039, F7).

### 3.7 Personas vinculadas — `/related-parties` (FR-040)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&basis=&date=&page=` | RelatedParties.Read | `ListRelatedPartiesQuery` | → lista `{ publicId, person { personPublicId, fullName, documentNumber }, basis, linkedTo? { personPublicId, fullName }, relationship?, validFrom, validTo?, source }`; auditado `Lending.RelatedParty.Viewed` | | K1 |
| `POST /` | RelatedParties.Manage | `RegisterRelatedPartyCommand` | `{ personPublicId, basis: BoardMember \| OversightBoardMember \| LegalRepresentative \| SignificantContributor \| Spouse \| Relative, linkedToPersonPublicId?, relationship?, validFrom, source, supportAttachmentPublicId? }` → 201 | `Lending.RelatedParty.Duplicate`, `.LinkRequired` (cónyuge o pariente sin la persona por la que existe el vínculo) | K1 |
| `POST /{id}/end` | RelatedParties.Manage | `EndRelatedPartyCommand` | `{ validTo, reason }` → 204 | `Lending.RelatedParty.NotFound` | K1 |
| `GET /template.xlsx` · `POST /import?mode=` | RelatedParties.Read · .Manage | `ImportRelatedPartiesCommand` | plantilla `lending.related-parties` | `Import.Invalid` | K1 |

Registrar o terminar un vínculo revalida los cupos de la persona: un cupo de un vinculado sin decisión del
consejo queda bloqueado con «cupo por revalidar» (FR-142).

#### 3.7.1 Grupos conectados — `/connected-groups` (FR-041, C112; US2, bloque 6C, T0393)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&includeEnded=` | RelatedParties.Read | `ListConnectedGroupsQuery` | → arreglo `{ publicId, name, basis: EconomicLink \| LegalLink \| CommonCoDebtor \| RelatedParty, grounds, supportAttachmentPublicId?, validFrom, endedAt?, endReason?, members: [{ person, validFrom, endedAt? }] }`; auditado `Lending.RelatedParty.Viewed` | | K1 |
| `GET /{id}` | RelatedParties.Read | `GetConnectedGroupQuery` | → el anterior; auditado | `Lending.ConnectedGroup.NotFound` | K1 |
| `POST /` | RelatedParties.Manage | `RegisterConnectedGroupCommand` | `{ name, basis, grounds, memberPublicIds[≥2], supportAttachmentPublicId?, validFrom? }` → 201 | `Validation.Invalid` (sin fundamento o con menos de dos) | K1 |
| `POST /{id}/members` | RelatedParties.Manage | `AddConnectedGroupMemberCommand` | `{ personPublicId, reason }` → el grupo | `Lending.ConnectedGroup.MemberDuplicate`, `.Ended` | K1 |
| `POST /{id}/members/{personPublicId}/end` | RelatedParties.Manage | `EndConnectedGroupMemberCommand` | `{ reason }` → el grupo | `Lending.ConnectedGroup.MemberNotFound` | K1 |
| `POST /{id}/end` | RelatedParties.Manage | `EndConnectedGroupCommand` | `{ reason }` → el grupo terminado | `Lending.ConnectedGroup.Ended` | K1 |

La concentración se mide por deudor y por grupo con el mismo límite; el grupo también se **deriva** al calcular (vinculados de la
misma persona y codeudores comunes) sin guardarse. Es la pestaña «Grupos conectados» de `/cartera/vinculados`.

### 3.8 Alcance por línea — `/scopes/users/{userPublicId}` (T62)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /` | Scopes.Manage | `GetLendingUserScopeQuery` | → `{ user { userPublicId, name, email }, hasAllOffices, hasAllLines, offices: [{ branchPublicId, code, name, isDefault, accountingBranch? }], lines: [{ creditLinePublicId, code, name, isDefault }] }`. Las oficinas son de solo lectura: las asigna Seguridad (`SEC_UserBranchAssignments`) | `Generic.NotFound` | K1 |
| `PUT /` | Scopes.Manage | `SetUserCreditLineScopesCommand` | `{ lines: [{ creditLinePublicId, isDefault }] }` → el mismo DTO. Reemplaza las asignaciones (las retiradas quedan de baja lógica) | `Lending.Scope.DefaultDuplicate`; una línea fuera del alcance de quien asigna: 404 | K1 |

Es la pestaña «Alcance de Cartera» de `/admin/usuarios`. El asistente de activación lista a los usuarios con
roles de Cartera sin alcance (F4).

### 3.9 Plantillas — `/templates` (T82)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /` | Templates.View | `ListLendingTemplatesQuery` | → arreglo `{ key, name, description, sheets[], route, importPermission, availableFrom, personalData }` de `CatalogoDePlantillasDeCartera` (`lending.credit-lines`, `lending.migration.{parte}`, `lending.provisional-collections`, `lending.contribution-balances`, `lending.related-parties`, `lending.arrears-history`, `lending.payroll-agreements`) | | K1 |
| `GET /{templateKey}/template.xlsx` | Templates.View | `GetLendingTemplateQuery` | → el libro vacío (encabezados en la fila 1, instrucciones en otra hoja) | `Lending.Template.NotFound` | K1 |

Las rutas de importación viven en el recurso de cada plantilla (`route`), con su permiso.

### 3.10 Alertas — `/alerts` y `/alert-types`

Las de la plataforma (`COR_Alerts` sobre `COR_Notifications`), filtradas al módulo `Lending` y a los tipos
`Cartera.*` de §2.18 de las transversales, más `Integracion.*` cuyo origen o destino sea Cartera.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /alerts?status=&typeCode=&severity=&from=&to=&page=` | Alerts.View | `ListAlertsQuery` (módulo `Lending`) | → lista `AlertDto { publicId, typeCode, severity, subject, body, entity? { type, publicId, label, route }, status: Pending \| Attended, raisedAt, attendedAt?, attendedBy?, attendNote?, withoutRecipient }`. El cuerpo nombra el crédito por su número, nunca por el deudor | | K1 |
| `GET /alerts/{id}` | Alerts.View | `GetAlertQuery` | → `AlertDto` | `Alerts.Alert.NotFound` | K1 |
| `POST /alerts/{id}/attend` | Alerts.Attend | `AttendAlertCommand` | `{ note }` → `AlertDto` | `Alerts.Alert.AlreadyAttended` | K1 |
| `GET /alert-types` | Alerts.View | `ListAlertTypesQuery` (módulo `Lending`) | → arreglo `AlertTypeDto { typeCode, description, severity, recipientPermissions[], channels, thresholds?, validFrom, activeRecipients, withoutRecipient, availableFrom }` | | K1 |
| `POST /alert-types/{typeCode}/versions` | Alerts.Manage | `SaveAlertTypeCommand` | `{ recipientPermissions[], channels, thresholds?, validFrom, reason }` → 201 | `Alerts.Type.NotFound`, `.PermissionUnknown`, `.RecipientsRequired`, `.Overlaps` | K1 |

Cada usuario ve las alertas que lo alcanzan: tiene un permiso destinatario del tipo y alcance sobre la oficina
del caso. Sin destinatario activo van a `CompanyAdmin` con `withoutRecipient: true`.

---

## 4. Simulador y originación — `/api/lending`

### 4.1 Simulador — `/simulations` (FR-034, T29)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /schedule` | Simulations.Run | `SimulateRepaymentScheduleQuery` | `ScheduleSimulationInput` → `ScheduleSimulationDto` (abajo). No registra nada; sin clave | `Lending.CreditLine.NotFound`, `.Closed`, `Lending.Schedule.OutOfLineRange` (`data: { field, min, max }`), `Lending.Usury.Exceeded` (como resultado, no como error: ver abajo), `NationalValues.Missing` | K1 |
| `POST /schedule/pdf` | Simulations.Run | `RenderScheduleSimulationPdfQuery` | el mismo cuerpo → `application/pdf` sin número ni código, con la leyenda «Simulación – no constituye aprobación» (T80) | los de `/schedule` | K1 |
| `POST /schedule/send` | Simulations.Run | `SendScheduleSimulationCommand` | `{ simulation: ScheduleSimulationInput, recipientPersonPublicId }` → 201 `{ debtorMessagePublicId, status }`: un `DebtorMessage` `Informative` por correo con el PDF; no registra nada del crédito | `Lending.DebtorMessage.ChannelNotAuthorized`, `Lending.Authorization.Missing` (tratamiento de datos), `Lending.DebtorMessage.OutsideLegalWindow` (`data: { nextAllowedAt }`) | K1 |

```
// ScheduleSimulationInput
{ "creditLinePublicId": "…", "personPublicId": null, "amount": 12000000, "installments": 36,
  "periodicity": "Monthly", "disbursementDate": "2026-12-01", "firstDueDate": "2027-01-05",
  "amortizationSystem": "LevelPayment", "extraordinary": [{ "months": [6, 12], "amount": 500000 }],
  "grace": { "kind": "None", "periods": 0, "interestTreatment": null }, "insurances": ["VIDA-DEUDORES"] }
// ScheduleSimulationDto
{ "rulesVersion": "LND-2026.1", "effectiveAnnualRate": 0.245000, "nominalPeriodRate": 0.018402,
  "effectiveRateWithCharges": 0.251300,
  "usury": { "modalityKey": "CONSUMO", "rate": 0.298700, "date": "2026-12-01", "passes": true, "headroom": 0.047400 },
  "totals": { "principal": 12000000, "interest": 4876543.21, "insurance": 194400, "other": 0, "toPay": 17070943.21 },
  "installments": [{ "number": 1, "dueDate": "2027-01-05", "principal": 233144.10, "interest": 220818.33,
                     "insurance": 5400, "other": 0, "total": 459362.43, "balanceAfter": 11766855.90 }],
  "explanation": [{ "segment": "2026-12-01..2027-01-05", "days": 35, "dailyRate": 0.000601264150, "base": 12000000 }],
  "warnings": [] }
```

Una simulación que excede la usura **no** es error: responde 200 con `usury.passes = false` y el aviso
`Lending.Usury.Exceeded`, porque simular no registra nada (FR-034) y la persona necesita ver por qué. Radicar,
aprobar y desembolsar sí bloquean.

### 4.2 Solicitudes — `/applications` (FR-035–FR-042, FR-047)

Ciclo: `Draft` → `Submitted` (validaciones y enrutamiento) → `PendingDecision` → `Approved` (también con
condiciones: es el desenlace `ApproveWithConditions` de la decisión) · `Denied` · `Returned` (vuelve a `Draft`) →
`Disbursed` (el primer tramo creó el crédito) · `Granted` (cupo rotativo otorgado) · `Cancelled`.
`CreditApplicationStatus` es el de `data-model.md` §2.1 (§27). «Aprobación vencida» (FR-042) y «bloqueada por
listas» (hasta que el oficial libere) **se calculan** y la respuesta los muestra, pero no son estados guardados
(C53).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&line=&office=&applicant=&level=&mine=&from=&to=&page=` | Applications.Read | `ListCreditApplicationsQuery` | → lista `CreditApplicationListItemDto { publicId, number, applicant { personPublicId, fullName }, line { publicId, code, name }, office, amount, installments, status, level?, submittedAt?, decidedAt?, approvalExpiresAt? }`, la más reciente primero | | K1 |
| `GET /{id}` | Applications.Read | `GetCreditApplicationQuery` | → `CreditApplicationDto` (abajo) | `Lending.Application.NotFound` | K1 |
| `POST /` | Applications.Create | `CreateCreditApplicationCommand` | `CreditApplicationInput` → 201 `CreditApplicationDto` en `Draft` | `Lending.CreditLine.Closed`, `.NonMembersNotAllowed`, `Lending.Application.ApplicantNotMember`, `Lending.Schedule.OutOfLineRange`, `Lending.Usury.Exceeded`, `Lending.PayrollAgreement.OperatorRegistrationMissing` (libranza, FR-108), `Lending.Security.UserWithoutPerson` | K1 |
| `PUT /{id}` | Applications.Create | `UpdateCreditApplicationCommand` | `CreditApplicationInput` + `rowVersion` → `CreditApplicationDto`; sólo en `Draft` | `Lending.Application.NotEditable` (`data: { status }`), `Concurrency.StaleRowVersion` | K1 |
| `POST /{id}/documents` | Applications.Create | `AttachCreditApplicationDocumentCommand` | `{ requirementCode, attachmentPublicId }` → 201 (el adjunto ya subido con dueño `LoanApplicationDocument`) | `Lending.Application.RequirementUnknown`, `.NotEditable`, `Attachments.*` | K1 |
| `DELETE /{id}/documents/{documentId}` | Applications.Create | `RemoveCreditApplicationDocumentCommand` | 204, sólo mientras la solicitud no esté decidida | `Attachments.OwnerLocked` | K1 |
| `POST /{id}/submit` | Applications.Submit | `SubmitCreditApplicationCommand` | `{ rowVersion }` → `CreditApplicationDto` con `checks[]`, `score` y `routing`. Corre FR-036 para solicitante y codeudores en la misma transacción | `Lending.Application.DocumentsMissing` (`data: { missing: [{ code, name }] }`), `Lending.Authorization.Missing` (tratamiento, `data: { personPublicId, purpose, howToFix }`), `Lending.Usury.Exceeded`, `Lending.Concentration.LimitExceeded`, `Lending.Concentration.TechnicalCapitalUnavailable` | K1 |
| `POST /{id}/analysis` | Applications.Analyze | `RecordCreditApplicationAnalysisCommand` | `{ income, expenses, externalInstallments, notes, recommendation? }` → `CreditApplicationDto` (recalcula capacidad y vuelve a enrutar) | `Lending.Application.NotInAnalysis`, `Lending.Segregation.Conflict` | K1 |
| `POST /{id}/checks/rerun` | Applications.Analyze | `RerunCreditApplicationChecksCommand` | `{ checks?: [Screening \| Bureau \| Capacity \| PayrollCapacity \| Consolidated \| CreditLimit \| Concentration \| Usury \| Requirements] }` → `checks[]` | | K1 |
| `GET /{id}/routing` | Applications.Read | `GetCreditApplicationRoutingQuery` | → `{ matrixVersion, effectiveLevel, reasons[], consolidatedAmount, nonExceptionable[], contentSha256 }` | | K1 |
| `POST /{id}/disbursement-preview` | Disbursements.Execute | `PreviewDisbursementQuery` | §9.1 | §9.1 | K1 |
| `POST /{id}/cancel` | Applications.Cancel | `CancelCreditApplicationCommand` | `{ kind: Annulment \| Withdrawal, reason }` → 204; no genera cartera (FR-047) | `Lending.Application.AlreadyDisbursed` | K1 |

`CreditApplicationInput`:

```json
{ "applicantPersonPublicId": "…", "officePublicId": "…", "creditLinePublicId": "…", "purpose": "Vehículo",
  "amount": 12000000, "installments": 36, "periodicity": "Monthly", "firstDueDate": "2027-01-05",
  "amortizationSystem": "LevelPayment", "extraordinary": [], "grace": { "kind": "None" }, "insurances": ["VIDA-DEUDORES"],
  "paymentForm": "OwnPayroll", "payroll": { "payrollAgreementPublicId": null, "employeeId": "E-1043", "certificationAttachmentPublicId": "…", "certificationDate": "2026-11-20" },
  "prepaymentPreference": "ReduceTerm",
  "coDebtors": [{ "personPublicId": "…" }],
  "collaterals": [{ "kind": "CoDebtor", "personPublicId": "…" }],
  "ownLoanPayoffs": [{ "loanPublicId": "…", "mode": "Full" }],
  "authorizations": [{ "personPublicId": "…", "purpose": "CreditBureauQuery", "contactChannel": null,
                      "policyVersionPublicId": "…", "evidenceAttachmentPublicId": "…", "grantedAt": "2026-11-20" }],
  "simulationSha256": "c1f0…" }
```

- **Personas** (FR-035, I-1): solicitante y codeudores son `personPublicId` del maestro; el alta nueva va por
  `PersonaDialog` (`POST /api/core/people`), nunca por esta ruta.
- **Autorizaciones** (FR-006, T69): las que vienen en el cuerpo se escriben con `IAutorizacionDeDatos.Agregar`
  en la misma transacción de la radicación; las ya vigentes no se repiten.
- **Preferencia del deudor** (FR-035): obligatoria; se sella en el crédito al desembolsar y puede cambiar
  después (§8.3).
- **Libranza** (K2): en K1 sólo existe la forma `OwnPayroll` (nómina propia, sin convenio); `payrollAgreementPublicId`
  responde `Lending.PayrollAgreement.NotAvailable` (`data: { availableFrom: "K2" }`) hasta K2.
- **Capacidad declarada** (US2, bloque 6A; extensión aditiva): `applicantCapacity { monthlyIncome, monthlyExpenses,
  externalInstallments }` y los mismos campos en cada `coDebtors[]` (que suma `role`: `CoDebtor` o `Guarantor`). Sin ingreso
  declarado, la validación de capacidad queda `Unavailable` hasta el análisis (`POST /{id}/analysis`). La certificación de la
  nómina propia (`payroll.certificationAttachmentPublicId`, `certificationDate`) queda como documento adicional; el `employeeId`
  no tiene columna en K1 (`PayrollEmployeeIdentifier` nace con K2).
- **Preferencia** (US2, bloque 6A): se sella en la solicitud (`PrepaymentChoice`) al radicar; su fila de
  `LND_PrepaymentPreferences` (`Origin = Application`) nace con el crédito o el cupo, porque
  `CK_LND_PrepaymentPreferences_Scope` exige uno de los dos.

`CreditApplicationDto` = el input más `{ publicId, number, status, rowVersion, applicant, office, line {
publicId, code, name, versionPublicId }, documents: [{ requirementCode, name, mandatory, attachment? }],
checks: [{ kind, person, outcome: Passed \| Warning \| Blocked \| NotRun, detail, evidence?, at }],
score?: { value, minimum, variables: [{ code, points, explanation }] }, routing?, decisions: [{ level, outcome,
conditions?, decidedBy?, sessionItem?, at }], approval?: { outcome, conditions?, expiresAt }, loanPublicId?,
createdBy, createdAt }`.

### 4.3 Decisiones individuales y «Mis pendientes» (FR-038, FR-042, T63)

Toda decisión es un hecho `LND_CreditDecisions` con huella. Las **individuales** van por estas rutas; las
**colegiadas** nacen al cerrar el acta (§5). La misma forma sirve a todos los sujetos de
`LendingDecisionSubjects`.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /pending-decisions?subject=&page=` | Applications.Read | `ListPendingLendingDecisionsQuery` | → lista `{ subject, subjectPublicId, number, summary, amount, level, since, route, canDecide, excludedReason? }`: lo que el usuario puede decidir **ahora** (tiene el permiso del nivel, alcance y no está excluido) y lo que está en agenda de sus órganos. Es `/cartera/aprobaciones` | | K1 |
| `POST /applications/{id}/decisions` | Applications.Read (+ el del nivel) | `RecordCreditDecisionCommand` | `DecisionInput` (abajo) → 201 `{ decisionPublicId, outcome, applicationStatus, approvalExpiresAt? }` | `Lending.Attribution.LevelRequired`, `.NotExceptionable` (`data: { reasons[] }`), `Lending.Decision.ContentChanged` (`data: { currentSha256 }`), `Lending.Decision.NotPending`, `Lending.Segregation.Conflict`, `Lending.Concentration.LimitExceeded`, `Lending.Usury.Exceeded`, `Lending.Adapter.SimulatedInProduction` (evidencia de centrales simulada en una línea que exige consulta) | K1 |
| `POST /credit-limits/{id}/decisions` | CreditLimits.Read (+ el del nivel) | `RecordCreditDecisionCommand` | ídem, sujeto `CreditLimit` | ídem | K1 |
| `POST /loans/{id}/validation-failure/decisions` | Loans.Read (+ el del nivel) | `RecordCreditDecisionCommand` | ídem, sujeto `ValidationFailureException`, cuando la confirmación por excepción supera la atribución operativa (§8.2) | ídem | K1 |
| `POST /novelties/{id}/decisions` · `/waivers/{id}/decisions` | Novelties.Read · Loans.Read (+ el del nivel) | `RecordCreditDecisionCommand` | ídem, sujetos `Novelty` y `Condonation` | ídem | K3 |

```
// DecisionInput
{ "outcome": "ApproveWithConditions", "reason": "Monto menor por capacidad",
  "conditions": { "amount": 10000000, "installments": 30, "additionalCollaterals": [{ "kind": "CoDebtor" }] },
  "expectedContentSha256": "9e2d…" }
```

- **Nivel**: la ruta exige ver el sujeto; decidir exige además el `permissionCode` del nivel efectivo
  (individual). Sin él, el **404** del sujeto, como en la 012. Un nivel colegiado no se decide aquí: 422
  `Lending.Attribution.LevelRequired` (`data: { level, governingBody }`) y la pantalla ofrece llevarlo a
  sesión (§5).
- **Resultados** (`CreditDecisionOutcome`): `Approve`, `ApproveWithConditions` (condiciones menores: monto,
  plazo o garantía adicional), `Deny` (motivo obligatorio), `Return` (vuelve a `Draft` para corregir).
- **Sin excepción posible** (`NoExceptuables`): falta de capacidad de pago, de autorización del titular,
  coincidencia en listas y usura nunca se aprueban: 422 `Lending.Attribution.NotExceptionable`.
- **Huella**: `expectedContentSha256` es el `contentSha256` de `routing` que el decisor vio. Si el monto
  consolidado o el contenido cambió, 422 `Lending.Decision.ContentChanged` y la solicitud se reenruta.
- **Vigencia** (FR-042): la aprobación vale `Originacion.DiasVigenciaDeAprobacion`; vencida, el desembolso
  exige decidir de nuevo.
- **Atribución** (US2, bloque 6B): decide el nivel efectivo quien tiene su permiso **o el de un nivel individual de mayor rango**
  (una atribución mayor cubre una menor; los niveles no se acumulan); con sólo un permiso de un nivel menor,
  `Lending.Attribution.LevelRequired` con `governingBody` nulo; sin ningún permiso de decisión, el 404 del sujeto. El maestro global no
  decide (C86). La respuesta suma `approvedAmount` y, en un cupo, `resultingSubjectPublicId`: aprobar un cupo **otorga** una versión nueva y
  firme (§7.1). `Lending.Decision.ContentChanged` lleva `data.currentSha256` (no `currentContentSha256`, que es la del acta).

### 4.4 Consultas a centrales — `/bureau-queries` (FR-036, F-19)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /` | CreditBureau.Query | `QueryCreditBureauCommand` | `{ personPublicId, bureauCode, purpose: CreditApplication \| CreditLimit \| Review, applicationPublicId?, creditLimitPublicId? }` → 201 `{ bureauQueryPublicId, status: Ok \| NoHistory \| ChannelUnavailable \| InvalidData, simulated, queriedAt, validUntil }`, **sin** el contenido | `Lending.Authorization.Missing`, `.WithoutEvidence` (consulta a centrales sin evidencia, p. ej. migrada), `Lending.Adapter.Unavailable` (`data: { channelCode, retryAfter? }`), `Core.ExternalChannel.NotConfigured` | K1 |
| `GET /?personPublicId=&applicationPublicId=&page=` | CreditBureau.ReadReports | `ListBureauQueriesQuery` | → lista `{ publicId, person, bureauCode, purpose, status, simulated, queriedAt, validUntil, summary?: { score, negativeMarks } }` | | K1 |
| `GET /{id}` | CreditBureau.ReadReports | `GetBureauQueryQuery` | → el anterior + `artifact { attachmentPublicId, fileName }`; auditado `Lending.CreditBureauReport.Viewed` | `Lending.BureauQuery.NotFound` | K1 |
| `POST /{id}/download-link` | CreditBureau.ReadReports | `IssueBureauReportDownloadLinkCommand` | → `{ url, expiresAt }` del adjunto `LendingBureauReport`; auditado | | K1 |

El canal se sella en la consulta (T71). En producción un resultado `SIMULADO` queda registrado pero nunca
sirve de evidencia para aprobar (T73). El canal es el de `COR_ExternalChannelSettings` (clase `CreditBureau`, la central por
`BureauCode`) vigente al consultar; `purpose: Review` se guarda como `CreditLimit` (`BureauQueryPurpose` sólo tiene `Application` y
`CreditLimit`) y la respuesta suma `reused` cuando devuelve la consulta vigente (US2, bloque 6B).

### 4.5 Listas restrictivas: resultado y liberación — `/screening-results` (T70)

Cada consulta de Cartera a listas (radicación, cupo, venta) queda en `LND_ScreeningResults`. La carga de las
listas es de la plataforma (§22.1).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=Blocked\|Released\|NoMatch&personPublicId=&page=` (estado calculado de `Outcome` y `ReleasedAt`) | Compliance.RestrictiveLists.Release | `ListScreeningResultsQuery` | → lista `{ publicId, person { personPublicId, fullName, documentNumber }, context: Application \| Sale \| CreditLimit \| ListRescan, contextPublicId, listLoads: [{ listCode, version }], outcome: NoMatch \| PossibleMatch \| Match \| Unavailable, matches: [{ listCode, entryName, score, program }], status, releasedBy?, releasedAt?, reason? }` | | K1 |
| `GET /{id}` | Compliance.RestrictiveLists.Release | `GetScreeningResultQuery` | → el anterior con la evidencia | `Lending.ScreeningResult.NotFound` | K1 |
| `POST /{id}/release` | Compliance.RestrictiveLists.Release | `ReleaseScreeningMatchCommand` | `{ reason }` → el resultado liberado; desbloquea la solicitud o el cupo | `Lending.Screening.NotPending`, `Lending.Segregation.Conflict` (quien radicó no libera) | K1 |

Toda coincidencia bloquea y alerta `Cartera.CoincidenciaEnListas` al oficial; nunca se libera por atribución
(FR-038).

---

## 5. Sesiones y actas — `/api/lending/sessions` (FR-039, FR-040, T64)

Una sesión (`LND_Sessions`) es el acta de un órgano: asistencia, puntos y votos. **Todo lo que la spec dice
«con acta» es un punto de sesión** (§1.9). Ciclo: `Draft` (convocada) → `Open` (instalada, con asistencia) →
`Closed` (sellada con SHA-256 y PDF). Un acta cerrada no se edita: se corrige con otra sesión.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?body=&status=&from=&to=&page=` | Sessions.Read | `ListSessionsQuery` | → lista `{ publicId, body { publicId, code, name }, number?, kind, mode, heldAt, status, items, closedAt? }` | | K1 |
| `GET /{id}` | Sessions.Read | `GetSessionQuery` | → `SessionDto` (abajo); auditado `Lending.Session.Viewed` | `Lending.Session.NotFound` | K1 |
| `POST /` | Sessions.Manage | `CreateSessionCommand` | `{ governingBodyPublicId, kind: Ordinary \| Extraordinary, mode: InPerson \| Virtual \| Mixed, heldAt, notes? }` → 201 `SessionDto` en `Draft` | `Lending.GoverningBody.NotFound` | K1 |
| `POST /{id}/attendance` | Sessions.Manage | `RecordSessionAttendanceCommand` | `{ attendees: [{ memberPublicId, present, substitutesForMemberPublicId? }], guests?: [{ personPublicId, role }] }` → `SessionDto` en `Open` con `quorum { required, present, met }` | `Lending.Session.Closed`, `Lending.GoverningBody.MemberNotFound`, `Lending.Session.SubstituteInvalid` | K1 |
| `POST /{id}/items` | Sessions.Manage | `AddSessionItemCommand` | `{ subject, subjectPublicId, kind: Decision \| Recommendation, notes? }` → 201 `SessionItemDto` con `rule` (`Majority` o `RelatedPartyQualified`, la calcula el sistema) y `excluded[]` | `Lending.Session.Closed`, `Lending.Session.SubjectNotPending` (`data: { subjectStatus }`), `Lending.Session.SubjectNotForBody` (el nivel efectivo es de otro órgano) | K1 |
| `PUT /{id}/items/{itemId}` | Sessions.Manage | `UpdateSessionItemCommand` | `{ proposedOutcome, conditions?, notes? }` → `SessionItemDto` | `Lending.Session.Closed` | K1 |
| `DELETE /{id}/items/{itemId}` | Sessions.Manage | `RemoveSessionItemCommand` | `{ reason }` → 204, sólo sin votos | `Lending.Session.ItemHasVotes` | K1 |
| `POST /{id}/presence-challenge` | Sessions.Read | `CreateSessionPresenceChallengeCommand` | `{ personPublicId }` → `{ challengePublicId, expiresAt, methods: [Passkey, Totp], publicKeyOptions? }` (base64url, `wwwroot/js/webauthn.js`) | `Lending.Session.MemberNotUser` | K1 |
| `POST /{id}/items/{itemId}/votes` | Sessions.Read (+ `RecordVotes` o `Vote`) | `RecordSessionVoteCommand` | `{ personPublicId, vote: For \| Against \| Abstain, method: RecordedBySecretary \| OwnSession \| InPersonPasskey \| InPersonTotp, presence?: { challengePublicId, assertion?, totpCode? } }` → 201 `SessionVoteDto` | `Lending.Session.VoterExcluded` (`data: { reason: Applicant \| Filer \| Interested \| NotMember \| Absent }`), `.Closed`, `.VoteDuplicate`, `Lending.Permission.Required`, `Approvals.Presence.Invalid`, `.Expired`, `.TotpReused` | K1 |
| `POST /{id}/close` | Sessions.Close | `CloseSessionCommand` | `{ expectedContentSha256 }` → `SessionDto` cerrado: resultado de cada punto, `LND_CreditDecisions` escritas, `contentSha256`, `minutesAttachmentPublicId` | `Lending.Session.QuorumNotMet` (`data: { required, present }`), `.Closed`, `Lending.Decision.ContentChanged` (un sujeto cambió desde que se llevó a la sesión), y los del sujeto al aplicarse (p. ej. `Lending.Concentration.LimitExceeded`): **nada queda** si uno falla | K1 |
| `GET /{id}/minutes.pdf` | Sessions.Read | `GetSessionMinutesPdfQuery` | → `application/pdf` del acta sellada (o borrador marcado «SIN CERRAR»); auditado | | K1 |
| `GET /items?subject=&outcome=&body=&closedFrom=&page=` | Sessions.Read | `ListSessionItemsQuery` | → lista `{ sessionItemPublicId, session { publicId, number, body, closedAt }, subject, subjectPublicId, kind, outcome, conditions? }` de actas cerradas: el selector de «punto de acta» de §1.9 | | K1 |

`SessionDto`: `{ publicId, body, number?, kind, mode, heldAt, status, quorum, attendees[], items: [{
itemPublicId, subject, subjectPublicId, summary, kind, rule, excluded: [{ person, reason }], votes: [{ person,
vote, method, recordedBy, at }], tally { for, against, abstain, composition, eligible }, outcome?, conditions?
}], contentSha256?, closedBy?, closedAt?, minutesAttachmentPublicId? }`.

- **Votación** (`EvaluadorDeVotacion`): quórum sobre la composición vigente (principales con voto); mayoría
  ordinaria del órgano; vinculados en entidad con actividad financiera: `5 × favorables ≥ 4 × composición`
  (la fracción es un límite nacional y la cooperativa sólo la endurece); el radicador y los interesados no
  votan y la base sigue siendo la composición.
- **Quién registra el voto** (F8): con `Sessions.RecordVotes` (secretaría) el de cualquier miembro
  (`method = RecordedBySecretary`); con `Sessions.Vote` sólo el propio (`personPublicId` = la persona del usuario), desde
  su sesión o presente con passkey o TOTP. Sin ninguno de los dos, 422 `Lending.Permission.Required`.
- **Al cerrar**: cada punto `Decision` se aplica a su sujeto por `IAsuntoDeActa.AlDecidirAsync` en la misma
  transacción; un punto `Recommendation` (comité de riesgos) sólo queda como antecedente.

---

## 6. Garantías y pagaré — `/api/lending` (FR-043, FR-044, FR-082)

### 6.1 Garantías — `/collaterals`

Una garantía cuelga de una solicitud y pasa al crédito al desembolsar. Estados (`CollateralStatus`):
`Constituted`, `Valid`, `Released`, `Extinguished`, `Executed`. Clases (`CollateralKind`, §27):
`PromissoryNote`, `CoDebtor`, `Pledge`, `Mortgage`, `GuaranteeFund`, `Contributions`.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?loanPublicId=&applicationPublicId=&personPublicId=&kind=&status=&expiringWithinDays=&page=` | Guarantees.Read | `ListCollateralsQuery` | → lista `CollateralDto { publicId, kind, status, owner { kind: Application \| Loan, publicId, number }, value, coverage?, date, validFrom?, validTo?, coDebtor?, appraisal?, policy?, fund?, documents[], promissoryNote? }` | | K1 |
| `GET /{id}` | Guarantees.Read | `GetCollateralQuery` | → `CollateralDto` con su historial de estados | `Lending.Collateral.NotFound` | K1 |
| `POST /` | Guarantees.Manage | `RegisterCollateralCommand` | `{ ownerKind, ownerPublicId, kind, value, date, validFrom?, validTo?, coDebtorPersonPublicId?, appraisal?: { value, date, appraiser, attachmentPublicId }, policy?: { insurerPersonPublicId, number, validTo, attachmentPublicId }, fund?: { fundPersonPublicId, coverage, commission }, documents?: [attachmentPublicId] }` → 201 | `Lending.Collateral.OwnerLocked` (dueño ya decidido), `.KindNotAdmitted` (`data: { admitted[] }`), `Lending.Authorization.Missing` (codeudor sin autorización) | K1 |
| `PUT /{id}` | Guarantees.Manage | `UpdateCollateralCommand` | los datos editables → `CollateralDto`, sólo mientras el dueño no esté decidido | `Lending.Collateral.OwnerLocked` | K1 |
| `POST /{id}/status` | Guarantees.Manage | `ChangeCollateralStatusCommand` | `{ status: Constituted \| Valid \| Extinguished \| Executed, reason, supportAttachmentPublicId? }` → `CollateralDto` | `Lending.Collateral.TransitionInvalid` (`data: { from, to }`) | K1 |
| `POST /{id}/release` | Guarantees.Release | `ReleaseCollateralCommand` | `{ reason }` → `CollateralDto` en `Released` | `Lending.Collateral.FinalityPending` (`data: { finalityUntil, paymentOperationPublicId }`), `.LoanNotPaidOff` | K1 |

Una garantía que vence alerta `Cartera.GarantiaPorVencer` con la anticipación del parámetro (tarea
`cartera.garantias-por-vencer`). Una garantía sólo se libera cuando la cancelación queda **en firme** (FR-082):
pasados `Pagos.FirmezaDiasPorClaseDeMedio` sin reverso.

### 6.2 Pagaré — `/promissory-notes`

Físico o desmaterializado; el desmaterializado va por `IDepositoDeValores` con el canal sellado (`SIMULADO`
hasta que haya contrato, T73).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?loanPublicId=&applicationPublicId=&status=&page=` | Guarantees.Read | `ListPromissoryNotesQuery` | → lista `PromissoryNoteDto { publicId, form: Physical \| Dematerialized, status, number?, channelCode?, externalNoteId?, simulated, signers[], custody?, events[] }` | | K1 |
| `POST /` | PromissoryNotes.Manage | `CreatePromissoryNoteCommand` | `{ ownerKind: Application \| Loan, ownerPublicId, form, physicalNumber?, custodyLocation? }` → 201; el desmaterializado pide la creación al depósito y queda `PendingSignature` | `Lending.Adapter.Unavailable`, `Core.ExternalChannel.NotConfigured`, `Lending.PromissoryNote.AlreadyExists` | K1 |
| `POST /{id}/refresh` | PromissoryNotes.Manage | `RefreshPromissoryNoteStatusCommand` | → `PromissoryNoteDto` (consulta el estado al depósito; la tarea `cartera.pagares` lo hace sola) | `Lending.Adapter.Unavailable` | K1 |
| `POST /{id}/physical-custody` | PromissoryNotes.Manage | `RegisterPhysicalPromissoryNoteCustodyCommand` | `{ signedAt, custodyLocation, attachmentPublicId }` → `PromissoryNoteDto` en `InCustody` (sólo físico) | `Lending.PromissoryNote.InvalidState` | K1 |
| `POST /{id}/endorse` · `/{id}/void` | PromissoryNotes.Manage | `EndorsePromissoryNoteCommand` · `VoidPromissoryNoteCommand` | `{ endorseePersonPublicId?, reason }` → `PromissoryNoteDto` | `Lending.PromissoryNote.InvalidState`, `Lending.Adapter.SimulatedInProduction` | K1 |

`PromissoryNoteStatus` (§27): `Created`, `PendingSignature`, `Signed`, `InCustody`, `Endorsed`, `Voided`. En
producción un pagaré `SIMULADO` nunca queda perfeccionado: desembolsar responde `Lending.Adapter.SimulatedInProduction`
y dice qué configurar (T73); con pagaré físico en custodia sí.

---

## 7. Cupos, bloqueos, reservas y aportes — `/api/lending` (FR-049, FR-057–FR-060, D19)

### 7.1 Cupos — `/credit-limits`

Un cupo es consolidado, por línea o **rotativo** (el que consume Comercial, FR-049). Se calcula o se asigna,
con vigencia y motivo, y pasa por la matriz de atribuciones (sujeto `CreditLimit`). Disponible = cupo − saldos
vivos − reservas vigentes − aprobadas sin desembolsar (FR-058).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&kind=&status=&line=&page=` | CreditLimits.Read | `ListCreditLimitsQuery` | → lista `CreditLimitDto { publicId, person, kind: Consolidated \| ByLine \| Revolving, line?, amount, origin: Calculated \| Assigned \| Migration, status (calculado, texto de presentación: `PendingDecision` \| `Active` \| `Blocked` \| `Ended`), blockReason?, validFrom, validTo?, prepaymentPreference?, decision? }` | | K1 |
| `GET /availability?personPublicId=&creditLinePublicId=&date=` | CreditLimits.Read | `GetCreditLimitAvailabilityQuery` | → `{ consolidated { limit, used, reserved, approvedNotDisbursed, available }, byLine[], revolving?, status, daysPastDue?, blocks[] }` | `Core.Person.NotFound` | K1 |
| `POST /` | CreditLimits.Assign | `AssignCreditLimitCommand` | `{ personPublicId, kind, creditLinePublicId?, origin: Calculated \| Assigned, amount?, validFrom, validTo?, prepaymentPreference?, reason, screeningResultPublicId?, bureauQueryPublicId?, authorizations?[] }` → 201 `CreditLimitDto`: `Active` si cabe en la atribución del usuario y la matriz no pide otro nivel; si no (monto sobre su atribución operativa o nivel superior en la matriz), `PendingDecision` con `routing`, sin error | `Lending.Concentration.LimitExceeded` (`data: { limit, current, requested, excess, basis }`), `Lending.Concentration.TechnicalCapitalUnavailable`, `Lending.CreditLimit.Overlaps`, `Lending.CreditLine.NonMembersNotAllowed`, `Lending.Authorization.Missing`, `Lending.Screening.Blocked` | K1 |
| `POST /{id}/prepayment-preference` | PrepaymentPreferences.Manage | `ChangePrepaymentPreferenceCommand` | `{ choice: ReduceTerm \| ReduceInstallment, reason?, evidenceAttachmentPublicId? }` → 201 la versión nueva (sólo cupo rotativo; la heredan los créditos que nazcan después) | `Lending.CreditLimit.NotRevolving` | K1 |
| `POST /{id}/end` | CreditLimits.Assign | `EndCreditLimitCommand` | `{ validTo, reason }` → `CreditLimitDto` en `Ended` | `Lending.CreditLimit.NotActive` | K1 |
| `POST /{id}/revalidate` | CreditLimits.Assign | `RevalidateCreditLimitCommand` | `{ reason }` → `CreditLimitDto` (levanta «cupo por revalidar» si ya cumple; si no, lo deja bloqueado con lo que falta) | `Lending.Concentration.LimitExceeded`, `Lending.Attribution.LevelRequired` | K1 |

- **Antes de activar** (FR-142): otorgar un cupo evalúa endeudamiento consolidado, grupo conectado,
  concentración y vinculados **contra la carga aplicada vigente**, como si ya hubiera nacido. Al activar o al
  reemplazar la carga, `Cartera.CupoPorRevalidar` marca los que ya no cumplen.
- **Concentración** (FR-041, T59): exceder el límite individual **bloquea**. Sin patrimonio técnico vigente,
  responde `Lending.Concentration.TechnicalCapitalUnavailable` y alerta `Cartera.PatrimonioTecnicoNoDisponible`.
- **Listas**: si la consulta de listas da coincidencia, el cupo queda bloqueado y la respuesta lo dice
  (`Lending.Screening.Blocked`, `data: { screeningResultPublicId }`) hasta que el oficial libere (§4.5). En K1 la
  respuesta es el **201** con el cupo en `Blocked` y el aviso `Lending.Screening.Blocked` en `warnings[]` (con
  `screeningResultPublicId`): un 422 revertiría el resultado que el oficial tiene que liberar (US2, bloque 6B).
- **Estado y versiones** (US2, bloque 6B; sin columnas nuevas): cada cambio es una versión de sólo inserción. Una asignación que
  supera la atribución operativa o que la matriz lleva a otro nivel (regla, vinculado, excepción) es una versión que **espera
  decisión** (su enrutamiento va sellado en `CalculationDetail`) y no rige hasta que se decida; aprobarla (individual o en acta) crea
  la versión firme con `DecisionId`; negarla o devolverla la deja `Ended`. `validTo` se aplica como una versión en cero desde el día
  siguiente. `GET /availability` suma `applicable { available, creditLimitPublicId }` cuando trae `creditLinePublicId` (el de la línea,
  el consolidado o el rotativo, C54).

### 7.2 Bloqueos y embargos — `/blocks` (FR-060)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&kind=&active=&page=` | Loans.Read | `ListDebtorBlocksQuery` | → lista `{ publicId, person, kind, origin: Manual \| Automatic, reason, since, liftedAt?, liftedBy?, support? }` | | K1 |
| `POST /` | Blocks.Manage | `RegisterDebtorBlockCommand` | `{ personPublicId, kind: Manual \| Garnishment, reason, supportAttachmentPublicId? }` → 201 | `Lending.Block.Duplicate` | K1 |
| `POST /{id}/lift` | Blocks.Manage | `LiftDebtorBlockCommand` | `{ reason }` → 204 | `Lending.Block.Automatic` (`data: { kind, howItLifts }`): un bloqueo automático se levanta solo cuando desaparece su causa; salvo el de insolvencia con la negociación fracasada (K3, decisión del dueño del 2026-10-15), que lo levanta una persona —la lista lo marca `canLiftManually: true`— | K1 |

`DebtorBlockReason` (`data-model.md`): `Manual`, `Arrears`, `ValidationFailed`, `WrittenOff`, `RestrictiveListMatch`,
`AssociateWithdrawal`, `Deceased`, `Insolvency`, `Garnishment`, `LimitRevalidation`. Un bloqueo
impide originar y vender a crédito, y lo informa la consulta de cupo de Comercial.

### 7.3 Reservas de número — `/number-reservations` (FR-057, T22)

Las crea y libera Comercial por `IReservasDeCartera`, dentro de su confirmación; aquí sólo se consultan.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&number=&from=&to=&page=` | NumberReservations.Read | `ListLoanNumberReservationsQuery` | → lista `{ publicId, loanNumber, status: Reserved \| Used \| Released \| Expired, amount, person, paymentReference { module: "INV", paymentPublicId }, reservedAt, expiresAt, usedByLoanPublicId?, releasedAt? }` | | K1 |

### 7.4 Saldos de aportes por plantilla — `/contribution-balances` (D19)

Provisional mientras Aportes no ofrezca `IConsultaDeAportes` (que hoy responde «no disponible»).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&asOf=&page=` | CreditLimits.Read | `ListContributionBalancesQuery` | → lista `{ person, contributions, permanentSavings, cutoffDate, loadPublicId, loadedAt }` (la última carga con corte ≤ `asOf`) | | K1 |
| `GET /template.xlsx` · `POST /import?mode=` | CreditLimits.Read · CreditLimits.Assign | `ImportContributionBalancesCommand` | plantilla `lending.contribution-balances` (documento, aportes, ahorro permanente, fecha de corte) → `ImportResultDto` | `Import.Invalid` | K1 |

---

## 8. Créditos y liquidación — `/api/lending/loans` (FR-061–FR-070, T23, T24)

### 8.1 Consultas del crédito

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?search=&status=&line=&office=&personPublicId=&role=Debtor\|CoDebtor&modality=&minDaysPastDue=&origin=&page=` | Loans.Read | `ListLoansQuery` | → lista `LoanListItemDto { publicId, number, debtor, line, office, status, origin, disbursedAmount, principalBalance, daysPastDue, informativeRating?, marks[] }` (saldos de la posición diaria vigente) | | K1 |
| `GET /{id}` | Loans.Read | `GetLoanQuery` | → `LoanDto` (abajo) | `Lending.Loan.NotFound` | K1 |
| `GET /{id}/statement?date=&recordedAsOf=` | Loans.Read | `GetLoanStatementQuery` | → estado de cuenta a D con la información hasta R (FR-069): cuotas pagadas, vencidas, cubiertas por anticipo y por vencer; días de mora; saldos por concepto; calificación informativa y oficial; etapa; garantías; bloqueos; estado de libranza y no aplicados; versiones del plan | | K1 |
| `GET /{id}/settlement?date=&recordedAsOf=` | Payoffs.Read | `GetLoanSettlementQuery` | → `LoanSettlementDto` (abajo); auditado `Lending.Payoff.Viewed` con D, R y la huella | `NationalValues.Missing` (sólo si D ≤ hoy) | K1 |
| `GET /{id}/schedule?version=` | Loans.Read | `GetRepaymentScheduleQuery` | → `{ version, validFrom, reason, rulesVersion, installments[] }`; sin `version`, la vigente; `GET /{id}/schedule/versions` lista todas | | K1 |
| `GET /{id}/operations?kind=&page=` | Loans.Read | `ListLoanOperationsQuery` | → lista `LoanOperationDto { publicId, number, kind, valueDate, accountingDate, registeredAt, amount, collector? { code, class }, meansClass?, payer?, receiptReference?, reversedBy?, reverses?, actor }` | | K1 |
| `GET /{id}/operations/{operationId}` | Loans.Read | `GetLoanOperationQuery` | → `LoanOperationDto` + `distribution: [{ installment, concept, amount }]` + `messages: [{ type, messagePublicId, status }]`: es el **comprobante de aplicación** (FR-077) | `Lending.LoanOperation.NotFound` | K1 |
| `GET /{id}/transactions?from=&to=&concept=&page=` | Loans.Read | `ListLoanTransactionsQuery` | → lista `{ kind, concept, installment?, amount, valueDate, accountingDate, registeredAt, operationPublicId, reversesTransaction?, reversedBy?, messagePublicId? }` (el libro, sólo lectura) | | K1 |
| `GET /{id}/rating?date=&recordedAsOf=` | Ratings.Read | `GetLoanRatingQuery` | → `{ date, informative: { category, subcategory?, cause, candidates[] }, official?: { category, closePublicId, month }, model, label: "Informativa" }` | | K1 |

**Liquidación** (FR-068, FR-011): `ReconstruccionDeCredito` a D con la información hasta R; consultar no
escribe. D futura proyecta sin pagos y devuelve `validUntil`.

```json
{ "loanPublicId": "…", "number": "CR-000123", "date": "2026-12-15", "recordedAsOf": "2026-12-15T15:02:11Z",
  "rulesVersion": "LND-2026.1", "fingerprint": "sha256:7ab3…",
  "toPayOff": 8123456.78,
  "byConcept": { "Principal": 7900000.00, "CurrentInterest": 98765.43, "DefaultInterest": 12345.67,
                 "CurrentInterestInSuspense": 0, "DefaultInterestInSuspense": 0, "Insurance": 3600.00,
                 "CollectionFee": 0, "OtherCharge": 0 },
  "credits": { "Advance": 0, "CreditBalance": 0 },
  "byInstallment": [{ "number": 12, "dueDate": "2026-12-05", "Principal": 233144.10, "DefaultInterest": 12345.67 }],
  "explanation": [{ "concept": "CurrentInterest", "from": "2026-12-05", "to": "2026-12-15", "days": 10,
                    "base": 7900000.00, "effectiveAnnual": 0.245000, "dailyRate": 0.000601264150, "cappedByUsury": false }],
  "validUntil": null, "warnings": [] }
```

`LoanDto`: `{ publicId, number, status: PendingDisbursement \| Active \| PaidOff \| WrittenOff \| Voided \|
Novated, origin: Origination \| Commercial \| Migration, debtor, coDebtors[], office, line { publicId, code,
name }, modality, subsegment, terms { version, rateKind, effectiveAnnual, indexCode?, margin?, dayCount,
rateEquivalence, rounding, allocationOrder, prepaymentPreference, paymentForm, rulesVersion }, amounts {
approved, disbursed, tranches }, marks { restructured, restructuringCount, validationFailed, inGrace,
suspended, payrollPendingTransfer, migratedNotActivated }, links { application?, novatedFrom?, novatedTo?,
paidOffBy?, pays[] }, collaterals[], promissoryNote?, payroll?, stage?, blocks[], invoiceReference? }`.

### 8.2 Validación fallida de una venta (FR-052)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /{id}/validation-failure/confirm` | ValidationFailures.Confirm | `ConfirmValidationFailureCommand` | `{ reason }` → `LoanDto` sin la marca; la consulta de validación de Comercial pasa a `Validated` «confirmada por excepción» (B8). Si el monto supera la atribución operativa, la obligación pasa a decisión (§4.3) | `Lending.Loan.NotValidationFailed`, `Lending.Attribution.NotExceptionable` (`data: { reasons[] }`: listas, usura, autorización, capacidad), `Lending.Segregation.Conflict` | K1 |

### 8.3 Preferencia del deudor para abonos a capital (FR-035, FR-075, D24)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /{id}/prepayment-preference` | Loans.Read | `GetPrepaymentPreferenceQuery` | → `{ current: { choice, since, origin }, history[] }` | | K1 |
| `POST /{id}/prepayment-preference` | PrepaymentPreferences.Manage | `ChangePrepaymentPreferenceCommand` | `{ choice: ReduceTerm \| ReduceInstallment, reason?, evidenceAttachmentPublicId? }` → 201 la versión nueva (`LND_PrepaymentPreferences`, H); es una `LoanOperation` `PreferenceChange` | `Lending.Loan.NotActive` | K1 |

La preferencia es instrucción permanente y revocable del deudor; la entidad nunca decide por él (FR-075).

### 8.4 Anulación de un crédito pendiente de giro (FR-048)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /{id}/void` | Disbursements.Void | `VoidPendingLoanCommand` | `{ reason }` → `LoanDto` en `Voided`; sólo `PendingDisbursement` tras un giro rechazado, cheque no cobrado o anulado, sin otro tramo vivo | `Lending.Loan.NotPendingDisbursement` | K1 |

---

## 9. Desembolsos, órdenes de giro y archivos — `/api/lending` (FR-045–FR-048, D27, T78)

### 9.1 Desembolso — `/disbursements`

Desembolsar sella condiciones y plan, crea o amplía el crédito, recoge los créditos propios por el contrato,
emite la orden de giro para Tesorería (`OrdenDeGiroEmitida`) y el hecho `CreditoDesembolsado`, en una
transacción. El desembolso **en especie** sólo nace de una venta de Comercial (§26).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /applications/{id}/disbursement-preview` | Disbursements.Execute | `PreviewDisbursementQuery` | `DisbursementInput` → `{ usury { rate, passes }, approval { expiresAt, valid }, collaterals { perfected, pending[] }, payoffs: [{ loanPublicId, number, toPayOff, daysPastDue, requiresRestructuring }], netToDisburse, schedule, prevalidation { applies, postable?, responded, errors[], warnings[] }, segregation { conflict? }, warnings[] }` (sin clave; no escribe) | `Lending.Application.NotFound` | K1 |
| `POST /disbursements` | Disbursements.Execute | `ExecuteLoanDisbursementCommand` | `DisbursementInput` + `acknowledgeWarnings?` → 201 `DisbursementResultDto` (abajo) | `Lending.Application.NotApproved`, `.ApprovalExpired` (`data: { expiredAt }`), `Lending.Collateral.NotPerfected` (`data: { pending: [{ kind, reason }] }`), `Lending.Adapter.SimulatedInProduction`, `Lending.Usury.Exceeded`, `Lending.Concentration.LimitExceeded`, `.TechnicalCapitalUnavailable`, `Lending.Payoff.RequiresRestructuring` (`data: { loans[] }`, hasta K3), `Lending.Disbursement.TrancheNotAllowed`, `.ExceedsApproved` (`data: { approved, disbursed, requested }`), `Lending.Segregation.Conflict`, `Lending.AmountLimit.Exceeded`, `Lending.Office.WithoutAccountingBranch`, `Lending.Prevalidation.NotPostable`, `NationalValues.Missing` | K1 |
| `GET /disbursements?loanPublicId=&from=&to=&form=&page=` | Loans.Read | `ListLoanDisbursementsQuery` | → lista `{ operationPublicId, number, loan, tranche, amount, netAmount, form, valueDate, order { publicId, status }, reversed }` | | K1 |

```
// DisbursementInput
{ "applicationPublicId": "…", "loanPublicId": null, "trancheAmount": null,
  "valueDate": "2026-12-01", "form": "Transfer",
  "beneficiary": { "personPublicId": "…", "bankPublicId": "…", "accountType": "Savings", "accountNumber": "0123456789" },
  "ownLoanPayoffs": [{ "loanPublicId": "…", "mode": "Full" }],
  "expectedPreviewSha256": "a91c…" }
// DisbursementResultDto
{ "loanPublicId": "…", "loanNumber": "CR-000124", "operationPublicId": "…", "operationNumber": "DS-000981",
  "tranche": 1, "grossAmount": 12000000.00, "payoffs": [{ "loanPublicId": "…", "number": "CR-000087",
  "applied": 3456789.01, "receiptOperationPublicId": "…" }], "netToDisburse": 8543210.99,
  "order": { "publicId": "…", "number": "OG-000412", "status": "Issued" },
  "scheduleVersion": 1, "messages": [{ "type": "CreditoDesembolsado", "messagePublicId": "…" }],
  "prevalidation": { "applies": true, "postable": true }, "warnings": [] }
```

- **Formas** (FR-045): `Transfer` (a la cuenta del deudor o de un tercero), `Check`, `ThirdPartyPayment` (a
  un proveedor o vendedor, beneficiario persona del maestro). La cuenta destino se propone desde la persona y
  se sella en la orden (I10).
- **Tramos**: `loanPublicId` + `trancheAmount` desembolsa el siguiente tramo de un crédito cuya línea lo admite;
  cada tramo es una operación y una versión del plan (D12).
- **Recogida** (FR-046): cada crédito recogido se paga por el contrato con la clase de recaudador
  `OwnLoanPayoff` y clave `CAR:{operaciónDeDesembolso:N}:{obligationPublicId:N}`; los créditos quedan
  enlazados. Un recogido en mora es reestructuración: hasta K3 responde
  `Lending.Payoff.RequiresRestructuring` («requiere reestructuración»).
- **Validación previa** (FR-127, T56): con `Contabilidad.FaltaDeRegla = Bloquear`, la falta de regla bloquea
  el desembolso; con `Advertir` sale en `warnings` y se sella `PrevalidationOutcome`.
- **Segregación**: el par `Decidir × Desembolsar` impide que la persona que decidió desembolse el mismo
  expediente.

### 9.2 Órdenes de giro — `/disbursement-orders`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&loanPublicId=&from=&to=&withoutResultDays=&page=` | Loans.Read | `ListDisbursementOrdersQuery` | → lista `DisbursementOrderDto { publicId, number, loan, tranche, form, beneficiary { person, bank?, accountType?, accountMasked? }, netAmount, issuedAt, status: Issued \| Disbursed \| Rejected \| CheckNotCashed \| Voided, fileReference?, previousOrder?, results: [{ outcome, reference, at, messagePublicId }] }` | | K1 |
| `GET /{id}` | Loans.Read | `GetDisbursementOrderQuery` | → `DisbursementOrderDto` con el número de cuenta completo | `Lending.DisbursementOrder.NotFound` | K1 |
| `POST /{id}/void` | DisbursementOrders.Void | `VoidDisbursementOrderCommand` | `{ reason }` → `DisbursementOrderDto`; emite `AnulacionDeOrdenDeGiro` (Related = la orden). Queda `Issued` hasta que Tesorería responda «anulada» o «girada» | `Lending.DisbursementOrder.NotVoidable` (`data: { status }`), `.InFile` (`data: { filePublicId }`: anule primero el archivo), `Lending.Segregation.Conflict` | K1 |
| `POST /{id}/reissue` | Disbursements.Execute | `ReissueDisbursementOrderCommand` | `{ form, beneficiary, reason }` → 201 la orden nueva (Related = la anterior), para un crédito `PendingDisbursement` tras un rechazo | `Lending.DisbursementOrder.NotReissuable`, `Lending.AmountLimit.Exceeded` | K1 |

El resultado lo informa Tesorería con `ResultadoDelGiro` (sin ruta, §26): «rechazada», «cheque no cobrado» o
«anulada» reversan el desembolso desde su origen (`DesembolsoReversado`) y dejan el crédito
`PendingDisbursement`; «girada» después de una anulación no reversa (se gestiona como pago, FR-048).
Sin resultado en `Giros.DiasSinResultado`, alerta `Cartera.GiroSinResultado`.

### 9.3 Archivos de desembolso — `/disbursement-files` (FR-047, T78)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&from=&to=&page=` | DisbursementFiles.Generate | `ListDisbursementFilesQuery` | → lista `{ publicId, number, format { publicId, code, bank }, sourceAccount { code, name, accountMasked }, orders, total, sha256, status: Generated \| Voided, hasResults, generatedBy, generatedAt }` | | K1 |
| `GET /source-accounts?formatPublicId=` | DisbursementFiles.Generate | `ListDisbursementSourceAccountsQuery` | → las cuentas bancarias del plan del banco del formato (por `IContabilidadParaCartera.CuentasBancariasAsync`) | | K1 |
| `POST /preview` | DisbursementFiles.Generate | `PreviewDisbursementFileQuery` | `{ formatPublicId, sourceAccountPublicId, orderPublicIds?[] }` → `{ lines[], pending: [{ order, reason: BankCodeMissing \| NoBankAccount \| AlreadyInFile }], total }` (sin clave) | `Core.BankFileFormat.NotFound` | K1 |
| `POST /` | DisbursementFiles.Generate | `GenerateDisbursementFileCommand` | `{ formatPublicId, sourceAccountPublicId, orderPublicIds[], reason? }` → 201 el archivo (adjunto `LendingDisbursementFile` subido **antes** de guardar la fila, con SHA-256); cada orden lleva la referencia del archivo | `Lending.DisbursementFile.AccountNotOfFormatBank`, `.OrderAlreadyInFile` (`data: { orders[] }`), `Core.BankFileFormat.ScopeMismatch` | K1 |
| `POST /{id}/download-link` | DisbursementFiles.Generate | `IssueDisbursementFileDownloadLinkCommand` | → `{ url, expiresAt }`; auditado | | K1 |
| `POST /{id}/void` | DisbursementFiles.Generate | `VoidDisbursementFileCommand` | `{ reason }` → 204; sólo antes del primer resultado de Tesorería; las órdenes quedan libres para otro archivo | `Lending.DisbursementFile.HasResults` | K1 |

Formatos: los de `/api/core/bank-file-formats?scope=LoanDisbursement` (§24). Una orden va a lo sumo a un archivo
no anulado, y Tesorería no la paga por otra vía (FR-045).

---

## 10. Pagos por pantalla de Cartera (FR-071–FR-082, T31, CH47)

**No hay ruta para aplicar pagos.** Todo dinero que reduce una obligación entra por `IContratoDeObligaciones`
(Recaudo, Nómina, Aportes, recogida, recaudador de prueba), que corre en proceso dentro de la unidad de trabajo
del recaudador y no tiene HTTP (§26). Cartera ofrece por pantalla sólo **simular** y **reversar**; en ambientes
de prueba, el recaudador de prueba ejercita el contrato por las rutas de §20.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /loans/{id}/payment-simulations` | Payments.Simulate | `SimulateLoanPaymentQuery` | `{ modality: FullPayoff \| PastDue \| CycleInstallment \| Advance \| ExtraordinaryPrincipal \| SpecificInstallment \| FreeAmount, amount?, valueDate, prepaymentChoice?, installmentNumber? }` → `PaymentSimulationDto` (abajo); sin clave | `Lending.Payment.ValueDateOutOfRange` (`data: { minDate }`), `Lending.Payment.SpecificInstallmentBlocked` (`data: { pendingBefore[] }`), `NationalValues.Missing` | K1 |
| `POST /loans/{id}/operations/{operationId}/reversal-preview` | Payments.Reverse | `PreviewLoanPaymentReversalQuery` | `{ reason? }` → `{ previewSha256, reversal { byConcept }, reallocations: [{ operationPublicId, number, before[], after[] }], documentsToVoid: [{ publicId, kind, number }], ratingNow, ratingAfter, notifiesCollector }` (sin clave) | `Lending.Reversal.OfReversal`, `.OwnedByCollector` (`data: { collectorClass: "Payroll" }`) | K1 |
| `POST /loans/{id}/operations/{operationId}/reverse` | Payments.Reverse | `ReverseLoanPaymentCommand` | `{ reason, expectedPreviewSha256 }` → 201 `{ reversalOperationPublicId, number, reallocations[], voidedDocuments[], messages: [{ type: "PagoReversado" \| "PagoReversadoPorCartera", messagePublicId }] }` | `Lending.Reversal.OfReversal`, `.OwnedByCollector`, `Lending.Reversal.PreviewChanged` (`data: { previewSha256 }`), `Lending.LoanOperation.NotPayment` | K1 |

`PaymentSimulationDto` (la misma función distribuye al simular y al aplicar, FR-072):

```json
{ "simulationId": "sha256:4e0d…", "modality": "ExtraordinaryPrincipal", "amount": 2000000.00, "valueDate": "2026-12-15",
  "distribution": [{ "installment": 12, "concept": "DefaultInterest", "amount": 12345.67 },
                   { "installment": null, "concept": "Principal", "amount": 1987654.33 }],
  "surplus": 0, "advance": 0, "creditBalance": 0,
  "resultingBalances": { "Principal": 5912345.67 },
  "alternatives": { "ReduceTerm": { "installments": 22, "schedule": [] }, "ReduceInstallment": { "installmentAmount": 301234.00, "schedule": [] } },
  "notices": [{ "code": "Lending.Payment.AdvanceDoesNotReduceInterest", "message": "…" }],
  "validUntil": "2026-12-15" }
```

- **Abono extraordinario** (FR-075): devuelve los dos planes; el deudor elige (`prepaymentChoice`). Sin elección
  ni preferencia registrada, el valor queda como saldo a favor (`Lending.Payment.PreferenceMissing` como aviso).
- **Reverso** (FR-080, FR-081, T33): deja el crédito exactamente como si el pago no hubiera existido; los pagos
  posteriores se redistribuyen con su fecha valor y se muestran antes de confirmar; los documentos cuyo corte
  es igual o posterior al pago reversado se anulan (`VigenciaDeDocumentos`). Un pago de **Nómina** sólo lo
  reversa Nómina (`Lending.Reversal.OwnedByCollector`). El reverso que inicia Cartera emite
  `PagoReversadoPorCartera` al recaudador.
- **Fecha valor antes de un cierre** (FR-012, D16): el reverso no la necesita; un pago con fecha valor anterior
  a un cierre exige `Lending.Payments.BackdateBeforeClose`, y eso lo revisa el contrato con los permisos y el
  límite del usuario del recaudador (no su alcance) por `IPermissionChecker`; lo describe `interfaces.md` (C41).

---

## 11. Novedades, condonaciones, castigo y eventos del deudor — `/api/lending` (K3; FR-090–FR-098, T102)

### 11.1 Novedades — `/novelties` (FR-090–FR-093)

Ciclo (`LoanNoveltyStatus`): `Draft` → `Submitted` → `Approved` · `Rejected` → `Applied` · `Cancelled`.
La **causa** la calcula el sistema (`Restructuring` si la motiva el deterioro de la capacidad de pago, cualquiera
sea el mecanismo; si no, `Modification`). Enums de `data-model.md` (§27): `NoveltyMechanism` (`Extension`,
`PaymentDateChange`, `RateChange`, `Novation`, `Refinancing`), `NoveltyCause` (`Restructuring`, `Modification`),
`LoanNoveltyStatus`. El acuerdo de insolvencia no es un mecanismo: es una novedad `Restructuring` con su proceso
(`insolvencyProceedingPublicId`, §11.4).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&cause=&loanPublicId=&page=` | Novelties.Read | `ListLoanNoveltiesQuery` | → lista `{ publicId, number, loans[], mechanism, cause, status, requestedBy, requestedAt, level? }` | | K3 |
| `GET /{id}` | Novelties.Read | `GetLoanNoveltyQuery` | → `LoanNoveltyDto { …, cause, reason, newTerms, simulation?, consents: [{ party, role, consented?, attachment?, at? }], routing?, decisions[], appliedOperation?, newLoan? }` | `Lending.Novelty.NotFound` | K3 |
| `POST /` | Novelties.Request | `RequestLoanNoveltyCommand` | `{ loanPublicIds[], mechanism, requestMotive: Hardship \| DebtorRequest (va a `CauseDeterminationJson`; la causa la calcula el sistema), newTerms: { rate?, installments?, periodicity?, paymentDay?, graceKind?, payoffLoans?[] }, insolvencyProceedingPublicId?, reason }` → 201 `LoanNoveltyDto` en `Draft` | `Lending.Novelty.ModificationNotAllowed` (`data: { recentDaysPastDue, limit }`), `Lending.Loan.NotActive` | K3 |
| `POST /{id}/simulate` | Novelties.Request | `SimulateLoanNoveltyQuery` | → `{ cause, newSchedule, ratingNow, ratingAfter, restructuringCount, usury, concentration, prevalidation, warnings[] }` (sin clave) | `Lending.Usury.Exceeded` | K3 |
| `POST /{id}/submit` | Novelties.Request | `SubmitLoanNoveltyCommand` | → `LoanNoveltyDto` en `Submitted` con `routing` (vinculados: consejo) | `Lending.Usury.Exceeded`, `Lending.Concentration.LimitExceeded` | K3 |
| `POST /{id}/consents` | Novelties.Request | `RegisterNoveltyConsentCommand` | `{ partyPersonPublicId, consented, attachmentPublicId, addendumAttachmentPublicId? }` → 201 (adjunto `LendingNoveltyConsent`) | `Lending.Novelty.PartyUnknown` | K3 |
| `POST /{id}/decisions` | Novelties.Read (+ nivel) | `RecordCreditDecisionCommand` | §4.3 | §4.3 | K3 |
| `POST /{id}/coverage-exception` | Novelties.Read (+ nivel de excepción) | `ApproveNoveltyCoverageExceptionCommand` | `{ reason }` (con clave) → `LoanNoveltyDto`. Bloque D (decisión del dueño del 2026-10-15): la excepción de cobertura la aprueba **otra persona** antes de aplicar, con la atribución del nivel de excepción de la matriz vigente; queda como decisión individual `Approve` con `isPolicyException` sobre la novedad (en `decisions[]` con `policyException: true`). Sólo con la novedad `Approved`, una vez, y quien la radicó no la aprueba | `Lending.Novelty.NotApproved`, `.AlreadyApplied`, `.CoverageExceptionExists`, `.CoverageInsufficient` (la matriz no tiene nivel de excepción), `Lending.Attribution.LevelRequired`, `Lending.Segregation.Conflict` | K3 |
| `POST /{id}/apply` | Novelties.Apply | `ApplyLoanNoveltyCommand` | `{ withoutConsent?: [{ partyPersonPublicId, decision: DoNotApply \| ApplyWithGuaranteeExtinguished, reason, collateralPublicId?, useCoverageException? }], acknowledgeWarnings? }` (`useCoverageException`, bloque D: si la cobertura restante no cumple la línea, usa la excepción que aprobó otra persona por `/coverage-exception` y la sella en `ExceptionDecisionId` del consentimiento; sin ella, `Lending.Novelty.CoverageExceptionMissing`; si la aprobó quien aplica —usuario o persona—, `.CoverageExceptionSameUser`) → `{ operationPublicId, scheduleVersion, newLoanPublicId?, messages[] }`; emite `CreditoReestructurado`, `CreditoModificado` o `CreditoNovado` | `Lending.Novelty.NotApproved`, `.ConsentMissing` (`data: { parties[] }`), `.CoverageInsufficient` (`data: { required, remaining }`), `.CoverageExceptionMissing` (`data: { required, remaining }`), `.CoverageExceptionSameUser`, `Lending.Prevalidation.NotPostable`, `Lending.Segregation.Conflict` | K3 |
| `POST /{id}/cancel` | Novelties.Request | `CancelLoanNoveltyCommand` | `{ reason }` → 204 | `Lending.Novelty.AlreadyApplied` | K3 |

Una novación o refinanciación crea un crédito enlazado que hereda marca, conteo, calificación e historial de
mora (FR-090); sus garantías se constituyen aparte (FR-092). Los costos de la reestructuración no se trasladan al
deudor (FR-091).

### 11.2 Condonaciones — `/waivers` (FR-094)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&loanPublicId=&page=` | Loans.Read | `ListWaiversQuery` | → lista `{ publicId, number, loan, concepts: [{ concept, amount }], status, level?, requestedBy, appliedAt? }` | | K3 |
| `POST /` | Condonations.Request | `RequestWaiverCommand` | `{ loanPublicId, concepts: [{ concept: DefaultInterest \| CurrentInterest \| CurrentInterestInSuspense \| DefaultInterestInSuspense \| CollectionFee \| Insurance \| Principal, amount }], reason, supportAttachmentPublicId? }` → 201 con `routing` | `Lending.Waiver.ExceedsBalance` (`data: { concept, balance }`) | K3 |
| `POST /{id}/decisions` | Loans.Read (+ nivel) | `RecordCreditDecisionCommand` | §4.3 | §4.3 | K3 |
| `POST /{id}/apply` | Condonations.Apply | `ApplyWaiverCommand` | `{ acknowledgeWarnings? }` → `{ operationPublicId, messages: [CondonacionAprobada] }` | `Lending.Waiver.NotApproved`, `Lending.Prevalidation.NotPostable`, `Lending.Segregation.Conflict` | K3 |
| `POST /{id}/cancel` | Condonations.Request | `CancelWaiverCommand` | `{ reason }` → 204 | | K3 |

### 11.3 Castigo — `/write-off-batches` (FR-095, FR-096)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&page=` | Loans.Read | `ListWriteOffBatchesQuery` | → lista `{ publicId, number, status: Draft \| Submitted \| Approved \| Rejected \| Applied \| Cancelled, items, total, preparedBy, sessionItem?, appliedAt? }` | | K3 |
| `GET /{id}` | Loans.Read | `GetWriteOffBatchQuery` | → el lote con `items: [{ loan, balance, impairment, daysPastDue, requirements: [{ code, met, evidence? }] }]` y `rejected: [{ loan, missing[], reason }]` (también los excluidos al aplicar) | `Lending.WriteOffBatch.NotFound` | K3 |
| `POST /` | WriteOffs.Prepare | `CreateWriteOffBatchCommand` | `{ loanPublicIds[], reason, reports: [{ loanPublicId, irrecoverabilityAttachmentPublicId, reportAttachmentPublicId }] }` → 201; los que no cumplen quedan en `rejected` con lo que falta; `warnings: [{ code, message, whoFixes? }]` (bloque D) con lo de la norma y la política que sigue pendiente de validar —`WriteOffReportTermPending`, `WriteOffMinimumArrearsPending`—, sin bloquear | | K3 |
| `POST /{id}/items` · `DELETE /{id}/items/{itemId}` | WriteOffs.Prepare | `AddWriteOffBatchItemCommand` · `RemoveWriteOffBatchItemCommand` | ídem por crédito, sólo en `Draft` | `Lending.WriteOff.RequirementMissing` (`data: { missing[] }`, C98), `Lending.WriteOffBatch.NotDraft` | K3 |
| `POST /{id}/submit` | WriteOffs.Prepare | `SubmitWriteOffBatchCommand` | → el lote en `Submitted` (congelado para llevarlo al consejo, §5) | | K3 |
| `POST /{id}/void` | WriteOffs.Prepare | `VoidWriteOffBatchCommand` | `{ reason }` (con clave) → el lote en `Cancelled`. Bloque D: se anula mientras no se aplique (`Draft`, `Submitted` o `Approved`); el motivo va a la auditoría del comando; sus créditos quedan libres para otro lote | `Lending.WriteOffBatch.InvalidStatus`, `Validation.Invalid` (sin motivo) | K3 |
| `POST /{id}/apply` | WriteOffs.Apply | `ApplyWriteOffBatchCommand` | `{ sessionItemPublicId }` → `{ operations[], messages: [CreditoCastigado], excluded: [{ loanPublicId, loanNumber, missing[], reason }] }`; los requisitos se vuelven a verificar y el crédito que ya no cumple (o ya no está vigente) se **excluye** con su motivo —ítem `Rejected`, evento `Lending.WriteOffItem.Excluded`— y el resto se castiga (bloque D); si no queda ninguno, el lote no se aplica. Si el tipo de entidad debe reportar castigos, alerta `Cartera.PlazoRegulatorioPorVencer` | `Lending.WriteOff.ApprovalRequired`, `Lending.WriteOff.NoLoansLeft` (`data: { batchNumber, excluded[] }`), `Lending.Session.ItemMismatch`, `Lending.Segregation.Conflict` (`ArmarLoteDeCastigo × AplicarCastigo`), `Lending.Prevalidation.NotPostable` | K3 |

La cartera castigada sigue en cobranza; sus pagos entran por el contrato y emiten `RecuperacionDeCastigada`
desde K1 (FR-096); la vista `charge-offs` la sigue (§17).

### 11.4 Eventos del deudor, reclamación al seguro e insolvencia — `/debtor-events`, `/insurance-claims`, `/insolvency-proceedings` (FR-097, FR-098, T102)

Las tablas `LND_InsuranceClaims` y `LND_InsolvencyProceedings` nacen en K1 para lo migrado y para lo que
bloquea; su registro y trámite por pantalla son de K3. En K1 se leen en el estado de cuenta.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /debtor-events?personPublicId=&kind=&page=` | Loans.Read | `ListDebtorEventsQuery` | → lista `{ publicId, person, kind: Deceased \| AssociateWithdrawal \| Garnishment (los valores de `DebtorBlockReason`: el evento se guarda como bloqueo del deudor), eventDate, registeredBy, support?, effects[] }` | | K3 |
| `POST /debtor-events` | DebtorEvents.Manage | `RegisterDebtorEventCommand` | `{ personPublicId, kind, eventDate, supportAttachmentPublicId, insurerPersonPublicId? }` → 201 con los efectos: fallecimiento bloquea cobranza, mensajes y reporte negativo y abre la reclamación `InProcess`; retiro y embargo bloquean el cupo | `Lending.DebtorEvent.Duplicate` | K3 |
| `GET /insurance-claims?status=&loanPublicId=&page=` | Loans.Read | `ListInsuranceClaimsQuery` | → lista `{ publicId, loan, insurer, status: InProcess \| Paid \| Denied \| Withdrawn, openedAt, closedAt?, paymentOperation? }` | | K1 |
| `POST /insurance-claims/{id}/status` | DebtorEvents.Manage | `ChangeInsuranceClaimStatusCommand` | `{ status: Denied \| Withdrawn, reason, supportAttachmentPublicId? }` → la reclamación. `Paid` lo pone sólo el contrato cuando la aseguradora paga | `Lending.InsuranceClaim.NotInProcess` | K3 |
| `GET /insolvency-proceedings?status=&personPublicId=&page=` | Loans.Read | `ListInsolvencyProceedingsQuery` | → lista `{ publicId, person, status: Negotiating \| Agreement \| Failed, startDate, authority, loans[], noveltyPublicId? }` | | K1 |
| `POST /insolvency-proceedings` | Insolvency.Manage | `OpenInsolvencyProceedingCommand` | `{ personPublicId, startDate, authority, fileNumber, supportAttachmentPublicId }` → 201 en `Negotiating`: suspende causación y cobro y mantiene la calificación | `Lending.Insolvency.AlreadyOpen` | K3 |
| `POST /insolvency-proceedings/{id}/failure` | Insolvency.Manage | `RecordInsolvencyFailureCommand` | `{ date, reason, supportAttachmentPublicId }` → en `Failed`; la calificación pasa a la que fija la norma. El bloqueo `Insolvency` de crédito nuevo **se mantiene** (decisión del dueño del 2026-10-15): lo levanta una persona con `Lending.Blocks.Manage` y motivo por `POST /blocks/{id}/lift` (§7.2) | `Lending.Insolvency.NotNegotiating` | K3 |

El acuerdo de insolvencia es una novedad `Restructuring` ligada al proceso (`LND_LoanNovelties.InsolvencyProceedingId`,
§11.1) que, al aplicarse, cierra el proceso en `Agreement` y levanta solo el bloqueo `Insolvency` de crédito nuevo.

---

## 12. Riesgo y cierres — `/api/lending/risk` (FR-099–FR-107, T45–T50)

### 12.1 Política SARC de la cooperativa — `/risk/policy` (T45)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?date=` | Parameters.View | `GetRiskPolicyQuery` | → `{ effective: { anexo1 { documentPublicId, sha256, verified }, overrides, combined }, versions: [{ publicId, validFrom, status: PendingValidation \| Validated, sessionItem?, registeredBy }] }` (`PoliticaSarcEfectiva.Combinar`) | | K1 |
| `POST /versions` | RiskPolicy.Manage | `AddRiskPolicyVersionCommand` | `{ validFrom, overrides (esquema parcial del Anexo), reason }` → 201 en `PendingValidation` | `Lending.RiskPolicy.LessStrict` (`data: { cells: [{ path, norm, proposed }] }`), `.SchemaInvalid` | K1 |
| `POST /versions/{id}/validate` | RiskPolicy.Validate | `ValidateRiskPolicyVersionCommand` | `{ sessionItemPublicId }` → la versión `Validated`; rige desde `validFrom` | `Lending.Session.ItemMismatch`, `Lending.Segregation.Conflict` | K1 |

### 12.2 Calificación informativa y por análisis — `/risk/ratings`, `/risk/analysis-ratings`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /ratings?date=&recordedAsOf=&line=&office=&category=&cause=&page=` | Ratings.Read | `ListDailyRatingsQuery` | → lista `{ loan, debtor, modality, daysPastDue, category, subcategory?, cause, officialCategory?, label: "Informativa" }` (no se guarda, T46) | | K1 |
| `GET /analysis-ratings?active=&page=` | Ratings.Read | `ListAnalysisRatingsQuery` | → lista `{ publicId, loan, category, subcategory?, reason, support?, since, liftedAt? }` | | K1 |
| `POST /analysis-ratings` | Ratings.Override | `SetAnalysisRatingCommand` | `{ loanPublicId, category, subcategory?, reason, supportAttachmentPublicId }` → 201 | `Lending.AnalysisRating.NotWorse` (`data: { ruleCategory }`: nunca a una mejor) | K1 |
| `POST /analysis-ratings/{id}/lift` | Ratings.Override | `LiftAnalysisRatingCommand` | `{ reason }` → 204 | `Lending.AnalysisRating.NotActive` | K1 |

### 12.3 Cierre mensual — `/risk/monthly-closes` (FR-107, T47–T49)

El cierre es una corrida `MonthlyClose` del `ProcesadorDeCartera`: la orden encola (202) y el proceso corre con
la persona que ordenó como actor. Encabezado y líneas nunca cambian; el estado `Reopened` se deriva de la
reapertura y volver a cerrar crea `Version + 1`.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?year=&kind=` | Ratings.Read | `ListMonthlyClosesQuery` | → arreglo `{ publicId, kind: Base \| Monthly, year, month, version, status: Closed \| Reopened, informationCutoffAt, marks { obligatoryModelUnavailable, unvalidatedParameters, incompleteHistory }, totals { loans, grossPortfolio, individualImpairment, generalImpairment } }` | | K1 |
| `GET /{year}/{month}/readiness` | MonthlyClose.Execute | `GetMonthlyCloseReadinessQuery` | → `{ ready, requirements: [{ code: PreviousClose \| AccountingPeriodOpen \| AccrualUpToDate \| ReplicaSynced \| NoRunInProgress, met, detail }] }` | | K1 |
| `POST /{year}/{month}/close` | MonthlyClose.Execute | `OrderMonthlyCloseCommand` | `{ reason? }` → **202** `{ processRunPublicId, status: Requested }` | `Lending.MonthlyClose.PreviousMissing`, `.AccrualBehind`, `.AccountingPeriodClosed`, `.ReplicationUnavailable` (uno por requisito de `readiness`), `Lending.Process.AlreadyRunning` (`data: { processRunPublicId }`) | K1 |
| `GET /{id}` | Ratings.Read | `GetMonthlyCloseQuery` | → el encabezado con R, marca de agua de valores nacionales, documentos usados con SHA-256, política y modelo por segmento, reaperturas y partes emitidas | `Lending.MonthlyClose.NotFound` | K1 |
| `GET /{id}/loans?category=&page=` | Ratings.Read | `ListMonthlyCloseLoansQuery` | → lista por crédito: categoría, subcategoría, causa, días, base, garantías admisibles, deterioro individual, vigencia de la línea | | K1 |
| `GET /{id}/changes?page=` | Ratings.Read | `GetMonthlyCloseChangesQuery` | → reporte de cambios: `{ loan, debtor, previous, current, cause }` frente al cierre anterior (el primero, frente al cierre base) | | K1 |
| `GET /{id}/reconciliation` | Reconciliation.View | `GetLendingAccountingReconciliationQuery` | → el cuadre con Contabilidad al último día del mes (§16.3) | | K1 |
| `POST /{id}/reopen` | MonthlyClose.Reopen | `ReopenMonthlyCloseCommand` | `{ reason, sessionItemPublicId? }` → **202** `{ processRunPublicId, status: Requested }`: sólo encola la corrida `CloseReopen` (C58). La transacción única de T47 ocurre **dentro de la corrida**: inserta `LND_MonthlyCloseReopenings`, emite `ReversoDeCierre` por partes y `CierreDeCarteraReabierto`, y crea su propio lote `LendingMonthlyClose` con `BatchScopeKey` `LendingClose:{aaaa-mm}` | `Lending.MonthlyClose.NotLast`, `.AccountingPeriodClosed` (Contabilidad ya cerró el período), `Lending.Process.AlreadyRunning`, `Lending.Attribution.LevelRequired` | K1 |

- **Requisitos para ordenar** (T47): cierre anterior vigente (el mensual previo o, en el primero tras activar,
  el cierre base, aunque sea del mismo mes, T49), período contable abierto, causación al día hasta el fin de
  mes y réplica de valores nacionales sincronizada. Cada requisito incumplido responde su código de
  `data-model.md` §10.1 (`PreviousMissing`, `AccountingPeriodClosed`, `AccrualBehind`, `ReplicationUnavailable`:
  si la central no responde, el cierre se niega con motivo, B7). La corrida revalida lo mismo al empezar.
- **Cierre programado**: con `Riesgo.CierreProgramado` lo crea la tarea `cartera.cierre-programado`; por defecto
  es manual y pasado `Riesgo.DiaLimiteCierre` alerta `Cartera.CierreMensualPendiente`.

### 12.4 Modelo de deterioro por segmento — `/risk/impairment-model` (FR-105, T50)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?date=` | Ratings.Read | `GetImpairmentModelBySegmentQuery` | → `[{ segment, model: ArrearsAnnex1 \| ExpectedLossAnnex2, basis: NotObligated \| Obligated \| Voluntary, mark?: "ObligatoryModelUnavailable", since, election? }]` (`SelectorDeModeloDeDeterioro`) | | K1 |
| `GET /elections` | Ratings.Read | `ListImpairmentModelElectionsQuery` | → arreglo de adopciones y desistimientos | | K6a |
| `POST /elections` | ImpairmentModel.Manage | `RegisterImpairmentModelElectionCommand` | `{ electionPublicId, kind: VoluntaryAdoption \| VoluntaryWithdrawal, segments[]?, validFrom, sessionItemPublicId, impactStudyAttachmentPublicId, noticeDate, noticeAttachmentPublicId, reason }` → 201 con la elección. `electionPublicId` es el sujeto del punto del acta (`ImpairmentModelChange`) que la pantalla genera al proponer; sin segmentos, los tres. **Enmienda del bloque 3 de K6a (2026-10-17)**: sin `probabilityTablesDocumentPublicId` (la PI voluntaria es la del Anexo 2 vigente, clave `VOLUNTARIA`); el estudio y el aviso son adjuntos `LendingImpairmentModelElectionSupport` de la propuesta | Los de data-model §23.8: `Lending.ImpairmentModel.IncompleteHistory` (`data: { loans[] }`; aquí decía `HistoryIncomplete`), `.NoticeOutOfTerm` (`data: { earliestValidFrom }`), `.Obligated` (aquí decía `ObligatoryNoWithdrawal`), `.NotAlternating`, `.ValidFromTaken`, `.RetroactiveValidFrom`, `.RuleMissing`, `.SegmentInvalid`, `.VoluntaryPdMissing`, `.SupportInvalid` **(nuevo)**, `Lending.Session.ItemMismatch`, `Lending.Security.UserWithoutPerson` | K6a |

Antes de K6a la ruta `POST /elections` no existe (404): ninguna cooperativa puede adoptar el modelo
voluntariamente (FR-105).

### 12.5 Historial de mora — `/risk/arrears-history` (FR-105, FR-139, FR-140)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /completeness?date=&segment=` | Ratings.Read | `GetArrearsHistoryCompletenessQuery` | → `{ date, monthsRequired, ruleDocument { publicId, code }, incomplete: [{ loan, segment, monthsAvailable, missingMonths[] }] }` (`HistorialDeMora.Construir`) | | K1 |
| `GET /template.xlsx` · `POST /import?mode=` | ArrearsHistory.Import | `ImportArrearsHistoryCommand` | plantilla `lending.arrears-history` (`numeroDeCredito`, `mes`, `moraMaximaDias`, `moraAlCierreDias`, `fuente`) → `ImportResultDto` con `extra.completeness[]` en la revisión; información, sin transacciones ni hechos; también antes de activar. Sin descarga con datos | `Import.Invalid`, `Lending.ArrearsHistory.LoanNotInLoad`, `.MonthClosed`, `.LoadClosed`, `.NoAppliedLoad` | K6a |

### 12.6 Evaluación de cartera — `/risk/evaluations` (K3; FR-102)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=` | Ratings.Read | `ListPortfolioEvaluationsQuery` | → arreglo `{ publicId, cutoffDate, effectMonth, status: Draft \| Recommended \| Approved \| Applied \| Rejected \| Discarded, loans, recommendationItem?, approvalItem? }` | | K3 |
| `GET /{id}?page=` | Ratings.Read | `GetPortfolioEvaluationQuery` | → la evaluación con sus créditos (`proposedCategory`, `reason`) | `Lending.PortfolioEvaluation.NotFound` | K3 |
| `POST /` | PortfolioEvaluations.Prepare | `PreparePortfolioEvaluationCommand` | `{ cutoffDate, effectMonth, criteria, loans: [{ loanPublicId, proposedCategory, reason }] }` → 201; `warnings: [{ code, message, whoFixes? }]` (bloque D) con `PortfolioEvaluationPeriodicityPending` si la periodicidad de la norma sigue pendiente de validar, sin bloquear | `Lending.PortfolioEvaluation.Overlaps` | K3 |
| `POST /{id}/discard` | PortfolioEvaluations.Prepare | `DiscardPortfolioEvaluationCommand` | `{ reason }` (con clave) → la evaluación `Discarded`. Bloque D: se descarta mientras no se aplique (`Draft`, `Recommended` o `Approved`); el motivo va a la auditoría del comando; libera su corte | `Lending.PortfolioEvaluation.InvalidStatus`, `Validation.Invalid` (sin motivo) | K3 |
| `POST /{id}/apply` | PortfolioEvaluations.Apply | `ApplyPortfolioEvaluationCommand` | `{ recommendationSessionItemPublicId, approvalSessionItemPublicId }` → la evaluación `Applied`; la recalificación se refleja en el cierre del mes de efecto y dura hasta la siguiente | `Lending.Session.ItemMismatch` | K3 |

---

## 13. Libranza — `/api/lending` (K2; FR-108–FR-116)

### 13.1 Pagadurías y convenios — `/payroll-agreements`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?includeInactive=` | PayrollAgreements.View | `ListPayrollAgreementsQuery` | → arreglo `{ publicId, code, name, payer { personPublicId, legalName, taxId }, isOwnPayroll, currentVersion { validFrom, cutoffDay, sendDay, payDay, transferWindowDays }, activeLoans, isActive }` | | K2 |
| `GET /{id}` | PayrollAgreements.View | `GetPayrollAgreementQuery` | → el convenio con versiones, contactos (nombre, cargo y contacto institucional) y códigos de respuesta | `Lending.PayrollAgreement.NotFound` | K2 |
| `POST /` | PayrollAgreements.Manage | `CreatePayrollAgreementCommand` | `{ code, name, payerPersonPublicId, isOwnPayroll, firstVersion }` → 201; la pagaduría es persona jurídica del maestro (la propia cooperativa para su nómina, I13) | `Catalogo.CodigoDuplicado`, `Lending.PayrollAgreement.PayerNotLegalEntity`, `.OperatorRegistrationMissing` (FR-108) | K2 |
| `POST /{id}/versions` | PayrollAgreements.Manage | `AddPayrollAgreementVersionCommand` | `{ validFrom, cutoffDay, sendDay, payDay, outboundFormatPublicId, resultFormatPublicId, deductionCap, includesPastDue, noveltyRules, transferWindowDays, notAppliedAction: Cashier \| Collections, resultCodes: [{ code, reason }], reason }` → 201 | `Lending.PayrollAgreement.VersionOverlaps`, `Core.BankFileFormat.ScopeMismatch` (los formatos deben ser de los ámbitos `PayrollDeductionRequest` y `PayrollDeductionResult`) | K2 |
| `PUT /{id}/contacts` | PayrollAgreements.Manage | `SetPayrollAgreementContactsCommand` | `{ contacts: [{ name, position, email, phone }] }` → arreglo | | K2 |
| `GET /{id}/pending-transfers?asOf=` | PayrollDeductionFiles.Read | `ListPendingPayrollTransfersQuery` | → lo descontado pendiente de giro por período, con días de mora de la pagaduría y si superó el umbral | | K2 |
| `GET /template.xlsx` · `POST /import?mode=` | PayrollAgreements.View · .Manage | `ImportPayrollAgreementsCommand` | plantilla `lending.payroll-agreements` | `Import.Invalid` | K2 |

### 13.2 Archivos de descuento — `/payroll-deduction-files` (FR-113, T78)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?agreementPublicId=&year=&month=&status=&page=` | PayrollDeductionFiles.Read | `ListPayrollDeductionFilesQuery` | → lista `{ publicId, agreement, year, month, version, status: Generated \| Superseded \| Sent \| Voided, lines, total, sha256, generatedAt, sentAt? }` | | K2 |
| `GET /{id}?page=` | PayrollDeductionFiles.Read | `GetPayrollDeductionFileQuery` | → el archivo con sus líneas (`employeeId`, crédito, cuota, valor, novedad `New \| Change \| Cancellation \| Unchanged`) | `Lending.PayrollDeductionFile.NotFound` | K2 |
| `POST /preview` | PayrollDeductionFiles.Generate | `PreviewPayrollDeductionFileQuery` | `{ agreementPublicId, year, month }` → las líneas y novedades que saldrían (sin clave) | | K2 |
| `POST /` | PayrollDeductionFiles.Generate | `GeneratePayrollDeductionFileCommand` | `{ agreementPublicId, year, month, reason? }` → 201; regenerar antes del envío crea versión y deja la anterior `Superseded`; emite `DescuentosPorPagaduria` a Recaudo | `Lending.PayrollFile.AlreadySent` (la corrección va como novedad del período siguiente, I9), `Lending.PayrollAgreement.NoCurrentVersion` | K2 |
| `POST /{id}/download-link` | PayrollDeductionFiles.Read | `IssuePayrollDeductionFileDownloadLinkCommand` | → `{ url, expiresAt }`; auditado | | K2 |
| `POST /{id}/void` | PayrollDeductionFiles.Generate | `VoidPayrollDeductionFileCommand` | `{ reason }` → 204, sólo antes del envío | `Lending.PayrollFile.AlreadySent` | K2 |

`Sent` lo marca el mensaje `EnvioDeDescuentosConfirmado` de Recaudo, y lo descontado y no aplicado llega con
`ResultadoDeDescuentos` (sin ruta, §26).

### 13.3 No aplicados y traslado — `/payroll-not-applied`, `/loans/{id}/payroll-transfer`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /payroll-not-applied?agreementPublicId=&origin=&status=&page=` | PayrollDeductionFiles.Read | `ListInstallmentNonDeductionsQuery` | → lista `{ publicId, loan, installment, collector, reason, origin: Contract \| Activation \| PaymasterResult, lastAction?: DebtorNotified \| IncludedInNextFile \| MovedToCashPayment \| SentToCollection \| Closed (`NonDeductionFollowUpAction`; sin acciones = abierta), notifiedAt?, since }` (también los de la nómina propia nacidos en K1) | | K2 |
| `POST /payroll-not-applied/{id}/resolve` | PayrollNotApplied.Manage | `ResolveInstallmentNonDeductionCommand` | `{ action: MovedToCashPayment \| SentToCollection \| Closed, reason }` (`NonDeductionFollowUpAction`) → la marca resuelta; avisa al deudor por sus canales autorizados | `Lending.InstallmentNonDeduction.AlreadyResolved` | K2 |
| `POST /loans/{id}/payroll-transfer` | PayrollTransfers.Manage | `TransferLoanPayrollCommand` | `{ newAgreementPublicId?, employeeId?, certificationAttachmentPublicId?, certificationDate?, reason }` → la operación; conserva la fecha original para la prelación; sin pagaduría nueva pasa a la forma alterna (caja) y avisa al deudor; emite `DimensionContableCambiada` si cambia con o sin libranza | `Lending.Payroll.DeductionCapacityExceeded` (`data: { available, requested }`), `Lending.Loan.NotPayroll` | K2 |

La capacidad de descuento (FR-110) se valida también al radicar por libranza: 422
`Lending.Payroll.DeductionCapacityExceeded` con la capacidad disponible.

---

## 14. Mensajes al deudor y cobranza — `/api/lending` (FR-117–FR-124, T74–T77)

### 14.1 Plantillas de mensaje — `/message-templates` (T76)

Sobre `COR_NotificationTemplates` (módulo `Lending`), versionadas; el mensaje sella `PublicId` y versión.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?purpose=&channel=&date=` | MessageTemplates.View | `ListMessageTemplatesQuery` | → arreglo `{ code, purpose, channel, version, validFrom, validTo?, subject?, body, variables[], providerTemplateId?, isSeeded, reviewPending }` | | K1 |
| `GET /{code}/history` | MessageTemplates.View | `GetMessageTemplateHistoryQuery` | → versiones | `Lending.MessageTemplate.NotFound` | K1 |
| `POST /{code}/versions` | MessageTemplates.Manage | `CreateMessageTemplateVersionCommand` | `{ purpose, channel, subject?, body, variables[], providerTemplateId?, validFrom, reason }` → 201 | `Lending.MessageTemplate.VariableNotAllowed` (`data: { variable, allowed[] }`), `.SmsTooLong` (aviso, `data: { length, limit }`), `.Overlaps` | K1 |
| `POST /preview` | MessageTemplates.View | `RenderMessageTemplatePreviewQuery` | `{ code?, body?, channel, sampleLoanPublicId? }` → `{ subject?, body, length, warnings[] }` (sin clave) | | K1 |

### 14.2 Mensajes al deudor — `/debtor-messages` (FR-122, T74, T75)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?loanPublicId=&personPublicId=&purpose=&channel=&status=&from=&to=&page=` | DebtorMessages.Read | `ListDebtorMessagesQuery` | → lista `DebtorMessageDto { publicId, loan?, recipient, channel, purpose, template { code, version }, requestedAt, scheduledFor, status, lastEvent { kind, at, detail }, simulated }` | | K1 |
| `GET /{id}` | DebtorMessages.Read | `GetDebtorMessageQuery` | → `DebtorMessageDto` + cuerpo renderizado, destino copiado al enviar, autorización usada y `events[]` (sólo inserción) | `Lending.DebtorMessage.NotFound` | K1 |
| `POST /` | DebtorMessages.Send | `SendDebtorMessageCommand` | `{ loanPublicId?, recipientPersonPublicId, channel: Email \| Sms \| WhatsApp \| Letter, purpose: Collection \| PriorNotice \| Informative \| DocumentDelivery, templateCode, scheduledFor?, variables? }` → 201 `DebtorMessageDto` (`Queued`, `Deferred` con el nuevo instante, o `Excluded` con motivo) | `Lending.DebtorMessage.OutsideLegalWindow` (envío inmediato, `data: { nextAllowedAt }`), `.ChannelNotAuthorized`, `.RecipientNotObligated` (nunca referencias, `LaCobranzaNuncaContactaReferencias`), `.WeeklyChannelUsed` (`data: { channelUsed, weekStart }`), `Lending.Authorization.WithoutEvidence` | K1 |
| `POST /{id}/cancel` | DebtorMessages.Cancel | `CancelDebtorMessageCommand` | `{ reason }` → 204, sólo antes de `Sent` | `Lending.DebtorMessage.AlreadySent` | K1 |

La tarea `cartera.mensajes` reevalúa la ventana al despachar y `cartera.entregas` consulta el estado al
proveedor (sin webhooks, T75). `CollectionFeeNotice` (aviso de gasto de cobranza) es de K4.

### 14.3 Avisos previos — `/prior-notices` (FR-123, T77)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?loanPublicId=&status=&page=` | PriorNotices.Read | `ListPriorNoticesQuery` | → lista `{ publicId, loan, episodeStart, requiredCommunications, communications: [{ debtorMessage?, manualEvidence?, sentAt }], evidenceDate?, earliestReportDate?, status: Pending \| InProgress \| Valid \| Closed (calculado, `PriorNoticeStatus`), origin: Migration \| Process }` | | K1 |
| `POST /{id}/send` | PriorNotices.Send | `SendPriorNoticeCommand` | `{ channel }` → 201 el `DebtorMessage` `PriorNotice` | `Lending.DebtorMessage.*`, `Lending.PriorNotice.AlreadyEvidenced` | K4 |
| `POST /{id}/evidence` | PriorNotices.RegisterEvidence | `RegisterPriorNoticeEvidenceCommand` | `{ communicationNumber, sentAt, carrier, trackingNumber, attachmentPublicId }` (canal `Letter`, adjunto `LendingPriorNoticeEvidence`) → el aviso | `Lending.PriorNotice.EvidenceDuplicate` | K4 |

Una constancia de un canal `SIMULADO` no vale en producción (T73). Sin aviso válido, el dato negativo no entra al
archivo de centrales (§18).

### 14.4 Reclamos sobre el reporte — `/credit-report-claims` (FR-006, T69)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&personPublicId=&page=` | CreditReportClaims.Read | `ListCreditReportClaimsQuery` | → lista `{ publicId, person, loan?, kind: Claim \| Impersonation, status: InProcess \| Answered \| TacitlyAccepted \| Withdrawn, receivedAt, dueDate, extended }` | | K1 |
| `POST /` | CreditReportClaims.Manage | `RegisterCreditReportClaimCommand` | `{ personPublicId, loanPublicId?, kind, receivedAt, channel, description, attachmentPublicIds[] }` → 201 con `dueDate` en días hábiles nacionales; marca «reclamo en trámite» | | K4 |
| `POST /{id}/extend` · `/{id}/answer` · `/{id}/withdraw` | CreditReportClaims.Manage | `ExtendCreditReportClaimCommand` · `AnswerCreditReportClaimCommand` · `WithdrawCreditReportClaimCommand` | `{ reason }` · `{ answer, attachmentPublicId }` · `{ reason }` → el reclamo | `Lending.CreditReportClaim.NotInProcess`, `.AlreadyExtended` | K4 |

El ciclo diario da por aceptado el reclamo vencido sin respuesta (`TacitlyAccepted`) desde K4.

### 14.5 Etapas, asignación y agenda (K4) — `/collection-stages`, `/collector-assignments`, `/agenda`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /collection-stages?date=` | Collections.Read | `ListCollectionStagesQuery` | → la versión vigente `{ version, validFrom, stages: [{ code, name, daysFrom, daysTo, suggestedActions[], templateCodes[], feeRule? { base, rate, cap, moment } }] }` | | K4 |
| `POST /collection-stages/versions` | CollectionStages.Manage | `AddCollectionStageVersionCommand` | `{ stages[], validFrom, reason }` → 201 | `Lending.CollectionStage.RangesInvalid`, `Lending.Parameter.BelowLegalFloor` | K4 |
| `GET /collector-assignments?collector=&loanPublicId=&page=` | Collections.Read | `ListCollectorAssignmentsQuery` | → lista con historia (sólo inserción) | | K4 |
| `POST /collector-assignments` | Collections.Assign | `AssignCollectorCommand` | `{ loanPublicIds[], collectorUserPublicId, reason }` → 201 | | K4 |
| `POST /collector-assignments/distribute` | Collections.Assign | `DistributeCollectionsCommand` | `{ basis: ByCount \| ByBalance, filters { office?, stage?, line?, payer?, balanceFrom?, balanceTo? }, collectorUserPublicIds[], previewOnly, reason }` → la distribución (`RepartoDeGestores`); con `previewOnly` no escribe | | K4 |
| `GET /agenda?date=&collector=` | Collections.Read | `GetCollectorAgendaQuery` | → `{ date, items: [{ kind: Promise \| FollowUp \| AgreementInstallment \| Stage, loan, debtor, due, route }] }` | | K4 |

### 14.6 Gestiones y promesas — `/collection-actions`, `/promises` (FR-119)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /collection-actions?loanPublicId=&collector=&from=&to=&page=` | Collections.Read | `ListCollectionActionsQuery` | → lista `{ publicId, loan, at, channel, contactedPerson, isDirectContact, result, notes, promise?, voidedBy? }` | | K4 |
| `POST /collection-actions` | Collections.RecordActivity | `RecordCollectionActionCommand` | `{ loanPublicId, at, channel: Phone \| InPerson \| Email \| Sms \| WhatsApp \| Letter \| Visit, contactedPersonPublicId, isDirectContact, resultCode, notes, promise?: { date, amount } }` → 201 | `Lending.CollectionAction.ContactNotObligated` (sólo deudor o codeudor), `Lending.DebtorMessage.OutsideLegalWindow` (contacto fuera de horario) | K4 |
| `POST /collection-actions/{id}/void` | Collections.RecordActivity | `VoidCollectionActionCommand` | `{ reason }` → 204 (la corrección es una gestión nueva que anula la anterior) | | K4 |
| `GET /promises?status=&collector=&page=` | Collections.Read | `ListPaymentPromisesQuery` | → lista `{ publicId, loan, date, amount, status: Pending \| Kept \| Broken (derivado), action }` | | K4 |

### 14.7 Acuerdos de pago — `/payment-agreements` (FR-120)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&loanPublicId=&page=` | Collections.Read | `ListPaymentAgreementsQuery` | → lista `{ publicId, loan, status: Proposed \| Approved \| Rejected \| Cancelled, performance?: Current \| Fulfilled \| Broken (calculado), installments[], compliance }` | | K4 |
| `POST /` | PaymentAgreements.Create | `CreatePaymentAgreementCommand` | `{ loanPublicId, installments: [{ dueDate, amount }], reason }` → 201 en `Proposed`; sólo sobre lo vencido | `Lending.PaymentAgreement.ChangesTerms` (si cambia condiciones, es una novedad, FR-090) | K4 |
| `POST /{id}/approve` · `/{id}/cancel` | PaymentAgreements.Approve · .Create | `ApprovePaymentAgreementCommand` · `CancelPaymentAgreementCommand` | `{ reason? }` → el acuerdo | `Lending.Segregation.Conflict`, `Lending.PaymentAgreement.NotProposed` | K4 |

### 14.8 Cobro jurídico — `/legal-processes` (FR-124)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&lawyer=&page=` | Collections.Read | `ListLegalProcessesQuery` | → lista `{ publicId, loans[], lawyer { personPublicId, fullName }, court, fileNumber, status, startDate, lastAction? }` | | K4 |
| `POST /` | LegalProcesses.Manage | `OpenLegalProcessCommand` | `{ loanPublicIds[], lawyerPersonPublicId, court, fileNumber, startDate }` → 201 | `Lending.LegalProcess.LoanNotInLegalStage` | K4 |
| `POST /{id}/actions` | LegalProcesses.Manage | `RecordLegalProcessActionCommand` | `{ date, kind, description, fees?, costs?, attachmentPublicIds[] }` → 201 (adjuntos `LendingLegalProcessDocument`) | | K4 |
| `POST /{id}/close` | LegalProcesses.Manage | `CloseLegalProcessCommand` | `{ outcome, reason }` → 204 | | K4 |

Una insolvencia abierta suspende el cobro jurídico (FR-098).

### 14.9 Campañas — `/campaigns` (K4)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=` | Campaigns.Manage | `ListMessageCampaignsQuery` | → arreglo `{ publicId, name, templateCode, channel, purpose, scheduledFor, status: Draft \| Scheduled \| Running \| Completed \| Cancelled, counts { queued, deferred, excluded, sent, failed } }` | | K4 |
| `POST /` | Campaigns.Manage | `CreateMessageCampaignCommand` | `{ name, templateCode, channel, purpose, filters { stage?, office?, line?, minDaysPastDue? }, scheduledFor }` → 201 en `Draft` | | K4 |
| `POST /{id}/preview` | Campaigns.Manage | `PreviewMessageCampaignQuery` | → `{ recipients, excluded: [{ loan, reason: ChannelNotAuthorized \| Deceased \| Insolvency \| CollectionSuspended \| WeeklyChannelUsed }] }` (sin clave) | | K4 |
| `POST /{id}/schedule` · `/{id}/cancel` | Campaigns.Manage | `ScheduleMessageCampaignCommand` · `CancelMessageCampaignCommand` | `{ reason? }` → la campaña; cada mensaje nace en `LND_DebtorMessages` con su constancia de exclusión | | K4 |

---

## 15. Documentos al deudor y verificación — `/api/lending/documents` (FR-131, FR-132, T80)

`LND_IssuedDocuments` con número por clase (`NumeradorDeCartera`), copia de la identificación del titular (I-1),
corte o período, `InformationCutoff` (R), código de verificación de 16 caracteres Crockford base32
(`XXXX-XXXX-XXXX-XXXX`) con QR a `/cartera/verificar?codigo=`, SHA-256 y PDF guardado (adjunto
`LendingIssuedDocument`, no borrable). El estado se **calcula**: `Voided` si hay anulación; `SubjectToFinality`
mientras hoy < `FinalityUntil`; `Valid` en otro caso. Clases (`IssuedDocumentKind`): `PaymentPlan`,
`LoanClearance`, `GeneralClearance` y `BalanceCertificate` en K1 (propuesta I5); `Statement` y `TaxCertificate`
en K5 (fase 19: la ruta de emisión no cambia; `period` es obligatorio en el extracto y `year` opcional en el
certificado tributario —sin él, el año anterior—).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?kind=&personPublicId=&loanPublicId=&status=&from=&to=&page=` | Documents.Read | `ListIssuedLendingDocumentsQuery` | → lista `IssuedDocumentDto { publicId, kind, number, holder { personPublicId, fullName, documentNumber }, loans[], cutoffDate?, period?, year?, issuedAt, issuedBy, status, finalityUntil?, coversCoDebts?, verificationCode }` | | K1 |
| `GET /{id}` | Documents.Read | `GetIssuedLendingDocumentQuery` | → `IssuedDocumentDto` + `essentialData` + `events: [{ kind: Issued \| Delivered \| Voided, at, by, reason?, causeOperation? }]` | `Lending.Document.NotFound` | K1 |
| `POST /` | Documents.Issue | `IssueLendingDocumentCommand` | `{ kind, personPublicId, loanPublicId?, cutoffDate?, period?: { from, to }, year?, delivery: Download \| Email }` → 201 `{ documentPublicId, number, verificationCode, status, download?: { url, expiresAt }, debtorMessagePublicId? }` en **un paso** (FR-013) | `Lending.Document.ClearanceDenied` (`data: { obligations?: [{ loanPublicId, number, balance }], pendingSales?: [{ invoice, messagePublicId }] }`), `.KindNotAvailable` (`data: { availableFrom: "K5" }`, antes de K5), `.SignerMissing` (sin `Documentos.Firmante.*`), `.NothingToCertify`, **K5**: `.PeriodInvalid` (`data: { periodFrom, periodTo, maxYears }`: extracto sin período, al revés, que termina después de hoy o de cinco años o más), `.TaxYearNotClosed` (`data: { year, availableFrom }`); `Lending.DebtorMessage.ChannelNotAuthorized` (correo), `NationalValues.Missing` | K1 · K5 |
| `POST /{id}/download-link` | Documents.Reprint | `IssueLendingDocumentDownloadLinkCommand` | → `{ url, expiresAt }` del PDF guardado (idéntico); auditado `Lending.Document.Reprinted` | | K1 |
| `POST /{id}/send` | Documents.Issue | `SendLendingDocumentCommand` | `{ recipientPersonPublicId? }` → 201 `{ debtorMessagePublicId }` (propósito `DocumentDelivery`, exento de ventanas si lo pidió el titular, I3) | `Lending.Document.VoidedCannotReissue`, `Lending.DebtorMessage.ChannelNotAuthorized` | K1 |
| `POST /{id}/void` | Documents.Void | `VoidLendingDocumentCommand` | `{ reason }` → 204 | `Lending.Document.AlreadyVoided` | K1 |
| `GET /verify/{code}?sha256=` | Documents.Verify | `VerifyLendingDocumentQuery` | → `{ authentic, status, kind, number, issuedAt, holder { fullName, documentNumberMasked }, essentialData, voided?: { at, reason }, pdfMatches? }`; auditado `Lending.Document.Verified` (con `pdfMatches` si se comparó). **K5 (T0972, T0974)**: `sha256` opcional, la huella SHA-256 en hexadecimal de un PDF calculada en el navegador (el archivo nunca sube); `pdfMatches = true` sólo si es la del PDF guardado | `Lending.Document.NotFound` (código inexistente, con el mensaje «el código no corresponde a un documento de esta cooperativa»); `Validation.Invalid` (huella que no son 64 caracteres hexadecimales) | K1 · K5 |
| `GET /images` | Parameters.View | `GetLendingDocumentImagesQuery` | → `{ setupPublicId, logoOwnerType, signatureOwnerType, logo?, signature? }` (`{ attachmentPublicId, fileName, contentType, uploadedAt }`): el dueño de los adjuntos `LendingLogo` y `LendingSignatureImage` (la fila única de `LND_Setup`) y la imagen vigente de cada uno, para su carga en `/cartera/parametros` (I6, propuesta por defecto; bloque 14B). La subida va por `POST /api/attachments/uploads` con `Lending.Parameters.Manage` | | K1 |

- **Paz y salvo** (FR-131): sólo con saldo cero y sujeto a firmeza (D23); el general certifica sólo las
  obligaciones de Cartera, se niega si hay un mensaje de venta en reproceso (y dice la factura), menciona las
  ventas aceptadas fuera de Cartera y aplica `Documentos.PazYSalvoIncluyeCodeudas`.
- **Certificado de saldo** (FR-068): es la liquidación a una fecha impresa; la liquidación en pantalla (§8.1)
  no emite documento.
- **Extracto** (K5): saldo inicial a la víspera, operaciones del período por fecha valor (causación agregada por mes y
  concepto; los reversos conocidos, informativos) y saldo final, todo con R = la emisión; saldo inicial + movimientos =
  saldo final al centavo.
- **Certificado tributario** (K5): por crédito de la persona como deudora, los intereses pagados con fecha valor en el
  año (corriente y mora con sus suspensos, netos de los reversos conocidos a la emisión) y el saldo al 31 de diciembre;
  los de vivienda en su propia sección (vacía hasta K6b).
- **Anulación automática** (T80): el comando que reversa un pago anula, en la misma transacción, todo documento
  cuyo `InformationCutoff` sea igual o posterior al registro de lo reversado (`VigenciaDeDocumentos`).
- **Verificación sólo interna** (D21): no hay ruta anónima; fuera de producción el PDF lleva «AMBIENTE DE
  PRUEBAS – SIN VALIDEZ».

---

## 16. Integración: bandeja, hechos, conciliación, procesos e integridad — `/api/lending`

### 16.1 Bandeja de mensajes de Cartera — `/messages` (T19, CH32)

La misma bandeja de la plataforma (`ListIntegrationMessagesQuery`, `ReprocessMessagesCommand`,
`SendNotApplicableMessagesCommand`) con la estrategia `IVistaDeBandejaDelModulo` de Cartera: origen `CAR`,
alcance por oficina del crédito, modo vigente por tipo de hecho y nombre del documento de Cartera.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /messages?status=&destination=Accounting\|Inventory\|Treasury\|Receipts&type=&loanPublicId=&from=&to=&batch=&prevalidationOutcome=&page=` | Messages.Read | `ListIntegrationMessagesQuery` (vista `Lending`) | → lista `IntegrationMessageDto` (la forma de la 012, con `origin.kind = Operation \| FactPart \| Close \| DisbursementOrder \| Activation` y `originActor: { kind: Person \| Process, name }`) + `countsByStatus` | | K1 |
| `GET /messages/{id}` | Messages.Read | `GetIntegrationMessageQuery` | → el mensaje + `payload` (contrato `…V1`, con su SHA-256) + intentos + `dependsOn[]`/`dependents[]` | `Integration.Message.NotFound` | K1 |
| `POST /messages/reprocess` | Messages.Reprocess | `ReprocessMessagesCommand` | `{ messagePublicIds[], reason }` → **202** `{ batchPublicId, number, messages, dragged }` | `Integration.Message.NotRejected` | K1 |
| `POST /messages/send-not-applicable` | Messages.SendNotApplicable | `SendNotApplicableMessagesCommand` | `{ from, to, types?[], cutoffMessagePublicId, reason }` → **202** | `Integration.SendNotApplicable.ModeStillNotPosted` (`data: { types[] }`) | K1 |

`IntegrationMessageDto.result.route` lleva al comprobante (`/contabilidad/comprobantes/{id}`) o al lote resumido
(`/contabilidad/cartera/lotes/{id}`). Cartera no consulta tablas `ACC_`: el resultado sale de
`COR_IntegrationMessageDeliveries`.

### 16.2 Recibos de mensajes que llegan a Cartera — `/inbound-messages` (T17)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /inbound-messages?status=&type=&from=&to=&page=` | Messages.Read | `ListLendingInboundMessagesQuery` | → lista `{ messagePublicId, type, version, originModule: INV \| TES \| REC, receivedAt, status: Processed \| ValidationFailed \| Rejected \| NotIncorporated \| ConfirmedByException, loan?, evaluations: [{ code, reason, at }], response?: { type: ObligacionCreada \| ObligacionRechazada, messagePublicId } }` | | K1 |
| `GET /inbound-messages/{messagePublicId}` | Messages.Read | `GetLendingInboundMessageQuery` | → el recibo con el contenido y cada evaluación | `Lending.InboundMessage.NotFound` | K1 |
| `POST /inbound-messages/{messagePublicId}/mark-not-retryable` **(fijado aquí, C95)** | Messages.Reprocess | `MarkLendingInboundMessageNotRetryableCommand` | `{ reason }` → `{ status: Rejected, notRetryableAt, response: { type: ObligacionRechazada, messagePublicId } }` | `Lending.InboundMessage.NotFound`, `Lending.InboundMessage.NotMarkable` | K1 |

Reprocesar un mensaje entrante rechazado es de la bandeja de **Comercial** (`/api/inventory/messages/reprocess`,
el destino `Lending`); aquí se lee el recibo y, si una venta no convertible ya no se va a reprocesar, se marca no
reprocesable: sólo entonces sale `ObligacionRechazada` `NoConvertible` (`retryable = false`) y Comercial deja de verla
`Pending` (C95). Marcar un recibo que no está `Rejected` o que ya está marcado responde
`Lending.InboundMessage.NotMarkable`. Confirmar por excepción una validación fallida es §8.2. Un
recibo `NotIncorporated` (venta aceptada fuera de Cartera o fechada hasta el corte y llegada después de activar,
C63) no se reprocesa: su respuesta es `ObligacionRechazada` con `Lending.Sale.NotIncorporated`.

### 16.3 Hechos contables y conciliación — `/accounting/*` (DC-16, T58)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /accounting/facts?type=&status=&from=&to=&branch=&loanPublicId=&page=` | Messages.Read | `ListLendingAccountingFactsQuery` | → lista de los hechos `CAR → Accounting` con `{ messagePublicId, type, accountingDate, branch, mode, status, totals, part?: { partPublicId, tranche, loans }, voucher? }` | | K1 |
| `GET /accounting/fact-parts/{id}?page=` | Messages.Read | `GetAccountingFactPartQuery` | → la parte (`LND_AccountingFactParts`) con totales por combinación de dimensiones y la lista por crédito (número, tercero, valores por concepto) | `Lending.AccountingFactPart.NotFound` | K1 |
| `GET /accounting/reconciliation?date=&branch=` | Reconciliation.View | `GetLendingAccountingReconciliationQuery` | → `ReconciliationDto` (abajo) | `Lending.Accounting.NotStarted` (contabilidad no iniciada) | K1 |

`ReconciliationDto` (por **fecha contable**; Cartera no conoce cuentas, compara por conjuntos):

```
{ date, sets: [{ family, combinations: [{ modality, subsegment, category, concept }],
                 accounts: [{ code, name }], lendingBalance, accountingBalance, difference,
                 explainedBy: { pending, inBatch, rejected, notPosted }, unexplained }],
  bridges: [{ collectorClass, balance }], unexplainedTotal }
```

Un descuadre sin explicar alerta `Cartera.DescuadreContable` al contador y a cartera (FR-130).

### 16.4 Corridas — `/process-runs` (T5, T8)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /process-runs?kind=&status=&from=&to=&page=` | Processes.View | `ListLendingProcessRunsQuery` | → lista `{ publicId, kind: DailyCycle \| MonthlyClose \| CloseReopen \| NationalValueCorrection, date?, period?, status: Requested \| Running \| Completed \| CompletedWithErrors \| Failed, progress { done, total }, requestedBy { kind: Person \| Process, name }, startedAt?, finishedAt?, errors }` | | K1 |
| `GET /process-runs/{id}?page=` | Processes.View | `GetLendingProcessRunQuery` | → la corrida + `errors: [{ loanPublicId, loanNumber, step, code, message, attempts, lastAt }]` (sin datos del deudor) | `Lending.ProcessRun.NotFound` | K1 |
| `POST /process-runs` | Processes.Run | `RequestLendingProcessRunCommand` | `{ kind: DailyCycle, date? }` → **202** `{ processRunPublicId, status: Requested }`; sólo encola y corre con la persona como actor | `Lending.Process.AlreadyRunning`, `Lending.ProcessRun.AlreadyCompleted`, `Lending.ProcessRun.FutureDate` **(US4 bloque 8C)**, `Lending.ProcessRun.KindNotOrderable` (el cierre va por §12.3; `NationalValueCorrection` no se ordena) | K1 |
| `POST /process-runs/{id}/retry` | Processes.Run | `RetryLendingProcessRunCommand` | `{ reason }` → **202**; retoma desde la marca de agua de cada crédito | `Lending.ProcessRun.NotRetryable` (`data: { status }`) | K1 |

### 16.5 Integridad del libro — `/integrity/verify` (T33)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /integrity/verify` | Integrity.Verify | `VerifyLendingLedgerIntegrityQuery` | `{ loanPublicIds?[], sampleSize?, upTo? }` → `{ checked, upTo, mismatches: [{ loanPublicId, number, date, concept, ledger, reconstruction }], durationMs }`; una diferencia levanta alerta (sin clave; no corrige nada) | | K1 |

---

## 17. Informes y tablero — `/api/reports/lending` y `/api/lending/dashboard` (FR-133–FR-138, T99)

`LendingReportsEndpoints` se reescribe como el de Inventario: **una ruta por vista**, cada una con
`.RequirePermission("Lending.Reports.View")` y `.RequirePermissionWhenExporting("Lending.Reports.Export")`,
publicada con `MapVistaDeCartera` y su metadato `VistaDeInformeDeCartera`.

- **Formatos**: `?format=json|xlsx|pdf|docx` (`TablaExportable` por `EntregaDeInformes`), con encabezado de
  empresa, NIT, filtros, usuario y fecha. Columnas ocultas con `_` (`_credito`, `_persona`, `_operacion`,
  `_documento`, `_mensaje`) que la pantalla usa para profundizar.
- **Datos del deudor (PD)**: las vistas marcadas exigen además `Lending.Loans.Read` para consultarlas y
  `Lending.Reports.ExportPersonalData` para exportarlas; sin ellos, el mismo 404 y sin auditar (C84).
- **Alcance**: toda vista filtra por `IAlcanceDeCartera` (`LasConsultasDeCarteraRespetanElAlcance`).
- **Auditoría**: cada exportación a archivo emite `Lending.Report.Exported` por `LendingAuditEmitter` con la
  cooperativa y las filas.
- **Filtros comunes** (`FiltrosDeInformeDeCartera`): `from`, `to`, `asOf` (fecha de corte), `branch`, `line`,
  `modality`, `subsegment`, `advisor`, `payer`, `collector`, `stage`, `format`. Rango de hasta 5 años: 422
  `Lending.Report.RangeInvalid` / `.RangeTooLong`; un filtro propio inválido, `Lending.Report.FilterInvalid`
  con los admitidos.
- **Registro**: `GET /api/reports/lending` (`Reports.View`) → `[{ key, name, description, fileName, filters[],
  ownFilters?[], personalData, availableFrom, requiredPermission?, personalDataExceptWhen? { filter, values[] } }]`, sin las vistas que exigen un permiso
  que quien pregunta no tiene. Pantalla `/cartera/informes?vista=`. K5 (T0982): una vista con `personalDataExceptWhen` (la
  concentración por `by=line|sector|payer`) se ofrece a todos y su ruta pide `requiredPermission` y, al exportar, `ExportPersonalData` sólo
  cuando el filtro trae datos del deudor.

| Vista | E | Filtros propios | PD | Columnas (· ocultas) |
|---|---|---|---|---|
| `aging` | K1 | `asOf`, `ranges?` (por defecto `Informes.RangosDeEdad`) | sí | Crédito, Deudor, Línea, Modalidad, Oficina, Saldo de capital, Días de mora, Rango, Calificación · `_credito`, `_persona` |
| `placement` | K1 | `from`, `to`, `groupBy=line\|office\|advisor\|month` | no (agregado) | Dimensión, Créditos, Monto desembolsado, Tasa promedio, Plazo promedio |
| `receipts` | K1 | `from`, `to`, `groupBy=line\|collector\|modality\|meansClass` | no (agregado) | Dimensión, Pagos, Capital, Intereses, Otros, Total, Reversos |
| `transactions` | K1 | `from`, `to`, `loan?`, `kind?`, `dateKind=value\|accounting\|registered` | sí | Fecha valor, Fecha contable, Registro, Crédito, Tipo, Concepto, Cuota, Valor, Operación, Mensaje · `_credito`, `_operacion`, `_mensaje` |
| `usury` | K1 | `asOf`, `points?` (por defecto `Informes.PuntosAlertaUsura`) | sí | Sección 1: créditos topados (Crédito, Deudor, Tasa pactada, Usura, Días topados). Sección 2: líneas a menos de N puntos (Línea, Modalidad, Tasa, Usura, Margen) · `_credito` |
| `debtor-messages` | K1 | `from`, `to`, `purpose?`, `channel?`, `status?` | sí | Fecha, Crédito, Destinatario, Canal, Propósito, Plantilla, Estado, Motivo de exclusión o diferimiento · `_credito` |
| `migration-reconciliation` | K1 | `load` (obligatorio) | sí | Sección 1: cuadre por concepto, modalidad, sub-segmento, categoría y conjunto de cuentas. Sección 2: ventas (Factura, Cliente, Clasificación, Saldo, Aceptación, Punto de acta) · `_persona` |
| `payroll-agreements` | K2 | `asOf` | no | Pagaduría, Créditos, Exigible del período, Descontado pendiente de giro, Días de mora de la pagaduría, Umbral superado |
| `payroll-deduction-reconciliation` | K2 | `agreement`, `year`, `month` | sí | Empleado, Crédito, Enviado, Descontado, Pendiente de giro, Aplicado, No aplicado, Motivo · `_credito` |
| `restructured` | K3 | `asOf`, `mechanism?` | sí | Crédito, Deudor, Mecanismo, Conteo, Fecha, Calificación al reestructurar, Cuotas consecutivas pagadas, Calificación actual · `_credito` |
| `charge-offs` | K3 | `from`, `to` | sí | Crédito, Deudor, Fecha de castigo, Punto de acta, Saldo castigado, Recuperado, Gestiones, Último contacto · `_credito` |
| `collection-stages` | K4 | `asOf` | sí | Etapa, Crédito, Deudor, Días, Saldo, Gestor, Última gestión · `_credito` |
| `promises` | K4 | `status?` | sí | Crédito, Deudor, Fecha, Valor, Estado, Gestor · `_credito` |
| `payment-agreements` | K4 | `status?` | sí | Crédito, Deudor, Cuotas, Cumplidas, Vencidas, Estado · `_credito` |
| `prior-notices` | K4 | `status?` | sí | Crédito, Deudor, Episodio, Comunicaciones, Constancia, Fecha más temprana de reporte · `_credito` |
| `quality` | K5 | `asOf`, `groupBy=total\|office\|line\|modality` (por defecto `total`) | no | Dimensión, Cartera bruta, Vencida, Vencida/bruta, En riesgo (B–E), Riesgo/bruta, Deterioro, Cobertura (del último cierre vigente al corte; vencida con `Informes.DiasCarteraVencida`) |
| `vintages` | K5 | `from`, `to` (meses de desembolso; sin `from`, los doce hasta `to`) | no | Mes de desembolso, Monto, Mes de maduración, Mes del cierre, Mora 30, Mora 60, Mora 90 (%) |
| `concentration` | K5 | `asOf`, `top?` (1–500, 20), `warnAt?` (% del límite, 80), `by=debtor\|group\|line\|sector\|payer` | sí con `by=debtor\|group` (`personalDataExceptWhen`) | Dimensión, Saldo, Participación, % del límite individual, Señal · `_persona` |
| `issued-documents` | K5 | `from`, `to`, `kind?`, `status?` | sí | Fecha, Clase, Número, Titular, Estado, Código, Emitido por · `_documento` |
| `bureau-exclusions` | K5 | `bureau`, `year`, `month` | sí | Crédito, Deudor, Motivo (sin autorización, sin aviso previo, reclamo en trámite), Gestión sugerida · `_credito` |

**Tablero** — `GET /api/lending/dashboard?branch=&line=&asOf=` · `Dashboard.View` · `GetLendingDashboardQuery` → `LendingDashboardDto
{ asOf, scope { branches[], lines[] }, tiles: [{ key, label, value, previousValue?, unit: Money \| Count \| Percent \| Days, severity?,
link: { page, query } }] }`, sobre las posiciones diarias dentro del alcance (FR-135). Fichas: saldo, colocación del mes, calidad, deterioro,
recaudo frente al esperado, cosechas, cumplimiento de promesas, mensajes pendientes y rechazados, alertas pendientes. **E: K5.**
Implementado en la fase 20 (T0983, T0984; `DashboardEndpoints`): las claves son `saldo`, `colocacion-del-mes`, `calidad`, `deterioro`,
`recaudo-vs-esperado`, `cosechas`, `cumplimiento-de-promesas`, `mensajes-pendientes-y-rechazados`, `alertas-pendientes`; cada ficha suma
`note?` (cómo se lee o por qué no tiene dato) y su `link` a `/cartera/informes?vista=…`, `/cartera/bandeja-de-mensajes` o `/cartera/alertas`;
`scope.branches[]`/`lines[]` son `{ publicId, code, name }`. Una oficina o línea fuera del alcance da cifras en cero. Definiciones en
`docs/manual/cartera-informes-y-tablero.md` §4.

---

## 18. Regulatorios — `/api/lending/regulatory` (K5; FR-136, FR-137, T79)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /layouts` | RegulatoryFiles.Read | `ListRegulatoryLayoutsQuery` | → arreglo `{ code, kind: Sicses \| CreditBureau, bureauCode?, validFrom, validTo?, source, fields, unverifiedFields }` de `CatalogoDeLayoutsDeCartera` | | K5 |
| `POST /sicses/{year}/{month}/validate` | RegulatoryReports.Generate | `ValidateSicsesFileQuery` | → `{ layout, issues: [{ severity: Blocking \| Warning, code, field, loan?, message }], warnings: [LayoutSinCotejar?, ModeloObligatorioNoDisponible?] }` (no guarda; sin clave) | `Lending.MonthlyClose.NotFound` (sin cierre del mes) | K5 |
| `POST /sicses/{year}/{month}/generate` | RegulatoryReports.Generate | `GenerateSicsesFileCommand` | `{ acknowledgeWarnings }` → 201 `RegulatoryFileDto` en `Generated` (la versión anterior queda `Superseded`) | `Lending.Regulatory.BlockingIssues` (`data: { regulatoryFilePublicId, version, issues[] }`: queda una generación `Validated` **sin archivo** con lo encontrado), aviso `Lending.Regulatory.LayoutSinCotejar` (exige reconocerlo) | K5 |
| `POST /bureaus/{bureauCode}/{year}/{month}/validate` | CreditBureauFiles.Generate | `ValidateCreditBureauFileQuery` | → ídem, más `excluded: [{ loan, reason: NoAuthorization \| NoPriorNotice \| ClaimInProcess }]` | `Lending.Regulatory.BureauUnknown` | K5 |
| `POST /bureaus/{bureauCode}/{year}/{month}/generate` | CreditBureauFiles.Generate | `GenerateCreditBureauFileCommand` | `{ acknowledgeWarnings }` → 201 `RegulatoryFileDto`; sólo quienes autorizaron el reporte y, si el dato es negativo, con aviso previo con constancia del episodio | ídem | K5 |
| `GET /files?kind=&bureau=&year=&month=&status=&page=` | RegulatoryFiles.Read | `ListRegulatoryFilesQuery` | → lista `RegulatoryFileDto { publicId, kind, bureauCode?, year, month, version, status: Validated \| Generated \| Superseded \| Uploaded, layoutCode, warnings[], counts, sha256?, generatedBy, generatedAt, uploaded?: { filingNumber, at, by } }` | | K5 |
| `GET /files/{id}` | RegulatoryFiles.Read | `GetRegulatoryFileQuery` | → el archivo con `issues[]` y `excluded[]` | `Lending.RegulatoryFile.NotFound` | K5 |
| `POST /files/{id}/download-link` | RegulatoryFiles.Read | `IssueRegulatoryFileDownloadLinkCommand` | → `{ url, expiresAt }` (adjunto `LendingRegulatoryFile`); auditado | `Lending.RegulatoryFile.NoFile` (generación `Validated`) | K5 |
| `POST /files/{id}/mark-uploaded` | RegulatoryFiles.MarkUploaded | `MarkRegulatoryFileUploadedCommand` | `{ filingNumber, uploadedAt, receiptAttachmentPublicId? }` → `RegulatoryFileDto` en `Uploaded`; el período ya no se regenera | `Lending.RegulatoryFile.NotGenerated` | K5 |

La marca «modelo obligatorio no disponible» (D28) va como advertencia en la generación y nunca dentro del
archivo. El cargue en el portal lo hace una persona.

---

## 19. Migración y activación — `/api/lending` (K1; FR-139–FR-143, T83, T84)

### 19.1 Cargas — `/migration/loads`

Una carga (`LND_MigrationLoads`) tiene corte y partes; estados `Draft`, `Applied`, `Replaced`, `Activated`.
**Aplicar sólo guarda la carga** (FR-142): los créditos nacen al activar.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /migration/loads` | Migration.Load | `ListMigrationLoadsQuery` | → arreglo `{ publicId, cutoffDate, status, parts: [{ part, rows, sha256?, importedAt? }], appliedAt?, appliedBy?, differences { pending, accepted } }` | | K1 |
| `GET /migration/loads/{id}` | Migration.Load | `GetMigrationLoadQuery` | → la carga con el resumen por parte y lo registrado a mano que prevalece (FR-142) | `Lending.MigrationLoad.NotFound` | K1 |
| `POST /migration/loads` | Migration.Load | `CreateMigrationLoadCommand` | `{ cutoffDate, notes? }` → 201 en `Draft` | `Lending.Activation.AlreadyActivated` (Cartera ya activada) | K1 |
| `GET /migration/loads/{id}/parts/{part}/template.xlsx` | Migration.Load | `GetLendingTemplateQuery` | → la plantilla `lending.migration.{part}` (una hoja; admite .csv al importar) | `Lending.Template.NotFound` | K1 |
| `POST /migration/loads/{id}/parts/{part}/import?mode=` | Migration.Load | `ImportMigrationPartCommand` | multipart → `ImportResultDto`; una parte admite **varios archivos** (cada uno, una fila de `LND_MigrationLoadParts`, C64); `?replace=true` reemplaza la parte entera en el borrador, sin él el archivo se suma | `Import.Invalid`, `Lending.Migration.LoadNotDraft` | K1 |
| `POST /migration/loads/{id}/apply` | Migration.Load | `ApplyMigrationLoadCommand` | `{ reason }` → `{ status: Applied, balanceCheck, salesReconciliation, replacedLoadPublicId? }`; valida entre partes (el plan suma el capital, referencias cruzadas, historial de los segmentos cubiertos), todo o nada | `Lending.Migration.CrossPartErrors` (`data: { errors: [{ part, row, column, message }] }`), `Lending.Migration.PartMissing` (`data: { parts[] }`), `Lending.Migration.ArrearsHistoryRequired` (cooperativa obligada), `Lending.Schedule.PrincipalMismatch`, `Lending.Migration.LoadNotDraft` | K1 |

`{part}` es una de las partes de §2.3 de las transversales en kebab: `loans`, `installments`, `balances`,
`risk-data`, `restructurings`, `collaterals`, `co-debtors`, `credit-limits`, `blocks`, `authorizations`,
`prior-notices`, `bureau-reports`, `claims`, `related-parties`, `charged-off`, `sales`, `arrears-history`. Una
carga aplicada nueva deja la anterior `Replaced` (ensayo). Ninguna plantilla crea personas (I11). Lo que fija C64:
el castigo migrado trae su soporte histórico **como dato** (`soporteDelCastigo`, sólo en la preparación; T64 rige lo
que se decide en el ERP); las reclamaciones al seguro y las insolvencias viajan en la parte `blocks` (motivos
`Deceased` e `Insolvency`) y nacen al activar; los reportes vigentes en centrales pasan a
`LND_PriorNotices.MigratedReportedNegativeSince`. Columnas en `contracts/plantillas.md` §5.

### 19.2 Cuadre, conciliación de ventas y diferencias

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /migration/loads/{id}/balance-check` | Reconciliation.View | `GetMigrationBalanceCheckQuery` | → cuadre FR-141 a la fecha de corte por concepto, modalidad, sub-segmento, categoría y conjunto de cuentas (por `IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync`), con diferencias y su aceptación; vivienda fuera hasta K6b | `Lending.Accounting.NotStarted` | K1 |
| `GET /migration/loads/{id}/reconciliation?classification=&page=` | Migration.Load | `GetMigrationSalesReconciliationQuery` | → las ventas acumuladas hasta el corte, listadas **sin consumirlas** (`IMensajesEntrantes.PendientesAsync`), con su clasificación: `Migrated`, `CancelledBeforeCutoff`, `NotMigratedWithBalance`, `OutsideLendingD29` (`SaleReconciliationOutcome`) | | K1 |
| `POST /migration/loads/{id}/reconciliation/marks` | Migration.Load | `MarkMigrationSaleCommand` | `{ messagePublicId, classification: CancelledBeforeCutoff \| OutsideLendingD29, reason }` → la venta marcada (la segunda es la de servicios a asociados confirmados antes de `CreditWithoutLending`, T83) | `Lending.MigrationLoad.SaleNotPending` | K1 |
| `POST /migration/loads/{id}/differences/accept` | Migration.AcceptDifferences | `AcceptMigrationDifferencesCommand` | `{ differencePublicIds[], sessionItemPublicId, reason }` → las diferencias aceptadas (D17), venta por venta en las de ventas | `Lending.Session.ItemMismatch`, `Lending.Segregation.Conflict` (`CargarMigracion × AceptarDiferencias`) | K1 |

### 19.3 Activación — `/activation` (FR-142, T84)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /activation` | Activation.Execute | `GetLendingActivationStatusQuery` | → `ActivationReadinessDto` (abajo): estado, carga vigente, fecha que correspondería, plazo y lista de requisitos con su estado | | K1 |
| `GET /activation/provisional-collections/template.xlsx` | Activation.Execute | `GetLendingTemplateQuery` | → plantilla `lending.provisional-collections` (factura, valor, fecha valor, referencia contable) | | K1 |
| `POST /activation/provisional-collections/review` | Activation.Execute | `ReviewProvisionalCollectionsCommand` (escribe el adjunto: lleva clave) | multipart con el archivo → `{ stagedAttachmentPublicId, sha256, rows, errors[] }`: revisión previa contra las ventas de la carga, que deja el archivo como adjunto; **no guarda filas** | `Import.Invalid`, `Lending.ProvisionalCollection.ExceedsSale` | K1 |
| `POST /activation` | Activation.Execute | `ActivateLendingCommand` | `{ migrationLoadPublicId, activationDate, provisionalCollectionsAttachmentPublicId?, sessionItemPublicId, acknowledgeWarnings, reason }` → 201 `{ activationPublicId, activationDate, loans, openingTransactions, baseClosePublicId, creditLimits, revalidatedLimits, nonDeductionsMarked, provisionalCollectionsStored, messages: [CarteraActivada, …] }`. `sessionItemPublicId` es **obligatorio** (acta de activación, `LND_Activations.SessionItemId` no nulo, C61) | `Lending.Activation.RequirementsMissing` (`data: { missing: [{ code, detail, whoFixes }] }`), `.CutoffInvalid` (`data: { expected }`: día siguiente al corte), `.DeadlinePassed` (plazo vencido o mes contable cerrado: pida una carga con corte nuevo), `.DifferencesNotAccepted` (sin aceptar o cambiaron desde que se aceptaron: vuelva a aceptar), `.AlreadyActivated`, `Lending.Session.ItemMismatch`, `Import.Invalid` y `Lending.ProvisionalCollection.ExceedsSale` (cobros provisionales) | K1 |

`ActivationReadinessDto.requirements[].code`: `LoadApplied`, `DifferencesAccepted`, `SalesReconciled`,
`DateInTerm`, `AccountingMonthOpen` (sólo falta sin contabilidad iniciada: el mes de C+1 cerrado queda `NotApplicable` con el aviso `AccountingMonthClosed`, respuesta del dueño del 2026-10-14), `ReceiptsCollectorRegistered`, `TreasuryDestinationRegistered`
(producción, sin dobles), `ExpectedLossAvailable` (cooperativa obligada), `ExpectedLossParameters` y
`ArrearsHistoryComplete` (donde K6a esté desplegada), `Annex1Current`, `UsuryForActiveLines`,
`LendingRolesCreated`, `OfficesLinkedToAccountingBranches`, `PayrollBridgeRuleResolved`. Avisos que no
bloquean: `CommercialParametersToAdjust` (fecha de habilitación, política sin respuesta, quién registra la
cuenta por cobrar), `PayrollConceptAccounts` (la pantalla pregunta a Nómina por
`GET /api/payroll/concept-definitions/DESC_CARTERA/accounts`), `UsersWithoutScope` y `AccountingMonthClosed` (el mes de C+1
cerrado o sin crear en Contabilidad: la activación procede y lo que emite queda en el primer mes abierto; 2026-10-14) y,
desde K3 (bloque D, decisión del dueño del 2026-10-15), lo de la norma y la política que sigue vacío y pendiente de validar:
`WriteOffReportTermPending` (plazo del reporte de castigos, `writeOffReport` del tipo de entidad), `PortfolioEvaluationPeriodicityPending`
(`portfolioEvaluation`) y `WriteOffMinimumArrearsPending` (`Castigo.DiasMinimosDeMora`). La
respuesta de `POST /activation` lleva los mismos avisos en `warnings`.

**Cómo corre la activación** (C60): `ActivateLendingCommand` abre una transacción explícita y guarda **por tandas
de 1.000 créditos** con `ChangeTracker.Clear()` entre tandas; el destino `Lending` del registro de participantes se
habilita en la misma transacción. Si algo falla, no queda nada. Se mide con 50.000 créditos bajo `RUN_PERF_TESTS`.

**Cobros provisionales** (C62, FR-142): se revisan antes (`…/review`, que deja el adjunto) y la activación vuelve a
validarlos y **los guarda** en `LND_ProvisionalCollections` (H, `[EscrituraUnica]`), todo o nada; **no los aplica**,
porque la obligación de su venta todavía no existe. Los aplica el consumidor de Comercial al crear esa obligación,
con clave idempotente.

**Ventas fechadas hasta el corte que llegan después de activar** (C63, FR-143; pregunta I16): no entran a Cartera.
El consumidor deja el recibo `NotIncorporated` y responde `ObligacionRechazada` **no reprocesable** con
`Lending.Sale.NotIncorporated`, y alerta `Cartera.VentaNoIncorporada` (C99). «Anterior a la activación» se lee «anterior o igual al corte».

### 19.4 Roles sugeridos — `/activation/suggested-roles` (T61)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /activation/suggested-roles` | Security.Roles.Create | `CreateSuggestedLendingRolesCommand` | `{ templateKeys?[] }` (por defecto las trece) → `{ created: [{ templateKey, rolePublicId, code }], alreadyExisting[], omitted[] }`; idempotente | | K1 |

---

## 20. Dobles de prueba — `/api/lending/test-doubles` (sólo fuera de producción; FR-089, T20)

Las rutas se publican sólo si `Lending:TestDoubles:Enabled = true` y `!IHostEnvironment.IsProduction()`; en
producción no existen (404) y la API no arranca si la configuración los pide. En cada cooperativa se habilitan con
una fila `Class = Test` del registro (§21.1). Todo lo que emiten lleva a la persona que prueba como usuario de
origen.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /collector/obligations?personPublicId=&includeCoDebtor=` | TestDoubles.Operate | `ListTestCollectorObligationsQuery` | → lo que devuelve `ListarAsync` del contrato (resultado `Answered`, C28) | `Lending.NotActivated` (lista vacía con motivo) | K1 |
| `POST /collector/simulations` | TestDoubles.Operate | `SimulateTestCollectorPaymentQuery` | `{ obligationPublicId, modality, amount, valueDate, prepaymentChoice?, paymentIdempotencyKeyToReverse? }` → `SimularAsync` (resultado `Answered`; con la clave de un pago, muestra la redistribución antes de reversarlo, C38) | | K1 |
| `POST /collector/payments` | TestDoubles.Operate | `ApplyTestCollectorPaymentCommand` | `{ items: [PagoAAplicar], interactive, simulationId? }` → `RespuestaDelContratoDto<ComprobanteDeAplicacionDto>` por ítem con `Applied`, `AlreadyApplied` o `Rejected` (recaudador `TST`, clase `Test`, clave `TST:{guid:N}:{obligationPublicId:N}`: la pantalla propone el `guid` y deja repetirlo para probar la idempotencia, C35) | los del contrato (`Lending.Contract.*`, entre ellos `.BeforeCutoff` y `.KeyReused`; `Lending.Payment.*`) | K1 |
| `POST /collector/payments/{key}/reverse` | TestDoubles.Operate | `ReverseTestCollectorPaymentCommand` | `{ reason }` → `ReversarAsync` | `Lending.Reversal.*` | K1 |
| `POST /collector/credit-balances/resolve` | TestDoubles.Operate | `ResolveTestCollectorCreditBalanceCommand` | `{ obligationPublicId, kind: Refund \| ApplyToObligation \| ExtraordinaryPrincipal, targetObligationPublicId?, prepaymentChoice? }` → `ResolverSaldoAFavorAsync` | | K1 |
| `GET /collector/reversals` | TestDoubles.Operate | `ListTestCollectorReversalsQuery` | → los `PagoReversadoPorCartera` recibidos | | K1 |
| `POST /collector/payroll-deduction-files/{id}/confirm-sent` | TestDoubles.Operate | `ConfirmTestPayrollFileSentCommand` | → emite `EnvioDeDescuentosConfirmado` como REC | | K2 |
| `POST /collector/payroll-deduction-results` | TestDoubles.Operate | `EmitTestPayrollDeductionResultCommand` | multipart con el archivo de respuesta (formato del convenio, `FlatFileReader`) → emite `ResultadoDeDescuentos` | `Import.Invalid` | K2 |
| `GET /treasury/orders?status=` | TestDoubles.Operate | `ListTestTreasuryOrdersQuery` | → las órdenes y anulaciones recibidas por la tesorería de prueba | | K1 |
| `POST /treasury/orders/{orderPublicId}/results` | TestDoubles.Operate | `EmitTestDisbursementResultCommand` | `{ outcome: Disbursed \| Rejected \| CheckNotCashed \| Voided, reference, date }` → emite `ResultadoDelGiro` como TES | | K1 |

Pantalla `/cartera/dobles-de-prueba`. No son canales simulados ni dobles de pruebas unitarias (CH20).

---

## 21. Plataforma — `/api/core` y `/api/saas` (E-05)

Fuera del grupo `/api/lending`: no aplican el alcance de Cartera. Llevan `RequireAuthorization()`,
`ErrorEnvelopeFilter`, su permiso y `Idempotency-Key` en las escrituras. Módulos de auditoría: `Core` (registro y
canales), `NationalValues` (valores nacionales, en la cadena, diez años).

### 21.1 Registro de recaudadores y destinos — `/api/core/integration-participants` (T16)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?class=&role=&destination=&activeOn=` | Core.IntegrationParticipants.View | `ListIntegrationParticipantsQuery` | → arreglo `{ publicId, code, class: Receipts \| Payroll \| Contributions \| Treasury \| Test \| Accounting \| Lending \| Inventory, role: ObligationsCollector \| MessageDestination, destination?, isEnabled, isTestDouble, validFrom, validTo?, adoptions: [{ contract, version, validFrom }], implementationAvailable, counts }` | | K1 |
| `POST /` | Core.IntegrationParticipants.Manage | `RegisterIntegrationParticipantCommand` | `{ code, class, role, destination?, isEnabled, isTestDouble, adoptions: [{ contract, version }], validFrom, reason }` → 201. `destination` (de `IntegrationDestinations`) es obligatorio con `MessageDestination`; único `(Code, Role, Destination)` por vigencia (C36). Habilitar o inhabilitar cierra la fila y abre otra | `Integration.Participant.TestDoubleInProduction`, `.VersionNotSupported` (`data: { contract, supported[] }`), `.Overlaps` | K1 |
| `POST /{id}/retire` | Core.IntegrationParticipants.Manage | `RetireIntegrationParticipantCommand` | `{ validTo, reason }` → 204 | `Integration.Participant.InUse` (destino `Lending` con Cartera activa) | K1 |

Códigos de participante sembrados (C35): `NOM` (Nómina, recaudador, contrato v1), `CNT` (Contabilidad, destino
`Accounting`), `INV` (Comercial, destino `Inventory` de las respuestas), `CAR` (Cartera: destino `Lending`, sembrado
con `isEnabled = false` hasta la activación, que lo habilita en su misma transacción, C36) y `TST` (los dobles de
prueba, sólo fuera de producción). `CAR` como recaudador (la recogida de créditos propios, `OwnLoanPayoff`) **no**
tiene fila (T91). Las claves derivadas son `{clave}:reverso` y `{clave}:destino`. Recaudo, Tesorería y Aportes los
registra su propia feature.

### 21.2 Canales externos — `/api/core/external-channels` (T71–T73)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?kind=` | Core.ExternalChannels.View | `ListExternalChannelSettingsQuery` | → arreglo `{ publicId, kind: Sms \| WhatsApp \| SecuritiesDepository \| CreditBureau \| RestrictiveLists, bureauCode?, channelCode, validFrom, validTo?, credentialKey?, verifiedAt?, verifiedBy?, lastOutcome?, simulated, circuit: Closed \| Open }` | | K1 |
| `POST /` | Core.ExternalChannels.Manage | `ConfigureExternalChannelCommand` | `{ kind, bureauCode?, channelCode, validFrom, credentialKey?, reason }` → 201; `SIMULADO` y `CARGA-PROPIA` no piden credencial; en producción `SIMULADO` se admite antes y después de activar, siempre sin efecto legal | `Core.ExternalChannel.ChannelUnknown` (`data: { kind, available[] }`), `.CredentialKeyRequired`, `.Overlaps` | K1 |
| `POST /{id}/verify` | Core.ExternalChannels.Manage | `VerifyExternalChannelCredentialCommand` | → `{ outcome: Ok \| Rejected \| InProcess \| NotFound \| ChannelUnavailable \| InvalidData, detail, verifiedAt }` (llama `ProbarAsync`) | `Core.ExternalChannel.CredentialMissing` (no hay archivo en el Secret `erp-canales-externos`) | K1 |

La credencial vive en `/secrets/canales-externos/{tenantPublicId}.{channelCode}.json`; la base sólo guarda
`CredentialKey` (`LasCredencialesDeCanalesExternosNoTocanLaBase`). Pantalla `/admin/canales-externos`.

### 21.3 Copia de valores nacionales en la cooperativa — `/api/core/national-values`, `/api/core/regulatory-documents` (T37–T41)

Sólo lectura y conformidad: la cooperativa nunca modifica la copia.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /national-values?code=&key=&date=&recordedAsOf=&page=` | Core.NationalValues.View | `ListNationalValuesQuery` | → lista `{ publicId, code, key, validFrom, validTo?, referenceDate, value, unit, source, sequence, knownAt, supersedes?, review?: { decision: Confirmed \| Objected, by, at } \| null }`; con `date` y `recordedAsOf`, la regla bitemporal (`VigenciaBitemporal`) | | K1 |
| `POST /national-values/{id}/review` | Core.NationalValues.Review | `ReviewNationalValueCommand` | `{ decision: Confirmed \| Objected, notes }` → 201 (`COR_NationalValueReviews`, H; no toca la copia) | `NationalValues.AlreadyReviewed` | K1 |
| `GET /national-values/replications?page=` | Core.NationalValues.View | `ListNationalValueReplicationsQuery` | → bitácora de réplicas `{ at, fromSequence, toSequence, values, documents, outcome, effects[] }` | | K1 |
| `GET /regulatory-documents?kind=&date=` | Core.NationalValues.View | `ListRegulatoryDocumentsQuery` | → arreglo `{ publicId, kind, code, validFrom, schemaVersion, sha256, verified, knownAt, review? }` | | K1 |
| `GET /regulatory-documents/{id}` | Core.NationalValues.View | `GetRegulatoryDocumentQuery` | → el documento con su contenido JSON | `NationalValues.DocumentNotFound` | K1 |
| `POST /regulatory-documents/{id}/review` | Core.NationalValues.Review | `ReviewRegulatoryDocumentCommand` | `{ status, notes }` → 201 | `NationalValues.AlreadyReviewed` | K1 |

Sin revisión, un valor está «pendiente de validar»: no bloquea el cálculo, marca el cierre y el SICSES y alerta
(T41). Pantalla `/admin/valores-nacionales` (la cooperativa consulta y revisa).

### 21.4 Perfil regulatorio — `/api/core/regulatory-profile` (FR-018, T50)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?date=` | Core.RegulatoryProfile.View | `GetRegulatoryProfileQuery` | → `{ current: { organizationType, financialActivity, supervisionLevel, category?, niifGroup, payrollOperatorRegistration?: { number, date }, validFrom, support }, versions[], expectedLoss: { obligated, segments[], from?, basis }, niifGroupMismatch? }` | | K1 |
| `POST /versions` | Core.RegulatoryProfile.Manage | `AddRegulatoryProfileVersionCommand` | `{ organizationType, financialActivity, supervisionLevel, category?, niifGroup, payrollOperatorRegistration?, validFrom, supportAttachmentPublicId, reason }` → 201 + la evaluación de obligación (`EfectoDeReplicacionObligacionSobrevenida`): si obliga después de activar, alerta `Cartera.ObligacionSobrevenida` y lista créditos sin historial completo | `Core.RegulatoryProfile.Overlaps`, `.OrganizationTypeUnknown` (catálogo `TiposDeOrganizacion`) | K1 |

Pantalla `/admin/perfil-regulatorio`.

### 21.5 Registro central de valores nacionales — `/api/saas` (maestro; T37, B5)

Lo protege `RequireMasterAdminAttribute.Check` (el maestro con segundo factor), sin código de permiso. Escribe en
la base administrativa (`ADM_NationalValues`, `ADM_RegulatoryDocuments`, sólo inserción); **ningún cálculo lee
`ADM_`**: la réplica copia a cada cooperativa (§21.3).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /national-values?code=&key=&from=&to=&page=` | maestro | `ListPlatformNationalValuesQuery` | → lista con `sequence`, `registeredAt`, `registeredBy`, `supersedesPublicId?` | | K1 |
| `POST /national-values` | maestro | `RegisterNationalValueCommand` | `{ code: USURA \| IBR \| IPC \| TOPE_TASA_VIVIENDA \| UVR \| SMMLV \| UVT \| FESTIVO \| LIMITE, key, validFrom, validTo?, referenceDate, value, unit: EA \| NominalMV \| Pesos \| Fraccion \| Dias \| Meses \| Horas, source }` → 201 | `NationalValues.CodeUnknown`, `.KeyInvalid` (`data: { code, allowedKeys? }`: la clave de `USURA` es una modalidad certificada del catálogo), `NationalValues.Value.AlreadyRegistered` (ya existe desde esa fecha: va como corrección), `NationalValues.CodeNotAvailable` (`UVR` y `TOPE_TASA_VIVIENDA` hasta K6b) | K1 |
| `POST /national-values/{id}/corrections` | maestro | `CorrectNationalValueCommand` | `{ value, validFrom?, validTo?, source, reason }` → 201 la fila nueva con `SupersedesPublicId`; la réplica encola en cada cooperativa la corrida `NationalValueCorrection` si afecta días causados (T28) | `NationalValues.NotFound` | K1 |
| `POST /national-values/import?mode=review\|apply` | maestro | `ImportNationalValuesCommand` | multipart: la plantilla `saas.national-values` (.xlsx) o el paquete exportado de otro ambiente (.json) → `ImportResultDto`. Reutiliza definición, lector y reglas del ejecutor común con **su propia transacción sobre `IAdminDbContext`** (C82) | `Import.Invalid`, `NationalValues.ConflictingValue` | K1 |
| `GET /national-values/export?from=&codes=` | maestro | `ExportNationalValuesQuery` | → paquete `.json` (valores y documentos con SHA-256) para importar en otro ambiente | | K1 |
| `GET /regulatory-documents?kind=` | maestro | `ListPlatformRegulatoryDocumentsQuery` | → arreglo con `sequence`, `verified`, `sha256` | | K1 |
| `POST /regulatory-documents` | maestro | `RegisterRegulatoryDocumentCommand` | `{ kind: CatalogoDeModalidades \| TiposDeOrganizacion \| SarcAnexo1 \| SarcAnexo2 \| ReglaDePerdidaEsperada, code, validFrom, schemaVersion, content, verified }` → 201; lo valida `IValidadorDeDocumentoNormativo` de su clase | `NationalValues.Document.Invalid` (`data: { errors: [{ path, message }] }`), `NationalValues.DocumentKindNotAvailable` (`SarcAnexo2` hasta K6a) | K1 |
| `GET /regulatory-documents/{id}/content` | maestro | `GetPlatformRegulatoryDocumentContentQuery` | → el JSON del documento | | K1 |

Pantalla `/admin/valores-nacionales` (el maestro registra). La central calcula, sin leer cooperativas, qué códigos
esperados no tienen vigencia próxima y lo muestra ahí; cada cooperativa recibe `Cartera.ValorNacionalFaltante` (C69).

---

## 22. Cumplimiento — `/api/compliance` (T69, T70)

Módulo de auditoría `Compliance` (en la cadena, diez años).

### 22.1 Listas restrictivas — `/restrictive-lists` (F-18)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /` | Compliance.RestrictiveLists.View | `ListRestrictiveListLoadsQuery` | → arreglo `{ publicId, listCode, source, publishedOn, validFrom, version, status: Active \| Replaced, rows, sha256, loadedBy, loadedAt }` | | K1 |
| `GET /{id}/entries?search=&page=` | Compliance.RestrictiveLists.View | `ListRestrictiveListEntriesQuery` | → lista de entradas de la lista pública (no son personas del maestro) | `Compliance.RestrictiveList.NotFound` | K1 |
| `GET /template.xlsx` · `POST /import?mode=` | Compliance.RestrictiveLists.View · .Load | `ImportRestrictiveListCommand` | plantilla `compliance.restrictive-lists` + `{ listCode, source, publishedOn, validFrom }` → `ImportResultDto`; la carga nueva deja la anterior `Replaced` | `Import.Invalid` | K1 |

La coincidencia y su liberación son de la operación de Cartera (§4.5). Pantalla `/compliance/listas-restrictivas`.

### 22.2 Autorizaciones del titular — `/habeas-data/authorizations` (FR-006)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=` | Compliance.HabeasData.ViewHistory | `ListDataAuthorizationsQuery` | → arreglo `{ publicId, purpose: GeneralTreatment \| CreditProcessing \| CreditBureauQuery \| CreditBureauReport \| ContactChannel, contactChannel?, policyVersion { publicId, version }, origin, grantedAt, evidence?, revokedAt?, status }` | `Core.Person.NotFound` | K1 |
| `POST /` | Compliance.HabeasData.RecordConsent | `RecordDataAuthorizationCommand` | `{ personPublicId, purpose, contactChannel?, policyVersionPublicId, evidenceAttachmentPublicId, origin, grantedAt }` → 201 (adjunto `DataAuthorization`) | `Compliance.HabeasData.NoCurrentPolicy` (sin versión vigente para la finalidad) | K1 |

Las rutas existentes `consents/history/{personId:int}` y `consents/revoke` quedan en desuso (Principio VI); la
revocación va como solicitud del titular (§22.3).

### 22.3 Solicitudes del titular — `/data-subject-requests` (FR-006, T69)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&kind=&personPublicId=&dueBefore=&page=` | Compliance.DataSubjectRequests.Read | `ListDataSubjectRequestsQuery` | → lista `{ publicId, person, kind: Query \| Update \| Rectification \| Suppression \| Revocation, purpose?, receivedAt, dueDate, extended, status: Received \| Extended \| Answered \| Withdrawn, outcome? }` | | K1 |
| `GET /{id}` | Compliance.DataSubjectRequests.Read | `GetDataSubjectRequestQuery` | → la solicitud con su respuesta y, en revocación, la constancia del deber (consulta de obligaciones) | `Compliance.DataSubjectRequest.NotFound` | K1 |
| `POST /` | Compliance.DataSubjectRequests.Register | `RegisterDataSubjectRequestCommand` | `{ personPublicId, kind, purpose?, channel, receivedAt, description, attachmentPublicIds[] }` → 201 con `dueDate` en días hábiles nacionales (`PlazosDelTitular`) | | K1 |
| `POST /{id}/extend` | Compliance.DataSubjectRequests.Respond | `ExtendDataSubjectRequestCommand` | `{ reason }` → la solicitud | `Compliance.DataSubjectRequest.AlreadyExtended` | K1 |
| `POST /{id}/respond` | Compliance.DataSubjectRequests.Respond | `RespondDataSubjectRequestCommand` | `{ outcome: Granted \| PartiallyGranted \| Denied, answer, attachmentPublicId? }` → la solicitud; una revocación de finalidad de crédito con deber vigente responde `Denied` con la constancia (`ReglaDeRevocacion`) | `Compliance.DataSubjectRequest.NotOpen` | K1 |

Pantalla `/compliance/solicitudes-del-titular`.

---

## 23. Lado contable — `/api/accounting/lending` y `/api/accounting/regulatory-indicators` (E-01; DC, T51–T59)

Molde de `/api/accounting/inventory` de la 012. Permisos `Accounting.LendingRules.{View, Manage}`,
`Accounting.LendingBatches.{View, Run}`, `Accounting.RegulatoryIndicators.{View, Manage}`. Los consumidores
(`PostLendingFactsCommand`, `PostLendingSummaryGroupCommand`) y los cinco métodos de `IContabilidadParaCartera` no
tienen ruta.

### 23.1 Matriz de reglas — `/rules`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /rules/catalog` | LendingRules.View | `GetLendingRulesCatalogQuery` | → `{ operations: [{ code, requiredRoles[] }], roles: [{ code, requiredDimensions[], allowedDimensions[] }], dimensions: { creditLines[], modalities[], subsegments[], concepts[], collectorClasses[], riskCategories[], reasons[] } }` (operaciones y roles de `OperacionesDeCartera` y `RolesDeCuentaDeCartera`; valores de dimensión por `IDimensionesDeCartera`) | | K1 |
| `GET /rules?operation=&role=&asOf=&creditLineCode=&modalityCode=&subsegmentCode=&riskCategory=&conceptCode=&collectorClass=&account=&onlyCurrent=&page=` | LendingRules.View | `ListLendingPostingRulesQuery` | → lista `LendingPostingRuleDto { rulePublicId, operation, role, dimensions { creditLineCode?, modalityCode?, subsegmentCode?, riskCategory?, conceptCode?, isEmployee?, inGracePeriod?, onBehalfOfAssociate?, restructured?, collectorClass?, reasonCode?, branch?, costCenter? }, account { publicId, code, name, isEligible }, validFrom, validTo?, notes, dimensionKey, specificityWeight, isCurrent, reviewPending }` | | K1 |
| `GET /rules/{id}` · `GET /rules/{id}/versions` | LendingRules.View | `GetLendingPostingRuleQuery` · `ListLendingPostingRuleVersionsQuery` | → la regla · sus versiones de la misma `DimensionKey` | `Accounting.LendingRule.NotFound` | K1 |
| `POST /rules` | LendingRules.Manage | `CreateLendingPostingRuleCommand` | `{ operation, role, dimensions, accountPublicId, validFrom, notes, reason }` → 201 `{ rulePublicId }` | `Accounting.LendingRule.DimensionRequired`, `.DimensionNotAllowed`, `.DimensionCodeUnknown`, `.Overlaps`, `.RetroactiveOverPosted` (`data: { lastPostedDate }`), `Accounting.Account.*` | K1 |
| `POST /rules/{id}/versions` | LendingRules.Manage | `AddLendingPostingRuleVersionCommand` | `{ accountPublicId, validFrom, notes, reason }` → 201 | ídem | K1 |
| `POST /rules/{id}/deactivate` | LendingRules.Manage | `DeactivateLendingPostingRuleCommand` | `{ validTo, reason }` → 204 | | K1 |
| `GET /rules/template.xlsx[?withData=true]` · `POST /rules/import?mode=` | LendingRules.View · .Manage | `GetLendingRulesTemplateQuery` · `ImportLendingPostingRulesCommand` | plantilla de la matriz → `ImportResultDto`; auditado `Accounting.LendingRules.Imported` | `Import.Invalid` | K1 |
| `POST /rules/resolve` | LendingRules.View | `ResolveLendingRuleQuery` | `{ operation, role, dimensions, date }` → `{ rule?, account?, candidates: [{ rulePublicId, specificityWeight, matched }] }` («¿a qué cuenta va esta combinación?», sin clave) | | K1 |

Dimensiones exigidas sin `*` según el rol; opcionales con pesos en potencias de dos (Línea 512, Empleado 256,
Gracia 128, Sub-segmento 64, Reestructurado 32, PorCuentaDelAsociado 16, Centro 8, Sucursal 4; T52). Las reglas se
siembran sin cuenta, «pendiente de validar».

### 23.2 Tipos de comprobante — `/voucher-mappings`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /voucher-mappings` | LendingRules.View | `ListLendingVoucherMappingsQuery` | → arreglo `{ mappingPublicId, operation, collectorClass (\* = toda la operación), voucherType { publicId, code, name }, crossDocumentType? { publicId, code }, isSeeded, reviewPending }` (semilla `DS`, `RC`, `DN`, `CA`, `PV`, `CN`; cruce `PG` y `FV`, T54) | | K1 |
| `PUT /voucher-mappings` | LendingRules.Manage | `SetLendingVoucherMappingCommand` | `{ operation, collectorClass, voucherTypePublicId, crossDocumentTypePublicId?, reason }` → el mapeo | `Accounting.VoucherType.NotAllowedForModule` (debe ser `Usage = Module`, `ModuleCode = CAR`) | K1 |

### 23.3 Completitud — `GET /completeness?date=`

`LendingRules.View` · `GetLendingRulesCompletenessQuery` → `{ date, missingRules: [{ operation, role, dimensions,
usedBy: { creditLines[], loans }, lastUsedAt? }], ineligibleRules[], unmappedOperations[], summary }`. Las
combinaciones en uso las da Cartera por `IDimensionesDeCartera`; Contabilidad no lee `LND_`. **K1.**

### 23.4 Lotes — `/batches`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /batches?from=&to=&status=&trigger=&page=` | LendingBatches.View | `ListLendingBatchesQuery` | → lista `IntegrationBatchDto` (la forma de la 012) con `trigger` incluido `LendingAccrualRun = 7` y `LendingMonthlyClose = 8` y `scheduleKey` con prefijo `CAR.` | | K1 |
| `POST /batches/preview` | LendingBatches.View | `PreviewLendingBatchQuery` | `{ from, to, types?[], scheduleKey?, branchPublicId? }` → `{ cutoffMessagePublicId, vouchers: [{ voucherTypeCode, operationDate, branch, granularity, lines[], totals }], excluded[] }` (sin clave) | | K1 |
| `POST /batches` | LendingBatches.Run | `OrderIntegrationBatchCommand` (módulo `CAR`) | `{ cutoffMessagePublicId, from, to, types?[], scheduleKey?, branchPublicId?, reason }` → **202** `{ batchPublicId, number, status: Requested }` | `Accounting.LendingBatch.AlreadyRunning` (`data: { batchPublicId, number }`) | K1 |
| `GET /batches/{id}` | LendingBatches.View | `GetLendingBatchQuery` + `GetLendingBatchTotalsQuery` | → el lote con comprobantes, rechazados y totales (`TotalesDelLoteAsync` suma INV y CAR por separado) | `Accounting.LendingBatch.NotFound` | K1 |

El resumido de la causación es un comprobante por día con la sucursal en cada línea y una línea por crédito en
las cuentas que exigen tercero, cruce o base (T56).

### 23.5 Recibos — `GET /postings`

`GET /postings?loanNumber=&message=&batch=&from=&to=&page=` · `Accounting.Vouchers.View` ·
`ListLendingPostingsQuery` → lista `{ messagePublicId, type, version, originPublicId, relatedMessagePublicId?,
operationDate, accountingDocumentPublicId?, voucher?: { typeCode, number }, withoutVoucher?: Informational \|
ZeroValue \| NotPosted, batchPublicId?, originUserName?, originActorKind, postedAt }` (`ACC_LendingPostings`). Es
el vínculo hecho ↔ comprobante. **K1.**

### 23.6 Indicadores de la entidad — `/api/accounting/regulatory-indicators` (DC-18, T59)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?code=&asOf=` | Accounting.RegulatoryIndicators.View | `ListRegulatoryIndicatorsQuery` | → arreglo `{ publicId, code: TechnicalCapital \| SolvencyRatio, cutoffDate, value, source, support?, registeredBy, registeredAt, current, expiresOn }` | | K1 |
| `POST /` | Accounting.RegulatoryIndicators.Manage | `RegisterRegulatoryIndicatorCommand` | `{ code, cutoffDate, value, source, supportAttachmentPublicId, notes? }` → 201; auditado `Accounting.RegulatoryIndicator.Registered` | `Accounting.RegulatoryIndicator.Duplicate` (mismo código y corte) | K1 |

Sin indicador vigente (más viejo que `Contabilidad.IndicadoresVigenciaMeses`), Cartera bloquea lo que aumenta el
endeudamiento por concentración (§7.1) y aplica lo conservador de D19 en el arrastre y el deterioro. Pantalla
`/contabilidad/indicadores-regulatorios`.

### 23.7 Errores del lado contable

Canónicos (§2.17 de las transversales): `Accounting.LendingRule.{Missing, Overlaps, RetroactiveOverPosted}`,
`Accounting.LendingFact.{Unbalanced, CurrencyNotSupported, WaitingForOriginal}` (llegan a la bandeja, no a una
petición), `Accounting.Period.{LendingNotClosed, LendingPending, LendingLoadWillExpire}` y
`Accounting.Document.LendingCorrectsWithNewVoucher`. De esta sección: `Accounting.LendingRule.{DimensionRequired,
DimensionNotAllowed, DimensionCodeUnknown, NotFound}`, `Accounting.LendingBatch.{AlreadyRunning, NotFound}`,
`Accounting.RegulatoryIndicator.Duplicate` y `Accounting.VoucherType.NotAllowedForModule`.

---

## 24. Cambios en rutas existentes de otros módulos

| Ruta existente | Cambio | Por qué | E |
|---|---|---|---|
| `POST /api/accounting/periods/{year}/{month}/close` | El cuerpo acepta `acknowledgeLendingPending` y `acknowledgeLendingLoad`. Bloquea con 422 `Accounting.Period.LendingNotClosed` (sin reconocimiento posible) si Cartera está activada con fecha ≤ fin de mes y no hay cierre de Cartera del mes posterior a su última reapertura; avisa `Accounting.Period.LendingPending` (`data: { pending, inBatch, rejected, oldestOperationDate, types[] }`) y `.LendingLoadWillExpire` (`data: { loadPublicId, cutoffDate }`) | T57, FR-146 | K1 |
| `POST /api/accounting/documents/{id}/reverse` | Un comprobante de origen `CAR` responde 422 `Accounting.Document.ModuleOwned`; el poster rechaza `PrepareReversalAsync` con `Accounting.Document.LendingCorrectsWithNewVoucher` | T55 | K1 |
| `POST /api/payroll/runs/{runId}/approve` | Cuerpo + `acknowledgeLendingPendingActivation`; sin él y con deudores de una carga aplicada sin activar, 422 `Payroll.LendingPendingActivationAckRequired`; aplica el lote del contrato antes del NM; avisos `Payroll.LoanDeductionNotApplied` | T92, FR-112 | K1 |
| `POST /api/payroll/settlements/terminations/{runId}/approve` | Ídem; además `Payroll.Settlement.LoanProposalOutdated` y `.LoanAmountsChanged` (todo o nada) | T92, FR-088 | K1 |
| `POST /api/payroll/runs/{runId}/reverse` · `POST /api/payroll/settlements/terminations/{runId}/reverse` | Reversan por el contrato los recaudos de Cartera de la corrida (`ReversarAsync` con la clave del pago y el motivo), todo o nada (G3, R105); si Cartera rechaza uno, 422 `Payroll.LoanReversalRejected` (ordinaria) o `Payroll.Settlement.LoanReversalRejected` (definitiva) con el motivo de Cartera y la corrida sigue aprobada (fijado al implementar T0160/T0165) | T92, FR-088 | K1 |
| `GET /api/payroll/runs/{runId}/loan-deductions` (nueva) | `Payroll.Runs.View` · `ListPayrollLoanDeductionsQuery` → arreglo `{ employee, obligationPublicId, obligationNumber, expected, deducted, notDeducted, reason?, status: Calculated \| Applied \| PartiallyApplied \| NotApplied \| Rejected \| Reversed, carteraReceiptPublicId?, carteraReversalPublicId? }` (`PAY_PayrollLoanDeductions`) | DE-07 | K1 |
| `POST /api/payroll/legal-parameters` (alta de vigencia) y rutas de festivos | `SMMLV` y `UVT` responden 422 `Payroll.LegalParameter.NationalValue`; los festivos `National` no se editan (sólo los escribe la réplica) | T93 | K1 |
| `DELETE /api/core/people/{id}` | `Person.HasActiveLoans` (se conserva el código, mensaje nuevo) si la persona tiene obligaciones vivas como deudor o codeudor o está «migrada, sin activar» (por `IConsultaDeObligaciones`) | T94, FR-148 | K1 |
| `GET /api/core/people/{id}/detail` | `PortfolioSummaryDto` sale de la consulta de obligaciones y suma `lendingStatus: NotActivated \| Active` | T94 | K1 |
| `POST /api/inventory/sales/credit-evaluations` | Con Cartera activada devuelve el origen real (`Validated`) y los bloqueos `Inventory.Credit.LendingBlocked`, `.QuotaExceeded`, `.LendingNoResponse` | T95, FR-145 | K1 |
| `GET /api/core/payment-means` · `POST`/`PUT` | Medio de crédito con `creditWithoutLending` (sólo clases de crédito, inmutable con pagos; sujeto a A6) | T95, D29 | K1 |
| `GET /api/core/bank-file-formats` · `POST` | `scope` admite `LoanDisbursement = 4` (K1) y `PayrollDeductionRequest = 5`, `PayrollDeductionResult = 6` (K2, elegidos por referencia, sin cruce de vigencias); filtro `?scope=` | T78 | K1 / K2 |
| `POST /api/core/branches` · `PUT /{id}` | La pantalla de Agencias ofrece por fin el vínculo `tenantBranchPublicId` (agencia → oficina), que ya aceptaban los comandos | T62, C9 | K1 |
| `GET /api/admin/roles/templates?module=Lending` | Sirve las trece plantillas `cartera.*` (§2.5) | T61 | K1 |
| `GET /api/audit/logs` | Filtro nuevo `?operationKey=`: muestra juntos el intento (`Lending.ObligationContract.Attempted`) y el hecho (`…Applied`, `…Reversed`, `…CreditBalanceResolved`, `…Rejected`) del contrato; un intento sin hecho se rotula «revertido con el recaudador» (A11) | T68 | K1 |
| `GET /api/payroll/concept-definitions/{code}/accounts` | Sin cambio; la usa la pantalla de activación para avisar si `DESC_CARTERA` no tiene cuentas | T84 | K1 |
| `/api/inventory/messages*` | Sin cambio de ruta; la bandeja de Inventario pasa a la estrategia `IVistaDeBandejaDelModulo` y reprocesa los entrantes al destino `Lending` | T19 | K1 |

---

## 25. Lo heredado: `/api/savings/*` y rutas retiradas (T3, T88, T99; enmienda A10)

**Se retiran** (paso 3 de T88): las 63 rutas de crédito de `Endpoints/Lending` (`accruals`, `classifications`,
`defaults`, `housing-parameters`, `interest-rates`, `loan-applications`, `portfolios`, `payments`,
`deduction-concepts`, `payroll-deductions`, `periodicity-parameters`, `portfolio-accounts`,
`provision-parameters`, `scoring-parameters`, `term-rates`) y `aging`, `aging/pdf` y `loan-statement/{id}/pdf` de
`/api/reports/lending`. No dejan alias: responden 404.

**Se mudan** si el dueño aprueba A10 (propuesta por defecto), en el mismo commit del retiro, sin tocar tablas ni
rutas de pantalla:

| Antes | Después | Archivo |
|---|---|---|
| `/api/lending/contributions` | `/api/savings/contributions` | `Endpoints/Savings/ContributionsEndpoints.cs` |
| `/api/lending/savings-accounts` | `/api/savings/savings-accounts` | `SavingsAccountsEndpoints.cs` |
| `/api/lending/savings-interest-liquidation` | `/api/savings/savings-interest-liquidation` | `SavingsInterestEndpoints.cs` |
| `/api/lending/savings-parameters` | `/api/savings/savings-parameters` | `SavingsParametersEndpoints.cs` |
| `/api/lending/sipla-parameters` | `/api/savings/sipla-parameters` | `SiplaParametersEndpoints.cs` |
| `/api/lending/statements` | `/api/savings/statements` (sin la parte de crédito) | `StatementsEndpoints.cs` |
| `/api/lending/sub-zones` | `/api/savings/sub-zones` | `SubZonesEndpoints.cs` |
| `/api/lending/transaction-codes` | `/api/savings/transaction-codes` | `TransactionCodesEndpoints.cs` |
| `/api/lending/withdrawal-statuses` | `/api/savings/withdrawal-statuses` | `WithdrawalStatusesEndpoints.cs` |
| `/api/lending/withdrawals` | `/api/savings/withdrawals` (el bloqueo por créditos pasa a la consulta de obligaciones, T94) | `WithdrawalsEndpoints.cs` |
| `/api/lending/zone-types` | `/api/savings/zone-types` | `ZoneTypesEndpoints.cs` |
| `/api/lending/zones` | `/api/savings/zones` | `ZonesEndpoints.cs` |
| `/api/reports/lending/person-portfolio/{id}` (+ `/pdf`) | `/api/reports/savings/person-portfolio/{id}` (+ `/pdf`), **sin créditos** | `SavingsReportsEndpoints.cs` |
| `/api/reports/lending/savings-statement/{id}/pdf` | `/api/reports/savings/savings-statement/{id}/pdf` | `SavingsReportsEndpoints.cs` |

Conservan sus permisos de hoy (ninguno) y quedan **exentos por nombre** de `LosEndpointsProtegidosExigenPermiso`
hasta su feature; su auditoría usa el módulo `Savings`. Las páginas conservadas (`Pages/AhorrosYAportes`, rutas
`/cartera/ahorros*`, `/cartera/aportes`, etc., reservadas) actualizan las URL que llaman. **Si A10 se rechaza**,
estas rutas siguen en `/api/lending/*` hasta la feature de Ahorros y `ElEspacioLendingEsSoloCartera` las exime por
nombre; ningún prefijo de la tabla choca con una ruta nueva de Cartera.

---

## 26. Lo que no tiene ruta

- **Puertos que Cartera ofrece** (en proceso, autorizados con el permiso del módulo que llama, sin alcance de
  Cartera): `IConsultasDeCartera` (`EstadoCrediticioAsync`, `EstadoDeValidacionAsync`), `IReservasDeCartera`
  (`ReservarAsync`, `LiberarAsync`), `IConsultaDeObligaciones` (`ObligacionesAsync`, `CuotasDeNominaAsync`),
  `IContratoDeObligaciones` (`ListarAsync`, `SimularAsync`, `AplicarAsync`, `ReversarAsync`,
  `ResolverSaldoAFavorAsync`; `VersionDelContrato = 1`) e `IDimensionesDeCartera`. Los de escritura validan y
  auditan por su cuenta (`LosPuertosDeCarteraValidanYAuditanSusEscrituras`, T91). Su forma la fija
  `contracts/interfaces.md`; aquí sólo se exponen por los dobles de prueba (§20).
- **Puerto que Cartera consume**: `IContabilidadParaCartera` (`EvaluarAsync`, `SaldosDeCuentasMapeadasAsync`,
  `EstadoDelPeriodoAsync`, `IndicadoresDeLaEntidadAsync`, `CuentasBancariasAsync`) e `IConsultaDeAportes`.
- **Consumidores de mensajes** (`LosComandosDeConsumoNoTienenRuta`): `ConsumeCommercialCreditMessageCommand`,
  `ConsumeTreasuryResultCommand`, `ConsumePayrollDeductionResultCommand` (K2), `PostLendingFactsCommand`,
  `PostLendingSummaryGroupCommand`, `RegisterDeliveryResultCommand`.
- **Procesos**: `RunDailyAccrualCommand`, `RunNationalValueCorrectionCommand`, `ExecuteMonthlyCloseCommand`,
  `CreateBaseCloseCommand` (dentro de la activación), `ReplicateNationalValuesCommand`,
  `RegisterDebtorMessageAttemptCommand` (tarea `cartera.mensajes`), `RaiseAlertCommand` y las tareas de §2.13 de
  las transversales.
- **Emisión de mensajes**: `EmisionDeCartera` dentro de cada comando; ningún mensaje se emite por una ruta propia.
- **El desembolso en especie y los ajustes de Comercial**: nacen del consumidor de `VentaACreditoRegistrada` y
  `AjusteDeVentaACredito`; la obligación de una venta nunca se crea a mano.

---

## 27. Nombres fijados en este documento

Nombres que este contrato necesita y que `decisiones-transversales.md` no fija; siguen T1–T4 y `data-model.md`
los adopta o los cambia.

**Rutas auxiliares** (dentro de los prefijos de §2.15 de las transversales o por la regla plural-kebab): `/api/lending/modalities`,
`/commercial-term-equivalences`, `/credit-lines/{id}/scoring`, `/credit-lines/{id}/usury-check`,
`/credit-lines/{id}/close|reopen`, `/parameters/{key}/history|versions`, `/fact-types`,
`/attribution-matrices/preview`, `/alerts`, `/alerts/{id}/attend`, `/alert-types/{typeCode}/versions`,
`/simulations/schedule/pdf|send`, `/applications/{id}/{documents, submit, analysis, checks/rerun, routing,
disbursement-preview, cancel}`, `/pending-decisions`, `/credit-limits/{id}/decisions|prepayment-preference|end|revalidate`,
`/credit-limits/availability`, `/loans/{id}/{validation-failure/confirm, validation-failure/decisions,
prepayment-preference, void, schedule, schedule/versions, operations, operations/{operationId}, transactions,
payroll-transfer}`, `/loans/{id}/operations/{operationId}/reversal-preview|reverse`, `/screening-results` (lista
y detalle), `/bureau-queries/{id}/download-link`, `/sessions/{id}/presence-challenge`, `/sessions/items`,
`/collaterals/{id}/status|release`, `/promissory-notes/{id}/{refresh, physical-custody, endorse, void}`,
`/disbursement-orders/{id}/void|reissue`, `/disbursement-files/{preview, source-accounts, {id}/void}`,
`/risk/monthly-closes/{year}/{month}/{readiness, close}`, `/risk/monthly-closes/{id}/{loans, changes,
reconciliation, reopen}`, `/risk/impairment-model`, `/risk/arrears-history/completeness`,
`/risk/analysis-ratings/{id}/lift`, `/inbound-messages`, `/accounting/fact-parts/{id}`,
`/process-runs` (POST), `/payroll-agreements/{id}/{versions, contacts, pending-transfers}`,
`/payroll-not-applied/{id}/resolve`, `/write-off-batches`, `/waivers`, `/novelties`, `/debtor-events`,
`/insurance-claims`, `/insolvency-proceedings`, `/collector-assignments/distribute`, `/promises`,
`/message-templates/{code}/history|versions`, `/message-templates/preview`, `/campaigns/{id}/{preview, schedule,
cancel}`, `/migration/loads/{id}/{parts/{part}/…, reconciliation/marks, differences/accept}`,
`/activation/provisional-collections/review`, `/activation/suggested-roles`,
`/test-doubles/{collector, treasury}/…` (§20), `/api/core/national-values/replications`,
`/api/saas/regulatory-documents` (GET), `/api/compliance/habeas-data/authorizations` (POST),
`/api/compliance/data-subject-requests/{id}/extend|respond`, `/api/payroll/runs/{runId}/loan-deductions`
(nombrada por DE-07).

**Comandos y consultas** (verbo + entidad, T1): `ListCreditLinesQuery`, `GetCreditLineQuery`,
`GetCreditLineVersionQuery`, `CreateCreditLineCommand`, `AddCreditLineVersionCommand`, `CloseCreditLineCommand`,
`ReopenCreditLineCommand`, `DeleteCreditLineCommand`, `CheckCreditLineUsuryQuery`, `ImportCreditLinesCommand`,
`ListModalitiesQuery`, `ListCommercialTermEquivalencesQuery`, `SetCommercialTermEquivalencesCommand`,
`GetCreditLineScoringQuery`, `SetCreditLineScoringCommand`, `ListAttributionMatricesQuery`,
`GetAttributionMatrixQuery`, `AddAttributionMatrixVersionCommand`, `PreviewAttributionRoutingQuery`,
`ListGoverningBodiesQuery`, `GetGoverningBodyQuery`, `CreateGoverningBodyCommand`,
`AddGoverningBodyRulesVersionCommand`, `AddGoverningBodyMemberCommand`, `EndGoverningBodyMemberCommand`,
`DeactivateGoverningBodyCommand`, `ListRelatedPartiesQuery`, `RegisterRelatedPartyCommand`,
`EndRelatedPartyCommand`, `ImportRelatedPartiesCommand`, `GetLendingUserScopeQuery`,
`SetUserCreditLineScopesCommand`, `ListLendingTemplatesQuery`, `GetLendingTemplateQuery`,
`RenderScheduleSimulationPdfQuery`, `SendScheduleSimulationCommand`, `ListCreditApplicationsQuery`,
`GetCreditApplicationQuery`, `CreateCreditApplicationCommand`, `UpdateCreditApplicationCommand`,
`AttachCreditApplicationDocumentCommand`, `RemoveCreditApplicationDocumentCommand`,
`SubmitCreditApplicationCommand`, `RecordCreditApplicationAnalysisCommand`, `RerunCreditApplicationChecksCommand`,
`GetCreditApplicationRoutingQuery`, `CancelCreditApplicationCommand`, `ListPendingLendingDecisionsQuery`,
`RecordCreditDecisionCommand`, `ListBureauQueriesQuery`, `GetBureauQueryQuery`,
`IssueBureauReportDownloadLinkCommand`, `ListScreeningResultsQuery`, `GetScreeningResultQuery`,
`ListSessionsQuery`, `GetSessionQuery`, `CreateSessionCommand`, `RecordSessionAttendanceCommand`,
`AddSessionItemCommand`, `UpdateSessionItemCommand`, `RemoveSessionItemCommand`,
`CreateSessionPresenceChallengeCommand`, `RecordSessionVoteCommand`, `CloseSessionCommand`,
`GetSessionMinutesPdfQuery`, `ListSessionItemsQuery`, `ListCollateralsQuery`, `GetCollateralQuery`,
`RegisterCollateralCommand`, `UpdateCollateralCommand`, `ChangeCollateralStatusCommand`,
`ReleaseCollateralCommand`, `ListPromissoryNotesQuery`, `CreatePromissoryNoteCommand`,
`RefreshPromissoryNoteStatusCommand`, `RegisterPhysicalPromissoryNoteCustodyCommand`,
`EndorsePromissoryNoteCommand`, `VoidPromissoryNoteCommand`, `ListCreditLimitsQuery`,
`GetCreditLimitAvailabilityQuery`, `AssignCreditLimitCommand`, `ChangePrepaymentPreferenceCommand`,
`GetPrepaymentPreferenceQuery`, `EndCreditLimitCommand`, `RevalidateCreditLimitCommand`, `ListDebtorBlocksQuery`,
`RegisterDebtorBlockCommand`, `LiftDebtorBlockCommand`, `ListLoanNumberReservationsQuery`,
`ListContributionBalancesQuery`, `ImportContributionBalancesCommand`, `ListLoansQuery`, `GetLoanQuery`,
`GetRepaymentScheduleQuery`, `ListLoanOperationsQuery`, `GetLoanOperationQuery`, `ListLoanTransactionsQuery`,
`GetLoanRatingQuery`, `ConfirmValidationFailureCommand`, `VoidPendingLoanCommand`, `PreviewDisbursementQuery`,
`ExecuteLoanDisbursementCommand` (no se reutiliza el heredado `DisburseLoanCommand`, T96),
`ListLoanDisbursementsQuery`, `ListDisbursementOrdersQuery`, `GetDisbursementOrderQuery`,
`VoidDisbursementOrderCommand`, `ReissueDisbursementOrderCommand`, `ListDisbursementFilesQuery`,
`ListDisbursementSourceAccountsQuery`, `PreviewDisbursementFileQuery`, `GenerateDisbursementFileCommand`,
`IssueDisbursementFileDownloadLinkCommand`, `VoidDisbursementFileCommand`, `PreviewLoanPaymentReversalQuery`,
`ListLoanNoveltiesQuery`, `GetLoanNoveltyQuery`, `RequestLoanNoveltyCommand`, `SimulateLoanNoveltyQuery`,
`SubmitLoanNoveltyCommand`, `RegisterNoveltyConsentCommand`, `ApplyLoanNoveltyCommand`, `CancelLoanNoveltyCommand`,
`ListWaiversQuery`, `RequestWaiverCommand`, `ApplyWaiverCommand`, `CancelWaiverCommand`,
`ListWriteOffBatchesQuery`, `GetWriteOffBatchQuery`, `CreateWriteOffBatchCommand`, `AddWriteOffBatchItemCommand`,
`RemoveWriteOffBatchItemCommand`, `SubmitWriteOffBatchCommand`, `ApplyWriteOffBatchCommand`,
`ListDebtorEventsQuery`, `RegisterDebtorEventCommand`, `ListInsuranceClaimsQuery`,
`ChangeInsuranceClaimStatusCommand`, `ListInsolvencyProceedingsQuery`, `OpenInsolvencyProceedingCommand`,
`RecordInsolvencyFailureCommand`, `GetRiskPolicyQuery`, `ListDailyRatingsQuery`, `ListAnalysisRatingsQuery`,
`ListMonthlyClosesQuery`, `GetMonthlyCloseReadinessQuery`, `GetMonthlyCloseQuery`, `ListMonthlyCloseLoansQuery`,
`GetMonthlyCloseChangesQuery`, `GetImpairmentModelBySegmentQuery`, `ListImpairmentModelElectionsQuery`,
`GetArrearsHistoryCompletenessQuery`, `ListPortfolioEvaluationsQuery`, `GetPortfolioEvaluationQuery`,
`PreparePortfolioEvaluationCommand`, `ApplyPortfolioEvaluationCommand`, `ListPayrollAgreementsQuery`,
`GetPayrollAgreementQuery`, `CreatePayrollAgreementCommand`, `AddPayrollAgreementVersionCommand`,
`SetPayrollAgreementContactsCommand`, `ListPendingPayrollTransfersQuery`, `ImportPayrollAgreementsCommand`,
`ListPayrollDeductionFilesQuery`, `GetPayrollDeductionFileQuery`, `PreviewPayrollDeductionFileQuery`,
`GeneratePayrollDeductionFileCommand`, `IssuePayrollDeductionFileDownloadLinkCommand`,
`VoidPayrollDeductionFileCommand`, `ListInstallmentNonDeductionsQuery`, `ResolveInstallmentNonDeductionCommand`,
`TransferLoanPayrollCommand`, `ListMessageTemplatesQuery`, `GetMessageTemplateHistoryQuery`,
`CreateMessageTemplateVersionCommand`, `RenderMessageTemplatePreviewQuery`, `ListDebtorMessagesQuery`,
`GetDebtorMessageQuery`, `SendDebtorMessageCommand`, `CancelDebtorMessageCommand`, `ListPriorNoticesQuery`,
`SendPriorNoticeCommand`, `RegisterPriorNoticeEvidenceCommand`, `ListCreditReportClaimsQuery`,
`RegisterCreditReportClaimCommand`, `ExtendCreditReportClaimCommand`, `AnswerCreditReportClaimCommand`,
`WithdrawCreditReportClaimCommand`, `ListCollectionStagesQuery`, `AddCollectionStageVersionCommand`,
`ListCollectorAssignmentsQuery`, `AssignCollectorCommand`, `DistributeCollectionsCommand`,
`GetCollectorAgendaQuery`, `ListCollectionActionsQuery`, `RecordCollectionActionCommand`,
`VoidCollectionActionCommand`, `ListPaymentPromisesQuery`, `ListPaymentAgreementsQuery`,
`CreatePaymentAgreementCommand`, `ApprovePaymentAgreementCommand`, `CancelPaymentAgreementCommand`,
`ListLegalProcessesQuery`, `OpenLegalProcessCommand`, `RecordLegalProcessActionCommand`,
`CloseLegalProcessCommand`, `ListMessageCampaignsQuery`, `CreateMessageCampaignCommand`,
`PreviewMessageCampaignQuery`, `ScheduleMessageCampaignCommand`, `CancelMessageCampaignCommand`,
`ListIssuedLendingDocumentsQuery`, `GetIssuedLendingDocumentQuery`, `IssueLendingDocumentDownloadLinkCommand`,
`ListLendingInboundMessagesQuery`, `GetLendingInboundMessageQuery`, `MarkLendingInboundMessageNotRetryableCommand` (C95), `ListLendingAccountingFactsQuery`,
`GetAccountingFactPartQuery`, `GetLendingAccountingReconciliationQuery`, `ListLendingProcessRunsQuery`,
`GetLendingProcessRunQuery`, `RetryLendingProcessRunCommand`, `ListRegulatoryLayoutsQuery`,
`ValidateCreditBureauFileQuery`, `ListRegulatoryFilesQuery`, `GetRegulatoryFileQuery`,
`IssueRegulatoryFileDownloadLinkCommand`, `ListMigrationLoadsQuery`, `GetMigrationLoadQuery`,
`CreateMigrationLoadCommand`, `ImportMigrationPartCommand` (el nombre de `plantillas.md`), `GetMigrationBalanceCheckQuery`,
`GetMigrationSalesReconciliationQuery`, `MarkMigrationSaleCommand`, `AcceptMigrationDifferencesCommand`,
`GetLendingActivationStatusQuery`, `ReviewProvisionalCollectionsCommand`, los nueve de los dobles de prueba
(`ListTestCollectorObligationsQuery`, `SimulateTestCollectorPaymentQuery`, `ApplyTestCollectorPaymentCommand`,
`ReverseTestCollectorPaymentCommand`, `ResolveTestCollectorCreditBalanceCommand`, `ListTestCollectorReversalsQuery`,
`ConfirmTestPayrollFileSentCommand`, `EmitTestPayrollDeductionResultCommand`, `ListTestTreasuryOrdersQuery`,
`EmitTestDisbursementResultCommand`), `ListExternalChannelSettingsQuery`, `ListNationalValuesQuery`,
`ReviewNationalValueCommand`, `ListNationalValueReplicationsQuery`, `ListRegulatoryDocumentsQuery`,
`GetRegulatoryDocumentQuery`, `ReviewRegulatoryDocumentCommand`, `GetRegulatoryProfileQuery`,
`ListPlatformNationalValuesQuery`, `ExportNationalValuesQuery`, `ListPlatformRegulatoryDocumentsQuery`,
`GetPlatformRegulatoryDocumentContentQuery`, `ListRestrictiveListLoadsQuery`, `ListRestrictiveListEntriesQuery`,
`ImportRestrictiveListCommand`, `ListDataAuthorizationsQuery`, `RecordDataAuthorizationCommand`,
`ListDataSubjectRequestsQuery`, `GetDataSubjectRequestQuery`, `RegisterDataSubjectRequestCommand`,
`ExtendDataSubjectRequestCommand`, `RespondDataSubjectRequestCommand`, `GetLendingRulesCatalogQuery`,
`ListLendingPostingRulesQuery`, `GetLendingPostingRuleQuery`, `ListLendingPostingRuleVersionsQuery`,
`ListLendingVoucherMappingsQuery`, `ListLendingBatchesQuery`, `GetLendingBatchQuery`,
`ListRegulatoryIndicatorsQuery`, `ListPayrollLoanDeductionsQuery`. Se reutilizan los de la plataforma:
`ListParametersQuery`, `GetParameterHistoryQuery`, `AddParameterVersionCommand`, `ListPermissionAmountLimitsQuery`,
`SetPermissionAmountLimitCommand`, `ListAlertsQuery`, `AttendAlertCommand`, `ListAlertTypesQuery`,
`SaveAlertTypeCommand`, `ListIntegrationMessagesQuery`, `GetIntegrationMessageQuery`, `ReprocessMessagesCommand`,
`SendNotApplicableMessagesCommand`, `OrderIntegrationBatchCommand`.

**Enums y constantes de cuerpo** (C100): los nombres y números son los de `data-model.md` (tabla de enumeraciones
de la parte 1, lista de la parte 2 —«valores desde 1 en el orden escrito»— y §9); en JSON entran por nombre o por
número y salen como número (`EnumPorNombreONumero`). Constante `LendingDecisionSubjects` (de S4, con
`RiskPolicyVersion` de C52): `CreditApplication`, `CreditLimit`, `Novelty`, `Condonation`, `WriteOffBatch`,
`PortfolioEvaluation`, `ImpairmentModelChange`, `ActivationDifferences`, `ValidationFailureException`,
`RiskPolicyVersion`.

- **Líneas**: `CreditLineStatus { Draft = 1, Active = 2, Closed = 3 }`, `CreditLineClosedReason { Manual = 1,
  ModalityRetired = 2 }` (el bloqueo por usura no es un motivo de cierre: `usuryBlockedSince`), `DebtorKind {
  Associate = 1, Customer = 2 }` (sin «empleado»: `forEmployees`, C96), `RepaymentChannel { Cashier = 1,
  OwnPayroll = 2, PaymasterDeduction = 3, DirectDebit = 4 }`.
- **Solicitudes**: `ApplicationKind { Loan = 1, RevolvingLimit = 2 }`, `CreditApplicationStatus { Draft = 1,
  Submitted = 2, PendingDecision = 3, Returned = 4, Approved = 5, Denied = 6, Disbursed = 7, Granted = 8,
  Cancelled = 9 }` (sin `Expired` ni `Blocked`: «aprobación vencida» y «bloqueada por listas» se calculan y la
  respuesta los muestra, C53; «aprobada con condiciones» es el desenlace `CreditDecisionOutcome.ApproveWithConditions`),
  `ApplicationCheckKind { RestrictiveLists = 1, CreditBureau = 2, PaymentCapacity = 3, DeductionCapacity = 4,
  ConsolidatedExposure = 5, CreditLimit = 6, Concentration = 7, Usury = 8, LineRequirements = 9,
  DataAuthorization = 10, Scoring = 11, RelatedParty = 12, Documents = 13 }`, `CheckStage { AtSubmission = 1,
  BeforeDecision = 2, BeforeDisbursement = 3 }`, `CheckOutcome { Passed = 1, Warning = 2, Blocking = 3,
  NotApplicable = 4, Unavailable = 5 }`, `CreditDecisionOutcome { Approve = 1, ApproveWithConditions = 2, Deny = 3,
  Return = 4 }`.
- **Listas**: `ScreeningContext { Application = 1, Sale = 2, CreditLimit = 3, ListRescan = 4 }`, `ScreeningOutcome {
  NoMatch = 1, PossibleMatch = 2, Match = 3, Unavailable = 4 }`.
- **Órganos y actas**: `GoverningBodyKind { BoardOfDirectors = 1, OversightBoard = 2, CreditCommittee = 3,
  RiskCommittee = 4, Management = 5, Other = 99 }`, `BodyMemberRole { Chair = 1, Secretary = 2, Member = 3,
  Alternate = 4 }`, `SessionKind { Ordinary = 1, Extraordinary = 2 }`, `SessionMode { InPerson = 1, Virtual = 2,
  Mixed = 3 }`, `SessionStatus { Draft = 1, Open = 2, Closed = 3 }`, `AttendanceStatus { Present = 1, Absent = 2,
  Excused = 3 }`, `SessionItemKind { Decision = 1, Recommendation = 2 }`, `VotingRule { Majority = 1,
  RelatedPartyQualified = 2 }`, `VoteValue { For = 1, Against = 2, Abstain = 3 }`, `VoteMethod {
  RecordedBySecretary = 1, OwnSession = 2, InPersonPasskey = 3, InPersonTotp = 4 }`, `RelatedPartyBasis {
  BoardMember = 1, OversightBoardMember = 2, LegalRepresentative = 3, SignificantContributor = 4, Spouse = 5,
  Relative = 6 }`.
- **Garantías, pagarés y cupos**: `CollateralKind { PromissoryNote = 1, CoDebtor = 2, Pledge = 3, Mortgage = 4,
  GuaranteeFund = 5, Contributions = 6 }`, `CollateralStatus { Constituted = 1, Valid = 2, Released = 3,
  Extinguished = 4, Executed = 5 }`, `PromissoryNoteKind { Physical = 1, Dematerialized = 2 }`,
  `PromissoryNoteStatus { Created = 1, PendingSignature = 2, Signed = 3, InCustody = 4, Endorsed = 5, Voided = 6 }`,
  `CreditLimitKind { Consolidated = 1, ByLine = 2, Revolving = 3 }`, `CreditLimitOrigin { Calculated = 1,
  Assigned = 2, Migration = 3 }` (el estado del cupo es texto de presentación, sin enum guardado),
  `DebtorBlockReason { Manual = 1, Arrears = 2, ValidationFailed = 3, WrittenOff = 4, RestrictiveListMatch = 5,
  AssociateWithdrawal = 6, Deceased = 7, Insolvency = 8, Garnishment = 9, LimitRevalidation = 10 }` (también el
  `kind` de `/debtor-events`: `Deceased`, `AssociateWithdrawal`, `Garnishment`).
- **Desembolso y crédito**: `DisbursementForm { Transfer = 1, Check = 2, ThirdPartyPayment = 3, InKind = 4 }`,
  `DisbursementOrderStatus { Issued = 1, Disbursed = 2, Rejected = 3, CheckNotCashed = 4, Voided = 5 }`,
  `LoanStatus { PendingDisbursement = 1, Active = 2, PaidOff = 3, WrittenOff = 4, Voided = 5, Novated = 6 }`.
- **Novedades, condonaciones y castigo** (parte 2, desde 1 en el orden escrito): `NoveltyMechanism { Extension,
  PaymentDateChange, RateChange, Novation, Refinancing }` (el acuerdo de insolvencia es una novedad
  `Restructuring` con su proceso, no un mecanismo), `NoveltyCause { Restructuring, Modification }`,
  `LoanNoveltyStatus { Draft, Submitted, Approved, Rejected, Applied, Cancelled }`, `ConsentRefusalResolution {
  DoNotApply, ApplyWithGuaranteeExtinguished }`, `WaiverStatus` y `WriteOffBatchStatus { Draft, Submitted,
  Approved, Rejected, Applied, Cancelled }`, `WriteOffItemStatus { Included, Rejected, WrittenOff }`,
  `InsuranceClaimStatus { InProcess = 1, Paid = 2, Denied = 3, Withdrawn = 4 }`, `InsolvencyProceedingStatus {
  Negotiating = 1, Agreement = 2, Failed = 3 }`.
- **Riesgo**: `RiskPolicyVersionStatus { PendingValidation, Validated }` (calculado), `ImpairmentModelElectionKind {
  VoluntaryAdoption, VoluntaryWithdrawal }`, `PortfolioEvaluationStatus { Draft, Recommended, Approved, Applied,
  Rejected, Discarded }`, `ImpairmentModel { ArrearsAnnex1 = 1, ExpectedLossAnnex2 = 2 }`, `ModelBasis {
  NotObligated = 1, Obligated = 2, Voluntary = 3 }`.
- **Libranza y cobranza**: `NonDeductionOrigin { Contract = 1, Activation = 2, PaymasterResult = 3 }`,
  `NonDeductionFollowUpAction { DebtorNotified, IncludedInNextFile, MovedToCashPayment, SentToCollection, Closed }`
  (la marca no tiene estado propio: «abierta» = sin acción `Closed`), `CollectionActionChannel { Phone, InPerson,
  Email, Sms, WhatsApp, Letter, Visit }`, `PaymentPromiseStatus { Pending, Kept, Broken }` (calculado),
  `PaymentAgreementStatus { Proposed, Approved, Rejected, Cancelled }`, `PaymentAgreementPerformance { Current,
  Fulfilled, Broken }` (calculado), `MessageCampaignStatus { Draft, Scheduled, Running, Completed, Cancelled }`,
  `PriorNoticeStatus { Pending, InProgress, Valid, Closed }` (calculado).
- **Migración**: `SaleReconciliationOutcome { Migrated, CancelledBeforeCutoff, NotMigratedWithBalance,
  OutsideLendingD29 }`.
- **Plataforma y cumplimiento** (§9.2): `DataSubjectRequestKind { Query = 1, Update = 2, Rectification = 3,
  Suppression = 4, Revocation = 5 }`, `DataSubjectRequestStatus { Received = 1, Extended = 2, Answered = 3,
  Withdrawn = 4 }`, `DataSubjectRequestOutcome { Granted = 1, PartiallyGranted = 2, Denied = 3 }`,
  `NationalValueReviewDecision { Confirmed = 1, Objected = 2 }`, `RegulatoryIndicatorKind { TechnicalCapital = 1,
  SolvencyRatio = 2 }`.

Los estados derivados que salen por nombre (`status` de cierres, documentos, promesas, cupos, avisos previos) son
texto de presentación y no se guardan.

**Códigos de error nuevos** (422 salvo `*.NotFound`, 404). Los que `data-model.md` §10 fija (activación, carga,
cierre, contrato antes del corte, recogida con mora, concentración, línea cerrada, firmante, solapes) se usan aquí
literales y no se repiten en esta lista (C71). Los que agrega este contrato: `Lending.Permission.Required`,
`Lending.AmountLimit.Exceeded`, `Lending.Currency.NotSupported`, `Lending.Office.WithoutAccountingBranch`,
`Lending.CreditLine.{NotFound, ModalityUnknown, ModalityNotAvailable, NonMembersNotAllowed, VersionOverlaps,
AlreadyClosed, NotClosed}`, `Lending.CommercialTermEquivalence.{Duplicate, PeriodicityUnknown}`,
`Lending.Scoring.{WeightsInvalid, VariableUnknown}`, `Lending.Parameter.NotYetAvailable`,
`Lending.AttributionMatrix.NotFound`, `Lending.Attribution.{MatrixInvalid, LevelUnknown}`,
`Lending.GoverningBody.{NotFound, RulesInvalid, MemberDuplicate, MemberNotFound, InUse}`,
`Lending.RelatedParty.{NotFound, Duplicate, LinkRequired}`, `Lending.Scope.DefaultDuplicate`,
`Lending.Template.NotFound`, `Lending.Schedule.OutOfLineRange`, `Lending.Application.{NotFound, ApplicantNotMember,
NotEditable, RequirementUnknown, DocumentsMissing, NotInAnalysis, NotApproved, ApprovalExpired, AlreadyDisbursed}`,
`Lending.Decision.{ContentChanged, NotPending}`, `Lending.BureauQuery.NotFound`, `Lending.ScreeningResult.NotFound`,
`Lending.Screening.{NotPending, Blocked}`, `Lending.Session.{NotFound, ItemMismatch, SubstituteInvalid,
SubjectNotPending, SubjectNotForBody, ItemHasVotes, MemberNotUser, VoteDuplicate}`, `Lending.Collateral.{NotFound,
OwnerLocked, KindNotAdmitted, TransitionInvalid, FinalityPending, LoanNotPaidOff, NotPerfected}`,
`Lending.PromissoryNote.{AlreadyExists, InvalidState}`,
`Lending.CreditLimit.{Overlaps, NotActive, NotRevolving}`, `Lending.Block.{Duplicate, Automatic}`,
`Lending.Loan.{NotFound, NotActive, NotValidationFailed, NotPendingDisbursement, NotPayroll}`,
`Lending.LoanOperation.{NotFound, NotPayment}`, `Lending.Payment.{SpecificInstallmentBlocked,
AdvanceDoesNotReduceInterest}` (el segundo es aviso), `Lending.Reversal.PreviewChanged`,
`Lending.Disbursement.{TrancheNotAllowed, ExceedsApproved}`,
`Lending.DisbursementOrder.{NotFound, NotVoidable, InFile, NotReissuable}`,
`Lending.DisbursementFile.{AccountNotOfFormatBank, OrderAlreadyInFile, HasResults}`, `Lending.Novelty.{NotFound,
ModificationNotAllowed, PartyUnknown, NotApproved, ConsentMissing, CoverageInsufficient, AlreadyApplied}`,
`Lending.Waiver.{ExceedsBalance, NotApproved}`, `Lending.WriteOffBatch.{NotFound, NotDraft}`,
`Lending.WriteOff.{RequirementMissing, ApprovalRequired}`, `Lending.DebtorEvent.Duplicate`,
`Lending.InsuranceClaim.NotInProcess`, `Lending.Insolvency.{AlreadyOpen, NotNegotiating}`,
`Lending.RiskPolicy.{LessStrict, SchemaInvalid}`, `Lending.AnalysisRating.{NotWorse, NotActive}`,
`Lending.MonthlyClose.NotFound`, `Lending.ImpairmentModel.{HistoryIncomplete,
NoticeOutOfTerm, ObligatoryNoWithdrawal}`, `Lending.ArrearsHistory.LoanNotInLoad`, `Lending.PortfolioEvaluation.{NotFound,
Overlaps}`, `Lending.PayrollAgreement.{NotFound, NotAvailable, PayerNotLegalEntity, OperatorRegistrationMissing,
VersionOverlaps, NoCurrentVersion}`, `Lending.PayrollDeductionFile.NotFound`, `Lending.Payroll.DeductionCapacityExceeded`,
`Lending.InstallmentNonDeduction.AlreadyResolved`, `Lending.MessageTemplate.{NotFound, VariableNotAllowed, SmsTooLong,
Overlaps}`, `Lending.DebtorMessage.{NotFound, AlreadySent}`, `Lending.PriorNotice.{AlreadyEvidenced, EvidenceDuplicate}`,
`Lending.CreditReportClaim.{NotInProcess, AlreadyExtended}`, `Lending.CollectionStage.RangesInvalid`,
`Lending.CollectionAction.ContactNotObligated`, `Lending.PaymentAgreement.{ChangesTerms, NotProposed}`,
`Lending.LegalProcess.LoanNotInLegalStage`, `Lending.Document.{NotFound, KindNotAvailable, AlreadyVoided}` (`KindNotAvailable` lo nombra C88),
`Lending.InboundMessage.{NotFound, NotMarkable}` (`NotMarkable`, C95), `Lending.AccountingFactPart.NotFound`, `Lending.Accounting.NotStarted`,
`Lending.ProcessRun.{NotFound, KindNotOrderable, NotRetryable}`, `Lending.Report.{RangeInvalid, RangeTooLong,
FilterInvalid}`, `Lending.Regulatory.BureauUnknown`, `Lending.RegulatoryFile.{NotFound, NoFile, NotGenerated}`,
`Lending.MigrationLoad.{NotFound, SaleNotPending}`, `Lending.Migration.PartMissing` (de `plantillas.md`);
de plataforma: `Integration.Participant.InUse`, `Core.ExternalChannel.{ChannelUnknown,
CredentialKeyRequired, Overlaps, CredentialMissing, NotConfigured}`, `Core.RegulatoryProfile.{Overlaps,
OrganizationTypeUnknown}`, `NationalValues.{AlreadyReviewed, DocumentNotFound, CodeUnknown, KeyInvalid,
CodeNotAvailable, NotFound, DocumentKindNotAvailable}`, `Compliance.RestrictiveList.NotFound`,
`Compliance.DataSubjectRequest.{NotFound, AlreadyExtended, NotOpen}`, `Core.BankFileFormat.ScopeMismatch`;
contables: `Accounting.LendingRule.{DimensionRequired, DimensionNotAllowed, DimensionCodeUnknown, NotFound}`,
`Accounting.LendingBatch.{AlreadyRunning, NotFound}`, `Accounting.RegulatoryIndicator.Duplicate`.

**Eventos de auditoría nuevos**: `Lending.Session.Viewed`, `Lending.RelatedParty.Viewed`, `Lending.Document.Verified`
(además de los de S8 que §2.17 de las transversales no lista: `Lending.Payoff.Viewed`,
`Lending.CreditBureauReport.Viewed`, `Lending.Document.Reprinted`, `Lending.Report.Exported`,
`Lending.CreditStatus.Queried`, `Lending.Access.Denied`, `Lending.Access.OutOfScope`, `Lending.Request.Rejected`).

**Otros**: clave de recaudador del doble de prueba `TST:{guid:N}:{obligationPublicId:N}` (gramática de T21; C35
retira `TEST:`);
nombres de catálogo para `GET /api/catalogos/{catalogo}/codigo/{codigo}`: `lineas-de-credito`, `organos`,
`etapas-de-cobranza`, `pagadurias`; archivo de rutas por área `Endpoints/Lending/{CreditLines, Parameters,
Attributions, GoverningBodies, RelatedParties, Scopes, Templates, Alerts, Simulations, Applications, Decisions,
BureauQueries, ScreeningResults, Sessions, Collaterals, PromissoryNotes, CreditLimits, Blocks, NumberReservations,
ContributionBalances, Loans, Disbursements, DisbursementOrders, DisbursementFiles, Novelties, Waivers, WriteOffs,
DebtorEvents, Risk, PayrollAgreements, PayrollDeductionFiles, DebtorMessages, MessageTemplates, Collections,
Campaigns, Documents, Messages, Accounting, ProcessRuns, Integrity, Dashboard, Regulatory, Migration, Activation,
TestDoubles}Endpoints.cs`.

**Recuento** (2026-10-05, contado sobre las tablas de §3 a §24 y las rutas que el texto nombra, sin repetir las que
aparecen en dos secciones —`disbursement-preview` y las decisiones de novedades y condonaciones—): **380 rutas
distintas** (con `mark-not-retryable`, C95). 305 en `/api/lending` (10 de ellas, los dobles de prueba, sólo fuera de producción), 21 en
`/api/reports/lending` (el registro y 20 vistas), 14 en `/api/core`, 8 en `/api/saas`, 11 en `/api/compliance`,
20 en `/api/accounting` y 1 nueva en `/api/payroll` (`loan-deductions`); los cambios a rutas existentes de §24 y las
mudanzas de §25 no se cuentan. Permisos: **123 códigos `Lending.*`** (§2.1) y **20 códigos nuevos de otros
módulos** (§2.2: 6 `Accounting.*`, 8 `Core.*`, 6 `Compliance.*`).

---

## 28. Conciliación

Los puntos que este contrato dejó para conciliar se decidieron en `decisiones-transversales.md` §7 (siglas `API-n`).
El cuerpo ya aplica cada decisión.

| Punto | Decisión |
|---|---|
| API-1 `asOf` con dos sentidos | C83: `date` = D y `recordedAsOf` = R en `/loans/*`, `/risk/ratings`, `/api/core/national-values` (y `/parameters`); `asOf` = fecha de corte en informes y tablero |
| API-2 `Reports.View` con deudores | C84: las vistas PD exigen además `Loans.Read` y `ExportPersonalData` para exportar |
| API-3 enviar la simulación | C87: a la persona simulada con autorización y correo, sólo correo, propósito `Informative` |
| API-4 reabrir un cierre | C58: 202; la transacción de T47 corre en la corrida `CloseReopen`, con su propio lote `LendingClose:{aaaa-mm}` |
| API-5 permiso de la preferencia | C85: `Lending.PrepaymentPreferences.Manage` (K1, plantillas asesor y jefe) |
| API-6 ruta de condonaciones | C85: `/api/lending/waivers`, lectura con `Loans.Read` |
| API-7 claves de las rutas del cierre | C88: ordenar y preparar por `{year}/{month}`, detalle por `{id}` |
| API-8 no aplicados de la nómina propia | C88: en K1 en el estado de cuenta y la alerta; lista y resolución en K2 |
| API-9 `BackdateBeforeClose` en el contrato | C41: el contrato consulta permisos y límite por `IPermissionChecker`; lo describe `interfaces.md` |
| API-10 listar coincidencias en listas | C84: lista y detalle exigen `Compliance.RestrictiveLists.Release` |
| API-11 cuadre de una carga | C85: `Lending.Reconciliation.View` |
| API-12 nombres bajo `View` | C84: nombre, cargo y contacto institucional, nunca documento |
| API-13 avisos previos y reclamos | C88: lectura desde K1, escritura desde K4 |
| API-14 documentos en K1 | C88: plan de pagos, paz y salvo y certificado de saldo en K1; extracto y certificado tributario en K5 (`Lending.Document.KindNotAvailable`) |
| API-15 completitud del historial de mora | C88: desde K1 |
| API-16 cobros provisionales | C62: la activación los valida y guarda en `LND_ProvisionalCollections`; el consumidor de Comercial los aplica al nacer la obligación |
| API-17 A10 sin respuesta | Sin decisión en §7: sigue la pregunta A10 de la §5 y rige su propuesta (mudanza a `/api/savings`, §25) |
| API-18 referencias cruzadas | §7.6: ante duda gana el contrato dueño del tema (`api.md` rutas y permisos; `data-model.md` tablas, enums y errores) |
| API-19 consulta a centrales | C84: `POST /bureau-queries` devuelve sólo el estado; el contenido, por `ReadReports` |
| API-20 `cartera.administrador` con decisiones | C86: excluye `Lending.Decisions.*` y `Lending.Sessions.Vote` |
| API-21 concentración antes de activar | Sin decisión en §7: rige la lectura (sin patrimonio técnico, `Lending.Concentration.TechnicalCapitalUnavailable`); el runbook de puesta en marcha pide registrarlo antes de otorgar cupos |

Además se aplicaron aquí C28 (`Answered` en listar y simular, §20), C35 (claves `TST:`, códigos sembrados), C36
(`destination` e `isEnabled` del registro), C38, C60–C63 (activación), C64 (partes de la migración), C69, C71 (códigos
de `data-model.md` §10, literales) y C82 (importación de valores nacionales).


---

## 29. Enmienda K6b (2026-10-17): vivienda Ley 546 y UVR

Ninguna ruta nueva. Las respuestas ganan campos **opcionales** (nulos o con su defecto fuera de vivienda y UVR), así lo de K1–K6a
no cambia; las entradas aceptan dos campos nuevos.

| Ruta | Campo | Qué es |
|---|---|---|
| `POST /credit-lines`, `POST /credit-lines/{id}/versions` (entrada) · `CreditLineVersionDto` | `denominationUnit` (`Pesos` por defecto, `Uvr`) | La unidad del capital. `Uvr` sólo en una combinación «admite UVR» del catálogo de modalidades y sin tramos (`Lending.Housing.UvrNotAdmitted`, `Lending.Housing.UvrTranchesNotSupported`) |
| `GET /credit-lines/{id}` | `housing` | En una línea de vivienda: `{ date, rateCap: { rate, validFrom, source, nationalValuePublicId } \| null, limits: [{ code, value, source, nationalValuePublicId }], missing: [string] }`, el tope y los cinco límites de la Ley 546 vigentes a `date`; lo que falta va en `missing` sin romper la consulta |
| `POST /applications`, `PUT /applications/{id}` (entrada) · `CreditApplicationDto` | `isSocialHousing` (bool?) | Sólo en una solicitud de vivienda: VIS elige `VIVIENDA_FINANCIABLE_VIS`; nulo en las demás |
| `POST /simulations/schedule` → `ScheduleSimulationDto` | `isHousing` · `uvr` | `isHousing`: la línea sigue la Ley 546 (sella `LND-2026.2`). `uvr`, en una línea en UVR: `{ uvrDate, uvrValue, amountUvr, toPayUvr, installments: [{ number, dueDate, principal, interest, insurance, total, balanceAfter }], legend }`; las cuotas de `installments` (en pesos) son su equivalencia a esa UVR |
| `GET /loans/{id}` → `LoanTermsDto` | `denominationUnit` (`Pesos` \| `Uvr`) | La unidad sellada |
| `GET /loans/{id}/schedule` | `uvrValue` · `installments[].uvr` | La UVR con que se valoró el plan y la cuota en UVR `{ openingPrincipal, principal, interest, insurance, total, closingPrincipal }` |
| `GET /loans/{id}/transactions` | `uvrAmount` · `uvrValue` | El movimiento en UVR y la UVR de su fecha valor; la clase nueva `MonetaryCorrection` es la corrección monetaria diaria |
| `GET /loans/{id}/settlement` · `GET /loans/{id}/statement` | `uvr` | `{ uvrDate, uvrValue, projected, lastPublished, principalUvr, toPayOffUvr, balancesUvr, legend }`: la UVR del día (a una fecha sin UVR publicada, **proyectada** desde la última, sólo para mostrar, con aviso en `warnings`) y los saldos en UVR; los montos en pesos de la respuesta son su valor a esa UVR |

Errores (todos 422 con su dato y fuente): `Lending.Housing.VariableRateNotAllowed`, `.RateCapExceeded` (con el tope del día y su
fuente), `.TermOutOfLimits`, `.UvrNotAdmitted`, `.FinancingExceeded`, `.InstallmentToIncomeExceeded`, `.DataRequired`,
`.FirstRankMortgageRequired` (desembolso) y `NationalValues.Missing` (sin tope, límite o UVR del día). Antes de K6b siguen
`Lending.CreditLine.HousingNotAvailable` y `Lending.Migration.HousingNotAvailable`.
