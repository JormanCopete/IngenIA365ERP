# Data Model: Alta de persona en un paso desde los módulos

**Feature**: 008 | **Date**: 2026-09-13

**Sin columnas ni tablas nuevas.** Esta feature cambia **quién escribe** cada dato, no el
esquema. Lo que toca la base son dos migraciones (§5): una de datos y una que cambia el filtro
de un índice único.

## 1. Persona (`COR_People`, entidad `Person`)

| Grupo | Campos | Quién los escribe |
|---|---|---|
| Identificación | `IdType` (2, default `C`), `TaxId` (20, **único**, `UK_COR_People_TaxId` sin filtro), `TaxIdCheckDigit`, `IdIssuedAt`, `IdIssueDate`, `FirstName` (150), `LastName` (150), `BusinessName` (150), `PersonType` (2) | `CreatePerson`, `UpdatePerson`, compuestos (vía `PersonFactory`) |
| Contacto | `Address` (120), `Phone1` (40), `Phone2`, `Mobile` (30), `Email`, `CityId` (FK; entra como `CityPublicId`) | ídem |
| Demografía | `Gender` (2), `MaritalStatus` (2), `DateOfBirth`, `EducationLevel` (2) | ídem |
| Banderas **simples** | `IsCustomer`, `IsSupplier`, `IsAdvisor`, `IsThirdParty`, `ReceivesInvoice` | `CreatePerson`, `UpdatePerson` (editables en Personas) |
| Banderas **derivadas** | `IsEmployee`, `IsAssociate`, `IsSalesperson` | **sólo** el handler de la fila hija: `RegisterEmployee[WithPerson]`, `TerminateEmployee`, `RegisterAssociate[WithPerson]`, `CreateSalesperson`, `RestorePerson` (recalcula), migración §5 |
| Estado | `Status` | `CreatePerson`, `UpdatePerson` |
| Soft-delete | `IsDeleted`, `DeletedAt`, `DeletedBy` | `DeletePerson` (enciende), `RestorePerson` (apaga) |
| Auditoría | `CreatedAt/By`, `UpdatedAt/By` | todos los handlers que la tocan |

**Regla de derivación** (FR-003, Principio V):

```
IsEmployee    = ∃ PAY_Employees   e : e.PersonId = p.Id ∧ ¬e.IsDeleted ∧ e.Status ≠ -1
IsAssociate   = ∃ COR_Associates  a : a.PersonId = p.Id ∧ ¬a.IsDeleted
IsSalesperson = ∃ INV_Salespeople s : s.PersonId = p.Id ∧ ¬s.IsDeleted
```

**Unicidad** (FR-007, R4): `TaxId` es único **entre todas las filas, eliminadas incluidas**.
`PersonFactory` consulta con `IgnoreQueryFilters()`:

| Encuentra | Resultado |
|---|---|
| nada | crea |
| viva | `Person.TaxIdDuplicate` → `{ code, message: "Ya existe {FullName} con ese documento.", data: { publicId, fullName } }` |
| eliminada | `Person.TaxIdDeleted` → `{ code, message: "Ese documento pertenece a una persona eliminada el {DeletedAt:d}: {FullName}.", data: { publicId, fullName, deletedAt } }` |

## 2. Empleado (`PAY_Employees`, entidad `Employee`) — sin columnas nuevas; índice filtrado

- `PersonId` (FK obligatoria, navegación `Employee.Person`), `PublicId`.
- Laborales: `BaseSalary`/`Salary`, `ContractType` (0..10), `HireDate`, `TerminationDate`,
  `TerminationCause`, `Status` (**1** activo, **-1** retirado; otros valores = inactivo pero vivo),
  `EmployeeType`, `RehireDate` (legado; queda `DateTime.MaxValue`, no se usa — R5).
- Seguridad social: `HealthInsuranceId`, `PensionProviderId`, `WorkRiskProviderId`,
  `WorkRiskRateId` (fila de `PAY_WorkRiskRates`), `SeveranceFundId`, `FamilySubsidyId`.
- Nómina: `PayrollPlanId`, `PayrollBankId`, `PayrollBankAccountNumber`, `PayrollBankAccountType` (0..2).
- Hija: `PAY_SalaryChanges` (navegación `SalaryChange.Employee`) — el registro crea la fila
  inicial en el mismo `SaveChanges`.

**Ciclo de vida** (FR-017):

```
[registro] ──▶ Activo (1) ──terminate──▶ Retirado (-1)  ← inmutable en adelante
                                              │
                                              └── reingreso = NUEVA ficha Activo (1) para la misma persona
```

- Invariante: **a lo sumo una ficha viva** (`Status ≠ -1`) por persona. Lo garantizan
  `Employee.AlreadyExists` en el handler **y** el índice `UK_PAY_Employees_PersonId`, que desde la
  migración `UnaSolaFichaVivaPorPersona` es único sólo entre fichas vivas
  (`[Status] <> -1 AND [IsDeleted] = 0`, traducido a PostgreSQL por `ProviderModelConventions`).
  Antes era único sin filtro —una ficha por persona para siempre— y el reingreso reventaba con 500.
- `GetEmployeeByPersonIdQuery`: `Where(Status != -1).OrderByDescending(HireDate).FirstOrDefault()`.
- `EmployeeDetailDto` **suma** `PersonPublicId`.

## 3. Asociado (`COR_Associates`, entidad `Associate`) — sin cambios de esquema

