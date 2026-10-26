# Retomar la 015 — estado al pausar (2026-10-26, plan completo)

Rama `015-recaudo-tesoreria`, sólo commits locales (sin push). **Las 47 fases del plan están construidas**: la 46 en `2694c598`
(diagnóstico, exportación y plantilla de pendientes, sin borrar nada), `31ddea2d` (**el commit propio del retiro destructivo**) y
`9e078b5e` (runbook y notas); el Polish en `160a6bf4`. El árbol quedó limpio, sin fases a medias. Ninguna prueba se ha corrido.

## Hecho
| Entrega | Fases | Estado |
|---|---|---|
| Diseño | spec, plan, research, data-model, DT, contracts, tasks | 26 historias, FR-001–228, 497 tareas; constitución 2.0.1 (`RCD_`, `TES_`) |
| R0/T0 | 1–21 | construida; falta la fase 22 (verificación, congelamiento, despliegues) |
| R1 | 23–31 | construida |
| T1 | 32–35 | construida |
| R2 | 36–39 | construida (38 convenios y canales por archivo con `RCD_PaymentReferences` y dígito; 39 informes de canales); T0438 y T0439 (corrida y congelamiento de `RecaudoCanales`, promoción) sin hacer |
| T2 | 40–45 | construida (40 plataforma, 41 conciliación completa, 42 pagos a proveedores y cuentas por pagar, 43 liquidez y flujo, 44 adquirentes, 45 informes); T0442 (P5), T0466 (corrida y congelamiento de `TesoreriaAmpliada`) y T0467 (promoción) sin hacer |
| Retiro de la Tesorería heredada | 46 | construida, **sin aplicar en ningún ambiente**: par `RetiroDeLaTesoreriaHeredada` (guarda `TES.RetiroHeredado.Aprobado`, `THROW 50016`, irreversible) con la cabecera de aprobación **pendiente**; T0468, T0469 (correr el diagnóstico), T0473, T0478 y T0479 son del dueño o de ambiente |
| Polish | 47 | construida; T0481 sin marcar (correr las pruebas de textos y de arquitectura) |

Cada fase deja en tasks.md sus «Notas de la construcción» con las desviaciones; los nombres nuevos están en
decisiones-transversales §2.7.

## Regla vigente: pruebas al final
Por decisión del dueño (2026-10-23) **no se corre ninguna prueba hasta terminar todo el plan**: se escriben y se compila
por proyecto (`dotnet build -m:1`; `dotnet build-server shutdown` al cerrar cada fase). Todas las pruebas llevan la nota
«prueba escrita, sin correr». Al cerrar la fase 45 compilan sin errores API, Web, Web.Client, Persistence y los cinco
proyectos de pruebas.

## Orden para retomar
1. ~~Fase 46 y Polish (47)~~: hechas el 2026-10-26.
2. Corrida completa: Domain, Application, Architecture y Shared, uno a la vez
   (`dotnet test <proyecto> -m:1 --no-restore -- xUnit.MaxParallelThreads=4`); luego e2e en PostgreSQL y SQL Server.
   Ojo: `InformesDeTesoreriaT0Tests` exigía exactamente siete vistas y fallaba desde la fase 35 (corregido en la 45);
   pueden aparecer otras de ese tipo.
3. Puerta T0301 (e2e migradas de Cartera y Captaciones en verde) → T0296, T0297, T0306 (`RetiroDeLosDoblesDePrueba`),
   T0307, T0308, T0310.
   Ojo también: las e2e que bajan el historial (retiros de la 012, 013 y 014, `ComercioAmpliadoMigracionTests`) ahora llaman a
   `RetiroDeLaTesoreriaHeredadaTests.SacarElRetiroAsync`, porque el `Down()` del retiro lanza; si ese ayudante falla (usa piezas internas de
   EF: `IMigrationsModelDiffer`, `IMigrationsSqlGenerator`, `IHistoryRepository`), fallan las cinco a la vez.
