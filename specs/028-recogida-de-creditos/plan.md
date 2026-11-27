# Plan de implementación: 028 — Recogida de créditos y productos en el desembolso

**Rama**: `028-recogida-de-creditos` · **Fecha**: 2026-11-27 · **Spec**: [spec.md](spec.md)
**Base de código**: `origin/develop` @ `d810d07f`. Investigación: [research.md](research.md) · Datos: [data-model.md](data-model.md) ·
API: [contracts/api.md](contracts/api.md) · Guía: [quickstart.md](quickstart.md).

## Resumen

El motor ya recoge créditos en el desembolso; esta feature lo **expone en pantallas**, lo hace **seguro** (proyección guardada, recálculo y
diferencias antes de confirmar, validaciones después de recoger, contabilidad que cuadra sola) y lo **amplía** (cuota causada que se deja a la
pagaduría, intereses proporcionales, aportes y ahorros, facturas a crédito). De paso corrige la cuota enviada a la pagaduría en la caja y en
toda cancelación (H1).

Se entrega en **cuatro entregas**, cada una con valor propio y desplegable sola:

| Entrega | Qué trae | Historias | Migración | Tamaño |
|---|---|---|---|---|
| **E1** Recoger créditos | Parámetros, consulta de productos a (D, R), solicitud con proyección, validaciones después de recoger, expediente, desembolso que recalcula y muestra diferencias, orden por lo aplicado, contabilidad que netea, un comprobante opcional, giro nuevo, garantías, intereses proporcionales, informe | HU1, HU2, HU3, HU5, HU8 | `RecogidaDeCreditos` | grande |
| **E2** Cuota causada y pagaduría | «Todo menos la cuota causada» en el motor, consulta de cuota enviada, créditos cerrados en la libranza, giro posterior, caja F7 | HU4 | `CuotaDejadaALaPagaduria` | grande |
| **E3** Aportes y ahorros | Recogida por el registro de cuentas, anulación, contabilidad, pantalla | HU6 | — | mediano |
| **E4** Facturas a crédito | Puerto con Recaudo, recibo sin caja, Transporte | HU7 | — (semilla) | mediano |

E1 basta para reemplazar «desembolsar y cancelar en caja con F7» en COOFLOPAL. E2 es la que pidió el dueño para los créditos por nómina y
cierra H1, que hoy ya afecta a la caja.

## Contexto técnico

- **Lenguaje**: C# / .NET 10.0.5, Minimal APIs con Carter, CQRS con MediatR, FluentValidation; Blazor (Shared) con Syncfusion 33.2.8 por
  paquetes de componente.
- **Almacenamiento**: base por cooperativa (PostgreSQL y SQL Server); migraciones EF por proveedor **posteriores a `EsquemaBase`**, en par.
- **Motor**: reconstrucción pura en Domain (`MotorDeCartera`, `AplicadorDePagos`, `ReconstruccionDeCredito`) con casos dorados; un solo escritor
  del libro (`RegistroDeCartera`) y de cuentas (`RegistroDeCuentas`); los pagos entran sólo por el contrato de obligaciones.
- **Pruebas**: xUnit en Domain, Application, Architecture y Shared; e2e con Testcontainers en los dos motores **una sola vez al final**.
- **Construcción**: `dotnet build IngenIA365ERP.slnx -m:1`; no más de dos sesiones compilando a la vez.
- **Restricciones**: de fábrica nada cambia; Cartera no conoce cuentas; lo contabilizado no se edita (se reversa); `Idempotency-Key` en toda
  escritura; `IndicadorDeCarga` en toda pantalla nueva o modificada; ninguna pantalla, informe ni manual nombra el sistema anterior.
- **Volumen**: una solicitud recoge pocos productos (≤ 20); la consulta de candidatos liquida cada crédito vivo del asociado (≤ 50) en menos
  de 2 s.

## Constitution Check

| # | Principio | Estado | Nota |
|---|---|---|---|
| I | Spec-First | PASS | spec 028 con decisiones D1–D13 pendientes; este plan; tareas; código sólo tras el OK del dueño |
| II | Clean Architecture | PASS | modalidad nueva y cuota congelada en el motor puro (Domain); orquestación en `Application/Lending/Payoffs`; EF en Persistence; Recaudo por puerto (E4) |
| III | CQRS + MediatR | PASS | consulta de candidatos y comandos con validador hermano; endpoints sólo reenvían |
| IV | Multi-tenancy | PASS | todo en la base de la cooperativa; sin trabajos de fondo nuevos |
| V | Person centralizada | PASS | las recogidas guardan `LoanId`/`AccountId`; la persona sale de Personas |
| VI | PublicId | PASS | rutas y DTO por `PublicId` |
| VII | Soft-delete + auditoría | PASS | entidades nuevas `AuditableEntity` con filtro |
| VIII | Validación dual | PASS | la pantalla bloquea lo no recogible, el neto ≤ 0 y el parcial sin valor; el servidor lo vuelve a validar |
| IX | Errores visibles | PASS | cada bloqueo con código y motivo en español; sin `catch` silencioso |
| X | Trazabilidad | PASS | desembolso con proyectado, aplicado, diferencias y quién las reconoció; `Lending.Payoff.Viewed` en proyección y vista previa |
| XI | Inmutabilidad contable | PASS | la recogida es un pago; anular reversa con clave derivada; el DS cambia sólo cómo reparte el crédito, no edita nada |
| XII | Migraciones | PASS | dos pares aditivos; `PayoffRequest` se conserva (sin destructiva) |
| UI | Indicador de carga | PASS | cada zona nueva con `IndicadorDeCarga`; `LasPantallasDicenQueEstanCargando` |
| Satélite | — | N/A | no toca `canal/` |

