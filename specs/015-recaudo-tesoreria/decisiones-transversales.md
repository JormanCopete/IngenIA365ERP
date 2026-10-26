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

**Fijados en la construcción de la fase 8 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `TransicionDelResultado` · `TransicionesDelResultado.Evaluar` | la tabla fija de FR-215 como función pura; admite también la orden lista que paga un archivo de Cartera (FR-108) | `Domain/Treasury/Resultados` |
| `PaymentResponse.{BankFileFormatId, FileName, DiscrepancyCount, InformedCount}` · `PaymentResponseLine.{BeneficiaryTaxId, BankReference, DiscrepancyDetail, ReviewedAt, ReviewAccepted, ReviewNote}` · `DisbursementResult.{Source, RecordedByCentralUserId, RecordedByName, RecordedAt}` | columnas que data-model §10.6 y §10.7 no listaban; `RecordedByUserId` es nulo en el «anulada» | `TES_PaymentResponses`, `TES_PaymentResponseLines`, `TES_DisbursementResults` |
| `ResultadoARegistrar` · `QuienRegistra` · `ResultadoRegistrado` · `RegistroDeResultadosDelGiro.RegistrarAsync` / `.TomarCerrojosDeRecaudoAsync` | el único escritor del resultado y lo que recibe | `Application/Treasury/Results` |
| origen del `ResultadoDelGiro` = el `DisbursementResult` (clase `TreasuryResult`, tipo `OP`), relacionado = la orden del origen (`DisbursementOrder`) | dos resultados de la misma orden no chocan en la UK de `COR_IntegrationMessages` | `RegistroDeResultadosDelGiro` |
| `EmisionDeTesoreria.TipoDeOrden = "OP"` · `ClasePagoGirado = "PaymentOrderDisbursed"` · `PagoDeOrdenGiradoAsync` | el tipo de documento que el emisor exige en un origen `Document` y el hecho del pago girado | `Treasury/Accounting` |
| `GetPaymentResponseQuery` · `ListPaymentResponsesQuery` · `ListDisbursementResultsQuery` · `GET /payment-files/{id}/responses` · `GET /payment-orders/{id}/results` | consultas y rutas que api.md §10 no listaba | `Treasury/Results`, `PaymentResultsEndpoints.cs` |
| `Treasury.Result.{NotSent, InvalidTransition, SupportRequired, Discrepancy, InvalidOutcome, ReasonRequired}` · `Treasury.Response.{FormatNotFound, Unreadable, Empty, UnknownOutcome, NoDiscrepancy, AlreadyReviewed}` · `Treasury.File.HasResults` | errores de US11 | `ErroresDeTesoreria.Resultados.cs`, `MarkPaymentFileRejectedByBankCommandHandler` |
| adjunto `TreasuryResultSupport` (`AdjuntosDeModulo.SoporteDeResultadoDelGiro`) | el soporte del registro manual; dueño la orden o el lote; sube `Treasury.Results.Register`, lee `Treasury.Orders.View`; no se borra | `Attachments/Common/AdjuntosDeModulo.cs` |
| `VigilanciaDeTesoreria` · `TareaDeVigilanciaDeTesoreria` (`tesoreria.vigilancia`, cada 6 h) | sólo alerta; en T0 las órdenes enviadas sin resultado a `Giros.DiasSinResultado` días hábiles | `Application/Treasury/Tareas` |
| `ResultadosClient` · `/tesoreria/resultados` | la pantalla de US11 | `Shared` |

**Fijados en la construcción de la fase 9 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `ReviewPaymentBatchItemCommand` · `POST /api/treasury/payment-batches/{id}/items/{itemId}/review` (cuerpo `reason` = la nota) · `Treasury.Batch.ItemNotFlagged` | la revisión expresa de una orden marcada, con nota, por otra persona que quien preparó | `Application/Treasury/Batches`, `PaymentsEndpoints.cs` |
| `GuardasDeAprobacionDelLote(db, listas, alertas)` | exige la revisión de `RecentAccountChange` antes de las listas, y alerta `Tesoreria.ListaVinculante` al encontrar la coincidencia | `PaymentBatchCommands.cs` |
| `VistasDeTesoreria` · `RecentAccountChangesQuery` · `GET /api/treasury/reports/recent-account-changes` | el registro de vistas de Tesorería nace con una vista; la fase 20 lo completa | `Application/Treasury/Reports` |
| `ResultadosE2E.CargarRespuestaAsync` | la carga de la respuesta del banco por multipart en las e2e | `tests/…/Treasury/CuentaDestinoCambiadaYArchivoAlteradoTests.cs` |

**Fijados en la construcción de la fase 10 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `ReceiptVoidingState` · `PayerCreditBalanceKind` · `PayerCreditBalanceOrigin` (+`LendingRefund = 7`) · `PayerCreditBalanceState` · `PayerCreditBalanceMovementKind` (+`RefundRejected = 5`) · `PayerReceivableState` | enumeraciones que data-model §20 no numeraba | `Domain/Enums/Receipts/ReceiptsReversalEnums.cs` |
| `ReceiptVoiding.Number` (escritura única, nulo hasta aplicar) · `.DecidedAt` · `.DecisionReason` · `.VerificationCode` · `ModuleReversal.{MessageType, Reason, ConfirmedAt, ConfirmationNote}` · `PayerCreditBalance.{OriginDocumentNumber, BranchId}` · `PayerCreditBalanceMovement.{DocumentNumber, RecordedByUserId, RecordedAt, Note}` | columnas que data-model §4 no listaba | `RCD_ReceiptVoidings`, `RCD_ModuleReversals`, `RCD_PayerCreditBalances…` |
| `ListReceiptVoidingsQuery` · `GET /api/receipts/voidings` · `GetVoidingDocumentQuery` · `GET /api/receipts/voidings/{id}/document` · `AnulacionModel` · `IDocumentosDeRecaudoEnPdf.Anulacion` | consulta y documento de la anulación | `Receipts/Voidings`, `VoidingsEndpoints.cs` |
| `Receipts.Voiding.{AlreadyVoided, SelfApproval, ContractRejected, PayrollOwned, NotPending}` · `Receipts.Reversal.{UnknownApplication, NotOurs, CauseAlreadyConfirmed, InvalidCause}` · `Receipts.CreditBalance.{InsufficientBalance, Resolved, LendingRefundRejected, ReceiptNotOfPayer}` · `Receipts.Activation.{RequirementsMissing, AlreadyActive}` · `Treasury.Activation.{RequirementsMissing, AlreadyActive}` | errores de US5 y de la activación mínima | `ErroresDeRecaudo.Anulaciones.cs`, `ActivacionDeTesoreria.cs` |
| `SaldosAFavor` · `ClasesDeOrdenDeRecaudo` · `ResultadoDeOrdenesDeRecaudo` · `OrdenesDePagoDeTesoreria` | los saldos a favor y los dos puertos de la devolución por orden de pago | `Receipts/CreditBalances`, `Treasury/Orders` |
| `ReciboAnuladoV1` (`RAN`) · `AplicacionReversadaPorModuloV1` (`ARM`) · `SaldoAFavorDePagadorResueltoV1` (`SFP`) · `SaldoAFavorDeCarteraDevueltoV1` (`SFC`) · roles `SaldoAFavorDePagadores`, `PuenteDeDevolucion` · tipos de origen `RMOD`, `SAF` | los hechos de US5 | `Contracts/Receipts/HechosDeReversosV1.cs`, `EmisionDeRecaudo.Reversos.cs` |
| `RequisitosDeActivacionDeRecaudo` · `RequisitosDeActivacionDeTesoreria` · `RequisitoDeActivacionDto` · `EstadoDeActivacionDto` · `ActivateReceiptsCommand` · `ActivateTreasuryCommand` | la activación mínima del bloque 2 (T0183b) | `Receipts/Activation`, `Treasury/Activation` |
| `RecaudoYTesoreriaE2E.RegistrarYActivarAsync(http, coop)` | activa por las rutas lo que ya cumple sus requisitos básicos | `tests/…/Receipts/RecaudoYTesoreriaE2E.cs` |

**Decisión del dueño del 2026-10-24 sobre T0113 (búsqueda por número)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `IBusquedaDeObligaciones.PorNumeroAsync` · `BusquedaPorNumero` · `ObligacionEncontradaDto` · `ClaseDeObligacionEncontrada` | el puerto de lectura que Cartera y Captaciones ofrecen a la caja: un crédito o una cuenta por su número, con la persona dueña | `Application/Common/Integration/Lending` (el espacio de los puertos de Cartera) |
| `BusquedaDeObligaciones` | su implementación sobre créditos y cuentas | `Application/Lending/Integration` |
| `GetPayablesQuery.ObligationNumber` · `GET /api/receipts/payables?number=` · `Receipts.Payables.AmbiguousNumber` | la caja llega a la persona por el número | `Receipts/CashDesk`, `CashDeskEndpoints.cs` |

**Decisión del dueño del 2026-10-24 sobre T0115 (quien efectúa el pago)**:

| Nombre | Qué | Dónde |
|---|---|---|
| regla de `Receipts.Compliance.DeliveredByRequired` | obligatorio con el efectivo en `Sarlaft.UmbralTransaccionIndividual` (parámetro con vigencia, sembrado en 5.000.000) o con el pagador distinto del titular; si no, opcional; `data.reason` = `CashThreshold` / `PayerIsNotHolder`, `data.threshold` | `ConfirmacionDeRecibo`, `ErroresDeRecaudo.QuienEfectuaRequerido(motivo, umbral)` |
| `SetCashOperationDeliveredByCommand` · `PUT /api/receipts/cash-operations/{id}/delivered-by` · `CajaClient.QuienEfectuaAsync` | registrar quién efectúa en una operación en preparación | `Receipts/CashDesk/CajaCommands.cs`, `CashDeskEndpoints.cs`, `/recaudo/caja` |

