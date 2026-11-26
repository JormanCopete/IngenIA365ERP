# Transporte: vehículos, servicios y facturación mensual

Feature 020. Este manual es para quien administra el módulo en una cooperativa de transporte. Los
avisos a sistemas externos (radio, app…) están en [conectores-externos.md](conectores-externos.md).

## 1. Habilitar el módulo

Transporte no se ve en ninguna cooperativa hasta que el **administrador de la plataforma** lo enciende
en **Seguridad › Módulos por cooperativa** (`/saas/modulos`), con un motivo.

- Encender no escribe nada en la cooperativa en ese momento: en la siguiente pasada del programador
  (unos segundos) quedan sembrados los catálogos (marcas, clases, combustibles, modalidades, clases de
  servicio, tipos de documento) y registrados los participantes `TRN` y `CON` de la bandeja.
- Apagar oculta el menú y las rutas (responden 403 `Module.NotEnabled`, también al maestro). **Los datos
  se conservan**, y los pagos de facturas de servicios ya emitidas se siguen aplicando.
- Los permisos `Transport.*` se ven en Roles sólo con el módulo encendido.

## 2. Vehículos y propietario

**Transporte › Vehículos**. Cada vehículo tiene:

- **placa** (única entre los vivos; se guarda en mayúsculas, sin espacios ni guiones);
- **número interno** de la cooperativa (único, obligatorio);
- **código radio-móvil** (opcional, único entre vivos): el número con que el sistema externo identifica el
  vehículo;
- ficha técnica, vinculación (ingreso, contrato, tarjeta de operación, rodamiento) y equipos.

El **propietario** es una persona del maestro de personas. Si no existe, se crea en el mismo paso
(«Nueva persona»). La **clase de vinculación** no se digita: es «asociado» si la persona tiene una ficha
de asociado vigente, y «externo» si no.

Los **cambios de estado** (Activo, Inactivo, Retirado) y de **propietario** llevan fecha efectiva y motivo,
y quedan en la historia. Al cambiar de propietario, la ficha informa las deudas del saliente: **no
bloquean**.

### Buscar y filtrar el parque

La lista de vehículos pagina en el servidor (25, 50, 100 o 200 por página) y dice cuántos hay con el
criterio. Se combina:

- **Buscar**: placa (con o sin guion), número interno, radio-móvil, nombre o razón social del propietario
  (las palabras en cualquier orden, sin tildes) o su documento. Un número corto (hasta 4 dígitos) se busca
  como número interno o lateral y como documento exacto; desde 5 dígitos, también los documentos que
  empiezan así.
- **Estado**, **vinculación** (asociado o externo, deducida a hoy) y **servicio**: «con algún servicio»,
  «sin ningún servicio», o un servicio en particular con «suscritos» o «no suscritos».

La columna **Servicios** muestra las suscripciones que no han terminado: las vigentes y las que empiezan más
adelante («· desde 01/12»). En verde y con «· pactado», las que tienen precio pactado. Una suscripción
terminada antes de hoy o quitada no cuenta.

**Exportar a Excel** (permiso `Transport.Vehicles.Export`) descarga lo filtrado, con la columna de
servicios. Es el informe «Vehículos» con los mismos filtros, y queda en la auditoría.

## 3. Plantillas

**Transporte › Plantillas**: misma mecánica que las demás cargas (revisar sin guardar, aplicar todo o nada,
crear, actualizar o conservar).

| Plantilla | Llave | Para qué |
|---|---|---|
| Vehículos | placa | alta y actualización del parque; propietario por documento |
| Cambios de vehículo | placa | estado o propietario masivos, con fecha y motivo |
| Documentos | placa + tipo | renovaciones masivas (SOAT, revisión, pólizas…) |
| Conductores | documento | marcar conductores y asignarles vehículo |
| Suscripciones | placa + servicio | suscribir el parque a sus servicios |

## 4. Documentos del vehículo

Los tipos (SOAT, revisión técnico-mecánica, tarjeta de operación, pólizas RCC, RCE, todo riesgo, revisión
preventiva) vienen sembrados y cada cooperativa los ajusta: obligatorio sí/no y días de aviso.

