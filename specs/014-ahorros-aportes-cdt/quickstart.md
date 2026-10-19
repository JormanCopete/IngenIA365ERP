# Quickstart: probar las captaciones de la Cartera (feature 014)

Guía para verificar cada historia de punta a punta en local y en las pruebas e2e. Los nombres de rutas,
permisos y tablas son los de [research.md](research.md) §0 con sus enmiendas (§5–§25); los cuerpos, los de
[contracts/api.md](contracts/api.md), cotejado con el código al cierre (2026-10-19, T225–T233).

**Estado al cierre (2026-10-19)**: `CatalogoDeCartera.EntregaVigente = K11` (todas las entregas de captaciones
desplegadas: nada pide fijar la entrega en las pruebas). El esquema de la 014 vive en **una sola migración par**,
`CaptacionesAportes` (PostgreSQL `20261019104128`, SQL Server `20261019104056`), detrás de `RetiroDeCaptacionesHeredadas`
(`20261018143710` / `20261018143627`): las `CaptacionesAhorro`, `CaptacionesCdat`, `CaptacionesAsamblea` y
`CaptacionesLiquidez` que preveía tasks.md **no nacieron** (research E35). Las dos están **congeladas desde el
2026-10-19 (T225)**, junto con las de la 013, y lo vigila la prueba de arquitectura `LasMigracionesCongeladasNoSeRegeneran`:
todo cambio de esquema posterior es una migración nueva, nunca una regeneración.

## 0. Preparación

1. Docker Desktop corriendo (las e2e levantan PostgreSQL o SQL Server, Mongo y Redis con Testcontainers). Es **lo único**
   que hace falta: el worktree no tiene `appsettings.Development.json` y no lo necesita, porque la fixture
   `CentralIdentityApiFixture` pone su propio WebAuthn, su maestro y su par de llaves JWT por corrida.
2. La 013 completa y verde en la rama base.
3. Compilar y correr las pruebas sin contenedores:

```bash
dotnet build IngenIA365ERP.slnx
```

```bash
dotnet test tests/IngenIA365ERP.Domain.Tests --filter "FullyQualifiedName~Lending"
```

```bash
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~Lending"
```

```bash
dotnet test tests/IngenIA365ERP.Architecture.Tests
```

4. Las e2e de captaciones (`tests/IngenIA365ERP.API.IntegrationTests/Lending/Deposits/`, colección «Captaciones e2e»,
   `CaptacionesCollection`; `PagaduriasTests` corre en la colección «Cartera e2e» de la 013):

```bash
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~IntegrationTests.Lending.Deposits"
```

En SQL Server, con `DB_PROVIDER=SqlServer`. Las dos pruebas de volumen (`CausacionDeCaptacionesPerformanceTests`, SC-006,
y `MigracionPerformanceTests`, SC-011) sólo miden con `RUN_PERF_TESTS=1`; sin ella se reportan omitidas con su motivo.
Por decisión del dueño, las e2e de la 014 se escribieron y compilaron fase a fase y se corren **una sola vez al final**.

5. Cooperativa de prueba (`CaptacionesE2E`): una cooperativa **aislada** por nombre (prefijo `cap_`) con la Cartera de
   créditos activada con carga vacía por las rutas reales de la 013 (`CarteraE2E.ActivarCarteraAsync`, que registra el
   recaudador `TST` y la tesorería `TES` de prueba). `CooperativaConCaptacionesAsync` registra el recaudador de cuentas
   (`ProveedorDeCuentas` adoptado) y la tesorería con `OrdenDeGiroEmitida` v2 adoptada, cumple los requisitos de la
   activación como una cooperativa real (`RequisitosDeCaptacionesAsync`: roles sugeridos de captaciones, perfil
   regulatorio con `mayTakeDeposits`, matriz mínima de reglas; ya no se dispensan fuera de producción, research §13 D14,
   D18) y activa aportes y captaciones sin carga por `POST /api/lending/deposits/activation`. Los parámetros nuevos se
   leen con sus defectos «pendiente de validar» del catálogo (`ParametrosDeCartera.Definiciones`).

