-- ============================================================================================================
-- IngenIA365ERP — feature 012, T315 (FR-091; quickstart §3.5; contracts/plantillas.md §15)
-- Extracción de las CIFRAS DE SOLIDO a una fecha de corte, con las columnas de la plantilla 15:
--     fecha | bodega | producto | cantidad | valor | grupoContable
-- El resultado se pega en la hoja «Datos» de la plantilla (Inventario › Cifras de SOLIDO › Plantilla vacía) o se
-- exporta a .csv. Los códigos van TAL COMO VIENEN de SOLIDO: el ERP los resuelve al importar y los que no existan en
-- el catálogo nuevo quedan «sin producto en el catálogo» con su grupo contable.
--
-- ESTADO: PROPUESTA PENDIENTE DE CONFIRMAR POR EL DUEÑO (tarea T315 en tasks.md). Lo que falta confirmar:
--   (1) qué tablas y campos de SOLIDO dan la existencia por bodega a una fecha. Este guion la reconstruye desde los
--       movimientos (inv_movtos: IdProducto, idbodega, FecMovto, Cantidad, IdTipoMovto) con el signo de cada tipo de
--       movimiento, que se declara abajo en @SignoDeTipo porque SOLIDO no lo guarda en un campo conocido por este
--       equipo (inv_tipomovtos);
--   (2) el valor: se toma cantidad × costo promedio del producto (inv_productos.CostoPro). CostoPro es el costo de HOY,
--       no el de la fecha de corte; si SOLIDO guarda el costo por movimiento (inv_movtos.Costo) y el dueño prefiere el
--       valor acumulado de los movimientos, se cambia el bloque «valor» por la suma de Cantidad × Costo con signo;
--   (3) la traducción de los grupos de SOLIDO (inv_grupos.idgrupo del producto) a los grupos contables nuevos, que se
--       declara en @TraduccionDeGrupos. Un grupo sin traducción sale vacío y la fila, si el producto tampoco existe en
--       el catálogo nuevo, la rechaza la importación (Import.Cell.Required en grupoContable).
-- Se corre en SQL Server contra la base de SOLIDO (esquema [dbo]); no escribe nada.
-- ============================================================================================================

SET NOCOUNT ON;

DECLARE @Corte DATE = '2026-11-30';        -- la víspera de la activación de las bodegas que se comparan

-- (1) Signo de cada tipo de movimiento de SOLIDO: +1 entra, -1 sale, 0 no mueve existencias. A CONFIRMAR POR EL DUEÑO.
DECLARE @SignoDeTipo TABLE (IdTipoMovto INT PRIMARY KEY, Signo SMALLINT NOT NULL);
-- INSERT INTO @SignoDeTipo (IdTipoMovto, Signo) VALUES (1, 1), (2, -1), ...;

-- (3) Traducción de grupos de producto de SOLIDO a códigos de grupo contable del ERP. A CONFIRMAR POR EL DUEÑO.
DECLARE @TraduccionDeGrupos TABLE (IdGruProducto INT PRIMARY KEY, GrupoContable NVARCHAR(10) NOT NULL);
-- INSERT INTO @TraduccionDeGrupos (IdGruProducto, GrupoContable) VALUES (1, N'ABARROTES'), (2, N'AGRO'), ...;

IF NOT EXISTS (SELECT 1 FROM @SignoDeTipo)
BEGIN
    RAISERROR(N'Declare el signo de cada tipo de movimiento de SOLIDO en @SignoDeTipo antes de correr el guion (T315, dueño).', 16, 1);
    RETURN;
END;

WITH Existencia AS (
    SELECT
        RTRIM(CAST(m.idbodega AS NVARCHAR(20)))    AS Bodega,
        RTRIM(CAST(m.IdProducto AS NVARCHAR(40)))  AS Producto,
        SUM(CAST(m.Cantidad AS DECIMAL(18, 4)) * t.Signo) AS Cantidad
    FROM dbo.inv_movtos m
    JOIN @SignoDeTipo t ON t.IdTipoMovto = m.IdTipoMovto
    WHERE CAST(m.FecMovto AS DATE) <= @Corte
      AND m.idbodega IS NOT NULL
    GROUP BY m.idbodega, m.IdProducto
)
SELECT
    CONVERT(CHAR(10), @Corte, 23)                                        AS fecha,
    e.Bodega                                                              AS bodega,
    e.Producto                                                            AS producto,
    e.Cantidad                                                            AS cantidad,
    -- (2) valor: cantidad × costo promedio de hoy en SOLIDO (ver la nota de arriba).
    CAST(ROUND(e.Cantidad * ISNULL(p.CostoPro, 0), 2) AS DECIMAL(18, 2)) AS valor,
    g.GrupoContable                                                       AS grupoContable
FROM Existencia e
LEFT JOIN dbo.inv_productos p ON RTRIM(CAST(p.IdProducto AS NVARCHAR(40))) = e.Producto
LEFT JOIN @TraduccionDeGrupos g ON g.IdGruProducto = p.IdGruProducto
WHERE e.Cantidad <> 0
ORDER BY e.Bodega, e.Producto;
