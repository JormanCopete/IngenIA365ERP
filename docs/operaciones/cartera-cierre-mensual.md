# Runbook: el cierre mensual de Cartera

> Feature 013, fase 12 (US9, K1). Para el jefe de cartera y quien opera el proceso de Cartera. Qué es cada cosa está en el manual
> [cartera-riesgo-y-cierre-mensual.md](../manual/cartera-riesgo-y-cierre-mensual.md); el proceso diario, en
> [cartera-proceso-diario.md](cartera-proceso-diario.md).

## 1. Antes del fin de mes

1. Que la **contadora** haya registrado el patrimonio técnico y la relación de solvencia vigentes (`/contabilidad/indicadores-regulatorios`).
   Más viejos que `Contabilidad.IndicadoresVigenciaMeses` o sin dato: el deterioro no descuenta aportes y la concentración bloquea.
2. Que la **matriz contable de Cartera** tenga reglas para los roles del cierre (saldo por categoría, deterioro individual y general,
   recuperación) en cada categoría: `/contabilidad/cartera/completitud`.
3. Las calificaciones por análisis del mes, registradas antes de ordenar (el cierre las lee a R).

## 2. Ordenar el cierre de M (al empezar M+1)

1. Que el **ciclo diario** haya corrido hasta el último día de M (lo normal: el ciclo del 1.º de M+1 lo deja).
2. `/cartera/cierres` → año y mes → «Revisar requisitos». Cada requisito incumplido dice qué falta:

   | Código | Qué hacer |
   |---|---|
   | `Lending.MonthlyClose.PreviousMissing` | cerrar antes el mes anterior (o, si es el primero, revisar que la activación dejó el cierre base) |
   | `Lending.MonthlyClose.AccountingPeriodClosed` | Contabilidad ya cerró M: reabrir el período contable con la contadora |
   | `Lending.MonthlyClose.AccrualBehind` | correr o reintentar el ciclo diario (`/cartera/bandeja-de-mensajes`, corridas) |
   | `Lending.MonthlyClose.ReplicationUnavailable` | la central de valores nacionales no respondió: reintentar más tarde |
   | `Lending.Process.AlreadyRunning` | hay un cierre o una reapertura en curso: esperar o reintentar esa corrida |
   | `Lending.MonthlyClose.MonthNotEnded` | el mes todavía no terminó |

3. «Ordenar el cierre» (202). Se sigue con «Actualizar estado»: la corrida pasa por el paso 1 (líneas, por tandas) y el paso 2 (transacción
   final). Si queda **Fallida** o **con errores**, se reintenta desde la corrida (`POST /api/lending/process-runs/{id}/retry`): conserva R y
   retoma las líneas que faltan; un crédito sin línea no deja cerrar.
4. Sin orden manual: con `Riesgo.CierreProgramado` encendido la tarea `cartera.cierre-programado` lo ordena desde `Riesgo.DiaLimiteCierre` y
   alerta `Cartera.CierreProgramadoFallido` si un requisito falla. Apagado (por defecto), `cartera.vigilancia` alerta
   `Cartera.CierreMensualPendiente` pasado ese día (10 por defecto).

## 3. Revisar y cuadrar

1. `/cartera/cierres/{id}`: marcas, totales, **reporte de cambios** (anterior → nueva con causa) y las líneas con su explicación.
2. Las **partes emitidas** llegan a Contabilidad por el despachador: lote `LendingMonthlyClose` en `/contabilidad/cartera/lotes`, comprobante
   PV. Un hecho rechazado (falta una regla) se corrige en la matriz y se reprocesa desde la pestaña «Emitidos» de la bandeja de Cartera.
3. Pestaña **«Cuadre con Contabilidad»**: la conciliación al último día de M debe dar **cero** por conjunto de cuentas; lo pendiente de
   entregar se explica hecho por hecho en `/cartera/conciliacion-contable?date=`. La contadora la firma.

## 4. Cerrar M en Contabilidad

Sólo **después** del cierre de Cartera: antes, Contabilidad responde `Accounting.Period.LendingNotClosed` (sin reconocimiento posible). Con
hechos de Cartera todavía pendientes en el mes avisa `Accounting.Period.LendingPending`, que se reconoce si la contadora lo acepta.

## 5. Reabrir (antes del cierre contable)

1. Sólo el **último** cierre vigente y sólo mientras Contabilidad no haya cerrado M (`Lending.MonthlyClose.AccountingPeriodClosed`); nunca el
   base (`BaseNotReopenable`); uno que no es el último responde `NotLast`.
2. `/cartera/cierres` → «Reabrir» con motivo (y punto de acta si el reglamento lo pide). Encola la reapertura: emite un reverso por parte en su
   propio lote (otro PV) y el informativo de reapertura; el cierre queda **Reabierto**.
3. Corregir lo que motivó la reapertura (por ejemplo, una calificación por análisis) y **ordenar de nuevo**: nace la versión siguiente. Volver
   a cuadrar (§3) antes de cerrar en Contabilidad.

## 6. Después del cierre

- Un pago con fecha valor del mes cerrado exige `Lending.Payments.BackdateBeforeClose`; no cambia el cierre y su hecho va al mes siguiente.
- Los hechos del día siguiente llevan la categoría contable del cierre nuevo.

## 7. Problemas frecuentes

| Síntoma | Causa y salida |
|---|---|
| La reclasificación salió «sin comprobante (mismas cuentas)» | la matriz da la misma cuenta a las dos categorías: es correcto si así lo decidió la contadora; si no, una regla por categoría |
| El cierre quedó marcado «modelo obligatorio no disponible» | la cooperativa está obligada al Anexo 2 (perfil regulatorio y regla vigentes) y no llega hasta K6a: se aplica el Anexo 1; informar a la Superintendencia según el plan |
| El cierre está «pendiente de validar» | tablas del Anexo 1 sin cotejar (E8): la contadora las coteja; no bloquea |
| La conciliación no da cero | hechos todavía en la bandeja (pendientes, en lote o rechazados) o una combinación sin regla: ver la explicación por conjunto |