4. Fase 22 y los congelamientos de cada entrega (huellas en `LasMigracionesCongeladasNoSeRegeneran`: `RecaudoCanales`
   en T0438, `TesoreriaAmpliada` en T0466), y las tareas de ambiente, con autorización del dueño.
5. El retiro de la Tesorería anterior, sólo con el dueño y en este orden (`docs/operaciones/tesoreria-retiro-heredado.md`): desplegar la
   versión **anterior** al retiro (`2694c598` o cualquier commit antes de `31ddea2d`); correr el diagnóstico en cada base (T0469); exportar
   para archivo y cargar los pendientes donde haya filas (T0473); respaldo por base; que el dueño, segundo revisor, apruebe por escrito y
   complete la cabecera (T0478, un commit que sólo toca comentarios); sembrar `TES.RetiroHeredado.Aprobado` sólo en las bases con filas; y
   desplegar el retiro (T0479). Antes, decidir P9 (tarjeta débito, T0468; rige D32: no se toca).

## Migraciones
Pares en orden: `PlataformaParaRecaudoYTesoreria`, `RecaudoNucleo`, `TesoreriaNucleo`,
`IntegracionContableDeRecaudoYTesoreria`, `TesoreriaConciliacionMinima`, `RecaudoYTesoreriaPuestaEnMarcha`,
`RecaudoCompleto`, `CarteraYCaptacionesConPartidaCitada`, `TesoreriaCompleta`, `RecaudoCanales` (9 tablas, R2) y
`TesoreriaAmpliada` (10 tablas, T2: `TES_LegacyPendingLoads`, `TES_ReconciliationRules`, `TES_BankNotes`,
`TES_PayrollDispersions`, `INV_SupplierInvoicePayments` (E-03), `TES_CashFlowCategories`, `TES_CashFlowProjections`,
`TES_LiquiditySnapshots`, `TES_AcquirerSettlements`, `TES_AcquirerSettlementLines`; por SQL idempotente, las adopciones de
los mensajes nuevos de T2 por INV, CAR y TES y las seis categorías sugeridas del flujo). Para regenerar `RecaudoCanales`:
borrar sus archivos y restaurar el snapshot de `524a19fe`; para `TesoreriaAmpliada`: borrar sus archivos, restaurar el
snapshot de `bcf9ac39`, generar y volver a poner el SQL de adopciones y categorías (el guion de la sesión lo hacía con
`post_t2.py`; si no está, copiar los bloques `migrationBuilder.Sql` del par actual). `ef migrations remove` intenta
conectarse: no usarlo. R0/T0, R1 y T1 ya no se regeneran. Después de T2 va el par destructivo `RetiroDeLaTesoreriaHeredada` (PostgreSQL `20261026192926`, SQL Server
`20261026192828`): si hubiera que regenerarlo, borrar sus cuatro archivos, restaurar el snapshot de `9e078b5e~2` (`2694c598`), generarlo con
`add-migration.ps1` y volver a armar la guarda, la cabecera y el `Down()` que lanza (copiarlos del par actual). Paridad OK (Application 74, Admin 15) y
`has-pending-model-changes` «No changes» en los dos motores con cadenas ficticias `Database__ConnectionStrings__*`.

## Decisiones pendientes del dueño (rige un defecto mientras tanto)
- Umbrales: 3 titulares (pagador tercero frecuente), 2 consignaciones (consignante repetido), 30 días (partida antigua).
- Conciliación: el abono llevado a Recaudo no pesa en la diferencia; alcance de la segregación del conciliador.
- Puesta en marcha: cheques girados en tránsito como línea informativa; saldo al corte = saldo en libros; cajero sin 2FA sólo avisa.
- Cierre de mes con cuadre provisional: negado sólo en el mes de la activación.
- «Gira cheques» deducido de una chequera activa; parámetros de RCD/TES sólo por plantilla (sin pantalla).
- R1: referencia exacta = número completo; defectos nuevos (egresos varios y aplicar al cargar apagados; consignación
  reportada: verificar primero, 3 y 5 días; GMF RCD 0,004 con el tributarista); anular la devolución de un saldo a favor de
  Cartera se confirma en la corrida; cotejar el layout UIAF antes de enero de 2027.
