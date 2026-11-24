# Plan de implementación: 025 — Causación por período y tasa mensual

**Rama**: `025-causacion-por-periodo` · **Fecha**: 2026-11-24 · **Spec**: [spec.md](spec.md)
**Base de código**: `origin/develop` @ `9c41d8db`.

## Resumen

Seis historias en seis fases. Cada fase compila, prueba, se documenta y puede salir sola.

| Orden | Fase | Historia | Tamaño |
|---|---|---|---|
| 1 | El estado de cuenta muestra la cuota regular | HU6 | pequeño |
| 2 | Ajuste del corte (la plata que falta) | HU1 | grande |
| 3 | Tasa pactada en MV | HU2 | grande |
| 4 | Corrección de la tasa de la carga | HU3 | mediano |
| 5 | La carga causa completo el período en curso | HU4 | mediano |
| 6 | Causación por período calendario | HU5 | grande |

**Por qué este orden**:
- La fase 1 es inmediata.
- La 2 recupera dinero que hoy no se cobra.
- La 3 es la base de la 4 y de los casos dorados exactos de la 6.
- La 5 reusa la mecánica de la 2.
- La 6 es la más amplia y depende de D3.

## Contexto técnico

- .NET 10, Clean Architecture, CQRS con MediatR.
- **Motor de Cartera:** puro y en `decimal`, en `Domain/Lending`.
- **Proceso de causación de la 024:** `Application/Lending/AccrualProcesses`.
- **Libro:** inmutable y con un solo escritor, `RegistroDeCartera`.
- **Contabilización:** por la matriz `ACC_LendingPostingRules`.
- **Migraciones EF:** por motor en `Persistence.Migrations.{PostgreSql|SqlServer}/Application/`. Hoy sólo `EsquemaBase` está congelada.
- **Restricciones que vigilan pruebas de arquitectura:**
  - `NadieEscribeElLibroDeCarteraFueraDelRegistro`
  - `LosHechosInmutablesNoSeModifican`: los cambios insertan versiones nuevas.
  - `LosSaldosYTasasDeCarteraTienenSuPrecision`
  - `ElMotorDeCarteraEsDecimal`
  - `LaCarteraNoTieneValoresLegalesFijos`: no admite los literales 7 ni 10. Las fracciones salen del valor del enum `RepaymentPeriodicity`.
  - `LosEnumsDelContratoNumeranComoElDominio`
  - `CadaParametroRespetaLaEntregaDeSuModulo`

## Constitution Check

| # | Principio | Estado | Nota |
|---|---|---|---|
| I | Spec-First | PASS | spec, plan y luego tasks. |
| II | Clean Architecture | PASS | Las reglas nuevas van en el motor puro (Domain) y los procesos en Application. |
| III | CQRS + MediatR | PASS | Las clases nuevas del proceso de la 024 reusan sus comandos. La opción de la carga va en su comando con validador. |
| IV | Multi-tenancy | PASS | Todo corre en la base de la cooperativa. |
| V | Person centralizada | N/A | |
| VI | PublicId | PASS | Las rutas y DTOs nuevos usan PublicId. |
| VII | Soft-delete + auditoría | PASS | Firme, reproceso y corrección quedan auditados con motivo. |
| VIII | Validación dual | PASS | En pantalla y en FluentValidation: base de tasa, exclusión EA/MV, convención 30/360. |
| IX | Errores visibles | PASS | Rechazos con código (`Lending.Rate.MonthlyRequiresThirty360`, `Lending.Cutover.AlreadyAdjusted`…). |
| X | Trazabilidad | PASS | Hechos contables y auditoría de la firma. |
| XI | Inmutabilidad contable | PASS | El ajuste y la corrección **insertan**: una transacción nueva en el libro, una versión nueva de condiciones y una versión nueva del plan. Nada se edita. |
| XII | Migraciones | PASS | Dos migraciones aditivas, nuevas y posteriores a `EsquemaBase`. |
| UI | `IndicadorDeCarga` | PASS | Editor de línea, carga y `/cartera/causacion` ya lo llevan; lo nuevo va dentro de sus zonas. |

## Fase 0 — Decisiones (antes de las fases 2, 4 y 6)

