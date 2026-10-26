# Comercial: compras

> Manual funcional del grupo **Compras** del menú (feature 012, entregas I1 y I5). Para el comprador, el bodeguero que
> recibe y quien aprueba. El índice del módulo está en [comercial.md](comercial.md); el documento de inventario por
> dentro (borrador, confirmación, anulación, kardex), en [inventario-documentos-y-kardex.md](inventario-documentos-y-kardex.md).

## Qué hace

Compras registra todo lo que entra a la cooperativa por parte de un proveedor y lo que se le devuelve:

- **Solicitud de compra**: una dependencia deja constancia de lo que necesita y para cuándo. No mueve inventario.
- **Orden de compra**: el pedido al proveedor con precios, entrega esperada y condiciones de pago. Tampoco mueve
  inventario; se recibe en una o varias entregas.
- **Recepción**: la mercancía que entra a una bodega. Es lo que sube la existencia y el costo.
- **Compra directa**: la recepción y la factura del proveedor en una sola operación, para la compra de mostrador.
- **Factura del proveedor**: lo que el proveedor cobra, contra lo recibido y los servicios sin recepción, con sus
  impuestos y retenciones.
- **Notas del proveedor**: notas crédito y débito sobre una factura suya.
- **Devolución a proveedor**: mercancía que sale de vuelta, al costo con que entró.
- **Cruce a tres vías**: la comparación automática de lo ordenado, lo recibido y lo facturado.
- **Costos adicionales**: fletes y seguros repartidos entre lo recibido.
- **Documento soporte**: la compra a un proveedor que no está obligado a facturar.
- **Eventos RADIAN**: el acuse de recibo y el recibo del bien de las facturas electrónicas recibidas.

Lo confirmado no se edita ni se borra: se **anula**, y la anulación es un documento contrario con su propia fecha.

## Quién lo hace

| Tarea | Permiso |
|---|---|
| Consultar todo lo de compras | ver compras (`Inventory.Purchases.View`) |
| Registrar solicitudes, órdenes, recepciones, facturas, notas, devoluciones, costos adicionales, documentos soporte | registrar compras (`Inventory.Purchases.Create`) |
| Compra directa; confirmar, enviar y cerrar el saldo de órdenes; confirmar costos adicionales y documentos soporte | confirmar compras (`Inventory.Purchases.Confirm`) |
| Anular | anular compras (`Inventory.Purchases.Void`) |
| Emitir el acuse y el recibo del bien desde el ERP | emitir eventos RADIAN (`Inventory.Purchases.EmitRadianEvent`) |
| Registrar un evento RADIAN emitido fuera del ERP | registrar eventos RADIAN (`Inventory.Purchases.RegisterRadianEvent`) |
| Ver costos y diferencias de precio | leer costos (`Inventory.Costs.Read`) |
| Aprobar lo que queda en aprobación | el permiso del nivel, en Inventario → Aprobaciones |

Cada usuario sólo trabaja con las bodegas de su alcance. Quien crea un documento no lo aprueba.

## Paso a paso en pantalla

### Antes de la primera compra

- El proveedor existe como persona en el maestro. En la compra directa y al leer el XML de una factura, si el NIT no
  existe, se puede crear el proveedor ahí mismo con su autorización de datos.
- La bodega que recibe está **activa** (ver [comercial-inventario-y-bodegas.md](comercial-inventario-y-bodegas.md)).
- Los productos tienen su grupo contable, su IVA y su concepto de retención.
- Si se usa el cruce a tres vías, las tolerancias están registradas en Inventario → Parámetros.

### Solicitud de compra

1. **Compras → Solicitudes**, «Nueva solicitud».
2. Tipo, bodega que la necesita, para cuándo, quién la pide y los productos leídos con el lector (el empaque leído es la
   unidad). «Guardar».
3. «Enviar a aprobación»: la política del tipo decide si queda en aprobación; sin política, se confirma.
4. Confirmada, cada línea muestra lo pendiente por ordenar. «Crear orden» abre la orden con ese pendiente.

### Orden de compra

1. **Compras → Órdenes de compra**, «Nueva orden» (o «Crear orden» desde una solicitud, que llega prellenada).
2. Proveedor, bodega que recibe, entrega esperada, condiciones y líneas con precio. «Guardar».
3. «Confirmar». Si el monto pasa el límite de quien confirma o la política lo pide, queda en aprobación y la pantalla
   dice por qué.
4. «Ver PDF» muestra la orden; «Enviar al proveedor» la manda al correo del proveedor en el maestro o al que se indique.
5. «Recibir contra esta orden» abre la recepción con lo pendiente por recibir de cada línea.
6. «Cerrar saldo», con motivo, cierra lo que falta por recibir: la orden ya no admite recepciones.

### Recepción

1. **Compras → Recepciones**, «Nueva recepción» (o «Recibir contra esta orden» desde la orden; en la recepción, «Recibir
   lo pendiente de la orden» trae las líneas).
