# Grupos del asociado

Los **grupos del asociado** son la clasificación libre que cada cooperativa usa para agrupar a sus asociados: zona, empresa,
gremio, agencia… No son centros de costo, no llevan cuentas ni datos de nómina y no dependen de la pagaduría.

## Dónde
- Catálogo: **Maestros › Grupos del asociado** (`/maestros/grupos-del-asociado`). Ver: permiso `Core.AssociateGroups.View`;
  crear, editar, inactivar y reactivar: `Core.AssociateGroups.Manage`.
- En la ficha del asociado (**Asociados › Registro**): lista «Grupo del asociado» (opcional). El listado de asociados
  (`GET /api/core/associates?AssociateGroupPublicId=`) filtra por grupo y devuelve el grupo de cada fila.

## Reglas
- El **código** es la nomenclatura de la cooperativa: letras, dígitos, punto, guion y guion bajo, sin espacios, en
  mayúsculas, hasta 10 caracteres, único entre los grupos vigentes. No cambia una vez creado. La pantalla avisa al salir del
  campo si ya existe (`GET /api/catalogos/grupos-del-asociado/codigo/{codigo}`).
- El nombre (hasta 80) y la descripción (hasta 200) sí se editan.
- Un grupo **no se borra**: se inactiva con motivo. Uno que tienen asociados vivos no se inactiva
  (`AssociateGroup.InUse`): cámbielos de grupo antes. Un grupo inactivo no se puede asignar a un asociado; el que ya lo tenía
  lo conserva.
- Toda escritura lleva `Idempotency-Key`; sin el permiso la API responde 404.

## Qué pasó con el centro de costo del asociado
`COR_Associates.CostCenterId` existía desde la migración del sistema anterior pero **ningún comando lo escribía** y ningún
informe lo leía. Se retiró (migración `RetiroDelCentroDeCostoDelAsociado`, destructiva con guarda: se niega si alguna fila
tiene valor). El catálogo `COR_CostCenters` queda **sólo como centro de costo contable** (Contabilidad, Inventario y
Comercial, matriz de Cartera, presupuesto y nómina por cuentas de concepto). Sus campos de empresa y nómina
(`CompanyName`, `CompanyTaxId`, `PayrollType`, `Period`, `PayrollPeriodicity`, `PayrollStatus`) son **heredados** del
sistema anterior: no se usan para clasificar asociados y no se borran en este cambio.

## Pendiente
- Plantilla de importación masiva de grupos: no existe todavía (el patrón de plantillas vive en `Inventory.Imports`);
  se agrega si la cooperativa necesita cargar muchos grupos.