using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
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
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByStatementLinePublicId",
                schema: "dbo",
                table: "RCD_ReceivedChecks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RCD_ClearingBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    RemittancePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepositedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckCount = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatementLinePublicIds = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
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
