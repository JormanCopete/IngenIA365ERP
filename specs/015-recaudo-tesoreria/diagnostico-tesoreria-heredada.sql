-- ============================================================================
-- Diagnóstico previo al retiro de la Tesorería anterior (feature 015, fase 46, T0469)
-- Contexto : antes de aplicar la migración destructiva RetiroDeLaTesoreriaHeredada
--            hay que saber, base por base, cuántas filas tienen las tres tablas
--            heredadas (cheques, conceptos y facturas) y cuáles siguen pendientes
--            (cheques girados sin cobrar, facturas por pagar), y cuáles de esos
--            pendientes ya se cargaron a la Tesorería nueva con la plantilla
--            tesoreria.pendientes-heredados (TES_LegacyPendingLoads).
-- Modo     : SÓLO LECTURA. Ejecutar en la base de cada cooperativa, en local, DEV,
--            QA y producción, y anotar el resultado en la cabecera de la migración
--            y en docs/operaciones/tesoreria-retiro-heredado.md, con el respaldo.
-- Motores  : la parte 1 es PostgreSQL; la parte 2, SQL Server. Correr la que toque.
-- Estados  : en los cheques, «E» = girado sin cobrar (C cobrado, A anulado,
--            D devuelto); en las facturas, «P» = pendiente de pago. Una fila dada de
--            baja (IsDeleted) cuenta en el total: la guarda de la migración también
--            la cuenta, y la exportación la archiva.
-- La referencia de un pendiente cargado es el Id de la fila heredada (la columna
-- «Id» de la exportación), como texto.
-- ============================================================================

-- ============================================================================
-- Parte 1 — PostgreSQL
-- ============================================================================

-- 1.1 Filas por tabla (todas, incluidas las dadas de baja).
SELECT 'TRS_Checks' AS tabla, COUNT(*) AS filas FROM dbo."TRS_Checks"
UNION ALL SELECT 'TRS_Concepts', COUNT(*) FROM dbo."TRS_Concepts"
UNION ALL SELECT 'TRS_Invoices', COUNT(*) FROM dbo."TRS_Invoices"
ORDER BY tabla;

-- 1.2 Pendientes y cuántos faltan por cargar.
SELECT 'Cheques sin cobrar' AS pendiente,
       COUNT(*) AS filas,
       COALESCE(SUM(c."Amount"), 0) AS valor,
       COUNT(*) FILTER (WHERE l."Id" IS NULL) AS sin_cargar,
       COALESCE(SUM(c."Amount") FILTER (WHERE l."Id" IS NULL), 0) AS valor_sin_cargar
FROM dbo."TRS_Checks" c
LEFT JOIN dbo."TES_LegacyPendingLoads" l
       ON l."Kind" = 1 AND l."IsDeleted" = false AND l."LegacyReference" = c."Id"::text
WHERE c."IsDeleted" = false AND c."Status" = 'E'
UNION ALL
SELECT 'Facturas por pagar',
       COUNT(*),
       COALESCE(SUM(f."Amount"), 0),
       COUNT(*) FILTER (WHERE l."Id" IS NULL),
       COALESCE(SUM(f."Amount") FILTER (WHERE l."Id" IS NULL), 0)
FROM dbo."TRS_Invoices" f
LEFT JOIN dbo."TES_LegacyPendingLoads" l
       ON l."Kind" = 2 AND l."IsDeleted" = false AND l."LegacyReference" = f."Id"::text
WHERE f."IsDeleted" = false AND f."Status" = 'P';

-- 1.3 El detalle de los pendientes que faltan por cargar (lo que va a la plantilla).
SELECT 'ChequeSinCobrar' AS clase, c."Id" AS referencia, c."BankId" AS banco_heredado, c."CheckNumber" AS numero,
       p."TaxId" AS beneficiario, c."Amount" AS valor, c."CheckDate" AS fecha, NULL AS factura
FROM dbo."TRS_Checks" c
LEFT JOIN dbo."COR_People" p ON p."Id" = c."PersonId"
WHERE c."IsDeleted" = false AND c."Status" = 'E'
  AND NOT EXISTS (SELECT 1 FROM dbo."TES_LegacyPendingLoads" l
                  WHERE l."Kind" = 1 AND l."IsDeleted" = false AND l."LegacyReference" = c."Id"::text)
UNION ALL
SELECT 'FacturaPorPagar', f."Id", NULL, NULL, p."TaxId", f."Amount", COALESCE(f."DueDate", f."InvoiceDate"), f."InvoiceNumber"
FROM dbo."TRS_Invoices" f
LEFT JOIN dbo."COR_People" p ON p."Id" = f."PersonId"
WHERE f."IsDeleted" = false AND f."Status" = 'P'
  AND NOT EXISTS (SELECT 1 FROM dbo."TES_LegacyPendingLoads" l
                  WHERE l."Kind" = 2 AND l."IsDeleted" = false AND l."LegacyReference" = f."Id"::text)
