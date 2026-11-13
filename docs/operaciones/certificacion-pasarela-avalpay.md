# Canal digital — certificación de la pasarela Avalpay (feature 021, T111)

Lista para recorrer **en el ambiente de pruebas de Avalpay** antes de pedir la salida a producción de un sitio. La hace
el dueño (o quien él designe) con las credenciales de prueba que entregó Avalpay.

**Lo que este documento NO lleva, a propósito**: ninguna credencial, URL de la entidad, login ni llave secreta, y ningún
texto de la guía de certificación de Avalpay (es de uso confidencial). Las credenciales viven sólo en la base del canal,
cifradas, y se digitan en la pantalla del ERP (`/canal/destinos`, «Credenciales del sitio»). Este repositorio es público.

## Antes de empezar

1. El canal corre con `CANAL_PASARELA=placetopay` en el ambiente donde se certifica (DEV o QA, nunca producción).
2. En el ERP de la cooperativa de prueba:
   - el canal está encendido, con código de canal y textos legales publicados (términos, política, preguntas
     frecuentes de la cooperativa y de la pasarela);
   - hay un destino de pago con convenio de recaudo de tipo pasarela, y sus credenciales de prueba quedaron **activas**
     (el canal crea y consulta una sesión de prueba al guardarlas: si quedan «fallidas», el motivo lo dice);
   - en la pasarela, la URL de notificación del sitio es `https://<host del canal>/webhooks/placetopay/<id del sitio>`
     (el id lo muestra la pantalla del destino).
3. Una persona de prueba habilitada, con cuenta del canal activa, correo verificado y conceptos publicados: un crédito
   con valor exigible y un aporte con cuota pendiente.
4. Recaudo activado en la cooperativa de prueba (sin eso el aviso al ERP queda reintentando, lo cual también es una
   prueba válida del punto 13).

## Recorrido

Anotar en cada fila la fecha, el medio usado, la referencia y el resultado. «Dónde mirar» dice cómo comprobarlo sin
abrir la base.

| # | Caso | Qué debe pasar | Dónde mirar | Resultado |
|---|---|---|---|---|
| 1 | PSE aprobado | Resumen con detalle, total en COP y la pasarela; redirige; al volver: «Pago aprobado» con referencia, fecha y hora, valor, autorización/CUS | Pantalla de retorno; historial | pendiente |
| 2 | PSE rechazado | Retorno «Pago rechazado», sin cobro; el producto se puede pagar otra vez | Retorno; lista de conceptos | pendiente |
| 3 | PSE pendiente | Retorno «Pago pendiente» con el contacto de la cooperativa; el producto queda bloqueado con el aviso de pago en proceso (referencia, valor, contacto) | Retorno; «Pagar en línea» | pendiente |
| 4 | Pendiente que se resuelve | Sin volver a entrar, la sonda lo consulta a los 15 min y el historial muestra el estado final | Historial; auditoría `Canal.Pago.EstadoCambiado` con fuente `probe` | pendiente |
| 5 | Tarjeta aprobada | Igual que 1, con franquicia y medio de tarjeta; nada de la tarjeta en el canal ni en el ERP | Retorno; comprobante | pendiente |
| 6 | Tarjeta rechazada | Igual que 2 | Retorno | pendiente |
| 7 | Doble clic en «Pagar» | Un solo intento y una sola sesión en la pasarela | Historial (una sola referencia) | pendiente |
| 8 | Cancelar en la pasarela y volver | Retorno con el estado que diga la pasarela (no el de la URL) | Retorno | pendiente |
| 9 | Sesión vencida | Dejar pasar el vencimiento configurado (10–30 min): «La sesión de pago venció», sin cobro | Retorno; historial | pendiente |
| 10 | Notificación de la pasarela | Llega firmada y converge con el retorno sin duplicar eventos | Auditoría `Canal.Webhook.Recibida` y un solo cambio de estado | pendiente |
| 11 | Historial | Al menos las últimas cinco transacciones, en orden descendente, con fecha y hora, referencia, autorización/CUS, estado y valor COP | `/cooperativa/pagos` | pendiente |
| 12 | Comprobante | En un pago aplicado, «Descargar comprobante» baja el PDF del ERP | Retorno e historial | pendiente |
| 13 | Aviso al ERP | El pago aprobado aparece como recibo «en línea» en Recaudo; con el ERP caído, se reintenta solo y el retorno dice «registrando» | Recaudo → recibos; retorno | pendiente |
| 14 | Términos y política | Antes de pagar, si la versión cambió, se piden aceptar; siempre enlazados desde el resumen y el pie | Resumen; pie de página | pendiente |
| 15 | Preguntas frecuentes | Las de la cooperativa y las de la pasarela, desde el pie de página | `/ayuda/preguntas` | pendiente |
| 16 | Logo de la pasarela | Visible en el resumen antes de redirigir | Resumen | pendiente — hoy se muestra el nombre del destino; si la guía exige la imagen oficial, hay que cargarla |
| 17 | Datos del comprador | Documento, nombres, correo y celular viajan validados; un dato inválido lo dice antes de ir a la pasarela | Resumen (mensaje `Canal.Pago.DatosDelComprador`) | pendiente |
| 18 | Reverso | Desde el ERP antes del corte diario | — | **fase 10**, no se certifica todavía |

## Lo que hay que confirmar con el analista de Avalpay

Quedó abierto en research.md (R11) y el adaptador tiene una suposición en cada caso; anotar la respuesta aquí y, si
contradice la suposición, abrir la corrección:

- **Sesión vencida**: el adaptador la reconoce como `REJECTED` con motivo `EX`, o rechazada sin ningún intento después
  de su vencimiento.
- **Estado `FAILED`**: si existe en la sesión; hoy cualquier estado desconocido se trata como fallido.
- **Documentos extranjeros**: PPT, PEP y pasaporte se envían como `PPN`.
- **Frecuencia de la consulta**: 15 minutos las primeras 24 horas (configurable con `CANAL_FRECUENCIA_PENDIENTES`);
  la guía de producción habla de 24 h.
- **Reverso**: ventana y reglas (`/api/transaction` con `action = reverse`).
- **Impuestos**: hoy no se envían (ningún concepto del canal los trae).

## Resultado

| Fecha | Sitio (últimos 4 del login) | Quién | Casos aprobados | Observaciones |
|---|---|---|---|---|
| | | | | |
