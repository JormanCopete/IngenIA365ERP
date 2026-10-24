# Decisiones transversales — 015 Recaudo y Tesorería

**Feature**: 015 | **Fecha**: 2026-10-22, actualizado el 2026-10-23 (`/speckit-analyze` y decisiones del dueño) | Molde: `013/decisiones-transversales.md`

Este documento **manda** sobre cualquier otra redacción de nombres en los artefactos de la 015. Recoge los nombres
canónicos (§2), las decisiones transversales TR1–TR43 (§3), los choques entre frentes y cómo se resolvieron (§4) y las
preguntas que quedan para el dueño con su propuesta por defecto (§5). Las decisiones del dueño de `## Clarifications` de
la spec no se reabren.

## 1. Cómo leer este documento

- **TRn** es una decisión de diseño que atraviesa varios artefactos; su razón y alternativas están en
  [research.md](research.md) (R-nn citado).
- **CHn** es un choque entre lo que dicen dos frentes (spec, contratos de otras features, código) y cómo se resolvió.
- **Qn** es una pregunta abierta al dueño. Si no responde, **rige la propuesta**.

## 2. Nombres canónicos

### 2.1 Reglas

- Inglés para entidades, tablas, columnas, rutas, permisos y códigos de error; español para motores puros, puertos,
  mensajes, parámetros, alertas y pruebas (desviación registrada de la 013, T101).
- Módulos `Receipts` (código `REC`) y `Treasury` (código `TES`); prefijos `RCD_` y `TES_` (en la constitución desde la
  enmienda 2.0.1 del 2026-10-23; `TRS_` sólo para lo heredado hasta su retiro); rutas `/api/receipts/*`,
  `/api/treasury/*`; pantallas `/recaudo/*`, `/tesoreria/*`; permisos `Receipts.*`, `Treasury.*`.

### 2.2 Espacios de nombres y carpetas

| Capa | Recaudo | Tesorería |
|---|---|---|
| Entidades | `Domain/Entities/Receipts/{Configuration, Sessions, Vaults, Operations, Banks, Paymasters, Compliance, Activation}` | `Domain/Entities/Treasury/{BankAccounts, Payments, Checkbooks, Remittances, Statements, Reconciliation, Activation}` (lo heredado sigue en la raíz hasta T2) |
| Motores puros | `Domain/Receipts/{Imputacion, Arqueo, Cumplimiento, Huellas}` | `Domain/Treasury/{Resultados, Lotes, Conciliacion, Gmf}`; común `Domain/Common/Taxation/CalculoDeGmf` |
| Application | `Application/Receipts/{Setup, Sessions, Vaults, CashDesk, Voidings, Reversals, CreditBalances, Checks, BankItems, Paymasters, Compliance, Accounting, Activation, Imports, Reports, Integration}` | `Application/Treasury/{BankAccounts, LiquidityFund, Orders, Batches, PaymentFiles, Results, Checkbooks, Remittances, Statements, Reconciliation, Accounting, Activation, Imports, Reports, Integration}` |
| Puertos | `Application/Common/Integration/Receipts` | `Application/Common/Integration/Treasury` |
| Persistencia | `Persistence/Configurations/Receipts`, `Persistence/Receipts/CerrojoDeRecaudo.cs` | `Persistence/Configurations/Treasury/<Área>`, `Persistence/Treasury/CerrojoDeTesoreria.cs` |
| API | `API/Endpoints/Receipts/*`, `API/Endpoints/Reports/ReceiptsReportsEndpoints.cs` | `API/Endpoints/Treasury/<Área>Endpoints.cs`, `API/Endpoints/Reports/TreasuryReportsEndpoints.cs` |
| Pantallas | `Shared/Pages/Recaudo/*`, `Shared/Services/Recaudo/*Client.cs` | `Shared/Pages/Tesoreria/*` (nuevas), `Shared/Services/Tesoreria/*Client.cs` |
| Contabilidad | `Application/Accounting/Receipts/{Reglas, Contabilizacion, Lotes, Plantilla}` | `Application/Accounting/Treasury/{Reglas, Contabilizacion, Plantilla}` |

### 2.3 Tablas

- **Recaudo R0** (`RecaudoNucleo`): `RCD_Setup`, `RCD_CollectionPoints`, `RCD_CashBoxes`, `RCD_CashBoxCashiers`,
  `RCD_PaymentMeansCollectionPoints`, `RCD_DocumentTypes`, `RCD_MiscConcepts`, `RCD_ContingencyBooks`,
  `RCD_SupervisorDelegations`, `RCD_ChannelModalityRules`, `RCD_UserCollectionPointScopes`, `RCD_UserCashBoxScopes`,
  `RCD_UserPaymasterScopes`, `RCD_CollectionPointDays`, `RCD_CashSessions`, `RCD_CashCounts`, `RCD_CashCountLines`,
  `RCD_CashCountDenominations`, `RCD_CashCountTerminalBatches`, `RCD_CashCountReferences`, `RCD_CashMovements`,
  `RCD_CashMovementDenominations`, `RCD_CashSessionAdjustments`, `RCD_Vaults`, `RCD_VaultCustodians`,
  `RCD_VaultMovements`, `RCD_VaultMovementDenominations`, `RCD_VaultDenominationBalances`, `RCD_DayCloses`,
  `RCD_DayCloseLines`, `RCD_DailySquares`, `RCD_PayerControls`, `RCD_CashOperations`, `RCD_Receipts`,
  `RCD_ReceiptMeans`, `RCD_ReceiptApplications`, `RCD_ReceiptApplicationPortions`, `RCD_SourceOfFundsDeclarations`,
  `RCD_ReceiptVoidings`, `RCD_ModuleReversals`, `RCD_PayerCreditBalances`, `RCD_PayerCreditBalanceMovements`,
  `RCD_PayerReceivables`, `RCD_CashierReceivables`, `RCD_CashSurpluses`, `RCD_ReceivedChecks`,
  `RCD_ReceivedCheckEvents`, `RCD_CheckReturnCauses`, `RCD_BankItemIdentifications`, `RCD_UnidentifiedItems`,
  `RCD_UnidentifiedItemResolutions`, `RCD_ProcessingRuns`, `RCD_ProcessingRunLines`, `RCD_InboundMessages`,
  `RCD_PayrollDeductionFiles`, `RCD_PayrollDeductionFileLines`, `RCD_PayrollFileSendings`, `RCD_PaymasterResponses`,
  `RCD_PaymasterResponseLines`, `RCD_PaymasterRemittances`, `RCD_PaymasterRemittanceLines`, `RCD_CashTransactions`,
  `RCD_ComplianceAlertsLog`, `RCD_Activations` (64 tablas; `RCD_Activations` nace con la activación mínima del bloque 2,
  TR42).
- **Recaudo R1** (`RecaudoCompleto`): `RCD_CashTransactionReports`, `RCD_CashTransactionReportLines`,
  `RCD_CashDisbursements`, `RCD_CashDisbursementVoidings`, `RCD_PendingApplications`,
  `RCD_PendingApplicationResolutions`, `RCD_ReportedDeposits`, `RCD_IdentificationRules`,
  `RCD_CommercialReceivableApplications`, `RCD_PaymasterCalendar` (10).
