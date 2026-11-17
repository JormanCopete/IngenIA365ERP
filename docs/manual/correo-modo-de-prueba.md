# Modo de prueba del correo

Para hacer facturas, compras, cotizaciones y demás pruebas en una cooperativa en producción sin que los correos les lleguen a los
clientes, proveedores, asociados o usuarios reales (pedido del dueño del 2026-11-17).

## Los parámetros

Se configuran en **Inventario › Parámetros**, módulo **CORREO**, como cualquier parámetro: una vigencia nueva desde una fecha, con
motivo.

| Clave | Valores | Qué hace |
|---|---|---|
| `Correo.EnvioATerceros` | `Enviar` (defecto), `Suspender`, `Redirigir` | Enviar: normal. Suspender: no sale ningún correo. Redirigir: todos van al buzón de prueba. |
| `Correo.BuzonDePrueba` | una o varias direcciones, separadas por `,` o `;` | Adonde llegan los correos en Redirigir. Vacío con Redirigir = no sale nada (nunca al destinatario real). |
| `Correo.SuspendidosQuedanPendientes` | `true` / `false` (defecto) | Sólo con Suspender: pendientes para reenviarlos después o descartados (queda la constancia). |

## Qué alcanza

Todo correo que la aplicación manda **dentro de la cooperativa**: facturas electrónicas y notas al comprador, ventas, cotizaciones,
órdenes de compra, recibos de caja, comprobantes de pago, extractos, certificados, desprendibles de nómina y los avisos de las alertas.

**No alcanza** (siempre llegan a la persona real):

- los correos que salen sin cooperativa elegida: entrar, recuperar la clave, el segundo factor;
- las invitaciones de usuarios nuevos, la aceptación de la invitación, el cambio de correo y el aviso de cambio de clave.

**Tampoco detiene la DIAN**: una factura de prueba se transmite y valida de verdad. El modo de prueba es sólo del correo.

## Cómo se ve

- **Redirigir**: asunto con `[PRUEBA]` y una franja al comienzo que dice a quién habría llegado. El adjunto va igual.
- Mientras el modo no sea Enviar, **todas las pantallas** muestran una franja amarilla con el modo y un enlace a los retenidos.
- **Administración › Correos retenidos** (`/admin/correos-retenidos`, permiso `Core.HeldEmails.View`): cada correo suspendido o
  desviado, para quién era, el asunto y qué pasó.

## Reenviar los pendientes

1. Poner `Correo.EnvioATerceros` en `Enviar`.
2. En Correos retenidos, filtro «Pendientes de reenviar», marcar los que deben salir (o ninguno = todos) y **Reenviar**
   (permiso `Core.HeldEmails.Manage`). Los que no deben salir, **Descartar**.

Reenviar con el modo de prueba encendido responde `Correo.ModoDePruebaActivo`. Al reenviar o descartar se borran el cuerpo y los
adjuntos guardados; queda la constancia.

## Para el código

- El control vive en **un solo sitio**, `CorreoSegunModoDeEnvio` (Application/Common/Notifications), que envuelve al SMTP en
  `Storage/DependencyInjection`. Todo `IEmailSender` pasa por él.
- Un correo sin el que una persona no puede entrar al sistema lleva `EmailMessage.SiempreAlDestinatario = true`.
- El reenvío usa `IRemitenteDirecto` (el SMTP directo).
- Tablas `COR_HeldEmails` y `COR_HeldEmailAttachments` (migración aditiva `CorreosRetenidos`).