- **D1** (dónde se cobra el ajuste): bloquea la fase 2.
- **D2** (si se corrige la tasa y desde cuándo): bloquea la fase 4.
- **D3** (interés al cancelar en período calendario): bloquea la fase 6.

Las fases 1, 3 y 5 no esperan.

## Fase 1 — «Cuota» del estado de cuenta (HU6)

- **Dónde:** `Application/Lending/PersonStatement/EstadoDeCuentaDeLaPersona.cs`. La variable `regular` (l. 285) hoy toma el `Total` de la primera cuota regular.
- **Cambio:** pasa a ser el total **más frecuente** entre las cuotas regulares del plan vigente; si hay empate, gana la siguiente por vencer. Una función pura `CuotaRegular(plan, hoy)` con pruebas.
- **Pruebas** en `Application.Tests/Lending/PersonStatement`:
  - plan migrado con la primera cuota recortada;
  - cuota fija normal;
  - abono que regeneró la cuota (gana la de la versión vigente);
  - plan de una sola cuota.
- **Manual:** una línea en el tema del estado de cuenta.

## Fase 2 — Ajuste del corte (HU1)

### Por qué no basta con escribir en el libro
El libro tiene que ser igual a la reconstrucción (`RegistroDeCartera.Conciliar`). Un interés que no esté en la **entrada** de la reconstrucción se «corregiría» de vuelta la noche siguiente. Además:
- hoy ninguna operación suma interés corriente a un crédito vivo;
- `Charge` sólo admite gastos de cobranza y otros cargos (`CargoEnLaReconstruccion.cs:33`);
- el ajuste comercial y la condonación sólo restan.

### Diseño
1. **Motor (Domain)**
   - `EntradaDeReconstruccion` gana `AjustesDelCorte`: lista de (fecha, cuota, valor) de interés corriente.
   - `Replay/CausacionDeCredito` (`Corrida.Ejecutar`, l. 508) los suma a la cubeta de interés corriente de esa cuota en su fecha, como hoy hace con los cargos.
   - `AplicadorDePagos.ValorDeCuota` (l. 355) y `ComprobantesDePago.CuotaDelCiclo` (l. 63) los incluyen.
   - **Arreglo de paso:** hoy esas dos funciones ignoran el interés que la cuota trajo en la carga (`interesCorriente`, saldo inicial). La caja subestima la primera cuota cuando la carga sí lo trae. Se corrige en el mismo punto.
2. **Libro**
   - `LoanTransactionKind.CutoverInterestAdjustment`: el siguiente número libre en `LoanEnums.cs`, igual en el enum del contrato.
   - `RegistroDeCartera` gana `RegistrarAjusteDelCorte`. Es el único escritor.
   - `CargadorDeReconstruccion` carga esas filas en `AjustesDelCorte`, con lo conocido en R.
3. **Plan**
   - Una **versión nueva del plan** (`RegistrarPlanRegenerado`) con el interés de la cuota destino aumentado en el ajuste, para que F7, el extracto y la caja digan lo mismo.
   - El capital no cambia.
4. **Contabilidad**
   - Hecho nuevo `InteresDelCorte` (código corto `IDC`) en `Accounting/Lending/Reglas/OperacionesDeCartera.cs`, con los roles de `ICA`: cuenta por cobrar de interés e ingreso.
   - Comprobante `CA`, en línea.
   - Va al catálogo de mensajes (`CatalogoDeMensajesDeCarteraV1.cs`) y a la plantilla de la matriz como fila sugerida sin cuenta.
5. **Proceso**
   - Clase nueva `CutoverAdjustment` de `IClaseDeProcesoDeCausacion` (`CalculoDeProcesoDeCausacion.cs:150`), registrada en DI.
   - **Calcular** (sin escribir), por crédito nacido de una carga (`IsMigratedPlan`):
     1. busca la primera cuota con vencimiento posterior al corte;
     2. calcula el interés de su período completo, desde el vencimiento anterior en 30/360 o la convención sellada, sobre el capital al corte, con la tasa y la equivalencia selladas (`EquivalenciaDeTasas.TasaDelPeriodo`);
     3. le resta lo causado de esa cuota hasta su vencimiento más el interés cargado;
     4. lo que dé es el **ajuste**.
   - **Excluidos con motivo:** cancelado, castigado, UVR, suspendido, ya ajustado, sin plan periódico, ajuste menor a un peso.
   - **Destino según D1:** la cuota indicada (recomendado: la primera sin vencer a la fecha de firma).
   - **Poner en firme:** cuatro ojos y cerrojo de los créditos; luego, por crédito:
     1. transacción `CutoverInterestAdjustment` en la fecha de firma;
     2. versión nueva del plan;
     3. hecho `IDC`;
     4. marca de «ajustado».
   - Para los motivos y el reproceso reusa lo de la 024, con el mismo patrón que `CorreccionDeValorNacional` (`Accrual/RunNationalValueCorrectionCommand.cs:45-140`).
   - **El borrador es la medición:** da la cifra exacta de producción sin consultar la base.