**Fijados en la construcción de la fase 11 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `HuellaDeMovimientoBancario.{Calcular, DelArchivo, Clasificar}` · `MovimientoParaHuella` · `HuellaCalculada` · `DestinoDeLaLinea` (`Nueva`, `OmitidaPorHuella`, `PosibleDuplicado`) · `LineaClasificada` | el motor puro de la huella y lo que decide de cada línea | `Domain/Receipts/Huellas` |
| `BankStatement` (H) · `BankStatementLine` (H, columnas mutables `DuplicateDecision`, `RecaudoStatus`, `RecaudoIdentifiedAmount`, `ReconciliationStatus`) · `HasBankIdentifier` · DbSets `TreasuryBankStatements`, `TreasuryBankStatementLines` | el extracto; la UK de la huella se filtra a `HasBankIdentifier = 1` | `Entities/Treasury/Statements`, `TES_BankStatements…` |
| `BankStatementSource` · `StatementLineDirection` · `StatementLineDuplicateDecision` · `StatementLineRecaudoStatus` · `StatementLineReconciliationStatus` | enumeraciones del extracto | `Domain/Enums/Treasury/TreasuryStatementEnums.cs` |
| `BankItemOutcome` · `UnidentifiedItemOrigin` · `UnidentifiedItemResolutionKind` (+`RefundRejected = 5`) · `ProcessingRunKind` · `ProcessingLineOutcome` (+`Pending = 5`) | enumeraciones de la identificación, las partidas y las corridas | `Domain/Enums/Receipts/ReceiptsBankEnums.cs` |
| `BankItemIdentification.{ApprovalRequestPublicId, Note}` · `UnidentifiedItem.{BranchId, OpenedOn}` · `UnidentifiedItemResolution.{DocumentNumber, MiscConceptId, Note}` · `ProcessingRun.{SourceNumber, Batches}` · `ProcessingRunLine.{Amount, RequestJson, Attempts, LastAttemptAt}` · `Receipt.OriginalValueDate` | columnas que data-model §3.3 y §5 no listaban | `RCD_*` |
| `LoadBankStatementCommand` · `ListBankStatementsQuery` · `GetBankStatementQuery` · `ListBankStatementLinesQuery` · `MovimientoCapturado` · `BankStatementLoadDto` · clase de numeración `EX` · `POST /api/treasury/statements/captured` · `GET /api/treasury/statements` | la carga única del extracto | `Application/Treasury/Statements`, `StatementsEndpoints.cs` |
| `MovimientosBancariosParaRecaudo` · `IMovimientosBancariosParaRecaudo.{LineaAsync, DecidirDuplicadoAsync, UltimasCargasAsync}` · `UltimaCargaDeExtractoDto` · `FiltroDeAbonos.{SoloPendientes, OficinaPublicIds}` · `LineaDeExtractoDto` ampliado | el puerto del extracto; no aplica el alcance de Tesorería a Recaudo | `Application/Treasury/Statements`, `Common/Integration/Treasury` |
| `AplicacionNoInteractiva` · `LineaNoInteractiva` · `ParteNoInteractiva` · `OrigenABloquear` · `ResultadoNoInteractivo` · `ClaveDelRecibo` · `NumeradorDeRecaudo.Devolver` | la aplicación no interactiva por línea (unidad lógica con su clave; la tanda comparte transacción) | `Application/Receipts/BankItems`, `Common` |
| `CorridasNoInteractivas` · `Receipts.ProcessingRun.LineRejected` · `HuellaDeRecibo.DelReciboAsync` · `IReprocesoDeCorrida` | las corridas, su resumen por tanda y el reproceso de otras clases | `Application/Receipts/BankItems`, `Common/ClavesDeRecaudo.cs` |
| `ListBankItemsQuery` · `ListBankItemIdentificationsQuery` · `GET /bank-items/{lineId}/identifications` · `GetProcessingRunQuery` · `ListUnidentifiedItemsQuery` · `AbonosDeRecaudo` · `PartidasPorIdentificar` (numeración `PI`) | consultas y piezas de US6 | `Application/Receipts/BankItems` |
| `ApprovalSubjects.BankItemClassification` (`Receipts.BankItems.Classify`) · `ApprovalSubjects.UnidentifiedItemToIncome` (`Receipts.UnidentifiedItems.MoveToIncome`) | la clasificación y la partida vencida llevada a ingreso, con aprobador presente | `Domain/Approvals` |
| `Receipts.BankItem.{NotACredit, DuplicatePending, NoTransferMeans, NotADuplicate, ConceptInvalid, ReturnedDisbursementOrderRequired}` · `Receipts.UnidentifiedItem.{InsufficientBalance, Resolved, NotDue, DestinationNotIncome}` · `Receipts.ProcessingRun.LineNotRetryable` · `Receipts.Prevalidation.NotPostable` (en procesos) · `Treasury.Statement.{FormatNotFound, Empty, SourceRequired, AccountInactive}` | errores de US6 | `ErroresDeRecaudo.Bancos.cs`, `ErroresDeTesoreria.Extractos.cs` |
| `ReciboDeBancoEmitidoV1` (`RBE`) · `PartidaPorIdentificarReconocidaV1` (`PIR`) · `PartidaClasificadaV1` (`PCL`) · `PartidaDevueltaAlConsignanteV1` (`PDC`) · roles `PartidasPorIdentificar`, `SaldoDePagadurias`, `CuentaPorCobrarPagador`, `CustodiaPorLista` · tipos de origen `PI`, `CLA` | los hechos de US6 y los roles de las fases 11 a 15 | `Contracts/Receipts/HechosDeBancosV1.cs`, `EmisionDeRecaudo.Bancos.cs` |
| `AntiguedadDeRecaudo` · `TareaDeAntiguedadDeRecaudo` (`recaudo.antiguedad`, cada 24 h) · `PermisoDelOficial` | sólo alerta; deja `AgingStep` | `Application/Receipts/Tareas` |
| `BancosClient` · `EnvioDeRecaudo` · `ExtractosClient` · `/recaudo/extractos` · `/recaudo/partidas` · `/tesoreria/extractos` | clientes y pantallas de US6 | `Shared` |

**Fijados en la construcción de la fase 12 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `PayrollDeductionFile` · `PayrollDeductionFileLine` (H, `[EscrituraUnica]` `DeductionOutcome`/`DeductedAmount`/`DeductionDate`, `[ColumnaMutable]` `RemittedAmount`) · `PayrollFileSending` (H) · `PaymasterResponse` · `PaymasterResponseLine` · `PaymasterRemittance` · `PaymasterRemittanceLine` · DbSets `ReceiptsPayrollDeductionFiles`, `ReceiptsPayrollDeductionFileLines`, `PayrollFileSendings`, `PaymasterResponses`, `PaymasterResponseLines`, `PaymasterRemittances`, `PaymasterRemittanceLines` | la copia en Recaudo del archivo de descuentos, su envío, sus respuestas y los giros | `Entities/Receipts/Paymasters`, `RCD_*` |
| `PayrollDeductionFileState` · `PayrollDeductionLineKind` · `PayrollFileChannel` · `PaymasterResponseSource` · `PaymasterResponseState` · `PaymasterDeductionOutcome` · `PaymasterRemittanceSource` · `PaymasterRemittanceState` · `PaymasterAllocationOrder` · `PaymasterRemittanceLineOutcome` | enumeraciones de las pagadurías | `Domain/Enums/Receipts/ReceiptsPaymasterEnums.cs` |
| `ConsumePayrollDeductionFileCommand` | el consumo de `DescuentosPorPagaduria` v1 y v2 (lo manda `DestinoRecaudo`) | `Application/Receipts/Paymasters` |
| `ConfirmPayrollFileSentCommand` · `LoadPaymasterResponseCommand` · `FilaDeRespuestaCapturada` · `PreviewPaymasterResponseQuery` · `EmitPaymasterResultCommand` · `RefreshPaymasterResponseStatusCommand` · `EvaluacionDeCarteraDeLaRespuesta` · `ListPayrollDeductionFilesQuery` · `GetPayrollDeductionFileQuery` · `DownloadPayrollDeductionFileCommand` · `ListPaymasterResponsesQuery` · `PagaduriasDeRecaudo` | envío, respuesta, devolución y evaluación de Cartera | `Application/Receipts/Paymasters` |
| `RegisterPaymasterRemittanceCommand` · `ApplyPaymasterRemittanceCommand` · `ResolvePaymasterBalanceCommand` · `GetPaymasterRemittanceQuery` · `ListPaymasterRemittancesQuery` · `GirosDePagaduria` (`IReprocesoDeCorrida`) · `GirosDePagaduria.Clave` (`REC:{giro:N}:{línea:N}`) · clase de numeración `GP` | el giro y su aplicación línea por línea | `Application/Receipts/Paymasters` |
| `EmisionDeRecaudo.{GiroDePagaduriaRecibidoAsync, ReciboDePagaduriaEmitidoAsync, MensajeDeLibranzaAsync}` · `GiroDePagaduriaRecibidoV1` (`GPR`) · `ReciboDePagaduriaEmitidoV1` (`RPG`) · clases de origen `PaymasterRemittance`, `PaymasterReceipt`, `PayrollFileDelivery`, `PayrollDeductionResponse` | los hechos y los mensajes de libranza de Recaudo | `Application/Receipts/Accounting`, `Contracts/Receipts/HechosDePagaduriasV1.cs` |
| `TablasDeRecaudo.LineasDeDescuento` · `SqlDelCerrojoDeRecaudo.TablaLineasDeDescuento` | `RCD_PayrollDeductionFileLines` en el nivel 6 del cerrojo | `Receipts/Common`, `Persistence/Receipts` |
| `Receipts.Paymaster.{SupersedesSentFile, VersionNotAdopted, RemittanceExceedsPending, PaymasterUnknown, PersonUnknown, FileNotSendable, FileNotSent, ResponseFormatNotFound, ResponseUnreadable, ResponseEmpty, ResponseNotEmittable, RemittanceNoLines, RemittanceApplied, NotPaymasterBalance, SupportRequired}` | errores de las pagadurías | `ErroresDeRecaudo.Pagadurias.cs` |
| `POST /api/receipts/payroll-files/{id}/responses/captured` · `GET …/responses` · `POST …/responses/{rid}/refresh` · `GET /api/receipts/paymaster-remittances` · `POST /api/receipts/paymaster-balances/{id}/resolve` · pantalla `/recaudo/pagadurias` · `PagaduriasClient` | rutas y pantalla que api.md §6 no listaba | `PaymastersEndpoints.cs`, `Shared` |

**Fijados en la construcción de la fase 13 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `ReceivedCheck` (`DrawerName`, `PayerPersonId`, `BranchId`, `ReceiptId`, `PayerReceivableId`) · `ReceivedCheckEvent` (H) · `CheckReturnCause` (`CountsForDrawerList`) · `ReceivedCheckState` · `ApplicationState.ReversedByCheckReturn = 4` · DbSets `ReceivedChecks`, `ReceivedCheckEvents`, `CheckReturnCauses` | el cheque recibido, sus eventos y las causales | `Entities/Receipts/Operations/ReceivedChecks.cs`, `RCD_ReceivedChecks…` |
| `ChequeDeCajaDto` · `MedioDeCajaDto.Check` · `ChequesRecibidos.{ValidarAsync, PlazoAsync, Nuevo, Transicion, VisibleAsync}` · `ReceivedCheckDto` · `DrawerWithReturnsDto` · `CheckReturnCauseDto` | el cheque como medio y su validación | `Application/Receipts/Checks`, `CashDesk/CajaDtos.cs` |
| `RegisterCheckInCustodyCommand` · `MarkReceivedCheckDepositedCommand` · `MarkReceivedCheckInClearingCommand` · `MarkReceivedCheckPaidCommand` · `TransicionesDeChequeHandler` · `ReturnReceivedCheckCommand` (clase de numeración `CD`) · `ListReceivedChecksQuery` · `ListDrawersWithReturnsQuery` · `ListCheckReturnCausesQuery` | custodia, canje manual y devolución | `Application/Receipts/Checks` |
| `ChequeRecibidoDevueltoV1` (`CRD`) · `EmisionDeRecaudo.ChequeRecibidoDevueltoAsync` · clase de origen `ReceivedCheckReturn` | el hecho del cheque devuelto | `Contracts/Receipts/HechosDeChequesV1.cs`, `EmisionDeRecaudo.Cheques.cs` |
| `Receipts.Check.{DataRequired, NotPostdated, Duplicate, InvalidTransition, UnknownReturnCause, ReturnRejectedByContract}` | errores que data-model no nombraba | `ErroresDeRecaudo.Cheques.cs` |
| `POST /api/receipts/received-checks/custody` · `GET …/drawers-with-returns` · `GET …/return-causes` · pantalla `/recaudo/cheques-recibidos` · `ChequesRecibidosClient` · `ChequeDeCajaRequest` | rutas y pantalla que api.md no listaba | `ReceivedChecksEndpoints.cs`, `Shared` |

