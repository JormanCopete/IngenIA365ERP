# Tareas: 028 — Recogida de créditos y productos en el desembolso

**Entrada**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md)
**Pruebas**: unitarias y de arquitectura con cada fase; las de punta a punta (contenedores) **una sola vez al final** (fase 13), en los dos motores.
**Construcción**: `dotnet build IngenIA365ERP.slnx -m:1`; a lo sumo dos sesiones compilando a la vez.
**Etiquetas**: `[US1]`…`[US8]` = HU1…HU8 de la spec. Entregas: **E1** = fases 1–7, **E2** = fases 8–9, **E3** = fase 10, **E4** = fase 11.
Rutas: `Dom` = `src/Core/IngenIA365ERP.Domain`, `App` = `src/Core/IngenIA365ERP.Application`, `Pers` = `src/Infrastructure/IngenIA365ERP.Persistence`,
`Mig` = `src/Infrastructure/IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/Application`, `API` = `src/Presentation/IngenIA365ERP.API`,
`Sh` = `src/Presentation/IngenIA365ERP.Shared`, `TD` = `tests/IngenIA365ERP.Domain.Tests`, `TA` = `tests/IngenIA365ERP.Application.Tests`,
`TR` = `tests/IngenIA365ERP.Architecture.Tests`, `TS` = `tests/IngenIA365ERP.Shared.Tests`, `TI` = `tests/IngenIA365ERP.API.IntegrationTests`.

**Antes de empezar**: anotar en `research.md` §3 las respuestas del dueño a D1–D13 y ajustar las tareas que nombran la decisión.

## Fase 1 — Preparación (E1)

- [X] T001 Enums `PayoffProductKind`, `PayoffMode`, `InstallmentTreatment`, `PayoffDifferenceCause` (banderas) y `SentInstallmentState` en `Dom/Enums/Lending/PayoffEnums.cs`
- [X] T002 [P] Siete parámetros de Cartera (R15) con sus defectos de hoy y sus textos de pantalla en `Dom/Lending/Parameters/ParametrosDeCartera.cs` (`Recogida.CuotaCausada`, `Recogida.InteresesProporcionales`, `Recogida.ProductosAdmitidos` con patrón, `Recogida.AlDesembolsar`, `Recogida.ToleranciaDeDiferencia`, `Recogida.GiroPosteriorDeCreditoRecogido`, `Pagos.CuotaEnviadaAlCancelar`)
- [X] T003 [P] Lector puro de `Recogida.ProductosAdmitidos` (`Producto:modo,modo;…` → mapa) en `Dom/Lending/Payoffs/ProductosAdmitidos.cs`, con casos en `TD/Lending/Payoffs/ProductosAdmitidosTests.cs`
- [X] T004 [P] Pruebas de que los defectos de los siete parámetros reproducen lo de hoy en `TA/Lending/Payoffs/ParametrosDeRecogidaTests.cs`

## Fase 2 — Fundamentos (bloquea todo)

