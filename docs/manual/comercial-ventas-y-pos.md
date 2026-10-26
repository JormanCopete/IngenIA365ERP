# Comercial: ventas, punto de venta y caja

> Manual funcional de los grupos **Ventas** y **Punto de venta** del menú, y de **Maestros → Medios de pago** (feature
> 012, entregas I3 e I6). Para el asesor de ventas, el cajero, el supervisor de caja y quien parametriza precios y
> cajas. El índice del módulo está en [comercial.md](comercial.md); la parte electrónica de cada venta (DIAN), en
> [comercial-facturacion-electronica.md](comercial-facturacion-electronica.md).

## Qué hace

- **Ventas de oficina**: factura con pagos múltiples, nota crédito (devolución o rebaja), nota débito, entrega por tirilla,
  carta o correo y reimpresión con la marca «COPIA».
- **Ciclo comercial**: cotización (ofrece precios, no mueve nada), pedido (reserva la mercancía), remisión (despacha sin
  facturar) y factura desde remisiones.
- **Precios**: listas de precios con vigencia por cliente, segmento, canal y sucursal; topes de descuento por rol;
  promociones que se aplican solas.
- **Punto de venta (POS)**: la venta en caja con lector y teclado, cobro con uno o varios medios, suspender y recuperar.
- **Caja**: apertura con base, movimientos (retiros, ingresos de base, traslados, reclasificaciones), cierre con arqueo
  por medio de pago y cierre del día por punto.
- **Medios de pago**: efectivo, tarjetas, transferencias, bonos, créditos, con sus franquicias, adquirentes, datáfonos y
  denominaciones del efectivo.
- **Vendedores y canales**, e **informes de ventas y de caja**.

Toda venta descuenta la existencia de su bodega al confirmarse (salvo la que viene de una remisión, que ya la descontó),
y lo confirmado no se edita: se corrige con una nota o se anula.

## Quién lo hace

| Tarea | Permiso |
|---|---|
| Consultar ventas | ver ventas (`Inventory.Sales.View`) |
| Registrar facturas, notas, cotizaciones, pedidos, remisiones | registrar ventas (`Inventory.Sales.Create`) |
| Confirmar | confirmar ventas (`Inventory.Sales.Confirm`) |
| Anular | anular ventas (`Inventory.Sales.Void`) |
| Vender a crédito | vender a crédito (`Inventory.Sales.SellOnCredit`), con su monto máximo |
| Vender en el POS | vender en el POS (`Inventory.Pos.Sell`) |
| Abrir y cerrar caja | abrir sesión (`Inventory.CashSessions.Open`), cerrar sesión (`Inventory.CashSessions.Close`) |
| Movimientos de caja | registrar (`Inventory.CashMovements.Create`), aprobar (`Inventory.CashMovements.Approve`) |
| Cierre del día | cerrar (`Inventory.DayClose.Execute`), reabrir (`Inventory.DayClose.Reopen`) |
| Puntos de venta y cajas | ver (`Inventory.PointsOfSale.View`), cambiar (`Inventory.PointsOfSale.Manage`) |
| Listas de precios y promociones | ver (`Inventory.Prices.View`), cambiar (`Inventory.Prices.Manage`) |
| Topes de descuento | cambiar (`Inventory.DiscountCaps.Manage`) |
| Medios de pago | ver (`Core.PaymentMeans.View`), cambiar (`Core.PaymentMeans.Manage`) |
| Informes y exportación | ver (`Inventory.Reports.View`), exportar (`Inventory.Reports.Export`; con datos de clientes, además `Inventory.Reports.ExportPersonalData`) |

## Paso a paso en pantalla

### Antes de vender

1. **Maestros → Medios de pago**: los medios que acepta la cooperativa y, en sus pestañas, franquicias, adquirentes,
   datáfonos de cobro y denominaciones del efectivo.
