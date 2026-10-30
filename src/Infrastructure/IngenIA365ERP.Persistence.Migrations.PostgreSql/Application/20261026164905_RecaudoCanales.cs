using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
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
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaidByStatementLinePublicId",
                schema: "dbo",
                table: "RCD_ReceivedChecks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RCD_ClearingBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    RemittancePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepositedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReturnedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatementLinePublicIds = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "RCD_CollectionAgreements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    PartnerPersonId = table.Column<int>(type: "integer", nullable: true),
                    PartnerAgreementCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_RCD_CollectionAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CollectionAgreements_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CollectionAgreements_COR_People_PartnerPersonId",
                        column: x => x.PartnerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_PaymentReferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BaseNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CheckDigitValue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Algorithm = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    ProductPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Target = table.Column<int>(type: "integer", nullable: true),
                    ProductNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_RCD_PaymentReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PaymentReferences_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_CollectionAgreementVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollectionAgreementId = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ReferenceMode = table.Column<int>(type: "integer", nullable: false),
                    ReferenceLength = table.Column<int>(type: "integer", nullable: false),
                    CheckDigit = table.Column<int>(type: "integer", nullable: false),
                    BankFileFormatId = table.Column<int>(type: "integer", nullable: false),
                    CommissionPerPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
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
                    table.PrimaryKey("PK_RCD_CollectionAgreementVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_CollectionAgreementVersions_COR_BankFileFormats_BankFil~",
                        column: x => x.BankFileFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_CollectionAgreementVersions_RCD_CollectionAgreements_Co~",
                        column: x => x.CollectionAgreementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_AgreementFiles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CollectionAgreementId = table.Column<int>(type: "integer", nullable: false),
                    CollectionAgreementVersionId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Lines = table.Column<int>(type: "integer", nullable: false),
                    Applied = table.Column<int>(type: "integer", nullable: false),
                    Unidentified = table.Column<int>(type: "integer", nullable: false),
                    Rejected = table.Column<int>(type: "integer", nullable: false),
                    Omitted = table.Column<int>(type: "integer", nullable: false),
                    TotalInformed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalApplied = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalUnidentified = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LoadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingRunId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCD_AgreementFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementFiles_RCD_CollectionAgreementVersions_Collecti~",
                        column: x => x.CollectionAgreementVersionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionAgreementVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementFiles_RCD_CollectionAgreements_CollectionAgree~",
                        column: x => x.CollectionAgreementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementFiles_RCD_ProcessingRuns_ProcessingRunId",
                        column: x => x.ProcessingRunId,
                        principalSchema: "dbo",
                        principalTable: "RCD_ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementFiles_SEC_Users_LoadedByUserId",
                        column: x => x.LoadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_GatewayFiles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CollectionAgreementId = table.Column<int>(type: "integer", nullable: false),
                    CollectionAgreementVersionId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Lines = table.Column<int>(type: "integer", nullable: false),
                    Applied = table.Column<int>(type: "integer", nullable: false),
                    Unidentified = table.Column<int>(type: "integer", nullable: false),
                    Rejected = table.Column<int>(type: "integer", nullable: false),
                    Omitted = table.Column<int>(type: "integer", nullable: false),
                    TotalInformed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalApplied = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalUnidentified = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LoadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingRunId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCD_GatewayFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_GatewayFiles_RCD_CollectionAgreementVersions_Collection~",
                        column: x => x.CollectionAgreementVersionId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionAgreementVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_GatewayFiles_RCD_CollectionAgreements_CollectionAgreeme~",
                        column: x => x.CollectionAgreementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_GatewayFiles_RCD_ProcessingRuns_ProcessingRunId",
                        column: x => x.ProcessingRunId,
                        principalSchema: "dbo",
                        principalTable: "RCD_ProcessingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_GatewayFiles_SEC_Users_LoadedByUserId",
                        column: x => x.LoadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_AgreementFileLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgreementFileId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PartnerSequence = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PayerTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    UnidentifiedItemId = table.Column<int>(type: "integer", nullable: true),
                    PaymentReferenceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCD_AgreementFileLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementFileLines_RCD_AgreementFiles_AgreementFileId",
                        column: x => x.AgreementFileId,
                        principalSchema: "dbo",
                        principalTable: "RCD_AgreementFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementFileLines_RCD_PaymentReferences_PaymentReferen~",
                        column: x => x.PaymentReferenceId,
                        principalSchema: "dbo",
                        principalTable: "RCD_PaymentReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementFileLines_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementFileLines_RCD_UnidentifiedItems_UnidentifiedIt~",
                        column: x => x.UnidentifiedItemId,
                        principalSchema: "dbo",
                        principalTable: "RCD_UnidentifiedItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_AgreementReconciliations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CollectionAgreementId = table.Column<int>(type: "integer", nullable: false),
                    AgreementFileId = table.Column<int>(type: "integer", nullable: true),
                    GatewayFileId = table.Column<int>(type: "integer", nullable: true),
                    StatementLinePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    InformedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    UnexplainedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Tolerance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_RCD_AgreementReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementReconciliations_RCD_AgreementFiles_AgreementFi~",
                        column: x => x.AgreementFileId,
                        principalSchema: "dbo",
                        principalTable: "RCD_AgreementFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementReconciliations_RCD_CollectionAgreements_Colle~",
                        column: x => x.CollectionAgreementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementReconciliations_RCD_GatewayFiles_GatewayFileId",
                        column: x => x.GatewayFileId,
                        principalSchema: "dbo",
                        principalTable: "RCD_GatewayFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementReconciliations_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_AgreementReconciliations_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_GatewayFileLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GatewayFileId = table.Column<int>(type: "integer", nullable: false),
                    GatewayOutcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    GatewayReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    GatewayDate = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PartnerSequence = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PayerTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    UnidentifiedItemId = table.Column<int>(type: "integer", nullable: true),
                    PaymentReferenceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCD_GatewayFileLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_GatewayFileLines_RCD_GatewayFiles_GatewayFileId",
                        column: x => x.GatewayFileId,
                        principalSchema: "dbo",
                        principalTable: "RCD_GatewayFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_GatewayFileLines_RCD_PaymentReferences_PaymentReference~",
                        column: x => x.PaymentReferenceId,
                        principalSchema: "dbo",
                        principalTable: "RCD_PaymentReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_GatewayFileLines_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_GatewayFileLines_RCD_UnidentifiedItems_UnidentifiedItem~",
                        column: x => x.UnidentifiedItemId,
                        principalSchema: "dbo",
                        principalTable: "RCD_UnidentifiedItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReceivedChecks_ClearingBatchId",
                schema: "dbo",
                table: "RCD_ReceivedChecks",
                column: "ClearingBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementFileLines_PartnerSequence",
                schema: "dbo",
                table: "RCD_AgreementFileLines",
                column: "PartnerSequence");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementFileLines_PaymentReferenceId",
                schema: "dbo",
                table: "RCD_AgreementFileLines",
                column: "PaymentReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementFileLines_ReceiptId",
                schema: "dbo",
                table: "RCD_AgreementFileLines",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementFileLines_UnidentifiedItemId",
                schema: "dbo",
                table: "RCD_AgreementFileLines",
                column: "UnidentifiedItemId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_AgreementFileLines_File_Line",
                schema: "dbo",
                table: "RCD_AgreementFileLines",
                columns: new[] { "AgreementFileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_AgreementFileLines_PublicId",
                schema: "dbo",
                table: "RCD_AgreementFileLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementFiles_CollectionAgreementVersionId",
                schema: "dbo",
                table: "RCD_AgreementFiles",
                column: "CollectionAgreementVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementFiles_LoadedByUserId",
                schema: "dbo",
                table: "RCD_AgreementFiles",
                column: "LoadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementFiles_ProcessingRunId",
                schema: "dbo",
                table: "RCD_AgreementFiles",
                column: "ProcessingRunId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_AgreementFiles_Agreement_Sha256",
                schema: "dbo",
                table: "RCD_AgreementFiles",
                columns: new[] { "CollectionAgreementId", "FileSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_AgreementFiles_Number",
                schema: "dbo",
                table: "RCD_AgreementFiles",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_AgreementFiles_PublicId",
                schema: "dbo",
                table: "RCD_AgreementFiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementReconciliations_AgreementFileId",
                schema: "dbo",
                table: "RCD_AgreementReconciliations",
                column: "AgreementFileId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementReconciliations_ApprovedByUserId",
                schema: "dbo",
                table: "RCD_AgreementReconciliations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementReconciliations_CollectionAgreementId",
                schema: "dbo",
                table: "RCD_AgreementReconciliations",
                column: "CollectionAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementReconciliations_GatewayFileId",
                schema: "dbo",
                table: "RCD_AgreementReconciliations",
                column: "GatewayFileId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_AgreementReconciliations_RegisteredByUserId",
                schema: "dbo",
                table: "RCD_AgreementReconciliations",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_AgreementReconciliations_Number",
                schema: "dbo",
                table: "RCD_AgreementReconciliations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_AgreementReconciliations_PublicId",
                schema: "dbo",
                table: "RCD_AgreementReconciliations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_AgreementReconciliations_StatementLine",
                schema: "dbo",
                table: "RCD_AgreementReconciliations",
                column: "StatementLinePublicId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CollectionAgreements_BranchId",
                schema: "dbo",
                table: "RCD_CollectionAgreements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CollectionAgreements_PartnerPersonId",
                schema: "dbo",
                table: "RCD_CollectionAgreements",
                column: "PartnerPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CollectionAgreements_Code",
                schema: "dbo",
                table: "RCD_CollectionAgreements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CollectionAgreements_PublicId",
                schema: "dbo",
                table: "RCD_CollectionAgreements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_CollectionAgreementVersions_BankFileFormatId",
                schema: "dbo",
                table: "RCD_CollectionAgreementVersions",
                column: "BankFileFormatId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CollectionAgreementVersions_Agreement_ValidFrom",
                schema: "dbo",
                table: "RCD_CollectionAgreementVersions",
                columns: new[] { "CollectionAgreementId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_CollectionAgreementVersions_PublicId",
                schema: "dbo",
                table: "RCD_CollectionAgreementVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_GatewayFileLines_PartnerSequence",
                schema: "dbo",
                table: "RCD_GatewayFileLines",
                column: "PartnerSequence");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_GatewayFileLines_PaymentReferenceId",
                schema: "dbo",
                table: "RCD_GatewayFileLines",
                column: "PaymentReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_GatewayFileLines_ReceiptId",
                schema: "dbo",
                table: "RCD_GatewayFileLines",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_GatewayFileLines_UnidentifiedItemId",
                schema: "dbo",
                table: "RCD_GatewayFileLines",
                column: "UnidentifiedItemId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_GatewayFileLines_File_Line",
                schema: "dbo",
                table: "RCD_GatewayFileLines",
                columns: new[] { "GatewayFileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_GatewayFileLines_PublicId",
                schema: "dbo",
                table: "RCD_GatewayFileLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_GatewayFiles_CollectionAgreementVersionId",
                schema: "dbo",
                table: "RCD_GatewayFiles",
                column: "CollectionAgreementVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_GatewayFiles_LoadedByUserId",
                schema: "dbo",
                table: "RCD_GatewayFiles",
                column: "LoadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_GatewayFiles_ProcessingRunId",
                schema: "dbo",
                table: "RCD_GatewayFiles",
                column: "ProcessingRunId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_GatewayFiles_Agreement_Sha256",
                schema: "dbo",
                table: "RCD_GatewayFiles",
                columns: new[] { "CollectionAgreementId", "FileSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_GatewayFiles_Number",
                schema: "dbo",
                table: "RCD_GatewayFiles",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_GatewayFiles_PublicId",
                schema: "dbo",
                table: "RCD_GatewayFiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PaymentReferences_Person_Mode",
                schema: "dbo",
                table: "RCD_PaymentReferences",
                columns: new[] { "PersonId", "Mode" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_PaymentReferences_Product",
                schema: "dbo",
                table: "RCD_PaymentReferences",
                column: "ProductPublicId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PaymentReferences_PublicId",
                schema: "dbo",
                table: "RCD_PaymentReferences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PaymentReferences_Reference",
                schema: "dbo",
                table: "RCD_PaymentReferences",
                column: "Reference",
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

            // Feature 015, R2 (fase 38, T0434; FR-089, FR-091, FR-198): los tipos de documento de los recibos de convenio y en línea y la modalidad de esos canales
            // (créditos CycleInstallment; cuentas Installments si hay exigibles, si no Free), como las de los canales de R0. Idempotente.
            migrationBuilder.Sql(@"
INSERT INTO dbo.""RCD_DocumentTypes"" (""PublicId"", ""Code"", ""Name"", ""Kind"", ""UsesPointPrefix"", ""Status"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT gen_random_uuid(), v.code, v.name, v.kind, FALSE, 1, FALSE, now(), 'SYSTEM'
FROM (VALUES ('RV', 'Recibo de convenio', 3), ('RL', 'Recibo en línea', 5)) AS v(code, name, kind)
WHERE NOT EXISTS (SELECT 1 FROM dbo.""RCD_DocumentTypes"" t WHERE t.""Code"" = v.code AND t.""IsDeleted"" = FALSE)
  AND NOT EXISTS (SELECT 1 FROM dbo.""RCD_DocumentTypes"" t WHERE t.""Kind"" = v.kind AND t.""Status"" = 1 AND t.""IsDeleted"" = FALSE);
INSERT INTO dbo.""RCD_ChannelModalityRules"" (""PublicId"", ""Channel"", ""Target"", ""LoanModality"", ""AccountModality"", ""OnlyWhenInstallmentsDue"", ""Priority"", ""ValidFrom"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT gen_random_uuid(), v.channel, v.target, v.loan, v.account, v.due, v.priority, DATE '2026-01-01', FALSE, now(), 'SYSTEM'
FROM (VALUES (4, 1, 3, NULL, FALSE, 1), (4, 2, NULL, 2, TRUE, 1), (4, 2, NULL, 1, FALSE, 2), (5, 1, 3, NULL, FALSE, 1), (5, 2, NULL, 2, TRUE, 1), (5, 2, NULL, 1, FALSE, 2)) AS v(channel, target, loan, account, due, priority)
WHERE NOT EXISTS (SELECT 1 FROM dbo.""RCD_ChannelModalityRules"" r WHERE r.""Channel"" = v.channel AND r.""Target"" = v.target AND r.""IsDeleted"" = FALSE);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RCD_ReceivedChecks_RCD_ClearingBatches_ClearingBatchId",
                schema: "dbo",
                table: "RCD_ReceivedChecks");

            migrationBuilder.DropTable(
                name: "RCD_AgreementFileLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_AgreementReconciliations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ClearingBatches",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_GatewayFileLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_AgreementFiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_GatewayFiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_PaymentReferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CollectionAgreementVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_CollectionAgreements",
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
