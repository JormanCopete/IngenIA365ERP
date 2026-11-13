# Cuenta contable propia de cada entidad de seguridad social

Muchas cooperativas llevan un auxiliar por entidad: el 245005xx de cada EPS, el 245010xx de cada
fondo de pensiones, y así con la ARL, los fondos de cesantías y las cajas. Cada concepto de nómina
admite una sola cuenta crédito, así que esa separación se hace en la entidad, no en el concepto.

## Dónde se configura

En **Nómina › EPS**, **Fondos de pensiones**, **ARL**, **Fondos de cesantías** y **Cajas de
compensación**, el campo **«Cuenta contable de nómina (opcional)»** usa el mismo buscador que las
cuentas de los conceptos. La cuenta tiene que ser:

- auxiliar (de movimiento), activa y habilitada para Nómina;
- si exige tercero, la entidad debe tener su persona vinculada («Persona que la representa como tercero»).

Vacía, la entidad usa la cuenta crédito del concepto, como siempre. En el listado se ve la columna
«Cuenta contable», y si la cuenta deja de servir (se inactiva, se deshabilita para Nómina) el diálogo
lo dice en rojo.

## Qué hace al contabilizar

Al aprobar una nómina (ordinaria o liquidación especial), para cada línea de una entidad con cuenta
propia:

| | Débito | Crédito |
|---|---|---|
| **Aporte del empleador** (salud, pensión, ARL, caja) | gasto del concepto, la entidad como tercero | **cuenta de la entidad**, la entidad como tercero |
| **Deducción de ley del empleado** (salud, pensión, solidaridad) | cuenta débito del concepto (salarios por pagar), el empleado como tercero | **cuenta de la entidad**, la entidad como tercero |
| **Cesantías anuales** (`CESANTIAS`, sólo en esa liquidación) | cuenta débito del concepto | **cuenta del fondo de cesantías**, el fondo como tercero |

La regla en una frase: **la cuenta de la entidad reemplaza la cuenta crédito del concepto**, que es
la del pasivo con la entidad; el débito siempre es el del concepto. Si un valor sale negativo (un
ajuste en contra), los lados se invierten como en cualquier otra línea.

Qué conceptos cuentan como «de una entidad»:

- los aportes del empleador que ya llevaban a la entidad como tercero: `SALUD*` → EPS, `PENSION*` →
  fondo de pensiones, `ARL*`, `CAJA*` y `CESANT*` (salvo los ajustes de provisión);
- las deducciones del empleado `SALUD*` → EPS y `PENSION*` (excepto la voluntaria), `FSP`,
  `FDO_SOLID*` y `FONDO_SOLID*` → fondo de pensiones.

SENA, ICBF, las provisiones (`PROV_*`) y sus ajustes no tienen entidad y no cambian.

Los créditos de las deducciones se agrupan por entidad: una línea por EPS, no una por empleado.

## El aviso en Conceptos

En **Nómina › Conceptos › Cuentas contables**, si el concepto es de una entidad y alguna tiene cuenta
propia, el diálogo lo avisa y lista cuáles: para esas entidades la cuenta crédito de la tabla no se
usa. Las que no tienen cuenta propia siguen con la de la tabla, así que el concepto necesita igual
su fila de cuentas.

## Si algo falla al aprobar

- «La cuenta X del concepto Y exige tercero y la EPS Z no tiene persona vinculada»: vincule la
  persona en la pantalla de la entidad.
- La cuenta de la entidad pasa por las mismas reglas que cualquier otra (activa, habilitada para
  Nómina, centro de costo si lo exige): el error dice cuál.