2. **Ventas → Canales de venta**: los canales (almacén, domicilios, convenios…).
3. **Ventas → Listas de precios**: al menos la lista **general** (sin ámbito) con los precios de todo lo que se vende.
4. **Ventas → Topes de descuento**: el descuento máximo por línea y por total de cada rol.
5. **Ventas → Puntos de venta**: cada punto con sus cajas y los tipos de documento de cada caja.
6. **Ventas → Vendedores**, si la cooperativa registra quién vende.
7. La facturación electrónica configurada (ver [comercial-facturacion-electronica.md](comercial-facturacion-electronica.md)).

### Medios de pago

**Maestros → Medios de pago** tiene cinco pestañas: Medios, Franquicias, Adquirentes, Datáfonos de cobro y
Denominaciones. Cada una con su botón «Nuevo…». En un medio se define:

- código, clase (efectivo, tarjeta, transferencia, bono, crédito…), la **tecla rápida** del POS y el código DIAN;
- si pide referencia y cuál (el número del bono; con referencia única, un bono no se usa dos veces);
- si da vueltas o admite pago parcial;
- cómo se **arquea** (por denominaciones, por lote del datáfono, por referencias, o no se arquea) y con qué tolerancia;
- la comisión esperada y, en los créditos, plazo, cuotas y periodicidad;
- **dónde se ofrece**: en todos los puntos, canales y tipos de venta, o sólo en los elegidos.

Editar un medio pide motivo y queda en su historia. «Plantilla 11» los carga en bloque. Los medios de crédito sólo
aparecen con un cliente identificado y a quien tiene el permiso de vender a crédito.

### Puntos de venta y cajas

1. **Ventas → Puntos de venta**, «Nuevo punto»: sucursal, canal, bodega por defecto y si **vende en el POS** o cobra en
   oficina.
2. «Cajas» → «Nueva caja»: código, nombre, bodega de la sucursal del punto, datáfono por defecto, impresión (tirilla de
   58 u 80 mm o carta), número de copias y el tipo de documento de cada uno de sus roles: venta POS, factura a pedido del
   comprador, nota de ajuste POS, nota crédito de factura y los dos de contingencia.
3. «Plantilla 10» carga puntos y cajas en bloque.

### Listas de precios y topes de descuento

1. **Ventas → Listas de precios**, «Nueva lista»: ámbito (cliente, segmento, canal, sucursal; vacío es la general),
   vigencia y si los precios incluyen impuestos. El ámbito, el código y si incluye impuestos no cambian después.
2. En la lista, los precios se agregan o cambian por producto y unidad («Agregar al cambio») y se guardan juntos con un
   motivo («Guardar precios»). «Consultar el precio que gana» dice qué precio sale, de qué lista y cuáles compitieron.
3. **Ventas → Topes de descuento**, «Nuevo tope»: por rol, en porcentaje de la línea y del total, con vigencia. A quien
   tiene varios roles le aplica el mayor.
4. «Plantilla 12» (listas con sus precios) y «Plantilla 13» (topes) cargan en bloque.

El precio de una venta sale de la lista **más específica vigente** para ese cliente, su segmento, el canal y la
sucursal; sin ninguna, de la general.

### Promociones

**Ventas → Promociones**, «Nueva promoción»: código, nombre, clase (porcentaje, valor por unidad, lleve N pague M,
precio por cantidad con sus tramos o precio de paquete), vigencia y motivo.

- **Ámbitos**: productos, categorías (con sus subcategorías), segmentos y canales; vacío es todo lo vendido. Entre clases
  de ámbito se exigen todas; dentro de una clase basta una.
- **Acumulable**: entre las no acumulables gana la de mayor descuento. Una línea con promoción no admite descuento
  manual.
- La promoción se aplica sola al vender, en oficina y en el POS, como descuento en la línea que baja la base de los
  impuestos; el documento dice cuál se aplicó.
- Ya usada en un documento confirmado, sólo cambian el nombre, el fin de la vigencia y si está activa.

### Venta de oficina (factura)

1. **Ventas → Nueva factura** (o «Nueva factura» desde Documentos de venta).
2. Tipo, bodega, cliente, vendedor y productos; el precio sale de la lista que gana y las promociones se aplican solas.
   Desde un pedido, «Traer el pedido» trae lo pendiente.
