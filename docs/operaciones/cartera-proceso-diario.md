# Cartera: el proceso diario

> Feature 013 (cartera financiera), fase 8 (US4, entrega K1). Runbook para operaciones y el jefe de cartera. Qué se
> causa y cómo se lee una liquidación está en [docs/manual/cartera-causacion-y-liquidacion.md](../manual/cartera-causacion-y-liquidacion.md);
> el contrato, en [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §16.4 y §16.5 y
> [data-model.md](../../specs/013-cartera-financiera/data-model.md) §22.

El **`ProcesadorDeCartera`** es un servicio de fondo de la API (lo arranca sólo `Program.cs`; el DbMigrator nunca). Cada
`IntervalSeconds` recorre las cooperativas activas **con Cartera activada** y, en cada una, conduce sus corridas
(`LND_ProcessRuns`) en este orden: la que está corriendo, la corrección por valor nacional pendiente, el **ciclo diario**
siguiente (uno a la vez, hasta hoy, el de hoy sólo pasada `StartTimeLocal`) y, desde US9, el cierre mensual o su
reapertura pedidos. Todo lo hace dentro de `IEjecutorEnCooperativa`, con el actor «Proceso de integración» y la base de
la cooperativa; una cooperativa que falla no detiene a las demás.

## 1. Opciones `Lending:Process` y el arrendamiento

| Clave | Defecto | Qué es |
|---|---|---|
| `Enabled` | `true` | Apaga el procesador entero (la fixture de pruebas lo apaga y conduce las tandas a mano). |
| `IntervalSeconds` | 30 | Cada cuánto empieza una pasada. |
| `BudgetSeconds` | 60 | Cuánto trabaja en una cooperativa por pasada; tiene que ser menor que el arrendamiento. |
| `LeaseTtlSeconds` | 120 | Vida del arrendamiento `lending.process` de cada base (`COR_BackgroundLeases`). |
| `MaxParallelCooperatives` | 2 | Cooperativas que se recorren a la vez. |
| `StartTimeLocal` | `00:30` | Hora local (America/Bogota) desde la que nace el ciclo del día. |
| `BatchSize` | 250 | Créditos por tanda (un `SaveChanges` por tanda). |

El arrendamiento `lending.process` impide que dos réplicas de la API trabajen la misma cooperativa: quien lo tiene lo
renueva entre tandas; si la réplica muere, otra lo toma cuando vence. Una opción incoherente (presupuesto mayor o igual
al arrendamiento) impide arrancar con un mensaje que dice cuál.

**Cada tanda**: transacción explícita; cerrojo de los créditos **con salto** (`FOR UPDATE SKIP LOCKED` en PostgreSQL,
`UPDLOCK, ROWLOCK, READPAST` en SQL Server): un crédito que una operación tiene tomado no se espera, se cuenta como
saltado y lo toma el ciclo siguiente (que lo pone al día sin duplicar). Si la tanda entera no se puede guardar, se
reintenta crédito por crédito y el que vuelve a fallar queda en `LND_ProcessRunErrors`. Al terminar, la corrida guarda
sus totales (`TotalsJson`) y su huella (`ResultSha256`) como evento resumen y verifica una muestra del libro.

## 2. Ver, ordenar y reintentar corridas

- **Ver**: `GET /api/lending/process-runs?kind=&status=&from=&to=` y `GET /api/lending/process-runs/{id}`
  (`Lending.Processes.View`): estado, avance (créditos recorridos de los del día), quién la pidió (persona o proceso),
  tandas, partes, saltados y los errores por crédito (número, paso, código, mensaje, intentos), sin datos del deudor.
  En la interfaz se ven en el detalle de la bandeja de mensajes de Cartera cuando exista (DT §2.16 no fija pantalla).
- **Ordenar** un ciclo (`POST /api/lending/process-runs` con `{ "kind": "DailyCycle", "date": "aaaa-mm-dd" }`,
  `Lending.Processes.Run`, `Idempotency-Key`): **sólo encola** (202); la corrida la corre el procesador con la persona
  como quien la pidió. Otra del mismo día pedida o corriendo responde `Lending.Process.AlreadyRunning`; una ya terminada,
  `Lending.ProcessRun.AlreadyCompleted` (se reintenta, no se repite); una fecha futura, `Lending.ProcessRun.FutureDate`.
  El cierre y la corrección no se ordenan aquí (`Lending.ProcessRun.KindNotOrderable`).
- **Reintentar** (`POST /api/lending/process-runs/{id}/retry` con `{ "reason": "…" }`): sólo una corrida `Failed` o
  `CompletedWithErrors`. Vuelve a recorrer desde el principio y **cada crédito retoma desde su propia marca**: lo ya
  causado no se vuelve a causar. Los errores anteriores quedan; los resuelve el intento que tenga éxito.

## 3. Un día sin causar y las alertas

La tarea **`cartera.vigilancia`** corre cada hora y, pasada la tolerancia (06:00), alerta:

| Alerta | Cuándo | A quién | Qué hacer |
|---|---|---|---|
| `Cartera.ProcesoDiarioNoCorrio` | el ciclo de ayer no está `Completed` | jefe de cartera (procesos) | Ver la corrida; si falló, reintentarla; si no existe, revisar que el procesador esté vivo (logs `[Cartera.PasadaFallida]`, `[Cartera.CooperativaFallida]`) y el arrendamiento. |
| `Cartera.CreditoSinCausar` | un crédito quedó en los errores y su marca no avanzó | jefe de cartera | Leer el código del error (por ejemplo `Lending.Replay.OperationKindNotSupported`, `Lending.Rules.UnknownVersion`); corregir la causa y reintentar. |
| `Cartera.ValorNacionalFaltante` | una modalidad no tiene usura certificada para el día (error de grupo, sin crédito) | jefe de cartera y quien revisa valores nacionales | Registrar la usura en el registro central y replicar; el ciclo siguiente pone al día los créditos de esa modalidad. Los demás créditos no se detienen. |
| `Cartera.TareaDiariaNoCorrio` | una tarea diaria de Cartera no dejó su marca | jefe de cartera | Revisar el programador de tareas. |
| `Cartera.LibroDescuadrado` | la verificación encontró que el libro de un crédito no cuadra con su reconstrucción | jefe de cartera y contadora | Ver §5. No se corrige editando. |
| `Cartera.IndiceDesactualizado` · `Cartera.UsuraTopada` | el último IBR/IPC es viejo · la tasa del día se topó en la usura | quien revisa valores nacionales · líneas | Informativas. |

**No hay que hacer nada para recuperar días**: el ciclo siguiente causa cada día pendiente con su tasa. Si se quiere
adelantar, se ordena el ciclo de hoy.

## 4. Corrección de un valor nacional

Cuando el maestro corrige una usura ya causada (`POST /api/saas/national-values/{id}/corrections`), la **réplica**
encola en su misma transacción una corrida `NationalValueCorrection` (`Requested`, pedida por el proceso, con los días
afectados). El procesador la toma **antes** que el ciclo del día. Registra por crédito, día, concepto y cuota los
ajustes (`AccrualAdjustment`, partes `VNC-…` con `AjusteDeCausacionPorFechaValor`, fecha contable del período abierto)
y, si hubo cobro de más, el saldo a favor (desde US5). No se ordena a mano ni se reintenta como un ciclo: si falla,
queda `Failed` con sus errores y el procesador la vuelve a tomar.

## 5. Verificar el libro

`POST /api/lending/integrity/verify` (`Lending.Integrity.Verify`, sin clave: es una consulta) con
`{ "loanPublicIds": [...] }`, `{ "sampleSize": 200 }` o vacío (todos), y opcionalmente `upTo`. Responde
`{ checked, upTo, mismatches: [{ loanPublicId, number, date, concept, ledger, reconstruction }], durationMs }`.
**No corrige nada**; cada crédito con diferencias levanta `Cartera.LibroDescuadrado`. Ante una diferencia:

1. Leer el libro del crédito (pestaña «Libro» de `/cartera/creditos/{id}`) desde el primer día descuadrado y sus
   operaciones con su comprobante.
2. Si la diferencia nace de una operación registrada después de causar (una fecha valor anterior que no se concilió),
   es un defecto: reportarlo con el número del crédito, el día y el concepto.
3. Nunca escribir en `LND_LoanTransactions` a mano (Principio XI; la guarda de inmutabilidad lo rechaza).

## 6. Volumen y partición

Con 50.000 créditos el ciclo escribe unas 50.000 posiciones diarias (`LND_LoanDailyPositions`, ~18 millones al año) y
del orden de 150.000 filas de libro por día, en partes de hasta 250 créditos por tipo y tanda. La meta es una pasada
completa en **menos de 30 minutos** (SC-007); la mide `CausacionDiariaPerformanceTests` con `RUN_PERF_TESTS=1`
(quickstart §1.8), que por ahora siembra la cartera copiando un crédito desembolsado porque la carga de la migración
llega con US8. **Al pasar de 20.000 créditos**, particionar `LND_LoanDailyPositions` por año (D13) es un cambio de
esquema por migración con respaldo previo; `LND_LoanTransactions` y `COR_IntegrationMessages` se vigilan con la misma
prueba. El índice `IX_LND_LoanDailyPositions` `(PositionDate, BranchId, CreditLineId)` sostiene los informes del día.