## 1. Casos dorados (SC-002, SC-019)

`tests/IngenIA365ERP.Domain.Tests/Lending/Deposits/Casos/` (67 JSON, calculados y verificados con `calcular.py --verificar`)
debe pasar al centavo; entre ellos, como mínimo:

| Caso | Archivo | Esperado |
|---|---|---|
| CDAT 10.000.000, 180 días, 10 % EA, al vencimiento, base 365 | `cdat-01-vencimiento-base-365` | 481.244 |
| Ídem base 360 | `cdat-02-vencimiento-base-360` | 488.088 |
| Ídem mensual vencido base 360 | `cdat-03-mensual-vencido` | 6 × 79.741 = 478.446 |
| Ídem mensual anticipado | `cdat-04-mensual-anticipado` | 79.111 por mes |
| Ahorro 3 % EA, saldos 2 M / 5 M / 1,5 M por 10 días, saldo diario | `interes-01-saldo-diario` | 6.884, sin retención (umbral) |
| Ídem saldo mínimo | `interes-02-saldo-minimo` | 3.649 |
| Contractual 12 × 200.000 al 6 % / al 8 % | `contractual-01-…`, `contractual-02-…` | 2.465.306 / 2.486.777 (bonificación 21.471) |
| GMF: retiros de 20.000.000 en el mes de la cuenta exenta (UVT 52.374) | `gmf-01-tope-cruzado` | 6.676 |
| GMF: redención CDAT abonada en cuenta (10.488.088) | `gmf-02-redencion-cdat-abonada` | 41.952 |

Y **toda** la suite `dotnet test tests/IngenIA365ERP.Domain.Tests --filter Lending` sigue verde sin tocar ningún JSON de
crédito (`Domain.Tests/Lending/Casos` tiene 45, más los de libranza, novedades, riesgo, cobranza y demás en sus carpetas;
research E08). Los siete archivos regulatorios dorados están en
`tests/IngenIA365ERP.Application.Tests/Lending/Deposits/Reports/Casos/*.esperado.txt` (regenerables sólo con
`CARTERA_ESCRIBIR_ESPERADO=1`).

## 2. Recorrido por historia

