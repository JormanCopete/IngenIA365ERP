# Tesorería: manual del tesorero (T0)

> Feature 015, entrega T0. Receta para **el tesorero** (plantilla `tesoreria.tesorero`), con lo que hacen **el aprobador**
> (`tesoreria.aprobador`) y **el conciliador** (`tesoreria.conciliador`). Historias US10, US11, US19 parte T0, US25 y US26 parte T0 de la
> [spec](../../specs/015-recaudo-tesoreria/spec.md).

Tesorería gira el dinero que los módulos le piden pagar —desembolsos de Cartera, retiros y devoluciones de Captaciones, devoluciones
de Recaudo— desde las **cuentas bancarias propias** de la cooperativa, y le informa a cada origen el resultado. No decide a quién
ni cuánto se paga: lo dice la orden que llega. La Tesorería heredada (`/tesoreria/cheques`, `/tesoreria/conceptos`,
`/tesoreria/facturas`, `/tesoreria/flujo-caja`) convive hasta su retiro y no se usa para R0/T0.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Cuentas bancarias propias, vínculo contable, propósitos | `/tesoreria/cuentas-bancarias` | `Treasury.BankAccounts.View` · `.Manage` |
| Fondo de liquidez: pedir y aprobar su uso | `/tesoreria/fondo-de-liquidez` | `Treasury.LiquidityFund.Request` · `.Approve` |
| Bandeja de órdenes de pago | `/tesoreria/ordenes` | `Treasury.Orders.View` · `.Manage` |
| Lotes: armar y aprobar (con monto) | `/tesoreria/lotes` | `Treasury.Batches.Prepare` · `.Approve` |
| Archivo de pago: generar, descargar, marcar enviado, anular | `/tesoreria/archivos-de-pago` | `Treasury.PaymentFiles.*` |
| Resultados del giro y respuesta del banco | `/tesoreria/resultados` | `Treasury.Results.Register` |
| Chequeras y cheques girados | `/tesoreria/chequeras`, `/tesoreria/cheques-girados` | `Treasury.Checkbooks.*` · `Treasury.Checks.*` |
| Remesas de efectivo | `/tesoreria/remesas` | `Treasury.Remittances.View` · `.Confirm` |
| Extractos bancarios | `/tesoreria/extractos` | `Treasury.Statements.Load` · `.View` |
| Conciliación bancaria mínima | `/tesoreria/conciliacion` | `Treasury.Reconciliation.View` · `.Work` · `.Close` · `.Reopen` |
| Informes de Tesorería | `/tesoreria/informes` | `Treasury.Reports.View` · `.Export` |

## 2. Cuentas propias y fondo de liquidez

Cada cuenta propia se vincula a su auxiliar bancario del plan de cuentas (el vínculo lo escribe Contabilidad, E-04) y declara sus
propósitos: recaudo, pagos, **fondo de liquidez**. El fondo de liquidez es exclusivo: nada sale de él sin un uso aprobado y su aviso
previo a la Supersolidaria (alerta `Tesoreria.UsoDelFondoDeLiquidez`). Qué cuentas son fondo lo define el dueño con el oficial (P17,
**pendiente**): mientras tanto, ninguna.

## 3. De la orden al pago

1. Las órdenes llegan solas a la bandeja (`OrdenDeGiroEmitida` de Cartera y Captaciones, órdenes de Recaudo). Se validan contra las
   listas y la cuenta destino de la persona (aprobada por otra persona; una cuenta recién cambiada lleva su marca).
2. **Armar el lote** por cuenta de origen y forma: archivo del banco, portal del banco o cheques.
3. **Aprobar** el lote: otra persona, con su monto de atribución y el segundo factor presente.
4. **Generar** el archivo (queda con su SHA-256), descargarlo, cargarlo en el banco y **marcarlo enviado**.
5. **Registrar el resultado**: cargando la respuesta del banco o a mano con su soporte: girada, rechazada, anulada o cheque no cobrado.
   Cada resultado va al origen a nombre de la persona que lo registró; una orden rechazada se puede **regirar**.

Antes de registrar un «girada», emitir o anular un cheque o confirmar una remesa, la pantalla pregunta la **validación previa
contable**: con la matriz de Tesorería incompleta y la política en «Bloquear», responde `Treasury.Prevalidation.NotPostable` y dice qué
falta (lo completa Contabilidad en `/contabilidad/tesoreria/matriz`).

## 4. Cheques y remesas

- La chequera se registra con su rango; los cheques de un lote de cheques se emiten en orden, se imprimen, se entregan con constancia y
  se anulan con causa. Un cheque entregado y no cobrado a `Cheques.DiasSinCobrar` días (30, pendiente) alerta.
- La remesa de la bóveda llega en tránsito y se **confirma** contra el extracto o con soporte; sin confirmar a `Remesas.DiasParaConfirmar`
  días, alerta.

## 5. Conciliación bancaria mínima (el conciliador)

Por cuenta y mes: se abre, el **cruce automático** empareja extracto y registros (valor, fecha con `Conciliacion.DiasDeHolgura` días,
referencia), se cruza a mano lo demás, y lo que no cruza queda como **partida conciliatoria** por clase y antigüedad. Un abono sin
registro pasa a Recaudo como partida por identificar. Se cierra con diferencia cero (deja su informe) y se reabre con motivo como
versión nueva. **El conciliador no puede haber preparado ni aprobado pagos de esa cuenta en el período.** Si Contabilidad reabre el mes,
las conciliaciones cerradas del mes quedan desactualizadas y se vuelven a conciliar.
