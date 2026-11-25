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

La **política de facturación** (con vigencia) dice si a mitad de mes se cobra el mes completo o la
fracción, el tipo de documento de venta, la bodega o punto de venta y el día de vencimiento.

## 7. Facturación mensual

**Transporte › Facturación de servicios**.

- **Vista previa** del período: qué vehículos se facturan, cuáles se omiten y por qué.
- **Individual**: un vehículo, un período.
- **Masiva**: todos los vehículos activos con suscripciones vigentes. La pantalla avanza la corrida por
  tandas; si se cierra, se retoma donde iba.

Cada vehículo recibe **una factura de Comercial** con una línea por servicio, a nombre del propietario.

- **A crédito**: cada servicio queda como una obligación de Cartera (o una cuenta por cobrar si no hay
  Cartera).
- **De contado**: se paga en el mismo proceso (requiere caja abierta).

No se factura dos veces el mismo vehículo y período: repetir la corrida no duplica nada.

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
se toma como el **lateral de un vehículo externo**: la línea muestra «lateral 618 · fuera del parque» y
guarda ese número. El cargo nace sin vehículo y **el aviso al sistema externo sale con ese lateral** (la
placa y el número interno van vacíos). Una placa que no está en el parque se rechaza: los externos se
identifican sólo por el lateral. El lateral de un vehículo retirado cuenta como externo.

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
salen como «Externo, lateral …».

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
