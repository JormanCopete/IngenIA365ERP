using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class RecaudoCanales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClearingBatchId",
                schema: "dbo",
                table: "RCD_ReceivedChecks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByStatementLinePublicId",
                schema: "dbo",
                table: "RCD_ReceivedChecks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RCD_ClearingBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    RemittancePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepositedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatementLinePublicIds = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_ClearingBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ClearingBatches_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ClearingBatches_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceivedChecks_ClearingBatchId",
                schema: "dbo",
                table: "RCD_ReceivedChecks",
                column: "ClearingBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ClearingBatches_Account_Status",
                schema: "dbo",
                table: "RCD_ClearingBatches",
                columns: new[] { "OwnBankAccountPublicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ClearingBatches_BranchId",
                schema: "dbo",
                table: "RCD_ClearingBatches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ClearingBatches_CreatedByUserId",
                schema: "dbo",
                table: "RCD_ClearingBatches",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ClearingBatches_Number",
                schema: "dbo",
                table: "RCD_ClearingBatches",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ClearingBatches_PublicId",
                schema: "dbo",
                table: "RCD_ClearingBatches",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RCD_ReceivedChecks_RCD_ClearingBatches_ClearingBatchId",
                schema: "dbo",
                table: "RCD_ReceivedChecks",
                column: "ClearingBatchId",
                principalSchema: "dbo",
                principalTable: "RCD_ClearingBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RCD_ReceivedChecks_RCD_ClearingBatches_ClearingBatchId",
                schema: "dbo",
                table: "RCD_ReceivedChecks");

            migrationBuilder.DropTable(
                name: "RCD_ClearingBatches",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_RCD_ReceivedChecks_ClearingBatchId",
                schema: "dbo",
                table: "RCD_ReceivedChecks");

            migrationBuilder.DropColumn(
                name: "ClearingBatchId",
                schema: "dbo",
                table: "RCD_ReceivedChecks");

            migrationBuilder.DropColumn(
                name: "PaidByStatementLinePublicId",
                schema: "dbo",
                table: "RCD_ReceivedChecks");
        }
    }
}
