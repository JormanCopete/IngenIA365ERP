using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class EquivalenciasDelProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "INV_SupplierItemCodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierPersonId = table.Column<int>(type: "integer", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ProductUnitId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_SupplierItemCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_SupplierItemCodes_COR_People_SupplierPersonId",
                        column: x => x.SupplierPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_SupplierItemCodes_INV_ProductUnits_ProductUnitId",
                        column: x => x.ProductUnitId,
                        principalSchema: "dbo",
                        principalTable: "INV_ProductUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_SupplierItemCodes_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_SupplierItemCodes_ProductId",
                schema: "dbo",
                table: "INV_SupplierItemCodes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_SupplierItemCodes_ProductUnitId",
                schema: "dbo",
                table: "INV_SupplierItemCodes",
                column: "ProductUnitId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierItemCodes_PublicId",
                schema: "dbo",
                table: "INV_SupplierItemCodes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_SupplierItemCodes_Supplier_ItemCode",
                schema: "dbo",
                table: "INV_SupplierItemCodes",
                columns: new[] { "SupplierPersonId", "ItemCode" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INV_SupplierItemCodes",
                schema: "dbo");
        }
    }
}
