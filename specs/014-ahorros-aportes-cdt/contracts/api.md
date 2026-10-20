# Contratos HTTP: captaciones y aportes de la Cartera financiera

**Feature**: 014 | **Fecha**: 2026-10-14 | **Entregas**: K7 (aportes) · K8 (ahorro a la vista y permanente) · K9 (CDAT
y contractual) · K10 (asamblea, inactividad, fallecimiento) · K11 (liquidez e informes) | **Fuente de nombres**:
`research.md` §0 (manda) y R01–R31 | **Spec**: FR-001–FR-128, Interfaces, Clarifications 2026-10-14 | **Molde**:
`specs/013-cartera-financiera/contracts/api.md` (en adelante, **«la 013»**) | **Hermanos**: `contrato-de-cuentas.md`
(puertos en proceso), `mensajes.md` (bandeja), `contabilidad.md` (matriz), `plantillas-y-archivos.md`, `documentos.md`

## Estado al cierre (2026-10-19, T225–T233)

Este contrato se **cotejó con el código** de la rama `014-ahorros-aportes-cdt` al cierre local de la feature. Manda el
código; después `research.md` §5–§25 (enmiendas E01–E225 y las decisiones del dueño de §13); después este texto. Con
`CatalogoDeCartera.EntregaVigente = K11` todas las rutas de K7 a K11 están disponibles (la columna E dice desde qué
entrega existe cada una). La 014 suma **180 rutas REST**:

| Dónde | Rutas |
|---|---|
| `/api/lending` en `Endpoints/Lending/Deposits/` (17 archivos; incluye las dos de plantilla e importación de las partes de la migración, `MapPlantillaPorRuta`) | 139 |
| `/api/reports/lending/{vista}` (`MapVistaDeCartera`): 16 vistas y 7 vistas previas de formatos regulatorios (§18.1) | 23 |
| `/api/lending/approval-policies` (§1.6, D53) | 2 |
| `/api/lending/payroll-agreements/{id}/file-groups` · `/concept-equivalences` (GET y POST) y `/payroll-deduction-files/{id}/unmatched` (§18.3) | 5 |
| `/api/lending/test-doubles/collector/accounts/*` y `/collector/treasury/accounts/*`, sólo fuera de producción (§18.4) | 9 |
| `GET /api/payroll/runs/{runId}/account-deductions` (§18.5) | 1 |
| `GET /api/core/people/{id}/co-held-accounts` (§18.5, D20) | 1 |

Cómo se contaron: las líneas `.Map(Get|Post|Put|Delete|Patch)(` que la rama agrega sobre `09ca91f6` (el cierre de la 013)
en `src/Presentation/IngenIA365ERP.API/Endpoints/` son 157, de las cuales dos son rutas de la 013 a las que sólo se les sumó
un filtro (`GET /api/lending/documents` con `accountPublicId`, `GET /api/lending/regulatory/files` con `format`); a las 155
restantes se suman las dos rutas de `MapPlantillaPorRuta` y las 23 de `MapVistaDeCartera`, que ese conteo no ve. Sin los
dobles de prueba son **171**. Se retiraron las **74** rutas heredadas de §19: la variación neta es **+106**.

Secciones que cambiaron en el cotejo (cada cambio cita su enmienda): §1 (moneda, 202 de la atribución), §2 (40 permisos
nuevos, 163 en total), §3 (sin `rate-quote` ni plantillas de líneas; cuerpos de versión), §4 (sin `installments` ni
`negotiated-rate` por cuenta; nacen `GET /{id}/contractual` y el detalle de bloqueos; cuerpos y códigos), §5 (nacen
`POST /{id}/fund` y `POST /{id}/titles`; la redención es a la fecha de hoy), §6 (cuerpo de la decisión), §7 (nacen
`GET /assignments` y los dos `reject`), §8 (nace `GET /{id}/responses/draft`; los avisos van en un 201), §11 (cuerpos),
§12 (nace `POST /{id}/void`; estados y cuerpo de `apply`), §13 (nace `POST /{id}/void`; rutas de entrega), §14 (estados,
orígenes y beneficiario), §15 (nace `GET /risk`; soporte obligatorio y alcance total), §16 (activación sin carga), §17 (el
lote responde 200 y admite extractos), §18.1 (dos vistas previas más), §18.2 (siete formatos), §18.3 (equivalencias),
§18.5 (rutas nuevas fuera de la Cartera), §21 (conteo) y §22 (códigos).

Este contrato cubre **sólo HTTP**: lo que llama una pantalla de la Cartera para captaciones y aportes. El contrato
de cuentas (`IContratoDeCuentas`), la consulta de aportes y los consumidores de mensajes **no tienen ruta** (§20).
Las captaciones **son** la Cartera: todo lo de la 013 §1 (convenciones) y §2 (permisos) rige sin repetirse; aquí se
fijan sólo las diferencias y las rutas nuevas.

Resumen de lo que rige:

- Prefijo **`/api/lending`**; informes en `/api/reports/lending/{vista}`. Ninguna ruta nueva vive en `/api/savings`
  ni en `/api/cdt`: esas 70 rutas (más 4 de informes) **se retiran sin alias** (§19).
- Todo identificador que cruza es **`PublicId`** (`{id:guid}`); el número de cuenta y el de título son texto para
  mostrar y buscar (`?number=`).
- Toda escritura lleva **`Idempotency-Key`** (`IOperacionIdempotente`, la 013 §1.5); las consultas que usan POST
  por el tamaño del cuerpo (simulaciones, vistas previas, cotizaciones) van **sin** clave y se marcan «sin clave».
- Sobre `{ code, message, traceId, data? }`; sin permiso o fuera de alcance, **el mismo 404** que lo inexistente
  (`Lending.Savings.NotFound`, `Lending.TermDeposit.NotFound`…); regla de negocio, 422 con `data`; forma, 400.
- **`Read`** = trae datos del titular; **`View`** = catálogos, conteos y agregados sin persona (convención de la 013).

---

## 1. Convenciones propias de captaciones

### 1.1 Grupos y archivos

Un archivo por área en `src/Presentation/IngenIA365ERP.API/Endpoints/Lending/Deposits/` (research §0.1), todos con
`LendingGroup.Grupo(app, "/…", "Cartera …")`: `RequireAuthorization()`, `ErrorEnvelopeFilter`,
`.AuditarIntentoDenegado(ModuloDeAuditoria.Lending)` y `.RequirePermission(...)` en **cada** ruta
(`LosEndpointsProtegidosExigenPermiso` ya entra por glob a `Endpoints/Lending/**`). Archivos: `LinesEndpoints.cs`,
`MemberAccountsEndpoints.cs`, `TermDepositsEndpoints.cs`, `NegotiatedRatesEndpoints.cs`, `ContributionsEndpoints.cs`,
`GarnishmentsEndpoints.cs`, `DirectDebitsEndpoints.cs`, `GmfExemptionsEndpoints.cs`, `MembershipWithdrawalsEndpoints.cs`,
`AssemblyDistributionsEndpoints.cs`, `DeceasedCasesEndpoints.cs`, `UnclaimedEndpoints.cs`, `LiquidityFundEndpoints.cs`,
`DepositsMigrationEndpoints.cs`, `DepositDocumentsEndpoints.cs` y `RegulatoryEndpoints.cs` (17 archivos). Los dobles de
prueba de captaciones viven en el `Endpoints/Lending/TestDoublesEndpoints.cs` de la 013 (sólo fuera de producción) y la
política de aprobación de movimientos en `Endpoints/Lending/AttributionsEndpoints.cs`. Los bloqueos de cuentas
(`/accounts/{id}/holds`) se declaran en `GarnishmentsEndpoints.cs`. Los endpoints **sólo reenvían** al `ISender`.

### 1.2 Alcance

El alcance de la Cartera (`IAlcanceDeCartera`: oficinas y **líneas de cualquier naturaleza**, FR-004) rige en toda
ruta de este contrato y falla cerrado. Una cuenta de una oficina o una línea fuera del alcance es **inexistente**
(404 `Lending.Savings.NotFound`, `Lending.TermDeposit.NotFound`), con el evento `Lending.Access.OutOfScope`. Las
listas filtran en silencio. `LND_UserCreditLineScopes` sirve igual: la línea es común (R01).

### 1.3 Antes de activar y por naturaleza

- **Activación por naturaleza** (FR-116, R21). Aportes y captaciones tienen cada una su fecha en `LND_Setup`
  (`ContributionsActivationDate`, `DepositsActivationDate`). Antes de activar una naturaleza sólo se puede:
  parametrizar sus líneas (§3), migrar y activar (§16), consultar el registro de plantillas. Cualquier otra
  escritura sobre cuentas de esa naturaleza responde 422 **`Lending.Contributions.NotActivated`** o
  **`Lending.Savings.NotActivated`**, y las lecturas devuelven listas vacías.
- **Captación exige créditos activados** (R21): activar aportes o captaciones antes que los créditos responde
  `Lending.Activation.LoansFirst`.
- **Sin K8 no hay captación** (spec, entregas): una línea de captación se crea pero no se activa hasta que el
  despliegue tenga K8; hasta entonces 422 `Lending.Savings.DeliveryNotAvailable` (`data: { nature, availableFrom }`;
  enmiendas E31, E38).
- **Por entrega**: la columna E de cada tabla es la entrega en que la ruta nace. **Enmienda E31/E127**: las rutas existen
  desde que se despliegan y lo que pide una entrega no desplegada responde 422 `Lending.Savings.DeliveryNotAvailable` con
  su `availableFrom` (no un 404); al cierre `EntregaVigente = K11`. El catálogo de permisos nace **entero en K7** (§2).

### 1.4 Las dos fechas de una liquidación

Como en la 013 §1.8: en `/accounts/{id}/balance`, `/settlements/simulate`, `/close-preview`,
`/term-deposits/{id}/redeem/simulate` y `/contributions/summary`, `date` es la fecha D (pasada, hoy o futura) y
**`recordedAsOf`** el instante R hasta el que se toma lo registrado. La respuesta devuelve los dos, la huella
(`fingerprint`) y la **explicación día por día** (FR-011): `[{ date, balance, rateTier, dailyRate, interest,
belowMinimum, valueDateAdjustment? }]`.

**Cotejo con el código (2026-10-19)**: reciben `recordedAsOf` sólo `GET /accounts/{id}/balance`,
`POST /accounts/{id}/settlements/simulate` y `GET /lines/{id}`; `/close-preview` y `/term-deposits/{id}/redeem/simulate`
reciben sólo `date`, y `/contributions/summary` sólo `date` (sin `recordedAsOf`, toma todo lo registrado, como el saldo
sin `recordedAsOf`, E124).

### 1.5 Decimales y enums

- Montos `Monto` (18,2); tasas como **fracción** (`TasaAnual` 9,6: `0.100000` es 10 % EA); tasas diarias y
  equivalentes en la explicación con hasta 18 decimales; la causación diaria guarda 6 decimales (R05) y sale así en
  la explicación. Moneda sólo `COP`: ningún cuerpo lleva moneda (el código `Lending.Currency.NotSupported` no nació).
- Enums de research §0.3 por nombre o número (`EnumPorNombreONumero`), salida como número; estados derivados por
  nombre.

### 1.6 Atribuciones con monto en captaciones (FR-051, FR-071, FR-079, FR-127)

- **Atribuciones operativas con monto** (`SEC_PermissionAmountLimits`, la 013 §2.4) que suma esta feature
  **(nuevo en contracts)**: `Lending.Accounts.Withdraw` (valor del retiro), `Lending.Accounts.Deposit` (valor de una
  consignación **en efectivo**, para el control de efectivo; FR-059), `Lending.TermDeposits.Redeem` (capital
  redimido antes del vencimiento) y `Lending.MembershipWithdrawals.Settle` (valor de la devolución). El límite
  efectivo es el mayor de los roles activos del usuario.
- **Superarla no es error**: la operación queda **en espera de un aprobador presente** con segundo factor (passkey o
  TOTP, la mecánica de aprobaciones de la plataforma 012) y **no mueve saldo** hasta aprobarse (US4-5). La respuesta
  es 202 con `{ status: PendingApproval, approvalPublicId, required: { permissionCode, amount, maxAmount } }` y
  `Location: /api/inventory/approvals/{approvalPublicId}` (la plataforma de aprobaciones); el reintento con el mismo
  `Idempotency-Key` y `approvalPublicId` aprobado aplica (los cuerpos de retiro, traslado, fondeo y redención llevan
  `approvalPublicId?`). Por el contrato de cuentas la misma situación es un rechazo `Lending.Savings.PendingApproval`
  (enmienda E80).
- **Niveles configurables por cooperativa** (decisión del dueño del 2026-10-18, D53): la aprobación de `AccountWithdrawal` y
  `AccountCashDeposit` sigue la **política** que la cooperativa registre en la plataforma de aprobaciones —niveles en orden,
  cada uno con su umbral y el permiso de quien lo aprueba— por `GET`/`POST /api/lending/approval-policies`
  (`Lending.Attributions.View`/`.Manage`, sólo esos dos sujetos: otro responde `Lending.ApprovalPolicy.SubjectNotAllowed`) y la
  pantalla `/cartera/captaciones/aprobaciones`; sin política rige la regla fija de un nivel (el mismo permiso con un monto que
  alcance). Si la política no exige aprobación para el monto, el movimiento procede. `Lending.Accounts.Withdraw` y
  `.Deposit` entran a los limitables de Cartera (`/api/lending/amount-limits`, `/cartera/atribuciones`);
  `TermDeposits.Redeem` y `MembershipWithdrawals.Settle` entran con su historia.
- **Matriz de atribuciones de la Cartera** con sujetos nuevos de `LendingDecisionSubjects` (textos, no hay enum;
  research C06): `DepositWithdrawal`, `NegotiatedRate`, `MembershipWithdrawal`, `DeceasedDelivery`, `ChargeWaiver`,
  `TermDepositRedemption`, `ContributionRefund`, `AssemblyDistribution`, `GarnishmentRegistration`. Las rutas de
  decisión (aprobar una tasa negociada, una devolución, una entrega) responden el 404 del recurso a quien no tiene el
  nivel que la matriz pide (la 013 §1.3).

### 1.7 Segregación (FR-005)

Pares fijos (comparación por persona, `ReglasDeSegregacion`, nunca el entero del token): quien **registra** un
embargo no lo **levanta**; quien **abre** con excepción no **aprueba** la apertura; quien **radica** un retiro de
asociado no lo **aprueba**; quien **registra** una entrega por fallecimiento, una devolución de aportes o una decisión
de asamblea no la **aprueba**; quien **pide** una tasa negociada no la **aprueba**. Violarlo: 422
`Lending.Segregation.Conflict` (la 013 §1.14).

### 1.8 Errores comunes de captaciones (además de la 013 §1.14)

| Código | HTTP | Cuándo | `data` |
|---|---|---|---|
| `Lending.Savings.NotFound` · `Lending.TermDeposit.NotFound` | 404 | cuenta o CDAT inexistente, sin permiso o fuera de alcance | |
| `Lending.Savings.NotActivated` · `Lending.Contributions.NotActivated` | 422 | la naturaleza no está activada (§1.3) | `{ nature }` |
| `Lending.Savings.LineNotAllowedByProfile` | 422 | el perfil regulatorio vigente no autoriza la clase (US1-1, FR-018) | `{ depositClass, profilePublicId, allowedClasses[] }` |
| `Lending.Savings.MembersOnly` | 422 | la línea o el perfil exigen asociado vigente (FR-030) | `{ personPublicId, associateStatus, howToFix: "Vincular primero" }` |
| `Lending.Savings.InsufficientAvailable` | 422 | la operación supera el disponible (FR-057) | `{ balance, holds, garnishments, pendingClearance, minimumBalance, available, requested }` |
| `Lending.Savings.SignaturesMissing` | 422 | manejo conjunto o firmante con límite sin las firmas (US4-1) | `{ handlingMode, required[], provided[] }` |
| `Lending.Savings.AccountNotOperable` | 422 | estado que no admite la operación (`Closed`, `Closing`, bloqueo total, fallecido) | `{ status, holds[] }` |
| `Lending.Savings.CashLimitExceeded` | 422 | efectivo sobre el máximo de la línea | `{ maxCash, requested }` |
| `Lending.Savings.SourceOfFundsRequired` | 422 | efectivo sobre el umbral sin declaración de origen de fondos (FR-059) | `{ threshold, amount }` |
| `Lending.Savings.Overdrawn` | aviso | reverso de una consignación ya usada deja sobregiro técnico; la cuenta queda bloqueada (FR-056) | `{ negativeBalance, holdPublicId }` |
| `Lending.Contributions.MinimumBreached` | 422 | la devolución perforaría el mínimo no reducible (FR-042) | `{ minimum, balance, requested }` |
| `Lending.Contributions.ParticipationLimitExceeded` | 422 | aporte que supera el límite individual (FR-043) | `{ limit, current, requested, source }` |
| `Lending.Gmf.AlreadyMarked` | 422 | ya hay cuenta exenta vigente para la persona (FR-036) | `{ accountPublicId, accountNumber, since }` |
| `Lending.Savings.PendingApproval` | 202 (no es error) | atribución superada (§1.6) | `{ approvalPublicId, required }` |

---

## 2. Permisos (catálogo `Lending.*`, research §0.7)

Los siembra la API al arrancar (`LendingPermissionCatalogSeeder`, nunca el DbMigrator), **enteros desde K7**,
también los de K8 a K11. Se suman a los 123 de la 013: **40 códigos nuevos**, total **163** (E12; el 40.º es
`Lending.MembershipWithdrawals.ReverseAfterPayment`, D44; lo fija `PermisosDeCarteraTests`).

