using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CargosMasivos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BulkChargeLineId",
                schema: "dbo",
                table: "LND_Loans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowsBulkCharge",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "BulkChargeDefaultAmount",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BulkChargeDefaultInstallments",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BulkChargeEntersCollection",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BulkChargeReportsToBureaus",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LND_BulkChargeRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    ChargeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Installments = table.Column<int>(type: "integer", nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    ChannelMode = table.Column<int>(type: "integer", nullable: false),
                    RepaymentChannel = table.Column<int>(type: "integer", nullable: true),
                    PaymentCycle = table.Column<int>(type: "integer", nullable: true),
                    FirstDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CounterpartMode = table.Column<int>(type: "integer", nullable: false),
                    CounterpartPersonId = table.Column<int>(type: "integer", nullable: true),
                    AmortizationSystem = table.Column<int>(type: "integer", nullable: true),
                    Insurances = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    FiltersJson = table.Column<string>(type: "text", nullable: false),
                    PreviewSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalLines = table.Column<int>(type: "integer", nullable: false),
                    PendingCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    VoidedCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    VoidProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    PreparedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BatchPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchNumber = table.Column<long>(type: "bigint", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VoidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_BulkChargeRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeRuns_COR_People_CounterpartPersonId",
                        column: x => x.CounterpartPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeRuns_LND_CreditLineVersions_CreditLineVersion~",
                        column: x => x.CreditLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeRuns_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeRuns_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeRuns_LND_ProcessRuns_VoidProcessRunId",
                        column: x => x.VoidProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeRuns_SEC_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeRuns_SEC_Users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeRuns_SEC_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_BulkChargeLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Installments = table.Column<int>(type: "integer", nullable: false),
                    FirstDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RepaymentChannel = table.Column<int>(type: "integer", nullable: false),
                    PayrollAgreementId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ReasonDetail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Warning = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    SourceRow = table.Column<int>(type: "integer", nullable: true),
                    HoldsPeriod = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_BulkChargeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeLines_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeLines_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeLines_LND_BulkChargeRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "dbo",
                        principalTable: "LND_BulkChargeRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeLines_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeLines_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BulkChargeLines_LND_PayrollAgreements_PayrollAgreementId",
                        column: x => x.PayrollAgreementId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_BulkChargeLineId",
                schema: "dbo",
                table: "LND_Loans",
                column: "BulkChargeLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeLines_BranchId",
                schema: "dbo",
                table: "LND_BulkChargeLines",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeLines_LoanId",
                schema: "dbo",
                table: "LND_BulkChargeLines",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeLines_PayrollAgreementId",
                schema: "dbo",
                table: "LND_BulkChargeLines",
                column: "PayrollAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeLines_PersonId",
                schema: "dbo",
                table: "LND_BulkChargeLines",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeLines_Run_Status",
                schema: "dbo",
                table: "LND_BulkChargeLines",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_BulkChargeLines_Line_Person_Period",
                schema: "dbo",
                table: "LND_BulkChargeLines",
                columns: new[] { "CreditLineId", "PersonId", "Period" },
                unique: true,
                filter: "\"HoldsPeriod\" = TRUE AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_BulkChargeLines_PublicId",
                schema: "dbo",
                table: "LND_BulkChargeLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_ConfirmedByUserId",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_CounterpartPersonId",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "CounterpartPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_CreditLineVersionId",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "CreditLineVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_Line_Period",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                columns: new[] { "CreditLineId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_PreparedByUserId",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_ProcessRunId",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_Status",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_VoidedByUserId",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BulkChargeRuns_VoidProcessRunId",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "VoidProcessRunId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_BulkChargeRuns_Number",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_BulkChargeRuns_PublicId",
                schema: "dbo",
                table: "LND_BulkChargeRuns",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_Loans_LND_BulkChargeLines_BulkChargeLineId",
                schema: "dbo",
                table: "LND_Loans",
                column: "BulkChargeLineId",
                principalSchema: "dbo",
                principalTable: "LND_BulkChargeLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LND_Loans_LND_BulkChargeLines_BulkChargeLineId",
                schema: "dbo",
                table: "LND_Loans");

            migrationBuilder.DropTable(
                name: "LND_BulkChargeLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_BulkChargeRuns",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_LND_Loans_BulkChargeLineId",
                schema: "dbo",
                table: "LND_Loans");

            migrationBuilder.DropColumn(
                name: "BulkChargeLineId",
                schema: "dbo",
                table: "LND_Loans");

            migrationBuilder.DropColumn(
                name: "AllowsBulkCharge",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropColumn(
                name: "BulkChargeDefaultAmount",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropColumn(
                name: "BulkChargeDefaultInstallments",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropColumn(
                name: "BulkChargeEntersCollection",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropColumn(
                name: "BulkChargeReportsToBureaus",
                schema: "dbo",
                table: "LND_CreditLineVersions");
        }
    }
}
