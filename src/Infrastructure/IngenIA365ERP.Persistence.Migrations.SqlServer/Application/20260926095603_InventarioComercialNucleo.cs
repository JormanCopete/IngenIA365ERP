using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 012 (T440, decisiones-transversales §2.15): par aditivo <c>InventarioComercialNucleo</c>, cierre de la entrega I1.
    /// Crea las 39 tablas <c>INV_</c> de I1: el documento genérico (tipos, secuencias, documentos, líneas, vínculos, contraparte
    /// e impuestos), el catálogo (unidades, categorías, marcas, grupos contables, canales, productos y sus unidades, códigos de
    /// barras, impuestos y cambios de grupo), las bodegas (tipos, ubicaciones, alcance por usuario, activaciones, políticas de
    /// reorden, causas de ajuste), el kardex y sus tres proyecciones, la puesta en marcha y los períodos con su valorizado, las
    /// cifras de SOLIDO, el documento del proveedor y sus eventos, los faltantes de traslado y la foto y las capturas de los
    /// conteos. Cambia <c>UK_INV_Salespeople_PersonId</c> a único entre las filas vivas (T427). Nada <c>COR_</c>/<c>SEC_</c>
    /// (van en <c>PlataformaParaInventario</c>) ni de I2–I6. En SQL Server el índice <c>IX_INV_Products_SearchText</c> es no agrupado con <c>INCLUDE (Code, Name, Status)</c>. Sin marcador destructivo: <c>Down()</c> quita sólo lo agregado
    /// y devuelve el índice de vendedores a su forma anterior.
    /// </summary>
    public partial class InventarioComercialNucleo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_INV_Salespeople_PersonId",
                schema: "dbo",
                table: "INV_Salespeople");

            migrationBuilder.CreateTable(
                name: "INV_AccountingGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_AccountingGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_AdjustmentCauses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AllowsPositive = table.Column<bool>(type: "bit", nullable: false),
                    AllowsNegative = table.Column<bool>(type: "bit", nullable: false),
                    AllowsTransitWriteOff = table.Column<bool>(type: "bit", nullable: false),
                    RequiresAttachment = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsRequiredBySystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_AdjustmentCauses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Brands",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Periods",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CloseVersion = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    CloseWarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnbilledShipmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnbilledShipmentsAcceptedByUserId = table.Column<int>(type: "int", nullable: true),
                    UnbilledShipmentsAcceptedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Periods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductCategories",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ProductCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ProductCategories_INV_ProductCategories_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "dbo",
                        principalTable: "INV_ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_SalesChannels",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_SalesChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Setup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastClosedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedByUserId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Setup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_UnitsOfMeasure",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AllowedDecimals = table.Column<byte>(type: "tinyint", nullable: false),
                    DianUnitCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_UnitsOfMeasure", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_WarehouseTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Behavior = table.Column<int>(type: "int", nullable: false),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_WarehouseTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Class = table.Column<int>(type: "int", nullable: false),
                    FiscalPrefix = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    IsContingency = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RequiresCounterparty = table.Column<bool>(type: "bit", nullable: false),
                    RequiresCostCenter = table.Column<bool>(type: "bit", nullable: false),
                    RequiresReason = table.Column<bool>(type: "bit", nullable: false),
                    RequiresExternalReference = table.Column<bool>(type: "bit", nullable: false),
                    SalesChannelId = table.Column<int>(type: "int", nullable: true),
                    IsTaxableWithdrawal = table.Column<bool>(type: "bit", nullable: false),
                    VatNonDeductible = table.Column<bool>(type: "bit", nullable: false),
                    AllowsFutureDate = table.Column<bool>(type: "bit", nullable: false),
                    AllWarehouses = table.Column<bool>(type: "bit", nullable: false),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentTypes_INV_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "dbo",
                        principalTable: "INV_SalesChannels",
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
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    BrandId = table.Column<int>(type: "int", nullable: true),
                    BaseUnitId = table.Column<int>(type: "int", nullable: false),
                    AccountingGroupId = table.Column<int>(type: "int", nullable: true),
                    VatSaleTreatment = table.Column<int>(type: "int", nullable: false),
                    WithholdingConceptId = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TracksLot = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TracksSerial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TracksExpiry = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsPurchasable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsSellable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SearchText = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Products_COR_WithholdingConcepts_WithholdingConceptId",
                        column: x => x.WithholdingConceptId,
                        principalSchema: "dbo",
                        principalTable: "COR_WithholdingConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Products_INV_AccountingGroups_AccountingGroupId",
                        column: x => x.AccountingGroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_AccountingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Products_INV_Brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "dbo",
                        principalTable: "INV_Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Products_INV_ProductCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "dbo",
                        principalTable: "INV_ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Products_INV_UnitsOfMeasure_BaseUnitId",
                        column: x => x.BaseUnitId,
                        principalSchema: "dbo",
                        principalTable: "INV_UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_Warehouses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    WarehouseTypeId = table.Column<int>(type: "int", nullable: false),
                    Behavior = table.Column<int>(type: "int", nullable: false),
                    ActivationStatus = table.Column<int>(type: "int", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Warehouses_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Warehouses_INV_WarehouseTypes_WarehouseTypeId",
                        column: x => x.WarehouseTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_WarehouseTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Warehouses_SEC_Users_ActivatedByUserId",
                        column: x => x.ActivatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentSequences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DocumentSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentSequences_INV_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CostStates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ScopeWarehouseId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    LastUnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_CostStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CostStates_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductAccountingGroupChanges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    FromAccountingGroupId = table.Column<int>(type: "int", nullable: false),
                    ToAccountingGroupId = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ProductAccountingGroupChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ProductAccountingGroupChanges_INV_AccountingGroups_FromAccountingGroupId",
                        column: x => x.FromAccountingGroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_AccountingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ProductAccountingGroupChanges_INV_AccountingGroups_ToAccountingGroupId",
                        column: x => x.ToAccountingGroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_AccountingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ProductAccountingGroupChanges_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductTaxes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    TaxDefinitionId = table.Column<int>(type: "int", nullable: false),
                    TaxRateCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AppliesTo = table.Column<int>(type: "int", nullable: false),
                    TaxableUnitsPerBaseUnit = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ProductTaxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ProductTaxes_COR_TaxDefinitions_TaxDefinitionId",
                        column: x => x.TaxDefinitionId,
                        principalSchema: "dbo",
                        principalTable: "COR_TaxDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ProductTaxes_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductUnits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UsedForPurchase = table.Column<bool>(type: "bit", nullable: false),
                    UsedForSale = table.Column<bool>(type: "bit", nullable: false),
                    IsDefaultPurchase = table.Column<bool>(type: "bit", nullable: false),
                    IsDefaultSale = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ProductUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ProductUnits_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ProductUnits_INV_UnitsOfMeasure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "dbo",
                        principalTable: "INV_UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_Documents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Class = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true),
                    DiscardedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscardedByUserId = table.Column<int>(type: "int", nullable: true),
                    DiscardReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    DestinationWarehouseId = table.Column<int>(type: "int", nullable: true),
                    TransitWarehouseId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: true),
                    CounterpartyPersonId = table.Column<int>(type: "int", nullable: true),
                    SalespersonId = table.Column<int>(type: "int", nullable: true),
                    SalesChannelId = table.Column<int>(type: "int", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PostingMode = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VoidsDocumentId = table.Column<int>(type: "int", nullable: true),
                    VoidedByDocumentId = table.Column<int>(type: "int", nullable: true),
                    FiscalNumberReleased = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OperationMunicipalityDaneCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CountKind = table.Column<int>(type: "int", nullable: true),
                    CountScope = table.Column<int>(type: "int", nullable: true),
                    CountScopeJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsBlindCount = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CountSnapshotAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CountSnapshotKardexEntryId = table.Column<long>(type: "bigint", nullable: true),
                    CountRound = table.Column<byte>(type: "tinyint", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReturnsGoods = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsFullReversal = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CorrectionConceptCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Documents_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_COR_People_CounterpartyPersonId",
                        column: x => x.CounterpartyPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_Documents_VoidedByDocumentId",
                        column: x => x.VoidedByDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_Documents_VoidsDocumentId",
                        column: x => x.VoidsDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "dbo",
                        principalTable: "INV_SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_Salespeople_SalespersonId",
                        column: x => x.SalespersonId,
                        principalSchema: "dbo",
                        principalTable: "INV_Salespeople",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_Warehouses_TransitWarehouseId",
                        column: x => x.TransitWarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_SEC_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Documents_SEC_Users_DiscardedByUserId",
                        column: x => x.DiscardedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentTypeWarehouses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DocumentTypeWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentTypeWarehouses_INV_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentTypeWarehouses_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_LegacyFigures",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportBatchPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProductCodeRaw = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WarehouseCodeRaw = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    AccountingGroupId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    QuantityIn = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    QuantityOut = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ValueIn = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValueOut = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_LegacyFigures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_LegacyFigures_INV_AccountingGroups_AccountingGroupId",
                        column: x => x.AccountingGroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_AccountingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_LegacyFigures_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_LegacyFigures_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PeriodClosingBalances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    AccountingGroupId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Superseded = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_PeriodClosingBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PeriodClosingBalances_INV_AccountingGroups_AccountingGroupId",
                        column: x => x.AccountingGroupId,
                        principalSchema: "dbo",
                        principalTable: "INV_AccountingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PeriodClosingBalances_INV_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "dbo",
                        principalTable: "INV_Periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PeriodClosingBalances_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PeriodClosingBalances_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_ReorderPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MaximumQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReorderPoint = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ReorderPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ReorderPolicies_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ReorderPolicies_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_StockBalances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Physical = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LastMovementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_StockBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_StockBalances_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_StockBalances_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_UserWarehouseScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_UserWarehouseScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_UserWarehouseScopes_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_UserWarehouseScopes_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_WarehouseActivations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ComparisonJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalDifference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsBalanced = table.Column<bool>(type: "bit", nullable: false),
                    DifferenceAcceptedByUserId = table.Column<int>(type: "int", nullable: true),
                    AcceptanceReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedByUserId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_WarehouseActivations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_WarehouseActivations_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_WarehouseLocations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_WarehouseLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_WarehouseLocations_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductBarcodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    ProductUnitId = table.Column<int>(type: "int", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_ProductBarcodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ProductBarcodes_INV_ProductUnits_ProductUnitId",
                        column: x => x.ProductUnitId,
                        principalSchema: "dbo",
                        principalTable: "INV_ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ProductBarcodes_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentLinks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: false),
                    TargetDocumentId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DocumentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLinks_INV_Documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLinks_INV_Documents_TargetDocumentId",
                        column: x => x.TargetDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentPartySnapshots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    DianOrganizationType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DianIdTypeCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckDigit = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: true),
                    LegalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MunicipalityDaneCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CityName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DepartmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CountryCode = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DianResponsibilities = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DianTaxSchemeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsVatResponsible = table.Column<bool>(type: "bit", nullable: false),
                    IsLargeContributor = table.Column<bool>(type: "bit", nullable: false),
                    IsSelfWithholder = table.Column<bool>(type: "bit", nullable: false),
                    IsVatWithholdingAgent = table.Column<bool>(type: "bit", nullable: false),
                    IsSimpleTaxRegime = table.Column<bool>(type: "bit", nullable: false),
                    IsIncomeTaxFiler = table.Column<bool>(type: "bit", nullable: false),
                    WithholdingExempt = table.Column<bool>(type: "bit", nullable: false),
                    IcaWithholdingExempt = table.Column<bool>(type: "bit", nullable: false),
                    CiiuCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DocumentPartySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentPartySnapshots_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentPartySnapshots_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_SupplierInvoiceDetails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentClass = table.Column<int>(type: "int", nullable: false),
                    SupplierPersonId = table.Column<int>(type: "int", nullable: false),
                    SupplierPrefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SupplierNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cufe = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCredit = table.Column<bool>(type: "bit", nullable: false),
                    IsElectronic = table.Column<bool>(type: "bit", nullable: false),
                    IsDebitNote = table.Column<bool>(type: "bit", nullable: false),
                    IsReleased = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_SupplierInvoiceDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_SupplierInvoiceDetails_COR_People_SupplierPersonId",
                        column: x => x.SupplierPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_SupplierInvoiceDetails_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_SupplierInvoiceEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    EventCode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Cude = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EvidenceAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ElectronicDocumentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_SupplierInvoiceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_SupplierInvoiceEvents_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CountSnapshotLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: true),
                    TheoreticalQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SnapshotUnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AddedDuringCapture = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MovementsAfterSnapshot = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Difference = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    RecountRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastRound = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_CountSnapshotLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CountSnapshotLines_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CountSnapshotLines_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CountSnapshotLines_INV_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dbo",
                        principalTable: "INV_WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    QuantityBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RoundingQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    ListPriceIncludesTaxes = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    ToLocationId = table.Column<int>(type: "int", nullable: true),
                    LotId = table.Column<int>(type: "int", nullable: true),
                    SerialId = table.Column<int>(type: "int", nullable: true),
                    AdjustmentCauseId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AffectsCost = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLines_INV_AdjustmentCauses_AdjustmentCauseId",
                        column: x => x.AdjustmentCauseId,
                        principalSchema: "dbo",
                        principalTable: "INV_AdjustmentCauses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLines_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLines_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLines_INV_UnitsOfMeasure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "dbo",
                        principalTable: "INV_UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLines_INV_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dbo",
                        principalTable: "INV_WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLines_INV_WarehouseLocations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalSchema: "dbo",
                        principalTable: "INV_WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_StockDetails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_StockDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_StockDetails_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_StockDetails_INV_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dbo",
                        principalTable: "INV_WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_StockDetails_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_CountCaptures",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    SnapshotLineId = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<byte>(type: "tinyint", nullable: false),
                    CounterUserId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reads = table.Column<int>(type: "int", nullable: false),
                    IsCorrection = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_CountCaptures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CountCaptures_INV_CountSnapshotLines_SnapshotLineId",
                        column: x => x.SnapshotLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_CountSnapshotLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CountCaptures_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CountCaptures_SEC_Users_CounterUserId",
                        column: x => x.CounterUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentLineLinks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentLinkId = table.Column<int>(type: "int", nullable: false),
                    SourceLineId = table.Column<int>(type: "int", nullable: false),
                    TargetLineId = table.Column<int>(type: "int", nullable: false),
                    QuantityBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DocumentLineLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLineLinks_INV_DocumentLines_SourceLineId",
                        column: x => x.SourceLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLineLinks_INV_DocumentLines_TargetLineId",
                        column: x => x.TargetLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentLineLinks_INV_DocumentLinks_DocumentLinkId",
                        column: x => x.DocumentLinkId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentTaxLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentLineId = table.Column<int>(type: "int", nullable: true),
                    TaxDefinitionId = table.Column<int>(type: "int", nullable: false),
                    TaxRateId = table.Column<int>(type: "int", nullable: false),
                    TaxRateCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Treatment = table.Column<int>(type: "int", nullable: false),
                    WithholdingConceptId = table.Column<int>(type: "int", nullable: true),
                    MunicipalityDaneCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    AmountPerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxableUnits = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Base = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DianTaxCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ExplanationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_DocumentTaxLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentTaxLines_COR_TaxDefinitions_TaxDefinitionId",
                        column: x => x.TaxDefinitionId,
                        principalSchema: "dbo",
                        principalTable: "COR_TaxDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentTaxLines_COR_TaxRates_TaxRateId",
                        column: x => x.TaxRateId,
                        principalSchema: "dbo",
                        principalTable: "COR_TaxRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentTaxLines_COR_WithholdingConcepts_WithholdingConceptId",
                        column: x => x.WithholdingConceptId,
                        principalSchema: "dbo",
                        principalTable: "COR_WithholdingConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentTaxLines_INV_DocumentLines_DocumentLineId",
                        column: x => x.DocumentLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentTaxLines_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_KardexEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentLineId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: true),
                    SerialId = table.Column<int>(type: "int", nullable: true),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    QuantityBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostScopeWarehouseId = table.Column<int>(type: "int", nullable: false),
                    CostMethod = table.Column<int>(type: "int", nullable: false),
                    ReversesEntryId = table.Column<long>(type: "bigint", nullable: true),
                    AffectsEntryId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_KardexEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_KardexEntries_INV_DocumentLines_DocumentLineId",
                        column: x => x.DocumentLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_KardexEntries_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_KardexEntries_INV_KardexEntries_AffectsEntryId",
                        column: x => x.AffectsEntryId,
                        principalSchema: "dbo",
                        principalTable: "INV_KardexEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_KardexEntries_INV_KardexEntries_ReversesEntryId",
                        column: x => x.ReversesEntryId,
                        principalSchema: "dbo",
                        principalTable: "INV_KardexEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_KardexEntries_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_KardexEntries_INV_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "dbo",
                        principalTable: "INV_WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_KardexEntries_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_TransferDiscrepancies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DispatchDocumentId = table.Column<int>(type: "int", nullable: false),
                    ReceiptDocumentId = table.Column<int>(type: "int", nullable: false),
                    DispatchLineId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    QuantityBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ResolvedQuantityBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    Resolution = table.Column<int>(type: "int", nullable: true),
                    ResolutionQuantityBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ResolutionRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionRequestedByUserId = table.Column<int>(type: "int", nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdjustmentCauseId = table.Column<int>(type: "int", nullable: true),
                    ResolutionDocumentId = table.Column<int>(type: "int", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_TransferDiscrepancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_TransferDiscrepancies_INV_AdjustmentCauses_AdjustmentCauseId",
                        column: x => x.AdjustmentCauseId,
                        principalSchema: "dbo",
                        principalTable: "INV_AdjustmentCauses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_TransferDiscrepancies_INV_DocumentLines_DispatchLineId",
                        column: x => x.DispatchLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_TransferDiscrepancies_INV_Documents_DispatchDocumentId",
                        column: x => x.DispatchDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_TransferDiscrepancies_INV_Documents_ReceiptDocumentId",
                        column: x => x.ReceiptDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_TransferDiscrepancies_INV_Documents_ResolutionDocumentId",
                        column: x => x.ResolutionDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_TransferDiscrepancies_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_INV_Salespeople_PersonId",
                schema: "dbo",
                table: "INV_Salespeople",
                column: "PersonId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_AccountingGroups_Code",
                schema: "dbo",
                table: "INV_AccountingGroups",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_AccountingGroups_PublicId",
                schema: "dbo",
                table: "INV_AccountingGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_AdjustmentCauses_Code",
                schema: "dbo",
                table: "INV_AdjustmentCauses",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_AdjustmentCauses_PublicId",
                schema: "dbo",
                table: "INV_AdjustmentCauses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_Brands_Code",
                schema: "dbo",
                table: "INV_Brands",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Brands_PublicId",
                schema: "dbo",
                table: "INV_Brands",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_CostStates_Product_Scope",
                schema: "dbo",
                table: "INV_CostStates",
                columns: new[] { "ProductId", "ScopeWarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_CostStates_PublicId",
                schema: "dbo",
                table: "INV_CostStates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CountCaptures_CounterUserId",
                schema: "dbo",
                table: "INV_CountCaptures",
                column: "CounterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CountCaptures_Document_Round_Line",
                schema: "dbo",
                table: "INV_CountCaptures",
                columns: new[] { "DocumentId", "Round", "SnapshotLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_CountCaptures_SnapshotLineId",
                schema: "dbo",
                table: "INV_CountCaptures",
                column: "SnapshotLineId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CountCaptures_PublicId",
                schema: "dbo",
                table: "INV_CountCaptures",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_CountSnapshotLines_LocationId",
                schema: "dbo",
                table: "INV_CountSnapshotLines",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CountSnapshotLines_Product_Document",
                schema: "dbo",
                table: "INV_CountSnapshotLines",
                columns: new[] { "ProductId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_CountSnapshotLines_Location",
                schema: "dbo",
                table: "INV_CountSnapshotLines",
                columns: new[] { "DocumentId", "ProductId", "LocationId" },
                unique: true,
                filter: "[LotId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CountSnapshotLines_Location_Lot",
                schema: "dbo",
                table: "INV_CountSnapshotLines",
                columns: new[] { "DocumentId", "ProductId", "LocationId", "LotId" },
                unique: true,
                filter: "[LotId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CountSnapshotLines_PublicId",
                schema: "dbo",
                table: "INV_CountSnapshotLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLineLinks_DocumentLinkId",
                schema: "dbo",
                table: "INV_DocumentLineLinks",
                column: "DocumentLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLineLinks_TargetLineId",
                schema: "dbo",
                table: "INV_DocumentLineLinks",
                column: "TargetLineId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentLineLinks_PublicId",
                schema: "dbo",
                table: "INV_DocumentLineLinks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentLineLinks_SourceLine_TargetLine",
                schema: "dbo",
                table: "INV_DocumentLineLinks",
                columns: new[] { "SourceLineId", "TargetLineId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLines_AdjustmentCauseId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "AdjustmentCauseId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLines_LocationId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLines_ProductId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLines_ToLocationId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "ToLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLines_UnitId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentLines_Document_LineNumber",
                schema: "dbo",
                table: "INV_DocumentLines",
                columns: new[] { "DocumentId", "LineNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentLines_PublicId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLinks_TargetDocumentId",
                schema: "dbo",
                table: "INV_DocumentLinks",
                column: "TargetDocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentLinks_PublicId",
                schema: "dbo",
                table: "INV_DocumentLinks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentLinks_Source_Target_Kind",
                schema: "dbo",
                table: "INV_DocumentLinks",
                columns: new[] { "SourceDocumentId", "TargetDocumentId", "Kind" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentPartySnapshots_PersonId",
                schema: "dbo",
                table: "INV_DocumentPartySnapshots",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentPartySnapshots_Document_Version",
                schema: "dbo",
                table: "INV_DocumentPartySnapshots",
                columns: new[] { "DocumentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentPartySnapshots_PublicId",
                schema: "dbo",
                table: "INV_DocumentPartySnapshots",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_BranchId",
                schema: "dbo",
                table: "INV_Documents",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_Class_Status_OperationDate",
                schema: "dbo",
                table: "INV_Documents",
                columns: new[] { "Class", "Status", "OperationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_ConfirmedByUserId",
                schema: "dbo",
                table: "INV_Documents",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_CostCenterId",
                schema: "dbo",
                table: "INV_Documents",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_CounterpartyPersonId",
                schema: "dbo",
                table: "INV_Documents",
                column: "CounterpartyPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_CreatedByUserId",
                schema: "dbo",
                table: "INV_Documents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_DestinationWarehouseId",
                schema: "dbo",
                table: "INV_Documents",
                column: "DestinationWarehouseId",
                filter: "[DestinationWarehouseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_DiscardedByUserId",
                schema: "dbo",
                table: "INV_Documents",
                column: "DiscardedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_SalesChannelId",
                schema: "dbo",
                table: "INV_Documents",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_SalespersonId",
                schema: "dbo",
                table: "INV_Documents",
                column: "SalespersonId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_Status_OperationDate",
                schema: "dbo",
                table: "INV_Documents",
                columns: new[] { "Status", "OperationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_TransitWarehouseId",
                schema: "dbo",
                table: "INV_Documents",
                column: "TransitWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_VoidedByDocumentId",
                schema: "dbo",
                table: "INV_Documents",
                column: "VoidedByDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_Warehouse_OperationDate",
                schema: "dbo",
                table: "INV_Documents",
                columns: new[] { "WarehouseId", "OperationDate" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_Documents_PublicId",
                schema: "dbo",
                table: "INV_Documents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_Documents_Type_Prefix_Number",
                schema: "dbo",
                table: "INV_Documents",
                columns: new[] { "DocumentTypeId", "Prefix", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL AND [FiscalNumberReleased] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Documents_VoidsDocumentId",
                schema: "dbo",
                table: "INV_Documents",
                column: "VoidsDocumentId",
                unique: true,
                filter: "[VoidsDocumentId] IS NOT NULL AND [Status] <> 4");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentSequences_PublicId",
                schema: "dbo",
                table: "INV_DocumentSequences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentSequences_Type_Prefix",
                schema: "dbo",
                table: "INV_DocumentSequences",
                columns: new[] { "DocumentTypeId", "Prefix" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentTaxLines_DocumentId",
                schema: "dbo",
                table: "INV_DocumentTaxLines",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentTaxLines_DocumentLineId",
                schema: "dbo",
                table: "INV_DocumentTaxLines",
                column: "DocumentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentTaxLines_TaxDefinition_Document",
                schema: "dbo",
                table: "INV_DocumentTaxLines",
                columns: new[] { "TaxDefinitionId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentTaxLines_TaxRateId",
                schema: "dbo",
                table: "INV_DocumentTaxLines",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentTaxLines_WithholdingConceptId",
                schema: "dbo",
                table: "INV_DocumentTaxLines",
                column: "WithholdingConceptId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentTaxLines_PublicId",
                schema: "dbo",
                table: "INV_DocumentTaxLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentTypes_Class",
                schema: "dbo",
                table: "INV_DocumentTypes",
                column: "Class");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentTypes_SalesChannelId",
                schema: "dbo",
                table: "INV_DocumentTypes",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentTypes_Code",
                schema: "dbo",
                table: "INV_DocumentTypes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentTypes_PublicId",
                schema: "dbo",
                table: "INV_DocumentTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentTypeWarehouses_WarehouseId",
                schema: "dbo",
                table: "INV_DocumentTypeWarehouses",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentTypeWarehouses_PublicId",
                schema: "dbo",
                table: "INV_DocumentTypeWarehouses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentTypeWarehouses_Type_Warehouse",
                schema: "dbo",
                table: "INV_DocumentTypeWarehouses",
                columns: new[] { "DocumentTypeId", "WarehouseId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_AffectsEntryId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "AffectsEntryId",
                filter: "[AffectsEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_DocumentId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_DocumentLineId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "DocumentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_LocationId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_OperationDate",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "OperationDate");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_Product_Scope_Date",
                schema: "dbo",
                table: "INV_KardexEntries",
                columns: new[] { "ProductId", "CostScopeWarehouseId", "OperationDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_Product_Warehouse_Date",
                schema: "dbo",
                table: "INV_KardexEntries",
                columns: new[] { "ProductId", "WarehouseId", "OperationDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_ReversesEntryId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "ReversesEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_WarehouseId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_KardexEntries_PublicId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_LegacyFigures_AccountingGroupId",
                schema: "dbo",
                table: "INV_LegacyFigures",
                column: "AccountingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_LegacyFigures_AsOf_Warehouse",
                schema: "dbo",
                table: "INV_LegacyFigures",
                columns: new[] { "AsOfDate", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_LegacyFigures_Batch",
                schema: "dbo",
                table: "INV_LegacyFigures",
                column: "ImportBatchPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_LegacyFigures_ProductId",
                schema: "dbo",
                table: "INV_LegacyFigures",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_LegacyFigures_WarehouseId",
                schema: "dbo",
                table: "INV_LegacyFigures",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_LegacyFigures_PublicId",
                schema: "dbo",
                table: "INV_LegacyFigures",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PeriodClosingBalances_AccountingGroupId",
                schema: "dbo",
                table: "INV_PeriodClosingBalances",
                column: "AccountingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PeriodClosingBalances_Period_Superseded",
                schema: "dbo",
                table: "INV_PeriodClosingBalances",
                columns: new[] { "PeriodId", "Superseded" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_PeriodClosingBalances_ProductId",
                schema: "dbo",
                table: "INV_PeriodClosingBalances",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PeriodClosingBalances_WarehouseId",
                schema: "dbo",
                table: "INV_PeriodClosingBalances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PeriodClosingBalances_Period_Version_Product_Warehouse",
                schema: "dbo",
                table: "INV_PeriodClosingBalances",
                columns: new[] { "PeriodId", "Version", "ProductId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_PeriodClosingBalances_PublicId",
                schema: "dbo",
                table: "INV_PeriodClosingBalances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_Periods_PublicId",
                schema: "dbo",
                table: "INV_Periods",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_Periods_Year_Month",
                schema: "dbo",
                table: "INV_Periods",
                columns: new[] { "Year", "Month" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductAccountingGroupChanges_FromAccountingGroupId",
                schema: "dbo",
                table: "INV_ProductAccountingGroupChanges",
                column: "FromAccountingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductAccountingGroupChanges_Product_Date",
                schema: "dbo",
                table: "INV_ProductAccountingGroupChanges",
                columns: new[] { "ProductId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductAccountingGroupChanges_ToAccountingGroupId",
                schema: "dbo",
                table: "INV_ProductAccountingGroupChanges",
                column: "ToAccountingGroupId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductAccountingGroupChanges_PublicId",
                schema: "dbo",
                table: "INV_ProductAccountingGroupChanges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductBarcodes_ProductUnitId",
                schema: "dbo",
                table: "INV_ProductBarcodes",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductBarcodes_Barcode",
                schema: "dbo",
                table: "INV_ProductBarcodes",
                column: "Barcode",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductBarcodes_Primary",
                schema: "dbo",
                table: "INV_ProductBarcodes",
                column: "ProductId",
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductBarcodes_PublicId",
                schema: "dbo",
                table: "INV_ProductBarcodes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductCategories_ParentId",
                schema: "dbo",
                table: "INV_ProductCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductCategories_Path",
                schema: "dbo",
                table: "INV_ProductCategories",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductCategories_Code",
                schema: "dbo",
                table: "INV_ProductCategories",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductCategories_PublicId",
                schema: "dbo",
                table: "INV_ProductCategories",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_AccountingGroupId",
                schema: "dbo",
                table: "INV_Products",
                column: "AccountingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_BaseUnitId",
                schema: "dbo",
                table: "INV_Products",
                column: "BaseUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_BrandId",
                schema: "dbo",
                table: "INV_Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_CategoryId",
                schema: "dbo",
                table: "INV_Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_SearchText",
                schema: "dbo",
                table: "INV_Products",
                column: "SearchText")
                .Annotation("SqlServer:Include", new[] { "Code", "Name", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_Status",
                schema: "dbo",
                table: "INV_Products",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Products_WithholdingConceptId",
                schema: "dbo",
                table: "INV_Products",
                column: "WithholdingConceptId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Products_Code",
                schema: "dbo",
                table: "INV_Products",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Products_PublicId",
                schema: "dbo",
                table: "INV_Products",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductTaxes_TaxDefinitionId",
                schema: "dbo",
                table: "INV_ProductTaxes",
                column: "TaxDefinitionId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductTaxes_Product_Tax",
                schema: "dbo",
                table: "INV_ProductTaxes",
                columns: new[] { "ProductId", "TaxDefinitionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductTaxes_PublicId",
                schema: "dbo",
                table: "INV_ProductTaxes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductUnits_UnitId",
                schema: "dbo",
                table: "INV_ProductUnits",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductUnits_DefaultPurchase",
                schema: "dbo",
                table: "INV_ProductUnits",
                column: "ProductId",
                unique: true,
                filter: "[IsDefaultPurchase] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductUnits_DefaultSale",
                schema: "dbo",
                table: "INV_ProductUnits",
                column: "ProductId",
                unique: true,
                filter: "[IsDefaultSale] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductUnits_Product_Unit",
                schema: "dbo",
                table: "INV_ProductUnits",
                columns: new[] { "ProductId", "UnitId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductUnits_PublicId",
                schema: "dbo",
                table: "INV_ProductUnits",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ReorderPolicies_WarehouseId",
                schema: "dbo",
                table: "INV_ReorderPolicies",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ReorderPolicies_Product_Warehouse",
                schema: "dbo",
                table: "INV_ReorderPolicies",
                columns: new[] { "ProductId", "WarehouseId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ReorderPolicies_PublicId",
                schema: "dbo",
                table: "INV_ReorderPolicies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_SalesChannels_Code",
                schema: "dbo",
                table: "INV_SalesChannels",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_SalesChannels_PublicId",
                schema: "dbo",
                table: "INV_SalesChannels",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_Setup_PublicId",
                schema: "dbo",
                table: "INV_Setup",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_StockBalances_WarehouseId",
                schema: "dbo",
                table: "INV_StockBalances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_StockBalances_Product_Warehouse",
                schema: "dbo",
                table: "INV_StockBalances",
                columns: new[] { "ProductId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_StockBalances_PublicId",
                schema: "dbo",
                table: "INV_StockBalances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_StockDetails_LocationId",
                schema: "dbo",
                table: "INV_StockDetails",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_StockDetails_WarehouseId",
                schema: "dbo",
                table: "INV_StockDetails",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_StockDetails_Location",
                schema: "dbo",
                table: "INV_StockDetails",
                columns: new[] { "ProductId", "WarehouseId", "LocationId" },
                unique: true,
                filter: "[LotId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_INV_StockDetails_Location_Lot",
                schema: "dbo",
                table: "INV_StockDetails",
                columns: new[] { "ProductId", "WarehouseId", "LocationId", "LotId" },
                unique: true,
                filter: "[LotId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_INV_StockDetails_PublicId",
                schema: "dbo",
                table: "INV_StockDetails",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierInvoiceDetails_Cufe",
                schema: "dbo",
                table: "INV_SupplierInvoiceDetails",
                column: "Cufe",
                unique: true,
                filter: "[Cufe] IS NOT NULL AND [IsReleased] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierInvoiceDetails_Document",
                schema: "dbo",
                table: "INV_SupplierInvoiceDetails",
                column: "DocumentId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierInvoiceDetails_PublicId",
                schema: "dbo",
                table: "INV_SupplierInvoiceDetails",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierInvoiceDetails_Supplier_Class_Number",
                schema: "dbo",
                table: "INV_SupplierInvoiceDetails",
                columns: new[] { "SupplierPersonId", "DocumentClass", "SupplierPrefix", "SupplierNumber" },
                unique: true,
                filter: "[IsReleased] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_INV_SupplierInvoiceEvents_Status",
                schema: "dbo",
                table: "INV_SupplierInvoiceEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierInvoiceEvents_Document_EventCode",
                schema: "dbo",
                table: "INV_SupplierInvoiceEvents",
                columns: new[] { "DocumentId", "EventCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierInvoiceEvents_PublicId",
                schema: "dbo",
                table: "INV_SupplierInvoiceEvents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferDiscrepancies_AdjustmentCauseId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "AdjustmentCauseId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferDiscrepancies_DispatchDocumentId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "DispatchDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferDiscrepancies_DispatchLineId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "DispatchLineId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferDiscrepancies_ProductId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferDiscrepancies_ResolutionDocumentId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "ResolutionDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferDiscrepancies_ResolvedAt",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "ResolvedAt",
                filter: "[ResolvedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_INV_TransferDiscrepancies_PublicId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_TransferDiscrepancies_Receipt_DispatchLine_Kind",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                columns: new[] { "ReceiptDocumentId", "DispatchLineId", "Kind" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_UnitsOfMeasure_Code",
                schema: "dbo",
                table: "INV_UnitsOfMeasure",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_UnitsOfMeasure_PublicId",
                schema: "dbo",
                table: "INV_UnitsOfMeasure",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_UserWarehouseScopes_WarehouseId",
                schema: "dbo",
                table: "INV_UserWarehouseScopes",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_UserWarehouseScopes_Default",
                schema: "dbo",
                table: "INV_UserWarehouseScopes",
                column: "UserId",
                unique: true,
                filter: "[IsDefault] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_UserWarehouseScopes_PublicId",
                schema: "dbo",
                table: "INV_UserWarehouseScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_UserWarehouseScopes_User_Warehouse",
                schema: "dbo",
                table: "INV_UserWarehouseScopes",
                columns: new[] { "UserId", "WarehouseId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_WarehouseActivations_PublicId",
                schema: "dbo",
                table: "INV_WarehouseActivations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_WarehouseActivations_Warehouse",
                schema: "dbo",
                table: "INV_WarehouseActivations",
                column: "WarehouseId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_WarehouseLocations_Default",
                schema: "dbo",
                table: "INV_WarehouseLocations",
                column: "WarehouseId",
                unique: true,
                filter: "[IsDefault] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_WarehouseLocations_PublicId",
                schema: "dbo",
                table: "INV_WarehouseLocations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_WarehouseLocations_Warehouse_Code",
                schema: "dbo",
                table: "INV_WarehouseLocations",
                columns: new[] { "WarehouseId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Warehouses_ActivatedByUserId",
                schema: "dbo",
                table: "INV_Warehouses",
                column: "ActivatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Warehouses_BranchId",
                schema: "dbo",
                table: "INV_Warehouses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Warehouses_WarehouseTypeId",
                schema: "dbo",
                table: "INV_Warehouses",
                column: "WarehouseTypeId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Warehouses_Branch_Transit",
                schema: "dbo",
                table: "INV_Warehouses",
                column: "BranchId",
                unique: true,
                filter: "[Behavior] = 2 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Warehouses_Code",
                schema: "dbo",
                table: "INV_Warehouses",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Warehouses_PublicId",
                schema: "dbo",
                table: "INV_Warehouses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_WarehouseTypes_Code",
                schema: "dbo",
                table: "INV_WarehouseTypes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_WarehouseTypes_PublicId",
                schema: "dbo",
                table: "INV_WarehouseTypes",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INV_CostStates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CountCaptures",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentLineLinks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentPartySnapshots",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentSequences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentTaxLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentTypeWarehouses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_KardexEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_LegacyFigures",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PeriodClosingBalances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ProductAccountingGroupChanges",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ProductBarcodes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ProductTaxes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ReorderPolicies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Setup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_StockBalances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_StockDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_SupplierInvoiceDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_SupplierInvoiceEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_TransferDiscrepancies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_UserWarehouseScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_WarehouseActivations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CountSnapshotLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentLinks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Periods",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ProductUnits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_AdjustmentCauses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Documents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Products",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_WarehouseLocations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_AccountingGroups",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Brands",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ProductCategories",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_UnitsOfMeasure",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Warehouses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_SalesChannels",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_WarehouseTypes",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_INV_Salespeople_PersonId",
                schema: "dbo",
                table: "INV_Salespeople");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Salespeople_PersonId",
                schema: "dbo",
                table: "INV_Salespeople",
                column: "PersonId",
                unique: true);
        }
    }
}