- [X] T005 Entidades `CreditApplicationPayoff` y `DisbursementPayoff` (data-model) en `Dom/Entities/Lending/Payoffs/`
- [X] T006 Configuraciones `CreditApplicationPayoffConfiguration` y `DisbursementPayoffConfiguration` (`ComoEntidadDeCartera`, índice único filtrado de la proyectada, índice por `LoanId`) en `Pers/Configurations/Lending/Payoffs/`; `DbSet` en `App/Common/Interfaces/IApplicationDbContext*.cs` y el contexto
- [X] T007 Migración `RecogidaDeCreditos` en los dos motores con `tools/scripts/add-migration.ps1 -Name RecogidaDeCreditos -Context Application` (fechada después de `EsquemaBase`), con el SQL que copia `LND_CreditApplications.PayoffRequest` a filas `Loan`; `tools/scripts/check-migration-parity.ps1`
- [X] T008 Comparador puro de desgloses `DiferenciaDeRecogida` (por concepto, con la causa) en `Dom/Lending/Payoffs/DiferenciaDeRecogida.cs`, con casos en `TD/Lending/Payoffs/DiferenciaDeRecogidaTests.cs`
- [X] T009 `InteresesAlCancelar.ModoAlRecogerAsync` (R5: `ComoAlCancelar`/`Si`/`No`, excepciones legales) en `App/Lending/Common/InteresesAlCancelar.cs`
- [X] T010 `LiquidacionDeRecogida` (R3): reconstrucción a (D, R), modo de intereses, desglose, bloqueos (mora, castigo, insolvencia, sin saldo), huella; reemplaza `MotorDeCreditoEnDesembolso.LiquidarAsync` en `App/Lending/Payoffs/LiquidacionDeRecogida.cs` y `App/Lending/Disbursements/IMotorDeCreditoEnDesembolso.cs`; registro en `App/DependencyInjection.cs`
- [X] T011 [P] Pruebas de `LiquidacionDeRecogida`: tres modos de intereses × causación diaria, al inicio del ciclo y por período calendario; vivienda; tope del prepago; anticipo y saldo a favor, en `TA/Lending/Payoffs/LiquidacionDeRecogidaTests.cs`
- [X] T012 [P] Regla de arquitectura `LaRecogidaSeLiquidaEnUnSoloSitio` en `TR/Principles/LaRecogidaSeLiquidaEnUnSoloSitio.cs`
- [X] T013 `PayoffInput` (R1) y la traducción de `ownLoanPayoffs` en `App/Lending/Payoffs/PayoffInput.cs`; ambos cuerpos a la vez → `Request.BodyInvalid`

## Fase 3 — HU1: ver los productos y simular al vender (P1, E1) 🎯

**Objetivo**: el asesor ve los productos del asociado a (D, R), marca lo que recoge y ve el neto.
**Prueba independiente**: un asociado con dos créditos al día y uno en mora: la consulta devuelve los tres, el de mora no elegible; cambiar D
cambia el valor y el neto.

- [X] T014 [US1] `GetPayoffCandidatesQuery` + validador (créditos como deudor en el alcance, admitidos según la línea del crédito nuevo, aviso de otra solicitud viva, resumen con neto, cuotas liberadas y capacidad después) en `App/Lending/Payoffs/GetPayoffCandidatesQuery.cs`; audita `Lending.Payoff.Viewed`
- [X] T015 [US1] DTO `PayoffCandidatesDto` en `App/Lending/Payoffs/PayoffDtos.cs`
- [X] T016 [US1] Ruta `POST /api/lending/payoffs/candidates` (`Lending.Payoffs.Read`) en `API/Endpoints/Lending/PayoffsEndpoints.cs`
- [X] T017 [P] [US1] PDF de la simulación de recogida con el membrete, reusando el generador de la simulación de cancelación, en `API/Reports/SimulacionDeRecogidaReport.cs` y su ruta `/payoffs/candidates/pdf`
- [X] T018 [P] [US1] Cliente y DTO en `Sh/Services/Cartera/RecogidasClient.cs` y `Sh/Services/Cartera/CarteraDtos.Recogidas.cs`
- [X] T019 [US1] Componente `ProductosParaRecoger.razor` (tabla con marcas, modo, valor parcial, desglose desplegable, avisos, fecha proyectada, neto, botón de PDF; `IndicadorDeCarga`) en `Sh/Components/Cartera/ProductosParaRecoger.razor`
- [X] T020 [US1] Integrar el componente en `Sh/Pages/Cartera/SolicitudNueva.razor` (paso 2, después del simulador; la fecha esperada de desembolso alimenta D)
- [X] T021 [P] [US1] Pruebas de la consulta (elegibilidad, alcance, admitidos de fábrica = sólo créditos, otra solicitud viva, neto ≤ 0) en `TA/Lending/Payoffs/GetPayoffCandidatesQueryTests.cs`
- [X] T022 [P] [US1] Pruebas de textos y DTO del cliente en `TS/Cartera/RecogidasTests.cs`

## Fase 4 — HU2: radicar, corregir y decidir (P1, E1)