**Transporte › Documentos** muestra el tablero de vencidos y por vencer. Los vencimientos **se informan**:
no bloquean ni inactivan el vehículo. Renovar un documento deja el anterior en la historia.

## 5. Conductores

Registro básico: la persona marcada como conductor y sus vehículos asignados con fechas (uno puede ser el
principal). Licencias, tarjeta de control y seguridad social no están en esta versión.

## 6. Servicios

**Transporte › Servicios**. Cada servicio (radio, app u otro) es un **producto de Comercial** de clase
servicio, con su precio vigente. En la configuración del servicio se dice:

- su **prioridad** (orden de las líneas en la factura y de los pagos que no dicen a qué línea van);
- la **línea de Cartera** de su obligación (marcada «de servicio», sin interés), si la cooperativa tiene
  Cartera;
- los **medios de pago a crédito** para asociados y para externos (deben admitir pago parcial);
- el **conector** que avisa al sistema externo cuando el servicio queda pagado (opcional).

Cada vehículo se suscribe a los servicios que quiera, con vigencia; la suscripción puede llevar un
**precio pactado** con vigencia propia (no cuenta como descuento ni pide aprobación).

En la ficha del vehículo, pestaña **Servicios** (permiso `Transport.Services.Manage`):

- **Terminar** pide la fecha (el último día que se cobra, desde el inicio de la suscripción) y el motivo.
  En una que ya tiene fin, **Cambiar fin** lo mueve; si lo alarga, el precio pactado se alarga con él.
- **Quitar** da de baja, con motivo, una suscripción que **nunca se facturó**: la que todavía no empieza o
  la que no tiene cargos de la facturación mensual no anulados en su vigencia. Las ventas sueltas con la
  placa no cuentan. Queda en la auditoría y deja de verse en todo lado.
- **Cambiar inicio**, con la misma condición, mueve el inicio y el primer precio pactado.
- Lo ya facturado no se quita ni se le mueve el inicio: se termina.

### Terminar en lote con la plantilla

La plantilla de suscripciones (Transporte › Plantillas) es por **placa y código de producto**. La fecha
`desde` crea la suscripción si no existe y, si existe, sólo la identifica: sirve cualquier fecha de su
vigencia. Sobre una suscripción que ya existe:

- con `hasta`, **la termina** en esa fecha si no tenía fin o tenía otro (se informa como «actualizar»,
  campo `hasta`);
- con `precioPactado`, pacta ese precio;
- con «conservar» no se toca;
- un `hasta` vacío no le quita el fin a una que lo tiene.

Para quitar en lote las que nunca se facturaron no hay plantilla: se quitan una a una desde la ficha.

La **política de facturación** (con vigencia) dice si a mitad de mes se cobra el mes completo o la
fracción, el tipo de documento de venta, la bodega o punto de venta y el día de vencimiento.

## 7. Facturación mensual

**Transporte › Facturación de servicios**.

- **Vista previa** del período: qué vehículos se facturan, cuáles se omiten y por qué.
- **Individual**: un vehículo, un período.
- **Masiva**: todos los vehículos activos con suscripciones vigentes. La pantalla avanza la corrida por
  tandas mientras siga abierta. Si se cierra, la corrida queda **en curso** donde iba y se retoma con
  **Continuar**, en «Corridas anteriores» o en el aviso que sale al intentar facturar otra vez el mismo
  período. Continuar sigue el mismo mecanismo de tandas: no duplica nada. Si la corrida avanzó hace menos de
  dos minutos, la pantalla pide confirmar, porque puede que otra persona la esté corriendo. Las facturas de
  lo que falte salen a nombre de quien continúa.

**Antes de facturar**, la pantalla revisa el período y pide confirmar —es un aviso, no un bloqueo— si:

- el período es **anterior al mes en curso**;
- hay suscripciones por facturar que **empezaron después del día 1**. Con la política de mes completo se
  cobran el mes entero. Si ese mes ya se cobró por otro medio, por ejemplo en la carga del sistema anterior,
  facturarlo lo cobraría dos veces.

