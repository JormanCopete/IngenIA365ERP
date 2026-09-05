# Permisos — Nómina (Feature 005)

Convención del catálogo `SEC_Permissions`: `Code = "{Resource}.{Action}"`,
sembrado por un seeder hermano de `DomainPermissionCatalogSeeder`
(`PayrollPermissionCatalogSeeder`, idempotente: sólo inserta lo que falta). Se
exigen con `.RequirePermission("…")`; sin permiso, **404 indistinguible**.

Los permisos legados de `IdentitySeedData` (`Payroll.Employees.*`,
`Payroll.PayrollPeriods.*`, `Payroll.PayrollReports.*`) se conservan; los nuevos
no los reemplazan ni los duplican.

## Catálogo nuevo

| Resource | Action | Descripción |
|---|---|---|
| `Payroll.Plans` | `View` | Ver planes de nómina |
| `Payroll.Plans` | `Manage` | Crear, editar, desactivar planes; cambiar el plan de un empleado |
| `Payroll.Novelties` | `View` | Ver novedades, historial de salarios y recurrentes |
| `Payroll.Novelties` | `Create` | Registrar novedades, cambios de salario y recurrentes |
| `Payroll.Novelties` | `Update` | Corregir una novedad (crea versión) |
| `Payroll.Novelties` | `Cancel` | Anular una novedad; desactivar una recurrente |
| `Payroll.Novelties` | `Import` | Descargar plantilla y cargar archivo de novedades |
| `Payroll.Runs` | `View` | Ver borradores, detalle por empleado, comparativo, cuadre |
| `Payroll.Runs` | `Calculate` | Calcular o recalcular el período |
| `Payroll.Runs` | `Approve` | Aprobar la liquidación (genera el asiento) |
| `Payroll.Runs` | `AuthorizeException` | Autorizar excepciones a los bloqueos (neto negativo, tope de deducción…) |
| `Payroll.Runs` | `Reverse` | Reversar un período aprobado |
| `Payroll.Runs` | `Export` | Exportar el detalle con explicaciones |
| `Payroll.Payments` | `View` | Ver la relación de pago |
| `Payroll.Payments` | `Mark` | Marcar pagado (total o por empleado) |
| `Payroll.Payments` | `Unmark` | Retirar una marca de pago (con motivo) |
| `Payroll.Payslips` | `View` | Ver y descargar comprobantes de pago |
| `Payroll.Payslips` | `Send` | Enviar comprobantes por correo |
| `Payroll.Concepts` | `View` | Ver definiciones, versiones, catálogo heredado; probar en seco |
| `Payroll.Concepts` | `Manage` | Crear, revisar, desactivar conceptos; cuentas; reaplicar semilla |
| `Payroll.LegalParameters` | `View` | Ver parámetros legales y sus vigencias |
| `Payroll.LegalParameters` | `Manage` | Registrar vigencias nuevas |

## Roles integrados (asignación en la semilla de roles de la cooperativa)

| Rol | Permisos de nómina |
|---|---|
| Administrador de Cooperativa | Todos |
| Operador | `Payroll.Novelties.*` (sin `Import` si la cooperativa lo restringe), `Payroll.Runs.View`, `Payroll.Runs.Calculate`, `Payroll.Payments.View`, `Payroll.Payslips.View`, `Payroll.Concepts.View`, `Payroll.LegalParameters.View`, `Payroll.Plans.View` |
| Auditor | Todos los `View` + `Payroll.Runs.Export` |
| Solo Lectura | Todos los `View` |

## Segregación de funciones (FR-020, FR-021)

- `ApprovePayrollRunCommand` rechaza con `Payroll.SegregationOfDuties` si quien
  aprueba es quien registró o corrigió **cualquier** novedad vigente del período o
  quien calculó la corrida, salvo que `Payroll.AllowSameUserApproval = true`; en ese
  caso exige `confirm: true` por segunda vez (`Payroll.ConfirmationRequired`) y marca
  la corrida `ApprovedWithoutSegregation`.
- Autorizar una excepción exige `Payroll.Runs.AuthorizeException` **además** de
  `Payroll.Runs.Approve`; la autorización queda en `ExceptionsJson` con usuario y
  motivo, y en auditoría.
- Reversar exige `Payroll.Runs.Reverse`, que ningún rol integrado distinto del
  Administrador de Cooperativa tiene.

## Auditoría (Principio X)

Todos los comandos pasan por `AuditBehavior` (MediatR). Además, los comandos con
efecto contable o sobre datos de personas (`Approve`, `Reverse`, `MarkPayments`,
`RevertPaymentMark`, `SendPayslips`, `RegisterSalaryChange`, `SetEmployeeWithholding`)
escriben también un evento explícito con `IAuditAppendOnlyWriter` que incluye
`EntityPublicId` y valores antes/después, porque el behavior genérico sólo guarda el
comando tal cual.