Re-evaluación tras el diseño: PASS. Sin desviaciones que justificar (no hay tabla de complejidad).

## E1 — Recoger créditos (HU1, HU2, HU3, HU5, HU8)

**Domain**
- Enums `PayoffProductKind`, `PayoffMode`, `InstallmentTreatment`, `PayoffDifferenceCause`.
- Entidades `CreditApplicationPayoff` y `DisbursementPayoff` (`Domain/Entities/Lending/Payoffs/`).
- `Domain/Lending/Payoffs/DiferenciaDeRecogida` (puro): compara dos desgloses y clasifica la causa por concepto. Casos en `Domain.Tests`.
- Parámetros en `ParametrosDeCartera` (R15) con sus defectos de hoy; `Pagos.CuotaEnviadaAlCancelar` se define aquí y se usa en E2.

**Persistence**
- Configuraciones, `DbSet`, migración `RecogidaDeCreditos` en los dos motores con la copia de `PayoffRequest` a filas
  (`tools/scripts/add-migration.ps1`, `check-migration-parity.ps1`).

**Application**
- `Lending/Payoffs/LiquidacionDeRecogida` (R3) reemplaza a `MotorDeCreditoEnDesembolso.LiquidarAsync`; `InteresesAlCancelar.ModoAlRecogerAsync`
  (R5).
- `GetPayoffCandidatesQuery` (§1 de la API) con la simulación del neto y la capacidad después de recoger.
- Solicitud: `PayoffInput` (R1), persistencia de la proyección, validación al radicar, aviso de otra solicitud viva, `FotoDeSolicitud` desde las
  filas, DTO con `payoffs` y `payoffSummary`, `UsesAdvancedTerms` sin recogidas.
- Validaciones después de recoger (R8): `ContextoDeValidacion.Recogidas`, `CapacidadDePago`, `ExposicionConsolidadaDelDeudor`, cupo,
  `ValidacionDeConcentracion` y la concentración del desembolso.
- Desembolso (R9): recogidas desde la solicitud, recálculo, diferencias y causa, `Recogida.AlDesembolsar`, tolerancia, reconocimiento en la
  huella, defensa `AppliedDiffers`, orden por lo aplicado (H4), filas `LND_DisbursementPayoffs`, recogida parcial como abono a capital (D7),
  validación contable previa con los `PagoAplicado` de las recogidas (FR-027a).
- Giro nuevo con las recogidas (H6); garantías al girar (H7, R12); anulación que marca `ReversedAt`.
- Contabilidad (R10, R11): constructor del DS por el neto + puente por recogida; casos dorados `01` actualizado y `0x` nuevo; parámetro de un
  solo comprobante en `ComprobantePorRecibo`.
- Informe `PayoffsReportQuery` (R17); «recogido por / recogió» en el detalle del crédito y el estado de cuenta; auditoría del desembolso.

**API**: `POST /payoffs/candidates`; cuerpos de solicitud y desembolso; vista `payoffs`. Cliente en `Shared/Services/Cartera`.

**Shared (pantallas)**: `ProductosParaRecoger.razor`; `SolicitudNueva.razor`; `SolicitudDetalle.razor`; `Desembolsos.razor`;
`CreditoDetalle.razor`; `/cartera/informes`. PDF de la simulación por el mismo generador de la simulación de cancelación.

**Pruebas**: motor de diferencias; liquidación de recogida (los tres modos de intereses × causación diaria / período calendario / inicio del
ciclo; vivienda y tope); validaciones después de recoger; desembolso (recalcular, mantener, omitido, tolerancia, reconocimiento, orden por lo
aplicado, giro nuevo); contabilidad (DS neto, puente en cero, giro a tercero, un comprobante); arquitectura `LaRecogidaSeLiquidaEnUnSoloSitio`
y `La_recogida_del_desembolso_paga_por_el_contrato`; Shared (DTO y textos).

## E2 — Cuota causada y pagaduría (HU4)

**Domain** (va primero, con sus casos dorados antes de cualquier pantalla — riesgo principal)
- `AplicadorDePagos`: modalidad `PayoffExceptCurrentInstallment` (R4): cubre todo salvo la cuota `n`, abona a capital las posteriores, deja `n`
  tal como se envió y la congela; la reconstrucción respeta la congelación sellada en la decisión del pago. Casos dorados: causación diaria,
  al inicio del ciclo, período calendario, con anticipo, con seguro y administración, cuota `n` ya parcialmente pagada.

