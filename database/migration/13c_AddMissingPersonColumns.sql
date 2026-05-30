-- ============================================================================
-- IngenIA365ERP — Migracion 13c: Agregar columnas faltantes a COR_People
-- ============================================================================
--
-- La entity Person.cs declara cuatro columnas de retencion auxiliar que el
-- DDL original NO incluia en COR_People (estaban solo en COR_Companies).
-- Como decidimos (P4) que los datos fiscales viven en COR_People, las
-- agregamos aqui.
--
-- Es idempotente: cada ALTER usa NOT EXISTS.
-- ============================================================================

SET NOCOUNT ON;
GO

PRINT N'Migracion 13c: Agregar columnas WithholdingAux* a COR_People...';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'WithholdingAux')
BEGIN
    ALTER TABLE [dbo].[COR_People]
    ADD [WithholdingAux] BIT NOT NULL CONSTRAINT [DF_COR_People_WithholdingAux] DEFAULT 0;
    PRINT N'  Columna WithholdingAux agregada';
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'WithholdingAuxAmount')
BEGIN
    ALTER TABLE [dbo].[COR_People]
    ADD [WithholdingAuxAmount] DECIMAL(17,2) NULL;
    PRINT N'  Columna WithholdingAuxAmount agregada';
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'WithholdingAuxPct')
BEGIN
    ALTER TABLE [dbo].[COR_People]
    ADD [WithholdingAuxPct] DECIMAL(7,4) NULL;
    PRINT N'  Columna WithholdingAuxPct agregada';
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.COR_People') AND name = 'WithholdingAuxAccount')
BEGIN
    ALTER TABLE [dbo].[COR_People]
    ADD [WithholdingAuxAccount] NVARCHAR(20) NULL;
    PRINT N'  Columna WithholdingAuxAccount agregada';
END;
GO

PRINT N'';
PRINT N'================================================================';
PRINT N'  Migracion 13c completada — COR_People tiene WithholdingAux*';
PRINT N'================================================================';
GO