Lo mismo hace «Facturar este mes» en la ficha del vehículo. La vista previa muestra los mismos avisos. Si el
mes ya estaba cobrado, no lo facture: mueva el inicio de esas suscripciones (si nunca se facturaron) o
facture desde el mes siguiente. La pantalla propone el mes en curso.

Cada vehículo recibe **una factura de Comercial** con una línea por servicio, a nombre del propietario.

- **A crédito**: cada servicio queda como una obligación de Cartera (o una cuenta por cobrar si no hay
  Cartera).
- **De contado**: se paga en el mismo proceso (requiere caja abierta).

No se factura dos veces el mismo vehículo y período: repetir la corrida no duplica nada.

### Validar la facturación en Excel

Con el permiso de exportar los informes de Transporte (`Transport.Vehicles.Export`):

- **Vista previa › Exportar a Excel**: una fila por servicio de cada vehículo que se facturaría (número interno,
  placa, propietario, servicio, cantidad, precio, si es pactado y valor) y, en otra hoja, los omitidos con su motivo.
  No guarda nada.
- **Corridas anteriores › Exportar**: el resultado de una corrida, una fila por cargo, con:
  - la factura y su **estado DIAN** («No electrónica» para un comprobante);
  - el vehículo, el documento y el nombre del propietario;
  - el servicio, el valor y lo pagado;
  - la ruta y la **obligación de Cartera** con su estado;
  - el estado del cargo (abierto, pagado o anulado) y si hay que revisarlo.

  Es el informe «Facturación de servicios» limitado a la corrida; en Transporte › Informes sale por período.

### Anular la corrida y volver a facturar

**Corridas anteriores › Anular** (permisos `Transport.Billing.Run` y `Inventory.Sales.Void`), en una corrida
terminada. Pide el motivo y confirmación, y anula por tandas cada factura de la corrida que siga **abierta y sin
pagos** —sin abonos ni aviso al sistema externo—:

- una **factura electrónica** con su **nota crédito de anulación total**, que va a la DIAN y, si fue a crédito, ajusta la
  obligación en Cartera;
- un **comprobante no electrónico** con su documento contrario.

Sus cargos quedan anulados en el mismo momento y el período se puede volver a facturar con «Facturar el período»,
sin otro bloqueo: lo que quedó vivo no se cobra dos veces. Al terminar dice cuántas anuló y cuáles no, con el motivo
(ya pagada, con abonos, ya avisada, o lo que Comercial respondió, como un tipo de nota que falta o una aprobación
pendiente). Una que falló no se vuelve a intentar en la misma anulación; se puede volver a anular la corrida.

**Una nota crédito hecha en Comercial** sobre una factura de servicios también llega a Transporte:

- la **total** anula el cargo con la misma regla de la anulación: sin pago ni aviso queda anulado; con pago o aviso,
  queda para revisar;
- una **parcial** sólo baja el valor del cargo en proporción a lo que queda de su línea; si lo asignado ya lo cubre,
  queda pagado;
- notas parciales que suman toda la línea cuentan como la total.

El valor se recalcula siempre desde la factura y sus notas confirmadas, así que repetir el mensaje no cambia nada.
Una factura acreditada del todo deja de ser «la factura del período» y la facturación emite otra.

## 7a. Vender un servicio en el POS o en la factura de oficina

Los servicios también se venden sueltos en Comercial, con el precio que se digite en el momento (producto
de **precio digitado en la venta**). Al agregar a la venta un producto que es un servicio de Transporte
activo, la línea pide **placa o lateral**:

- en el **POS**, el campo sale debajo de las líneas y el foco va a él;
- en la **factura de oficina**, es la columna «Placa o lateral».

Se puede escribir la placa (con o sin guion), el **lateral** (el código radio-móvil, que es el número que
recibe el sistema externo) o el número interno. Al salir del campo se muestra el vehículo y su
propietario, y la línea guarda la placa. **Sin placa o lateral la venta no se cobra ni se confirma.**

**Vehículos que no son de la cooperativa** (desde el 2026-11-25): los servicios también se venden a
clientes externos. Si lo digitado no es de ningún vehículo del parque y es un **número de 1 a 10 dígitos**,
se toma como el **lateral de un vehículo externo**, y desde el 2026-11-26 la línea **pide además su placa**:

- en el **POS** aparece al lado el campo «Placa del vehículo externo» y el foco va a él;
- en la **factura de oficina** aparece debajo del lateral, en la misma columna.

**Sin la placa el externo no se cobra ni se confirma** («falta placa del vehículo externo»). La placa se
escribe con o sin guion, de 5 a 10 letras o números; si es la placa de un vehículo del parque se rechaza
(ese vehículo se vende por su placa, no como externo). La línea muestra «WDK637 · lateral 618 · fuera del
parque» y guarda `618/WDK637`, que es lo que sale en la tirilla y en la factura. El cargo nace sin vehículo y
**el aviso al sistema externo sale con el lateral y la placa** (el número interno va vacío). Una placa sola
que no está en el parque se sigue rechazando: los externos se identifican por el lateral. El lateral de un
vehículo retirado cuenta como externo.

Los externos vendidos antes del 2026-11-26 no tienen placa: su aviso sale con el lateral también como
placa.

Al confirmar la venta nace un cargo por cada línea de servicio:

- lo pagado **de contado** se reparte entre los servicios por su prioridad; el que queda cubierto queda
  pagado y avisa al sistema externo;
- lo que va **a crédito** sigue la forma de pago: por Cartera, si está activa, o como cuenta por cobrar.
  Si esa forma de pago cubre más que el servicio (otros productos, o varios servicios), el aviso espera a
  que la obligación quede **saldada**;
- el deudor es el cliente de la venta (sin cliente, el propietario del vehículo; un externo sin cliente
  queda sin deudor).

Anular la venta funciona igual que anular una factura de servicios (sección 8).

## 8. Envíos a sistemas externos

**El aviso tiene un interruptor**: Inventario › Parámetros, módulo `TRN`, clave
`Transporte.AvisoAlSistemaExterno`. **Arranca apagado.** Apagado, las ventas y los pagos se registran
igual, pero no sale ningún aviso, y lo pagado mientras tanto **no se avisa nunca**, ni al encenderlo.
Encendido, avisa cada servicio que quede pagado desde ese momento.

Cuando la línea de un servicio queda **saldada** —por un pago de Cartera, por un recibo de caja que paga
la factura o de contado al facturar—, el conector del servicio avisa al sistema externo por el valor de
la línea. Los pagos parciales esperan.

**Transporte › Envíos a sistemas externos** muestra cada aviso (pendiente, enviado, con aviso,
reintentando, en espera, revisar, pago anulado, cerrado a mano), sus intentos con el código y un extracto
de la respuesta, y permite **reintentar** o **cerrar a mano** con motivo. Los envíos de vehículos externos
salen como «Externo, WDK637 · lateral 618» (los anteriores al 2026-11-26, como «Externo, lateral 618»).

**Cómo ver el registro de un envío** (el «log»), con el permiso `Transport.Notifications.View`:

1. Transporte › **Envíos a sistemas externos**; filtrar por estado y fechas. Cada fila dice la última
   respuesta (código HTTP y extracto).
2. **Detalle** abre el envío: el identificador que recibió el sistema externo y **cada intento** —el
   inicio de sesión y cada llamada— con fecha y hora, código, resultado, duración, la dirección llamada,
   el cuerpo enviado (sin credenciales) y el extracto de la respuesta.
3. Para guardarlo o pasarlo, Transporte › **Informes**, vista de envíos, exporta a Excel o PDF el rango de
   fechas (sin cuerpos ni datos personales).

- Si un pago ya avisado **se anula o se reversa**, la anulación procede, el envío queda «pago anulado» y
  llega una alerta: **el sistema externo no deshace**, hay que revisarlo allá.
- Si ese servicio vuelve a quedar pagado, **no se avisa de nuevo**: queda marcado para revisar.

## 9. Informes

**Transporte › Informes** (y el Centro de Reportes): vehículos, historia, vencimientos, conductores,
facturación y envíos, en pantalla, Excel o PDF. Exportar exige `Transport.Vehicles.Export` y queda en la
auditoría. El informe de envíos no lleva cuerpos ni datos personales.