**Persistence**: migración `CuotaDejadaALaPagaduria` (`LoanClosedAtResult`, columnas de la cuota dejada).

**Application**
- `CuotaEnviadaDelCredito` (R6) y `ObligacionListadaDto.SentInstallment`.
- `LiquidacionDeRecogida` con `Recogida.CuotaCausada` (y la elección con `Preguntar`); la recogida aplica `PayoffExceptCurrentInstallment`.
- Libranza (R7): `ConsumoDeLibranza` con crédito cerrado; `PendientesDeGiro`, `TareasDeLibranza`, `ProyeccionCentralDeRiesgo` los excluyen; el
  archivo siguiente no vuelve a llevar el crédito con su cuota dejada después de pagarla.
- Contrato: rechazo con sucesor; Recaudo `GirosDePagaduria.AnotarAsync` según `Recogida.GiroPosteriorDeCreditoRecogido`.
- Caja: `Pagos.CuotaEnviadaAlCancelar` en la cancelación (`FullPayoff` → `PayoffExceptCurrentInstallment` con `Receipts`), F7 y el detalle de la
  obligación.
- Seguimiento: `LeftInstallmentSettledAt` cuando la cuota dejada se paga; aviso al asesor si no se descuenta.

**Shared**: columna y explicación de la cuota causada en `ProductosParaRecoger`, `Desembolsos`, `CreditoDetalle`; aviso y opción en `Caja.razor`.

**Pruebas**: casos dorados; consumo con crédito cerrado; giro posterior en sus tres opciones; caja con y sin el parámetro; regresión de
libranza y caja con los parámetros de fábrica.

## E3 — Aportes y ahorros (HU6)

- Explorar primero el orden de cerrojos del cruce del retiro frente al del desembolso (R13) y anotar el resultado en `research.md`.
- `Lending/Payoffs/RecogidaDeCuentas` por `ConsignacionEnCuentas` + `RegistroDeCuentas`; `ClaseDeMedio` admite `CAR`/`OwnLoanPayoff`/
  `InternalOffset` sólo por el camino interno (prueba de que el puerto lo sigue rechazando).
- Candidatos: cuentas de aportes y ahorros con sus cuotas, según `Recogida.ProductosAdmitidos`.
- Anulación por `ReversionDeCuentas`; contabilidad por el mismo puente; completitud de la matriz.
- Pantalla: las cuentas en `ProductosParaRecoger` con sus modos.
- Pruebas de aplicación, anulación y contabilidad.

## E4 — Facturas a crédito de Comercial (HU7)

- Explorar primero si `AplicacionNoInteractiva` corre dentro de la transacción explícita del desembolso (R14); si no, se replantea la entrega
  con el dueño antes de construir.
- Puerto `IRecogidaEnRecaudo` (Common/Integration/Receipts) e implementación en Recaudo; canal `LoanDisbursement`; clase de medio
  `InternalOffset` (semilla); lectura del saldo de facturas por el puerto.
- Contabilidad: rol del medio en Recaudo y aviso de completitud cruzada.
- Pantalla y pruebas (incluido el cargo de Transporte pagado).

## Cierre (todas las entregas)

- Manual `docs/manual/cartera-recogida-de-creditos.md`; actualizar `cartera-desembolso-y-garantias.md` §5 (desactualizada),
  `cartera-libranza-y-pagadurias.md` (créditos cerrados con cuota enviada), el manual de caja (F7) y `cartera-integracion-contable.md` (el
  puente ya no exige hacer coincidir dos reglas).
- Viñeta en `CLAUDE.md` y totales remedidos.
- **e2e una sola vez al final**, en PostgreSQL y SQL Server: ampliar `DesembolsoYRecogidaTests` (proyección → diferencias → desembolso →
  comprobantes → Tesorería), libranza con crédito recogido (E2), cuentas (E3), facturas (E4).
- Diagnóstico antes de promover (lo corre la sesión coordinadora con el dueño): desembolsos con recogidas ya existentes y solicitudes con
  `PayoffRequest` en DEV, QA y PDN.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| La modalidad «todo menos la cuota causada» rompe la reconstrucción de créditos con pagos posteriores | Motor primero en E2 con casos dorados; la decisión del pago sella todo lo que la reconstrucción necesita |
| El DS nuevo cambia comprobantes de desembolsos con recogidas ya hechos | Sólo cambia con recogidas; diagnóstico previo; lo ya contabilizado no se toca |
| Interbloqueo entre el desembolso y operaciones de cuentas (E3) | Exploración del orden de cerrojos antes de construir |
| `AplicacionNoInteractiva` no corre en la transacción del desembolso (E4) | Exploración previa; si no, se replantea E4 con el dueño |
| Las e2e se corren sólo al final | Las unitarias cubren cada regla; casos dorados del motor y de contabilidad |
| No se pudo consultar PDN (créditos por nómina con cuota enviada) | Se pide a la sesión coordinadora o al dueño el conteo real antes de E2 |
