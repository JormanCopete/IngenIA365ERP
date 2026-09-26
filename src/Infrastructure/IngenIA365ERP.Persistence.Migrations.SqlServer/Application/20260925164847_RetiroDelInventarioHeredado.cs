using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 012 — inventario comercial, retiro del módulo heredado (specs/012-inventario-comercial/tasks.md
    /// T037; data-model.md §12; decisiones-transversales.md T4 y §2.15; FR-092).
    ///
    /// <para>
    /// MIGRACION-DESTRUCTIVA-APROBADA (Principio XII). Borra las 23 tablas <c>INV_*</c> del inventario
    /// heredado de SOLIDO; <c>INV_Salespeople</c> (vendedores) queda intacta con sus filas. Se genera sobre
    /// el <b>modelo intermedio</b> —sin las 23 entidades y sin ninguna nueva— y va en commit propio, antes de
    /// <c>PlataformaParaInventario</c> e <c>InventarioComercialNucleo</c>, porque los nombres
    /// <c>InventoryDocument</c>, <c>Product</c> y <c>Warehouse</c> vuelven después con otro esquema.
    /// El diagnóstico (<c>specs/012-inventario-comercial/diagnostico-inventario-heredado.sql</c>, T030) dio
    /// 0 filas en las 23 tablas y 0 vendedores en todas las bases de DEV, QA y producción.
    /// </para>
    ///
    /// <para>
    /// Respaldo: <c>pg_dump</c> de cada base de cooperativa inmediatamente antes de aplicar, en cada
    /// ambiente (runbook de despliegue; en DEV el respaldo es el propio contenedor). Segundo revisor:
    /// pendiente, lo nombra el dueño antes de promover (T985).
    /// </para>
    ///
    /// <para>
    /// Qué hace, en orden: (1) guarda (SQL Server <c>THROW 50012</c>): cuenta las filas de las 23 tablas y, si alguna
    /// tiene y la base no trae la fila viva <c>COR_SystemSettings.SettingKey = 'INV.RetiroHeredado.Aprobado'</c>
    /// —la aprobación del dueño para esa cooperativa—, se niega nombrando cada tabla con su cantidad;
    /// (2) suelta las FK de las 23 tablas (entre ellas y hacia <c>ACC_Documents</c>), para
    /// que el orden de los borrados no importe; (3) borra las 23 tablas (generado).
    /// </para>
    ///
    /// <para>
    /// <c>Down()</c> recrea vacías las 23 tablas con la definición que conocía el snapshot (generado por
    /// EF). Es irreversible en datos: con la guarda, sólo se pierden filas que el dueño aprobó perder.
    /// </para>
    /// </summary>
    public partial class RetiroDelInventarioHeredado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // (1) Guarda: sólo sobre tablas heredadas vacías, o con la aprobación del dueño en la base.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [dbo].[COR_SystemSettings]
                               WHERE [SettingKey] = N'INV.RetiroHeredado.Aprobado' AND [IsDeleted] = 0)
                BEGIN
                    DECLARE @tabla sysname, @filas bigint, @conFilas nvarchar(max), @sql nvarchar(max), @mensaje nvarchar(2048);
                    DECLARE tablas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT t FROM (VALUES (N'INV_CommissionParameters'), (N'INV_CommissionPriceParams'), (N'INV_DiscountTypes'), (N'INV_Discounts'), (N'INV_Documents'), (N'INV_Invoices'), (N'INV_Transactions'), (N'INV_TransactionTypes'), (N'INV_Locations'), (N'INV_OrderDocuments'), (N'INV_OrderTransactions'), (N'INV_PhysicalInventory'), (N'INV_Prices'), (N'INV_PriceListTypes'), (N'INV_PrimaryGroups'), (N'INV_ProductAccounts'), (N'INV_Products'), (N'INV_ProductGroups'), (N'INV_SalesPoints'), (N'INV_SecondaryGroups'), (N'INV_Shifts'), (N'INV_VatAccounts'), (N'INV_Warehouses')) AS v(t);
                    OPEN tablas;
                    FETCH NEXT FROM tablas INTO @tabla;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        IF OBJECT_ID(N'[dbo].' + QUOTENAME(@tabla), N'U') IS NOT NULL
                        BEGIN
                            SET @sql = N'SELECT @n = COUNT_BIG(*) FROM [dbo].' + QUOTENAME(@tabla);
                            EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @filas OUTPUT;
                            IF @filas > 0
                                SET @conFilas = CONCAT(@conFilas, CASE WHEN @conFilas IS NULL THEN N'' ELSE N', ' END,
                                                       @tabla, N' (', @filas, N' filas)');
                        END
                        FETCH NEXT FROM tablas INTO @tabla;
                    END
                    CLOSE tablas;
                    DEALLOCATE tablas;
                    IF @conFilas IS NOT NULL
                    BEGIN
                        SET @mensaje = CONCAT(N'RetiroDelInventarioHeredado: las tablas del inventario heredado tienen filas: ', @conFilas,
                            N'. La migracion las borra y solo se aplica sobre tablas vacias o con la aprobacion del dueno para esta cooperativa (fila COR_SystemSettings.SettingKey = ''INV.RetiroHeredado.Aprobado''), previo respaldo y segundo revisor (feature 012, FR-092, Principio XII).');
                        THROW 50012, @mensaje, 1;
                    END
                END
                """);

            // (2) FKs de las tablas heredadas (entre ellas y hacia ACC_Documents).
            migrationBuilder.DropForeignKey(
                name: "FK_INV_Warehouses_INV_Locations_LocationId",
                schema: "dbo",
                table: "INV_Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_SecondaryGroups_INV_PrimaryGroups_PrimaryGroupId",
                schema: "dbo",
                table: "INV_SecondaryGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Documents_ACC_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Documents_INV_TransactionTypes_TransactionTypeId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_OrderDocuments_INV_TransactionTypes_TransactionTypeId",
                schema: "dbo",
                table: "INV_OrderDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_ProductGroups_INV_SecondaryGroups_SecondaryGroupId",
                schema: "dbo",
                table: "INV_ProductGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Products_INV_DiscountTypes_DiscountTypeId",
                schema: "dbo",
                table: "INV_Products");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Products_INV_DiscountTypes_DiscountTypeId1",
                schema: "dbo",
                table: "INV_Products");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Products_INV_ProductGroups_GroupId",
                schema: "dbo",
                table: "INV_Products");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Products_INV_ProductGroups_ProductGroupId",
                schema: "dbo",
                table: "INV_Products");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Discounts_INV_DiscountTypes_DiscountTypeId",
                schema: "dbo",
                table: "INV_Discounts");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Discounts_INV_Products_ProductId",
                schema: "dbo",
                table: "INV_Discounts");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_OrderTransactions_INV_Products_ProductId",
                schema: "dbo",
                table: "INV_OrderTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_OrderTransactions_INV_TransactionTypes_TransactionTypeId",
                schema: "dbo",
                table: "INV_OrderTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_PhysicalInventory_INV_Products_ProductId",
                schema: "dbo",
                table: "INV_PhysicalInventory");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Prices_INV_PriceListTypes_PriceListTypeId",
                schema: "dbo",
                table: "INV_Prices");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Prices_INV_Products_ProductId",
                schema: "dbo",
                table: "INV_Prices");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Transactions_INV_Products_ProductId",
                schema: "dbo",
                table: "INV_Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Transactions_INV_TransactionTypes_TransactionTypeId",
                schema: "dbo",
                table: "INV_Transactions");

            // (3) Las 23 tablas. INV_Salespeople no se toca.
            migrationBuilder.DropTable(
                name: "INV_CommissionParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CommissionPriceParams",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Discounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Documents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Invoices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_OrderDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_OrderTransactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PhysicalInventory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Prices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ProductAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_SalesPoints",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Shifts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Transactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_VatAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Warehouses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PriceListTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Products",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_TransactionTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Locations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DiscountTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ProductGroups",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_SecondaryGroups",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PrimaryGroups",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "INV_CommissionParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommissionGroupId = table.Column<int>(type: "int", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    InvoiceTypeId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SalesRangeEnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SalesRangeStart = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_CommissionParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_CommissionPriceParams",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommissionGroupId = table.Column<int>(type: "int", nullable: false),
                    CommissionRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    InvoiceTypeId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_CommissionPriceParams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_DiscountTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountClass = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TypeCode = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DiscountTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Invoices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdjustmentTypeId = table.Column<int>(type: "int", nullable: true),
                    CommissionGroup = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CommissionWithholdingRate = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: true),
                    DeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeCreditLineId = table.Column<int>(type: "int", nullable: true),
                    EmployeeDeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    EmployeeTerm = table.Column<int>(type: "int", nullable: true),
                    EmployeeVoucherCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    EmployerCreditLineId = table.Column<int>(type: "int", nullable: true),
                    EmployerDeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    EmployerTerm = table.Column<int>(type: "int", nullable: true),
                    EmployerVoucherCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    FinalNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InitialNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InvoiceCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    InvoiceConsecutive = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OpensRegister = table.Column<bool>(type: "bit", nullable: false),
                    PortfolioVoucherCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PreventCostUtility = table.Column<bool>(type: "bit", nullable: false),
                    PrintsBonusTickets = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ResolutionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SingleDiscountOnly = table.Column<bool>(type: "bit", nullable: false),
                    Term = table.Column<int>(type: "int", nullable: true),
                    ThirdPartyCreditLineId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    ThirdPartySpecialLineId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    ThirdPartySpecialVoucher = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    ThirdPartyVoucherCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatesCosts = table.Column<int>(type: "int", nullable: false),
                    VatRegime = table.Column<int>(type: "int", nullable: false),
                    VatWithDiscount = table.Column<bool>(type: "bit", nullable: false),
                    VoucherConsecutive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Locations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LocationCode = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_PriceListTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PriceClass = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TypeCode = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_PriceListTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_PrimaryGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupCode = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_PrimaryGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    NetAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    NonTaxableSalesAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ProductGroupId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TaxableSalesAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    TransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VatAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ProductAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_SalesPoints",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateId = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    PointCode = table.Column<int>(type: "int", nullable: false),
                    PrinterName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TransactionTypeId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_SalesPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Shifts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShiftCode = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_TransactionTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllowsBonus = table.Column<bool>(type: "bit", nullable: false),
                    ControlsStock = table.Column<int>(type: "int", nullable: false),
                    CostVoucherCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CostsProducts = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: true),
                    DeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DocumentClass = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    InvoiceControl = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsReturn = table.Column<bool>(type: "bit", nullable: false),
                    OrderPedido_SustainPrice = table.Column<bool>(type: "bit", nullable: false),
                    PortfolioVoucherCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SequenceNumber = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TotalInPurchase = table.Column<bool>(type: "bit", nullable: false),
                    TransactionVoucherCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    TransfersAccounting = table.Column<bool>(type: "bit", nullable: false),
                    TypeCode = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatesAccounting = table.Column<int>(type: "int", nullable: false),
                    ValidatesCreditLimit = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_TransactionTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_VatAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    ProductGroupId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VatRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_VatAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Warehouses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortDescription = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarehouseCode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Warehouses_INV_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dbo",
                        principalTable: "INV_Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_SecondaryGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrimaryGroupId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupCode = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_SecondaryGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_SecondaryGroups_INV_PrimaryGroups_PrimaryGroupId",
                        column: x => x.PrimaryGroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_PrimaryGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_Documents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountingDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    TransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    AuditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BankId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CashAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CheckAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditCardAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    DebitCardAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvoiceNumber = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    PaymentClassId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnSequence = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: true),
                    ReturnTypeId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SalesPersonId = table.Column<int>(type: "int", nullable: true),
                    SalesPointId = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Documents_ACC_Documents_AccountingDocumentId",
                        column: x => x.AccountingDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_TransactionTypes_TransactionTypeId",
                        column: x => x.TransactionTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_TransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_OrderDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    AuditAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BankId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CashAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ChangeAmount = table.Column<decimal>(type: "decimal(17,0)", precision: 17, nullable: false),
                    CheckAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreditCardAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    DebitCardAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DeductionType = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DiscountDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IcaAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(17,2)", precision: 17, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: false),
                    InvoiceNumber = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    PaymentClassId = table.Column<int>(type: "int", nullable: false),
                    Periodicity = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnSequence = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: true),
                    ReturnTypeId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SalesPersonId = table.Column<int>(type: "int", nullable: true),
                    SalesPointId = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    SubTotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Term = table.Column<int>(type: "int", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TransferSequence = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: true),
                    TransferTypeId = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VatAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_OrderDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_OrderDocuments_INV_TransactionTypes_TransactionTypeId",
                        column: x => x.TransactionTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_TransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SecondaryGroupId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupCode = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MaxSalesQuantity = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestrictsLimit = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ProductGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ProductGroups_INV_SecondaryGroups_SecondaryGroupId",
                        column: x => x.SecondaryGroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_SecondaryGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_Products",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscountTypeId = table.Column<int>(type: "int", nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ControlsStock = table.Column<bool>(type: "bit", nullable: false),
                    CostPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentStock = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountTypeId1 = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MaxSalesQuantity = table.Column<int>(type: "int", nullable: false),
                    MaxStock = table.Column<int>(type: "int", nullable: false),
                    MinStock = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OtherTax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ProductCode = table.Column<int>(type: "int", nullable: false),
                    ProductGroupId = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestrictsLimit = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VatRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Products_INV_DiscountTypes_DiscountTypeId",
                        column: x => x.DiscountTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_DiscountTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Products_INV_DiscountTypes_DiscountTypeId1",
                        column: x => x.DiscountTypeId1,
                        principalSchema: "dbo",
                        principalTable: "INV_DiscountTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_INV_Products_INV_ProductGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_ProductGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Products_INV_ProductGroups_ProductGroupId",
                        column: x => x.ProductGroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_ProductGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "INV_Discounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscountTypeId = table.Column<int>(type: "int", nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CustomerType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountClass = table.Column<int>(type: "int", nullable: true),
                    DiscountRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GroupId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PaymentClassId = table.Column<int>(type: "int", nullable: true),
                    PeriodCode = table.Column<int>(type: "int", nullable: true),
                    ProductClass = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseAmount = table.Column<int>(type: "int", nullable: false),
                    PurchasePeriod = table.Column<int>(type: "int", nullable: true),
                    QuantityEnd = table.Column<int>(type: "int", nullable: false),
                    QuantityStart = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Discounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Discounts_INV_DiscountTypes_DiscountTypeId",
                        column: x => x.DiscountTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_DiscountTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Discounts_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_OrderTransactions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    TransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    AdminFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AdminVat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AirportTax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ConsecutiveNumber = table.Column<long>(type: "bigint", nullable: false),
                    CostAmount = table.Column<decimal>(type: "decimal(16,2)", precision: 16, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DiscountRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    FuelTax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IcaAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    IcaRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsOrderApplied = table.Column<bool>(type: "bit", nullable: false),
                    IsPosTransaction = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    MovementClass = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    NetTotal = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OtherTax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PeriodCode = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SaleType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    SalesPointId = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TicketVat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TransferRecord = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VatAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    WithholdingAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    WithholdingRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_OrderTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_OrderTransactions_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_OrderTransactions_INV_TransactionTypes_TransactionTypeId",
                        column: x => x.TransactionTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_TransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PhysicalInventory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    PeriodCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PhysicalCount = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TheoreticalCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_PhysicalInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PhysicalInventory_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_Prices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PriceListTypeId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PriceClass = table.Column<int>(type: "int", nullable: true),
                    PriceValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Prices_INV_PriceListTypes_PriceListTypeId",
                        column: x => x.PriceListTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_PriceListTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Prices_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_Transactions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    TransactionTypeId = table.Column<int>(type: "int", nullable: false),
                    AdminFee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AdminVat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AirportTax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ConsecutiveNumber = table.Column<long>(type: "bigint", nullable: false),
                    CostFlag = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    FuelTax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsPosTransaction = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    NetTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherTax = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SalesPointId = table.Column<int>(type: "int", nullable: true),
                    SequenceNumber = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TicketVat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Transactions_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Transactions_INV_TransactionTypes_TransactionTypeId",
                        column: x => x.TransactionTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_TransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_CommissionParameters_PublicId",
                schema: "dbo",
                table: "INV_CommissionParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CommissionPriceParams_PublicId",
                schema: "dbo",
                table: "INV_CommissionPriceParams",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Discounts_DiscountTypeId",
                schema: "dbo",
                table: "INV_Discounts",
                column: "DiscountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Discounts_ProductId",
                schema: "dbo",
                table: "INV_Discounts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Discounts_PublicId",
                schema: "dbo",
                table: "INV_Discounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DiscountTypes_PublicId",
                schema: "dbo",
                table: "INV_DiscountTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DiscountTypes_TypeCode",
                schema: "dbo",
                table: "INV_DiscountTypes",
                column: "TypeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "INV_Documents",
                column: "AccountingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_PublicId",
                schema: "dbo",
                table: "INV_Documents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_TransactionTypeId_SequenceNumber",
                schema: "dbo",
                table: "INV_Documents",
                columns: new[] { "TransactionTypeId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Invoices_InvoiceCode",
                schema: "dbo",
                table: "INV_Invoices",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Invoices_PublicId",
                schema: "dbo",
                table: "INV_Invoices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Locations_LocationCode",
                schema: "dbo",
                table: "INV_Locations",
                column: "LocationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Locations_PublicId",
                schema: "dbo",
                table: "INV_Locations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_OrderDocuments_PublicId",
                schema: "dbo",
                table: "INV_OrderDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_OrderDocuments_TransactionTypeId_SequenceNumber",
                schema: "dbo",
                table: "INV_OrderDocuments",
                columns: new[] { "TransactionTypeId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_OrderTransactions_ProductId",
                schema: "dbo",
                table: "INV_OrderTransactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_OrderTransactions_PublicId",
                schema: "dbo",
                table: "INV_OrderTransactions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_OrderTransactions_TransactionTypeId",
                schema: "dbo",
                table: "INV_OrderTransactions",
                column: "TransactionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PhysicalInventory_ProductId",
                schema: "dbo",
                table: "INV_PhysicalInventory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PhysicalInventory_PublicId",
                schema: "dbo",
                table: "INV_PhysicalInventory",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceListTypes_PublicId",
                schema: "dbo",
                table: "INV_PriceListTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceListTypes_TypeCode",
                schema: "dbo",
                table: "INV_PriceListTypes",
                column: "TypeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Prices_PriceListTypeId_ProductId_CustomerType",
                schema: "dbo",
                table: "INV_Prices",
                columns: new[] { "PriceListTypeId", "ProductId", "CustomerType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Prices_ProductId",
                schema: "dbo",
                table: "INV_Prices",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Prices_PublicId",
                schema: "dbo",
                table: "INV_Prices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PrimaryGroups_GroupCode",
                schema: "dbo",
                table: "INV_PrimaryGroups",
                column: "GroupCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PrimaryGroups_PublicId",
                schema: "dbo",
                table: "INV_PrimaryGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductAccounts_ProductGroupId_TransactionTypeId_WarehouseId_LocationId",
                schema: "dbo",
                table: "INV_ProductAccounts",
                columns: new[] { "ProductGroupId", "TransactionTypeId", "WarehouseId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductAccounts_PublicId",
                schema: "dbo",
                table: "INV_ProductAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductGroups_GroupCode",
                schema: "dbo",
                table: "INV_ProductGroups",
                column: "GroupCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductGroups_PublicId",
                schema: "dbo",
                table: "INV_ProductGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductGroups_SecondaryGroupId",
                schema: "dbo",
                table: "INV_ProductGroups",
                column: "SecondaryGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_DiscountTypeId",
                schema: "dbo",
                table: "INV_Products",
                column: "DiscountTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_DiscountTypeId1",
                schema: "dbo",
                table: "INV_Products",
                column: "DiscountTypeId1");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_GroupId",
                schema: "dbo",
                table: "INV_Products",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_ProductCode",
                schema: "dbo",
                table: "INV_Products",
                column: "ProductCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_ProductGroupId",
                schema: "dbo",
                table: "INV_Products",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_PublicId",
                schema: "dbo",
                table: "INV_Products",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_SalesPoints_PointCode_Status_DateId",
                schema: "dbo",
                table: "INV_SalesPoints",
                columns: new[] { "PointCode", "Status", "DateId" },
                unique: true,
                filter: "[DateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_INV_SalesPoints_PublicId",
                schema: "dbo",
                table: "INV_SalesPoints",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_SecondaryGroups_GroupCode",
                schema: "dbo",
                table: "INV_SecondaryGroups",
                column: "GroupCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_SecondaryGroups_PrimaryGroupId",
                schema: "dbo",
                table: "INV_SecondaryGroups",
                column: "PrimaryGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_SecondaryGroups_PublicId",
                schema: "dbo",
                table: "INV_SecondaryGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Shifts_PublicId",
                schema: "dbo",
                table: "INV_Shifts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Shifts_ShiftCode",
                schema: "dbo",
                table: "INV_Shifts",
                column: "ShiftCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Transactions_ProductId",
                schema: "dbo",
                table: "INV_Transactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Transactions_PublicId",
                schema: "dbo",
                table: "INV_Transactions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Transactions_TransactionTypeId",
                schema: "dbo",
                table: "INV_Transactions",
                column: "TransactionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransactionTypes_PublicId",
                schema: "dbo",
                table: "INV_TransactionTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransactionTypes_TypeCode",
                schema: "dbo",
                table: "INV_TransactionTypes",
                column: "TypeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_VatAccounts_PublicId",
                schema: "dbo",
                table: "INV_VatAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Warehouses_LocationId",
                schema: "dbo",
                table: "INV_Warehouses",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Warehouses_PublicId",
                schema: "dbo",
                table: "INV_Warehouses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Warehouses_WarehouseCode_LocationId",
                schema: "dbo",
                table: "INV_Warehouses",
                columns: new[] { "WarehouseCode", "LocationId" },
                unique: true);
        }
    }
}
