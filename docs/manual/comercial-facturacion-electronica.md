# Comercial: facturación electrónica ante la DIAN

> Manual funcional de la facturación electrónica del módulo Comercial (feature 012, entrega I4): **Administración →
> Facturación electrónica**, **Maestros → Resoluciones DIAN**, **Ventas → Documentos electrónicos** y **Ventas →
> Contingencias DIAN**. Para el administrador que configura, el responsable de facturación que vigila la bandeja y la
> contadora. El índice del módulo está en [comercial.md](comercial.md).

> **Importante: hoy no hay facturación electrónica con validez fiscal.** La cooperativa no tiene todavía un proveedor
> tecnológico contratado y el ERP no tiene instalado ningún canal real. En producción la emisión funciona sobre el
> **canal simulado**: todo documento sale con la leyenda **«SIN VALIDEZ FISCAL»**, **no llega a la DIAN** y su resultado
> (validado, rechazado, en contingencia) lo decide el simulador. Sirve para aprender, parametrizar y probar el circuito
> completo, no para facturar ante la DIAN. Mientras tanto, si la cooperativa está obligada a facturar electrónicamente,
> su facturación con validez fiscal tiene que seguir saliendo por el medio que use hoy.

## Qué hace

Toda **factura**, **nota crédito**, **nota débito**, **documento equivalente POS** (la tirilla de caja) y su nota de
ajuste, y todo **documento soporte** (compras a no obligados) y su nota de ajuste:

1. se numera con una **resolución DIAN vigente** asociada al canal de emisión;
2. lleva su código único (CUFE, CUDE o CUDS) y su código QR;
3. se transmite por el canal configurado y se reintenta solo si el canal o la DIAN no responden;
4. se entrega al comprador (tirilla, PDF o correo) después de validarse;
5. queda en la **bandeja de documentos electrónicos** con su historia: versiones, transmisiones, mensajes y
   correcciones.

Las **contingencias** (falla del facturador o de la DIAN) no detienen la venta: el documento queda pendiente y se
transmite cuando se puede, dentro del plazo de la norma.

## Quién lo hace

| Tarea | Permiso |
|---|---|
| Ver la configuración | ver la configuración (`ElectronicInvoicing.Settings.View`) |
| Registrar una vigencia nueva, verificar la credencial | configurar (`ElectronicInvoicing.Settings.Manage`) |
| Registrar, corregir, retirar y asociar resoluciones | resoluciones (`ElectronicInvoicing.Resolutions.Manage`) |
| Ver la bandeja | ver documentos (`ElectronicInvoicing.Documents.View`) |
| Corregir rechazos (casos a, b y c) | corregir documentos (`ElectronicInvoicing.Documents.Correct`) |
| Pasar un documento pendiente al canal vigente | transmitir por el canal vigente (`ElectronicInvoicing.Documents.TransmitByCurrentChannel`) |
| Anular una venta electrónica, pedir factura sobre un documento equivalente | confirmar ventas (`Inventory.Sales.Confirm`) |

La **credencial** del proveedor tecnológico nunca se escribe en el ERP: la carga soporte en el almacén de secretos del
ambiente y en la pantalla sólo se verifica.

## Paso a paso en pantalla

### 1. La configuración de la emisión

**Administración → Facturación electrónica**.

Arriba, **Preparación para emitir**: dice si la cooperativa está «Lista para emitir electrónicamente», «No obligada:
comprobante no electrónico» o «Bloqueada: falta completar», con una tabla de **qué falta, dónde se corrige y con qué
permiso**, y el consumo y los días por vencer de cada resolución.

Debajo, la **vigencia actual** (modo, canal, ambiente, si la emisión está activa, quién envía el correo al comprador,
software, datos del emisor), la **credencial del canal**, los **canales de la instalación** con lo que cada uno sabe hacer
y el **historial de vigencias**.

Para configurar, «Nueva vigencia»:

- **Modo**: «Proveedor tecnológico» o «Software propio».
- **Canal**: los instalados. Hoy sólo aparece **SIMULADO · Canal simulado (sólo pruebas)**.
- **Ambiente**: «Producción» o «Habilitación (pruebas)». El canal simulado sólo admite «Habilitación (pruebas)»: con
  «Producción» la vigencia no se guarda.
- Software y set de pruebas (sólo en software propio), **correo al comprador** («Lo envía el ERP» o «Lo envía el
  canal»), la fecha **Desde**, «Emisión activa» y los datos del emisor (NIT, dígito de verificación, razón social,
  municipio, correo).
- El motivo.

La vigencia actual se cierra la víspera de la fecha nueva. Lo ya numerado sigue por el canal con que se numeró.

«Verificar credencial» prueba la credencial del canal. El canal simulado no necesita credencial: basta verificar.

### 2. Las resoluciones DIAN

