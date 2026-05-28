-- ============================================================================
-- IngenIA365ERP — Seed: COR_Countries
-- ============================================================================
--
-- Carga el catalogo de paises con foco en Colombia + principales del continente
-- y socios comerciales. Ordenado por relevancia para el dominio (cooperativas
-- colombianas).
--
-- Es idempotente: usa MERGE para no duplicar si ya existen, y solo inserta
-- los faltantes. Tambien preserva PublicId si la fila ya existe.
--
-- Como ejecutar:
--   sqlcmd -S <server> -d <database> -i 01_Seed_Countries.sql
--   o desde SSMS abrir y F5
-- ============================================================================

SET NOCOUNT ON;
GO

PRINT N'Seeding COR_Countries...';

-- Tabla temporal con los paises a insertar
DECLARE @Countries TABLE (Name NVARCHAR(100));

INSERT INTO @Countries (Name) VALUES
    -- Colombia primero
    (N'Colombia'),
    -- Vecinos
    (N'Venezuela'),
    (N'Ecuador'),
    (N'Peru'),
    (N'Brasil'),
    (N'Panama'),
    -- Resto de Sudamerica
    (N'Argentina'),
    (N'Bolivia'),
    (N'Chile'),
    (N'Paraguay'),
    (N'Uruguay'),
    (N'Guyana'),
    (N'Surinam'),
    -- Centroamerica y Caribe
    (N'Mexico'),
    (N'Costa Rica'),
    (N'Cuba'),
    (N'El Salvador'),
    (N'Guatemala'),
    (N'Haiti'),
    (N'Honduras'),
    (N'Nicaragua'),
    (N'Republica Dominicana'),
    (N'Puerto Rico'),
    (N'Jamaica'),
    -- Norteamerica
    (N'Estados Unidos'),
    (N'Canada'),
    -- Europa relevante
    (N'Espana'),
    (N'Alemania'),
    (N'Belgica'),
    (N'Francia'),
    (N'Holanda'),
    (N'Italia'),
    (N'Portugal'),
    (N'Reino Unido'),
    (N'Suecia'),
    (N'Suiza'),
    -- Asia
    (N'China'),
    (N'Japon'),
    (N'India'),
    (N'Corea del Sur'),
    -- Oceania
    (N'Australia'),
    (N'Nueva Zelanda');

-- Insertar solo los que NO existen (preservando los PublicIds que ya tenga la BD)
INSERT INTO [dbo].[COR_Countries] ([Name], [CreatedAt], [CreatedBy], [IsDeleted])
SELECT
    src.Name,
    SYSUTCDATETIME(),
    N'SEED',
    0
FROM @Countries src
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[COR_Countries] tgt
    WHERE tgt.Name = src.Name AND tgt.IsDeleted = 0
);

DECLARE @Inserted INT = @@ROWCOUNT;
DECLARE @Total INT = (SELECT COUNT(*) FROM [dbo].[COR_Countries] WHERE IsDeleted = 0);

PRINT N'  Paises insertados: ' + CAST(@Inserted AS NVARCHAR(10));
PRINT N'  Total paises activos en BD: ' + CAST(@Total AS NVARCHAR(10));
GO
