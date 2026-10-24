# Recaudo: manual del cajero (R0)

> Feature 015 (Recaudo y Tesorería), entrega R0. Receta para **el cajero** (plantilla de rol `recaudo.cajero`). El contrato
> está en [contracts/api.md](../../specs/015-recaudo-tesoreria/contracts/api.md) y en
> [spec.md](../../specs/015-recaudo-tesoreria/spec.md) (US2, US3, US14); esto es cómo se trabaja en la ventanilla.

La caja de Recaudo recibe el dinero de las personas y lo **entrega** a los módulos dueños de cada obligación: Cartera (créditos) y
Captaciones (aportes y cuentas). Recaudo no decide cuánto se debe ni cómo se imputa un pago a una cuota: lo pregunta a cada módulo
por su contrato y le aplica lo que la persona eligió. Un recibo puede pagar varias obligaciones de varios titulares a la vez.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Abrir la sesión, ver lo esperado y arquear al cierre | `/recaudo/sesiones` | `Receipts.Session.Open` · `.Close` |
| La caja: buscar, elegir, cobrar y confirmar | `/recaudo/caja` | `Receipts.Receipt.Register` |
| Los recibos emitidos y su reimpresión | `/recaudo/recibos` | `Receipts.Receipt.View` · `.Print` |
| Pedir la anulación de un recibo | `/recaudo/anulaciones` | `Receipts.Voiding.Request` |
| Digitar los recibos del talonario de contingencia | `/recaudo/contingencia` | `Receipts.Receipt.RegisterContingency` |

El cajero sólo ve las cajas que tiene asignadas (alcance por caja o por punto) y necesita el **segundo factor inscrito**: es una
condición de la lista de go/no-go (`/admin/go-no-go`, condición `REC.SecondFactorCashiersAndSupervisors`).

## 2. Abrir la sesión

1. En `/recaudo/sesiones`, elija su caja y abra la sesión con la base que le entregó el supervisor. Con `Caja.UnaSesionPorCajero`
   (por defecto sí) no puede tener dos sesiones abiertas.
2. La sesión pertenece al **día del punto**: si el supervisor ya cerró el día, no se abre otra hasta el día siguiente.
3. Con `Caja.ArqueoCiego` (por defecto sí) usted cuenta sin ver lo esperado; lo esperado lo ve quien tenga `Receipts.Session.ViewExpected`.

## 3. La caja en tres pasos

1. **Buscar a la persona**: por documento, nombre o el número de la obligación. Si quien paga no es el titular, registre al
   **pagador** (quien entrega el dinero) y al titular: el sistema marca el recibo como pagado por un tercero.
2. **Elegir qué paga**: la caja muestra lo que cada módulo dice que la persona puede pagar (cuotas exigibles, aportes, cuentas). La
   persona elige el valor de cada línea y, si paga de más en un crédito, **qué hacer con el excedente** (abono a capital con menor
   cuota o menor plazo, según lo que ofrezca Cartera): esa elección viaja al módulo tal como la dijo el deudor.
3. **Cobrar**: registre los medios (efectivo, transferencia y los demás ofrecidos en su punto) hasta cubrir lo aplicado; las vueltas
   sólo salen del efectivo. Los medios se imputan a las aplicaciones en el orden de `Recibo.OrdenDeImputacion` (inmediatos primero y
   el cheque al final). Confirme: el recibo toma su número **después** de validar todo y se entrega impreso.

Atajos: cada medio puede tener su tecla rápida (la define el supervisor en la configuración del medio).

## 4. Efectivo grande: declaración y quien efectúa

Desde `Sarlaft.UmbralDeclaracionOrigenFondos` (5.000.000 por operación, **pendiente de validar** por el oficial, P7) la caja exige la
**declaración de origen de fondos** con su soporte, una vez por recibo, y el documento y nombre de **quien efectúa** la entrega. Sin
ellas no se confirma (`Receipts.Compliance.DeclarationRequired`, `.DeliveredByRequired`).

Si la persona aparece en una **lista vinculante**, la operación queda bloqueada y no se recibe su dinero: no insista, el oficial de
cumplimiento ya recibió la alerta. En una lista **no vinculante**, según `Sarlaft.ListasNoVinculantes.Efecto`:
«Sigue» confirma con alerta; «Retiene» pide **retener** el efectivo en custodia de la sesión (botón «Retener»); el oficial lo
libera o lo devuelve por orden de pago.

## 5. Si el sistema dice «no contabilizable»

Con la matriz contable incompleta y la política `Contabilidad.FaltaDeRegla = Bloquear`, la caja no confirma
(`Receipts.Accounting.NotPostable`) y dice qué hecho y qué rol faltan. Avise al supervisor: lo completa Contabilidad en
`/contabilidad/recaudo/matriz`.

## 6. Contingencia (sin sistema)

Si la ventanilla se queda sin sistema, se recibe en el **talonario de contingencia** registrado para el punto. Al volver, digite cada
recibo en `/recaudo/contingencia` con la fecha valor del talonario, el número del talonario y **la imagen del recibo en papel**
(obligatoria). El informe de la pantalla muestra los números por digitar y los sin usar; la tarea diaria alerta los que faltan
(`Recaudo.ContingenciaPorDigitar`).

## 7. Cerrar la sesión

Cuente el efectivo por denominación y los demás medios en `/recaudo/sesiones` → «Arquear y cerrar». Una diferencia la aprueba el
supervisor (otra persona); un faltante aprobado se trata según `Caja.TratamientoFaltante` (por defecto, cuenta por cobrar al cajero).

## 8. Anular un recibo

Pida la anulación en `/recaudo/anulaciones` con su motivo; la aprueba otra persona con `Receipts.Voiding.Approve`. La anulación es
un documento nuevo: el recibo original no se borra ni se edita, y cada módulo reversa lo que había aplicado.