| Código | Qué habilita | E |
|---|---|---|
| `Lending.DepositLines.View` · `.Manage` | ver líneas de captación, versiones, tramos, cargos y reglas de tasa negociada · crearlas, versionarlas, cerrarlas, reabrirlas e importarlas | K8 |
| `Lending.ContributionLines.View` · `.Manage` | ídem para la línea de aportes | K7 |
| `Lending.Accounts.View` | conteos, saldos agregados y calendario de vencimientos sin titular | K7 |
| `Lending.Accounts.Read` | ver cuentas, saldos, disponible, libro, operaciones, extracto en pantalla, cuotas y condiciones (datos del titular; lectura sensible auditada) | K7 |
| `Lending.Accounts.Open` · `.Close` | abrir cuentas (y aprobar una apertura excepcional, con segregación) · cancelarlas | K7 |
| `Lending.Accounts.Deposit` | atribución con monto de la consignación en efectivo por el contrato (§1.6) | K7 |
| `Lending.Accounts.Withdraw` | retiro por orden de giro desde la pantalla; atribución con monto del retiro por el contrato (§1.6) | K8 |
| `Lending.Accounts.Transfer` | traslado entre cuentas desde la pantalla | K7 |
| `Lending.Accounts.Reverse` | reversar desde la Cartera una operación de cuenta (no de Nómina ni de otro recaudador salvo la consignación, FR-092) | K7 |
| `Lending.Accounts.Parties.Manage` | titulares, cotitulares, firmantes, representantes, beneficiarios; actualización de datos por mayoría de edad; instrucción al vencimiento del contractual | K7 |
| `Lending.TermDeposits.Constitute` · `.Renew` · `.Redeem` | simular y constituir un CDAT · renovarlo o cambiar su instrucción · redimirlo (atribución con monto) | K9 |
| `Lending.NegotiatedRates.Request` · `.Approve` | pedir una tasa negociada · decidirla según la matriz de atribuciones | K9 |
| `Lending.Contributions.Manage` | devoluciones sin retiro, cesiones y su aprobación (segregación) | K7 |
| `Lending.Holds.Manage` | bloqueos de cuentas | K8 |
| `Lending.Garnishments.Register` · `.Release` | registrar embargos, respuestas y consignaciones al juzgado · desembargar | K8 |
| `Lending.DirectDebits.Manage` | autorizaciones de débito automático | K8 |
| `Lending.GmfExemptions.Manage` | marcar y desmarcar la cuenta exenta de GMF | K8 |
| `Lending.Charges.Manage` · `.Waive` | registrar y reversar cargos · condonarlos y exonerar una cuenta | K8 |
| `Lending.Interest.Settle` | liquidar a mano un período de intereses | K8 |
| `Lending.MembershipWithdrawals.File` · `.Approve` · `.Settle` | radicar y desistir · aprobar o negar con acta, aprobar la devolución · liquidar el retiro (atribución con monto) | K7 |
| `Lending.MembershipWithdrawals.ReverseAfterPayment` | **(decisión del dueño del 2026-10-18, D44)** reversar un pago del cruce al retiro con el retiro ya pagado: reabre la cuenta con el saldo a favor del exasociado | K7 |
| `Lending.Assembly.Register` · `.Approve` | registrar y simular revalorización, excedentes y amortización · aprobarla y aplicarla, reversarla | K10 |
| `Lending.DeceasedCases.Register` · `.Approve` | registrar el fallecimiento y la entrega · aprobar la entrega | K10 |
| `Lending.Unclaimed.Manage` | trasladar a no reclamados y pagarlos | K10 |
| `Lending.LiquidityFund.View` · `.Manage` | ver base, requerido y constituido · registrar constituido e instantáneas | K11 |
| `Lending.Deposits.Migration` | cargas de migración de captaciones y aportes, cifras de referencia | K7 |
| `Lending.Deposits.Activate` | ver la preparación y activar aportes o captaciones | K7 |

Se **reutilizan** de la 013: `Lending.Migration.AcceptDifferences` (aceptar diferencias con acta),
`Lending.Reconciliation.View`, `Lending.Documents.*` (documentos de §17), `Lending.Reports.View` · `.Export` ·
`.ExportPersonalData`, `Lending.RegulatoryReports.Generate`, `Lending.RegulatoryFiles.Read` · `.MarkUploaded`,
`Lending.Messages.*`, `Lending.Processes.*`, `Lending.Sessions.*`, `Lending.PayrollAgreements.*`,
`Lending.PayrollDeductionFiles.*`, `Lending.TestDoubles.Operate`, `Lending.Templates.View`, `Lending.Scope.*`.

**Plantillas de rol** (la 013 §2.5) que se suman **(nuevo en contracts)**: `cartera.jefe-captaciones`,
`cartera.asesor-captaciones`, `cartera.analista-aportes`, `cartera.tesorero-liquidez`. `cartera.contador`,
`cartera.auditor` y `cartera.revisor-fiscal` reciben los `Read`/`View` nuevos por su glob; `cartera.administrador`
excluye `NegotiatedRates.Approve` (administrar no es aprobar tasa) y, por **decisión del dueño del 2026-10-18** (research §13 D17),
también el rol integrado `CompanyAdmin` (`LendingPermissionCatalogSeeder.ExcluidosDelAdministrador`). Las cuatro plantillas de
captaciones las exige sólo la activación de aportes o captaciones (`DepositRolesCreated`, D18); la de créditos, sólo las trece suyas.

**Cómo leer las tablas**: `Ruta` relativa al prefijo de la sección · `Permiso` sin `Lending.` · `Comando / consulta`
· `Cuerpo → respuesta` resumidos · `Errores` propios · `E` entrega.

---

## 3. Líneas — `/api/lending/lines` (`LinesEndpoints.cs`; FR-027–FR-029, FR-127, FR-128; R01, R13, R14)