**Fijados en la construcción de la fase 14 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `Checkbook` · `IssuedCheck` · `IssuedCheckEvent` (H) · `CashRemittance` (`IdempotencyKey`, `DispatchDate`, `SourceDocument…`, `ConfirmedOn`, `LiquidityFundUseId`) · `CheckbookState` · `IssuedCheckState` · `RemittanceDirection` · `RemittanceState` · DbSets `Checkbooks`, `IssuedChecks`, `IssuedCheckEvents`, `CashRemittances` | chequeras, cheques girados y remesas | `Entities/Treasury/Checkbooks`, `Entities/Treasury/Remittances`, `TES_*` |
| `RegisterCheckbookCommand` · `VoidCheckbookCommand` · `SpoilBlankCheckCommand` · `IssueChecksForBatchCommand` · `DeliverIssuedCheckCommand` · `VoidIssuedCheckCommand` · `MarkIssuedCheckCashedCommand` · `ListCheckbooksQuery` · `ListIssuedChecksQuery` · `ChequerasDeTesoreria` · `CheckbookDto` · `IssuedCheckDto` | la chequera y el cheque girado | `Application/Treasury/Checkbooks` |
| `ChequeModel` · `IDocumentosDeTesoreriaEnPdf` · `GetIssuedCheckDocumentQuery` · `ValorEnLetras` · `DocumentosDeTesoreriaReport` | la impresión del cheque | `Application/Treasury/Checkbooks/DocumentoDeCheque.cs`, `API/Reports` |
| `RemesasDeTesoreria` (puerto) · `IRemesasDeTesoreria.RecibidaEnBovedaAsync` · `SolicitudDeRemesa.{VaultPublicId, BagSeal, MinutesAttachmentPublicId}` · `ConfirmRemittanceCommand` · `RegisterBankToVaultRemittanceCommand` · `ListCashRemittancesQuery` · `CashRemittanceDto` · clase de numeración `RM` | la remesa en Tesorería | `Application/Treasury/Remittances`, `Common/Integration/Treasury` |
| `DispatchVaultRemittanceCommand` · `VaultRemittanceDto` · `ListReceiptsOwnBankAccountsQuery` · `GET /api/receipts/own-bank-accounts` · `RegisterVaultMovementCommand` confirma la remesa que vuelve por el puerto | la remesa en Recaudo | `Application/Receipts/Vaults/RemesasDeBoveda.cs` |
| `ChequeGiradoEmitidoV1` (`CHE`) · `ChequeGiradoAnuladoV1` (`CHA`) · `RemesaConfirmadaV1` (`RMC`) · `RemesaDespachadaV1` (`RMD`) · `EmisionDeTesoreria.{ChequeGiradoEmitidoAsync, ChequeGiradoAnuladoAsync, RemesaConfirmadaAsync}` · `EmisionDeRecaudo.RemesaDespachadaAsync` · clases de origen `IssuedCheck`, `CashRemittance` | los hechos de cheques y remesas | `Contracts/Treasury/HechosDeChequesYRemesasV1.cs`, `Contracts/Receipts/HechosDeRemesasV1.cs` |
| `VigilanciaDeTesoreria.{ChequesSinCobrarAsync, RemesasSinConfirmarAsync}` | alertas de la tarea `tesoreria.vigilancia` | `Application/Treasury/Tareas` |
| `Treasury.Checkbook.{Exhausted, Voided, RangeOverlaps, AccountInactive, OtherAccount}` · `Treasury.Batch.{NotChecks, NoChecksToIssue}` · `Treasury.Check.{NotDeliverable, NotVoidable, StillCashable}` · `Treasury.Remittance.{NotInTransit, SupportRequired, OtherAccount, AccountNotAllowed}` · `Receipts.Vault.RemittanceMismatch` | errores | `ErroresDeTesoreria.Cheques.cs`, `ErroresDeRecaudo.Remesas.cs` |
| `/api/treasury/checkbooks` · `/api/treasury/issued-checks` (no `/checks`, área heredada) · `POST /api/treasury/remittances/bank-to-vault` · `POST /api/receipts/vaults/{id}/remittances` · pantallas `/tesoreria/chequeras`, `/tesoreria/cheques-girados`, `/tesoreria/remesas` · `ChequesYRemesasClient` | rutas y pantallas | `CheckbooksEndpoints.cs`, `RemittancesEndpoints.cs`, `Shared` |

**Fijados en la construcción de la fase 15 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `SenalesDeEfectivo.{Evaluar, Consignante}` · `TransaccionDeEfectivo` · `ParametrosDeSenales` · `SenalDeEfectivo` · `SenalDetectada` | el motor puro de las señales | `Domain/Receipts/Cumplimiento` |
| `CashTransaction` (H) · `ComplianceAlertLog` · `CashTransactionDocumentKind` · `CashTransactionDirection` · DbSets `ReceiptsCashTransactions`, `ReceiptsComplianceAlertLogs` | el registro de efectivo y las señales evaluadas | `Entities/Receipts/Compliance`, `RCD_CashTransactions`, `RCD_ComplianceAlertsLog` |
| `ListasDeRecaudo` (reemplaza a `ListasDeRecaudoSinConsulta`) · `RegistroDeEfectivo` (los dos umbrales de señales son parámetros desde el 2026-10-24: `Sarlaft.PagadorTerceroFrecuente.Titulares`, `Sarlaft.ConsignanteRepetido.Partidas`) · `CumplimientoDeCaja` | las listas y el registro de efectivo de la caja | `Application/Receipts/Compliance` |
| `WithholdCashOperationCommand` · `ReleaseWithheldOperationCommand` · `RefundWithheldMoneyByOrderCommand` (clase de orden `WithheldMoneyRefund`) · `ListWithheldOperationsQuery` · `ListCashTransactionsQuery` · `ListComplianceSignalsQuery` | retener, liberar, devolver y consultar | `ComplianceCommands.cs` |
| `RegisterContingencyReceiptCommand` · `GetContingencyBookStatusQuery` · `ContingencyBookStatusDto` · `AntiguedadDeRecaudo.ContingenciaPorDigitarAsync` | la contingencia de la caja | `Application/Receipts/CashDesk/ContingenciaDeCaja.cs` |
| `DineroRetenidoEnCustodiaV1` (`DCU`) · `CustodiaLiberadaV1` (`CLB`) · `EmisionDeRecaudo.{DineroRetenidoEnCustodiaAsync, CustodiaLiberadaAsync}` · clase de origen `ComplianceCustody` | los hechos de la custodia | `Contracts/Receipts/HechosDeCumplimientoV1.cs`, `EmisionDeRecaudo.Cumplimiento.cs` |
| `Receipts.Compliance.{WithholdRequired, WithholdNotApplicable, WithholdNoCash, NotWithheld, ReleaseBySameUser, SessionClosed}` · `Receipts.Contingency.{BookNotUsable, NumberOutOfRange, NumberUsed, ImageRequired, Rejected}` | errores | `ErroresDeRecaudo.Cumplimiento.cs` |
| `POST /api/receipts/cash-operations/{id}/withhold` · `…/refund-by-order` · `GET /api/receipts/compliance/withheld` · `GET /api/receipts/contingency-books/{id}/status` · pantallas `/recaudo/cumplimiento`, `/recaudo/contingencia` · `CumplimientoClient` | rutas y pantallas que api.md no listaba | `ComplianceEndpoints.cs`, `Shared` |

**Fijados en la construcción de la fase 16 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `ReglaContableDeModulo` · `ReceiptsPostingRule` · `TreasuryPostingRule` · `MapeoDeComprobanteDeModulo` · `ReceiptsVoucherMapping` · `TreasuryVoucherMapping` · `RecepcionContableDeModulo` · `ReceiptsPosting` · `TreasuryPosting` | las matrices, los tipos de comprobante y los recibos de contabilización | `Entities/Accounting/Integration/MatricesDeRecaudoYTesoreria.cs`; `ACC_ReceiptsPostingRules`, `ACC_TreasuryPostingRules`, `ACC_ReceiptsVoucherMappings`, `ACC_TreasuryVoucherMappings`, `ACC_ReceiptsPostings`, `ACC_TreasuryPostings` |
| `MatricesDeRecaudoYTesoreria` · `ModuloDeLaMatriz` · `RolDeLaMatriz` · `HechoDeLaMatriz` · `MatrizVigente` · `ResolutorDeLaMatriz` · `ErroresDeLaMatriz` (`Accounting.ReceiptsRule.*`, `Accounting.TreasuryRule.*`, `Accounting.OwnBankAccount.NotLinked`) | el catálogo y la resolución | `Application/Accounting/RecaudoYTesoreria` |
| `HechoContableDeModulo` · `LineaDeHecho` · `ConstructorDeComprobanteDeModulo` · `ConsumoDeModulo` · `ContabilizacionDeModulo` · `AgrupadorDeRecaudo` (clave `REC` + fecha + punto) · `GetReceiptsAndTreasuryBatchTotalsQuery` | el consumo | íd. |
| `PostReceiptsFactsCommand` · `PostReceiptsSummaryGroupCommand` · `PostTreasuryFactsCommand` | los comandos de consumo, sin ruta | `Accounting/{Receipts,Treasury}/Contabilizacion` |
| `EvaluacionPreviaDeModulo` · `SaldosPorRol` · `PlantillaDeLaMatriz` · `ConsultasDeReglas` · `ReglasDeModuloQueries` | validación previa, saldo por rol, plantilla | `Application/Accounting/RecaudoYTesoreria` |
| `CreateModulePostingRuleCommand` · `DeactivateModulePostingRuleCommand` · `ListModulePostingRulesQuery` · `GetModulePostingTemplateQuery` · `GetModuleCompletenessQuery` · `ListModuleVoucherMappingsQuery` · `SetModuleVoucherMappingCommand` · `ListModuleBatchesQuery` | reglas, plantilla, completitud, tipos y lotes de los dos módulos (`Module` = `REC` o `TES`) | `ReglasDeLaMatrizDeModulo.cs`, `LotesDeModulo.cs` |
| `IDimensionesDeRecaudo` · `IDimensionesDeTesoreria` · `DimensionDeMatrizDto` · `DimensionesDeRecaudo` · `DimensionesDeTesoreria` | lo que los módulos ofrecen a la plantilla | `Common/Integration/Accounting/IDimensionesDeRecaudoYTesoreria.cs`, `Receipts/Accounting`, `Treasury/Accounting` |
| `SetReceiptsAccountingPolicyCommand` · `SetTreasuryAccountingPolicyCommand` · `CambioDePoliticaContable` · `PoliticaContableDto` · `AntiguedadDeRecaudo.PoliticaContableEnAdvertirAsync` · `VigilanciaDeTesoreria.PoliticaContableEnAdvertirAsync` | la política contable | `Receipts/Setup`, `Treasury/Common`, `Common/Parameters` |
| `LoteDelDiaDeRecaudo` · `TareaDeLoteDelDiaDeRecaudo` (`recaudo.lote-del-dia`) | el lote del día | `Receipts/Tareas/LoteDelDiaDeRecaudo.cs` |
| `AccountingErrors.DocumentReceiptsOrTreasuryOwned` · `AuditEventTypes.Accounting{Receipts,Treasury}{Posted,Rejected}` · `ModuloContable.Nombre(REC)` | guarda y auditoría | `Accounting/Posting`, `Common/Audit` |
| Tipos de comprobante `RR`, `RB`, `MC`, `AR` (REC), `TP` (TES) · `ReceiptsTreasuryVoucherMappingsSeeder` (Order 93) | la semilla sin cuentas | `voucher-types.json`, `Seeding/Parametric` |
| `DevolucionNoGiradaV1` (`DNG`) · `EmisionDeRecaudo.DevolucionNoGiradaAsync` · clase de origen `RefundNotDisbursed` | la devolución que Tesorería no giró | `HechosDeReversosV1.cs`, `EmisionDeRecaudo.Devoluciones.cs` |
| `ArchivoDeDesembolsosGeneradoV1` · `ArchivoDeDesembolsosAnuladoV1` (clave `Confirmation:{archivo:N}` en los dos) · `EmisionDeCartera.ArchivoDeDesembolsosAsync` · clase de origen `DisbursementFile` · `ConsumeLendingDisbursementFileCommand` · `ConsumeKeyedTreasuryUnitCommand` · `LendingDisbursementFile` · `LendingDisbursementFileOrder` · `LendingDisbursementFileState` | E-01 c | `Contracts/Lending/ArchivoDeDesembolsosV1.cs`, `Treasury/Integration/ConsumoDelArchivoDeDesembolsos.cs`; `TES_LendingDisbursementFiles`, `TES_LendingDisbursementFileOrders` |
| `/api/accounting/{receipts,treasury}/{rules, rules/{id}/deactivate, rules/template, rules/template.xlsx, rules/completeness, voucher-types, voucher-types/{factType}, batches}` · `PUT /api/receipts/accounting-policy` · `PUT /api/treasury/accounting-policy` · pantallas `/contabilidad/recaudo/{matriz, tipos-de-comprobante, completitud, lotes}`, `/contabilidad/tesoreria/{matriz, tipos-de-comprobante, completitud}` · `MatricesDeRecaudoYTesoreriaClient` | rutas y pantallas | `ReceiptsIntegrationEndpoints.cs`, `AccountingPolicyEndpoints.cs`, `Shared` |


