using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CarteraPerdidaEsperada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DaysInDefault",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedLossScore",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                type: "decimal(28,18)",
                precision: 28,
                scale: 18,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedLossZ",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                type: "decimal(28,18)",
                precision: 28,
                scale: 18,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncompleteArrearsHistory",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInDefault",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LossGivenDefaultCollateralType",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProbabilityOfDefaultKey",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LND_ArrearsHistoryLoads",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationLoadId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppliedByUserId = table.Column<int>(type: "int", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CoverageJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_LND_ArrearsHistoryLoads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ArrearsHistoryLoads_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ArrearsHistoryLoads_SEC_Users_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_ImpairmentModelElections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    SegmentCodes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SessionItemId = table.Column<int>(type: "int", nullable: false),
                    ImpactStudyAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupervisorNoticeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupervisorNoticeAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeLeadDays = table.Column<int>(type: "int", nullable: false),
                    RegulatoryDocumentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncompleteHistoryCheckJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_LND_ImpairmentModelElections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ImpairmentModelElections_LND_SessionItems_SessionItemId",
                        column: x => x.SessionItemId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ImpairmentModelElections_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanArrearsHistory_ArrearsHistoryLoadId",
                schema: "dbo",
                table: "LND_LoanArrearsHistory",
                column: "ArrearsHistoryLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ArrearsHistoryLoads_AppliedByUserId",
                schema: "dbo",
                table: "LND_ArrearsHistoryLoads",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ArrearsHistoryLoads_MigrationLoadId",
                schema: "dbo",
                table: "LND_ArrearsHistoryLoads",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ArrearsHistoryLoads_PublicId",
                schema: "dbo",
                table: "LND_ArrearsHistoryLoads",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ImpairmentModelElections_RegisteredByUserId",
                schema: "dbo",
                table: "LND_ImpairmentModelElections",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ImpairmentModelElections_SessionItemId",
                schema: "dbo",
                table: "LND_ImpairmentModelElections",
                column: "SessionItemId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ImpairmentModelElections_PublicId",
                schema: "dbo",
                table: "LND_ImpairmentModelElections",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ImpairmentModelElections_ValidFrom",
                schema: "dbo",
                table: "LND_ImpairmentModelElections",
                column: "ValidFrom",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanArrearsHistory_LND_ArrearsHistoryLoads_ArrearsHistoryLoadId",
                schema: "dbo",
                table: "LND_LoanArrearsHistory",
                column: "ArrearsHistoryLoadId",
                principalSchema: "dbo",
                principalTable: "LND_ArrearsHistoryLoads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanArrearsHistory_LND_ArrearsHistoryLoads_ArrearsHistoryLoadId",
                schema: "dbo",
                table: "LND_LoanArrearsHistory");

            migrationBuilder.DropTable(
                name: "LND_ArrearsHistoryLoads",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ImpairmentModelElections",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanArrearsHistory_ArrearsHistoryLoadId",
                schema: "dbo",
                table: "LND_LoanArrearsHistory");

            migrationBuilder.DropColumn(
                name: "DaysInDefault",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans");

            migrationBuilder.DropColumn(
                name: "ExpectedLossScore",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans");

            migrationBuilder.DropColumn(
                name: "ExpectedLossZ",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans");

            migrationBuilder.DropColumn(
                name: "IncompleteArrearsHistory",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans");

            migrationBuilder.DropColumn(
                name: "IsInDefault",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans");

            migrationBuilder.DropColumn(
                name: "LossGivenDefaultCollateralType",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans");

            migrationBuilder.DropColumn(
                name: "ProbabilityOfDefaultKey",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans");
        }
    }
}