6. **Datos**
   - Columna nueva `LND_LoanControls.CutoverAdjustedAt`, nula.
   - Migración aditiva `AjusteDelCorte` (PostgreSQL y SQL Server).
7. **Pantalla**
   - `/cartera/causacion` gana la clase en su selector.
   - El borrador lista por crédito: capital al corte, días faltantes, ajuste y cuota destino. Lleva totales y exportación.
8. **Permisos:** reusa `Lending.AccrualProcesses.Calculate/Confirm`. Sin permisos nuevos.

### Pruebas
- **Casos dorados nuevos** (`Domain.Tests/Lending/Casos/39-ajuste-del-corte-*.json`), con `calcular.py` extendido:
  - vencimiento el día 9;
  - vencimiento el día 30 (sin ajuste);
  - cuota con interés cargado;
  - cuota destino ya vencida;
  - quincenal.
- **Aplicación:** el borrador no escribe; firme a cuatro ojos; no se repite; la conciliación nocturna no lo revierte.
- **Arquitectura:** el escritor único sigue siendo único; el enum del contrato numera igual.
- **e2e:** una cooperativa aislada cargada con corte, se activa, se ajusta y se causa una noche. El libro sigue igual a la reconstrucción.

## Fase 3 — Tasa pactada en MV (HU2)

### Datos
- `CreditLineVersion` (`Entities/Lending/Configuration/CreditLineVersion.cs`) y `LoanTermsVersion` (`Entities/Lending/Loans/LoanTermsVersion.cs`) ganan:
  - `RateBasis` (enum nuevo en `TermsEnums.cs`: `EffectiveAnnual = 1`, `MonthlyNominal = 2`);
  - `MonthlyRate` (alias de precisión nuevo `TasaMensual`, 9,8, en `PrecisionDeCartera.cs`).
- `AnnualRate` se sigue llenando con la E.A. derivada, redondeada a 6, sólo para informar.
- Lo mismo en las bandas de tasa de la línea, si se pactan en MV.
- Migración aditiva `TasaMensual`, con defecto `EffectiveAnnual`: lo existente no cambia.

### Motor
**Hoy todas las entradas son E.A.:**
- `EntradaDePlan.TasaEfectivaAnual`;
- `EntradaDeTasaDelDia.CorrienteEA`;
- `ContextoDeReconstruccion.TasaCorrienteEA` (`ReconstruccionDeCredito.cs:40, 54, 77`);
- la llave de memoria (l. 317).

**Cambio:** un tipo `TasaPactada(Base, Valor)` reemplaza el `decimal` en esas entradas. La llave de memoria incluye la base.

**`EquivalenciaDeTasas`:**
- `TasaDiaria` con base MV = `MV / Base(Thirty360)`, lineal.
- `TasaDelPeriodo` sigue lineal.
- `EfectivaDeMensual(MV)` = (1 + MV)^12 − 1, exacta en decimal, para la usura y los informes.
- `EsTasaPactable` admite una MV de hasta 8 decimales. Hoy, 0,0180 en E.A. derivada no cabe en 6 decimales y la rechazan `CreditLineVersion.Validar` (237-240), `ReconstruccionDeCredito.ValidarTasasSelladas` (353-363) y `GeneradorDePlan.ValidarNovedad` (510).

**`TasaDelDia.Calcular`** (107-154): con base MV el tope compara la E.A. derivada con la usura. Si la pasa, la tasa del día es la mensual equivalente de la usura menos el diferencial de cargos.

**`GeneradorDePlan`:**
- interés por cuota = saldo × MV × días30/360 / 30;
- la primera cuota se prorratea lineal (l. 55), como la herramienta anterior.