2. Tipo, bodega, fecha, proveedor, remisión o guía, municipio de la operación (se propone el de la sucursal) y las líneas
   leídas con el lector. Si el producto controla lote, vencimiento o serie, la línea los pide.
3. «Guardar»: cada guardado trae la vista previa de impuestos.
4. «Confirmar». Si la política lo pide, queda en aprobación, congelada y sin número, hasta la última aprobación.
5. La recepción confirmada muestra por línea lo que falta facturar y lo devuelto, y sus facturas y devoluciones.
6. «Anular», con motivo: no se puede si tiene factura, devolución o costos adicionales vigentes; anúlelos primero.

Lo recibido de más contra una orden sólo entra dentro de la tolerancia de la cooperativa.

### Compra directa

Para la compra de mostrador, en tres pasos:

1. **Compras → Compra directa**. Proveedor, bodega y los datos de la factura del proveedor: prefijo, número, CUFE,
   emisión, vencimiento y forma de pago. Si el proveedor no está obligado a facturar, en lugar de la factura se elige un
   tipo de documento soporte.
2. «Leer los productos» con el lector; el empaque leído es la unidad.
3. «Confirmar la compra». Impuestos y retenciones los calcula el sistema con el catálogo vigente a la fecha. El resultado
   muestra los dos números (recepción y factura) o, si la política o el monto lo piden, la recepción en aprobación con la
   factura en borrador enlazada. «Otra compra» empieza de nuevo.

Un doble clic o un reintento no compran dos veces.

### Factura del proveedor

1. **Compras → Facturas de proveedor**, «Registrar factura».
2. Proveedor, número y CUFE. Se puede prellenar desde el XML de la factura del proveedor: el archivo se lee y se
   descarta, no se guarda.
3. «Facturar lo pendiente de la recepción» trae las líneas no facturadas; se agregan los servicios sin recepción.
   «Guardar» muestra impuestos y retenciones con su explicación y, con el permiso de costos, la diferencia de costo con
   la recepción.
4. «Confirmar»: queda la foto de impuestos y retenciones de ese momento.
5. Si las recepciones vienen de una orden, la factura pasa por el **cruce a tres vías** (pestaña «Cruce»).
6. «Anular el registro», con motivo; con notas vigentes no se anula. Los eventos RADIAN no cambian al anular.

### Eventos RADIAN de la factura recibida

En el detalle de la factura confirmada, la sección «Eventos RADIAN» muestra el acuse de recibo (030) y el recibo del bien
(032) con su estado:

- «Emitir acuse y recibo» los emite el ERP por el canal de facturación electrónica, el 030 antes que el 032. El recibo
  del bien exige la recepción confirmada.
- Si la cooperativa los emitió por fuera del ERP, «Registrar» deja constancia de dónde y cuándo.

Mientras el canal de facturación electrónica sea el **simulado**, los eventos emitidos desde el ERP **no llegan a la
DIAN** (ver [comercial-facturacion-electronica.md](comercial-facturacion-electronica.md)). La alerta de eventos pendientes
avisa los días que diga el parámetro «Compras.DiasAlertaEventosRadian».

### Notas del proveedor

1. **Compras → Notas de proveedor**.
2. La factura (confirmada), la clase (crédito o débito), las líneas por valor o por cantidad y si cambia el costo de lo
   recibido.
3. «Guardar y calcular impuestos» trae impuestos y retenciones en proporción, desde la foto de la factura.
4. «Confirmar la nota». Una nota crédito no pasa de lo que queda de la factura. «Anular» la deshace con un documento
   contrario.

### Devolución a proveedor

1. **Compras → Devoluciones**. Elija la recepción confirmada: se ve lo devolvible (lo recibido menos lo ya devuelto).
2. Las cantidades van en la unidad de la recepción. «Guardar» deja el borrador con sus avisos.
3. «Confirmar la devolución». Sale al costo con que entró; la diferencia contra el costo promedio la registra el sistema
   como ajuste de costo.

### Cruce a tres vías

Al confirmar una factura cuyas recepciones vienen de una orden, cada línea se compara con lo ordenado y lo recibido, en
cantidad y en precio, con las tolerancias vigentes. Lo que excede queda **retenido**.

1. **Compras → Cruce a tres vías** muestra las líneas retenidas, aprobadas y rechazadas, con lo ordenado, recibido y
   facturado, las diferencias y la tolerancia que se usó. Los precios se ven con el permiso de costos.
2. Una línea retenida se aprueba o rechaza en **Inventario → Aprobaciones**. Aprobada la última, la factura se confirma y
   la diferencia de precio ajusta el costo de lo que sigue en existencia y el costo de venta de lo ya vendido.
   Rechazada, la factura vuelve a borrador.
3. Facturar **más de lo recibido** nunca entra por tolerancia: se corrige la factura o se registra otra recepción.

El informe «Cruce de compras a tres vías» está en Inventario → Informes.

### Costos adicionales (fletes y seguros)

1. **Compras → Costos adicionales**, «Repartir un flete o seguro».
2. La factura del servicio (ya confirmada), las recepciones que cubre, el método de reparto (valor, cantidad, peso,
   volumen o manual) y el monto (por defecto, lo que queda sin repartir de esa factura).
