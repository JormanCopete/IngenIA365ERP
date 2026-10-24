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
                name: "RCD_CashDisbursements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DocumentTypeId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CashSessionId = table.Column<int>(type: "integer", nullable: false),
                    CashBoxId = table.Column<int>(type: "integer", nullable: false),
                    CollectionPointId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CashierUserId = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryPersonId = table.Column<int>(type: "integer", nullable: false),
                    ReceivedByPersonId = table.Column<int>(type: "integer", nullable: true),
                    ProductPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProductNature = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PayerCreditBalanceId = table.Column<int>(type: "integer", nullable: true),
                    MiscConceptId = table.Column<int>(type: "integer", nullable: true),
                    ModuleAuthorizationPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleAuthorizationNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GmfAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    GmfByOrigin = table.Column<bool>(type: "boolean", nullable: false),
                    SignersJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SignatureAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OperationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VerificationCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashDisbursements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_COR_People_ReceivedByPersonId",
                        column: x => x.ReceivedByPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_CollectionPoints_CollectionPointId",
                        column: x => x.CollectionPointId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "RCD_DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_MiscConcepts_MiscConceptId",
                        column: x => x.MiscConceptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_MiscConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_RCD_PayerCreditBalances_PayerCreditBa~",
                        column: x => x.PayerCreditBalanceId,
                        principalSchema: "dbo",
                        principalTable: "RCD_PayerCreditBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursements_SEC_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashTransactionReports",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ApplicationState = table.Column<int>(type: "integer", nullable: false),
                    LayoutCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LayoutVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LayoutVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UploadReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UploadSupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    IssuesJson = table.Column<string>(type: "text", nullable: false),
                    BlockingIssues = table.Column<int>(type: "integer", nullable: false),
                    Warnings = table.Column<int>(type: "integer", nullable: false),
                    IsAbsenceReport = table.Column<bool>(type: "boolean", nullable: false),
                    Lines = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedByUserId = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_RCD_CashTransactionReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashTransactionReports_SEC_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashTransactionReports_SEC_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_IdentificationRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Criterion = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ApplyOnLoad = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_RCD_IdentificationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RCD_PaymasterCalendar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymasterPersonId = table.Column<int>(type: "integer", nullable: false),
                    PeriodYear = table.Column<int>(type: "integer", nullable: false),
                    PeriodMonth = table.Column<int>(type: "integer", nullable: false),
                    SubPeriod = table.Column<int>(type: "integer", nullable: false),
                    PayrollPayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BusinessDays = table.Column<int>(type: "integer", nullable: false),
                    LegalRemittanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RCD_PaymasterCalendar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PaymasterCalendar_COR_People_PaymasterPersonId",
                        column: x => x.PaymasterPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_PaymasterCalendar_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "RCD_ReportedDeposits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    ProductPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedImmediately = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    StatementLinePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "integer", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_RCD_ReportedDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_ReportedDeposits_SEC_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashDisbursementVoidings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashDisbursementId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CashSessionId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleReversalPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModuleReversalNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerificationCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashDisbursementVoidings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursementVoidings_RCD_CashDisbursements_CashDisb~",
                        column: x => x.CashDisbursementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashDisbursements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursementVoidings_RCD_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursementVoidings_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashDisbursementVoidings_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CashTransactionReportLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashTransactionReportId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DocumentsJson = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_RCD_CashTransactionReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CashTransactionReportLines_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CashTransactionReportLines_RCD_CashTransactionReports_C~",
                        column: x => x.CashTransactionReportId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CashTransactionReports",
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
                name: "IX_RCD_CashDisbursements_BeneficiaryPersonId_OperationDate",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                columns: new[] { "BeneficiaryPersonId", "OperationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_BranchId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_CashBoxId_OperationDate",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                columns: new[] { "CashBoxId", "OperationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_CashierUserId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_CashSessionId_Status",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                columns: new[] { "CashSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_CollectionPointId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "CollectionPointId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_MiscConceptId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "MiscConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_PayerCreditBalanceId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "PayerCreditBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursements_ReceivedByPersonId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "ReceivedByPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursements_DocumentTypeId_Number",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                columns: new[] { "DocumentTypeId", "Number" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursements_OperationKey",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "OperationKey",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursements_PublicId",
                schema: "dbo",
                table: "RCD_CashDisbursements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursementVoidings_ApprovedByUserId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursementVoidings_CashSessionId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashDisbursementVoidings_RequestedByUserId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursementVoidings_CashDisbursementId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "CashDisbursementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashDisbursementVoidings_PublicId",
                schema: "dbo",
                table: "RCD_CashDisbursementVoidings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashTransactionReportLines_PersonId",
                schema: "dbo",
                table: "RCD_CashTransactionReportLines",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashTransactionReportLines_PublicId",
                schema: "dbo",
                table: "RCD_CashTransactionReportLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashTransactionReportLines_Report_Line",
                schema: "dbo",
                table: "RCD_CashTransactionReportLines",
                columns: new[] { "CashTransactionReportId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashTransactionReports_GeneratedByUserId",
                schema: "dbo",
                table: "RCD_CashTransactionReports",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CashTransactionReports_UploadedByUserId",
                schema: "dbo",
                table: "RCD_CashTransactionReports",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashTransactionReports_Period_Version",
                schema: "dbo",
                table: "RCD_CashTransactionReports",
                columns: new[] { "PeriodYear", "PeriodMonth", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CashTransactionReports_PublicId",
                schema: "dbo",
                table: "RCD_CashTransactionReports",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_IdentificationRules_IsActive_Priority",
                schema: "dbo",
                table: "RCD_IdentificationRules",
                columns: new[] { "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "UK_RCD_IdentificationRules_Code",
                schema: "dbo",
                table: "RCD_IdentificationRules",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_IdentificationRules_PublicId",
                schema: "dbo",
                table: "RCD_IdentificationRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PaymasterCalendar_LegalRemittanceDate",
                schema: "dbo",
                table: "RCD_PaymasterCalendar",
                column: "LegalRemittanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PaymasterCalendar_RegisteredByUserId",
                schema: "dbo",
                table: "RCD_PaymasterCalendar",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PaymasterCalendar_Paymaster_Period",
                schema: "dbo",
                table: "RCD_PaymasterCalendar",
                columns: new[] { "PaymasterPersonId", "PeriodYear", "PeriodMonth", "SubPeriod" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PaymasterCalendar_PublicId",
                schema: "dbo",
                table: "RCD_PaymasterCalendar",
                column: "PublicId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_Account_Status_Date",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                columns: new[] { "OwnBankAccountPublicId", "Status", "DepositDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_BranchId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_PersonId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_ReceiptId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_RegisteredByUserId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_StatementLinePublicId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "StatementLinePublicId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReportedDeposits_VerifiedByUserId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReportedDeposits_Number",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "Number",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReportedDeposits_PublicId",
                schema: "dbo",
                table: "RCD_ReportedDeposits",
                column: "PublicId",
                unique: true);

            // Feature 015, R1 (fase 26, T0373): el tipo de documento del egreso de caja (clase 7). Idempotente: no repite el código ni ocupa una clase ya tomada.
            migrationBuilder.Sql(@"
INSERT INTO dbo.""RCD_DocumentTypes"" (""PublicId"", ""Code"", ""Name"", ""Kind"", ""UsesPointPrefix"", ""Status"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT gen_random_uuid(), v.code, v.name, v.kind, FALSE, 1, FALSE, now(), 'SYSTEM'
FROM (VALUES ('EG', 'Egreso de caja', 7)) AS v(code, name, kind)
WHERE NOT EXISTS (SELECT 1 FROM dbo.""RCD_DocumentTypes"" t WHERE t.""Code"" = v.code AND t.""IsDeleted"" = FALSE)
  AND NOT EXISTS (SELECT 1 FROM dbo.""RCD_DocumentTypes"" t WHERE t.""Kind"" = v.kind AND t.""Status"" = 1 AND t.""IsDeleted"" = FALSE);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCD_CashDisbursementVoidings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashTransactionReportLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_IdentificationRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PaymasterCalendar",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PendingApplicationResolutions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ReportedDeposits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashDisbursements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CashTransactionReports",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PendingApplications",
                schema: "dbo");
        }
    }
}
