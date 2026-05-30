-- ============================================================================
-- IngenIA365ERP — Schema 13e: ADM_Branches (Sucursales)
-- ============================================================================
--
-- Contexto (T033 / Fase 0 — Cimientos técnicos):
--   Cada cooperativa-tenant (`ADM_Tenants`) tiene N sucursales con código
--   único dentro del tenant. Exactamente una sucursal es la matriz
--   (`IsHeadquarters = 1`). El claim JWT `branch_id` se emite a partir de la
--   sucursal por defecto del usuario en la cooperativa activa.
--
-- Idempotencia:
--   Todo `CREATE TABLE` y `CREATE INDEX` se envuelve en `IF NOT EXISTS`.
--   El script puede ejecutarse N veces sin fallar.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d <database> -i 13e_Admin_Branches.sql
-- ============================================================================

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ADM_Branches')
BEGIN
    CREATE TABLE dbo.ADM_Branches (
        Id              INT IDENTITY(1,1) NOT NULL,
        PublicId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        TenantId        INT NOT NULL,
        Code            NVARCHAR(20)  NOT NULL,
        Name            NVARCHAR(200) NOT NULL,
        Address         NVARCHAR(300) NULL,
        Phone           NVARCHAR(30)  NULL,
        Email           NVARCHAR(200) NULL,
        IsHeadquarters  BIT NOT NULL DEFAULT 0,
        IsActive        BIT NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(100) NOT NULL DEFAULT N'SYSTEM',
        UpdatedAt       DATETIME2 NULL,
        UpdatedBy       NVARCHAR(100) NULL,
        IsDeleted       BIT NOT NULL DEFAULT 0,
        DeletedAt       DATETIME2 NULL,
        DeletedBy       NVARCHAR(100) NULL,
        RowVersion      ROWVERSION NOT NULL,
        CONSTRAINT PK_ADM_Branches PRIMARY KEY (Id),
        CONSTRAINT UK_ADM_Branches_PublicId UNIQUE (PublicId),
        CONSTRAINT FK_ADM_Branches_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.ADM_Tenants(Id)
    );
    PRINT N'  + ADM_Branches creada.';
END
ELSE
    PRINT N'  = ADM_Branches ya existe.';
GO

-- Unique compuesto (TenantId, Code). Filtrado por filas vivas para soportar
-- el escenario de reactivación con el mismo código tras un soft-delete.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_ADM_Branches_Tenant_Code' AND object_id = OBJECT_ID(N'dbo.ADM_Branches'))
BEGIN
    CREATE UNIQUE INDEX UX_ADM_Branches_Tenant_Code
        ON dbo.ADM_Branches (TenantId, Code)
        WHERE IsDeleted = 0;
    PRINT N'  + UX_ADM_Branches_Tenant_Code creado.';
END
GO

-- Solo una sede principal por tenant entre las vivas.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_ADM_Branches_Tenant_Headquarters' AND object_id = OBJECT_ID(N'dbo.ADM_Branches'))
BEGIN
    CREATE UNIQUE INDEX UX_ADM_Branches_Tenant_Headquarters
        ON dbo.ADM_Branches (TenantId)
        WHERE IsHeadquarters = 1 AND IsDeleted = 0;
    PRINT N'  + UX_ADM_Branches_Tenant_Headquarters creado.';
END
GO

PRINT N'13e_Admin_Branches: OK';
GO
