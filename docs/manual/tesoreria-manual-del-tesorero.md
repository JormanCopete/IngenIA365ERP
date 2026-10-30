# Tesorería: manual del tesorero (T0 a T2)

> Feature 015, entrega T0. Receta para **el tesorero** (plantilla `tesoreria.tesorero`), con lo que hacen **el aprobador**
> (`tesoreria.aprobador`) y **el conciliador** (`tesoreria.conciliador`). Historias US10, US11, US19 parte T0, US25 y US26 parte T0 de la
> [spec](../../specs/015-recaudo-tesoreria/spec.md).

Tesorería gira el dinero que los módulos le piden pagar —desembolsos de Cartera, retiros y devoluciones de Captaciones, devoluciones
de Recaudo— desde las **cuentas bancarias propias** de la cooperativa, y le informa a cada origen el resultado. No decide a quién
ni cuánto se paga: lo dice la orden que llega. Las pantallas de la Tesorería anterior (`/tesoreria/cheques`, `/tesoreria/conceptos`,
`/tesoreria/facturas`, `/tesoreria/flujo-caja`) se retiraron después de T2 (§8); sus reemplazos están en este manual.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Cuentas bancarias propias, vínculo contable, propósitos | `/tesoreria/cuentas-bancarias` | `Treasury.BankAccounts.View` · `.Manage` |
| Fondo de liquidez: pedir y aprobar su uso | `/tesoreria/fondo-de-liquidez` | `Treasury.LiquidityFund.Request` · `.Approve` |
| Bandeja de órdenes de pago | `/tesoreria/ordenes` | `Treasury.Orders.View` · `.Manage` · `.ResolveDestination` |
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
   Una transferencia que llega **sin cuenta destino** (por ejemplo, el retiro de un asociado sin cuenta de depósito) la resuelve Tesorería
   con «Resolver destino» (permiso `Treasury.Orders.ResolveDestination`): **completar la cuenta** con una cuenta registrada y activa del
   beneficiario, o **girar por cheque** (la orden pasa a un lote de cheques). Las dos piden motivo y quedan en la auditoría.
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

## 6. Lo que trae T1

- **Traslados entre cuentas propias** (`/tesoreria/traslados`): se piden con su motivo y otra persona los aprueba según el monto (aprobar no
  mueve dinero). Quien lo ejecuta en el portal del banco lo marca **enviado** con la referencia: sale de la cuenta de origen y queda **en
  tránsito**. Llega a la de destino sola al cruzar el abono en la conciliación de esa cuenta, o se confirma con la constancia del banco.
  Desde una cuenta del fondo de liquidez exige un uso del fondo aprobado. Lo pedido o aprobado sin enviar se anula con motivo.
- **Cierre del mes de los cheques** (`/tesoreria/cierre-de-cheques`): una vez por cuenta y mes, ya terminado el mes. Los cheques emitidos y
  no entregados al corte vuelven al efectivo de la cuenta (y regresan el primer día del mes siguiente); los entregados sin cobrar más allá
  de `Cheques.DiasACuentaPorPagar` días (180, pendiente de validar) pasan a **cuenta por pagar al beneficiario**, nunca a ingreso.
- **Órdenes manuales** (`/tesoreria/ordenes-manuales`): sólo por un concepto del **catálogo cerrado** (lo administra el aprobador), con
  soporte, dentro del tope del concepto y con la retención en la fuente que el concepto exija; sin ella, cause primero la cuenta por pagar
  en Contabilidad. Las aprueban **dos personas** distintas de quien prepara; nadie prepara ni aprueba una orden a su propio nombre. Con las
  dos aprobaciones entra a la bandeja y sigue el camino de todas.
- **Firmantes y tokens** (`/tesoreria/firmantes`): por cuenta, cada firmante con su clase de firma, lo que firma solo y si firma en
  conjunto, y cada token del portal con su serial y quién lo tiene. El alta y el retiro llevan el **acta** y los aprueba otra persona. Si un
  firmante o el dueño de un token se retira de la cooperativa, llega la alerta `Tesoreria.FirmanteRetirado`: pida su retiro y avísele al banco.
- **Comprobantes de pago** (`/tesoreria/comprobantes-de-pago`): cada giro registrado como «girado» deja su comprobante numerado con código de
  verificación; se imprime y se envía al beneficiario por correo sólo si autorizó ese canal.
- **Cierre contable**: si Contabilidad cierra un mes con conciliaciones de ese mes abiertas o sesiones de caja sin cerrar, avisa y lo deja
  reconocer; si la cooperativa pone `Contabilidad.CierreExigeConciliaciones`, no cierra hasta cerrarlas.
- **Informes de T1**: `payment-vouchers`, `transfers` y `month-end-checks` en `/tesoreria/informes`.

## 7. Lo que trae T2

| Qué | Dónde | Permiso |
|---|---|---|
| Notas bancarias (GMF, comisiones, intereses, impuestos) | `/tesoreria/notas-bancarias` | `Treasury.BankNotes.Register` · `.Approve` |
| Reglas de emparejamiento de la conciliación | `/tesoreria/reglas-de-conciliacion` | `Treasury.Reconciliation.View` · `.Work` |
| Cuentas por pagar y calendario de pagos | `/tesoreria/cuentas-por-pagar` | `Treasury.Payables.View` · `.Manage` · `.Pay` |
| Posición de liquidez y sus fotos | `/tesoreria/liquidez` | `Treasury.Liquidity.View` · `.Manage` |
| Flujo de caja proyectado | `/tesoreria/flujo-de-caja` | `Treasury.Liquidity.View` · `.Manage` |
| Tablero de Tesorería | `/tesoreria/tablero` | `Treasury.Liquidity.View` |
| Conciliación de adquirentes (redes de tarjetas) | `/tesoreria/adquirentes` | `Treasury.AcquirerSettlements.Load` · `.Approve` |

