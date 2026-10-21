using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class CarteraLibranza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayrollAgreementId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_LoanOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollDeductionFileId",
                schema: "dbo",
                table: "LND_InboundMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollAgreementId",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayrollEmployeeIdentifier",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LND_PayrollAgreements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PaymasterPersonId = table.Column<int>(type: "int", nullable: false),
                    IsOwnPayroll = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreements_COR_People_PaymasterPersonId",
                        column: x => x.PaymasterPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollAgreementContacts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollAgreementId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollAgreementContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementContacts_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementContacts_LND_PayrollAgreements_PayrollAgreementId",
                        column: x => x.PayrollAgreementId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollAgreementVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollAgreementId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Periodicity = table.Column<int>(type: "int", nullable: false),
                    CutoffDay = table.Column<byte>(type: "tinyint", nullable: false),
                    PaymentDay = table.Column<byte>(type: "tinyint", nullable: false),
                    SendLeadDays = table.Column<int>(type: "int", nullable: false),
                    OutboundFormatId = table.Column<int>(type: "int", nullable: false),
                    ResultFormatId = table.Column<int>(type: "int", nullable: false),
                    DeductionCapFraction = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    IncludesPastDue = table.Column<bool>(type: "bit", nullable: false),
                    NoveltyRulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransferWindowDays = table.Column<int>(type: "int", nullable: true),
                    NonDeductionAction = table.Column<int>(type: "int", nullable: false),
                    AlternateRepaymentChannel = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollAgreementVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementVersions_COR_BankFileFormats_OutboundFormatId",
                        column: x => x.OutboundFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementVersions_COR_BankFileFormats_ResultFormatId",
                        column: x => x.ResultFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementVersions_LND_PayrollAgreements_PayrollAgreementId",
                        column: x => x.PayrollAgreementId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollAgreementResultCodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollAgreementVersionId = table.Column<int>(type: "int", nullable: false),
                    PaymasterCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    NonDeductionReason = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollAgreementResultCodes", x => x.Id);
                    table.CheckConstraint("CK_LND_PayrollAgreementResultCodes_Reason", "([Outcome] = 1 AND [NonDeductionReason] IS NULL) OR ([Outcome] <> 1 AND [NonDeductionReason] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementResultCodes_LND_PayrollAgreementVersions_PayrollAgreementVersionId",
                        column: x => x.PayrollAgreementVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreementVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionFiles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollAgreementId = table.Column<int>(type: "int", nullable: false),
                    PayrollAgreementVersionId = table.Column<int>(type: "int", nullable: false),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "tinyint", nullable: false),
                    SubPeriodNumber = table.Column<byte>(type: "tinyint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OutboundFormatId = table.Column<int>(type: "int", nullable: false),
                    BaseFileId = table.Column<int>(type: "int", nullable: true),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewCount = table.Column<int>(type: "int", nullable: false),
                    ChangeCount = table.Column<int>(type: "int", nullable: false),
                    CancellationCount = table.Column<int>(type: "int", nullable: false),
                    UnchangedCount = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedByUserId = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryMessagePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupersededByFileId = table.Column<int>(type: "int", nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentConfirmationMessagePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymasterReference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedByUserId = table.Column<int>(type: "int", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FirstResultAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollDeductionFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFiles_COR_BankFileFormats_OutboundFormatId",
                        column: x => x.OutboundFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFiles_LND_PayrollAgreementVersions_PayrollAgreementVersionId",
                        column: x => x.PayrollAgreementVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreementVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFiles_LND_PayrollAgreements_PayrollAgreementId",
                        column: x => x.PayrollAgreementId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFiles_LND_PayrollDeductionFiles_BaseFileId",
                        column: x => x.BaseFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFiles_LND_PayrollDeductionFiles_SupersededByFileId",
                        column: x => x.SupersededByFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFiles_SEC_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFiles_SEC_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_NonDeductionFollowUps",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstallmentNonDeductionId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    IsAutomatic = table.Column<bool>(type: "bit", nullable: false),
                    DebtorMessageId = table.Column<int>(type: "int", nullable: true),
                    PayrollDeductionFileId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_LND_NonDeductionFollowUps", x => x.Id);
                    table.CheckConstraint("CK_LND_NonDeductionFollowUps_Reason", "[IsAutomatic] = 1 OR [Reason] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_LND_NonDeductionFollowUps_LND_DebtorMessages_DebtorMessageId",
                        column: x => x.DebtorMessageId,
                        principalSchema: "dbo",
                        principalTable: "LND_DebtorMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_NonDeductionFollowUps_LND_InstallmentNonDeductions_InstallmentNonDeductionId",
                        column: x => x.InstallmentNonDeductionId,
                        principalSchema: "dbo",
                        principalTable: "LND_InstallmentNonDeductions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_NonDeductionFollowUps_LND_PayrollDeductionFiles_PayrollDeductionFileId",
                        column: x => x.PayrollDeductionFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_NonDeductionFollowUps_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionFileLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollDeductionFileId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    LoanPayrollLinkId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    EmployeeIdAtPaymaster = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RepaymentInstallmentId = table.Column<long>(type: "bigint", nullable: true),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PastDueAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NoveltyKind = table.Column<int>(type: "int", nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    LibranzaDate = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_LND_PayrollDeductionFileLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileLines_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileLines_LND_LoanPayrollLinks_LoanPayrollLinkId",
                        column: x => x.LoanPayrollLinkId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanPayrollLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileLines_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileLines_LND_PayrollDeductionFiles_PayrollDeductionFileId",
                        column: x => x.PayrollDeductionFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileLines_LND_RepaymentInstallments_RepaymentInstallmentId",
                        column: x => x.RepaymentInstallmentId,
                        principalSchema: "dbo",
                        principalTable: "LND_RepaymentInstallments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionResults",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollDeductionFileLineId = table.Column<int>(type: "int", nullable: false),
                    PayrollDeductionFileId = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    InboundMessageId = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DeductedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NotDeductedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymasterResultCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NonDeductionReason = table.Column<int>(type: "int", nullable: true),
                    TransferDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymasterArrearsThresholdDays = table.Column<int>(type: "int", nullable: true),
                    SupersedesResultId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollDeductionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionResults_LND_InboundMessages_InboundMessageId",
                        column: x => x.InboundMessageId,
                        principalSchema: "dbo",
                        principalTable: "LND_InboundMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionResults_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionResults_LND_PayrollDeductionFileLines_PayrollDeductionFileLineId",
                        column: x => x.PayrollDeductionFileLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFileLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionResults_LND_PayrollDeductionFiles_PayrollDeductionFileId",
                        column: x => x.PayrollDeductionFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionResults_LND_PayrollDeductionResults_SupersedesResultId",
                        column: x => x.SupersedesResultId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanPayrollLinks_PayrollAgreementId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks",
                column: "PayrollAgreementId",
                filter: "[PayrollAgreementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "PayrollDeductionResultId",
                filter: "[PayrollDeductionResultId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InstallmentNonDeductions_PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                column: "PayrollDeductionResultId",
                unique: true,
                filter: "[PayrollDeductionResultId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_PayrollDeductionFileId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "PayrollDeductionFileId",
                filter: "[PayrollDeductionFileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_PayrollAgreementId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "PayrollAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_NonDeductionFollowUps_ActorUserId",
                schema: "dbo",
                table: "LND_NonDeductionFollowUps",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_NonDeductionFollowUps_DebtorMessageId",
                schema: "dbo",
                table: "LND_NonDeductionFollowUps",
                column: "DebtorMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_NonDeductionFollowUps_NonDeduction_At",
                schema: "dbo",
                table: "LND_NonDeductionFollowUps",
                columns: new[] { "InstallmentNonDeductionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_NonDeductionFollowUps_PayrollDeductionFileId",
                schema: "dbo",
                table: "LND_NonDeductionFollowUps",
                column: "PayrollDeductionFileId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_NonDeductionFollowUps_PublicId",
                schema: "dbo",
                table: "LND_NonDeductionFollowUps",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollAgreementContacts_PersonId",
                schema: "dbo",
                table: "LND_PayrollAgreementContacts",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementContacts_Agr_Person_Role_From",
                schema: "dbo",
                table: "LND_PayrollAgreementContacts",
                columns: new[] { "PayrollAgreementId", "PersonId", "Role", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementContacts_PublicId",
                schema: "dbo",
                table: "LND_PayrollAgreementContacts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementResultCodes_PublicId",
                schema: "dbo",
                table: "LND_PayrollAgreementResultCodes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementResultCodes_Version_Code",
                schema: "dbo",
                table: "LND_PayrollAgreementResultCodes",
                columns: new[] { "PayrollAgreementVersionId", "PaymasterCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollAgreements_PaymasterPersonId",
                schema: "dbo",
                table: "LND_PayrollAgreements",
                column: "PaymasterPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreements_Code",
                schema: "dbo",
                table: "LND_PayrollAgreements",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreements_OwnPayroll",
                schema: "dbo",
                table: "LND_PayrollAgreements",
                column: "IsOwnPayroll",
                unique: true,
                filter: "[IsOwnPayroll] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreements_PublicId",
                schema: "dbo",
                table: "LND_PayrollAgreements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollAgreementVersions_OutboundFormatId",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                column: "OutboundFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollAgreementVersions_ResultFormatId",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                column: "ResultFormatId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementVersions_Agreement_From",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                columns: new[] { "PayrollAgreementId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementVersions_PublicId",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFileLines_LoanId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFileLines_LoanPayrollLinkId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                column: "LoanPayrollLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFileLines_PersonId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFileLines_RepaymentInstallmentId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                column: "RepaymentInstallmentId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFileLines_File_Line",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                columns: new[] { "PayrollDeductionFileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFileLines_File_Loan",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                columns: new[] { "PayrollDeductionFileId", "LoanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFileLines_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFiles_BaseFileId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "BaseFileId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFiles_GeneratedByUserId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFiles_OutboundFormatId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "OutboundFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFiles_PayrollAgreementVersionId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "PayrollAgreementVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFiles_SupersededByFileId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "SupersededByFileId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFiles_VoidedByUserId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Sent",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                columns: new[] { "PayrollAgreementId", "PeriodYear", "PeriodMonth", "SubPeriodNumber" },
                unique: true,
                filter: "[Status] = 3 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Version",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                columns: new[] { "PayrollAgreementId", "PeriodYear", "PeriodMonth", "SubPeriodNumber", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFiles_Number",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFiles_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionResults_FileId",
                schema: "dbo",
                table: "LND_PayrollDeductionResults",
                column: "PayrollDeductionFileId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionResults_InboundMessageId",
                schema: "dbo",
                table: "LND_PayrollDeductionResults",
                column: "InboundMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionResults_Loan_TransferDue",
                schema: "dbo",
                table: "LND_PayrollDeductionResults",
                columns: new[] { "LoanId", "TransferDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionResults_SupersedesResultId",
                schema: "dbo",
                table: "LND_PayrollDeductionResults",
                column: "SupersedesResultId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionResults_Line_Message",
                schema: "dbo",
                table: "LND_PayrollDeductionResults",
                columns: new[] { "PayrollDeductionFileLineId", "InboundMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionResults_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionResults",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplications_LND_PayrollAgreements_PayrollAgreementId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "PayrollAgreementId",
                principalSchema: "dbo",
                principalTable: "LND_PayrollAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InboundMessages_LND_PayrollDeductionFiles_PayrollDeductionFileId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "PayrollDeductionFileId",
                principalSchema: "dbo",
                principalTable: "LND_PayrollDeductionFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InstallmentNonDeductions_LND_PayrollDeductionResults_PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                column: "PayrollDeductionResultId",
                principalSchema: "dbo",
                principalTable: "LND_PayrollDeductionResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanOperations_LND_PayrollDeductionResults_PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "PayrollDeductionResultId",
                principalSchema: "dbo",
                principalTable: "LND_PayrollDeductionResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanPayrollLinks_LND_PayrollAgreements_PayrollAgreementId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks",
                column: "PayrollAgreementId",
                principalSchema: "dbo",
                principalTable: "LND_PayrollAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Feature 013 K2 (T0829): CAR adopta los dos mensajes de Recaudo sobre el archivo de descuentos. Idempotente.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[COR_IntegrationParticipantAdoptions] ([ParticipantId], [ContractName], [Version], [ValidFrom], [ValidTo], [Reason], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT p.[Id], v.[Contrato], CAST(v.[Version] AS smallint), '1900-01-01', NULL, N'Migración CarteraLibranza', NEWID(), 0, SYSUTCDATETIME(), N'SYSTEM'
FROM (VALUES
    (N'EnvioDeDescuentosConfirmado', 1),
    (N'ResultadoDeDescuentos', 1)
) AS v([Contrato], [Version])
JOIN [dbo].[COR_IntegrationParticipants] p
  ON p.[Code] = N'CAR' AND p.[Role] = 2 AND p.[Destination] = N'Lending' AND p.[ValidTo] IS NULL AND p.[IsDeleted] = 0
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[COR_IntegrationParticipantAdoptions] a
    WHERE a.[ParticipantId] = p.[Id] AND a.[ContractName] = v.[Contrato] AND a.[Version] = v.[Version]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM [dbo].[COR_IntegrationParticipantAdoptions] WHERE [Reason] = N'Migración CarteraLibranza';");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditApplications_LND_PayrollAgreements_PayrollAgreementId",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InboundMessages_LND_PayrollDeductionFiles_PayrollDeductionFileId",
                schema: "dbo",
                table: "LND_InboundMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InstallmentNonDeductions_LND_PayrollDeductionResults_PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanOperations_LND_PayrollDeductionResults_PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanPayrollLinks_LND_PayrollAgreements_PayrollAgreementId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks");

            migrationBuilder.DropTable(
                name: "LND_NonDeductionFollowUps",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollAgreementContacts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollAgreementResultCodes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionResults",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionFileLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionFiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollAgreementVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollAgreements",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanPayrollLinks_PayrollAgreementId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanOperations_PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropIndex(
                name: "UK_LND_InstallmentNonDeductions_PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions");

            migrationBuilder.DropIndex(
                name: "IX_LND_InboundMessages_PayrollDeductionFileId",
                schema: "dbo",
                table: "LND_InboundMessages");

            migrationBuilder.DropIndex(
                name: "IX_LND_CreditApplications_PayrollAgreementId",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropColumn(
                name: "PayrollAgreementId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks");

            migrationBuilder.DropColumn(
                name: "PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropColumn(
                name: "PayrollDeductionResultId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions");

            migrationBuilder.DropColumn(
                name: "PayrollDeductionFileId",
                schema: "dbo",
                table: "LND_InboundMessages");

            migrationBuilder.DropColumn(
                name: "PayrollAgreementId",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropColumn(
                name: "PayrollEmployeeIdentifier",
                schema: "dbo",
                table: "LND_CreditApplications");
        }
    }
}
