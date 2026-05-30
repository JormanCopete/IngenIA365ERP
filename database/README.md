# database/

Scripts SQL del proyecto. Convención **dos bases de datos en SQL Server**.

## Bases de datos

| Database | Cadena en `appsettings*.json` | Qué vive ahí |
|----------|--------------------------------|--------------|
| `IngenIA365ERP` | `ConnectionStrings:DefaultConnection` (alias `SqlServer`) | Operación de cada cooperativa: `SEC_*`, `COR_*`, `ACC_*`, `LND_*`, `NOM_*`, `INV_*`, `CDT_*`, `DEB_*`, `TRS_*`, `AUD_*`, `WEB_*`, `COM_Attachments`, `NOT_Notifications`. |
| `IngenIA365ERP_Admin` | `ConnectionStrings:TenantConnection` | Multi-tenant global: `ADM_Tenants`, `ADM_Branches`, `ADM_Subscriptions`, `ADM_TenantSettings`. Su DbContext es `TenantDbContext`. |

> **Regla**: el prefijo de la tabla decide la BD destino.
> - `ADM_*` → `IngenIA365ERP_Admin`
> - resto (`SEC_*`, `COR_*`, …) → `IngenIA365ERP`
>
> Cada script en `schema/` y `migration/` declara explícitamente su BD destino en el header con `▸ DATABASE TARGET:`.

## Estructura

```text
database/
├── schema/        # DDL base — crea tablas/índices nuevos. Numerado 01..13X.
├── migration/     # Cambios sobre tablas existentes (ADD COLUMN, ALTER, backfills).
│                  # Numerado a partir de 14.
├── seed/          # Datos semilla idempotentes (catálogos, usuarios iniciales).
└── DBDefinicion.sql  # Legado SOLIDO (funciones T-SQL antiguas).
```

## Cómo ejecutar

Cada script lleva en el header el comando `sqlcmd` exacto. Resumen:

```powershell
# IngenIA365ERP — operacional
sqlcmd -S <server> -d IngenIA365ERP        -i schema/IngenIA365ERP_Schema_07_SEC_AUD_WEB_ADM.sql
sqlcmd -S <server> -d IngenIA365ERP        -i schema/13d_Security_Mfa_Password_Policy.sql
sqlcmd -S <server> -d IngenIA365ERP        -i migration/14_RowVersion_For_Optimistic_Concurrency.sql
sqlcmd -S <server> -d IngenIA365ERP        -i migration/17_User_Extra_Flags.sql
sqlcmd -S <server> -d IngenIA365ERP        -i migration/18_SEC_Users_BackfillColumns.sql

# IngenIA365ERP_Admin — multi-tenant
sqlcmd -S <server> -d IngenIA365ERP_Admin  -i schema/13e_Admin_Branches.sql
sqlcmd -S <server> -d IngenIA365ERP_Admin  -i migration/16_Tenant_Add_Nit_LegalFields.sql
```

Todos los scripts son **idempotentes**: usan `IF NOT EXISTS` contra `sys.tables` / `sys.columns` / `sys.indexes`. Se pueden ejecutar N veces sin efecto secundario.

## Orden de aplicación recomendado para una BD nueva

### `IngenIA365ERP`

1. `schema/IngenIA365ERP_Schema_01_COR.sql` … `12_SeedData.sql` (DDL base completo).
2. `schema/13d_Security_Mfa_Password_Policy.sql` (Fase 0 US1 — MFA + password policy).
3. `schema/13f_Attachments.sql` *(pendiente, Fase 0 US5)*.
4. `schema/13g_Notifications.sql` *(pendiente, Fase 0 US6)*.
5. `schema/13h_HabeasData.sql` *(pendiente, Fase 0 US7)*.
6. `migration/14_RowVersion_For_Optimistic_Concurrency.sql` (RowVersion masivo).
7. `migration/17_User_Extra_Flags.sql` (Fase 0 US1 — User + RefreshToken).
8. `migration/18_SEC_Users_BackfillColumns.sql` (solo si el SEC_Users del entorno está desfasado).
9. `migration/19_SEC_Roles_UserRoles_BackfillColumns.sql` (solo si SEC_Roles / SEC_UserRoles están desfasadas — fix del JOIN al cargar `User.Roles`).
10. `migration/20_SEC_LoginAttempts_BackfillColumns.sql` (solo si SEC_LoginAttempts está desfasada — añade `PublicId`, `WasSuccessful`, audit cols, `RowVersion`).
11. `migration/21_Roles_Scope_BuiltIn.sql` (Fase 0 US2 — Role.Code/TenantId/IsBuiltIn/IsAssignable).
12. `migration/22_SEC_Permissions_Resource_Action_Plus_Roles_TenantNullable.sql` (Fase 0 US2 — añade `Resource`/`Action` a SEC_Permissions y hace SEC_Roles.TenantId nullable; necesario para el seed de built-ins).
13. `migration/23_SEC_Roles_TenantId_Nullable.sql` (complemento de 22 — drop/alter/recreate del default constraint + índice único cuando 22 no pudo).
14. `migration/24_SEC_Roles_TenantId_Convert_To_Int.sql` (intento de orden de operaciones — superseded por 25).
15. `migration/25_SEC_Roles_TenantId_Convert_To_Int_v2.sql` (orden correcto + `SET XACT_ABORT ON` + transacción: allow NULL → sanear → convertir tipo → recrear índice. Esta es la que se debe correr cuando `TenantId` viene como NVARCHAR con valores legacy como 'system').

### `IngenIA365ERP_Admin`

1. `schema/13e_Admin_Branches.sql` (sucursales).
2. `migration/16_Tenant_Add_Nit_LegalFields.sql` (NIT + razón social + régimen).

## MongoDB

Bootstrap del audit log:

```bash
mongosh "mongodb://<host>:27017" database/migration/15_Audit_Mongodb_Bootstrap.json
```

Crea la BD `IngenIA365ERP_Audit`, índices compuestos y TTL de 5 años. Idempotente.
