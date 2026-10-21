using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CarteraInformes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LND_RegulatoryFiles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    BureauCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "smallint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LayoutCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LayoutHasUnverifiedFields = table.Column<bool>(type: "boolean", nullable: false),
                    MonthlyCloseId = table.Column<int>(type: "integer", nullable: true),
                    ObligatoryModelUnavailable = table.Column<bool>(type: "boolean", nullable: false),
                    IssuesJson = table.Column<string>(type: "text", nullable: false),
                    BlockingIssueCount = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedJson = table.Column<string>(type: "text", nullable: true),
                    ExcludedCount = table.Column<int>(type: "integer", nullable: false),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    TotalsJson = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedByUserId = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: true),
                    FilingNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FilingDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_RegulatoryFiles", x => x.Id);
                    table.CheckConstraint("CK_LND_RegulatoryFiles_BureauCode", "(\"Kind\" = 1 AND \"BureauCode\" IS NULL) OR (\"Kind\" = 2 AND \"BureauCode\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_RegulatoryFiles_LND_MonthlyCloses_MonthlyCloseId",
                        column: x => x.MonthlyCloseId,
                        principalSchema: "dbo",
                        principalTable: "LND_MonthlyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RegulatoryFiles_SEC_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RegulatoryFiles_SEC_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_RegulatoryFileLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegulatoryFileId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Included = table.Column<bool>(type: "boolean", nullable: false),
                    ExclusionReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ReportedStatusCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DaysPastDue = table.Column<int>(type: "integer", nullable: false),
                    BalanceReported = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsNegative = table.Column<bool>(type: "boolean", nullable: false),
                    EpisodeStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PriorNoticeId = table.Column<int>(type: "integer", nullable: true),
                    HasClaimInProcess = table.Column<bool>(type: "boolean", nullable: false),
                    IsWithdrawalOfUnnoticedReport = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LND_RegulatoryFileLines", x => x.Id);
                    table.CheckConstraint("CK_LND_RegulatoryFileLines_ExclusionReason", "\"Included\" = TRUE OR \"ExclusionReason\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_LND_RegulatoryFileLines_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RegulatoryFileLines_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RegulatoryFileLines_LND_PriorNotices_PriorNoticeId",
                        column: x => x.PriorNoticeId,
                        principalSchema: "dbo",
                        principalTable: "LND_PriorNotices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RegulatoryFileLines_LND_RegulatoryFiles_RegulatoryFileId",
                        column: x => x.RegulatoryFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_RegulatoryFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_RegulatoryFileLines_Loan_Person",
                schema: "dbo",
                table: "LND_RegulatoryFileLines",
                columns: new[] { "LoanId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_RegulatoryFileLines_PersonId",
                schema: "dbo",
                table: "LND_RegulatoryFileLines",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RegulatoryFileLines_PriorNoticeId",
                schema: "dbo",
                table: "LND_RegulatoryFileLines",
                column: "PriorNoticeId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_RegulatoryFileLines_File_Line",
                schema: "dbo",
                table: "LND_RegulatoryFileLines",
                columns: new[] { "RegulatoryFileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_RegulatoryFileLines_File_Loan_Person",
                schema: "dbo",
                table: "LND_RegulatoryFileLines",
                columns: new[] { "RegulatoryFileId", "LoanId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_RegulatoryFileLines_PublicId",
                schema: "dbo",
                table: "LND_RegulatoryFileLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_RegulatoryFiles_GeneratedByUserId",
                schema: "dbo",
                table: "LND_RegulatoryFiles",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RegulatoryFiles_MonthlyCloseId",
                schema: "dbo",
                table: "LND_RegulatoryFiles",
                column: "MonthlyCloseId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RegulatoryFiles_UploadedByUserId",
                schema: "dbo",
                table: "LND_RegulatoryFiles",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_RegulatoryFiles_Bureau_Period_Version",
                schema: "dbo",
                table: "LND_RegulatoryFiles",
                columns: new[] { "BureauCode", "PeriodYear", "PeriodMonth", "Version" },
                unique: true,
                filter: "\"Kind\" = 2 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_RegulatoryFiles_PublicId",
                schema: "dbo",
                table: "LND_RegulatoryFiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_RegulatoryFiles_Sicses_Period_Version",
                schema: "dbo",
                table: "LND_RegulatoryFiles",
                columns: new[] { "PeriodYear", "PeriodMonth", "Version" },
                unique: true,
                filter: "\"Kind\" = 1 AND \"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LND_RegulatoryFileLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_RegulatoryFiles",
                schema: "dbo");
        }
    }
}