3. «Guardar» calcula el total con impuestos y retenciones.
4. Se registran los pagos, uno o varios medios. Los medios que se arquean van a la sesión de caja abierta.
5. «Confirmar». La factura se numera con la resolución DIAN y se transmite (ver el manual de facturación electrónica).
6. En el detalle de la venta: «Entregar» (tirilla, carta en PDF o por correo; la primera entrega sale sin marca) y
   «Reimprimir» (con la marca «COPIA», queda auditada).

### Nota crédito (devolución o rebaja)

Desde una venta confirmada, «Nota crédito»: por cantidad (con devolución a una bodega), por valor, o la anulación
total, con los **reintegros** por medio de pago. «Guardar» y «Confirmar».

Un documento electrónico no se anula con un documento contrario: «Anular» emite en una sola confirmación la nota
crédito total (o la nota de ajuste del documento equivalente POS) con la devolución.

### Nota débito

**Ventas → Notas débito**, elija la factura y «Nueva nota débito»: el concepto de corrección (intereses, gastos, un
mayor valor), las líneas, «Guardar» para ver el total, los pagos y «Confirmar». No mueve la existencia y se transmite a la
DIAN como la factura. Validada, no se anula: se corrige con otra nota.

### Cotización, pedido y remisión

- **Ventas → Cotizaciones**, «Nueva cotización»: tipo, bodega, cliente, vendedor, **vigencia** y productos. Confirmarla
  no reserva ni emite nada. Confirmada y vigente, «Convertir en pedido» crea el borrador del pedido con las líneas y
  precios cotizados; se puede convertir más de una vez (pedidos parciales). Vencida, ya no se convierte.
- **Ventas → Pedidos**, «Nuevo pedido»: cada línea muestra físico, reservado y disponible en la bodega. Confirmado,
  **reserva** lo pedido hasta la fecha de vencimiento de la reserva (que se libera sola). Si no alcanza el disponible, no
  se confirma. Desde el pedido: «Remisionar» despacha sin facturar y «Facturar» factura lo pendiente. Anular libera lo
  que siga reservado.
- **Ventas → Remisiones**, desde un pedido o «Remisión directa»: despacha la mercancía y reconoce su costo sin facturarla.
  La lista dice cuántos días lleva cada una sin facturar. Para facturar, elija remisiones confirmadas del mismo cliente
  y «Facturar remisiones»: la factura trae lo pendiente de cada línea, de sólo lectura. Sin facturar se anula (la
  mercancía vuelve al costo con que salió); facturada, ya no.

### Vender en el POS

Hace falta una **sesión de caja abierta** en un punto que vende en el POS. Si no la tiene, la pantalla lleva a abrirla.

1. **Punto de venta → Vender en el POS**.
2. **Leer los productos**: el campo de lectura siempre tiene el foco. Enter lee el código exacto (el de un empaque trae su
   unidad); «3*» antes de leer multiplica por 3; un código desconocido abre la búsqueda. Si el producto controla serie, la
   lectura siguiente es su serie; si controla lote, se propone el que vence primero.
3. **Cambiar la venta** con las teclas de la tabla de abajo.
4. **Cobrar (F10)**: el panel muestra lo que falta, lo que sobra y las vueltas. La tecla rápida o el botón de un medio
   agrega un pago por lo que falta. De la tarjeta sólo se digitan los **últimos cuatro dígitos**; el datáfono se propone
   el de la caja. «Confirmar (F10)» confirma; «Volver (Esc)» regresa a la venta.
5. **Entregar**: la tirilla sale con el cobro y se imprime. Si la venta quedó en aprobación, se espera y se entrega al
   aprobarse. «Nueva venta (Enter)» empieza la siguiente.

La venta vive en el servidor desde la primera lectura: un doble clic o una conexión que se cae no venden dos veces.

Atajos del POS:

