using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class TesoreriaCompleta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "MovedToPayableOn",
                schema: "dbo",
                table: "TES_IssuedChecks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemittancePublicId",
                schema: "dbo",
                table: "INV_DayCloses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TES_MonthEndCheckRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DaysToPayable = table.Column<int>(type: "int", nullable: false),
                    ReclassifiedCount = table.Column<int>(type: "int", nullable: false),
                    ReclassifiedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MovedToPayableCount = table.Column<int>(type: "int", nullable: false),
                    MovedToPayableAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReversalPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunByUserId = table.Column<int>(type: "int", nullable: false),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_TES_MonthEndCheckRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_MonthEndCheckRuns_SEC_Users_RunByUserId",
                        column: x => x.RunByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_MonthEndCheckRuns_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_Transfers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromBankAccountId = table.Column<int>(type: "int", nullable: false),
                    ToBankAccountId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LiquidityFundUseId = table.Column<int>(type: "int", nullable: true),
                    SentByUserId = table.Column<int>(type: "int", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    BankReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ConfirmedByStatementLinePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmationSupportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TES_Transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_SEC_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_SEC_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_TES_BankAccounts_FromBankAccountId",
                        column: x => x.FromBankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_TES_BankAccounts_ToBankAccountId",
                        column: x => x.ToBankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Transfers_TES_LiquidityFundUses_LiquidityFundUseId",
                        column: x => x.LiquidityFundUseId,
                        principalSchema: "dbo",
                        principalTable: "TES_LiquidityFundUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TES_IssuedChecks_MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks",
                column: "MovedToPayableRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_MonthEndCheckRuns_RunByUserId",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                column: "RunByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_MonthEndCheckRuns_Account_Period",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                columns: new[] { "BankAccountId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_MonthEndCheckRuns_Number",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_MonthEndCheckRuns_PublicId",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_MonthEndCheckRuns_ReversalPublicId",
                schema: "dbo",
                table: "TES_MonthEndCheckRuns",
                column: "ReversalPublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_ConfirmedByUserId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_From_Status",
                schema: "dbo",
                table: "TES_Transfers",
                columns: new[] { "FromBankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_LiquidityFundUseId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "LiquidityFundUseId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_RequestedByUserId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_SentByUserId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Transfers_To_Status",
                schema: "dbo",
                table: "TES_Transfers",
                columns: new[] { "ToBankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_TES_Transfers_Number",
                schema: "dbo",
                table: "TES_Transfers",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_Transfers_PublicId",
                schema: "dbo",
                table: "TES_Transfers",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TES_IssuedChecks_TES_MonthEndCheckRuns_MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks",
                column: "MovedToPayableRunId",
                principalSchema: "dbo",
                principalTable: "TES_MonthEndCheckRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TES_IssuedChecks_TES_MonthEndCheckRuns_MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks");

            migrationBuilder.DropTable(
                name: "TES_MonthEndCheckRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_Transfers",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_TES_IssuedChecks_MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks");

            migrationBuilder.DropColumn(
                name: "MovedToPayableOn",
                schema: "dbo",
                table: "TES_IssuedChecks");

            migrationBuilder.DropColumn(
                name: "MovedToPayableRunId",
                schema: "dbo",
                table: "TES_IssuedChecks");

            migrationBuilder.DropColumn(
                name: "RemittancePublicId",
                schema: "dbo",
                table: "INV_DayCloses");
        }
    }
}