3. «Calcular el reparto»: una fila por línea de recepción con su base, lo asignado y el residuo del redondeo; con el
   permiso de costos, la porción que va a inventario y la que va a costo de venta.
4. «Confirmar»: ajusta el costo en el kardex sin reescribir las recepciones. Lo que sigue en existencia sube de costo y
   lo ya vendido va a costo de venta.

### Documento soporte

Para comprar a quien **no está obligado a facturar** (una persona natural que vende un servicio o un bien):

1. **Compras → Documentos soporte**, «Nuevo documento soporte».
2. Proveedor no obligado, las recepciones que cubre («Traer lo pendiente de la recepción») y los servicios sin
   recepción. «Guardar» calcula impuestos y retenciones.
3. «Confirmar» numera el documento con la resolución de documento soporte y lo transmite por el canal de facturación
   electrónica.
4. La **nota de ajuste** se hace desde el documento soporte confirmado: valor por línea y concepto de corrección.
   Siempre disminuye.

Según cómo lo genere la cooperativa (parámetro de documento soporte):

- **Por operación**: al confirmar la recepción de un no obligado, el sistema deja el borrador del documento soporte
  enlazado y avisa.
- **Semanal**: «Generar los de la semana» prepara un borrador por proveedor no obligado con las recepciones de la semana
  que no tienen factura ni documento soporte. Los borradores se revisan y confirman uno a uno.

## Reglas importantes

- **La recepción es la que mueve el inventario.** La solicitud y la orden no tocan existencias ni costos; la factura
  tampoco, salvo la diferencia de precio que aprueba el cruce a tres vías.
- **Los impuestos y retenciones los calcula el sistema** con el catálogo tributario vigente a la fecha de la operación
  y el perfil tributario del proveedor. Al confirmar queda la foto: un cambio posterior del catálogo no la toca.
- **Lo confirmado no se edita.** Se anula con un documento contrario, en orden: primero lo que depende (notas, facturas,
  devoluciones, costos adicionales) y después la recepción.
- **Fechas anteriores**: si la cooperativa las permite, la pantalla avisa cuando la fecha es anterior al último
  movimiento de algún producto y «Ver impacto en costos» muestra los documentos afectados antes de confirmar.
- **Aprobaciones**: la política del tipo de documento y el monto máximo de quien confirma deciden si algo queda en
  aprobación. Mientras está en aprobación, el documento está congelado y sin número.
- **Un documento soporte validado no se anula**: se corrige con su nota de ajuste.
- **Facturación electrónica simulada**: mientras no haya proveedor tecnológico, el documento soporte, su nota de ajuste
  y los eventos RADIAN salen por el canal simulado y no llegan a la DIAN.

## Preguntas frecuentes

**Recibí la mercancía pero la factura llega después. ¿Qué hago?**
Registre la recepción y confírmela; la existencia sube de una vez. Cuando llegue la factura, «Registrar factura» y
«Facturar lo pendiente de la recepción».

**El proveedor mandó más de lo que pedí.**
Si viene de una orden, entra sólo lo que cabe en la tolerancia. Lo demás se rechaza en la recepción o se registra una
recepción aparte sin orden.

**La factura tiene un precio distinto al de la orden y quedó retenida.**
Es el cruce a tres vías. La diferencia se aprueba o rechaza en Inventario → Aprobaciones. Si se rechaza, la factura
vuelve a borrador para corregirla.

**Me equivoqué en una recepción confirmada.**
Anúlela con motivo (si tiene factura, devolución o costos adicionales, anúlelos primero) y regístrela de nuevo.

**¿Cómo le cargo el flete a los productos?**
Registre la factura del transportador como servicio, confírmela y use «Repartir un flete o seguro» en Costos
adicionales.

**¿El acuse de recibo que emití llegó a la DIAN?**
Hoy no: el canal de facturación electrónica es el simulado. Si la cooperativa lo emitió en el portal de la DIAN o en otro
sistema, regístrelo con «Registrar» para que quede la constancia.

**¿Por qué no me deja recibir en una bodega?**
Porque no está en su alcance o todavía no está activa. Lo primero lo asigna el administrador en Inventario → Alcance
comercial; lo segundo es parte de la puesta en marcha.

## Lo que todavía no está

- **Canal real de facturación electrónica**: documento soporte, nota de ajuste y eventos RADIAN van por el canal
  simulado hasta que la cooperativa contrate un proveedor tecnológico.
- **Generación semanal automática del documento soporte**: hoy se dispara a mano con «Generar los de la semana»; la regla
  del último día hábil y la numeración de la nota de ajuste están por cotejar con la contadora y el proveedor.
- **Otros eventos RADIAN** (aceptación expresa o tácita, endoso): fuera del alcance.
- **Compras en moneda extranjera** (importaciones y diferencia en cambio): fuera del alcance.
- **Valores por confirmar con el dueño**: las tolerancias del cruce a tres vías de la cooperativa.