La identidad común es `LND_CreditLines` con `Nature` (`LineNature`: `Placement` 1, `Deposit` 2, `Contribution` 3).
`/api/lending/credit-lines` (la 013 §3.1) sigue para colocación y **lista sólo `Placement`**. Las versiones son
`LND_DepositLineVersions` (con `LND_DepositLineRateTiers` y `LND_DepositLineCharges`) y
`LND_ContributionLineVersions`, inmutables con vigencia bitemporal y sin retroactividad (FR-007). El código es
`CodigoDeCatalogo` único **en todo el catálogo** (de cualquier naturaleza, US1-4).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?nature=&status=&depositClass=&includeClosed=` | `AlgunPermiso(CreditLines.View, DepositLines.View, ContributionLines.View)` | `ListLendingLinesQuery` **(nuevo en contracts)** | → arreglo `LendingLineListItemDto { publicId, code, name, nature, depositClass?, status, closedReason?, currentVersion { publicId, validFrom, summary } }`, sólo las naturalezas cuyo `View` tiene quien pregunta y del alcance | `Lending.Report.FilterInvalid` | K7 |
| `GET /{id}?date=&recordedAsOf=` | ídem | `GetLendingLineQuery` | → identidad + versión vigente en (`date`, `recordedAsOf`) según la naturaleza + historial de versiones (bitemporal, enmienda E42) | `Lending.Line.NotFound` | K7 |
| `POST /deposit` | DepositLines.Manage | `CreateDepositLineCommand` | `{ code, name, depositClass, active, firstVersion: DepositLineVersionInput }` → 201; sin `active` la línea queda en borrador (`Draft`, enmienda E38) | `Catalogo.CodigoDuplicado`, `Lending.Savings.LineNotAllowedByProfile`, `Lending.Savings.DeliveryNotAvailable` (`data: { nature, availableFrom }`), `Lending.DepositLine.Invalid` (`data.errors[{ field, code, message }]`), `Lending.Savings.RateTiersOverlap`, `Lending.TermDeposit.LineIncomplete`, `Lending.DepositLine.ReferencedLineInvalid`, `Lending.Savings.RateChangeNoticeTooShort`, `Lending.Parameter.BelowLegalFloor` (enmienda E36) | K8 |
| `POST /contribution` | ContributionLines.Manage | `CreateContributionLineCommand` | `{ code, name, active, firstVersion: ContributionLineVersionInput }` → 201; una cooperativa tiene a lo sumo una línea de aportes activa | `Catalogo.CodigoDuplicado`, `Lending.Contributions.LineAlreadyActive`, `Lending.ContributionLine.Invalid`, `Lending.Contributions.FractionBelowLegal`, `Lending.Contributions.InstallmentCapExceeded` (US1-3: `data: { capPercent, contributionsShareMin, source }`) (enmienda E36) | K7 |
| `GET /{id}/deposit-versions` | DepositLines.View | `ListLendingLineVersionsQuery` (con `nature = Deposit`) | → arreglo `{ publicId, validFrom, validTo?, registeredAt, registeredBy, reason }` | | K8 |
| `GET /{id}/deposit-versions/{versionId}` | DepositLines.View | `GetDepositLineVersionQuery` | → `DepositLineVersionDto` (= el input + `publicId`, `registeredAt`) | `Lending.DepositLine.VersionNotFound` | K8 |
| `POST /{id}/deposit-versions` | DepositLines.Manage | `AddDepositLineVersionCommand` | `DepositLineVersionInput` con `validFrom` ≥ hoy y `reason` → 201; cierra la anterior la víspera; un cambio de tasa de una línea a la vista **programa el aviso** a los titulares con la anticipación del reglamento (FR-038) | `Lending.DepositLine.VersionOverlaps`, `.Retroactive` (`validFrom` < hoy), `.ClassImmutable` (la clase no cambia), los del alta | K8 |
| `GET /{id}/contribution-versions` · `GET /{id}/contribution-versions/{versionId}` · `POST /{id}/contribution-versions` | ContributionLines.View · .View · .Manage | `ListLendingLineVersionsQuery` (con `nature = Contribution`) · `GetContributionLineVersionQuery` · `AddContributionLineVersionCommand` | como las de captación, con `ContributionLineVersionInput` | `Lending.ContributionLine.VersionNotFound`, `.VersionOverlaps`, `.Retroactive`, `Lending.Contributions.InstallmentCapExceeded` | K7 |
| `POST /{id}/close` · `POST /{id}/reopen` | `AlgunPermiso(DepositLines.Manage, ContributionLines.Manage)` + el `Manage` de su naturaleza (si falta, 422 `Lending.Permission.Required`) | `CloseLendingLineCommand` · `ReopenLendingLineCommand` | `{ reason }` → la línea; cerrar no afecta las cuentas vivas; reabrir revalida el perfil y **también activa una línea en borrador** con las mismas validaciones del alta activa (enmienda E38) | `Lending.Line.UseCreditLinesRoute` (colocación), `Lending.Savings.LineNotAllowedByProfile`, `Lending.Savings.DeliveryNotAvailable`, `Lending.Contributions.LineAlreadyActive` | K7 |
| `GET /{id}/negotiated-rate-rules?date=` | DepositLines.View | `ListNegotiatedRateRulesQuery` | → arreglo `NegotiatedRateRuleDto { publicId, criteria { minSeniorityMonths?, amountFrom?, amountTo?, termDaysFrom?, termDaysTo?, baseRateFrom?, baseRateTo?, holderBalanceFrom?, officePublicIds?[], channels?[] }, maxMarginEa, keepsMarginOnRollover, validFrom, validTo? }` | | K9 |
| `POST /{id}/negotiated-rate-rules` | DepositLines.Manage | `AddNegotiatedRateRuleCommand` | `{ criteria, maxMarginEa, keepsMarginOnRollover, validFrom, reason }` → 201; las reglas se versionan por código (enmienda E133) | `Lending.NegotiatedRate.RuleOverlaps`, `.LineDoesNotAllow`, `.Retroactive`, `.MarginInvalid` | K9 |
| `POST /{id}/negotiated-rate-rules/{ruleId}/end` | DepositLines.Manage | `EndNegotiatedRateRuleCommand` | `{ validTo, reason }` → la regla (terminar = versión inactiva, enmienda E133) | `Lending.NegotiatedRate.RuleNotFound` | K9 |
| `GET /{id}/charges?date=` | DepositLines.View | `ListDepositLineChargesQuery` | → los cargos de la versión vigente en `date` (`kind`, `trigger`, cálculo, valor, gratuitos, exoneraciones, gravado; enmienda E83) | | K8 |

**No construidas** (enmiendas E42, E115): la cotización `POST /{id}/rate-quote` (la tasa de tabla la dan la simulación del
CDAT, `POST /term-deposits/simulate`, y la apertura) y las plantillas `lending.deposit-lines` / `lending.contribution-lines`
(`/lines/{deposit|contribution}/template.xlsx` · `/import`): las líneas se crean por pantalla (`/cartera/lineas`) o por
`POST /deposit` · `/contribution` (pregunta abierta al dueño, research §16.1). Total de §3: **16 rutas**.

`DepositLineVersionInput` (sella cada versión; nombres de bloque de `data-model.md`; **al día con el código, enmiendas E42,
E83**: la tasa es `Fixed` o `Variable` y los tramos van dentro de `rate`; la retención la nombra
`tax.withholdingRateParameterCode` —vacío = la tarifa de la clase—; los cargos llevan `trigger`, no `event`):

```json
{
  "validFrom": "2027-01-01", "reason": "Tasa aprobada por el consejo, acta 12", "depositClass": "Demand",
  "officePublicIds": null, "requiresMembership": true, "allowsCoHolders": true, "allowsMinors": true,
  "allowsLegalEntities": false, "allowsPartialWithdrawals": true,
  "rate": { "kind": "Fixed", "effectiveAnnual": null, "variable": null, "tierMode": "WholeBalance",
            "tiers": [{ "balanceFrom": 0, "balanceTo": 999999.99, "termDaysFrom": null, "termDaysTo": null, "amountFrom": null, "amountTo": null, "effectiveAnnual": 0.010000 },
                      { "balanceFrom": 1000000, "balanceTo": null, "effectiveAnnual": 0.030000 }] },
  "declaresAgreedRate": false, "negotiatedRateAllowed": false, "keepsNegotiatedMarginOnRollover": false, "rateChangeNoticeDays": 30,
  "balanceMethod": "DailyBalance",
  "minimums": { "toOpen": 10000, "toMaintain": 10000, "toEarnInterest": 100000 },
  "dayCount": "Actual365", "rateEquivalence": "Compound",
  "rounding": { "unit": 1, "mode": "AwayFromZero", "moment": "PerTotal" },
  "settlementPeriodicity": "Monthly", "settlementDay": null,
  "tax": { "withholdingRateParameterCode": null, "gmfApplies": true },
  "limits": { "maxWithdrawalPerOperation": 20000000, "maxWithdrawalPerDay": 30000000, "maxCashPerOperation": 10000000, "maxCashPerDay": null },
  "inactivity": { "days": 360, "keepsAccruing": true, "suspendsOnTotalGarnishment": false, "suspendsOnDeceased": false },
  "termDeposit": null,
  "contractual": null,
  "permanent": null,
  "charges": [{ "code": "RETADIC", "name": "Retiro adicional", "kind": "ExtraOperation", "trigger": "WithdrawalOverFree", "enabled": true,
                "calculation": "FixedAmount", "fixedAmount": 2000, "percent": null, "percentBase": null, "min": null, "max": null,
                "freePerPeriod": 4, "freePeriod": "Monthly", "taxedWithVat": true, "commercialItemCode": null, "uncollectedPolicy": "KeepPending",
                "exemptions": { "averageBalanceAtLeast": 5000000, "minors": true } }]
}
```

Bloque `termDeposit` (sólo `depositClass = TermDeposit`, FR-028): `{ minTermDays, maxTermDays, interestPeriodicities[],
timings[]: Arrears|Advance, rolloverAutomatic, maxRollovers, rolloverRateRule: SameRate|CurrentTableRate|Agreed,
rolloverCapitalizes, graceBusinessDays, graceAccrues, afterMaturity: StopAccruing|MoveToDemand, demandLinePublicId?,
earlyRedemption: { allowed, penaltyMode: PenaltyRate|Discount, penaltyRateEa?, penaltyDiscount?, minDaysBeforeRedemption? } }`
(la tasa negociada se declara en la versión, no en el bloque). Bloque `contractual` (FR-074): `{ installmentPeriodicity,
minInstallment, termMonthsFrom, termMonthsTo, bonusRateEa, breachRateEa, toleranceInstallments, breachEffect:
BreachRate|MoveToDemand|Cancel, bonusRecognition: AtFulfillment|Provisioned }` (enmiendas E42, E142). Bloque `permanent`:
`{ withdrawableOnlyOnMembershipWithdrawal }` (E211).

`ContributionLineVersionInput` (FR-029; **enmienda E42**): `{ validFrom, reason, minimumNonReducible: { kind, amount?,
fraction? }, installment: { kind: FixedAmount|IncomeFraction|MinimumWageFraction, amount?, fraction?, roundingUnit,
roundingMode, periodicity, dueDay?, minimumFirst }, permanentSavingsShare?: { depositLinePublicId, fraction },
allowsExtraordinary, maxParticipationFraction?, excessAction: Reject|RefundByDecision, allowsTransferBetweenMembers,
refundTermDays, lossDeductionEnabled }`. Un sobrepago sin extraordinarios admitidos se rechaza
(`Lending.Contributions.OverpaymentNotAllowed`, E56).

- **Perfil regulatorio** (FR-018, US1-1): una línea de captación activa sólo si el perfil vigente autoriza su clase;
  la de aportes, siempre.
- **Pisos legales** (FR-014): un valor menos estricto que el parámetro con fuente (tope de la cuota en fondos de
  empleados, `TOPE_CUOTA_FONDO_EMPLEADOS_PCT`, `FRACCION_MINIMA_APORTES`) responde
  `Lending.Parameter.BelowLegalFloor` o `Lending.Contributions.InstallmentCapExceeded`.
- **Sin usura** (R01, `LasCaptacionesNoUsanLaUsura`): ninguna ruta de esta sección llama a `ControlDeUsura`.

---

## 4. Cuentas — `/api/lending/accounts` (`MemberAccountsEndpoints.cs`; FR-030–FR-039, FR-050–FR-066, FR-128)

Una cuenta es `LND_MemberAccounts` (`MemberAccount`) de naturaleza `Deposit` (clases `Demand`, `Contractual`,
`Permanent`; el CDAT tiene su sección, §5) o `Contribution`. **El dinero entra y sale por el contrato de cuentas**
(`contrato-de-cuentas.md`): estas rutas no consignan efectivo; sí ordenan retiros por giro y traslados internos.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?search=&nature=&depositClass=&line=&office=&status=&personPublicId=&number=&page=` | Accounts.Read | `ListMemberAccountsQuery` | → lista `MemberAccountListItemDto { publicId, number, nature, depositClass?, line { publicId, code, name }, office, holder { personPublicId, fullName, documentNumber }, status, balance, available, marks[] }` (de la posición diaria vigente) | | K7 |
| `GET /counts?nature=&line=&office=&status=` | Accounts.View | `GetMemberAccountCountsQuery` | → `{ byStatus: [{ status, accounts, balance }], byLine[] }` sin titulares | | K7 |
| `GET /{id}` | Accounts.Read | `GetMemberAccountQuery` | → `MemberAccountDto` (abajo); lectura auditada `Lending.Savings.BalanceViewed` (E124) | `Lending.Savings.NotFound` | K7 |
| `GET /{id}/balance?date=&recordedAsOf=` | Accounts.Read | `GetMemberAccountBalanceQuery` | → `{ date, recordedAsOf, fingerprint, byConcept {…}, available { balance, holds, garnishments, pendingClearance, minimumBalance, available }, accruedNotSettled, explanation[] }`; auditado `Lending.Savings.BalanceViewed` | `NationalValues.Missing` | K7 |
| `POST /` | Accounts.Open | `OpenMemberAccountCommand` | `OpenAccountInput` (abajo) → 201 `MemberAccountDto` en `Active`, o `Opening` si hay coincidencia en listas o la versión exige depósito de apertura (`openingBlockers`: `ScreeningMatch`, `InitialDeposit`; enmienda E45). Se abre con la fecha de hoy. Un CDAT no se abre por aquí (`Lending.Savings.UseTermDepositRoute`, E52); un contractual exige `contractual { installment, termMonths, maturityInstruction? }` (E143) | `Lending.Savings.MembersOnly`, `.MemberDataIncomplete` (`data.missing`, E44), `.LineNotAllowedByProfile`, `.LineNotOpen`, `.LineWithoutVersion`, `.OfficeNotAllowedByLine`, `.HolderDeceased`, `.PermanentAlreadyOpen`, `.OpeningDateInvalid`, `.UseTermDepositRoute`, `.SourceOfFundsRequired`, `Lending.Savings.NotActivated`, `Lending.Contributions.NotActivated`, `Lending.Contributions.AccountAlreadyOpen` (una cuenta de aportes viva por persona), `Lending.Contributions.OnlyHolderAndBeneficiaries`, `Lending.Savings.BeneficiariesMustSum100` (`data: { sum }`), `.BeneficiaryRecordRequired` (`data.field = beneficiaryRecordAttachmentPublicId`; con beneficiarios, la constancia también al abrir: D34), `.CoHoldersNotAllowed`, `.MinorsNotAllowed`, `.LegalEntitiesNotAllowed`, `.JointRequiresTwoSigners`, `.PartyRepeated`, `.PartyInvalid`, `.RepresentativeRequired` (`data: { supportRequired }`) (E47), `Lending.Contractual.TermsRequired`, `.TermsNotAllowed`, `.InstallmentBelowMinimum`, `.TermOutOfRange`, `.NoInstallments` (E143), `Lending.MembershipWithdrawal.AlreadyInProgress` (E75), `Lending.Gmf.AlreadyMarked`, `Lending.Authorization.Missing` (finalidad `GeneralTreatment` del titular y de cada cotitular, `data.personPublicId`, E44), `Lending.Security.UserWithoutPerson`, `Lending.Adapter.Unavailable` (canal de listas caído), `Lending.Screening.Blocked` (aviso: queda `Opening` hasta que el oficial libere, US2-2) | K7 |
| `POST /{id}/approve-opening` | Accounts.Open | `ApproveMemberAccountOpeningCommand` | `{ reason }` → `MemberAccountDto` en `Active` (o sigue `Opening` hasta el depósito inicial; un CDAT no se activa por aquí, E130) | `Lending.Segregation.Conflict`, `Lending.Savings.NotOpening`, `Lending.Screening.Blocked` (quedan bloqueos de listas sin levantar, E45), `Lending.Security.UserWithoutPerson` | K7 |
| `GET /{id}/close-preview?date=` | Accounts.Read | `PreviewMemberAccountClosingQuery` | → `{ settlement { interest, withholding, net }, gmf, charges, toDeliver, blockers: [{ kind, detail }], contractualPenalty?, contractualExplanation? }` (sin clave; en un contractual vigente, el costo de incumplirlo, E149); lectura auditada `Lending.Savings.BalanceViewed` (E124) | | K8 |
| `POST /{id}/close` | Accounts.Close | `CloseMemberAccountCommand` | `{ date?, delivery: { kind: TransferToAccount \| DisbursementOrder, targetAccountPublicId?, payee? }, signatures?[], reason }` → la operación `Cancellation` (`CAN-`) con la puesta al día y la liquidación a la fecha, y la entrega en su **propia** operación; con giro la cuenta queda `Closing` hasta que Tesorería informa el giro (E99). Sólo ahorro a la vista y contractual (un contractual vigente se incumple y se reliquida al castigo, E149); aportes y permanente se cancelan con el retiro del asociado; el CDAT por su ruta. El cierre en efectivo por el contrato (`RetirarAsync` con `closeAccount`) **no está construido** (E78, E99) | `Lending.Savings.CloseBlocked` (`data: { blockers[] }`: embargo, bloqueo, canje, sobregiro técnico, titular fallecido y cargos por cobrar, E99), `.CloseDateInvalid` (sólo hoy), `Lending.Contributions.ClosureOnlyByWithdrawal`, `Lending.Contractual.MaturityPending`, `Lending.Savings.UseTermDepositRoute`, `Lending.Savings.SignaturesMissing`, `Lending.Prevalidation.NotPostable` | K8 |
| `GET /{id}/operations?kind=&from=&to=&page=` | Accounts.Read | `ListMemberAccountOperationsQuery` | → lista `MemberAccountOperationDto { publicId, number, kind, valueDate, accountingDate, registeredAt, amount, collector? { code, class }, meansClass?, payer?, receiptReference?, reversedBy?, reverses?, actor, origin: Person\|Process }` | | K7 |
| `GET /{id}/operations/{operationId}` | Accounts.Read | `GetMemberAccountOperationQuery` | → el comprobante: la operación + `transactions[]` (concepto, valor, tres fechas) + `messages: [{ type, messagePublicId, status }]` | `Lending.Savings.OperationNotFound` | K7 |
| `GET /{id}/transactions?from=&to=&concept=&dateKind=value\|accounting\|registered&page=` | Accounts.Read | `ListMemberAccountTransactionsQuery` | → lista `{ kind (MemberAccountTransactionKind), concept, amount, valueDate, accountingDate, registeredAt, operationPublicId, reversesTransaction?, reversedBy?, countsAsHolderActivity, messagePublicId? }` (sólo lectura) | | K7 |
| `POST /{id}/operations/simulate` (sin clave) | Accounts.Read | `SimulateMemberAccountOperationQuery` | `{ kind: Withdrawal\|Transfer\|Close, amount, targetAccountPublicId?, meansClass?, signatures[] }` → `{ allowed, available {…}, gmf { base, exemptUsed, exemptRemaining, amount }, charges[], signatures { required[], missing[] }, attribution { requiresApproval, permissionCode?, maxAmount? }, warnings[] }`; el destino también en el alcance (E123) | | K7 |
| `POST /{id}/operations/withdrawals` | Accounts.Withdraw | `WithdrawToDisbursementOrderCommand` | `{ amount, valueDate?, payee: { personPublicId, bankPublicId?, accountType?, accountNumber? }, signatures[], reason?, approvalPublicId? }` → 201 `{ operation, order { publicId, number, status } }` o 202 `PendingApproval` (§1.6). Emite `RetiroDeCuentaAplicado` + `GmfRetenido` + `OrdenDeGiroEmitida` v2 (`DepositWithdrawal`) | `Lending.Savings.InsufficientAvailable`, `.SignaturesMissing`, `.AccountNotOperable`, `Lending.Savings.WithdrawalLimitExceeded` (`data: { perOperation, perDay, usedToday }`), `.ValueDateOutOfRange`, `Lending.Contributions.NotWithdrawable`, `Lending.Contractual.WithdrawalNotAllowed` (E144), `Lending.Prevalidation.NotPostable` | K8 |
| `POST /{id}/operations/transfers` | Accounts.Transfer | `TransferBetweenMemberAccountsCommand` | `{ targetAccountPublicId, amount, valueDate?, signatures[], reason?, targetModality?, approvalPublicId? }` → 201 la operación (una sola, con las dos cuentas en orden de `Id`, R08) o 202 `PendingApproval`; ahorro ↔ CDAT sigue `Gmf.HechosGeneradores` (FR-052); el destino tiene que estar en el alcance de quien opera (fuera, el 404 de la cuenta, E123); un traslado a un contractual paga cuotas (E144) | los de `withdrawals`, `Lending.Savings.TargetNotOperable`, `.SameAccount`, `Lending.Contributions.ParticipationLimitExceeded` (destino aportes) | K7 |
| `POST /{id}/operations/{operationId}/reverse` | Accounts.Reverse | `ReverseMemberAccountOperationCommand` | `{ reason }` → la operación de reverso; arrastra GMF, retención, cargos y cruces enlazados (FR-002); una consignación de un recaudador emite `ConsignacionReversadaPorCartera` (FR-092). Desde la pantalla nunca queda sobregiro (E85) | `Lending.Savings.ReversalOfReversal`, `.ReversalOwnedByCollector` (retiros y traslados de un recaudador, y todo lo de Nómina, se reversan por el contrato), `.ReversalOwnedByProcess` (cruce del retiro, débito automático, devoluciones, cargos, liquidación, cancelación, asamblea, consignación al juzgado: se corrigen desde su origen; E85, E98, E163, E186), `.ReversalWouldOverdraw` (US4-6: `data: { blockingOperation { publicId, number } }`). **`ReversalWindowClosed` no se construyó** (no hay parámetro de ventana, E85) | K7 |
| `GET /{id}/parties?date=` | Accounts.Read | `GetMemberAccountPartiesQuery` | → `{ handlingMode, parties: [{ publicId, person, role (MemberAccountPartyRole), share?, signatureLimit?, supportAttachmentPublicId?, validFrom, validTo? }], beneficiaries: [{ person, percent }], requiresDataUpdate }` | | K7 |
| `PUT /{id}/parties` | Accounts.Parties.Manage | `SetMemberAccountPartiesCommand` | `{ handlingMode, parties[], beneficiaries[], beneficiaryRecordAttachmentPublicId?, evidenceAttachmentPublicId?, reason }` → el conjunto completo: la parte igual sigue, lo demás se cierra la víspera y nace hoy; los beneficiarios cambian en conjunto (FR-034, E47) | `Lending.Savings.BeneficiariesMustSum100`, `.BeneficiaryRecordRequired`, `.CoHoldersNotAllowed`, `.RepresentativeRequired`, `.JointRequiresTwoSigners`, `.PartyRepeated`, `.PartyInvalid`, `Lending.Authorization.Missing` | K7 |
| `POST /{id}/data-update` | Accounts.Parties.Manage | `ConfirmMemberAccountDataUpdateCommand` | `{ reason }` → levanta la marca «actualizar datos» tras la mayoría de edad (FR-033), exige conocimiento del asociado y autorización del titular vigentes | `Lending.Savings.DataUpdateNotRequired`, `Lending.Authorization.Missing` | K7 (enmienda E49: es la salida de la marca de la mayoría de edad) |
| `PUT /{id}/maturity-instruction` | Accounts.Parties.Manage | `SetContractualMaturityInstructionCommand` | `{ kind: TransferToAccount\|DisbursementOrder, targetAccountPublicId?, payee?, reason }` (sólo un contractual que no haya terminado; se guarda en `MaturityInstructionJson`, la sellada al abrir es hecho; E151) | `Lending.Savings.NotContractual`, `Lending.Contractual.NotCurrent`, `.DestinationInvalid` | K9 |
| `GET /{id}/contractual` | Accounts.Read | `GetContractualQuery` | → el contrato del ahorro contractual: condiciones selladas, plan de cuotas, cumplimiento, proyección e instrucción al vencimiento (**nueva**, enmienda E151); lectura auditada | `Lending.Savings.NotContractual` | K9 |
| `GET /{id}/holds?includeReleased=` | Accounts.Read | `ListAccountHoldsQuery` | → `{ publicId, reason (HoldReason), amount?, total, loanPublicId?, since, until?, releasedAt?, releasedBy?, support? }` | | K8 |
| `POST /{id}/holds` | Holds.Manage | `PlaceAccountHoldCommand` | `{ reason (HoldReason), amount?, validTo?, loanPublicId? (garantía, FR-054), supportAttachmentPublicId?, notes }` → 201; por esta vía sólo `Internal`, `LoanCollateral`, `SignaturePending` y `Other` (enmienda E161) | `Lending.Holds.OwnedByProcess` (los demás motivos), `Lending.Holds.InsufficientAvailable` (uno por valor no supera el disponible), `Lending.Holds.Invalid` (la garantía exige un crédito vivo) | K8 |
| `POST /{id}/holds/{holdId}/release` | Holds.Manage | `ReleaseAccountHoldCommand` | `{ reason }` → 204 | `Lending.Holds.NotFound`, `Lending.Holds.OwnedByProcess` (`Deceased`, `ScreeningMatch`, `Garnishment`: se levantan desde su origen), `Lending.Holds.LoanStillActive` (garantía con crédito vivo) (enmienda E161) | K8 |
| `GET /{id}/charges?status=&page=` | Accounts.Read | `ListAccountChargesQuery` | → `AccountCharge { publicId, kind, code, amount, vat?, status: Collected\|Pending\|Waived\|Reversed, causedAt, invoice?: { number, status } }` (el estado de la factura lo informa Comercial, FR-128) | | K8 |
| `POST /{id}/charges` | Charges.Manage | `RegisterAccountChargeCommand` | `{ chargeCode, eventReference?, reason }` → 201 (cargos por evento de pantalla: reposición de medio, certificado adicional) | `Lending.Savings.ChargeNotInLine`, `.ChargeExempt` | K8 |
| `POST /{id}/charges/{chargeId}/waive` | Charges.Waive | `WaiveAccountChargeCommand` | `{ reason }` → el cargo `Waived`; si estaba cobrado, reintegro | `Lending.Savings.ChargeNotWaivable`, `Lending.Segregation.Conflict` | K8 |
| `POST /{id}/charges/{chargeId}/reverse` | Charges.Manage | `ReverseAccountChargeCommand` | `{ reason }` → el reverso de la operación del cargo; si estaba gravado emite `CargoDeCaptacionReversado` | `Lending.Savings.ReversalOfReversal`, `Lending.Savings.ChargeCollectedLater` (su deuda ya la cobró una consignación posterior: se condona, E83), `.ChargeNotFound` | K8 |
| `POST /{id}/charges/exemption` | Charges.Waive | `SetAccountChargeExemptionCommand` | `{ chargeCodes[], validFrom, validTo?, reason }` → exoneración individual aprobada (FR-128) | `Lending.Segregation.Conflict` | K8 |
| `GET /{id}/settlements?page=` | Accounts.Read | `ListInterestSettlementsQuery` | → `InterestSettlement { publicId, periodFrom, periodTo, accrued, settled, roundingAdjustment, withholding, net, settledAt, origin }` | | K8 |
| `POST /{id}/settlements/simulate` (sin clave) | Accounts.Read | `SimulateInterestSettlementQuery` | `{ date, recordedAsOf? }` → `{ periodFrom, periodTo, interest, withholding { base, inflationaryComponent, rate, threshold, thresholdReachedOn[], amount }, net, explanation[] }` (FR-064) | | K8 |
| `POST /{id}/settlements` | Interest.Settle | `SettleAccountInterestCommand` | `{ periodTo, reason }` → 201 la liquidación nueva; **idempotente por cuenta y período**: repetirla responde 200 con la existente y `alreadyExisted` (SC-003, E103) | `Lending.Savings.SettlementPeriodOpen` (período no vencido o ya liquidado), `.AccrualBehind` (`data: { lastAccruedDate }`), `.AccountDoesNotAccrue` (E98) | K8 |
| `GET /{id}/statement?from=&to=` | Accounts.Read | `GetMemberAccountStatementQuery` | → extracto en pantalla (no es documento, §17): saldo inicial, una línea por operación por fecha valor (lo causado sin operación, una por mes), intereses abonados, retención, GMF, saldo final y el disponible de hoy; a lo sumo 366 días (E124); auditado `Lending.Savings.StatementViewed` | | K7 |
| `GET /{id}/terms?version=` | Accounts.Read | `GetMemberAccountTermsQuery` | → `MemberAccountTermsVersion` sellada (CDAT, contractual, tasa negociada) y su historia | | K9 |

**No construidas** (cotejo con el código): `GET /{id}/installments` —las cuotas se listan en `GET /contributions/installments`
(§7) y el plan del contractual en `GET /{id}/contractual`— y `POST /{id}/negotiated-rate` —la tasa negociada se pide al
constituir o renovar el CDAT (`negotiation` / `requestedMarginEa`, §5) y se decide en §6—. Las de bloqueos se declaran en
`GarnishmentsEndpoints.cs`. Total de §4: **33 rutas** (18 en `MemberAccountsEndpoints.cs`, 12 en
`MemberAccountOperationsEndpoints.cs` y 3 de bloqueos).

`OpenAccountInput`:

```json
{
  "linePublicId": "…", "holderPersonPublicId": "…", "officePublicId": "…",
  "handlingMode": "Indistinct",
  "parties": [{ "personPublicId": "…", "role": "CoHolder", "share": 0.500000, "signatureLimit": null, "supportAttachmentPublicId": null, "validFrom": "2027-01-04" }],
  "beneficiaries": [{ "personPublicId": "…", "percent": 60.00 }, { "personPublicId": "…", "percent": 40.00 }],
  "beneficiaryRecordAttachmentPublicId": "…",
  "markGmfExempt": true,
  "contractual": null,
  "sourceOfFunds": { "declarationAttachmentPublicId": null },
  "openingDate": "2027-01-04"
}
```

