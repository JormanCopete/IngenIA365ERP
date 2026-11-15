using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class HistoriaDePreciosYCamposDeVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "INV_DocumentFieldDefinitions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DataType = table.Column<int>(type: "integer", nullable: false),
                    MaxLength = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ShowInSales = table.Column<bool>(type: "boolean", nullable: false),
                    ShowInQuotes = table.Column<bool>(type: "boolean", nullable: false),
                    PrintOnInvoice = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_INV_DocumentFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "INV_PriceListItemHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PriceListId = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    UnitId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NewPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PreviousUnitId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_INV_PriceListItemHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PriceListItemHistory_INV_PriceLists_PriceListId",
                        column: x => x.PriceListId,
                        principalSchema: "dbo",
                        principalTable: "INV_PriceLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PriceListItemHistory_INV_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "dbo",
                        principalTable: "INV_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PriceListItemHistory_INV_UnitsOfMeasure_PreviousUnitId",
                        column: x => x.PreviousUnitId,
                        principalSchema: "dbo",
                        principalTable: "INV_UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_PriceListItemHistory_INV_UnitsOfMeasure_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "dbo",
                        principalTable: "INV_UnitsOfMeasure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DocumentFieldValues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    DefinitionId = table.Column<int>(type: "integer", nullable: false),
                    FieldCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrintOnInvoice = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_INV_DocumentFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DocumentFieldValues_INV_DocumentFieldDefinitions_Defini~",
                        column: x => x.DefinitionId,
                        principalSchema: "dbo",
                        principalTable: "INV_DocumentFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DocumentFieldValues_INV_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentFieldDefinitions_Code",
                schema: "dbo",
                table: "INV_DocumentFieldDefinitions",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentFieldDefinitions_PublicId",
                schema: "dbo",
                table: "INV_DocumentFieldDefinitions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_DocumentFieldValues_DefinitionId",
                schema: "dbo",
                table: "INV_DocumentFieldValues",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentFieldValues_Document_Definition",
                schema: "dbo",
                table: "INV_DocumentFieldValues",
                columns: new[] { "DocumentId", "DefinitionId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_DocumentFieldValues_PublicId",
                schema: "dbo",
                table: "INV_DocumentFieldValues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceListItemHistory_List_Product_At",
                schema: "dbo",
                table: "INV_PriceListItemHistory",
                columns: new[] { "PriceListId", "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceListItemHistory_PreviousUnitId",
                schema: "dbo",
                table: "INV_PriceListItemHistory",
                column: "PreviousUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceListItemHistory_ProductId",
                schema: "dbo",
                table: "INV_PriceListItemHistory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_PriceListItemHistory_UnitId",
                schema: "dbo",
                table: "INV_PriceListItemHistory",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_PriceListItemHistory_PublicId",
                schema: "dbo",
                table: "INV_PriceListItemHistory",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INV_DocumentFieldValues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PriceListItemHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_DocumentFieldDefinitions",
                schema: "dbo");
        }
    }
}
