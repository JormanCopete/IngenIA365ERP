# Tesorería: programar pagos, banco de salida y dispersión

Pedido del dueño del 2026-11-15. Este manual dice qué hace hoy Tesorería con lo causado y qué cambió ese día.

## Cómo se paga lo causado

1. **Lo causado llega a Tesorería como orden de pago** (`/tesoreria/ordenes`): facturas y notas de proveedor de Comercial, cuentas por
   pagar contables convertidas en `/tesoreria/cuentas-por-pagar`, giros de Cartera y Captaciones, devoluciones de Recaudo y órdenes
   manuales. **La nómina no se paga por aquí**: tiene su propia dispersión (`/nomina/dispersion`).
2. **Se programa la fecha** (nuevo). Cada orden nace con la fecha de su origen: el vencimiento de la factura o la que se eligió al
   convertir la cuenta por pagar. En la bandeja, **Reprogramar** cambia la fecha de una orden; también se pueden marcar varias y
   reprogramarlas juntas. Se puede mientras la orden está recibida, pendiente o lista y no está en un lote, con motivo y nunca para antes
   de hoy. Cada cambio queda auditado.
3. **Se elige el banco de salida armando un lote** (`/tesoreria/lotes`). Un lote es de **una sola cuenta bancaria propia**: para pagar
   unas órdenes desde AV Villas y otras desde Bancolombia, se arma un lote por cuenta. Un lote admite órdenes de **personas distintas**, y
   cada orden sigue siendo su propia transacción: su línea en el archivo, su resultado, su asiento y su comprobante de pago. Lo aprueba
   otra persona.
4. **Se genera el archivo o se marca enviado por el portal**, y se registra el resultado. Al registrar «girado» se contabiliza: **débito
   a la cuenta por pagar y crédito al banco** del lote (la cuenta contable vinculada a la cuenta propia).

### La cuenta que se debita (cambio del 2026-11-15)

En una cuenta por pagar contable, el débito sale ahora de **la cuenta donde está pendiente la deuda**, la del tercero con ese documento
cruce entre las cuentas marcadas para el rol «Cuentas por pagar contables». Antes lo elegía la matriz: con varias cuentas para el rol
(2335, 2505…) no tenía cómo distinguirlas y podía cancelar la deuda en otra cuenta. Si la deuda está repartida en dos cuentas, o no se
encuentra, decide la matriz como antes.

## Dispersión: el archivo del banco

El archivo del lote sale con el **formato del banco de la cuenta de salida**. Si ese banco no tiene uno activo, sale con
`PAGOS-GENERICO` (CSV). La cuenta del beneficiario sale de sus **cuentas bancarias registradas** (Maestros › Cuentas bancarias de la
persona, con doble control). Se toma la más reciente activa cuando nace la orden.

El 2026-11-15 se agregaron dos formatos, **inactivos y pendientes de validar**. Se siembran sólo si la cooperativa tiene ese banco en su
catálogo:

| Código | Banco | Forma |
|---|---|---|
| `BANCOLOMBIA-PAB` | Bancolombia | Ancho fijo PAB: registro de control «1» y un «6» por pago. Ahorros 37, corriente 27; CC 1, CE 2, NIT 3, TI 4, PA 5. |
| `AVVILLAS-PAGOS` | AV Villas | Cargue masivo delimitado por «;»: tipo y número de documento, nombre, banco ACH, tipo de cuenta (AH/CC), cuenta, valor, referencia. |

Están armados con el formato **público** de cada banco, no con el instructivo que el banco entrega a la cooperativa. Antes de usarlos:

1. En **Maestros › Formatos bancarios**, revise el formato y **actívelo**.
2. Arme en `/tesoreria/lotes` un lote de prueba desde una cuenta de ese banco. Su **vista previa** ya muestra el archivo con ese formato.
3. Cargue ese archivo en el portal empresarial del banco **sin aprobar el pago**.
4. Si el banco lo rechaza o pide otro layout, **desactive** el formato (los lotes vuelven al genérico) y corrija los campos en la misma
   pantalla. Cuando el banco lo acepte, déjelo activo.

## Lo que falta

- **Un solo pago para varias cuentas de la misma persona.** Hoy cada orden es su propio pago y su propia línea en el archivo. Juntarlas
  necesita cambios en la base de datos. Queda para después de desplegar la consolidación de migraciones, que hoy las tiene congeladas.
- **El banco de salida por orden.** Hoy se elige por lote, un lote por cuenta. Por orden también necesita una columna nueva.