`MemberAccountDto` (suma `openingBlockers[]`, E45; `contractual` con `installment`, `periodicity`, `installments`,
`paidInstallments`, `arrearsInstallments`, `status`, `maturityDate`, E151): `{ publicId, number, nature, depositClass?, status, line { publicId, code, name }, office, holder,
parties[], beneficiaries[], handlingMode, gmfExempt { since? }, balances { byConcept: { Principal, InterestPayable,
Withholding, Gmf, ContributionMinimum, ContributionOrdinary, ContributionExtraordinary, ContributionRevaluation,
ChargeReceivable, Bonus }, available {…} }, lastAccruedDate, lastSettledThrough, terms?, contractual?: { installment,
paidInstallments, arrearsInstallments, status: OnTrack\|Breached\|Completed }, marks: { requiresDataUpdate, inactive,
garnished, deceasedHolder, overdrawnTechnical }, holds[], directDebits[] }`.

- **Número** (FR-019, FR-035): consecutivo por línea (`LND_NumberSequences`, clase por línea, R08) asignado al final de
  la transacción, con el código de la línea y un guion como prefijo (`AHVISTA-000001`, E46); dos aperturas concurrentes
  reciben números consecutivos sin hueco (US2-5).
- **Menores** (FR-033): con titular menor, el representante con soporte es obligatorio y es el único firmante.
- **Lecturas sensibles** auditadas por `LendingAuditEmitter` (lista propia `LecturasSensiblesDeCaptaciones`, E124): saldo,
  detalle, lista de cuentas de una persona, vista previa de la cancelación y resumen de aportes
  (`Lending.Savings.BalanceViewed`), extracto (`Lending.Savings.StatementViewed`), libro y uso del GMF de una persona
  (`Lending.Savings.TransactionsViewed`), embargos (`Lending.Garnishment.Viewed`, E164).
- **Alcance** (E123): la **otra** cuenta de una operación (destino de un traslado, de su simulación o de la entrega de una
  cancelación) también tiene que estar en el alcance; fuera, el mismo 404 con `Lending.Access.OutOfScope`.

---

## 5. CDAT — `/api/lending/term-deposits` (`TermDepositsEndpoints.cs`; FR-067–FR-073, FR-127; R11)

Un CDAT es una `MemberAccount` de clase `TermDeposit` con sus `LND_MemberAccountTermsVersions` selladas y su plan
`LND_TermDepositSchedules` por versión. Prórroga y renovación son **la misma cuenta** con versión nueva y **título
nuevo** (número con sufijo de versión, R11).

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&line=&office=&maturityFrom=&maturityTo=&issueTermBand=&personPublicId=&negotiated=&page=` | Accounts.Read | `ListTermDepositsQuery` | → lista `{ publicId, number, titleNumber, holder, line, amount, termDays, effectiveAnnual, negotiatedMargin?, issueTermBand, openedOn, maturityDate, status, instruction, rolloverCount }` | | K9 |
| `GET /maturity-calendar?from=&to=&office=` | Accounts.View | `GetTermDepositMaturityCalendarQuery` | → por día y plazo de emisión: cantidad y capital que vence (sin titulares) | | K9 |
| `GET /{id}` | Accounts.Read | `GetTermDepositQuery` | → `TermDepositDto { account, currentTerms { version, amount, termDays, effectiveAnnual, tableRate, negotiated?: { marginEa, rulePublicId, approvedBy, reason }, interestPeriodicity, timing, paymentDestination, maturityInstruction, graceUntil?, issueTermBand, rulesVersion }, termsHistory[], titles[], status }` | `Lending.TermDeposit.NotFound` | K9 |
| `GET /{id}/schedule?version=` | Accounts.Read | `GetTermDepositScheduleQuery` | → `[{ number, from, to, days, rate, interest, withholdingEstimate, net, paidOn?, operationPublicId? }]` | | K9 |
| `POST /simulate` (sin clave) | TermDeposits.Constitute | `SimulateTermDepositQuery` | `{ linePublicId, amount, termDays, interestPeriodicity, timing, startDate, requestedMarginEa?, holderPersonPublicId? }` → `{ tableRate, rate, schedule[], totalInterest, withholdingEstimate { appliesThreshold, amount }, maturityDate (siguiente hábil, D19), issueTermBand, negotiation?: { allowed, maxMarginEa, withinUserAttribution } }` | `Lending.TermDeposit.TermOutOfRange` (`data: { minTermDays, maxTermDays }`), `.AmountOutOfRange`, `.NoRateForTerm`, `.PeriodicityNotAllowed` | K9 |
| `POST /` | TermDeposits.Constitute | `ConstituteTermDepositCommand` | `ConstituteTermDepositInput` (abajo) → 201 `TermDepositDto` en `Opening` con su propuesta en `PendingTermsJson`: la versión 1 y el plan se **sellan al fondear** (E130), por el recaudador (consignación exacta) o por `POST /{id}/fund`; con tasa negociada fuera de la atribución, `Opening` + solicitud `PendingDecision` (US6-6) | los de `simulate`, `Lending.TermDeposit.StartDateInvalid`, `.FundingInvalid`, `Lending.Savings.MembersOnly`, `Lending.NegotiatedRate.MarginAboveRule`, `Lending.Savings.BeneficiaryRecordRequired`, `Lending.Authorization.Missing` | K9 |
| `POST /{id}/fund` | TermDeposits.Constitute | `FundTermDepositCommand` | `{ sourceAccountPublicId, signers?[], approvalPublicId?, reason }` → 201 la operación de constitución por traslado desde una cuenta de ahorro viva del titular (con su GMF) o 202 `PendingApproval` (**nueva**, enmienda E130) | `Lending.TermDeposit.AlreadyFunded`, `.PendingNegotiation`, `.FundingInvalid`, `Lending.Savings.InsufficientAvailable`, `.SignaturesMissing` | K9 |
| `PUT /{id}/maturity-instruction` | TermDeposits.Renew | `SetTermDepositMaturityInstructionCommand` | `{ instruction: Rollover\|RenewWithNewTerms\|CancelAndPay, paymentDestination?: { kind: Account\|PaymentOrder, accountPublicId?, payeePersonPublicId?, bankPublicId?, accountType?, accountNumber? }, reason }` → hasta el fin de la gracia, en `MaturityInstructionJson` (la sellada es hecho; E136) | `Lending.TermDeposit.GraceExpired`, `.DestinationInvalid` | K9 |
| `POST /{id}/renew` | TermDeposits.Renew | `RenewTermDepositCommand` | `{ termDays, interestPeriodicity, timing, reason, amountChange?: { kind: Increase\|Decrease, amount, funding?\|destination? }, requestedMarginEa?, maturityInstruction?, paymentDestination? }` → versión nueva, plan nuevo, título nuevo enlazado; sólo entre el vencimiento y el fin de la gracia; paga antes lo pendiente; retención antes de capitalizar (FR-063) | `Lending.TermDeposit.NotInRenewalWindow`, `.Garnished` (FR-072), `.NegotiationRequiresApproval` (margen sólo dentro de la atribución, E136), `.NoRolloverRate`, los de `simulate` | K9 |
| `POST /{id}/redeem/simulate` (sin clave) | TermDeposits.Redeem | `SimulateTermDepositRedemptionQuery` | `{ date }` → `{ principal, accruedInterest, penalty { kind, repricedRate?, amount }, withholding, gmf, net, explanation[] }` | `Lending.TermDeposit.EarlyRedemptionNotAllowed` (US6-4) | K9 |
| `POST /{id}/redeem` | TermDeposits.Redeem | `RedeemTermDepositCommand` | `{ destination: { kind: Account\|PaymentOrder, targetAccountPublicId?, payeePersonPublicId?, bankPublicId?, accountType?, accountNumber? }, expectedPreviewSha256, reason, approvalPublicId? }` → **a la fecha de hoy** (sin `date`, E141): 201 la operación, o 202 `PendingApproval` (atribución con monto, FR-071); en la gracia es cancelación sin penalidad; con giro la cuenta queda `Closing` (E136) | `Lending.TermDeposit.EarlyRedemptionNotAllowed`, `.Garnished`, `.DeceasedHolder`, `.DestinationInvalid`, `Lending.Savings.PreviewChanged` (`data: { preview }`) | K9 |
| `GET /{id}/titles` | Accounts.Read | `ListTermDepositTitlesQuery` | → los documentos `TermDepositTitle` de la cadena (`documentos.md`) con la versión que titula y el estado calculado (E173) | | K9 |
| `POST /{id}/titles` | **Documents.Issue** | `ReissueTermDepositTitleCommand` | → 201 el título vigente; emite el título pendiente (sin firmante o con el almacén caído al sellar, o un CDAT migrado) y devuelve el vigente si ya existe (**nueva**, enmienda E173; declarada en `DepositDocumentsEndpoints.cs`) | `Lending.Document.SignerMissing` | K9 |

`ConstituteTermDepositInput`:

```json
{
  "linePublicId": "…", "holderPersonPublicId": "…", "officePublicId": "…", "handlingMode": "Indistinct",
  "parties": [], "beneficiaries": [{ "personPublicId": "…", "percent": 100.00 }],
  "amount": 10000000.00, "termDays": 180, "interestPeriodicity": "Monthly", "timing": "Arrears", "startDate": "2027-01-04",
  "paymentDestination": { "kind": "Account", "accountPublicId": "…" },
  "maturityInstruction": "Rollover",
  "funding": { "kind": "Collector", "sourceAccountPublicId": null, "signers": [], "approvalPublicId": null },
  "beneficiaryRecordAttachmentPublicId": "…", "channel": null,
  "negotiation": { "requestedMarginEa": 0.012000, "reason": "Asociado con cinco años y 200 millones en captaciones" },
  "expectedPreviewSha256": "a91c…"
}
```

`funding.kind`: `Collector` (Recaudo o Tesorería consignan **exactamente** `amount` por `ConsignarAsync` mientras está
`Opening`; otro valor: `Lending.TermDeposit.FundingAmountMismatch`; un CDAT ya constituido no recibe consignaciones:
`Lending.TermDeposit.NoAdditionalDeposits`, E130) o `TransferFromAccount` (`sourceAccountPublicId`, con GMF según
`Gmf.HechosGeneradores`). `paymentDestination.kind` es `Account` o `PaymentOrder` (giro a un beneficiario, E131). La prórroga automática, el pago de intereses en cada fecha del plan, la
reclasificación por plazo de emisión y el «vencido por pagar» los hace el ciclo diario (R10), sin ruta.

---

## 6. Tasa negociada — `/api/lending/negotiated-rate-requests` (FR-127, R13, D34)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&line=&page=` | `AlgunPermiso(NegotiatedRates.Request, NegotiatedRates.Approve)` | `ListNegotiatedRateRequestsQuery` | → lista `{ publicId, account { publicId, number, kind }, holder, amount, termDays?, tableRate, requestedMarginEa, maxMarginEa, rule, requestedBy, status: PendingDecision\|Approved\|Rejected\|Withdrawn\|Expired, routing { level, kind: Individual\|Session } }`; quien no tiene el nivel no ve las de otros | | K9 |
| `GET /{id}` | ídem | `GetNegotiatedRateRequestQuery` | → la solicitud con criterios evaluados y costo frente a la tabla | `Lending.NegotiatedRate.NotFound` | K9 |
| `POST /{id}/approve` | NegotiatedRates.Approve | `DecideNegotiatedRateCommand` | `{ reason, approvalPublicId? (aprobador presente con segundo factor), sessionItemPublicId? (decisión de un órgano con punto de acta cerrado, asunto `NegotiatedRate`) }` (la ruta dice la decisión: no hay campo `decision`) → sella tabla, margen, regla, aprobador y motivo en `MemberAccountTermsVersion`; si el CDAT esperaba, pasa a esperar el fondeo (E133) | `Lending.NegotiatedRate.NotFound` (el mismo 404 a quien no tiene el nivel: `LevelRequired` no nació), `.MarginAboveRule`, `.AlreadyDecided`, `Lending.TermDeposit.AlreadyFunded`, `Lending.Segregation.Conflict`, `Lending.Session.ItemMismatch` | K9 |
| `POST /{id}/reject` | NegotiatedRates.Approve | `DecideNegotiatedRateCommand` | `{ reason, approvalPublicId?, sessionItemPublicId? }` → el CDAT queda con la tasa de la tabla si el titular la acepta, o se desiste | ídem | K9 |
| `POST /{id}/withdraw` | NegotiatedRates.Request | `WithdrawNegotiatedRateRequestCommand` | `{ reason }` | `Lending.NegotiatedRate.AlreadyDecided`, `.OnlyRequester` (sólo quien la pidió la retira) | K9 |

La solicitud nace al constituir o renovar un CDAT con un margen fuera de la atribución de quien lo pide (§5); no hay ruta
para pedirla sobre una cuenta a la vista o contractual (cotejo con el código).

---

## 7. Aportes — `/api/lending/contributions` (FR-040–FR-049, FR-094; R20)

La cuota la genera el ciclo diario (FR-040) y la pagan los recaudadores por el contrato (`ListarExigiblesAsync`,
`ConsignarAsync`); revalorización y excedentes van en §12, el retiro del asociado en §11.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /summary?personPublicId=&date=` | Accounts.Read | `GetContributionsSummaryQuery` | → `{ person, date, byClass { Minimum, Ordinary, Extraordinary, Revalued }, minimumNonReducible, permanentSavings, installmentsInArrears: [{ period, dueDate, pending }], asCollateralOf: [{ loanPublicId, number }] }`; **mismo saldo** que `IConsultaDeAportes` v2 y que el extracto (SC-010); exige el alcance sobre **todas** las cuentas de la persona (E123) y se audita `Lending.Savings.BalanceViewed` (E124). No recibe `recordedAsOf` (cotejo) | | K7 |
| `GET /installments?personPublicId=&status=&period=&line=&page=&pageSize=` | Accounts.Read | `ListAccountInstallmentsQuery` | → cuotas de aportes, permanente y contractual `{ publicId, period, dueDate, amount, paid, pending, status: Pending\|PartiallyPaid\|Paid\|NotDeducted, notDeductedReason? }` | | K7 |
| `GET /refunds?status=&page=&pageSize=` | Accounts.Read | `ListContributionRequestsQuery` (clase `Refund`) | → devoluciones sin retiro (exceso de participación, FR-043), estado `PendingApproval\|Approved\|Rejected` (`LND_ContributionRequests`, E58) | | K7 |
| `POST /refunds` | Contributions.Manage | `RequestContributionRefundCommand` | `{ accountPublicId, amount, cause: ParticipationExcess\|Other, sessionItemPublicId?, reason }` → 201 `PendingApproval` | `Lending.Contributions.MinimumBreached`, `.NoParticipationExcess` (E58), `.HasLiveLoans` (FR-044: `data: { loans[] }`), `.EntityMinimumBreached` (FR-080: la devolución queda pendiente y avisa al gerente) | K7 |
| `POST /refunds/{refundId}/approve` | Contributions.Manage | `ApproveContributionRefundCommand` | `{ form: DisbursementOrder\|TransferToAccount, payee?, targetAccountPublicId?, reason }` → la operación y la orden | `Lending.Segregation.Conflict`, `Lending.Contributions.RequestAlreadyDecided`, los del registro | K7 |
| `POST /refunds/{refundId}/reject` | Contributions.Manage | `RejectContributionRequestCommand` | `{ reason }` → la solicitud `Rejected` (**nueva**, enmienda E58) | `Lending.Contributions.RequestNotFound`, `.RequestAlreadyDecided`, `Lending.Segregation.Conflict` | K7 |
| `GET /assignments?status=&page=&pageSize=` | Accounts.Read | `ListContributionRequestsQuery` (clase `Assignment`) | → cesiones (**nueva**, cotejo con el código) | | K7 |
| `POST /assignments` | Contributions.Manage | `RequestContributionAssignmentCommand` | `{ fromAccountPublicId, toPersonPublicId, amount, sessionItemPublicId?, reason }` → 201 `PendingApproval` (FR-048, sólo si la versión de la línea lo admite: `allowsTransferBetweenMembers`); la cuenta que recibe también en el alcance (E123) | `Lending.Contributions.AssignmentNotAllowed`, `.MinimumBreached`, `.HasLiveLoans`, `.AccountNotFound` | K7 |
| `POST /assignments/{assignmentId}/approve` | Contributions.Manage | `ApproveContributionAssignmentCommand` | `{ reason }` → la operación (traslado entre cuentas de aportes) | `Lending.Segregation.Conflict` | K7 |
| `POST /assignments/{assignmentId}/reject` | Contributions.Manage | `RejectContributionRequestCommand` | `{ reason }` → la cesión `Rejected` (**nueva**, enmienda E58) | `Lending.Contributions.RequestAlreadyDecided`, `Lending.Segregation.Conflict` | K7 |

`/api/lending/contribution-balances` (la 013 §7.4) queda **sólo** como insumo de la migración: desde la activación de
aportes deja de leerse en la consulta (FR-094) y su importación responde `Lending.Contributions.UseMigration`.

---

## 8. Embargos — `/api/lending/garnishments` (FR-055, FR-065, FR-072; R23)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&status=&page=&pageSize=` | Accounts.Read | `ListGarnishmentsQuery` | → `Garnishment { publicId, person, authority (Court\|TaxAuthority\|OtherPublicEntity), courtPersonPublicId, processNumber, officeReference, officeDate, receivedOn, claimantName, amount, isAlimony, includesContributions, status: Active\|Released\|Rejected (por número: 2, 3, 4; `Registered` no se usa), retained: [{ accountPublicId, number, amount }], consigned, unembargableLimit, responseDueOn, responseOverdue, responses[] }` (enmiendas E157, E161); auditado `Lending.Garnishment.Viewed` (E164) | | K8 |
| `GET /{id}` | Accounts.Read | `GetGarnishmentQuery` | → el embargo con `explanation[]` del límite inembargable (`LimiteInembargable`) y `warnings[]` (`Lending.Garnishment.ResponseOverdue`, informativo); exige el alcance sobre todas las cuentas de la persona (E164) | `Lending.Garnishment.NotFound` | K8 |
| `GET /{id}/responses/draft?kind=` | Accounts.Read | `GetGarnishmentResponseDraftQuery` | → el texto de la respuesta (la plantilla de la entidad o la estándar, `Embargos.PlantillasDeRespuesta`, llena con `{Oficio}`, `{Proceso}`, `{Embargado}`, `{Retenido}`, `{Cuentas}`…); lectura auditada (**nueva**, enmienda E158) | | K8 |
| `POST /` | Garnishments.Register | `RegisterGarnishmentCommand` | `{ personPublicId, authority, courtPersonPublicId, courtCode?, processNumber, officeReference, officeDate, receivedOn?, claimantName, claimantTaxId?, claimantPersonPublicId?, amount?, isAlimony, accountsScope: AllSavings\|Specific, accountPublicIds?, includesContributions, destinationBankPublicId?, destinationAccountNumber?, supportAttachmentPublicId, notes? }` → 201 **`Active`** (rige al registrarse, sin aprobación: CGP art. 593 num. 10, E157) con lo retenido por cuenta en el orden de `Embargos.OrdenDeCuentas`; pone el bloqueo de deudor `Garnishment` de la 013. **`ContributionsNotGarnishable` y `NothingGarnishable` son avisos en `warnings` del 201**, no errores (la respuesta al juzgado queda registrada en el mismo guardado; E161) | `Lending.Garnishment.Invalid`, `.AccountNotOfPerson` | K8 |
| `POST /{id}/responses` | Garnishments.Register | `RegisterGarnishmentResponseCommand` | `{ kind: Acknowledgement\|BalanceReport\|ContributionsNotGarnishable\|ConsignmentNotice\|ReleaseAcknowledgement, attachmentPublicId?, sentOn, text? }` → la respuesta, emitida como documento `GarnishmentResponse` (`issuedDocumentPublicId`, `issuedDocumentNumber`; sin firmante, el aviso `Lending.Garnishment.ResponseDocumentNotIssued`; E176) | | K8 |
| `POST /{id}/consign` | Garnishments.Register | `ConsignGarnishmentCommand` | `{ amount, payee?: { bankPublicId, accountNumber, accountType } (la cuenta de depósitos judiciales; si no viene, la del embargo), reason }` → una operación `GarnishmentConsignment` (`EMB-`) por cuenta, con `EmbargoConsignado` y su orden `OrdenDeGiroEmitida` v2 (`GarnishmentConsignment`, beneficiario el juzgado); sin GMF (E163) | `Lending.Garnishment.ExceedsRetained`, `.DestinationMissing`, `.NotActive`, `Lending.Prevalidation.NotPostable` | K8 |
| `POST /{id}/release` | Garnishments.Release | `ReleaseGarnishmentCommand` | `{ releaseOfficeReference, releaseOfficeDate?, supportAttachmentPublicId, amount?, reason }` → con `amount` menor que la medida la reduce (sigue activo); sin `amount`, la levanta toda y quita el bloqueo de deudor; conserva la historia (US11-3, E161) | `Lending.Segregation.Conflict` (quien registró no levanta), `Lending.Garnishment.AlreadyReleased`, `.NotActive`, `.PartialReleaseInvalid` | K8 |