- **Recaudo R2** (`RecaudoCanales`): `RCD_CollectionAgreements`, `RCD_CollectionAgreementVersions`,
  `RCD_PaymentReferences`, `RCD_AgreementFiles`, `RCD_AgreementFileLines`, `RCD_AgreementReconciliations`,
  `RCD_GatewayFiles`, `RCD_GatewayFileLines`, `RCD_ClearingBatches` (9).
- **Tesorería T0** (`TesoreriaNucleo`): `TES_Setup`, `TES_BankAccounts`, `TES_UserBankAccountScopes`,
  `TES_LiquidityFundUses`, `TES_InboundMessages`, `TES_PaymentOrders`, `TES_PaymentBatches`, `TES_PaymentBatchItems`,
  `TES_PaymentFiles`, `TES_PaymentFileLines`, `TES_PaymentResponses`, `TES_PaymentResponseLines`,
  `TES_DisbursementResults`, `TES_LendingDisbursementFiles`, `TES_LendingDisbursementFileOrders`, `TES_Checkbooks`,
  `TES_IssuedChecks`, `TES_IssuedCheckEvents`, `TES_CashRemittances`, `TES_BankStatements`, `TES_BankStatementLines`,
  `TES_Activations` (22).
- **Tesorería T0, conciliación mínima** (`TesoreriaConciliacionMinima`, par nuevo y aditivo posterior al congelamiento del
  10/11): `TES_Reconciliations`, `TES_ReconciliationMatches`, `TES_ReconcilingItems` (3).
- **Recaudo y Tesorería, puesta en marcha** (`RecaudoYTesoreriaPuestaEnMarcha`, par nuevo y aditivo posterior al
  10/11): `RCD_OpeningSquares`, `RCD_OpeningSquareLines`, `RCD_ReferenceFigures`, `TES_OpeningSquares`,
  `TES_OpeningSquareLines`, `TES_ReferenceFigures` (6). Con éstas, R0 sigue en 67 `RCD_` y T0 en 28 `TES_`.
- **Tesorería T1** (`TesoreriaCompleta`): `TES_ManualOrderConcepts`, `TES_BankAccountSigners`, `TES_BankAccountTokens`,
  `TES_Transfers`, `TES_PaymentVouchers`, `TES_MonthEndCheckRuns` (6).
- **Tesorería T2** (`TesoreriaAmpliada`): `TES_BankNotes`, `TES_ReconciliationRules`, `TES_AcquirerSettlements`,
  `TES_AcquirerSettlementLines`, `TES_CashFlowCategories`, `TES_CashFlowProjections`, `TES_LiquiditySnapshots`,
  `TES_LegacyPendingLoads` (8).
- **Plataforma** (`PlataformaParaRecaudoYTesoreria`): `COR_PersonBankAccounts`, `COR_GoLiveChecklists`,
  `COR_GoLiveChecklistItems` (3).
- **Contabilidad** (`IntegracionContableDeRecaudoYTesoreria`): `ACC_ReceiptsPostingRules`, `ACC_ReceiptsPostings`,
  `ACC_ReceiptsVoucherMappings`, `ACC_TreasuryPostingRules`, `ACC_TreasuryPostings`, `ACC_TreasuryVoucherMappings`,
  `ACC_OwnBankAccountLinks` (7).
- **Retiro** (después de T2): `TRS_Checks`, `TRS_Concepts`, `TRS_Invoices`.
- **Sin uso desde R0/T0** (E-04, TR41): `ACC_BankReconciliations`, `ACC_BankStatementLines`, `ACC_BankStatementColumnMaps`.
  No se borran en esta feature; su retiro, si se hace, es una migración destructiva aparte (pendiente).

### 2.4 Motores, puertos y servicios con nombre fijo

