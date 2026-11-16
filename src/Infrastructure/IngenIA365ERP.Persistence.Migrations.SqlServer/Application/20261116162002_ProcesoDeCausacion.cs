using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class ProcesoDeCausacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LND_AccrualProcesses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Periodicity = table.Column<int>(type: "int", nullable: false),
                    SubPeriod = table.Column<byte>(type: "tinyint", nullable: true),
                    DateFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    DateTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Concepts = table.Column<int>(type: "int", nullable: false),
                    CreditLineId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    PaymasterPersonId = table.Column<int>(type: "int", nullable: true),
                    ReprocessOfId = table.Column<int>(type: "int", nullable: true),
                    ReprocessedById = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CalculationCount = table.Column<int>(type: "int", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalculatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasBlockingIssues = table.Column<bool>(type: "bit", nullable: false),
                    LoanCount = table.Column<int>(type: "int", nullable: false),
                    IncludedFactCount = table.Column<int>(type: "int", nullable: false),
                    ExcludedFactCount = table.Column<int>(type: "int", nullable: false),
                    IncludedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExcludedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovalMethod = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BatchPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatchNumber = table.Column<long>(type: "bigint", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedByUserId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_LND_AccrualProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_COR_People_PaymasterPersonId",
                        column: x => x.PaymasterPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_LND_AccrualProcesses_ReprocessOfId",
                        column: x => x.ReprocessOfId,
                        principalSchema: "dbo",
                        principalTable: "LND_AccrualProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_LND_AccrualProcesses_ReprocessedById",
                        column: x => x.ReprocessedById,
                        principalSchema: "dbo",
                        principalTable: "LND_AccrualProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_SEC_Users_CalculatedByUserId",
                        column: x => x.CalculatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcesses_SEC_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccrualProcessFacts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccrualProcessId = table.Column<int>(type: "int", nullable: false),
                    MessagePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FactType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CausedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    LoanCount = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExclusionReason = table.Column<int>(type: "int", nullable: true),
                    IsBlocking = table.Column<bool>(type: "bit", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_AccrualProcessFacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcessFacts_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcessFacts_LND_AccrualProcesses_AccrualProcessId",
                        column: x => x.AccrualProcessId,
                        principalSchema: "dbo",
                        principalTable: "LND_AccrualProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccrualProcessLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccrualProcessId = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    FactType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Concept = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExclusionReason = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_LND_AccrualProcessLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcessLines_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcessLines_LND_AccrualProcesses_AccrualProcessId",
                        column: x => x.AccrualProcessId,
                        principalSchema: "dbo",
                        principalTable: "LND_AccrualProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcessLines_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccrualProcessLines_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_ApprovalRequestPublicId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "ApprovalRequestPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_ApprovedByUserId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_BranchId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_CalculatedByUserId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "CalculatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_CreatedByUserId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_CreditLineId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_PaymasterPersonId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "PaymasterPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_ReprocessedById",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "ReprocessedById");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_ReprocessOfId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "ReprocessOfId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_RequestedByUserId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_Status_Range",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                columns: new[] { "Status", "DateFrom", "DateTo" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcesses_VoidedByUserId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccrualProcesses_Number",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccrualProcesses_PublicId",
                schema: "dbo",
                table: "LND_AccrualProcesses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcessFacts_BranchId",
                schema: "dbo",
                table: "LND_AccrualProcessFacts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcessFacts_MessagePublicId",
                schema: "dbo",
                table: "LND_AccrualProcessFacts",
                column: "MessagePublicId",
                filter: "[MessagePublicId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcessFacts_Process_Status",
                schema: "dbo",
                table: "LND_AccrualProcessFacts",
                columns: new[] { "AccrualProcessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccrualProcessFacts_PublicId",
                schema: "dbo",
                table: "LND_AccrualProcessFacts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcessLines_BranchId",
                schema: "dbo",
                table: "LND_AccrualProcessLines",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcessLines_CreditLineId",
                schema: "dbo",
                table: "LND_AccrualProcessLines",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcessLines_LoanId",
                schema: "dbo",
                table: "LND_AccrualProcessLines",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualProcessLines_Process_Status",
                schema: "dbo",
                table: "LND_AccrualProcessLines",
                columns: new[] { "AccrualProcessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccrualProcessLines_PublicId",
                schema: "dbo",
                table: "LND_AccrualProcessLines",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LND_AccrualProcessFacts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AccrualProcessLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AccrualProcesses",
                schema: "dbo");
        }
    }
}
