using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class ComercioAmpliado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentProductId",
                schema: "dbo",
                table: "INV_Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VariantKey",
                schema: "dbo",
                table: "INV_Products",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromotionId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "INV_Lots",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ManufactureDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_INV_Lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Lots_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductComponents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ComponentProductId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_INV_ProductComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ProductComponents_INV_Products_ComponentProductId",
                        column: x => x.ComponentProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ProductComponents_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_Promotions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BuyQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PayQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BundlePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsCumulative = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_INV_Promotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Reservations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    DocumentLineId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    QuantityBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ConsumedQuantityBase = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedByDocumentId = table.Column<int>(type: "int", nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_INV_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Reservations_INV_DocumentLines_DocumentLineId",
                        column: x => x.DocumentLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Reservations_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Reservations_INV_Documents_ReleasedByDocumentId",
                        column: x => x.ReleasedByDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Reservations_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Reservations_INV_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_VariantAttributes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_INV_VariantAttributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_Serials",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: true),
                    InStockWarehouseId = table.Column<int>(type: "int", nullable: true),
                    InStockLocationId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_INV_Serials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_Serials_INV_Lots_LotId",
                        column: x => x.LotId,
                        principalSchema: "dbo",
                        principalTable: "INV_Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Serials_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Serials_INV_WarehouseLocations_InStockLocationId",
                        column: x => x.InStockLocationId,
                        principalSchema: "dbo",
                        principalTable: "INV_WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_Serials_INV_Warehouses_InStockWarehouseId",
                        column: x => x.InStockWarehouseId,
                        principalSchema: "dbo",
                        principalTable: "INV_Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PromotionScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionId = table.Column<int>(type: "int", nullable: false),
                    ScopeKind = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductCategoryId = table.Column<int>(type: "int", nullable: true),
                    Segment = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    SalesChannelId = table.Column<int>(type: "int", nullable: true),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_INV_PromotionScopes", x => x.Id);
                    table.CheckConstraint("CK_INV_PromotionScopes_OneTarget", "([ScopeKind] = 1 AND [ProductId] IS NOT NULL AND [ProductCategoryId] IS NULL AND [Segment] IS NULL AND [SalesChannelId] IS NULL) OR ([ScopeKind] = 2 AND [ProductId] IS NULL AND [ProductCategoryId] IS NOT NULL AND [Segment] IS NULL AND [SalesChannelId] IS NULL) OR ([ScopeKind] = 3 AND [ProductId] IS NULL AND [ProductCategoryId] IS NULL AND [Segment] IS NOT NULL AND [SalesChannelId] IS NULL) OR ([ScopeKind] = 4 AND [ProductId] IS NULL AND [ProductCategoryId] IS NULL AND [Segment] IS NULL AND [SalesChannelId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_INV_PromotionScopes_INV_ProductCategories_ProductCategoryId",
                        column: x => x.ProductCategoryId,
                        principalSchema: "dbo",
                        principalTable: "INV_ProductCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PromotionScopes_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PromotionScopes_INV_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "dbo",
                        principalTable: "INV_Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PromotionScopes_INV_SalesChannels_SalesChannelId",
                        column: x => x.SalesChannelId,
                        principalSchema: "dbo",
                        principalTable: "INV_SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PromotionTiers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromotionId = table.Column<int>(type: "int", nullable: false),
                    MinQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_INV_PromotionTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PromotionTiers_INV_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalSchema: "dbo",
                        principalTable: "INV_Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_VariantAttributeValues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VariantAttributeId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_INV_VariantAttributeValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_VariantAttributeValues_INV_VariantAttributes_VariantAttributeId",
                        column: x => x.VariantAttributeId,
                        principalSchema: "dbo",
                        principalTable: "INV_VariantAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_ProductVariantValues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    VariantAttributeId = table.Column<int>(type: "int", nullable: false),
                    VariantAttributeValueId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_INV_ProductVariantValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_ProductVariantValues_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ProductVariantValues_INV_VariantAttributeValues_VariantAttributeValueId",
                        column: x => x.VariantAttributeValueId,
                        principalSchema: "dbo",
                        principalTable: "INV_VariantAttributeValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_ProductVariantValues_INV_VariantAttributes_VariantAttributeId",
                        column: x => x.VariantAttributeId,
                        principalSchema: "dbo",
                        principalTable: "INV_VariantAttributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_TransferDiscrepancies_LotId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_StockDetails_LotId",
                schema: "dbo",
                table: "INV_StockDetails",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Products_Parent_VariantKey",
                schema: "dbo",
                table: "INV_Products",
                columns: new[] { "ParentProductId", "VariantKey" },
                unique: true,
                filter: "[ParentProductId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_LotId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_KardexEntries_SerialId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "SerialId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLines_LotId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLines_SerialId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "SerialId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentLineDiscounts_PromotionId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CountSnapshotLines_LotId",
                schema: "dbo",
                table: "INV_CountSnapshotLines",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Lots_Product_ExpiryDate",
                schema: "dbo",
                table: "INV_Lots",
                columns: new[] { "ProductId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_Lots_Product_Code",
                schema: "dbo",
                table: "INV_Lots",
                columns: new[] { "ProductId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Lots_PublicId",
                schema: "dbo",
                table: "INV_Lots",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductComponents_ComponentProductId",
                schema: "dbo",
                table: "INV_ProductComponents",
                column: "ComponentProductId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductComponents_Product_Component",
                schema: "dbo",
                table: "INV_ProductComponents",
                columns: new[] { "ProductId", "ComponentProductId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductComponents_PublicId",
                schema: "dbo",
                table: "INV_ProductComponents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductVariantValues_ValueId",
                schema: "dbo",
                table: "INV_ProductVariantValues",
                column: "VariantAttributeValueId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_ProductVariantValues_VariantAttributeId",
                schema: "dbo",
                table: "INV_ProductVariantValues",
                column: "VariantAttributeId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductVariantValues_Product_Attribute",
                schema: "dbo",
                table: "INV_ProductVariantValues",
                columns: new[] { "ProductId", "VariantAttributeId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_ProductVariantValues_PublicId",
                schema: "dbo",
                table: "INV_ProductVariantValues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Promotions_Validity",
                schema: "dbo",
                table: "INV_Promotions",
                columns: new[] { "ValidFrom", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_Promotions_Code",
                schema: "dbo",
                table: "INV_Promotions",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Promotions_PublicId",
                schema: "dbo",
                table: "INV_Promotions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PromotionScopes_ProductCategoryId",
                schema: "dbo",
                table: "INV_PromotionScopes",
                column: "ProductCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PromotionScopes_ProductId",
                schema: "dbo",
                table: "INV_PromotionScopes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PromotionScopes_Promotion_Kind",
                schema: "dbo",
                table: "INV_PromotionScopes",
                columns: new[] { "PromotionId", "ScopeKind" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_PromotionScopes_SalesChannelId",
                schema: "dbo",
                table: "INV_PromotionScopes",
                column: "SalesChannelId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PromotionScopes_PublicId",
                schema: "dbo",
                table: "INV_PromotionScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_PromotionTiers_Promotion_MinQuantity",
                schema: "dbo",
                table: "INV_PromotionTiers",
                columns: new[] { "PromotionId", "MinQuantity" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PromotionTiers_PublicId",
                schema: "dbo",
                table: "INV_PromotionTiers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Reservations_DocumentId",
                schema: "dbo",
                table: "INV_Reservations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Reservations_DocumentLineId",
                schema: "dbo",
                table: "INV_Reservations",
                column: "DocumentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Reservations_ExpiresOn",
                schema: "dbo",
                table: "INV_Reservations",
                column: "ExpiresOn",
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Reservations_Product_Warehouse_Status",
                schema: "dbo",
                table: "INV_Reservations",
                columns: new[] { "ProductId", "WarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_Reservations_ReleasedByDocumentId",
                schema: "dbo",
                table: "INV_Reservations",
                column: "ReleasedByDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Reservations_WarehouseId",
                schema: "dbo",
                table: "INV_Reservations",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Reservations_PublicId",
                schema: "dbo",
                table: "INV_Reservations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_Serials_InStockLocationId",
                schema: "dbo",
                table: "INV_Serials",
                column: "InStockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Serials_InStockWarehouseId",
                schema: "dbo",
                table: "INV_Serials",
                column: "InStockWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Serials_LotId",
                schema: "dbo",
                table: "INV_Serials",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Serials_Product_SerialNumber",
                schema: "dbo",
                table: "INV_Serials",
                columns: new[] { "ProductId", "SerialNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_Serials_PublicId",
                schema: "dbo",
                table: "INV_Serials",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_VariantAttributes_Code",
                schema: "dbo",
                table: "INV_VariantAttributes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_VariantAttributes_PublicId",
                schema: "dbo",
                table: "INV_VariantAttributes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_VariantAttributeValues_Attribute_Code",
                schema: "dbo",
                table: "INV_VariantAttributeValues",
                columns: new[] { "VariantAttributeId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_VariantAttributeValues_PublicId",
                schema: "dbo",
                table: "INV_VariantAttributeValues",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_CountSnapshotLines_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_CountSnapshotLines",
                column: "LotId",
                principalSchema: "dbo",
                principalTable: "INV_Lots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_DocumentLineDiscounts_INV_Promotions_PromotionId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts",
                column: "PromotionId",
                principalSchema: "dbo",
                principalTable: "INV_Promotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_DocumentLines_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "LotId",
                principalSchema: "dbo",
                principalTable: "INV_Lots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_DocumentLines_INV_Serials_SerialId",
                schema: "dbo",
                table: "INV_DocumentLines",
                column: "SerialId",
                principalSchema: "dbo",
                principalTable: "INV_Serials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_KardexEntries_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "LotId",
                principalSchema: "dbo",
                principalTable: "INV_Lots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_KardexEntries_INV_Serials_SerialId",
                schema: "dbo",
                table: "INV_KardexEntries",
                column: "SerialId",
                principalSchema: "dbo",
                principalTable: "INV_Serials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_Products_INV_Products_ParentProductId",
                schema: "dbo",
                table: "INV_Products",
                column: "ParentProductId",
                principalSchema: "dbo",
                principalTable: "INV_Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_StockDetails_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_StockDetails",
                column: "LotId",
                principalSchema: "dbo",
                principalTable: "INV_Lots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_TransferDiscrepancies_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies",
                column: "LotId",
                principalSchema: "dbo",
                principalTable: "INV_Lots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INV_CountSnapshotLines_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_CountSnapshotLines");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_DocumentLineDiscounts_INV_Promotions_PromotionId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_DocumentLines_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_DocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_DocumentLines_INV_Serials_SerialId",
                schema: "dbo",
                table: "INV_DocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_KardexEntries_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_KardexEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_KardexEntries_INV_Serials_SerialId",
                schema: "dbo",
                table: "INV_KardexEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Products_INV_Products_ParentProductId",
                schema: "dbo",
                table: "INV_Products");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_StockDetails_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_StockDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_TransferDiscrepancies_INV_Lots_LotId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies");

            migrationBuilder.DropTable(
                name: "INV_ProductComponents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_ProductVariantValues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PromotionScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PromotionTiers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Reservations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Serials",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_VariantAttributeValues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Promotions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_Lots",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_VariantAttributes",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_INV_TransferDiscrepancies_LotId",
                schema: "dbo",
                table: "INV_TransferDiscrepancies");

            migrationBuilder.DropIndex(
                name: "IX_INV_StockDetails_LotId",
                schema: "dbo",
                table: "INV_StockDetails");

            migrationBuilder.DropIndex(
                name: "UK_INV_Products_Parent_VariantKey",
                schema: "dbo",
                table: "INV_Products");

            migrationBuilder.DropIndex(
                name: "IX_INV_KardexEntries_LotId",
                schema: "dbo",
                table: "INV_KardexEntries");

            migrationBuilder.DropIndex(
                name: "IX_INV_KardexEntries_SerialId",
                schema: "dbo",
                table: "INV_KardexEntries");

            migrationBuilder.DropIndex(
                name: "IX_INV_DocumentLines_LotId",
                schema: "dbo",
                table: "INV_DocumentLines");

            migrationBuilder.DropIndex(
                name: "IX_INV_DocumentLines_SerialId",
                schema: "dbo",
                table: "INV_DocumentLines");

            migrationBuilder.DropIndex(
                name: "IX_INV_DocumentLineDiscounts_PromotionId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts");

            migrationBuilder.DropIndex(
                name: "IX_INV_CountSnapshotLines_LotId",
                schema: "dbo",
                table: "INV_CountSnapshotLines");

            migrationBuilder.DropColumn(
                name: "ParentProductId",
                schema: "dbo",
                table: "INV_Products");

            migrationBuilder.DropColumn(
                name: "VariantKey",
                schema: "dbo",
                table: "INV_Products");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                schema: "dbo",
                table: "INV_DocumentLineDiscounts");
        }
    }
}
