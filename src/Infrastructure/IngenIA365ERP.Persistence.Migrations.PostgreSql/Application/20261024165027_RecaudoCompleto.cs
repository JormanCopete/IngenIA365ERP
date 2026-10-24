using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class RecaudoCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RCD_PendingApplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: false),
                    ReceiptApplicationId = table.Column<long>(type: "bigint", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    TargetModule = table.Column<int>(type: "integer", nullable: false),
                    LoadObligationPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductReference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_PendingApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplications_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplications_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplications_RCD_ReceiptApplications_ReceiptAppl~",
                        column: x => x.ReceiptApplicationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_ReceiptApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplications_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_PendingApplicationResolutions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PendingApplicationId = table.Column<int>(type: "integer", nullable: false),
                    ProductPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Modality = table.Column<int>(type: "integer", nullable: false),
                    PrepaymentChoice = table.Column<int>(type: "integer", nullable: true),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ModuleOperationPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleOperationNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_RCD_PendingApplicationResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplicationResolutions_RCD_PendingApplications_P~",
                        column: x => x.PendingApplicationId,
                        principalSchema: "dbo",
                        principalTable: "RCD_PendingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PendingApplicationResolutions_SEC_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PendingApplicationResolutions_ResolvedByUserId",
                schema: "dbo",
                table: "RCD_PendingApplicationResolutions",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PendingApplicationResolutions_PendingApplicationId",
                schema: "dbo",
                table: "RCD_PendingApplicationResolutions",
                column: "PendingApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PendingApplicationResolutions_PublicId",
                schema: "dbo",
                table: "RCD_PendingApplicationResolutions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PendingApplications_BranchId",
                schema: "dbo",
                table: "RCD_PendingApplications",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PendingApplications_PersonId_Status",
                schema: "dbo",
                table: "RCD_PendingApplications",
                columns: new[] { "PersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PendingApplications_ReceiptId",
                schema: "dbo",
                table: "RCD_PendingApplications",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PendingApplications_PublicId",
                schema: "dbo",
                table: "RCD_PendingApplications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PendingApplications_ReceiptApplicationId",
                schema: "dbo",
                table: "RCD_PendingApplications",
                column: "ReceiptApplicationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCD_PendingApplicationResolutions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PendingApplications",
                schema: "dbo");
        }
    }
}
