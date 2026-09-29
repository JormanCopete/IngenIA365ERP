using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class ComprasYCosteoAvanzado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BalanceClosedAt",
                schema: "dbo",
                table: "INV_Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalanceClosedByUserId",
                schema: "dbo",
                table: "INV_Documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BalanceClosedReason",
                schema: "dbo",
                table: "INV_Documents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpectedDate",
                schema: "dbo",
                table: "INV_Documents",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "INV_CostLayers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ScopeWarehouseId = table.Column<int>(type: "int", nullable: false),
                    EntryKardexEntryId = table.Column<long>(type: "bigint", nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
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
                    table.PrimaryKey("PK_INV_CostLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CostLayers_INV_KardexEntries_EntryKardexEntryId",
                        column: x => x.EntryKardexEntryId,
                        principalSchema: "dbo",
                        principalTable: "INV_KardexEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CostLayers_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_LandedCostAllocations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    ReceiptDocumentId = table.Column<int>(type: "int", nullable: false),
                    ReceiptLineId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    AllocationMethod = table.Column<int>(type: "int", nullable: false),
                    Basis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RoundingResidue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    ExistingRatio = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    ExistingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SoldAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_INV_LandedCostAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_LandedCostAllocations_INV_DocumentLines_ReceiptLineId",
                        column: x => x.ReceiptLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_LandedCostAllocations_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_LandedCostAllocations_INV_Documents_ReceiptDocumentId",
                        column: x => x.ReceiptDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_LandedCostAllocations_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_PurchaseMatchLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceDocumentId = table.Column<int>(type: "int", nullable: false),
                    InvoiceLineId = table.Column<int>(type: "int", nullable: false),
                    OrderLineId = table.Column<int>(type: "int", nullable: true),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedNotInvoicedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    InvoicedQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OrderedUnitPrice = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ReceivedUnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    InvoicedUnitPrice = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    QuantityDifference = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PriceDifferenceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceDifferenceRate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    ExceedsTolerance = table.Column<bool>(type: "bit", nullable: false),
                    ToleranceJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Reasons = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_INV_PurchaseMatchLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PurchaseMatchLines_INV_DocumentLines_InvoiceLineId",
                        column: x => x.InvoiceLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PurchaseMatchLines_INV_DocumentLines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PurchaseMatchLines_INV_Documents_InvoiceDocumentId",
                        column: x => x.InvoiceDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_LayerConsumptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExitKardexEntryId = table.Column<long>(type: "bigint", nullable: false),
                    LayerId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
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
                    table.PrimaryKey("PK_INV_LayerConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_LayerConsumptions_INV_CostLayers_LayerId",
                        column: x => x.LayerId,
                        principalSchema: "dbo",
                        principalTable: "INV_CostLayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_LayerConsumptions_INV_KardexEntries_ExitKardexEntryId",
                        column: x => x.ExitKardexEntryId,
                        principalSchema: "dbo",
                        principalTable: "INV_KardexEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_BalanceClosedByUserId",
                schema: "dbo",
                table: "INV_Documents",
                column: "BalanceClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_CostLayers_Product_Scope_Order",
                schema: "dbo",
                table: "INV_CostLayers",
                columns: new[] { "ProductId", "ScopeWarehouseId", "OperationDate", "EntryKardexEntryId" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_CostLayers_EntryKardexEntryId",
                schema: "dbo",
                table: "INV_CostLayers",
                column: "EntryKardexEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_CostLayers_PublicId",
                schema: "dbo",
                table: "INV_CostLayers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_LandedCostAllocations_ProductId",
                schema: "dbo",
                table: "INV_LandedCostAllocations",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_LandedCostAllocations_ReceiptDocumentId",
                schema: "dbo",
                table: "INV_LandedCostAllocations",
                column: "ReceiptDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_LandedCostAllocations_ReceiptLineId",
                schema: "dbo",
                table: "INV_LandedCostAllocations",
                column: "ReceiptLineId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_LandedCostAllocations_Document_ReceiptLine",
                schema: "dbo",
                table: "INV_LandedCostAllocations",
                columns: new[] { "DocumentId", "ReceiptLineId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_INV_LandedCostAllocations_PublicId",
                schema: "dbo",
                table: "INV_LandedCostAllocations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_LayerConsumptions_ExitKardexEntryId",
                schema: "dbo",
                table: "INV_LayerConsumptions",
                column: "ExitKardexEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_LayerConsumptions_LayerId",
                schema: "dbo",
                table: "INV_LayerConsumptions",
                column: "LayerId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_LayerConsumptions_PublicId",
                schema: "dbo",
                table: "INV_LayerConsumptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PurchaseMatchLines_InvoiceDocumentId",
                schema: "dbo",
                table: "INV_PurchaseMatchLines",
                column: "InvoiceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PurchaseMatchLines_InvoiceLineId",
                schema: "dbo",
                table: "INV_PurchaseMatchLines",
                column: "InvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PurchaseMatchLines_OrderLineId",
                schema: "dbo",
                table: "INV_PurchaseMatchLines",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PurchaseMatchLines_PublicId",
                schema: "dbo",
                table: "INV_PurchaseMatchLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_Documents_SEC_Users_BalanceClosedByUserId",
                schema: "dbo",
                table: "INV_Documents",
                column: "BalanceClosedByUserId",
                principalSchema: "dbo",
                principalTable: "SEC_Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_INV_Documents_SEC_Users_BalanceClosedByUserId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropTable(
                name: "INV_LandedCostAllocations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_LayerConsumptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PurchaseMatchLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_CostLayers",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_INV_Documents_BalanceClosedByUserId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "BalanceClosedAt",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "BalanceClosedByUserId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "BalanceClosedReason",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "ExpectedDate",
                schema: "dbo",
                table: "INV_Documents");
        }
    }
}
