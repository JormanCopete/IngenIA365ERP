# Retomar la 015 — estado al pausar (2026-10-27, plan completo, e2e en verde en los dos motores y dobles retirados)

Rama `015-recaudo-tesoreria`, sólo commits locales (sin push). **Las 47 fases del plan están construidas**: la 46 en `2694c598`
(diagnóstico, exportación y plantilla de pendientes, sin borrar nada), `31ddea2d` (**el commit propio del retiro destructivo**) y
`9e078b5e` (runbook y notas); el Polish en `160a6bf4`. El árbol quedó limpio, sin fases a medias. **Las pruebas sin contenedores se corrieron
en verde el 2026-10-26 y las e2e el 2026-10-26/27, en PostgreSQL y SQL Server** (abajo); tras la puerta T0301 se retiraron los dobles de prueba
(`72800349`).

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
| Polish | 47 | construida; T0481 hecha en la corrida final |
| Corrida final sin contenedores | — | en verde el 2026-10-26: `9b2fa7bb`, `18aba5e8`, `ad79eaf9`, `6f7e5749`, `d51c4b3e` |
| Corrida e2e y retiro de los dobles (fase 19) | — | e2e en verde en los dos motores el 2026-10-26/27 (salvo 15 de adjuntos S3, ambiente); T0301, T0296–T0308, T0310 hechas: `2e361377` … `0a27d672` |

Cada fase deja en tasks.md sus «Notas de la construcción» con las desviaciones; los nombres nuevos están en
decisiones-transversales §2.7.

## Pruebas: corrida e2e (2026-10-26/27)
Con Docker, en PostgreSQL y SQL Server (`DB_PROVIDER`). Cifras al cerrar, con lo que cayó por el contenedor repetido aislado:

| Lote | PostgreSQL | SQL Server |
|---|---|---|
| Receipts + Treasury (30) | 30 pasan / 0 fallan / 0 omitidas (primera: 29 fallan, el login en 500) | 30 / 0 / 0 |
| Lending, puerta T0301 con los dobles presentes (205) | 195 / 0 / 10 (primera: 11 fallan) | 195 / 0 / 10 |
| Lending + Receipts + Treasury tras el retiro (235) | 225 / 0 / 10 | 225 / 0 / 10 |
| Suite completa (725) | 691 / 0 / 19, **15 sin correr** | 691 / 0 / 19, **15 sin correr** |

Las 15 sin correr son las de adjuntos sobre S3: `quay.io/minio/minio:latest` respondió 401 toda la corrida (ambiente). Repetidas aisladas: cuatro de
`AhorroContractualTests` y una de `IntegridadDeAuditoriaTests` en PostgreSQL (`57P03` con los dos motores en paralelo) y `GruposDelAsociadoTests` (un
choque de documentos de prueba, corregido). Las 10/19 omitidas lo son con su motivo (`RUN_PERF_TESTS`, `[Fact(Skip)]`). Tras el retiro, sin
contenedores: Domain 2.789, Application 6.972, Architecture 858, Shared 514, todas en verde. Qué se corrigió y por qué, en las «Notas de la corrida e2e» de
tasks.md (antes de «Dependencies»); nombres nuevos en DT §2.7 («Fijados en la corrida e2e»). Commits `2e361377` … `0a27d672`; el retiro de los dobles, en
`72800349`.

## Pruebas: corrida final sin contenedores (2026-10-26)
Por decisión del dueño (2026-10-23) no se corrió ninguna prueba hasta terminar el plan. El 2026-10-26 se corrieron los cuatro proyectos sin
contenedores, uno a la vez (`dotnet test tests/IngenIA365ERP.<X>.Tests -m:1 --no-restore -- xUnit.MaxParallelThreads=4`, `dotnet build-server
shutdown` entre uno y otro), hasta verde:

| Proyecto | Primera corrida | Al cerrar |
|---|---|---|
| Domain | 2.789 pasan, 0 fallan, 0 omitidas | 2.789 pasan, 0 fallan, 0 omitidas |
| Application | 5.288 pasan, 1.698 fallan (el catálogo de mensajes no arrancaba; quitado eso, 65), 0 omitidas | 6.986 pasan, 0 fallan, 0 omitidas |
| Architecture | 838 pasan, 21 fallan, 0 omitidas | 859 pasan, 0 fallan, 0 omitidas |
| Shared | 521 pasan, 0 fallan, 0 omitidas | 521 pasan, 0 fallan, 0 omitidas |

Las e2e (`IngenIA365ERP.API.IntegrationTests`) compilan y **siguen sin correr**: van aparte, con Docker, en PostgreSQL y SQL Server.
`has-pending-model-changes` «No changes» en los dos motores; ninguna migración se tocó. Qué se corrigió y por qué, en las «Notas de la corrida final»
de tasks.md (después de la fase 47) y los nombres nuevos en decisiones-transversales §2.7 («Fijados en la corrida final»). Lo que el dueño tiene que
confirmar, en «Decisiones pendientes» (abajo, «Corrida final»).

