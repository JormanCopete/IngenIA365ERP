using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class TesoreriaNucleo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TES_BankAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BankId = table.Column<int>(type: "integer", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Purposes = table.Column<int>(type: "integer", nullable: false),
                    IsGmfExempt = table.Column<bool>(type: "boolean", nullable: false),
                    BankThirdPartyPersonId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    LastStatementBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LastStatementDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_TES_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_BankAccounts_COR_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankAccounts_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_BankAccounts_COR_People_BankThirdPartyPersonId",
                        column: x => x.BankThirdPartyPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PaymentOrders",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    OriginOrderPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginOrderNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OriginMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginMessageVersion = table.Column<short>(type: "smallint", nullable: true),
                    OrderKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryPersonId = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BeneficiaryTaxIdType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    BeneficiaryTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationBankCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DestinationBankId = table.Column<int>(type: "integer", nullable: true),
                    DestinationAccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DestinationAccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PersonBankAccountId = table.Column<int>(type: "integer", nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Concept = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceDocumentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeneficiaryIsDebtor = table.Column<bool>(type: "boolean", nullable: false),
                    BranchPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidByLendingFile = table.Column<bool>(type: "boolean", nullable: false),
                    LendingDisbursementFileId = table.Column<int>(type: "integer", nullable: true),
                    ReplacesOrderId = table.Column<int>(type: "integer", nullable: true),
                    GmfExempt = table.Column<bool>(type: "boolean", nullable: false),
                    GmfExemptionReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    GmfEstimate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PendingReasonsJson = table.Column<string>(type: "text", nullable: false),
                    Flags = table.Column<int>(type: "integer", nullable: false),
                    AccountChangedRecently = table.Column<bool>(type: "boolean", nullable: false),
                    VoidRequested = table.Column<bool>(type: "boolean", nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VoidedByCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_TES_PaymentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PaymentOrders_COR_Banks_DestinationBankId",
                        column: x => x.DestinationBankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentOrders_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentOrders_COR_PersonBankAccounts_PersonBankAccountId",
                        column: x => x.PersonBankAccountId,
                        principalSchema: "dbo",
                        principalTable: "COR_PersonBankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentOrders_TES_PaymentOrders_ReplacesOrderId",
                        column: x => x.ReplacesOrderId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_Setup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ResultWithThirdPerson = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_TES_Setup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TES_LiquidityFundUses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankAccountId = table.Column<int>(type: "integer", nullable: false),
                    Cause = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupervisorNoticeAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UsedByDocumentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_TES_LiquidityFundUses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_LiquidityFundUses_SEC_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_LiquidityFundUses_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_LiquidityFundUses_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_UserBankAccountScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    BankAccountId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_TES_UserBankAccountScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_UserBankAccountScopes_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_UserBankAccountScopes_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_DisbursementResults",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentOrderId = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TreasuryReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CheckNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RecordedByCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentResponseLineId = table.Column<int>(type: "integer", nullable: true),
                    StatementLinePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmittedMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_TES_DisbursementResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_DisbursementResults_SEC_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_DisbursementResults_TES_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_InboundMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
                    OriginModule = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaymentOrderId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_TES_InboundMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_InboundMessages_TES_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PaymentBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BankAccountId = table.Column<int>(type: "integer", nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    BankFileFormatId = table.Column<int>(type: "integer", nullable: true),
                    PreparedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OrderCount = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GmfEstimate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovalRequestPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LiquidityFundUseId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InputsHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentByUserId = table.Column<int>(type: "integer", nullable: true),
                    UploadedAtBankBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AuthorizedAtBankBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("PK_TES_PaymentBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PaymentBatches_COR_BankFileFormats_BankFileFormatId",
                        column: x => x.BankFileFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentBatches_SEC_Users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentBatches_SEC_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentBatches_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentBatches_TES_LiquidityFundUses_LiquidityFundUseId",
                        column: x => x.LiquidityFundUseId,
                        principalSchema: "dbo",
                        principalTable: "TES_LiquidityFundUses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PaymentBatchItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentBatchId = table.Column<int>(type: "integer", nullable: false),
                    PaymentOrderId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GmfEstimate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Flags = table.Column<int>(type: "integer", nullable: false),
                    ExplicitlyReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_TES_PaymentBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PaymentBatchItems_SEC_Users_ExplicitlyReviewedByUserId",
                        column: x => x.ExplicitlyReviewedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentBatchItems_TES_PaymentBatches_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentBatchItems_TES_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PaymentFiles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentBatchId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    BankFileFormatId = table.Column<int>(type: "integer", nullable: false),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedByUserId = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DownloadedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentByUserId = table.Column<int>(type: "integer", nullable: true),
                    UploadedAtBankBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AuthorizedAtBankBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RejectedByBankAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedByBankReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TES_PaymentFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PaymentFiles_COR_BankFileFormats_BankFileFormatId",
                        column: x => x.BankFileFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentFiles_SEC_Users_DownloadedByUserId",
                        column: x => x.DownloadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentFiles_SEC_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentFiles_SEC_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentFiles_TES_PaymentBatches_PaymentBatchId",
                        column: x => x.PaymentBatchId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PaymentFileLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentFileId = table.Column<int>(type: "integer", nullable: false),
                    PaymentBatchItemId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
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
                    table.PrimaryKey("PK_TES_PaymentFileLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PaymentFileLines_TES_PaymentBatchItems_PaymentBatchItem~",
                        column: x => x.PaymentBatchItemId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentBatchItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentFileLines_TES_PaymentFiles_PaymentFileId",
                        column: x => x.PaymentFileId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PaymentResponses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentFileId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    BankFileFormatId = table.Column<int>(type: "integer", nullable: true),
                    ResponseAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LoadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    DiscrepancyCount = table.Column<int>(type: "integer", nullable: false),
                    InformedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_TES_PaymentResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PaymentResponses_COR_BankFileFormats_BankFileFormatId",
                        column: x => x.BankFileFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentResponses_SEC_Users_LoadedByUserId",
                        column: x => x.LoadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentResponses_TES_PaymentFiles_PaymentFileId",
                        column: x => x.PaymentFileId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_PaymentResponseLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentResponseId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    PaymentFileLineId = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    BankCause = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BeneficiaryTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Discrepancy = table.Column<bool>(type: "boolean", nullable: false),
                    DiscrepancyDetail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewAccepted = table.Column<bool>(type: "boolean", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisbursementResultId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_TES_PaymentResponseLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_PaymentResponseLines_SEC_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentResponseLines_TES_DisbursementResults_Disburseme~",
                        column: x => x.DisbursementResultId,
                        principalSchema: "dbo",
                        principalTable: "TES_DisbursementResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentResponseLines_TES_PaymentFileLines_PaymentFileLi~",
                        column: x => x.PaymentFileLineId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentFileLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_PaymentResponseLines_TES_PaymentResponses_PaymentRespon~",
                        column: x => x.PaymentResponseId,
                        principalSchema: "dbo",
                        principalTable: "TES_PaymentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccounts_BankThirdPartyPersonId",
                schema: "dbo",
                table: "TES_BankAccounts",
                column: "BankThirdPartyPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_BankAccounts_BranchId",
                schema: "dbo",
                table: "TES_BankAccounts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_BankAccounts_BankId_AccountNumber",
                schema: "dbo",
                table: "TES_BankAccounts",
                columns: new[] { "BankId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_BankAccounts_Code",
                schema: "dbo",
                table: "TES_BankAccounts",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_TES_BankAccounts_PublicId",
                schema: "dbo",
                table: "TES_BankAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_DisbursementResults_RecordedByUserId",
                schema: "dbo",
                table: "TES_DisbursementResults",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_DisbursementResults_PaymentOrderId_Outcome",
                schema: "dbo",
                table: "TES_DisbursementResults",
                columns: new[] { "PaymentOrderId", "Outcome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_DisbursementResults_PublicId",
                schema: "dbo",
                table: "TES_DisbursementResults",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_InboundMessages_PaymentOrderId",
                schema: "dbo",
                table: "TES_InboundMessages",
                column: "PaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_InboundMessages_MessagePublicId",
                schema: "dbo",
                table: "TES_InboundMessages",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_InboundMessages_PublicId",
                schema: "dbo",
                table: "TES_InboundMessages",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_LiquidityFundUses_BankAccountId_Status",
                schema: "dbo",
                table: "TES_LiquidityFundUses",
                columns: new[] { "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_LiquidityFundUses_DecidedByUserId",
                schema: "dbo",
                table: "TES_LiquidityFundUses",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_LiquidityFundUses_RequestedByUserId",
                schema: "dbo",
                table: "TES_LiquidityFundUses",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_LiquidityFundUses_Number",
                schema: "dbo",
                table: "TES_LiquidityFundUses",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_LiquidityFundUses_PublicId",
                schema: "dbo",
                table: "TES_LiquidityFundUses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentBatches_BankAccountId_Status",
                schema: "dbo",
                table: "TES_PaymentBatches",
                columns: new[] { "BankAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentBatches_BankFileFormatId",
                schema: "dbo",
                table: "TES_PaymentBatches",
                column: "BankFileFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentBatches_LiquidityFundUseId",
                schema: "dbo",
                table: "TES_PaymentBatches",
                column: "LiquidityFundUseId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentBatches_PreparedByUserId",
                schema: "dbo",
                table: "TES_PaymentBatches",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentBatches_SentByUserId",
                schema: "dbo",
                table: "TES_PaymentBatches",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentBatches_Number",
                schema: "dbo",
                table: "TES_PaymentBatches",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentBatches_PublicId",
                schema: "dbo",
                table: "TES_PaymentBatches",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentBatchItems_ExplicitlyReviewedByUserId",
                schema: "dbo",
                table: "TES_PaymentBatchItems",
                column: "ExplicitlyReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentBatchItems_PaymentBatchId",
                schema: "dbo",
                table: "TES_PaymentBatchItems",
                column: "PaymentBatchId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentBatchItems_PaymentOrderId_Live",
                schema: "dbo",
                table: "TES_PaymentBatchItems",
                column: "PaymentOrderId",
                unique: true,
                filter: "\"IsLive\" = TRUE AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentBatchItems_PublicId",
                schema: "dbo",
                table: "TES_PaymentBatchItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentFileLines_PaymentBatchItemId",
                schema: "dbo",
                table: "TES_PaymentFileLines",
                column: "PaymentBatchItemId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentFileLines_PaymentFileId_LineNumber",
                schema: "dbo",
                table: "TES_PaymentFileLines",
                columns: new[] { "PaymentFileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentFileLines_PublicId",
                schema: "dbo",
                table: "TES_PaymentFileLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentFiles_BankFileFormatId",
                schema: "dbo",
                table: "TES_PaymentFiles",
                column: "BankFileFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentFiles_DownloadedByUserId",
                schema: "dbo",
                table: "TES_PaymentFiles",
                column: "DownloadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentFiles_GeneratedByUserId",
                schema: "dbo",
                table: "TES_PaymentFiles",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentFiles_SentByUserId",
                schema: "dbo",
                table: "TES_PaymentFiles",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentFiles_PaymentBatchId_Version",
                schema: "dbo",
                table: "TES_PaymentFiles",
                columns: new[] { "PaymentBatchId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentFiles_PublicId",
                schema: "dbo",
                table: "TES_PaymentFiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_BeneficiaryPersonId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_DestinationBankId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "DestinationBankId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_PersonBankAccountId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "PersonBankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_ReplacesOrderId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "ReplacesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentOrders_Status_PayDate",
                schema: "dbo",
                table: "TES_PaymentOrders",
                columns: new[] { "Status", "PayDate" });

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentOrders_Number",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentOrders_Origin_OriginOrderPublicId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                columns: new[] { "Origin", "OriginOrderPublicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentOrders_PublicId",
                schema: "dbo",
                table: "TES_PaymentOrders",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentResponseLines_DisbursementResultId",
                schema: "dbo",
                table: "TES_PaymentResponseLines",
                column: "DisbursementResultId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentResponseLines_PaymentFileLineId",
                schema: "dbo",
                table: "TES_PaymentResponseLines",
                column: "PaymentFileLineId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentResponseLines_ReviewedByUserId",
                schema: "dbo",
                table: "TES_PaymentResponseLines",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentResponseLines_PaymentResponseId_LineNumber",
                schema: "dbo",
                table: "TES_PaymentResponseLines",
                columns: new[] { "PaymentResponseId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentResponseLines_PublicId",
                schema: "dbo",
                table: "TES_PaymentResponseLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentResponses_BankFileFormatId",
                schema: "dbo",
                table: "TES_PaymentResponses",
                column: "BankFileFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentResponses_LoadedByUserId",
                schema: "dbo",
                table: "TES_PaymentResponses",
                column: "LoadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_PaymentResponses_PaymentFileId",
                schema: "dbo",
                table: "TES_PaymentResponses",
                column: "PaymentFileId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_PaymentResponses_PublicId",
                schema: "dbo",
                table: "TES_PaymentResponses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_Setup_PublicId",
                schema: "dbo",
                table: "TES_Setup",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_UserBankAccountScopes_BankAccountId",
                schema: "dbo",
                table: "TES_UserBankAccountScopes",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_UserBankAccountScopes_PublicId",
                schema: "dbo",
                table: "TES_UserBankAccountScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_UserBankAccountScopes_UserId_BankAccountId",
                schema: "dbo",
                table: "TES_UserBankAccountScopes",
                columns: new[] { "UserId", "BankAccountId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            // Feature 015 (T0050; data-model §9.1): la fila única de TES_Setup, con Id = 1 y sin activar. Idempotente. Nivel 1 del cerrojo de Tesorería.
            migrationBuilder.Sql(@"
INSERT INTO dbo.""TES_Setup"" (""Id"", ""PublicId"", ""Status"", ""ResultWithThirdPerson"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
OVERRIDING SYSTEM VALUE
SELECT 1, gen_random_uuid(), 1, FALSE, FALSE, now(), 'SYSTEM'
WHERE NOT EXISTS (SELECT 1 FROM dbo.""TES_Setup"");
SELECT setval(pg_get_serial_sequence('dbo.""TES_Setup""', 'Id'), GREATEST((SELECT MAX(""Id"") FROM dbo.""TES_Setup""), 1));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TES_InboundMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PaymentResponseLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_Setup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_UserBankAccountScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_DisbursementResults",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PaymentFileLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PaymentResponses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PaymentBatchItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PaymentFiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PaymentOrders",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_PaymentBatches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_LiquidityFundUses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_BankAccounts",
                schema: "dbo");
        }
    }
}
