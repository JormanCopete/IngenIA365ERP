using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class TesoreriaConciliacionMinima : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TES_Reconciliations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ConciliatorUserId = table.Column<int>(type: "int", nullable: false),
                    StatementBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LedgerBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReconcilingItemsTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReportAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReopenedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousReconciliationId = table.Column<int>(type: "int", nullable: true),
                    OutdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutdatedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TES_Reconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_Reconciliations_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Reconciliations_SEC_Users_ConciliatorUserId",
                        column: x => x.ConciliatorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Reconciliations_SEC_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Reconciliations_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_Reconciliations_TES_Reconciliations_PreviousReconciliationId",
                        column: x => x.PreviousReconciliationId,
                        principalSchema: "dbo",
                        principalTable: "TES_Reconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_ReconciliationMatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReconciliationId = table.Column<int>(type: "int", nullable: false),
                    StatementLineId = table.Column<long>(type: "bigint", nullable: false),
                    RecordKind = table.Column<int>(type: "int", nullable: false),
                    RecordPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Rule = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GroupKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchedByUserId = table.Column<int>(type: "int", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLive = table.Column<bool>(type: "bit", nullable: false),
                    UndoneByUserId = table.Column<int>(type: "int", nullable: true),
                    UndoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_TES_ReconciliationMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_ReconciliationMatches_SEC_Users_MatchedByUserId",
                        column: x => x.MatchedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_ReconciliationMatches_SEC_Users_UndoneByUserId",
                        column: x => x.UndoneByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_ReconciliationMatches_TES_BankStatementLines_StatementLineId",
                        column: x => x.StatementLineId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankStatementLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_ReconciliationMatches_TES_Reconciliations_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalSchema: "dbo",
                        principalTable: "TES_Reconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_ReconcilingItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReconciliationId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Since = table.Column<DateOnly>(type: "date", nullable: false),
                    SourcePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StatementLineId = table.Column<long>(type: "bigint", nullable: true),
                    UnidentifiedItemPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AlertPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_TES_ReconcilingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_ReconcilingItems_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_ReconcilingItems_TES_BankStatementLines_StatementLineId",
                        column: x => x.StatementLineId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankStatementLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_ReconcilingItems_TES_Reconciliations_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalSchema: "dbo",
                        principalTable: "TES_Reconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReconciliationMatches_MatchedByUserId",
                schema: "dbo",
                table: "TES_ReconciliationMatches",
                column: "MatchedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReconciliationMatches_ReconciliationId_StatementLineId",
                schema: "dbo",
                table: "TES_ReconciliationMatches",
                columns: new[] { "ReconciliationId", "StatementLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReconciliationMatches_StatementLineId",
                schema: "dbo",
                table: "TES_ReconciliationMatches",
                column: "StatementLineId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReconciliationMatches_UndoneByUserId",
                schema: "dbo",
                table: "TES_ReconciliationMatches",
                column: "UndoneByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_ReconciliationMatches_PublicId",
                schema: "dbo",
                table: "TES_ReconciliationMatches",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_ReconciliationMatches_Reconciliation_Record_Live",
                schema: "dbo",
                table: "TES_ReconciliationMatches",
                columns: new[] { "ReconciliationId", "RecordKind", "RecordPublicId" },
                unique: true,
                filter: "[IsLive] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Reconciliations_ClosedByUserId",
                schema: "dbo",
                table: "TES_Reconciliations",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Reconciliations_ConciliatorUserId",
                schema: "dbo",
                table: "TES_Reconciliations",
                column: "ConciliatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Reconciliations_PeriodYear_PeriodMonth_Status",
                schema: "dbo",
                table: "TES_Reconciliations",
                columns: new[] { "PeriodYear", "PeriodMonth", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_Reconciliations_PreviousReconciliationId",
                schema: "dbo",
                table: "TES_Reconciliations",
                column: "PreviousReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_Reconciliations_ReopenedByUserId",
                schema: "dbo",
                table: "TES_Reconciliations",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_Reconciliations_Account_Period_Version",
                schema: "dbo",
                table: "TES_Reconciliations",
                columns: new[] { "BankAccountId", "PeriodYear", "PeriodMonth", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_Reconciliations_PublicId",
                schema: "dbo",
                table: "TES_Reconciliations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReconcilingItems_CreatedByUserId",
                schema: "dbo",
                table: "TES_ReconcilingItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReconcilingItems_ReconciliationId",
                schema: "dbo",
                table: "TES_ReconcilingItems",
                column: "ReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReconcilingItems_StatementLineId",
                schema: "dbo",
                table: "TES_ReconcilingItems",
                column: "StatementLineId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_ReconcilingItems_PublicId",
                schema: "dbo",
                table: "TES_ReconcilingItems",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TES_ReconciliationMatches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_ReconcilingItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_Reconciliations",
                schema: "dbo");
        }
    }
}