**Objetivo**: la solicitud guarda la proyección, valida después de recoger y el decisor la ve.
**Prueba independiente**: un ingreso que sólo alcanza liberando la cuota del crédito viejo pasa la capacidad con la recogida y no sin ella.

- [X] T023 [US2] `CreditApplicationInput.Payoffs` y `PayoffProjection` en `App/Lending/Origination/CreditApplicationDtos.cs`; persistencia de filas `CreditApplicationPayoff` (reescritura al guardar) en `App/Lending/Origination/CreditApplicationCommands.cs`; deja de escribir `PayoffRequest`
- [X] T024 [US2] Validación al guardar y radicar (del solicitante, vivo, admitido, modo admitido, parcial con valor, neto positivo) con los códigos `Lending.Payoff.*` en `App/Lending/Origination/ErroresDeOriginacion.cs` y el validador
- [X] T025 [US2] Validaciones después de recoger (R8): `ContextoDeValidacion.Recogidas` y su lectura en `App/Lending/Origination/ValidacionesDeOriginacion.cs`; `CapacidadDePago.CuotasPropiasAsync` con exclusiones en `CapacidadDePago.cs`; `ExposicionConsolidadaDelDeudor`, cupo (`IDisponibleDeCupo`) y `ValidacionDeConcentracion` con el capital recogido; detalle `releasedByPayoffs`
- [X] T026 [US2] `FotoDeSolicitud` lee las recogidas de las filas (grupo «Recogidas») en `App/Lending/Origination/FotoDeSolicitud.cs`
- [X] T027 [US2] DTO de la solicitud con `payoffs` y `payoffSummary`; `UsesAdvancedTerms` sin recogidas, en `App/Lending/Origination/CreditApplicationDtos.cs`
- [X] T028 [US2] `Sh/Pages/Cartera/SolicitudNueva.razor`: manda `Payoffs`, quita el bloqueo por recogidas (sigue para cuotas extraordinarias y gracia), abre la corrección con las recogidas guardadas y las deja editar si el grupo es editable
- [X] T029 [US2] Sección «Lo que recoge» (proyectado, total, neto, fecha) en `Sh/Pages/Cartera/SolicitudDetalle.razor` con `IndicadorDeCarga`
- [X] T030 [P] [US2] Pruebas de radicación con recogidas y de las cinco validaciones después de recoger en `TA/Lending/Origination/SolicitudConRecogidasTests.cs`
- [X] T031 [P] [US2] Pruebas de la foto y de la corrección al devolver con recogidas en `TA/Lending/Origination/CorreccionConRecogidasTests.cs`

## Fase 5 — HU3: desembolsar con recálculo y diferencias (P1, E1)

**Objetivo**: el desembolso toma las recogidas de la solicitud, recalcula, muestra diferencias y gira por lo aplicado.
**Prueba independiente**: proyectada el 20/11, pago en caja el 25/11, desembolso el 28/11: diferencia con causa «Pagos», neto mayor, orden por
el neto mostrado.