---

## 9. Débito automático — `/api/lending/direct-debits` (FR-090, R16)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&accountPublicId=&loanPublicId=&status=` | Accounts.Read | `ListDirectDebitMandatesQuery` | → `DirectDebitMandate { publicId, account, scope: Loan\|AllHolderLoans, loan?, allowsPartial, validFrom, validTo?, status, lastDebits[] }` | | K8 |
| `POST /` | DirectDebits.Manage | `RegisterDirectDebitMandateCommand` | `{ accountPublicId, scope, loanPublicId?, allowsPartial, validFrom?, validTo?, authorizationAttachmentPublicId, authorizedByPersonPublicId? }` → 201 | `Lending.DirectDebit.NotHolder` (el titular de la cuenta no es deudor del crédito), `.AccountNotDemand`, `.Overlaps`, `.Retroactive` | K8 |
| `POST /{id}/revoke` | DirectDebits.Manage | `RevokeDirectDebitMandateCommand` | `{ validTo?, reason, evidenceAttachmentPublicId? }` | `Lending.DirectDebit.NotFound`, `.AlreadyRevoked` | K8 |

El débito lo hace el ciclo diario en la fecha exigible (sin ruta): retiro interno de la cuenta + pago al crédito con
`InternalOffset` en **una operación**, clave `CAR:debito-automatico:{loanPublicId:N}:{fecha}` (R16).

---

## 10. Exención de GMF — `/api/lending/gmf-exemptions` (FR-036, R07)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&active=` | Accounts.Read | `ListGmfExemptAccountsQuery` | → `{ publicId, person, account, validFrom, validTo?, markedBy, reason }` | | K7 (E51) |
| `GET /usage?personPublicId=&year=&month=` | Accounts.Read | `GetGmfMonthlyUsageQuery` | → `{ policy (GmfExemptionPolicy), capUvt, capAmount, usedExempt, remaining, taxed, gmfCharged }` (`LND_GmfMonthlyUsage`) | | K8 |
| `POST /` | GmfExemptions.Manage | `MarkGmfExemptAccountCommand` | `{ accountPublicId, validFrom?, reason, replaceCurrent }` → 201; `replaceCurrent` cambia la marca vigente en el mismo comando (E48); la marca que rige hoy reclasifica en el mismo comando (E159); con política `PerHolderLimit` o `NotApplicable` no hay marca | `Lending.Gmf.AlreadyMarked`, `.AccountNotSavings`, `.PolicyWithoutMark`, `.NotHolder`, `.Retroactive` (no se marca hacia atrás) | K7 (E48, E51) |
| `POST /{id}/end` | GmfExemptions.Manage | `EndGmfExemptAccountCommand` | `{ validTo?, reason }`; la cuenta nueva parte del acumulado del mes de la persona | `Lending.Gmf.NotFound`, `.AlreadyEnded`, `.Retroactive` | K7 (E48, E51) |

---

## 11. Retiro del asociado — `/api/lending/membership-withdrawals` (FR-076–FR-080, FR-124; R15)

Estados `MembershipWithdrawalStatus`: `Filed → Approved → Settled → Paid`, más `Desisted`, `Rejected`, `ToRedo`.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&cause=&personPublicId=&page=` | `AlgunPermiso(MembershipWithdrawals.File, .Approve, .Settle)` | `ListMembershipWithdrawalsQuery` | → lista `{ publicId, number, person, cause, filedOn, status, approval?: { sessionItemPublicId, on }, settlement?: { refund, crossed }, refundOrder? }` | | K7 |
| `GET /{id}` | ídem | `GetMembershipWithdrawalQuery` | → el trámite con `lines: [MembershipWithdrawalLine { kind: ContributionBalance\|SavingsBalance\|TermDepositBalance\|LossDeduction\|LoanOffset\|Refund, account?, loan?, concept?, amount, explanation }]` por versión de la liquidación (`Refund` es informativa; los CDAT vivos van como `TermDepositBalance` informativa; enmiendas E65, E137) | `Lending.MembershipWithdrawal.NotFound` | K7 |
| `POST /` | MembershipWithdrawals.File | `FileMembershipWithdrawalCommand` | `{ personPublicId, cause (MembershipWithdrawalCause), causeDetail?, requestedOn?, supportAttachmentPublicIds?, note }` → 201 `Filed` | `Lending.MembershipWithdrawal.AlreadyInProgress`, `Lending.MembershipWithdrawal.NotAssociate` | K7 |
| `POST /{id}/desist` | MembershipWithdrawals.File | `DesistMembershipWithdrawalCommand` | `{ reason }` → `Desisted` (sólo antes de aprobar) | `Lending.MembershipWithdrawal.NotFiled` | K7 |
| `POST /{id}/approve` | MembershipWithdrawals.Approve | `DecideMembershipWithdrawalCommand` | `{ sessionItemPublicId, effectiveDate?, reason }` (la ruta dice la decisión; punto de una sesión **cerrada** cuyo asunto es el trámite, E70) → `Approved`; emite **`RetiroDeAsociadoAprobado`** a Asociados en el mismo guardado | `Lending.Segregation.Conflict`, `Lending.Session.ItemMismatch`, `Lending.MembershipWithdrawal.EffectiveDateInvalid`, `.InvalidTransition` | K7 |
| `POST /{id}/reject` | MembershipWithdrawals.Approve | `DecideMembershipWithdrawalCommand` | `{ sessionItemPublicId, reason }` (resultado `Deny` o `Return` del punto) → `Rejected` | ídem | K7 |
| `POST /{id}/settlement/preview` (sin clave) | MembershipWithdrawals.Settle | `PreviewMembershipWithdrawalSettlementQuery` | `{ date?, lossDeduction?: { sessionItemPublicId, year, proportion } }` (la proporción la da el acta, enmienda E68) → `{ lines[], offsets: [{ loanPublicId, number, modality, distribution[] }] (prelación D14), lossDeduction?, refund, entityMinimum { capital, minimum, wouldBreach }, pendingLoanBalance, fundsOrder, sha256, warnings[] }` | `Lending.MembershipWithdrawal.NotApproved`, `.LossDeductionNotEnabled`, `.SettlementDateInvalid` | K7 |
| `POST /{id}/settle` | MembershipWithdrawals.Settle | `SettleMembershipWithdrawalCommand` | `{ date?, expectedPreviewSha256, lossDeduction?: { sessionItemPublicId, year, proportion }, refund?: { form: TransferToAccount\|DisbursementOrder, targetAccountPublicId?, payee? }, reason, pendingLoanBalance?: MoveToCashier\|KeepRepaymentChannel }` (decisión del dueño del 2026-10-18, D45: con deuda pendiente después del cruce, quien liquida elige si los créditos pasan a caja —defecto— o conservan su forma de pago; D41 fija el orden de los fondos) → `Settled` con la operación del cruce por crédito y cuenta (`InternalOffset`, **todo o nada**, FR-077; E67), la pérdida en su propia operación (E68) y la orden de devolución por cuenta (E71); devolución sobre la atribución: 202 `PendingApproval`; con el mínimo irreducible de la entidad perforado, sólo la devolución de los **aportes** queda pendiente (los ahorros se devuelven, D43) y se avisa (`Cartera.DevolucionRetenidaPorMinimo`). Sin nada que devolver queda `Paid` (E72) | `Lending.Savings.PreviewChanged`, `Lending.MembershipWithdrawal.NotApproved`, `.OffsetRejected`, `.SettlementDateInvalid`, `.LossDeductionNotEnabled`, `Lending.Contributions.EntityMinimumBreached` (aviso), `Lending.Prevalidation.NotPostable` | K7 |
| `POST /{id}/refund/approve` | MembershipWithdrawals.Approve | `ApproveMembershipRefundCommand` | `{ approvalPublicId?, payee?, reason }` → ordena lo que quedó por devolver (la retenida por el mínimo, comparando otra vez, o la reintegrada por un giro rechazado; reemplaza al `reissue`); quien liquida radica la devolución y otra persona la aprueba (E71) | `Lending.Segregation.Conflict`, `Lending.MembershipWithdrawal.NotSettled`, `.NothingToRefund` | K7 |

Un reverso del pago del cruce en el crédito arrastra el débito de aportes y ahorros en la misma operación y deja el
retiro en `ToRedo` (US10-4); `settle` vuelve a liquidar sobre el mismo trámite. `Paid` lo marca el consumo de
`ResultadoDelGiro` de la orden de devolución.
**Decisiones del dueño del 2026-10-18** (research §13): el cruce y el pago de cada crédito van en el mismo comprobante (D45: un
`CruceAlRetiroAplicado` por crédito en la unidad de su `PagoAplicado`); el orden de los fondos lo fijan los estatutos
(`Captaciones.OrdenDeFondosAlRetiro`, D41); el motivo del retiro llena el de Asociados por el mapeo
`Captaciones.MotivosDeRetiroDeAsociados` (D46); el reverso tardío de un pago del cruce con el retiro pagado y las cuentas
canceladas ya no se rechaza: con `Lending.MembershipWithdrawals.ReverseAfterPayment` reabre las cuentas en cierre con el
saldo a favor del exasociado, devuelve el retiro a `Settled` y avisa (alerta `Cartera.ReversoTardioDeCruce` y correo a
gerencia, revisoría fiscal y `Captaciones.DestinatariosReversoTardio`); sin el permiso responde
`Lending.MembershipWithdrawal.AccountsClosed` (`data.requiredPermission`) y, si el exasociado ya reingresó con otra cuenta
de aportes viva, `Lending.MembershipWithdrawal.ReopenConflict` (D44).

---

## 12. Asamblea — `/api/lending/assembly-distributions` (FR-045–FR-047; K10)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?year=&kind=&status=` | `AlgunPermiso(Assembly.Register, Assembly.Approve)` | `ListAssemblyDistributionsQuery` | → arreglo `{ publicId, kind (AssemblyDistributionKind), year, sessionItem?, fund, percentOrValue, base, status: Simulated\|Applied\|Voided\|Reversed, totals }` (`Simulated` es el borrador; `Approved` no se usa; E183); sólo con **alcance total** (fuera, lista vacía; E184) | | K10 |
| `GET /{id}?page=` | ídem | `GetAssemblyDistributionQuery` | → la decisión con sus líneas por asociado | `Lending.AssemblyDistribution.NotFound` | K10 |
| `POST /` | Assembly.Register | `RegisterAssemblyDistributionCommand` | `{ kind, year, fundAmount, percent?, value?, base?: AverageBalance\|YearEndBalance (D13), destination?: Contributions\|Savings\|Payment (excedentes), targetDepositLineCode?, correctsPublicId? (corrige una reversada de la misma clase y ejercicio y hereda su acta), reason }` → 201 borrador `Simulated`; el punto del acta **no** va aquí: nombra a la decisión y se da al aplicar (E183, E191); el ejercicio tiene que estar cerrado | `Lending.AssemblyDistribution.Invalid`, `.CorrectionInvalid`, `.ContributionsNotActivated`, `Lending.Savings.DeliveryNotAvailable` | K10 |
| `POST /{id}/simulate` (sin clave) | Assembly.Register | `SimulateAssemblyDistributionQuery` | → `{ lines[], limitedByIpc { applied, ipc, referenceDate, source }, proratedToFund { applied, factor }, warnings[], totals, sha256 }` (US12-1) | `NationalValues.Missing` (IPC); aviso si un asociado no tiene cuenta de destino: su excedente se capitaliza en aportes (E257) | K10 |
| `POST /{id}/void` | Assembly.Register | `VoidAssemblyDistributionCommand` | `{ reason }` → el borrador `Voided` (descartar; **nueva**, enmienda E191) | `Lending.AssemblyDistribution.NotDraft` | K10 |
| `POST /{id}/apply` | Assembly.Approve | `ApplyAssemblyDistributionCommand` | `{ sessionItemPublicId? (nulo en una corrección), expectedPreviewSha256, supportAttachmentPublicIds[] (el acta firmada, `LendingAssemblySupport`), reason }` (E191) → `Applied`: una operación por asociado y **un** hecho masivo por decisión (E186, E187); **otra persona** que quien registró | `Lending.Segregation.Conflict`, `Lending.Savings.PreviewChanged`, `Lending.AssemblyDistribution.AlreadyApplied`, `.DuplicateSession`, `.SupportRequired`, `.NothingToApply`, `Lending.Contributions.EntityMinimumBreached`, `Lending.Session.ItemMismatch`, `Lending.Prevalidation.NotPostable` | K10 |
| `POST /{id}/reverse` | Assembly.Approve | `ReverseAssemblyDistributionCommand` | `{ reason }` → `Reversed` entera; para corregir se registra otra con `correctsPublicId` | `Lending.AssemblyDistribution.NotApplied`, `.PaymentNotReversible` (excedentes pagados en dinero), `.PayoutsInProgress`, `.ReversalWouldOverdraw` (E188) | K10 |
| `POST /{id}/orders/{orderId}/reissue` | Assembly.Approve | `ReissueSurplusPayoutOrderCommand` | `{ bankPublicId?, accountType?, accountNumber?, reason }` → la orden nueva (`orderPublicId`, `amount`, `reissuesOrderNumber`); regira el pago de excedentes que Tesorería rechazó, anuló o no se cobró, una vez por orden (**nueva**, E258, respuesta 92) | `Lending.PayoutOrder.NotReissuable` | K10 |

---

