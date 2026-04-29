-- ============================================================
-- IngenIA365ERP — Data Migration Script
-- Step 06: INV_ (Inventory) — 24 tables
-- Source: [old].dbo.inv_*
-- Target: [dbo].INV_*
-- ============================================================
-- Prerequisites:
--   01_Create_MigrationSchema.sql (migration schema + Map_* tables)
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT OFF;
GO

PRINT '================================================================';
PRINT '  06_Migrate_INV_Inventory.sql — START  ' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT '================================================================';
GO

-- ============================================================
-- BLOCK 1: INV_Locations (inv_ubicacion)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_Locations (inv_ubicacion)...';

    INSERT INTO [dbo].[INV_Locations] (
        [LocationCode], [Description], [ShortDescription],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        s.idubicacion,
        ISNULL(NULLIF(RTRIM(s.descripcion), ''), N'Sin descripcion'),
        NULLIF(RTRIM(s.resumido), ''),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.inv_ubicacion s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'INV_Locations', (SELECT COUNT(*) FROM [dbo].[INV_Locations]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'INV_Locations', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 2: INV_PrimaryGroups (inv_Grupo_Primario)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_PrimaryGroups (inv_Grupo_Primario)...';

    INSERT INTO [dbo].[INV_PrimaryGroups] ([GroupCode],[Name],[ShortName],[CreatedBy],[CreatedAt])
    SELECT s.idgrupo,ISNULL(NULLIF(RTRIM(s.descripcion),''),N'Sin nombre'),NULLIF(RTRIM(s.resumido),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_Grupo_Primario s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_PrimaryGroups',(SELECT COUNT(*) FROM [dbo].[INV_PrimaryGroups]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_PrimaryGroups',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 3: INV_SecondaryGroups (inv_GrupoSecundario)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_SecondaryGroups (inv_GrupoSecundario)...';

    INSERT INTO [dbo].[INV_SecondaryGroups] ([GroupCode],[Name],[ShortName],[PrimaryGroupId],[CreatedBy],[CreatedAt])
    SELECT s.idgrupo,ISNULL(NULLIF(RTRIM(s.descripcion),''),N'Sin nombre'),NULLIF(RTRIM(s.resumido),''),s.idgrupoprimario,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_GrupoSecundario s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_SecondaryGroups',(SELECT COUNT(*) FROM [dbo].[INV_SecondaryGroups]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_SecondaryGroups',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 4: INV_ProductGroups (inv_grupos)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_ProductGroups (inv_grupos)...';

    INSERT INTO [dbo].[INV_ProductGroups] ([GroupCode],[Name],[ShortName],[SecondaryGroupId],[RestrictsLimit],[MaxSalesQuantity],[CreatedBy],[CreatedAt])
    SELECT s.IdGruProducto,ISNULL(NULLIF(RTRIM(s.Descripcion),''),N'Sin nombre'),NULLIF(RTRIM(s.Resumido),''),s.idgruposecund,ISNULL(s.rstingLimeteVentas,0),ISNULL(s.cantRestringVentas,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_grupos s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_ProductGroups',(SELECT COUNT(*) FROM [dbo].[INV_ProductGroups]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_ProductGroups',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 5: INV_DiscountTypes (inv_tipodstos)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_DiscountTypes (inv_tipodstos)...';

    INSERT INTO [dbo].[INV_DiscountTypes] ([TypeCode],[Name],[ShortName],[DiscountClass],[CreatedBy],[CreatedAt])
    SELECT s.IdTipoDsto,ISNULL(NULLIF(RTRIM(s.Descripcion),''),N'Sin nombre'),NULLIF(RTRIM(s.Resumido),''),s.ClaseDsto,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_tipodstos s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_DiscountTypes',(SELECT COUNT(*) FROM [dbo].[INV_DiscountTypes]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_DiscountTypes',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 6: INV_Products (inv_productos) — populate Map_Products
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_Products (inv_productos) — CRITICAL...';

    INSERT INTO [dbo].[INV_Products] (
        [ProductCode], [Name], [ShortName], [GroupId], [DiscountTypeId],
        [UnitOfMeasure], [CostPrice], [SalePrice], [VatRate],
        [MinStock], [MaxStock], [CurrentStock], [IsActive],
        [Barcode], [OtherTax], [ControlsStock], [RestrictsLimit], [MaxSalesQuantity],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        s.IdProducto,
        ISNULL(NULLIF(RTRIM(s.Descripcion), ''), N'Sin nombre'),
        NULLIF(RTRIM(s.Resumido), ''),
        s.IdGruProducto, s.IdTipoDsto,
        NULLIF(RTRIM(s.Medida), ''),
        ISNULL(s.CostoPro, 0), ISNULL(s.PreVenta, 0), ISNULL(s.TasaIva, 0),
        ISNULL(s.stockminimo, 0), ISNULL(s.stockmaximo, 0),
        ISNULL(s.CantDisp, 0),
        CASE WHEN ISNULL(s.estado, 1) = 1 THEN 1 ELSE 0 END,
        NULLIF(RTRIM(s.CodigoBarras), ''),
        ISNULL(s.otrosimp, 0),
        ISNULL(s.ctrlexistencia, 0),
        ISNULL(s.rstingLimeteVentas, 0),
        ISNULL(s.cantRestringVentas, 0),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.inv_productos s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

    -- Populate Map_Products
    INSERT INTO [migration].[Map_Products] (OldProductCode, NewProductId)
    SELECT [ProductCode], [Id]
    FROM [dbo].[INV_Products];

    PRINT '   Map_Products populated: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;

    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'INV_Products', (SELECT COUNT(*) FROM [dbo].[INV_Products]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'INV_Products', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 7: INV_TransactionTypes (inv_tipomovtos)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_TransactionTypes (inv_tipomovtos)...';

    INSERT INTO [dbo].[INV_TransactionTypes] ([TypeCode],[Description],[ShortDescription],[TransactionVoucherCode],[CostVoucherCode],[SequenceNumber],[ControlsStock],[DocumentClass],[UpdatesAccounting],[PortfolioVoucherCode],[CreditLineId],[DeductionType],[InvoiceControl],[TotalInPurchase],[CostsProducts],[IsReturn],[TransfersAccounting],[OrderPedido_SustainPrice],[AllowsBonus],[ValidatesCreditLimit],[CreatedBy],[CreatedAt])
    SELECT s.IdTipoMovto,ISNULL(NULLIF(RTRIM(s.Descripcion),''),N'Sin nombre'),NULLIF(RTRIM(s.Resumido),''),NULLIF(RTRIM(s.CpteTran),''),NULLIF(RTRIM(s.CpteCost),''),ISNULL(s.Secuencia,0),ISNULL(s.CtrlExistencia,0),NULLIF(RTRIM(s.ClaDoc),''),ISNULL(s.ActualizaContab,0),NULLIF(RTRIM(s.CpteCartera),''),s.Lincred,NULLIF(RTRIM(s.clades),''),NULLIF(RTRIM(s.ctrlfactura),''),ISNULL(s.totalencompra,0),ISNULL(s.Costea,0),ISNULL(s.esdevolucion,0),ISNULL(s.trasladacontab,0),ISNULL(s.ordenpedido_sostieneprecio,0),ISNULL(s.permiteRegalia,0),ISNULL(s.validacupocredito,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_tipomovtos s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_TransactionTypes',(SELECT COUNT(*) FROM [dbo].[INV_TransactionTypes]),N'OK',SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_TransactionTypes',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 8: INV_Shifts (inv_turnos)
-- ============================================================
BEGIN TRY BEGIN TRAN; PRINT '>> INV_Shifts (inv_turnos)...';
    INSERT INTO [dbo].[INV_Shifts] ([ShiftCode],[Name],[StartTime],[EndTime],[CreatedBy],[CreatedAt])
    SELECT s.IdTurno,ISNULL(NULLIF(RTRIM(s.Descripcion),''),N'Sin nombre'),NULLIF(RTRIM(s.HoraInicial),''),NULLIF(RTRIM(s.HoraFinal),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_turnos s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_Shifts',(SELECT COUNT(*) FROM [dbo].[INV_Shifts]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_Shifts',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 9: INV_Warehouses (inv_bodegas)
-- ============================================================
BEGIN TRY BEGIN TRAN; PRINT '>> INV_Warehouses (inv_bodegas)...';
    INSERT INTO [dbo].[INV_Warehouses] ([WarehouseCode],[LocationId],[Description],[ShortDescription],[CreatedBy],[CreatedAt])
    SELECT s.IdBodega,ISNULL(s.idubicacion,0),ISNULL(NULLIF(RTRIM(s.Descripcion),''),N'Sin nombre'),NULLIF(RTRIM(s.Resumido),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_bodegas s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_Warehouses',(SELECT COUNT(*) FROM [dbo].[INV_Warehouses]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_Warehouses',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 10: INV_Salespeople (inv_Vendedor)
-- ============================================================
BEGIN TRY BEGIN TRAN; PRINT '>> INV_Salespeople (inv_Vendedor)...';
    INSERT INTO [dbo].[INV_Salespeople] ([IdNumber],[Name],[LastName],[Address],[Phone],[Mobile],[CityId],[SalespersonType],[AppliesCommission],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.cedula),''),N'0'),ISNULL(NULLIF(RTRIM(s.nombre),''),N'Sin nombre'),NULLIF(RTRIM(s.apellido),''),NULLIF(RTRIM(s.direccion),''),NULLIF(RTRIM(s.telefono),''),NULLIF(RTRIM(s.celular),''),s.ciudad,s.tipo_vendedor,ISNULL(s.aplicacomision,0),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_Vendedor s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_Salespeople',(SELECT COUNT(*) FROM [dbo].[INV_Salespeople]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_Salespeople',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 11: INV_Transactions (inv_movtos) — large table
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_Transactions (inv_movtos) — LARGE TABLE...';

    INSERT INTO [dbo].[INV_Transactions] (
        [TransactionTypeId], [SequenceNumber], [TransactionDate],
        [InvoiceNumber], [ProductId], [Quantity], [VatRate], [DiscountRate],
        [CostFlag], [SystemDate], [CustomerId],
        [VatAmount], [DiscountAmount], [UnitPrice], [UserId],
        [SalesPointId], [ShiftId], [SubTotal], [NetTotal],
        [AdminFee], [AdminVat], [TicketVat], [OtherTax], [AirportTax], [FuelTax],
        [IsPosTransaction], [WarehouseId], [LocationId], [ConsecutiveNumber],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(s.IdTipoMovto, 0), ISNULL(s.Secuencia, 0),
        CASE WHEN s.FecMovto <= '1900-01-02' THEN CAST('1900-01-01' AS DATE) ELSE CAST(s.FecMovto AS DATE) END,
        NULLIF(RTRIM(s.NumFactura), ''),
        ISNULL(s.IdProducto, 0), ISNULL(s.Cantidad, 0),
        ISNULL(s.TasaIva, 0), ISNULL(s.TasaDsto, 0),
        ISNULL(s.Costo, 0),
        CASE WHEN s.Fechasys <= '1900-01-02' THEN SYSUTCDATETIME() ELSE s.Fechasys END,
        s.IdCliente,
        ISNULL(s.VlrIva, 0), ISNULL(s.VlrDsto, 0), ISNULL(s.PreUnitario, 0),
        NULLIF(RTRIM(s.Idusuario), ''),
        s.IdPunto, s.IdTurno,
        ISNULL(s.Subtotal, 0), ISNULL(s.Neto, 0),
        ISNULL(s.cuotaadmin, 0), ISNULL(s.ivaadmin, 0),
        ISNULL(s.ivaboleteria, 0), ISNULL(s.otrosimp, 0),
        ISNULL(s.tasaaeropuerto, 0), ISNULL(s.tasacombustible, 0),
        ISNULL(s.espos, 0), s.idbodega, s.idubicacion,
        ISNULL(s.consecutivo, 0),
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.inv_movtos s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'INV_Transactions', (SELECT COUNT(*) FROM [dbo].[INV_Transactions]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'INV_Transactions', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCK 12: INV_Documents (inv_docs)
-- ============================================================
BEGIN TRY
    BEGIN TRAN;
    PRINT '>> INV_Documents (inv_docs)...';

    INSERT INTO [dbo].[INV_Documents] (
        [TransactionTypeId], [SequenceNumber], [CustomerId], [EntryDate],
        [TotalAmount], [DiscountAmount], [VatAmount], [Status], [UserId],
        [PaymentClassId], [CashAmount], [CreditAmount], [DebitCardAmount],
        [CreditCardAmount], [CheckAmount], [BankId], [ItemCount],
        [BankAccountNumber], [SalesPointId], [ShiftId], [AuditAmount],
        [Detail], [InvoiceNumber], [DueDate], [SalesPersonId],
        [ReturnTypeId], [ReturnSequence],
        [CreatedBy], [CreatedAt]
    )
    SELECT
        ISNULL(s.IdTipoMovto, 0), ISNULL(s.Secuencia, 0),
        s.IdCliente,
        CASE WHEN s.FecIng <= '1900-01-02' THEN CAST('1900-01-01' AS DATETIME2) ELSE s.FecIng END,
        ISNULL(s.VlrTotal, 0), ISNULL(s.VlrDsto, 0), ISNULL(s.VlrIva, 0),
        ISNULL(s.Estado, 0), NULLIF(RTRIM(s.IdUsuario), ''),
        ISNULL(s.ClaPago, 0), ISNULL(s.VlrEfectivo, 0), ISNULL(s.VlrCredito, 0),
        ISNULL(s.VlrTarjDeb, 0), ISNULL(s.VlrTarjCred, 0), ISNULL(s.VlrCheque, 0),
        NULLIF(RTRIM(s.Idbanco), ''), ISNULL(s.Articulos, 0),
        NULLIF(RTRIM(s.CuentaBanco), ''),
        s.IdPunto, s.IdTurno, ISNULL(s.VlrAuditoria, 0),
        NULLIF(RTRIM(s.Detalle), ''), s.Factura,
        CASE WHEN s.fecvence <= '1900-01-02' THEN NULL ELSE CAST(s.fecvence AS DATE) END,
        s.idVendedor, s.idtipomovdev, s.secuenciadev,
        N'MIGRATION', SYSUTCDATETIME()
    FROM [old].dbo.inv_docs s;

    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10));
    COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ExecutedAt)
    VALUES (N'INV_Documents', (SELECT COUNT(*) FROM [dbo].[INV_Documents]), N'OK', SYSUTCDATETIME());
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT '   ERROR: ' + ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog] (TableName, RowsMigrated, Status, ErrorMessage, ExecutedAt)
    VALUES (N'INV_Documents', 0, N'FAIL', ERROR_MESSAGE(), SYSUTCDATETIME());
END CATCH;
GO

-- ============================================================
-- BLOCKS 13-24: Remaining INV tables
-- inv_precios, inv_tipolistas, inv_dstos, inv_facturas,
-- inv_cuentas, inv_cuentasiva, inv_invfisico,
-- inv_param_comisiones, inv_param_comisiones_Precios,
-- inv_puntos, inv_movtos_orden, inv_docs_orden
-- ============================================================

-- BLOCK 13: INV_Prices (inv_precios)
BEGIN TRY BEGIN TRAN; PRINT '>> INV_Prices (inv_precios)...';
    INSERT INTO [dbo].[INV_Prices] ([PriceListTypeId],[ProductId],[CustomerType],[StartDate],[EndDate],[Price],[PriceClass],[Description],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.IdTipoPrecio,0),ISNULL(s.IdProducto,0),ISNULL(NULLIF(RTRIM(s.TipoCliente),''),N''),CASE WHEN s.FecIni<='1900-01-02' THEN NULL ELSE s.FecIni END,CASE WHEN s.FecFin<='1900-01-02' THEN NULL ELSE s.FecFin END,ISNULL(s.Precio,0),s.ClasePrecio,NULLIF(RTRIM(s.Descripcion),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_precios s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_Prices',(SELECT COUNT(*) FROM [dbo].[INV_Prices]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_Prices',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 14: INV_PriceListTypes (inv_tipolistas)
BEGIN TRY BEGIN TRAN; PRINT '>> INV_PriceListTypes (inv_tipolistas)...';
    INSERT INTO [dbo].[INV_PriceListTypes] ([TypeCode],[Name],[ShortName],[PriceClass],[CreatedBy],[CreatedAt])
    SELECT s.IdTipoPrecio,ISNULL(NULLIF(RTRIM(s.Descripcion),''),N'Sin nombre'),NULL,s.ClasePrecio,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_tipolistas s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_PriceListTypes',(SELECT COUNT(*) FROM [dbo].[INV_PriceListTypes]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_PriceListTypes',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 15: INV_Discounts (inv_dstos)
BEGIN TRY BEGIN TRAN; PRINT '>> INV_Discounts (inv_dstos)...';
    INSERT INTO [dbo].[INV_Discounts] ([ProductId],[DiscountTypeId],[DiscountClass],[CustomerId],[ProductClass],[CustomerType],[PaymentClassId],[StartDate],[EndDate],[QuantityStart],[QuantityEnd],[PurchasePeriod],[PurchaseAmount],[DiscountRate],[PeriodCode],[GroupId],[CreatedBy],[CreatedAt])
    SELECT s.IdProducto,s.IdTipoDsto,s.ClaseDsto,NULLIF(RTRIM(s.IdCliente),''),NULLIF(RTRIM(s.ClaProducto),''),NULLIF(RTRIM(s.TipoCliente),''),s.ClaPago,CASE WHEN s.Fecini<='1900-01-02' THEN NULL ELSE s.Fecini END,CASE WHEN s.FecFin<='1900-01-02' THEN NULL ELSE s.FecFin END,ISNULL(s.CantInicial,0),ISNULL(s.CantFinal,0),s.PerCompra,ISNULL(s.VlrCompra,0),ISNULL(s.TasaDsto,0),s.Periodo,NULLIF(RTRIM(s.IdGrupo),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_dstos s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_Discounts',(SELECT COUNT(*) FROM [dbo].[INV_Discounts]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_Discounts',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 16: INV_Invoices (inv_facturas)
BEGIN TRY BEGIN TRAN; PRINT '>> INV_Invoices (inv_facturas)...';
    INSERT INTO [dbo].[INV_Invoices] ([InvoiceCode],[Resolution],[ResolutionDate],[VatRegime],[Prefix],[InitialNumber],[FinalNumber],[InvoiceConsecutive],[EmployeeVoucherCode],[EmployeeCreditLineId],[EmployeeDeductionType],[EmployeeTerm],[EmployerVoucherCode],[EmployerCreditLineId],[EmployerDeductionType],[EmployerTerm],[AdjustmentTypeId],[PortfolioVoucherCode],[CreditLineId],[DeductionType],[Term],[UpdatesCosts],[PrintsBonusTickets],[OpensRegister],[CommissionGroup],[CommissionWithholdingRate],[PreventCostUtility],[VoucherConsecutive],[SingleDiscountOnly],[VatWithDiscount],[ThirdPartySpecialLineId],[ThirdPartyCreditLineId],[ThirdPartySpecialVoucher],[ThirdPartyVoucherCode],[CreatedBy],[CreatedAt])
    SELECT ISNULL(NULLIF(RTRIM(s.IdCodigo),''),N'0'),NULLIF(RTRIM(s.Resolucion),''),CASE WHEN s.FecResol<='1900-01-02' THEN NULL ELSE CAST(s.FecResol AS DATE) END,ISNULL(s.RegimenIva,0),NULLIF(RTRIM(s.Prefijo),''),NULLIF(RTRIM(s.NumInicial),''),NULLIF(RTRIM(s.NumFinal),''),ISNULL(s.consecutivo,0),NULLIF(RTRIM(s.CpteCartEmpl),''),s.lincredEmpl,NULLIF(RTRIM(s.cladesEmpl),''),s.plazoEmpl,NULLIF(RTRIM(s.CpteCartPatro),''),s.lincredPatro,NULLIF(RTRIM(s.cladesPatro),''),s.plazoPatro,s.TipMovAjuInv,NULLIF(RTRIM(s.CpteCartera),''),s.lincred,NULLIF(RTRIM(s.clades),''),s.plazo,ISNULL(s.actualizacostos,0),ISNULL(s.impbonoregalia,0),ISNULL(s.abrecajon,0),NULLIF(RTRIM(s.grupocomision),''),ISNULL(s.retftecomision,0),ISNULL(s.previenecostoutilidad,0),ISNULL(s.consecutivocpte,0),ISNULL(s.soloundscto,0),ISNULL(s.ivacondscto,0),NULLIF(RTRIM(s.LincredTeresp),''),NULLIF(RTRIM(s.LincredTer),''),NULLIF(RTRIM(s.CpteCartTeresp),''),NULLIF(RTRIM(s.CpteCartTer),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_facturas s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_Invoices',(SELECT COUNT(*) FROM [dbo].[INV_Invoices]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_Invoices',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 17: INV_ProductAccounts (inv_cuentas)
BEGIN TRY BEGIN TRAN; PRINT '>> INV_ProductAccounts (inv_cuentas)...';
    INSERT INTO [dbo].[INV_ProductAccounts] ([ProductGroupId],[TransactionTypeId],[WarehouseId],[LocationId],[VatAccountCode],[DiscountAccountCode],[TaxableSalesAccountCode],[NonTaxableSalesAccountCode],[NetAccountCode],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.IdGruProducto,0),ISNULL(s.IdTipoMovto,0),ISNULL(s.idbodega,0),ISNULL(s.idubicacion,0),NULLIF(RTRIM(s.Iva),''),NULLIF(RTRIM(s.Dstos),''),NULLIF(RTRIM(s.VentasGrabadas),''),NULLIF(RTRIM(s.VentasNoGrabadas),''),NULLIF(RTRIM(s.Neto),''),N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_cuentas s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_ProductAccounts',(SELECT COUNT(*) FROM [dbo].[INV_ProductAccounts]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_ProductAccounts',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

-- BLOCK 18: INV_SalesPoints (inv_puntos)
BEGIN TRY BEGIN TRAN; PRINT '>> INV_SalesPoints (inv_puntos)...';
    INSERT INTO [dbo].[INV_SalesPoints] ([PointCode],[UserId],[TransactionTypeId],[PrinterName],[Status],[DateId],[ShiftId],[BaseAmount],[WarehouseId],[LocationId],[CreatedBy],[CreatedAt])
    SELECT ISNULL(s.IdPunto,0),NULLIF(RTRIM(s.IdUsuario),''),s.IdTipoMovto,NULLIF(RTRIM(s.Printer),''),ISNULL(s.Estado,0),CASE WHEN s.IdFecha<='1900-01-02' THEN NULL ELSE s.IdFecha END,s.IdTurno,ISNULL(s.BaseVenta,0),s.idbodega,s.idubicacion,N'MIGRATION',SYSUTCDATETIME()
    FROM [old].dbo.inv_puntos s;
    PRINT '   Rows: ' + CAST(@@ROWCOUNT AS VARCHAR(10)); COMMIT TRAN;
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ExecutedAt) VALUES(N'INV_SalesPoints',(SELECT COUNT(*) FROM [dbo].[INV_SalesPoints]),N'OK',SYSUTCDATETIME());
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRAN; PRINT '   ERROR: '+ERROR_MESSAGE();
    INSERT INTO [migration].[MigrationLog](TableName,RowsMigrated,Status,ErrorMessage,ExecutedAt) VALUES(N'INV_SalesPoints',0,N'FAIL',ERROR_MESSAGE(),SYSUTCDATETIME());
END CATCH;
GO

PRINT '================================================================';
PRINT '  06_Migrate_INV_Inventory.sql — END  ' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT '================================================================';
GO
