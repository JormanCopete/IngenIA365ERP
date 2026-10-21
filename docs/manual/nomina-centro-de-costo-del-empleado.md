# Centro de costo del empleado en Nómina

Desde el 2026-10-22 a cada empleado se le puede asignar un **centro de costo contable** (el catálogo
`COR_CostCenters`, Maestros › Centros de costo) y el comprobante `NM` de la nómina lo toma. Los
asociados no usan este catálogo: ellos tienen «grupos del asociado» (`docs/manual/grupos-del-asociado.md`).

## Para quien liquida la nómina

1. **Asignarlo.** En Nómina › Empleados, «Editar» (o la pestaña «Datos Laborales» de la ficha) tiene el
   selector **Centro de costo**: se busca escribiendo parte del código o del nombre; la «x» lo deja en
   «Ninguno». El alta de un empleado nuevo (con persona nueva o sobre una persona existente) lo trae igual.
   Pide el mismo permiso que editar al empleado (`Payroll.Employees.Update`; en el alta, `Create`).
2. **Verlo.** El listado de empleados tiene la columna «Centro de costo» y un filtro arriba («Todos los
   centros de costo»).
3. **Qué hace en el comprobante.** Al aprobar una nómina (ordinaria, prima, cesantías, vacaciones o
   definitiva) cada línea del `NM` lleva el centro del empleado **si la cuenta contable de ese concepto
   maneja centro de costo**; en las cuentas que no lo manejan (por ejemplo el pasivo) el contrato lo
   descarta solo, no es un error. Si en Nómina › Conceptos › «Cuentas» hay una fila para ese centro
   (selector «Centro de costo» de la tabla), el concepto de ese empleado usa **esa** cuenta; si no hay fila
   para el centro se usa la fila «Por defecto».
4. **Empleado sin centro.** Es válido: se usan las cuentas por defecto del concepto, como antes. Solo
   falla cuando la cuenta del concepto **exige** centro de costo: la aprobación se detiene con
   `Payroll.Employee.CostCenterRequired` y el mensaje dice **a qué empleados** asignarles uno («El empleado
   Bruno Prueba (doc. 123) no tiene centro de costo y la cuenta contable de uno de sus conceptos lo
   exige…»); la respuesta trae `data.employees[]` con cada empleado, su documento y los conceptos
   afectados. Si el centro asignado se eliminó después del catálogo: `Payroll.Employee.CostCenterDeleted`.
5. **Auditoría.** Asignar, cambiar o quitar el centro deja el evento `Payroll.Employee.CostCenterChanged`
   (módulo Payroll) con el centro anterior y el nuevo.

## Para quien integra con la API

- `POST /api/payroll/employees`, `POST /api/payroll/employees/with-person` (dentro de `employee`) y
  `PUT /api/payroll/employees/{id}` aceptan `costCenterPublicId` (nulo = ninguno). Inexistente o
  eliminado: `Core.CostCenter.NotFound`.
- **Convención de edición.** El `PUT` de la ficha reemplaza casi todos los campos (lo que no viene se
  pone en cero), pero el centro se trata distinto porque cambia las cuentas del comprobante: **nulo =
  no cambia**, y `clearCostCenter: true` lo deja en «Ninguno». Un cliente anterior que no conoce el campo
  no lo borra.
- `GET /api/payroll/employees/{id}` devuelve `costCenterPublicId`, `costCenterCode`, `costCenterName`; el
  listado `GET /api/payroll/employees` los devuelve por fila y filtra con `?CostCenterPublicId=`.

## Para soporte y desarrollo

- **Modelo.** `PAY_Employees.CostCenterRefId` (int nulo, FK `Restrict` a `COR_CostCenters`, índice
  `IX_PAY_Employees_CostCenterRefId`), migración `CentroDeCostoDelEmpleado`. La migración **puebla** la
  columna desde el código heredado: casa `PAY_Employees.CostCenterId` con `COR_CostCenters.LegacyCode` sin
  distinguir mayúsculas ni espacios, solo fichas y centros no borrados (si dos centros comparten código,
  gana el de menor `Id`); los códigos vacíos o que no casan quedan en nulo. Diagnóstico del 2026-10-22: en
  DEV, QA y PDN (cooflopal) ningún empleado tenía código heredado, la población no movió ninguna fila.
- **Columna heredada.** `PAY_Employees.CostCenterId` (texto, 8, requerido) **no se borró**: sigue en el
  modelo como columna heredada, nadie la lee ni la escribe (el alta la deja en `""`). Se retira después
  con una **migración propia con guarda** (verificar que no queden consumidores y que `CostCenterRefId` sea
  la fuente de verdad en los tres ambientes; es destructiva y pide respaldo y segundo revisor, Principio XII).
- **Poster.** `PayrollAccountingPoster` agrupa por (concepto, centro, tercero), busca la cuenta del
  concepto para ese centro con respaldo a la fila por defecto y manda `PostingLine.CostCenterId`; las
  liquidaciones especiales reutilizan la misma armada (`SettlementAccountingPoster` solo traduce algunos
  códigos y deja pasar los de centro de costo).
- **Asignación masiva.** No existe plantilla de importación de empleados; asignar muchos centros hoy es
  empleado por empleado. Pendiente: si se construye esa plantilla, lleva la columna `centroDeCosto` (código).
- **Campos sin uso.** `PayrollAccountingEntry.CostCenterCode`, `PayrollPlanLiquidation.CostCenterCode`,
  `VacationLiquidation.CostCenterCode` (tablas heredadas sin ningún consumidor) y la tabla `ConceptAccount`
  con la pantalla `/nomina/cuentas-concepto` (cuenta por concepto y centro con un texto de 10) **no alimentan
  la contabilización**: la parametrización vigente es `PAY_ConceptDefinitionAccounts` (Nómina › Conceptos ›
  Cuentas). Conviene retirarlos con una migración destructiva aparte, con guarda, junto con la columna heredada.