**Validación:** base MV exige `DayCount = Thirty360` y `RateEquivalence = Linear` (`Lending.Rate.MonthlyRequiresThirty360`).

### Flujo de sellado
**Cadena:** `SimuladorDeCredito.SimularAsync` (tasa en l. 197) → `SimulacionCalculada.TasaPactada` (l. 58) → `TramoDeDesembolso.Condiciones` (l. 137-205).
- La solicitud **no guarda la tasa**: el desembolso vuelve a simular desde la versión de línea (`RequisitosDelDesembolso.cs:224, 331-341`). Basta con que la versión de línea lleve la base.
- **Otros puntos:** novedades (`CondicionesDeNovedad.cs:43`), reajuste de tasa variable (`ReajusteDeTasa.cs:81`, sigue en E.A.) y nacimiento de migrados (`NacimientoDeCreditosMigrados.Condiciones`, 435-500).

### Usura
`ControlDeUsura.Evaluar` (l. 79) compara la efectiva por TIR y no cambia. Revisar que reciba la base:
- `ReglasDeLineaDeCredito.cs` (47, 93, 236, 384);
- `EfectoDeReplicacionUsuraEnLineas.cs:104`;
- solicitud: `CreditApplicationCommands.cs:142`, `SubmitCreditApplicationCommand.cs:87`, `DecisionSobreSolicitud.cs:189`;
- desembolso: `RequisitosDelDesembolso.cs:227`;
- importación: `ImportMigrationPartCommand.cs:976-988`.

### API, pantallas y plantillas
- **DTO:** `CreditLineDtos.cs:14` `CurrentRateInput` gana `Basis` y `MonthlyRate`; Shared `CarteraDtos.Lineas.cs:15`.
- **`EditorDeVersionDeLinea.razor`** (81-83, bandas 149-150): selector de base, campo MV con cuatro decimales y la E.A. calculada al lado. La mora queda en E.A. (D4).
- **Resumen y detalle:** `LineaDetalle.razor:232`, `ResumenDeVersion.razor:45,86`, `CreditoDetalle.razor:98`, `SimuladorDeCredito.razor:108`.
- **Plantillas:**
  - líneas: `CatalogoDePlantillasDeCartera.cs:137, 229, 278` e `ImportCreditLinesCommand.cs:331, 405`;
  - migración: `DefinicionesDeMigracion.cs:62, 226` e `ImportMigrationPartCommand.cs:450`;
  - columna `tasaCorrienteMV`, excluyente con `tasaCorrienteEA`. `FilaDeImportacion.Porcentaje` lee hasta 4 decimales.

### Documentos e informes («E.A.» y, si aplica, «MV»)
- Plan de pagos: `ConstructorDeDocumentosDeCartera.cs:197-215` → `DocumentosDeCarteraReport.cs:452`.
- `SimulacionDeCreditoReport.cs:77`.
- Campos de firma: `DatosParaFirma.cs:285`, `CamposDelSistema.cs:51`.
- Estado de cuenta: `EstadoDeCuentaDeLaPersona.cs:291`.
- Informes de usura y colocación: `UsuryReportQuery.cs:114`, `PlacementReportQuery.cs:92`.
- **El SICSES** (`DatosDelCreditoAlCorte.cs:73`) sigue informando E.A., la derivada. Las centrales no imprimen tasa.

### Pruebas
- **Casos dorados** `40-tasa-mv-mensual`, `41-tasa-mv-quincenal`, `42-tasa-mv-primera-cuota-prorrateada`, `43-tasa-mv-tope-de-usura`, con valores de la herramienta anterior calculados a mano.
- `calcular.py` con la base MV.
- **Pruebas de equivalencia:** `Rates/*` y `ContadorDeDiasTests`.
- **Arquitectura:** precisión del alias nuevo; sin literales legales.
- **Regresión:** los 38+ casos dorados existentes iguales al centavo.

## Fase 4 — Corrección de la tasa de la carga (HU3, según D2)