- [X] T032 [US3] `DisbursementInput.Payoffs` opcional y `AcknowledgedPayoffDifferences` en `App/Lending/Disbursements/DisbursementInput.cs`; validador en `ExecuteLoanDisbursementCommand.cs`
- [X] T033 [US3] `RequisitosDelDesembolso`: recogidas desde `LND_CreditApplicationPayoffs` (`MustMatchApplication`), liquidación por `LiquidacionDeRecogida`, comparación con `DiferenciaDeRecogida`, causa leyendo las operaciones del producto entre R y ahora, omitidos, `Recogida.AlDesembolsar`, `Recogida.ToleranciaDeDiferencia`, huella con diferencias, concentración sobre bruto − recogido, en `App/Lending/Disbursements/RequisitosDelDesembolso.cs`
- [X] T034 [US3] Vista previa con `payoffs[]` ampliado y `payoffDifferences` en `App/Lending/Disbursements/PreviewDisbursementQuery.cs` y `DisbursementDtos.cs`; emite `Lending.Payoff.Viewed`
- [X] T035 [US3] Comando: reconocimiento obligatorio (`DifferencesNotAcknowledged`), relectura bajo el cerrojo, defensa `AppliedDiffers`, orden por bruto − aplicado (H4), filas `DisbursementPayoff`, tramos siguientes sin recogidas, en `App/Lending/Disbursements/ExecuteLoanDisbursementCommand.cs`
- [X] T036 [US3] Recogida parcial de crédito como lo exigible + `ExtraordinaryPrincipal` con la elección del deudor (D7) en `App/Lending/Disbursements/RecogidaEnElDesembolso.cs`
- [X] T037 [US3] Validación contable previa que incluye los `PagoAplicado` de las recogidas (FR-027a) en `RequisitosDelDesembolso.HechoParaValidar` y su llamada
- [X] T038 [US3] Giro nuevo con las recogidas de la operación original y el mismo neto (H6) en `App/Lending/Disbursements/ReissueDisbursementOrderCommand.cs`
- [X] T039 [US3] Garantías y pagaré del recogido se liberan al quedar `Paid` la orden del crédito nuevo (D12, H7) en `App/Lending/Disbursements/Collaterals/GarantiasDeCartera.cs` y `App/Lending/Integration/ConsumeTreasuryResultCommand.cs`; texto «por liberar al girar el crédito …»
- [X] T040 [US3] Anulación: marca `ReversedAt`/`ReversalKey` en `DisbursementPayoff` en `App/Lending/Disbursements/VoidPendingLoanCommand.cs`
- [X] T041 [US3] Auditoría del desembolso con proyectado, actual, aplicado, diferencia, causa y quién reconoció en `ExecuteLoanDisbursementCommand.Distribucion`
- [X] T042 [US3] `Sh/Pages/Cartera/Desembolsos.razor`: quita el letrero; tabla de recogidas (proyectado / actual / diferencia / causa / omitido / cuota causada), casilla «Revisé las diferencias», neto real, resultado con lo aplicado por producto; `IndicadorDeCarga`; DTO en `Sh/Services/Cartera/CarteraDtos.Desembolsos.cs`
- [X] T043 [P] [US3] Pruebas del desembolso: recalcular, mantener lo proyectado, omitido, tolerancia, reconocimiento, `MustMatchApplication`, orden por lo aplicado, parcial a capital, tramos, en `TA/Lending/Disbursements/DesembolsoConDiferenciasTests.cs` (ampliar `RecogidaEnElDesembolsoTests.cs`)
- [X] T044 [P] [US3] Pruebas del giro nuevo y de la liberación diferida de garantías en `TA/Lending/Disbursements/GiroNuevoYGarantiasConRecogidaTests.cs`

## Fase 6 — Contabilidad del desembolso con recogidas (FR-027, E1)

**Objetivo**: el DS cuadra solo y el puente queda en cero.
**Prueba independiente**: desembolso de 10.000.000 que recoge 2.300.000: DS con «desembolso por girar» 7.700.000 y puente 2.300.000; el RC
debita 2.300.000 al mismo puente; saldo del puente 0.

- [X] T045 [US3] `PayoffDeductionV1` con `ProductKind`, `AccountRef`, `ReceivableRef` opcionales en `App/Common/Integration/Contracts/Lending/BloquesComunesDeCarteraV1.cs`; emisión en `App/Lending/Integration/EmisionDeCartera.Desembolso.cs`
- [X] T046 [US3] Constructor del DS: `DesembolsoPorGirar` por el neto y `RecaudoPorAplicar`/`OwnLoanPayoff` por cada recogida (tercero = deudor, cruce = producto) en `App/Accounting/Lending/Contabilizacion/ConstructorDeLineasDeCartera.cs`; sin recogidas, idéntico
- [X] T047 [US3] Parámetro `Accounting.LendingDisbursementWithPayoffsInOneVoucher` (defecto falso) y agrupación del DS con sus RC en `App/Accounting/Lending/Lotes/ComprobantePorRecibo.cs` y la definición del parámetro de Contabilidad
- [X] T048 [P] [US3] Casos dorados: actualizar `TA/Accounting/Lending/Casos/01-desembolso-por-transferencia.json`; nuevo `TA/Accounting/Lending/Casos/NN-desembolso-con-dos-recogidas-giro-a-tercero.json` (cuenta con cruce, un comprobante)
- [X] T049 [P] [US3] Prueba de conciliación: puente en cero y «desembolso por girar» = neto de las órdenes abiertas, incluida una orden rechazada, en `TA/Lending/Integration/SaldosContablesConRecogidaTests.cs`