| Nombre | Qué | Dónde |
|---|---|---|
| `ImputacionDeMedios` | porciones aplicación × medio | Domain/Receipts/Imputacion |
| `ArqueoDeCaja` | esperado, contado, diferencia por medio | Domain/Receipts/Arqueo |
| `SenalesDeEfectivo` | acumulados y fraccionamiento | Domain/Receipts/Cumplimiento |
| `HuellaDeMovimientoBancario` | huella por formato | Domain/Receipts/Huellas (lo usa Tesorería por puerto) |
| `TransicionesDelResultado` | FR-215 | Domain/Treasury/Resultados |
| `ArmadoDeLote` | selección, pendientes, marcas, totales | Domain/Treasury/Lotes |
| `CruceDeConciliacion` | valor, fecha con holgura, referencia | Domain/Treasury/Conciliacion |
| `EvaluadorDeGmfDeEgresos` · `CalculoDeGmf` | GMF | Domain/Treasury/Gmf · Domain/Common/Taxation |
| `CerrojoDeRecaudo` · `CerrojoDeTesoreria` | orden fijo | Persistence |
| `NumeradorDeRecaudo` · `NumeradorDeTesoreria` | numeración al final de la confirmación; en el recibo, después de los cerrojos de Recaudo y antes de los contratos (§2.7) | Application |
| `DestinoRecaudo` · `DestinoTesoreria` | consumidores de `Receipts` y `Treasury` | Application/{Receipts,Treasury}/Integration |
| `ConfirmacionDeRecibo` | el flujo único de confirmación (validación → listas → validación previa → cerrojos → numeración → contratos → hechos → un `SaveChanges`; §2.7) | Application/Receipts/CashDesk |
| `AplicacionNoInteractiva` | línea por transacción con su clave | Application/Receipts/BankItems |
| `RequisitosDeActivacionDeRecaudo` · `…DeTesoreria` | requisitos verificables | Application/*/Activation |
| `AlcanceDeRecaudoDeLaPeticion` · `AlcanceDeTesoreriaDeLaPeticion` | alcance | API/Services |
| `ValidadorDeOrdenes` | FR-109, FR-203, FR-210 | Application/Treasury/Orders |
| Puertos | `ICuentasBancariasPropias`, `IMovimientosBancariosParaRecaudo`, `IRemesasDeTesoreria`, `IOrdenesDePagoDeTesoreria`, `IRecaudoParaConciliacion`, `IPartidasDesdeConciliacion`, `IResultadoDeOrdenesDeRecaudo`, `IContabilidadParaRecaudo`, `IContabilidadParaTesoreria`, `IDimensionesDeRecaudo`, `IDimensionesDeTesoreria`, `IEstadoOperativoParaElCierre`, `IConciliacionBancariaParaContabilidad`, `ICuentasBancariasDePersona`, `IVerificacionDeSalida`, `IConsultaDeCargaSinActivar` (R1), `IEfectivoDelPuntoDeVenta` (R1), `IReferenciasDePago` (R2) | [contracts/interfaces.md](contracts/interfaces.md) |

### 2.5 Tareas programadas (sólo proponen, preparan y alertan, FR-174)

| Tarea | Hora | Qué |
|---|---|---|
| `recaudo.lote-del-dia` | tras la hora de corte | dispara el lote contable resumido de recibos (lo hace Contabilidad) y deja su marca de corrida; el orden con Cartera es la restricción de FR-185 (TR43), no una dependencia |
| `recaudo.cuadre-diario` | después del lote | `RCD_DailySquares` y alerta |
| `recaudo.sesiones-olvidadas` | hora de corte | marca `ForgottenOpen` y alerta |
| `recaudo.antiguedad` | diaria | partidas, saldos a favor, extractos sin cargar, contingencia por digitar, cuadre provisional, política en «advertir» |
| `tesoreria.vigilancia` | diaria | órdenes sin resultado, cheques sin cobrar, remesas sin confirmar, partidas conciliatorias antiguas, cuadre provisional, política en «advertir» |

El orden con el ciclo diario de Cartera (lote de Recaudo → cuadre diario → ciclo diario de Cartera, FR-185) **no** lo
coordina `ProgramadorDeTareas`, que no declara dependencias entre tareas: es una **restricción de configuración
comprobada** (TR43). `Recaudo.HoraDeCorte` tiene que caer antes de `Lending:Process:StartTimeLocal` (00:30) en la línea
del día de operación (un inicio de madrugada se cuenta en el día siguiente); una versión del parámetro que no la cumpla
se rechaza con `Receipts.Setup.CutoffAfterLendingCycle`, y la activación y la lista de go/no-go la verifican. Dentro de
Recaudo, el cuadre diario de una cooperativa no corre hasta que el lote de ese día terminó (marca de corrida del lote).
Ninguna tarea llama a los contratos ni registra resultados (`NingunProcesoAplicaPagosNiInformaResultados`).

### 2.6 Permisos (sembrados por la API al arrancar)

`Receipts.Setup.View/Manage`, `Receipts.Scope.All/Manage`, `Receipts.Session.Open/View/ViewExpected/Move/Reclassify/
Close/CloseForgotten/Adjust/SurpriseCount`, `Receipts.Count.ApproveDifference`, `Receipts.Day.Close`,
`Receipts.Vault.View/Move/Remit`, `Receipts.Receipt.Register/RegisterContingency/View/Print/Backdate`,
`Receipts.Voiding.Request/Approve`, `Receipts.Reversals.View/Resolve`, `Receipts.CreditBalances.View/Resolve`,
`Receipts.Checks.View/Manage/Return`, `Receipts.BankItems.View/Identify/Classify`,
`Receipts.UnidentifiedItems.View/Resolve/MoveToIncome`, `Receipts.Paymasters.View/ConfirmSent/LoadResponse/
RegisterRemittance/ApplyRemittance`, `Receipts.Compliance.View/Release`, `Receipts.Sarlaft.Report`,
`Receipts.Accounting.PolicyChange`, `Receipts.Activation.View/Activate/Square/ProvisionalSquare`, `Receipts.Imports.Run`,
`Receipts.Reports.View/Export`, `Receipts.Disbursements.*` (R1), `Receipts.Agreements.*` (R2);
`Treasury.BankAccounts.View/Manage`, `Treasury.BankBalances.View`, `Treasury.Scope.All/Manage` (`All` por decisión del dueño del 2026-10-23, §2.7),
`Treasury.LiquidityFund.Request/Approve`, `Treasury.Orders.View/Manage`, `Treasury.Batches.Prepare/Approve`,
`Treasury.PaymentFiles.Generate/Download/MarkSent/Void`, `Treasury.Results.Register`, `Treasury.Checkbooks.View/Manage`,
`Treasury.Checks.View/Deliver/Void/Print`, `Treasury.Remittances.View/Confirm`, `Treasury.Statements.Load/View`,
`Treasury.Reconciliation.View/Work/Close/Reopen`, `Treasury.Accounting.PolicyChange`,
`Treasury.Activation.View/Activate/Square/ProvisionalSquare`, `Treasury.Imports.Run`, `Treasury.Reports.View/Export`,
`Treasury.ManualOrders.*`, `Treasury.Transfers.*`, `Treasury.Signers.*` (T1), `Treasury.BankNotes.*`,
`Treasury.Payables.*` (T2); `Core.PersonBankAccounts.View/Request/Approve`; `Platform.GoLive.View/Manage/Override`;
`Accounting.ReceiptsMatrix.View/Manage`, `Accounting.TreasuryMatrix.View/Manage`. Los que llevan monto
(`SEC_PermissionAmountLimits`): `Receipts.Voiding.Approve`, `Receipts.Count.ApproveDifference`,
`Receipts.Receipt.Backdate`, `Treasury.Batches.Approve`, `Treasury.LiquidityFund.Approve`, `Treasury.Transfers.Approve`.

### 2.7 Nombres fijados después del diseño (2026-10-23)

Lo que tasks.md marcaba **(nuevo)** y lo que trajeron el `/speckit-analyze` y las decisiones del dueño:

| Nombre | Qué | Dónde |
|---|---|---|
| `PaymentBatchState` | `Draft = 1`, `PendingApproval = 2`, `Approved = 3`, `FileGenerated = 4`, `Sent = 5`, `PartiallyConfirmed = 6`, `Confirmed = 7`, `RejectedByBank = 8`, `Voided = 9` | data-model §10.3, §20 |
| `ReconciliationState.Outdated = 4` | la conciliación cerrada de un mes cuyo período contable se reabrió | data-model §12.2, §20 |
| `ScreeningContext.Collection = 6` · `.Disbursement = 7` | contextos de listas (1 a 5 ya existen) en `Domain/Enums/Lending/OriginationEnums.cs` | data-model §16.3, §20 |
| `Lending.Payment.BankItemDateMismatch` | v2 del contrato de obligaciones, `ValueDate ≠ MovementDate` con la partida citada (R1) | contratos-v2.md §1.2; data-model §21 |
| `Receipts.Setup.CutoffAfterLendingCycle` | versión de `Recaudo.HoraDeCorte` que no cae antes del ciclo de Cartera (TR43) | data-model §21 |
| `428` · `Approvals.PresenceRequired` · encabezado `X-Approval-Request` | la caja pide el aprobador presente y la operación se repite con la misma clave; las fallas del desafío reusan los `Approvals.Presence.*` de la 012 (`NotRequester`, `Invalid`, `Expired`, `TotpReused`) | api.md §1.4; data-model §21 |
| `AdoptContractVersionCommand` | Recaudo adopta las v2 de los contratos (R1) | `Application/Receipts/Activation` |
| `IReferenciasDePago` | referencia de pago estable para los documentos al asociado (R2) | `Application/Common/Integration/Receipts` |
| `IConciliacionBancariaParaContabilidad` | Tesorería ofrece, Contabilidad usa: la reapertura de un período marca desactualizadas las conciliaciones cerradas del mes (TR41) | interfaces.md §3 |
| `TomarCerrojosAsync` | en `IResultadoDeOrdenesDeRecaudo` e `IPartidasDesdeConciliacion`: Tesorería toma el nivel de Recaudo antes de sus propios cerrojos (CH16) | interfaces.md §2; data-model §0.5 |
| `Receipts.ProcessingRun.Summary` | evento resumen de una corrida no interactiva con `receipts[] { publicId, sha256 }` (huella `HuellaDeRecibo`) y `rejectedLines` (TR37) | data-model §5.3 |
| vista `parallel-comparison` | informe del paralelo contra las cifras de referencia | api.md §17 |
| `TesoreriaConciliacionMinima` · `RecaudoYTesoreriaPuestaEnMarcha` | pares nuevos y aditivos posteriores al congelamiento del 10/11 | data-model §25 |
| `NadieEscribeLaConciliacionBancariaDeContabilidad` | prueba de arquitectura: ningún tipo escribe `ACC_BankReconciliations`, `ACC_BankStatementLines` ni `ACC_BankStatementColumnMaps` | tests/IngenIA365ERP.Architecture.Tests/Principles |
| `IsMinimal` | columna de `RCD_Activations` y `TES_Activations`: activación mínima del bloque 2 (TR42) | data-model §15.1 |
| `CajaDeHumoTests` | e2e de humo de la caja, excepción declarada a «e2e una sola vez» | tests/IngenIA365ERP.API.IntegrationTests/Receipts |
| `docs/operaciones/recaudo-tesoreria-despliegue.md` | runbook de despliegue de R0/T0 y de cada entrega posterior | tasks.md T0326 |
| `DevolucionPorPagar` | rol existente de Captaciones (013/014) que salda el giro de una devolución (no `RefundPayable`) | contabilidad.md §1, §4 |

**Fijados en la construcción de la fase 2 (2026-10-24)**. Los aprobó el dueño ese día:

| Nombre | Qué | Dónde |
|---|---|---|
| `UK_COR_Sequences_Name_RcdTes` **(nuevo)** | la numeración de Recaudo y Tesorería vive en `COR_Sequences` con la clave en `Name` (`RCD:{código}:{prefijo}`, `TES:{código}`), el módulo en `DocumentType` y el prefijo en `ShortName`. Un índice único filtrado a `DocumentType IN ('RCD','TES')` deja al cerrojo crear la fila que falta sin numerar dos veces; las filas heredadas de `sys_consecu` quedan fuera del filtro | `SequenceConfiguration`; migración `PlataformaParaRecaudoYTesoreria` |
| `OrdenDeCerrojos` | lleva, por unidad de trabajo, los niveles ya tomados por los dos cerrojos y lanza si llegan fuera del orden de data-model §0.5 (también una fila `RCD_` después de una `TES_`, CH16) | `Application/Receipts/Common` |
| `ModuloActivoBehavior` · `IEscrituraDeRecaudo` · `IEscrituraDeTesoreria` | la guarda de módulo activo como comportamiento de MediatR, no como filtro de ruta. La escritura de negocio marcada responde `Receipts.NotActivated` o `Treasury.NotActivated`; la configuración y la puesta en marcha no se marcan | `Application/Common/Behaviors` |
| `ModuloDeAuditoria.Receipts` · `ModuloDeAuditoria.GoLive` | módulos encadenados con diez años de conservación. Las cuentas bancarias de la persona (`.Core.PersonBankAccounts`) se auditan en `Treasury`. `Conservacion.Anios` se registra con piso de 5, pero el TTL del flujo sigue fijo en diez años. **Decisión del dueño del 2026-10-24**: en R0/T0 la auditoría conserva diez años fijos; que el TTL lea el parámetro queda para **R1** | `Application/Common/Audit` |
| `EntregaDeRecaudo` (`R0`, `R1`, `R2`) · `EntregaDeTesoreria` (`T0`, `T1`, `T2`) | las líneas de entregas `RCD` y `TES` de los parámetros | `Domain/Receipts`, `Domain/Treasury` |
| `CatalogoDeParametros.ModulosConRutaPropia` | `LND`, `RCD` y `TES` no aparecen en la ruta de parámetros de Inventario | `Domain/Common/Parametros` |
| `FormatosGenericosDeRecaudoYTesoreria` | los cuatro genéricos de §4 de archivos-y-plantillas. Los siembra `BankFileFormatsSeeder`, que es un `IDataSeeder` paramétrico idempotente por código, y no la migración. `RESPUESTA-PAGADURIA-GENERICA` es CSV, no XLSX: el motor común sólo lee archivos planos. El código admite 20 caracteres: `RESP-PAGOS-GENERICO` y `RESP-PAGADURIA-GEN` | `Application/Common/BankFiles` |
| `BankFieldSource` 100–111 y 120–125 | los orígenes de lectura del extracto y de la respuesta a pagos (archivos-y-plantillas §2); la referencia del movimiento se llama `MovementReference` porque `Reference` ya era sinónimo de `BatchReference` | `Domain/Enums/Core/BankFiles.cs` |
| `PerfilesSugeridos.ModuloRecaudo` · `.ModuloTesoreria` | las siete plantillas de rol viven en `PerfilesSugeridos`, como las de Cartera, y no en el sembrador de permisos | `Application/Security/Roles/Plantillas` |
| `ReceiptsAndTreasuryPermissionCatalogSeeder.LecturasSensibles` | `Core.PersonBankAccounts.View`, `Treasury.BankBalances.View` y `Receipts.Session.ViewExpected` no van en el `*.View` de Operator, ReadOnly ni Auditor | `Identity/Seed` |
| Claves `CuentasDestino.*` y `Contabilidad.CierreExigeConciliaciones` | van en el módulo de parámetros `TES`, que es quien las usa, en vez de abrir un módulo «plataforma» o `ACC` sin ruta ni permiso | `ParametrosDeRecaudoYTesoreria` |
| Copia de `Person.SupplierBank*` | sólo se copia la cuenta cuyo código de banco casa con `COR_Banks.TransferCode` o `LegacyCode`; las demás siguen en la persona | migración `PlataformaParaRecaudoYTesoreria` |

**Fijados en la construcción de las fases 3 y 4 (2026-10-24) y decisiones del dueño del 2026-10-23**:

| Nombre | Qué | Dónde |
|---|---|---|
| `Treasury.Scope.All` | **decisión del dueño**: alcance total de Tesorería sin filas de asignación, como `Receipts.Scope.All`; lo trae el administrador de la cooperativa (que recibe todo el catálogo) | `ReceiptsAndTreasuryPermissionCatalogSeeder`; `AlcanceDeTesoreriaDelActor` |
| Numeración del recibo antes de los contratos | **decisión del dueño**: el recibo se numera después de los cerrojos propios de Recaudo (niveles 1 a 6) y antes de llamar a los contratos, porque `ReferenciaDeRecibo.Number` queda guardado en la operación de Cartera y Captaciones. En la misma transacción: si algo falla, el número no se consume. Los demás documentos numeran al final | `ConfirmacionDeRecibo`; data-model §0.5 |
| `GET /api/receipts/users` · `ListReceiptsUsersQuery` | **decisión del dueño**: los usuarios (id público y nombre) que eligen las pantallas de configuración de Recaudo, con `Receipts.Setup.View`, en vez de `/api/admin/users` | `Application/Receipts/Security` |
| `Conservacion.Anios` | **decisión del dueño**: diez años fijos en R0/T0; leer el parámetro queda para R1 | `Application/Common/Audit` |
| `IAlcanceDeRecaudo` · `AlcanceDeRecaudo` · `IAlcanceDeTesoreria` · `AlcanceDeTesoreria` | los alcances que fallan cerrado; el cajero vigente de una caja la tiene en su alcance sin otra fila | `Application/Common/Scope` |
| `PersonasDeLosUsuarios` | la comparación por persona que comparten las dos segregaciones | `Application/Common/Security` |
| `ApprovalSubjects.{CashCountDifference, CashMeansReclassification, ReceiptBackdate, CashSessionAdjustment, ReceiptVoiding, PaymentBatchApproval, LiquidityFundUse}` · `ApprovalSourceTypes.{ReceiptsOperation, TreasuryDocument}` | sujetos y fuentes de aprobación, con regla fija de un nivel sin política | `Domain/Approvals` |
| `POST /api/receipts/approvals/{id}/presence-challenge` · `/decide` | el desafío y la decisión del aprobador presente desde la caja, con `Receipts.Session.Open` (las de la 012 piden `Inventory.Approvals.View`) | `API/Endpoints/Receipts/ScopeEndpoints.cs` |
| `Receipts.Approval.{Rejected, Mismatch, SelfApproval, ApproverLimitTooLow}` · `Treasury.Approval.{Pending, ApproverLimitTooLow}` · `Treasury.Result.ThirdPersonRequired` | errores de aprobación y segregación | `ErroresDeRecaudo`, `ErroresDeTesoreria`, fuentes |
| `Receipts.PaymentMeans.{Inactive, AlreadyOffered}` · `Receipts.Cashier.{PersonRequired, Overlaps}` · `Receipts.CashBox.{MainAlreadyExists, CapsInvalid}` · `Receipts.DocumentType.KindTaken` · `Receipts.MiscConcept.SystemConcept` · `Receipts.ContingencyBook.Overlaps` · `Receipts.Delegation.SamePerson` · `Receipts.Vault.{AlreadyExists, MainCashBoxRequired, CustodiansRequired}` | errores de la configuración de Recaudo | `ErroresDeRecaudo.Configuracion` |
| `ReceiptsCatalogStatus` · `CashBoxBaseMode` · `VaultMode` · `MiscConceptDirection` · `ContingencyBookState` | enumeraciones que data-model §20 no numeraba | `Domain/Enums/Receipts` |
| `ChannelModalityRule.OnlyWhenInstallmentsDue` | la regla de cuentas de D38 («cuotas si hay exigibles, si no libre») como dos filas por prioridad | `RCD_ChannelModalityRules` |
| catálogos `puntos-de-recaudo`, `cajas-de-recaudo`, `conceptos-varios`, `tipos-de-recibo` | duplicado de código antes de guardar | `BuscarCodigoDeCatalogoQuery` |
| tipos de documento sembrados `RC`, `RB`, `RP`, `RT`, `AN`, `MC`, `AS` · conceptos `GIRODEV`, `SOBRANTE`, `REINTEGRO`, `RENDIM` | semilla de `RecaudoNucleo` (T0075) | migración |

**Fijados en la construcción de la fase 5 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `ReceiptsCashSessions` · `ReceiptsCashCounts` · `ReceiptsCashCountLines` · `ReceiptsCashCountDenominations` · `ReceiptsCashCountTerminalBatches` · `ReceiptsDayCloses` · `ReceiptsDayCloseLines` | los `DbSet` de Recaudo cuyas clases se llaman como las del POS de la 012; las tablas son las `RCD_` | `IApplicationDbContext` |
| `IX_RCD_CashSessions_CashierUserId_Live` | la de una sesión por cajero no es índice único: depende de `Caja.UnaSesionPorCajero` (parámetro con vigencia) y la comprueba la apertura | `CashSessionConfiguration` |
| `CashCount.VerificationCode` · `CashSurplus.ToIncomeFrom` nulo | el código del informe del cierre y del acta; sin `Caja.Sobrantes.DiasAntesDeIngreso` el sobrante no tiene fecha de paso a ingreso | `Entities/Receipts/Sessions` |
| `CatalogoDeMensajesDeRecaudoV1` · `MCR` `MRC` `DAA` `ASR` `SLI` · `ReceiptsFactDimensionsV1` · `FactLineV1` · `RolesDeRecaudo` | el catálogo de hechos `REC → Accounting` y su forma común | `Application/Common/Integration/Contracts/Receipts` |
| `EmisionDeRecaudo` | único emisor de hechos de Recaudo; sin contabilidad iniciada `NotPosted`, con `Contabilidad.ModoDePaso = NoPasa` también, si no en línea; código de oficina = `LegacyCode` o su Id | `Application/Receipts/Accounting` |
| `ContabilidadParaRecaudo` (provisional) | estado del período real, validación previa sin matriz (`SinMatriz`) y saldo contable vacío, hasta la fase 16 | `Application/Accounting/Receipts` |
| `ValoresSelladosDeLaSesion` · `MedioSellado` | lo que la sesión sella al abrir (R-14) | `Application/Receipts/Sessions` |
| `ArqueosDeSesion` · `SesionesDeCaja` · `MovimientosDeBoveda` · `TablasDeRecaudo` | el arqueo con `ArqueoDeCaja`, lo esperado y su proyección, el saldo de la bóveda y su verificación, los nombres de tabla del cerrojo | `Application/Receipts` |
| `MovimientosDeBoveda.PrefijoDeBoveda = "BV"` | los movimientos de bóveda numeran con el tipo `MC` en otra fila de `COR_Sequences` | `Application/Receipts/Vaults` |
| `MarcaDelLoteDelDia` (`RCD.LoteDelDia.Terminado`) | la marca de corrida que espera el cuadre diario; la deja `recaudo.lote-del-dia` (T0263) | `Application/Receipts/Sessions/Tareas` |
| `recaudo.sesiones-olvidadas` (cada 15 min) · `recaudo.cuadre-diario` (cada 30 min) | deciden con la hora de corte de la cooperativa; sólo marcan, comparan y alertan | `TareasDeCaja.cs` |
| `AdjuntosDeModulo.SoporteDeCaja` (`ReceiptsCashSupport`) | soportes y actas de la caja: dueño la sesión o la bóveda, no se borran | `Application/Attachments/Common` |
| 428 `Approvals.PresenceRequired` con `data.contentSha256` | la huella que el aprobador presente confirma en `…/decide` | `ErroresDeRecaudo.Seguridad` |
| `Receipts.Session.{NotCashierOfBox, CashBoxInactive, NoMeansOffered, BaseMismatch, NotClosed, NotForgotten, WitnessRequired}` · `Receipts.Count.{ReasonRequired, NotPending, ApproverLimitTooLow, TreatmentInvalid}` · `Receipts.Day.AlreadyClosed` · `Receipts.Movement.{DoubleCustodyRequired, InsufficientCash, Invalid, SupportRequired}` · `Receipts.Vault.{InsufficientBalance, MinutesRequired, Inactive}` · `Receipts.Receivable.AuthorizationRequired` · `Receipts.Surplus.{NotDue, NotLiability}` | errores de la sesión, el arqueo y la bóveda; 409 para `.CashierExclusive` y `.Day.AlreadyClosed` | `ErroresDeRecaudo.Sesiones` |
| `GET /sessions/{id}/counts` · `GET /sessions/{id}/close-report` · `GET /surprise-counts/{id}/minutes` · `GET /collection-points/{id}/days/{date}/report` · `GET/POST /cashier-receivables[/{id}/payroll-authorization]` · `GET/POST /cash-surpluses[/{id}/to-income]` · `POST /daily-squares/{id}/explain` | rutas que api.md §3 no listaba | `API/Endpoints/Receipts/SessionsEndpoints.cs` |
| `CashCountKind` · `CollectionPointDayStatus` · `SessionAdjustmentKind` · `DailySquareStatus` · `CashierReceivableState` · `CashSurplusState` · `VaultMovementKind.RetainedCounterfeit = 6` | enumeraciones que data-model §20 no numeraba | `Domain/Enums/Receipts` |

**Fijados en la construcción de la fase 6 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `[ColumnaMutable]` | atributo de las columnas que data-model declara mutables en un hecho (el estado del recibo, del medio, de la aplicación, de la porción); `GuardaDeInmutabilidad` lo admite y sólo lo usan Recaudo y Tesorería | `Domain/Common` |
| `IssuedReceipts` | el `DbSet` de `RCD_Receipts` (un miembro `Receipts` chocaba con el espacio `Application.Receipts`) | `IApplicationDbContext` |
| `ReceiptMeans.MeansClass` como `PaymentMeansClass` | los números 1 a 9 y 99 son los de `ClaseDeMedioDeRecaudo` (`ClavesDeRecaudo.ClaseDelContrato`) | `Entities/Receipts/Operations` |
| `ClavesDeRecaudo` (`REC:{recibo:N}:{aplicación:N}:{clase}[-n]`) · `HuellaDeRecibo` · `ClavesDeRecaudo.Recaudador` | la clave de cada porción (con `-n` desde el segundo medio de la misma clase en la misma aplicación) y la huella de lo confirmado | `Application/Receipts/Common` |
| `IListasDeRecaudo` · `ListasDeRecaudoSinConsulta` | el gancho de listas de la confirmación; responde `Clear` hasta la fase 15 | `Application/Receipts/Common` |
| `ConfirmacionDeRecibo` | el único camino del dinero que entra por la caja; numera antes de los contratos; guarda por pasos en la misma transacción | `Application/Receipts/CashDesk` |
| `ReciboEmitido` (`RCE`) · `REC.RCE\|CierreDeTurno\|\|PorDia` · roles `PuenteDelModulo`, `ConceptoVario` | el hecho del recibo y su lote por defecto con alcance la sesión | `Contracts/Receipts`, `EmisionDeRecaudo.Recibos.cs` |
| `Receipts.CashOperation.{NotPreparing, NoItems, Withheld}` · `Receipts.Receipt.{AdvanceNoticeRequired, ImputationInvalid, MiscConceptInvalid, CrossDocumentRequired, ThirdPartyRequired, MeansNotOffered}` · `Receipts.Accounting.NotPostable` · `Receipts.Payables.PersonNotFound` | errores de la operación de caja y el recibo; `DistributionChanged` responde 409 | `ErroresDeRecaudo.Recibos` |
| `GET /cash-operations/{id}` · `GET /receipts` · `GET /receipts/{id}` | rutas que api.md §4 no listaba; todas en `CashDeskEndpoints.cs` | `API/Endpoints/Receipts` |
| `ReceiptMeansState` · `PortionState` · `SourceOfFundsRequiredBy` | enumeraciones que data-model §20 no numeraba | `Domain/Enums/Receipts` |
| **pendiente del dueño**: buscar por número de obligación · cuándo es obligatorio «quien efectúa» | la búsqueda por número necesita un puerto de Cartera (hoy sólo por persona); la regla de `DeliveredByRequired` no está escrita | T0113, T0115 |

**Fijados en la construcción de la fase 7 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `OwnBankAccount.Code` · `.Name` · catálogo `cuentas-bancarias-propias` | la nomenclatura de la cuenta propia (`CodigoDeCatalogo`, dimensión `OwnBankAccountCode`) y su nombre | `TES_BankAccounts` |
| `OwnBankAccountType` · `TreasuryCatalogStatus` · `LiquidityFundCause` · `LiquidityFundUseState` · `PaymentBatchItemFlags` · `PaymentBatchItemState` · `PaymentResponseSource` · `PaymentResponseOutcome` | enumeraciones que data-model §20 no numeraba | `Domain/Enums/Treasury/TreasuryPaymentEnums.cs` |
| `InboundMessageOutcome` | `Processed = 1`, `Rejected = 2`: el resultado del recibo de `TES_InboundMessages` y `RCD_InboundMessages` | `Domain/Enums/Integration` |
| `PaymentOrder.{BeneficiaryName, BeneficiaryTaxIdType, BeneficiaryTaxId, DestinationBankCode, BranchPublicId, BeneficiaryIsDebtor, VoidReason, VoidedByCentralUserId, VoidedByName, ReceivedAt, ValidatedAt, Flags}` | lo que la orden sella del mensaje (con ello se escribe el archivo, se coteja la respuesta, se exime el GMF y se nombra a quien anuló) | `TES_PaymentOrders` |
| `PaymentBatchItem.IsLive` · `UK_TES_PaymentBatchItems_PaymentOrderId_Live` | «una orden en a lo sumo un lote vigente» (FR-116) como índice único filtrado `[IsLive] = 1` | `TES_PaymentBatchItems` |
| `PaymentBatch.{PayDate, LiquidityFundUseId, SubmittedAt, ApprovedAt, SentAt, SentByUserId, UploadedAtBankBy, AuthorizedAtBankBy, Note}` · `PaymentFile.{FileName, SentByUserId, RejectedByBankReason}` · `PaymentFileLine.RecordText` | columnas que data-model §10 no listaba | `TES_PaymentBatches`, `TES_PaymentFiles` |
| clases de numeración `OP` (orden), `LP` (lote), `UF` (uso del fondo) | `TES:{código}` en `COR_Sequences` | `NumeradorDeTesoreria` |
| `IContabilidadParaTesoreria.EstadoDelPeriodoAsync` · `VincularCuentaBancariaAsync → Task<Result>` | la fecha contable de los hechos de Tesorería y el vínculo que rechaza sin excepción | `Common/Integration/Accounting`, `Accounting/Treasury/ContabilidadParaTesoreria` |
| `Accounting.OwnBankAccount.NotBankAccount` | la cuenta contable elegida no es bancaria de movimiento activa | `ContabilidadParaTesoreria` |
| `GET /api/treasury/bank-accounts/{id}` · `/bank-accounts/chart-accounts?bank=` · `GET /payment-batches` · `GET /payment-batches/{id}` · `POST /payment-batches/{id}/sent` · `GET /payment-files` | rutas que api.md §9 y §10 no listaban | `BankAccountsEndpoints.cs`, `PaymentsEndpoints.cs` |
| `MarkPaymentBatchSentByPortalCommand` · `DownloadPaymentFileCommand` · `ListChartBankAccountsQuery` | comandos y consultas nuevos de US10 | `Application/Treasury` |
| `Treasury.BankAccount.{Duplicate, Inactive, PurposeRequired}` · `Treasury.LiquidityFund.{NotLiquidityFundAccount, NotPending, SelfApproval}` · `Treasury.Order.{InvalidForm, BeneficiaryNotFound, DestinationMissing, NotReady, InvalidState, UnknownOrder}` · `Treasury.Batch.{NotDraft, NotPendingApproval, Empty, FormatNotFound, NoFileForm, SourceNotPayments, SelfReview}` · `Treasury.File.{Voided, Tampered, Missing, LineTooLong, BankActorsRequired}` | errores de US10 (y la revisión de US26) | `ErroresDeTesoreria.Pagos.cs` |
| `IListasDeTesoreria` · `ListasDeTesoreria` | la consulta a listas (contexto `Disbursement`) que sólo lee; `Match` = vinculante, `PossibleMatch` = no vinculante (pendiente del dueño con el oficial) | `Application/Treasury/Orders` |
| `GuardaDelFondoDeLiquidez` · `AprobableUsoDelFondo` · `AprobableLoteDePago` · `GuardasDeAprobacionDelLote` · `LotesDePago` · `LineasDelArchivo` · `RegistroDeResultadosDelGiro` | las piezas de US10 con nombre propio | `Application/Treasury` |
| `ConsumidoresDeRecaudoYTesoreria` | la bandera transitoria (T0141): registra `DestinoTesoreria` (y `DestinoRecaudo`) sólo si los dobles no se registran | `API/Integration/DoblesDePruebaDeCartera.cs` |
| `CatalogoDeMensajesDeTesoreriaV1` · `GMP` (`GmfDePagoCausado`) · `POG` (`PagoDeOrdenGirado`) · `TreasuryFactDimensionsV1` · `TreasuryFactLineV1` · `RolesDeTesoreria` | el catálogo de hechos `TES → Accounting` y su forma | `Contracts/Treasury`, `Treasury/Accounting/EmisionDeTesoreria.cs` |
| `RecaudoNoConoceContabilidad` · `TesoreriaNoConoceContabilidad` | dejan fuera `Application.{Receipts,Treasury}.Accounting` (el emisor de hechos del propio módulo) | `tests/IngenIA365ERP.Architecture.Tests/Principles` |

## 3. Decisiones transversales

### Módulos y frontera

- **TR1** · Dos módulos `Receipts`/`Treasury`, códigos `REC`/`TES`, prefijos `RCD_`/`TES_` por la enmienda 2.0.1 de la
  constitución (R-02, R-03).
- **TR2** · Lo heredado de Tesorería no se toca hasta T2; lo nuevo en subcarpetas y subrutas; el cheque girado se llama
  `IssuedCheck` (R-04).
- **TR3** · Recaudo y Tesorería se hablan por puertos síncronos en la misma transacción; ninguno toca las tablas del otro
  (R-05).
- **TR4** · El extracto es de Tesorería y se carga una vez; Recaudo identifica sobre sus líneas (R-06).

### Caja y recibo

- **TR5** · Operación de caja con `PublicId` estable que será el del recibo; el recibo nace al confirmar (R-07).
- **TR6** · Un lote por contrato, obligaciones antes que cuentas, dentro de la transacción de Recaudo (R-08).
- **TR7** · Clave por porción `REC:{recibo:N}:{aplicación:N}:{clase}`; pagaduría `REC:{giro:N}:{línea:N}` (R-09).
- **TR8** · Huella de negocio sin simulación, aprobador ni instante; claves sin vencimiento (R-10).
- **TR9** · Imputación de medios pura y configurable; el cheque al final (R-11).
- **TR10** · Cerrojos en orden fijo: Recaudo → Cartera → Captaciones → Tesorería → numeración (R-12). Vale en las dos
  direcciones: cuando Tesorería llama a Recaudo (`IResultadoDeOrdenesDeRecaudo`, `IPartidasDesdeConciliacion`), toma
  antes el nivel de Recaudo por `TomarCerrojosAsync` y sólo después los suyos (CH16).
- **TR11** · Numeración al final, `COR_Sequences` (R-13); en el recibo, la numeración del recibo va después de los cerrojos propios de Recaudo y **antes** de los contratos (decisión del dueño del 2026-10-23: el contrato guarda el número del recibo en su operación); en la misma transacción, un rechazo no consume número.
- **TR12** · Sesión con valores sellados al abrir; arqueo puro; un cierre no se reabre (R-14).
- **TR13** · Bóveda de Recaudo; remesa de Tesorería registrada por puerto en la misma transacción (R-15).

### Bancos, pagadurías y procesos

- **TR14** · Recaudo consume `DescuentosPorPagaduria` v1 y v2 y emite envío y resultado sin cambiarlos (R-16).
- **TR15** · En lo no interactivo la unidad es la línea, a nombre de quien confirma (R-17).
- **TR16** · Ningún proceso aplica pagos ni informa resultados; las tareas sólo proponen, preparan y alertan (FR-174).

### Tesorería

- **TR17** · Bandeja única de órdenes con validación de cuenta destino, banco y listas; una orden en a lo sumo un lote
  vigente (R-18, FR-116).
- **TR18** · `ResultadoDelGiro` v1 sin cambios; transiciones fijas por motor puro; lo registra una persona (R-18).
- **TR19** · El hecho de pago nace al confirmar el banco (transferencia) o al emitir (cheque) (R-19).
- **TR20** · Archivo de pago por el motor común (ámbito 3 renombrado), adjunto no borrable, descarga por otra persona,
  cotejo de la respuesta (R-20).
- **TR21** · Cuentas destino en `COR_PersonBankAccounts` con doble control y espera configurable sin valor sembrado (R-21).
- **TR22** · Fondo de liquidez exclusivo: nada sale sin uso aprobado (FR-207).

### Contabilidad

- **TR23** · Dos emisores en `DestinoContabilidad`, dos matrices `ACC_Receipts*`/`ACC_Treasury*`, vínculo de la cuenta
  propia en Contabilidad (R-22).
- **TR24** · Validación previa por combinación; bloquea en pantalla y avisa en procesos; cambiar a «advertir» con motivo y
  alerta diaria (R-23).
- **TR25** · Recibos de caja por lotes resumidos por día, punto y medio; lo demás en línea (D25).
- **TR26** · Puesta en marcha sin hechos; cuadre definitivo o provisional; guarda de cierre del mes de la activación (R-27).

### Cumplimiento y GMF

- **TR27** · Cuatro parámetros SARLAFT separados con vigencia y fuente; 5 y 30 millones «pendiente de validar» (R-24).
- **TR28** · Registro propio de transacciones en efectivo; en R1 suma lo del punto de venta por consulta, sin copiar (R-24).
- **TR29** · Listas vinculantes bloquean siempre en los dos módulos; no vinculantes según parámetro (R-24, FR-203).
- **TR30** · GMF con parámetros propios de Tesorería; aritmética común; no se evalúa lo que el origen ya liquidó (R-25).

### Participantes, versiones y activación

- **TR31** · Registro real de `REC` (recaudador y destino) y `TES` (destino) en el guardado de la activación; Tesorería no
  recaudadora (R-26, D2).
- **TR32** · Retiro de los dobles: migración de datos + borrado del código + rechazo en todo ambiente + e2e migradas antes
  (R-26).
- **TR33** · v2 de los dos contratos con `PartidaBancariaCitada`; v1 sigue admitida (R-29).
- **TR34** · Activación con requisitos verificables y la lista de go/no-go de plataforma (R-27, R-28).

### Seguridad, auditoría y uso

- **TR35** · Alcance por oficina, punto, caja, pagaduría y cuenta bancaria; falla cerrado (R-30).
- **TR36** · Aprobaciones de la 012: presente en caja, diferida en Tesorería; segregación por persona (R-31).
- **TR37** · Auditoría encadenada con módulos `Receipts` y `Treasury`; lecturas sensibles con lista cerrada; cada corrida
  no interactiva deja un evento resumen (`Receipts.ProcessingRun.Summary`) con la lista de cada recibo aplicado
  (`PublicId` y huella `HuellaDeRecibo`), verificable recibo por recibo, por `COR_AuditOutbox` en la cadena de sellos, y
  uno por línea rechazada. Cumple el Principio X sin desviación (decisión del dueño del 2026-10-23; R-32).
- **TR38** · Precisión por alias; nada en coma flotante (R-33).
- **TR39** · Caja en tres pasos con atajos, en Web y MAUI, con `IndicadorDeCarga` (R-35).
- **TR40** · Migraciones pares por entrega. Los pares de R0/T0 se regeneran hasta el despliegue en QA del 10/11 (T0271)
  y ahí se **congelan** con sus huellas en `LasMigracionesCongeladasNoSeRegeneran`; desde entonces todo cambio de R0/T0
  es un par nuevo y aditivo (decisión del dueño del 2026-10-23). El retiro heredado, destructivo con guarda, después de
  T2 (R-34).

### Después del analyze (2026-10-23)

- **TR41** · La conciliación bancaria es **una sola, de Tesorería**: reemplaza a la de Contabilidad (enmienda E-04 a la
  009, FR-055 a FR-060). `ACC_BankReconciliations`, `ACC_BankStatementLines` y `ACC_BankStatementColumnMaps` quedan sin
  uso y nadie las escribe; `ReopenPeriodCommand` marca las de Tesorería por `IConciliacionBancariaParaContabilidad`
  (R-39).
- **TR42** · **Activación mínima** de Recaudo y Tesorería en el bloque 2 (requisitos básicos y registro real de `REC` y
  `TES` en el mismo guardado), para que el hito del 04/11 en DEV y el paralelo en QA del 10/11 operen sin registro a
  mano; la activación completa (cuadre, matriz, supervisor con permisos, chequera) la completa la fase 19 sobre el
  mismo comando (R-40).
- **TR43** · La hora de corte es una **restricción de configuración comprobada**, no una dependencia entre tareas (§2.5,
  FR-185).

## 4. Choques y cómo se resolvieron

| # | Choque | Resolución |
|---|---|---|
| CH1 | La 014 previó a Tesorería como recaudadora de transferencias (`TES:` en el contrato de cuentas) | D2 del dueño: todo entra por Recaudo; lo de la 014 queda sin uso; ningún contrato cambia |
| CH2 | La 013 decidió que el archivo de desembolsos «no viaja en ningún mensaje» (C10) | E-01 (c): nuevo mensaje `ArchivoDeDesembolsosGenerado`/`…Anulado` para conciliar (D16) |
| CH3 | Los requisitos de activación de la 013 admiten la clase `Test` fuera de producción | E-01 (e), E-02 (g): sólo cuentan los reales en todo ambiente |
| CH4 | El doble de tesorería usa el código `TES`, el mismo de la Tesorería real | migración de datos que cierra la fila del doble; la real abre otra vigencia |
| CH5 | `ElCatalogoDeMensajesEsUnico` fija hoy 1 tipo `TES` y 3 `REC` | se amplía con los hechos y mensajes de esta feature |
| CH6 | La spec dice «la conciliación carga el extracto» y «Recaudo carga el extracto» | un solo almacén de Tesorería y un solo comando, desde las dos pantallas (TR4) |
| CH7 | FR-119 («girada» sólo al confirmar el banco) contra el registro contable del cheque al girarlo | el hecho del cheque nace al emitir; el resultado «girada», al entregar (TR19) |
| CH8 | La remesa aparece en Recaudo (bóveda, FR-035) y en Tesorería (FR-105) | movimiento de bóveda en Recaudo, documento de remesa en Tesorería, por puerto en la misma transacción (TR13) |
| CH9 | No existe entidad de cuentas bancarias de la persona; FR-210 la exige | `COR_PersonBankAccounts` en la plataforma (E-06), con copia de los campos sueltos como `Imported` |
| CH10 | El GMF vive en parámetros `LND` de Captaciones; Tesorería no puede leerlos | parámetros propios `TES` y aritmética común (TR30) |
| CH11 | `AccountingModules` no tiene bandera de Recaudo; `ModuloContable` no tiene `REC` | `Receipts = 128` y `Recaudo = "REC"` |
| CH12 | El cerrojo de Cartera y Captaciones es «setup → persona → crédito → cuenta» y FR-170 dice «obligaciones antes que cuentas» | un lote de obligaciones y después uno de cuentas: la persona ya tomada no espera (TR6, TR10) |
| CH13 | La ruta `/api/treasury/checks` y la pantalla `/tesoreria/cheques` son heredadas | lo nuevo usa `/issued-checks`, `/checkbooks` y `/tesoreria/cheques-girados`, `/tesoreria/chequeras` |
| CH14 | Lo heredado y el paralelo necesitan cotejarse contra lo que traía la cooperativa | se habla de «lo heredado» y de «cifras de referencia» (`RCD_ReferenceFigures`, `TES_ReferenceFigures`); ningún texto del producto nombra el sistema anterior |
| CH15 | La 009 tiene su propia conciliación bancaria (FR-055 a FR-060, tablas `ACC_Bank*`) y la reapertura de un período las marca `Outdated` | la de Tesorería la reemplaza (TR41); la reapertura mira la de Tesorería por puerto |
| CH16 | TR10 fija Recaudo → … → Tesorería, pero Tesorería llama a Recaudo al registrar el resultado de una orden de Recaudo o al crear una partida desde la conciliación, con sus filas ya tomadas | Tesorería toma el nivel de Recaudo por el puerto (`TomarCerrojosAsync`) antes de sus cerrojos; el orden global no se invierte (data-model §0.5) |

## 5. Preguntas al dueño (con propuesta por defecto)

| # | Pregunta | Propuesta (rige si no responde) |
|---|---|---|
| Q1 | ¿El umbral de declaración de origen de fondos de Recaudo es el mismo valor que el de transacción individual (5.000.000)? | Sí, sembrado igual y «pendiente de validar» por el oficial (P7) |
| Q2 | ¿Los tipos de comprobante `RR`, `RB`, `MC`, `AR`, `TP`, `TB` (y `EG` en R1) son aceptables para la contadora? | Sí, salvo choque con los vigentes; se cotejan al sembrar (P12) |
| Q3 | ¿El hecho del pago por transferencia se fecha con la fecha del débito que informa el banco? | Sí (TR19) |
| Q4 | ¿La migración copia las cuentas bancarias de proveedores que hoy están en el maestro como «importadas» y pagables sin espera? | Sí, marcadas «importada sin certificación» en el lote para revisión expresa |
| Q5 | ¿La hora de corte por defecto del día de Recaudo es 18:00? | Sí, configurable por cooperativa |
| Q6 | ¿Los días sin resultado (3), sin cobrar un cheque (30) y a cuenta por pagar (180) son razonables como defecto? | Sí, «pendiente de validar» |
| Q7 | ¿Un faltante de caja menor que la tolerancia del medio se registra igual como hecho? | Sí, con motivo y sin aprobación (FR-032) |
| Q8 | ¿El GMF de un pago que dispone de una cuenta contable se causa como gasto y por pagar, o lo cobra el banco en el mismo débito? | Parámetro `Gmf.HechosGeneradoresDeEgresos` con la forma; defecto «gasto y por pagar» hasta la validación del tributarista (P10) |
| Q9 | ¿El paralelo en QA corre con las e2e de Cartera y Captaciones ya migradas a los módulos reales? | En dos tiempos. Del 04/11 (DEV) y del 10/11 (QA) al 19/11 rige la **bandera transitoria** `Lending:TestDoubles:Enabled = false` (T0141): el código de los dobles sigue, sus consumidores no se registran y los reales sí, y Recaudo y Tesorería se registran con la activación mínima (TR42). Desde el despliegue en QA del 19/11 el código de los dobles ya no existe (T0307), después de la puerta de las e2e migradas (T0301) |
| Q10 | ¿La lista de go/no-go vive en la plataforma y sirve también para Cartera y Captaciones? | Sí (TR34) |

Los pendientes de datos del dueño P1 a P19 están en [research.md](research.md) «Pendientes del dueño que tocan el diseño»,
con lo que rige mientras tanto y lo que bloquea la salida.
