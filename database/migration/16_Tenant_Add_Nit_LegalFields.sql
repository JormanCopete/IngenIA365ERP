-- ============================================================================
-- IngenIA365ERP — Migración 16: ADM_Tenants — NIT y campos legales
-- ============================================================================
--
-- Contexto (T034 / Fase 0 — Cimientos técnicos):
--   La cooperativa-tenant debe declarar su NIT, razón social, dirección
--   legal y régimen tributario. Estos campos llegan a los reportes
--   SARLAFT y a la portada del PDF firmado del audit log (T089). Mientras
--   los valores se backfillean por los administradores, los campos son
--   nullable; el índice único de NIT se aplica solo cuando NIT no es nulo.
--
-- Idempotencia: `IF NOT EXISTS` sobre `sys.columns` y `sys.indexes`.
--
-- Cómo ejecutar:
--   sqlcmd -S <server> -d <database> -i 16_Tenant_Add_Nit_LegalFields.sql
-- ============================================================================

SET NOCOUNT ON;
GO

PRINT N'================================================================';
PRINT N'Migración 16: Añadir NIT y campos legales a ADM_Tenants';
PRINT N'================================================================';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ADM_Tenants') AND name = N'Nit')
BEGIN
    ALTER TABLE dbo.ADM_Tenants ADD Nit NVARCHAR(20) NULL;
    PRINT N'  + Nit añadida.';
END
ELSE
    PRINT N'  = Nit ya existe.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ADM_Tenants') AND name = N'LegalName')
BEGIN
    ALTER TABLE dbo.ADM_Tenants ADD LegalName NVARCHAR(200) NULL;
    PRINT N'  + LegalName añadida.';
END
ELSE
    PRINT N'  = LegalName ya existe.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ADM_Tenants') AND name = N'LegalAddress')
BEGIN
    ALTER TABLE dbo.ADM_Tenants ADD LegalAddress NVARCHAR(300) NULL;
    PRINT N'  + LegalAddress añadida.';
END
ELSE
    PRINT N'  = LegalAddress ya existe.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ADM_Tenants') AND name = N'TaxRegime')
BEGIN
    ALTER TABLE dbo.ADM_Tenants ADD TaxRegime NVARCHAR(50) NULL;
    PRINT N'  + TaxRegime añadida.';
END
ELSE
    PRINT N'  = TaxRegime ya existe.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_ADM_Tenants_Nit' AND object_id = OBJECT_ID(N'dbo.ADM_Tenants'))
BEGIN
    CREATE UNIQUE INDEX UX_ADM_Tenants_Nit
        ON dbo.ADM_Tenants (Nit)
        WHERE Nit IS NOT NULL AND IsDeleted = 0;
    PRINT N'  + UX_ADM_Tenants_Nit creado.';
END
GO

PRINT N'16_Tenant_Add_Nit_LegalFields: OK';
GO