## Fase 7 — HU5 e HU8: intereses proporcionales e informe (P2, E1)

- [X] T050 [US5] La recogida aplica el modo de `ModoAlRecogerAsync` sellado en la decisión del pago (`FullPayoff` con el modo) en `App/Lending/Disbursements/RecogidaEnElDesembolso.cs` y `App/Lending/Payments/AplicacionDePagos.cs`
- [X] T051 [US5] Explicación en el desglose («interés devuelto: N días», «se cobra el proporcional por el tope del prepago») en `LiquidacionDeRecogida` y en `ProductosParaRecoger.razor`
- [X] T052 [P] [US5] Pruebas de los escenarios de HU5 (1–3) en `TA/Lending/Payoffs/InteresesProporcionalesTests.cs`
- [X] T053 [US8] Vista `payoffs` (`TablaExportable`, filtros período, oficina, línea, asesor, producto; datos personales) en `App/Lending/Reports/Vistas/PayoffsReportQuery.cs` y su registro en el catálogo de vistas de Cartera
- [X] T054 [US8] «Recogido por el crédito …» y «recogió …» en el detalle del crédito (`App/Lending/Loans/` DTO y `Sh/Pages/Cartera/CreditoDetalle.razor`) y en el estado de cuenta (`App/Lending/PersonStatement/SeccionesDelEstadoDeCuenta.cs`)
- [X] T055 [US8] La vista en `/cartera/informes` (`Sh/Pages/Cartera/Informes.razor`) y su tarjeta
- [X] T056 [P] [US8] Pruebas de la vista en `TA/Lending/Reports/PayoffsReportQueryTests.cs`
- [X] T057 Puerta de E1: build `-m:1` sin errores; Domain, Application, Architecture y Shared verdes; commit de la entrega

## Fase 8 — HU4 (motor): todo menos la cuota causada (P1, E2) — riesgo principal

**Objetivo**: el motor puro cancela todo salvo la cuota causada, que queda tal como se envió y congelada.
**Prueba independiente**: casos dorados que cierran al peso cuando la cuota dejada se paga después.

- [X] T058 [US4] `ModalidadDePago.PayoffExceptCurrentInstallment = 8` en `App/Common/Integration/Lending/EnumsDelContratoDeCartera.cs`; admitida para `CAR` y `Receipts` en `App/Lending/Payments/ClasesDeMedioDeRecaudo.cs` y el validador del contrato
- [X] T059 [US4] Rama `CancelacionSalvoCuota(n)` en `Dom/Lending/Payments/AplicadorDePagos.cs`: cubre todo menos los conceptos de `n`, abona a capital las posteriores, completa el interés del período en `n` si falta (D13-A) y la marca congelada en la decisión
- [X] T060 [US4] La reconstrucción respeta la cuota congelada (no causa más interés en `n` hasta su vencimiento) en `Dom/Lending/Replay/ReconstruccionDeCredito.cs` y `EntradaDeReconstruccion.cs`
- [X] T061 [P] [US4] Casos dorados: causación diaria, al inicio del ciclo, período calendario, con anticipo, con seguro y administración, cuota `n` parcialmente pagada, pago de `n` después (cierra al peso), en `TD/Lending/Payments/Casos/` y `TD/Lending/Payments/CancelacionSalvoCuotaTests.cs`
- [X] T062 [US4] Créditos en UVR: la modalidad no se ofrece (siempre total con explicación) en `LiquidacionDeRecogida`
- [X] T062a [US4] D13 ajustada (dueño, 2026-11-27; Ley 1555 de 2012): dentro del tope legal del prepago (o sin los valores nacionales para saberlo) la cuota que se deja lleva el interés **proporcional** —lo causado hasta la fecha sobre todo el saldo y el resto del período sólo sobre su capital—; fuera, D13-A. `InteresesAlCancelar.CuotaDejadaAsync` decide, `SolicitudDeDistribucion.CuotaDejadaProporcional` y `EstadoDelCredito.InteresPorCausarSobreSuCapital` en el motor, sello `LeftInstallmentRule = Proporcional` con `LeftInstallmentRuleReason`, devolución de lo reconocido de más con la operación; liquidación, candidatos, vista previa, listado de la caja y textos; casos dorados 06–08 (E-15)

