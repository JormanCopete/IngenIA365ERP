# Libro auxiliar

Pantalla `/contabilidad/libro-auxiliar`. Permiso `Accounting.Reports.View`; exportar a Excel, PDF o Word pide `Accounting.Reports.Export`.

## Dos formas de verlo

- **Árbol del plan** (por defecto). Se baja con un clic, nivel por nivel: clase › grupo › cuenta › subcuenta › auxiliar › tercero ›
  documento cruce › comprobante › línea. Cada fila muestra saldo inicial, débitos, créditos y saldo final.
- **Sólo cuentas de movimiento** (casilla en los filtros, 2026-11-15). Lista sólo las auxiliares, sin clases ni grupos. Un clic en una
  abre sus **movimientos**:
  - primero la fila «Saldo inicial», el saldo con que la cuenta llega a la fecha inicial;
  - luego cada línea en orden de fecha y comprobante, con su saldo corrido;
  - al final, la fila «Saldo final».

  La casilla queda guardada para el usuario: la próxima vez el libro abre como lo dejó.

## El filtro de cuenta

Una cuenta sola en «Cuenta desde», sin «hasta», es **esa cuenta y lo que cuelga de ella**: «1105» trae 110505, 11050501…, y nada
de pasivo ni de patrimonio. Hasta el 2026-11-15 traía todo lo que venía después de 1105. Con «desde» y «hasta» es un rango, como
siempre. La regla aplica a todos los informes contables.

## Columnas de los movimientos

El botón **Columnas** deja elegir qué se ve en los movimientos:

- fecha
- tercero
- documento del tercero
- centro de costo
- documento cruce
- descripción
- sucursal

Comprobante, débito, crédito y saldo van siempre. La elección queda en las preferencias del usuario, en cualquier equipo, y es la
misma que sale al exportar. Sin elegir nada: fecha, tercero, centro de costo, documento cruce y descripción.

## Ver un comprobante

Un clic en un movimiento abre el comprobante en una ventana, con todas sus líneas, sin salir del libro. «Abrir en su pantalla» lleva a
`/contabilidad/comprobantes/{id}`.

## Técnico

- El nodo `entries:<código>` de `GET /api/reports/accounting/ledger` devuelve los movimientos.
- `onlyMovement=true` cambia la raíz por la lista plana de auxiliares.
- `columns=` recibe los códigos de `Domain/Accounting/ColumnasDelLibroAuxiliar`, separados por coma.
- Las preferencias del usuario son `contabilidad.libro-auxiliar.columnas` y `contabilidad.libro-auxiliar.solo-movimiento`.
- La pantalla repite el catálogo de columnas. Lo vigila `LasColumnasDelLibroAuxiliarSonLasMismas`.