- T1/R2: hecho nuevo de Comercial `RemesaDelPuntoDeVentaDespachada`; hecho `ChequeRecibidoPagado` al cruzar el canje;
  definición de «firmante retirado»; la diferencia de un convenio sin explicar (recomendado: a `ComisionesDeConvenio` sólo con
  aprobación).
- T2 (recomendación entre paréntesis):
  - el hecho de **cobro tardío** `ChequeNoCobradoPagado` (CTP, `CuentaPorPagarChequesNoCobrados` → `Banco`) (dejarlo así);
  - facturas de proveedor **de contado** a Tesorería (no; sólo las a crédito con saldo);
  - vencimiento de la cuenta por pagar contable = fecha de su primer movimiento (sí, hasta que Contabilidad guarde vencimientos);
  - cuenta sin firmantes registrados: el lote no exige combinación de firmas (informar, no bloquear);
  - aprobación de notas bancarias directa por otra persona, sin el motor de montos (dejarla así); rol `OtrosCargosBancarios`;
  - si Captaciones **usa** el `SaldoDelFondoDeLiquidez` en su cálculo (mostrarlo al lado del constituido para cotejar);
  - foto de la posición a mano, sin tarea al cierre del día (sí, por ahora);
  - recaudos esperados = lo **programado**, no lo pendiente (sí, para la posición);
  - layout real de la liquidación de cada red (Redeban, Credibanco) para cargarlo como formato (lo aporta el dueño);
  - lo cobrado que la red no liquidó queda en `MedioPorCobrar` al aprobar (sí, sin cuenta de diferencias); tolerancia de
    adquirentes por defecto cero; `MedioPorCobrar` se parametriza también en la matriz de Tesorería (la contadora).
- Datos P1–P19 (tabla de research.md): personal y niveles, SARLAFT, matrices con la contadora, GMF, escenarios de no-go,
  fondo de liquidez, quién aprueba el cuadre, conservación.
- Retiro de la Tesorería anterior: P9 (tarjeta débito); confirmar que la factura heredada por pagar se migra como orden contra su cuenta por
  pagar contable (recomendado: sí, la deuda está en los saldos de apertura) y que un cheque heredado sin cobrar entra como cheque en tránsito de
  la puesta en marcha (`IsOpeningLoad`, recomendado: sí); el diagnóstico, el respaldo y la aprobación por base.

## Congelamientos pendientes
- `RecaudoCanales` (T0438) y `TesoreriaAmpliada` (T0466): tras la corrida de su entrega, en commit propio con sus huellas.
- `RetiroDeLaTesoreriaHeredada`: no tiene congelamiento propio en tasks.md; si se congela, sus comentarios de aprobación entran en la huella y
  completarlos exige actualizarla en el mismo commit (como en la 014).
- `RetiroDeLosDoblesDePrueba` (T0306): no existe todavía; se genera después de la puerta T0301 y se sella en T0337.

## Tareas de ambiente y del dueño (no se hacen sin autorización)
- Fase 22 (T0331 a T0341): la e2e completa una vez, sellos, ensayo, PR y despliegue de R0/T0.
- Promociones: R1 (T0405), T1 (T0425), R2 (T0439), T2 (T0467), con respaldo por base.
- Retiro de los dobles: T0296, T0297, T0306, T0307, T0308, T0310 tras la puerta T0301.
- Retiro de la Tesorería anterior: T0468, T0469, T0473, T0478, T0479.
- MAUI a mano y QA por rol, como en las features anteriores.