| Tecla | Qué hace |
|---|---|
| Enter | Lee el código escrito o leído |
| F2 | Buscar producto |
| F3 | Cambiar la cantidad de la línea elegida (las flechas eligen la línea) |
| F4 | Cliente: buscarlo o crearlo con su autorización de datos; si el comprador pide factura, la venta pasa a factura |
| F6 | Vendedor |
| F7 | Descuento o precio digitado (de la línea elegida o del total) |
| Supr | Quitar la línea elegida |
| F8 | Suspender la venta con un rótulo |
| F9 | Recuperar una venta suspendida (en cualquier caja del punto) |
| F10 | Cobrar; otra vez, confirmar |
| Esc | Cancelar el diálogo o volver a la venta |
| Ctrl+Alt+R | Reimprimir la última venta con la marca «COPIA» |
| Ctrl+Alt+D | Descartar la venta en curso, con motivo |
| Ctrl+Alt+M | Ir a Movimientos de caja |
| Ctrl+Alt+C | Ir al cierre de la caja |

**Descuento sobre el tope**: la línea queda esperando la aprobación de alguien con un tope suficiente, desde su bandeja
de aprobaciones o **en persona en la caja** con su llave (passkey) o el código de su autenticador. Nunca con contraseña.

### Sesión de caja

1. **Punto de venta → Sesiones de caja**: su sesión abierta aparece arriba. Si no tiene, elija punto y caja libre, cuente
   la base por denominaciones y «Abrir la caja». Una caja tiene una sola sesión abierta y, si la cooperativa lo pide, un
   cajero también.
2. El detalle de la sesión muestra lo pendiente antes de cerrar (ventas suspendidas, en borrador o pendientes de
   entrega, que se entregan desde ahí con «Entregar») y sus movimientos.

### Movimientos de caja

**Punto de venta → Movimientos de caja**, «Nuevo movimiento»: retiro parcial a caja fuerte, traslado a otra caja,
retiro para consignar, ingreso de base o **reclasificación** de un pago entre medios (por ejemplo, de Visa a Mastercard
sin tocar la venta). «Guardar borrador» o «Guardar y confirmar». Si su política lo pide, quedan en aprobación. Cada uno
tiene su comprobante con firmas.

### Cierre de la caja

Desde la sesión o con Ctrl+Alt+C en el POS:

1. Cada medio se cuenta según su forma de arqueo: el efectivo por denominaciones, las tarjetas por el lote de cada
   datáfono, los que se cotejan por referencia marcándolas una a una. Los que no se arquean no se piden.
2. Con **arqueo ciego** el cajero no ve lo esperado.
3. Una diferencia fuera de la tolerancia pide motivo; el sistema decide su tratamiento (sobrante, faltante al cajero o
   al gasto) según la política de la cooperativa.
4. El retiro de cierre lleva el efectivo a la caja fuerte, a otra caja o a consignar. «Cerrar la caja».
5. Cerrada, «Informe del arqueo» descarga el PDF y «Recontar con lo digitado» permite rehacer el conteo.

### Cierre del día

**Punto de venta → Cierre del día**: con todas las sesiones del día cerradas, «Cerrar el día» consolida por medio,
adquirente y datáfono. «Reabrir» pide motivo y el siguiente cierre es otra versión.

### Vendedores y canales

- **Ventas → Vendedores**, «Nuevo vendedor»: la persona se busca en el maestro (o se crea en su diálogo). Dar el rol a
  quien lo tuvo lo **restaura** con el mismo identificador; «Retirar…» pide motivo. «Plantilla» (la 9) los carga en
  bloque.
- **Ventas → Canales de venta**: los canales que usan los tipos de documento, los puntos de venta y las listas de
  precios. Uno en uso no se inactiva.

### Informes de ventas

**Ventas → Informes de ventas**: elija el informe, el rango y el punto, y «Consultar». Están: ventas por sesión de caja,
por caja y por medio de pago, la sesión de caja, el cierre del día, pagos con tarjeta, movimientos de caja, diferencias
de arqueo, bonos redimidos, aprobaciones de descuento e indicios de deterioro. «Excel», «PDF» o «Word» para exportar.
Las cifras de margen, rotación y ventas del día también están en el **tablero de inventario**.

