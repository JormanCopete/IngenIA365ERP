# Unidades del producto, listas de precios y campos adicionales de venta

Mejora del 2026-11-15, pedida por el dueño después del 07/11 de COOFLOPAL. Ese día se cargaron los precios
de 21 aceites en UND y veinte minutos después su unidad base pasó a CTO. Los precios quedaron en una unidad
que el producto ya no tenía, la venta no los encontraba y la pantalla de precios mostraba el producto dos
veces.

## 1. Unidades del producto

| Parámetro (módulo INV) | Valores | Defecto | Qué hace |
|---|---|---|---|
| `Productos.VariasUnidades` | `NoPermitir`, `Permitir` | `NoPermitir` | Con `NoPermitir` el producto sólo tiene su unidad base. La ficha no muestra la sección de unidades alternas, la API las rechaza (`Inventory.ProductUnit.NotAllowed`) y la plantilla de productos marca como error la hoja «Unidades». |
| `Productos.UnidadBasePorDefecto` | código de unidad | `UND` | El alta de un producto (pantalla y plantilla) toma esta unidad si no se indica otra. Se puede cambiar. |

Si hay productos con unidades alternas, volver a `NoPermitir` se rechaza
(`Inventory.ProductUnit.StillInUse`) y el mensaje lista esos productos. Primero se retiran sus alternas.

En la ficha del producto, con `Permitir`, la casilla **«Se vende o compra en varias unidades»** muestra la
sección de alternas. Al desmarcarla se pide confirmación y se retiran todas sus alternas, junto con sus
precios.

**El precio acompaña a la unidad.** Esto vale en la ficha, en la plantilla de productos y en la
actualización masiva:

- **Cambiar la unidad base** (sólo se puede si el producto no tiene movimientos) mueve sus precios a la
  unidad nueva en todas las listas, en el mismo guardado. Si una lista ya tenía precio en la unidad nueva,
  ése se queda y el de la anterior sale.
- **Retirar una alterna** retira sus precios.

Las dos reglas viven en `UnidadesDelProducto`. Una unidad base no puede ser al mismo tiempo una alterna
del producto (`Inventory.Product.BaseUnitIsAlternate`).

**Compra y venta.** Con `Permitir`, al elegir un producto que tiene alternas para esa operación,
`SelectorDeProducto` (`Operacion="Venta"` o `"Compra"`) pregunta la unidad y propone la de defecto. Si el
producto se leyó por el código de barras de un empaque, la unidad ya viene. El servidor rechaza una unidad
que no corresponde a la operación (`Inventory.Unit.NotForOperation`) o una alterna retirada.

## 2. Pantalla de la lista de precios (`/ventas/listas-de-precios/{id}`)

**El buscador.** Es un solo campo: código, nombre, referencia o código de barras, igual que en las
ventas. Filtra desde **3 caracteres** y trae todo producto que coincida, tenga o no precio en la lista.
Vacío, muestra los productos que ya tienen precio en la lista.

**La ventana del producto.** Al elegir un producto se abre su ventana, que muestra:

- el **precio actual** en cada unidad en que se vende (la base y las alternas de venta);
- el **precio nuevo** de cada una, con un motivo obligatorio;
- «Sacar de la lista»;
- la **historia** de sus precios en la lista: fecha, qué pasó, unidad, precio anterior y nuevo, motivo y
  quién.

Los precios sólo se cambian desde esa ventana.

**La historia** (`INV_PriceListItemHistory`) la escribe `HistoriaDePrecios`, el único sitio, en el mismo
guardado que el cambio. Registra altas, cambios, retiros y pasos de unidad, vengan de la ventana, de la
plantilla de listas, de la actualización masiva o del cambio de unidad. Empieza el 15/11/2026: lo
anterior no se reconstruye.

## 3. Cotizaciones

Desde el 15/11/2026, por defecto, una cotización **se confirma sin cliente**: sale para el consumidor
final. Al cargarla en una venta, ésta toma el cliente que se le asigne allí. El parámetro
`Comercial.CotizacionExigeCliente` (defecto `false`) vuelve a exigir el cliente. Enviar la cotización por
correo sigue pidiendo un correo, que se puede escribir en el momento. En la cotización rigen las mismas
reglas de unidades que en la venta.

## 4. Campos adicionales de venta (`/ventas/campos-adicionales`)

La cooperativa define los campos que quiera; en COOFLOPAL, la placa del vehículo. Cada campo tiene:

- código y nombre;
- tipo: texto, número o fecha;
- tamaño;
- si es obligatorio;
- dónde se pide: ventas (factura de oficina y POS), cotizaciones o los dos;
- si se imprime en la factura;
- orden y estado.

Los administra quien tiene `Inventory.Parameters.Manage`.

**En el borrador.** El campo aparece en la cabecera de la factura, en el POS y en la cotización. Cada
valor se guarda al salir del campo (`PUT /api/inventory/documents/{id}/fields`), se valida (tipo, tamaño;
el texto se guarda en mayúsculas) y queda **sellado**: el documento guarda una copia del código, el
nombre y si se imprime. Cambiar la definición después no altera lo ya facturado.

**Al confirmar.** Los campos obligatorios se exigen (`Inventory.DocumentField.Required`).

**En la factura.** Los campos que se imprimen van, como «Placa: ABC123», en las **notas del documento
electrónico**: la DIAN los recibe y la representación gráfica los imprime.

**De la cotización a la venta.** Al cargar una cotización en una venta, los campos pasan a la venta si
ésta los pide y todavía no los tiene.

## Migración

`HistoriaDePreciosYCamposDeVenta`, aditiva, en par: crea `INV_PriceListItemHistory`,
`INV_DocumentFieldDefinitions` e `INV_DocumentFieldValues`.