- `PersonId` (FK obligatoria), `PublicId`; afiliación (`JoinDate`, `ContributionRate`,
  `ContributionPledged`, `CategoryRating`, `AssociateClass`, `PaymentType`, `BranchId`,
  `SectionId`, `CommitteeId`, `EmployerCompanyId`), empleo externo (`ExternalEmployerName`,
  `ExternalEmploymentStartDate`, `ExternalSalary`, `ExternalSalaryType`, `ExternalSeverance`,
  `ExternalSeveranceFund`), banca de depósito (`DepositBankId`, `DepositBankAccountNumber`,
  `DepositBankAccountType`), cónyuge laboral (`SpouseEmployer`, `SpouseSalary`, `SpousePosition`,
  `SpouseProfession`), `Status`, `WithdrawalDate`, `RejoinDate`.
- Una fila por persona (`Associate.AlreadyExists`); «ya asociada» → la pantalla abre edición (US2.2).
- Retiro de asociado **no cambia** en esta feature (hoy no apaga `IsAssociate`; queda anotado
  para la feature que lo revise — la regla de derivación mira la fila, no `WithdrawalDate`).

## 4. Permiso (`SEC_Permissions`, `SEC_RolePermissions`) — sin cambios de esquema

Códigos `Recurso.Acción` nuevos en el catálogo de **cada** cooperativa (sembrados al arrancar,
idempotentes):

| Recurso | Acciones | Operator | Auditor / ReadOnly | CompanyAdmin |
|---|---|---|---|---|
| `Core.People` | View, Create, Update, Delete | View, Create, Update | View | todo |
| `Core.Associates` | View, Create, Update | View, Create, Update | View | todo |
| `Payroll.Employees` | View, Create, Update, Terminate | View, Create, Update | View | todo |

- «Restaurar persona» usa `Core.People.Delete` (misma potestad que eliminar; sin código nuevo).
- Roles **personalizados** existentes: reciben los tres `*.View` (FR-010) por
  `ConcederLecturaDeMaestrosATodosLosRolesAsync`; la escritura la asigna el administrador.
- Resolución por petición (`PermisosDeLaPeticion`, caché Redis 30 min por usuario+cooperativa);
  el seeder invalida la caché de la cooperativa al insertar vínculos.
- Exposición al cliente: `MyPermissionsDto(bool IsGlobalMasterAdmin, IReadOnlyList<string> Permissions)`.

## 5. Migraciones (par PostgreSQL / SQL Server)

### 5a. `UnaSolaFichaVivaPorPersona` (índice)

`DropIndex` + `CreateIndex` de `UK_PAY_Employees_PersonId` con filtro de fichas vivas. Reversible:
`Down` vuelve al índice sin filtro (fallaría sólo si ya hubiera dos fichas de una misma persona, y
entonces hay que decidir cuál conservar, no correr `Down` a ciegas).

### 5b. `ReconciliarBanderasDerivadasDePersona` (datos)

Sólo DML, idempotente, sin `DELETE`, `Down` no-op. Seis `UPDATE` (encender/apagar × tres
banderas), cada uno con `WHERE` que sólo toca filas desalineadas y sella `UpdatedAt/UpdatedBy =
'system:migration:008'`:

```sql
-- ejemplo PostgreSQL (esquema dbo, nombres del snapshot)
UPDATE dbo."COR_People" p SET "IsEmployee" = TRUE, "UpdatedAt" = now() at time zone 'utc', "UpdatedBy" = 'system:migration:008'
 WHERE NOT p."IsEmployee" AND EXISTS (SELECT 1 FROM dbo."PAY_Employees" e
        WHERE e."PersonId" = p."Id" AND NOT e."IsDeleted" AND e."Status" <> -1);
UPDATE dbo."COR_People" p SET "IsEmployee" = FALSE, ...
 WHERE p."IsEmployee" AND NOT EXISTS (...);
-- ídem IsAssociate ↔ COR_Associates, IsSalesperson ↔ INV_Salespeople
```

Diagnóstico previo y posterior: [diagnostico-banderas.sql](diagnostico-banderas.sql) (debe dar
0 en las seis filas de desalineación después de aplicar).

## 6. Contratos de entrada (Application)

```csharp
public record PersonInput {                       // SIN IsEmployee / IsAssociate / IsSalesperson
    string IdType = "C"; string TaxId; string? TaxIdCheckDigit; string? IdIssuedAt; DateOnly? IdIssueDate;
    string FirstName; string LastName; string? BusinessName; string? PersonType;
    string? Address; string? Phone1; string? Phone2; string? Mobile; string? Email; Guid? CityPublicId;
    string? Gender; string? MaritalStatus; DateOnly? DateOfBirth; string? EducationLevel;
    bool IsCustomer; bool IsSupplier; bool IsAdvisor; bool IsThirdParty; bool ReceivesInvoice;
    string? Status; }
public record EmployeeInput {                     // los 13 campos laborales de RegisterEmployeeCommand
    decimal BaseSalary; int ContractType; DateTime HireDate;
    Guid? HealthInsurancePublicId; Guid? PensionProviderPublicId; Guid? WorkRiskProviderPublicId; Guid? WorkRiskRatePublicId;
    Guid? SeveranceProviderPublicId; Guid? FamilyCompensationFundPublicId; Guid? PayrollPlanPublicId;
    Guid? PayrollBankPublicId; string? PayrollBankAccountNumber; int PayrollBankAccountType; }
public record AssociateInput { /* los 23 campos de RegisterAssociateCommand salvo PersonPublicId */ }
```

Validadores hermanos (`PersonInputValidator`: `LastName`, `FirstName`, `TaxId`, `IdType`
requeridos; `Email` formato; longitudes; `DateOfBirth` pasada — las reglas que hoy repiten
Create y Update; `EmployeeInputValidator`: `BaseSalary > 0`, `HireDate`, `ContractType 0..10`,
`PayrollBankAccountType 0..2`; `AssociateInputValidator`: `ContributionRate ≥ 0`,
`ExternalSalary ≥ 0`). Los compuestos los reutilizan con `SetValidator`.