**Fijados en los pendientes de las fases 15 y 16 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `Sarlaft.PagadorTerceroFrecuente.Titulares` (3) · `Sarlaft.ConsignanteRepetido.Partidas` (2) | los dos umbrales de señales, parámetros `RCD` con vigencia «pendiente de validar» (P7); reemplazan a las constantes de `RegistroDeEfectivo` y `AntiguedadDeRecaudo` | `ParametrosDeRecaudoYTesoreria`, data-model §23 |
| `CashOperation.{RefundPaymentOrderPublicId, RefundReleasePublicId, RefundReleaseNumber}` · `ResultadoDeOrdenesDeRecaudo.InformarCustodiaAsync` · `EmisionDeRecaudo.VuelveALaCustodia` (`ComplianceCustody`) · evento `Receipts.Compliance.RefundOrderResult` | la devolución no girada del dinero retenido reabre la custodia | `RCD_CashOperations` (par `RecaudoNucleo`), `CreditBalanceCommands.cs` |
| `EmisionDeTesoreria.ValidacionPreviaAsync` · `EmisionDeTesoreria.PoliticaBloquear` · `Treasury.Prevalidation.NotPostable` (`ErroresDeTesoreria.NoContabilizable`) | la validación previa de las pantallas de Tesorería | `Treasury/Accounting/EmisionDeTesoreria.ValidacionPrevia.cs`, `ErroresDeTesoreria.cs` |


**Fijados en la construcción de la fase 17 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `CruceDeConciliacion.Cruzar` · `LineaACruzar` · `RegistroACruzar` · `CrucePropuesto` · reglas `StatementLink`, `ValueDateReference`, `ValueDateReferenceGroup` | el motor puro del cruce | `Domain/Treasury/Conciliacion` |
| `Reconciliation` (`ClosedByUserId`, `PreviousReconciliationId`, `OutdatedAt`, `OutdatedReason`) · `ReconciliationMatch` (H; `GroupKey`, `RecordNumber`, `IsLive`, `UndoneBy…`) · `ReconcilingItem` (`StatementLineId`, `IsVoided`) · `ReconciliationKind` · `ReconciliationRecordKind` · `ReconciliationMatchMode` · `ReconcilingItemKind` · DbSets `TreasuryReconciliations`, `TreasuryReconciliationMatches`, `TreasuryReconcilingItems` · `UK_TES_Reconciliations_Account_Period_Version` | la conciliación mínima | `Entities/Treasury/Reconciliation`, `TES_Reconciliations…` (par `TesoreriaConciliacionMinima`) |
| `RegistroConciliableDto.StatementLinePublicId` · `RecaudoParaConciliacion` (implementa `IRecaudoParaConciliacion` e `IPartidasDesdeConciliacion`) · evento `Receipts.UnidentifiedItem.FromReconciliation` | los puertos de Recaudo | `Common/Integration/Receipts`, `Receipts/Integration` |
| `OpenReconciliationCommand` · `ListReconciliationsQuery` · `GetReconciliationQuery` · `AutoMatchReconciliationCommand` · `AddManualMatchCommand` (`RegistroElegido`) · `RemoveMatchCommand` · `AddReconcilingItemCommand` · `CloseReconciliationCommand` · `ReopenReconciliationCommand` · `ConciliacionesDeTesoreria` · `ConciliacionMinima` · `ReglasDelConciliador` · `RegistroDisponible` · `ReconciliationDto` · `ConciliacionModel` | los comandos de la conciliación | `Treasury/Reconciliation` |
| `IConciliacionBancariaParaContabilidad.MarcarDesactualizadasAsync` · `ConciliacionBancariaParaContabilidad` · evento `Treasury.Reconciliation.Outdated` | Tesorería para la reapertura del período | `Common/Integration/Accounting`, `Treasury/Reconciliation` |
| `Treasury.Reconciliation.{NotOpen, AmountMismatch, RecordNotAvailable, InvalidPeriod, ItemInvalid}` | errores que data-model no nombraba | `ErroresDeTesoreria.Conciliacion.cs` |
| `Conciliacion.DiasPartidaAntigua` (TES, 30, pendiente del dueño) · `VigilanciaDeTesoreria.PartidasConciliatoriasAntiguasAsync` | la alerta `Tesoreria.PartidaConciliatoriaAntigua` | `ParametrosDeRecaudoYTesoreria`, `Treasury/Tareas` |
| `IDocumentosDeTesoreriaEnPdf.Conciliacion` · `DocumentosDeTesoreriaReport.Conciliacion` | el informe de la conciliación cerrada | `Treasury/Checkbooks/DocumentoDeCheque.cs`, `API/Reports` |
| `ReconciliationsEndpoints` (`/api/treasury/reconciliations…`) · pantalla `/tesoreria/conciliacion` · `ConciliacionClient` · `NadieEscribeLaConciliacionBancariaDeContabilidad` | rutas, pantalla y prueba | `API`, `Shared`, `Architecture.Tests` |


**Fijados en la construcción de la fase 18 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `CuadreDeAperturaBase` · `LineaDeCuadreDeAperturaBase` (H) · `CifraDeReferenciaBase` · `OpeningSquareKind` · `OpeningSquareState` · `CalculoDelCuadreDeApertura` · `LineaPropuestaDeCuadre` · `LineaCalculadaDeCuadre` · `CalculoDeCuadre` | lo común del cuadre y su motor puro | `Domain/Common/Cuadres/CuadreDeApertura.cs` |
| `ReceiptsOpeningSquare` · `ReceiptsOpeningSquareLine` · `ReceiptsReferenceFigure` · `TreasuryOpeningSquare` · `TreasuryOpeningSquareLine` · `TreasuryReferenceFigure` · DbSets del mismo nombre en plural | las seis tablas | `RCD_OpeningSquares`, `RCD_OpeningSquareLines`, `RCD_ReferenceFigures`, `TES_*` (par `RecaudoYTesoreriaPuestaEnMarcha`) |
| `ReceivedCheck.IsOpeningLoad` · `CashRemittance.IsOpeningLoad` · `OwnBankAccount.{OpeningBalance, OpeningBalanceOn}` · `VaultMovementKind.OpeningBalance = 7` | lo cargado por la puesta en marcha | mismo par |
| `RegistroDeCuadres` · `ErroresDelCuadre` (`{Receipts,Treasury}.Activation.{SquareHasDifference, ProvisionalDeadlineInvalid}`) · `OpeningSquareDto` · `OpeningSquareLineDto` · `CurrentOpeningSquareDto` · `ReferenceFigureComparisonDto` · `ISolicitudDeCuadreProvisional` · `ValidadorDeCuadreProvisional<T>` | el registro común del cuadre | `Application/Common/Cuadres/CuadresDeApertura.cs` |
| `LineasDelCuadreDeRecaudo` · `GetReceiptsOpeningSquareQuery` · `RegisterProvisionalOpeningSquareCommand` · `RegisterDefinitiveOpeningSquareCommand` · `ListReceiptsReferenceFiguresQuery` | el cuadre de Recaudo | `Receipts/Activation/CuadreDeAperturaDeRecaudo.cs` |
| `LineasDelCuadreDeTesoreria` (`ChequesGiradosEnTransito` informativa) · `GetTreasuryOpeningSquareQuery` · `RegisterTreasuryProvisionalOpeningSquareCommand` · `RegisterTreasuryDefinitiveOpeningSquareCommand` · `ListTreasuryReferenceFiguresQuery` | el cuadre de Tesorería | `Treasury/Activation/CuadreDeAperturaDeTesoreria.cs` |
| `PlantillasDePuestaEnMarchaDeRecaudo` (+ `recaudo.cifras-de-referencia`) · `ImportReceiptsGoLiveTemplateCommand` · `Receipts.Imports.{UserWithoutSecondFactor, VaultAlreadyLoaded, VaultTotalMismatch, TemplateNotFound}` | las plantillas de Recaudo | `Receipts/Imports` |
| `PlantillasDePuestaEnMarchaDeTesoreria` (+ `tesoreria.cifras-de-referencia`) · `ImportTreasuryGoLiveTemplateCommand` · `Treasury.Imports.{CheckbookRangeOverlaps, CheckbookNotFound, TemplateNotFound}` | las plantillas de Tesorería | `Treasury/Imports` |
| `PlantillaDeCuentasBancariasDePersonas` · `ImportPersonBankAccountsCommand` · `Core.PersonBankAccount.ImportedWithoutCertificate` | la plantilla de plataforma | `Core/PersonBankAccounts/Imports` |
| eventos `{Receipts,Treasury}.Activation.{ProvisionalSquareRegistered, DefinitiveSquareRegistered}` | auditoría del cuadre | `CuadresDeRecaudo`, `CuadresDeTesoreria` |
| `GET /api/{receipts,treasury}/opening-squares/current` · `POST …/provisional` · `POST …/definitive` · `GET /api/{receipts,treasury}/reference-figures` · `GET/POST /api/{receipts,treasury}/imports/{code}/{template.xlsx,import}` · pantallas `/recaudo/puesta-en-marcha`, `/tesoreria/puesta-en-marcha` · `PuestaEnMarchaDeModulo` · `PuestaEnMarchaClient` | rutas y pantallas (las plantillas por la forma común de la plataforma, no la de api.md §14) | `ActivationEndpoints.cs`, `Shared` |


