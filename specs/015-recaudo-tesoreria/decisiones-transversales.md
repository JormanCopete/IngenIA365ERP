# Decisiones transversales — 015 Recaudo y Tesorería

**Feature**: 015 | **Fecha**: 2026-10-22 | Molde: `013/decisiones-transversales.md`

Este documento **manda** sobre cualquier otra redacción de nombres en los artefactos de la 015. Recoge los nombres
canónicos (§2), las decisiones transversales TR1–TR40 (§3), los choques entre frentes y cómo se resolvieron (§4) y las
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
- Módulos `Receipts` (código `REC`) y `Treasury` (código `TES`); prefijos `RCD_` y `TES_`; rutas `/api/receipts/*`,
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
  `RCD_ComplianceAlertsLog`, `RCD_Activations`, `RCD_OpeningSquares`, `RCD_OpeningSquareLines`, `RCD_ReferenceFigures`
  (67 tablas).
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
  `TES_Reconciliations`, `TES_ReconciliationMatches`, `TES_ReconcilingItems`, `TES_Activations`, `TES_OpeningSquares`,
  `TES_OpeningSquareLines`, `TES_ReferenceFigures` (28).
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
| `NumeradorDeRecaudo` · `NumeradorDeTesoreria` | numeración al final | Application |
| `DestinoRecaudo` · `DestinoTesoreria` | consumidores de `Receipts` y `Treasury` | Application/{Receipts,Treasury}/Integration |
| `ConfirmacionDeRecibo` | el flujo único de confirmación (validación → listas → validación previa → cerrojos → contratos → numeración → hechos → un `SaveChanges`) | Application/Receipts/CashDesk |
| `AplicacionNoInteractiva` | línea por transacción con su clave | Application/Receipts/BankItems |
| `RequisitosDeActivacionDeRecaudo` · `…DeTesoreria` | requisitos verificables | Application/*/Activation |
| `AlcanceDeRecaudoDeLaPeticion` · `AlcanceDeTesoreriaDeLaPeticion` | alcance | API/Services |
| `ValidadorDeOrdenes` | FR-109, FR-203, FR-210 | Application/Treasury/Orders |
| Puertos | `ICuentasBancariasPropias`, `IMovimientosBancariosParaRecaudo`, `IRemesasDeTesoreria`, `IOrdenesDePagoDeTesoreria`, `IRecaudoParaConciliacion`, `IPartidasDesdeConciliacion`, `IResultadoDeOrdenesDeRecaudo`, `IContabilidadParaRecaudo`, `IContabilidadParaTesoreria`, `IDimensionesDeRecaudo`, `IDimensionesDeTesoreria`, `IEstadoOperativoParaElCierre`, `ICuentasBancariasDePersona`, `IVerificacionDeSalida`, `IConsultaDeCargaSinActivar` (R1), `IEfectivoDelPuntoDeVenta` (R1) | [contracts/interfaces.md](contracts/interfaces.md) |

### 2.5 Tareas programadas (sólo proponen, preparan y alertan, FR-174)

| Tarea | Hora | Qué |
|---|---|---|
| `recaudo.lote-del-dia` | tras la hora de corte | dispara el lote contable resumido de recibos (lo hace Contabilidad); orden fijo FR-185 |
| `recaudo.cuadre-diario` | después del lote | `RCD_DailySquares` y alerta |
| `recaudo.sesiones-olvidadas` | hora de corte | marca `ForgottenOpen` y alerta |
| `recaudo.antiguedad` | diaria | partidas, saldos a favor, extractos sin cargar, contingencia por digitar, cuadre provisional, política en «advertir» |
| `tesoreria.vigilancia` | diaria | órdenes sin resultado, cheques sin cobrar, remesas sin confirmar, partidas conciliatorias antiguas, cuadre provisional, política en «advertir» |

El orden con el ciclo diario de Cartera: lote de Recaudo → cuadre diario → ciclo diario de Cartera (FR-185), coordinado
por `ProgramadorDeTareas` con dependencias declaradas. Ninguna tarea llama a los contratos ni registra resultados
(`NingunProcesoAplicaPagosNiInformaResultados`).

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
`Treasury.BankAccounts.View/Manage`, `Treasury.BankBalances.View`, `Treasury.Scope.Manage`,
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

## 3. Decisiones transversales

### Módulos y frontera

- **TR1** · Dos módulos `Receipts`/`Treasury`, códigos `REC`/`TES`, prefijos `RCD_`/`TES_` (R-02, R-03).
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
- **TR10** · Cerrojos en orden fijo: Recaudo → Cartera → Captaciones → Tesorería → numeración (R-12).
- **TR11** · Numeración al final, `COR_Sequences` (R-13).
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
- **TR37** · Auditoría encadenada con módulos `Receipts` y `Treasury`; lecturas sensibles con lista cerrada; corridas con
  evento resumen (R-32).
- **TR38** · Precisión por alias; nada en coma flotante (R-33).
- **TR39** · Caja en tres pasos con atajos, en Web y MAUI, con `IndicadorDeCarga` (R-35).
- **TR40** · Migraciones pares por entrega, congeladas al promover R0/T0; el retiro heredado, destructivo con guarda,
  después de T2 (R-34).

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
| Q9 | ¿El paralelo en QA corre con las e2e de Cartera y Captaciones ya migradas a los módulos reales? | Sí: los dobles no existen en QA desde el despliegue de R0/T0 |
| Q10 | ¿La lista de go/no-go vive en la plataforma y sirve también para Cartera y Captaciones? | Sí (TR34) |

Los pendientes de datos del dueño P1 a P19 están en [research.md](research.md) «Pendientes del dueño que tocan el diseño»,
con lo que rige mientras tanto y lo que bloquea la salida.
