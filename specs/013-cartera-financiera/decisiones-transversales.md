# Decisiones transversales — 013 Cartera financiera

**Fecha**: 2026-10-05 · **Estado**: armonización de la fase 0 (ocho frentes investigados por separado) y conciliación de los artefactos del plan (§7, C01..C102).
**Fuente de verdad** para `research.md`, `data-model.md`, `contracts/*`, `quickstart.md`, `plan.md` y
`tasks.md` de la feature 013. Quien redacte esos artefactos **reutiliza literalmente** los nombres de la §2 y
respeta las decisiones T1..T102 de la §3. Si un borrador contradice este documento, gana este documento; si
este documento contradice la especificación o la constitución, ganan ellas y se corrige aquí.

Insumos leídos: `spec.md` (149 FR, 23 SC, 17 historias, D1–D29, I-1–I-6, enmiendas E-01–E-05), la
constitución (12 principios) y los ocho informes de la fase 0. Los informes no se publican: su contenido pasa
a `research.md`. Las siglas de origen de cada decisión son las de esos informes:

| Sigla | Frente |
|---|---|
| PM-01..PM-18 | Bandeja de mensajes, procesos de fondo, idempotencia, dobles de prueba |
| DC-01..DC-19 | Lado contable (enmienda E-01) |
| M1..M16 | Motor financiero (causación, plan, pagos, concurrencia, precisión) |
| R1..R18 | Riesgo, valores nacionales, cierres y perfil regulatorio |
| DE-01..DE-19 | Enmiendas a Nómina, Personas y Comercial (E-02 a E-04) |
| R-01..R-09 | Retiro de la cartera heredada (con guion, para no confundirlo con R1..R18) |
| S1..S11 | Seguridad, atribuciones, actas, segregación, habeas data y auditoría |
| F-01..F-20 | Adaptadores, mensajería al deudor, archivos, documentos, informes, plantillas y pantallas |

Decisiones del dueño que este documento da por fijas y no reabre: **Q1 B** (se espera a Recaudo y
Tesorería; no hay recaudo ni giros provisionales; sin ellos no hay activación en producción), **Q2 A**
(Anexo 1 en K1, pérdida esperada completa en K6a, vivienda en K6b; los dos modelos parametrizables por
cooperativa y segmento), **Q3** (las cuotas de servicios a asociados no son cartera, D29), los defectos D1–D28
y las interpretaciones I-1–I-6 (D22). COOFLOPAL no recibe ahorros: no está obligada a la pérdida esperada.

---

## 1. Cómo leer este documento

- **Manda sobre cualquier otra redacción de nombres.** Un nombre de tabla, entidad, motor, puerto, comando,
  mensaje, permiso, ruta, pantalla, error, parámetro, alerta, tarea o prueba que aparezca aquí se copia tal
  cual. Un informe o un borrador que diga otra cosa queda reemplazado; la §4 registra cada reemplazo.
- **Numeración.** Decisiones **T1..T102** (§3); choques entre informes **CH1..CH50** (§4); preguntas al dueño
  por área, de **A1** a **I15** (§5). Las tareas citan «T37» o «CH12», nunca el informe.
- **Revisión del 2026-10-05.** Dos revisiones críticas (constitución y spec; código y coherencia) corrigieron este
  documento: T101 reúne las desviaciones que el plan registra en `Complexity Tracking`, T102 la reclamación al
  seguro y la insolvencia, CH50 el momento de retirar las pantallas heredadas, y las enmiendas nuevas a la spec
  van en §5-A (A6–A11) y en T97.
- **Marcas.** `E` = entrega en que nace la pieza (K1, K2, K3, K4, K5, K6a, K6b). `H` = hecho inmutable
  (`IHechoInmutable`, sólo inserción). **(fijado aquí)** = nombre que ningún informe propuso y que este
  documento fija porque la spec exige la pieza; todo lo demás sale literal de un informe, de la spec o del
  código existente.
- **Lo que no nombra.** Columnas de detalle, DTO internos y rutas finas los nombra `data-model.md` o
  `contracts/*` con las reglas de T1–T4, sin contradecir lo de aquí.
- **Entregas.** K1 es lo mínimo para salir en vivo (además de Recaudo, Tesorería y, si la cooperativa está
  obligada, K6a). El plan puede reordenar K2–K6b y debe adelantar K6a antes de la primera cooperativa
  obligada (spec, «Entregas sugeridas»).

---

## 2. Nombres canónicos

### 2.1 Reglas de nombres (T1–T4)

- **Inglés**: entidades (PascalCase singular), tablas, columnas, enums del dominio (tipo y valores),
  comandos y consultas (verbo + entidad), DTO de rutas (sufijo `Dto`), rutas (`/api/{módulo}/{plural-kebab}`),
  permisos (`Módulo.Recurso.Acción`), códigos de error (`Área.Recurso.Motivo`), carpetas y espacios de
  nombres nuevos.
- **Español** (precedente de la 012): motores puros, servicios, puertos entre módulos **y sus DTO**, lectores,
  guardias, registros, pruebas de arquitectura, **tipos de mensaje** (español PascalCase, como la spec),
  claves de parámetro, tipos de alerta y nombres de tarea programada. Es una **desviación** de la constitución
  («identificadores en inglés»), heredada de la 012, que el plan registra en `Complexity Tracking` (T101).
- **DTO de los puertos**: los de **salida** llevan el sufijo `Dto` (`EstadoCrediticioDto`, `ResultadoDeReservaDto`,
  `RespuestaDelContratoDto<T>`); los de **entrada** (solicitudes, consultas y sus ítems: `SolicitudDeReserva`,
  `SolicitudDeAplicacion`, `PagoAAplicar`, `NoDescontado`, `ReferenciaDeRecibo`) no lo llevan, como
  `ConsultaCrediticia` y `SolicitudDeEmision` de la 012. También es desviación registrada (T101).
- **Enums de los puertos** (los que viajan entre módulos, en `Application.Common.Integration.Lending`): el
  **tipo** en español, como sus DTO; los **valores** en inglés e idénticos a los del motor cuando representan lo
  mismo (`ModalidadDePago.FullPayoff` ↔ `PaymentModality.FullPayoff`).
- **Sin tildes** en identificadores (`ObligacionCreada`, `RecuperacionDeCastigada`); la pantalla muestra la
  tilde.
- **Prefijos de tabla**: sólo los de la constitución. `LND_` (Cartera), `ACC_` (lado contable), `COR_`
  (plataforma en la base de la cooperativa), `ADM_` (base administrativa), `PAY_` e `INV_` (enmiendas). **No
  se crea** ninguna tabla `CMP_` nueva (el prefijo no está en la constitución): sólo se amplían las dos
  existentes con columnas.
- **Ningún nombre heredado se reutiliza.** Ninguna tabla ni entidad nueva lleva el nombre de una de las 93
  `LND_` heredadas (55 retiradas, 38 conservadas). La transacción de cartera es `LND_LoanTransactions`, el
  nombre literal del Principio XI.
- **Numeradores**: la propiedad se llama `NextValue` (nunca `NextNumber`, que vigila
  `NingunModuloEscribeMovimientosFueraDelContrato`).
- Toda entidad nueva hereda `AuditableEntity` o `AuditableEntityLong`, tiene `PublicId` y su configuración
  declara el filtro de borrado lógico. Los UK e IX de nombre largo se nombran con `HasDatabaseName` (PostgreSQL
  trunca a 63 caracteres).

### 2.2 Módulo de código y espacios de nombres (T3, T4)

| Pieza | Lo nuevo (Cartera de esta feature) | Lo heredado que se conserva |
|---|---|---|
| Entidades | `Domain/Entities/Lending/{Configuration, Origination, Governance, Loans, Schedules, Operations, Transactions, Projections, Integration, Risk, Payroll, Collections, Documents, Migration}` | Las 38 entidades conservadas pasan a `Domain/Entities/Savings` sin cambiar su `ToTable` |
| Enums | `Domain/Enums/Lending` | — |
| Motores puros | `Domain/Lending/{Rates, DayCount, Schedules, Accrual, Delinquency, Payments, Replay, Risk, Risk/Normativa, Attributions, Sessions, Segregation, Collections, Payroll, Documents}` | — |
| Plataforma pura | `Domain/Common/{Calendar, NationalValues, Layouts, Compliance}`, `Domain/Common/VigenciaBitemporal`; en `NationalValues` también los documentos normativos que la plataforma valida o consume (`CatalogoDeModalidades`, `TiposDeOrganizacion`, `CatalogoDeLimitesLegales`; T45) | — |
| Application | `Application/Lending/{Engine, Ledger, Accrual, Settlement, Payments, Common, Integration, Origination, Sessions, Attributions, Segregation, Disbursements, Risk, Payroll, Collections, Documents, Regulatory, Reports, Imports, Migration, Activation, Adapters}` | Handlers de ahorros, aportes, SIPLA, zonas y retiros pasan a `Application/Savings` |
| Lado contable | `Application/Accounting/Lending/{Reglas, Contabilizacion, Lotes, Consultas}` (espejo de `Accounting/Inventory`) | — |
| Puertos entre módulos | `Application/Common/Integration/Lending` (los de Cartera), `Application/Common/Integration/Accounting` (los de Contabilidad para Cartera), `Application/Common/Integration/Savings` (consulta de aportes) | `IConsultasDeCartera` y su stub siguen donde están |
| Contratos de mensaje | `Application/Common/Integration/Contracts/Lending/<Type>V1.cs` | `Contracts/Inventory` suma `VentaACreditoRegistradaV2` (es de Comercial, T18); los bytes de los contratos existentes no cambian (`ContratosDeMensajesTests`) |
| Plataforma Application | `Application/Common/{NationalValues, Calendar, Messaging, Compliance}` | — |
| Persistencia | `Persistence/Configurations/Lending`, `Persistence/Lending/{CerrojoDeCartera, SqlDelCerrojoDeCartera}`, `PrecisionDeCartera` | `Configurations/Savings` |
| API | `Endpoints/Lending/*.cs` (`/api/lending/*`), `Endpoints/Reports/LendingReportsEndpoints.cs` (reescrito), `Integration/ProcesadorDeCartera`, `Reports/DocumentosDeCarteraReport` | `Endpoints/Savings/*.cs` (`/api/savings/*`), `SavingsReportsEndpoints` |
| Adaptadores | Proyecto nuevo `src/Infrastructure/IngenIA365ERP.ExternalChannels` (`Messaging`, `Securities`, `CreditBureaus`, `RestrictiveLists`, `Credentials`, `Circuit`) | — |
| Pantallas | `Shared/Pages/Cartera` (`/cartera/*`) | `Shared/Pages/AhorrosYAportes` (mismas rutas `/cartera/...` de ahorros) |
| Auditoría | Módulos `Lending`, `Compliance`, `NationalValues` (encadenados, diez años) | Módulo nuevo `Savings` (regla «.Savings» antes de «.Core») |
| Retirado | `IngenIA365ERP.Legacy` entero; bloque `Lending` de `IdentitySeedData`; `Pages/CarteraFinanciera` de crédito | — |

### 2.3 Tablas nuevas de Cartera (`LND_`)

`E` = entrega; `H` = hecho inmutable. Todas viven en la base de cada cooperativa.

**Configuración, catálogos y alcance**

| Tabla | Entidad | E | Propósito (origen) |
|---|---|---|---|
| `LND_Setup` | `LendingSetup` | K1 | Fila única: cerrojo compartido de Cartera (exclusivo al cerrar o reabrir un mes), fecha de activación vigente (M13). |
| `LND_CreditLines` | `CreditLine` | K1 | Línea: código de la cooperativa (`CodigoDeCatalogo`), nombre, destino, estado; con créditos vivos no se borra, se cierra (R-06, FR-019, FR-032). |
| `LND_CreditLineVersions` | `CreditLineVersion` | K1 | Versión con vigencia y fecha de registro: código de modalidad y sub-segmento (del documento nacional, T44), tasas, montos, plazos, periodicidades, sistema, gracia, convención, equivalencia, redondeo, prelación, recálculo por notas, formas de pago, origen comercial, cupo y aportes, «exige consulta a centrales», bloque de puntaje, **concepto tributario del interés** (no gravado o gravado, con su soporte; D20, T44) (R-06, FR-019–FR-032, FR-037). |
| `LND_CommercialTermEquivalences` (fijado aquí) | `CommercialTermEquivalence` | K1 | Tabla de equivalencias de periodicidades y plazos de Comercial a los de Cartera (FR-022; hoja «Equivalencias» de `lending.credit-lines`). Una periodicidad sin equivalencia es error del mensaje (T17). |
| `LND_CreditLineRateBands` | `CreditLineRateBand` | K1 | Tasas por rango de monto, plazo o segmento (R-06, FR-020). |
| `LND_CreditLineDocumentRequirements` (fijado aquí) | `CreditLineDocumentRequirement` | K1 | Documentos por línea con sus condiciones (FR-025; hoja «Documentos» de F-16). |
| `LND_CreditLineInsurances` (fijado aquí) | `CreditLineInsurance` | K1 | Seguros: aseguradora (persona), base, tarifa, periodicidad, retención de prima (FR-028; hoja «Seguros»). |
| `LND_CreditLineCollateralRules` (fijado aquí) | `CreditLineCollateralRule` | K1 | Codeudores y garantías exigidos por rango (FR-026; hoja «Garantias»). |
| `LND_FactTypes` | `LendingFactType` | K1 | Catálogo sembrado de tipos de hecho; da el `PublicId` del ámbito `LendingFactType` (DC-11) y el código corto `ShortCode` de la `ScheduleKey` (C06). |
| `LND_NumberSequences` | `LendingNumberSequence` (fijado aquí) | K1 | Numeradores con `NextValue`: créditos, operaciones, documentos emitidos, actas, archivos (F-13, PM-10). |
| `LND_UserCreditLineScopes` | `UserCreditLineScope` | K1 | Líneas que opera cada usuario (S3). |
| `LND_AttributionMatrices` · `LND_AttributionLevels` · `LND_AttributionRules` | `AttributionMatrix` · `AttributionLevel` · `AttributionRule` | K1 | Matriz de atribuciones versionada, niveles individuales o colegiados y reglas por sujeto (S4). |
| `LND_GoverningBodies` · `LND_GoverningBodyMembers` | `GoverningBody` · `GoverningBodyMember` | K1 | Órganos (consejo, comités, gerencia) con quórum y mayorías con vigencia; miembros = personas del maestro (S5). |
| `LND_RelatedParties` | `RelatedParty` | K1 | Personas vinculadas con su fundamento (S, FR-040). |
| `LND_RiskPolicyVersions` | `RiskPolicyVersion` (H) | K1 | Política SARC del consejo, más estricta que la norma (R11). |
| `LND_ContributionBalanceLoads` | `ContributionBalanceLoad` | K1 | Saldo de aportes y ahorro permanente por plantilla, con fecha (D19; R-06, F-16). |

**Originación, garantías y cupos**

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `LND_CreditApplications` | `CreditApplication` | K1 | Solicitud (no reutiliza `LND_LoanApplications`) (R-06). |
| `LND_CreditApplicationParties` | `CreditApplicationParty` | K1 | Solicitante, codeudores y avalistas (R-06). |
| `LND_CreditApplicationDocuments` | `CreditApplicationDocument` | K1 | Documentos de la lista, adjuntos (R-06). |
| `LND_CreditApplicationChecks` | `CreditApplicationCheck` | K1 | Cada validación de FR-036 con su resultado y evidencia (R-06). |
| `LND_CreditScores` | `CreditScore` (H) | K1 | Puntaje con la explicación por variable (R-06, FR-037). |
| `LND_CreditDecisions` | `CreditDecision` (H) | K1 | Decisión de un nivel: sujeto, nivel, versión de matriz, resultado, condiciones, huella (S4). |
| `LND_Sessions` · `LND_SessionAttendances` · `LND_SessionItems` · `LND_SessionVotes` | `Session` · `SessionAttendance` · `SessionItem` · `SessionVote` (H) | K1 | Sesión con acta, asistencia, puntos y votos (S5; no reutiliza `LND_Minutes`). |
| `LND_CaseParticipations` | `CaseParticipation` (H) | K1 | Registro de intervenciones para la segregación (S7). |
| `LND_ScreeningResults` | `ScreeningResult` | K1 | Consulta a listas restrictivas por persona y operación, versión de la carga, resultado y liberación (F-18). |
| `LND_BureauQueries` | `BureauQuery` | K1 | Consulta a centrales con su evidencia (F-19). |
| `LND_Collaterals` | `Collateral` | K1 | Garantías (no reutiliza `LND_Guarantees`) (R-06, FR-043). |
| `LND_PromissoryNotes` | `PromissoryNote` | K1 | Pagaré físico o desmaterializado; canal sellado e id externo (R-06, F-19). |
| `LND_CreditLimits` | `CreditLimit` | K1 | Cupo consolidado, por línea y rotativo, con vigencia y motivo (R-06, FR-058). |
| `LND_DebtorBlocks` | `DebtorBlock` | K1 | Bloqueos y embargos (R-06, FR-060). |
| `LND_LoanNumberReservations` | `LoanNumberReservation` | K1 | Reserva de número ligada al pago de una venta; lleva el valor y descuenta cupo (PM-10, T22). |
| `LND_DebtorControls` (fijado aquí) | `DebtorControl` | K1 | Una fila por persona con algo en Cartera, sin saldos: fila del cerrojo por deudor que serializa reserva, aprobación, desembolso y asignación de cupo; se crea si falta (T34). |
| `LND_PrepaymentPreferences` | `PrepaymentPreference` (H) | K1 | Preferencia del deudor para abonos a capital, versionada (M). |

**Crédito, libro y proyecciones**

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `LND_Loans` | `Loan` | K1 | Crédito u obligación: número, deudor, oficina, línea, estado, marcas, origen (no reutiliza `LND_LoanPortfolios`). |
| `LND_LoanParties` | `LoanParty` | K1 | Codeudores del crédito (R-06). |
| `LND_LoanTermsVersions` | `LoanTermsVersion` (H) | K1 | Condiciones pactadas selladas por versión, con `RulesVersion` (M). |
| `LND_LoanOperations` | `LoanOperation` (H, bigint) | K1 | Hecho primario y **comprobante de la operación**: desembolso, saldo inicial, pago, reverso, reajuste, novedad, cargo, condonación, ajuste de Comercial, castigo, cambio de preferencia, resolución de saldo a favor. UK (`CollectorCode`, `IdempotencyKey`) (M2, T23). |
| `LND_LoanTransactions` | `LoanTransaction` (H, bigint) | K1 | Libro: una fila por concepto y cuota, tres fechas, `ReversesTransactionId`; objeto del Principio XI (M2). |
| `LND_RepaymentScheduleVersions` · `LND_RepaymentInstallments` | `RepaymentScheduleVersion` · `RepaymentInstallment` (H) | K1 | Plan materializado por versión (M2). |
| `LND_LoanControls` | `LoanControl` | K1 | Una fila por crédito sin saldos: `LastAccruedDate`, `Sequence`, `LastRegisteredAt`; fila del cerrojo (M2). |
| `LND_LoanDailyPositions` | `LoanDailyPosition` (H, bigint) | K1 | Instantánea versionada por crédito y día (I-6) (M2). |
| `LND_InstallmentNonDeductions` (fijado aquí) | `InstallmentNonDeduction` (H) | K1 | No aplicado de una cuota: crédito, cuota, recaudador, motivo, origen (`Contract` para el `NoDescontado` de la nómina propia, `Activation` para «Cartera sin activar»; desde K2 `PaymasterResult`, FR-115) y referencia al ítem que lo informó. Es la marca «no aplicado» del crédito y el estado de libranza de FR-069 (FR-112, FR-142; T92). |
| `LND_InsuranceClaims` (fijado aquí) | `InsuranceClaim` | K1 (datos) / K3 (proceso) | Reclamación al seguro de vida deudores por fallecimiento: crédito, aseguradora (persona), estado y cierre al aplicarse el pago de la aseguradora (FR-085, FR-097; T102). |
| `LND_InsolvencyProceedings` (fijado aquí) | `InsolvencyProceeding` | K1 (datos) / K3 (proceso) | Proceso de insolvencia del deudor: inicio de la negociación (suspende causación y cobro), acuerdo o fracaso (FR-098; T102). |
| `LND_DisbursementOrders` | `DisbursementOrder` | K1 | Orden de giro con beneficiario y cuenta sellados (R-06, FR-045). |
| `LND_DisbursementFiles` · `LND_DisbursementFileLines` | `DisbursementFile` · `DisbursementFileLine` (H) | K1 | Archivo de transferencia con SHA-256 (F-10). |

**Integración, procesos y mensajes al deudor**

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `LND_InboundMessages` | `LendingInboundMessage` | K1 | Recibo único por `MessagePublicId` de todo mensaje que llega a Cartera (PM-09). |
| `LND_InboundMessageEvaluations` | `LendingInboundMessageEvaluation` (H) | K1 | Cada evaluación de un mensaje recibido (PM-09). |
| `LND_AccountingFactParts` | `AccountingFactPart` | K1 | Parte de un hecho masivo: fecha o cierre, sucursal, tipo y tanda que la produjo; es el **origen** del mensaje (T12, T14) (PM-04). |
| `LND_ProcessRuns` · `LND_ProcessRunErrors` | `LendingProcessRun` · `LendingProcessRunError` | K1 | Corridas del ciclo diario y del cierre, con errores por crédito (PM-13; reemplaza `LND_AccrualRuns` de M14). |
| `LND_DebtorMessages` · `LND_DebtorMessageEvents` | `DebtorMessage` · `DebtorMessageEvent` (H) | K1 | Cola de mensajes al deudor e historia de cada envío (F-04). |

**Riesgo y cierres**

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `LND_MonthlyCloses` | `MonthlyClose` (H) | K1 | Cierre mensual y cierre base (`Kind` Base/Monthly), R sellado; nace en la transacción final de su corrida y nunca cambia: su estado se deriva (T47) (R13). |
| `LND_MonthlyCloseLoans` | `MonthlyCloseLoan` (H) | K1 | Línea por crédito del cierre, escrita por tandas y enlazada a la corrida; su vigencia se deriva de las reaperturas (T47) (R13). |
| `LND_MonthlyCloseReopenings` (fijado aquí) | `MonthlyCloseReopening` (H) | K1 | Reapertura de un cierre: motivo, atribución, quién y cuándo; de ella salen el estado «reabierto» del cierre y la vigencia de sus líneas (T47). |
| `LND_MonthlyCloseGeneralImpairments` | `MonthlyCloseGeneralImpairment` | K1 | Deterioro general por dimensión (R13). |
| `LND_LoanArrearsHistory` | `LoanArrearsHistoryEntry` (H) | K1 | Historial de mora mensual (R14). |
| `LND_AnalysisRatings` | `AnalysisRating` | K1 | Calificación por análisis (sólo peor) (R). |
| `LND_PortfolioEvaluations` · `LND_PortfolioEvaluationLoans` | `PortfolioEvaluation` · `PortfolioEvaluationLoan` | K3 | Evaluación de cartera (R). |
| `LND_ImpairmentModelElections` | `ImpairmentModelElection` | K6a | Adopción o desistimiento voluntario de la pérdida esperada (R15). |
| `LND_ArrearsHistoryLoads` | `ArrearsHistoryLoad` | K6a | Carga complementaria del historial de mora (R14, US8-10). |

**Libranza (K2), novedades (K3), cobranza (K4), documentos e informes**

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `LND_PayrollAgreements` · `LND_PayrollAgreementVersions` · `LND_PayrollAgreementContacts` · `LND_PayrollAgreementResultCodes` | `PayrollAgreement` · … | K2 | Pagaduría (persona jurídica), convenio versionado, contactos, códigos de respuesta (F-11). |
| `LND_PayrollDeductionFiles` · `LND_PayrollDeductionFileLines` | `PayrollDeductionFile` · `PayrollDeductionFileLine` (H) | K2 | Archivo de descuentos inmutable (F-11). |
| `LND_LoanNovelties` | `LoanNovelty` | K3 | Trámite de novedad por causa (R-06). |
| `LND_Waivers` | `Waiver` | K3 | Condonación (R-06). |
| `LND_WriteOffBatches` · `LND_WriteOffBatchItems` | `WriteOffBatch` · `WriteOffBatchItem` | K3 | Lote de castigo (R-06). |
| `LND_PriorNotices` | `PriorNotice` | K1 (datos) / K4 (proceso) | Aviso previo por episodio (F-09); nace en K1 para recibir lo migrado (FR-142). |
| `LND_CreditReportClaims` | `CreditReportClaim` | K1 (datos) / K4 (proceso) | Reclamo y suplantación sobre el reporte (S10); nace en K1 por la misma razón. |
| `LND_MessageCampaigns` | `MessageCampaign` | K4 | Campañas de mensajes (F-04). |
| `LND_CollectionStages` · `LND_CollectionStageHistory` · `LND_CollectorAssignments` | `CollectionStage` · `CollectionStageHistoryEntry` · `CollectorAssignment` | K4 | Etapas versionadas, historia de etapas y reparto (F-08). |
| `LND_CollectionActions` · `LND_PaymentPromises` · `LND_PaymentAgreements` · `LND_PaymentAgreementInstallments` | `CollectionAction` · `PaymentPromise` · `PaymentAgreement` · `PaymentAgreementInstallment` | K4 | Gestiones, promesas y acuerdos (F-08). |
| `LND_LegalProcesses` · `LND_LegalProcessActions` | `LegalProcess` · `LegalProcessAction` | K4 | Cobro jurídico (F-08). |
| `LND_IssuedDocuments` · `LND_IssuedDocumentLoans` · `LND_IssuedDocumentEvents` | `IssuedLendingDocument` · `IssuedDocumentLoan` · `IssuedDocumentEvent` (H) | K1 propuesto / K5 en la spec | Documentos emitidos con código de verificación (F-13; pregunta I5). |
| `LND_RegulatoryFiles` | `RegulatoryFile` | K5 | Generaciones del SICSES y de centrales (F-12). |

**Migración y activación**

| Tabla | Entidad | E | Propósito |
|---|---|---|---|
| `LND_MigrationLoads` · `LND_MigrationLoadParts` | `MigrationLoad` · `MigrationLoadPart` | K1 | Carga por corte (Draft, Applied, Replaced, Activated) y sus partes (F-17). |
| `LND_MigrationLoadDifferences` | `MigrationLoadDifference` | K1 | Diferencias del cuadre y de la conciliación de ventas, con su aceptación (D17) (R-06). |
| `LND_Activations` | `LendingActivation` (H) | K1 | Registro de la activación: fecha, carga, quién, acta (R-06). |
| `LND_Migration{Loans, Installments, Balances, RiskData, Restructurings, Collaterals, CoDebtors, CreditLimits, Blocks, Authorizations, PriorNotices, BureauReports, Claims, RelatedParties, ChargedOff, Sales, ArrearsHistory}` | `Migration{…}Row` | K1 | Preparación por parte (F-17); `Collaterals`, `CreditLimits` y `RelatedParties` renombran `Guarantees`, `Quotas` y `RelatedPersons` de F-17; `ArrearsHistory` (fijado aquí) recibe el historial opcional de FR-139. |

Total: 101 tablas `LND_` de negocio más 17 de preparación de la migración, **antes de la conciliación**. La §7 suma `LND_GoverningBodyRuleVersions`, `LND_DisbursementOrderResults` (H), `LND_LoanPayrollLinks` (K1) y `LND_ProvisionalCollections` (K1) (C44), marca como hechos (H) las entidades de C42 y pasa `LND_RepaymentInstallments` a `bigint` (C43); la cifra vigente es la de `data-model.md`.

### 2.4 Tablas nuevas del lado contable (`ACC_`, enmienda E-01)

| Tabla | Entidad | E | Propósito (DC-01) |
|---|---|---|---|
| `ACC_LendingPostingRules` | `LendingPostingRule` | K1 | Matriz de Cartera: operación, rol, dimensiones por código (`*` = cualquiera), cuenta, sucursal, centro, vigencia, `DimensionKey` (300), `SpecificityWeight` (int). Sin FK a `LND_`. |
| `ACC_LendingVoucherMappings` | `LendingVoucherMapping` | K1 | Clave `Operation`/`CollectorClass` (`*` = toda la operación) → tipo de comprobante (Module, CAR) y documento cruce. |
| `ACC_LendingPostings` | `LendingPosting` (H, bigint) | K1 | Recibo de cada hecho procesado; UK `MessagePublicId` sin filtro; `OriginUserCentralId` nulo admitido; `RelatedMessagePublicId`. |
| `ACC_RegulatoryIndicators` | `RegulatoryIndicator` | K1 | Patrimonio técnico y solvencia registrados por el contador con fuente y soporte (DC-18). |

### 2.5 Tablas nuevas de plataforma y de otros módulos

**Base administrativa** (migración Admin `ValoresNacionales`)

| Tabla | Entidad | E | Propósito (R1, R2) |
|---|---|---|---|
| `ADM_NationalValues` | `PlatformNationalValue` (H) | K1 | Valor nacional escalar: `Code`, `Key`, `ValidFrom`, `ValidTo?`, `ReferenceDate`, `Value` (20,10), `Unit`, `Source`, `Sequence`, `RegisteredAt`, `RegisteredBy`, `SupersedesPublicId`. |
| `ADM_RegulatoryDocuments` | `PlatformRegulatoryDocument` (H) | K1 | Documento normativo tipado: `Kind`, `Code`, `ValidFrom`, `Content` (JSON), `ContentSha256`, `SchemaVersion`, `Verified`. |

**Base de cada cooperativa** (`COR_`)

| Tabla | Entidad | E | Propósito (origen) |
|---|---|---|---|
| `COR_NationalValues` | `NationalValue` (H) | K1 | Copia del valor nacional (`SourcePublicId` y `SourceSequence` únicos, `KnownAt`) (R1). |
| `COR_RegulatoryDocuments` | `RegulatoryDocument` (H) | K1 | Copia del documento normativo (R1). |
| `COR_NationalValueReplications` | `NationalValueReplication` | K1 | Bitácora de cada réplica (R4). |
| `COR_NationalValueReviews` | `NationalValueReview` (H) | K1 | Conformidad del contador u oficial con una versión (R7). |
| `COR_RegulatoryProfileVersions` | `RegulatoryProfileVersion` (H) | K1 | Perfil regulatorio con vigencia (tipo de organización, actividad financiera, supervisión, categoría, grupo NIIF y registro como operador de libranza con número y fecha, que se valida antes de originar por libranza, FR-108) (R15). |
| `COR_IntegrationParticipants` · `COR_IntegrationParticipantAdoptions` | `IntegrationParticipant` · `IntegrationParticipantAdoption` | K1 | Registro de recaudadores y destinos por cooperativa, y versiones adoptadas (PM-07). |
| `COR_ExternalChannelSettings` | `ExternalChannelSetting` | K1 | Canal externo por cooperativa y clase, con `CredentialKey` y verificación (F-02). |
| `COR_RestrictiveListLoads` · `COR_RestrictiveListEntries` | `RestrictiveListLoad` · `RestrictiveListEntry` (H) | K1 | Carga provisional de listas restrictivas con vigencia (F-18). |
| `COR_DataSubjectRequests` | `DataSubjectRequest` | K1 | Solicitudes del titular con plazos (S10, con prefijo constitucional: CH23). |

**SEC_**: ninguna tabla nueva. Se reutiliza `SEC_PermissionAmountLimits` para las atribuciones operativas
(T63) y `SEC_UserBranchAssignments` para el alcance por oficina (T62).

**Otros módulos**

| Tabla | Entidad | E | Propósito (origen) |
|---|---|---|---|
| `PAY_PayrollLoanDeductions` | `PayrollLoanDeduction` | K1 | Descuento de Cartera de la nómina ordinaria por empleado y obligación: esperado, descontado, no descontado y motivo, comprobante y reverso (DE-07). |
| `INV_LendingObligationResponses` | `LendingObligationResponse` | K1 | Recibo de Comercial de `ObligacionCreada`/`ObligacionRechazada`: número y estado del crédito, nunca saldos (DE-14). |

### 2.6 Columnas, valores de enum y filas nuevas en lo existente

| Dónde | Cambio | E | Origen |
|---|---|---|---|
| `COR_IntegrationMessages` | `OriginActorKind` (`ActorKind`, defecto `Person`); `OriginUserCentralId` pasa a nulo | K1 | PM-02 |
| `COR_NotificationTemplates` | `Code`, `Module`, `Purpose`, `Version`, `ValidFrom`, `ValidTo`, `ProviderTemplateId`, `Variables`, `IsSeeded` | K1 | F-07 |
| `CMP_HabeasDataConsents` | `Purpose`, `ContactChannel`, `EvidenceAttachmentPublicId`, `Origin` | K1 | S10 |
| `CMP_HabeasDataPolicyVersions` | `Purpose` (una versión vigente por finalidad) | K1 | S10 |
| `PAY_SettlementDeductions` | + `ObligationPublicId`, `ObligationNumber`, `CarteraReversalPublicId`, `UK_PAY_SettlementDeductions_Termination_Obligation` (en `NominaRecaudaPorElContrato`); − `LoanPortfolioId`, su FK, `IX_…_LoanPortfolioId` y `UK_…_Termination_Loan` (en `RetiroDeLaCarteraHeredada`) | K1 | DE-09, R-02 (CH37) |
| `PAY_Novelties` | + `ObligationPublicId`, `ObligationNumber` con copia idempotente desde `LND_LoanPortfolios` (en `NominaRecaudaPorElContrato`; identifican la obligación de cada novedad `LoanDeduction`, T92); − `LoanPortfolioId` (en el retiro; la tabla real es `PAY_Novelties`) | K1 | R-02, T87 |
| `PAY_PayrollRuns` | `LendingActivationAckBy`, `LendingActivationAckAt` | K1 | DE-10 |
| `INV_DocumentPayments` | `ReservedLoanNumber`, `CreditLineCode`, `ReservationPublicId`, `ConsultationEvidence` (C67), `CreditWithoutLending` (este último sujeto a la enmienda A6) | K1 | DE-12, DE-13, DE-16 (CH11) |
| `COR_IntegrationParticipants` | `Destination`, `IsEnabled`; único `(Code, Role, Destination)`; códigos sembrados `NOM`, `CAR`, `TST`, `CNT`, `INV` (C35, C36) | K1 | PM-07 |
| `ADM_NationalValueCounters` (Admin) | contador con cerrojo de la `Sequence` común (C44) | K1 | R1 |
| `COR_Alerts` | oficina y línea del alcance (C68) | K1 | SC-015 |
| `PAY_Holidays` | `NationalValuePublicId` (C68) | K1 | T93 |
| `COR_PaymentMeans` | `CreditWithoutLending` (sujeto a la enmienda A6, T95) | K1 | DE-16 |
| `CatalogoDeMensajesV1` (INV) | + `VentaACreditoRegistradaV2` (`OriginModule = INV`, `AdmiteProceso = false`); cada entrada existente recibe los campos nuevos de `TipoDeMensaje` (T10) | K1 | T18 |
| `LND_SiplaGroupParameters` (conservada) | se suelta su FK a la línea heredada; la columna `CreditLineId` queda como dato | K1 | R-02 |
| `COR_BackgroundLeases` | fila `lending.process` | K1 | PM-13 |
| `COR_SystemSettings` | fila de aprobación `LND.RetiroHeredado.Aprobado` (a mano, tras el respaldo) | K1 | R-04 |
| `IntegrationDestinations` | `Inventory`, `Treasury`, `Receipts` | K1 | PM-06 (CH9, CH10) |
| `DeliveryAttemptOutcome` | `ValidationFailed = 5` | K1 | PM-09 |
| `BatchTrigger` | `LendingAccrualRun = 7`, `LendingMonthlyClose = 8` | K1 | DC-11 |
| `ParameterScopeKind` | `CreditLine = 6`, `LendingFactType = 7` | K1 | R17, DC-11 |
| `HolidayOrigin` (Nómina) | `National = 6` | K1 | R6 |
| `BankFileScope` | `LoanDisbursement = 4` (K1); `PayrollDeductionRequest = 5`, `PayrollDeductionResult = 6` (K2), marcados `SeleccionPorReferencia`: `SolapadoAsync` no les aplica el cruce de vigencias (T78) | K1/K2 | F-10, F-11 |
| `BankFieldSource` | `PayerEmployeeId`, `LoanNumber`, `InstallmentNumber`, `DueDate`, `NoveltyKind`, `PastDueAmount`, `PeriodYear`, `PeriodMonth`, `LibranzaDate`, `ResultCode`, `DeductedAmount`, `DeductionDate` | K2 | F-11 |
| `AuditoriaEncadenada.Modulos` · `AuditRetention.ModulosDeDiezAnios` | + `Lending`, `Compliance`, `NationalValues` | K1 | S8, R4 |
| `ModuloDeAuditoria` | reglas «.Compliance», «.NationalValues», «.Savings» antes de «.Core» | K1 | S8, R4, R-07 |
| `CatalogoDeParametros.Modulos` | + `ParametrosDeCartera.Modulo = "LND"` | K1 | R17 |
| `DefinicionDeParametro.DisponibleDesde` · `CatalogoDeParametros.EntregaVigente` | pasan de `EntregaDelComercio` a una entrega por módulo (`EntregaDeModulo`: módulo + orden), a la que se convierten `EntregaDelComercio` y `EntregaDeCartera` (T43) | K1 | R17 |
| `PermisosLimitables` | admite códigos por módulo (`Lending.*` además de `Inventory.*`) | K1 | S4, S6 |
| `SEC_Users.PersonId` | sin cambio de esquema; obligatorio para quien radique, decida, vote o desembolse | K1 | S7 |
| `CompanyAdmin` (rol integrado) | se excluyen de su glob `Lending.Decisions.*` y `Lending.Sessions.Vote` | K1 | S2 |
| `TransaccionExplicita` | método `HayEnCurso(db)` | K1 | DE-02 |

### 2.7 Enumeraciones (dominio en inglés; puertos con tipo en español)

**`Domain/Enums/Lending`**

| Enum | Valores | Origen |
|---|---|---|
| `LoanConcept` | `Principal`, `CurrentInterest`, `DefaultInterest`, `CurrentInterestInSuspense`, `DefaultInterestInSuspense`, `Insurance`, `CollectionFee`, `OtherCharge`, `Advance`, `CreditBalance` | M |
| `LoanTransactionKind` | `Disbursement`, `OpeningBalance`, `Accrual`, `AccrualAdjustment`, `SuspenseReclassification`, `PaymentAllocation`, `PaymentReallocation`, `AdvanceReceived`, `AdvanceApplied`, `CreditBalanceRecognized`, `CreditBalanceSettled`, `Charge`, `Waiver`, `CommercialAdjustment`, `WriteOff`, `Recovery` | M |
| `LoanOperationKind` | `Disbursement`, `OpeningBalance`, `Payment`, `PaymentReversal`, `DisbursementReversal` (fijado aquí, FR-048), `RateReset`, `TermsChange`, `Charge`, `Waiver`, `CommercialAdjustment`, `WriteOff`, `PreferenceChange`, `CreditBalanceResolution`, `ProvisionalCollection` (fijado aquí, FR-142) | M |
| `LoanStatus` (fijado aquí; estados de la spec) | `PendingDisbursement`, `Active`, `PaidOff`, `WrittenOff`, `Voided`, `Novated` | spec, Key Entities |
| `LoanOrigin` (fijado aquí) | `Origination`, `Commercial`, `Migration` | spec |
| `DisbursementOrderStatus` (fijado aquí) | `Issued`, `Disbursed`, `Rejected`, `CheckNotCashed`, `Voided` | FR-045 |
| `CollateralStatus` (fijado aquí) | `Constituted`, `Valid`, `Released`, `Extinguished`, `Executed` | FR-043 |
| `DayCountConvention` | `Thirty360 = 1`, `Actual365 = 2`, `Actual360 = 3` | M |
| `RateEquivalence` | `Compound = 1`, `Linear = 2` | M |
| `AmortizationSystem` · `GraceKind` · `GraceInterestTreatment` | `LevelPayment = 1`, `ConstantPrincipal = 2`, `Bullet = 3` · `None`, `Principal`, `Total` · `FirstInstallment`, `Distributed` | M |
| `PaymentModality` | `FullPayoff = 1`, `PastDue = 2`, `CycleInstallment = 3`, `Advance = 4`, `ExtraordinaryPrincipal = 5`, `SpecificInstallment = 6`, `FreeAmount = 7` | M |
| `PrepaymentChoice` · `AllocationOrder` | `ReduceTerm = 1`, `ReduceInstallment = 2` · `OldestInstallmentFirst = 1`, `ConceptFirst = 2` | M |
| `RoundingMoment` · `RoundingMode` | `PerConcept`, `PerInstallment`, `PerTotal` · `AwayFromZero`, `ToEven`, `Up`, `Down` | M |
| `RateKind` · `DefaultRateKind` | `Fixed`, `Variable` · `Fixed`, `RelativeToCurrent` | M |
| `LoanNumberReservationStatus` | `Reserved = 1`, `Used = 2`, `Released = 3`, `Expired = 4` | PM |
| `InboundMessageStatus` | `Processed = 1`, `ValidationFailed = 2`, `Rejected = 3`, `NotIncorporated = 4`, `ConfirmedByException = 5` | PM |
| `LendingProcessKind` · `LendingProcessRunStatus` | `DailyCycle = 1`, `MonthlyClose = 2`, `CloseReopen = 3`, `NationalValueCorrection = 4` (fijado aquí, T28) · `Requested = 0`, `Running = 1`, `Completed = 2`, `CompletedWithErrors = 3`, `Failed = 4` | PM |
| `NonDeductionOrigin` (fijado aquí) | `Contract`, `Activation`, `PaymasterResult` (K2) | T92 |
| `InsuranceClaimStatus` · `InsolvencyProceedingStatus` (fijados aquí) | `InProcess`, `Paid`, `Denied`, `Withdrawn` · `Negotiating`, `Agreement`, `Failed` | T102 |
| `RatingCategory` · `RatingSubcategory` | `A`..`E` · `None`, `E1`, `E2` | R |
| `RatingCause` | `Arrears`, `Model`, `Drag`, `Restructured`, `Evaluation`, `Analysis`, `SingleInstallment`, `NewLoan`, `Insolvency`, `Homologation` | R |
| `ImpairmentModel` · `ModelBasis` | `ArrearsAnnex1`, `ExpectedLossAnnex2` · `NotObligated`, `Obligated`, `Voluntary` | R |
| `MonthlyCloseKind` · `MonthlyCloseStatus` (calculado, no es columna) | `Base`, `Monthly` · `Closed`, `Reopened` (si tiene `MonthlyCloseReopening`) | R, T47 |
| `CreditDecisionOutcome` | `Approve`, `ApproveWithConditions`, `Deny`, `Return` | S4 |
| `RelatedPartyBasis` | `BoardMember`, `OversightBoardMember`, `LegalRepresentative`, `SignificantContributor`, `Spouse`, `Relative` | S |
| `CreditReportClaimKind` | `Claim`, `Impersonation` (estado `InProcess`, `Answered`, `TacitlyAccepted`, `Withdrawn`) | S10 |
| `DebtorMessageEventKind` · `DebtorMessagePurpose` · `MessageChannel` | `Queued`, `Deferred`, `Excluded`, `Sent`, `Delivered`, `Failed`, `Simulated`, `Cancelled` · `Collection`, `PriorNotice`, `Informative`, `DocumentDelivery`, `CollectionFeeNotice` · `Email`, `Sms`, `WhatsApp`, `Letter` | F-04 |
| `IssuedDocumentKind` · `IssuedDocumentStatus` (calculado) | `PaymentPlan`, `Statement`, `LoanClearance`, `GeneralClearance`, `BalanceCertificate`, `TaxCertificate` · `Valid`, `SubjectToFinality`, `Voided` | F-13 |
| `PayrollFileStatus` · `PayrollNoveltyKind` | `Generated`, `Superseded`, `Sent`, `Voided` · `New`, `Change`, `Cancellation`, `Unchanged` | F-11 |
| `RegulatoryFileKind` · `RegulatoryFileStatus` | `Sicses`, `CreditBureau` · `Validated`, `Generated`, `Superseded`, `Uploaded` | F-12 |
| `EntregaDeCartera` (precedente `EntregaDelComercio`) | `K1`, `K2`, `K3`, `K4`, `K5`, `K6a`, `K6b`; constante `CatalogoDeCartera.EntregaVigente` | R15 |

**Plataforma**: `IntegrationParticipantClass {Receipts = 1, Payroll = 2, Contributions = 3, Treasury = 4, Test = 5,
Accounting = 6, Lending = 7, Inventory = 8}` e `IntegrationParticipantRole {ObligationsCollector = 1,
MessageDestination = 2}` (`Domain/Enums/Integration`, PM-07); `RegulatoryDocumentKind {CatalogoDeModalidades,
TiposDeOrganizacion, SarcAnexo1, SarcAnexo2, ReglaDePerdidaEsperada}` (R2; los valores son los códigos del
documento); `ExternalChannelKind {Sms, WhatsApp, SecuritiesDepository, CreditBureau, RestrictiveLists}` y
`ExternalOutcome {Ok, Rejected, InProcess, NotFound, ChannelUnavailable, InvalidData}` (F-01, F-02);
`DataAuthorizationPurpose {GeneralTreatment, CreditProcessing, CreditBureauQuery, CreditBureauReport,
ContactChannel}` (S10). **Nómina**: `PayrollLoanDeductionStatus {Calculated, Applied, PartiallyApplied,
NotApplied, Rejected, Reversed}` (DE-07).

**Puertos de Cartera** (`Application.Common.Integration.Lending`; tipo en español, valores en inglés, CH40):
`ClaseDeRecaudador {Receipts, Payroll, Contributions, OwnLoanPayoff, Test}` (recaudo, nómina, aportes, recogida de
créditos propios, prueba: la lista cerrada de FR-087). La pagaduría externa **no** es recaudador: paga por Recaudo,
va como **pagador** y con la clase de medio `ExternalPaymasterDeduction` (FR-085). `OwnLoanPayoff` es la propia
Cartera y no se registra en el registro de participantes (T91); `ClaseDeMedioDeRecaudo` = los
valores de `PaymentMeansClass` de la 012 más `OwnPayrollDeduction`, `ExternalPaymasterDeduction` e
`InternalOffset` (FR-082); `ModalidadDePago` (valores de `PaymentModality`); `MotivoDeNoDescuento
{DeductionCap, InsufficientNet, Termination, Leave, Disability, DeductionPriority, Other}` (C56); `ResultadoDelContrato {Applied,
AlreadyApplied, Rejected, Answered}` (C28); `EstadoDeCartera {NotActivated, Active}`. La columna `CollectorClass` de la
matriz contable guarda el valor de `ClaseDeRecaudador` como texto.

### 2.8 Motores puros (sin IO, sin EF, `DateOnly`, sólo `decimal`)

| Motor | Carpeta | Qué hace | Origen |
|---|---|---|---|
| `MotorDeCartera` | `Domain/Lending` | Fachada estática (molde `MotorDeCosteo`) | M1 |
| `ReconstruccionDeCredito` + `EntradaDeReconstruccion`, `EstadoDelCredito`, `LibroTeorico` | `Domain/Lending/Replay` | Única fuente de saldos, cuotas, mora y suspensión a D con información hasta R | M3 |
| `Causacion`, `ReglaDeSuspension` | `Domain/Lending/Accrual` | Causación por diferencia de acumulados; suspensión y reanudación | M4, M9 |
| `AlturaDeMora` | `Domain/Lending/Delinquency` | Días de mora con tolerancia y fecha exigible | M9 |
| `AritmeticaDecimal`, `EquivalenciaDeTasas`, `TasaDelDia`, `ControlDeUsura` | `Domain/Lending/Rates` | Potencias, raíz y TIR en decimal; EA ↔ diaria y período; tasa topada; usura | M5, M7 |
| `ContadorDeDias` | `Domain/Lending/DayCount` | 30/360, real/365, real/360 (a excluida, b incluida) | M6 |
| `CalendarioHabil` | `Domain/Common/Calendar` | Fecha exigible del art. 829 con la lista de días no hábiles que recibe | M6, R5 (CH34) |
| `FechasDeVencimiento`, `GeneradorDePlan` | `Domain/Lending/Schedules` | Vencimientos por periodicidad; plan y su regeneración | M6, M8 |
| `Prelacion`, `AplicadorDePagos` | `Domain/Lending/Payments` | Prelación sellada; distribución en siete modalidades (simula y aplica) | M10 |
| `RedondeoDeCartera`, `ExplicacionDeCartera`, `HuellaDeCartera`, `VersionesDeReglas` | `Domain/Lending` | Unidad, modo y momento; explicación por tramo; SHA-256 canónico; `RulesVersion` | M8, M11, M15 |
| `MotorDeCalificacion`, `MotorDeDeterioroIndividual`, `MotorDeDeterioroGeneral` | `Domain/Lending/Risk` | Calificación en dos fases con arrastre; deterioro del Anexo 1; general | R12 |
| `MotorDePerdidaEsperada`, `FuncionLogistica` | `Domain/Lending/Risk` | Pérdida esperada del Anexo 2 (K6a); logística en decimal | R10, R12 |
| `SelectorDeModeloDeDeterioro`, `HomologacionDeCalificacion`, `PoliticaSarcEfectiva`, `HistorialDeMora` | `Domain/Lending/Risk` | Modelo por segmento; calificación contable; norma + política; historial y completitud | R12, R11, R14, R15 |
| `Anexo1`, `Anexo2`, `ReglaDePerdidaEsperada` | `Domain/Lending/Risk/Normativa` | Documentos tipados que sólo usa Cartera; la central los valida al registrarlos por `IValidadorDeDocumentoNormativo`, que Cartera implementa (T45) | R2, R9, R10 |
| `CatalogoDeModalidades`, `TiposDeOrganizacion`, `CatalogoDeLimitesLegales` | `Domain/Common/NationalValues` | Documentos tipados y catálogo de límites con su dirección de lo estricto, que la plataforma valida o consume (registro de `USURA`, perfil regulatorio, plazos del titular) (T45; la plataforma no depende de un módulo, CH34) | R2, R8, R16 |
| `EnrutadorDeAtribuciones`, `NoExceptuables` | `Domain/Lending/Attributions` | Nivel efectivo de una solicitud; lo que nunca admite excepción | S4 |
| `EvaluadorDeVotacion` | `Domain/Lending/Sessions` | Quórum, mayorías, 4/5 de la composición, exclusiones | S5 |
| `ReglasDeSegregacion` | `Domain/Lending/Segregation` | Conflictos por persona | S7 |
| `VentanaLegalDeContacto`, `RepartoDeGestores` | `Domain/Lending/Collections` | Ley 2300; reparto por número o saldo | F-05, F-08 |
| `NovedadesDeLibranza` | `Domain/Lending/Payroll` | Altas, cambios y cancelaciones del archivo (K2) | F-11 |
| `VigenciaDeDocumentos` | `Domain/Lending/Documents` | Qué documentos anula un reverso | F-13 |
| `VigenciaBitemporal` | `Domain/Common` | Regla única de lectura a D con información hasta R | R3 |
| `LayoutDeArchivo`, `EscritorPorLayout`, `ValidadorDeLayout` | `Domain/Common/Layouts` | Layouts regulatorios genéricos (la PILA no se toca) | F-12 |
| `CoincidenciaDeNombres`, `ReglaDeRevocacion`, `PlazosDelTitular` | `Domain/Common/Compliance` | Coincidencia por nombre normalizado; revocación con deber; plazos del titular | F-18, S10 |

### 2.9 Puertos, lectores, registros y servicios con nombre fijo

**Puertos entre módulos** (cada uno declara exactamente sus métodos; lo fijan pruebas de arquitectura)

| Puerto | Métodos | Implementación provisional → real | Quién lo usa | Origen |
|---|---|---|---|---|
| `IConsultasDeCartera` (existe) | `EstadoCrediticioAsync`, `EstadoDeValidacionAsync` | `ConsultasDeCarteraNoHabilitada` → `ConsultasDeCartera` | Comercial | 012, DE-12 |
| `IReservasDeCartera` | `ReservarAsync(SolicitudDeReserva)`, `LiberarAsync(SolicitudDeLiberacion)` → `ResultadoDeReservaDto` | `ReservasDeCarteraNoHabilitada` → `ReservasDeCartera` | Comercial | PM-10, DE-13 (CH11) |
| `IConsultaDeObligaciones` | `ObligacionesAsync`, `CuotasDeNominaAsync` | `ConsultaDeObligacionesSinActivar` → `ObligacionesDeCartera` (fijado aquí) | Personas, Nómina, Aportes (retiro), Cumplimiento | DE-01, R-03 (CH43) |
| `IContratoDeObligaciones` | `ListarAsync`, `SimularAsync`, `AplicarAsync`, `ReversarAsync`, `ResolverSaldoAFavorAsync`; `const VersionDelContrato = 1` | `ContratoDeObligacionesSinActivar` → `ContratoDeObligacionesDeCartera` (fijado aquí) | Recaudo, Nómina, Aportes, recogida, recaudador de prueba | DE-01, R-03 |
| `IContabilidadParaCartera` | `EvaluarAsync`, `SaldosDeCuentasMapeadasAsync`, `EstadoDelPeriodoAsync`, `IndicadoresDeLaEntidadAsync`, `CuentasBancariasAsync` | `ContabilidadParaCartera` | Cartera | DC-17 |
| `IDimensionesDeCartera` | catálogo de códigos y combinaciones en uso | `DimensionesDeCartera` | Contabilidad | DC-17 |
| `IConsultaDeAportes` | saldo de aportes y ahorro permanente | `ConsultaDeAportesNoDisponible` (fijado aquí; D19, responde «no disponible») | Cartera | R12 |

DTO de los puertos (español, `Application.Common.Integration.Lending`; entrada sin sufijo y salida con `Dto`, §2.1):
`SolicitudDeReserva`, `SolicitudDeLiberacion`, `ResultadoDeReservaDto`; `ConsultaDeObligaciones`,
`ObligacionesDePersonasDto`, `ObligacionesDePersonaDto` (con `MigratedNotActivated`, C26), `ObligacionDto`,
`ConsultaDeCuotasDeNomina`, `CuotasDeNominaDto`, `CuotaDeNominaDto`; `SolicitudDeAplicacion` (lote de
`PagoAAplicar`), `NoDescontado`, `ReferenciaDeRecibo`, `SolicitudDeReverso`, `RespuestaDelContratoDto<T>`,
`ComprobanteDeAplicacionDto` (DE).
`EstadoCrediticioDto` suma `TotalQuota`, `DaysPastDue`, `BlockReason`, `AllowsSale`; `LineaDeCreditoDto` suma
`Periodicities`; `EstadosDeValidacion` suma `NotIncorporated` (DE-12, PM-10).

**Plataforma**

| Pieza | Qué es | Origen |
|---|---|---|
| `IRegistroDeIntegracion` · `EscritorDelRegistroDeIntegracion` | Lector del registro de participantes; escritor que agrega sin guardar | PM-07 |
| `IVistaDeBandejaDelModulo` | Estrategia por módulo de la bandeja (alcance, modo, nombre del documento) | PM-15 |
| `CatalogoDeMensajes` (agregado) · `CatalogoDeMensajesDeCarteraV1` · `CatalogoDeMensajesHaciaCarteraV1` | Une `CatalogoDeMensajesV1` (INV, sin renombrar ni mover; suma `VentaACreditoRegistradaV2` y sus entradas reciben los campos nuevos de `TipoDeMensaje`) con CAR y con TES/REC | PM-01, DC-03 (CH7) |
| `IValoresNacionales` · `LectorDeValoresNacionales` | `LeerAsync(codigo, clave, fecha, informacionHasta?)`, `LeerUltimoPublicadoAsync`, `LeerSerieAsync` (fijado aquí, para la carga en bloque de T40) | R5 |
| `ICalendarioHabil` · `CalendarioDeDiasHabiles` | `EsHabil`, `SiguienteHabil`, `Contar` sobre `FESTIVO` replicado | R5 |
| `INormativaVigente` · `LectorDeDocumentoNormativo` | Documento tipado vigente con su `PublicId` y SHA-256 | R5 |
| `IValidadorDeDocumentoNormativo` (fijado aquí) | Punto de extensión por `RegulatoryDocumentKind`: valida el contenido al registrarlo en la central; Cartera registra los de `SarcAnexo1`, `SarcAnexo2` y `ReglaDePerdidaEsperada` (T45) | R2 |
| `IRegistroCentralDeValoresNacionales` | Lectura del registro central (sólo el replicador) | R5 |
| `ReplicacionDeValoresNacionales` · `ReplicateNationalValuesCommand` | Núcleo único de la réplica y su comando sin ruta | R4 |
| `IEfectoDeReplicacion` · `EfectoDeReplicacionEnNomina` · `EfectoDeReplicacionObligacionSobrevenida` | Efectos de la réplica en otros módulos, en la misma transacción | R4, R6 |
| `IAlcanceDeCartera` · `AlcanceDeCartera` · `AlcanceDeCarteraDeLaPeticion` · `FiltroDeAlcanceDeCartera` · `OficinasDelUsuario` | Alcance por oficina y línea | S3 |
| `IAsuntoDeActa` | Gancho de lo que decide un acta (`AlDecidirAsync`, `DescribirAsync`) | S5 |
| `IAutorizacionDeDatos` (ampliado) | `VigenteAsync(persona, finalidad, canal?)`, `Agregar(...)` sin guardar | S10 |
| `IListasRestrictivas` · `IProveedoresDeListas` | Consulta de listas (`Application/Common/Compliance`) | F-01, F-18 |
| `ICanalDeMensajeria` · `ICanalesDeMensajeria` · `CanalDeCorreo` | Mensajería saliente a personas (`Application/Common/Messaging`) | F-04 |
| `IDocumentoDeCarteraRenderer` · `DocumentosDeCarteraReport` | PDF de documentos de Cartera (QuestPDF en la API) | F-14 |
| `TransaccionExplicita.HayEnCurso` | Dice si hay transacción en curso | DE-02 |

**Cartera**

| Servicio | Qué es | Origen |
|---|---|---|
| `CargadorDeReconstruccion` | Lectura en bloque para 1..N créditos | M1 |
| `ISeriesRegulatorias` | Series a R para el motor, sobre los lectores únicos de T40 | M1 (CH48) |
| `RegistroDeCartera` | Único escritor del libro, de la marca de agua y de las posiciones | M1 |
| `ICerrojoDeCartera` · `CerrojoDeCartera` · `SqlDelCerrojoDeCartera` | Cerrojo por deudor y por crédito en orden fijo (T34) | M13 |
| `NumeradorDeCartera` | Único que toca `LND_NumberSequences` | F-13, PM-10 |
| `EmisionDeCartera` | Arma toda solicitud de emisión de Cartera (molde `EmisionDeInventario`) | PM, DC (CH42) |
| `ModoDePasoDeCartera`, `ValidacionPreviaContableDeCartera`, `FechaContable`, `ConciliacionContableDeCartera`, `SaldosContablesDeCartera` | Lado de Cartera de la integración contable (`Application/Lending/Integration`) | DC-09, DC-11, DC-15, DC-16 |
| `DestinoCartera` · `VersionesAceptadasDeCartera` | Consumidor del destino `Lending` | PM-09 |
| `RegistroDeIntervenciones` | Único escritor de `LND_CaseParticipations` | S7 |
| `LendingAuditEmitter` | Lecturas sensibles auditadas | S8 |
| `GuardiaDeCanalesExternos` · `CanalesExternosOptions` | Efecto legal de los canales simulados | F-03 |
| `RenderizadorDePlantillasDeMensaje` | Sustitución `{{Variable}}` desde lista blanca | F-07 |
| `LoanDisbursementLines` | Líneas del archivo de desembolsos | F-10 |
| `CatalogoDeLayoutsDeCartera` · `ProyeccionSicsesCartera` · `ProyeccionCentralDeRiesgo` | Layouts y proyecciones regulatorias (K5) | F-12 |
| `CatalogoDePlantillasDeCartera` | Plantillas `lending.*` | F-16 |
| `TesoreriaDePrueba` · `RecaudadorDePrueba` | Dobles de FR-089, sólo fuera de producción | PM-17 |

**Contabilidad** (`Application/Accounting/Lending`): `OperacionesDeCartera`, `RolesDeCuentaDeCartera`,
`OrigenesDeCartera` (`LendingDocument`, `LendingPostingBatch`), `ResolutorDeReglasDeCartera`,
`MatrizVigenteDeCartera`, `ConstructorDeLineasDeCartera` (sin IO), `AgrupadorDeResumidosDeCartera`,
`ConsumoDeCartera`, `TiposDeComprobanteDeCartera`, `ReglasDeLaMatrizDeCartera`, `ConjuntosDeCuentas`
(generalizado de la 012) (DC). **Nómina**: `PropuestaDeDescuentosDeCartera`, `RecaudoPorNomina` (DE-04,
DE-07). **Comercial**: `DestinoInventario` (consumidor de las respuestas de Cartera; CH9).

### 2.10 Comandos y consultas principales

Los comandos de pantalla de Cartera son `IOperacionIdempotente` (T21). Los consumidores no tienen ruta
(`LosComandosDeConsumoNoTienenRuta`). Las operaciones de originación, desembolso y novedad las nombra
`contracts/api.md` con el patrón verbo + entidad.

| Área | Comandos y consultas |
|---|---|
| Motor y libro | `RunDailyAccrualCommand` (una tanda del ciclo diario a la fecha d, sin ruta), `GetLoanSettlementQuery`, `GetLoanStatementQuery`, `SimulateRepaymentScheduleQuery`, `SimulateLoanPaymentQuery`, `ReverseLoanPaymentCommand` (reverso pedido por un usuario), `VerifyLendingLedgerIntegrityQuery` (M; sin `ApplyLoanPaymentCommand`: CH47) |
| Procesos | `RequestLendingProcessRunCommand` (202), `OrderMonthlyCloseCommand` (202), `ExecuteMonthlyCloseCommand` (sin ruta), `ReopenMonthlyCloseCommand`, `CreateBaseCloseCommand` (en la activación), `RunNationalValueCorrectionCommand` (sin ruta, una tanda de la corrida `NationalValueCorrection`, T28; fijado aquí) (PM-13, R13) |
| Mensajes | `ConsumeCommercialCreditMessageCommand`, `ConsumeTreasuryResultCommand`, `ConsumePayrollDeductionResultCommand` (K2) (sin ruta); `ReprocessMessagesCommand` y `SendNotApplicableMessagesCommand` (los de la plataforma) (PM-09, PM-15) |
| Plataforma | `RegisterIntegrationParticipantCommand`, `RetireIntegrationParticipantCommand`, `ListIntegrationParticipantsQuery` (PM-07); `RegisterNationalValueCommand`, `CorrectNationalValueCommand`, `RegisterRegulatoryDocumentCommand`, `ImportNationalValuesCommand` (R, en `Application/Saas/NationalValues`); `AddRegulatoryProfileVersionCommand` (R15); `ConfigureExternalChannelCommand`, `VerifyExternalChannelCredentialCommand` (F-02) |
| Riesgo | `AddRiskPolicyVersionCommand`, `ValidateRiskPolicyVersionCommand`, `SetAnalysisRatingCommand`, `LiftAnalysisRatingCommand`, `RegisterImpairmentModelElectionCommand` (K6a), `ImportArrearsHistoryCommand` (K6a) (R) |
| Contabilidad | `PostLendingFactsCommand`, `PostLendingSummaryGroupCommand` (sin ruta); `EvaluateLendingPostingQuery`, `GetLendingAccountBalancesQuery`, `GetAccountingPeriodStatusQuery`, `GetEntityIndicatorsQuery`; `CreateLendingPostingRuleCommand`, `AddLendingPostingRuleVersionCommand`, `DeactivateLendingPostingRuleCommand`, `ImportLendingPostingRulesCommand`, `GetLendingRulesTemplateQuery`, `SetLendingVoucherMappingCommand`, `ResolveLendingRuleQuery`, `GetLendingRulesCompletenessQuery`, `PreviewLendingBatchQuery`, `ListLendingPostingsQuery`, `GetLendingBatchTotalsQuery` (DC lo llamó `TotalesDeLoteDeCarteraQuery`), `ListPendingIntegrationMessagesQuery(módulo)`, `RegisterRegulatoryIndicatorCommand` (DC). Todos con verbo, como pide la constitución, aunque sus pares de la 012 (`InventoryAccountBalancesQuery`, `InventoryRulesCompletenessQuery`, `PendingInventoryMessagesQuery`) no lo llevan |
| Seguridad | `CreateSuggestedLendingRolesCommand` (S2 lo llamó `CrearRolesSugeridosDeCarteraCommand`), `ReleaseScreeningMatchCommand` (F-18; reemplaza `ReleaseRestrictiveListMatchCommand` de S11) |
| Adaptadores y documentos | `QueryCreditBureauCommand`, `RegisterDebtorMessageAttemptCommand`, `IssueLendingDocumentCommand`, `VoidLendingDocumentCommand`, `SendLendingDocumentCommand`, `VerifyLendingDocumentQuery` (F) |
| Informes | `GetLendingDashboardQuery`; `ValidateSicsesFileQuery`, `GenerateSicsesFileCommand`, `GenerateCreditBureauFileCommand`, `MarkRegulatoryFileUploadedCommand` (K5) (F) |
| Migración y activación | `ApplyMigrationLoadCommand` (F-17), `ActivateLendingCommand` (fijado aquí, FR-142) |

### 2.11 Mensajes y hechos (T10–T18)

Todos nacen en versión 1, salvo `VentaACreditoRegistrada` v2. Los records de Cartera viven en
`Application/Common/Integration/Contracts/Lending/<Type>V1.cs` con bloques comunes propios: `LoanRefV1`,
`LoanFactDimensionsV1` (incluye modelo de deterioro, segmento y unidad UVR desde V1), `ConceptAmountV1`,
`OriginalFactRefV1`, `ExternalDocumentRefV1` (clase como texto), `LoanFactLineV1`, `LoanPlanSummaryV1` (PM).
`originEventKey` sólo toma las formas existentes: `Confirmation`, `Confirmation:{publicId:N}` (partes,
evaluaciones), `Close:{versión}`, `Reopen:{versión}`. En el cierre y su reapertura el **origen de cada mensaje es la
parte** (`LND_AccountingFactParts.PublicId`) y la clave `Close:{versión}`/`Reopen:{versión}`: así dos partes del mismo
tipo no chocan en el único (origen, tipo, clave) de la bandeja (T12, T14). Ningún contenido lleva cuentas contables
(FR-126).

**Hechos de Cartera a Contabilidad** (`CAR → Accounting`). Modo por defecto según D8 y DC-11 (T56).

| `Type` | Kind | Lo origina | Unidad y relación | Modo por defecto | Comprobante | E |
|---|---|---|---|---|---|---|
| `CreditoDesembolsado` | Business | `LoanOperation` Disbursement (cada tramo) | Confirmation | EnLinea | DS | K1 |
| `DesembolsoReversado` | Business | `LoanOperation` DisbursementReversal | Related = desembolso | EnLinea | DS | K1 |
| `CreditoAjustadoDesdeComercial` | Business | `LoanOperation` CommercialAdjustment | Related = desembolso en especie | EnLinea | DS | K1 |
| `InteresCausado` · `MoraCausada` · `SeguroCausado` | Business | Tanda de la corrida `DailyCycle`, por partes (T14) | Confirmation:{parte}, origen = la parte | PorLotes resumido, `FinDeCausacion` | CA | K1 |
| `AjusteDeCausacionPorFechaValor` | Business | La operación que lo provoca, o una tanda de la corrida `NationalValueCorrection` (T28) | Related = operación | PorLotes resumido | CA | K1 |
| `CausacionSuspendida` · `CausacionReanudada` | Informational | Ciclo diario o la operación que cruza el umbral | Confirmation | Always, sin comprobante | — | K1 |
| `PagoAplicado` | Business | `LoanOperation` Payment | Confirmation; en su unidad también `AnticipoRecibido`, `CobroDeInteresEnSuspenso` y `RecuperacionDeCastigada` | EnLinea | RC (DN si recauda Nómina) | K1 |
| `PagoReversado` | Business | `LoanOperation` PaymentReversal | Related = pago | EnLinea | RC (DN) | K1 |
| `AnticipoRecibido` · `AnticipoAplicado` | Business | Pago · ciclo diario al vencer la cuota | — | EnLinea | RC | K1 |
| `CobroDeInteresEnSuspenso` | Business | Pago | unidad del pago | EnLinea | RC | K1 |
| `SaldoAFavorReconocido` · `SaldoAFavorResuelto` | Business | Ajuste de Comercial, corrección de usura (corrida `NationalValueCorrection`, T28), abono sin preferencia · `CreditBalanceResolution` | — | EnLinea | RC | K1 |
| `RecuperacionDeCastigada` | Business | Pago sobre un castigado | unidad del pago | EnLinea | RC | K1 |
| `DimensionContableCambiada` | Business | La operación que cambia la dimensión, o el ciclo diario que la detecta | — | EnLinea; PorLotes si la emite el proceso y el tipo no tiene vigencia propia (lo decide `ModoDePasoDeCartera`, T56) | CN | K1 |
| `CobroProvisionalIncorporado` | Business | Plantilla de cobros provisionales al activar | — | EnLinea | RC | K1 |
| `DeterioroCalculado` · `CarteraReclasificadaPorCalificacion` | Business | Cierre mensual, por partes, en la transacción final del cierre (T47) | Close:{versión}, origen = la parte | PorLotes resumido, `CierreDeCartera` | PV | K1 |
| `ReversoDeCierre` | Business | Reapertura del cierre, por partes | Reopen:{versión}, origen = la parte de la reapertura; Related = la parte del cierre que revierte | PorLotes resumido, misma fecha contable | PV | K1 |
| `GastoDeCobranzaCausado` | Business | `LoanOperation` Charge | — | EnLinea | CA | K4 (declarado en K1) |
| `CreditoReestructurado` · `CreditoModificado` · `CreditoNovado` | Business | `LoanOperation` TermsChange | — | EnLinea | CN | K3 (declarados en K1) |
| `CondonacionAprobada` | Business | `LoanOperation` Waiver | — | EnLinea | CN | K3 (declarado en K1) |
| `CreditoCastigado` | Business | `LoanOperation` WriteOff (ítem del lote) | — | EnLinea | PV | K3 (declarado en K1) |
| `CarteraActivada` · `CargaDeCarteraAplicada` · `CierreDeCarteraRealizado` · `CierreDeCarteraReabierto` | Informational | Activación · aplicar carga · cierre · reapertura | — | Always, sin comprobante, con recibo | — | K1 |

**Mensajes de Cartera a otros destinos y de otros módulos a Cartera**

| `Type` | Origen → destino | Kind | Lo origina / cadena | E |
|---|---|---|---|---|
| `ObligacionCreada` · `ObligacionRechazada` | CAR → Inventory | Business, entrega Always | Recibo de la venta; cadena = la venta (`origin.publicId` del mensaje entrante). Un ajuste de una venta aceptada sin migrar responde `ObligacionRechazada` con motivo `SinObligacion`, no reprocesable (FR-143, T17) | K1 |
| `OrdenDeGiroEmitida` · `AnulacionDeOrdenDeGiro` | CAR → Treasury | Business, Always | Orden de giro; la anulación lleva Related = la orden; un giro nuevo, Related = la orden anterior | K1 |
| `PagoReversadoPorCartera` | CAR → Receipts (o el destino del recaudador, `SolicitudDeEmision.Destino`) | Business, Always | Reverso iniciado en Cartera; Related = el pago | K1 |
| `DescuentosPorPagaduria` | CAR → Receipts | Business, Always | Archivo de descuentos (PublicId del adjunto, SHA-256, totales; sin bytes) | K2 |
| `VentaACreditoRegistrada` v1 y v2 · `AjusteDeVentaACredito` v1 | INV → Lending | Business | La v1 y el ajuste son de la 012; la v2 la agrega esta feature en `Contracts/Inventory` y en `CatalogoDeMensajesV1` (suma `ReservedLoanNumber` y `ReservationPublicId`, T18) | K1 |
| `ResultadoDelGiro` v1 | TES → Lending | Business | Definido por esta feature (D27); referido a la clave de la orden | K1 |
| `EnvioDeDescuentosConfirmado` v1 · `ResultadoDeDescuentos` v1 | REC → Lending | Business | Definidos por esta feature (D15) | K2 |

### 2.12 Destinos, módulos de origen y registro de participantes (T15, T16)

- **Destinos** (`IntegrationDestinations`, texto): `Accounting`, `Lending` (existen); `Inventory` (Comercial),
  `Treasury` (Tesorería), `Receipts` (Recaudo). Un solo `IDestinoDeMensajes` por destino; un segundo hace
  fallar el arranque con `Integration.Destination.DuplicateConsumer`.
- **Módulos de origen** (`OriginModule`, 3 caracteres): `INV` (Comercial), `CAR` (Cartera, igual que
  `ModuloContable.Cartera`), `TES` (Tesorería), `REC` (Recaudo). Lo declara el tipo en el catálogo, nunca la
  solicitud.
- **Registro** por cooperativa: `COR_IntegrationParticipants` (`Code`, `Class`, `Role`, `IsTestDouble`,
  vigencia, motivo, quién) y `COR_IntegrationParticipantAdoptions` (contrato o tipo de mensaje adoptado y su
  versión, p. ej. «ProveedorDeObligaciones» v1, «OrdenDeGiroEmitida» v1). Semilla por migración,
  idempotente: `Accounting` (destino, activo), `Inventory` (destino de las respuestas, activo con E-02),
  `Payroll` (recaudador, contrato v1, activo con E-03); `Lending` (destino) inactivo hasta la activación de
  Cartera. Recaudo, Tesorería y Aportes los registra su propia feature; los dobles, una fila `Class = Test`. La
  recogida de créditos propios (`ClaseDeRecaudador.OwnLoanPayoff`) **no** se registra: es la propia Cartera y el
  contrato la exime de la verificación del registro (T91). La pagaduría externa no es recaudador (§2.7).

### 2.13 Servicios de fondo, tareas programadas y arrendamientos (T5–T7)

**`ProcesadorDeCartera`** (`BackgroundService` en `API/Integration`, registrado sólo en `Program.cs`).
Arrendamiento nuevo `lending.process` (`NombresDeArrendamiento.ProcesoDeCartera`, fila sembrada por la
migración `PlataformaParaCartera`). Opciones técnicas `Lending:Process`: `Enabled`, `IntervalSeconds` 30,
`BudgetSeconds` 60, `LeaseTtlSeconds` 120, `MaxParallelCooperatives` 2, `StartTimeLocal` «00:30»,
`BatchSize` 250. No hay tamaño de parte aparte: **la parte cabe en la tanda** (T14). Corre las clases de
`LendingProcessKind`; cada tanda es un `RunDailyAccrualCommand` (o `RunNationalValueCorrectionCommand`) por
`IEjecutorEnCooperativa` con origen `Tarea:cartera.diario:{fecha}`, y guarda sus transacciones y sus partes con sus
mensajes en un solo `SaveChanges`.

Pasos del ciclo diario, en este orden: (1) réplica de valores nacionales al día (si trae una corrección que afecta
días ya causados, encola la corrida `NationalValueCorrection`, T28); (2) reajuste de tasas
variables; (3) causación corriente, mora y seguros; (4) suspensión y reanudación; (5) anticipos al vencer;
(6) bloqueos automáticos y detección de dimensión contable cambiada; (7) desde K4, etapas de cobranza,
promesas vencidas y reclamos con aceptación tácita; (8) verificación del libro sobre una muestra
(`VerifyLendingLedgerIntegrityQuery`).

**Tareas `ITareaProgramada`** (arrendamiento `scheduled.tasks`; cada una sale de inmediato si Cartera no
está activada, salvo la réplica)

| Nombre | Cadencia | Qué hace | E | Origen |
|---|---|---|---|---|
| `valores-nacionales.replicar` | cada pasada | `ReplicateNationalValuesCommand` en cada cooperativa | K1 | R4 |
| `cartera.vigilancia` | cada hora | `Cartera.ProcesoDiarioNoCorrio`, `Cartera.CreditoSinCausar`, `Cartera.CierreMensualPendiente` | K1 | PM-14, R17 |
| `cartera.reservas-vencidas` | diaria | Vence reservas sin confirmar ni liberar; `Cartera.ReservaVencida` | K1 | PM-14 |
| `cartera.giros-sin-resultado` | diaria | Órdenes sin resultado; `Cartera.GiroSinResultado` | K1 | PM-14 |
| `cartera.cierre-programado` | diaria | Crea la corrida `MonthlyClose` si `Riesgo.CierreProgramado` está activo | K1 | PM-14, R13 (CH38) |
| `cartera.garantias-por-vencer` | diaria | Alerta de garantías por vencer | K1 | PM-14 |
| `cartera.mensajes` | cada minuto | Reevalúa y despacha la cola de mensajes al deudor | K1 | F-06 (CH38) |
| `cartera.entregas` | periódica | Consulta el estado de entrega al proveedor | K1 | F-06 |
| `cartera.pagares` | periódica | Estado de firma y custodia de pagarés desmaterializados | K1 | F-19 |
| `cartera.archivos-de-descuento` | diaria | Archivos por generar según el convenio | K2 | F-11 |
| `cartera.giros-de-pagaduria` | diaria | Ventana de giro vencida; pagaduría en mora | K2 | F-11 (CH38) |

Arrendamientos que se reutilizan: `scheduled.tasks`, `integration.dispatch`, `audit.forward`,
`email.dispatch`.

### 2.14 Permisos y plantillas de rol (T60, T61)

Siembra la API al arrancar (`PhaseZeroSecuritySeeder` → seeders por prefijo, antes de `BuiltInRolesSeeder`),
nunca el DbMigrator. **Todo `Lending.*` se siembra entero en K1**, también lo de K2–K6b. Lectura con datos del
deudor = `Read`; `View` sólo para catálogos, parámetros, tableros y bandejas sin datos personales.

**`Lending.*`** (`LendingPermissionCatalogSeeder`)
- Configuración: `CreditLines.View`, `CreditLines.Manage`; `Parameters.View`, `Parameters.Manage`;
  `Scoring.View`, `Scoring.Manage`; `Attributions.View`, `Attributions.Manage`; `Segregation.Manage`;
  `GoverningBodies.View`, `GoverningBodies.Manage`; `RelatedParties.Read`, `RelatedParties.Manage`;
  `RiskPolicy.Manage`, `RiskPolicy.Validate`; `Scopes.Manage`, `Scope.AllOffices`, `Scope.AllLines`;
  `Templates.View`; `MessageTemplates.View`, `MessageTemplates.Manage`
- Originación: `Simulations.Run`; `Applications.Read`, `.Create`, `.Submit`, `.Analyze`, `.Cancel`;
  `Decisions.Analyst`, `.Coordinator`, `.Management`, `.Exceptions`; `CreditBureau.Query`,
  `CreditBureau.ReadReports`; `Sessions.Read`, `.Manage`, `.RecordVotes`, `.Vote`, `.Close`;
  `Guarantees.Read`, `.Manage`, `.Release`; `PromissoryNotes.Manage`; `CreditLimits.Read`, `.Assign`;
  `Blocks.Manage`; `NumberReservations.Read`
- Crédito y pagos: `Disbursements.Execute`, `.Void`; `DisbursementOrders.Void`; `DisbursementFiles.Generate`;
  `Loans.Read`; `Payoffs.Read`; `Payments.Simulate`, `.Reverse`, `.BackdateBeforeClose`;
  `ValidationFailures.Confirm`; `Integrity.Verify`
- Integración y procesos: `Messages.Read`, `.Reprocess`, `.SendNotApplicable` (bandeja y hechos: el detalle
  trae el contenido con deudores y la parte su lista por crédito, así que es `Read`); `Processes.View`, `.Run`;
  `Reconciliation.View`; `TestDoubles.Operate`
- Riesgo: `Ratings.Read`, `.Override`; `MonthlyClose.Execute`, `.Reopen`; `ImpairmentModel.Manage`;
  `ArrearsHistory.Import` (K6a); `PortfolioEvaluations.Prepare`, `.Apply` (K3)
- Libranza (K2): `PayrollAgreements.View`, `.Manage`; `PayrollDeductionFiles.Read`, `.Generate`;
  `PayrollNotApplied.Manage`; `PayrollTransfers.Manage`
- Novedades (K3): `Novelties.Read`, `.Request`, `.Apply`; `Condonations.Request`, `.Apply`; `WriteOffs.Prepare`,
  `.Apply`; `DebtorEvents.Manage`; `Insolvency.Manage`
- Mensajes al deudor y cobranza: `DebtorMessages.Read`, `.Send`, `.Cancel` (K1); `Campaigns.Manage`;
  `Collections.Read`, `.Assign`, `.RecordActivity`; `CollectionStages.Manage`; `PaymentAgreements.Create`,
  `.Approve`; `LegalProcesses.Manage`; `PriorNotices.Read`, `.Send`, `.RegisterEvidence`;
  `CreditReportClaims.Read`, `.Manage` (K4)
- Documentos e informes: `Documents.Read`, `.Issue`, `.Reprint`, `.Void`, `.Verify`; `Reports.View`, `.Export`,
  `.ExportPersonalData`; `Dashboard.View`; `RegulatoryFiles.Read` (lista y descarga: SICSES y centrales traen
  deudores), `.MarkUploaded`;
  `RegulatoryReports.Generate`; `CreditBureauFiles.Generate`
- Migración y activación: `Migration.Load`, `.AcceptDifferences`; `Activation.Execute`
- Alertas: `Alerts.View`, `.Attend`, `.Manage`

**Otros prefijos**: `Accounting.LendingRules.View/Manage`, `Accounting.LendingBatches.View/Run`,
`Accounting.RegulatoryIndicators.View/Manage` (DC); `Core.IntegrationParticipants.View/Manage` (PM-07);
`Core.NationalValues.View/Review`, `Core.RegulatoryProfile.View/Manage` (R); `Core.ExternalChannels.View/Manage`
(F-02); `Compliance.RestrictiveLists.View/Load/Release` (F-18) y `Compliance.DataSubjectRequests.Read/Register/
Respond` (S1), en `DomainPermissionCatalogSeeder`. El registro central de valores nacionales lo protege
`RequireMasterAdminAttribute.Check` (rutas `/api/saas/*`), sin código de permiso.

**Plantillas** (`PerfilesSugeridos`, módulo `Lending`; `GET /api/admin/roles/templates?module=Lending`; S2):
`cartera.administrador`, `cartera.asesor`, `cartera.analista`, `cartera.jefe`, `cartera.comite-credito`,
`cartera.comite-riesgos`, `cartera.gerente`, `cartera.consejo`, `cartera.gestor-cobranza`,
`cartera.oficial-cumplimiento`, `cartera.contador`, `cartera.auditor`, `cartera.revisor-fiscal`. Se crean con
`CreateSuggestedLendingRolesCommand` al preparar la activación.

### 2.15 Rutas por área e informes (T98, T99)

Toda ruta nueva lleva `.RequirePermission` (el 404 genérico fuera de permiso o alcance) y el grupo
`/api/lending` lleva `.AuditarIntentoDenegado(ModuloDeAuditoria.Lending)` (T67). Los recursos sin ruta en esta
tabla toman el plural-kebab de su entidad (`/api/lending/credit-limits`, `/collaterals`, `/promissory-notes`,
`/disbursements`, `/disbursement-orders`, `/loans`, `/blocks`) y los detalla `contracts/api.md`.

| Prefijo | Área | Permisos | Origen |
|---|---|---|---|
| `/api/lending/credit-lines` (+ `template.xlsx`, `import`) | Líneas | `CreditLines.*` | R-06, F-16 |
| `/api/lending/simulations/schedule` | Simulador | `Simulations.Run` | M |
| `/api/lending/applications` (+ `{id}/decisions`) | Solicitudes y decisiones | `Applications.*`, `Decisions.*` | S4 |
| `/api/lending/attribution-matrices` · `/api/lending/amount-limits` | Atribuciones y montos operativos | `Attributions.*` | S4 |
| `/api/lending/governing-bodies` · `/api/lending/sessions` (+ `{id}/attendance`, `items`, `items/{itemId}/votes`, `close`, `minutes.pdf`) | Órganos y actas | `GoverningBodies.*`, `Sessions.*` | S5 |
| `/api/lending/related-parties` · `/api/lending/scopes/users/{userPublicId}` | Vinculados · alcance | `RelatedParties.*` · `Scopes.Manage` | S3 |
| `/api/lending/screening-results/{id}/release` · `/api/lending/bureau-queries` | Listas y centrales | `Compliance.RestrictiveLists.Release` · `CreditBureau.*` | F-18, F-19 (CH24) |
| `/api/lending/number-reservations` | Reservas (consulta) | `NumberReservations.Read` | PM-10 |
| `/api/lending/loans/{id}/settlement?date=&asOf=` · `/statement` · `/payment-simulations` · `/rating?date=` | Crédito | `Payoffs.Read`, `Loans.Read`, `Payments.Simulate`, `Ratings.Read` | M3, R |
| `/api/lending/disbursement-files` (+ `{id}/download-link`) | Archivo de desembolsos | `DisbursementFiles.Generate` | F-10 |
| `/api/lending/messages` (+ `{id}`, `reprocess`, `send-not-applicable`) | Bandeja de integración | `Messages.*` | PM-15 (CH32) |
| `/api/lending/process-runs` (+ `{id}/retry`) · `/api/lending/integrity/verify` | Corridas · libro | `Processes.*` · `Integrity.Verify` | PM-13, M (CH1) |
| `/api/lending/accounting/reconciliation?date=` · `/api/lending/accounting/facts` | Conciliación y hechos | `Reconciliation.View` · `Messages.Read` | DC-16 |
| `/api/lending/risk/{policy, impairment-model/elections, ratings, analysis-ratings, monthly-closes, arrears-history/import, evaluations}` | Riesgo y cierres | `RiskPolicy.*`, `ImpairmentModel.Manage`, `Ratings.*`, `MonthlyClose.*`, `ArrearsHistory.Import`, `PortfolioEvaluations.*` | R |
| `/api/lending/payroll-agreements` · `/payroll-deduction-files` · `/payroll-not-applied` | Libranza (K2) | `Payroll*` | F-11 |
| `/api/lending/debtor-messages` (+ `{id}/cancel`) · `/message-templates` · `/campaigns` | Mensajes al deudor | `DebtorMessages.*`, `MessageTemplates.*`, `Campaigns.Manage` | F-04 (CH32) |
| `/api/lending/{collection-actions, promises, payment-agreements, collection-stages, collector-assignments, agenda, legal-processes, prior-notices, credit-report-claims}` | Cobranza (K4) | `Collections.*`, `PaymentAgreements.*`, `LegalProcesses.Manage`, `PriorNotices.*`, `CreditReportClaims.*` | F-08, F-09, S10 |
| `/api/lending/documents` (+ `{id}`, `{id}/download-link`, `{id}/send`, `{id}/void`, `verify/{code}`) | Documentos | `Documents.*` | F-13 |
| `/api/lending/regulatory/{sicses/…, bureaus/…, files, files/{id}/download-link, files/{id}/mark-uploaded, layouts}` | SICSES y centrales (K5) | `Regulatory*`, `CreditBureauFiles.Generate` | F-12 |
| `/api/lending/templates` · `/api/lending/migration/loads` (+ `{id}/parts/{parte}/template.xlsx`, `…/import`, `{id}/apply`, `{id}/reconciliation`, `{id}/balance-check`) · `/api/lending/activation` (+ `provisional-collections/…`) · `/api/lending/contribution-balances/…` | Plantillas, migración, activación | `Templates.View`, `Migration.*`, `Activation.Execute` | F-16, F-17, DC-16 |
| `/api/lending/dashboard?branch=&line=&asOf=` | Tablero | `Dashboard.View` | F-15 |
| `/api/lending/test-doubles/{treasury, collector}/…` | Dobles (sólo fuera de producción) | `TestDoubles.Operate` | PM-17 |
| `/api/reports/lending` (registro) · `/api/reports/lending/{vista}?format=` | Centro de informes | `Reports.View`, `.Export`, `.ExportPersonalData` | F-15 |
| `/api/accounting/lending/{rules, rules/catalog, rules/{id}/versions, rules/{id}/deactivate, rules/template.xlsx, rules/import, rules/resolve, completeness, voucher-mappings, batches, batches/preview, batches/{id}, postings}` · `/api/accounting/regulatory-indicators` | Lado contable | `Accounting.LendingRules.*`, `Accounting.LendingBatches.*`, `Accounting.RegulatoryIndicators.*` | DC |
| `/api/core/integration-participants` (+ `{id}/retire`) · `/api/core/external-channels` (+ `verify`) | Plataforma | `Core.IntegrationParticipants.*` · `Core.ExternalChannels.*` | PM-07, F-02 |
| `/api/core/national-values` (+ `{id}/review`) · `/api/core/regulatory-documents` (+ `{id}/review`) · `/api/core/regulatory-profile` (+ `versions`) | Copia y perfil | `Core.NationalValues.*`, `Core.RegulatoryProfile.*` | R |
| `/api/saas/national-values` (+ `{id}/corrections`, `import`, `export`) · `/api/saas/regulatory-documents` (+ `{id}/content`) | Registro central | maestro | R1 |
| `/api/compliance/restrictive-lists` (+ `template.xlsx`, `import`) · `/api/compliance/habeas-data/authorizations?personPublicId=` · `/api/compliance/data-subject-requests` | Cumplimiento | `Compliance.*` | F-18, S10 |
| `/api/payroll/runs/{runId}/loan-deductions` | Nómina | `Payroll.Runs.View` | DE-07 |
| `/api/savings/*` | Lo conservado (12 grupos; cambio de ruta sujeto a la enmienda A10) | exentos por nombre hasta su feature | R-07 |
| `/api/reports/savings/{person-portfolio, savings-statement}` | Informes conservados | los de hoy, exentos por nombre como `/api/savings/*` | R-08, T99 |

**Retiradas**: las 63 rutas de crédito de `Endpoints/Lending` y `aging`, `aging/pdf` y `loan-statement/{id}/pdf`
de `/api/reports/lending`. `person-portfolio` se conserva **sin créditos** y, con `savings-statement`, pasa a
`SavingsReportsEndpoints` bajo `/api/reports/savings` (`PersonPortfolioReportQuery` a `Application/Savings`), así
`/api/reports/lending` queda sólo para la Cartera nueva (R-08; CH44; T99).

**Vistas del centro de informes** (`/api/reports/lending/{vista}`, `TablaExportable`, rango hasta 5 años, una
auditoría `Lending.Report.Exported` por exportación): K1 `aging`, `placement`, `receipts` (F-15 la llamó
`collections`; CH10), `transactions`, `usury`, `debtor-messages`, `migration-reconciliation`; K2
`payroll-agreements`, `payroll-deduction-reconciliation`; K3 `restructured`, `charge-offs`; K4 `collection-stages`,
`promises`, `payment-agreements`, `prior-notices`; K5 `quality`, `vintages`, `concentration`, `issued-documents`,
`bureau-exclusions`. Filtros (`FiltrosDeInformeDeCartera`): `from`, `to`, `asOf`, `branch`, `line`, `modality`,
`subsegment`, `advisor`, `payer`, `collector`, `stage`, `format`.

### 2.16 Pantallas (`Shared/Pages/Cartera`, T98)

| Grupo | Rutas |
|---|---|
| Configuración | `/cartera/lineas`, `/cartera/lineas/{id:guid}`, `/cartera/parametros`, `/cartera/plantillas`, `/cartera/vinculados`, `/cartera/atribuciones` (fijado aquí), `/cartera/pagadurias`, `/cartera/pagadurias/{id:guid}` (K2), `/cartera/etapas-de-cobranza` (K4), `/cartera/plantillas-de-mensaje` |
| Originación y créditos | `/cartera/simulador`, `/cartera/solicitudes`, `/cartera/solicitudes/nueva`, `/cartera/solicitudes/{id:guid}`, `/cartera/actas`, `/cartera/cupos`, `/cartera/desembolsos`, `/cartera/creditos`, `/cartera/creditos/{id:guid}`, `/cartera/aprobaciones`, `/cartera/novedades` (K3), `/cartera/condonaciones` (K3) |
| Libranza y cobranza | `/cartera/archivos-de-descuento`, `/cartera/archivos-de-descuento/{id:guid}`, `/cartera/no-aplicados` (K2); `/cartera/mensajes` (K1); `/cartera/cobranza`, `/cartera/agenda`, `/cartera/acuerdos-de-pago`, `/cartera/campanas`, `/cartera/avisos-previos`, `/cartera/juridico` (K4) |
| Riesgo, documentos y regulatorio | `/cartera/cierres`, `/cartera/cierres/{id:guid}`, `/cartera/evaluaciones` (K3), `/cartera/castigos` (K3), `/cartera/documentos`, `/cartera/verificar`, `/cartera/sicses`, `/cartera/centrales` (K5), `/cartera/autorizaciones` |
| Integración, migración e informes | `/cartera/bandeja-de-mensajes`, `/cartera/conciliacion-contable`, `/cartera/migracion`, `/cartera/activacion`, `/cartera/informes?vista=`, `/cartera/tablero` (K5), `/cartera/alertas`, `/cartera/dobles-de-prueba` (fijado aquí; sólo fuera de producción) |
| Otras áreas | `/contabilidad/cartera/matriz`, `/contabilidad/cartera/lotes`, `/contabilidad/cartera/lotes/{Id:guid}`, `/contabilidad/cartera/completitud`, `/contabilidad/cartera/tipos-de-comprobante` (molde de `/contabilidad/inventario/*` de la 012), `/contabilidad/indicadores-regulatorios` (DC); todas en el menú y en `LasPantallasDeCarteraEstanEnElMenu`; `/compliance/listas-restrictivas`, `/compliance/solicitudes-del-titular` (junto a `/compliance/habeas-data/*` que ya existe; CH44); `/admin/canales-externos`, `/admin/participantes-de-integracion`, `/admin/valores-nacionales` (maestro: registra; cooperativa: consulta y revisa), `/admin/perfil-regulatorio` (fijados aquí, CH44) |

**Rutas reservadas** a lo conservado (la Cartera nueva no las usa): `/cartera/ahorros*`, `/cartera/depositos`,
`/cartera/retiros`, `/cartera/retiros-asociado`, `/cartera/estados-retiro`, `/cartera/cdt*`, `/cartera/aportes`,
`/cartera/parametros-ahorro`, `/cartera/parametros-sipla`, `/cartera/zonas`, `/cartera/tipos-zona`,
`/cartera/codigos-movimiento`, `/cartera/extractos` (R-08). Menú: grupo **«Cartera»** con `PermissionGate` por
enlace y grupo **«Ahorros y aportes»** con los 16 enlaces conservados (F-20, R-08).

### 2.17 Códigos de error (familias)

| Familia | Códigos | Origen |
|---|---|---|
| Activación y habilitación | `Lending.NotActivated` («Cartera no activada»; contrato y consulta de obligaciones). «Cartera no habilitada» sigue siendo el estado `NotEnabled` de los puertos de Comercial, no un error | R-03, DE, PM |
| Contrato | `Lending.Contract.KeyReused`, `.CollectorNotRegistered`, `.TransactionRequired`; `Lending.Payment.DistributionChanged`, `.ValueDateOutOfRange`, `.ValueDateBeforeClosing`, `.PreferenceMissing`; `Lending.Reversal.OfReversal`, `.OwnedByCollector` (un pago de Nómina sólo lo reversa Nómina, T31) | PM-12, DE-02, M |
| Concentración y líneas | `Lending.Concentration.TechnicalCapitalUnavailable` (T59); `Lending.CreditLine.InterestTaxConceptMissing` (D20, T44) (fijados aquí) | FR-041, D20 |
| Reservas y ventas | `Lending.Reservation.KeyReused`, `.NotFound` (sólo en las rutas de reserva, C20); `Lending.Sale.NotIncorporated` (C63); `Lending.Payment.MeansClassNotAllowed` (C30) | PM-10 |
| Migración, activación y cierre | los fija `data-model.md` §10 (C71) | DM3 |
| Motor | `Lending.Usury.Exceeded`, `Lending.Schedule.PrincipalMismatch`, `Lending.Parameter.BelowLegalFloor` | M, R8 |
| Procesos | `Lending.Process.AlreadyRunning` | PM-13 |
| Seguridad y actas | `Lending.Segregation.Conflict`, `Lending.Security.UserWithoutPerson`, `Lending.Session.VoterExcluded`, `.QuorumNotMet`, `.Closed`, `Lending.Attribution.NotExceptionable`, `.LevelRequired`, `Lending.Authorization.Missing`, `.WithoutEvidence` | S |
| Mensajes al deudor | `Lending.DebtorMessage.ChannelNotAuthorized`, `.RecipientNotObligated`, `.OutsideLegalWindow`, `.WeeklyChannelUsed` (F-05 los llamó `Lending.Message.*`; CH32) | F-05 |
| Adaptadores y documentos | `Lending.Adapter.SimulatedInProduction`, `.Unavailable`; `Lending.Document.ClearanceDenied`, `.VoidedCannotReissue`; `Lending.PayrollFile.AlreadySent`; `Lending.Regulatory.LayoutSinCotejar` (advertencia), `.BlockingIssues` | F |
| Validación previa | `Lending.Prevalidation.NotPostable`, `.NoResponse` | DC-15 |
| Plataforma | `Integration.Participant.TestDoubleInProduction`, `.VersionNotSupported`; `Integration.Destination.DuplicateConsumer`; `NationalValues.Missing` (M lo llamó `Lending.NationalValue.Missing`; CH35); `Compliance.RestrictiveList.Match` | PM, R5, F-18 |
| Contabilidad | `Accounting.LendingRule.Missing`, `.Overlaps`, `.RetroactiveOverPosted`; `Accounting.LendingFact.Unbalanced`, `.CurrencyNotSupported`, `.WaitingForOriginal`; `Accounting.Period.LendingNotClosed`, `.LendingPending`, `.LendingLoadWillExpire`; `Accounting.Document.LendingCorrectsWithNewVoucher` | DC |
| Nómina | Reutilizados del código: `Payroll.Settlement.ConceptAccountsMissing` (liquidaciones) y `Payroll.ConceptWithoutAccounts` (ordinaria) (§7.6). Nuevos: `Payroll.Settlement.LoanProposalOutdated`, `.LoanAmountsChanged`, `.PortfolioUnavailable` (se conserva); `Payroll.LendingPendingActivation` (aviso), `Payroll.LendingPendingActivationAckRequired`, `Payroll.LoanDeductionNotApplied` (aviso); `Payroll.LegalParameter.NationalValue` | DE, R6 |
| Comercial y Personas | `Inventory.Credit.LendingBlocked`, `.QuotaExceeded`, `.LendingNoResponse`; `Person.HasActiveLoans` (se conserva, mensaje nuevo) | DE-11, DE-12 |
| Retiro | `THROW 50013` en la guarda de SQL Server (50012 es de la 012) | R-04 |

### 2.18 Tipos de alerta (`COR_AlertTypes.TypeCode`)

- **Cartera (de los informes)**: `Cartera.ProcesoDiarioNoCorrio`, `Cartera.CreditoSinCausar`,
  `Cartera.ReservaVencida`, `Cartera.GiroSinResultado`, `Cartera.CierreProgramadoFallido` (PM);
  `Cartera.ValorNacionalFaltante`, `Cartera.ReplicaDeValoresNacionalesAtrasada`, `Cartera.IndiceDesactualizado`,
  `Cartera.NormativaPendienteDeValidar`, `Cartera.LimiteLegalMasEstricto`, `Cartera.ObligacionSobrevenida`,
  `Cartera.ModeloObligatorioNoDisponible`, `Cartera.HistorialDeMoraIncompleto`, `Cartera.CierreMensualPendiente`
  (R); `Cartera.CanalExternoNoDisponible`, `Cartera.MensajeAlDeudorFallido`, `Cartera.CoincidenciaEnListas`,
  `Cartera.NoAplicadoDePagaduria`, `Cartera.GiroDePagaduriaAtrasado`, `Cartera.ArchivoDeDescuentosPendiente`,
  `Cartera.AvisoPrevioSinConstancia`, `Cartera.PagarePendienteDeFirma` (F).
- **Cartera (fijados aquí, FR-016 los exige y ningún informe los nombró)**: `Cartera.DescuadreContable`
  (FR-130), `Cartera.GarantiaPorVencer` (FR-043), `Cartera.UsuraTopada` y `Cartera.LineaCerradaPorUsura`
  (FR-021), `Cartera.CuotaNoDescontadaPorNomina` (FR-112), `Cartera.CupoPorRevalidar` (FR-142),
  `Cartera.PlazoRegulatorioPorVencer` (FR-095, FR-136), `Cartera.PatrimonioTecnicoNoDisponible` (FR-041, T59; al
  contador).
- **Otras áreas**: `Contabilidad.PeriodoInexistente` (DC-09); `Nomina.ValorNacionalDistinto` (fijado aquí: la fila
  que Nómina ya tenía difiere del valor nacional que la réplica adopta, T93); se reutilizan `Integracion.ValidacionFallida`
  (hoy sin escritor, lo levanta T17), `Integracion.MensajeRechazado` e `Integracion.MensajeSinEntregar`, con
  destinatarios por módulo de origen y destino (PM-15).

### 2.19 Tipos de comprobante y documentos cruce (T54)

| Código | Uso para Cartera | Estado |
|---|---|---|
| `DS` | Desembolso, su reverso, ajustes desde Comercial | sembrado (CAR) |
| `RC` | Pagos, reversos, anticipos, saldos a favor, cobro de suspenso, recuperación de castigada, cobro provisional | sembrado (CAR) |
| `DN` | `PagoAplicado` y `PagoReversado` con `CollectorClass = Payroll` | sembrado (CAR) |
| `CA` | Interés, mora, seguro, gasto de cobranza, ajuste por fecha valor | sembrado (CAR) |
| `PV` | Deterioro, reclasificación por calificación, castigo, reverso de cierre | sembrado (CAR) |
| `CN` | «Novedades de cartera»: reestructuración, modificación, novación, condonación, cambio de dimensión | **nuevo** en `voucher-types.json` |

Cruce por defecto: `PG` con el número del crédito; en los roles de Comercial (`CuentaPorCobrarComercial`), `FV`
con la factura. Todo sembrado «pendiente de validar» por la contadora (`LendingVoucherMappingsSeeder`, Order 88,
`lending-voucher-mappings.json`; DC-08). Alternativa en pregunta C6: cruce nuevo `CR`.

### 2.20 Parámetros (`COR_ParameterVersions`, módulo `LND`) y configuración técnica (T43)

Catálogo `ParametrosDeCartera` (`Modulo = "LND"`). Las claves **no** llevan el prefijo «Cartera.»: el
módulo ya lo dice. Los parámetros legales se siembran «pendiente de validar» y respetan el piso o techo
nacional (T42). Ámbitos: `None`, `CreditLine`, `LendingFactType`.

| Key | Valores | Defecto | Ámbitos | E | Origen |
|---|---|---|---|---|---|
| `Pagos.DiasMaximosFechaValor` | int | 5 (D4); no rige para los pagos con fecha valor reglada por la spec: Nómina y, desde K2, la pagaduría con referencia (enmienda A9) | None | K1 | M |
| `Mora.ToleranciaDeSaldoVencido` | decimal (pesos) | 0 | None, CreditLine | K1 | M, R17 (CH36) |
| `Mora.SobreIntereses` (fijado aquí) | bool, con techo nacional | false; nunca en vivienda | None | K1 | M (pregunta) |
| `Riesgo.ReglaMesesFaltantes` | `MayorObservada`, `DelCorte`, `Fija` | MayorObservada | None | K1 (uso K6a) | R14, R17 |
| `Riesgo.DiaLimiteCierre` · `Riesgo.CierreProgramado` | int · bool | 10 · false | None | K1 | R17 |
| `Riesgo.RedondeoDeterioro` | `Centavo`, `Peso` | Centavo | None | K1 | R17 |
| `Riesgo.CalificacionDiariaConArrastre` · `Riesgo.ExcepcionAportesArrastre` | bool · bool | true · false (forzado por D19) | None | K1 | R17 |
| `Contabilidad.ModoDePaso` | `EnLinea`, `PorLotes`, `NoPasa` | D8 (T56); `NoPasa` si la contabilidad no está iniciada | None, LendingFactType | K1 | DC-11 |
| `Contabilidad.Granularidad` | `PorDia`, `PorDiaYSucursal` | PorDia | LendingFactType | K1 | DC-11, DC-12 |
| `Contabilidad.DisparadorDeLote` · `Contabilidad.HoraDeLote` | `Hora`, `FinDeCausacion`, `CierreDeCartera` · hora | por tipo (T56) | LendingFactType | K1 | DC-11 |
| `Contabilidad.PoliticaSinRespuesta` | `ConfirmarConPendiente`, `Bloquear` | ConfirmarConPendiente (D7) | None | K1 | DC-11, DC-15 |
| `Contabilidad.FaltaDeRegla` | `Advertir`, `Bloquear` | Advertir. «Bloquear» sólo muerde en las operaciones de pantalla de Cartera (desembolso, novedad, condonación, castigo); en el contrato, el consumidor y los procesos siempre advierte (T56, enmienda A8). La descripción del parámetro lo dice | None, LendingFactType | K1 | DC-15 |
| `Contabilidad.ValidacionPreviaSegundos` | int | 3 | None | K1 | DC-11 |
| `Contabilidad.DisminucionDeDeterioro` (fijado aquí) | `MismoEjercicioPrimero`, `TodoARecuperacion` | MismoEjercicioPrimero | None | K1 | DC-13 |
| `Contabilidad.IndicadoresVigenciaMeses` (fijado aquí) | int | 6 | None | K1 | DC-18 |
| `Contabilidad.DiferirComisiones` (fijado aquí) | bool | false (FR-017); validado por la contadora (C10) | None | K1 | FR-017 |
| `Informes.PuntosAlertaUsura` (fijado aquí) | decimal (puntos de tasa EA) | defecto en `data-model.md`, «pendiente de validar» | None | K1 | FR-133 |
| `Causacion.ContinuaTrasFallecimiento` (fijado aquí) | bool | true (edge case «El deudor fallece») | None, CreditLine | K1 | FR-097, T102 |
| `Segregacion.Incompatibilidades` | pares de acciones | `Decidir × Desembolsar` activo | None | K1 | S7 |
| `Cobranza.HorarioLunesAViernes` · `Cobranza.HorarioSabado` · `Cobranza.ContactosPorDia` | horario · horario · int | techo nacional | None | K1 (informativos) / K4 | F-05 |
| `AvisoPrevio.DiasMinimos` · `AvisoPrevio.UmbralDosComunicaciones` | int · decimal | 20 (piso) · nacional | None | K4 | F-09 |
| `Mensajes.Reintentos` · `Canales.UmbralDeFallas` | int · int | — | None | K1 | F-02, F-06 |
| `Centrales.DiasDeVigenciaDeConsulta` · `Listas.UmbralDeCoincidenciaDeNombre` | int · decimal | — | None | K1 | F-18, F-19 |
| `Documentos.Firmante.Nombre` · `Documentos.Firmante.Cargo` · `Documentos.PazYSalvoIncluyeCodeudas` | texto · texto · bool | — · — · false | None | K1 | F-14 |
| `Informes.RangosDeEdad` | lista de días | defecto en `data-model.md` | None | K1 | F-15 |
| Fijados aquí, que la spec exige: `Pagos.FirmezaDiasPorClaseDeMedio` (D23: 10 hábiles; cruce interno 0), `Originacion.DiasVigenciaDeAprobacion` (FR-042), `Reservas.DiasDeVigencia` (FR-057), `Giros.DiasSinResultado` (FR-045), `Cupo.MoraMaximaParaVender` (FR-059), `Bloqueos.DiasDeMoraParaBloquear` (FR-060), `Libranza.VentanaDeGiroDias` (FR-114; 3), `Libranza.UmbralDeMoraDePagaduria` (FR-114) | — | defecto en `data-model.md`, sembrado «pendiente de validar» | None | K1/K2 | spec |

**Técnicos (appsettings, no son de la cooperativa)**: `Lending:Process:*` (§2.13), `Lending:TestDoubles:Enabled`,
sección `ExternalChannels`, `CanalesExternosOptions.AmbienteDePruebas` (desde `!IsProduction()`).

### 2.21 Valores nacionales y documentos normativos (T37–T42, T44)

- **Códigos escalares** (`CodigosDeValorNacional`, `Domain/Common/NationalValues`): `USURA` (clave = modalidad
  certificada), `IBR`, `IPC` (clave = plazo o serie), `TOPE_TASA_VIVIENDA` (K6b), `UVR` (K6b), `SMMLV`, `UVT`,
  `FESTIVO` (clave = fecha), `LIMITE` (clave = código del límite). Unidades: `EA`, `NominalMV`, `Pesos`,
  `Fraccion`, `Dias`, `Meses`, `Horas`.
- **Límites legales** (`LIMITE`, catálogo `CatalogoDeLimitesLegales` con unidad, dirección de lo estricto y clave
  de Cartera que acota): `AVISO_PREVIO_DIAS`, `COBRANZA_HORA_FIN_LV`, `LIBRANZA_CAPACIDAD_DESCUENTO`,
  `PREPAGO_TOPE_SMMLV`, `CONCENTRACION_INDIVIDUAL`, `DETERIORO_GENERAL_MINIMO` (R8), más la fracción calificada
  para vinculados (piso 4/5, S5) y los plazos del titular (S10). La lista completa vive en el catálogo y la
  publica `data-model.md`.
- **Documentos normativos** (`RegulatoryDocumentKind`): `CatalogoDeModalidades`, `TiposDeOrganizacion`,
  `SarcAnexo1` (K1), `ReglaDePerdidaEsperada` (K1, para detectar obligación), `SarcAnexo2` (K6a). Semilla desde
  JSON embebidos (`PlatformNationalValuesSeeder`, Admin, Order 6); réplica `NationalValuesReplicaSeeder`
  (Tenant, Order 88).
- **Modalidades** (documento `CatalogoDeModalidades`): `CONSUMO`, `COMERCIAL`, `VIVIENDA`, `MICROCREDITO`,
  `POPULAR_PRODUCTIVO_RURAL`, `POPULAR_PRODUCTIVO_URBANO`, `PRODUCTIVO_RURAL`, `PRODUCTIVO_URBANO`,
  `PRODUCTIVO_MAYOR_MONTO`; sub-segmentos `ConLibranza`/`SinLibranza`, `PersonaNatural`/`PersonaJuridica`,
  `Empleados` y, para `MICROCREDITO`, `Inmobiliario`/`Empresarial` (el CUIF los separa en 1448 y 1455 y la matriz
  necesita el sub-segmento para elegir la cuenta; pregunta E10). Segmentos SARC: `VIVIENDA`, `CONSUMO`, `MICROCREDITO`, `COMERCIAL_PN`, `COMERCIAL_PJ`; segmentos de
  pérdida esperada: `CONSUMO_LIBRANZA`, `CONSUMO_SIN_LIBRANZA`, `COMERCIAL_PN` (R9, R10, R16).

Nota: el Order 88 lo piden dos semillas (`LendingVoucherMappingsSeeder` de DC-08 y `NationalValuesReplicaSeeder`
de R4). Se fija: **`NationalValuesReplicaSeeder` = 88** (debe correr antes de todo lo que lee valores),
**`LendingVoucherMappingsSeeder` = 89**, `LendingFactTypesSeeder` = 90 (CH36).

### 2.22 Adaptadores y canales (T71–T73)

| Puerto | Registro | Canales | Comportamiento del `SIMULADO` (último dígito del documento) | E |
|---|---|---|---|---|
| `ICanalDeMensajeria` (plataforma) | `ICanalesDeMensajeria` | `CanalDeCorreo` (sobre `IEmailSender`), SMS y WhatsApp por proveedor, `MensajeriaSimulada` | 1 número inválido; 2 entregado en la consulta siguiente; 3 fallo de entrega; 5 no disponible | K1 (correo e informativos); SMS y WhatsApp reales con contrato |
| `IDepositoDeValores` | `IDepositosDeValores` | proveedor, `DepositoDeValoresSimulado` | 1 firma rechazada; 3 firma pendiente; 5 no disponible | K1 |
| `ICentralDeRiesgo` | `ICentralesDeRiesgo` | por central (`BureauCode`), `CentralDeRiesgoSimulada` | 1 reporte negativo; 2 sin historia; 3 puntaje bajo; 5 no disponible | K1 |
| `IListasRestrictivas` (plataforma) | `IProveedoresDeListas` | `CanalDeListasCargadas` (`CARGA-PROPIA`, vale en producción), `ListasSimuladas` | 8 posible coincidencia; 9 coincidencia | K1 |

Todos devuelven `ResultadoExterno<T>` con `ExternalOutcome`; nunca lanzan por rechazo ni por transporte; el
código de canal se **sella** en la operación. Credenciales: Secret `erp-canales-externos` montado en
`/secrets/canales-externos/{tenantPublicId}.{channelCode}.json`, leído por `CredencialesDeCanalExternoEnArchivo`;
guion `tools/scripts/crear-secreto-canales-externos.ps1`; circuito `CircuitoDeCanalExterno`. Sólo `Program.cs`
llama `AddExternalChannels`. Los **dobles de prueba** de FR-089 (`TesoreriaDePrueba`, `RecaudadorDePrueba`) no
son canales: ver T20 y CH20.

### 2.23 Plantillas de importación (`CatalogoDePlantillasDeCartera`, T82)

| Clave | Qué carga | E | Origen |
|---|---|---|---|
| `lending.credit-lines` | Hojas `Lineas`, `Tasas`, `Documentos`, `Seguros`, `Garantias`, `Equivalencias` (FR-022, `LND_CommercialTermEquivalences`) | K1 | F-16 |
| `lending.migration.{parte}` | Una plantilla de una hoja por parte de la carga (§2.3) | K1 | F-17 |
| `lending.provisional-collections` | Cobros provisionales al registrar la activación | K1 | F-16 |
| `lending.contribution-balances` | Saldo de aportes y ahorro permanente con fecha (D19) | K1 | F-16 |
| `lending.related-parties` | Personas vinculadas (F-16 la llamó `lending.related-persons`; CH30) | K1 | F-16 |
| `lending.arrears-history` | Carga complementaria del historial (F-16 la llamó `lending.delinquency-history`; CH46) | K6a | F-16, R14 |
| `lending.payroll-agreements` | Convenios de pagaduría | K2 | F-16 |
| `compliance.restrictive-lists` | Listas restrictivas (plataforma) | K1 | F-18 |
| Matriz contable | `/api/accounting/lending/rules/template.xlsx` (`ImportLendingPostingRulesCommand`) | K1 | DC |

Ninguna crea personas: un documento que no existe en `COR_People` es un error de fila y columna.
`RutasDePlantilla.MapPlantilla` se generaliza para recibir la definición y el permiso de datos personales.

### 2.24 Tipos de adjunto (`AdjuntosDeModulo`, T81)

- **De módulo, no borrables**: `LendingIssuedDocument` (`Lending.Documents.Read`), `LendingDisbursementFile`,
  `LendingPayrollDeductionFile`, `LendingRegulatoryFile`, `LendingBureauReport`
  (`Lending.CreditBureau.ReadReports`) (F-19).
- **Con subida**: `LoanApplicationDocument` (mientras la solicitud no esté decidida), `LendingGuaranteeDocument`,
  `LendingPayrollCertification`, `LendingPriorNoticeEvidence`, `LendingLegalProcessDocument`,
  `LendingNoveltyConsent` (F-19); `DataAuthorization` (evidencia de autorización, S10). Una vez decidido el
  dueño, no se borran (`Attachments.OwnerLocked`).

### 2.25 Pruebas con nombre fijo

**Arquitectura nuevas** (`tests/IngenIA365ERP.Architecture.Tests/Principles`)

| Prueba | Fija | Origen |
|---|---|---|
| `LaCarteraSoloSeTocaPorSusInterfaces` | Lista cerrada de FR-008: nadie depende de la Cartera nueva salvo por `Common.Integration.Lending`; métodos exactos de los **cinco** puertos que Cartera ofrece (los cuatro de T90 más `IDimensionesDeCartera`); ninguna FK ni navegación de `PAY_`/`INV_` a `LND_` | DE-17 |
| `CarteraNoConoceContabilidad` · `LosHechosDeCarteraNoLlevanCuentas` · `LoDeCarteraSeCorrigeConComprobanteNuevo` | Frontera contable en las dos direcciones; contratos sin cuentas; guarda del poster | DC |
| `SoloElEmisorEscribeLaBandeja` · `ElCatalogoDeMensajesEsUnico` · `EmisionesDeProcesoPermitidas` · `UnSoloConsumidorPorDestino` | Bandeja de varios emisores; sólo tipos CAR marcados admiten proceso | PM, DC-03 |
| `LosDoblesDePruebaNoSeRegistranEnProduccion` · `LosComandosDeCarteraLlevanClave` | FR-089; FR-009 | PM |
| `LaCarteraNoTieneValoresLegalesFijos` | Literales decimales de Nómina más `365`; `7` y `10` sólo por el valor del enum de periodicidad; sospechosos del Anexo con exención por archivo | M16, R18 (CH39) |
| `ElMotorDeCarteraEsDecimal` · `NadieEscribeElLibroDeCarteraFueraDelRegistro` · `LosSaldosYTasasDeCarteraTienenSuPrecision` · `LaCarteraUsaLaFechaLocal` | Motor | M16 |
| `LosValoresNacionalesSeLeenEnUnSoloSitio` · `SoloElReplicadorLeeElRegistroCentral` · `SoloLaReplicaEscribeLosValoresNacionalesDeNomina` (SMMLV, UVT y festivos `National`; reemplaza a `SoloLaReplicaEscribeElSalarioMinimo`) · `LosCierresDeCarteraNoSeReescriben` | Valores nacionales y cierres | R18, T93 |
| `CadaParametroRespetaLaEntregaDeSuModulo` (fijado aquí) | `DisponibleDesde` por módulo: ninguna clave de Cartera se ofrece antes de su entrega | T43 |
| `LaCarteraHeredadaNoVuelve` · `NingunaTablaConservadaApuntaALaCarteraRetirada` · `ElEspacioLendingEsSoloCartera` | Retiro | R-05, R-07 |
| `LasConsultasDeCarteraRespetanElAlcance` · `LasLecturasSensiblesDeCarteraSeAuditan` · `LosCodigosViewDeCarteraNoTienenDatosPersonales` (la bandeja, los hechos y los archivos regulatorios van con `Read`) | Seguridad | S |
| `LosPuertosDeCarteraValidanYAuditanSusEscrituras` (fijado aquí) | Cada método de escritura de `IContratoDeObligaciones` e `IReservasDeCartera` ejecuta su validador y escribe su evento (T91, T22) | T101 |
| `LosProveedoresExternosSoloLosConoceSuAdaptador` · `LasCredencialesDeCanalesExternosNoTocanLaBase` · `LaCobranzaNuncaContactaReferencias` · `LasPantallasDeCarteraEstanEnElMenu` | Adaptadores y pantallas | F |

**Ampliadas**: `El_puerto_de_Cartera_declara_exactamente_sus_dos_consultas` (+ los dos métodos de
`IReservasDeCartera`), `InventarioNoConoceContabilidadNiCartera`, `NingunTrabajoDeFondoOperaSinCooperativa` (+
`ProcesadorDeCartera`), `LosComandosDeConsumoNoTienenRuta`, `LosHechosInmutablesNoSeModifican`,
`NingunModuloEscribeMovimientosFueraDelContrato`, `PrincipioXI_ContableImmutable` (se activa sola con
`Entities/Lending/Transactions`), `PrincipioXII_MigracionesDestructivas`, `LosEndpointsProtegidosExigenPermiso`
(+ `Endpoints/Lending/*.cs`, exención por nombre de `Endpoints/Savings`), `LaSegregacionNoUsaUserIdDelToken` (+
carpetas de Cartera), `LasPantallasDicenQueEstanCargando` (`ModulosMigrados` += `Cartera`),
`TodoEnlaceDelMenuTieneSuPagina`, `LosUnicosDeDescuentosSoloCuentanFilasVivasTests` (`ObligationPublicId`),
`LaUvtSeLeeEnUnSoloSitio` (sigue en `LectorDeUvt`, que lee la proyección, T93); de Application,
`LosFormatosElegidosPorReferenciaNoCompitenPorVigencia` (fijado aquí, T78) y
`ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite` (fijado aquí, T56).

**Dominio**: casos dorados en `Domain.Tests/Lending/Casos/*.json` (`CasoDoradoDeCartera`,
`CasosDoradosDeCarteraTests`, ~28 en K1), `Domain.Tests/Lending/Risk/Casos/*.json`,
`Domain.Tests/Lending/Collections/Casos/*.json`, casos de `EvaluadorDeVotacion` y de `EnrutadorDeAtribuciones`;
propiedades `SumaDeCapitalDelPlanTests`, `ReproducibilidadDeLiquidacionTests`, `ReversoComoSiNoHubieraExistidoTests`,
`ElReversoEnlazaCadaFilaConSuOriginalTests` (fijado aquí, T33), `CausacionIdempotenteTests`; `RendimientoDeCausacionTests` (50.000 créditos, motor solo, < 60 s); casos dorados
de Nómina con la cuota de Cartera, tope y neto insuficiente (DE-19). Baterías de conformidad
`ConformidadDeMensajeria`, `ConformidadDelDepositoDeValores`, `ConformidadDeCentral`; `TextosDeCarteraTests`.

**Integración** (colección «Cartera e2e» (fijado aquí), dos motores, cooperativas aisladas para lo que activa
Cartera): `RetiroDeLaCarteraHeredadaTests`, `CarteraDesplegadaSinActivarTests` (definitiva, ordinaria, borrado de
persona y venta a crédito), `NominaRecaudaPorElContratoTests` (incluye que un usuario de Cartera no reversa un pago de
Nómina), `CreditoConCarteraTests`, `ConcurrenciaPagoYCausacionTests`, `ReplicaSobreCooperativaSembradaTests` (fijado
aquí: la réplica adopta el SMMLV, la UVT y los festivos que Nómina ya tenía, T93), `CausacionDiariaPerformanceTests`
(`RUN_PERF_TESTS=1`).

### 2.26 Migraciones pares (PostgreSQL / SQL Server, `tools/scripts/add-migration.ps1`)

| E | Nombre | Tipo | Contenido |
|---|---|---|---|
| K1 | `NominaRecaudaPorElContrato` | aditiva | `PAY_PayrollLoanDeductions`; `PAY_PayrollRuns.LendingActivationAck*`; `PAY_SettlementDeductions.ObligationPublicId`, `ObligationNumber`, `CarteraReversalPublicId` y `PAY_Novelties.ObligationPublicId`, `ObligationNumber`, todos con copia idempotente desde `LND_LoanPortfolios`, y `UK_PAY_SettlementDeductions_Termination_Obligation` (DE-09; CH37; T87) |
| K1 | `ComercioConCartera` | aditiva | `INV_DocumentPayments.ReservedLoanNumber`, `CreditLineCode`, `CreditWithoutLending`; `COR_PaymentMeans.CreditWithoutLending` (las dos `CreditWithoutLending`, sujetas a la enmienda A6); `INV_LendingObligationResponses` (DE) |
| K1 | `RetiroDeLaCarteraHeredada` | **destructiva con guarda** | Guarda (PG `DO $$ … RAISE EXCEPTION`, SQL Server `THROW 50013`) que cuenta las 55 tablas, `LND_SiplaGroupParameters`, `PAY_SettlementDeductions` y `PAY_Novelties` con `LoanPortfolioId` no nulo y `ACC_Documents` con `OriginModule = 'CAR'`; se niega salvo la fila `LND.RetiroHeredado.Aprobado`; suelta las 40 FK, `LoanPortfolioId` de `PAY_SettlementDeductions` (y su FK, IX y UK) y de `PAY_Novelties`; borra las 55 tablas. Cabecera `MIGRACION-DESTRUCTIVA-APROBADA` con respaldo y segundo revisor; commit propio; `Down()` recrea vacío (R-04) |
| K1 | `ValoresNacionales` (Admin) | aditiva | `ADM_NationalValues`, `ADM_RegulatoryDocuments` (R1) |
| K1 | `PlataformaParaCartera` | aditiva | `COR_NationalValues`, `COR_RegulatoryDocuments`, `COR_NationalValueReplications`, `COR_NationalValueReviews`, `COR_RegulatoryProfileVersions`, `COR_IntegrationParticipants` y `…Adoptions` (con su semilla idempotente), `COR_IntegrationMessages.OriginActorKind` y `OriginUserCentralId` nulo, `COR_ExternalChannelSettings`, `COR_RestrictiveListLoads` y `…Entries`, `COR_DataSubjectRequests`, columnas de `CMP_HabeasData*` y de `COR_NotificationTemplates`, fila `lending.process` en `COR_BackgroundLeases` |
| K1 | `CarteraNucleo` | aditiva | Tablas `LND_` de K1 de configuración (con `LND_CommercialTermEquivalences`), originación (con `LND_DebtorControls`), crédito, libro (con `LND_InstallmentNonDeductions`), integración, procesos, mensajes al deudor, avisos, reclamos, reclamaciones al seguro e insolvencia (datos), documentos (si se adelantan), migración y activación |
| K1 | `CarteraRiesgo` | aditiva | `LND_RiskPolicyVersions`, `LND_MonthlyCloses`, `…Loans`, `…GeneralImpairments`, `…Reopenings`, `LND_LoanArrearsHistory`, `LND_AnalysisRatings` (R) |
| K1 | `IntegracionContableDeCartera` | aditiva | `ACC_LendingPostingRules`, `ACC_LendingVoucherMappings`, `ACC_LendingPostings`, `ACC_RegulatoryIndicators` (DC-01) |
| K2 | `CarteraLibranza` (fijado aquí) | aditiva | `LND_PayrollAgreement*`, `LND_PayrollDeductionFile*` |
| K3 | `CarteraNovedades` (fijado aquí) | aditiva | `LND_LoanNovelties`, `LND_Waivers`, `LND_WriteOffBatch*`, `LND_PortfolioEvaluation*` |
| K4 | `CarteraCobranza` (fijado aquí) | aditiva | `LND_MessageCampaigns`, `LND_Collection*`, `LND_CollectorAssignments`, `LND_PaymentPromises`, `LND_PaymentAgreement*`, `LND_LegalProcess*` |
| K5 | `CarteraInformes` (fijado aquí) | aditiva | `LND_RegulatoryFiles` (y `LND_IssuedDocument*` si no se adelantan a K1) |
| K6a | `CarteraPerdidaEsperada` (fijado aquí) | aditiva | `LND_ImpairmentModelElections`, `LND_ArrearsHistoryLoads` |
| K6b | `CarteraVivienda` (fijado aquí) | aditiva, sólo si hace falta | Columnas UVR (alias `ValorUvr` 18,4) |

Orden en K1: `NominaRecaudaPorElContrato` y `ComercioConCartera` (con los consumidores ya en los puertos
«sin activar») → `RetiroDeLaCarteraHeredada` (commit propio, modelo intermedio) → `ValoresNacionales` (Admin) →
`PlataformaParaCartera` → `CarteraNucleo` → `CarteraRiesgo` → `IntegracionContableDeCartera` (T88).

---

## 3. Decisiones transversales (T1..T102)

Formato: **Decisión** · **Por qué** · **FR** que cubre · **Origen** (informe y, si hubo, el choque de la §4).

### Nombres y módulo

**T1 · Idioma de los identificadores.**
Decisión: la regla de §2.1. Los enums de los puertos llevan tipo en español y valores en inglés idénticos a los
del motor; las carpetas nuevas van en inglés, salvo `Application/Accounting/Lending/{Reglas, Contabilizacion,
Lotes, Consultas}`, que replica a propósito `Accounting/Inventory`. Renombres obligatorios: `Originacion`,
`Actas`, `Atribuciones`, `Segregacion`, `Integracion`, `Migracion`, `Activacion` → `Origination`, `Sessions`,
`Attributions`, `Segregation`, `Integration`, `Migration`, `Activation`; `CrearRolesSugeridosDeCarteraCommand`
→ `CreateSuggestedLendingRolesCommand`; `TotalesDeLoteDeCarteraQuery` → `GetLendingBatchTotalsQuery`. Comandos y
consultas nuevos llevan siempre verbo (`Get…`, `List…`), aunque sus pares de la 012 no lo lleven (§2.10). DTO de
puertos: salida con `Dto`, entrada sin sufijo (§2.1).
Por qué: un solo criterio evita dos nombres para lo mismo. El criterio de lo que va en español (y de los DTO de
entrada sin sufijo) **viene de la 012, no de la constitución**, que pide identificadores en inglés y DTO con `Dto`:
son desviaciones que el plan registra en `Complexity Tracking` (T101); proponer una enmienda constitucional es la
otra salida, por el procedimiento de Governance. FR: transversal. Origen: M1, S7, DE-01, PM-09, R-07, DC (CH40,
CH41).

**T2 · Prefijos de tabla y ningún nombre heredado.**
Decisión: `LND_` para Cartera; `COR_` para la plataforma en la base de la cooperativa; `ADM_` sólo para el
registro central de valores nacionales; `ACC_`, `PAY_`, `INV_` para las enmiendas. No se crea ninguna tabla
`CMP_`. Ninguna tabla nueva reutiliza uno de los 93 nombres heredados; la transacción es `LND_LoanTransactions`.
Por qué: la constitución fija la lista de prefijos; no reutilizar nombres deja estricta para siempre
`LaCarteraHeredadaNoVuelve` y útil el `Down()` del retiro (la 012 lo perdió con `INV_Documents`); usar el nombre
del Principio XI resuelve I-5 sin enmienda. FR: FR-144, I-5. Origen: R-06, R1, F-18, S10 (CH23).

**T3 · `Lending` es sólo la Cartera nueva; lo conservado se muda a `Savings`.**
Decisión: en el mismo commit del retiro, las 38 entidades conservadas, sus configuraciones, handlers, 12 grupos
de rutas y 18 pantallas pasan a `Domain.Entities.Savings`, `Application.Savings`, `Configurations/Savings`,
`Endpoints/Savings` (`/api/savings/*`) y `Pages/AhorrosYAportes` (las pantallas conservan sus rutas
`/cartera/...`). Sus `ToTable` no cambian y el scaffold no debe traer operaciones sobre ellas. Módulo de
auditoría `Savings`. Es hogar provisional hasta las features de Ahorros, Aportes y SARLAFT. Las tablas no cambian,
pero el espacio de nombres y las rutas API sí, y FR-144 dice que lo conservado queda «intacto»: es la enmienda A10
(T97). Si el dueño no la aprueba, lo conservado mantiene sus rutas `/api/lending/*` hasta la feature de Ahorros y
`ElEspacioLendingEsSoloCartera` las exime por nombre.
Por qué: la prueba de FR-008 y la de permisos por ruta nacen sin excepciones, y la auditoría encadenada de
Cartera no arrastra eventos de ahorros. FR: FR-008, FR-144, FR-149. Origen: R-07, DC-19, DE-17 (preguntas A10, H5).

**T4 · Dónde vive cada pieza.**
Decisión: el mapa de §2.2. Motores puros en `Domain/Lending/*` y `Domain/Common/*`; puertos entre módulos en
`Application/Common/Integration/{Lending, Accounting, Savings}`; contratos de mensaje en
`Contracts/Lending`; adaptadores externos en el proyecto nuevo `IngenIA365ERP.ExternalChannels`; trabajos de
fondo sólo en la API. Por qué: Principio II y el molde de la 012 (puertos donde los referencia quien consume).
FR: FR-008. Origen: M1, DC-01, F-01, R5, S3.

### Ejecución y actor

**T5 · Un solo servicio de fondo para lo pesado: `ProcesadorDeCartera`.**
Decisión: `BackgroundService` en la API con arrendamiento `lending.process` y corridas en `LND_ProcessRuns`
(`DailyCycle`, `MonthlyClose`, `CloseReopen`, `NationalValueCorrection`; C59) con errores en `LND_ProcessRunErrors`. Recorre las cooperativas
activas con paralelismo 2 y presupuesto de 60 s por cooperativa y pasada (menor que el TTL de 120 s); salta las
que no tienen Cartera activada. Procesa por tandas de 250 créditos, cada tanda un `RunDailyAccrualCommand` por
`IEjecutorEnCooperativa`, retomando donde quedó. **Regla única: un hecho nace en la transacción que guarda lo que
cuenta.** Cada tanda guarda sus `LoanTransaction` y emite sus partes (T14) en el mismo `SaveChanges`; un corte entre
tandas deja cada tanda completa con su hecho y nunca causación sin hecho. Un crédito que falla se reintenta solo, queda en
`LND_ProcessRunErrors` con alerta y su marca de agua no avanza; los demás siguen. El cierre y su reapertura son
clases de corrida del mismo servicio: nunca corren a la vez que la causación de esa cooperativa. Orden de pasos
del ciclo en §2.13. La garantía de exactitud es la marca de agua y la unicidad, no el arrendamiento.
Por qué: `ProgramadorDeTareas` corre en serie, sin presupuesto, y renueva el arrendamiento sólo entre tareas;
una causación de minutos lo perdería y atrasaría a las demás cooperativas (SC-007). Un solo servicio y un solo
arrendamiento evitan que cierre y causación compitan. FR: FR-062–FR-067, FR-107, FR-117, FR-119, FR-142,
SC-007, SC-020, Principio IV. Origen: PM-13, M14, R13 (CH1).

**T6 · Qué es el día de causación y cómo se pone al día.**
Decisión: la causación del día d son los intereses de (d−1, d]; el ciclo del día d corre desde las 00:30 hora
local (`StartTimeLocal`, `HoyLocal`), así un pago con fecha valor d ve el interés hasta d. Causar d exige
`LoanControl.LastAccruedDate = d−1`: una fecha a la vez y en orden; si un día no corrió, la pasada siguiente
recorre de la marca más uno hasta hoy. Toda operación sobre un crédito primero lo pone al día hasta su fecha, en
su propia transacción. El proceso bloquea con salto (`SKIP LOCKED` / `READPAST`) y deja el crédito ocupado para la
pasada siguiente. Por qué: idempotencia sin una fila de control por día (18 millones al año) y ninguna caja
esperando a la causación. FR: FR-062, FR-066, FR-070, SC-020. Origen: M4, M13, M14; PM-13 proponía el día
siguiente a la 01:00 (CH2).

**T7 · Tareas livianas por `ITareaProgramada`.**
Decisión: las de §2.13, registradas como singletons en `Program.cs`; terminan en segundos, salen si Cartera no
está activada (salvo la réplica) e idempotentes por su estado y por el `DedupKey` de las alertas. La mensajería
al deudor corre desde K1 (`cartera.mensajes` cada minuto y `cartera.entregas`), porque los informativos de D24 y
de FR-112 ya existen en K1. Por qué: comparten `scheduled.tasks` con la 012 y no pueden llevar trabajo pesado.
FR: FR-016, FR-045, FR-057, FR-075, FR-107, FR-114, FR-122. Origen: PM-14, F-06, F-11, F-19, R4 (CH38).

**T8 · Actor de lo automático y de las órdenes manuales.**
Decisión: lo que corre solo firma como el proceso de la cooperativa (`Actor.ProcesoDeIntegracion`, canal
`Process`, sin IP); no hay usuarios técnicos. Una orden manual (`RequestLendingProcessRunCommand`,
`OrderMonthlyCloseCommand`) responde 202, sólo encola, y la corrida se ejecuta con la persona que la ordenó como
actor. El usuario que originó un hecho viaja como dato (`UsuarioDeOrigen`). Los dobles de prueba emiten con la
persona que prueba como usuario de origen. Por qué: I-2 y la regla «la orden encola y el proceso corre» de la
012. FR: FR-003, I-2. Origen: PM-02, PM-13, PM-17, R13.

**T9 · Fecha local en todo Cartera.**
Decisión: todo `Lending` usa `IDateTimeService.HoyLocal` (America/Bogota); ningún `TodayUtc`. Lo vigila
`LaCarteraUsaLaFechaLocal`. Por qué: después de las 19:00 `TodayUtc` ya dice mañana, y la causación, la fecha
valor y la mora son por día local. FR: FR-012, FR-062, FR-070. Origen: M16.

### Mensajes

**T10 · Bandeja con varios emisores, definidos por el catálogo.**
Decisión: `TipoDeMensaje` gana `OriginModule`, `Destinos` (conjunto admitido) y `AdmiteProceso`;
`EmisorDeMensajes` deja la constante `"INV"` y toma el módulo del tipo; una solicitud sólo trae contenidos de un
mismo módulo y dice `SolicitudDeEmision.Destino` cuando el tipo admite varios. `CatalogoDeMensajes` agrega
`CatalogoDeMensajesV1` (INV, sin renombrar ni mover; cada entrada recibe los campos nuevos y suma
`VentaACreditoRegistradaV2`, T18), `CatalogoDeMensajesDeCarteraV1` (CAR) y
`CatalogoDeMensajesHaciaCarteraV1` (TES, REC); `MensajesEntrantes`, el emisor y las versiones aceptadas leen el
agregado. `MensajeEntrante` gana `OriginDocumentClassText` y `RelatedDocumentClassText`. Los bytes de los
contratos INV no cambian (`ContratosDeMensajesTests`). Por qué: es lo que menos toca la 012 y el catálogo sigue
siendo lo único que dice qué módulo emite qué y adónde (T8 de la 012); poner el módulo en la solicitud dejaría a
un módulo emitir tipos de otro. FR: FR-008, FR-055, FR-125, FR-149, D15, D27. Origen: PM-01; DC-03 proponía el
módulo en la solicitud (CH4).

**T11 · El proceso puede originar mensajes de Cartera (enmienda de la regla de la 012).**
Decisión: `COR_IntegrationMessages` gana `OriginActorKind` (defecto `Person`) y `OriginUserCentralId` pasa a
nulo; `SolicitudDeEmision` gana `UsuarioDeOrigen`. El emisor resuelve el usuario de origen así: la persona que
actúa; si no hay, el `UsuarioDeOrigen` de la solicitud (el del mensaje consumido o el de quien ordenó la
corrida); si tampoco, sella el proceso (`OriginActorKind = Process`, central nulo). Sólo los tipos con
`AdmiteProceso = true` aceptan proceso: los masivos de la causación y del cierre, `CausacionSuspendida`,
`CausacionReanudada`, `AnticipoAplicado`, `DimensionContableCambiada` detectada por el proceso,
`AjusteDeCausacionPorFechaValor` y `SaldoAFavorReconocido` (los emite también la corrida `NationalValueCorrection`,
T28), los informativos de cierre y las respuestas a Comercial. Todos los tipos INV quedan en `false`: para Inventario la
regla «el mensaje lo origina la persona» sigue intacta. Lo fija `EmisionesDeProcesoPermitidas`. Por qué: sin
esto Cartera no puede emitir ningún hecho automático (el emisor lanza hoy). FR: FR-003, FR-057, FR-062–FR-066,
FR-107, FR-125, FR-149, I-2. Origen: PM-02, DC-03 (CH3).

**T12 · Origen, unidad y dependencias de un mensaje de Cartera.**
Decisión: el origen es el documento u operación de Cartera que produce el hecho (`LoanOperation`, la parte de
una corrida, la parte de un cierre o de su reapertura, la orden de giro, la activación), nunca el crédito; en el
cierre el origen es la **parte** (`LND_AccountingFactParts`) y no el cierre, porque el único (origen, tipo, clave) de
la bandeja no admite dos partes del mismo tipo con la misma clave `Close:{versión}`; los informativos del cierre sí
nacen del cierre. Los hechos de una operación con el mismo modo y tipo de comprobante comparten `originEventKey` y
forman una unidad y un comprobante (p. ej. pago + anticipo + cobro de suspenso); en los masivos la unidad es la parte
y el resumido diario las agrupa por lote. Las dependencias apuntan **sólo al original** (`Related`): reverso → pago,
anulación → orden, orden nueva → anterior, parte del reverso de cierre → parte del cierre que revierte. `CadenaDeOrden` (nueva en
`SolicitudDeEmision`) se usa sólo en las respuestas a Comercial, con la venta como raíz. No hay cadena por crédito
hacia Contabilidad: un hecho rechazado no congela la contabilidad posterior del crédito; la conciliación lo
explica (T58). La `LoanTransaction` guarda el `MessagePublicId` del hecho que la llevó. Por qué: FR-055 y FR-126
piden orden sólo frente al original y en la cadena de la venta; una cadena por crédito detendría todo un crédito
por una regla faltante. FR: FR-045, FR-055, FR-080, FR-081, FR-126. Origen: DC-04, PM-03 (CH5).

**T13 · Qué lleva el sobre de Cartera.**
Decisión: `OperationDate` = fecha contable (T36); fecha valor y de registro van en el contenido.
`BranchPublicId` = sucursal contable de la oficina del crédito (`COR_Branches.TenantBranchPublicId`).
`PersonPublicId` = tercero del hecho (deudor, pagaduría o aseguradora); nulo en los masivos. `OriginDocumentClass`
lleva la clase de Cartera como texto (`LoanDisbursement`, `PaymentApplication`…) y `DocumentTypeCode` el tipo
de documento de Cartera; `WarehouseCode` y `FiscalUniqueCode` nulos; `Currency = COP`. Una oficina sin sucursal
contable vinculada no emite: se bloquea antes de confirmar (pregunta C9). Por qué: Contabilidad fecha con
`OperationDate` y filtra por la sucursal del sobre. FR: FR-012, FR-126, FR-127. Origen: PM-05, DC-09.

**T14 · Hechos masivos por partes.**
Decisión: `InteresCausado`, `MoraCausada`, `SeguroCausado`, `DeterioroCalculado`,
`CarteraReclasificadaPorCalificacion` y `ReversoDeCierre` se emiten por partes: una por fecha o cierre ×
sucursal contable × tipo de hecho × **tanda** (la parte cabe en la tanda que la produce: no hay `FactPartSize`). Cada
parte tiene su fila en `LND_AccountingFactParts`, que es el origen del mensaje, totales por combinación de dimensiones
y **siempre** la lista por crédito (número, tercero, valores por concepto). Clave: `Confirmation:{partePublicId:N}` en
la causación y en la corrida `NationalValueCorrection`; `Close:{versión}` / `Reopen:{versión}` en el cierre y su
reapertura, sin chocar porque el origen ya es la parte (T12). En la causación cada parte nace en el `SaveChanges` de
su tanda y la `LoanTransaction` guarda el `MessagePublicId` de su parte; en el cierre, todas nacen en su transacción
final (T47). Los demás hechos van por crédito. Por qué: un mensaje por crédito y día serían unos 18 millones al año;
uno por día y cooperativa, megabytes y sucursales mezcladas en un sobre de una sola sucursal; una parte más grande que
la tanda no podría nacer en la transacción de lo que cuenta (unas 200 tandas al día con 50.000 créditos: cientos de
mensajes, no millones). FR: FR-128, FR-130, SC-007. Origen: PM-04, DC-04.

**T15 · Destinos nuevos y un solo consumidor por destino.**
Decisión: destinos `Inventory`, `Treasury` y `Receipts` (§2.12); los dobles consumen esos mismos destinos. El
arranque falla con `Integration.Destination.DuplicateConsumer` si dos consumidores declaran el mismo destino
(`UnSoloConsumidorPorDestino`). `DestinoContabilidad` sigue siendo el único de `Accounting` y **reparte por
`OriginModule`**: INV al consumo actual, CAR a `PostLendingFactsCommand` / `PostLendingSummaryGroupCommand`;
`UnidadDeConsumo` gana `OriginModule`; las versiones aceptadas son por módulo; `PlanearLote` separa por módulo;
`TotalesDelLoteAsync` suma los dos recibos; las `ScheduleKey` de Cartera llevan prefijo `CAR.` y la hora sellada
y `ScheduleIntegrationBatchesCommand` usa el respaldo de Inventario sólo con claves INV;
`PendingInventoryMessagesQuery` filtra INV y su lógica pasa a `ListPendingIntegrationMessagesQuery(módulo)`. Por qué:
el despachador toma un consumidor por destino sin avisar; sin filtro por módulo, el aviso del cierre contable
contaría los pendientes de Cartera como de Inventario. FR: FR-045, FR-053, FR-080, FR-113, FR-125, FR-128,
FR-149. Origen: PM-06, PM-11, DC-02 (CH6, CH9, CH10).

**T16 · Registro de participantes por cooperativa y habilitación por el registro.**
Decisión: `COR_IntegrationParticipants` y `COR_IntegrationParticipantAdoptions` (§2.12), leídos por
`IRegistroDeIntegracion` y escritos por `EscritorDelRegistroDeIntegracion`, que agrega sin guardar (la activación
de Cartera y la habilitación de su destino van en el mismo guardado). Un participante cuenta si tiene fila activa,
su implementación está en el despliegue con esa versión y no es un doble en producción
(`Integration.Participant.TestDoubleInProduction`). `DestinosHabilitadosAsync` del despachador **deja de leer**
`ParametrosDeInventario.CarteraIntegracionHabilitadaDesde`: un destino se recorre si tiene consumidor y fila
activa a hoy. Antes de activar, los mensajes de Comercial se acumulan sin intentos ni alertas. El parámetro de
Comercial sigue sirviendo a Comercial (sello de la cuenta por cobrar, crédito provisional) y la pantalla de
activación le muestra que debe igualarlo. `SalesQueries` calcula «disponible» con el registro. Rutas
`/api/core/integration-participants`. Por qué: FR-142 dice que activar Cartera habilita la recepción, y Cartera
no escribe parámetros de Comercial; quita además la dependencia de la plataforma a `Domain.Inventory.Parameters`.
FR: FR-086, FR-089, FR-142, FR-143, FR-145, FR-149. Origen: PM-07, PM-08; DE-15 proponía exigir también el
parámetro (CH13, CH16).

**T17 · Consumidor de Cartera, recibo y evaluaciones.**
Decisión: `DestinoCartera` (destino `Lending`, en `Application/Lending/Integration`) acepta
`VentaACreditoRegistrada` v1 y v2, `AjusteDeVentaACredito` v1, `ResultadoDelGiro` v1 y, desde K2,
`EnvioDeDescuentosConfirmado` y `ResultadoDeDescuentos`. Cada familia se consume con un comando sin ruta
(`ConsumeCommercialCreditMessageCommand`, `ConsumeTreasuryResultCommand`, `ConsumePayrollDeductionResultCommand`)
que en un `SaveChanges` guarda el recibo `LND_InboundMessages` (UK `MessagePublicId`), una
`LND_InboundMessageEvaluations` por evaluación, el efecto y los mensajes que emite. Desenlaces: obligación creada
→ `Processed` con el PublicId y el número del crédito; evaluación negativa (FR-052) → la variante nueva
`ResultadoDeConsumo.ValidationFailed(ref, número, código, motivo)`, que `RegisterDeliveryResultCommand` lleva a
`DeliveryStatus.ValidationFailed` y con la que levanta `Integracion.ValidacionFallida`; mensaje no convertible →
evaluación + `ObligacionRechazada` + `Rejected` (reprocesable; una periodicidad o un plazo sin fila en
`LND_CommercialTermEquivalences` es de esta clase, FR-022); venta anterior al corte (FR-143) → según su conciliación,
`Processed`, o «no incorporada» con `ObligacionRechazada` no reprocesable; **ajuste de una venta aceptada sin migrar**
(FR-143) → `NotIncorporated` y `ObligacionRechazada` con motivo `SinObligacion`, no reprocesable, con la venta como
raíz de cadena. Confirmar por excepción cambia el recibo, no la entrega. Por qué: contrato §13 de la 012 (recibo único, orden, versiones) y FR-051: la
evaluación ocurre al consumir. FR: FR-009, FR-050–FR-055, FR-143, SC-005, SC-010. Origen: PM-09; DE-14 proponía
`ProcessedWithFailedValidation` (CH12).

**T18 · Mensaje de venta v2 y respuestas a Comercial.**
Decisión: `VentaACreditoRegistradaV2` = v1 + `ReservedLoanNumber` (string?) + `ReservationPublicId` (Guid?);
`CreditLineCode` ya existe en v1 y llega lleno cuando Cartera respondió. Es un mensaje de Comercial: su record va en
`Contracts/Inventory` y se agrega a `CatalogoDeMensajesV1` (`OriginModule = INV`, `AdmiteProceso = false`); las
entradas existentes reciben los campos nuevos de `TipoDeMensaje` sin cambiar los bytes de sus contratos. Comercial
emite sólo v2 y Cartera acepta las dos mientras quede alguna entrega v1; `AjusteDeVentaACredito` sigue en v1. Cartera
responde con `ObligacionCreada` (número, línea, valor, resumen del plan, estado `Validated` / `ValidationFailed` /
`Migrated` / `CancelledBeforeCutoff`) y `ObligacionRechazada` (código, motivo —`SinObligacion` para el ajuste de una
venta aceptada sin migrar—, reprocesable o no), al destino `Inventory`, con la venta como raíz de cadena; una
`ObligacionRechazada` por evaluación (`Confirmation:{evaluationPublicId:N}`).
`DestinoInventario` (Comercial) escribe `INV_LendingObligationResponses` (UK `MessagePublicId`): número y estado,
nunca saldos ni cuotas. Por qué: C2 y E-02; el número viaja sin que Comercial lea a Cartera. FR: FR-053, FR-055,
FR-057, FR-145, SC-005. Origen: PM-10, DE-14 (CH9, CH11).

**T19 · Bandeja de Cartera, turnos equitativos y lectura sin consumir.**
Decisión: la parte de Inventario de `ListIntegrationMessagesQuery` y `SendNotApplicableMessagesCommand` pasa a
una estrategia `IVistaDeBandejaDelModulo` (alcance, modo vigente por tipo, nombre del documento); Cartera la
implementa con alcance por oficina. Rutas `/api/lending/messages*` sobre `ReprocessMessagesCommand` (ya
genérico). El despachador turna los destinos (una tanda por destino por vuelta) en vez de vaciar cada uno.
`IMensajesEntrantes` gana `PendientesAsync(destino, tipos, hastaFechaDeOperacion, página)` para listar sin
consumir las ventas acumuladas. Por qué: un rechazo debe llegar a quien puede actuar; un atraso en Accounting no
puede comerse los 5 s de SC-005; FR-143 lista sin consumir y el consumidor no lee la bandeja por su cuenta. FR:
FR-016, FR-130, FR-143, SC-005. Origen: PM-15, PM-16, PM-18.

**T20 · Dobles de prueba de Recaudo y Tesorería.**
Decisión: `TesoreriaDePrueba` (consume `Treasury`, emite `ResultadoDelGiro` como TES) y `RecaudadorDePrueba`
(consume `Receipts`, llama al contrato y desde K2 emite como REC) se registran en DI sólo si
`Lending:TestDoubles:Enabled = true` **y** `!IHostEnvironment.IsProduction()`; si la configuración los pide en
producción, la API no arranca. En cada cooperativa se habilitan con una fila `Class = Test` que el escritor
rechaza en producción. Pantalla y rutas con `Lending.TestDoubles.Operate`. No son canales simulados (T73) ni dobles
de pruebas unitarias. Por qué: FR-089 exige que no puedan registrarse en producción y que una prueba lo fije
(`LosDoblesDePruebaNoSeRegistranEnProduccion`); el carácter de producción lo da el despliegue, no la cooperativa.
FR: FR-089, FR-142, FR-149, D27. Origen: PM-17 (CH20; pregunta B4 sobre el ambiente de QA).

### Idempotencia

**T21 · Cuatro capas y una gramática de claves.**
Decisión: (1) **pantalla**: comandos `IOperacionIdempotente`, cabecera `Idempotency-Key`, `COR_OperationKeys`
(`LosComandosDeCarteraLlevanClave`); (2) **contrato y reserva** (llamados dentro de la unidad de trabajo de otro
módulo): clave de negocio de texto más `HuellaDeOperacion.Calcular`, con índice único en el documento de Cartera
— `LND_LoanOperations` (`CollectorCode`, `IdempotencyKey`) y `LND_LoanNumberReservations` —; misma clave y misma
huella responden «ya aplicado» con el comprobante original, otra huella `Lending.Contract.KeyReused` /
`Lending.Reservation.KeyReused`; no son `IOperacionIdempotente` ni `IReintentableAnteConcurrencia`; (3)
**mensaje recibido**: `LND_InboundMessages`; **emitido**: el único (origen, tipo, clave) de la bandeja; (4)
**procesos**: marca de agua más índice único de la causación por crédito, concepto, cuota y fecha valor.
Gramática de claves de recaudador `{MÓDULO}:{operación}:{obligationPublicId:N}`, con `{MÓDULO}` = código de
módulo de `ModuloContable` (no el prefijo de tablas): Nómina `NOM:{runPublicId:N}:{obligationPublicId:N}` (versión
aprobada de la nómina o de la liquidación); recogida `CAR:{operaciónDeDesembolso:N}:{obligationPublicId:N}` (fijado
aquí); Recaudo y Aportes la fijan en su feature con su prefijo `REC`/su código. La **reserva** es una forma aparte,
porque no hay obligación todavía: `INV:{paymentPublicId:N}:{monto}`. Por qué: `COR_OperationKeys` usa un Guid,
compara el usuario central y abre su propia transacción, cuando el contrato corre en la del recaudador (I-4).
FR: FR-009, FR-057, FR-066, FR-086–FR-088, FR-112, SC-010. Origen: PM-12, DE-03, DE-13, M2 (CH17).

**T22 · Reserva de número de crédito.**
Decisión: puerto `IReservasDeCartera` (`ReservarAsync`, `LiberarAsync`; stub `ReservasDeCarteraNoHabilitada`, que
responde `NotEnabled` antes de la activación). Corre **dentro** de la unidad de trabajo de la confirmación de
Comercial, también si la venta queda en aprobación, sin guardar por su cuenta; orden de cerrojos inventario →
Cartera (setup y control del deudor, T34), y el numerador de Cartera al final. Como no pasa por el pipeline de
MediatR, cada método **ejecuta explícitamente su validador FluentValidation** (el mismo que tendría como comando) y
escribe su evento de auditoría (`Lending.NumberReservation.Reserved` / `.Released`, T68) en `COR_AuditOutbox`
dentro de la transacción. La falta de regla contable nunca la frena: no produce hecho contable (T56). Repetir la clave devuelve el mismo número; si el monto cambió,
Comercial libera la reserva y pide otra con la clave nueva. Estados `Reserved`, `Used`, `Released`, `Expired`;
una vencida (tarea `cartera.reservas-vencidas`) nunca se reasigna, y si después llega la v2 con ese número lo toma
(`Expired` → `Used`) con alerta informativa. La reserva lleva el valor y **descuenta cupo**: no hay tabla aparte de
reservas de cupo. `DiscardInventoryDraftCommand` y `DiscardPosDraftCommand` liberan. Por qué: la consulta de cupo
no escribe y se llama varias veces; reservar sí escribe; dentro de la confirmación no quedan reservas huérfanas.
FR: FR-057, FR-058, FR-145, D3, D26, SC-008, SC-010. Origen: PM-10, DE-13 (CH11, CH29; plan B en pregunta G6).

### Motor financiero y precisión

**T23 · Tres capas: hecho primario, libro y proyecciones.**
Decisión: (a) `LoanOperation` es la decisión de una persona o un proceso y **es el comprobante de la operación**
(lleva `Number`, recaudador, clave, clase de medio, pagador, referencia del recibo y la huella de la simulación):
no hay `LND_PaymentApplications`; (b) `LoanTransaction` es el libro, una fila por concepto y cuota con tres fechas,
nunca se edita, y saldo = Σ de sus filas; (c) proyecciones `RepaymentScheduleVersion`/`RepaymentInstallment`,
`LoanDailyPosition` (instantánea versionada sólo de inserción) y `LoanControl` (sin saldos: cerrojo y marca de
agua). La autoridad es la reconstrucción (T24); lo materializado se verifica contra ella. El evento de auditoría de
cada `LoanOperation` (el de `AuditBehavior` en pantalla, el de T68 en el contrato) lleva la **distribución completa
por cuota y concepto**: es el «antes y después» de FR-003 de las filas del libro que la operación escribe. Por qué: FR-081 y
SC-004 exigen redistribuir pagos posteriores, posible sólo si el pago se guarda como decisión; Contabilidad y la
conciliación necesitan filas con fecha contable. FR: FR-001, FR-002, FR-012, FR-069, FR-077, I-5, I-6. Origen:
M2; R-06 proponía otros nombres y una tabla de comprobantes (CH25, CH27).

**T24 · Liquidación a la fecha D con la información hasta R.**
Decisión: `ReconstruccionDeCredito.Reconstruir(entrada, D)` es la única fuente de saldos, cuotas, mora y
suspensión. Entran las operaciones con `RegisteredAt ≤ R` sin reverso registrado ≤ R, las versiones de condiciones
≤ R, los valores nacionales y parámetros conocidos en R (T38). Avanza por segmentos entre eventos, en forma
cerrada (costo por eventos, no por días). D futura se proyecta sin pagos y devuelve `ValidaHasta`. Consultar no
escribe; sólo audita la lectura (T67). El documento emitido guarda la huella y `RulesVersion`. Por qué: FR-011,
D25 y SC-001 al centavo, aunque después se registre un pago con fecha valor anterior. FR: FR-011, FR-068,
FR-069, FR-007, D25, SC-001. Origen: M3.

**T25 · Causación por diferencia de acumulados.**
Decisión: lo causado el día d por concepto y cuota es `U(A(d)) − U(A(d−1))`, con `A` el acumulado exacto
(compuesto `C·((1+r_d)^n − 1)` o lineal) y `U` el redondeo sellado; la ventana se reinicia al cambiar el capital
base o la tasa. Corriente sobre capital no vencido (incluido el de una cuota vencida en día no hábil hasta su
fecha exigible), mora sobre capital vencido, seguros según su periodicidad. El interés nunca entra a la base. Por
qué: el interés de la cuota resulta igual al causado entre vencimientos, al centavo y sin deriva. FR: FR-010,
FR-062–FR-064, FR-023. Origen: M4.

**T26 · Aritmética decimal y una única aproximación.**
Decisión: `AritmeticaDecimal` (potencia por cuadrados, raíz por Newton a 24 decimales, TIR Newton con bisección);
la tasa diaria es `RaizEntera(1+EA, B) − 1` redondeada a 18 decimales —la única aproximación del motor, escrita en
la explicación—; `FuncionLogistica` (K6a) por serie en decimal. Prohibidos en `Domain/Lending` `double`, `float`,
`Math.Pow`, `Math.Exp`, `Math.Log` (`ElMotorDeCarteraEsDecimal`). Los casos dorados se calculan con decimal de 40
dígitos, no en Excel. Por qué: `decimal` es software y da lo mismo en toda plataforma; el `double` del legado no
es reproducible. FR: FR-010, SC-001, SC-023. Origen: M5, R10.

**T27 · Convenciones de días y calendario hábil.**
Decisión: `ContadorDeDias` cuenta de a excluida a b incluida (30/360 con el 31 que no causa y fin de febrero que
completa 30; real/365 con base fija; real/360). La regla del art. 829 (pagar el siguiente hábil sin mora) no se
configura. `CalendarioHabil` es puro, vive en `Domain/Common/Calendar` y recibe la lista de días no hábiles; la
plataforma la sirve por `ICalendarioHabil` (`CalendarioDeDiasHabiles`) con los festivos **nacionales** replicados
(no los «Manual» de Nómina); el motor los recibe en `EntradaDeReconstruccion`. No se reutilizan
`CalendarConventions` ni `DiasHabiles` de Nómina. Por qué: otra semántica (prorrateo inclusivo) y acoplaría a
Nómina. FR: FR-010, FR-022, FR-070. Origen: M6, R5, F-05 (CH34; preguntas D2, D3).

**T28 · Usura dentro del motor.**
Decisión: `ControlDeUsura` compara la efectiva resultante (TIR diaria de los flujos con cargos computables) con la
usura de su certificación al crear o modificar la línea, simular, aprobar y desembolsar; sella la sobretasa de
cargos al desembolsar; topa cada día la tasa corriente y la de mora (`TasaDelDia`); una usura (o cualquier valor
nacional) corregida después de causar días que la usaban se aplica como «reliquidación por corrección de valor
nacional»: el primer paso del ciclo diario, al replicar la corrección, encola una corrida `NationalValueCorrection`
del `ProcesadorDeCartera`, que nadie ordena a mano (la corrección la registró el maestro en la central, fuera de la
cooperativa) y corre como el proceso, por tandas (`RunNationalValueCorrectionCommand`); registra ajustes
(`AjusteDeCausacionPorFechaValor`) y, si se cobró de más, saldo a favor (`SaldoAFavorReconocido`), dos tipos que por
eso admiten proceso (T11). Por qué: el caso límite prohíbe comparar la nominal; SC-002 exige cero días por encima y
la devolución no puede esperar a que alguien la ordene. FR: FR-020, FR-021, SC-002. Origen: M7.

**T29 · Plan de pagos y versión de reglas.**
Decisión: `GeneradorDePlan` proyecta la causación de T25 suponiendo pagos a tiempo (cuota fija por valor presente
con factores reales, abono constante, pago único, extraordinarias, gracia a capital o total sin capitalizar); Σ
capital = monto (SC-017); la última cuota absorbe el resto. Cada operación que cambia el plan produce una versión
regenerada desde la decisión. `LoanTermsVersion` sella `RulesVersion` (`LND-AAAA.n`): un cambio de algoritmo que
mueva centavos es versión nueva y sólo aplica a créditos o novedades nuevos. Por qué: regenerar desde la decisión
permite FR-081; `RulesVersion` protege FR-011 frente a cambios del programa. FR: FR-022, FR-023, FR-031, FR-034,
FR-067, FR-075, SC-017. Origen: M8.

**T30 · Mora, altura y suspensión derivadas de la reconstrucción.**
Decisión: el capital de la cuota vence el día siguiente a su fecha exigible; la altura de mora cuenta desde la
cuota impaga más antigua cuyo saldo vencido supere `Mora.ToleranciaDeSaldoVencido`, sin las cubiertas por
anticipo (y desde K2 sin las «descontado pendiente de giro» dentro del umbral). Suspensión por altura de la
modalidad, reestructurado más de una vez, insolvencia o castigo; lo suspendido se causa en
`CurrentInterestInSuspense` / `DefaultInterestInSuspense` (el castigo usa los mismos conceptos con su marca); lo
causado antes no se reclasifica (lo deteriora SARC al 100 %). Las transiciones salen del estado; Application emite
los mensajes. Por qué: una marca puesta por el proceso no se reconstruye ante una fecha valor anterior. FR:
FR-063, FR-065, FR-069, FR-070, FR-096, FR-114. Origen: M9.

**T31 · Aplicación de pagos.**
Decisión: `AplicadorDePagos.Distribuir` simula y aplica con la misma función; prelación sellada
(`OldestInstallmentFirst` por defecto: gastos informados, seguros, mora con su suspenso, corriente con su
suspenso, capital); siete modalidades de `PaymentModality`; lo que sobra es excedente y vuelve al recaudador; el
abono extraordinario usa la elección del deudor o su preferencia vigente y, sin preferencia, queda como saldo a
favor; el anticipo no reduce intereses. Todo dinero que reduce una obligación entra por el contrato (T91); no hay
comando de pantalla para aplicar pagos. `ReverseLoanPaymentCommand` (el reverso que pide un usuario de Cartera)
rechaza las operaciones con `CollectorClass = Payroll` (`Lending.Reversal.OwnedByCollector`): esos pagos sólo los
reversa Nómina por `ReversarAsync`, y así el NM y `PAY_PayrollLoanDeductions` nunca conservan un descuento que en
Cartera ya no existe. Por qué: FR-072 exige distribución idéntica al simular y al aplicar; D24; FR-080 («un pago de
Nómina sólo lo reversa Nómina»). FR: FR-030, FR-071–FR-077, FR-080, FR-087, D5, D6, D24, SC-003. Origen: M10 (CH47).

**T32 · Simulación sin estado con huella.**
Decisión: simular es reconstruir y distribuir sin escribir; `SimulationId` = SHA-256 canónico (`HuellaDeCartera`)
de las entradas y la distribución. Aplicar en la forma interactiva recalcula bajo el cerrojo y compara la huella:
si difiere, `Lending.Payment.DistributionChanged` con la simulación nueva; la forma sin interacción nunca responde
«cambió». En un abono extraordinario una simulación devuelve los dos planes. Por qué: comparar la distribución y no
un contador evita «cambió» falsos cuando sólo corrió la causación. FR: FR-073, FR-079, FR-085, SC-003, SC-008.
Origen: M11.

**T33 · Fecha valor anterior y reversos.**
Decisión: en un reverso (de pago o de desembolso, FR-048), **cada fila propia de la operación reversada recibe su
fila espejo** con `ReversesTransactionId` (Principio XI, FR-002: la cadena original → reverso se ve desde los dos
lados); en el desembolso reversado también las de la causación de ese crédito. Después se reconstruye con y sin la
operación y `RegistroDeCartera.Conciliar` registra **además** cada diferencia restante por (día valor, concepto,
cuota) como transacción nueva (`AccrualAdjustment`, `PaymentReallocation`, `SuspenseReclassification`) con fecha
contable del período abierto, enlazada a la operación de reverso; lo mismo, sin espejos, para un pago con fecha valor
anterior. Los pagos posteriores se redistribuyen con su fecha valor y elección, y se muestran antes de confirmar. Lo
fija `ElReversoEnlazaCadaFilaConSuOriginalTests`. Invariante: Σ libro a d =
reconstrucción(d, ahora) para todo d ≤ `LastAccruedDate` (`VerifyLendingLedgerIntegrityQuery`, muestra nocturna en
el ciclo y completa bajo pedido). El límite D4 y la atribución antes de un cierre los aplica Application (con la
excepción de Nómina de la enmienda A9). Por qué: el crédito debe quedar exactamente como si el pago no hubiera
existido, sin editar nada. FR: FR-002, FR-012, FR-048, FR-078, FR-080, FR-081, SC-004. Origen: M12.

**T34 · Concurrencia por crédito.**
Decisión: `ICerrojoDeCartera` en orden fijo dentro de `TransaccionExplicita`: `LND_Setup` compartido (exclusivo al
cerrar o reabrir) → `LND_DebtorControls` exclusivos por Id, del deudor y de los codeudores de la operación (la fila se
crea si falta, como las proyecciones de la 012) → `LND_LoanControls` exclusivos por Id (todos los de la operación en
una sentencia) → numeración al final. El paso por deudor lo toman la reserva, la aprobación, el desembolso y la
asignación de cupo: sin él, dos de ellas del mismo deudor leerían el mismo disponible (FR-058) y se pasarían del cupo,
porque una reserva o una solicitud todavía no tienen `LoanControl`. Las operaciones esperan (`FOR UPDATE` / `UPDLOCK, ROWLOCK, HOLDLOCK`); el proceso salta (`SKIP LOCKED` /
`READPAST`). `Sequence` y `RegisteredAt` monótonos por crédito bajo el cerrojo. El contrato de obligaciones usa
este mismo cerrojo (no `ICerrojoPorClave`). Validaciones previas fuera del cerrojo. Por qué: una operación espera
a la otra sin perderse ninguna y la causación masiva nunca deja esperando a una caja. FR: FR-041, FR-058, FR-066,
FR-079, FR-086. Origen: M13; DE-02 proponía el cerrojo por clave (CH19).

**T35 · Precisión y redondeo.**
Decisión: `PrecisionDeCartera` con los alias `Monto` (18,2), `TasaAnual` (9,6, fracción), `TasaEquivalente`
(28,18) y `ValorUvr` (18,4, K6b); `LosSaldosYTasasDeCarteraTienenSuPrecision` exige el alias. `RedondeoDeCartera`
sella unidad (0,01; 1; 10; 100; 1.000), modo (`AwayFromZero`, `ToEven`, `Up`, `Down`) y momento (`PerConcept`,
`PerInstallment`, `PerTotal`); el residuo queda visible. Una tasa pactada con más de 6 decimales de fracción se
rechaza. Por qué: PostgreSQL redondea en silencio `numeric(18,2)`. FR: FR-010, FR-020, SC-017, SC-023. Origen:
M15.

### Fechas y valores nacionales

**T36 · Fecha contable.**
Decisión: Cartera la calcula al emitir con `IContabilidadParaCartera.EstadoDelPeriodoAsync`: la fecha valor si su
mes no tiene cierre de Cartera y está abierto en Contabilidad; si no, el primer día del primer mes que cumpla las
dos cosas. La causación del día d se fecha d; los hechos del cierre de M, el último día de M. Contabilidad fecha el
comprobante con esa fecha y nunca la corre; `PeriodClosed` → `Rejected`; `PeriodNotFound` (p. ej. el ejercicio
siguiente sin abrir el 1 de enero) → `Retry` con la alerta `Contabilidad.PeriodoInexistente`. Por qué: la fecha
contable es de Cartera (FR-012) y la conciliación la necesita idéntica en los dos lados. FR: FR-012, FR-107, D4.
Origen: DC-09, PM-05.

**T37 · Registro central y copia por cooperativa.**
Decisión: los valores nacionales se registran una vez en la base administrativa (`ADM_NationalValues`,
`ADM_RegulatoryDocuments`, rutas `/api/saas/*`, el maestro con segundo factor) y se copian sin modificar a
`COR_NationalValues` y `COR_RegulatoryDocuments` de cada cooperativa. **Ningún cálculo lee `ADM_`**. Dos formas:
escalares por código y clave, y documentos normativos tipados en JSON con `SchemaVersion` y `Verified` (molde
PILA). Por qué: I-3 (un registro, sin lecturas entre bases al calcular); la usura cambia cada mes y el IBR cada
día, así que no puede ser JSON embebido. FR: FR-014, FR-015, FR-149, I-3. Origen: R1, R2.

**T38 · Bitemporalidad con una sola regla de lectura.**
Decisión: toda fila con vigencia de los valores nacionales es hecho de sólo inserción; corregir es una fila nueva
con `SupersedesPublicId`. `VigenciaBitemporal.Vigente(versiones, D, R)` toma las versiones conocidas en R que
cubren D y no fueron reemplazadas en R; gana el `ValidFrom` más reciente y, si empatan, la `Sequence` mayor. En la
cooperativa R es `KnownAt`. `LectorDeParametros` gana `informacionHasta` opcional, con una regla **propia** porque
`COR_ParameterVersions` no tiene `Sequence`, `SupersedesPublicId` ni `KnownAt` y `AddParameterVersion` reescribe
`ValidTo`: R se compara con `CreatedAt`; entran las versiones creadas hasta R; el `ValidTo` guardado se ignora y se
deriva de la versión siguiente conocida en R (la víspera de su `ValidFrom`); una baja posterior a R no cuenta. Sin
`informacionHasta`, nada cambia para Inventario. Corregir una vigencia ya empezada sigue siendo imposible para los
parámetros de la cooperativa (`SeCruza`): sólo los valores nacionales se corrigen, con `SupersedesPublicId`. Por qué:
reconstruir «lo que se sabía en R» (FR-011) sin cambiar el escritor único de parámetros de la 012. FR: FR-007,
FR-011, FR-012, D25. Origen: R3, M3.

**T39 · Réplica idempotente.**
Decisión: un núcleo único (`ReplicacionDeValoresNacionales.AplicarAsync`) copia en orden lo que tenga `Sequence`
mayor que la última local, con `SourcePublicId` único como guarda, escribe `COR_NationalValueReplications` y guarda
una vez. Tres entradas: la tarea `valores-nacionales.replicar`, el seeder `NationalValuesReplicaSeeder` (alta de
cooperativa) y el primer paso del ciclo diario y del cierre (que guardan la marca de agua usada). Si la central no
responde, el cierre oficial se niega con motivo. Los efectos en otros módulos van por `IEfectoDeReplicacion` en la
misma transacción. La réplica se audita con el módulo `NationalValues`. Por qué: ninguna transacción cruza bases y
reintentar es seguro. FR: FR-015, FR-016, FR-066, FR-107, SC-020, I-3. Origen: R4.

**T40 · Lectores únicos de valores nacionales.**
Decisión: dentro de la cooperativa sólo leen `COR_NationalValues` y `COR_RegulatoryDocuments` `IValoresNacionales`,
`ICalendarioHabil` e `INormativaVigente`; sólo el replicador lee la central. `IValoresNacionales` ofrece `LeerAsync`,
`LeerUltimoPublicadoAsync` (último índice publicado a la fecha, con alerta si es viejo) y `LeerSerieAsync` para la
carga en bloque de la causación; `ISeriesRegulatorias` de Cartera se construye sobre ellos. Si falta el valor de una
fecha, lo que lo necesita se bloquea (`NationalValues.Missing`) y se alerta. Los lectores de Nómina y de la 012 que
leen su propia proyección (`PAY_LegalParameters`, `PAY_Holidays`, y `LectorDeUvt` para la UVT) no cambian: leen lo
que sólo escribe la réplica (T93). Por qué: un sitio memorizado y
vigilable (`LosValoresNacionalesSeLeenEnUnSoloSitio`, `SoloElReplicadorLeeElRegistroCentral`). FR: FR-015,
FR-016, FR-067, FR-070, SC-018. Origen: R5, M1, M14 (CH35, CH48).

**T41 · «Pendiente de validar» es un hecho aparte y no bloquea.**
Decisión: la conformidad del contador u oficial es `COR_NationalValueReviews` (sobre la copia, sin modificarla);
sin revisión, el valor está «pendiente de validar». Calcular no se bloquea: el cierre guarda la marca
`ParametrosSinValidar` con la lista, el reporte de cambios y el SICSES la muestran como advertencia y se alerta
`Cartera.NormativaPendienteDeValidar`. `Verified` del documento es el cotejo de la plataforma. La activación en
producción sí exige un Anexo 1 vigente y la usura de las modalidades de las líneas activas. Por qué: precedentes
D-43 (PILA) y `TaxRate.ReviewPending`; no frenar el primer cierre obligatorio. FR: FR-014, FR-136, FR-142. Origen:
R7 (pregunta E1).

**T42 · Pisos y techos legales; la cooperativa sólo puede ser más estricta.**
Decisión: cada límite es un valor nacional `LIMITE` con su clave; `CatalogoDeLimitesLegales` fija unidad,
dirección de lo estricto y la clave de parámetro que acota. Un `IReglasDeParametros` de Cartera rechaza una
vigencia menos estricta (`Lending.Parameter.BelowLegalFloor`, con el límite y su fuente); al leer, rige lo más
estricto entre cooperativa y límite vigente en D; un límite nuevo más estricto alerta
`Cartera.LimiteLegalMasEstricto`. La fracción calificada de vinculados (4/5) y los plazos del titular son límites.
Por qué: reusa el escritor único de parámetros y no deja valores legales en el código. FR: FR-014, FR-015,
FR-040, FR-041, FR-122, FR-123, SC-018. Origen: R8, S5, S10.

**T43 · Parámetros de Cartera en la plataforma.**
Decisión: módulo `LND` en `COR_ParameterVersions` (catálogo `ParametrosDeCartera`), con los ámbitos nuevos
`CreditLine = 6` y `LendingFactType = 7` (sobre `LND_FactTypes`; el `IResolutorDeAmbitoDeParametro` pasa a
compuesto Inventario + Cartera). Claves de §2.20 sin prefijo «Cartera.». Lo que no es decisión de la cooperativa
(hora del ciclo, paralelismo, tamaño de tanda) es configuración técnica. Las decisiones con tablas (política SARC,
atribuciones, órganos) tienen su tabla. **Entrega de cada clave**: hoy la plataforma sólo restringe por
`EntregaDelComercio` (`DefinicionDeParametro.DisponibleDesde`, `CatalogoDeParametros.EntregaVigente = I6`); pasa a
una entrega por módulo (`EntregaDeModulo`, §2.6), de modo que una clave de Cartera marcada K2, K4 o K6a no se ofrece
ni se acepta hasta que `CatalogoDeCartera.EntregaVigente` la alcance; lo fija
`CadaParametroRespetaLaEntregaDeSuModulo`. Por qué: un lector y un escritor únicos, auditados. FR: FR-007, FR-014,
FR-149. Origen: R17, DC-11, M, F, S7 (CH36).

**T44 · Catálogo de modalidades como documento nacional.**
Decisión: modalidades y sub-segmentos son el documento `CatalogoDeModalidades` con vigencia, que por combinación
da segmento SARC, segmento de pérdida esperada, la clave de `USURA` (modalidad certificada) o el tope de vivienda, y
si admite UVR. La versión de la línea sella **códigos**, no una llave; la usura de un crédito en D se busca por la
modalidad certificada que el catálogo vigente en D asigna. No hay tablas `LND_CreditModalities`. Una modalidad
retirada cierra las líneas a créditos nuevos. El cambio de segmento efectivo (p. ej. la libranza deja de estar
activa) emite `DimensionContableCambiada`. La versión de la línea sella además el **concepto tributario del
interés** (no gravado o gravado, con el soporte del concepto de la contadora): sin él, una línea de origen comercial
con tasa mayor que cero no se activa (`Lending.CreditLine.InterestTaxConceptMissing`); si es gravado, lo factura
Comercial (D20; pregunta C11). Por qué: la clasificación es nacional y la cooperativa no inventa modalidades; D20 no
tiene valor por defecto. FR: FR-019–FR-021, FR-024, FR-125, FR-126, D20. Origen: R16; R-06 proponía tablas por
cooperativa (CH28).

### Riesgo y cierres

**T45 · Normativa SARC tipada y política de la cooperativa.**
Decisión: los documentos que la plataforma valida o consume (`CatalogoDeModalidades`, `TiposDeOrganizacion`,
`CatalogoDeLimitesLegales`) viven en `Domain/Common/NationalValues`; los que sólo usa Cartera (`Anexo1`, `Anexo2`,
`ReglaDePerdidaEsperada`) en `Domain/Lending/Risk/Normativa`, y la central los valida al registrarlos por el punto de
extensión `IValidadorDeDocumentoNormativo` que Cartera implementa: la plataforma no depende de un módulo (CH34). Los
anexos son documentos tipados (`Anexo1`: segmentos, rangos, reglas especiales, porcentajes con
condición de subcategoría tipada y base, garantías por meses, aportes, arrastre, suspensión, reestructurados,
general; `Anexo2`: variables tipadas, coeficientes, cortes, PI por tipo de organización con clave `VOLUNTARIA`,
PDI, VEA, homologación). La política del consejo es `LND_RiskPolicyVersions` (sobreescrituras con el mismo esquema
parcial, sólo más estrictas celda por celda, validadas con permiso propio, ligadas a un punto de acta). Se combinan
en `PoliticaSarcEfectiva.Combinar`, pura; si la norma se endurece después, gana la norma. Por qué: una circular
nueva es dato, no código. FR: FR-007, FR-014, FR-099, FR-103, FR-104, FR-106. Origen: R2, R9, R10, R11.

**T46 · Motores de riesgo puros.**
Decisión: `MotorDeCalificacion` en dos fases (por crédito: mora, modelo, pago único, crédito nuevo, reestructurado,
evaluación, análisis, insolvencia; luego arrastre por deudor y modalidad SARC) devuelve la peor candidata con su
causa, las candidatas, la subcategoría y la calificación contable A–E; `MotorDeDeterioroIndividual`,
`MotorDeDeterioroGeneral`, `MotorDePerdidaEsperada` (K6a), `SelectorDeModeloDeDeterioro` y
`HomologacionDeCalificacion`. Mientras rija D19: sin excepción de aportes en el arrastre y sin descuento de aportes
en el deterioro. La calificación diaria informativa usa el mismo motor al leer y no se guarda. Casos dorados en
`Domain.Tests/Lending/Risk/Casos`. Por qué: molde de Nómina; reproducible y sin valores legales. FR: FR-093,
FR-098–FR-106, SC-021, SC-023, D19, I-6. Origen: R12.

**T47 · Cierre mensual inmutable con R sellado.**
Decisión: `OrderMonthlyCloseCommand` encola (202) una corrida `MonthlyClose` del `ProcesadorDeCartera`; exige el
**cierre anterior vigente** —el mensual del mes anterior o, en el primer cierre tras activar, el cierre base, aunque
sea del mismo mes (T49)—, período contable abierto, causación al día hasta fin de mes y réplica sincronizada. Fija
`InformationCutoffAt` (R), la marca de agua de valores nacionales, los documentos usados con su SHA-256, la política
y el modelo por segmento. Aplica la regla de T5 («un hecho nace en la transacción que guarda lo que cuenta»): (1) las
tandas calculan y guardan `LND_MonthlyCloseLoans` enlazadas a la corrida, sin mensajes; (2) una **transacción final**
inserta el encabezado `LND_MonthlyCloses` (H) con R y las marcas `ObligatoryModelUnavailable`,
`UnvalidatedParameters`, `IncompleteHistory`, `LND_MonthlyCloseGeneralImpairments`, las partes
(`LND_AccountingFactParts`, una por tanda × sucursal × tipo) con sus mensajes `DeterioroCalculado` y
`CarteraReclasificadaPorCalificacion` (T48) y el informativo `CierreDeCarteraRealizado`. Una corrida que no termina
deja líneas sin encabezado, que no cuentan; el reintento las retoma. Reporte de cambios y cuadre se derivan al leer.
Reabrir: sólo el último, con motivo y atribución y antes del cierre contable; en una transacción inserta
`LND_MonthlyCloseReopenings` (H) y emite `ReversoDeCierre` por partes, enlazado a las del cierre, y
`CierreDeCarteraReabierto`. Encabezado y líneas nunca cambian: el estado `Reopened` y la vigencia de las líneas se
**derivan** de la reapertura (I-5 incluye los cierres en el Principio XI); volver a cerrar crea `Version + 1`. Cierre manual por defecto
(`Riesgo.CierreProgramado` apagado; alerta `Cartera.CierreMensualPendiente` pasado `Riesgo.DiaLimiteCierre`). Por
qué: FR-107 y FR-011; 50.000 créditos no caben en una petición. FR: FR-012, FR-107, FR-125, FR-128, FR-130,
FR-146, D12, SC-006. Origen: R13, DC-13, PM-13 (CH1, CH7).

**T48 · Reclasificación completa por categoría y deterioro en delta.**
Decisión: al cerrar M, Cartera emite por partición y en orden: (1) `CarteraReclasificadaPorCalificacion` de
**todo** saldo con dimensión de categoría —capital, intereses, pagos por cuenta, deterioro individual de cada
concepto y suspenso en orden— de la categoría anterior a la nueva; (2) `DeterioroCalculado` como **delta** en la
categoría nueva: aumentos contra gasto; disminuciones primero contra el gasto del mismo ejercicio y el resto contra
recuperación (`Contabilidad.DisminucionDeDeterioro`); (3) general y adicional con su traslado. La categoría contable
de un saldo es siempre la oficial del último cierre (o del cierre base), nunca la diaria; las subcategorías van a su
letra. El primer cierre compara con el cierre base. Por qué: en el CUIF el deterioro y el suspenso también van por
categoría; liberar y constituir todo inflaría gasto e ingreso. Amplía FR-107 (pregunta C4). FR: FR-103, FR-104,
FR-107, SC-006. Origen: DC-13, DC-06.

**T49 · Cierre base e historial de mora.**
Decisión: al activar nace un `MonthlyClose` `Kind = Base` **fechado al corte** con lo migrado, sin hechos. El corte
no tiene que ser fin de mes (FR-142 no lo exige): el primer cierre mensual es el del mes que contiene la fecha de
activación y toma el base como cierre anterior (T47). Así, si el corte es el 15, Cartera cierra ese mismo mes y
Contabilidad puede cerrarlo con la regla de T57(a). El
historial vive en `LND_LoanArrearsHistory` (origen `Migration`, `ComplementaryLoad`, `MonthlyClose`);
`HistorialDeMora.Construir` elige la fuente que manda por mes, sigue novaciones y refinanciaciones y responde si está
completo con los meses que pide `ReglaDePerdidaEsperada`; los faltantes siguen `Riesgo.ReglaMesesFaltantes`
(«la mayor observada», pendiente de validar). La carga complementaria (`LND_ArrearsHistoryLoads`) es K6a. Por qué:
el Anexo 2 pide la mora máxima mensual de hasta 36 meses y antes del corte no hay transacciones. FR: FR-105,
FR-139, FR-140, FR-142, SC-013. Origen: R14 (CH26, CH46).

**T50 · Perfil regulatorio, selector de modelo y obligación sobrevenida.**
Decisión: el perfil es plataforma (`COR_RegulatoryProfileVersions`, rutas `/api/core/regulatory-profile`), con
grupo NIIF con vigencia sembrado desde `AccountingSetup.NiifGroup` y alerta si difieren, y el registro como operador
de libranza (FR-108), que se valida antes de originar por libranza. `SelectorDeModeloDeDeterioro`
devuelve por segmento modelo, fundamento y marca. K6a se habilita con `CatalogoDeCartera.EntregaVigente`
(`EntregaDeCartera`). La obligación sobrevenida (D28) se evalúa al registrar el perfil y en el efecto de réplica de
una `ReglaDePerdidaEsperada` nueva registrada después de la activación: alerta `Cartera.ObligacionSobrevenida`, y sin
K6a los cierres llevan `ObligatoryModelUnavailable`. La adopción voluntaria se niega sin K6a, con historial
incompleto o con aviso fuera de plazo. Por qué: el tipo de entidad decide lo que la norma exige y otros módulos
(SICSES, Aportes) lo usarán. FR: FR-017, FR-018, FR-105, FR-142, D28, Q2. Origen: R15 (preguntas A1, E2, E3).

### Contabilidad (enmienda E-01)

**T51 · Frente contable propio; Cartera nunca llama al poster.**
Decisión: Cartera emite hechos y Contabilidad los convierte en comprobantes con piezas propias (`ACC_Lending*`,
`Application/Accounting/Lending`, adaptador `ContabilidadParaCartera`). Dimensiones por código, sin FK a `LND_`;
sólo cuenta, sucursal y centro llevan FK `Restrict`. Lo genérico del consumidor de Inventario (recibo, revisión de
versión y moneda, rechazo con líneas, conjuntos de cuentas) se extrae a una base común sin cambiar el
comportamiento de INV. `AccountReferenceFinder` y `ListInvalidParameterizationsQuery` miran la matriz de Cartera.
Por qué: C3 y R6 prohíben la llamada directa; las tablas propias no tocan la matriz de Inventario en producción.
FR: FR-125, FR-126, FR-129, FR-146. Origen: DC-01.

**T52 · Catálogo fijo de hechos y roles; dimensiones de la matriz.**
Decisión: `OperacionesDeCartera` (los hechos de §2.11) y `RolesDeCuentaDeCartera` (`Capital`,
`InteresPorCobrar`, `PagoPorCuentaDelAsociado`, `CargoPorCobrar`, `Ingreso`, `SuspensoEnOrden`,
`ContraOrdenContingente`, `CastigadaEnOrden`, `ContraOrdenControl`, `DeterioroIndividual`, `DeterioroGeneral`,
`GastoDeterioro`, `RecuperacionDeterioro`, `GastoDeCastigoOCondonacion`, `IngresoPorRecuperacion`,
`IngresoDiferido`, `RecaudoPorAplicar`, `DesembolsoPorGirar`, `PuenteComercial`, `CuentaPorCobrarComercial`,
`Anticipo`, `SaldoAFavor`, `SeguroPorPagar`, `CuentaPorPagarATercero`), datos fijos en código con su tabla
débito/crédito (importe negativo invierte el lado). Columnas de `LendingPostingRule`: `Operation`, `Role`,
`CreditLineCode`, `ModalityCode`, `SubsegmentCode`, `RiskCategory`, `ConceptCode`, `IsEmployee`, `InGracePeriod`,
`OnBehalfOfAssociate`, `Restructured`, `CollectorClass`, `ReasonCode`, `BranchId`, `CostCenterId`, `AccountId`,
vigencia, `DimensionKey`, `SpecificityWeight`. Exigidas sin `*` según el rol; opcionales con pesos en potencias de
dos (Línea 512, Empleado 256, Gracia 128, Sub-segmento 64, Reestructurado 32, PorCuentaDelAsociado 16, Centro 8,
Sucursal 4). La dimensión «deudor empleado» sale del estado actual de la persona maestra. `ResolveLendingRuleQuery`
responde «¿a qué cuenta va esta combinación?». Roles sembrados sin cuenta, «pendiente de validar». Por qué: el CUIF
hace que «empleados» gane a la división por libranza y separa intereses, deterioro y suspenso por categoría. FR:
FR-099, FR-107, FR-125, FR-126, FR-129. Origen: DC-05, DC-06 (preguntas C4, C7, C8).

**T53 · Puente por clase de recaudador y `DESC_CARTERA` con asiento.**
Decisión: `PagoAplicado`, `SaldoAFavorResuelto` (devuelto) y `RecuperacionDeCastigada` mueven el rol
`RecaudoPorAplicar`, resuelto por `CollectorClass`; cada recaudador contabiliza su lado contra ese puente.
Enmienda D-08 de la 010: `DESC_CARTERA` vuelve a contabilizar en el NM y en la definitiva contra el puente
«recaudos por nómina para Cartera» (`AffectsAccounting = true`); se retiran la excepción de `ICalculationRule`,
de `ProposedDeductionsRule` y de `AdjustSettlementDeductionCommand`, y `CuentaDebitoDelDescuentoAsync`. La recogida
lleva su puente a la misma cuenta que `DesembolsoPorGirar`, así el neto es lo que queda por girar. Por qué: Cartera
no conoce cajas, medios ni pasivos del recaudador (FR-008); el saldo del puente es lo recaudado sin contabilizar y
la conciliación se explica sola. FR: FR-046, FR-056, FR-083, FR-088, FR-112, FR-126, FR-130, FR-147. Origen: DC-07,
DE-08 (pregunta C1).

**T54 · Tipos de comprobante y documento cruce.**
Decisión: la tabla de §2.19; `ACC_LendingVoucherMappings` con clave `Operation`/`CollectorClass`; se reutilizan
`DS`, `RC`, `DN`, `CA`, `PV` (ya de CAR) y se agrega `CN`; cruce `PG` con el número del crédito y `FV` en los roles
de Comercial. Las filas de Cartera del contrato de la 009 (`contabilizacion.md`) se reemplazan por la referencia a
esta enmienda. Por qué: «los sembrados no se eliminan ni cambian de uso»; C3 reemplaza la llamada directa, no los
tipos. FR: FR-125, FR-126, FR-146. Origen: DC-08 (pregunta C6).

**T55 · Lo de Cartera se corrige con comprobante nuevo.**
Decisión: nada de Cartera usa `PrepareReversalAsync`; la guarda del poster se extiende a CAR
(`Accounting.Document.LendingCorrectsWithNewVoucher`) y `ReverseDocumentCommand` responde `ModuleOwned`. Cada reverso
es un comprobante regular de su propio hecho y fecha contable, enlazado por `RelatedMessagePublicId`, con las
**dimensiones vigentes** del saldo que restituye (p. ej. la categoría actual), resueltas con la matriz vigente en
esa fecha; si los dos lados resuelven la misma cuenta, no hay líneas. En el libro de Cartera el reverso de un pago o
de un desembolso (FR-048) es siempre fila espejo con `ReversesTransactionId` más las diferencias de la reconstrucción
(T33); este comprobante contabiliza esas filas. Por qué: un pago de A reversado tras un cierre
que pasó el crédito a C restituye capital que hoy vive en C; espejar A rompería SC-006. FR: FR-002, FR-048,
FR-080, FR-107, FR-130, I-5. Origen: DC-10 (`LoDeCarteraSeCorrigeConComprobanteNuevo`).

**T56 · Modo de paso, resumido y validación previa.**
Decisión: cada tipo de hecho sella su modo al emitirse (ámbito `LendingFactType`). Defectos (D8): `InteresCausado`,
`MoraCausada`, `SeguroCausado`, `AjusteDeCausacionPorFechaValor` y `DimensionContableCambiada` emitida por el
proceso → `PorLotes` resumido con disparador `FinDeCausacion` (como el modo se guarda por tipo, el de
`DimensionContableCambiada` lo decide `ModoDePasoDeCartera`: si el tipo no tiene vigencia propia, `EnLinea` cuando la
emite una operación y `PorLotes` cuando la emite el proceso; con vigencia propia, rige para los dos; lo fija
`ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite`); `DeterioroCalculado`,
`CarteraReclasificadaPorCalificacion` y `ReversoDeCierre` → `PorLotes` resumido con `CierreDeCartera`; todo lo demás
`EnLinea`; `NoPasa` mientras la contabilidad no esté iniciada. `BatchTrigger.LendingAccrualRun` y
`LendingMonthlyClose` crean el lote en el guardado de la corrida o del cierre (`BatchScopeKey` `AccrualRun:{fecha}`,
`LendingClose:{aaaa-mm}`). El resumido de la causación es **un comprobante por día** (clave fecha contable + tipo),
cada línea con su sucursal; suma sin netear por cuenta, lado, sucursal y centro y conserva una línea por crédito en
las cuentas que exigen tercero, cruce o base. Validación previa por `IContabilidadParaCartera.EvaluarAsync`,
reducida a una pregunta por combinación de dimensiones: la falta de regla **bloquea sólo** en las operaciones que
nacen en pantallas de Cartera (desembolso, novedad, condonación, castigo) si `Contabilidad.FaltaDeRegla = Bloquear`;
en el contrato, en el consumidor de Comercial y en los procesos **siempre advierte**, sella `PrevalidationOutcome` y
alerta; sin respuesta rige D7 (advierte y permite). La reserva de número no se evalúa (corre dentro de la confirmación
de Comercial y no produce hecho contable) y el alta de una obligación desde una venta tampoco se frena: la crea el
consumidor, que advierte y sella, y D9 manda crearla aunque la evaluación falle; **la venta de Comercial nunca se
detiene por la matriz de Cartera**. Que «Bloquear» no alcance al contrato ni a los procesos recorta FR-127 y US7-2:
es la enmienda A8 (T97). Por qué: un pago que ya ocurrió no se rechaza por un vacío de la matriz y la nómina es
«todo o nada». FR: FR-088, FR-127, FR-128, D7, D8, US7-3. Origen:
DC-11, DC-12, DC-15 (pregunta C5).

**T57 · Contabilidad no cierra un mes que Cartera no cerró.**
Decisión: Cartera emite cuatro informativos con recibo y sin comprobante: `CarteraActivada`,
`CargaDeCarteraAplicada`, `CierreDeCarteraRealizado`, `CierreDeCarteraReabierto`. `ClosePeriodCommand` gana
`AcknowledgeLendingPending` y `AcknowledgeLendingLoad` y comprueba: (a) bloqueo sin reconocimiento
`Accounting.Period.LendingNotClosed` si hay activación con fecha ≤ fin de M y no hay cierre de Cartera de M posterior
a su última reapertura; (b) aviso `Accounting.Period.LendingPending` si hay hechos CAR pendientes, en lote o
rechazados con fecha contable en M; (c) aviso `Accounting.Period.LendingLoadWillExpire` si la última carga aplicada
sin activar tiene su día siguiente al corte dentro de M. Sin `CarteraActivada`, Contabilidad cierra como hoy. Por
qué: FR-146 sin que Contabilidad lea `LND_` y sin un puerto inverso. FR: FR-107, FR-142, FR-146, D12. Origen:
DC-14; PM proponía dos informativos (CH8; pregunta A2).

**T58 · Conciliación y cuadre de activación por conjuntos.**
Decisión: `IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync(corte, combinaciones)` delega en
`GetLendingAccountBalancesQuery`: resuelve cada combinación con la matriz vigente al corte, arma conjuntos
combinación↔cuenta por familia de rol (`ConjuntosDeCuentas`) y lee saldos sólo por `MovimientosContables` (apertura
incluida). `ConciliacionContableDeCartera` compara por **fecha contable** capital e intereses por modalidad,
sub-segmento y categoría, deterioro individual y general, anticipos, saldos a favor, suspenso, castigada y puentes, y
explica cada diferencia con los hechos pendientes, en lote, rechazados o que no pasan. La misma consulta sirve al
cuadre de activación con las combinaciones de la carga. Descuadre sin explicar → `Cartera.DescuadreContable`. Por
qué: si la contadora lleva varias categorías a una auxiliar, sólo se compara por conjunto; Cartera no conoce
cuentas. FR: FR-130, FR-141, FR-143, SC-006, SC-013. Origen: DC-16.

**T59 · Puertos de la frontera contable e indicadores de la entidad.**
Decisión: `IContabilidadParaCartera` con exactamente cinco métodos (§2.9) e `IDimensionesDeCartera` (Cartera →
Contabilidad: líneas con su modalidad, códigos de modalidad y sub-segmento, conceptos, clases de recaudador y
combinaciones en uso). Las pérdidas se derivan de cuentas (clases 4 a 7 del ejercicio sin el CI; saldos de 3510,
3610 y 3910); patrimonio técnico y solvencia los registra el contador en `ACC_RegulatoryIndicators` con fuente y
soporte; la consulta devuelve el último con corte ≤ fecha y, si es más viejo que
`Contabilidad.IndicadoresVigenciaMeses`, «no disponible». Sin indicadores vigentes: para la excepción de aportes del
arrastre y el descuento de aportes en el deterioro (FR-100, FR-103) Cartera aplica lo conservador de D19; para el
**límite de concentración** (FR-041), que bloquea, se **bloquean** el desembolso, la asignación de cupo y la novedad que
aumenten el endeudamiento (`Lending.Concentration.TechnicalCapitalUnavailable`, «patrimonio técnico no disponible») y
se alerta al contador (`Cartera.PatrimonioTecnicoNoDisponible`); usar el último registrado con advertencia es la
alternativa que se pregunta en C3. Por qué: FR-008 pide lista cerrada y verificada; el patrimonio técnico tiene
deducciones de norma que el plan de cuentas no da; FR-041 no admite que exceder el límite sólo se reporte. FR:
FR-008, FR-012, FR-041, FR-047, FR-100, FR-103, FR-127, FR-129, FR-130. Origen: DC-17, DC-18 (preguntas A1, C3).

### Seguridad y actas

**T60 · Catálogo `Lending.*` por acción, sembrado entero en K1.**
Decisión: la lista de §2.14 en `LendingPermissionCatalogSeeder` (inserta lo que falta, actualiza descripciones,
nunca en el DbMigrator). Datos del deudor = `Read`; crear, decidir, ejecutar y anular son acciones separadas; cuatro
códigos de decisión para niveles individuales. Se borra el bloque `Lending` de `IdentitySeedData` y su rol
`LoanOfficer`. `LosCodigosViewDeCarteraNoTienenDatosPersonales` fija qué `.View` existen. Por qué: `*.View` llega a
Operator, ReadOnly y Auditor; `Read` cierra eso sin tocar los roles integrados; sembrar entero evita que las
plantillas cambien de forma entre entregas. La bandeja de integración, el detalle de los hechos y los archivos
regulatorios llevan datos del deudor (el contenido completo del mensaje, la lista por crédito de cada parte, las
filas del SICSES y de centrales): son `Lending.Messages.Read` y `Lending.RegulatoryFiles.Read`; `.View` queda para
catálogos, parámetros, tableros y conteos sin persona. FR: FR-004, FR-005, FR-013, FR-138, SC-015. Origen: S1, R-09;
los demás informes usaron otros nombres (CH33).

**T61 · Roles como plantillas y un comando que los crea.**
Decisión: trece plantillas `cartera.*` en `PerfilesSugeridos` (§2.14) y `CreateSuggestedLendingRolesCommand`
(`Security.Roles.Create`), idempotente, como paso del asistente de activación; después cada rol es de la
cooperativa. No se tocan Auditor, Operator ni ReadOnly; a `CompanyAdmin` se le excluyen `Lending.Decisions.*` y
`Lending.Sessions.Vote` (administrar no es aprobar crédito). Por qué: `BuiltInRolesSeeder` reinserta vínculos en cada
arranque y no deja ajustar un rol integrado. FR: FR-004; supuesto «Usuarios». Origen: S2 (preguntas F2, F3).

**T62 · Alcance por oficina y línea, que falla cerrado.**
Decisión: `IAlcanceDeCartera` (`TodasLasOficinas`, `Oficinas`, `OficinaPorDefecto`, `TodasLasLineas`, `Lineas`),
memorizado por petición y total en segundo plano. Oficinas: `SEC_UserBranchAssignments` → `ADM_Branches` →
`COR_Branches` por `TenantBranchPublicId`, con el salto común extraído a `OficinasDelUsuario` (que
`UserBranchScope` reutiliza con su semántica abierta); en Cartera, sin filas no hay oficinas, salvo
`Lending.Scope.AllOffices`. Líneas: `LND_UserCreditLineScopes`, salvo `Lending.Scope.AllLines`. Toda consulta filtra
con `FiltroDeAlcanceDeCartera`; todo comando valida oficina y línea y responde el 404 genérico. Los puertos para otros
módulos no aplican el alcance de Cartera. En K1 la pantalla de Agencias deja elegir el vínculo agencia → oficina.
Leer oficinas, sucursales contables y el vínculo usuario–persona (también en T13 y T66) es una interfaz consumida que
la lista cerrada de FR-008 suma por la enmienda A1.
Por qué: SC-015 exige cero accesos fuera de oficina o línea; la oficina ya existe y la asigna seguridad. FR: FR-004,
FR-135, FR-138, SC-015. Origen: S3 (pregunta F4).

**T63 · Atribuciones por niveles.**
Decisión: matriz versionada (`LND_AttributionMatrices`, `…Levels`, `…Rules`) sellada en cada solicitud;
`EnrutadorDeAtribuciones` toma el nivel efectivo como el mayor entre techo de monto consolidado, reglas que casan,
consejo si hay vinculado (forzado) y nivel de excepción (puntaje bajo, alertas). La solicitud va sola a ese nivel; los
niveles no se acumulan. `NoExceptuables` rechaza siempre falta de capacidad de pago, de autorización, coincidencia en
listas y usura. Cada decisión es un hecho `LND_CreditDecisions` con huella; si el monto consolidado cambia, se
reenruta. `SEC_PermissionAmountLimits` se usa sólo para las atribuciones **operativas** (`Disbursements.Execute`,
`CreditLimits.Assign`, `ValidationFailures.Confirm`, `Payments.BackdateBeforeClose`) en `/api/lending/amount-limits`.
Por qué: FR-038 pide niveles con condiciones más allá del monto, cuatro resultados y que la solicitud vaya sola a su
nivel; los límites por rol no distinguen crédito, condonación ni novedad. FR: FR-037, FR-038, FR-040, FR-042,
FR-052, FR-058, FR-094. Origen: S4.

**T64 · Actas propias de Cartera; toda «acta» es un punto de sesión.**
Decisión: órganos (`LND_GoverningBodies`, con quórum y mayorías con vigencia, prefijo y consecutivo del acta) y
miembros (personas del maestro, con o sin voto, vigencia); sesiones (`LND_Sessions`: número, clase, modo, quórum,
estado, `ContentSha256` al cerrar, PDF emitido), asistencia, puntos (`Decision` o `Recommendation`, regla
`Majority` o `RelatedPartyQualified`, excluidos con motivo) y votos (`LND_SessionVotes`, H, uno por persona y
punto; registrados por la secretaría o por el miembro con su sesión o en presencia con passkey o TOTP). Lo calcula
`EvaluadorDeVotacion`: quórum sobre la composición vigente, mayoría ordinaria por órgano, vinculados con 5 ×
favorables ≥ 4 × composición (piso nacional), radicador e interesados excluidos
(`Lending.Session.VoterExcluded`). Cerrar el acta aplica cada punto por `IAsuntoDeActa`, escribe
`LND_CreditDecisions` y sella el SHA-256; un acta cerrada se corrige con otra sesión. **Todo lo que la spec dice
«con acta»** —decisiones colegiadas, vinculados, castigo, evaluación de cartera, política SARC, adopción o
desistimiento de la pérdida esperada, aceptación de diferencias (D17)— referencia un `SessionItem` por su PublicId;
no hay campos de texto «referencia del acta». Por qué: FR-039 y FR-040 exigen sesión, quórum, votos y acta inmutable;
nada de eso existe; `COR_Committees` es el catálogo heredado de comités culturales y `LND_Minutes` no tiene PublicId
ni votos. FR: FR-039, FR-040, FR-095, FR-102, FR-105, FR-141, D17. Origen: S5; R-06, R11, R13 y R15 proponían
otras formas (CH21; preguntas F7, F8).

**T65 · El motor `COR_Approval*` de la 012 no decide créditos.**
Decisión: Cartera no crea `COR_ApprovalRequests`. Reutiliza de la plataforma la identidad (`IActorActual`), los
límites de monto para lo operativo, la presencia (`IDesafiosDePresencia`), las alertas, las vigencias y la
auditoría. `PermisosLimitables` admite códigos por módulo; el motor no se toca. Conviene una vista «Mis pendientes»
(`/cartera/aprobaciones`) con las decisiones de Cartera. Por qué: el motor de la 012 está cosido a Inventario y su
semántica (umbral acumulativo, un aprobador por nivel, dos resultados) no es la de FR-038/FR-039; extenderlo es un
riesgo alto sobre lo productivo. FR: FR-038, FR-039, FR-149. Origen: S6 (pregunta F1).

**T66 · Segregación por persona.**
Decisión: regla fija —quien radica o solicita no decide ni vota sobre lo suyo— y pares configurables en
`Segregacion.Incompatibilidades` (acciones `Radicar`, `Analizar`, `Decidir`, `Votar`, `Desembolsar`,
`AnularOrdenDeGiro`, `ArmarLoteDeCastigo`, `AplicarCastigo`, `CargarMigracion`, `AceptarDiferencias`; activo por
defecto `Decidir × Desembolsar`). Registro único `LND_CaseParticipations` por `RegistroDeIntervenciones`, en la
misma transacción del acto; `ReglasDeSegregacion` compara por `SEC_Users.Id` y, si los dos la tienen, por persona
(`Lending.Segregation.Conflict`). Todo usuario que radique, decida, vote o desembolse debe tener
`SEC_Users.PersonId` (`Lending.Security.UserWithoutPerson`). Las carpetas de Cartera entran a
`LaSegregacionNoUsaUserIdDelToken`. Por qué: FR-005 compara por persona, aunque tenga varios usuarios. FR: FR-005,
FR-039, SC-015. Origen: S7 (preguntas F5, F6).

### Habeas data y auditoría

**T67 · Cartera en la cadena de auditoría.**
Decisión: `Lending`, `Compliance` y `NationalValues` entran a `AuditoriaEncadenada.Modulos` y a la retención de diez
años. El evento lleva la oficina (marcador `IConOficina`, o la oficina por defecto del usuario) y el dispositivo
(User-Agent más `X-Dispositivo` opcional de MAUI). `LoanTransaction` **no** lleva `[SinDiffDeAuditoria]`: es la
tabla del Principio XI y el atributo se aplica por clase, así que apagaría también el antes/después de desembolsos,
pagos y reversos (a diferencia de `KardexEntry` de la 012, que no está en ese principio). Lo llevan sólo
`LoanDailyPosition`, `MonthlyCloseLoan` y el detalle de las corridas (`LND_ProcessRunErrors`), con un evento por
cierre y por corrida (conteos, totales, huella). La **causación del proceso** no pasa por el `ChangeTracker`: `RegistroDeCartera`
la escribe en bloque dentro de la transacción de la tanda y registra un evento resumen por tanda y otro por corrida
(conteos, totales, huella); ésa es la única excepción al diff por fila y el plan la registra (T101). El evento de cada
`LoanOperation` lleva la distribución completa (T23, T68). `LendingAuditEmitter` registra las lecturas sensibles con
lista fija (liquidación con D, R y huella; centrales; reimpresión y descarga; exportaciones; actas y vinculados;
consulta de cupo de Comercial, cuyo `EventId` es el `EvidenceId`). Las reglas regulatorias fallan en el handler
(quedan como rechazo auditado) y **también se auditan los rechazos de forma**: como `ValidationBehavior` corre antes
de `AuditBehavior`, el mismo metadato del grupo `/api/lending` que audita el intento denegado (`Lending.Access.Denied`,
antes del 404) registra `Lending.Request.Rejected` (comando, campos y códigos, sin valores) cuando la respuesta es un
rechazo de validación; las consultas fuera de alcance emiten `Lending.Access.OutOfScope`. Verificación: la de la
plataforma, sin trabajo nocturno. Por qué: FR-003 («incluidas las fallidas y rechazadas») y SC-014 (100 %) sin
enmienda; un diff por cada transacción de la causación saturaría la bandeja de auditoría. FR: FR-003, FR-059,
FR-138, FR-149, SC-014, Principio X. Origen: S8, R4 (preguntas F9, F10).

**T68 · Auditoría del contrato síncrono: el intento fuera, el hecho dentro.**
Decisión: cuando un recaudador llama al contrato dentro de su transacción, Cartera escribe
`Lending.ObligationContract.Attempted` **antes**, por un contexto aparte (sobrevive a cualquier reversión), con la
clave y el recaudador, y `Lending.ObligationContract.Applied` (o `Reversed`, `CreditBalanceResolved`, `Rejected`)
**dentro** de la transacción, en `COR_AuditOutbox`, con la distribución completa por cuota y concepto (T23). Lo mismo
para la reserva y la liberación de número (`Lending.NumberReservation.Reserved` / `.Released`, dentro de la
transacción de Comercial, T22). El desenlace «revertido» **se deriva**: un intento sin hecho con la misma clave fue
revertido junto con el recaudador, y la consulta de auditoría lo muestra así (filtra por `OperationKey`). Como I-4
pide «registrar» el desenlace, derivarlo es la enmienda A11 (T97); si el dueño la rechaza, la tarea
`cartera.vigilancia` escribe un evento compensatorio `Lending.ObligationContract.RolledBack` por cada intento sin
hecho pasado su plazo. Por qué: I-4 pide el intento y su desenlace, y Cartera no puede saber al volver si el
recaudador confirmará; derivarlo no exige ganchos en el pipeline de otro módulo. FR: FR-003, FR-057, FR-088, I-4.
Origen: S9; DE-02 proponía `TareasTrasElCommit.AlTerminar` (CH18).

**T69 · Habeas data por finalidad en la plataforma; reclamos del reporte en Cartera.**
Decisión: (a) autorizaciones: columnas aditivas en `CMP_HabeasDataConsents` (`Purpose`, `ContactChannel`,
`EvidenceAttachmentPublicId`, `Origin`) y `CMP_HabeasDataPolicyVersions` (`Purpose`); `IAutorizacionDeDatos` gana
`VigenteAsync(persona, finalidad, canal?)` y `Agregar(...)` sin guardar, que la radicación escribe en su
transacción; una autorización migrada sin evidencia deja el crédito vivo y cobrable y bloquea sólo la consulta y el
reporte a centrales y **los mensajes de cobranza** (FR-006); las gestiones, las etapas y el cobro jurídico siguen. (b) Solicitudes del titular en `COR_DataSubjectRequests` (consulta, actualización, rectificación,
supresión, revocación) con plazos en días hábiles nacionales; `ReglaDeRevocacion` por finalidad pregunta el deber
legal a la consulta de obligaciones (FR-061) y deja constancia. (c) Reclamos y suplantación sobre el reporte en
`LND_CreditReportClaims`, con la marca «reclamo en trámite» y aceptación tácita por el ciclo diario (K4; la tabla
nace en K1 para lo migrado). (d) Rutas por PublicId; las de `{personId:int}` quedan en desuso. Por qué: un solo
registro de autorizaciones por persona (la 012 ya escribe ahí); el reclamo nace y muere en el archivo de centrales,
que es de Cartera. FR: FR-006, FR-036, FR-122, FR-123, FR-137, FR-139, SC-012. Origen: S10; R-06 proponía tablas
`LND_` (CH23; preguntas A1, F11).

**T70 · Listas restrictivas en la plataforma; el resultado en Cartera.**
Decisión: la carga provisional con vigencia es de plataforma (`COR_RestrictiveListLoads`,
`COR_RestrictiveListEntries`; `/api/compliance/restrictive-lists`; `Compliance.RestrictiveLists.View/Load`; plantilla
`compliance.restrictive-lists`); la consulta va por `IListasRestrictivas` con el canal `CARGA-PROPIA` (coincidencia
exacta por documento y posible por nombre con `CoincidenciaDeNombres`, umbral parametrizado). Cada consulta de
Cartera (radicación, venta, cupo) queda en `LND_ScreeningResults`; toda coincidencia bloquea y alerta
`Cartera.CoincidenciaEnListas`; sólo libera quien tenga `Compliance.RestrictiveLists.Release`, con motivo
(`ReleaseScreeningMatchCommand`, `/api/lending/screening-results/{id}/release`), y no puede ser quien radicó.
`LND_Blacklist` no se reutiliza. Por qué: el oficial no es un rol de Cartera y Comercial y Aportes reusarán la carga;
el resultado y su liberación son de la operación de Cartera. FR: FR-016, FR-036, FR-038, FR-049. Origen: F-18; S11
proponía todo en `LND_` (CH24; pregunta I12).

### Adaptadores y mensajería

**T71 · Adaptadores externos por puerto, con registro y canal simulado.**
Decisión: los puertos de §2.22, cada uno con `ChannelCode`, `Capacidades` y `ProbarAsync`, y un registro que
resuelve por el código **sellado** en la operación; adaptadores en `IngenIA365ERP.ExternalChannels`; sólo
`Program.cs` llama `AddExternalChannels`, después de `AddApplication`, que deja los defectos «sin canal». Nunca lanzan
por rechazo de negocio ni por transporte. Batería de conformidad por puerto. Por qué: pasar a canal real es
configuración y contrato, no código en Cartera (T40 de la 012, probado con la DIAN). FR: FR-036, FR-044, FR-122,
FR-137, FR-149. Origen: F-01.

**T72 · Configuración por cooperativa y credenciales fuera de la base.**
Decisión: `COR_ExternalChannelSettings` (`/api/core/external-channels`, verificación con `ProbarAsync`) guarda sólo
`CredentialKey`; la credencial vive en el Secret `erp-canales-externos` (§2.22), con ruta derivada sólo de la
cooperativa resuelta y de un código validado; `SIMULADO` y `CARGA-PROPIA` no piden archivo; circuito en memoria por
cooperativa y canal con alerta `Cartera.CanalExternoNoDisponible`. No se reutiliza `CredencialesEnArchivo` de la DIAN
(acoplado y con caché interna). Por qué: precedente de la DIAN; secretos fuera de la base y del repositorio. FR:
FR-003, FR-149, Principio IV. Origen: F-02 (pregunta I1).

**T73 · Guardia única del efecto legal de los canales simulados.**
Decisión: `GuardiaDeCanalesExternos` es el único sitio que decide; `CanalesExternosOptions.AmbienteDePruebas` sale de
`!IsProduction()` en `Program.cs`. En producción un resultado `SIMULADO` nunca perfecciona un pagaré, nunca cuenta
como constancia de aviso previo ni como evidencia de centrales para aprobar, ni envía un mensaje real
(`Lending.Adapter.SimulatedInProduction`, dice qué configurar). Configurar `SIMULADO` en producción se admite
**antes y después de activar**, como pide la spec («canal simulado hasta que la cooperativa tenga contrato»,
FR-044, FR-122): la guarda ya le quita todo efecto legal. Una cooperativa activada sin contratos —el caso de
COOFLOPAL— opera así: pagaré físico, aprobación en las líneas sin «exige consulta a centrales» (las que la exigen no
aprueban hasta tener canal real), mensajes al deudor por correo (los de SMS y WhatsApp quedan simulados y no
salen) y aviso previo con constancia manual. Fuera de producción todo documento lleva «AMBIENTE DE PRUEBAS – SIN
VALIDEZ». Por qué: una evidencia falsa con efecto legal expone a sanciones (Leyes 1266 y 2157), y prohibir el
simulado tras activar dejaría sin canal a quien todavía no contrata. FR: FR-036, FR-043, FR-044, FR-122, FR-123,
FR-142. Origen: F-03 (CH20; pregunta I4).

**T74 · Mensajería saliente a personas.**
Decisión: la plataforma ofrece `ICanalDeMensajeria` con `CanalDeCorreo`, SMS y WhatsApp; `NotificationChannels` no se
amplía y `COR_Notifications` no se usa para deudores. Cartera es dueña de `LND_DebtorMessages` (inmutable desde que
nace: plantilla y versión, cuerpo renderizado, canal, propósito, destinatario, copia del destino al enviar, crédito,
autorización, clave `{origen}:{entidad}:{propósito}:{fecha}`, canal sellado) y de `LND_DebtorMessageEvents` (sólo
inserción). Sólo deudor y codeudores obligados son destinatarios. Por qué: I-5 incluye los mensajes emitidos y
`COR_Notifications` es mutable y de usuarios. FR: FR-003, FR-075, FR-112, FR-115, FR-122, FR-149, I-1, I-5,
SC-011. Origen: F-04 (pregunta I14).

**T75 · Ventana legal de contacto y despacho por sondeo.**
Decisión: `VentanaLegalDeContacto` (pura) decide `Permitido`, `Diferido` o `Excluido` con el horario efectivo
(techo nacional ∩ restricción de la cooperativa, festivos nacionales, autorización por canal, fallecido, insolvencia,
cobro suspendido), un contacto por día y, tras un contacto directo, sólo el canal de ese contacto en la semana. Se
evalúa al programar y otra vez al despachar (`cartera.mensajes`); un envío manual fuera de ventana se rechaza
(`Lending.DebtorMessage.OutsideLegalWindow` con `nextAllowedAt`); uno programado se difiere con constancia. Por
propósito: `Collection` y `PriorNotice` aplican todo; `Informative` respeta horario y tope y no fija canal;
`DocumentDelivery` pedido por el titular queda exento de ventanas. Estado de entrega por sondeo (`cartera.entregas`);
**no hay webhooks** (serían rutas anónimas sin cooperativa). Por qué: SC-011 verificable sobre el registro. FR:
FR-014, FR-015, FR-098, FR-122, SC-011. Origen: F-05, F-06 (preguntas I2, I3).

**T76 · Plantillas de mensaje versionadas sobre `COR_NotificationTemplates`.**
Decisión: columnas aditivas (§2.6); cambiar una plantilla crea fila nueva y el mensaje sella `PublicId` y versión;
`RenderizadorDePlantillasDeMensaje` sustituye sólo variables de la lista blanca del propósito, escapa el HTML y avisa
el largo del SMS. Semillas «pendiente de validar»: aviso previo, recordatorio preventivo, mora administrativa,
prejurídica, no aplicado de pagaduría, paso a caja, abono a capital aplicado (D24), gasto de cobranza informado,
envío de documento. Por qué: la tabla existe vacía en cada cooperativa; los `.cshtml` no los edita la cooperativa. FR:
FR-007, FR-117, FR-121, FR-122, FR-123. Origen: F-07 (pregunta I15).

**T77 · Cobranza y aviso previo (K4).**
Decisión: gestiones inmutables (`LND_CollectionActions`; una corrección es una gestión nueva que anula la anterior),
promesas con estado derivado, acuerdos sobre lo vencido sin cambiar condiciones, etapas versionadas, historia de etapas
y asignaciones de sólo inserción, `RepartoDeGestores` puro, cobro jurídico con abogado persona del maestro. Un gasto de
cobranza se causa sólo después del `Sent`/`Delivered` de un mensaje `CollectionFeeNotice`. Aviso previo por episodio
(`LND_PriorNotices`): comunicaciones exigidas (una, o dos en días distintos bajo el umbral), constancia = evento
`Sent` del mensaje o registro manual con guía adjunta; fecha más temprana de reporte = constancia + días del parámetro
(piso 20). Sin aviso válido, el dato negativo no entra al archivo de centrales; uno reportado sin aviso se retira en el
siguiente. Por qué: SC-012 por episodio y con prueba. FR: FR-117–FR-121, FR-123, FR-124, FR-137, SC-012. Origen:
F-08, F-09.

### Archivos y documentos

**T78 · Archivos planos de Cartera con el motor común.**
Decisión: desembolsos por transferencia con `BankFileScope.LoanDisbursement` y `LoanDisbursementLines`
(beneficiario sellado en la orden, neto tras recogida, concepto = número del crédito); cuenta de origen entre las que
informa `IContabilidadParaCartera.CuentasBancariasAsync`, del banco del formato; `LND_DisbursementFiles` inmutable con
SHA-256; una orden va a lo sumo a un archivo no anulado; anular el archivo sólo antes del primer resultado de
Tesorería. K2: ámbitos `PayrollDeductionRequest` y `PayrollDeductionResult`, siempre genéricos y elegidos por
referencia desde la versión del convenio (`OutboundFormatId`, `ResultFormatId`); se marcan `SeleccionPorReferencia`
en `BankFileScope` y `SolapadoAsync` **deja de aplicarles** el cruce de vigencias —si no, sólo habría un formato
genérico vigente por ámbito y todas las pagadurías compartirían un layout de ida y uno de vuelta—; lo fija
`LosFormatosElegidosPorReferenciaNoCompitenPorVigencia`, y `/maestros/formatos-bancarios` los filtra por ámbito;
`FlatFileReader` puro, inverso del escritor;
`NovedadesDeLibranza`; `LND_PayrollDeductionFiles` inmutable; regenerar antes del envío crea versión y deja la anterior
`Superseded`; después del envío, `Lending.PayrollFile.AlreadySent`. Cartera no procesa la respuesta: consume
`ResultadoDeDescuentos`. Por qué: un solo motor (D-42 de la 010). FR: FR-045, FR-047, FR-108–FR-116, FR-149,
SC-016. Origen: F-10, F-11 (preguntas I8–I10).

**T79 · Layouts regulatorios embebidos con cotejo por campo.**
Decisión: motor genérico puro en `Domain/Common/Layouts`; `CatalogoDeLayoutsDeCartera` carga
`Application/Lending/Regulatory/Layouts/sicses-cartera-*.json` y `central-{CODIGO}-*.json` con vigencia y fuente; un
campo sin `verified` deja la advertencia `Lending.Regulatory.LayoutSinCotejar`. `LND_RegulatoryFiles` guarda
versiones (`Validated` sin archivo si hay bloqueantes, `Generated`, `Superseded`, `Uploaded` con radicado), la marca
`ModeloObligatorioNoDisponible` como advertencia y nunca dentro del archivo, lo excluido y el SHA-256. Archivos dorados
`.esperado.txt` byte a byte. La PILA no se toca. Por qué: layouts nacionales iguales para todos (I-3 sin réplica);
precedente PILA. FR: FR-014, FR-136, FR-137, SC-019, D28. Origen: F-12 (pregunta I7).

**T80 · Documentos emitidos con código de verificación.**
Decisión: `LND_IssuedDocuments` con número por clase (`NumeradorDeCartera`), copia de la identificación del titular
(I-1), corte o período, `InformationCutoff` (R), código de 16 caracteres Crockford base32 (`XXXX-XXXX-XXXX-XXXX`) con
QR a `/cartera/verificar?codigo=`, SHA-256, PDF guardado (adjunto no borrable), `FinalityUntil` (D23) y si cubre
codeudas; eventos de sólo inserción; estado **calculado** (`Voided` si hay anulación; `SubjectToFinality` mientras hoy
< `FinalityUntil`; `Valid` en otro caso). `VigenciaDeDocumentos` corre en el comando que reversa y anula todo documento
cuyo `InformationCutoff` sea igual o posterior al registro de lo reversado. Emitir es un paso
(`IssueLendingDocumentCommand`, descarga o correo); reimprimir es descargar el PDF guardado. El PDF del simulador no es
documento emitido: no lleva número ni código. Verificación sólo por usuarios de la cooperativa (D21). Propuesta: la
infraestructura y tres clases (plan de pagos, paz y salvo, certificado de saldo) en K1; extracto y certificado
tributario en K5. Por qué: FR-132 sin claves que rotar; la reimpresión idéntica. FR: FR-013, FR-034, FR-082, FR-131,
FR-132, D21, D23. Origen: F-13, F-14 (preguntas I5, I6).

**T81 · Adjuntos de Cartera por dueño.**
Decisión: los tipos de §2.24 en `AdjuntosDeModulo`, único sitio que decide lectura, subida y borrado; los de módulo no
se borran; los de subida quedan bloqueados cuando su dueño se decide. Por qué: las evidencias regulatorias no se
borran (Principio XI, FR-003). FR: FR-006, FR-035, FR-036, FR-043, FR-044, FR-109, FR-124. Origen: F-19, S10.

**T82 · Plantillas de importación sobre el ejecutor común.**
Decisión: `CatalogoDePlantillasDeCartera` con las claves de §2.23, registro en `GET /api/lending/templates`, pantalla
`/cartera/plantillas`; revisión previa, todo o nada, errores por fila y columna; ninguna crea personas.
`RutasDePlantilla.MapPlantilla` se generaliza (definición y permiso de datos personales por parámetro) y se corren las
pruebas de las 16 plantillas de la 012. Por qué: el ejecutor ya da lo que piden FR-033 y FR-139. FR: FR-033, FR-040,
FR-139, FR-140, FR-142, D19. Origen: F-16 (CH30, CH46).

### Migración y activación

**T83 · Carga de migración por partes; aplicar sólo guarda la carga.**
Decisión: `LND_MigrationLoads` (corte; `Draft`, `Applied`, `Replaced`, `Activated`; revisión, cuadre y conciliación
de ventas) con `LND_MigrationLoadParts` y la preparación por parte (§2.3), cada parte una plantilla de una hoja
(admite .csv). `ApplyMigrationLoadCommand` valida entre partes (el plan pendiente suma el capital, referencias
cruzadas, historial de los segmentos cubiertos), todo o nada, y calcula el cuadre (FR-141) y la conciliación de ventas
(FR-143) con `LND_MigrationLoadDifferences` y su aceptación por punto de acta (D17). Sólo una carga `Applied` se
activa; una nueva deja la anterior `Replaced` (ensayo). Lo migrado de K2–K4 (pagaduría, etapa de cobranza, avisos,
reclamos, reestructuraciones) se conserva como dato desde K1. Los créditos migrados conservan el capital por cuota
cargado y su interés lo causa el motor desde el corte. **Ventas de servicios a asociados confirmadas antes de
`CreditWithoutLending`** (T95): ya emitieron `VentaACreditoRegistrada` v1 hacia Lending (I1–I6 están en producción
desde el 2026-10-01) y, como no son cartera (D29), en la conciliación de FR-143 se marcan desde la lista, con motivo y
atribución, como «aceptadas fuera de Cartera (D29)», una clase propia de `LND_MigrationLoadDifferences`: no bloquean la activación como «no migradas con saldo», se aceptan venta por venta con motivo y un
punto de acta (D17) y al activar responden `ObligacionRechazada` no reprocesable, igual que las aceptadas sin migrar
(pregunta G11). Por qué: una sola hoja de 50.000 créditos con sus cuotas pasa
los topes del ejecutor; FR-142 dice que aplicar sólo guarda la carga. FR: FR-139–FR-143, SC-013. Origen: F-17
(preguntas D5, I11).

**T84 · Activación.**
Decisión: `ActivateLendingCommand` registra en `LND_Activations` la fecha (día siguiente al corte de la carga
aplicada, dentro del plazo de FR-012 y con el mes abierto en Contabilidad), recalcula cuadre y conciliación (si
difieren de los aceptados, se vuelve a aceptar), hace nacer desde la carga créditos, transacciones de saldo inicial,
cierre base, cupos, bloqueos, autorizaciones, avisos, reclamos y vinculados, revalida los cupos otorgados a mano
(`Cartera.CupoPorRevalidar`), marca como no aplicadas, con origen `Activation` y motivo «Cartera sin activar», en
`LND_InstallmentNonDeductions` las cuotas de nómina propia vencidas en el intervalo, aplica la plantilla de cobros
provisionales, activa el destino `Lending` en el registro (T16) y emite `CarteraActivada`, todo en el mismo guardado.
El cierre base se fecha al corte, que no tiene que ser fin de mes (T49). En producción exige: un recaudador `Receipts`
y un destino `Treasury` reales en el registro con versiones vigentes (no los dobles), K6a desplegada si la cooperativa
es obligada, Anexo 1 vigente y usura de las modalidades de las líneas activas, roles de Cartera creados, toda oficina
con créditos vinculada a una sucursal contable y la regla `RecaudoPorAplicar` con `CollectorClass = Payroll` resuelta
en la matriz (por `IContabilidadParaCartera`), porque desde T53 `DESC_CARTERA` contabiliza. **En todo ambiente donde
K6a esté desplegada** exige además, para una cooperativa obligada, los parámetros de FR-106 vigentes a la fecha del
primer cierre que los use y el historial de mora completo a esa fecha (FR-105) de los créditos migrados de los
segmentos cubiertos (lo cubre la e2e de activación de K6a). Si falta algo, se niega y lista lo que falta. Muestra lo
que Comercial debe ajustar en sus parámetros y, como aviso, si `DESC_CARTERA` tiene cuentas: la pantalla lo pregunta a
Nómina por `GET /api/payroll/concept-definitions/{code}/accounts`, porque el comando no lee `PAY_` (FR-008); sin
cuentas, la primera nómina con descuentos de Cartera no se aprueba (`Payroll.Settlement.ConceptAccountsMissing`).
Por qué: FR-142 es literal. FR: FR-089, FR-105, FR-106, FR-112, FR-141, FR-142, FR-143, D17, US8-10. Origen: PM-07,
DC-07, DC-14, DE-08, R15, R-06, S2, S3.

**T85 · Cartera desplegada y sin activar.**
Decisión: las implementaciones reales de los puertos responden por cooperativa: consulta de obligaciones sin
obligaciones exigibles y con «migrada, sin activar» para los deudores de la carga vigente; contrato con listar y
simular vacíos y aplicar, reversar y resolver rechazados con `Lending.NotActivated`; consultas, reserva y liberación de
Comercial con `NotEnabled` (Comercial lo trata como crédito provisional); mensajes de Comercial acumulados. Nómina
aprueba sin descuentos de Cartera y, si hay deudores de una carga aplicada, exige reconocer «Cartera por activar». Lo
cubre `CarteraDesplegadaSinActivarTests`. Por qué: FR-142 lo exige con o sin Recaudo y Tesorería. FR: FR-061,
FR-083, FR-142, FR-146–FR-148. Origen: R-03, DE-10, DE-12, DE-15.

### Retiro de lo heredado

**T86 · Clasificación de las 93 tablas: se retiran 55, se conservan 38.**
Decisión: la lista de R-01 se publica en `data-model.md` (tabla, origen, grupo, decisión, FK) y la confirma el
diagnóstico por ambiente. Se retiran también `LND_PeriodicityParameters`, `LND_DeductionValues`, `LND_InterestRates`,
`LND_OnlineQueries` y `LND_RecreationApplications`. Las compartidas (`LND_Minutes`, `LND_MinuteAttendees`,
`LND_Blacklist`, `LND_TransactionCodes`, `LND_Documents`) **se conservan**: no son de crédito ni tienen FK a lo
retirado; las decide su feature (Ahorros, SARLAFT). Si una tabla de crédito tiene datos de ahorro o aportes, el retiro
se detiene. Por qué: C6 y D10 al pie de la letra («sólo las de crédito»). FR: FR-144, C6, D10. Origen: R-01; S
proponía retirar las compartidas vacías (CH22; preguntas H2, H3).

**T87 · Migración destructiva con guarda y aprobación por base.**
Decisión: `RetiroDeLaCarteraHeredada` (§2.26) con guarda que cuenta filas (incluidas las dadas de baja) y se niega
nombrando tabla y cantidad salvo la fila `LND.RetiroHeredado.Aprobado`; suelta la FK de `LND_SiplaGroupParameters`
(conservada, su columna queda como dato) y la de `PAY_SettlementDeductions`; retira `LoanPortfolioId` de
`PAY_SettlementDeductions` y de `PAY_Novelties` (la copia del PublicId y del número a `ObligationPublicId` y
`ObligationNumber` de **las dos** tablas ya la hizo `NominaRecaudaPorElContrato`, así no se pierde historia; si en
algún ambiente esa copia no encuentra el crédito heredado, el runbook lo declara como pérdida, Principio XII); los
`ACC_Documents` con origen CAR no se tocan (son inmutables). Diagnóstico
`specs/013-cartera-financiera/diagnostico-cartera-heredada.sql` (sólo lectura, dos motores) en DEV, QA y PDN, en cada
base de cooperativa y en `ingenia365erp`, antes de fusionar y el día de cada promoción (PDN con autorización expresa);
runbook `docs/operaciones/cartera-retiro-heredado.md`; respaldo `<base>-AAAAMMDD-pre-f013.dump` (también de la base
administrativa, por los valores nacionales); e2e `RetiroDeLaCarteraHeredadaTests`. La base administrativa no tiene
`LND_`: el retiro no la toca. Por qué: Principio XII y el molde de la 012. FR: FR-144. Origen: R-02, R-04, R-05,
DE-09 (CH37; preguntas H1, H4, H7).

**T88 · Orden de los commits del retiro.**
Decisión: (1) puertos `IConsultaDeObligaciones` e `IContratoDeObligaciones` con sus implementaciones «sin activar» y
los consumidores migrados a ellos (Personas, definitiva y ordinaria de Nómina, `AccountReferenceFinder`, retiro de
asociado, extractos y portafolio sin crédito), con `NominaRecaudaPorElContrato` y `ComercioConCartera`; (2) mudanza a
`Savings`; (3) retiro de **todo** lo heredado de crédito: código, rutas, pantallas, menú, temas del manual,
tarjetas del centro de reportes, PDF (`LoanStatementReport`, `PortfolioAgingReport`), permisos legados y el proyecto
Legacy (CH50: las pantallas no sobreviven a sus rutas); (4) `RetiroDeLaCarteraHeredada` sobre el modelo
intermedio, en commit propio; (5) después, `ValoresNacionales`, `PlataformaParaCartera`, `CarteraNucleo`,
`CarteraRiesgo`, `IntegracionContableDeCartera` y las implementaciones reales. Al cerrar (1), ningún archivo fuera de
la cartera heredada nombra `LND_` heredadas ni sus `DbSet`, y `Ventas/CreditoProvisionalTests` sigue verde. Por qué:
el modelo intermedio compila y es correcto en producción; no se reutilizan nombres en un mismo scaffold (T4 de la 012).
FR: FR-144, FR-147, FR-148. Origen: R-03, R-07, R-08, DE (pasos).

**T89 · Permisos, adaptador y rutas heredadas.**
Decisión: se borra el bloque `Lending` de `IdentitySeedData` y su rol; se retira el proyecto `IngenIA365ERP.Legacy`
entero (su única implementación es `LendingLegacyAdapter`, código muerto), su línea en `IngenIA365ERP.slnx` y la
exclusión de `RepoPath.cs`. Las páginas, enlaces, temas del manual y tarjetas del centro de reportes de crédito se
retiran en el paso 3 de T88, con sus rutas; la rama acepta un tramo sin pantallas de crédito hasta que las nuevas se
publican, porque las heredadas no forman un ciclo utilizable ni tienen datos (diagnóstico de T87; CH50). Usuarios
con `ui.pagina-inicio = /cartera/recaudos` se revisan.
Por qué: dos fuentes del mismo prefijo de permisos y código sin consumidores. FR: FR-144, Principio III. Origen: R-08,
R-09, F-20 (pregunta H6).

### Enmiendas a otros módulos

**T90 · Cinco puertos que ofrece Cartera, con métodos exactos.**
Decisión: §2.9: `IConsultasDeCartera` (2), `IReservasDeCartera` (2), `IConsultaDeObligaciones` (2),
`IContratoDeObligaciones` (5 + `VersionDelContrato`), con DTO y enums en `Application.Common.Integration.Lending`, y
`IDimensionesDeCartera` para Contabilidad (T59; interfaz nueva de la lista cerrada, enmienda A1);
`LaCarteraSoloSeTocaPorSusInterfaces` fija los métodos de los cinco;
stubs en ese mismo espacio («no habilitada» para Comercial, «sin activar» para obligaciones) y reales en
`Application/Lending/Integration`, que los reemplazan en `DependencyInjection`. Los mensajes van en `Contracts/Lending`.
La prueba de la 012 se amplía con los dos métodos de `IReservasDeCartera`. Por qué: molde T31 de la 012; separar
reservas de consultas porque una consulta no cambia estado. FR: FR-008, FR-053, FR-057, FR-059, FR-061, FR-083,
FR-086, FR-145, FR-147, FR-148, SC-018. Origen: DE-01, PM-10, R-03 (CH11, CH43).

**T91 · El contrato corre síncrono en la unidad de trabajo del recaudador.**
Decisión: `ContratoDeObligacionesDeCartera` usa el `IApplicationDbContext` del ámbito de quien llama; `AplicarAsync`,
`ReversarAsync` y `ResolverSaldoAFavorAsync` exigen transacción en curso (`TransaccionExplicita.HayEnCurso`; si no,
`Lending.Contract.TransactionRequired`); nunca abren ni confirman la suya; pueden guardar dentro (decide la de afuera);
bloquean con `ICerrojoDeCartera` en orden de Id; se llaman directo, nunca por `ISender` desde un comando reintentable.
Como no pasan por el pipeline de MediatR, **cada método de escritura** (`AplicarAsync`, `ReversarAsync`,
`ResolverSaldoAFavorAsync`, y `ReservarAsync`/`LiberarAsync` de T22) cumple dos cosas que el pipeline daría: ejecuta
explícitamente su validador FluentValidation, el mismo que tendría como comando, y escribe su evento de auditoría
(T68); lo fija `LosPuertosDeCarteraValidanYAuditanSusEscrituras`, y la desviación de los Principios III y X la
registra el plan (T101). `AplicarAsync` recibe un lote (una respuesta por ítem) para los recaudadores sin interacción.
El recaudador debe estar en el registro (`Lending.Contract.CollectorNotRegistered`), salvo la recogida de créditos
propios (`OwnLoanPayoff`), que es la propia Cartera dentro de su desembolso y queda exenta de esa verificación. El
reverso de un pago de la clase `Payroll` sólo lo admite por `ReversarAsync` del mismo recaudador (T31). Por qué: I-4
y FR-086; la lección de `TransaccionDeLiquidacion`: un reintento anidado vacía el ChangeTracker, y el precedente
`RecaudoDeCredito` ya se llama directo. FR: FR-003, FR-009, FR-046, FR-079, FR-080, FR-086–FR-088, I-4. Origen:
DE-02, M13, S9 (CH18, CH19).

**T92 · Nómina recauda por el contrato.**
Decisión: recaudador `Payroll` (semilla del registro, contrato v1), forma sin interacción, clase de medio
`OwnPayrollDeduction`, pagador = el empleado deudor, recibo = el NM o la liquidación. **Definitiva**:
`PropuestaDeDescuentosDeCartera` propone por `IConsultaDeObligaciones` (todas las vivas como deudor, castigadas
incluidas; `SaldoTotal` → total para cancelar, `SoloCuotasCausadas` → exigible; desglose JSON con las mismas claves;
`DeudaPropuesta` cambia `LoanPortfolioId` por `ObligationPublicId` y `ObligationNumber`); `SettlementRunWorkflow`
gana el gancho `antesDeContabilizar`, donde se vuelve a consultar (`Payroll.Settlement.LoanProposalOutdated`) y se
aplica el lote todo o nada (`Payroll.Settlement.LoanAmountsChanged` si hay rechazo o excedente); fecha valor = la de
aprobación; reversar reversa por el contrato (`CarteraReversalPublicId`); el ajuste muestra el saldo exacto por
`SimularAsync`. **Ordinaria**: `CuotasDeNominaAsync` alimenta novedades `LoanDeduction`, una por obligación, con
`PAY_Novelties.ObligationPublicId` y `ObligationNumber`; el motor ya no salta el tope (la cuota va primera entre las
autorizadas; lo que no cabe queda no descontado con motivo); `PAY_PayrollLoanDeductions` guarda esperado, descontado,
no descontado y motivo; `ApprovePayrollRunCommand` pasa a `TransaccionExplicita` y aplica el lote antes del NM, con
los `NoDescontado` en la misma llamada, que Cartera guarda en `LND_InstallmentNonDeductions` (origen `Contract`): es
la marca del crédito hasta K2, la cuota sigue exigible y se alerta `Cartera.CuotaNoDescontadaPorNomina` (FR-112); un
rechazo o excedente no bloquea (la línea baja a lo aplicado, el neto sube); fecha valor = fin del período (exenta del
límite D4, enmienda A9); reversar la ordinaria reversa por el contrato. Un usuario de Cartera no reversa estos pagos
(`Lending.Reversal.OwnedByCollector`, T31). Ambas aprobaciones ganan
`AcknowledgeLendingPendingActivation` (columnas en `PAY_PayrollRuns`). Se retiran `RecaudoDeCredito`,
`ProcessPaymentCommand`, `ProcessPayrollDeductionCommand`, `PayrollDeductionEntries` y `FormaDePagoNomina`. Por qué:
C4, FR-088 y FR-112 literales. FR: FR-061, FR-071, FR-073, FR-080, FR-087, FR-088, FR-112, FR-115, FR-142, FR-147.
Origen: DE-03–DE-07, DE-10 (CH17; preguntas D15, G2–G5, G7, G10).

**T93 · Salario mínimo, UVT y festivos en Nómina: proyección de la réplica.**
Decisión (K1): Nómina implementa `EfectoDeReplicacionEnNomina`: cuando la réplica trae `SMMLV`, `UVT` o `FESTIVO`,
escribe su propia proyección en la misma transacción (fila de `PAY_LegalParameters` con fuente «Valor nacional
{SourcePublicId}»; fila de `PAY_Holidays` con `HolidayOrigin.National`), con cuatro reglas:
1. **Adopta lo que ya existe.** Cada cooperativa ya tiene esas filas (`PayrollLegalParametersSeeder`, Order 71,
   siembra el SMMLV y la UVT; `HolidaysSeeder`, Order 74, los festivos 2026–2028 con `FestivosLey51`), y los únicos
   `UK_PAY_LegalParameters_Code_ValidFrom` y `UK_PAY_Holidays_Date` harían fallar la réplica entera y el alta de una
   cooperativa nueva. Si ya hay fila con el mismo `Code`/`ValidFrom` (o la misma fecha) y el mismo valor, se marca como
   proyección (fuente «Valor nacional {SourcePublicId}», origen `National`) en vez de insertarla; si el valor difiere,
   se conserva la de Nómina y se alerta `Nomina.ValorNacionalDistinto` para que el contador decida.
2. **Las semillas dejan de sembrarlo.** Salen el SMMLV y la UVT de `PayrollLegalParametersSeeder` y el generador
   Ley 51 de `HolidaysSeeder` (que queda vacío y se retira); en el alta de una cooperativa nueva los escribe
   `NationalValuesReplicaSeeder` (Order 88, después de 71 y 74). `FestivosLey51` pasa al seeder de la central.
3. **Cierra la vigencia anterior** como `AddLegalParameterVersionCommand` (`ValidTo` = víspera de la nueva).
4. **Sólo la réplica escribe**: `AddLegalParameterVersionCommand` rechaza `SMMLV` y `UVT`
   (`Payroll.LegalParameter.NationalValue`) y los comandos de festivos no editan los `National`.

Los festivos `Manual` siguen siendo locales de Nómina y Cartera no los usa. `LectorDeUvt` sigue siendo el único lector
de la UVT (`LaUvtSeLeeEnUnSoloSitio`) y lee la proyección. Lo vigilan
`SoloLaReplicaEscribeLosValoresNacionalesDeNomina` y la e2e `ReplicaSobreCooperativaSembradaTests`. Leer
directamente de los valores nacionales desde los cargadores (`ValoresNacionalesParaNomina`) queda como mejora
posterior. FR-147 y C8 dicen que Nómina «lee» los valores nacionales para que no haya dos fuentes: conservar una
proyección que sólo escribe la réplica es la enmienda A7 (T97). Por qué: una sola fuente (la central) sin tocar los
13 lectores de `PAY_LegalParameters` ni los motores en producción; cada tabla la escribe su dueño. FR: FR-015,
FR-147, FR-149, C8. Origen: R6; DE-18 proponía el lector directo (CH14, CH15; preguntas A7, B6, E5, G1).

**T94 · Personas y retiro de asociado por la consulta de obligaciones.**
Decisión: `DeletePersonCommand` bloquea (`Person.HasActiveLoans`, mensaje nuevo) si la persona tiene obligaciones
vivas como deudor **o codeudor**, o está «migrada, sin activar»; `GetPersonDetailQuery` arma `PortfolioSummaryDto` (con
`LendingStatus`) por la misma consulta; los ahorros siguen en `SavingsAccounts`. `WithdrawalQueries` y
`ProcessWithdrawalCommand` (consumidor «Aportes, en el retiro» de FR-061) pasan a la consulta en K1. Se agrega la
prueba que hoy falta de la rama de borrado. Por qué: FR-148 y FR-061; si no se adaptan, no compilan al retirar.
FR: FR-061, FR-142, FR-144, FR-148. Origen: DE-11 (pregunta A5).

**T95 · Comercial con Cartera (E-02).**
Decisión: antes de activar, las consultas reales responden `NotEnabled` y rige el crédito provisional; activada,
`CreditoEnLaVenta` aplica la respuesta (bloqueado o en mora no permitida → `Inventory.Credit.LendingBlocked`; cupo
insuficiente → `Inventory.Credit.QuotaExceeded`; sin respuesta en `Cartera.ConsultaSegundos` → la política
`Cartera.PoliticaSinRespuesta`; validada → origen `Validated`, sin aprobación provisional, `ConsultationEvidence` =
`EvidenceId`) y `EvaluateSaleCreditQuery` devuelve el origen real. Reserva por T22; v2 y respuestas por T18;
`DocumentPayment.CreditLineCode` (la línea elegida). `Cartera.CuentaPorCobrarRegistradaPor` pasa su defecto a
«Cartera» (D16), forzado a «Contabilidad» mientras Comercial no fije su fecha de habilitación. **D29**:
`PaymentMeans.CreditWithoutLending` (sólo clases de crédito, inmutable con pagos) copiado a
`DocumentPayment.CreditWithoutLending`: no consulta ni reserva, sella «Contabilidad» y no emite a Cartera; la
conciliación de FR-143 no ve las ventas que llevan la marca, y las confirmadas antes de existir el dato se tratan como
«aceptadas fuera de Cartera (D29)» (T83). **Construir `CreditWithoutLending` aquí es una enmienda de alcance**: la spec
(Alcance; Dependencias · Comercial · servicios a asociados) lo deja como ajuste de Comercial fuera de esta feature. Se
somete al dueño como A6 (T97); si la rechaza, sale de `ComercioConCartera` y queda como **precondición de
activación**: un ajuste de la 012 del que depende la conciliación de FR-143 de toda cooperativa que venda servicios a
crédito. Se enmienda FR-062 de la 012 (Comercial guarda número y estado, nunca saldos) y se cierran sus tareas IC
(T661–T667, rama Cartera de T886, T1026–T1027). Por qué: FR-145 y D29 (sin el dato, los servicios a crédito nacerían
como obligación o bloquearían la activación). FR: FR-049–FR-060, FR-142, FR-143, FR-145, D16, D26, D29. Origen:
DE-12–DE-16 (preguntas A6, G6, G8, G9, G11).

**T96 · Contabilidad: enmienda de la 009.**
Decisión: el contrato de la 009 deja de asignar a Cartera comprobantes por llamada directa (sus filas de
`contracts/contabilizacion.md` remiten a esta enmienda); se retiran `DisburseLoanCommand`, `RecaudoDeCredito`,
`AccrueInterestCommand`, `ClassifyPortfolioCommand` y `ProcessPayrollDeductionCommand` (los tres últimos con el
marcador «E3 (feature 009)»), las cuentas como texto de `LND_CreditLineParameters`, `LND_PortfolioAccounts` y
`LND_ProvisionParameters` (con sus tablas) y la rama de `AccountReferenceFinder` que las lee. Se conservan
`LiquidateSavingsInterestCommand` (de `Application/Lending/SavingsInterest`, se muda a `Savings`) y
`LiquidateCDTInterestCommand`, que vive en `Application/CDT/InterestLiquidation` y queda sin cambios. Por qué: C3 y FR-146: un
solo camino al libro. FR: FR-144, FR-146, C3. Origen: DC-19.

**T97 · Enmiendas a la especificación y a otras features que esta armonización propone.**
Decisión: se someten al dueño (§5, grupo A) y, aprobadas, se escriben en la spec antes de `contracts`: (1) lista
cerrada de interfaces: «Dimensiones de Cartera para Contabilidad» (Ofrece), «Perfil regulatorio de la cooperativa»
(Consume, plataforma), «Autorizaciones del titular» (Consume, Cumplimiento), «Oficinas, sucursales contables y
usuario–persona» (Consume, Seguridad y plataforma: T13, T62, T66) y la consulta de obligaciones también para
Cumplimiento al evaluar una revocación (A1); (2) FR-125 suma los cuatro informativos de T57 (A2); (3) FR-107 y FR-126
suman la reclasificación completa y la dimensión «reestructurado» (T48, T52; C4); (4) la fila XII de la spec deja de
decir que el retiro toca la base administrativa (A4); (5) Alcance y Dependencias · Comercial · servicios a asociados:
el dato `CreditWithoutLending` entra en E-02 de esta feature (T95, A6); (6) FR-147 y C8: Nómina conserva una
proyección del SMMLV, la UVT y los festivos que sólo escribe la réplica, en vez de leer la plataforma (T93, A7);
(7) FR-127 y US7-2: «Bloquear» por falta de regla sólo aplica a las operaciones de pantalla de Cartera (T56, A8);
(8) FR-012 y D4: los pagos sin interacción cuya fecha valor fija la spec —Nómina y, desde K2, la pagaduría con
referencia (FR-114)— quedan exentos del límite de días y de la atribución antes de un cierre (T92, A9); (9) FR-144: lo conservado cambia de espacio de nombres y de
ruta API, no de tablas ni de rutas de pantalla (T3, T99, A10); (10) I-4: el desenlace «revertido» del contrato se
deriva de un intento sin hecho en vez de escribirse (T68, A11). A otras features: 012 (regla «el mensaje lo origina la persona» con la excepción
de T11; FR-062 por T95), 010 (D-08 por T53), 009 (T96). Por qué: lo exige el desacoplamiento verificado (FR-008) y no
se puede construir sin ellas; las (5) a (10) son lecturas que este documento hacía sin someterlas. FR: FR-008,
FR-012, FR-107, FR-125, FR-126, FR-127, FR-144–FR-149, I-4. Origen: DC P1, DC P2, DC P4, R15, S10, R-xx, DE-14, DE-08,
DE-16, PM-02, R6.

### Pantallas e informes

**T98 · Pantallas en carpeta nueva y menú reordenado.**
Decisión: `Pages/Cartera` (en `ModulosMigrados`) con las rutas de §2.16; grupo de menú «Cartera» con `PermissionGate`
por enlace y grupo «Ahorros y aportes» con lo conservado; ninguna pantalla dice el nombre del sistema anterior. Las
rutas de crédito heredadas (`/cartera/creditos`, `/cartera/solicitudes`) se reutilizan sólo después del paso 3 de
T88, que retira las páginas viejas (CH50; Blazor falla al ejecutar ante rutas ambiguas). `TodoEnlaceDelMenuTieneSuPagina`,
`ManualCatalogoTests` y `LasPantallasDeCarteraEstanEnElMenu` (página, ruta y permiso). Por qué: FR-144 y la lección de
la 012 (§2.11). FR: FR-004, FR-013, FR-144. Origen: F-20, R-08 (CH44).

**T99 · Centro de informes y tablero.**
Decisión: `LendingReportsEndpoints` se reescribe como el de Inventario: registro `GET /api/reports/lending`,
`VistaDeInformeDeCartera` como metadato, `MapVistaDeCartera` con `Lending.Reports.View`,
`RequirePermissionWhenExporting(Lending.Reports.Export)` y `Lending.Reports.ExportPersonalData`, rango de hasta cinco
años y una auditoría por exportación; vistas de §2.15; todo filtra por `IAlcanceDeCartera`. Los dos informes
conservados que no son de crédito (`person-portfolio` sin créditos y `savings-statement`) salen de ese grupo a
`SavingsReportsEndpoints` (`/api/reports/savings/…`) y `PersonPortfolioReportQuery` a `Application/Savings`, así
`ElEspacioLendingEsSoloCartera` no tiene excepciones (enmienda A10). Tablero
`GET /api/lending/dashboard` sobre las posiciones diarias dentro del alcance. Por qué: molde de la 012 con un solo punto
de exportación auditada. FR: FR-004, FR-133–FR-135, FR-138. Origen: F-15 (CH10, CH44).

**T100 · Reglas de toda pantalla de Cartera.**
Decisión: `<PermissionGate Required="Lending.…" MostrarAviso="true">`, `IndicadorDeCarga` con `_carga.Iniciar()` en
toda zona que carga o guarda, `PersonSearchPicker` para buscar y `PersonaDialog` para el alta (FR-035), simulador
visible en toda pantalla de originación, mensajes en español claro sin códigos técnicos, ningún color literal ni
`<style>` propio. Las operaciones frecuentes de FR-013 se verifican en tres pasos o menos. Por qué: Principio UI,
FR-013, Principio V. FR: FR-013, FR-035. Origen: F-20.

### Revisión (agregadas el 2026-10-05)

**T101 · Desviaciones de la constitución que el plan registra en `Complexity Tracking`.**
Decisión: `plan.md` registra, cada una con su motivo y su mitigación:

| Desviación | Principio o convención | Motivo | Mitigación |
|---|---|---|---|
| Los métodos de escritura de `IContratoDeObligaciones` e `IReservasDeCartera` se llaman directo, fuera del pipeline de MediatR | III (los behaviors no se saltan), X (nada sensible fuera del pipeline) | Corren en la unidad de trabajo del recaudador o de Comercial (I-4); por `ISender` un reintento anidado vacía el `ChangeTracker` (`TransaccionDeLiquidacion`); precedente `RecaudoDeCredito` | Cada método ejecuta su validador y escribe su evento (T22, T68, T91); lo fija `LosPuertosDeCarteraValidanYAuditanSusEscrituras` |
| La causación del proceso se audita con un evento resumen por tanda y por corrida, no con un diff por fila | X (antes/después) | ~150.000 filas al día con 50.000 créditos saturarían la auditoría | Huella, conteos y totales; el libro es inmutable y `VerifyLendingLedgerIntegrityQuery` lo compara con la reconstrucción (T67) |
| Motores, servicios, puertos, DTO de puerto, mensajes, parámetros y pruebas en español | «Identificadores en inglés» | Precedente de la 012; un solo criterio en el código vivo | Regla escrita en §2.1; o enmienda constitucional por Governance |
| DTO de entrada de los puertos sin sufijo `Dto` | «DTOs: sufijo `Dto`» | Precedente de la 012 (`ConsultaCrediticia`, `SolicitudDeEmision`) | Los de salida sí lo llevan (§2.1) |

Por qué: la constitución pide documentar en `Complexity Tracking` toda violación con su justificación, o el plan se
rechaza; T1 decía que el criterio venía de la constitución y no es así. FR: transversal. Origen: revisión crítica
del 2026-10-05.

**T102 · Fallecimiento, reclamación al seguro e insolvencia.**
Decisión: `LND_InsuranceClaims` y `LND_InsolvencyProceedings` nacen en K1 para recibir lo migrado y lo que bloquea
desde K1 (T30 ya suspende por insolvencia); su registro y su trámite por pantalla son de K3 (`DebtorEvents.Manage`,
`Insolvency.Manage`). El fallecimiento bloquea cobranza, mensajes y reporte negativo (FR-097, FR-060) y abre la
reclamación en `InProcess`; la causación sigue salvo que `Causacion.ContinuaTrasFallecimiento` diga otra cosa. El
contrato reconoce el pago de la aseguradora así: un `AplicarAsync` cuyo pagador es la aseguradora de una reclamación
`InProcess` de esa obligación la cierra (`Paid`) en la misma transacción, con la referencia de la operación; lo que
quede del saldo sigue su cobro. La insolvencia abre el proceso en `Negotiating` (suspende causación y cobro y mantiene
la calificación), y su acuerdo (una reestructuración, K3) o su fracaso lo cierran. Por qué: FR-085 manda cerrar la
reclamación al aplicar el pago de la aseguradora y FR-097, FR-098 piden estado y trámite que sólo los permisos no dan.
FR: FR-060, FR-085, FR-097, FR-098. Origen: revisión crítica del 2026-10-05.

---

## 4. Choques entre informes y cómo se resolvieron (CH1..CH50)

Formato: **Propuestas** · **Decisión** · **Descartado y por qué** · decisión de la §3 que lo aplica.

### Procesos, mensajes e idempotencia

**CH1 · Servicio de fondo, arrendamiento y corridas.** Propuestas: `ProcesadorDeCartera`, `lending.process`,
`LND_ProcessRuns`/`…Errors`, opciones `Lending:Process` (PM-13) · `ProcesoDiarioDeCartera`, `lending.accrual`,
`LND_AccrualRuns`/`…Failures`, `Lending:Accrual` (M14) · arrendamiento `lending.monthly-close` para el cierre (R13).
Decisión: PM-13 (un servicio, un arrendamiento, una tabla de corridas con clases `DailyCycle`, `MonthlyClose`,
`CloseReopen`); la lógica de tandas y fallas de M14 y la del cierre de R13 corren dentro. Descartado: dos servicios y
dos arrendamientos, porque cierre y causación de la misma cooperativa competirían por los mismos créditos y el cierre
exige la causación al día. → T5, T47.

**CH2 · Qué es el día de causación y a qué hora corre.** Propuestas: el ciclo de D empieza el día siguiente a la
01:00 (PM-13) · la causación de d son los intereses de (d−1, d] y corre desde las 00:30 de d (M4, M14). Decisión: M4
(semántica del motor) con la hora como opción técnica `StartTimeLocal`. Descartado: PM-13, porque un pago con fecha
valor d no vería el interés de d hasta el día siguiente. → T6.

**CH3 · Cómo emite el proceso.** Propuestas: columna `OriginActorKind`, `OriginUserCentralId` nulo, `UsuarioDeOrigen`
y marca `AdmiteProceso` en el catálogo (PM-02) · lista blanca `EmisionesDeProcesoPermitidas` en código y usuario de
origen «Proceso de la cooperativa» (DC-03). Decisión: el mecanismo de PM-02; el nombre de DC-03 queda para la prueba
de arquitectura que fija qué tipos admiten proceso. Descartado: un usuario de origen con nombre fijo, porque `UserRefV1`
ya admite nulo y un nombre falso confunde la auditoría. → T11.

**CH4 · Quién dice el módulo de origen.** Propuestas: el catálogo, por tipo (PM-01) · la solicitud de emisión (DC-03).
Decisión: el catálogo. Descartado: la solicitud, porque un módulo podría emitir tipos de otro. → T10.

**CH5 · Orden entre mensajes de un crédito.** Propuestas: cadena por crédito para todos sus hechos y respuestas
(PM-03) · dependencias sólo hacia el original (`Related`) (DC-04). Decisión: DC-04 para Contabilidad, Tesorería y
Recaudo; cadena (`CadenaDeOrden`) sólo en las respuestas a Comercial, con la venta como raíz. Descartado: la cadena por
crédito, porque una regla faltante en un tipo de hecho congelaría toda la contabilidad posterior del crédito; el orden
que exigen FR-055 y FR-126 lo dan `Related` y la cadena de la venta. → T12.

**CH6 · Contabilidad como destino de dos módulos.** Propuestas: `UnidadDeConsumo.OriginModule` y prefijo de
`ScheduleKey` (PM-11) · enrutador en `DestinoContabilidad`, versiones aceptadas por módulo, `PendingIntegrationMessagesQuery` (hoy `ListPendingIntegrationMessagesQuery`)
y `BatchTrigger` 7 y 8 (DC-02, DC-11). Decisión: las dos, que son complementarias; la `ScheduleKey` de Cartera lleva
prefijo `CAR.` y disparadores `FinDeCausacion`/`CierreDeCartera`. Descartado: un destino aparte «AccountingLending»
(los dos informes lo descartaron). → T15, T56.

**CH7 · Nombres de los hechos.** Propuestas distintas para lo mismo: `AjusteDeCausacionPorFechaValor` (PM) /
`CausacionAjustadaPorFechaValor` (DC); `RecuperacionDeCastigada` (PM) / `CastigadaRecuperada` (DC);
`CarteraReclasificadaPorCalificacion` (PM, R13) / `CarteraReclasificada` (DC); `ReversoDeCierre` (PM) /
`CierreDeCarteraReversado` (DC, R13); `CatalogoDeMensajesDeCartera` (PM) / `CatalogoDeMensajesDeCarteraV1` (DC).
Decisión: el nombre literal de FR-125 (los primeros de cada par) y el sufijo `V1` en el catálogo, como
`CatalogoDeMensajesV1`. Descartado: los demás, por la regla de la 012 «tipos de mensaje como la spec». → §2.11.

**CH8 · Informativos de cierre para Contabilidad.** Propuestas: `CierreMensualDeCarteraRealizado` y `…Reabierto`
(PM) · `CierreDeCarteraRealizado`, `CierreDeCarteraReabierto`, `CarteraActivada`, `CargaDeCarteraAplicada` (DC-14).
Decisión: los cuatro de DC-14. Descartado: sólo dos, porque la regla de cierre necesita saber desde cuándo Cartera está
activada y avisar del corte de una carga sin activar. → T57.

**CH9 · Nombre del destino de Comercial.** Propuestas: `Inventory` (PM-06) · `Commercial` con consumidor
`DestinoComercial` (DE-14). Decisión: `Inventory`, con consumidor `DestinoInventario`. Descartado: `Commercial`, porque
el módulo de código, los permisos y la clase de participante de Comercial son `Inventory` (T3 de la 012). → T15, T18.

**CH10 · Nombre de Recaudo.** Propuestas: destino y clase `Collections` (PM-06, PM-07) · cobranza de Cartera como
`Collection*` en tablas, permisos `Lending.Collections.*` y vista `collections` del centro de informes con el sentido
de «recaudo» (F-08, F-15, S1). Decisión: Recaudo = **`Receipts`** (destino, clase de participante, valor de
`ClaseDeRecaudador`, vista `receipts`); cobranza = `Collection*`. Descartado: `Collections` para Recaudo, porque en
inglés de banca es cobranza y chocaría con el área de cobranza ya nombrada por dos frentes. → T15, §2.15 (pregunta B2).

**CH11 · Puerto, DTO y clave de las reservas.** Propuestas: `IReservasDeCartera`, `ReservaDto`, v2 con
`ReservedLoanNumber` y `ReservationPublicId`, clave (INV, pago) (PM-10) · `IReservasDeCredito`, `ResultadoDeReserva`, v2 con
`ReservedCreditNumber` y `CreditLineCode`, clave `INV:{pago}:{monto}`, columna `DocumentPayment.ReservedCreditNumber`
(DE-01, DE-13, DE-14). Decisión: puerto `IReservasDeCartera` (como `IConsultasDeCartera`); resultado
`ResultadoDeReservaDto` (salida con `Dto`, §2.1); v2 con `ReservedLoanNumber` y `ReservationPublicId` (`CreditLineCode` ya está en v1); clave con
monto (DE-13); columna `DocumentPayment.ReservedLoanNumber`. Descartado: la clave sin monto, porque un cambio de monto
quedaría como `KeyReused` sin salida; `CreditLineCode` en v2, porque ya existe. → T18, T22.

**CH12 · Variante de `ResultadoDeConsumo` para la validación fallida.** Propuestas: `ValidationFailed(ref, número,
código, motivo)` (PM-09) · `ProcessedWithFailedValidation` (DE-14). Decisión: `ValidationFailed`, que coincide con
`DeliveryStatus.ValidationFailed` y `DeliveryAttemptOutcome.ValidationFailed = 5`. → T17.

**CH13 · Qué habilita el destino Cartera en el despachador.** Propuestas: sólo el registro de participantes (PM-08) ·
el parámetro de Comercial **y** el registro (DE-15). Decisión: sólo el registro, que activa la activación de Cartera.
Descartado: exigir también el parámetro, porque FR-142 dice que activar habilita la recepción y Cartera no escribe
parámetros de Comercial. → T16.

**CH14 · Salario mínimo y festivos para Nómina.** Propuestas: proyección escrita por Nómina desde la réplica
(`EfectoDeReplicacionEnNomina`) (R6) · lector único `ValoresNacionalesParaNomina` en cinco cargadores y retiro de las
filas Ley 51 y decretadas de `PAY_Holidays` (DE-18). Decisión: R6 en K1. Descartado por ahora DE-18, más literal pero
con regresión amplia sobre Nómina y PILA en producción; queda como mejora. La fuente sigue siendo una sola: sólo la
réplica escribe esas filas. Como FR-147 y C8 dicen que Nómina «lee» la plataforma, la decisión se somete como
enmienda (A7). → T93 (preguntas A7, G1).

**CH15 · Festivos «Manual» de una cooperativa.** Propuestas: quedan locales de Nómina y Cartera no los usa (R) · fila
local marcada dentro de la réplica nacional (DE-18). Decisión: R. Descartado: mezclarlos en la réplica, porque un día
no laboral propio no es festivo del Código de Comercio. → T27, T93 (pregunta E5).

**CH16 · Cómo queda Nómina registrada como recaudador.** Propuestas: semilla idempotente en la migración, como las
filas de arrendamiento (PM-07) · la API al arrancar, como los permisos (DE-03). Decisión: migración
(`PlataformaParaCartera`). Descartado: el arranque, porque el registro lo leen el despachador y la activación y debe
existir con la versión del esquema que trae la clase; el precedente de `COR_BackgroundLeases` es la migración. → T16.

**CH17 · Clave de idempotencia de Nómina.** Propuestas: `Payroll:{runVersionPublicId:N}:{loanPublicId:N}` (PM-12) ·
`PAY:{runPublicId:N}:{obligationPublicId:N}` (DE-03). Decisión: DE-03 con el prefijo corregido al código de módulo de
`ModuloContable`, `NOM:{runPublicId:N}:{obligationPublicId:N}` (`PAY` es el prefijo de las tablas de Nómina, no su
código de módulo), y el vocabulario «obligación» de los contratos. → T21.

**CH18 · Auditoría del contrato síncrono.** Propuestas: `TareasTrasElCommit.AlTerminar(bool confirmado)` con bitácora
del contrato (DE-02) · intento por contexto aparte antes y hecho dentro de la transacción, desenlace derivado (S9).
Decisión: S9. Descartado: DE-02, porque toca el pipeline de comportamientos de la plataforma y acopla Cartera a lo que
pase después del commit de otro módulo. → T68.

**CH19 · Cerrojo del contrato.** Propuestas: `ICerrojoPorClave` movido a `Common/Persistence` (DE-02) · cerrojo de
fila sobre `LND_LoanControls` en orden de Id (M13). Decisión: M13 para todo (contrato, pantallas y proceso).
Descartado: dos mecanismos de bloqueo sobre el mismo crédito, porque el proceso necesita saltar filas ocupadas y el
orden debe ser uno. → T34, T91.

**CH20 · Dobles de prueba, canales simulados y dobles unitarios.** Propuestas: dobles de integración de FR-089 con
guardia de DI y fila `Test` (PM-17) · guardia de canales `SIMULADO` con efecto legal (F-03) · dobles de los puertos en
pruebas unitarias (DE-19) · valor `Prueba` en `ClaseDeRecaudador` (DE) frente a `Test` en la clase de participante
(PM-07). Decisión: tres conceptos distintos con nombres distintos: **dobles de prueba** (`TesoreriaDePrueba`,
`RecaudadorDePrueba`; nunca en producción, la API no arranca), **canales simulados** (`SIMULADO`; en producción, antes y
después de activar, siempre sin efecto legal) y **dobles unitarios** (sólo en `tests/`). Un solo criterio de producción:
`IHostEnvironment.IsProduction()`. Valor `Test` en los dos enums. → T20, T73.

### Riesgo, seguridad y actas

**CH21 · Actas.** Propuestas: modelo propio de órganos, sesiones, puntos y votos (S5) · `LND_CommitteeSessions`,
`…Attendees`, `…Decisions` sobre `COR_Committees` (R-06) · `MinutesReference` como texto en política SARC y elección de
modelo (R11, R15) · la spec nombra «motor de aprobaciones» entre las dependencias (S6). Decisión: S5; todo «con acta»
referencia un `SessionItem`. Descartado: `COR_Committees` (es el catálogo heredado de comités culturales, sin quórum ni
composición con vigencia); el texto libre (no prueba nada ni deja votar); el motor de la 012 (T65). → T64, T65.

**CH22 · Tablas compartidas heredadas.** Propuestas: conservar `LND_Minutes`, `LND_MinuteAttendees`, `LND_Blacklist`,
`LND_TransactionCodes`, `LND_Documents` (R-01) · retirarlas si el diagnóstico confirma cero filas (S). Decisión:
conservar. Descartado: retirarlas, porque C6 dice «sólo las de crédito» y su feature dueña no existe. → T86 (pregunta H2).

**CH23 · Solicitudes del titular y prefijo `CMP_`.** Propuestas: `CMP_DataSubjectRequests` y columnas en
`CMP_HabeasData*` (S10) · `LND_DataSubjectAuthorizations` y `LND_DataSubjectRequests` (R-06). Decisión: autorizaciones
como columnas en las dos `CMP_` existentes; solicitudes en **`COR_DataSubjectRequests`**. Descartado: tablas `LND_`
(duplicarían el consentimiento de la 012) y una tabla `CMP_` nueva (el prefijo no está en la constitución). → T2, T69.

**CH24 · Listas restrictivas.** Propuestas: `LND_RestrictiveListLoads`, `…Entries`, `…Matches` y permisos
`Lending.RestrictiveLists.*` (S11) · carga `COR_` de plataforma, resultado `LND_ScreeningResults`, permisos
`Compliance.RestrictiveLists.*` (F-18) · `COR_RestrictiveListEntries` sobre `COR_ListParameters` (R-06). Decisión: F-18;
la liberación es una ruta de Cartera con el permiso de Cumplimiento. Descartado: todo en `LND_` (el oficial no es de
Cartera y otros módulos la reusarán) y colgarla del catálogo heredado de listas. → T70.

**CH25 · Libro y proyecciones.** Propuestas: `LND_RepaymentScheduleVersions`, `LND_RepaymentInstallments`,
`LND_LoanDailyPositions` (M2) · `LND_LoanScheduleVersions`, `LND_LoanInstallments`, `LND_LoanBalanceSnapshots`
(R-06). Decisión: M2 (el frente dueño del motor). → T23.

**CH26 · Cierres e historial.** Propuestas: `LND_MonthlyCloses`, `LND_MonthlyCloseLoans`, `LND_LoanArrearsHistory`
(R13, R14) · `LND_MonthlyClosings`, `LND_MonthlyClosingLoans`, `LND_LoanDelinquencyHistory` (R-06). Decisión: R13 y
R14 (el frente dueño del riesgo). → T47, T49.

**CH27 · Comprobante de aplicación.** Propuestas: `LND_PaymentApplications` y `…Lines` (R-06) · la `LoanOperation`
de pago lleva el número del comprobante y su distribución son las `LoanTransaction` (M2). Decisión: M2. Descartado:
tablas aparte, porque duplicarían la distribución que FR-081 redistribuye y la clave de idempotencia. → T23.

**CH28 · Modalidades.** Propuestas: documento nacional `CatalogoDeModalidades` (R16) · tablas
`LND_CreditModalities` y `LND_CreditModalitySubsegments` (R-06). Decisión: R16. Descartado: tablas por cooperativa,
porque la clasificación es nacional y la usura se certifica por modalidad. → T44.

**CH29 · Reserva de cupo.** Propuestas: la reserva de número lleva el valor y descuenta cupo (PM-10) ·
`LND_CreditLimitReservations` aparte (R-06). Decisión: PM-10. Descartado: una segunda tabla de reservas para el mismo
hecho. → T22.

**CH30 · Personas vinculadas.** Propuestas: `LND_RelatedParties`, `RelatedPartyBasis` (S) · `LND_RelatedPersons`
(R-06) y plantilla `lending.related-persons` (F-16). Decisión: `RelatedParties` en tabla, migración y plantilla
(`lending.related-parties`, `LND_MigrationRelatedParties`). → §2.3, §2.23.

**CH31 · Libranza, cobranza, aviso previo y regulatorio.** Propuestas de R-06: `LND_PaymasterAgreements`,
`LND_DeductionFiles`/`…Lines`, `LND_CollectionAssignments`, `LND_RepaymentAgreements`, `LND_LegalProceedings`,
`LND_PriorNoticeDeliveries`, `LND_RegulatoryReports`, `LND_CreditBureauFiles` · propuestas de F-08, F-09, F-11, F-12:
`LND_PayrollAgreements*`, `LND_PayrollDeductionFiles`/`…Lines`, `LND_CollectorAssignments`, `LND_PaymentAgreements*`,
`LND_LegalProcesses*`, comunicaciones del aviso como `DebtorMessage`, `LND_RegulatoryFiles`. Decisión: F (los frentes
que diseñaron el comportamiento). → §2.3, T77–T79.

**CH32 · «Messages»: bandeja de integración y mensajes al deudor.** Propuestas: `/api/lending/messages` y
`Lending.Messages.*` para la bandeja (PM-15, S1) · los mismos nombres para los mensajes al deudor y errores
`Lending.Message.*` (F-04, F-05). Decisión: bandeja = `messages`/`Lending.Messages.*` (como `/api/inventory/messages`
de la 012); deudor = `/api/lending/debtor-messages`, `Lending.DebtorMessages.*`, `Lending.DebtorMessage.*`, pantalla
`/cartera/mensajes` (y la bandeja en `/cartera/bandeja-de-mensajes`). → §2.14, §2.15, §2.17.

**CH33 · Permisos con distinto nombre para lo mismo.** S1 es el catálogo; se renombran: `Lending.Simulator.Use` →
`Simulations.Run`; `Loans.View` → `Loans.Read`; `Settlement.View` → `Payoffs.Read`; `Accrual.View`/`Retry` y
`ProcessRuns.View`/`Order` → `Processes.View`/`Run`; `Risk.View` → `Ratings.Read`; `Risk.AnalysisRating.Manage` →
`Ratings.Override`; `PortfolioEvaluation.Manage`/`Approve` → `PortfolioEvaluations.Prepare`/`Apply`;
`NumberReservations.View` → `NumberReservations.Read`; `AccountingFacts.View` → `Messages.Read`; `Bureau.Query`/
`ViewReport` → `CreditBureau.Query`/`ReadReports`; `Documents.View` → `Documents.Read`; `Regulatory.Generate` →
`RegulatoryReports.Generate` y `CreditBureauFiles.Generate`; `Regulatory.View`/`MarkUploaded` →
`RegulatoryFiles.Read`/`MarkUploaded` (`Read` porque traen deudores, T60); `Disbursements.GenerateFile` → `DisbursementFiles.Generate`;
`PayrollFiles.View`/`Generate` → `PayrollDeductionFiles.Read`/`Generate`; `Payrollers.*` → `PayrollAgreements.*`;
`DeductionFiles.*` → `PayrollDeductionFiles.*`; `Unapplied.Manage` → `PayrollNotApplied.Manage`;
`LegalCollection.Manage` → `LegalProcesses.Manage`; `Collections.View`/`RegisterAction` → `Collections.Read`/
`RecordActivity`; `PaymentAgreements.Manage` → `.Create`/`.Approve`; `Lending.RegulatoryProfile.*` →
`Core.RegulatoryProfile.*`; `Lending.RestrictiveLists.*` → `Compliance.RestrictiveLists.*`; `Lending.Modalities.*`
desaparece (documento nacional). Se agregan los de otros frentes que S1 no tenía: `Integrity.Verify`,
`RiskPolicy.Manage/Validate`, `ArrearsHistory.Import`, `TestDoubles.Operate`, `Templates.View`, `MessageTemplates.*`,
`Campaigns.Manage`, `DebtorMessages.*`, `PriorNotices.RegisterEvidence`, `Documents.Verify`. Por qué: datos del deudor
= `Read` y un nombre por recurso. → T60.

**CH34 · Calendario hábil.** Propuestas: motor `CalendarioHabil` en `Domain/Lending/DayCount` (M6) · puerto de
plataforma `ICalendarioHabil` (R5) · puerto `ICalendarioDeFestivos` (F-05). Decisión: puerto `ICalendarioHabil`
(plataforma, lector único de `FESTIVO`) y motor puro `CalendarioHabil` en `Domain/Common/Calendar`, que usan la
implementación del puerto y Cartera. Descartado: `ICalendarioDeFestivos` (duplica el puerto) y el motor dentro de
`Domain/Lending` (la plataforma no puede depender de un módulo). → T27, T40.

**CH35 · Código del valor nacional faltante.** Propuestas: `Lending.NationalValue.Missing` (M) ·
`NationalValues.Missing` (R5). Decisión: `NationalValues.Missing`, del lector de plataforma. → T40.

**CH36 · Parámetros y semillas.** Propuestas: módulo «Cartera» (DC-11) frente a `LND` (R17, S7, M); claves con
prefijo «Cartera.» (M, F) frente a áreas (`Riesgo.`, `Contabilidad.`); tolerancia duplicada
(`Cartera.ToleranciaDeMora` de M y `Riesgo.ToleranciaSaldoVencido` de R17); hora y tamaño de tanda como parámetros de
la cooperativa (M) frente a técnicos (PM-13); `Order 88` pedido por `LendingVoucherMappingsSeeder` (DC-08) y por
`NationalValuesReplicaSeeder` (R4). Decisión: módulo `LND`; claves por área sin «Cartera.»; una sola
`Mora.ToleranciaDeSaldoVencido`; hora, paralelismo y tanda técnicos (la parte cabe en la tanda, T14); Order 88 para la réplica, 89 para los
mapeos, 90 para los tipos de hecho. → T43, §2.20, §2.21.

**CH37 · Cambio de `PAY_SettlementDeductions`.** Propuestas: migración `NominaRecaudaPorElContrato` antes del retiro,
que agrega `ObligationPublicId`, copia el PublicId heredado y suelta la FK y la columna (DE-09; además nombra mal la
tabla `PAY_PayrollNovelties`) · todo dentro del retiro destructivo, sin copia (R-02). Decisión: lo aditivo y la copia en
`NominaRecaudaPorElContrato`; soltar FK, índices y columnas (y `PAY_Novelties.LoanPortfolioId`) en
`RetiroDeLaCarteraHeredada`. Descartado: una segunda migración destructiva (DE-09) y perder la historia (R-02). →
T87, §2.26.

**CH38 · Nombres y entrega de tareas.** Propuestas: `cartera.cierre-programado` (PM-14) / `cartera.cierre-mensual`
(R13); `cartera.ventana-de-giro` (PM-14) / `cartera.giros-de-pagaduria` (F-11); `cartera.mensajeria` cada 5 minutos
desde K4 (PM-14) / `cartera.mensajes` cada minuto y `cartera.entregas` desde K1 (F-06). Decisión:
`cartera.cierre-programado`, `cartera.giros-de-pagaduria`, `cartera.mensajes` y `cartera.entregas` en K1. Descartado:
la mensajería en K4, porque los informativos de D24 y de FR-112 existen desde K1. → T7, §2.13.

**CH39 · Literales admitidos en `LaCarteraNoTieneValoresLegalesFijos`.** Propuestas: los de Nómina más 365, 7 y 10
(M16) · 0, 1, 2, 0.5, 12, 30, 100, 360 y 365 con 7 entre los sospechosos (R18). Decisión: los de Nómina (incluido 15)
más 365; 7 y 10 nunca como literal: salen del valor del enum de periodicidad (precedente de la feature 006); la lista
de sospechosos de R18 con exención comentada por archivo. → §2.25.

### Nombres, ubicación y pantallas

**CH40 · Idioma de los enums de los puertos.** Propuestas: enums en español (`ClaseDeRecaudador`, `ModalidadDePago`
con valores `Cancelacion`, `ValorLibre`…) (DE) · `PaymentModality` en inglés (M) · regla de la 012 (enums en inglés).
Decisión: tipo en español (son DTO de puerto, como `EstadosDeValidacion`), valores en inglés idénticos al motor. →
T1, §2.7.

**CH41 · Carpetas en español.** Propuestas: `Application/Lending/{Originacion, Actas, Atribuciones, Segregacion}` (S7),
`Integracion` (PM-09), `Migracion/Activacion` (R-07). Decisión: en inglés (`Origination`, `Sessions`, `Attributions`,
`Segregation`, `Integration`, `Migration`, `Activation`); la excepción es `Accounting/Lending/{Reglas, …}`, espejo de
lo que ya existe. → T1.

**CH42 · Servicio que arma las emisiones.** Propuestas: `EmisionDeCartera` (PM) · `EmisionContableDeCartera` (DC).
Decisión: `EmisionDeCartera`, uno solo para hechos y respuestas. → §2.9.

**CH43 · Implementaciones provisionales y reales de los puertos.** Propuestas: `ConsultaDeObligacionesNoHabilitada`,
`ContratoDeObligacionesNoHabilitado` (DE-01) · `ConsultaDeObligacionesSinActivar`, `ContratoDeObligacionesSinActivar`
(R-03); el DTO `ConsultaDeObligaciones` de DE choca con el nombre natural de la implementación real. Decisión: «sin
activar» para los puertos de obligaciones (responden `Lending.NotActivated`) y «no habilitada» para los de Comercial
(responden `NotEnabled`); reales `ObligacionesDeCartera` y `ContratoDeObligacionesDeCartera`. → T90.

**CH44 · Pantallas y rutas de informes de otras áreas.** Propuestas: `/cartera/conciliacion` (F-20) frente a
`/cartera/conciliacion-contable` (DC); `/cartera/canales-externos` (F-20) para una tabla `COR_` con permiso `Core.*`;
`/cumplimiento/listas-restrictivas` (F-18) frente al área `/compliance/*` que ya existe; `person-portfolio` retirado
(F-15) frente a conservado sin créditos (R-08). Decisión: `/cartera/conciliacion-contable` (no se confunde con la
conciliación de ventas de la migración); `/admin/canales-externos`; `/compliance/listas-restrictivas`; `person-portfolio`
conservado sin créditos. → T98, T99, §2.16.

**CH45 · Nombre del método de cuentas bancarias.** Propuestas: `CuentasBancariasAsync` (DC-17) ·
`CuentasBancariasDeOrigenAsync` (F-10). Decisión: `CuentasBancariasAsync` (el puerto es de DC). → T59, T78.

**CH46 · Carga complementaria del historial de mora.** Propuestas: plantilla `lending.delinquency-history` en K1 (F-16)
· `LND_ArrearsHistoryLoads` y la carga complementaria en K6a (R14, spec: US8-10 en K6a). Decisión: K6a, con la clave
`lending.arrears-history`; el historial opcional de la migración (FR-139) sí entra en K1 como parte de la carga
(`LND_MigrationArrearsHistory`). → T49, §2.23.

**CH47 · Comando de aplicación de pagos.** Propuestas: `ApplyLoanPaymentCommand` (M) · todo dinero entra por el
contrato, que es un servicio y no un comando (DE-02, S9, FR-087). Decisión: sin comando de aplicación; Cartera simula
con `SimulateLoanPaymentQuery` y reversa por pantalla con `ReverseLoanPaymentCommand`. → T31.

**CH48 · Series regulatorias del motor.** Propuestas: `ISeriesRegulatorias` de Cartera que lee usura, índices y
festivos (M1) · sólo los tres lectores de plataforma tocan los valores nacionales (R18). Decisión:
`ISeriesRegulatorias` se construye sobre los lectores (con `LeerSerieAsync` para la carga en bloque). → T40.

**CH49 · Oficina sin sucursal contable.** Propuestas: bloquear antes de confirmar (PM-05) · usar la sucursal principal
con alerta y exigir el vínculo al activar (DC P14). Decisión: bloquear y exigir el vínculo de toda oficina con créditos
al activar. Descartado: la principal, porque deja saldos en una sucursal equivocada que la conciliación por sucursal
no explica. → T13, T84 (pregunta C9).

**CH50 · Cuándo se retiran las pantallas de crédito heredadas.** Propuestas: todo lo heredado de crédito (código,
rutas, pantallas, menú, manual, PDF) sale en un commit previo a la migración destructiva (R-08) · cada página, enlace,
tema del manual y tarjeta sale en el mismo cambio que publica su reemplazo (F-20). Decisión: R-08; todo sale en el
paso 3 de T88 y se acepta un tramo sin pantallas de crédito. Descartado: F-20, porque las pantallas viejas
sobrevivirían a sus 63 rutas, ya retiradas en ese mismo paso, y responderían 404 hasta su reemplazo; conservar
también esas rutas obligaría a mantener la cartera heredada en el modelo intermedio. → T88, T89, T98.

---

## 5. Preguntas al dueño (consolidadas)

Las 106 preguntas de los ocho informes quedan en **91 sin duplicados**: diez se fundieron con otra igual (la cuenta
puente de Nómina, los informativos de cierre, los festivos «Manual», la fecha valor de Nómina y de la pagaduría, el
volumen del resumido, las tablas compartidas, el perfil regulatorio y las dimensiones en la lista cerrada, el retiro
de asociado), seis se resolvieron aquí como choque o decisión técnica (la hora y el paralelismo del proceso, la hora
de la causación, el registro de Nómina como recaudador, una `ObligacionRechazada` por evaluación, los tipos de
comprobante sembrados y la ruta `savings-statement`) y se agregó una (H7). La revisión crítica del 2026-10-05 agregó
seis (A8, A9, A11, C11, E10, G11) y llevó tres al grupo A porque eran enmiendas de la spec (G1 → A7, G8 → A6,
H5 → A10; sus filas quedan como remisión): son **97**, y la conciliación (§7.7) suma E11, I16 e I17: **100**. Cada una tiene
una propuesta por defecto coherente con la spec y la constitución; si el dueño no responde, **rige la propuesta**.
«Bloquea» dice qué no puede cerrarse sin respuesta explícita.

### A. Enmiendas a la especificación

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| A1 | ¿Se enmienda la lista cerrada de interfaces con «Dimensiones de Cartera para Contabilidad» (Ofrece), «Perfil regulatorio» (Consume, plataforma), «Autorizaciones del titular» (Consume, Cumplimiento), «Oficinas, sucursales contables y usuario–persona» (Consume, Seguridad y plataforma: alcance, sobre y segregación) y la consulta de obligaciones para Cumplimiento? | Sí (T97) | `LaCarteraSoloSeTocaPorSusInterfaces` y los contratos de K1 |
| A2 | ¿FR-125 suma los informativos `CarteraActivada`, `CargaDeCarteraAplicada`, `CierreDeCarteraRealizado` y `CierreDeCarteraReabierto`? | Sí (T57) | La regla de cierre contable de FR-146 (K1) |
| A3 | ¿Cartera emite hechos para las cuentas contingentes del CUIF (9115 aprobados no desembolsados, 913005 rotativos, 9110 garantías)? | Sí desde K1 con modo `NoPasa`; sus tipos se nombran por enmienda de este documento antes de `contracts` | No bloquea |
| A4 | ¿Se corrige la fila XII de la spec (el retiro no toca la base administrativa, que sólo se respalda)? | Sí | No (redacción) |
| A5 | ¿Se adaptan en K1 el retiro de asociado (por FR-061), los extractos y el portafolio de la persona (sin la parte de crédito)? | Sí (T94, T88) | El retiro de lo heredado |
| A6 | ¿Se enmiendan Alcance y Dependencias («Comercial · servicios a asociados») para que esta feature construya en E-02 el dato `CreditWithoutLending` (D29)? (antes G8) | Sí (T95); si no, sale de K1 y es precondición de activación: un ajuste de la 012 del que depende la conciliación de FR-143 | Conciliación de ventas de COOFLOPAL; `ComercioConCartera` |
| A7 | ¿Se enmiendan FR-147 y C8 para que Nómina conserve una proyección del SMMLV, la UVT y los festivos que sólo escribe la réplica, en vez de leerlos de la plataforma? (antes G1) | Sí (T93); leer directo (`ValoresNacionalesParaNomina`), después | Alcance de E-03 (K1) |
| A8 | ¿Se enmienda FR-127 (y US7-2) para que «Bloquear» por falta de regla contable sólo aplique a las operaciones de pantalla de Cartera (desembolso, novedad, condonación, castigo), y en el contrato, el consumidor y los procesos siempre advierta? | Sí (T56): un pago que ya ocurrió no se rechaza por un vacío de la matriz y la nómina es «todo o nada» | Validación previa (K1) |
| A9 | ¿Se enmiendan FR-012 y D4 con una excepción para los pagos sin interacción cuya fecha valor fija la propia spec: Nómina (fin del período en la ordinaria, aprobación en la definitiva; FR-088, FR-112) y, desde K2, el pago de pagaduría con referencia (fecha del descuento, FR-114), sin límite de días ni atribución antes de un cierre? | Sí (T92); la alternativa es parametrizar el límite por clase de recaudador sin exención fija | Validación de fecha valor del contrato (K1, K2) |
| A10 | ¿Se enmienda FR-144 («quedan intactos») para que lo conservado cambie de espacio de nombres (`Savings`) y de ruta API (`/api/savings/*`, `/api/reports/savings/*`), sin tocar tablas ni rutas de pantalla? (antes H5) | Sí (T3, T99); si no, conserva `/api/lending/*` hasta la feature de Ahorros, con exención por nombre | Estructura del plan y `ElEspacioLendingEsSoloCartera` |
| A11 | ¿Se enmienda I-4 para que el desenlace «revertido junto con el recaudador» se derive de un intento sin hecho, en vez de escribirse? | Sí (T68); si no, `cartera.vigilancia` escribe un evento compensatorio por cada intento sin hecho pasado su plazo | Auditoría del contrato (K1) |

### B. Plataforma, procesos y valores nacionales

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| B1 | ¿Se acepta que el proceso origine mensajes, sólo en los tipos de Cartera marcados (enmienda de la regla de la 012)? | Sí (T11) | K1: todo hecho automático y las respuestas a Comercial |
| B2 | ¿Recaudo se identifica como destino `Receipts` y módulo `REC`? | Sí (CH10); la alternativa era `Collections` | Contratos de K1 (`PagoReversadoPorCartera`, recaudador de prueba) |
| B3 | ¿Qué retención tiene `COR_IntegrationMessages` con el volumen de Cartera? | Sin purga; medir con 50.000 créditos y decidir después | No |
| B4 | ¿«Despliegue de producción» es `IHostEnvironment.IsProduction()`? ¿Con qué ambiente corren DEV y QA en el clúster? | Sí; confirmar que QA no corre como Production (si corre así, allí no hay dobles) | K1: dobles de prueba y activación |
| B5 | ¿Quién registra valores nacionales y documentos normativos en la central? | El maestro con segundo factor, con plantilla de índices y paquete de exportación e importación entre ambientes | Rutas `/api/saas/*` (K1) |
| B6 | ¿Entra la UVT a los valores nacionales ya en K1, proyectada a Nómina como el SMMLV? | Sí (T93): `LectorDeUvt` no cambia y lee la proyección; `AddLegalParameterVersion` rechaza `UVT` | No |
| B7 | Si la central no responde al momento del cierre oficial, ¿se niega el cierre? | Sí, con motivo y reintento; la calificación diaria calcula con lo replicado y lo advierte | Diseño del cierre (K1) |
| B8 | Al confirmar por excepción una venta con validación fallida, ¿la entrega queda en `ValidationFailed`? | Sí; cambia el recibo y la consulta responde `Validated` «confirmada por excepción» | No |

### C. Contabilidad

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| C1 | ¿`DESC_CARTERA` contabiliza en Nómina contra un puente «recaudos por nómina para Cartera» (enmienda D-08 de la 010)? | Sí (T53) | Pagos por nómina y retiro de `RecaudoDeCredito` (K1) |
| C2 | Cuando el deterioro baja, ¿primero contra el gasto del mismo ejercicio y el resto a recuperaciones (4225)? | Sí, parámetro con fuente «pendiente de validar» | La validación del cierre con la contadora |
| C3 | Patrimonio técnico y solvencia: ¿registrados por el contador con soporte? ¿Aplica la solvencia a COOFLOPAL? Sin patrimonio técnico vigente, ¿qué hace el límite de concentración (FR-041)? | Registrados en `ACC_RegulatoryIndicators`, vencen a los 6 meses; sin dato, en FR-100 y FR-103 lo conservador de D19, y en FR-041 se bloquean desembolso, cupo y novedad con «patrimonio técnico no disponible» y alerta al contador (T59); la alternativa es usar el último registrado con advertencia | FR-041, FR-100, FR-103 (K1) |
| C4 | ¿Se amplían FR-107 y FR-126 con la dimensión «reestructurado» y la reclasificación de todo saldo con categoría? | Sí (T48, T52) | Cierre de K1 y SC-006 |
| C5 | Resumido de la causación: uno por día con la sucursal en cada línea; con cuentas que exigen tercero, ~20.000 líneas al día. ¿Se acepta? | Sí; medir con `RUN_PERF_TESTS`; la granularidad admite «por día y sucursal» | No el plan; lo valida la prueba de volumen |
| C6 | ¿Se agrega el comprobante `CN` y se mantiene `PG` como cruce aunque un crédito de Comercial no tenga pagaré? | Sí a `CN`; `PG` por continuidad; si la contadora objeta, cruce nuevo `CR` | Semillas de K1 |
| C7 | Capitalización al reestructurar, castigo con deterioro insuficiente y condonación: ¿roles `IngresoDiferido` y `GastoDeCastigoOCondonacion`? | Sí, sembrados sin cuenta y «pendiente de validar» | Hechos de K3 |
| C8 | ¿La dimensión «deudor empleado» sale del estado actual de la persona maestra? | Sí; el ciclo diario detecta el cambio y emite `DimensionContableCambiada` | Hechos y conciliación de K1 |
| C9 | Oficina con créditos sin sucursal contable vinculada: ¿qué pasa? | Se bloquea la operación y la activación exige el vínculo (CH49) | Activación (K1) |
| C10 | ¿Se difieren comisiones y costos de originación (FR-017)? | No (`Contabilidad.DiferirComisiones` = false, defecto de FR-017); si se activa, un hecho de amortización en otra entrega | No |
| C11 | ¿El interés de financiación de las líneas de origen comercial es gravado con IVA (D20)? | Sin defecto: cada versión de línea sella el concepto con su soporte; sin él, una línea de origen comercial con tasa mayor que cero no se activa (T44) | Líneas de origen comercial con interés (K1) |

### D. Motor financiero y pagos

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| D1 | ¿Cómo se define la equivalencia «lineal»? | NA_m = m·((1+EA)^(1/m) − 1) dividida por la base; por defecto compuesta; la valida la contadora | Casos dorados de K1 |
| D2 | Para el art. 829, ¿el sábado es hábil? | Sí; no hábiles sólo domingos y festivos nacionales, como parámetro legal «pendiente de validar» | `CalendarioHabil` (K1) |
| D3 | Real/365 en año bisiesto: ¿base fija 365 con el 29 de febrero causando? | Sí; el control de usura usa base 365; caso dorado de 2028 | `ContadorDeDias` (K1) |
| D4 | Un pago con fecha valor anterior a otro ya aplicado, ¿redistribuye el posterior? | Sí, como el reverso de FR-081 | T33 (K1) |
| D5 | Créditos migrados: ¿se conserva el plan cargado o se regenera? | Se conserva el capital por cuota; el interés lo causa el motor desde el corte y se declara en el acta de activación | Saldo inicial y cuadre (K1) |
| D6 | Si el anticipo no alcanza una cuota en su vencimiento, ¿se aplica parcial? | Sí, con la prelación | `AplicadorDePagos` (K1) |
| D7 | En una cancelación, ¿se aplican solos el anticipo y el saldo a favor del mismo crédito? | El anticipo sí; el saldo a favor no (quinta operación del contrato) | Liquidación y listado (K1) |
| D8 | Pagar la cuota del ciclo antes del vencimiento, ¿reduce intereses? | No; el resto queda como anticipo de esa cuota | Modalidad 3 (K1) |
| D9 | ¿Cuándo se causan los seguros? | Al vencer la cuota del período, sobre el saldo inicial del período o el monto inicial según la línea | `SeguroCausado` (K1) |
| D10 | Tasa variable: ¿el margen se suma en la expresión del índice o sobre la EA? | En la expresión del índice, luego a EA, piso, techo y usura | `TasaDelDia` (K1) |
| D11 | Liquidación a fecha futura sin usura certificada: ¿proyecta o bloquea? | Proyecta con la última conocida e informa `ValidaHasta`; la causación real sí se bloquea | `GetLoanSettlementQuery` (K1) |
| D12 | Desembolso por tramos: ¿el plan se regenera en cada tramo? | Sí, sobre el capital desembolsado | Tramos (K1) |
| D13 | Posiciones diarias (~18 millones al año con 50.000 créditos): ¿diarias o sólo en eventos? | Diarias desde K1; particionar por año al pasar de 20.000 créditos | Modelo de proyecciones (K1) |
| D14 | ¿Se causa mora sobre intereses? | No por defecto; parámetro legal con techo, nunca en vivienda | Causación de mora (K1) |
| D15 | Fecha valor de Nómina y de los pagos de pagaduría con referencia (K2) | Ordinaria: fin del período; definitiva: fecha de aprobación; pagaduría con referencia: fecha del descuento. La exención del límite D4 la decide A9 | Validación de fecha valor del contrato (K1, K2) |
| D16 | Un pago con fecha valor anterior a un cierre exige atribución: ¿quién la tiene cuando paga Recaudo? | El usuario de Recaudo con `Lending.Payments.BackdateBeforeClose` y su límite; si no, el contrato rechaza con `Lending.Payment.ValueDateBeforeClosing`; la exención de Nómina la decide A9 | Contrato (K1) |

### E. Riesgo y normativa

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| E1 | ¿«Pendiente de validar» bloquea algo? | No: marca el cierre y el SICSES y alerta; la activación en producción exige Anexo 1 vigente y usura de las líneas activas (T41) | Guardas del cierre y de la activación |
| E2 | La CE 100 cuenta la antelación del aviso contra el reporte del mes de aplicación; FR-105, contra la fecha desde. ¿Cuál rige? | La de la spec, más estricta, con la antelación como dato del documento; se documenta la diferencia para la contadora | K6a (adopción voluntaria) |
| E3 | Grupo NIIF: ¿cuál es la fuente si ya vive en `AccountingSetup.NiifGroup` sin vigencia? | El perfil regulatorio, sembrado desde `AccountingSetup`, con alerta si difieren; una enmienda posterior hace que Contabilidad lo lea del perfil | No |
| E4 | Modalidades productivas del Decreto 455: ¿a qué segmento SARC van y qué modalidades certifica la Superintendencia Financiera desde octubre de 2026? | Popular productivo rural y urbano → microcrédito; productivo rural, urbano y de mayor monto → comercial; claves de usura según la certificación, «pendiente de validar» | Semilla del catálogo de modalidades (K1) |
| E5 | ¿Los festivos «Manual» de una cooperativa cuentan para la mora de Cartera? | No; Cartera usa sólo el calendario nacional | No |
| E6 | ¿El cierre mensual es manual o programado por defecto? | Manual; `Riesgo.CierreProgramado` apagado; alerta pasado `Riesgo.DiaLimiteCierre` (10) | No |
| E7 | ¿La calificación diaria informativa aplica el arrastre? | Sí, rotulada «informativa»; se apaga con `Riesgo.CalificacionDiariaConArrastre` | No |
| E8 | Cortes del puntaje, PDI por días, homologación y comercial persona jurídica vienen como imágenes en los PDF: ¿quién los transcribe y coteja? | El equipo los transcribe con `verified = false`; el dueño y la contadora cotejan contra la circular consolidada vigente | Semilla de K6a; del Anexo 1 en K1 sólo la tabla de comercial persona jurídica |
| E9 | ¿El deterioro de cada crédito se redondea al centavo o al peso? | Al centavo; en la pérdida esperada sólo el puntaje se redondea a la escala del documento | No |
| E10 | ¿El microcrédito se divide en los sub-segmentos `Inmobiliario` y `Empresarial` del catálogo de modalidades, como el CUIF (1448, 1455)? | Sí, «pendiente de validar» por la contadora; sin ellos la matriz no puede elegir la cuenta de capital | Semilla del catálogo de modalidades (K1) |

### F. Seguridad, actas y habeas data

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| F1 | ¿La decisión de crédito vive en un modelo propio de Cartera en vez del motor `COR_Approval*` de la 012 que la spec cita? | Sí (T63–T65) | Diseño de originación (K1) |
| F2 | ¿Basta con trece plantillas y un comando que crea los roles al preparar la activación? | Sí (T61) | Siembra de seguridad (K1) |
| F3 | ¿Se tocan Auditor y CompanyAdmin? | Auditor no (el revisor fiscal usa su plantilla); a CompanyAdmin se le excluyen `Lending.Decisions.*` y `Lending.Sessions.Vote` | `BuiltInRolesSeeder` (K1) |
| F4 | ¿Se confirma que el alcance falla cerrado y se agrega en K1 el selector agencia–oficina en Agencias? | Sí; el asistente de activación lista usuarios con roles de Cartera sin alcance | Puesta en marcha (K1) |
| F5 | ¿Todo usuario que radique, decida, vote o desembolse debe tener persona vinculada? | Sí (`Lending.Security.UserWithoutPerson`) | Segregación y actas (K1) |
| F6 | ¿Qué incompatibilidades vienen activas por defecto? | `Decidir × Desembolsar`, desactivable con motivo; las demás sugeridas e inactivas | Semilla de parámetros (K1) |
| F7 | Sesiones: suplentes, quórum, mayoría ordinaria y mayoría para vinculados en entidades sin actividad financiera | Composición = principales con voto; el suplente vota por un principal ausente; quórum mayoría absoluta de la composición; mayoría de los presentes elegibles; sin actividad financiera, la de los estatutos; todo «pendiente de validar» | `EvaluadorDeVotacion` (K1) |
| F8 | ¿Los votos los registra la secretaría o vota cada miembro en el sistema? | La secretaría por defecto; el miembro que sea usuario puede votar con su sesión o en presencia con passkey o TOTP | Pantalla de sesión (K1) |
| F9 | ¿Se auditan los rechazos del validador (forma)? | Sí, como pide FR-003: el metadato del grupo `/api/lending` registra `Lending.Request.Rejected` (campos y códigos, sin valores) (T67) | No |
| F10 | ¿Qué es «dispositivo» en la auditoría? | User-Agent más `X-Dispositivo` de MAUI; oficina por `IConOficina` o la del usuario | No |
| F11 | Plazos del titular (consultas 10 + 5, reclamos 15 + 8 días hábiles, leyenda en 2): ¿como límites con fuente? | Sí, «pendiente de validar», con festivos nacionales | Solicitudes del titular (K1) y reclamos (K4) |

### G. Nómina, Personas y Comercial

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| G1 | (Movida a A7: es enmienda de FR-147 y C8.) | — | — |
| G2 | En la ordinaria, ¿un rechazo o excedente de Cartera ajusta la línea al aprobar? | Sí, con vista previa; no impide aprobar | T92 |
| G3 | ¿Reversar una ordinaria aprobada reversa sus recaudos por el contrato? | Sí, todo o nada, como la definitiva | `ReversePayrollRunCommand` |
| G4 | ¿Qué propone la definitiva? | Todas las vivas como deudor, castigadas incluidas y de cualquier forma de pago; nunca las de codeudor | `PropuestaDeDescuentosDeCartera` |
| G5 | Prelación de la cuota de Cartera en la ordinaria frente a otras deducciones y alimentos | Primera entre las autorizadas, después de las de ley y sujeta al tope; alimentos cuando exista ese concepto | Motor de Nómina y sus casos dorados |
| G6 | ¿La reserva de número corre dentro de la unidad de trabajo de la confirmación de Comercial? | Sí; si la medición de cerrojos en el POS sale mal, transacción propia con `Expired` sin reasignar | E-02 (K1) |
| G7 | ¿Quién es el pagador de la nómina propia: el empleado o la cooperativa? | El empleado deudor | Dimensiones de `PagoAplicado` |
| G8 | (Movida a A6: es enmienda de Alcance y Dependencias.) | — | — |
| G9 | Si cambia el monto de una venta con número reservado | Se libera la reserva y se pide otra con la clave nueva | T22 |
| G10 | Definitivas y novedades ya registradas con crédito heredado: ¿basta conservar el PublicId y el número como historia? | Sí, sin FK, en `PAY_SettlementDeductions` y `PAY_Novelties` (T87) | `NominaRecaudaPorElContrato` |
| G11 | Ventas de servicios a crédito confirmadas antes de existir `CreditWithoutLending`, que ya emitieron `VentaACreditoRegistrada` hacia Lending: ¿cómo se resuelven? | En la conciliación de FR-143, marcadas «aceptadas fuera de Cartera (D29)» con motivo y punto de acta; responden `ObligacionRechazada` no reprocesable (T83) | Activación de COOFLOPAL |

### H. Retiro de lo heredado

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| H1 | ¿`LND_SiplaGroupParameters` se conserva sin su FK a la línea? | Sí; si tiene filas, decide el dueño | Scaffold del retiro |
| H2 | ¿Se conservan las compartidas (`LND_Minutes`, `LND_MinuteAttendees`, `LND_Blacklist`, `LND_TransactionCodes`, `LND_Documents`)? | Sí (CH22) | Clasificación final |
| H3 | ¿Se retiran como crédito `LND_PeriodicityParameters`, `LND_DeductionValues`, `LND_InterestRates`, `LND_OnlineQueries`, `LND_RecreationApplications`? | Sí | Lista de la guarda |
| H4 | Si un ambiente tiene `ACC_Documents` con origen CAR, ¿se aprueba el retiro dejándolos con origen huérfano? | Sí, con la fila de aprobación y constancia | Promoción en esa base |
| H5 | (Movida a A10: es enmienda de FR-144.) | — | — |
| H6 | ¿Se retira entero el proyecto `IngenIA365ERP.Legacy`? | Sí | No |
| H7 | ¿Quién es el segundo revisor del retiro y se autoriza correr el diagnóstico en PDN? | El dueño lo nombra; diagnóstico con autorización expresa | Promoción |

### I. Adaptadores, cobranza, documentos e informes

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| I1 | ¿Los proveedores de SMS y WhatsApp los contrata cada cooperativa? | Sí, credencial por cooperativa y canal | Paso a canal real (K4) |
| I2 | Ley 2300: «un solo canal por semana» tras un contacto directo, ¿es el canal de ese contacto? ¿Los informativos cuentan? | El del contacto directo; los informativos respetan horario y tope pero no fijan canal; lo valida el abogado | Casos dorados de `VentanaLegalDeContacto` |
| I3 | ¿Un documento pedido por el titular y enviado por correo está exento de las ventanas? | Sí; exige correo registrado y autorización de tratamiento | Envío de documentos |
| I4 | En producción y sin contratos, ¿se admite el simulado antes y después de activar? ¿Pueden las líneas aprobar sin consulta a centrales? | Sí, siempre sin efecto legal (T73, como dice FR-044); cada línea lleva «exige consulta a centrales»; sin pagaré desmaterializado, pagaré físico | Originación en producción (K1) |
| I5 | ¿Se adelantan a K1 el plan de pagos, el paz y salvo y el certificado de saldo (la spec deja los documentos en K5)? | Sí; extracto y certificado tributario en K5 | Orden de las entregas |
| I6 | Firma de la cooperativa y logotipo en los documentos | Nombre y cargo por parámetro más código y QR; imagen de firma y logotipo como adjuntos opcionales | PDF de documentos |
| I7 | ¿Cuál es el formato de cartera del SICSES y qué centrales usa COOFLOPAL? | El dueño aporta layouts; se cargan con `verified = false` | K5 |
| I8 | Layouts de ida y vuelta de cada pagaduría y quién procesa la vuelta mientras Recaudo no exista | Formatos con ámbitos 5 y 6; en pruebas, el doble emite `ResultadoDeDescuentos` | K2 |
| I9 | ¿Se puede regenerar un archivo de descuentos ya enviado? | No; la corrección va como novedad del período siguiente | K2 |
| I10 | ¿De dónde sale la cuenta bancaria del beneficiario del desembolso? | Se propone desde la persona o el asociado, se corrige al desembolsar y se sella en la orden | Desembolso por transferencia (K1) |
| I11 | ¿Cuántos créditos y cuotas trae la migración de COOFLOPAL y están ya en el maestro sus personas? | Carga por partes; las personas se registran antes (no hay plantilla de personas) | Migración (K1) |
| I12 | Listas restrictivas: ¿cada cooperativa carga las suyas? ¿Se re-escanea a los deudores vivos al cargar una versión? | Cada cooperativa; re-escaneo nocturno sólo con alerta al oficial | Listas (K1) |
| I13 | ¿La cooperativa debe existir como persona jurídica en `COR_People` para su convenio de nómina propia? | Sí | K2 |
| I14 | ¿Los avisos a usuarios internos también salen por SMS? | No; sólo el puerto de mensajería a personas | No |
| I15 | ¿Quién registra ante el proveedor las plantillas de WhatsApp aprobadas por Meta? | La cooperativa; sin `ProviderTemplateId` no se usa WhatsApp | WhatsApp real (K4) |

---

## 6. Lo que construye cada entrega

| Entrega | Migraciones | Tablas | Motores y piezas | Notas |
|---|---|---|---|---|
| **K1 · Núcleo y riesgo básico** | `NominaRecaudaPorElContrato`, `ComercioConCartera`, `RetiroDeLaCarteraHeredada` (destructiva), `ValoresNacionales` (Admin), `PlataformaParaCartera`, `CarteraNucleo`, `CarteraRiesgo`, `IntegracionContableDeCartera` | `LND_` de configuración, originación, crédito, libro, proyecciones, integración, procesos, mensajes al deudor, avisos y reclamos (datos), documentos (si se adelantan), migración, activación y riesgo; `ACC_Lending*`, `ACC_RegulatoryIndicators`; `ADM_`/`COR_` de valores nacionales, perfil, registro, canales, listas y solicitudes del titular; `PAY_PayrollLoanDeductions`; `INV_LendingObligationResponses` | Motor completo (M1–M16), `MotorDeCalificacion`, deterioro del Anexo 1, `SelectorDeModeloDeDeterioro` (siempre Anexo 1 con marca), `EnrutadorDeAtribuciones`, `EvaluadorDeVotacion`, `ReglasDeSegregacion`, `VentanaLegalDeContacto`, `VigenciaBitemporal`; `ProcesadorDeCartera` y tareas de K1; bandeja de varios emisores, registro, destinos, consumidor; cinco puertos ofrecidos y enmiendas E-01 a E-05; adaptadores con `SIMULADO`; dobles de prueba | Retiro primero (T88). Sale en vivo sólo con Recaudo y Tesorería reales y, si la cooperativa es obligada, K6a |
| **K2 · Libranza** | `CarteraLibranza` | `LND_PayrollAgreement*`, `LND_PayrollDeductionFile*` | `NovedadesDeLibranza`, `FlatFileReader`, ámbitos 5 y 6, `DescuentosPorPagaduria` y los mensajes de vuelta, `ConsumePayrollDeductionResultCommand`, tareas `cartera.archivos-de-descuento` y `cartera.giros-de-pagaduria` | Convenio de la nómina propia y sus no aplicados (FR-115) |
| **K3 · Novedades** | `CarteraNovedades` | `LND_LoanNovelties`, `LND_Waivers`, `LND_WriteOffBatch*`, `LND_PortfolioEvaluation*` | Regeneración del plan por novedad, novación con crédito enlazado, condonación, castigo, evaluación de cartera con actas; emisión de `CreditoReestructurado`, `…Modificado`, `…Novado`, `CondonacionAprobada`, `CreditoCastigado` | Hasta K3, la recogida de un crédito en mora responde «requiere reestructuración» |
| **K4 · Cobranza** | `CarteraCobranza` | `LND_MessageCampaigns`, `LND_Collection*`, `LND_CollectorAssignments`, `LND_PaymentPromises`, `LND_PaymentAgreement*`, `LND_LegalProcess*` (avisos y reclamos ya existen desde K1) | `RepartoDeGestores`, pasos de cobranza en el ciclo diario, aviso previo con constancia, reclamos con aceptación tácita, `GastoDeCobranzaCausado`, SMS y WhatsApp reales con contrato | — |
| **K5 · Informes** | `CarteraInformes` | `LND_RegulatoryFiles` (y `LND_IssuedDocument*` si no se adelantaron) | Motor de layouts, `ProyeccionSicsesCartera`, `ProyeccionCentralDeRiesgo`, extracto y certificado tributario, vistas de calidad, cosechas y concentración, tablero | SICSES y centrales los coteja el dueño |
| **K6a · Pérdida esperada** | `CarteraPerdidaEsperada` | `LND_ImpairmentModelElections`, `LND_ArrearsHistoryLoads` | `MotorDePerdidaEsperada`, `FuncionLogistica`, `HomologacionDeCalificacion`, documento `SarcAnexo2`, adopción y desistimiento voluntarios, carga complementaria del historial, `EntregaVigente = K6a` | Se adelanta antes de la primera cooperativa obligada; ningún hecho nuevo |
| **K6b · Vivienda** | `CarteraVivienda` (sólo si hace falta) | Columnas UVR | Valores `UVR` y `TOPE_TASA_VIVIENDA`, reglas de la Ley 546 como `RulesVersion` nueva, E1/E2 de vivienda por tiempo en E | Hasta K6b, vivienda no se origina ni se migra y queda fuera del cuadre |

### Lo que esta feature no hace (para que nadie lo agregue por su cuenta)

- Un recaudo, una caja o una confirmación de giros provisionales (Q1): sólo dobles de prueba fuera de producción.
- Ahorros, depósitos, CDT y aportes como productos; sólo se consulta el saldo de aportes (provisional por plantilla,
  D19) y se muda su código a `Savings` sin cambiarlo.
- Las cuotas de servicios a asociados (Q3, D29) no son cartera: Comercial las vende a crédito con
  `CreditWithoutLending`. Ese dato de Comercial lo construye esta feature **sólo si el dueño aprueba la enmienda A6**;
  si no, es un ajuste de la 012, fuera de esta feature y precondición de activación (T95).
- Asientos contables desde Cartera, cuentas en Cartera o lecturas de tablas `ACC_`.
- Un portal del asociado, verificación pública de documentos o webhooks entrantes (rutas anónimas).
- La gestión SARLAFT (segmentación, monitoreo, UIAF); sólo la consulta de listas y su carga provisional.
- Generalizar el motor de aprobaciones de la 012 o reescribir los cargadores de Nómina para leer valores nacionales
  (Nómina conserva una proyección que sólo escribe la réplica, enmienda A7).
- Un catálogo de modalidades por cooperativa, tablas `CMP_` nuevas o reutilizar cualquier nombre de tabla heredado.

---

## 7. Conciliación de los artefactos del plan (C01..C102, 2026-10-05)

Cada artefacto del plan cerró con una lista de «puntos para conciliar»: lecturas que hizo donde este documento no
decía lo bastante, o choques con un artefacto hermano. Aquí se decide cada uno. **Esta sección enmienda las T, CH y
nombres de la §2 que cita**: donde el texto de una T diga otra cosa, rige la decisión de aquí, y las tablas de la §2
ya llevan los nombres que agrega. Los artefactos reemplazaron su lista por una remisión a estas decisiones.

Siglas de origen: `RS1`/`RS2` = research (primera y segunda mitad), `DM1`/`DM2`/`DM3` = data-model (partes 1 a 3),
`QS` = quickstart, `API`, `MSG` = mensajes, `INT` = interfaces (`P1..P19`), `CON` = contabilidad, `ADA` =
adaptadores y archivos, `PLT` = plantillas. «Acepta» = rige la lectura que el artefacto aplicó.

### 7.1 Mensajes, unidades y orígenes

| # | Decisión | Enmienda | Origen |
|---|---|---|---|
| C01 | **Unidad de emisión = una operación y su modo.** Los acompañantes de un hecho principal forman una lista fija por tipo (`mensajes.md`) y **sellan el modo del principal**; `Contabilidad.ModoDePaso` no se ofrece en el ámbito `LendingFactType` para un acompañante. Cartera no conoce tipos de comprobante: Contabilidad resuelve **un comprobante por unidad** a partir del tipo del hecho principal. Se descarta la clave adicional `Confirmation:{uuidv5}` por grupo (modo, comprobante). | T12 | RS1-3, MSG-6, CON-3 |
| C02 | **Hechos por crédito que nace un proceso** (`AnticipoAplicado`, `CausacionSuspendida`, `CausacionReanudada`, `DimensionContableCambiada` del ciclo, `SaldoAFavorReconocido` de la corrida): origen = la parte de su tanda (`LND_AccountingFactParts`, que deja de ser sólo «parte de un hecho masivo»), modo propio del tipo, clave `Confirmation:{loanPublicId:N}`. | T12, T14, §2.3 | RS1-2, DM2-5, MSG-4 |
| C03 | **`AjusteDeCausacionPorFechaValor`**: emitido por la corrida `NationalValueCorrection`, va por partes (origen la parte, `PorLotes`, `CA`, clave `Confirmation:{partePublicId:N}`) y entra a la lista de masivos de T14; provocado por una operación fechada atrás, es acompañante de esa operación. «`Related` = operación» de la §2.11 se lee «origen = la operación»; la causa va en `causeFact`. | T14, §2.11 | RS1-2, MSG-5 |
| C04 | **Puesta al día de una operación**: la causación que la operación pone al día crea sus propias partes en `LND_AccountingFactParts` (una por tipo, la operación como tanda), origen la parte, clave `Confirmation:{partePublicId:N}`, modo de los masivos; sus `LoanTransaction` llevan ese `MessagePublicId`. | T6, T14, T25 | RS1-7 |
| C05 | **La corrida `NationalValueCorrection` la encola la réplica**, no el ciclo: un `IEfectoDeReplicacion` de Cartera (`EfectoDeReplicacionCorreccionDeValorNacional`) inserta en la misma transacción una corrida `Requested` cuando llega una fila con `SupersedesPublicId` que cubre días ya causados, sólo si Cartera está activada. | T28, T39 | RS1-1 |
| C06 | **`ScheduleKey`** = `CAR.{ShortCode}|{disparador}|{hora}|{granularidad}`: `LND_FactTypes.ShortCode` (3 a 4 letras, único; p. ej. `ICA`); la hora va vacía en `FinDeCausacion` y `CierreDeCartera`; el disparador `Hora` del parámetro se sella como la constante `HoraDiaria` de `ClavesDeLote` (012). | T15, §2.20 | RS1-4, CON-6, CON-7 |
| C07 | **Granularidad `PorHecho`** se suma a `PorDia` y `PorDiaYSucursal` (FR-128). La granularidad va en la `ScheduleKey`; `COR_IntegrationBatches.Granularity` sigue con `PostingGranularity` (resumido). | §2.20 | CON-5, DM3-20 |
| C08 | **Reversos heredan el modo de su original** (`DesembolsoReversado`, `PagoReversado`, `ReversoDeCierre`): un reverso nunca pasa sin su original ni espera a uno que no pasa. | T56 | CON-4 |
| C09 | **Alcance de lote `FinDeCausacion`**: un hecho nacido fuera de la corrida toma el día siguiente al de su registro, y el lote recoge también los alcances anteriores sin lote. «Clave fecha contable + tipo» de T56 = tipo de **comprobante**. | T56 | CON-8 |
| C10 | **Orden pagada por archivo**: la orden nace **al desembolsar** con `PaidByFile = true` en `OrdenDeGiroEmitidaV1`; la referencia al archivo queda en la orden de Cartera y el archivo no viaja en ningún mensaje. Anular un archivo lleva sus órdenes a otro (T78). Se retira `DisbursementFilePublicId` del contenido. | T12, T78 | INT-P6, MSG-7, ADA-1 |
| C11 | **Reverso de un desembolso que recogió créditos**: si el crédito se anula, el reverso reversa en la misma transacción los pagos de recogida (claves `CAR:`) y los recogidos vuelven a su estado; si queda pendiente de un giro nuevo (`PendingDisbursement`), se conservan. | T33 | INT-P6, DM1-9 |
| C12 | **Clave de `AnulacionDeOrdenDeGiro`**: `Confirmation:{ordenPublicId:N}`; `related` es la orden como documento (`DocumentRefV1`), y el mensaje de la orden va en `orderMessagePublicId` del resultado. | T12 | MSG-17 |
| C13 | **Los campos de giro** (`OrdenDeGiroEmitida`, `AnulacionDeOrdenDeGiro`, `ResultadoDelGiro`, `PagoReversadoPorCartera`) los fija `interfaces.md`; `mensajes.md` remite. | — | INT-P14 |
| C14 | **Recaudo sin persona**: se mantiene T11 (`PagoAplicado` y acompañantes **no** admiten proceso). La feature 014 aplica cada pago automático a nombre de la persona que cargó o aprobó el lote (débito automático, respuesta de pagaduría). Queda como restricción de la 014. | T11 | MSG-3 |
| C15 | **Cuentas contingentes (A3, propuesta por defecto)**: tipos `CreditoAprobadoSinDesembolsar` (9115), `CupoRotativoOtorgado` (913005) y `GarantiaConstituida` (9110), con signo, `Business`, modo `NoPasa`, comprobante `CN`, **`AdmiteProceso = true`** (el vencimiento de una aprobación o de un cupo lo detecta el ciclo), clave `Confirmation` sobre su operación, K1; dos roles de cuenta (`ContingenteDeudora`, `ContingenteAcreedoraPorContra`). El catálogo pasa de 44 a 47 filas. Si el dueño rechaza A3, se quitan. | §2.11, T11 | MSG-2, CON-13 |
| C16 | **Versión no aceptada (FR-053)**: `DestinoCartera.Acepta` admite toda versión de la familia de la venta y el consumidor responde `ObligacionRechazada`. Es enmienda a la 012 §12 (lista de T97). | T17, T97 | MSG-9 |
| C17 | **`ObligacionCreada` también en un reemplazo**: se envía al consumir un `AjusteDeVentaACredito` `Replacement` (FR-054, FR-057). | T18 | MSG-10 |
| C18 | **`ValidationFailed` directo** desde `Pending`/`InBatch`: enmienda a la tabla de estados de la 012 (`mensajes.md` §11), en la lista de T97. | T17, T97 | MSG-12 |
| C19 | **`DescuentosPorPagaduria` con líneas estructuradas** (`DeductionFileLinePublicId`, documento, obligación, valor), además de adjunto, SHA-256 y totales. | §2.11 | MSG-13 |
| C20 | **Reserva desconocida en una v2**: no rechaza; queda como evaluación y la obligación nace con otro número (`numberSource = Assigned`). `Lending.Reservation.NotFound` queda para las rutas de reserva. | §2.17 | MSG-14 |
| C21 | **Vocabulario nuevo aceptado**: `MotivoSinComprobante.NotIncorporated` (aditivo en la 012), la alerta `Cartera.ReservaVencida` para la reserva vencida tomada por una v2, `PrevalidationOutcome.NotPostable`, `NoVoucherReason.SameAccounts`, la alerta `Cartera.HechoNoContabilizable`, el adjunto `RegulatoryIndicatorSupport`. | §2.6, §2.18, §2.24 | MSG-15, MSG-16, CON-14 |
| C22 | **Contenido que `mensajes.md` debe traer para Contabilidad**: por importe de cargo, si es por cuenta del asociado; combinaciones anterior y nueva en traslados; el movimiento del deterioro; forma del desembolso, quién registró la cuenta por cobrar y la factura; el estado de castigado; en condonaciones y castigos, cuánto sale contra el deterioro. | §2.11 | CON-16 |
| C23 | **Usuario de origen del proceso**: el nombre del actor que ya existe, `Actor.NombreDelProceso` («Proceso de integración»); `contabilidad.md` lo usa en «registrado por». | T8 | MSG-8 |
| C24 | **Orden global de cerrojos en la venta a crédito**: filas de Inventario → `ReservarAsync` (`LND_Setup` compartido → `LND_DebtorControls` → numerador de Cartera) → numerador de Inventario. Ninguna operación de Cartera toma cerrojos de Inventario. Se mide en la prueba de volumen del POS. | T22 | RS1-5, INT-P8 |
| C25 | **La reserva corre bajo un punto de guardado propio** dentro de la transacción de Comercial y responde `NoResponse` ante una falla técnica (D26), en los dos motores. | T22 | INT-P9 |

### 7.2 Puertos y contrato de obligaciones

| # | Decisión | Enmienda | Origen |
|---|---|---|---|
| C26 | **Propiedades de puerto en inglés** (regla de la 012): `MigratedNotActivated` (y `MigratedNotActivated` en `CuotasDeNominaDto`), no `MigradaSinActivar`. | §2.9 | INT-P1 |
| C27 | **`IDimensionesDeCartera` vive en `Common.Integration.Lending`**, como los otros cuatro puertos ofrecidos; son **cinco** (T90). | §2.2, §2.25 | INT-P2, QS-10 |
| C28 | **`ResultadoDelContrato` suma `Answered`** para listar y simular (no se sobrecarga `Applied`). | §2.7 | INT-P3 |
| C29 | **Rol de saldo en el contrato**: `CombinacionDeCartera.Role` lleva el nombre del rol de `RolesDeCuentaDeCartera` como texto; `contabilidad.md` publica los roles de saldo que admite. | §2.9 | INT-P4 |
| C30 | **Medios `AssociateCredit` y `CustomerCredit`** se declaran y el contrato los rechaza (`Lending.Payment.MeansClassNotAllowed`): pagar crédito con crédito es recogida o novación. | §2.17 | INT-P5 |
| C31 | **Consulta de cupo** verifica listas **sin escribir** (evidencia en la auditoría); el resultado se guarda en la reserva y al consumir la venta. | T70 | INT-P7 |
| C32 | **Cuota de nómina en K1** = la del período; lo no descontado sigue exigible, alertado y en cobro por caja; desde K2 decide el convenio de la nómina propia. La ordinaria aplica `CycleInstallment` por lo descontado y reporta `NoDescontado`. | T92 | INT-P10, INT-P19 |
| C33 | **Vigencia de los indicadores regulatorios**: Contabilidad devuelve el último registrado con su corte y Cartera decide con `Contabilidad.IndicadoresVigenciaMeses`. | T59 | INT-P11, CON-10 |
| C34 | **`LaCarteraSoloSeTocaPorSusInterfaces` vigila las dos direcciones**: nadie depende de Cartera salvo por `Common.Integration.Lending`, y Cartera sólo depende de otros módulos por los puertos que consume. | §2.25 | INT-P12 |
| C35 | **Códigos de participante sembrados**: `NOM` (Nómina), `CAR` (recogida de Cartera, recaudador sin fila), `TST` (dobles de prueba), `CNT`, `INV`; claves derivadas `:reverso` y `:destino`. La clave del recaudador de prueba es `TST:{guid:N}`, la pantalla la propone y deja repetirla (para probar idempotencia). Se retira `TEST:`. | T16, T21 | INT-P13, MSG-11, MSG-18, QS-4 |
| C36 | **Registro de participantes**: columnas `Destination` e `IsEnabled`; único `(Code, Role, Destination)`; `RegisterIntegrationParticipantCommand` lleva `destination`. `Lending` se siembra inactivo. | §2.5, §2.12 | MSG-11, DM3-11, MSG-18 |
| C37 | **Pagos de Aportes**: hasta que la feature de Aportes declare su destino, un pago `Contributions` no se reversa desde Cartera (`Lending.Reversal.OwnedByCollector`), como los de Nómina. | T21 | INT-P15 |
| C38 | **`SimularAsync` admite `PaymentIdempotencyKeyToReverse`** para mostrar la redistribución antes de un reverso (FR-081). | §2.9 | INT-P16 |
| C39 | **«La distribución cambió»** rige sólo para la forma interactiva; la no interactiva espera el cerrojo y aplica sobre el estado nuevo. | T32 | INT-P17 |
| C40 | **Mensaje no convertible**: queda `Pending` con motivo y «para reproceso» (FR-053). | T17 | INT-P18 |
| C41 | **`Lending.Payments.BackdateBeforeClose` en el contrato**: el contrato consulta los permisos y el límite del usuario que actúa (no su alcance) por `IPermissionChecker`; lo describe `interfaces.md`. | T92, D16 | API-9 |

### 7.3 Modelo de datos

| # | Decisión | Enmienda | Origen |
|---|---|---|---|
| C42 | **Hechos (H) más allá de la §2.3**: `CreditLineVersion` y sus cuatro hijas, `CommercialTermEquivalence`, `CreditApplicationCheck`, `ScreeningResult` (liberación `[EscrituraUnica]`), `BureauQuery`, `CreditLimit`, `DebtorBlock` (levantamiento `[EscrituraUnica]`), `ContributionBalanceLoad`, `AttributionMatrix` con niveles y reglas, `DisbursementFile` (anulación `[EscrituraUnica]`), `LND_GoverningBodyRuleVersions`, `LND_DisbursementOrderResults`, `LND_MonthlyCloseGeneralImpairments`. **No** es hecho `LND_InboundMessages` (reprocesable); sí sus evaluaciones. | §2.3 | DM1-1, DM2-2, DM2-3 |
| C43 | **`LND_RepaymentInstallments` en `bigint`**. | §2.3 | DM1-2 |
| C44 | **Tablas nuevas**: `LND_GoverningBodyRuleVersions`, `LND_DisbursementOrderResults`, `LND_LoanPayrollLinks` (K1, fuente única de pagaduría y forma de pago con vigencia; `LND_Loans` no repite la pagaduría; el traslado de pagaduría de K2 es una operación de libranza con vínculo nuevo, no una `LoanNovelty`), `LND_ProvisionalCollections` (K1, `[EscrituraUnica]`), `ADM_NationalValueCounters` (contador con cerrojo de la `Sequence` de la central). Las cifras de la §2.3 se recuentan en `data-model.md`. | §2.3, §2.5 | DM1-3, DM2-1, DM2-7, DM2-8, DM3-1, PLT-1 |
| C45 | **`RegistroDeCartera`** escribe también `LND_LoanOperations`, `LND_LoanTermsVersions` y el plan; `NadieEscribeElLibroDeCarteraFueraDelRegistro` las vigila. | §2.9 | DM1-4 |
| C46 | **`[SinDiffDeAuditoria]`** además de posiciones, líneas del cierre y errores de corrida: `LoanControl`, `DebtorControl`, `LND_AccountingFactParts`, `LND_LoanArrearsHistory`, `LND_PayrollDeductionFileLines`, `LND_RegulatoryFileLines` y las 17 de preparación, cada una con su **evento resumen** por corrida, archivo o importación. Entra a T101 y a Complexity Tracking. | T67, T101 | DM1-5, DM2-4 |
| C47 | **FR-067 «registra la nueva tasa»** = operación `RateReset` (numerada, hecho) con su versión de condiciones, sin filas de libro. | — | DM1-6 |
| C48 | **Numeración**: prefijo del acta en `LND_GoverningBodies`, consecutivo en `LND_NumberSequences` (`SessionMinutes`); se suman las clases `Application` y `DisbursementOrder`. `NumeradorDeCartera` es el único que numera. | §2.3 | DM1-7 |
| C49 | **Reverso de un desembolso con varios tramos**: se espeja **sólo el capital del tramo**; la diferencia de causación la produce el recálculo desde la fecha valor del tramo como `AccrualAdjustment` (mismo mecanismo que lo fechado atrás). No se espeja la causación de los demás tramos. | T33 | DM1-8 |
| C50 | **Índice único de la causación** filtrado a `Kind = Accrual` y `ReversesTransactionId` nulo. | T21 | RS1-6 |
| C51 | **Dimensiones del libro**: la conciliación toma modalidad, sub-segmento, oficina y categoría del estado del crédito al corte; cambiarlas exige `DimensionContableCambiada`. La oficina sólo cambia por novedad de K3. | T58 | DM1-10, DM1-14 |
| C52 | **`LendingDecisionSubjects` suma `RiskPolicyVersion`** (T45 liga la política SARC a un punto de acta). | §2.7 | DM1-11 |
| C53 | **Estados calculados** («aprobación vencida», «bloqueada por listas») no se guardan. `PendingDisbursement` = crédito nacido cuyo giro se reversó; lo aprobado sin desembolsar es la solicitud `Approved`. | — | DM1-12, DM1-15 |
| C54 | **Cupo rotativo con `CreditLineId` nulo** vale para toda línea de origen comercial aplicable. | — | DM1-16 |
| C55 | **Novedad de Cartera en la ordinaria**: una `PAY_Novelties` `LoanDeduction` por empleado, obligación y período, que el recálculo reemplaza y el descarte del borrador anula (patrón del materializador de la 006). | T92 | RS2-2 |
| C56 | **`MotivoDeNoDescuento` suma `DeductionPriority`** (FR-115), en el puerto y en el dominio, antes de fijar `IContratoDeObligaciones` v1. | §2.7 | DM2-9 |
| C57 | **Destino copiado en el evento `Sent`** (`LND_DebtorMessageEvents.DestinationAddress`), no en el mensaje, que no cambia tras nacer. | T74 | DM2-10, ADA-3 |
| C58 | **Reapertura de un cierre = corrida** (`CloseReopen`): `ReopenMonthlyCloseCommand` responde 202 y la transacción única de T47 ocurre en la corrida; crea su propio lote `LendingMonthlyClose` con `BatchScopeKey` `LendingClose:{aaaa-mm}`. | §2.10, T47 | DM2-6, API-4, DM3-16 |
| C59 | **Clases de corrida**: `DailyCycle`, `MonthlyClose`, `CloseReopen`, `NationalValueCorrection`. | T5 | RS1-9 |
| C60 | **Activación en una transacción, con guardados por tandas**: `ActivateLendingCommand` abre una transacción explícita y guarda por tandas de 1.000 créditos con `ChangeTracker.Clear()`; el destino `Lending` se habilita en la misma transacción. Prueba de volumen con 50.000 créditos bajo `RUN_PERF_TESTS`. | T84 | DM2-17 |
| C61 | **Acta de activación obligatoria** (`LND_Activations.SessionItemId` no nulo): la spec la nombra (D5). | T84 | DM2-16 |
| C62 | **Cobros provisionales**: la activación valida y guarda `LND_ProvisionalCollections` (con la revisión previa que deja un adjunto); el consumidor de Comercial los aplica al nacer la obligación, con clave idempotente. | T84 | DM2-12, PLT-1, API-16 |
| C63 | **FR-143 «anterior a la activación» = anterior al corte**. Una venta fechada hasta el corte que llega después de activar queda `NotIncorporated` con `ObligacionRechazada` no reprocesable (`Lending.Sale.NotIncorporated`) y alerta (propuesta; pregunta **I16**). | T84 | DM2-11, DM2-21 |
| C64 | **Migración**: castigos con soporte histórico como dato (`HistoricalApprovalReference` / `soporteDelCastigo`, sólo en la preparación; T64 rige lo decidido en el ERP); reportes vigentes en centrales a `LND_PriorNotices.MigratedReportedNegativeSince`; etapa de cobranza conservada en la preparación y registrada al desplegar K4 (paso de despliegue en `tasks.md`); reclamaciones al seguro e insolvencias viajan en la parte `blocks` (motivos `Deceased`, `Insolvency`); varios archivos por parte (`LND_MigrationLoadParts`, una fila por archivo). | T64, T83, T84 | DM2-13–15, PLT-2, PLT-6, PLT-8 |
| C65 | **Numeración de enumeraciones**: la fija `data-model.md` §9 (parte 3); las partes 1 y 2 la usan. | §2.7 | DM2-19, DM3-9, PLT-4 |
| C66 | **FK circulares evitadas**: el cierre base apunta a la activación y la conciliación de ventas a su diferencia. FK de `CarteraNucleo` hacia tablas de riesgo las crea `CarteraRiesgo`. | §2.26 | DM2-20, DM3-17 |
| C67 | **Columnas de la venta**: `INV_DocumentPayments.ReservationPublicId` y `.ConsultationEvidence` (en `ComercioConCartera`). | §2.6, §2.26 | RS2-1, DM3-10 |
| C68 | **Arreglos de plataforma que descubrió el modelo**: `AdminDbContext.SaveChangesAsync` llama `GuardaDeInmutabilidad`; `COR_NotificationTemplates` cambia su único sin filtro por uno versionado; los únicos de `CMP_HabeasDataPolicyVersions` se rehacen con `Purpose`; `PAY_Holidays.NationalValuePublicId`; `COR_Alerts` suma oficina y línea (alcance, SC-015); los tipos de alerta reutilizados reciben destinatarios por una versión nueva de semilla con filtro por módulo de origen. | §2.6 | DM3-2–7 |
| C69 | **FR-015 «se alerta a quien lo registra»**: la central calcula, sin leer cooperativas, qué códigos esperados no tienen vigencia próxima y lo muestra en `/admin/valores-nacionales`; la cooperativa recibe `Cartera.ValorNacionalFaltante`. | T41 | DM3-8 |
| C70 | **Matriz contable sin filas sembradas**: la plantilla y la completitud presentan cada combinación sin cuenta como «pendiente de validar». | T52 | DM3-12 |
| C71 | **Códigos de error y límites que la §2 no nombraba** (activación, carga, cierre, contrato antes del corte, recogida con mora, concentración, línea cerrada, firmante, solapes; 17 códigos `LIMITE`, unidad `Veces`, límites de cobranza de inicio, sábado y contactos por día): los fija `data-model.md` §10 y §12; `api.md`, `quickstart.md` y `adaptadores-y-archivos.md` los usan literales. `Libranza.UmbralDeMoraDePagaduria` queda sin valor hasta cotejar la CBCF antes de K2. | §2.17, §2.21 | DM3-13, DM3-14, QS-3, ADA-4 |
| C72 | **`Lending.Regulatory.LayoutSinCotejar`** se conserva por el precedente `Pila.LayoutSinCotejar`. | §2.17 | DM3-15 |
| C73 | **Copia de `PAY_Novelties`** se conserva por simetría (no debería encontrar filas). **Copia de datos en `NominaRecaudaPorElContrato`**: transformación idempotente de datos existentes, no semilla; el Constitution Check de `plan.md` lo declara. | T87 | DM3-18, RS2-10 |
| C74 | **Destinatarios por defecto de las alertas** cotejados con `PerfilesSugeridos` (que fija `data-model.md`). | §2.18 | DM3-19, QS-8 |
| C75 | **Votos**: un voto mal registrado se corrige con un voto de reemplazo que referencia al anterior; único filtrado a votos no reemplazados; `EvaluadorDeVotacion` cuenta el vigente. | T64 | RS2-8 |
| C76 | **Marca de parámetros sin validar**: `UnvalidatedParameters` (columna en inglés). | T41 | RS1-8 |

### 7.4 Lado contable

| # | Decisión | Enmienda | Origen |
|---|---|---|---|
| C77 | **Hecho `*` en la matriz**: obligatorio en roles de saldo, orden, pasivo, puente, diferido y contingentes; opcional con peso 2 en los de resultado. **Centro de costo** se asigna a la línea y no se compara (no pesa ni entra en `DimensionKey`). | T52 | CON-1, CON-2 |
| C78 | **Pérdidas por clase 3 y naturaleza débito**, no por código (respeta `LaContabilidadNoTieneCuentasEnCodigo`). | T59 | CON-9 |
| C79 | **`DN` también para `RecuperacionDeCastigada|Payroll`; cruce `FV` en `CuentaPorCobrarComercial` y `PuenteComercial`**. | §2.19 | CON-11, CON-12 |
| C80 | **`FaltaDeRegla` gobierna todo hallazgo de la evaluación**; `PoliticaSinRespuesta = Bloquear` también se limita a las operaciones de pantalla (A8). | T56 | CON-15 |
| C81 | **Suspenso tras el castigo** va a `CastigadaEnOrden` y sale de `SuspensoEnOrden` al castigar (pendiente de validar, C7). | T30 | CON-17 |
| C82 | **Carpeta común** `Application/Accounting/Integration`. **Plantilla de valores nacionales**: `ImportNationalValuesCommand` reutiliza definición, lector y reglas del ejecutor con su propia transacción sobre `IAdminDbContext`. | T1, T82 | CON-18, PLT-9 |

### 7.5 Seguridad, rutas y pantallas

| # | Decisión | Enmienda | Origen |
|---|---|---|---|
| C83 | **`asOf`**: en `/loans/*`, `/risk/ratings` y `/api/core/national-values`, `date` = D y **`recordedAsOf`** = R; en informes y tablero, `asOf` = fecha de corte (como la 012). | §2.15 | API-1 |
| C84 | **Vistas con datos personales**: `Lending.Reports.View` más `Lending.Loans.Read` para consultarlas y `ExportPersonalData` para exportarlas. Coincidencias en listas: lista y detalle exigen `Compliance.RestrictiveLists.Release`. Consulta a centrales: `POST /bureau-queries` devuelve sólo el estado; el contenido sale por `ReadReports`. Nombres bajo `View` (órganos, pagaduría): nombre, cargo y contacto institucional, nunca documento. | T60 | API-2, API-10, API-12, API-19 |
| C85 | **Permisos nuevos**: `Lending.PrepaymentPreferences.Manage` (K1, plantillas asesor y jefe) para el cambio de preferencia de prepago. Se reutilizan: `Lending.CreditLimits.Assign` para la plantilla de saldos de aportes; `Compliance.HabeasData.ViewHistory`/`RecordConsent` para `/cartera/autorizaciones`; `Lending.Reconciliation.View` para el cuadre de una carga; `Lending.RegulatoryFiles.Read` para la lista de layouts. Condonaciones: ruta `/api/lending/waivers`, lectura con `Loans.Read`. | §2.14 | API-5, API-6, API-11, PLT-3, RS2-9, ADA-8 |
| C86 | **`cartera.administrador` excluye `Lending.Decisions.*` y `Lending.Sessions.Vote`**, como `CompanyAdmin`: administrar no es aprobar crédito. | T61 | API-20 |
| C87 | **Envío de la simulación**: `POST /simulations/schedule/send` admite a la persona simulada con autorización y correo, sólo por correo y propósito `Informative`. | T74 | API-3 |
| C88 | **Entregas de rutas**: avisos previos y reclamos se leen desde K1 y se escriben desde K4; no aplicados de la nómina propia se ven en K1 en el estado de cuenta y la alerta, lista y resolución en K2; completitud del historial de mora desde K1; documentos en K1 (I5: plan de pagos, paz y salvo, certificado de saldo; `LND_IssuedDocument*` en `CarteraNucleo`), extracto y certificado tributario en K5 (`Lending.Document.KindNotAvailable`). Rutas del cierre: ordenar y preparar por `{year}/{month}`, detalle por `{id}`. | §2.15, §2.26 | API-7, API-8, API-13–15, QS-9, ADA-9 |
| C89 | **Lecturas sensibles auditadas**: la lista cerrada de `research.md` §13; la liberación de coincidencia es `Lending.ScreeningMatch.Released`. **Auditoría de lo conservado sin A10**: `ModuloDeAuditoria` suma una regla por nombre que lleva los handlers de ahorros a `Savings` antes de la de `.Lending`. | T3, T67 | RS2-4, RS2-5 |
| C90 | **Adaptadores**: tarea `cartera.re-escaneo-de-listas` al cargar una versión de listas (alerta y bloqueo de originar y vender, no del cobro); `Letter` no exige autorización de canal, `Call` y `Visit` sólo son gestiones (K4); plantillas de mensaje con código de 10 (`CodigoDeCatalogo`); si el dueño acepta I6, adjuntos `LendingLogo` y `LendingSignatureImage`. | §2.13, §2.24, T74 | ADA-2, ADA-5–7 |

### 7.6 Pruebas, numeración y textos

- **Ubicación de pruebas** (QS-1, QS-2, QS-5, QS-6, QS-13): las baterías de conformidad de adaptadores van en
  `Application.Tests` y `TextosDeCarteraTests` en `Architecture.Tests/Principles`; `ReplicaSobreCooperativaSembradaTests`
  y `ValoresNacionalesYReplicaTests` van en la colección «Valores nacionales e2e» (la central es una por host);
  `RendimientoDeCausacionTests` corre siempre (motor solo); SC-005 de punta a punta se mide en QA y la e2e mide el
  consumo con pasada a mano; los dorados de layouts van en `Application.Tests` con la carpeta y variable que fija
  `quickstart.md`. Enmienda §2.25.
- **Lo que no se ensaya en DEV ni QA** (QS-7): lo que necesita valores ficticios en la central va en local y en las e2e.
- **Numeración de `research.md`** (RS2-12): R1–R61 y R101–R154; ningún artefacto cita R62–R100.
- **Conteo de llaves del retiro** (RS2-6): 40 (38 entre `LND_`, una a `COR_People`, una de `PAY_SettlementDeductions`).
- **`LND_SiplaGroupParameters.CreditLineId`** (RS2-7): queda requerida y sin FK; la decide la feature de SARLAFT.
- **`Payroll.Settlement.ConceptAccountsMissing`** (RS2-3, QS-11): **ya existe** en el código (`SettlementErrors`); la
  ordinaria usa el también existente `Payroll.ConceptWithoutAccounts`. No hay código nuevo: se listan en §2.17 como
  reutilizados.
- **`plan.md`** (RS2-11, DM2-18): las tres contradicciones (cadena por crédito, «no habilitada», nombre del actor)
  quedaron corregidas.
- **Referencias cruzadas** (API-18): los nombres de comandos, rutas y códigos que fija más de un contrato quedaron
  cruzados en la conciliación; ante duda, gana el contrato dueño del tema (`api.md` rutas y permisos, `interfaces.md`
  puertos, `mensajes.md` tipos y claves, `contabilidad.md` roles y matriz, `adaptadores-y-archivos.md` canales,
  layouts y documentos, `plantillas.md` columnas de importación, `data-model.md` tablas, enums y errores).

### 7.7 Lo que queda para el dueño o la contadora

Nuevas preguntas (se suman a la §5; si no responden, rige la propuesta):

| # | Pregunta | Propuesta por defecto | Bloquea |
|---|---|---|---|
| E11 | Si el primer vencimiento del formato de cartera del SICSES de COOFLOPAL llega antes que K5, ¿se adelanta la proyección del SICSES a K1? | Sí, sólo el formato de cartera, si la fecha lo exige | Salida en vivo de COOFLOPAL |
| I16 | Una venta fechada hasta el corte que llega después de activar, ¿queda fuera de Cartera (no incorporada, con alerta) o en espera reprocesable de una decisión? | Fuera de Cartera (C63) | Consumidor de ventas (K1) |
| I17 | Cobros externos por factura en la migración, ¿sumados por factura o uno por uno? | Sumados con su referencia | Plantilla `sales` (K1) |

Para validar por la contadora (se suman a C7, C11, E4 y E10): la tasa de mora como **factor** de la corriente
(`factorDeMora`, PLT-5), los defectos de parámetros de `data-model.md` (sobre todo `Cupo.MoraMaximaParaVender = 0` y
`Bloqueos.DiasDeMoraParaBloquear = 30`, DM1-17) y el centro de costo no comparable de la matriz (C77).

### 7.8 Residuos de la aplicación (cierre de la conciliación)

Al aplicar la §7, los artefactos dejaron estos ajustes finos; se deciden aquí y quedan aplicados.

| # | Decisión | Origen |
|---|---|---|
| C91 | **Roles contingentes según el CUIF**: `ContingenteAcreedora` (91xx, naturaleza crédito) y `ContraContingenteAcreedora` (9605, débito); reemplazan a `ContingenteDeudora` y `ContingenteAcreedoraPorContra` de C15. Tipos de hecho con los nombres de C15 y códigos cortos `CAD`, `CRO`, `GCT`. | contabilidad, mensajes |
| C92 | **913005 mide el cupo rotativo otorgado**: se mueve al otorgar, ampliar, reducir o vencer el cupo; no al usarlo ni al pagar. | contabilidad §19 vs mensajes |
| C93 | **Saldo a favor y deterioro**: `contabilidad.md` usa los valores de `mensajes.md` (origen `NoPrepaymentPreference`/`CommercialAdjustment`/`NationalValueCorrection`, resolución por `FormaDeResolucion`); el `reasonCode` del deterioro lleva el movimiento (`Increase`, `DecreaseSameYear`, `DecreasePriorYears`, `Transfer`) y el nivel (individual, general, adicional) sale de la línea. `SaldoAFavorReconocido` es acompañante de `PagoAplicado` y de `CreditoAjustadoDesdeComercial`. | mensajes |
| C94 | **Claves de contingentes**: movidos por otra operación, `Confirmation:{tipoDeHecho:N}` (no se mezclan con una unidad `EnLinea`); detectados por el ciclo, `Confirmation:{solicitud|cupo|garantía:N}` con la parte como origen. | mensajes |
| C95 | **Recibo de un mensaje no convertible (C40)**: `LND_InboundMessages` queda `Rejected` y reprocesable; la venta se informa `Pending` con su motivo y no se envía `ObligacionRechazada` hasta que un reproceso falle de forma definitiva o una persona lo marque no reprocesable. | research R9 |
| C96 | **Condición «empleado»** en tasas y documentos: `DebtorKind` no la lleva; la expresa la versión de la línea con `ForEmployees` (y la banda de tasa que la use). | api/plantillas |
| C97 | **`LND_MigrationRestructurings.Cause`** (`TermsVersionCause`: `Restructuring`, `Modification`) para la clase que pide la plantilla. | plantillas |
| C98 | **Códigos de error sumados a `data-model.md` §10**: `Lending.Migration.PartMissing`, `.BalanceMismatch`, `.HousingNotAvailable`, `.ArrearsMismatch`, `.EpisodeNotCurrent`; `Lending.ProvisionalCollection.ExceedsSale`; `NationalValues.ConflictingValue`; `Lending.WriteOff.RequirementMissing` (castigo sin requisito, K3). | plantillas, quickstart |
| C99 | **Alerta de la venta no incorporada (C63)**: `Cartera.VentaNoIncorporada`. | api |
| C100 | **Enumeraciones de `api.md` §27** reescritas con los nombres y números de `data-model.md` §9 (`CreditApplicationStatus` sin `Expired` ni `Blocked`, que son calculados, C53); `LendingDecisionSubjects` con `RiskPolicyVersion` (C52). | api |
| C101 | **Lecturas que quedan como estaban**: A10 sin respuesta rige la propuesta (mudanza a `/api/savings`, API-17); antes de activar, otorgar un cupo sin patrimonio técnico registrado responde `Lending.Concentration.TechnicalCapitalUnavailable` y el runbook pide registrarlo antes (API-21); adjuntos de I6 con `Lending.Parameters.Manage`; en `research.md`, el reverso del desembolso de un solo tramo sigue espejando la causación del crédito (C49 rige con varios tramos). | api, adaptadores, research |
| C102 | **Rechazo no reprocesable**: sólo lo declara una persona (`POST /api/lending/inbound-messages/{id}/mark-not-retryable`, `Lending.Messages.Reprocess`); ningún reproceso «falla de forma definitiva» por sí solo. Entonces nace `ObligacionRechazada` `NoConvertible` y `EstadoDeValidacionAsync` responde `NotIncorporated` (no hay crédito); `Failed` sigue significando «crédito creado con validación fallida». | interfaces, data-model |

Las decisiones de esta sección se escriben `C01`..`C102` (con dos dígitos o más); las preguntas de contabilidad de la
§5 se escriben `C1`..`C11`. `C10` decisión (orden pagada por archivo) y `C10` pregunta (`Contabilidad.DiferirComisiones`)
se distinguen por el contexto: en los artefactos, «decisión C10» o «pregunta C10».