**Fijados en la construcción de la fase 19 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RequisitosDeActivacionDeRecaudo.{Cuadre, CajeroConPermisos, SupervisorConPermisos, MesAbierto, MatrizCompleta, HoraDeCorte, PermisosDelCajero, PermisosDelSupervisor, EstadosQueActivan, CuadreAsync}` · códigos `OpeningSquare`, `CashierWithPermissions`, `SupervisorWithPermissions`, `OpenAccountingMonth`, `AccountingMatrixComplete`, `CutoffBeforeLendingCycle` | los requisitos de la activación completa de Recaudo | `Receipts/Activation/ActivacionDeRecaudo.cs` |
| `RequisitosDeActivacionDeTesoreria.{Cuadre, Chequera, MesAbierto, MatrizCompleta, CuadreAsync}` · código `CheckbookIfChecks` · `offersChecks` | los de Tesorería | `Treasury/Activation/ActivacionDeTesoreria.cs` |
| `CuadreParaActivar` · `EstadoDeActivacionDto.{OpeningSquarePublicId, OpeningSquareKind, CutoffDate}` · `ActivateReceiptsCommand(CutoffDate, Minimal)` · `ActivateTreasuryCommand(CutoffDate, Minimal, OffersChecks)` · `ActivateReceiptsCommandHandler.{VersionesDelDestino, VersionesDelRecaudador}` · `ActivateTreasuryCommandHandler.VersionesDelDestino` | la activación completa y la mínima | íd. |
| `Receipts.Activation.MinimalNotAllowed` · `Treasury.Activation.MinimalNotAllowed` | la activación mínima no existe en producción | íd. |
| `IContabilidadParaRecaudo.CompletitudDeLaMatrizAsync` · `IContabilidadParaTesoreria.CompletitudDeLaMatrizAsync` · `CompletitudContableDeModuloDto` (`Cumple`) · `CompletitudDeModulo` | la completitud de la matriz para la activación, por el puerto | `Common/Integration/Accounting`, `Accounting/Receipts/ContabilidadParaRecaudo.cs` |
| `ReglasDeParametrosDeRecaudo.InicioDelCicloDeCartera` | el inicio del ciclo con que se comprueba el corte | `Receipts/Setup` |
| `GET /api/{receipts,treasury}/activation/requirements?minimal=&cutoffDate=&offersChecks=` · `POST …/activation { cutoffDate, minimal, offersChecks }` · pantallas `/recaudo/activacion`, `/tesoreria/activacion` · `ActivacionDeModuloClient` · `EstadoDeActivacionDeModuloDto` · `RequisitoDeActivacionDeModuloDto` · `ActivacionDeModuloRequest` | rutas y pantallas | `ActivationEndpoints.cs`, `Shared` |
| `RecaudoYTesoreriaE2E.{RegistrarYActivarAsync, PagarEnCajaAsync, IdentificarAbonoAsync, OrdenDeTesoreriaAsync, GirarOrdenAsync, RegistrarResultadoAsync, CuentaDePagosAsync}` · `CarteraE2E.RecaudoYTesoreriaRealesAsync` · `DesembolsoE2E.OrdenDeTesoreriaAsync` · `LibranzaE2E.{ArchivoEnRecaudoAsync, ResponderEnRecaudoAsync, ArchivosDeRecaudo}` | los ayudantes de las e2e sobre los módulos reales | `tests/IngenIA365ERP.API.IntegrationTests` |
| `RecaudoEnsayoDelContrato` (`/api/e2e/recaudo/*`) | el arnés de las e2e del contrato como Recaudo real (`REC`), sólo en el proyecto de pruebas | `tests/IngenIA365ERP.API.IntegrationTests/Receipts` |
| `ParticipantesRealesDeRecaudoYTesoreriaTests` | reemplaza a `DoblesDePruebaDeCarteraTests` | `tests/IngenIA365ERP.API.IntegrationTests/Lending` |

**Fijados en la construcción de la fase 20 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `VistaDeInformeDeRecaudoYTesoreria` · `FiltrosDeInformeDeRecaudoYTesoreria` (`from`, `to`, `asOf`, `branch`, `point`, `cashBox`, `account`, `groupBy`; rango hasta 366 días) · `Reports.ReceiptsTreasury.{RangeInvalid, FilterInvalid}` | lo común de los informes de los dos módulos | `Application/Common/Reports/InformesDeRecaudoYTesoreria.cs` |
| `VistasDeRecaudo` (11 vistas de R0) · `ReceiptsReportQuery` | el centro de informes de Recaudo | `Application/Receipts/Reports/InformesDeRecaudo.cs` |
| `VistasDeTesoreria.Vistas` (7 vistas de T0) · `TreasuryReportQuery` · evento `Treasury.BankBalances.Read` | el centro de informes de Tesorería | `Application/Treasury/Reports` |
| `ReceiptsReportsEndpoints` · `TreasuryReportsEndpoints` (`GET /api/reports/{receipts,treasury}` = registro de vistas) · eventos `Receipts.Report.Exported`, `Treasury.Report.Exported` | rutas y auditoría de la exportación | `API/Endpoints/Reports` |
| pantallas `/recaudo/informes`, `/tesoreria/informes` · `InformesDeRecaudoYTesoreriaClient` · `Las_paginas_delgadas_de_la_matriz_existen_protegidas_y_en_el_menu` | pantallas, cliente y prueba de arquitectura | `Shared`, `Architecture.Tests` |

**Fijados en la construcción de la fase 21 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `VerificacionDeSalidaDeRecaudo` (`TestReceiptPerPointAndMeans`, `TestReceiptPerConcept`, `SecondFactorCashiersAndSupervisors`, `CashierScopesAssigned`, prefijo `REQ.`) · `VerificacionDeSalidaDeTesoreria` (`TestDisbursementPerAccount`, `TestDisbursementPerForm`, `SecondFactorApprovers`, `PermisosDeAprobador`) · `VerificacionDeSalidaDeCartera` (módulo `CAR`, `CAR.*`, `APO.*`, `CAR.Activated`, `APO.Activated`) | las condiciones automáticas del go/no-go | `Receipts/Activation`, `Treasury/Activation`, `Lending/Activation` |
| `EstadoOperativoDeRecaudoParaElCierre` · `EstadoOperativoDeTesoreriaParaElCierre` · `CuadreProvisionalPendiente` (`PendienteAsync`, `PendienteDelCierre`, `AlertarAsync`, `CerrarAlertasAsync`, `Clave`, `PermisoDelEscalamiento`) · `CuadreProvisionalPendienteDto` | la guarda del cuadre provisional y su alerta | `Receipts/Activation/EstadoOperativoParaElCierre.cs`, `Treasury/Activation`, `Common/Cuadres` |
| `Accounting.Period.OpeningSquarePending` (`AccountingErrors.PeriodOpeningSquarePending`, `data.pending[] { module, code, message }`) | el cierre del mes de la activación negado | `Accounting/Posting/AccountingErrors.cs` |
| `AntiguedadDeRecaudo.CuadreProvisionalPendienteAsync` · `VigilanciaDeTesoreria.CuadreProvisionalPendienteAsync` · claves `{tipo}:{cuadre:N}` y `{tipo}:{cuadre:N}:vencido` | la alerta diaria que escala | `Receipts/Tareas`, `Treasury/Tareas` |
| vista `parallel-comparison` (`VistasDeRecaudo.ComparacionDelParalelo`) · conceptos `PARALELO.MEDIO`, `PARALELO.PRODUCTO` · `VistasDeRecaudo.ProductosDelParalelo` | el informe del paralelo | `Receipts/Reports/InformesDeRecaudo.cs` |
| `AdjuntosDeModulo.EvidenciaDeGoLive` (`GoLiveEvidence`) | evidencias y escenario de no-go parcial de una lista | `Attachments/Common/AdjuntosDeModulo.cs` |
| pantalla `/admin/go-no-go` · `GoNoGoClient` (`ManualesSugeridas`: `CRONOGRAMA`, `NOGO-PARCIAL`, `PARALELO`, `PRODUCCION-LIMPIA`, `RESPALDOS`, `CAPACITACION`) | la lista en la interfaz | `Shared/Pages/Administracion/GoNoGo.razor`, `Shared/Services/Core` |
| `docs/manual/{recaudo-manual-del-cajero, recaudo-manual-del-supervisor, recaudo-manual-del-analista, tesoreria-manual-del-tesorero, recaudo-y-tesoreria-contabilizacion, recaudo-cumplimiento-sarlaft}.md` · `docs/operaciones/{recaudo-tesoreria-salida-en-vivo-cooflopal, recaudo-tesoreria-paralelo-en-qa, recaudo-tesoreria-despliegue, recaudo-tesoreria-go-no-go}.md` | manuales y runbooks de R0/T0 | `docs/` |

**Fijados en la construcción de la fase 23 (2026-10-24, base de R1)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `PartidaBancariaCitada` (`BankItemPublicId`, `MovementDate`, `OwnBankAccountPublicId`) · `PagoAAplicar.BankItem` · `ConsignacionAAplicar.BankItem` · `ContratosDeIntegracion.VersionConPartidaBancaria` · `HuellasDeLaVersion2.SinPartidaNula` | la v2 de los contratos de obligaciones y de cuentas | `Common/Integration/Lending`, `Common/Integration/ContratosDeIntegracion.cs` |
| `ErroresDePagos.{CodigoPartidaConOtraFecha, PartidaConOtraFecha, PartidaSinVersion2}` · `ReglasDeFechaValor.ValidarAsync(..., conPartidaBancaria)` · `ConsignacionEnCuentas.FechaValorAsync(..., sinVentana)` | las reglas de fecha valor de la v2 | `Lending/Payments`, `Lending/Deposits/Contributions` |
| `LND_LoanOperations.BankItemPublicId` · `LND_MemberAccountOperations.BankItemPublicId` · `IX_LND_LoanOperations_BankItemPublicId` · `IX_LND_MemberAccountOperations_BankItemPublicId` | la partida guardada | par `CarteraYCaptacionesConPartidaCitada` |
| `IConsultaDeCargaSinActivar` · `CargaSinActivarDto` · `ObligacionCargadaDto` · `ConsultaDeCargaSinActivar` (`Lending.Migration.InAppliedLoad`, `.NotInAppliedLoad`) | la consulta de la carga aplicada sin activar (noveno puerto ofrecido) | `Common/Integration/Lending`, `Lending/Integration` |
| `CancelacionConEntregaPorElContrato` · `Lending.Savings.CloseAmountChanged` · simulación `Operation = "Close"` · `SimulacionDeCuentaDto.AmountToDeliver` | la cancelación con entrega por el contrato de cuentas (E-02 d) | `Lending/Deposits/Accounts`, `Lending/Integration/ContratoDeCuentasDeCartera.cs` |
| `AdoptContractVersionCommand` · `VersionesDeContratosDto` · `POST /api/receipts/activation/contract-versions` · `EscritorDelRegistroDeIntegracion.AdoptarAsync` · evento `Receipts.ContractVersionAdopted` · `VersionesDeContratosDeRecaudoDto` | Recaudo adopta las v2 | `Receipts/Activation/AdopcionDeVersiones.cs`, `Common/Integration`, `Shared/Services/Recaudo` |
| `ConservacionDeAuditoria` (`ModuloDeParametros`, `AniosAsync`, `VenceEl`, `Defecto`) · `AuditEventDocument.RetentionYears` · `AuditoriaEncadenada.EscribirCarga` | `Conservacion.Anios` en la auditoría (piso de cinco) | `Common/Audit`, `Common/Interfaces/Audit` |
| `RecaudoCompleto` · `CarteraYCaptacionesConPartidaCitada` | los dos pares de R1 | `Persistence.Migrations.{PostgreSql,SqlServer}/Application` |

**Fijados en la construcción de la fase 24 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `CatalogoDeRecaudo.EntregaVigente = R1` | la entrega desplegada de Recaudo | `Domain/Receipts/CatalogoDeRecaudo.cs` |
| `CapturaDeMedios` (`ValidarAsync`, `PareceNumeroDeTarjeta`) · `Receipts.PaymentMeans.{CardCaptureIncomplete, CardNumberNotAllowed, ReferenceRequired}` | la captura de tarjetas, bonos y «otro» | `Receipts/CashDesk/CapturaDeMedios.cs`, `Receipts/Common/ErroresDeRecaudo.Recibos.cs` |
| tercero del `MedioPorCobrar` = persona del adquirente (`EmisionDeRecaudo.AdquirentesDeLosMediosAsync`) | el débito de las tarjetas en `ReciboEmitido` | `Receipts/Accounting/EmisionDeRecaudo.Recibos.cs` |
| casos `arqueo-tarjetas-01..03` | casos dorados del arqueo de R1 | `tests/IngenIA365ERP.Domain.Tests/Receipts/Casos` |

**Fijados en la construcción de la fase 25 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RCD_PendingApplications` (`PendingApplication`) · `RCD_PendingApplicationResolutions` (`PendingApplicationResolution`) · `PendingApplicationState` | el recaudo pendiente de aplicar | par `RecaudoCompleto`; `Domain/Enums/Receipts/ReceiptsR1Enums.cs` |
| `ReglasDelPendiente` · `ErroresDelPendiente` (`Receipts.PendingApplication.{NotEligible, ModuleNotActivated, NotPending}`, `Receipts.Voiding.PendingApplicationApplied`) · `ApplyPendingApplicationCommand` · `ListPendingApplicationsQuery` · `PendingApplicationDto` · `PendientesDeRecaudo` | reglas, errores, comando y consulta del pendiente | `Receipts/CashDesk/RecaudoPendienteDeAplicar.cs` |
| `ItemPedidoDto.{PendingTarget, LoadObligationPublicId, ProductReference}` · `ItemDeCajaDto` igual | el destino `PendingApplication` en la caja | `Receipts/CashDesk/CajaDtos.cs` |
| `PreselectPayablesCommand` · `POST /api/receipts/cash-operations/{id}/preselect?includeCoDebtor=` | F4, todo lo exigible en una acción | `Receipts/CashDesk/PreseleccionDeExigibles.cs` |
| `RecaudoPendienteReconocidoV1` (`RPR`) · `RecaudoPendienteAplicadoV1` (`RPA`) · rol `RecaudoPendienteDeAplicar` · `EmisionDeRecaudo.{RecaudoPendienteReconocidoAsync, RecaudoPendienteAplicadoAsync}` · roles `CuentasPorCobrarComerciales`, `GmfGasto`, `GmfPorPagar` (para las fases 26 y 30) | los hechos de R1 y sus roles | `Common/Integration/Contracts/Receipts/HechosDeR1V1.cs`, `HechosDeCajaV1.cs`, `Receipts/Accounting/EmisionDeRecaudo.Pendientes.cs` |
| `SendReceiptNoticeCommand` · `AvisosDeRecaudo` · `AvisoDeRecaudoDto` · `ResultadosDeAviso` (`Sent`, `NotAuthorized`, `NoDestination`, `Simulated`) · `POST /api/receipts/receipts/{id}/notify` · evento `Receipts.Receipt.Notified` | el aviso por correo | `Receipts/Notifications/AvisosDeRecaudo.cs` |
| permisos `Receipts.PendingApplications.View` · `.Apply` · `GET /api/receipts/pending-applications` · `POST …/{id}/apply` · pantalla `/recaudo/pendiente-de-aplicar` · evento `Receipts.PendingApplication.Applied` | rutas, permisos y pantalla | `API/Endpoints/Receipts/CashDeskEndpoints.cs`, `Shared/Pages/Recaudo/RecaudoPendienteDeAplicar.razor` |

**Fijados en la construcción de la fase 26 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RCD_CashDisbursements` (`CashDisbursement`, con `ProductNature`) · `RCD_CashDisbursementVoidings` (`CashDisbursementVoiding`) | el egreso de caja y su anulación | par `RecaudoCompleto` |
| `RegisterCashDisbursementCommand` · `VoidCashDisbursementCommand` · `ListCashDisbursementsQuery` · `GetCashDisbursementDocumentQuery` · `CashDisbursementDto` · `EgresoModel` · `EgresosDeCaja` · `ErroresDeEgreso` (`Receipts.Disbursement.{InsufficientCash, DailyCashOutCapReached, MiscExpensesDisabled, ConceptNotExpense, OverConceptMax, KindNotSupported, ModuleRejected, CloseAmountMismatch, AlreadyVoided, NotSameCashBox}`) | comandos, consultas y errores | `Receipts/Disbursements/EgresosDeCaja.cs` |
| `ReglasDeEgresoDeCaja` · `GmfDeEgreso` | el motor puro del GMF de Recaudo, el tope diario y el efectivo suficiente | `Domain/Receipts/Egresos/ReglasDeEgresoDeCaja.cs` |
| `CashMovementKindR.CashDisbursement = 8` · `.CashDisbursementReturn = 9` · `PayerCreditBalanceMovementKind.CashRefundVoided = 6` | el egreso en el arqueo y la reapertura del saldo | enums de Recaudo |
| `ApprovalSubjects.CashDisbursementApproval` (`Receipts.Disbursements.Approve`, con monto) | el egreso vario y la anulación de un egreso | `Domain/Approvals` |
| `Caja.EgresosVarios` · `Gmf.Tarifa` · `Gmf.HechosGeneradoresDeEgresos` · `Gmf.Redondeo` en `RCD` (R1) | los parámetros de los egresos | `ParametrosDeRecaudoYTesoreria` |
| `EgresoDeCajaEntregadoV1` (`EGE`) · `GmfDeEgresoCausadoV1` (`GEC`) · `EgresoDeCajaAnuladoV1` (`EGA`) · `EmisionDeRecaudo.{EgresoDeCajaEntregadoAsync, GmfDeEgresoCausadoAsync, EgresoDeCajaAnuladoAsync}` · tipo de comprobante `EG` · tipo de documento `EG` (clase 7) | los hechos del egreso | `HechosDeR1V1.cs`, `EmisionDeRecaudo.Egresos.cs` |
| `CodigosDelContratoDeCuentas.PendienteDeAprobacion` | el código del contrato de cuentas que el recaudador distingue | `Common/Integration/Lending/ContratoDeCuentasDtos.cs` |
| `RegistroDeEfectivo.{AlEgresarAsync, AlAnularEgresoAsync}` · `IDocumentosDeRecaudoEnPdf.Egreso` · rutas `/api/receipts/cash-disbursements` · pantalla `/recaudo/egresos` · eventos `Receipts.Disbursement.{Delivered, Voided}` | registro, documento, rutas y pantalla | `Receipts/Compliance`, `API/Endpoints/Receipts/DisbursementsEndpoints.cs`, `Shared/Pages/Recaudo/Egresos.razor` |

**Fijados en la construcción de la fase 27 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RCD_IdentificationRules` (`IdentificationRule`) · `RCD_ReportedDeposits` (`ReportedDeposit`) | reglas de identificación y consignación reportada | par `RecaudoCompleto`; `Entities/Receipts/Banks/R1Banks.cs` |
| `MotorDeIdentificacion` · `AbonoAIdentificar` · `ReglaDeIdentificacion` · `CandidatoPorReferencia` · `CandidatoReportado` · `CandidatoExigible` · `PropuestaDeIdentificacion` | el motor puro de propuestas | `Domain/Receipts/Identificacion/MotorDeIdentificacion.cs` |
| `PropuestasDeIdentificacion` · `GetBankItemProposalQuery` · `ApplyIdentificationRulesOnLoadCommand` · `ListIdentificationRulesQuery` · `SaveIdentificationRuleCommand` · `RegisterReportedDepositCommand` · `VerifyReportedDepositCommand` · `ListReportedDepositsQuery` · `ConsignacionesReportadas` · `ErroresDeIdentificacion` (`Receipts.IdentificationRule.ApplyOnLoadOnlyByReference`, `Receipts.ReportedDeposit.{NotCollectionAccount, NotToVerify, AmountMismatch, Rejected}`) | servicios, comandos y errores | `Receipts/BankItems/IdentificacionPorReglas.cs` |
| `IdentifyBankItemCommand.IdentificationRulePublicId` · `PedidoDeAbono.RuleId` | la regla que confirmó la persona | `Receipts/BankItems/BankItemCommands.cs` |
| `Identificacion.AplicarAlCargar` · `ConsignacionReportada.{Modo, DiasParaVerificar, DiasDeVentana}` (RCD, R1) · alerta `Recaudo.ConsignacionReportadaSinVerificar` · `AntiguedadDeRecaudo.ConsignacionesReportadasAsync` | parámetros y aviso | `ParametrosDeRecaudoYTesoreria`, `TiposDeAlerta`, `Receipts/Tareas` |
| rutas `/api/receipts/identification-rules`, `/api/receipts/bank-items/{lineId}/proposal`, `/api/receipts/bank-items/apply-on-load`, `/api/receipts/reported-deposits` · pantallas `/recaudo/reglas-de-identificacion`, `/recaudo/consignaciones-reportadas` · eventos `Receipts.IdentificationRule.Saved`, `Receipts.BankItems.AppliedOnLoad`, `Receipts.ReportedDeposit.{Registered, Verified}` | rutas, pantallas y auditoría | `API/Endpoints/Receipts/IdentificationEndpoints.cs`, `Shared/Pages/Recaudo` |

**Fijados en la construcción de la fase 28 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RCD_PaymasterCalendar` (`PaymasterCalendarEntry`: `PayrollPayDate`, `BusinessDays`, `LegalRemittanceDate`, `SubPeriod`) | la fecha de pago de la nómina y la fecha legal de giro | par `RecaudoCompleto`; `Entities/Receipts/Paymasters/PaymasterCalendarEntry.cs` |
| `CalendarioLegalDeGiro` · `EstadoDelPeriodoDePagaduria` | el motor puro del calendario | `Domain/Receipts/Pagadurias/CalendarioLegalDeGiro.cs` |
| `RegisterPaymasterPayDateCommand` · `GetPaymasterCalendarQuery` (con `OnlyLate`, el informe de morosas) · `PaymasterCalendarRowDto` · `CalendarioDePagadurias` · `AntiguedadDeRecaudo.GirosDePagaduriaTardiosAsync` · evento `Receipts.Paymaster.PayDateRegistered` | comando, consulta y aviso | `Receipts/Paymasters/CalendarioDePagadurias.cs`, `Receipts/Tareas` |
| rutas `GET/POST /api/receipts/paymaster-calendar` · sección «Calendario de giro» de `/recaudo/pagadurias` | rutas y pantalla | `API/Endpoints/Receipts/PaymastersEndpoints.cs`, `Shared/Pages/Recaudo/Pagadurias.razor` |

**Fijados en la construcción de la fase 29 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RCD_CashTransactionReports` (`CashTransactionReport`) · `RCD_CashTransactionReportLines` (`CashTransactionReportLine`, con `Source`) | el reporte de transacciones y sus líneas | par `RecaudoCompleto`; `Entities/Receipts/Compliance/CashTransactionReports.cs` |
| `ReporteDeTransacciones` · `TransaccionReportable` · `LineaDelReporte` | el armado puro del reporte | `Domain/Receipts/Cumplimiento/ReporteDeTransacciones.cs` |
| `uiaf-transacciones-2027-01.json` (`UIAF-TRANSACCIONES-v1`) · `CatalogoDeLayoutsDeRecaudo` | la estructura como dato, sin cotejar | `Receipts/Compliance/Layouts`, `Receipts/Compliance/ReporteDeTransaccionesCommands.cs` |
| `PreparacionDelReporteDeTransacciones` · `GeneracionDelReporteDeTransacciones` · `GenerateCashTransactionReportCommand` · `RegenerateCashTransactionReportCommand` · `MarkCashTransactionReportUploadedCommand` · `GetCashTransactionReportFileQuery` · `ListCashTransactionReportsQuery` · `ErroresDelReporteDeTransacciones` (`Receipts.CashTransactionReport.{AlreadyUploaded, LayoutMissing, NotGenerated}`; avisos `Recaudo.Sarlaft.{LayoutSinCotejar, ReporteSuspendido, SinTransacciones}`) | la generación y sus comandos | `Receipts/Compliance/ReporteDeTransaccionesCommands.cs` |
| `IEfectivoDelPuntoDeVenta` · `EfectivoDelPuntoDeVentaDto` · `EfectivoDelPuntoDeVenta` (Comercial) · `RegistroDeEfectivo.DelPuntoDeVentaAsync` | el registro único con el punto de venta (E-03) | `Common/Integration/Inventory`, `Inventory/Integration`, `Receipts/Compliance/RegistroDeEfectivo.cs` |
| adjunto `ReceiptsCashTransactionReport` · `AuditoriaDeRecaudo.ReporteDeEfectivoLeidoAsync` · eventos `Receipts.Sarlaft.{ReportGenerated, ReportUploaded}` · rutas `/api/receipts/compliance/cash-transaction-reports` · sección «Reporte de transacciones» de `/recaudo/cumplimiento` | adjunto, auditoría, rutas y pantalla | `Attachments/Common/AdjuntosDeModulo.cs`, `API/Endpoints/Receipts/ComplianceEndpoints.cs`, `Shared/Pages/Recaudo/Cumplimiento.razor` |

**Fijados en la construcción de la fase 30 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RCD_CommercialReceivableApplications` (`CommercialReceivableApplication`: `InvoiceNumber`, `AccountingDocumentPublicId`, `ConsultedBalance`) | el detalle de la aplicación `CommercialReceivable` | par `RecaudoCompleto`; `Entities/Receipts/Operations/CommercialReceivables.cs` |
| `ContabilidadParaRecaudo.CuentasPorCobrarComercialesAsync` · `RolCuentasPorCobrarComerciales` | la consulta de Contabilidad por tercero y factura | `Accounting/Receipts/ContabilidadParaRecaudo.cs` |
| `CuentasPorCobrarDeRecaudo` · `CommercialReceivableDto` · `CommercialReceivableApplicationDto` · `GetCommercialReceivablesQuery` · `ListCommercialReceivableApplicationsQuery` · `ErroresDeCuentasPorCobrar` (`Receipts.CommercialReceivable.{NotFound, OverBalance, InvoiceRequired}`) | lo pendiente neto de Recaudo, la consulta, el informe y los errores | `Receipts/CashDesk/CuentasPorCobrarComerciales.cs` |
| rutas `GET /api/receipts/commercial-receivables?person=` y `…/applications` · «Cobrar» factura en `/recaudo/caja` | rutas y pantalla | `API/Endpoints/Receipts/CashDeskEndpoints.cs`, `Shared/Pages/Recaudo/Caja.razor` |

**Fijados en la construcción de la fase 31 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `IRecaudoDeNominaParaConsolidado` · `RecaudoDeNominaDelDiaDto` · `RecaudoDeNominaParaConsolidado` (Nómina) | lo descontado por Nómina, de sólo lectura (E-05) | `Common/Integration/Payroll`, `Payroll/Integration` |
| `IDebitoAutomaticoParaConsolidado` · `DebitoAutomaticoDelDiaDto` · `DebitoAutomaticoParaConsolidado` (Cartera) | el débito automático del día, de sólo lectura (D26; enmienda E266 de la 014) | `Common/Integration/Lending`, `Lending/Integration` |
| vistas `consolidated-collection` · `voidings-with-reissue` · `delinquent-paymasters` · `received-checks` · `cash-transactions` · `pending-application-load` (`VistasDeRecaudo.{Consolidado, AnulacionesConReemision, PagaduriasMorosas, ChequesRecibidos, TransaccionesEnEfectivo, CargaDelPendiente}`) | las vistas de R1 | `Receipts/Reports/InformesDeRecaudo.cs`, `InformesDeRecaudoR1.cs` |
| `GetReceiptsDashboardQuery` · `TableroDeCajaDto` · `CifraDelTableroDto` · ruta `GET /api/receipts/dashboard` · pantalla `/recaudo/tablero` | el tablero de caja sin datos personales | `Receipts/Reports/TableroDeCaja.cs`, `API/Endpoints/Receipts/DashboardEndpoints.cs`, `Shared/Pages/Recaudo/Tablero.razor` |

**Fijados en la construcción de la fase 32 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `TrasladoEntreCuentasEnviadoV1` (`TEE`) · `TrasladoConfirmadoV1` (`TCF`) · `ChequesReclasificadosAlCierreV1` (`CRC`, con `IsReversal`) · `ChequeNoCobradoACuentaPorPagarV1` (`CNP`) | los hechos de T1, con el comprobante sugerido `TB` | `Common/Integration/Contracts/Treasury/HechosDeTesoreriaT1V1.cs`, `CatalogoDeMensajesDeTesoreriaV1`, `CatalogoDeLaMatriz` |
| `RolesDeTesoreriaT1.{FondosEnTransito, ChequesPorEntregar, CuentaPorPagarChequesNoCobrados}` | los roles de T1, por cuenta propia | ídem |
| tipo de comprobante `TB` («Notas y traslados bancarios») · `CatalogoDeTesoreria.EntregaVigente = T1` · par `TesoreriaCompleta` | la base de T1 | `voucher-types.json`, `Domain/Treasury/CatalogoDeTesoreria.cs`, `Persistence.Migrations.*` |

**Fijados en la construcción de la fase 33 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `TES_Transfers` (`Transfer`) · `TES_MonthEndCheckRuns` (`MonthEndCheckRun`, con `ReversalPublicId`) · `TES_IssuedChecks.{MovedToPayableRunId, MovedToPayableOn}` · `INV_DayCloses.RemittancePublicId` | traslados, cierre del mes de cheques y la remesa del punto de venta | par `TesoreriaCompleta`; `Persistence/Configurations/Treasury/T1/TesoreriaCompletaConfigurations.cs` |
| `TransferState` · `ManualOrderApprovalState` · `BankAccountAuthorityState` · `ReconciliationRecordKind.Transfer = 7` | enumeraciones de T1 | `Enums/Treasury/TreasuryT1Enums.cs`, `TreasuryReconciliationEnums.cs` |
| sujetos de aprobación `TreasuryTransfer` y `BankAccountAuthorityChange` (fase 34) · `AprobableTraslado` | la aprobación diferida del traslado | `Domain/Approvals`, `Treasury/Transfers` |
| `RequestTransferCommand` · `ApproveTransferCommand` · `RejectTransferCommand` · `MarkTransferSentCommand` · `ConfirmTransferCommand` · `VoidTransferCommand` · `ListTransfersQuery` · `TrasladosDeTesoreria` · `TransferDto` | los traslados | `Treasury/Transfers/TrasladosEntreCuentas.cs` |
| `RunMonthEndChecksCommand` · `ListMonthEndCheckRunsQuery` · `CierreDelMesDeCheques` · `MonthEndCheckRunDto` | el cierre del mes de los cheques | `Treasury/Checkbooks/CierreDelMesDeCheques.cs` |
| `EmisionDeTesoreria.{TrasladoEnviadoAsync, TrasladoConfirmadoAsync, ChequesReclasificadosAsync, ChequeNoCobradoACuentaPorPagarAsync, DimensionesDeCuentaAsync}` | los hechos de T1 | `Treasury/Accounting/EmisionDeTesoreria.Traslados.cs` |
| errores `Treasury.Transfer.{SameAccount, AccountInactive, InvalidState, SelfApproval, SupportRequired, StatementLineMismatch}` · `Treasury.MonthEndChecks.{AlreadyRun, MonthNotEnded}` · `Inventory.DayClose.{RemittanceUnavailable, RemittanceOverCash, RemittanceRejected}` | los errores | `Treasury/Common/ErroresDeTesoreria.T1.cs`, `Inventory/Cash/ErroresDeCaja.cs` |
| `ExecuteDayCloseCommand.Remittance` (`RemesaDelCierreDelDia`) | la remesa del punto de venta por Tesorería (E-03, T1) | `Inventory/Cash/DayCloseCommands.cs` |
| rutas `/api/treasury/transfers` (`/{id}/approve`, `/reject`, `/sent`, `/confirm`, `/void`) · `GET/POST /api/treasury/month-end/checks` · pantallas `/tesoreria/traslados`, `/tesoreria/cierre-de-cheques` · `TrasladosClient` · eventos `Treasury.Transfer.{Requested, Sent, Confirmed, Voided}`, `Treasury.MonthEndChecks.Run` | rutas, pantallas y auditoría | `API/Endpoints/Treasury/TransfersEndpoints.cs`, `Shared/Pages/Tesoreria` |

**Fijados en la construcción de la fase 34 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `TES_ManualOrderConcepts` (`ManualOrderConcept`) · `TES_BankAccountSigners` (`BankAccountSigner`) · `TES_BankAccountTokens` (`BankAccountToken`), base `BankAccountAuthority` · columnas de la orden manual en `TES_PaymentOrders` (`ManualOrderConceptId`, `SupportAttachmentPublicId`, `WithholdingAmount`, `PreparedByUserId`, `ManualApprovalState`, `FirstApprovedByUserId/At`, `SecondApprovedByUserId/At`, `ManualRejectionReason`) | catálogo, firmantes, tokens y la orden manual | par `TesoreriaCompleta` |
| `CreateManualPaymentOrderCommand` · `ApproveManualPaymentOrderCommand` · `RejectManualPaymentOrderCommand` · `ListManualPaymentOrdersQuery` · `ListManualOrderConceptsQuery` · `SaveManualOrderConceptCommand` · `OrdenesManuales` · `ManualPaymentOrderDto` · `ManualOrderConceptDto` | las órdenes manuales | `Treasury/Orders/OrdenesManuales.cs` |
| `RegisterBankAccountSignerCommand` · `RegisterBankAccountTokenCommand` · `RetireBankAccountAuthorityCommand` · `DecideBankAccountAuthorityCommand` · `ListBankAccountAuthoritiesQuery` · `FirmantesYTokens` · `AprobableCambioDeFirmantes` · `VigilanciaDeFirmantes` · `BankAccountAuthorityDto` | firmantes y tokens | `Treasury/BankAccounts/FirmantesYTokens.cs` |
| rol `RetencionEnLaFuentePorPagar` (posible en `PagoDeOrdenGirado`) · `EmisionDeTesoreria.LineasDelPago` | la retención de la orden manual al girar | `HechosDeTesoreriaT1V1.cs`, `CatalogoDeLaMatriz`, `EmisionDeTesoreria.Pagos.cs` |
| permisos `Treasury.ManualOrders.ManageConcepts` y `Treasury.Signers.Approve` · alerta `Tesoreria.FirmanteRetirado` · motivo de pendiente `Treasury.ManualOrder.PendingApproval` | permisos, alerta y bandeja | `ReceiptsAndTreasuryPermissionCatalogSeeder`, `TiposDeAlerta`, `ValidadorDeOrdenes` |
| errores `Treasury.ManualOrder.{ConceptInactive, OverConceptCap, BeneficiaryIsPreparer, BeneficiaryIsApprover, DeriveToAccountingPayable, InvalidWithholding, SelfApproval, SameApprover, NotPendingApproval, PendingApproval}` · `Treasury.Signer.{SelfApproval, InvalidState, AlreadyActive}` · `Treasury.Token.SerialInUse` | los errores | `Treasury/Common/ErroresDeTesoreria.T1.cs` |
| rutas `/api/treasury/manual-order-concepts` · `/api/treasury/manual-orders` (`/{id}/approve`, `/reject`) · `/api/treasury/bank-accounts/{id}/{signers,tokens}` (`/{authorityId}/retire`, `/approve`, `/reject`) · pantallas `/tesoreria/ordenes-manuales`, `/tesoreria/firmantes` · `OrdenesManualesClient` · eventos `Treasury.ManualOrder.{Prepared, Approved, Rejected}`, `Treasury.ManualOrderConcept.Saved`, `Treasury.Signer.{Requested, RetirementRequested}`, `Treasury.Token.{Requested, RetirementRequested}` | rutas, pantallas y auditoría | `API/Endpoints/Treasury/ManualOrdersEndpoints.cs`, `Shared/Pages/Tesoreria` |

**Fijados en la construcción de la fase 35 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `TES_PaymentVouchers` (`PaymentVoucher`, clase de numeración `CE`) · `ComprobantesDePago` · `ListPaymentVouchersQuery` · `GetPaymentVoucherDocumentQuery` · `SendPaymentVoucherCommand` · `IComprobanteDePagoEnPdf` · `ComprobanteDePagoModel` · `PaymentVoucherDto` · `AvisoDeTesoreriaDto` · evento `Treasury.PaymentVoucher.Sent` | el comprobante de pago | par `TesoreriaCompleta`; `Treasury/Results/ComprobantesDePago.cs`, `API/Reports/DocumentosDeTesoreriaReport.cs` |
| rutas `GET /api/treasury/payment-vouchers`, `GET /{id}/document`, `POST /{id}/send` · pantalla `/tesoreria/comprobantes-de-pago` · `ComprobantesDePagoClient` | rutas y pantalla | `API/Endpoints/Treasury/PaymentVouchersEndpoints.cs`, `Shared/Pages/Tesoreria/ComprobantesDePago.razor` |
| `AvisoOperativoDelCierre` (`Accounting.Period.ReconciliationsOpen`, `Accounting.Period.CashSessionsOpen`) · `ClosePeriodCommand.AcknowledgeOperationalPending` · `AccountingErrors.PeriodOperationalPending` | el aviso al cierre contable (D29) | `Common/Cuadres/AvisoOperativoDelCierre.cs`, `Accounting/Periods/PeriodCommands.cs` |
| vistas `payment-vouchers` · `transfers` · `month-end-checks` (`VistasDeTesoreria.{ComprobantesDePago, Traslados, CierreDeCheques}`) | las vistas de T1 | `Treasury/Reports/InformesDeTesoreriaT1.cs` |

**Fijados en la construcción de la fase 36 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `CatalogoDeRecaudo.EntregaVigente = R2` · par `RecaudoCanales` · ámbitos de lectura `CollectionAgreementFile` y `PaymentGatewayFile` en `BankFileFormatValidator` | la base de R2 | `Domain/Receipts/CatalogoDeRecaudo.cs`, `Common/BankFiles/BankFileFormatValidator.cs`, `Persistence.Migrations.*` |

**Fijados en la construcción de la fase 37 (2026-10-24)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RCD_ClearingBatches` (`ClearingBatch`, numeración `CN`) · `RCD_ReceivedChecks.{ClearingBatchId, PaidByStatementLinePublicId}` · `ClearingBatchState` | el lote de canje | par `RecaudoCanales`; `Entities/Receipts/Operations/ClearingBatch.cs`, `Enums/Receipts/ReceiptsR2Enums.cs` |
| `CreateClearingBatchCommand` · `MatchClearingBatchStatementCommand` · `ListClearingBatchesQuery` · `CanjeDeCheques` · `ClearingBatchDto` · errores `Receipts.ClearingBatch.{CheckNotEligible, MixedBranches, StatementLineMismatch, NothingInClearing, AmountMismatch}` | el canje completo | `Receipts/Checks/CanjeDeCheques.cs` |
| `BankItemOutcome.{ClearingBatch = 7, AgreementSettlement = 8}` · `RecaudoParaConciliacion.{ClaseCanje, ClaseConvenio}` | el abono que liquida un lote o un convenio | `Enums/Receipts/ReceiptsBankEnums.cs`, `Receipts/Integration/RecaudoParaConciliacion.cs` |
| enumeraciones de R2 `CollectionAgreementKind`, `PaymentReferenceMode`, `CheckDigitAlgorithm`, `PaymentReferenceState`, `CollectionFileState`, `CollectionFileLineOutcome`, `AgreementReconciliationState` (para la fase 38) | convenios y referencias | `Enums/Receipts/ReceiptsR2Enums.cs` |
| rutas `GET/POST /api/receipts/clearing-batches`, `POST /{id}/statement-match` · sección «Canje de cheques» de `/recaudo/cheques-recibidos` · eventos `Receipts.ClearingBatch.{Created, Paid}` | rutas, pantalla y auditoría | `API/Endpoints/Receipts/ClearingBatchesEndpoints.cs` |

**Fijados en la construcción de la fase 38 (2026-10-26)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `RCD_CollectionAgreements` (`CollectionAgreement`) · `RCD_CollectionAgreementVersions` (`CollectionAgreementVersion`) · `RCD_PaymentReferences` (`PaymentReference`) · `RCD_AgreementFiles` (`AgreementFile`) · `RCD_AgreementFileLines` · `RCD_GatewayFiles` (`GatewayFile`) · `RCD_GatewayFileLines` · `RCD_AgreementReconciliations` (`AgreementReconciliation`) · bases `CollectionFileBase`/`CollectionFileLineBase` · `UnidentifiedItemOrigin.AgreementFile = 4` | convenios, referencias, archivos y conciliación | par `RecaudoCanales`; `Entities/Receipts/Agreements/CollectionAgreements.cs`, `Persistence/Configurations/Receipts/ConveniosConfigurations.cs` |
| `DigitoDeVerificacion` · `EstructuraDeReferencia` · `LecturaDeReferencia` · `MotivoDeReferenciaInvalida` · `ConciliacionNeta` · `ExplicacionDeDiferencia` · `RetencionPracticada` | el motor puro | `Domain/Receipts/Convenios` |
| `SaveCollectionAgreementCommand` · `ListCollectionAgreementsQuery` · `ConveniosDeRecaudo` · `CollectionAgreementDto` · `ErroresDeConvenios` (`Receipts.Agreement.{AccountNotCollection, FormatInvalid, KindLocked, VersionRetroactive, NoCurrentVersion, Inactive}`, `Receipts.AgreementFile.{AlreadyLoaded, IllegibleLines, Empty, TotalsMismatch}`, `Receipts.AgreementReconciliation.{StatementLineMismatch, FileAlreadyReconciled, NotPendingApproval, SelfApproval}`, `Receipts.PaymentReference.ProductNotFound`) | los convenios | `Receipts/Agreements/ConveniosDeRecaudo.cs` |
| `ReferenciasDePagoDeRecaudo` · `IReferenciasDePago` (`ReferenciaDePagoDelProductoDto`) · `IssuePaymentReferenceCommand` · `ListPaymentReferencesQuery` · `PaymentReferenceDto` · `ReferenciaResuelta` | la referencia de pago | `Receipts/Agreements/ReferenciasDePago.cs`, `Common/Integration/Receipts/IReferenciasDePago.cs` |
| `LoadCollectionFileCommand` · `ListCollectionFilesQuery` · `ArchivosDeConvenio` (numeración `AF` y `GF`) · `MotivosDeLineaDeConvenio` (`Receipts.AgreementFile.{ReferenceInvalid, CheckDigitInvalid, ReferenceNotFound, PersonNeedsIdentification, PaymentAlreadyLoaded}`, `Receipts.GatewayFile.NotApproved`) · `PartidasPorIdentificar.ReconocerDeArchivoAsync` | la carga del archivo | `Receipts/Agreements/ArchivosDeConvenio.cs` |
| `ReconcileCollectionFileCommand` · `ApproveAgreementReconciliationCommand` · `ListAgreementReconciliationsQuery` · `ConciliacionesDeConvenio` (numeración `AC`) · `RetencionPedida` | la conciliación del convenio | `Receipts/Agreements/ConciliacionDelConvenio.cs` |
| `ComisionDeConvenioRegistradaV1` (`CCR`, comprobante `RB`) · `RolesDeRecaudoR2.{ComisionesDeConvenio, IvaDescontable, RetencionesAFavor}` · `RetencionDelHechoV1` · `EmisionDeRecaudo.ComisionDeConvenioAsync` | el hecho | `Common/Integration/Contracts/Receipts/HechosDeR2V1.cs`, `Receipts/Accounting/EmisionDeRecaudo.Convenios.cs` |
| parámetros `Referencias.DigitoDeVerificacion` y `Convenios.ToleranciaDeConciliacion` (RCD, R2) · plantilla `recaudo.convenios` · tipos de documento `RV` y `RL` (semilla del par) · `IdentifyBankItemCommand.InsurerClaim` (evento `Receipts.BankItem.InsurerPayment`) | parámetros, plantilla, semilla y aseguradora | `ParametrosDeRecaudoYTesoreria.cs`, `Receipts/Imports`, `Receipts/BankItems/BankItemCommands.cs` |
| adjuntos `ReceiptsDisbursementSignature`, `ReceiptsReportedDepositSupport`, `ReceiptsCashTransactionReportReceipt`, `ReceiptsAgreementFile` · `AdjuntosDeRecaudo` (Shared) | los tipos de adjunto propios de Recaudo | `Attachments/Common/AdjuntosDeModulo.cs`, `Shared/Services/Recaudo/AdjuntosDeRecaudo.cs` |
| rutas `/api/receipts/collection-agreements` (`/{id}`, `/{id}/files`, `/files`, `/{id}/reconciliations`, `/reconciliations`, `/reconciliations/{id}/approve`), `/api/receipts/payment-references`, `/api/receipts/gateway-files` · pantallas `/recaudo/convenios`, `/recaudo/archivos-de-convenio` · `ConveniosClient` · eventos `Receipts.Agreement.{Created, Updated}`, `Receipts.AgreementFile.Loaded`, `Receipts.GatewayFile.Loaded`, `Receipts.AgreementReconciliation.{Registered, Approved}`, `Receipts.PaymentReference.Issued` | rutas, pantallas y auditoría | `API/Endpoints/Receipts/AgreementsEndpoints.cs`, `Shared/Pages/Recaudo` |
| `ConstructorDeDocumentosDeCartera.LeyendaDeReferenciaDePago` (dato `paymentReference`) | la referencia en el plan de pagos y el extracto | `Lending/Documents/ConstructorDeDocumentosDeCartera.cs` |

**Fijados en la construcción de la fase 39 (2026-10-26)**:

| Nombre | Qué | Dónde |
|---|---|---|
| vistas `channels` · `agreements` (`VistasDeRecaudo.{Canales, Convenios}`) | los informes de R2 | `Receipts/Reports/InformesDeRecaudoR2.cs` |
| «Aseguradora que paga» (`IdentificarAbonoRequest.InsurerClaim`) | el pagador aseguradora en la identificación de un abono (FR-200) | `Shared/Pages/Recaudo/Extractos.razor` |

**Fijados en la construcción de la fase 40 (2026-10-26)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `CatalogoDeTesoreria.EntregaVigente = T2` · par `TesoreriaAmpliada` · `TES_LegacyPendingLoads` (`LegacyPendingLoad`) · `LegacyPendingKind`, `LegacyPendingLoadState`, `BankNoteKind`, `BankNoteState`, `ReconciliationRuleCriterion`, `CashFlowDirection`, `AcquirerSettlementState`, `AcquirerBatchSource` | la base de T2 | `Domain/Treasury/CatalogoDeTesoreria.cs`, `Enums/Treasury/TreasuryT2Enums.cs`, `Persistence/Configurations/Treasury/T2/TesoreriaAmpliadaConfigurations.cs` |
| parámetros `NotasBancarias.ExigenAprobacion`, `Liquidez.SemanasDeProyeccion`, `Adquirentes.ToleranciaDeConciliacion` (TES, T2) | los defectos de T2 | `ParametrosDeRecaudoYTesoreria.cs` |
| ruta `/api/treasury/cash-flow-projections` · pantalla `/tesoreria/flujo-de-caja` | el flujo proyectado (no choca con el heredado `/cash-flow`) | fase 43 |

**Fijados en la construcción de la fase 41 (2026-10-26)**:

| Nombre | Qué | Dónde |
|---|---|---|
| `TES_ReconciliationRules` (`ReconciliationRule`) · `TES_BankNotes` (`BankNote`, numeración `NB`) · `TES_PayrollDispersions` (`PayrollDispersion`, nueva) · `ReconciliationRecordKind.{BankNote = 8, PayrollDispersion = 9, AcquirerSettlement = 10}` | la conciliación completa | par `TesoreriaAmpliada`; `Entities/Treasury/Reconciliation/ConciliacionCompleta.cs` |
| `CruceDeConciliacion.CruzarConReglas` · `ReglaDeCruce` · casos `cruce-reglas-*.json` | el cruce con reglas | `Domain/Treasury/Conciliacion/CruceDeConciliacion.cs` |
| `SaveReconciliationRuleCommand` · `ListReconciliationRulesQuery` · `RegisterBankNoteCommand` · `DecideBankNoteCommand` · `ListBankNotesQuery` · `ListBankNoteSuggestionsQuery` · `ConciliacionCompletaDeTesoreria` · `ErroresDeTesoreriaT2` (`Treasury.BankNote.{StatementLineMismatch, AmountMismatch, StatementLineHasNote, NotPendingApproval, SelfApproval}`) | reglas y notas | `Treasury/Reconciliation/ConciliacionCompleta.cs` |
| `NotaBancariaRegistradaV1` (`NBR`, comprobante `TB`) · `RolesDeTesoreriaT2.{ComisionesBancarias, InteresesBancarios, IvaDescontable, RetencionesAFavor, OtrosCargosBancarios}` · `EmisionDeTesoreria.NotaBancariaAsync` | el hecho de la nota | `Contracts/Treasury/HechosDeTesoreriaT2V1.cs`, `Treasury/Accounting/EmisionDeTesoreria.Notas.cs` |
| módulo de origen `NOM` (`ModulosDeOrigen.Nomina`) · `DispersionDeNominaGeneradaV1` · `CuentaDeOrigenDeLaDispersionV1` · `CatalogoDeMensajesDeNominaV1` · `EmisionDeNomina` · `ConsumePayrollDispersionCommand` | E-05: la dispersión de Nómina a Tesorería | `Contracts/Payroll/DispersionDeNominaV1.cs`, `Payroll/Integration/EmisionDeNomina.cs`, `Treasury/Integration/ConsumoDeLaDispersionDeNomina.cs` |
| `ListBankAccountsForExogenaQuery` · `CuentaParaExogenaDto` | la exógena de las cuentas propias | `Treasury/BankAccounts/ExogenaDeCuentasBancarias.cs` |
| permisos `Treasury.BankNotes.Approve`, `Treasury.Liquidity.View`, `Treasury.Liquidity.Manage` | nuevos | `ReceiptsAndTreasuryPermissionCatalogSeeder`, `PerfilesSugeridos` |
| rutas `/api/treasury/bank-notes` (`/suggestions`, `/{id}/approve`, `/{id}/reject`), `/api/treasury/reconciliation-rules` (`/{id}`), `/api/treasury/bank-accounts/exogena` · pantallas `/tesoreria/notas-bancarias`, `/tesoreria/reglas-de-conciliacion` · `ConciliacionCompletaClient` · eventos `Treasury.BankNote.{Registered, Approved, Rejected}`, `Treasury.ReconciliationRule.Saved` | rutas, pantallas y auditoría | `API/Endpoints/Treasury/CompleteReconciliationEndpoints.cs`, `Shared/Pages/Tesoreria` |

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