- Clase `CutoverRateCorrection` del proceso de la 024.
- **Calcular:** por crédito migrado con base E.A., busca una MV redonda a 2 decimales cuya E.A. difiera menos de 0,005 puntos de la sellada. Lista la actual, la propuesta y el efecto en la próxima cuota.
- **Poner en firme** (cuatro ojos), por crédito:
  1. versión nueva de `LoanTermsVersion` con base MV, causa `CutoverCorrection` (valor nuevo de la causa de condiciones) y vigencia desde la fecha de D2;
  2. versión nueva del plan **sin tocar el capital**, con el interés recalculado de las cuotas sin vencer.
- **Hacia atrás no recalcula** (recomendación de D2).
- **Pruebas:** caso dorado `44-correccion-de-tasa-de-la-carga`. Un crédito ya corregido no vuelve a entrar.

## Fase 5 — La carga causa completo el período en curso (HU4)

**Hoy el interés nace desde el corte en dos sitios:**
- **el plan:** `GeneradorDePlan.DesdeMigracion` (644-656) toma `inicio = max(desde, corte)` (l. 647) y los tramos arrancan ahí;
- **la reconstrucción:** acumula desde el origen = corte (`Corrida.Ejecutar`, 516 y 563).

`NacimientoDeCreditosMigrados` (l. 211) nunca llena `CuotaMigrada.Desde`.

**Cambio:**
1. La carga de migración gana `CauseFullInProgressPeriod`: columna en la cabecera de la carga y casilla en `/cartera/migracion`; defecto **sí** (D5).
2. **Con la opción:**
   - `NacimientoDeCreditosMigrados` llena `Desde` de la primera cuota posterior al corte con el vencimiento anterior, calculado por la periodicidad, o con el desembolso si es la primera del crédito;
   - `DesdeMigracion` usa ese `Desde`, así el plan muestra el interés completo;
   - lo anterior al corte entra como `AjustesDelCorte` el día de la activación, con la misma transacción, el mismo hecho `IDC` y la misma marca de la fase 2. Por eso el crédito no vuelve a entrar al ajuste.
3. **Lo que la cuota trae en `interesCorriente` cuenta como causado:** el ajuste es el período completo menos lo cargado, sin bajar de cero.
4. **El cuadre de la activación** (`CuadresDeMigracion.cs`) y la vista previa muestran el total que suma la opción. Ese interés es ingreso del día de activación: **no** se cuadra contra el balance al corte.

**Pruebas:**
- caso dorado `26` con la variante «período completo»;
- la e2e de activación con y sin la opción.

## Fase 6 — Causación por período calendario (HU5, según D3)

### Parámetros (`ParametrosDeCartera.cs`)
- **`Causacion.Periodo`:** `CicloDeLaCuota` (de fábrica) o `PeriodoCalendario`, general con excepción por línea, como `Causacion.Momento` (l. 121, ámbito l. 675).
- **`Pagos.InteresesAlPagarAntesDelVencimiento`** (l. 56, valores en 628-632): gana `PeriodoCausado`. Lo leen `InteresesAlCancelar.cs:55-86` y lo sellan `DecisionDePago.cs:96,175`, `AplicacionDePagos.cs:569` y `SimulacionDeCancelacion.cs:105`.
- **Lector:** `PoliticaDeReconocimiento.cs:42` gana el período.

### Período de una cuota (Domain)
- `PeriodoDeCausacion` (`Domain/Lending/AccrualProcesses/PeriodoDeCausacion.cs`) gana `TramoQueContiene(vencimiento, periodicidad)`:
  - mes: del 1 al último día;
  - quincena: del 1 al 15 y del 16 al fin;
  - década: del 1 al 10, del 11 al 20 y del 21 al fin;
  - semana: la de la corrida.
- Los rangos ya existen en `Proponer`/`Validar` (52, 76). Los límites salen del valor del enum, no de literales.

### Reconocimiento automático
- `PuestaAlDiaDeCredito.Causar` (126-146; hoy reconoce el día `Desde+1`, 154-170): con `PeriodoCalendario`, el día de reconocimiento es el **primer día del tramo** que contiene el vencimiento.
- `CuotaPorReconocer` (`ReconstruccionDeCredito.cs:428-452`) no cambia: reconoce lo que falta por causar hasta el vencimiento.

### Proceso manual
- `ReconocimientoDelPeriodo.DelCredito` (151-183): hoy toma las cuotas cuyo ciclo se cruza con el rango (l. 163). Con `PeriodoCalendario` toma las que **vencen** en el rango.
- El motivo `CycleNotStarted` cambia por `PeriodNotStarted`.

