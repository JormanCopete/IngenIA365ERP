# Teclado y atajos

El ERP se puede trabajar sin ratón. Esto aplica en toda la aplicación, y sobre todo en la Caja de Recaudo, el Punto de venta y la factura de oficina.

## En cualquier pantalla

| Tecla | Qué hace |
|---|---|
| **Enter** | Pasa al siguiente campo. En el último campo lleva al botón principal (Guardar, Confirmar…); otro Enter lo ejecuta. |
| **Shift+Enter** | Vuelve al campo anterior. |
| **Tab** | También pasa al siguiente campo. |
| **F1** | Muestra cómo se usa el campo donde está el cursor. Esc o F1 otra vez la cierran. |
| **Esc** | Cierra lo que esté abierto: una lista, la ayuda o un diálogo. Nunca cancela una operación. |

Al entrar a un campo, su contenido queda seleccionado: lo que escriba lo reemplaza.

Si prefiere el comportamiento normal del navegador, apague **Perfil › Preferencias › Teclado › «Enter pasa al siguiente campo»**. El cambio se guarda en su cuenta.

Los valores en pesos se escriben con o sin puntos de miles: «50000» o «50.000». Al salir del campo se muestran con su formato.

## Buscar personas

Es el mismo buscador en todas las pantallas.

- Escriba el documento, el nombre o el apellido. La lista aparece mientras escribe y no importan las mayúsculas, las tildes ni el orden de las palabras.
- **↑ ↓** recorren la lista y **Enter** elige.
- Con el documento completo, **Enter** elige a la persona sin pasar por la lista.
- **↓** con el campo vacío muestra las personas recientes de ese usuario en ese equipo.
- **Alt+N** crea la persona si no existe, con el documento ya escrito.

En la Caja de Recaudo también acepta el número del crédito o de la cuenta.

## Medios de pago

El panel de medios funciona igual en el POS, la factura, las notas, los demás documentos de venta y la Caja.

- En la barra de medios, la **tecla del medio** lo agrega por lo que falta. Es la letra o el número que aparece en el botón y se configura en Maestros › Medios de pago. **← →** marcan un medio y **Enter** lo agrega.
- Al agregarlo, el cursor queda en el **valor**: escriba otro valor para pagar el resto con otro medio.
- **Enter** recorre los datos del medio:
  - efectivo: recibido;
  - tarjeta: datáfono, aprobación, lote y últimos 4;
  - transferencia o consignación: cuenta propia y referencia;
  - cheque: banco, número, cuenta, fecha, plaza y girador;
  - bono: número;
  - crédito: cuotas, plazo, periodicidad y primer vencimiento.
- Al terminar, el cursor vuelve a la barra si falta dinero, o va a Confirmar si no.
- Efectivo: **Alt+0** pone el valor exacto y **Alt+1…** los billetes del catálogo. Las vueltas se ven al instante.
- **↑ ↓** en la barra eligen un pago y **Supr** lo quita.
- El datáfono usado se recuerda en el equipo.

En ventas, la transferencia y la consignación piden la cuenta propia donde entró el dinero, entre las habilitadas para el punto de venta (Ventas › Puntos de venta). Si no hay cuentas habilitadas, la venta funciona como antes. El cheque pide un cliente identificado.

## Caja de Recaudo

Trabaja a pantalla completa.

| Tecla | Qué hace |
|---|---|
| F2 | Buscar persona o número |
| ↑ ↓ y Espacio | Recorrer lo que puede pagar y agregarlo |
| F4 | Agregar todo lo exigible |
| F7 | Todo para cancelar: cada crédito por su cancelación, las cuotas de las cuentas y las facturas |
| F10 | Ir a los medios; con los medios completos, confirmar |
| Ctrl+Alt+X | Cancelar la operación (pide confirmación) |
| Enter | Tras el recibo, abrir una operación nueva |
| Ctrl+P / Ctrl+Shift+P | Imprimir la tirilla o la carta |

La simulación se actualiza sola y la imputación se hace al confirmar.

## Punto de venta

Conserva sus teclas: F2 buscar, F3 cantidad, F4 cliente, F6 vendedor, F7 descuento, F8 suspender, F9 recuperar, F10 cobrar y confirmar, Supr quitar línea, Esc volver. **Ctrl+Alt+O** carga una cotización (en la factura de oficina es F8, que en el POS ya es suspender).

- Cada tecla es también un **botón** en la barra de abajo, que siempre está a la vista: hace lo mismo que la tecla y se ve apagado cuando no aplica (por ejemplo, F3 sin una línea elegida).
- Después de cobrar, **Enter o Esc** empiezan la venta siguiente, sin tener que volver al campo de lectura; leer un producto con el lector también la empieza con ese producto.
- F2 abre la búsqueda con lo que ya se había escrito.
- La lista de productos muestra el precio que aplica, el disponible y los empaques.
- Los diálogos se aplican con Enter.

## Ventanas de Comercial (factura, cotización, pedido, remisión, nota débito)

Desde el 2026-11-23 (pedido del dueño) todas usan la misma ventana, la del comprobante contable. Arriba va un encabezado
compacto; lo secundario (vencimiento, referencia, descuento al total, notas y campos de la cooperativa) queda en «Más
datos». Las líneas se desplazan solas y el pie, con los totales, los botones y la barra de atajos, siempre se ve. Cada
atajo es también un botón de la barra; si no aplica en ese momento sale gris, con el motivo al pasar el ratón.