## Fase 9 — HU4: cuota enviada, libranza, giro posterior y caja (P1, E2)

- [X] T063 [US4] Migración `CuotaDejadaALaPagaduria` (`LND_PayrollDeductionResults.LoanClosedAtResult`, columnas `LeftInstallment*` en `LND_DisbursementPayoffs`) en los dos motores; configuraciones en `Pers/Configurations/Lending/Payroll/` y `Payoffs/`; paridad
- [X] T064 [US4] `CuotaEnviadaDelCredito` (R6) en `App/Lending/Payroll/CuotaEnviadaDelCredito.cs`, con pruebas en `TA/Lending/Payroll/CuotaEnviadaDelCreditoTests.cs`
- [X] T064a [US4] Puerto de sólo lectura `IDescuentosDeNominaParaCartera` en `App/Common/Integration/Payroll/` e implementación en `App/Payroll/Integration/` (novedad `LoanDeduction` del período abierto, `PAY_PayrollLoanDeductions` en borrador o aprobada, por obligación); `CuotaEnviadaDelCredito` lo usa para la nómina propia; regla de arquitectura de métodos del puerto; pruebas en `TA/Payroll/Integration/DescuentosDeNominaParaCarteraTests.cs`
- [X] T065 [US4] `ObligacionListadaDto.SentInstallment` y `admitsPayoffExceptCurrentInstallment` en `App/Common/Integration/Lending/ContratoDeObligacionesDtos.cs` y `App/Lending/Integration/ContratoDeObligacionesDeCartera.cs`
- [X] T066 [US4] `LiquidacionDeRecogida` con `Recogida.CuotaCausada` (y `Preguntar` con la elección guardada en la solicitud) y la cuota enviada; la recogida aplica `PayoffExceptCurrentInstallment`; filas `LeftInstallment*`
- [X] T067 [US4] `ConsumoDeLibranza` con crédito no activo: `LoanClosedAtResult`, sin no descontado ni aviso al deudor, en `App/Lending/Payroll/Results/ConsumoDeLibranza.cs`
- [X] T068 [US4] Excluir los resultados con crédito cerrado en `App/Lending/Payroll/PendientesDeGiro.cs`, `App/Lending/Payroll/Tasks/TareasDeLibranza.cs` y `App/Lending/Regulatory/ProyeccionCentralDeRiesgo.cs`
- [X] T069 [US4] Rechazo con sucesor (`successorObligationPublicId`, `afterPayoff`) en `App/Lending/Payments/AplicacionDePagos.cs`; Recaudo aplica según `Recogida.GiroPosteriorDeCreditoRecogido` en `App/Receipts/Paymasters/GirosDePagaduria.cs` (`AlCreditoNuevo`: clave `REC:{giro}:{línea}:sucesor`; `AFavorDelAsociado`: saldo a favor de la persona)
- [X] T070 [US4] Seguimiento de la cuota dejada: `LeftInstallmentSettledAt` al pagarse; aviso al asesor si llega «no descontado», en `App/Lending/Payoffs/SeguimientoDeCuotaDejada.cs`
- [X] T071 [US4] Caja: `Pagos.CuotaEnviadaAlCancelar` en la cancelación (`FullPayoff` → `PayoffExceptCurrentInstallment`), aviso en F7 y en el detalle, en `App/Receipts/CashDesk/` y `Sh/Pages/Recaudo/Caja.razor` (con `IndicadorDeCarga`)
- [X] T072 [US4] Columna y explicación de la cuota causada en `ProductosParaRecoger.razor`, `Desembolsos.razor` y `CreditoDetalle.razor` («cuota dejada a la pagaduría») — parte de recogida en el bloque H1 (`ProductosParaRecoger`, `DetalleDeRecogida`, `RecogidasDelDesembolso`, `Desembolsos`, `LoQueRecoge`, `CreditoDetalle`); la de la caja con T071 (aviso en el ítem y en la lista de lo pagable, `CuotaEnviadaEnLaCaja`)
- [X] T073 [P] [US4] Pruebas: consumo con crédito cerrado, giro posterior en sus tres opciones, caja con `Incluir`/`Excluir`/`Preguntar`, regresión de libranza y caja con los defectos, en `TA/Lending/Payroll/CreditoCerradoEnLaLibranzaTests.cs` y `TA/Receipts/CashDesk/CuotaEnviadaEnLaCajaTests.cs`
- [X] T074 Puerta de E2: build y suites unitarias verdes; commit