**Maestros → Resoluciones DIAN**, «Nueva resolución»: el tipo (factura electrónica, documento equivalente POS,
documento soporte o contingencia), el número, el prefijo, el rango, la vigencia y, en la de factura, la clave técnica (se
digita una vez y después se ve enmascarada). Ningún canal crea resoluciones: se registran a mano.

Después, «Asociar a canal»: el canal y la fecha desde la cual se numera por él. **Sin asociación no se numera.**

Una resolución se corrige mientras no tenga números emitidos; con números, sólo se **retira** moviendo el fin de su
vigencia hacia atrás.

Con el canal simulado se registran **resoluciones de prueba** (prefijo y rango inventados) y se asocian al canal.

### 3. La bandeja de documentos electrónicos

**Ventas → Documentos electrónicos**: filtros por estado, tipo, contingencia y «los que llevan demasiado sin validar».
Los estados son: pendiente de transmitir, enviado sin respuesta, validado, validado con notificaciones, rechazado, en
contingencia del facturador (03), en contingencia de la DIAN (04) y anulado sin reemplazo.

El detalle muestra las versiones del documento (con sus archivos), cada transmisión con los mensajes traducidos al
español y las correcciones. Las acciones aparecen sólo cuando proceden:

- «Reintentar ahora»: no hace falta esperar el próximo reintento automático.
- «Consultar a la DIAN»: pregunta el estado de lo enviado.
- **Un rechazo se corrige** de una de tres formas:
  - **caso a**, «Corregir la copia fiscal (caso a)»: datos de la contraparte sin cambio económico, y «Corregir y volver a
    transmitir»;
  - **caso b**, «Preparar reemplazo» y «Reemplazar (caso b)»: un documento nuevo con el **mismo número**;
  - **caso c**, «Anular sin reemplazo (caso c)», con motivo.
  Los casos b y c exigen que una consulta confirme el rechazo.
- «Transmitir por el canal vigente», con motivo: ver «El paso al canal real», abajo.

### 4. Las contingencias

**Ventas → Contingencias DIAN**:

- La **03** (falla del facturador) la declara una persona con «Declarar contingencia 03», o la abre sola el canal cuando
  no responde.
- La **04** (falla de la DIAN) sólo la declara el canal.

En ninguna se detiene la venta: los documentos quedan pendientes, con la leyenda de contingencia, y se transmiten
después. «Cerrar la contingencia» fija el plazo de transmisión según el parámetro y la norma vigentes. Las constancias de
la cooperativa se adjuntan al evento y «Exportar la bitácora» la descarga.

### 5. Anular una venta electrónica y «pedir factura»

En el detalle de la venta (Ventas → Documentos de venta):

- «Anular» sobre un documento electrónico emite, en una sola confirmación, la **nota crédito total** (o la nota de ajuste
  del documento equivalente POS) con la devolución. No se anula con un documento contrario como el resto.
- Sobre un **documento equivalente POS** ya expedido, «Pedir factura» emite la nota de ajuste del documento equivalente
  y la factura a nombre del comprador en una sola operación. Se usa cuando el comprador pide la factura después de pagar
  en caja. En el POS, con F4 y «el comprador pide factura», la venta sale como factura desde el principio.

### 6. El documento soporte

Se emite desde **Compras → Documentos soporte** (ver [comercial-compras.md](comercial-compras.md)): se numera con la
resolución de documento soporte, se transmite por el mismo canal y se corrige con su **nota de ajuste**, que siempre
disminuye. Un documento soporte validado no se anula.

## Probar con el canal simulado

Mientras no haya canal real, la cooperativa puede ensayar todo el circuito:

1. «Nueva vigencia» con canal **SIMULADO** y ambiente **«Habilitación (pruebas)»**, «Emisión activa».
2. «Verificar credencial» (no hace falta cargar ninguna).
3. Resoluciones de prueba de factura, documento equivalente POS, documento soporte y contingencia, asociadas al canal.
4. Vender, anular, pedir factura y corregir como se describe arriba.

Lo que hay que saber del simulador:

- **Todo sale con «SIN VALIDEZ FISCAL»** en la tirilla y en el PDF, y **nada llega a la DIAN**. Sus códigos y archivos
  van marcados como simulados.
- **Si el comprador tiene correo, le llega** la representación con esa leyenda. Pruebe con terceros de prueba o avise al
  comprador.
- **El resultado lo decide el último dígito de la identificación del comprador**, no la DIAN:

  | Último dígito | Qué responde el simulador |
  |---|---|
  | 1 | **Rechazado** la primera vez; la corrección (caso a o b) se valida |
  | 2 | Validado **con notificaciones** |
  | 3 | **En proceso**: la primera consulta no lo encuentra y el reenvío se valida |
  | 4 | **La DIAN no está disponible**: el documento queda en contingencia 04 y se valida al transmitirlo |
  | 5 | **El canal no está disponible** por un rato: el documento queda pendiente y se reintenta; si las fallas se repiten, se abre sola la contingencia 03 del facturador |
  | cualquier otro | Validado |

  Por eso, en producción con el simulador, una venta a un comprador cuya cédula o NIT termina en 1, 3, 4 o 5 **se verá
  rechazada, en proceso o en contingencia aunque sus datos estén bien**. No es un error de la cooperativa.