## 13. Fallecimiento — `/api/lending/deceased-cases` (FR-082, FR-083; K10)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?status=&page=` | `AlgunPermiso(DeceasedCases.Register, DeceasedCases.Approve)` | `ListDeceasedHolderCasesQuery` | → `{ publicId, person, dateOfDeath, route?, status: Registered\|DeliveryPending\|Approved\|Delivered\|Cancelled, accounts[], totals }` (`Approved` = giros en curso, `Delivered` = todas las cuentas canceladas; E193); lectura auditada | | K10 |
| `GET /{id}` | ídem | `GetDeceasedHolderCaseQuery` | → el caso con beneficiarios vigentes al fallecimiento y entregas | `Lending.DeceasedCase.NotFound` | K10 |
| `POST /` | DeceasedCases.Register | `RegisterDeceasedHolderCommand` | `{ personPublicId, dateOfDeath, deathCertificateAttachmentPublicId, reason? }` → 201; bloquea **todas** las cuentas vivas de captación y aportes (`HoldReason.Deceased`), suspende la causación según la línea (FR-065) y anula los retiros en espera; exige el alcance sobre todas las cuentas de la persona (E200) | `Lending.DeceasedCase.AlreadyRegistered`, `.Invalid` | K10 |
| `POST /{id}/delivery/preview` (sin clave) | DeceasedCases.Register | `PreviewDeceasedDeliveryQuery` | `{ mode: Beneficiaries\|WithoutProbate\|Probate, recipients?: [{ personPublicId, percent?, … }] }` (sin fecha: el saldo del día; E201) → `{ recipients: [{ person, percent, amount }], capWithoutSuccession { uvt?, amount, source }, supportsRequired[], excluded[], sha256 }` | `Lending.DeceasedCase.NoBeneficiaries`, `.CapExceeded`, `.NothingToDeliver` | K10 |
| `POST /{id}/delivery` | DeceasedCases.Register | `RegisterDeceasedDeliveryCommand` | `{ mode, recipients: [{ personPublicId, percent?, form: TransferToAccount\|DisbursementOrder, targetAccountPublicId?, payee? }], supportAttachmentPublicIds[], expectedPreviewSha256, reason }` → `DeliveryPending` | ídem, `.SupportsRequired`, `Lending.Savings.PreviewChanged` | K10 |
| `POST /{id}/delivery/approve` | DeceasedCases.Approve | `ApproveDeceasedDeliveryCommand` | `{ reason }` → liquida a la fecha y entrega el saldo del día por los porcentajes (mayor residuo): una operación `DeceasedDelivery` (`FAL-`) por destinatario y cuenta y sus órdenes de giro (US14-2); la cuenta se cancela al pagarse todas (E201) | `Lending.Segregation.Conflict`, `Lending.DeceasedCase.InvalidTransition`, `Lending.Prevalidation.NotPostable` (E206) | K10 |
| `POST /{id}/void` | DeceasedCases.Approve | `VoidDeceasedHolderCaseCommand` | `{ reason }` → anula un registro errado antes de aprobar la entrega y levanta los bloqueos (**nueva**, enmienda E200) | `Lending.DeceasedCase.InvalidTransition` | K10 |
| `POST /{id}/orders/{orderId}/reissue` | DeceasedCases.Approve | `ReissueDeceasedPayoutOrderCommand` | `{ bankPublicId?, accountType?, accountNumber?, reason }` → la orden nueva; regira la orden de la entrega que Tesorería rechazó: el valor sigue por pagar al beneficiario y el GMF retenido no se vuelve a cobrar (**nueva**, E258, respuesta 96) | `Lending.PayoutOrder.NotReissuable` | K10 |

---

## 14. No reclamados — `/api/lending/unclaimed` (FR-081, FR-084; K10)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?personPublicId=&status=&page=` | Accounts.Read | `ListUnclaimedBalancesQuery` | → `UnclaimedBalance { publicId, person, origin: UncashedRefund\|InactiveBalance\|UncashedPayout, sourceReference, amount, since, status: Held\|Claimed }` (enmienda E198; el estado del pagado es `Claimed`); lectura auditada | | K10 |
| `POST /transfer` | Unclaimed.Manage | `TransferToUnclaimedCommand` | `{ accountPublicIds?[], refundPublicIds?[], reason }` → lo trasladado, todo o nada (el ciclo diario lo hace solo cuando `Captaciones.DiasParaNoReclamados` o `CuentasAbandonadas.DiasSinMovimiento` se cumplen; E198) | `Lending.Unclaimed.NotEligible` (`data: { items[] }`), `.RuleNotEnabled` (inactivas sin `CuentasAbandonadas.TrasladoHabilitado`, FR-084), `Lending.Prevalidation.NotPostable` (E206) | K10 |
| `POST /{id}/pay` | Unclaimed.Manage | `PayUnclaimedBalanceCommand` | `{ form: TransferToAccount\|DisbursementOrder, targetAccountPublicId?, payee?: { bankPublicId, accountType, accountNumber }, reason }` (el beneficiario es siempre el titular; E199) → la operación `UnclaimedPayment` (`PNR-`) sobre la cuenta de origen y la orden, o el traslado a una cuenta viva del titular | `Lending.Unclaimed.AlreadyPaid`, `.NotFound`, `Lending.Prevalidation.NotPostable` (E206) | K10 |

---

## 15. Fondo de liquidez — `/api/lending/liquidity-fund` (FR-025, FR-109, FR-110; R25; K11)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /?date=` | LiquidityFund.View | `GetLiquidityFundPositionQuery` | → `{ date, category, regime { stage: General\|PorMadurez\|Transicion, source, transition? }, base: [{ depositClass, band?, balance, accruedAndToAccrue, percent, required }], required, newRegimeRequired?, constituted, difference, alert }` (sin titulares; la base se lee del libro, E211) | `Lending.LiquidityFund.ProfileCategoryMissing`, `.RulesInvalid`, `.ScopeNotTotal` | K11 |
| `GET /risk?date=&measure=Irl\|Brecha` | LiquidityFund.View | `GetLiquidityRiskQuery` | → el IRL por bandas (`RiesgoDeLiquidez.BandasIrl`) o la brecha de liquidez (`.BandasDeBrecha`), con activos líquidos, entradas y salidas por banda y el acumulado (**nueva**, enmiendas E210, E212) | `Lending.LiquidityFund.ScopeNotTotal` | K11 |
| `GET /snapshots?from=&to=` | LiquidityFund.View | `ListLiquidityFundSnapshotsQuery` | → instantáneas (`LND_LiquidityFundSnapshots`, H) | | K11 |
| `POST /snapshots` | LiquidityFund.Manage | `TakeLiquidityFundSnapshotCommand` | `{ date?, reason? }` → **200** con la foto; una fecha tiene una sola foto y pedirla otra vez devuelve la misma (E209, E210) | `Lending.LiquidityFund.ScopeNotTotal` | K11 |
| `GET /holdings?date=` | LiquidityFund.View | `ListLiquidityFundHoldingsQuery` | → constituido (`LND_LiquidityFundHoldings`) | | K11 |
| `POST /holdings` | LiquidityFund.Manage | `RegisterLiquidityFundHoldingCommand` | `{ instrument (LiquidityHoldingKind: BankDeposit\|TermInvestment\|TrustFund\|GovernmentBond\|Other), entityPersonPublicId, reference, amount, constitutedOn, maturityDate?, supportAttachmentPublicId (**obligatorio**, dueño `LendingLiquidityFundSupport`), note?, publicId? }` → **200** (E210) | `Lending.LiquidityFund.Invalid`, `.SupportRequired`, `.ScopeNotTotal` | K11 |
| `POST /holdings/{holdingId}/end` | LiquidityFund.Manage | `EndLiquidityFundHoldingCommand` | `{ endedOn, reason }` → 200; `endedOn` es el primer día en que ya no cuenta (E209) | `Lending.LiquidityFund.HoldingNotFound`, `.HoldingEnded` | K11 |

Un constituido menor que el requerido alerta `Cartera.FondoDeLiquidezInsuficiente` **(nuevo en contracts)** al
tesorero y al gerente (US15-1); además `Cartera.FondoDeLiquidezEnElMargen` y `Cartera.RiesgoDeLiquidezBajo`, a
`Lending.LiquidityFund.Manage` y `Lending.Decisions.Management` (E213). Todo exige **alcance total** (E210). La foto la toma
también el cierre del día del ciclo según `FondoDeLiquidez.Periodicidad`. Los depósitos asegurados son una vista
(`insured-deposits`, §18.1).

---

## 16. Migración y activación — `/api/lending/deposits` (FR-113–FR-118; R21, R27)

Mecánica de la 013 §19 por **naturaleza** (`nature`: `Deposit` o `Contribution`). Una carga (`LND_MigrationLoads` con
`Nature` **(nuevo en contracts)**, o su tabla hermana si `data-model.md` lo prefiere) tiene corte y partes; aplicar
sólo guarda; las cuentas nacen al activar con **una transacción de saldo inicial por concepto fechada al corte, sin
hechos contables** (FR-114, US9-4). Partes y columnas: `plantillas-y-archivos.md` §3.

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /migration/loads?nature=` | Deposits.Migration | `ListDepositMigrationLoadsQuery` | → arreglo `{ publicId, nature, cutoffDate, status: Draft\|Applied\|Replaced\|Activated, parts[], differences { pending, accepted } }` | | K7 |
| `GET /migration/loads/{id}` | Deposits.Migration | `GetDepositMigrationLoadQuery` | → la carga con resumen por parte | `Lending.MigrationLoad.NotFound` | K7 |
| `POST /migration/loads` | Deposits.Migration | `CreateDepositMigrationLoadCommand` | `{ nature, cutoffDate, notes? }` → 201 `Draft` | `Lending.Activation.AlreadyActivated` (`data: { nature }`) | K7 |
| `GET /migration/loads/{id}/parts/{part}/template.xlsx?withData=` | Deposits.Migration | `MapPlantillaPorRuta` | → la plantilla `lending.{part}` (con `withData`, los datos de la parte; `source=contribution-balances` toma los saldos de aportes cargados; exportar datos personales pide `Lending.Reports.ExportPersonalData`) | `Lending.Template.NotFound`, `Lending.Migration.PartNotForNature`, `.PartNotAvailable` | K7 |
| `POST /migration/loads/{id}/parts/{part}/import?mode=&replace=` | Deposits.Migration | `ImportDepositMigrationPartCommand` | multipart → `ImportResultDto`; varios archivos por parte; `{part}` incluye `line-equivalences`, que se carga primero (E115) | `Import.Invalid`, `Lending.Migration.LoadNotDraft`, `.LineEquivalenceMissing`, `.AccountNumberDuplicate`, `.MinorWithoutRepresentative` (E121) | K7 |
| `POST /migration/loads/{id}/apply` | Deposits.Migration | `ApplyDepositMigrationLoadCommand` | `{ reason }` → `{ status: Applied, balanceCheck, replacedLoadPublicId? }`; todo o nada entre partes | `Lending.Migration.CrossPartErrors`, `.PartMissing`, `Lending.TermDeposit.ScheduleMismatch` | K7 |
| `GET /migration/loads/{id}/balance-check` | Reconciliation.View | `GetDepositMigrationBalanceCheckQuery` | → cuadre por línea, concepto y conjunto de cuentas contra `IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync` (FR-115) | `Lending.Accounting.NotStarted` | K7 |
| `GET /migration/loads/{id}/reference-figures` · `PUT …` | Deposits.Migration | `GetDepositReferenceFiguresQuery` · `SetDepositReferenceFiguresCommand` | `{ figures: [{ lineCode, concept, amount, source }] }` → comparación de los totales cargados con las **cifras de referencia** (FR-118; ninguna pantalla nombra el sistema de origen) | | K7 |
| `POST /migration/loads/{id}/differences/accept` | Migration.AcceptDifferences | `AcceptMigrationDifferencesCommand` (el de la 013, con `DeCaptaciones`; E117) | `{ differencePublicIds[], sessionItemPublicId, reason }` | `Lending.Session.ItemMismatch`, `Lending.Segregation.Conflict` | K7 |
| `GET /activation?nature=` | Deposits.Activate | `GetDepositsActivationStatusQuery` | → `{ nature, status, load?, expectedDate, deadline, requirements: [{ code, ok, detail, whoFixes }] }` | | K7 |
| `POST /activation` | Deposits.Activate | `ActivateDepositsCommand` / `ActivateContributionsCommand` (por `nature`) | `{ nature, migrationLoadPublicId?, activationDate, sessionItemPublicId?, acknowledgeWarnings, reason }` → 201 `{ activationPublicId, nature, activationDate, accounts, openingTransactions, messages: [CaptacionesActivadas] }`. **Sin carga** (FR-117) no hay acta: `sessionItemPublicId` va vacío y el corte es la víspera de la fecha (E31); con carga, fecha = corte + 1 y acta obligatoria (E121) | `Lending.Activation.RequirementsMissing`, `.CutoffInvalid`, `.DeadlinePassed`, `.DifferencesNotAccepted`, `.AlreadyActivated` (`data.nature`), `.LoansFirst`, `Lending.Savings.DeliveryNotAvailable` | K7 |

Requisitos (`requirements[].code`): `LoansActivated`, `LoadApplied` (salvo cooperativa sin historia, FR-117),
`DifferencesAccepted`, `DateInTerm`, `AccountingMonthOpen`, `AccountsCollectorRegistered` (algún recaudador con
`ProveedorDeCuentas` adoptado; en producción, real), `TreasuryDestinationRegistered` (con `OrdenDeGiroEmitida` v2
adoptada), `PayrollConceptsReady` (aviso si `DESC_APORTES`/`DESC_AHORROS` no tienen cuentas), `DeliveriesComplete`
(K7 a K11 desplegadas, Q1), `ProfileAllowsClasses` (captaciones), `LinesWithMatrix` (completitud de la
matriz para los hechos de la naturaleza) y `DepositRolesCreated` (los roles de las cuatro plantillas de captaciones y aportes).
**Decisión del dueño del 2026-10-18** (research §13 D14, D18): todos se exigen **en todos los ambientes** —hasta esa fecha
`DeliveriesComplete`, `ProfileAllowsClasses` y `LinesWithMatrix` quedaban `Waived` fuera de producción (E32)—; fuera de producción
sólo cambia que el recaudador y la tesorería pueden ser los dobles `TST` y `TES`. Los perfiles van por módulo: la activación
de créditos (`LendingRolesCreated`, la 013 §19.3) exige sólo las trece plantillas del crédito y ésta sólo las cuatro suyas.
Lo fijan **(nuevo en contracts)** los códigos `AccountsCollectorRegistered`,
`PayrollConceptsReady`, `DeliveriesComplete`, `ProfileAllowsClasses`, `LinesWithMatrix`, `LoansActivated`, `DepositRolesCreated`.

---

## 17. Documentos — `/api/lending/documents` (FR-101–FR-106; `documentos.md`)

Las rutas de la 013 §15 sirven sin cambio de forma para las clases 7 a 15 de `IssuedDocumentKind` (la 15,
`GarnishmentResponse`, la emite sólo el embargo: por `POST /` responde `IssuedAutomatically`, E176). El cuerpo de `POST /`
suma `accountPublicId?`, `year?`/`period?`, `includeRestrictions?` e `includePermanentSavings?` según la clase (E170);
`GET /` suma el filtro `accountPublicId` y el DTO `accounts[]` (E168). Una ruta nueva aquí (el título del CDAT pendiente,
`POST /term-deposits/{id}/titles`, está en §5):

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /batches` | Documents.Issue | `IssueLendingDocumentBatchCommand` **(nuevo en contracts)** | `{ kind: DepositTaxCertificate\|WithholdingCertificate\|AccountStatement, year?, period?: { from, to }, personPublicIds?[], delivery?: Download\|Email }` → **200 con el resumen**: corre dentro de la petición, cada documento en su propia transacción y uno que no sale queda en el detalle (enmienda E177: no 202 con una corrida, que impediría el ciclo diario); `AccountStatement` es del último período terminado según `Documentos.PeriodicidadDeExtractos`, uno por cuenta viva | `Lending.Document.KindNotAvailable`, `Lending.Document.TaxYearNotClosed` (E171), `.PeriodInvalid` | K8 |

---

## 18. Informes, regulatorios y cambios en rutas existentes

### 18.1 Vistas nuevas de `/api/reports/lending/{vista}` (FR-107, FR-108, FR-110–FR-112; R24)

Una ruta por vista con `MapVistaDeCartera`, `Lending.Reports.View` y `RequirePermissionWhenExporting(Reports.Export)`;
las marcadas PD exigen además `Lending.Accounts.Read` para consultar y `Reports.ExportPersonalData` para exportar. Al
cierre son **23**: 16 vistas y una vista previa por cada uno de los siete formatos regulatorios de §18.2
(`year`, `month`; la exógena sólo `year`; alcance total; enmiendas E216, E221). La conciliación (`deposits-reconciliation`)
pide `Lending.Reconciliation.View` y no trae titulares (E109).

| Vista | E | Filtros propios | PD | Columnas (· ocultas) |
|---|---|---|---|---|
| `deposit-balances` | K7 | `asOf`, `groupBy=line\|office\|class` | no | Dimensión, Cuentas, Capital, Interés por pagar, Total |
| `deposit-transactions` | K7 | `from`, `to`, `account?`, `kind?`, `dateKind` | sí | Fechas, Cuenta, Tipo, Concepto, Valor, Operación, Mensaje · `_cuenta`, `_operacion`, `_mensaje` |
| `deposit-accounts-by-status` | K7 | `asOf`, `status?` | sí | Cuenta, Titular, Línea, Oficina, Estado, Saldo, Marcas · `_cuenta`, `_persona` |
| `term-deposit-maturities` | K9 | `from`, `to`, `issueTermBand?` | sí | Vencimiento, CDAT, Titular, Capital, Tasa, Plazo de emisión, Instrucción · `_cuenta` |
| `depositor-concentration` | K8 | `asOf`, `top?` | sí | Titular, Saldo total, Participación, Acumulado · `_persona` |
| `interest-and-withholding` | K8 | `from`, `to`, `class?` | sí | Cuenta, Titular, Interés, Base, Componente inflacionario, Retención, Neto · `_cuenta` |
| `gmf` | K8 | `year`, `month` | sí | Persona, Cuenta exenta, Retirado, Exento usado, Gravado, GMF · `_persona` |
| `contributions-by-member` | K7 | `asOf`, `class?` | sí | Asociado, Mínimo, Ordinarios, Extraordinarios, Revalorizados, Total, Cuotas en mora · `_persona` |
| `installments-in-arrears` | K7 | `asOf`, `line?` | sí | Cuenta, Titular, Período, Vencimiento, Pendiente, Motivo · `_cuenta` |
| `unclaimed` | K10 | `asOf` | sí | Persona, Origen, Valor, Desde, Estado · `_persona` |
| `garnishments` | K8 | `status?` | sí | Persona, Oficio, Juzgado, Valor, Retenido, Alimentos, Estado · `_persona` |
| `negotiated-rates` | K9 | `from`, `to` | sí | CDAT, Titular, Tasa de tabla, Margen, Regla, Aprobador, Costo frente a la tabla · `_cuenta` |
| `charges` | K8 | `from`, `to`, `status?` | sí | Cuenta, Cargo, Valor, IVA, Estado, Factura · `_cuenta` |
| `liquidity-fund` | K11 | `asOf` | no | Clase, Banda, Base, Porcentaje, Requerido, Constituido, Diferencia |
| `insured-deposits` | K11 | `asOf` | sí | Ahorrador, Depósitos, Asegurado hasta el tope, No asegurado (sólo si la cooperativa está inscrita; sin aportes) · `_persona` |
| `deposits-reconciliation` | K7 | `asOf`, `branch?` | no | Línea, Concepto, Oficina, Saldo Cartera, Saldo contable, Diferencia, Explicada |
| `sicses-captaciones` | K11 | `year`, `month` | sí | La proyección del formato (vista previa; el archivo, §18.2) |
| `sicses-cotitulares` | K11 | `year`, `month` | sí | ídem (9068, enmienda E216) |
| `sicses-aportes` | K11 | `year`, `month` | sí | ídem (9013, enmienda E216) |
| `sicses-saldos-diarios` | K11 | `year`, `month` | sí | ídem |
| `sicses-fondo-liquidez` | K11 | `year`, `month` | no | ídem |
| `exogena-1019` | K11 | `year` | sí | ídem |
| `exogena-1020` | K11 | `year` | sí | ídem |