## Fase 10 — HU6: aportes y ahorros (P2, E3)

- [X] T075 [US6] Exploración: orden de cerrojos del cruce del retiro frente al desembolso (créditos → personas → cuentas); anotar el resultado en `research.md` R13 antes de construir
- [X] T076 [US6] `ConsignacionEnCuentas.ClaseDeMedio` admite `CAR`/`OwnLoanPayoff`/`InternalOffset` sólo por el camino interno en `App/Lending/Deposits/Contributions/ConsignacionEnCuentas.cs`; prueba de que `IContratoDeCuentas` la sigue rechazando
- [X] T077 [US6] `RecogidaDeCuentas` (modos `Installments` y `Deposit`, clave `CAR:{op:N}:{cuenta:N}`) en `App/Lending/Payoffs/RecogidaDeCuentas.cs`; llamada desde `ExecuteLoanDisbursementCommand`
- [X] T078 [US6] Candidatos de cuentas (aportes, ahorro a la vista, permanente, contractual; sin CDAT) con sus cuotas en `GetPayoffCandidatesQuery`
- [X] T079 [US6] Anulación por `ReversionDeCuentas` con `{clave}:reverso` en `VoidPendingLoanCommand.cs`
- [X] T080 [US6] Contabilidad: la consignación por recogida debita el mismo puente; completitud de la matriz para los productos admitidos en `App/Accounting/Lending/Consultas/GetLendingRulesCompletenessQuery.cs`
- [X] T081 [US6] Las cuentas en `ProductosParaRecoger.razor` y `Desembolsos.razor`
- [X] T082 [P] [US6] Pruebas: cuotas de aportes, extraordinario admitido y no admitido, ahorro contractual sólo cuotas, anulación, contabilidad, en `TA/Lending/Payoffs/RecogidaDeCuentasTests.cs`
- [X] T083 Puerta de E3: build y suites verdes; commit

## Fase 11 — HU7: facturas a crédito de Comercial (P3, E4)

- [ ] T084 [US7] Exploración: `AplicacionNoInteractiva` dentro de la transacción explícita del desembolso (cerrojos, numeración); si no sirve, detener E4 y consultar al dueño; anotar en `research.md` R14
- [ ] T085 [US7] Puerto `IRecogidaEnRecaudo` en `App/Common/Integration/Receipts/IRecogidaEnRecaudo.cs` e implementación en `App/Receipts/Integration/RecogidaEnRecaudo.cs` (canal `LoanDisbursement`, recibo sin caja, `ReciboEmitido`); regla de arquitectura de métodos del puerto
- [ ] T086 [US7] Clase de medio `InternalOffset` en `Dom/Enums/Core/PaymentEnums.cs` y su semilla; regla de Recaudo para su rol
- [ ] T087 [US7] Saldo de facturas por el puerto en `GetPayoffCandidatesQuery`; aplicación y anulación desde el desembolso
- [ ] T088 [US7] Aviso de completitud cruzada (el rol del medio en Recaudo y el puente de Cartera a la misma cuenta) en `GetLendingRulesCompletenessQuery`
- [ ] T089 [US7] Las facturas en `ProductosParaRecoger.razor` y `Desembolsos.razor`
- [ ] T090 [P] [US7] Pruebas: recibo, factura en cero, cargo de Transporte pagado, falla de Recaudo aborta el desembolso, en `TA/Lending/Payoffs/RecogidaDeFacturasTests.cs`
- [ ] T091 Puerta de E4: build y suites verdes; commit

