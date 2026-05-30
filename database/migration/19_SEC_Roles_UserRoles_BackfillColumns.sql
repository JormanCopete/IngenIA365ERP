-- =============================================================================
-- File:        19_SEC_Roles_UserRoles_BackfillColumns.sql
-- Phase:       Fase 0 — Cimientos técnicos · US1 (fix runtime login JOIN)
-- Idempotent:  YES (sys.columns + sys.indexes guards).
--
-- ▸ DATABASE TARGET: IngenIA365ERP  (cadena DefaultConnection / SqlServer)
--
-- Contexto:
--   Al hacer .Include(u => u.Roles) en LoginCommandHandler, EF genera un JOIN
--   a SEC_UserRoles + SEC_Roles. Si esas tablas no tienen las columnas
--   auditables (CreatedAt, IsDeleted, …) ni RowVersion, SQL Server lanza
--   error 207 "Invalid column name".
--
--   Junto con esta migración va el cambio en UserConfiguration que hace que el
--   N:N use la entidad explícita UserRole (FKs UserId/RoleId) en lugar de la
--   junction implícita (RolesId/UsersId).
--
-- Se puede ejecutar tantas veces como haga falta.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d IngenIA365ERP -i 19_SEC_Roles_UserRoles_BackfillColumns.sql
-- =============================================================================

SET NOCOUNT ON;
GO

-----------------------------------------------------------------------------
-- SEC_Roles
-----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_Roles')
BEGIN
    RAISERROR('La tabla SEC_Roles no existe. Crea el schema base antes de correr esta migración.', 16, 1);
    RETURN;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsSystemRole' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD IsSystemRole BIT NOT NULL CONSTRAINT DF_SEC_Roles_IsSystemRole DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsActive' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD IsActive BIT NOT NULL CONSTRAINT DF_SEC_Roles_IsActive DEFAULT 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedAt' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SEC_Roles_CreatedAt DEFAULT SYSUTCDATETIME();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedBy' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_SEC_Roles_CreatedBy DEFAULT N'SYSTEM';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedAt' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD UpdatedAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedBy' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD UpdatedBy NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsDeleted' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SEC_Roles_IsDeleted DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'DeletedAt' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD DeletedAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'DeletedBy' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD DeletedBy NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'RowVersion' AND Object_ID = OBJECT_ID('dbo.SEC_Roles'))
    ALTER TABLE dbo.SEC_Roles ADD RowVersion ROWVERSION NOT NULL;
GO

-----------------------------------------------------------------------------
-- SEC_UserRoles (la junction real del N:N User↔Role)
-----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SEC_UserRoles')
BEGIN
    RAISERROR('La tabla SEC_UserRoles no existe. Crea el schema base antes de correr esta migración.', 16, 1);
    RETURN;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'AssignedAt' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD AssignedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SEC_UserRoles_AssignedAt DEFAULT SYSUTCDATETIME();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'AssignedBy' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD AssignedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_SEC_UserRoles_AssignedBy DEFAULT N'SYSTEM';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedAt' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_SEC_UserRoles_CreatedAt DEFAULT SYSUTCDATETIME();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'CreatedBy' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_SEC_UserRoles_CreatedBy DEFAULT N'SYSTEM';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedAt' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD UpdatedAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'UpdatedBy' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD UpdatedBy NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsDeleted' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SEC_UserRoles_IsDeleted DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'DeletedAt' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD DeletedAt DATETIME2(0) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'DeletedBy' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD DeletedBy NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'PublicId' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD PublicId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SEC_UserRoles_PublicId DEFAULT NEWID();
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'RowVersion' AND Object_ID = OBJECT_ID('dbo.SEC_UserRoles'))
    ALTER TABLE dbo.SEC_UserRoles ADD RowVersion ROWVERSION NOT NULL;
GO

-- Índice único compuesto (UserId, RoleId) si no existe.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SEC_UserRoles_User_Role' AND object_id = OBJECT_ID('dbo.SEC_UserRoles'))
BEGIN
    -- Solo se crea si no hay duplicados existentes (evita fallo en bases con datos legados sucios).
    IF NOT EXISTS (
        SELECT 1 FROM dbo.SEC_UserRoles
        GROUP BY UserId, RoleId
        HAVING COUNT(*) > 1)
    BEGIN
        CREATE UNIQUE INDEX UX_SEC_UserRoles_User_Role ON dbo.SEC_UserRoles(UserId, RoleId);
    END
    ELSE
    BEGIN
        PRINT 'AVISO: SEC_UserRoles contiene duplicados (UserId, RoleId). Limpiar antes de crear el índice único.';
    END
END;
GO

PRINT 'SEC_Roles + SEC_UserRoles backfill: OK';
GO