## Reglas importantes

- **El precio no se digita libremente**: sale de la lista que gana. Cambiarlo o dar un descuento sobre el tope del rol
  pide aprobación.
- **Promoción o descuento manual, no los dos** en la misma línea.
- **El pedido reserva**: lo reservado deja de estar disponible para otras ventas hasta que se remisiona, se factura, se
  anula o vence la reserva.
- **Una remisión vieja sin facturar** levanta una alerta y el cierre del período de inventario pide aceptarla con motivo.
- **De la tarjeta sólo se guardan los últimos cuatro dígitos.** Nunca el número completo.
- **La aprobación en persona** se hace con llave (passkey) o código del autenticador del aprobador, nunca con contraseña.
- **Lo confirmado no se edita**: se corrige con nota crédito o débito, o se anula. Un documento ya validado ante la DIAN
  se anula con su nota total.
- **Reimprimir siempre marca «COPIA»** y queda auditado.
- **Venta a crédito**: mientras Cartera no esté activada en la cooperativa, una venta con un medio de crédito va como
  **crédito provisional**: queda en aprobación de alguien con el permiso de vender a crédito y un monto máximo suficiente
  (no el cajero ni quien la creó) y se confirma con la última aprobación. Ver
  [cartera-ventas-a-credito.md](cartera-ventas-a-credito.md).
- **Facturación electrónica simulada**: hoy las facturas, notas y tirillas del POS salen con la leyenda «SIN VALIDEZ
  FISCAL» y no llegan a la DIAN (ver [comercial-facturacion-electronica.md](comercial-facturacion-electronica.md)).

## Preguntas frecuentes

**El lector no encuentra el producto.**
Se abre la búsqueda (F2). Si no existe, hay que crearlo o agregarle el código de barras en Inventario → Productos.

**El cliente quiere factura a su nombre después de pagar en el POS.**
En el detalle de la venta, «Pedir factura»: emite la nota de ajuste del documento equivalente y la factura a nombre del
comprador en una sola operación.

**Me equivoqué de medio de pago (pagó con Mastercard y marqué Visa).**
Movimientos de caja → «Nuevo movimiento» de reclasificación entre medios. No toca la venta.

**Necesito dejar una venta a medias para atender a otro cliente.**
F8 la suspende con un rótulo; F9 la recupera, incluso en otra caja del mismo punto.

**La caja no me deja cerrar.**
Hay ventas suspendidas, en borrador o pendientes de entrega: el detalle de la sesión las muestra. Entréguelas o
descártelas antes.

**El cierre del día no deja cerrar.**
Alguna sesión del día sigue abierta. Todas deben estar cerradas.

**¿Por qué la venta a crédito quedó «en aprobación»?**
Porque es crédito provisional: la aprueba alguien con el permiso de vender a crédito y un monto máximo suficiente. En el
POS la pantalla espera la decisión; en la factura de oficina, la pestaña «Crédito» del documento muestra el estado.

**¿Por qué una factura salió rechazada si los datos están bien?**
Con el canal simulado de facturación electrónica el resultado lo decide el simulador, no la DIAN. Ver las preguntas
frecuentes de [comercial-facturacion-electronica.md](comercial-facturacion-electronica.md).

## Lo que todavía no está

- **Crédito validado por Cartera**: Cartera ya puede recibir las ventas a crédito, pero no está activada; hasta entonces,
  crédito provisional con aprobación.
- **En la factura de oficina**, la espera de la aprobación del crédito no se queda en la pantalla: avisa y lleva al
  documento.
- **Nota crédito sobre facturas expedidas fuera del ERP** (anteriores a la salida en vivo): sin decidir; hoy no se hace.
- **Validez fiscal**: las facturas, notas y documentos equivalentes van por el canal simulado.
- **Impresión de las cajas**: pendiente que el dueño defina impresora (58 u 80 mm) y navegador en modo quiosco.