### 18.2 Archivos regulatorios de captaciones — `/api/lending/regulatory` (FR-111, FR-112)

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `POST /deposit-formats/{formatCode}/validate` (sin clave) | RegulatoryReports.Generate | `ValidateDepositRegulatoryFileQuery` **(nuevo en contracts)** | `{ year, month? }` en el cuerpo → `{ layout, issues[], warnings: [LayoutSinCotejar?] }`; exige el cierre mensual vigente de la Cartera del mes (o de diciembre en la exógena) y **alcance total** (E219, E220) | `Lending.Regulatory.FormatUnknown`, `.PeriodNotClosed`, `.PeriodInvalid` (SICSES sin mes), `.ScopeNotTotal` (E222) | K11 |
| `POST /deposit-formats/{formatCode}/generate` | RegulatoryReports.Generate | `GenerateDepositRegulatoryFileCommand` **(nuevo en contracts)** | `{ year, month?, acknowledgeWarnings }` con `Idempotency-Key` → 201 `RegulatoryFileDto` (con `formatCode`); versiones **por formato**, archivo `{número}_{FORMATO}_{aaaa-mm\|aaaa}_v{n}.txt` (E220) | `Lending.Regulatory.BlockingIssues` (422; queda una versión `Validated` sin archivo), los de `validate`, aviso `Lending.Regulatory.LayoutSinCotejar` | K11 |

`{formatCode}` (**siete**, enmienda E216): `sicses-captaciones` (9026), `sicses-cotitulares` (9068), `sicses-aportes` (9013),
`sicses-saldos-diarios` (169), `sicses-fondo-liquidez` (9067), `exogena-1019`, `exogena-1020`; el F29 (159) no se construye.
Listar, descargar y marcar cargado son las rutas de la 013 §18 (`/files?format=`, `RegulatoryFileKind` suma `DepositsSicses`
3 y `Exogenous` 4 **(nuevo en contracts)**; la exógena se guarda con `PeriodMonth = 12`, E217). El permiso es
`Lending.RegulatoryReports.Generate` y la pantalla `/cartera/captaciones/reportes-regulatorios` (E223).

### 18.3 Rutas nuevas de libranza (FR-125, E-05) — `/api/lending/payroll-agreements`

| Ruta | Permiso | Comando / consulta | Cuerpo → respuesta | Errores | E |
|---|---|---|---|---|---|
| `GET /{id}/file-groups?date=` | PayrollAgreements.View | `ListPayrollAgreementFileGroupsQuery` | → `PayrollAgreementFileGroup { publicId, code, name, outboundFormat, resultFormat, calendar { cutoffDay, sendDay, payDay }, validFrom, validTo? }` | | K7 |
| `POST /{id}/file-groups` | PayrollAgreements.Manage | `SetPayrollAgreementFileGroupsCommand` | `{ validFrom, groups: [{ code, name, outboundFormatPublicId, resultFormatPublicId, calendar }], reason }` → versión nueva del convenio que copia la vigente (E61) | `Core.BankFileFormat.ScopeMismatch` (ámbitos 5 o 6 de K2), `Lending.PayrollAgreement.VersionOverlaps`, `.FileGroupDuplicate` | K7 |
| `GET /{id}/concept-equivalences?date=` | PayrollAgreements.View | `ListPayrollAgreementConceptEquivalencesQuery` | → `PayrollAgreementConceptEquivalence { paymasterConceptCode, fileGroupCode, members: [{ lineCode, nature, concept?, priority }], split: Priority\|Proportional, order }` | | K7 |
| `POST /{id}/concept-equivalences` | PayrollAgreements.Manage | `SetPayrollAgreementConceptEquivalencesCommand` | `{ validFrom, equivalences[], reason }` → versión nueva (`identityEquivalences` y `causeEquivalences` **no se construyeron**, E61) | `Lending.PayrollAgreement.EquivalenceWithoutSplit`, `.LineWithoutGroup` (`data: { lineCode }`), `.ConceptMappedTwice`, `.EquivalenceWithoutItems`, `.EquivalencePriorityRepeated`, `.EquivalenceDuplicate` (E61) | K7 |

`POST /payroll-deduction-files` y `/preview` (la 013 §13.2) suman `fileGroupCode` al cuerpo; generar con un concepto
sin regla de reparto o una línea con cuota sin grupo responde `Lending.PayrollAgreement.EquivalenceWithoutSplit` o
`.LineWithoutGroup` (FR-125).

**Construido (T091, decisión del dueño del 2026-10-18, D36).** Con una versión que usa grupos, `fileGroupCode` es
obligatorio (`Lending.PayrollFile.FileGroupRequired`, `data.fileGroups`) y debe ser de la versión vigente al período
(`.FileGroupNotFound`); sin grupos no se admite. El archivo usa el formato de ida del grupo, lleva los créditos y las
cuotas de cuentas cuyo código de la pagaduría es del grupo —cada línea con su código— y viaja como
`DescuentosPorPagaduria` v2; si lleva cuotas de cuentas y Recaudo no adoptó la v2,
`Lending.PayrollAgreement.ReceiptsVersionNotAdopted`. Una obligación o cuenta que ningún código cubre no va en ningún
archivo y la vista previa la cuenta (`withoutEquivalence`). La unicidad del período es por grupo. Respuestas: el
archivo suma `fileGroupCode`; su detalle, `accountLines[]`
(`{ publicId, lineNumber, employeeId, accountPublicId, accountNumber, holder, installmentPublicId, dueDate,
paymasterConceptCode, currentAmount, pastDueAmount, total, novelty, previousAmount }`); cada línea de crédito,
`paymasterConceptCode`; la vista previa, `accountLines[]` y `withoutEquivalence`. Ruta nueva
`GET /payroll-deduction-files/{id}/unmatched` (`Lending.PayrollDeductionFiles.Read`) →
`[{ payerEmployeeId, paymasterConceptCode, expected, deducted, reasonCode, paymasterResultCode?, deductionDate?,
recordedAt }]`: las filas de la respuesta que no se aplicaron. El doble de Recaudo
(`POST /test-doubles/collector/payroll-deduction-results`) lee la respuesta de un archivo con grupo con el formato de
vuelta del grupo (empleado, código, descontado, código de respuesta y fecha) y emite `ResultadoDeDescuentos` v2.

### 18.4 Dobles de prueba — `/api/lending/test-doubles/collector/accounts/*` (sólo fuera de producción)

`Lending.TestDoubles.Operate`; código `TST`, clase `Test`, clave `TST:{guid:N}` que la pantalla propone y deja repetir.

| Ruta | Llama | E |
|---|---|---|
| `GET /collector/accounts?personPublicId=&date=&includeAsSignatory=` | `ConsultarAsync` | K7 |
| `GET /collector/accounts/installments?personPublicId=&date=` | `ListarExigiblesAsync` | K7 |
| `POST /collector/accounts/simulations` (sin clave) | `SimularAsync` | K7 |
| `POST /collector/accounts/deposits` | `ConsignarAsync` | K7 |
| `POST /collector/accounts/withdrawals` | `RetirarAsync` | K8 |
| `POST /collector/accounts/transfers` | `TrasladarAsync` | K7 |
| `POST /collector/accounts/operations/{key}/reverse` | `ReversarAsync` | K7 |
| `POST /collector/treasury/accounts/deposits` | `ConsignarAsync` como el doble de Tesorería (`TES`, clase `Treasury`, clave `TES:{documento:N}:{cuenta:N}`, medios `Transfer`/`BankDeposit`) **(decisión del dueño del 2026-10-18, D52)** | K7 |
| `POST /collector/treasury/accounts/operations/{key}/reverse` | `ReversarAsync` como el doble de Tesorería (D52) | K7 |

El doble de Tesorería cuenta si quien prueba registra el participante `TES` con rol `ObligationsCollector`, clase
`Treasury`, `isTestDouble = true` y `ProveedorDeCuentas` adoptado (el mismo código que la tesorería de prueba como destino,
con otro rol). La tesorería de prueba, como destino, acepta además `ConsignacionReversadaPorCartera` y
`PagoReversadoPorCartera` (D19, D52).

`TesoreriaDePrueba` responde también las órdenes v2 (rutas existentes `/treasury/orders`).

### 18.5 Cambios en rutas existentes

| Ruta existente | Cambio | Por qué | E |
|---|---|---|---|
| `GET /api/lending/credit-lines` | Lista sólo `Nature = Placement`; crear o versionar con otra naturaleza no existe por aquí | R01 | K7 |
| `GET /api/lending/disbursement-orders` · `/{id}` · `/void` · `/reissue` | Filtros `?kind=&accountPublicId=`; el DTO suma `kind` (`OrderKind`, `mensajes.md`) y `account?`; `reissue` admite órdenes de captaciones | FR-091 | K7 |
| `GET /api/lending/messages` · `/accounting/facts` | `destination` admite `Associates`; filtro `accountPublicId=` | `mensajes.md` | K7 |
| `GET /api/lending/accounting/reconciliation` | Suma conjuntos de captaciones y aportes (`contabilidad.md` §8) | FR-100 | K7 |
| `GET /api/lending/process-runs/{id}` | Errores con `accountPublicId`, `accountNumber` y paso de captaciones | R10 | K7 |
| `POST /api/accounting/periods/{year}/{month}/close` | La guarda `Accounting.Period.LendingNotClosed` cubre la causación de captaciones activadas | FR-100, E-03 | K7 |
| `POST /api/accounting/documents/{id}/reverse` | Comprobantes de captaciones: `Accounting.Document.ModuleOwned` | FR-099 | K7 |
| `POST /api/payroll/runs/{runId}/approve` · `/reverse` | Aplican y reversan `DESC_APORTES` y `DESC_AHORROS` por el contrato de cuentas; avisos `Payroll.AccountDeductionNotApplied` **(nuevo en contracts)** | E-02 | K7 |
| `GET /api/payroll/runs/{runId}/account-deductions` (nueva en Nómina) | `Payroll.Runs.View` · `ListPayrollAccountDeductionsQuery` **(nuevo en contracts)** → `{ employee, accountPublicId, accountNumber, concept: DESC_APORTES\|DESC_AHORROS, expected, deducted, notDeducted, reason?, status, operationPublicId? }` | E-02 | K7 |
| `DELETE /api/core/people/{id}` · `GET …/{id}/detail` | Bloquea con `Person.HasLiveDepositProducts` **(nuevo en contracts)** si tiene cuentas vivas (FR-095); el detalle muestra productos de captación | E-04 | K7 |
| `PUT /api/core/associates/{id}` | Ya no cambia el estado a retirado a mano: `Associate.WithdrawalOwnedByLending` **(nuevo en contracts)**; el estado lo cambia el consumo de `RetiroDeAsociadoAprobado` | E-04, FR-124 | K7 |
| `GET /api/core/bank-file-formats` · `POST` | `scope` `PayrollDeductionRequest` (5) y `PayrollDeductionResult` (6) de K2 admiten líneas de cuentas (C01) | FR-125 | K7 |
| `/api/lending/contribution-balances` | Importar responde `Lending.Contributions.UseMigration` desde la activación de aportes | FR-094 | K7 |
| `/api/lending/documents` | Clases 7–15 (la 15, la respuesta al juzgado, E176); `GET /` filtra por `accountPublicId` (E168) | FR-101 | K7–K9 |
| `GET /api/lending/regulatory/files` | Filtro `format=` y `formatCode` en el DTO (E217) | FR-111 | K11 |
| `GET /api/core/people/{id}/co-held-accounts` (nueva en Core) | `Core.People.View` · `GetPersonCoHeldAccountsQuery`: las cuentas vivas donde la persona es cotitular; la ficha cuenta sólo las de titular (decisión del dueño del 2026-10-18, D20) | D20 | K7 |
| `GET` · `POST /api/lending/approval-policies` (nuevas en Atribuciones) | `Lending.Attributions.View` · `.Manage` · `ListApprovalPoliciesQuery` · `SaveApprovalPolicyCommand`: la política de niveles de `AccountWithdrawal` y `AccountCashDeposit` (§1.6, D53); `{ subject, validFrom, reason, levels[] }` | D53 | K8 |

---

## 19. Lo heredado que se retira sin alias (FR-120, R28)

Se retiran en el mismo paso que la migración `RetiroDeCaptacionesHeredadas`: responden **404** y no dejan alias
(`Endpoints/Savings`, `Endpoints/CDT`, la exención por nombre de `/api/savings/*` en
`LosEndpointsProtegidosExigenPermiso` y el módulo de auditoría `Savings`).

**52 rutas `/api/savings/*`**:

| Grupo | Rutas | Cantidad |
|---|---|---|
| `/api/savings/contributions` | `GET /{personId}`, `GET /{personId}/history`, `POST /` | 3 |
| `/api/savings/savings-accounts` | `GET /`, `GET /{id}`, `GET /{id}/statement`, `POST /`, `POST /{id}/deposit`, `POST /{id}/withdraw`, `POST /{id}/close` | 7 |
| `/api/savings/savings-interest-liquidation` | `POST /`, `GET /preview` | 2 |
| `/api/savings/savings-parameters` | `GET /`, `GET /{id}`, `POST /`, `PUT /{id}`, `DELETE /{id}` | 5 |
| `/api/savings/sipla-parameters` | ídem | 5 |
| `/api/savings/statements` | `POST /generate/{year}/{month}`, `GET /{personId}/{year}/{month}` | 2 |
| `/api/savings/sub-zones` | CRUD | 5 |
| `/api/savings/transaction-codes` | CRUD | 5 |
| `/api/savings/withdrawal-statuses` | CRUD | 5 |
| `/api/savings/withdrawals` | `GET /preview/{personId}`, `POST /`, `GET /` | 3 |
| `/api/savings/zone-types` | CRUD | 5 |
| `/api/savings/zones` | CRUD | 5 |

**18 rutas `/api/cdt/*`**: `/api/cdt/interest-liquidation` (`POST /`, `GET /preview`; 2), `/api/cdt/parameters`
(CRUD; 5), `/api/cdt/rates-by-term` (CRUD; 5), `/api/cdt/certificates` (`GET /`, `GET /{id}`, `GET /near-expiry`,
`POST /`, `POST /{id}/renew`, `POST /{id}/cancel`; 6).

**4 de informes**: `/api/reports/savings/person-portfolio/{personId}` (+ `/pdf`),
`/api/reports/savings/savings-statement/{accountId}/pdf`, `/api/reports/cdt/certificate/{cdtId}/pdf`.

Total retirado: **74 rutas** (70 de FR-120 más 4 de informes). Las pantallas heredadas (`/cartera/cdt*`,
`/cartera/depositos`, `/cartera/retiros`, `/cartera/extractos`, `/cartera/parametros-ahorro`,
`/cartera/parametros-sipla`, `/cartera/codigos-movimiento`, `/cartera/zonas`, `/cartera/tipos-zona`,
`/cartera/estados-retiro`) se retiran y las nuevas viven en `Pages/Cartera/Captaciones` (research §0.6).

---

## 20. Lo que no tiene ruta

- **Puertos** (`contrato-de-cuentas.md`): `IContratoDeCuentas` (siete métodos), `IConsultaDeAportes` v2,
  `IConsultasDeCartera.ProductosDeCaptacionAsync`. Se exponen sólo por los dobles de prueba (§18.4).
- **Consumidores** (`LosComandosDeConsumoNoTienenRuta`): `ConsumeMembershipWithdrawalApprovedCommand` (en Core),
  `ConsumeChargeInvoiceRegisteredCommand`, la ampliación de `ConsumeTreasuryResultCommand` y
  `ConsumePayrollDeductionResultCommand` (v2), y en Contabilidad `PostLendingFactsCommand` /
  `PostLendingSummaryGroupCommand` con los hechos nuevos.
- **Procesos**: el paso `PasoDeCaptaciones` del ciclo diario (causación, liquidación, cuotas, vencimientos, gracia,
  prórrogas, pagos del plan del CDAT, contractual, inactividad, cargos periódicos, mayoría de edad, débito
  automático) y la sección de captaciones del cierre mensual (R10). Se ordenan por `/process-runs` de la 013.

---

## 21. Conteo de rutas nuevas

Contado contra el código al cierre (2026-10-19; cómo, en «Estado al cierre»). Entre paréntesis, lo que decía el diseño.