## El paso al canal real

Lo que el sistema ya permite cuando la cooperativa contrate un proveedor tecnológico y soporte instale su canal:

1. Soporte instala el canal del proveedor y carga su credencial en el almacén de secretos del ambiente.
2. El administrador registra una **vigencia nueva** desde una fecha, con el canal real, el ambiente **«Producción»**, el
   modo y los datos del emisor, y la verifica con «Verificar credencial». La vigencia del simulado se cierra la víspera.
3. Se registran las **resoluciones reales** y se asocian al canal real desde esa fecha.
4. Desde esa fecha, lo que se numera sale por el canal real.

Lo que **no** pasa al canal real:

- Cada documento sigue por el canal con que se numeró. Lo **validado** con el simulador queda como está, con su leyenda
  «SIN VALIDEZ FISCAL»: **no se vuelve a transmitir**.
- «Transmitir por el canal vigente» sólo sirve para un documento todavía pendiente, rechazado o en contingencia cuyo
  canal ya se retiró, **del mismo ambiente** y con su resolución asociada al canal vigente. Como el simulado sólo trabaja
  en «Habilitación (pruebas)» y el real irá en «Producción», lo pendiente del simulado no se puede pasar al real.

Hoy el ERP no trae ningún adaptador de proveedor tecnológico ni el servicio central de software propio: esos pasos
quedan para cuando el dueño elija el proveedor.

## Reglas importantes

- **Sin resolución vigente asociada al canal no se numera.** La preparación para emitir lo avisa antes.
- **Las contingencias no detienen la venta**; lo pendiente se transmite después y la bandeja muestra lo que lleva
  demasiado sin validar.
- **La entrega al comprador va después de la validación**; reimprimir marca «COPIA».
- **Un documento validado no se edita ni se anula con un contrario**: se corrige con nota crédito, nota débito o nota de
  ajuste, o se anula con su nota total.
- **El canal simulado nunca en «Producción»**: el sistema no lo admite, porque no tendría validez fiscal.
- **La credencial no vive en el ERP**: sólo se verifica.
- **Cambiar de canal no toca lo emitido**: es una vigencia nueva desde una fecha.

## Preguntas frecuentes

**¿Las facturas que salen hoy sirven ante la DIAN?**
No. Llevan «SIN VALIDEZ FISCAL» y no se transmiten a la DIAN: el canal es el simulado.

**Una factura salió rechazada y los datos del cliente están bien.**
Con el simulador, una identificación que termina en 1 sale rechazada la primera vez. Corríjala con el caso a («Corregir
la copia fiscal», «Corregir y volver a transmitir») y se valida.

**Un documento quedó «en contingencia» y nadie la declaró.**
Con el simulador, una identificación que termina en 4 deja el documento en contingencia de la DIAN, y varias seguidas que
terminan en 5 abren la contingencia del facturador. Se transmite solo después, o con «Reintentar ahora».

**¿Puedo poner el canal simulado en «Producción» para que no salga la leyenda?**
No: el sistema no lo permite, justamente porque esos documentos no tienen validez fiscal.

**El comprador pagó en caja y ahora quiere factura a su nombre.**
En el detalle de la venta, «Pedir factura».

**¿Qué pasa con lo que emitimos con el simulador cuando tengamos el proveedor real?**
Queda como documento de prueba, con su leyenda; no se transmite por el canal real.

**La preparación dice «Bloqueada: falta completar».**
La tabla de abajo dice qué falta, en qué pantalla se corrige y con qué permiso (por ejemplo, una resolución sin asociar
al canal o la credencial sin verificar).

## Lo que todavía no está

- **Proveedor tecnológico real**: ningún adaptador instalado. Elegir y contratar el proveedor (con credenciales a nombre
  de la cooperativa), probar en su ambiente de pruebas y medir los tiempos es del dueño.
- **Software propio** (servicio central): el modo existe en la configuración, pero el canal no está construido.
- **Resoluciones reales de la cooperativa**: prefijos, rangos, software asociado, resolución de contingencia y numeración
  del documento equivalente POS los entrega el dueño.
- **Correo al comprador**: falta definir el buzón y el dominio del correo saliente.
- **Por cotejar con la contadora y el proveedor**: algunos códigos y textos de la norma (el de la contingencia del
  facturador en la representación gráfica, el tratamiento de contingencia del documento equivalente, la numeración de la
  nota de ajuste del documento soporte, el contenido exacto del QR).
- **Eventos RADIAN** distintos del acuse de recibo y el recibo del bien, e Ingenia365 como proveedor tecnológico: fuera
  del alcance.
