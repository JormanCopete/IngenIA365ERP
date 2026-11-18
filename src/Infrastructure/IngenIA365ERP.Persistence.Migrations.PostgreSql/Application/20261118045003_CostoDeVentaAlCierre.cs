using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CostoDeVentaAlCierre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CostAtPeriodClose",
                schema: "dbo",
                table: "INV_Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "INV_PeriodCostPostings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodId = table.Column<int>(type: "integer", nullable: false),
                    CloseVersion = table.Column<int>(type: "integer", nullable: false),
                    IsReversal = table.Column<bool>(type: "boolean", nullable: false),
                    ReversesId = table.Column<int>(type: "integer", nullable: true),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryCount = table.Column<int>(type: "integer", nullable: false),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_INV_PeriodCostPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_PeriodCostPostings_INV_Periods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "dbo",
                        principalTable: "INV_Periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INV_DeferredCosts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceDocumentId = table.Column<int>(type: "integer", nullable: false),
                    CostDocumentId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "smallint", nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    BranchPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CostCenterPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodCostPostingId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_INV_DeferredCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_DeferredCosts_INV_Documents_CostDocumentId",
                        column: x => x.CostDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DeferredCosts_INV_Documents_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalSchema: "dbo",
                        principalTable: "INV_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_DeferredCosts_INV_PeriodCostPostings_PeriodCostPostingId",
                        column: x => x.PeriodCostPostingId,
                        principalSchema: "dbo",
                        principalTable: "INV_PeriodCostPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_DeferredCosts_CostDocument",
                schema: "dbo",
                table: "INV_DeferredCosts",
                column: "CostDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DeferredCosts_PeriodCostPostingId",
                schema: "dbo",
                table: "INV_DeferredCosts",
                column: "PeriodCostPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DeferredCosts_SourceDocumentId",
                schema: "dbo",
                table: "INV_DeferredCosts",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_DeferredCosts_Year_Month_Posting",
                schema: "dbo",
                table: "INV_DeferredCosts",
                columns: new[] { "Year", "Month", "PeriodCostPostingId" });

            migrationBuilder.CreateIndex(
                name: "UK_INV_DeferredCosts_PublicId",
                schema: "dbo",
                table: "INV_DeferredCosts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_PeriodCostPostings_Period_Version_Reversal",
                schema: "dbo",
                table: "INV_PeriodCostPostings",
                columns: new[] { "PeriodId", "CloseVersion", "IsReversal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_INV_PeriodCostPostings_PublicId",
                schema: "dbo",
                table: "INV_PeriodCostPostings",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INV_DeferredCosts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "INV_PeriodCostPostings",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "CostAtPeriodClose",
                schema: "dbo",
                table: "INV_Documents");
        }
    }
}