## Orden para retomar
1. ~~Fase 46 y Polish (47)~~: hechas el 2026-10-26.
2. ~~Corrida de Domain, Application, Architecture y Shared~~ y ~~de las e2e en los dos motores~~: en verde el 2026-10-26/27 (arriba). Falta correr las
   15 de adjuntos sobre S3 cuando `quay.io` vuelva a servir la imagen de MinIO, y **escribir** las nueve e2e pendientes (T0204, T0215, T0224, T0237,
   T0255, T0274, T0285, T0313, T0319) y correr la de rendimiento de la conciliación (`RUN_PERF_TESTS=1`).
3. ~~Puerta T0301 → T0296, T0297, T0306, T0307, T0308, T0310~~: hechas el 2026-10-26/27 (`72800349`). Queda T0309 (ambiente): quitar
   `Lending:TestDoubles:Enabled` de GitOps de DEV y QA y reescribir `docs/operaciones/cartera-dobles-de-prueba.md` como «dobles retirados».
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
`has-pending-model-changes` «No changes» en los dos motores con cadenas ficticias `Database__ConnectionStrings__*`. Después del retiro va el par **de datos** `RetiroDeLosDoblesDePrueba` (PostgreSQL `20261027024928`, SQL Server
`20261027024843`, T0306): cierra `ValidTo` de los dobles vigentes y de sus adopciones; no se regenera (si hiciera falta: borrar sus cuatro archivos,
restaurar el snapshot del commit anterior a `72800349`, generarlo con `add-migration.ps1` y volver a poner su SQL). `has-pending-model-changes` «No
changes» en los dos motores y paridad 75/15 tras generarlo.

## Decisiones pendientes del dueño (rige un defecto mientras tanto)
- Corrida e2e (2026-10-27), a confirmar:
  - una orden de giro de Captaciones sin cuenta destino (asociado sin cuenta de depósito) queda `DestinationMissing` en Tesorería sin salida: no se
    puede completar la cuenta ni girar por cheque; sólo la resuelve un regiro de Cartera con destino. Recomendado: que Tesorería la gire por cheque o
    complete el destino;
  - `ConfirmacionDeRecibo` llama al contrato una vez por porción cuando un ítem se paga con dos medios (la simulación de cada pago la exige el contrato
    interactivo de la 013); con un solo medio por ítem sigue siendo una llamada;
  - el retiro de los dobles no deja el evento de auditoría por fila (la cadena de sellos no se escribe desde una migración); el motivo queda en la migración;
  - la ruta sin permiso responde 404 como la inexistente (FR-017): `SeguridadDeRecaudoYTesoreriaTests` esperaba 403 y se alineó a la plataforma.
- Corrida final (2026-10-26), a confirmar:
  - tres pruebas de arquitectura cambiaron de alcance, no de regla: `LosComandosDeRecaudoYTesoreriaLlevanClave` deja fuera a los consumidores de
    la bandeja y a las dos descargas GET (si las descargas deben llevar clave, hay que pasarlas a POST); `TextosDeRecaudoYTesoreriaTests` exenta los
    permisos de los `PermissionGate` (su exención no funcionaba) y los códigos que la pantalla compara para traducir; `LaContabilidadNoTieneCuentasEnCodigo`
    permite los ejemplos de la plantilla de Recaudo;
  - `CancelacionDeCuenta.EntregaAsync` (código de la 014, en producción) ahora sube la entrega hacia el saldo cuando el GMF redondeado dejaba unos
    pesos y la cuenta en `Closing`; afecta también la cancelación por pantalla (las pruebas de la 014 siguen en verde);
  - el rol `PuenteDeDevolucion` de la matriz de Tesorería no lo mueve ningún hecho (la devolución se gira por `PuenteDeGiroDelOrigen`); recomendado:
    dejarlo o retirarlo del catálogo de roles de Tesorería.
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
- `RetiroDeLosDoblesDePrueba` (T0306): generado el 2026-10-26 (PostgreSQL `20261027024928`, SQL Server `20261027024843`), sólo datos; se sella en T0337.
- Todos **listos para congelar**: las e2e de R1, T1, R2 y T2 están en verde (T0404, T0424, T0438, T0466) y T0337 puede sellar; quedan para cuando el
  dueño decida promover.

## Tareas de ambiente y del dueño (no se hacen sin autorización)
- Fase 22 (T0331 a T0341): la e2e completa una vez, sellos, ensayo, PR y despliegue de R0/T0.
- Promociones: R1 (T0405), T1 (T0425), R2 (T0439), T2 (T0467), con respaldo por base.
- Retiro de los dobles: hecho en código; T0309 (GitOps de DEV y QA y su runbook) sigue de ambiente.
- Retiro de la Tesorería anterior: T0468, T0469, T0473, T0478, T0479.
- MAUI a mano y QA por rol, como en las features anteriores.