| Tecla | Qué hace |
|---|---|
| F1 | Ayuda del campo |
| F2 | Buscar producto (según `Comercial.BusquedaDeProductos`: la ventana por nombre o el campo) |
| F3 | Ir al campo de producto para una línea nueva |
| F4 | Buscar el cliente en una ventana: se escribe el documento o el nombre, ↑ ↓ recorren y Enter elige |
| F8 | Traer documento (factura): remisiones por facturar del cliente o una cotización |
| F9 | Guardar el borrador (la factura además se guarda sola) |
| F10 | Medios de pago y, con los pagos completos, confirmar. Sin pagos (cotización, pedido, remisión), confirmar |
| Ctrl+Supr | Quitar la línea donde está el cursor |
| Ctrl+P | Imprimir: la factura confirmada; la cotización, el pedido y la remisión, en cualquier momento |
| Esc | En cotizaciones, pedidos, remisiones y notas débito, volver a la lista |

**Las líneas.** El recorrido es el mismo en todas: producto → (placa, si el producto la pide) → cantidad → precio →
descuento → otra vez el producto. Enter y Tab hacen lo mismo; Shift+Tab devuelve.

- Al elegir el producto (digitado y Enter, de la lista o de la ventana F2), el cursor va a su cantidad. Con «3*código»
  la cantidad ya va y el cursor se queda en el producto.
- La lectura del lector de códigos que cae en una cantidad vuelve sola al campo de producto.
- Si el mismo producto vuelve a entrar con la misma unidad y el mismo precio, y no pide lote, serie ni placa, se suma
  su cantidad a la línea que ya lo tiene. Si no, va en una línea nueva.

**Factura desde remisiones o cotización.** «Traer el pedido» se retiró. La factura trae documentos con **F8**:

- **Remisiones por facturar.** Se eligen una o varias del mismo cliente y se abre la factura desde remisiones, que
  trae lo pendiente de cada línea. Para facturar en parte se baja la cantidad (al precio de la remisión); subirla
  sobre lo pendiente no se puede. Si la factura en curso ya tenía líneas, queda guardada como borrador.
- **Cotizaciones.** La de siempre: vigente con sus precios, vencida con los de hoy.

La remisión confirmada tiene **Imprimir** y **Facturar** (mientras tenga algo por facturar). Los pedidos siguen su
camino: pedido → remisión → factura.

**Factura de oficina, además:** el borrador se guarda solo y el título dice si quedó guardado. Tipo, bodega y vendedor se
proponen como en la última factura del equipo. Al confirmar, Enter abre una nueva y Ctrl+P imprime en carta.

## Comprobante contable

**Contabilidad › Comprobantes › Nuevo** (2026-11-16). La ventana ocupa la pantalla: sólo las líneas se desplazan, y el pie con totales, botones y atajos siempre se ve. Cada atajo es también un botón de la barra de abajo.

| Tecla | Qué hace |
|---|---|
| F1 | Ayuda del campo donde está el cursor |
| F2 | Buscar en el campo: cuenta, tercero, tipo de documento cruce, centro, sucursal o tipo de comprobante |
| F3 | Línea nueva debajo de la activa; repite tercero, centro, sucursal y detalle |
| F4 | Buscar el tercero de la línea activa (en el campo o en la ventana, según el parámetro) |
| Ctrl+D | Duplicar la línea activa, sin los importes (hasta el 2026-11-23 era F4) |
| F6 | Balancear: poner en la línea activa lo que falta para cuadrar |
| Ctrl+Supr | Quitar las líneas seleccionadas o, si no hay selección, la activa. Si son varias, pide confirmación |
| F9 | Guardar el borrador. Desde ese momento cada cambio se guarda solo |
| F10 | Contabilizar: guarda, toma el número y deja listo un comprobante nuevo con el mismo tipo y fecha |
| Ctrl+P | Imprimir el comprobante abierto o, en uno nuevo, el último contabilizado |

**Enter.** Pasa de campo en campo: tipo, fecha, descripción y después cada campo de la línea. Al elegir una cuenta o un tercero, el cursor sigue al campo siguiente. En «Detalle», Enter pasa a la línea siguiente; en la última línea abre una nueva.

**F2 en el campo o en una ventana.** Lo decide el parámetro `Accounting.VoucherSearchMode`, en **Administración › Parámetros del sistema**:

- `EnElCampo` (el defecto): la lista sale debajo del campo mientras se escribe.
- `VentanaF2`: en la cuenta se digita el código exacto y Enter lo toma. Si el código no existe, o con F2, se abre una ventana para buscar por nombre. En tercero, centro, sucursal y documento cruce, F2 abre la ventana de búsqueda de ese campo.

**Selección.** La línea activa se marca con una barra a la izquierda. Para seleccionar varias líneas:

- clic en el número de la línea;
- **Shift+clic** marca un rango;
- **Ctrl+clic** suma o quita una línea;
- **Shift+↑/↓** extiende la selección;
- un clic en «#» del encabezado selecciona todas o ninguna.

**Lo que se pone solo.**

- La sucursal de la primera línea es la de la oficina por defecto de quien digita o, sin ella, la principal. Si sólo puede usar una, la lista sólo muestra esa.
- Cada línea nueva repite la sucursal de la anterior.
- El centro de costo y el tipo de documento cruce se ponen solos cuando hay uno solo.
- El tipo de comprobante es el último usado en el equipo.

Los pasos y la ayuda de la derecha van ocultos. **Pasos y ayuda** los muestra, y el equipo lo recuerda. Los soportes se adjuntan desde el botón **Soportes** del pie.

La impresión sale en carta vertical, con el mismo número, fecha y estado del comprobante y las firmas de quien elaboró, contabilizó y revisó. Un borrador lleva la marca de agua «BORRADOR».
