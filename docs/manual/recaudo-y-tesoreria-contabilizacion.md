# Recaudo y Tesorería: la contabilización (para la contadora)

> Feature 015, entregas R0 y T0, US8 (enmienda E-04 a la 009). Contrato:
> [contracts/contabilidad.md](../../specs/015-recaudo-tesoreria/contracts/contabilidad.md). Receta para **la contadora**; lo
> pendiente de validar está marcado (P8, P12).

## 1. Las reglas que no cambian

- **Recaudo y Tesorería no conocen cuentas.** Emiten **hechos** a Contabilidad en la misma transacción que su operación; ningún hecho
  lleva una cuenta: roles y dimensiones por código. Contabilidad resuelve la cuenta con su **matriz** y contabiliza siempre por
  `AccountingPoster` (un solo camino al libro).
- **No se siembra ninguna regla con cuenta.** La plantilla trae las filas sugeridas **sin cuenta**, según la configuración real de la
  cooperativa (medios por punto, conceptos, cuentas propias, clases de orden). Completar la matriz es del dueño con la contadora (P12).
- **Lo de Recaudo y Tesorería no se reversa desde Comprobantes** (`Accounting.Document.ModuleOwned`, «se corrige desde Recaudo /
  Tesorería»): se corrige con un documento nuevo del módulo (anulación, devolución no girada, reverso).

## 2. Pantallas

| Qué | Dónde | Permiso |
|---|---|---|
| Matriz de Recaudo: reglas, plantilla (también en Excel), cierre de vigencia | `/contabilidad/recaudo/matriz` | `Accounting.ReceiptsMatrix.View` · `.Manage` |
| Tipos de comprobante por hecho de Recaudo | `/contabilidad/recaudo/tipos-de-comprobante` | ídem |
| Completitud de la matriz de Recaudo | `/contabilidad/recaudo/completitud` | ídem |
| Lotes resumidos de los recibos de caja | `/contabilidad/recaudo/lotes` | ídem |
| Matriz, tipos y completitud de Tesorería | `/contabilidad/tesoreria/{matriz, tipos-de-comprobante, completitud}` | `Accounting.TreasuryMatrix.View` · `.Manage` |
| Política «bloquear / advertir» | `PUT /api/receipts/accounting-policy`, `PUT /api/treasury/accounting-policy` | `Receipts.Accounting.PolicyChange`, `Treasury.Accounting.PolicyChange` |

## 3. La resolución por pesos

Hecho × rol × dimensiones por código; cada dimensión es igualdad o `*`, y con varias reglas gana la más específica: caja 256, punto
128, medio 64, concepto 64, clase de orden 64, módulo, naturaleza, clase de recaudador y origen de la orden 32, cuenta propia 16,
causa 8, oficina 4, hecho 2 (sólo en roles de resultado), clase de medio 1. El centro de costo se asigna y no pesa. Un empate es un
error de configuración que la completitud muestra. Una regla rige desde su fecha y **nunca es retroactiva** sobre lo contabilizado.
Los roles `Banco` y `BancoRecaudo` no tienen regla: salen del **vínculo** de la cuenta propia con su auxiliar (`ACC_OwnBankAccountLinks`).

## 4. Hechos y tipos de comprobante

| Tipo | Hechos |
|---|---|
| `RR` recibo de caja | `ReciboEmitido` (por **lotes resumidos**: un comprobante por día y punto; tercero o cruce sólo donde la cuenta lo exige) |
| `RB` recibo de banco | recibo de banco y de pagaduría, giro de pagaduría recibido, partida reconocida, clasificada y devuelta al consignante |
| `MC` movimiento de caja | movimientos de caja y bóveda, medio reclasificado, diferencia de arqueo, ajuste de sesión, sobrante a ingreso, dinero retenido en custodia y su liberación, remesa despachada |
| `AR` anulaciones y reversos | recibo anulado, aplicación reversada por el módulo, cheque recibido devuelto, saldo a favor resuelto, saldo a favor de Cartera devuelto, devolución no girada |
| `TP` pago de Tesorería | pago de orden girado, cheque girado emitido y anulado, GMF de un pago, remesa confirmada |

Los tipos `RR`, `RB`, `MC`, `AR`, `TP` se siembran con `voucher-types.json` y sus mapeos sin cuenta (pendientes de validar).

## 5. Los puentes

`RecaudoPorAplicar` (el puente del módulo: Recaudo entregó y Cartera o Captaciones aplicaron), `DesembolsoPorGirar` (Cartera ordenó y
Tesorería giró) y `PuenteDeDevolucion` (Recaudo pidió devolver y Tesorería giró, o informó que no). La conciliación de puentes de Cartera
(`/cartera/conciliacion-contable`) suma el lado de Recaudo y Tesorería y explica cada diferencia hecho por hecho.

## 6. La validación previa

Con `Contabilidad.FaltaDeRegla = Bloquear` (el defecto), la **pantalla** no confirma una operación cuyo hecho no tiene regla
(`Receipts.Accounting.NotPostable` en la caja, `Treasury.Prevalidation.NotPostable` en Tesorería) y dice hecho, rol y quién la completa;
en los **procesos** la línea queda para reproceso. Sin contabilidad iniciada o con `Contabilidad.ModoDePaso = NoPasa`, nada se bloquea.
Pasar a «advertir» exige motivo, queda auditado y alerta cada día (`Recaudo.PoliticaContableEnAdvertir`, `Tesoreria.PoliticaContableEnAdvertir`).

## 7. Cierre del mes y cuadre de apertura

- El cierre del mes **de la activación** se niega mientras el cuadre de apertura de Recaudo o de Tesorería siga **provisional**
  (`Accounting.Period.OpeningSquarePending`); el definitivo lo libera.
- La conciliación bancaria es de **Tesorería** (`/tesoreria/conciliacion`, E-04): las tablas `ACC_BankReconciliations` y vecinas quedan
  sin uso. Reabrir un mes deja desactualizadas las conciliaciones cerradas del mes.

## 8. Pendiente de validar

- P12: las matrices completas (roles y cuentas de cada hecho) — **bloquea la salida**: la completitud al 100 % es condición del go/no-go.
- P8: las cuentas de faltantes y sobrantes, `Caja.Sobrantes.DiasAntesDeIngreso`, `Partidas.EscalonesDias`, `Partidas.DestinoVencido`.
- P10 (tributarista): hechos generadores del GMF (`Gmf.*`; rige cuatro por mil, devoluciones y partidas, «gasto y por pagar»).