- **Conciliación completa** (desde T2 toda conciliación nueva lo es): el cruce automático usa primero el vínculo con la línea del extracto y
  después las **reglas** de la cuenta (y las de todas), por prioridad y con sus días de holgura; un empate no se cruza. Las **notas
  bancarias** se registran casi siempre desde su línea del extracto (la pantalla las sugiere por la descripción) y, si
  `NotasBancarias.ExigenAprobacion`, las aprueba otra persona; al aprobarse quedan contabilizadas y se cruzan con su línea. Las
  **dispersiones de Nómina** llegan solas y también se cruzan.
- **Pagos a proveedores y cuentas por pagar**: desde T2 se pagan por Tesorería. Las facturas de proveedor **a crédito** que Comercial
  confirma llegan a la bandeja con su vencimiento como fecha de pago y su neto; si Comercial la anula antes de enviarla al banco, sale del
  lote. Las **cuentas por pagar contables** (las cuentas que la matriz de Tesorería asigna al rol de cuenta por pagar) se ven por tercero y
  documento cruce y se convierten en orden por todo o por una parte. Tesorería no registra facturas ni causa gastos: paga lo causado. El
  **calendario** agrupa lo abierto por fecha de pago.
- **Firmas del lote**: al aprobar un lote, las aprobaciones tienen que venir de los firmantes vigentes de la cuenta en una combinación
  válida para el monto —uno que firme solo por ese valor o dos que firmen en conjunto—. Una cuenta sin firmantes registrados no exige nada.
- **Cheque cobrado tarde**: si un cheque que ya había pasado a cuenta por pagar se cobra después, al marcarlo cobrado se contabiliza contra
  esa cuenta por pagar (pendiente de confirmar por el dueño).
- **Posición de liquidez**: bancos por su saldo en libros, bóvedas y cajas (lo que informa Recaudo), menos los compromisos (órdenes en lotes
  aprobados o enviados, órdenes en la bandeja, dispersiones de Nómina por pagar); los cheques en tránsito se muestran pero no restan (el
  saldo en libros ya los descontó). Lo esperado de Cartera y Captaciones (sus cuotas por vencer) suma sólo a lo **proyectado**. Si una
  consulta no responde, la posición sale sin ese componente y con el aviso. **Tomar foto** guarda la posición del día y le informa a
  Captaciones el saldo de las cuentas del fondo de liquidez.
- **Flujo de caja**: por semanas o meses, con las categorías de la cooperativa (vienen seis sugeridas). Una categoría automática toma lo
  programado de su fuente; lo que se digite para un período manda sobre lo calculado. Lo real de cada período que ya empezó son los
  movimientos contabilizados de las cuentas bancarias propias. Reemplaza al flujo de caja anterior, ya retirado (§8).
- **Adquirentes**: se carga el archivo de liquidación de la red (su formato se crea en Maestros → Formatos bancarios con el ámbito
  «liquidación de una red de tarjetas»). Cada lote se cruza con lo cobrado por datáfono en Recaudo y en el punto de venta; dentro de
  `Adquirentes.ToleranciaDeConciliacion` queda conciliado y contabilizado (banco, comisión, IVA y retenciones con la red como tercero
  contra el medio por cobrar); fuera, la diferencia la decide otra persona con motivo.
- **Informes de T2**: `reconciliations`, `liquidity` y `cash-flow` en `/tesoreria/informes` (los dos últimos, para quien ve todas las cuentas).

## 8. Después de T2: el retiro de la Tesorería anterior

Los cheques, las facturas, el flujo de caja y los conceptos de la Tesorería anterior se retiran con una migración destructiva con guarda
(`RetiroDeLaTesoreriaHeredada`), que la cooperativa sólo aplica con la aprobación del dueño. Antes, el tesorero:

1. **Exporta para archivo** las tres tablas (`POST /api/treasury/legacy-export`): un Excel y un csv por tabla, guardados como adjuntos que
   no se borran, con su huella.
2. **Carga lo pendiente** con la plantilla `tesoreria.pendientes-heredados` de la puesta en marcha: cada cheque girado sin cobrar nace como
   cheque en tránsito de su chequera (y la conciliación lo cruza cuando el banco lo pague); cada factura por pagar nace como orden de la bandeja
   contra su cuenta por pagar contable (la que dejó la apertura de Contabilidad con el proveedor y el documento cruce).

Lo que reemplaza a cada pantalla retirada:

| Antes | Ahora |
|---|---|
| Cheques | Chequeras y Cheques girados |
| Facturas CxP/CxC | Cuentas por pagar (las por cobrar son de Cartera y Comercial) |
| Flujo de caja | Flujo de caja proyectado (`/tesoreria/flujo-de-caja`) |
| Conceptos | los conceptos varios de Recaudo |

El procedimiento completo, con el diagnóstico, el respaldo y la aprobación por base, está en
[tesoreria-retiro-heredado.md](../operaciones/tesoreria-retiro-heredado.md).