| Historia | Pantalla | Pasos | Comprobación | e2e |
|---|---|---|---|---|
| US1 Líneas | `/cartera/lineas?naturaleza=aportes` y `?naturaleza=captacion` | Crear línea de aportes y línea a la vista con tramos; nueva versión con fecha futura | Consulta a fecha anterior devuelve la versión anterior; cooperativa sin autorización a captar rechaza activar la línea a la vista (`Lending.Savings.LineNotAllowedByProfile`) | `LineasDeCaptacionTests` |
| US2 Apertura | `/cartera/ahorros` | Abrir cuenta conjunta con dos titulares y beneficiarios 60/40 (con la constancia, D34); marcar exenta de GMF; abrir otra exenta | La segunda marca se rechaza con `Lending.Gmf.AlreadyMarked`; dos aperturas simultáneas reciben números consecutivos (`{código de la línea}-000001`) | `AperturaDeCuentasTests` |
| US2 Menor | `/cartera/ahorros/{id}` | Abrir cuenta de un menor con su representante; el subpaso de mayoría de edad del ciclo diario corre el día del cumpleaños 18 | El representante deja de firmar desde la víspera, la cuenta queda «actualizar datos» (`POST /accounts/{id}/data-update` la levanta), salen los dos avisos `MAYOREDAD` | `PartesYExencionTests` (Application) |
| US3 Aportes | `/cartera/aportes` y `/cartera/aportes/{persona}` | Generar cuotas con el ciclo diario; pagar por el recaudador de prueba (`POST /api/lending/test-doubles/collector/accounts/deposits`, `ConsignarAsync`); consultar el resumen | Saldo del resumen (`GET /api/lending/contributions/summary`) = `IConsultaDeAportes` v2 = extracto de aportes; la importación de `contribution-balances` responde `Lending.Contributions.UseMigration` | `AportesTests` |
| US4 Movimientos | `/cartera/ahorros/{id}` (movimientos) y `/cartera/captaciones/aprobaciones` | Consignar 1.000.000; retirar con un solo firmante en manejo conjunto; repetir la clave; retirar sobre la atribución | Rechazo por firmas; mismo comprobante sin duplicar; 202 `PendingApproval` en espera de un aprobador presente con segundo factor (D53) | `MovimientosTests`, `CargosYDebitoAutomaticoTests`, `ContratoDeCuentasCargaTests` |
| US5 Intereses | `/cartera/ahorros/{id}` (intereses) | Saldos variables un mes; correr el ciclo diario; liquidar dos veces | Interés igual al caso dorado; la segunda liquidación responde 200 con la primera (`alreadyExisted`) | `InteresesTests` |
| US6 CDAT | `/cartera/cdat`, `/cartera/cdat/nuevo`, `/cartera/cdat/{id}` | Constituir 10.000.000 a 180 días con pago mensual y fondearlo (`POST /term-deposits/{id}/fund` o consignación exacta); dejar vencer sin instrucción; pedir tasa negociada de 1,2 puntos con atribución de 0,5 | Seis pagos de 79.741 menos la retención del CDAT (por defecto el umbral diario no aplica al CDAT, `Retencion.AlcanceDelUmbral`); prórroga tras la gracia con versión 2 y título `-2`; tasa negociada `PendingDecision` en espera del aprobador | `CdatTests` |
| US7 Contractual | `/cartera/ahorros` (apertura con cuota y plazo) y la ficha | 12 cuotas, pagar 10 | Incumplido con la tasa castigo (`ContractualBreach`, aviso `CONTRATINC`) | `AhorroContractualTests` |
| US8 Contabilidad | `/contabilidad/cartera/matriz`, `/contabilidad/cartera/completitud`, `/cartera/conciliacion-contable` | Con la matriz completa: consignar, causar, liquidar; quitar una regla con `Contabilidad.FaltaDeRegla = Bloquear` | Comprobantes `MA`, `CC`, `TD` correctos; la pantalla (y el contrato) bloquean con la regla faltante (`Lending.Prevalidation.NotPostable`); conciliación en cero; Contabilidad no cierra el mes sin el cierre de la Cartera | `ContabilidadDeCaptacionesTests` |
| US9 Migración | `/cartera/captaciones/migracion` y `/cartera/captaciones/activacion` | Cargar 40 cuentas de aportes con un error en la fila 37; corregir; cuadrar; activar | Error por fila y columna sin guardar nada; saldo inicial sin hechos; activación al día siguiente del corte. El volumen de 500 cuentas lo mide `MigracionPerformanceTests` | `MigracionDeCaptacionesTests` |
| US10 Retiro | `/cartera/retiros-asociado` y `/cartera/retiros-asociado/{id}` | Radicar retiro; aprobar con el punto de un acta cerrada; liquidar con un crédito vivo; reversar el pago del cruce | Mensaje `RetiroDeAsociadoAprobado` a Asociados; cruce aplicado en el mismo comprobante que el pago (D45); devolución por orden de giro; retiro «por rehacer» (`ToRedo`) al reversar | `RetiroDeAsociadoTests` |
| US11 Embargos | `/cartera/embargos` | Embargo sobre una persona con dos cuentas sobre el límite; embargo sólo sobre aportes; embargo por alimentos | Sólo el exceso retenido; el de aportes nace `Rejected` con la respuesta «aportes inembargables» registrada (aviso en el 201); alimentos sin límite; quien registró no levanta | `EmbargosTests` |
| US12 Asamblea | `/cartera/asamblea` | Revalorización al 5 % con IPC 4 % y fondo menor; aplicarla otra persona con el acta | Limitada al IPC y prorrateada al fondo; aplicar dos veces responde `AlreadyApplied` | `AsambleaTests` |
| US13 Documentos | `/cartera/ahorros/{id}` (documentos), `/cartera/cdat/{id}`, `/cartera/documentos` | Extracto, certificado tributario anual; reversar una retención de diciembre | Certificado anulado y nuevo distinto; el mismo extracto sin cambios devuelve la misma copia | `DocumentosDeCaptacionesTests` |
| US14 Inactividad y fallecimiento | `/cartera/fallecimientos`, `/cartera/no-reclamados` | Dejar una cuenta sin movimientos; registrar fallecimiento con beneficiarios 50/50 y que otra persona apruebe la entrega | Inactiva (el abono de interés no la reactiva); dos órdenes de giro | `CicloDeVidaDeCuentasTests` |
| US15 Liquidez | `/cartera/fondo-de-liquidez` | CDAT de varios vencimientos y permanente sólo retirable al retiro; tomar la foto; registrar una inversión con soporte | Requerido por banda igual al cálculo manual; alerta `Cartera.FondoDeLiquidezInsuficiente`; IRL en `GET /liquidity-fund/risk` | `FondoDeLiquidezTests` |
| US16 Seguridad | (por API y `/admin/auditoria`, pestaña «Integridad») | Usuario de otra oficina consulta una cuenta y la usa como destino de un traslado; verificar la cadena de auditoría | Respuesta de inexistente con `Lending.Access.OutOfScope`; lecturas auditadas; cadena verificada | `SeguridadDeCaptacionesTests` |
| US17 Informes | `/cartera/captaciones/reportes-regulatorios`, `/cartera/informes` | Con el cierre mensual de la Cartera: validar y generar el SICSES de captaciones (9026) de un mes | El 9026 cuadra con `deposit-balances`; advertencia «formato sin cotejar» (`Lending.Regulatory.LayoutSinCotejar`); sin cierre, `Lending.Regulatory.PeriodNotClosed` | `InformesDeCaptacionesTests` |