## Fase 12 — Cierre

- [ ] T092 [P] Manual `docs/manual/cartera-recogida-de-creditos.md` (vender, radicar, desembolsar, parámetros, cuota causada, contabilidad, informe) y enlace en `docs/manual/cartera.md`
- [ ] T093 [P] Actualizar `docs/manual/cartera-desembolso-y-garantias.md` §5, `docs/manual/cartera-libranza-y-pagadurias.md` (crédito cerrado con cuota enviada), el manual de caja (F7) y `docs/manual/cartera-integracion-contable.md` (el puente cuadra solo)
- [ ] T094 [P] Revisar que ninguna pantalla, informe ni manual nuevo nombre el sistema anterior
- [ ] T095 Viñeta de la 028 en `CLAUDE.md` y totales remedidos (rutas, páginas, reportes, pruebas)

## Fase 13 — Pruebas de punta a punta (una sola vez, al final)

- [ ] T096 Ampliar `TI/Lending/DesembolsoYRecogidaTests.cs`: candidatos → solicitud con proyección → decisión → pago en caja → vista previa con diferencia → desembolso → comprobantes DS/RC con el puente en cero → resultado de Tesorería → liberación de garantías
- [ ] T097 [P] e2e de libranza: crédito con cuota enviada recogido con `Excluir` y con `Incluir` + `AlCreditoNuevo` en `TI/Lending/RecogidaConCuotaEnviadaTests.cs`
- [ ] T098 [P] e2e de aportes y facturas (E3, E4) en `TI/Lending/RecogidaDeOtrosProductosTests.cs`
- [ ] T099 Correr las e2e de Cartera, Recaudo y Contabilidad en PostgreSQL y SQL Server; anotar resultados en `CLAUDE.md`
- [ ] T100 PR a `develop`; CI verde; avisar a la sesión coordinadora con el diagnóstico que debe correr antes de promover (desembolsos con recogidas y solicitudes con `PayoffRequest` en DEV, QA y PDN)

## Dependencias

- Fase 1 → Fase 2 → (Fase 3 → Fase 4 → Fase 5 → Fase 6 → Fase 7) = **E1**.
- **E2** (fases 8–9) depende de E1 (usa `LiquidacionDeRecogida`, las filas y la pantalla); la fase 8 (motor) no depende de las pantallas y puede
  empezar en paralelo con la fase 7.
- **E3** (fase 10) depende de E1; independiente de E2.
- **E4** (fase 11) depende de E1; independiente de E2 y E3.
- Fases 12 y 13 al final de las entregas que se construyan.

## Paralelo

- Fase 2: T008, T011, T012 en paralelo una vez T005–T007 compilan.
- Fase 3: T017, T018, T021, T022 en paralelo con T019–T020.
- Fase 6: T048 y T049 en paralelo con T047.
- Fase 8 (motor puro) en paralelo con la fase 7 (informe y pantallas) — sin archivos en común.
- Con dos sesiones compilando como máximo, no más de dos de estas líneas a la vez.

## Estrategia

- **MVP = E1** (fases 1–7): reemplaza «desembolsar y cancelar en caja con F7», con proyección, diferencias y contabilidad que cuadra.
- **E2** cierra el pedido del dueño para créditos por nómina y libranza y la falla de la caja (H1).
- **E3** y **E4** según la parametrización que quiera cada cooperativa.
- Cada entrega deja build y suites unitarias verdes y su commit; las e2e una sola vez al final (fase 13).
