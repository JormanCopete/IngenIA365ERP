# Contratos: Alta de persona en un paso desde los módulos

**Feature**: 008 | **Date**: 2026-09-13

Convenciones vigentes: envelope de error `{ code, message, traceId }` (`ErrorEnvelopeFilter`);
`*.NotFound` → 404, `Validation.*` → 400, resto de fallos de negocio → **422**; sin permiso →
**404 `Generic.NotFound`** indistinguible de ruta inexistente (`PermissionAuthorizationFilter`).
Todo identificador que cruza es `PublicId` (Guid). Nada de lo existente cambia de ruta, verbo ni
forma; lo nuevo es aditivo (R11).

## 1. Permisos por ruta

| Ruta | Permiso | Estado |
|---|---|---|
| `GET /api/core/people` · `/{id}` · `/{id}/detail` · `/search?q=&rol=` · **`/by-document?taxId=`** | `Core.People.View` | existentes + 1 nueva |
| `POST /api/core/people` | `Core.People.Create` | existente |
| `PUT /api/core/people/{id}` | `Core.People.Update` | existente |
| `DELETE /api/core/people/{id}` | `Core.People.Delete` | existente |
| **`POST /api/core/people/{id}/restore`** | `Core.People.Delete` | nueva |
| `GET /api/core/associates` · `/{id}` · `/by-person/{personId}` | `Core.Associates.View` | existentes |
| `POST /api/core/associates` | `Core.Associates.Create` | existente |
| `PUT /api/core/associates/{id}` | `Core.Associates.Update` | existente |
| **`POST /api/core/associates/with-person`** | `Core.Associates.Create` **y** `Core.People.Create` | nueva |
| `GET /api/payroll/employees` · `/{id}` · `/by-person/{personId}` | `Payroll.Employees.View` | existentes |
| `POST /api/payroll/employees` | `Payroll.Employees.Create` | existente |
| `PUT /api/payroll/employees/{id}` | `Payroll.Employees.Update` | existente |
| `POST /api/payroll/employees/{id}/terminate` | `Payroll.Employees.Terminate` | existente |
| **`POST /api/payroll/employees/with-person`** | `Payroll.Employees.Create` **y** `Core.People.Create` | nueva |
| `GET/PUT /api/payroll/employees/{id}/withholding` | `Payroll.Novelties.View/Create` | sin cambio (ya lo tenían) |
| **`GET /api/admin/permissions/mine`** | sesión + cooperativa resuelta (sin código propio) | nueva |

`RequirePermission` encadenado dos veces = **AND**: el compuesto exige poder crear la persona
**y** el rol; con uno solo, 404.

## 2. Comandos y respuestas

### `POST /api/payroll/employees/with-person` → `201 { personPublicId, employeePublicId }`

```jsonc
{
  "person": {                       // PersonInput — SIN isEmployee/isAssociate/isSalesperson
    "idType": "C", "taxId": "1023456789", "taxIdCheckDigit": null, "idIssuedAt": "Cali", "idIssueDate": "2010-05-01",
    "firstName": "Ana", "lastName": "Pérez", "businessName": null, "personType": "N",
    "address": "Cra 1 # 2-3", "phone1": null, "phone2": null, "mobile": "3001234567", "email": "ana@x.co", "cityPublicId": "…",
    "gender": "F", "maritalStatus": "S", "dateOfBirth": "1990-01-01", "educationLevel": "U",
    "isCustomer": false, "isSupplier": false, "isAdvisor": false, "isThirdParty": false, "receivesInvoice": false,
    "status": "A"
  },
  "employee": {                     // EmployeeInput — RegisterEmployeeCommand sin personPublicId
    "baseSalary": 2500000, "contractType": 1, "hireDate": "2026-09-15",
    "healthInsurancePublicId": "…", "pensionProviderPublicId": "…", "workRiskProviderPublicId": "…", "workRiskRatePublicId": "…",
    "severanceProviderPublicId": "…", "familyCompensationFundPublicId": "…", "payrollPlanPublicId": "…",
    "payrollBankPublicId": "…", "payrollBankAccountNumber": "123", "payrollBankAccountType": 1
  }
}
```

Errores (422 salvo indicación): `Validation.Invalid` (400, reglas de ambos validadores),
`Person.TaxIdDuplicate`, `Person.TaxIdDeleted`, `Person.CityNotFound`, `Payroll.PlanNotFound`,
`Payroll.WorkRiskRateNotFound`. **Nada persiste** si falla cualquiera (un `SaveChanges`).
Auditoría: un evento `RegisterEmployeeWithPersonCommand` con el request completo.

### `POST /api/core/associates/with-person` → `201 { personPublicId, associatePublicId }`

`{ "person": PersonInput, "associate": AssociateInput }` — `AssociateInput` = los 23 campos de
`RegisterAssociateCommand` salvo `personPublicId` (`joinDate`, `contributionRate`,
`employerCompanyPublicId`, `branchPublicId`, `sectionPublicId`, `committeePublicId`,
`categoryRating`, `associateClass`, `paymentType`, `contributionPledged`, `externalEmployerName`,
`externalEmploymentStartDate`, `externalSalary`, `externalSalaryType`, `externalSeverance`,
`externalSeveranceFund`, `depositBankPublicId`, `depositBankAccountNumber`,
`depositBankAccountType`, `spouseEmployer`, `spouseSalary`, `spousePosition`, `spouseProfession`).
Errores: `Validation.Invalid`, `Person.TaxIdDuplicate`, `Person.TaxIdDeleted`, `Person.CityNotFound`.