## 3. Pagadurías (FR-125)

Pantalla `/cartera/pagadurias/{id}`, pestaña «Grupos y equivalencias». e2e `PagaduriasTests` (colección «Cartera e2e»),
que cubre los pasos 2 a 4 con créditos; las cuotas de cuentas en una pagaduría las prueban las pruebas de aplicación
(research §13 D36: falta un escenario e2e con asociados, aportes y empresa empleadora).

1. Crear un convenio con dos grupos de archivo (créditos; aportes y ahorros) y un concepto equivalente
   que agrupa la cuota de aportes y la de ahorro permanente con reparto por prelación.
2. Generar los archivos (`POST /api/lending/payroll-deduction-files` con `fileGroupCode`): sale uno por grupo, con el
   formato de ida de su grupo. La cuenta va a la pagaduría que es la empresa empleadora de la ficha del asociado (D36).
3. Cargar una respuesta que descuenta menos por el concepto agrupado: lo cubierto se aplica por
   prelación y el resto queda exigible como «no descontado por la pagaduría».
4. Cargar una respuesta con un empleado sin equivalencia: la fila queda en «no aplicados»
   (`GET /api/lending/payroll-deduction-files/{id}/unmatched`).

## 4. Retiro de lo heredado (R28)

Receta completa: `docs/operaciones/captaciones-retiro-heredado.md` (§9a, decisión del dueño del 2026-10-18, D15). e2e
`RetiroDeCaptacionesHeredadasTests`.

1. Correr `diagnostico-captaciones-heredadas.sql` en local, DEV, QA y producción y guardar el resultado.
2. Si todo está en cero: respaldo por base y la fila `COR_SystemSettings` `LND.CaptacionesHeredadas.RetiroAprobado`
   (el dueño es el segundo revisor y autoriza: no hay clientes en producción).
3. Aplicar la migración `RetiroDeCaptacionesHeredadas` con el DbMigrator. Sin la fila o con filas, se
   niega con el nombre de la tabla.
4. Verificar que `/api/savings/*`, `/api/cdt/*` y las pantallas heredadas responden 404 y que el menú no
   las enlaza (`TodoEnlaceDelMenuTieneSuPagina`).