ORDER BY clase, referencia;

-- 1.4 ¿Ya está la aprobación del dueño en esta base?
SELECT "SettingKey", "SettingValue", "CreatedAt", "CreatedBy"
FROM dbo."COR_SystemSettings"
WHERE "SettingKey" = 'TES.RetiroHeredado.Aprobado' AND "IsDeleted" = false;

-- ============================================================================
-- Parte 2 — SQL Server
-- ============================================================================

-- 2.1 Filas por tabla.
SELECT 'TRS_Checks' AS tabla, COUNT(*) AS filas FROM [dbo].[TRS_Checks]
UNION ALL SELECT 'TRS_Concepts', COUNT(*) FROM [dbo].[TRS_Concepts]
UNION ALL SELECT 'TRS_Invoices', COUNT(*) FROM [dbo].[TRS_Invoices]
ORDER BY tabla;

-- 2.2 Pendientes y cuántos faltan por cargar.
SELECT 'Cheques sin cobrar' AS pendiente,
       COUNT(*) AS filas,
       COALESCE(SUM(c.[Amount]), 0) AS valor,
       SUM(CASE WHEN l.[Id] IS NULL THEN 1 ELSE 0 END) AS sin_cargar,
       COALESCE(SUM(CASE WHEN l.[Id] IS NULL THEN c.[Amount] END), 0) AS valor_sin_cargar
FROM [dbo].[TRS_Checks] c
LEFT JOIN [dbo].[TES_LegacyPendingLoads] l
       ON l.[Kind] = 1 AND l.[IsDeleted] = 0 AND l.[LegacyReference] = CAST(c.[Id] AS nvarchar(60))
WHERE c.[IsDeleted] = 0 AND c.[Status] = 'E'
UNION ALL
SELECT 'Facturas por pagar',
       COUNT(*),
       COALESCE(SUM(f.[Amount]), 0),
       SUM(CASE WHEN l.[Id] IS NULL THEN 1 ELSE 0 END),
       COALESCE(SUM(CASE WHEN l.[Id] IS NULL THEN f.[Amount] END), 0)
FROM [dbo].[TRS_Invoices] f
LEFT JOIN [dbo].[TES_LegacyPendingLoads] l
       ON l.[Kind] = 2 AND l.[IsDeleted] = 0 AND l.[LegacyReference] = CAST(f.[Id] AS nvarchar(60))
WHERE f.[IsDeleted] = 0 AND f.[Status] = 'P';

-- 2.3 El detalle de los pendientes que faltan por cargar.
SELECT 'ChequeSinCobrar' AS clase, c.[Id] AS referencia, c.[BankId] AS banco_heredado, c.[CheckNumber] AS numero,
       p.[TaxId] AS beneficiario, c.[Amount] AS valor, c.[CheckDate] AS fecha, CAST(NULL AS nvarchar(20)) AS factura
FROM [dbo].[TRS_Checks] c
LEFT JOIN [dbo].[COR_People] p ON p.[Id] = c.[PersonId]
WHERE c.[IsDeleted] = 0 AND c.[Status] = 'E'
  AND NOT EXISTS (SELECT 1 FROM [dbo].[TES_LegacyPendingLoads] l
                  WHERE l.[Kind] = 1 AND l.[IsDeleted] = 0 AND l.[LegacyReference] = CAST(c.[Id] AS nvarchar(60)))
UNION ALL
SELECT 'FacturaPorPagar', f.[Id], NULL, NULL, p.[TaxId], f.[Amount], COALESCE(f.[DueDate], f.[InvoiceDate]), f.[InvoiceNumber]
FROM [dbo].[TRS_Invoices] f
LEFT JOIN [dbo].[COR_People] p ON p.[Id] = f.[PersonId]
WHERE f.[IsDeleted] = 0 AND f.[Status] = 'P'
  AND NOT EXISTS (SELECT 1 FROM [dbo].[TES_LegacyPendingLoads] l
                  WHERE l.[Kind] = 2 AND l.[IsDeleted] = 0 AND l.[LegacyReference] = CAST(f.[Id] AS nvarchar(60)))
ORDER BY clase, referencia;

-- 2.4 ¿Ya está la aprobación del dueño en esta base?
SELECT [SettingKey], [SettingValue], [CreatedAt], [CreatedBy]
FROM [dbo].[COR_SystemSettings]
WHERE [SettingKey] = 'TES.RetiroHeredado.Aprobado' AND [IsDeleted] = 0;