### `POST /api/core/people` (existente) → `201 publicId` · `PUT /api/core/people/{id}` → `204`

Cuerpo = `PersonInput` plano (compatible con `Personas.razor` y `NominaE2E`). Las propiedades
`isEmployee`, `isAssociate`, `isSalesperson`, si vienen, **se ignoran** (nunca 400). El `PUT`
deja de escribir las derivadas. Errores: `Person.TaxIdDuplicate`, `Person.TaxIdDeleted`,
`Person.CityNotFound`, `Person.NotFound` (404).

### `POST /api/core/people/{id}/restore` → `204`

Sin cuerpo. Reactiva la **misma** fila (`IsDeleted=false`, limpia `DeletedAt/DeletedBy`),
recalcula `IsEmployee/IsAssociate/IsSalesperson` desde las tablas hijas, sella `UpdatedAt/By`.
Errores: `Person.NotFound` (404: no existe **ni eliminada**), `Person.NotDeleted` (422: estaba
viva). Auditoría: evento `RestorePersonCommand`.

### `GET /api/core/people/by-document?taxId=` → `200 PersonByDocumentDto` | `404`

El documento va en **query string, no en la ruta**: `UseSerilogRequestLogging` registra `RequestPath`
y no la query, así que —igual que `/search?q=` hoy— el número no queda en el log de peticiones.
Busca por documento **incluyendo eliminadas** (`IgnoreQueryFilters`). Es lo que el diálogo
consulta para armar «Usar esa persona» / «Restaurar persona» después de un 422, y lo que el
picker consulta cuando la búsqueda no trae resultados y el término es numérico.

```jsonc
{ "publicId": "…", "fullName": "Ana Pérez", "taxId": "1023456789", "idType": "C",
  "isDeleted": true, "deletedAt": "2026-03-02T15:04:05Z",
  "isEmployee": false, "isAssociate": true, "isSalesperson": false, "status": "A" }
```

### `GET /api/core/people/search?q=&rol=` → `200 PersonSearchDto[]`

`rol` opcional ∈ `associate | employee | salesperson | customer | supplier | advisor | thirdparty`
(filtra por la bandera correspondiente). `PersonSearchDto` **suma** `isSalesperson`,
`isCustomer`, `isSupplier`, `isAdvisor`, `isThirdParty`, `receivesInvoice` (conserva `publicId`,
`identificationNumber`, `fullName`, `isAssociate`, `isEmployee`, `cityName`, `status`). Nunca
devuelve eliminadas.

### `GET /api/payroll/employees/by-person/{personId}` → `200 EmployeeDetailDto` | `404`

Devuelve **sólo la ficha viva** (`status != -1`; la de `hireDate` más reciente si hubiera más de
una por datos legados). `EmployeeDetailDto` **suma** `personPublicId`. Con la persona retirada
y sin ficha viva → 404 (la pantalla pasa a modo registro = reingreso con ficha nueva).

### `GET /api/admin/permissions/mine` → `200 MyPermissionsDto`

```jsonc
{ "isGlobalMasterAdmin": false, "permissions": ["Core.People.View", "Core.People.Create", "Payroll.Employees.View", "…"] }
```

Permisos **efectivos** del usuario en la **cooperativa activa** (unión de sus roles, resueltos
por `PermisosDeLaPeticion`, misma caché que usa el filtro). El maestro global recibe
`isGlobalMasterAdmin: true` y `permissions: []` (el cliente trata «maestro» como «todo»). Sin
cooperativa resuelta → **401 `Session.TenantNotSelected`** (lo da `TenantResolutionMiddleware`; la
ruta `/api/admin/permissions` no es exenta). El cliente sólo llama con cooperativa activa; para
el maestro sin cooperativa no consulta y trata «maestro» como «todo».

## 3. Mensajes que el cliente traduce

| Respuesta | Pantalla |
|---|---|
| 404 `Generic.NotFound` en POST/PUT/DELETE | «No tenés permiso para esta acción o el registro ya no existe.» (`ShowErrorAsync`) |
| 422 `Person.TaxIdDuplicate` | mensaje del servidor + botón «Usar esa persona» (consulta `/by-document`) |
| 422 `Person.TaxIdDeleted` | mensaje del servidor + «Restaurar persona» (si `Core.People.Delete`; llama `/restore` y sigue como existente) o «Pedile la restauración a quien administra Personas» |
| 422 `Employee.AlreadyExists` | la pantalla abre edición de la ficha viva (`/by-person`) |
| 422 `Associate.AlreadyExists` | ídem con `/api/core/associates/by-person` |

## 4. Códigos de permiso sembrados (catálogo por cooperativa)

`Core.People.View`, `Core.People.Create`, `Core.People.Update`, `Core.People.Delete`,
`Core.Associates.View`, `Core.Associates.Create`, `Core.Associates.Update`,
`Payroll.Employees.View`, `Payroll.Employees.Create`, `Payroll.Employees.Update`,
`Payroll.Employees.Terminate`. Reparto por rol en [data-model.md §4](../data-model.md).
