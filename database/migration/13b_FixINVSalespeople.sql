-- ============================================================================
-- IngenIA365ERP — Migracion 13b: Termina la conversion de INV_Salespeople
-- ============================================================================
--
-- La migracion 13 fallo en la seccion 5 porque las columnas IdNumber, Name, etc.
-- tenian UNIQUE constraints que el helper no dropeaba. Este script:
--   1. Drop unique/check constraints dependientes.
--   2. Drop columns duplicadas con Person.
--   3. Add PersonId + FK + index.
--
-- Es idempotente: usa IF EXISTS / NOT EXISTS.
-- ============================================================================

SET NOCOUNT ON;
GO

PRINT N'Migracion 13b: Terminando INV_Salespeople...';
GO

-- ---------------------------------------------------------------------------
-- 1. Drop unique constraint sobre IdNumber (si existe)
-- ---------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'UK_INV_Salespeople_IdNumber'
             AND object_id = OBJECT_ID('dbo.INV_Salespeople'))
BEGIN
    PRINT N'  Dropeando UK_INV_Salespeople_IdNumber...';
    ALTER TABLE [dbo].[INV_Salespeople] DROP CONSTRAINT [UK_INV_Salespeople_IdNumber];
END;

-- Por si quedo como indice no-constraint
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'UK_INV_Salespeople_IdNumber'
             AND object_id = OBJECT_ID('dbo.INV_Salespeople'))
BEGIN
    DROP INDEX [UK_INV_Salespeople_IdNumber] ON [dbo].[INV_Salespeople];
END;
GO

-- ---------------------------------------------------------------------------
-- 2. Drop check constraints / defaults sobre las columnas que vamos a borrar
-- ---------------------------------------------------------------------------
DECLARE @ConstraintName SYSNAME, @Sql NVARCHAR(MAX);
DECLARE constraint_cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE c.object_id = OBJECT_ID('dbo.INV_Salespeople')
      AND c.name IN (N'IdNumber', N'Name', N'LastName', N'Address', N'Phone', N'Mobile', N'CityId');

OPEN constraint_cur;
FETCH NEXT FROM constraint_cur INTO @ConstraintName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER TABLE [dbo].[INV_Salespeople] DROP CONSTRAINT [' + @ConstraintName + N']';
    EXEC sp_executesql @Sql;
    PRINT N'    drop CONSTRAINT [' + @ConstraintName + N']';
    FETCH NEXT FROM constraint_cur INTO @ConstraintName;
END;
CLOSE constraint_cur;
DEALLOCATE constraint_cur;
GO

-- ---------------------------------------------------------------------------
-- 3. Drop columns duplicadas con Person
-- ---------------------------------------------------------------------------
IF COL_LENGTH('dbo.INV_Salespeople', 'IdNumber')   IS NOT NULL ALTER TABLE [dbo].[INV_Salespeople] DROP COLUMN [IdNumber];
IF COL_LENGTH('dbo.INV_Salespeople', 'Name')       IS NOT NULL ALTER TABLE [dbo].[INV_Salespeople] DROP COLUMN [Name];
IF COL_LENGTH('dbo.INV_Salespeople', 'LastName')   IS NOT NULL ALTER TABLE [dbo].[INV_Salespeople] DROP COLUMN [LastName];
IF COL_LENGTH('dbo.INV_Salespeople', 'Address')    IS NOT NULL ALTER TABLE [dbo].[INV_Salespeople] DROP COLUMN [Address];
IF COL_LENGTH('dbo.INV_Salespeople', 'Phone')      IS NOT NULL ALTER TABLE [dbo].[INV_Salespeople] DROP COLUMN [Phone];
IF COL_LENGTH('dbo.INV_Salespeople', 'Mobile')     IS NOT NULL ALTER TABLE [dbo].[INV_Salespeople] DROP COLUMN [Mobile];
IF COL_LENGTH('dbo.INV_Salespeople', 'CityId')     IS NOT NULL ALTER TABLE [dbo].[INV_Salespeople] DROP COLUMN [CityId];

PRINT N'  Columnas duplicadas removidas';
GO

-- ---------------------------------------------------------------------------
-- 4. Add PersonId si no existe
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.INV_Salespeople') AND name = 'PersonId')
BEGIN
    ALTER TABLE [dbo].[INV_Salespeople] ADD [PersonId] INT NULL;
    PRINT N'  Columna PersonId agregada';
END;
GO

-- ---------------------------------------------------------------------------
-- 5. FK PersonId -> COR_People.Id
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_INV_Salespeople_COR_People')
   AND EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.INV_Salespeople') AND name = 'PersonId')
BEGIN
    ALTER TABLE [dbo].[INV_Salespeople]
    ADD CONSTRAINT [FK_INV_Salespeople_COR_People]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
    PRINT N'  FK FK_INV_Salespeople_COR_People creada';
END;
GO

-- ---------------------------------------------------------------------------
-- 6. Index sobre PersonId
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_INV_Salespeople_PersonId'
                 AND object_id = OBJECT_ID('dbo.INV_Salespeople'))
   AND EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.INV_Salespeople') AND name = 'PersonId')
BEGIN
    CREATE INDEX [IX_INV_Salespeople_PersonId] ON [dbo].[INV_Salespeople]([PersonId]);
    PRINT N'  Indice IX_INV_Salespeople_PersonId creado';
END;
GO

PRINT N'';
PRINT N'================================================================';
PRINT N'  Migracion 13b completada — INV_Salespeople convertido a hija de COR_People';
PRINT N'================================================================';
GO