### Caja y estado de cuenta
- `ComprobantesDePago.CuotaDelCiclo`, `ReconocidoDelCiclo` y la lista de la caja (`ContratoDeObligacionesDeCartera.cs:134`): con `PeriodoCalendario`, la «cuota del período» es la que vence en el tramo actual. Si ya está pagada, lo siguiente es anticipo.

### Interés al cancelar con `PeriodoCausado` (D3)
- `AplicadorDePagos.Cancelacion` (258-273) cobra lo reconocido y no pagado de los tramos ya empezados, y nada de un tramo sin empezar.
- Lo causado día a día de ese tramo se devuelve con la devolución de la 024 (`RecognitionRelease`).
- Mismas restricciones que `CuotaCompleta`: fuera del tope de la Ley 1555, nunca en vivienda.

### Filtros del proceso (FR-018)
- `AccrualProcessInput` (`AccrualProcessCommands.cs:29`) y la cabecera `AccrualProcess` ganan forma de pago, qué causa y ciclo de descuento.
- Cada filtro es exacto en la clase de reconocimiento.
- Migración aditiva `FiltrosDelProcesoDeCausacion`.
- Pantalla `Causacion.razor` (selector de subperíodo, l. 332).

### Pruebas
- **Casos dorados:**
  - `45-periodo-calendario-mensual`
  - `46-periodo-calendario-quincenal`
  - `47-cancelacion-periodo-causado`
  - `48-abono-a-mitad-de-periodo`
  - `49-pago-el-24-es-anticipo`
- **Aplicación:**
  - el proceso por rango toma por vencimiento;
  - la caja propone la cuota del tramo;
  - con `CicloDeLaCuota` todo da igual que hoy (regresión de los casos 37/38 de la 024).
- **e2e:** cooperativa aislada en `PeriodoCalendario` + `InicioDelCiclo` + `Manual`. Corre el proceso del 1 al 30, paga en caja y cancela a mitad de mes.

## Documentación (cada fase cierra con la suya)

- `docs/manual/cartera-causacion-y-liquidacion.md`: período calendario, tasa MV, ajuste del corte.
- `docs/manual/cartera-integracion-contable.md`: hecho `IDC` y su fila en la matriz.
- `docs/operaciones/cartera-carga-y-activacion.md` (o el runbook de migración vigente): la opción del período en curso.
- Manual en línea (`ManualCatalogo.cs`):
  - tema del proceso de causación;
  - tema del editor de líneas;
  - tema del estado de cuenta.
- CLAUDE.md: un párrafo de la 025 al cerrar.

## Migraciones (todas aditivas, nuevas, posteriores a `EsquemaBase`)

| Fase | Nombre | Qué agrega |
|---|---|---|
| 2 | `AjusteDelCorte` | `LND_LoanControls.CutoverAdjustedAt` |
| 3 | `TasaMensual` | `RateBasis` y `MonthlyRate` en versiones de línea, bandas y condiciones |
| 5 | `CargaConPeriodoCompleto` | la opción en la cabecera de la carga |
| 6 | `FiltrosDelProcesoDeCausacion` | los filtros de FR-018 en `LND_AccrualProcesses` |

## Riesgos

- **Conciliación nocturna:** si el ajuste o la tasa MV no entran a la **entrada** de la reconstrucción, la noche los revierte. Lo cubre una prueba de que el libro sigue igual a la reconstrucción tras un ciclo.
- **Memoria del motor:** la llave de tasas cambia de tipo. Lo cubren los casos dorados de rendimiento (`RUN_PERF_TESTS`).
- **Usura con MV:** la E.A. derivada tiene más de 6 decimales. Se compara sin redondear y sólo se redondea para mostrar.
- **Producción (COOFLOPAL):**
  - la fase 2 se corre primero en QA con una copia de la carga;
  - el borrador de producción se revisa con la contadora antes de ponerlo en firme;
  - la matriz necesita la fila de `IDC` antes del firme, o `Contabilidad.FaltaDeRegla = Bloquear` lo detiene.

## Entrega

- Una rama y una PR por fase hacia develop, con su CI verde.
- Producción sólo con autorización expresa del dueño, desplegada por la sesión principal.
- Las e2e de cada fase se corren al cerrar la fase.
