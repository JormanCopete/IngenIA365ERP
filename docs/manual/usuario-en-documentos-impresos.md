# Cómo sale el usuario en los documentos impresos

Pedido del dueño del 2026-11-15. Los documentos guardan quién los hizo con el **correo** de la sesión, y ese correo salía en el papel.
Ahora cada cooperativa elige cómo sale.

## Dónde se configura

En **Administración › Usuarios**, el recuadro «Cómo sale el usuario en los documentos impresos». Es el parámetro del sistema
`Core.PrintedUserDisplay`, que también se ve en Parámetros del sistema. Lo ve quien tiene `Security.Parameters.View` y lo cambia
quien tiene `Security.Parameters.Update`.

| Opción | Qué sale | Ejemplo |
|---|---|---|
| Nombre (por defecto) | primer nombre y primer apellido | Ana Pérez |
| Nombre completo | nombres y apellidos | Ana María Pérez Gómez |
| Iniciales | las iniciales de nombres y apellidos | AMPG |
| Cargo | el cargo del usuario en la cooperativa; sin cargo, el nombre | Tesorera |
| Sobrenombre | el sobrenombre que el administrador le puso a cada usuario; sin sobrenombre, el nombre | AMP · Caja 1 |
| Correo | lo de antes | ana@coop.co |

El nombre sale de la **persona** vinculada al usuario y el cargo del **cargo** del usuario. El **sobrenombre** se escribe en
Administración › Usuarios › (el usuario) › «En los documentos impresos»; admite hasta 60 caracteres. Un usuario sin persona vinculada sigue
saliendo con su correo. Lo que no es un usuario de la cooperativa (un nombre escrito a mano, un proceso) queda como está.

## Qué documentos

- Comprobante contable: registró y contabilizó.
- Recibo de caja: cajero.
- Anulación de recibo: pidió y aprobó.
- Egreso de caja: cajero.
- Cierre de sesión, cierre del día y actas de arqueo de Recaudo: cajero, quien arqueó, testigo y quien cerró.
- Arqueo y comprobante de movimiento de caja del POS: cajero, quien cerró y quien aprobó.
- Tirilla del POS: cajero.
- Cotización y orden de compra: elaboró.
- Informe de conciliación bancaria: quien la cerró.
- Liquidación definitiva de nómina: aprobó.

Las pantallas no cambian: siguen mostrando el usuario como antes.

## Lo que falta

- **Representación gráfica de la factura electrónica.** El nombre del cajero viaja en el documento que va a la DIAN; cambiarlo es
  otra decisión y no se tocó.

## Técnico

- Un solo sitio hace la traducción: `Application/Common/Impresos/UsuarioEnImpresos`. Cada constructor de un documento impreso le pide
  un formateador o el texto de un usuario por su Id.
- El valor por defecto lo siembra `SystemParametersSeeder`.
- El sobrenombre es `SEC_Users.PrintAlias`, de la migración `SobrenombreYPagoAgrupado`. Se escribe con `PUT /api/admin/users/{id}/print-alias`
  (`Security.Users.Update`).
- Lo vigilan `UsuarioEnImpresosTests` y `LosModosDelUsuarioEnImpresosSonLosMismos`.