| Sección | Rutas |
|---|---|
| §1.6 Política de aprobación (`/approval-policies`, D53) | 2 (no estaba) |
| §3 Líneas | 16 (21: sin `rate-quote` ni las cuatro de plantillas) |
| §4 Cuentas | 33 (34: sin `installments` ni `negotiated-rate`; nace `contractual`) |
| §5 CDAT | 13 (11: nacen `fund` y `POST titles`) |
| §6 Tasa negociada | 5 |
| §7 Aportes | 10 (7: nacen `GET /assignments` y los dos `reject`) |
| §8 Embargos | 7 (6: nace `responses/draft`) |
| §9 Débito automático | 3 |
| §10 Exención de GMF | 4 |
| §11 Retiro del asociado | 9 |
| §12 Asamblea | 7 (6: nace `void`) |
| §13 Fallecimiento | 7 (6: nace `void`) |
| §14 No reclamados | 3 |
| §15 Fondo de liquidez | 7 (6: nace `risk`) |
| §16 Migración y activación | 12 |
| §17 Documentos (lote) | 1 |
| §18.1 Vistas de informes | 23 (21: nacen `sicses-cotitulares` y `sicses-aportes`) |
| §18.2 Regulatorios | 2 |
| §18.3 Libranza | 5 (4: suma `payroll-deduction-files/{id}/unmatched`) |
| §18.4 Dobles de prueba | 9 (2026-10-18: +2 del doble de Tesorería, D52) |
| **Total `/api/lending` y `/api/reports/lending`** | **178** (168) |
| Nueva en Nómina (§18.5) | 1 |
| Nueva en Core (§18.5, D20) | 1 (no estaba) |
| **Total de la 014** | **180** (171 sin los dobles de prueba) |

Rutas que se retiran: **74**. Variación neta del total de rutas REST: **+106** (el diseño decía +95).

---

## 22. Nombres fijados en este documento (nuevo en contracts)

Lo que research §0 no nombra y este contrato necesitó, con las reglas de la 013 (inglés en símbolos, código de error
`Área.Recurso.Motivo`):

- **Rutas auxiliares**: `/lines/deposit`, `/lines/contribution`, `/lines/{id}/close` · `/reopen`,
  `/lines/{id}/negotiated-rate-rules/{ruleId}/end` (`/lines/{id}/rate-quote` y `/lines/{deposit|contribution}/template.xlsx`
  · `/import` no nacieron); `/accounts/counts`, `/accounts/{id}/balance`, `/approve-opening`, `/close-preview`, `/close`,
  `/operations/simulate`, `/operations/withdrawals`, `/operations/transfers`, `/operations/{operationId}/reverse`,
  `/data-update`, `/maturity-instruction`, `/contractual`, `/holds/{holdId}/release`, `/charges/{chargeId}/waive` · `/reverse`,
  `/charges/exemption`, `/settlements/simulate`, `/terms` (`/installments` y `/negotiated-rate` por cuenta no nacieron);
  `/term-deposits/maturity-calendar`, `/simulate`, `/{id}/fund`, `/{id}/schedule`, `/{id}/renew`, `/{id}/redeem` ·
  `/redeem/simulate`, `/{id}/titles` (GET y POST);
  `/negotiated-rate-requests` (grupo); `/contributions/summary`, `/installments`, `/refunds` (con `/approve` · `/reject`),
  `/assignments` (GET y POST, con `/approve` · `/reject`); `/garnishments/{id}/responses` · `/responses/draft` · `/consign` ·
  `/release`; `/direct-debits/{id}/revoke`; `/gmf-exemptions/usage`,
  `/{id}/end`; `/membership-withdrawals/{id}/desist` · `/approve` · `/reject` · `/settlement/preview` · `/settle` ·
  `/refund/approve`; `/assembly-distributions/{id}/simulate` · `/void` · `/apply` · `/reverse`;
  `/deceased-cases/{id}/delivery` · `/delivery/preview` · `/delivery/approve` · `/void`; `/unclaimed/transfer`, `/{id}/pay`;
  `/liquidity-fund/risk`, `/snapshots`, `/holdings`, `/holdings/{holdingId}/end`; `/approval-policies`; `/deposits/migration/loads/{id}/reference-figures`;
  `/documents/batches`; `/regulatory/deposit-formats/{formatCode}/validate` · `/generate`;
  `/payroll-agreements/{id}/file-groups` · `/concept-equivalences`; `/test-doubles/collector/accounts/*`;
  `/api/payroll/runs/{runId}/account-deductions`; `/api/core/people/{id}/co-held-accounts`.
- **Atribuciones con monto**: `Lending.Accounts.Withdraw`, `.Deposit`, `Lending.TermDeposits.Redeem`,
  `Lending.MembershipWithdrawals.Settle` en `PermisosLimitables`.
- **Plantillas de rol**: `cartera.jefe-captaciones`, `cartera.asesor-captaciones`, `cartera.analista-aportes`,
  `cartera.tesorero-liquidez`.
- **Comandos y consultas** de las tablas (todos los no listados en research §0): `ListLendingLinesQuery`,
  `GetLendingLineQuery`, `CreateDepositLineCommand`, `CreateContributionLineCommand`, `AddDepositLineVersionCommand`,
  `AddContributionLineVersionCommand`, `CloseLendingLineCommand`, `ReopenLendingLineCommand`,
  `AddNegotiatedRateRuleCommand`, `EndNegotiatedRateRuleCommand`, `QuoteDepositRateQuery`, `OpenMemberAccountCommand`,
  `ApproveMemberAccountOpeningCommand`, `CloseMemberAccountCommand`, `WithdrawToDisbursementOrderCommand`,
  `TransferBetweenMemberAccountsCommand`, `ReverseMemberAccountOperationCommand`, `SetMemberAccountPartiesCommand`,
  `RegisterAccountHoldCommand`, `ReleaseAccountHoldCommand`, `RegisterAccountChargeCommand`,
  `WaiveAccountChargeCommand`, `ReverseAccountChargeCommand`, `SetAccountChargeExemptionCommand`,
  `SettleAccountInterestCommand`, `SimulateInterestSettlementQuery`, `ConstituteTermDepositCommand`,
  `RenewTermDepositCommand`, `RedeemTermDepositCommand`, `RequestNegotiatedRateCommand`, `DecideNegotiatedRateCommand`,
  `RequestContributionRefundCommand`, `RequestContributionAssignmentCommand`, `RegisterGarnishmentCommand`,
  `ConsignGarnishmentCommand`, `ReleaseGarnishmentCommand`, `RegisterDirectDebitMandateCommand`,
  `MarkGmfExemptAccountCommand`, `FileMembershipWithdrawalCommand`, `DecideMembershipWithdrawalCommand`,
  `SettleMembershipWithdrawalCommand`, `RegisterAssemblyDistributionCommand`, `ApplyAssemblyDistributionCommand`,
  `RegisterDeceasedHolderCommand`, `RegisterDeceasedDeliveryCommand`, `TransferToUnclaimedCommand`,
  `TakeLiquidityFundSnapshotCommand`, `RegisterLiquidityFundHoldingCommand`, `CreateDepositMigrationLoadCommand`,
  `ImportDepositMigrationPartCommand`, `ApplyDepositMigrationLoadCommand`, `IssueLendingDocumentBatchCommand`, y sus
  hermanos de lectura con el mismo radical.
- **Cotejo con el código (2026-10-19)**: no nacieron `QuoteDepositRateQuery`, `RequestNegotiatedRateCommand`,
  `ImportDepositLinesCommand` ni `ImportContributionLinesCommand`. Se llaman distinto o nacieron después:
  `PlaceAccountHoldCommand` (no `RegisterAccountHoldCommand`), `ListLendingLineVersionsQuery` (una para las dos naturalezas),
  `ListContributionRequestsQuery` y `RejectContributionRequestCommand` (devoluciones y cesiones, E58), `GetContractualQuery`
  (E151), `FundTermDepositCommand` (E130), `ReissueTermDepositTitleCommand` (E173), `GetGarnishmentResponseDraftQuery` (E158),
  `VoidAssemblyDistributionCommand` (E191), `VoidDeceasedHolderCaseCommand` (E200), `GetLiquidityRiskQuery` (E210),
  `ListApprovalPoliciesQuery` · `SaveApprovalPolicyCommand` (D53) y `GetPersonCoHeldAccountsQuery` (D20).
- **Códigos de error**: `Lending.Savings.{NotFound, NotActivated, LineNotAllowedByProfile, DeliveryNotAvailable,
  MembersOnly, InsufficientAvailable, SignaturesMissing, AccountNotOperable, CashLimitExceeded, SourceOfFundsRequired,
  Overdrawn, PendingApproval, BeneficiariesMustSum100, CoHoldersNotAllowed, MinorsNotAllowed, RepresentativeRequired,
  NotOpening, CloseBlocked, OperationNotFound, WithdrawalLimitExceeded, TargetNotOperable, ReversalOfReversal,
  ReversalOwnedByCollector, ReversalWouldOverdraw, DataUpdateNotRequired, NotContractual, ChargeNotInLine, ChargeExempt,
  ChargeNotWaivable, SettlementPeriodOpen, AccrualBehind, PreviewChanged, BalanceViewed, StatementViewed,
  TransactionsViewed}` (`ReversalWindowClosed`, `HoldExceedsBalance` y `HoldAutomatic` no nacieron: E85 y E161);
  `Lending.Contributions.{NotActivated, MinimumBreached, ParticipationLimitExceeded, LineAlreadyActive,
  InstallmentCapExceeded, AccountAlreadyOpen, ClosureOnlyByWithdrawal, NotWithdrawable, HasLiveLoans,
  EntityMinimumBreached, AssignmentNotAllowed, UseMigration}` (`HoldNotAllowed` no nació: E161); `Lending.TermDeposit.{NotFound, TermOutOfRange,
  AmountOutOfRange, NoRateForTerm, PeriodicityNotAllowed, FundingAmountMismatch, GraceExpired, NotInRenewalWindow,
  Garnished, DeceasedHolder, EarlyRedemptionNotAllowed, ScheduleMismatch}`; `Lending.Gmf.{AlreadyMarked,
  AccountNotSavings, PolicyWithoutMark}`; `Lending.Line.{NotFound, UseCreditLinesRoute}`;
  `Lending.DepositLine.{Invalid, VersionNotFound, VersionOverlaps, Retroactive, ClassImmutable}`;
  `Lending.ContributionLine.{VersionOverlaps, Retroactive}`; `Lending.NegotiatedRate.{NotFound, RuleOverlaps,
  LineDoesNotAllow, NoApplicableRule, MarginAboveRule, AlreadyDecided}` (`LevelRequired` no nació: sin el nivel, el 404
  `NotFound`); `Lending.Garnishment.{NotFound,
  ContributionsNotGarnishable, NothingGarnishable, ExceedsRetained, AlreadyReleased}`; `Lending.DirectDebit.{NotHolder,
  AccountNotDemand, Overlaps}`; `Lending.MembershipWithdrawal.{NotFound, AlreadyInProgress, NotAssociate, NotFiled,
  NotApproved}`; `Lending.AssemblyDistribution.{NotFound, DuplicateSession, AlreadyApplied, NotApplied}`;
  `Lending.DeceasedCase.{NotFound, AlreadyRegistered, NoBeneficiaries, CapExceeded}`; `Lending.Unclaimed.{NotEligible,
  RuleNotEnabled, AlreadyPaid}`; `Lending.LiquidityFund.ProfileCategoryMissing`; `Lending.Activation.LoansFirst`;
  `Lending.Migration.PartNotForNature`; `Lending.Regulatory.{FormatUnknown, PeriodNotClosed}`;
  `Lending.PayrollAgreement.{EquivalenceWithoutSplit, LineWithoutGroup}`; `Lending.Document.TaxYearNotClosed` (el de la 013;
  `YearNotClosed` no nació, E171);
  `Payroll.AccountDeductionNotApplied`; `Person.HasLiveDepositProducts`; `Associate.WithdrawalOwnedByLending`.
- **Códigos que nacieron en el código** (además de los de arriba; cada uno en su enmienda): `Lending.Savings.{MemberDataIncomplete,
  UseTermDepositRoute, OpeningDateInvalid, LineNotOpen, LineWithoutVersion, OfficeNotAllowedByLine, HolderDeceased,
  PermanentAlreadyOpen, JointRequiresTwoSigners, PartyRepeated, PartyInvalid, LegalEntitiesNotAllowed, RateTiersOverlap,
  RateChangeNoticeTooShort, LedgerInconsistent, AmountTooPrecise, ReversalOwnedByProcess, ChargeCollectedLater,
  MembershipWithdrawalInProgress, CloseDateInvalid, AccountDoesNotAccrue, AccrualFailed, SettlementFailed, SameAccount,
  ValueDateOutOfRange}` (E20, E36, E44, E47, E52, E83, E85, E86, E98, E99); `Lending.Contributions.{OnlyHolderAndBeneficiaries,
  FractionBelowLegal, IncomeMissing, OverpaymentNotAllowed, NoParticipationExcess, RequestNotFound, RequestAlreadyDecided}`
  (E36, E47, E54, E56, E58); `Lending.ContributionLine.{Invalid, VersionNotFound}`; `Lending.DepositLine.ReferencedLineInvalid`
  (E36); `Lending.Contractual.{TermsRequired, TermsNotAllowed, InstallmentBelowMinimum, TermOutOfRange, NoInstallments,
  FreeDepositNotAllowed, WithdrawalNotAllowed, NotCurrent, DestinationInvalid, MaturityPending}` (E143–E151);
  `Lending.TermDeposit.{AlreadyFunded, PendingNegotiation, FundingInvalid, StartDateInvalid, NoAdditionalDeposits,
  NegotiationRequiresApproval, NoRolloverRate, DestinationInvalid, LineIncomplete}` (E36, E130, E136);
  `Lending.NegotiatedRate.{OnlyRequester, Retroactive, MarginInvalid, RuleNotFound}` (E133); `Lending.Gmf.{Retroactive,
  AlreadyEnded, NotFound, NotHolder}` (E48); `Lending.DirectDebit.{NotFound, AlreadyRevoked, Retroactive}`;
  `Lending.Holds.{NotFound, OwnedByProcess, LoanStillActive, InsufficientAvailable, Invalid}` y
  `Lending.Garnishment.{Invalid, NotActive, PartialReleaseInvalid, AccountNotOfPerson, DestinationMissing, ResponseOverdue,
  ResponseDocumentNotIssued}` (E157–E176); `Lending.MembershipWithdrawal.{NotSettled, EffectiveDateInvalid,
  SettlementDateInvalid, LossDeductionNotEnabled, OffsetRejected, NothingToRefund, AccountsClosed, ReopenConflict,
  InvalidTransition}` (E66, D44); `Lending.AssemblyDistribution.{Invalid, NotDraft, CorrectionInvalid, ContributionsNotActivated,
  NothingToApply, SupportRequired, PaymentNotReversible, PayoutsInProgress, ReversalWouldOverdraw}`
  (E183–E188); `Lending.DeceasedCase.{Invalid, InvalidTransition, NothingToDeliver, PendingCharges, SupportsRequired}` (E200,
  E201); `Lending.Unclaimed.{Invalid, NotFound, TransferFailed}`; `Lending.LiquidityFund.{RulesInvalid, Invalid, HoldingNotFound,
  HoldingEnded, SupportRequired, ScopeNotTotal}` (E210); `Lending.Regulatory.{PeriodInvalid, ScopeNotTotal, InversionSinTipo,
  UmbralSinValor}` (E219, E222); `Lending.Document.{NotAccountHolder, VoidedCannotReissue}` (E170, E171);
  `Lending.Migration.{PartNotAvailable, AccountNumberDuplicate, LineEquivalenceMissing, MinorWithoutRepresentative,
  AccountNotInLoad, ContractualInstallmentsDiffer, ContractualInstallmentNotInPlan}` (E115–E153);
  `Lending.Activation.LoadNotAvailable` (E31); `Lending.ApprovalPolicy.SubjectNotAllowed` (D53);
  `Payroll.AccountDeductionNotPostable`, `Payroll.AccountReversalRejected` (E59); `Accounting.LendingFact.CounterpartUnknown`
  (E107); `Accounting.LendingRule.LineNatureMismatch` (E24).
- **Requisitos de activación**: `LoansActivated`, `AccountsCollectorRegistered`, `PayrollConceptsReady`,
  `DeliveriesComplete`, `ProfileAllowsClasses`, `LinesWithMatrix`, `DepositRolesCreated` (D18, 2026-10-18).
- **Enums y valores de texto**: `RegulatoryFileKind.DepositsSicses`, `.Exogenous`; estados
  `AccountInstallment.status` (`Pending`, `PartiallyPaid`, `Paid`, `NotDeducted`), `AccountCharge.status`
  (`Collected`, `Pending`, `Waived`, `Reversed`), estados de solicitud de tasa negociada (`PendingDecision`,
  `Approved`, `Rejected`, `Withdrawn`, `Expired`), de embargo (`Registered` sin uso, `Active`, `Released`, `Rejected`; E157),
  de caso de fallecimiento (`Registered`, `Approved`, `Delivered`, `Cancelled`, `DeliveryPending`; E193), de no reclamado
  (`Held`, `Claimed`; E198), de distribución (`Simulated`, `Approved` sin uso, `Applied`, `Voided`, `Reversed`; E183);
  `LND_MigrationLoads.Nature`.
- **Alertas**: `Cartera.FondoDeLiquidezInsuficiente`; nacidas en el código: `Cartera.FondoDeLiquidezEnElMargen`,
  `Cartera.RiesgoDeLiquidezBajo` (E213), `Cartera.SobregiroTecnico` (E85), `Cartera.CausacionDeCaptacionesAtrasada` (E95),
  `Cartera.DevolucionRetenidaPorMinimo` (E71), `Cartera.ReversoTardioDeCruce` (D44).

## 23. Conciliación

| Punto | Decisión |
|---|---|
| Permisos `Accounts.Deposit` / `.Withdraw` sin ruta de efectivo | El dinero entra y sale por el contrato (FR-086); los dos permisos son **atribuciones con monto** que el contrato comprueba sobre el usuario que actúa en la forma interactiva (molde de `Payments.BackdateBeforeClose` de la 013), y `Withdraw` además guarda el retiro por giro de la pantalla |
| Crear líneas por `nature` en una sola ruta | Se partió en `/lines/deposit` y `/lines/contribution` para que el permiso sea fijo por ruta (la 013 §1.4) |
| `/accounts/{id}/charges` de research §0.6 | Son los cargos **causados** de la cuenta; el catálogo de la línea es `/lines/{id}/charges` y se edita en la versión de la línea |
| Carga de migración por naturaleza | Columna `Nature` en `LND_MigrationLoads` o tabla hermana: lo decide `data-model.md`; la ruta y su forma no cambian |
