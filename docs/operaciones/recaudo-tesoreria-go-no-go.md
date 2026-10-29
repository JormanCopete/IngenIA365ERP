# La lista de go/no-go de Recaudo y Tesorería

> Feature 015, US24 (R-28; FR-227). Runbook de la decisión de salir en vivo. La lista vive en el sistema
> (`/admin/go-no-go`, `/api/core/go-live-checklists`), no en una hoja aparte.

## 1. Quién hace qué

| Rol | Permiso | Qué hace |
|---|---|---|
| Líder de la implantación | `Platform.GoLive.View` · `.Manage` | crea la lista, verifica las condiciones, marca las manuales con su evidencia y registra la decisión |
| El dueño | `Platform.GoLive.Override` | aprueba **seguir con una condición en rojo**, con su motivo (queda a la vista, a su nombre) |

## 2. Crear la lista

En `/admin/go-no-go` → «Nueva lista»: nombre («Salida en vivo COOFLOPAL 01/12/2026»), ambiente y fecha prevista. Con «condiciones
manuales sugeridas» se agregan: `CRONOGRAMA`, `NOGO-PARCIAL`, `PARALELO`, `PRODUCCION-LIMPIA`, `RESPALDOS`, `CAPACITACION`. Las
automáticas las calcula cada módulo al crear la lista y al pulsar «Recalcular».

## 3. Las condiciones automáticas

| Módulo | Código | Cómo se verifica |
|---|---|---|
| `REC` | `REQ.*` | cada requisito de la activación completa de Recaudo (`/recaudo/activacion`): punto, caja con cajero, efectivo y transferencia, tipos de documento, cuadre de la puesta en marcha, cajero y supervisor con sus permisos, mes contable abierto, **matriz contable al 100 %** de lo que no esté en «no pasa», hora de corte antes del ciclo de Cartera |
| `REC` | `TestReceiptPerPointAndMeans` | un recibo vigente por cada medio ofrecido en cada punto vigente |
| `REC` | `TestReceiptPerConcept` | un recibo por cada concepto vario activo de la cooperativa |
| `REC` | `SecondFactorCashiersAndSupervisors` | cajeros asignados y supervisores con el segundo factor inscrito |
| `REC` | `CashierScopesAssigned` | cada cajero con alcance sobre su caja o su punto |
| `TES` | `REQ.*` | cada requisito de la activación completa de Tesorería (cuenta de pagos vinculada, cuadre, chequera si hay cheques, mes abierto, matriz al 100 %) |
| `TES` | `TestDisbursementPerAccount` | un giro pagado desde cada cuenta propia de pagos |
| `TES` | `TestDisbursementPerForm` | un pago por transferencia y, si hay chequera activa, uno por cheque |
| `TES` | `SecondFactorApprovers` | quienes aprueban lotes y registran resultados con el segundo factor |
| `CAR` | `CAR.*` / `CAR.Activated` | los requisitos de la activación de Cartera (entre ellos el recaudador y la tesorería **reales** registrados), o «activada» |
| `CAR` | `APO.*` / `APO.Activated` | los de la activación de Aportes |

En producción, Cartera y Aportes se activan **después** de Recaudo y Tesorería (el mismo 01/12): antes de activarlos, sus condiciones
`CAR.*`/`APO.*` sólo pueden estar en verde cuando Recaudo y Tesorería ya están activos.

## 4. Las condiciones manuales

Se marcan con el estado (verde, rojo, no aplica), **lo que se verificó** y, si hay, un soporte subido a la lista (botón «Subir evidencia
o escenario»; dueño de adjuntos `GoLiveEvidence`, no se borra). El escenario de **no-go parcial** con su plan de reversa (P15) es
obligatorio para decidir una salida parcial.

## 5. Decidir

- **Go**: sólo sin rojos sin aprobación (`Platform.GoLive.RedWithoutOverride` si hay alguno). Un rojo aprobado por el dueño queda a la vista.
- **Salida parcial**: con el escenario adjunto (`Platform.GoLive.ScenarioRequired`).
- **No-go**: siempre, con motivo.

La decisión se toma **una vez**: si hay que volver a verificar, se abre otra lista. Con rojos cerca de la fecha la plataforma alerta a
quien puede aprobarlos (`Plataforma.GoLiveEnRojo`).

## 6. Bloqueantes conocidos de R0/T0 (pendientes del dueño)

- P12: matrices contables completas (la completitud al 100 % es una condición automática).
- P7: umbrales SARLAFT y listas vinculantes validados.
- P15: escenarios de no-go parcial escritos y aprobados.
- La puerta T0301 (e2e de Cartera y Captaciones sobre los módulos reales, en los dos motores) y el retiro de los dobles (T0306, T0307).
