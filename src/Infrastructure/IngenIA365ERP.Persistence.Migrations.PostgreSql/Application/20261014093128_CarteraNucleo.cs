using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CarteraNucleo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LND_CaseParticipations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CaseRootType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CaseRootPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_LND_CaseParticipations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CaseParticipations_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CaseParticipations_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CommercialTermEquivalences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CommercialCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: true),
                    DaysPerUnit = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_LND_CommercialTermEquivalences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ConnectedGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Basis = table.Column<int>(type: "integer", nullable: false),
                    Grounds = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_ConnectedGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ConnectedGroups_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_ContributionBalanceLoads",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContributionsBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PermanentSavingsBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImportPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_LND_ContributionBalanceLoads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ContributionBalanceLoads_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ContributionBalanceLoads_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedReason = table.Column<int>(type: "integer", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CloseNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UsuryBlockedSince = table.Column<DateOnly>(type: "date", nullable: true),
                    UsuryBlockedByValuePublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditLines_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DebtorControls",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_DebtorControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DebtorControls_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DisbursementFiles",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BankId = table.Column<int>(type: "integer", nullable: false),
                    FormatId = table.Column<int>(type: "integer", nullable: false),
                    FormatCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DailySequence = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SourceAccountPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LineCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedByUserId = table.Column<int>(type: "integer", nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_DisbursementFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementFiles_COR_BankFileFormats_FormatId",
                        column: x => x.FormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementFiles_COR_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementFiles_SEC_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementFiles_SEC_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_FactTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ShortCode = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsInformational = table.Column<bool>(type: "boolean", nullable: false),
                    VoucherTypeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DefaultMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultBatchTrigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DeliveredIn = table.Column<int>(type: "integer", nullable: false),
                    IsSeeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_LND_FactTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_GoverningBodies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DecidesCredit = table.Column<bool>(type: "boolean", nullable: false),
                    MinutesPrefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
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
                    table.PrimaryKey("PK_LND_GoverningBodies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_IssuedDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    HolderTaxIdType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    HolderTaxId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    HolderName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: true),
                    TaxYear = table.Column<short>(type: "smallint", nullable: true),
                    InformationCutoff = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerificationCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PdfAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    EssentialDataJson = table.Column<string>(type: "text", nullable: false),
                    SettlementSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RulesVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FinalityUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    CoversCoDebts = table.Column<bool>(type: "boolean", nullable: true),
                    OutsideLendingNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SignerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SignerTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsTestEnvironment = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryMode = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_IssuedDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocuments_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocuments_SEC_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationLoads",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AppliedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivationDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrentCheckVersion = table.Column<int>(type: "integer", nullable: false),
                    BalanceCheckJson = table.Column<string>(type: "text", nullable: true),
                    BalanceCheckSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SalesReconciliationSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsBalanced = table.Column<bool>(type: "boolean", nullable: false),
                    SalesReconciled = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationSummaryJson = table.Column<string>(type: "text", nullable: true),
                    LoanCount = table.Column<int>(type: "integer", nullable: false),
                    ChargedOffCount = table.Column<int>(type: "integer", nullable: false),
                    PersonCount = table.Column<int>(type: "integer", nullable: false),
                    TotalsJson = table.Column<string>(type: "text", nullable: true),
                    ReplacedByLoadId = table.Column<int>(type: "integer", nullable: true),
                    ReplacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationLoads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoads_LND_MigrationLoads_ReplacedByLoadId",
                        column: x => x.ReplacedByLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoads_SEC_Users_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoads_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_NumberSequences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    MinDigits = table.Column<byte>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_LND_NumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ProcessRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ProcessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: true),
                    PeriodMonth = table.Column<byte>(type: "smallint", nullable: true),
                    CloseVersion = table.Column<int>(type: "integer", nullable: true),
                    NationalValueReplicationId = table.Column<int>(type: "integer", nullable: true),
                    AffectedFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    AffectedTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentStep = table.Column<byte>(type: "smallint", nullable: true),
                    RequestedByKind = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByCentralUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RequestedByIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    ExecutionOrigin = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InformationCutoffAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NationalValuesWatermark = table.Column<long>(type: "bigint", nullable: true),
                    CursorLoanId = table.Column<int>(type: "integer", nullable: true),
                    TotalLoans = table.Column<int>(type: "integer", nullable: false),
                    ProcessedLoans = table.Column<int>(type: "integer", nullable: false),
                    FailedLoans = table.Column<int>(type: "integer", nullable: false),
                    SkippedLoans = table.Column<int>(type: "integer", nullable: false),
                    BatchCount = table.Column<int>(type: "integer", nullable: false),
                    PartCount = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    TotalsJson = table.Column<string>(type: "text", nullable: true),
                    ResultSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastHeartbeatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_ProcessRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ProcessRuns_COR_NationalValueReplications_NationalValue~",
                        column: x => x.NationalValueReplicationId,
                        principalSchema: "dbo",
                        principalTable: "COR_NationalValueReplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ProcessRuns_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_RelatedParties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Basis = table.Column<int>(type: "integer", nullable: false),
                    RelatedThroughPersonId = table.Column<int>(type: "integer", nullable: true),
                    KinshipKind = table.Column<int>(type: "integer", nullable: true),
                    KinshipDegree = table.Column<byte>(type: "smallint", nullable: true),
                    ContributionPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EndedByUserId = table.Column<int>(type: "integer", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_LND_RelatedParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_RelatedParties_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RelatedParties_COR_People_RelatedThroughPersonId",
                        column: x => x.RelatedThroughPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RelatedParties_SEC_Users_EndedByUserId",
                        column: x => x.EndedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_ConnectedGroupMembers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_ConnectedGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ConnectedGroupMembers_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ConnectedGroupMembers_LND_ConnectedGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "dbo",
                        principalTable: "LND_ConnectedGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLineVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ModalityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubsegmentCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ForEmployees = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsNonAssociates = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsCommercialOrigin = table.Column<bool>(type: "boolean", nullable: false),
                    CommercialDefaultFor = table.Column<int>(type: "integer", nullable: true),
                    InterestTaxTreatment = table.Column<int>(type: "integer", nullable: true),
                    InterestTaxSupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RateKind = table.Column<int>(type: "integer", nullable: false),
                    AnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    IndexCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IndexKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Margin = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RateFloor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RateCeiling = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ResetPeriodicity = table.Column<int>(type: "integer", nullable: true),
                    DefaultRateKind = table.Column<int>(type: "integer", nullable: false),
                    DefaultRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DefaultRateFactor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinTermMonths = table.Column<int>(type: "integer", nullable: false),
                    MaxTermMonths = table.Column<int>(type: "integer", nullable: false),
                    MaxInstallments = table.Column<int>(type: "integer", nullable: true),
                    Periodicities = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultPeriodicity = table.Column<int>(type: "integer", nullable: false),
                    PaymentDay = table.Column<byte>(type: "smallint", nullable: true),
                    CutoffDay = table.Column<byte>(type: "smallint", nullable: true),
                    AmortizationSystems = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DefaultAmortizationSystem = table.Column<int>(type: "integer", nullable: false),
                    AllowsExtraordinaryInstallments = table.Column<bool>(type: "boolean", nullable: false),
                    MaxExtraordinaryFraction = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    AllowedGraceKind = table.Column<int>(type: "integer", nullable: false),
                    MaxGraceInstallments = table.Column<int>(type: "integer", nullable: true),
                    GraceInterestTreatment = table.Column<int>(type: "integer", nullable: true),
                    GraceDistributionInstallments = table.Column<int>(type: "integer", nullable: true),
                    AllowsTranches = table.Column<bool>(type: "boolean", nullable: false),
                    DayCount = table.Column<int>(type: "integer", nullable: false),
                    RateEquivalence = table.Column<int>(type: "integer", nullable: false),
                    RoundingUnit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoundingMode = table.Column<int>(type: "integer", nullable: false),
                    RoundingMoment = table.Column<int>(type: "integer", nullable: false),
                    AllocationOrder = table.Column<int>(type: "integer", nullable: false),
                    ConceptPriority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SuspenseWithItsConcept = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsSpecificInstallmentOutOfOrder = table.Column<bool>(type: "boolean", nullable: false),
                    PrepaymentOfferedFirst = table.Column<int>(type: "integer", nullable: false),
                    NoteRecalculation = table.Column<int>(type: "integer", nullable: false),
                    RepaymentChannels = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DefaultRepaymentChannel = table.Column<int>(type: "integer", nullable: false),
                    MinAssociateSeniorityMonths = table.Column<int>(type: "integer", nullable: true),
                    MinAge = table.Column<int>(type: "integer", nullable: true),
                    MaxAge = table.Column<int>(type: "integer", nullable: true),
                    RequiresEmployment = table.Column<bool>(type: "boolean", nullable: false),
                    ContributionsMultiple = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    MaxIndebtedness = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinContributionsBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RequiresBureauQuery = table.Column<bool>(type: "boolean", nullable: false),
                    ScoringModel = table.Column<string>(type: "text", nullable: true),
                    MinimumScore = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_LND_CreditLineVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditLineVersions_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_UserCreditLineScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LND_UserCreditLineScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_UserCreditLineScopes_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_UserCreditLineScopes_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_GoverningBodyMembers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoverningBodyId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    HasVote = table.Column<bool>(type: "boolean", nullable: false),
                    IsPrincipal = table.Column<bool>(type: "boolean", nullable: false),
                    AlternateForMemberId = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    AppointmentNotes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AppointmentAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_GoverningBodyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_GoverningBodyMembers_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_GoverningBodyMembers_LND_GoverningBodies_GoverningBodyId",
                        column: x => x.GoverningBodyId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_GoverningBodyMembers_LND_GoverningBodyMembers_Alternate~",
                        column: x => x.AlternateForMemberId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_GoverningBodyRuleVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoverningBodyId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QuorumNumerator = table.Column<int>(type: "integer", nullable: false),
                    QuorumDenominator = table.Column<int>(type: "integer", nullable: false),
                    QuorumComparison = table.Column<int>(type: "integer", nullable: false),
                    MajorityNumerator = table.Column<int>(type: "integer", nullable: false),
                    MajorityDenominator = table.Column<int>(type: "integer", nullable: false),
                    MajorityComparison = table.Column<int>(type: "integer", nullable: false),
                    MajorityBase = table.Column<int>(type: "integer", nullable: false),
                    RelatedPartyNumerator = table.Column<int>(type: "integer", nullable: true),
                    RelatedPartyDenominator = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_LND_GoverningBodyRuleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_GoverningBodyRuleVersions_LND_GoverningBodies_Governing~",
                        column: x => x.GoverningBodyId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_InsolvencyProceedings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Regime = table.Column<int>(type: "integer", nullable: true),
                    Authority = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CaseNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_InsolvencyProceedings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_InsolvencyProceedings_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_InsolvencyProceedings_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationLoadParts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: false),
                    PartKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BatchNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    ImportedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewSummaryJson = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationLoadParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoadParts_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoadParts_SEC_Users_ImportedByUserId",
                        column: x => x.ImportedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLineCollateralRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinTermMonths = table.Column<int>(type: "integer", nullable: true),
                    MaxTermMonths = table.Column<int>(type: "integer", nullable: true),
                    RequiredCoDebtors = table.Column<int>(type: "integer", nullable: false),
                    AdmittedKinds = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MinimumCoverage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ContributionsCountAsCollateral = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredPromissoryNoteKind = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditLineCollateralRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditLineCollateralRules_LND_CreditLineVersions_Credit~",
                        column: x => x.CreditLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLineDocumentRequirements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    AppliesTo = table.Column<int>(type: "integer", nullable: true),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DebtorKind = table.Column<int>(type: "integer", nullable: true),
                    RepaymentChannel = table.Column<int>(type: "integer", nullable: true),
                    MaxAgeDays = table.Column<int>(type: "integer", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_CreditLineDocumentRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditLineDocumentRequirements_LND_CreditLineVersions_C~",
                        column: x => x.CreditLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLineInsurances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    InsurerPersonId = table.Column<int>(type: "integer", nullable: false),
                    Base = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ChargePeriodicity = table.Column<int>(type: "integer", nullable: false),
                    AccrualTiming = table.Column<int>(type: "integer", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    RetainedFraction = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    SelfInsured = table.Column<bool>(type: "boolean", nullable: false),
                    IsDebtorLifeInsurance = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LND_CreditLineInsurances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditLineInsurances_COR_People_InsurerPersonId",
                        column: x => x.InsurerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditLineInsurances_LND_CreditLineVersions_CreditLineV~",
                        column: x => x.CreditLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLineRateBands",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreditLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinTermMonths = table.Column<int>(type: "integer", nullable: true),
                    MaxTermMonths = table.Column<int>(type: "integer", nullable: true),
                    DebtorKind = table.Column<int>(type: "integer", nullable: true),
                    ForEmployees = table.Column<bool>(type: "boolean", nullable: true),
                    MinSeniorityMonths = table.Column<int>(type: "integer", nullable: true),
                    AnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Margin = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditLineRateBands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditLineRateBands_LND_CreditLineVersions_CreditLineVe~",
                        column: x => x.CreditLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_Sessions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoverningBodyId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    HeldOn = table.Column<DateOnly>(type: "date", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RuleVersionId = table.Column<int>(type: "integer", nullable: true),
                    CompositionCount = table.Column<int>(type: "integer", nullable: true),
                    QuorumRequired = table.Column<int>(type: "integer", nullable: true),
                    PresentEligibleCount = table.Column<int>(type: "integer", nullable: true),
                    QuorumMet = table.Column<bool>(type: "boolean", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MinutesAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_Sessions_LND_GoverningBodies_GoverningBodyId",
                        column: x => x.GoverningBodyId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Sessions_LND_GoverningBodyRuleVersions_RuleVersionId",
                        column: x => x.RuleVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodyRuleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Sessions_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationArrearsHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "smallint", nullable: false),
                    MaxDaysPastDue = table.Column<int>(type: "integer", nullable: false),
                    DaysPastDueAtMonthEnd = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationArrearsHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationArrearsHistory_LND_MigrationLoadParts_Migratio~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationAuthorizations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ContactChannel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AuthorizedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PolicyVersionText = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HasEvidence = table.Column<bool>(type: "boolean", nullable: false),
                    ObtainedThrough = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationAuthorizations_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationAuthorizations_LND_MigrationLoadParts_Migratio~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationBalances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Concept = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_LND_MigrationBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationBalances_LND_MigrationLoadParts_MigrationLoadP~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationBlocks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    BlockReason = table.Column<int>(type: "integer", nullable: false),
                    Since = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Until = table.Column<DateOnly>(type: "date", nullable: true),
                    SupportReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InsurerPersonId = table.Column<int>(type: "integer", nullable: true),
                    InsuranceClaimStatus = table.Column<int>(type: "integer", nullable: true),
                    InsolvencyStatus = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationBlocks_COR_People_InsurerPersonId",
                        column: x => x.InsurerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationBlocks_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationBlocks_LND_MigrationLoadParts_MigrationLoadPar~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationBureauReports",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BureauCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastReportedYear = table.Column<short>(type: "smallint", nullable: false),
                    LastReportedMonth = table.Column<byte>(type: "smallint", nullable: false),
                    LastReportedStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsNegative = table.Column<bool>(type: "boolean", nullable: false),
                    EpisodeStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WithPriorNotice = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationBureauReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationBureauReports_LND_MigrationLoadParts_Migration~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationChargedOff",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OfficePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisbursementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WrittenOffOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PrincipalBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HistoricalApprovalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_LND_MigrationChargedOff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationChargedOff_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationChargedOff_LND_MigrationLoadParts_MigrationLoa~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationClaims",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BureauCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsExtended = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    PersonId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationClaims_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationClaims_LND_MigrationLoadParts_MigrationLoadPar~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationCoDebtors",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CoDebtorPersonId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Since = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationCoDebtors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationCoDebtors_COR_People_CoDebtorPersonId",
                        column: x => x.CoDebtorPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationCoDebtors_LND_MigrationLoadParts_MigrationLoad~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationCollaterals",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CollateralKind = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ConstitutedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    AppraisalValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AppraisalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PolicyNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PolicyExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    InsurerPersonId = table.Column<int>(type: "integer", nullable: true),
                    PromissoryNoteKind = table.Column<int>(type: "integer", nullable: true),
                    CustodianChannelCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ExternalNoteId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    GuaranteeFundPersonId = table.Column<int>(type: "integer", nullable: true),
                    CoveragePercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    FeePercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationCollaterals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationCollaterals_COR_People_GuaranteeFundPersonId",
                        column: x => x.GuaranteeFundPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationCollaterals_COR_People_InsurerPersonId",
                        column: x => x.InsurerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationCollaterals_LND_MigrationLoadParts_MigrationLo~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationCreditLimits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    LimitKind = table.Column<int>(type: "integer", nullable: false),
                    CreditLineCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PrepaymentChoice = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationCreditLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationCreditLimits_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationCreditLimits_LND_MigrationLoadParts_MigrationL~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationInstallments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Insurance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherCharges = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProjectedInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationInstallments_LND_MigrationLoadParts_MigrationL~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationLoans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: true),
                    OfficePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DisbursementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TermsJson = table.Column<string>(type: "text", nullable: false),
                    PrepaymentChoice = table.Column<int>(type: "integer", nullable: true),
                    RepaymentChannel = table.Column<int>(type: "integer", nullable: false),
                    PaymasterPersonId = table.Column<int>(type: "integer", nullable: true),
                    EmployeeIdAtPaymaster = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    LibranzaDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CollectionStageCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AdvisorPersonId = table.Column<int>(type: "integer", nullable: true),
                    InLegalCollection = table.Column<bool>(type: "boolean", nullable: false),
                    LawyerPersonId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoans_COR_People_AdvisorPersonId",
                        column: x => x.AdvisorPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoans_COR_People_LawyerPersonId",
                        column: x => x.LawyerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoans_COR_People_PaymasterPersonId",
                        column: x => x.PaymasterPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoans_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoans_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoans_LND_MigrationLoadParts_MigrationLoadPart~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationPriorNotices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EpisodeStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequiredCommunications = table.Column<int>(type: "integer", nullable: false),
                    FirstSentOn = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstChannel = table.Column<int>(type: "integer", nullable: false),
                    FirstReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SecondSentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SecondChannel = table.Column<int>(type: "integer", nullable: true),
                    SecondReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationPriorNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationPriorNotices_LND_MigrationLoadParts_MigrationL~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationRelatedParties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Basis = table.Column<int>(type: "integer", nullable: false),
                    RelatedToPersonId = table.Column<int>(type: "integer", nullable: true),
                    KinshipKind = table.Column<int>(type: "integer", nullable: true),
                    KinshipDegree = table.Column<int>(type: "integer", nullable: true),
                    ContributionPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    SupportReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_LND_MigrationRelatedParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationRelatedParties_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationRelatedParties_COR_People_RelatedToPersonId",
                        column: x => x.RelatedToPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationRelatedParties_LND_MigrationLoadParts_Migratio~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationRestructurings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Cause = table.Column<int>(type: "integer", nullable: false),
                    RestructuringCount = table.Column<int>(type: "integer", nullable: false),
                    LastRestructuredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RatingAtRestructuring = table.Column<int>(type: "integer", nullable: false),
                    ConsecutivePaidInstallments = table.Column<int>(type: "integer", nullable: false),
                    PredecessorLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationRestructurings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationRestructurings_LND_MigrationLoadParts_Migratio~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationRiskData",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DaysPastDue = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Subcategory = table.Column<int>(type: "integer", nullable: false),
                    Cause = table.Column<int>(type: "integer", nullable: true),
                    PrincipalImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneralImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EnteredEOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LND_MigrationRiskData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationRiskData_LND_MigrationLoadParts_MigrationLoadP~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationSales",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadPartId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    SaleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExternalCollections = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustmentsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAtCutoff = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    SourceLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreditLineCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CollectionsReference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationSales", x => x.Id);
                    table.CheckConstraint("CK_LND_MigrationSales_BalanceAtCutoff", "\"BalanceAtCutoff\" >= 0");
                    table.ForeignKey(
                        name: "FK_LND_MigrationSales_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationSales_LND_MigrationLoadParts_MigrationLoadPart~",
                        column: x => x.MigrationLoadPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_SessionAttendances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubstitutesMemberId = table.Column<int>(type: "integer", nullable: true),
                    ArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_SessionAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_SessionAttendances_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionAttendances_LND_GoverningBodyMembers_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionAttendances_LND_GoverningBodyMembers_Substitutes~",
                        column: x => x.SubstitutesMemberId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionAttendances_LND_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "dbo",
                        principalTable: "LND_Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccountingFactParts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    BatchNumber = table.Column<int>(type: "integer", nullable: true),
                    TriggerOperationId = table.Column<long>(type: "bigint", nullable: true),
                    MonthlyCloseId = table.Column<int>(type: "integer", nullable: true),
                    MonthlyCloseReopeningId = table.Column<int>(type: "integer", nullable: true),
                    ReversedPartId = table.Column<int>(type: "integer", nullable: true),
                    CloseVersion = table.Column<int>(type: "integer", nullable: true),
                    FactType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PerLoanMessages = table.Column<bool>(type: "boolean", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    OriginEventKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    LoanCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalsByDimensionJson = table.Column<string>(type: "text", nullable: false),
                    LoanListSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PrevalidationOutcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_LND_AccountingFactParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AccountingFactParts_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountingFactParts_LND_AccountingFactParts_ReversedPar~",
                        column: x => x.ReversedPartId,
                        principalSchema: "dbo",
                        principalTable: "LND_AccountingFactParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountingFactParts_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_Activations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActivationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionItemId = table.Column<int>(type: "integer", nullable: false),
                    CheckVersion = table.Column<int>(type: "integer", nullable: false),
                    BalanceCheckSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SalesReconciliationSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsProductionEnvironment = table.Column<bool>(type: "boolean", nullable: false),
                    RequirementsJson = table.Column<string>(type: "text", nullable: false),
                    ObligatoryModelUnavailable = table.Column<bool>(type: "boolean", nullable: false),
                    CountsJson = table.Column<string>(type: "text", nullable: false),
                    NonDeductionsMarked = table.Column<int>(type: "integer", nullable: false),
                    CreditLimitsToRevalidate = table.Column<int>(type: "integer", nullable: false),
                    ProvisionalCollectionsFileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProvisionalCollectionsCount = table.Column<int>(type: "integer", nullable: false),
                    ProvisionalCollectionsReviewAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommercialAdjustmentsJson = table.Column<string>(type: "text", nullable: false),
                    PayrollWarningAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    ActivatedMessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_LND_Activations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_Activations_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Activations_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_Setup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActivationId = table.Column<int>(type: "integer", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_Setup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_Setup_LND_Activations_ActivationId",
                        column: x => x.ActivationId,
                        principalSchema: "dbo",
                        principalTable: "LND_Activations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AttributionLevels",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatrixId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GoverningBodyId = table.Column<int>(type: "integer", nullable: true),
                    MaxConsolidatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsExceptionLevel = table.Column<bool>(type: "boolean", nullable: false),
                    IsBoardLevel = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LND_AttributionLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AttributionLevels_LND_GoverningBodies_GoverningBodyId",
                        column: x => x.GoverningBodyId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AttributionMatrices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LegalSource = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SessionItemId = table.Column<int>(type: "integer", nullable: true),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_AttributionMatrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AttributionMatrices_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AttributionRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MatrixId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: true),
                    ModalityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    MinConsolidatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxConsolidatedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinTermMonths = table.Column<int>(type: "integer", nullable: true),
                    MaxTermMonths = table.Column<int>(type: "integer", nullable: true),
                    CollateralKind = table.Column<int>(type: "integer", nullable: true),
                    ScoreBelowMinimum = table.Column<bool>(type: "boolean", nullable: true),
                    HasWarnings = table.Column<bool>(type: "boolean", nullable: true),
                    PolicyException = table.Column<bool>(type: "boolean", nullable: true),
                    MinDaysPastDue = table.Column<int>(type: "integer", nullable: true),
                    MaxDaysPastDue = table.Column<int>(type: "integer", nullable: true),
                    Concept = table.Column<int>(type: "integer", nullable: true),
                    MinimumLevelId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_AttributionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AttributionRules_LND_AttributionLevels_MinimumLevelId",
                        column: x => x.MinimumLevelId,
                        principalSchema: "dbo",
                        principalTable: "LND_AttributionLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AttributionRules_LND_AttributionMatrices_MatrixId",
                        column: x => x.MatrixId,
                        principalSchema: "dbo",
                        principalTable: "LND_AttributionMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AttributionRules_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_BureauQueries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    BureauCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AuthorizationConsentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    NegativeMarks = table.Column<int>(type: "integer", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    ReportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSimulated = table.Column<bool>(type: "boolean", nullable: false),
                    QueriedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QueriedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_LND_BureauQueries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_BureauQueries_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_BureauQueries_SEC_Users_QueriedByUserId",
                        column: x => x.QueriedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_Collaterals",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OwnerPersonId = table.Column<int>(type: "integer", nullable: false),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdmissibleValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValuationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AppraiserPersonId = table.Column<int>(type: "integer", nullable: true),
                    PolicyNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PolicyInsurerPersonId = table.Column<int>(type: "integer", nullable: true),
                    PolicyExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RegistrationReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    MortgageRank = table.Column<byte>(type: "smallint", nullable: true),
                    FundCoverage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    FundFee = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ConstitutedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    ReleasedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReleasedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReleaseOperationId = table.Column<long>(type: "bigint", nullable: true),
                    ExtinguishedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExtinguishReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExecutedOn = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_Collaterals", x => x.Id);
                    table.CheckConstraint("CK_LND_Collaterals_Kind", "\"Kind\" = 3 OR \"Kind\" = 4 OR \"Kind\" = 5 OR \"Kind\" = 6");
                    table.ForeignKey(
                        name: "FK_LND_Collaterals_COR_People_AppraiserPersonId",
                        column: x => x.AppraiserPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Collaterals_COR_People_OwnerPersonId",
                        column: x => x.OwnerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Collaterals_COR_People_PolicyInsurerPersonId",
                        column: x => x.PolicyInsurerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Collaterals_SEC_Users_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditApplicationChecks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationId = table.Column<int>(type: "integer", nullable: false),
                    PartyId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    IsExceptionable = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    ScreeningResultId = table.Column<int>(type: "integer", nullable: true),
                    BureauQueryId = table.Column<int>(type: "integer", nullable: true),
                    CreditScoreId = table.Column<int>(type: "integer", nullable: true),
                    EvidenceSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_LND_CreditApplicationChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationChecks_LND_BureauQueries_BureauQueryId",
                        column: x => x.BureauQueryId,
                        principalSchema: "dbo",
                        principalTable: "LND_BureauQueries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditApplicationDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationId = table.Column<int>(type: "integer", nullable: false),
                    RequirementId = table.Column<int>(type: "integer", nullable: true),
                    PartyId = table.Column<int>(type: "integer", nullable: true),
                    AttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditApplicationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationDocuments_LND_CreditLineDocumentRequir~",
                        column: x => x.RequirementId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineDocumentRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationDocuments_SEC_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditApplicationParties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    MonthlyIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MonthlyExpenses = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExternalInstallments = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditApplicationParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationParties_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditApplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ApplicantPersonId = table.Column<int>(type: "integer", nullable: false),
                    DebtorKind = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TermMonths = table.Column<int>(type: "integer", nullable: true),
                    Installments = table.Column<int>(type: "integer", nullable: true),
                    Periodicity = table.Column<int>(type: "integer", nullable: true),
                    AmortizationSystem = table.Column<int>(type: "integer", nullable: true),
                    FirstDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedDisbursementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GraceKind = table.Column<int>(type: "integer", nullable: true),
                    GraceInstallments = table.Column<int>(type: "integer", nullable: true),
                    ExtraordinaryInstallments = table.Column<string>(type: "text", nullable: true),
                    SelectedInsurances = table.Column<string>(type: "text", nullable: true),
                    SimulationSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RepaymentChannel = table.Column<int>(type: "integer", nullable: false),
                    PrepaymentChoice = table.Column<int>(type: "integer", nullable: true),
                    PayoffRequest = table.Column<string>(type: "text", nullable: true),
                    PolicyExceptionRequested = table.Column<bool>(type: "boolean", nullable: false),
                    PolicyExceptionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AttributionMatrixId = table.Column<int>(type: "integer", nullable: true),
                    RequiredLevelId = table.Column<int>(type: "integer", nullable: true),
                    RoutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RoutingSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConsolidatedExposure = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsRelatedParty = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedTerms = table.Column<string>(type: "text", nullable: true),
                    ApprovalDecisionId = table.Column<int>(type: "integer", nullable: true),
                    ApprovalValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    CreditLimitId = table.Column<int>(type: "integer", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_COR_People_ApplicantPersonId",
                        column: x => x.ApplicantPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_LND_AttributionLevels_RequiredLevelId",
                        column: x => x.RequiredLevelId,
                        principalSchema: "dbo",
                        principalTable: "LND_AttributionLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_LND_AttributionMatrices_AttributionM~",
                        column: x => x.AttributionMatrixId,
                        principalSchema: "dbo",
                        principalTable: "LND_AttributionMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_LND_CreditLineVersions_CreditLineVer~",
                        column: x => x.CreditLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_SEC_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplications_SEC_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditScores",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApplicationId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    MinimumScore = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    BelowMinimum = table.Column<bool>(type: "boolean", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    InputsSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_LND_CreditScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditScores_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditScores_LND_CreditApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditScores_LND_CreditLineVersions_CreditLineVersionId",
                        column: x => x.CreditLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditDecisions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubjectType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseRootPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributionMatrixId = table.Column<int>(type: "integer", nullable: true),
                    LevelId = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Conditions = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPolicyException = table.Column<bool>(type: "boolean", nullable: false),
                    DecisionSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedByPersonId = table.Column<int>(type: "integer", nullable: true),
                    SessionItemId = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    SupersedesDecisionId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditDecisions", x => x.Id);
                    table.CheckConstraint("CK_LND_CreditDecisions_Decider", "(\"DecidedByUserId\" IS NOT NULL AND \"SessionItemId\" IS NULL) OR (\"DecidedByUserId\" IS NULL AND \"SessionItemId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_CreditDecisions_COR_People_DecidedByPersonId",
                        column: x => x.DecidedByPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditDecisions_LND_AttributionLevels_LevelId",
                        column: x => x.LevelId,
                        principalSchema: "dbo",
                        principalTable: "LND_AttributionLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditDecisions_LND_AttributionMatrices_AttributionMatr~",
                        column: x => x.AttributionMatrixId,
                        principalSchema: "dbo",
                        principalTable: "LND_AttributionMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditDecisions_LND_CreditDecisions_SupersedesDecisionId",
                        column: x => x.SupersedesDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditDecisions_SEC_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLimits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    DecisionId = table.Column<int>(type: "integer", nullable: true),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CalculationDetail = table.Column<string>(type: "text", nullable: true),
                    ContributionBalanceLoadId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditLimits_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditLimits_LND_ContributionBalanceLoads_ContributionB~",
                        column: x => x.ContributionBalanceLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_ContributionBalanceLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditLimits_LND_CreditApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditLimits_LND_CreditDecisions_DecisionId",
                        column: x => x.DecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditLimits_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditLimits_SEC_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_SessionItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Rule = table.Column<int>(type: "integer", nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubjectPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    ExcludedPersons = table.Column<string>(type: "text", nullable: true),
                    RecommendsToBodyId = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    Conditions = table.Column<string>(type: "text", nullable: true),
                    VotesFor = table.Column<int>(type: "integer", nullable: true),
                    VotesAgainst = table.Column<int>(type: "integer", nullable: true),
                    Abstentions = table.Column<int>(type: "integer", nullable: true),
                    EvaluationExplanation = table.Column<string>(type: "text", nullable: true),
                    DecisionId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_SessionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_SessionItems_LND_CreditDecisions_DecisionId",
                        column: x => x.DecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionItems_LND_GoverningBodies_RecommendsToBodyId",
                        column: x => x.RecommendsToBodyId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionItems_LND_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "dbo",
                        principalTable: "LND_Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_ScreeningResults",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Context = table.Column<int>(type: "integer", nullable: false),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    CreditLimitId = table.Column<int>(type: "integer", nullable: true),
                    SalePaymentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChannelCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ListVersions = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Matches = table.Column<string>(type: "text", nullable: true),
                    IsSimulated = table.Column<bool>(type: "boolean", nullable: false),
                    QueriedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QueriedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_ScreeningResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ScreeningResults_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ScreeningResults_LND_CreditApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ScreeningResults_LND_CreditLimits_CreditLimitId",
                        column: x => x.CreditLimitId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ScreeningResults_SEC_Users_QueriedByUserId",
                        column: x => x.QueriedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ScreeningResults_SEC_Users_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationLoadDifferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: false),
                    CheckVersion = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DimensionKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ConceptFamily = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ModalityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SubsegmentCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RiskCategory = table.Column<int>(type: "integer", nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    AccountSetKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountSetDisplay = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreditPaymentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    LendingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AcceptanceReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SessionItemId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationLoadDifferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoadDifferences_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoadDifferences_LND_MigrationLoads_MigrationLo~",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoadDifferences_LND_SessionItems_SessionItemId",
                        column: x => x.SessionItemId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationLoadDifferences_SEC_Users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_SessionVotes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    PresenceChallengePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplacesVoteId = table.Column<int>(type: "integer", nullable: true),
                    ReplacementReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReplacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_SessionVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_SessionVotes_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionVotes_LND_GoverningBodyMembers_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "dbo",
                        principalTable: "LND_GoverningBodyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionVotes_LND_SessionItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionVotes_LND_SessionVotes_ReplacesVoteId",
                        column: x => x.ReplacesVoteId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionVotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_SessionVotes_SEC_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditReportClaims",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BureauCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExtendedDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExtensionNotifiedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AnsweredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AnswerUpholdsClaim = table.Column<bool>(type: "boolean", nullable: true),
                    AnswerSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AnswerAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    TacitAcceptanceOn = table.Column<DateOnly>(type: "date", nullable: true),
                    WithdrawnOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DataSubjectRequestId = table.Column<int>(type: "integer", nullable: true),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditReportClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditReportClaims_COR_DataSubjectRequests_DataSubjectR~",
                        column: x => x.DataSubjectRequestId,
                        principalSchema: "dbo",
                        principalTable: "COR_DataSubjectRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditReportClaims_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditReportClaims_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DebtorBlocks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    LiftedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LiftedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LiftedByUserId = table.Column<int>(type: "integer", nullable: true),
                    LiftReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_DebtorBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DebtorBlocks_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DebtorBlocks_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DebtorBlocks_SEC_Users_LiftedByUserId",
                        column: x => x.LiftedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DebtorMessageEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DebtorMessageId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReasonText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DestinationAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ExternalOutcome = table.Column<int>(type: "integer", nullable: true),
                    ProviderArtifactAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_DebtorMessageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DebtorMessageEvents_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DebtorMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    RecipientRole = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: true),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: true),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RenderedBody = table.Column<string>(type: "text", nullable: false),
                    BodySha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OriginPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PriorNoticeId = table.Column<int>(type: "integer", nullable: true),
                    IssuedDocumentId = table.Column<int>(type: "integer", nullable: true),
                    InstallmentNonDeductionId = table.Column<int>(type: "integer", nullable: true),
                    AuthorizationConsentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsManualImmediate = table.Column<bool>(type: "boolean", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedByKind = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_DebtorMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DebtorMessages_COR_NotificationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "dbo",
                        principalTable: "COR_NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DebtorMessages_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DebtorMessages_LND_IssuedDocuments_IssuedDocumentId",
                        column: x => x.IssuedDocumentId,
                        principalSchema: "dbo",
                        principalTable: "LND_IssuedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DebtorMessages_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DisbursementFileLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryPersonId = table.Column<int>(type: "integer", nullable: false),
                    DestinationBankId = table.Column<int>(type: "integer", nullable: true),
                    AccountType = table.Column<byte>(type: "smallint", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordText = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
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
                    table.PrimaryKey("PK_LND_DisbursementFileLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementFileLines_COR_Banks_DestinationBankId",
                        column: x => x.DestinationBankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementFileLines_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementFileLines_LND_DisbursementFiles_FileId",
                        column: x => x.FileId,
                        principalSchema: "dbo",
                        principalTable: "LND_DisbursementFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DisbursementOrderResults",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    InboundMessageId = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    ResultDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TreasuryReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReversalOperationId = table.Column<long>(type: "bigint", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_LND_DisbursementOrderResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_DisbursementOrders",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    DisbursementOperationId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryPersonId = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryTaxIdType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BeneficiaryTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BeneficiaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DestinationBankId = table.Column<int>(type: "integer", nullable: true),
                    DestinationAccountType = table.Column<byte>(type: "smallint", nullable: true),
                    DestinationAccountNumber = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayoffAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResultDueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    VoidRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidRequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PreviousOrderId = table.Column<int>(type: "integer", nullable: true),
                    PaidByFile = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentDisbursementFileId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_DisbursementOrders", x => x.Id);
                    table.CheckConstraint("CK_LND_DisbursementOrders_Form", "\"Form\" <> 4");
                    table.CheckConstraint("CK_LND_DisbursementOrders_Net", "\"NetAmount\" = \"GrossAmount\" - \"PayoffAmount\" AND \"NetAmount\" > 0");
                    table.CheckConstraint("CK_LND_DisbursementOrders_PaidByFile", "\"PaidByFile\" = FALSE OR \"Form\" = 1");
                    table.ForeignKey(
                        name: "FK_LND_DisbursementOrders_COR_Banks_DestinationBankId",
                        column: x => x.DestinationBankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementOrders_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementOrders_LND_DisbursementFiles_CurrentDisburs~",
                        column: x => x.CurrentDisbursementFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_DisbursementFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementOrders_LND_DisbursementOrders_PreviousOrder~",
                        column: x => x.PreviousOrderId,
                        principalSchema: "dbo",
                        principalTable: "LND_DisbursementOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementOrders_SEC_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DisbursementOrders_SEC_Users_VoidRequestedByUserId",
                        column: x => x.VoidRequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_InboundMessageEvaluations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InboundMessageId = table.Column<int>(type: "integer", nullable: false),
                    EvaluationNumber = table.Column<int>(type: "integer", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    CreditLineCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ChecksJson = table.Column<string>(type: "text", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Retryable = table.Column<bool>(type: "boolean", nullable: false),
                    PrevalidationOutcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ResponseMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_InboundMessageEvaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_InboundMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MessageVersion = table.Column<short>(type: "smallint", nullable: false),
                    OriginModule = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OriginPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OriginEventKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RelatedPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChainRootPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: true),
                    CreditPaymentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservationPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReservedLoanNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    DisbursementOrderId = table.Column<int>(type: "integer", nullable: true),
                    SaleReconciliationId = table.Column<int>(type: "integer", nullable: true),
                    EvaluationCount = table.Column<int>(type: "integer", nullable: false),
                    LastEvaluationId = table.Column<int>(type: "integer", nullable: true),
                    ResponseMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotRetryableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotRetryableByUserId = table.Column<int>(type: "integer", nullable: true),
                    NotRetryableReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirstReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExceptionDecisionId = table.Column<int>(type: "integer", nullable: true),
                    ExceptionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmedByExceptionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedByExceptionByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_InboundMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_InboundMessages_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_InboundMessages_LND_CreditDecisions_ExceptionDecisionId",
                        column: x => x.ExceptionDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_InboundMessages_LND_DisbursementOrders_DisbursementOrde~",
                        column: x => x.DisbursementOrderId,
                        principalSchema: "dbo",
                        principalTable: "LND_DisbursementOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_InboundMessages_LND_InboundMessageEvaluations_LastEvalu~",
                        column: x => x.LastEvaluationId,
                        principalSchema: "dbo",
                        principalTable: "LND_InboundMessageEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_InboundMessages_SEC_Users_ConfirmedByExceptionByUserId",
                        column: x => x.ConfirmedByExceptionByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_InboundMessages_SEC_Users_NotRetryableByUserId",
                        column: x => x.NotRetryableByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_InstallmentNonDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    RepaymentInstallmentId = table.Column<long>(type: "bigint", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    ReasonText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NotDeductedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContractOperationKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ActivationId = table.Column<int>(type: "integer", nullable: true),
                    AlertPublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_InstallmentNonDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_InstallmentNonDeductions_LND_Activations_ActivationId",
                        column: x => x.ActivationId,
                        principalSchema: "dbo",
                        principalTable: "LND_Activations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_InsuranceClaims",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    InsurerPersonId = table.Column<int>(type: "integer", nullable: false),
                    DeceasedPersonId = table.Column<int>(type: "integer", nullable: false),
                    DeathDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeathCertificateAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FiledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ClaimedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaidOperationId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_InsuranceClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_InsuranceClaims_COR_People_DeceasedPersonId",
                        column: x => x.DeceasedPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_InsuranceClaims_COR_People_InsurerPersonId",
                        column: x => x.InsurerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_InsuranceClaims_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_IssuedDocumentEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssuedDocumentId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CausingOperationId = table.Column<long>(type: "bigint", nullable: true),
                    DebtorMessageId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_IssuedDocumentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocumentEvents_LND_DebtorMessages_DebtorMessageId",
                        column: x => x.DebtorMessageId,
                        principalSchema: "dbo",
                        principalTable: "LND_DebtorMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocumentEvents_LND_IssuedDocuments_IssuedDocument~",
                        column: x => x.IssuedDocumentId,
                        principalSchema: "dbo",
                        principalTable: "LND_IssuedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocumentEvents_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_IssuedDocumentLoans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssuedDocumentId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    LastOperationId = table.Column<long>(type: "bigint", nullable: true),
                    LastTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    BalanceAtCutoff = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PayoffOperationId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_LND_IssuedDocumentLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocumentLoans_LND_IssuedDocuments_IssuedDocumentId",
                        column: x => x.IssuedDocumentId,
                        principalSchema: "dbo",
                        principalTable: "LND_IssuedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanControls",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    LastAccruedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    LastRegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastPositionDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanDailyPositions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PositionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    ModalityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubsegmentCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrincipalPastDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentInterestInSuspense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultInterestInSuspense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Insurance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectionFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Advance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PayoffAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DaysPastDue = table.Column<int>(type: "integer", nullable: false),
                    OldestUnpaidInstallment = table.Column<int>(type: "integer", nullable: true),
                    InstallmentsPastDue = table.Column<int>(type: "integer", nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    UsuryCapped = table.Column<bool>(type: "boolean", nullable: false),
                    AppliedAnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DailyRating = table.Column<int>(type: "integer", nullable: true),
                    DailyRatingSubcategory = table.Column<int>(type: "integer", nullable: true),
                    InputsSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_LND_LoanDailyPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanDailyPositions_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanDailyPositions_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanNumberReservations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: true),
                    CreditLimitId = table.Column<int>(type: "integer", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SalesDocumentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultationEvidence = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReservedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanNumberReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanNumberReservations_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNumberReservations_LND_CreditLimits_CreditLimitId",
                        column: x => x.CreditLimitId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNumberReservations_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNumberReservations_SEC_Users_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNumberReservations_SEC_Users_ReservedByUserId",
                        column: x => x.ReservedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_Loans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DebtorPersonId = table.Column<int>(type: "integer", nullable: false),
                    DebtorKind = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReservationId = table.Column<int>(type: "integer", nullable: true),
                    CreditLimitId = table.Column<int>(type: "integer", nullable: true),
                    SourceSalesDocumentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourcePaymentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceInvoiceNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SourceMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountsReceivableRecordedBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SaleObligationStatus = table.Column<int>(type: "integer", nullable: true),
                    ValidationFailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ValidationFailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConfirmedByExceptionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedByExceptionDecisionId = table.Column<int>(type: "integer", nullable: true),
                    OriginLoanId = table.Column<int>(type: "integer", nullable: true),
                    OriginLinkKind = table.Column<int>(type: "integer", nullable: true),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: true),
                    PaidOffOn = table.Column<DateOnly>(type: "date", nullable: true),
                    WrittenOffOn = table.Column<DateOnly>(type: "date", nullable: true),
                    NovatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    VoidedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_Loans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_Loans_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Loans_COR_People_DebtorPersonId",
                        column: x => x.DebtorPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Loans_LND_CreditApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Loans_LND_CreditDecisions_ConfirmedByExceptionDecisionId",
                        column: x => x.ConfirmedByExceptionDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Loans_LND_CreditLimits_CreditLimitId",
                        column: x => x.CreditLimitId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Loans_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Loans_LND_LoanNumberReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanNumberReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Loans_LND_Loans_OriginLoanId",
                        column: x => x.OriginLoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Loans_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanOperations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    OriginUserCentralId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectorClass = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CollectorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RequestSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PaymentModality = table.Column<int>(type: "integer", nullable: true),
                    PrepaymentChoice = table.Column<int>(type: "integer", nullable: true),
                    ChoiceFromPreference = table.Column<bool>(type: "boolean", nullable: true),
                    PaymentMeansClass = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PayerPersonId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceiptBranchPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptMeansReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PaymasterPendingLineReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SimulationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExcessAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FinalityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReversesOperationId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByOperationId = table.Column<long>(type: "bigint", nullable: true),
                    RelatedOperationId = table.Column<long>(type: "bigint", nullable: true),
                    DisbursementForm = table.Column<int>(type: "integer", nullable: true),
                    TrancheNumber = table.Column<int>(type: "integer", nullable: true),
                    InboundMessageId = table.Column<int>(type: "integer", nullable: true),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: true),
                    InsuranceClaimId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    InputsSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrevalidationOutcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_COR_People_PayerPersonId",
                        column: x => x.PayerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_LND_InboundMessages_InboundMessageId",
                        column: x => x.InboundMessageId,
                        principalSchema: "dbo",
                        principalTable: "LND_InboundMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_LND_InsuranceClaims_InsuranceClaimId",
                        column: x => x.InsuranceClaimId,
                        principalSchema: "dbo",
                        principalTable: "LND_InsuranceClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_LND_LoanOperations_RelatedOperationId",
                        column: x => x.RelatedOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_LND_LoanOperations_ReversedByOperationId",
                        column: x => x.ReversedByOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_LND_LoanOperations_ReversesOperationId",
                        column: x => x.ReversesOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanOperations_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MigrationSaleReconciliations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: false),
                    CheckVersion = table.Column<int>(type: "integer", nullable: false),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleDocumentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreditPaymentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    MigrationLoanRowId = table.Column<int>(type: "integer", nullable: true),
                    MigrationSaleRowId = table.Column<int>(type: "integer", nullable: true),
                    MarkedFromList = table.Column<bool>(type: "boolean", nullable: false),
                    MarkReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MarkedByUserId = table.Column<int>(type: "integer", nullable: true),
                    MarkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DifferenceId = table.Column<int>(type: "integer", nullable: true),
                    AdjustmentsUpToCutoff = table.Column<int>(type: "integer", nullable: false),
                    AdjustmentsAfterCutoff = table.Column<int>(type: "integer", nullable: false),
                    ResultingLoanId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MigrationSaleReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MigrationSaleReconciliations_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationSaleReconciliations_LND_Loans_ResultingLoanId",
                        column: x => x.ResultingLoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationSaleReconciliations_LND_MigrationLoadDifferenc~",
                        column: x => x.DifferenceId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoadDifferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationSaleReconciliations_LND_MigrationLoads_Migrati~",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationSaleReconciliations_LND_MigrationLoans_Migrati~",
                        column: x => x.MigrationLoanRowId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationSaleReconciliations_LND_MigrationSales_Migrati~",
                        column: x => x.MigrationSaleRowId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationSales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MigrationSaleReconciliations_SEC_Users_MarkedByUserId",
                        column: x => x.MarkedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PriorNotices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    EpisodeStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequiredCommunications = table.Column<byte>(type: "smallint", nullable: false),
                    MinimumDays = table.Column<int>(type: "integer", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: true),
                    MigratedFirstSentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MigratedSecondSentOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MigratedEvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MigratedReportedNegativeSince = table.Column<DateOnly>(type: "date", nullable: true),
                    MigratedBureauCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_LND_PriorNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PriorNotices_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PriorNotices_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PriorNotices_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_ProcessRunErrors",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    BatchNumber = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Step = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DetailJson = table.Column<string>(type: "text", nullable: true),
                    AlertPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_ProcessRunErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ProcessRunErrors_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ProcessRunErrors_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PromissoryNotes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustodianChannelCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ExternalNoteId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsSimulated = table.Column<bool>(type: "boolean", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InCustodyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndorsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndorseeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PhysicalLocation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    InstructionsAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReleasedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_PromissoryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PromissoryNotes_LND_CreditApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PromissoryNotes_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanParties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceApplicationPartyId = table.Column<int>(type: "integer", nullable: true),
                    ReleasedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReleaseOperationId = table.Column<long>(type: "bigint", nullable: true),
                    ExtinguishedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExtinguishReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExtinguishDecisionId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanParties_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanParties_LND_CreditApplicationParties_SourceApplicat~",
                        column: x => x.SourceApplicationPartyId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplicationParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanParties_LND_CreditDecisions_ExtinguishDecisionId",
                        column: x => x.ExtinguishDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanParties_LND_LoanOperations_ReleaseOperationId",
                        column: x => x.ReleaseOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanParties_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanPayrollLinks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    RepaymentChannel = table.Column<int>(type: "integer", nullable: false),
                    PaymasterPersonId = table.Column<int>(type: "integer", nullable: true),
                    IsOwnPayroll = table.Column<bool>(type: "boolean", nullable: false),
                    EmployeeIdAtPaymaster = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    LibranzaDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificationAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertifiedNetIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CertifiedAvailableCapacity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    PreviousLinkId = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanPayrollLinks", x => x.Id);
                    table.CheckConstraint("CK_LND_LoanPayrollLinks_Paymaster", "((\"RepaymentChannel\" = 2 OR \"RepaymentChannel\" = 3) AND \"PaymasterPersonId\" IS NOT NULL) OR ((\"RepaymentChannel\" = 1 OR \"RepaymentChannel\" = 4) AND \"PaymasterPersonId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_LND_LoanPayrollLinks_COR_People_PaymasterPersonId",
                        column: x => x.PaymasterPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanPayrollLinks_LND_LoanOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanPayrollLinks_LND_LoanPayrollLinks_PreviousLinkId",
                        column: x => x.PreviousLinkId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanPayrollLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanPayrollLinks_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanTermsVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Cause = table.Column<int>(type: "integer", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreditLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    ModalityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubsegmentCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ForEmployees = table.Column<bool>(type: "boolean", nullable: false),
                    InterestTaxTreatment = table.Column<int>(type: "integer", nullable: true),
                    RateKind = table.Column<int>(type: "integer", nullable: false),
                    AnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    IndexCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IndexKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Margin = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RateFloor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RateCeiling = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ResetPeriodicity = table.Column<int>(type: "integer", nullable: true),
                    NextResetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DefaultRateKind = table.Column<int>(type: "integer", nullable: false),
                    DefaultRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DefaultRateFactor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DefaultOnInterestAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    DayCount = table.Column<int>(type: "integer", nullable: false),
                    RateEquivalence = table.Column<int>(type: "integer", nullable: false),
                    AmortizationSystem = table.Column<int>(type: "integer", nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    Installments = table.Column<int>(type: "integer", nullable: false),
                    TermMonths = table.Column<int>(type: "integer", nullable: false),
                    FirstDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDay = table.Column<byte>(type: "smallint", nullable: true),
                    GraceKind = table.Column<int>(type: "integer", nullable: true),
                    GraceInstallments = table.Column<int>(type: "integer", nullable: true),
                    GraceInterestTreatment = table.Column<int>(type: "integer", nullable: true),
                    GraceDistributionInstallments = table.Column<int>(type: "integer", nullable: true),
                    ExtraordinaryInstallments = table.Column<string>(type: "text", nullable: true),
                    RoundingUnit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoundingMode = table.Column<int>(type: "integer", nullable: false),
                    RoundingMoment = table.Column<int>(type: "integer", nullable: false),
                    AllocationOrder = table.Column<int>(type: "integer", nullable: false),
                    ConceptPriority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SuspenseWithItsConcept = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsSpecificInstallmentOutOfOrder = table.Column<bool>(type: "boolean", nullable: false),
                    NoteRecalculation = table.Column<int>(type: "integer", nullable: false),
                    Insurances = table.Column<string>(type: "text", nullable: false),
                    ResultingEffectiveRate = table.Column<decimal>(type: "numeric(28,18)", precision: 28, scale: 18, nullable: true),
                    ChargesSpread = table.Column<decimal>(type: "numeric(28,18)", precision: 28, scale: 18, nullable: true),
                    UsuryValuePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RestructuringCount = table.Column<int>(type: "integer", nullable: false),
                    RestructuredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    InputsSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_LND_LoanTermsVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanTermsVersions_LND_CreditLineVersions_CreditLineVers~",
                        column: x => x.CreditLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanTermsVersions_LND_LoanOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanTermsVersions_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanTransactions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Concept = table.Column<int>(type: "integer", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReversesTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    InControl = table.Column<bool>(type: "boolean", nullable: false),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanTransactions", x => x.Id);
                    table.CheckConstraint("CK_LND_LoanTransactions_Amount", "\"Amount\" <> 0");
                    table.CheckConstraint("CK_LND_LoanTransactions_Origin", "(\"OperationId\" IS NOT NULL AND \"ProcessRunId\" IS NULL) OR (\"OperationId\" IS NULL AND \"ProcessRunId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_LoanTransactions_LND_LoanOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanTransactions_LND_LoanTransactions_ReversesTransacti~",
                        column: x => x.ReversesTransactionId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanTransactions_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanTransactions_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PrepaymentPreferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    CreditLimitId = table.Column<int>(type: "integer", nullable: true),
                    Choice = table.Column<int>(type: "integer", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    ApplicationId = table.Column<int>(type: "integer", nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    EvidenceAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_PrepaymentPreferences", x => x.Id);
                    table.CheckConstraint("CK_LND_PrepaymentPreferences_Scope", "(\"LoanId\" IS NOT NULL AND \"CreditLimitId\" IS NULL) OR (\"LoanId\" IS NULL AND \"CreditLimitId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_PrepaymentPreferences_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PrepaymentPreferences_LND_CreditApplications_Applicatio~",
                        column: x => x.ApplicationId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PrepaymentPreferences_LND_CreditLimits_CreditLimitId",
                        column: x => x.CreditLimitId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PrepaymentPreferences_LND_LoanOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PrepaymentPreferences_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PrepaymentPreferences_SEC_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_ProvisionalCollections",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivationId = table.Column<int>(type: "integer", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_ProvisionalCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ProvisionalCollections_LND_Activations_ActivationId",
                        column: x => x.ActivationId,
                        principalSchema: "dbo",
                        principalTable: "LND_Activations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ProvisionalCollections_LND_LoanOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ProvisionalCollections_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_RepaymentScheduleVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    TermsVersionId = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    EffectiveValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrincipalBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FirstRegeneratedInstallment = table.Column<int>(type: "integer", nullable: false),
                    InstallmentCount = table.Column<int>(type: "integer", nullable: false),
                    IsMigratedPlan = table.Column<bool>(type: "boolean", nullable: false),
                    RulesVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InputsSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_LND_RepaymentScheduleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_RepaymentScheduleVersions_LND_LoanOperations_OperationId",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RepaymentScheduleVersions_LND_LoanTermsVersions_TermsVe~",
                        column: x => x.TermsVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanTermsVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RepaymentScheduleVersions_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_RepaymentInstallments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleVersionId = table.Column<int>(type: "integer", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExigibleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpeningPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Interest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GraceInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Insurance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherCharges = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosingPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsExtraordinary = table.Column<bool>(type: "boolean", nullable: false),
                    IsGraceInstallment = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LND_RepaymentInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_RepaymentInstallments_LND_RepaymentScheduleVersions_Sch~",
                        column: x => x.ScheduleVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_RepaymentScheduleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountingFactParts_AccountingDate_FactType",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                columns: new[] { "AccountingDate", "FactType" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountingFactParts_BranchId",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountingFactParts_MonthlyCloseId",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "MonthlyCloseId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountingFactParts_MonthlyCloseReopeningId",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "MonthlyCloseReopeningId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountingFactParts_ReversedPartId",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "ReversedPartId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountingFactParts_Batch",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                columns: new[] { "ProcessRunId", "BatchNumber", "BranchId", "FactType" },
                unique: true,
                filter: "\"ProcessRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountingFactParts_MessagePublicId",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "MessagePublicId",
                unique: true,
                filter: "\"MessagePublicId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountingFactParts_PublicId",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountingFactParts_Trigger_Branch_Fact",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                columns: new[] { "TriggerOperationId", "BranchId", "FactType" },
                unique: true,
                filter: "\"TriggerOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Activations_RegisteredByUserId",
                schema: "dbo",
                table: "LND_Activations",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Activations_SessionItemId",
                schema: "dbo",
                table: "LND_Activations",
                column: "SessionItemId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Activations_MigrationLoadId",
                schema: "dbo",
                table: "LND_Activations",
                column: "MigrationLoadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Activations_PublicId",
                schema: "dbo",
                table: "LND_Activations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AttributionLevels_GoverningBodyId",
                schema: "dbo",
                table: "LND_AttributionLevels",
                column: "GoverningBodyId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AttributionLevels_Matrix_Code",
                schema: "dbo",
                table: "LND_AttributionLevels",
                columns: new[] { "MatrixId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AttributionLevels_Matrix_Rank",
                schema: "dbo",
                table: "LND_AttributionLevels",
                columns: new[] { "MatrixId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AttributionLevels_PublicId",
                schema: "dbo",
                table: "LND_AttributionLevels",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AttributionMatrices_RegisteredByUserId",
                schema: "dbo",
                table: "LND_AttributionMatrices",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AttributionMatrices_SessionItemId",
                schema: "dbo",
                table: "LND_AttributionMatrices",
                column: "SessionItemId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AttributionMatrices_PublicId",
                schema: "dbo",
                table: "LND_AttributionMatrices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AttributionMatrices_Version",
                schema: "dbo",
                table: "LND_AttributionMatrices",
                column: "VersionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AttributionRules_CreditLineId",
                schema: "dbo",
                table: "LND_AttributionRules",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AttributionRules_MinimumLevelId",
                schema: "dbo",
                table: "LND_AttributionRules",
                column: "MinimumLevelId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AttributionRules_Matrix_Order",
                schema: "dbo",
                table: "LND_AttributionRules",
                columns: new[] { "MatrixId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AttributionRules_PublicId",
                schema: "dbo",
                table: "LND_AttributionRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_BureauQueries_ApplicationId",
                schema: "dbo",
                table: "LND_BureauQueries",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BureauQueries_PersonId",
                schema: "dbo",
                table: "LND_BureauQueries",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_BureauQueries_QueriedByUserId",
                schema: "dbo",
                table: "LND_BureauQueries",
                column: "QueriedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_BureauQueries_PublicId",
                schema: "dbo",
                table: "LND_BureauQueries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CaseParticipations_Person_Root",
                schema: "dbo",
                table: "LND_CaseParticipations",
                columns: new[] { "PersonId", "CaseRootPublicId" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CaseParticipations_Root_Action",
                schema: "dbo",
                table: "LND_CaseParticipations",
                columns: new[] { "CaseRootPublicId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CaseParticipations_UserId",
                schema: "dbo",
                table: "LND_CaseParticipations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CaseParticipations_PublicId",
                schema: "dbo",
                table: "LND_CaseParticipations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_Collaterals_ApplicationId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Collaterals_AppraiserPersonId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "AppraiserPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Collaterals_LoanId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Collaterals_OwnerPersonId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "OwnerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Collaterals_PolicyInsurerPersonId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "PolicyInsurerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Collaterals_ReleasedByUserId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Collaterals_ReleaseOperationId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "ReleaseOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Collaterals_PublicId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CommercialTermEquivalences_Kind_Code_Version",
                schema: "dbo",
                table: "LND_CommercialTermEquivalences",
                columns: new[] { "Kind", "CommercialCode", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CommercialTermEquivalences_PublicId",
                schema: "dbo",
                table: "LND_CommercialTermEquivalences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ConnectedGroupMembers_PersonId",
                schema: "dbo",
                table: "LND_ConnectedGroupMembers",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ConnectedGroupMembers_Group_Person",
                schema: "dbo",
                table: "LND_ConnectedGroupMembers",
                columns: new[] { "GroupId", "PersonId" },
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ConnectedGroupMembers_PublicId",
                schema: "dbo",
                table: "LND_ConnectedGroupMembers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ConnectedGroups_RegisteredByUserId",
                schema: "dbo",
                table: "LND_ConnectedGroups",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ConnectedGroups_PublicId",
                schema: "dbo",
                table: "LND_ConnectedGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionBalanceLoads_RegisteredByUserId",
                schema: "dbo",
                table: "LND_ContributionBalanceLoads",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContributionBalanceLoads_Person_Date_Import",
                schema: "dbo",
                table: "LND_ContributionBalanceLoads",
                columns: new[] { "PersonId", "AsOfDate", "ImportPublicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContributionBalanceLoads_PublicId",
                schema: "dbo",
                table: "LND_ContributionBalanceLoads",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationChecks_App_Kind_Stage",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                columns: new[] { "ApplicationId", "Kind", "Stage", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationChecks_BureauQueryId",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "BureauQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationChecks_CreditScoreId",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "CreditScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationChecks_PartyId",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationChecks_ScreeningResultId",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "ScreeningResultId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationChecks_PublicId",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationDocuments_ApplicationId",
                schema: "dbo",
                table: "LND_CreditApplicationDocuments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationDocuments_PartyId",
                schema: "dbo",
                table: "LND_CreditApplicationDocuments",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationDocuments_RequirementId",
                schema: "dbo",
                table: "LND_CreditApplicationDocuments",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationDocuments_VerifiedByUserId",
                schema: "dbo",
                table: "LND_CreditApplicationDocuments",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationDocuments_PublicId",
                schema: "dbo",
                table: "LND_CreditApplicationDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationParties_PersonId",
                schema: "dbo",
                table: "LND_CreditApplicationParties",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationParties_App_Person",
                schema: "dbo",
                table: "LND_CreditApplicationParties",
                columns: new[] { "ApplicationId", "PersonId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationParties_PublicId",
                schema: "dbo",
                table: "LND_CreditApplicationParties",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_Applicant_Status",
                schema: "dbo",
                table: "LND_CreditApplications",
                columns: new[] { "ApplicantPersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_ApprovalDecisionId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "ApprovalDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_AttributionMatrixId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "AttributionMatrixId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_Branch_Status",
                schema: "dbo",
                table: "LND_CreditApplications",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_CancelledByUserId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_CreatedByUserId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_CreditLimitId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "CreditLimitId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_CreditLineId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_CreditLineVersionId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "CreditLineVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_LoanId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_RequiredLevelId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "RequiredLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_Status_ApprovalValidUntil",
                schema: "dbo",
                table: "LND_CreditApplications",
                columns: new[] { "Status", "ApprovalValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplications_SubmittedByUserId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplications_Number",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "Number",
                unique: true,
                filter: "\"Number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplications_PublicId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditDecisions_AttributionMatrixId",
                schema: "dbo",
                table: "LND_CreditDecisions",
                column: "AttributionMatrixId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditDecisions_DecidedByPersonId",
                schema: "dbo",
                table: "LND_CreditDecisions",
                column: "DecidedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditDecisions_DecidedByUserId",
                schema: "dbo",
                table: "LND_CreditDecisions",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditDecisions_LevelId",
                schema: "dbo",
                table: "LND_CreditDecisions",
                column: "LevelId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditDecisions_SessionItemId",
                schema: "dbo",
                table: "LND_CreditDecisions",
                column: "SessionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditDecisions_Subject_DecidedAt",
                schema: "dbo",
                table: "LND_CreditDecisions",
                columns: new[] { "SubjectType", "SubjectPublicId", "DecidedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditDecisions_SupersedesDecisionId",
                schema: "dbo",
                table: "LND_CreditDecisions",
                column: "SupersedesDecisionId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditDecisions_PublicId",
                schema: "dbo",
                table: "LND_CreditDecisions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLimits_ApplicationId",
                schema: "dbo",
                table: "LND_CreditLimits",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLimits_AssignedByUserId",
                schema: "dbo",
                table: "LND_CreditLimits",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLimits_ContributionBalanceLoadId",
                schema: "dbo",
                table: "LND_CreditLimits",
                column: "ContributionBalanceLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLimits_CreditLineId",
                schema: "dbo",
                table: "LND_CreditLimits",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLimits_DecisionId",
                schema: "dbo",
                table: "LND_CreditLimits",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLimits_Person_Kind_Line_ValidFrom",
                schema: "dbo",
                table: "LND_CreditLimits",
                columns: new[] { "PersonId", "Kind", "CreditLineId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLimits_PublicId",
                schema: "dbo",
                table: "LND_CreditLimits",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineCollateralRules_PublicId",
                schema: "dbo",
                table: "LND_CreditLineCollateralRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineCollateralRules_Version_Order",
                schema: "dbo",
                table: "LND_CreditLineCollateralRules",
                columns: new[] { "CreditLineVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineDocumentRequirements_PublicId",
                schema: "dbo",
                table: "LND_CreditLineDocumentRequirements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineDocumentRequirements_Version_Code",
                schema: "dbo",
                table: "LND_CreditLineDocumentRequirements",
                columns: new[] { "CreditLineVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLineInsurances_InsurerPersonId",
                schema: "dbo",
                table: "LND_CreditLineInsurances",
                column: "InsurerPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineInsurances_PublicId",
                schema: "dbo",
                table: "LND_CreditLineInsurances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineInsurances_Version_Code",
                schema: "dbo",
                table: "LND_CreditLineInsurances",
                columns: new[] { "CreditLineVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineRateBands_PublicId",
                schema: "dbo",
                table: "LND_CreditLineRateBands",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineRateBands_Version_Order",
                schema: "dbo",
                table: "LND_CreditLineRateBands",
                columns: new[] { "CreditLineVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLines_ClosedByUserId",
                schema: "dbo",
                table: "LND_CreditLines",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLines_Code",
                schema: "dbo",
                table: "LND_CreditLines",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLines_PublicId",
                schema: "dbo",
                table: "LND_CreditLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLineVersions_Line_ValidFrom",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                columns: new[] { "CreditLineId", "ValidFrom", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineVersions_Line_Version",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                columns: new[] { "CreditLineId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineVersions_PublicId",
                schema: "dbo",
                table: "LND_CreditLineVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditReportClaims_DataSubjectRequestId",
                schema: "dbo",
                table: "LND_CreditReportClaims",
                column: "DataSubjectRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditReportClaims_LoanId",
                schema: "dbo",
                table: "LND_CreditReportClaims",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditReportClaims_MigrationLoadId",
                schema: "dbo",
                table: "LND_CreditReportClaims",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditReportClaims_PersonId_Status",
                schema: "dbo",
                table: "LND_CreditReportClaims",
                columns: new[] { "PersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditReportClaims_PublicId",
                schema: "dbo",
                table: "LND_CreditReportClaims",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditScores_App_CalculatedAt",
                schema: "dbo",
                table: "LND_CreditScores",
                columns: new[] { "ApplicationId", "CalculatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditScores_CreditLineVersionId",
                schema: "dbo",
                table: "LND_CreditScores",
                column: "CreditLineVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditScores_PersonId",
                schema: "dbo",
                table: "LND_CreditScores",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditScores_PublicId",
                schema: "dbo",
                table: "LND_CreditScores",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorBlocks_CreatedByUserId",
                schema: "dbo",
                table: "LND_DebtorBlocks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorBlocks_LiftedByUserId",
                schema: "dbo",
                table: "LND_DebtorBlocks",
                column: "LiftedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorBlocks_LoanId",
                schema: "dbo",
                table: "LND_DebtorBlocks",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DebtorBlocks_Active",
                schema: "dbo",
                table: "LND_DebtorBlocks",
                columns: new[] { "PersonId", "Reason", "SourceKey" },
                unique: true,
                filter: "\"LiftedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DebtorBlocks_PublicId",
                schema: "dbo",
                table: "LND_DebtorBlocks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DebtorControls_PersonId",
                schema: "dbo",
                table: "LND_DebtorControls",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DebtorControls_PublicId",
                schema: "dbo",
                table: "LND_DebtorControls",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessageEvents_ActorUserId",
                schema: "dbo",
                table: "LND_DebtorMessageEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessageEvents_Deferred",
                schema: "dbo",
                table: "LND_DebtorMessageEvents",
                columns: new[] { "Kind", "DueAt" },
                filter: "\"Kind\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessageEvents_Queued",
                schema: "dbo",
                table: "LND_DebtorMessageEvents",
                columns: new[] { "Kind", "DueAt" },
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DebtorMessageEvents_Message_Sequence",
                schema: "dbo",
                table: "LND_DebtorMessageEvents",
                columns: new[] { "DebtorMessageId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DebtorMessageEvents_PublicId",
                schema: "dbo",
                table: "LND_DebtorMessageEvents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessages_InstallmentNonDeductionId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "InstallmentNonDeductionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessages_IssuedDocumentId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "IssuedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessages_LoanId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessages_PersonId_RequestedAt",
                schema: "dbo",
                table: "LND_DebtorMessages",
                columns: new[] { "PersonId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessages_PriorNoticeId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "PriorNoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessages_RequestedByUserId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessages_TemplateId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DebtorMessages_IdempotencyKey",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DebtorMessages_PublicId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementFileLines_BeneficiaryPersonId",
                schema: "dbo",
                table: "LND_DisbursementFileLines",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementFileLines_DestinationBankId",
                schema: "dbo",
                table: "LND_DisbursementFileLines",
                column: "DestinationBankId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementFileLines_OrderId",
                schema: "dbo",
                table: "LND_DisbursementFileLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementFileLines_File_Line",
                schema: "dbo",
                table: "LND_DisbursementFileLines",
                columns: new[] { "FileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementFileLines_File_Order",
                schema: "dbo",
                table: "LND_DisbursementFileLines",
                columns: new[] { "FileId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementFileLines_PublicId",
                schema: "dbo",
                table: "LND_DisbursementFileLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementFiles_BankId",
                schema: "dbo",
                table: "LND_DisbursementFiles",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementFiles_FormatId",
                schema: "dbo",
                table: "LND_DisbursementFiles",
                column: "FormatId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementFiles_GeneratedByUserId",
                schema: "dbo",
                table: "LND_DisbursementFiles",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementFiles_PaymentDate_Sequence",
                schema: "dbo",
                table: "LND_DisbursementFiles",
                columns: new[] { "PaymentDate", "DailySequence" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementFiles_VoidedByUserId",
                schema: "dbo",
                table: "LND_DisbursementFiles",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementFiles_Number",
                schema: "dbo",
                table: "LND_DisbursementFiles",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementFiles_PublicId",
                schema: "dbo",
                table: "LND_DisbursementFiles",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrderResults_Order_RegisteredAt",
                schema: "dbo",
                table: "LND_DisbursementOrderResults",
                columns: new[] { "OrderId", "RegisteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrderResults_ReversalOperationId",
                schema: "dbo",
                table: "LND_DisbursementOrderResults",
                column: "ReversalOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementOrderResults_Message",
                schema: "dbo",
                table: "LND_DisbursementOrderResults",
                column: "InboundMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementOrderResults_PublicId",
                schema: "dbo",
                table: "LND_DisbursementOrderResults",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_BeneficiaryPersonId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_CurrentDisbursementFileId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "CurrentDisbursementFileId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_DestinationBankId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "DestinationBankId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_IssuedByUserId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_LoanId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_PendingFile",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "Status",
                filter: "\"PaidByFile\" = TRUE AND \"CurrentDisbursementFileId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_PreviousOrderId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "PreviousOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_Status_ResultDueOn",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                columns: new[] { "Status", "ResultDueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_DisbursementOrders_VoidRequestedByUserId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "VoidRequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementOrders_Number",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementOrders_Operation",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "DisbursementOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DisbursementOrders_PublicId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_FactTypes_Code",
                schema: "dbo",
                table: "LND_FactTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_FactTypes_PublicId",
                schema: "dbo",
                table: "LND_FactTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_FactTypes_ShortCode",
                schema: "dbo",
                table: "LND_FactTypes",
                column: "ShortCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_GoverningBodies_Code",
                schema: "dbo",
                table: "LND_GoverningBodies",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_GoverningBodies_PublicId",
                schema: "dbo",
                table: "LND_GoverningBodies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_GoverningBodyMembers_AlternateForMemberId",
                schema: "dbo",
                table: "LND_GoverningBodyMembers",
                column: "AlternateForMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_GoverningBodyMembers_GoverningBodyId",
                schema: "dbo",
                table: "LND_GoverningBodyMembers",
                column: "GoverningBodyId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_GoverningBodyMembers_PersonId",
                schema: "dbo",
                table: "LND_GoverningBodyMembers",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_GoverningBodyMembers_PublicId",
                schema: "dbo",
                table: "LND_GoverningBodyMembers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_GoverningBodyRuleVersions_Body_Version",
                schema: "dbo",
                table: "LND_GoverningBodyRuleVersions",
                columns: new[] { "GoverningBodyId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_GoverningBodyRuleVersions_PublicId",
                schema: "dbo",
                table: "LND_GoverningBodyRuleVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_InboundMessageEvaluations_Message_Number",
                schema: "dbo",
                table: "LND_InboundMessageEvaluations",
                columns: new[] { "InboundMessageId", "EvaluationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_InboundMessageEvaluations_PublicId",
                schema: "dbo",
                table: "LND_InboundMessageEvaluations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_ConfirmedByExceptionByUserId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "ConfirmedByExceptionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_CreditPaymentPublicId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "CreditPaymentPublicId",
                filter: "\"CreditPaymentPublicId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_DisbursementOrderId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "DisbursementOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_ExceptionDecisionId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "ExceptionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_LastEvaluationId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "LastEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_LoanId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_NotRetryableByUserId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "NotRetryableByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_OriginPublicId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "OriginPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_PersonId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_SaleReconciliationId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "SaleReconciliationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InboundMessages_Status_LastProcessedAt",
                schema: "dbo",
                table: "LND_InboundMessages",
                columns: new[] { "Status", "LastProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_InboundMessages_MessagePublicId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_InboundMessages_PublicId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_InsolvencyProceedings_MigrationLoadId",
                schema: "dbo",
                table: "LND_InsolvencyProceedings",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InsolvencyProceedings_Person_Negotiating",
                schema: "dbo",
                table: "LND_InsolvencyProceedings",
                column: "PersonId",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InsolvencyProceedings_PublicId",
                schema: "dbo",
                table: "LND_InsolvencyProceedings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_InstallmentNonDeductions_Loan_DueDate",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                columns: new[] { "LoanId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_InstallmentNonDeductions_RepaymentInstallmentId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                column: "RepaymentInstallmentId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InstallmentNonDeductions_Activation_Inst",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                columns: new[] { "ActivationId", "RepaymentInstallmentId" },
                unique: true,
                filter: "\"ActivationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InstallmentNonDeductions_ContractKey_Inst",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                columns: new[] { "ContractOperationKey", "RepaymentInstallmentId" },
                unique: true,
                filter: "\"ContractOperationKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InstallmentNonDeductions_PublicId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_InsuranceClaims_DeceasedPersonId",
                schema: "dbo",
                table: "LND_InsuranceClaims",
                column: "DeceasedPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InsuranceClaims_InsurerPersonId",
                schema: "dbo",
                table: "LND_InsuranceClaims",
                column: "InsurerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InsuranceClaims_MigrationLoadId",
                schema: "dbo",
                table: "LND_InsuranceClaims",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InsuranceClaims_PaidOperationId",
                schema: "dbo",
                table: "LND_InsuranceClaims",
                column: "PaidOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InsuranceClaims_Loan_InProcess",
                schema: "dbo",
                table: "LND_InsuranceClaims",
                column: "LoanId",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InsuranceClaims_PublicId",
                schema: "dbo",
                table: "LND_InsuranceClaims",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentEvents_ActorUserId",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentEvents_CausingOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                column: "CausingOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentEvents_DebtorMessageId",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                column: "DebtorMessageId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocumentEvents_PublicId",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocumentEvents_Voided",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                column: "IssuedDocumentId",
                unique: true,
                filter: "\"Kind\" = 3");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentLoans_LastOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "LastOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentLoans_LastTransactionId",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "LastTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentLoans_LoanId",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentLoans_PayoffOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "PayoffOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocumentLoans_Document_Loan",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                columns: new[] { "IssuedDocumentId", "LoanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocumentLoans_PublicId",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocuments_IssuedByUserId",
                schema: "dbo",
                table: "LND_IssuedDocuments",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocuments_PersonId",
                schema: "dbo",
                table: "LND_IssuedDocuments",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocuments_Number",
                schema: "dbo",
                table: "LND_IssuedDocuments",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocuments_PublicId",
                schema: "dbo",
                table: "LND_IssuedDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocuments_VerificationCode",
                schema: "dbo",
                table: "LND_IssuedDocuments",
                column: "VerificationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanControls_LoanId",
                schema: "dbo",
                table: "LND_LoanControls",
                column: "LoanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanControls_PublicId",
                schema: "dbo",
                table: "LND_LoanControls",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanDailyPositions_BranchId",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanDailyPositions_CreditLineId",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanDailyPositions_Date_Branch_Line",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                columns: new[] { "PositionDate", "BranchId", "CreditLineId" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanDailyPositions_Loan_Date_Version",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                columns: new[] { "LoanId", "PositionDate", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanDailyPositions_PublicId",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNumberReservations_CreditLimitId",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "CreditLimitId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNumberReservations_CreditLineId",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNumberReservations_LoanId",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNumberReservations_PaymentPublicId",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "PaymentPublicId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNumberReservations_Person_Status",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                columns: new[] { "PersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNumberReservations_ReleasedByUserId",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNumberReservations_ReservedByUserId",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "ReservedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNumberReservations_Status_ExpiresOn",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                columns: new[] { "Status", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNumberReservations_Key",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNumberReservations_Number",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNumberReservations_PublicId",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_ActorUserId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_InboundMessageId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "InboundMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_InsuranceClaimId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "InsuranceClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_Loan_ValueDate",
                schema: "dbo",
                table: "LND_LoanOperations",
                columns: new[] { "LoanId", "ValueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_MigrationLoadId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_PayerPersonId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "PayerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_RelatedOperationId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "RelatedOperationId",
                filter: "\"RelatedOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_ReversedByOperationId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "ReversedByOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanOperations_Collector_Key",
                schema: "dbo",
                table: "LND_LoanOperations",
                columns: new[] { "CollectorCode", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanOperations_Loan_Sequence",
                schema: "dbo",
                table: "LND_LoanOperations",
                columns: new[] { "LoanId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanOperations_Number",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanOperations_PublicId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanOperations_Reverses",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "ReversesOperationId",
                unique: true,
                filter: "\"ReversesOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanParties_ExtinguishDecisionId",
                schema: "dbo",
                table: "LND_LoanParties",
                column: "ExtinguishDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanParties_PersonId",
                schema: "dbo",
                table: "LND_LoanParties",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanParties_ReleaseOperationId",
                schema: "dbo",
                table: "LND_LoanParties",
                column: "ReleaseOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanParties_SourceApplicationPartyId",
                schema: "dbo",
                table: "LND_LoanParties",
                column: "SourceApplicationPartyId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanParties_Loan_Person_Role",
                schema: "dbo",
                table: "LND_LoanParties",
                columns: new[] { "LoanId", "PersonId", "Role" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanParties_PublicId",
                schema: "dbo",
                table: "LND_LoanParties",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanPayrollLinks_OperationId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanPayrollLinks_PaymasterPersonId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks",
                column: "PaymasterPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanPayrollLinks_PreviousLinkId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks",
                column: "PreviousLinkId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanPayrollLinks_Loan_Current",
                schema: "dbo",
                table: "LND_LoanPayrollLinks",
                column: "LoanId",
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanPayrollLinks_PublicId",
                schema: "dbo",
                table: "LND_LoanPayrollLinks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_ApplicationId",
                schema: "dbo",
                table: "LND_Loans",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_Branch_Status",
                schema: "dbo",
                table: "LND_Loans",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_ConfirmedByExceptionDecisionId",
                schema: "dbo",
                table: "LND_Loans",
                column: "ConfirmedByExceptionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_CreditLimitId",
                schema: "dbo",
                table: "LND_Loans",
                column: "CreditLimitId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_Debtor_Status",
                schema: "dbo",
                table: "LND_Loans",
                columns: new[] { "DebtorPersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_Line_Status",
                schema: "dbo",
                table: "LND_Loans",
                columns: new[] { "CreditLineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_MigrationLoadId",
                schema: "dbo",
                table: "LND_Loans",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_OriginLoanId",
                schema: "dbo",
                table: "LND_Loans",
                column: "OriginLoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_ReservationId",
                schema: "dbo",
                table: "LND_Loans",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Loans_SourceInvoiceNumber",
                schema: "dbo",
                table: "LND_Loans",
                column: "SourceInvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Loans_Number",
                schema: "dbo",
                table: "LND_Loans",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Loans_PublicId",
                schema: "dbo",
                table: "LND_Loans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Loans_SourcePayment",
                schema: "dbo",
                table: "LND_Loans",
                column: "SourcePaymentPublicId",
                unique: true,
                filter: "\"SourcePaymentPublicId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTermsVersions_CreditLineVersionId",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                column: "CreditLineVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTermsVersions_OperationId",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanTermsVersions_Loan_Version",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                columns: new[] { "LoanId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanTermsVersions_PublicId",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTransactions_AccountingDate",
                schema: "dbo",
                table: "LND_LoanTransactions",
                column: "AccountingDate");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTransactions_Loan_AccountingDate",
                schema: "dbo",
                table: "LND_LoanTransactions",
                columns: new[] { "LoanId", "AccountingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTransactions_Loan_ValueDate",
                schema: "dbo",
                table: "LND_LoanTransactions",
                columns: new[] { "LoanId", "ValueDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTransactions_MessagePublicId",
                schema: "dbo",
                table: "LND_LoanTransactions",
                column: "MessagePublicId",
                filter: "\"MessagePublicId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTransactions_OperationId",
                schema: "dbo",
                table: "LND_LoanTransactions",
                column: "OperationId",
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTransactions_ProcessRunId",
                schema: "dbo",
                table: "LND_LoanTransactions",
                column: "ProcessRunId",
                filter: "\"ProcessRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanTransactions_Accrual",
                schema: "dbo",
                table: "LND_LoanTransactions",
                columns: new[] { "LoanId", "Concept", "InstallmentNumber", "ValueDate" },
                unique: true,
                filter: "\"Kind\" = 3 AND \"ReversesTransactionId\" IS NULL AND \"InstallmentNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanTransactions_AccrualNoInst",
                schema: "dbo",
                table: "LND_LoanTransactions",
                columns: new[] { "LoanId", "Concept", "ValueDate" },
                unique: true,
                filter: "\"Kind\" = 3 AND \"ReversesTransactionId\" IS NULL AND \"InstallmentNumber\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanTransactions_PublicId",
                schema: "dbo",
                table: "LND_LoanTransactions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanTransactions_Reverses",
                schema: "dbo",
                table: "LND_LoanTransactions",
                column: "ReversesTransactionId",
                unique: true,
                filter: "\"ReversesTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationArrearsHistory_Part_Row",
                schema: "dbo",
                table: "LND_MigrationArrearsHistory",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationArrearsHistory_PublicId",
                schema: "dbo",
                table: "LND_MigrationArrearsHistory",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationAuthorizations_PersonId",
                schema: "dbo",
                table: "LND_MigrationAuthorizations",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationAuthorizations_Part_Row",
                schema: "dbo",
                table: "LND_MigrationAuthorizations",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationAuthorizations_PublicId",
                schema: "dbo",
                table: "LND_MigrationAuthorizations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationBalances_Part_Row",
                schema: "dbo",
                table: "LND_MigrationBalances",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationBalances_PublicId",
                schema: "dbo",
                table: "LND_MigrationBalances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationBlocks_InsurerPersonId",
                schema: "dbo",
                table: "LND_MigrationBlocks",
                column: "InsurerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationBlocks_PersonId",
                schema: "dbo",
                table: "LND_MigrationBlocks",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationBlocks_Part_Row",
                schema: "dbo",
                table: "LND_MigrationBlocks",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationBlocks_PublicId",
                schema: "dbo",
                table: "LND_MigrationBlocks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationBureauReports_Part_Row",
                schema: "dbo",
                table: "LND_MigrationBureauReports",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationBureauReports_PublicId",
                schema: "dbo",
                table: "LND_MigrationBureauReports",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationChargedOff_PersonId",
                schema: "dbo",
                table: "LND_MigrationChargedOff",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationChargedOff_Part_Row",
                schema: "dbo",
                table: "LND_MigrationChargedOff",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationChargedOff_PublicId",
                schema: "dbo",
                table: "LND_MigrationChargedOff",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationClaims_PersonId",
                schema: "dbo",
                table: "LND_MigrationClaims",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationClaims_Part_Row",
                schema: "dbo",
                table: "LND_MigrationClaims",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationClaims_PublicId",
                schema: "dbo",
                table: "LND_MigrationClaims",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationCoDebtors_CoDebtorPersonId",
                schema: "dbo",
                table: "LND_MigrationCoDebtors",
                column: "CoDebtorPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationCoDebtors_Part_Row",
                schema: "dbo",
                table: "LND_MigrationCoDebtors",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationCoDebtors_PublicId",
                schema: "dbo",
                table: "LND_MigrationCoDebtors",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationCollaterals_GuaranteeFundPersonId",
                schema: "dbo",
                table: "LND_MigrationCollaterals",
                column: "GuaranteeFundPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationCollaterals_InsurerPersonId",
                schema: "dbo",
                table: "LND_MigrationCollaterals",
                column: "InsurerPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationCollaterals_Part_Row",
                schema: "dbo",
                table: "LND_MigrationCollaterals",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationCollaterals_PublicId",
                schema: "dbo",
                table: "LND_MigrationCollaterals",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationCreditLimits_PersonId",
                schema: "dbo",
                table: "LND_MigrationCreditLimits",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationCreditLimits_Part_Row",
                schema: "dbo",
                table: "LND_MigrationCreditLimits",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationCreditLimits_PublicId",
                schema: "dbo",
                table: "LND_MigrationCreditLimits",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationInstallments_Part_Row",
                schema: "dbo",
                table: "LND_MigrationInstallments",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationInstallments_PublicId",
                schema: "dbo",
                table: "LND_MigrationInstallments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoadDifferences_AcceptedByUserId",
                schema: "dbo",
                table: "LND_MigrationLoadDifferences",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoadDifferences_BranchId",
                schema: "dbo",
                table: "LND_MigrationLoadDifferences",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoadDifferences_SessionItemId",
                schema: "dbo",
                table: "LND_MigrationLoadDifferences",
                column: "SessionItemId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoadDifferences_Load_Version_Dim",
                schema: "dbo",
                table: "LND_MigrationLoadDifferences",
                columns: new[] { "MigrationLoadId", "CheckVersion", "DimensionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoadDifferences_PublicId",
                schema: "dbo",
                table: "LND_MigrationLoadDifferences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoadParts_ImportedByUserId",
                schema: "dbo",
                table: "LND_MigrationLoadParts",
                column: "ImportedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoadParts_Load_Part_Batch",
                schema: "dbo",
                table: "LND_MigrationLoadParts",
                columns: new[] { "MigrationLoadId", "PartKey", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoadParts_Load_Part_File",
                schema: "dbo",
                table: "LND_MigrationLoadParts",
                columns: new[] { "MigrationLoadId", "PartKey", "FileSha256" },
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoadParts_PublicId",
                schema: "dbo",
                table: "LND_MigrationLoadParts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoads_AppliedByUserId",
                schema: "dbo",
                table: "LND_MigrationLoads",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoads_CreatedByUserId",
                schema: "dbo",
                table: "LND_MigrationLoads",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoads_ReplacedByLoadId",
                schema: "dbo",
                table: "LND_MigrationLoads",
                column: "ReplacedByLoadId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoads_Activated",
                schema: "dbo",
                table: "LND_MigrationLoads",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 4 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoads_Applied",
                schema: "dbo",
                table: "LND_MigrationLoads",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 2 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoads_Draft",
                schema: "dbo",
                table: "LND_MigrationLoads",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoads_Number",
                schema: "dbo",
                table: "LND_MigrationLoads",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoads_PublicId",
                schema: "dbo",
                table: "LND_MigrationLoads",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoans_AdvisorPersonId",
                schema: "dbo",
                table: "LND_MigrationLoans",
                column: "AdvisorPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoans_CreditLineId",
                schema: "dbo",
                table: "LND_MigrationLoans",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoans_LawyerPersonId",
                schema: "dbo",
                table: "LND_MigrationLoans",
                column: "LawyerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoans_PaymasterPersonId",
                schema: "dbo",
                table: "LND_MigrationLoans",
                column: "PaymasterPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationLoans_PersonId",
                schema: "dbo",
                table: "LND_MigrationLoans",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoans_Part_Row",
                schema: "dbo",
                table: "LND_MigrationLoans",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoans_PublicId",
                schema: "dbo",
                table: "LND_MigrationLoans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationPriorNotices_Part_Row",
                schema: "dbo",
                table: "LND_MigrationPriorNotices",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationPriorNotices_PublicId",
                schema: "dbo",
                table: "LND_MigrationPriorNotices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationRelatedParties_PersonId",
                schema: "dbo",
                table: "LND_MigrationRelatedParties",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationRelatedParties_RelatedToPersonId",
                schema: "dbo",
                table: "LND_MigrationRelatedParties",
                column: "RelatedToPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationRelatedParties_Part_Row",
                schema: "dbo",
                table: "LND_MigrationRelatedParties",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationRelatedParties_PublicId",
                schema: "dbo",
                table: "LND_MigrationRelatedParties",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationRestructurings_Part_Row",
                schema: "dbo",
                table: "LND_MigrationRestructurings",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationRestructurings_PublicId",
                schema: "dbo",
                table: "LND_MigrationRestructurings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationRiskData_Part_Row",
                schema: "dbo",
                table: "LND_MigrationRiskData",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationRiskData_PublicId",
                schema: "dbo",
                table: "LND_MigrationRiskData",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSaleReconciliations_DifferenceId",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "DifferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSaleReconciliations_Invoice",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "InvoiceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSaleReconciliations_MarkedByUserId",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "MarkedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSaleReconciliations_Message",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "MessagePublicId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSaleReconciliations_MigrationLoanRowId",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "MigrationLoanRowId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSaleReconciliations_MigrationSaleRowId",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "MigrationSaleRowId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSaleReconciliations_PersonId",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSaleReconciliations_ResultingLoanId",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "ResultingLoanId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationSaleReconciliations_Load_Ver_Payment",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                columns: new[] { "MigrationLoadId", "CheckVersion", "CreditPaymentPublicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationSaleReconciliations_PublicId",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MigrationSales_PersonId",
                schema: "dbo",
                table: "LND_MigrationSales",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationSales_Part_Row",
                schema: "dbo",
                table: "LND_MigrationSales",
                columns: new[] { "MigrationLoadPartId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationSales_PublicId",
                schema: "dbo",
                table: "LND_MigrationSales",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_NumberSequences_Kind_Scope",
                schema: "dbo",
                table: "LND_NumberSequences",
                columns: new[] { "Kind", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_NumberSequences_PublicId",
                schema: "dbo",
                table: "LND_NumberSequences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PrepaymentPreferences_ApplicationId",
                schema: "dbo",
                table: "LND_PrepaymentPreferences",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PrepaymentPreferences_Limit_RegisteredAt",
                schema: "dbo",
                table: "LND_PrepaymentPreferences",
                columns: new[] { "CreditLimitId", "RegisteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_PrepaymentPreferences_Loan_RegisteredAt",
                schema: "dbo",
                table: "LND_PrepaymentPreferences",
                columns: new[] { "LoanId", "RegisteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_PrepaymentPreferences_OperationId",
                schema: "dbo",
                table: "LND_PrepaymentPreferences",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PrepaymentPreferences_PersonId",
                schema: "dbo",
                table: "LND_PrepaymentPreferences",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PrepaymentPreferences_RecordedByUserId",
                schema: "dbo",
                table: "LND_PrepaymentPreferences",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PrepaymentPreferences_PublicId",
                schema: "dbo",
                table: "LND_PrepaymentPreferences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PriorNotices_MigrationLoadId",
                schema: "dbo",
                table: "LND_PriorNotices",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PriorNotices_PersonId",
                schema: "dbo",
                table: "LND_PriorNotices",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PriorNotices_Loan_Person_Episode",
                schema: "dbo",
                table: "LND_PriorNotices",
                columns: new[] { "LoanId", "PersonId", "EpisodeStartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PriorNotices_PublicId",
                schema: "dbo",
                table: "LND_PriorNotices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ProcessRunErrors_LoanId_Unresolved",
                schema: "dbo",
                table: "LND_ProcessRunErrors",
                column: "LoanId",
                filter: "\"ResolvedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProcessRunErrors_PublicId",
                schema: "dbo",
                table: "LND_ProcessRunErrors",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProcessRunErrors_Run_Loan_Attempt",
                schema: "dbo",
                table: "LND_ProcessRunErrors",
                columns: new[] { "ProcessRunId", "LoanId", "AttemptNumber" },
                unique: true,
                filter: "\"LoanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ProcessRuns_NationalValueReplicationId",
                schema: "dbo",
                table: "LND_ProcessRuns",
                column: "NationalValueReplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ProcessRuns_RequestedByUserId",
                schema: "dbo",
                table: "LND_ProcessRuns",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ProcessRuns_Status_RequestedAt",
                schema: "dbo",
                table: "LND_ProcessRuns",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProcessRuns_Close",
                schema: "dbo",
                table: "LND_ProcessRuns",
                columns: new[] { "Kind", "PeriodYear", "PeriodMonth", "CloseVersion" },
                unique: true,
                filter: "\"Kind\" = 2 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProcessRuns_DailyCycle",
                schema: "dbo",
                table: "LND_ProcessRuns",
                columns: new[] { "Kind", "ProcessDate" },
                unique: true,
                filter: "\"Kind\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProcessRuns_PublicId",
                schema: "dbo",
                table: "LND_ProcessRuns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProcessRuns_Reopen",
                schema: "dbo",
                table: "LND_ProcessRuns",
                columns: new[] { "Kind", "PeriodYear", "PeriodMonth", "CloseVersion" },
                unique: true,
                filter: "\"Kind\" = 3 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProcessRuns_Running",
                schema: "dbo",
                table: "LND_ProcessRuns",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PromissoryNotes_ApplicationId",
                schema: "dbo",
                table: "LND_PromissoryNotes",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PromissoryNotes_LoanId",
                schema: "dbo",
                table: "LND_PromissoryNotes",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PromissoryNotes_ExternalNoteId",
                schema: "dbo",
                table: "LND_PromissoryNotes",
                column: "ExternalNoteId",
                unique: true,
                filter: "\"ExternalNoteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PromissoryNotes_PublicId",
                schema: "dbo",
                table: "LND_PromissoryNotes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ProvisionalCollections_LoanId",
                schema: "dbo",
                table: "LND_ProvisionalCollections",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ProvisionalCollections_OperationId",
                schema: "dbo",
                table: "LND_ProvisionalCollections",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProvisionalCollections_Activation_Row",
                schema: "dbo",
                table: "LND_ProvisionalCollections",
                columns: new[] { "ActivationId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProvisionalCollections_Invoice_Reference",
                schema: "dbo",
                table: "LND_ProvisionalCollections",
                columns: new[] { "InvoiceNumber", "AccountingReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProvisionalCollections_PublicId",
                schema: "dbo",
                table: "LND_ProvisionalCollections",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_RelatedParties_EndedByUserId",
                schema: "dbo",
                table: "LND_RelatedParties",
                column: "EndedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RelatedParties_PersonId",
                schema: "dbo",
                table: "LND_RelatedParties",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RelatedParties_RelatedThroughPersonId",
                schema: "dbo",
                table: "LND_RelatedParties",
                column: "RelatedThroughPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_RelatedParties_PublicId",
                schema: "dbo",
                table: "LND_RelatedParties",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_RepaymentInstallments_PublicId",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_RepaymentInstallments_Version_Number",
                schema: "dbo",
                table: "LND_RepaymentInstallments",
                columns: new[] { "ScheduleVersionId", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_RepaymentScheduleVersions_OperationId",
                schema: "dbo",
                table: "LND_RepaymentScheduleVersions",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RepaymentScheduleVersions_TermsVersionId",
                schema: "dbo",
                table: "LND_RepaymentScheduleVersions",
                column: "TermsVersionId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_RepaymentScheduleVersions_Loan_Version",
                schema: "dbo",
                table: "LND_RepaymentScheduleVersions",
                columns: new[] { "LoanId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_RepaymentScheduleVersions_PublicId",
                schema: "dbo",
                table: "LND_RepaymentScheduleVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ScreeningResults_ApplicationId",
                schema: "dbo",
                table: "LND_ScreeningResults",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ScreeningResults_CreditLimitId",
                schema: "dbo",
                table: "LND_ScreeningResults",
                column: "CreditLimitId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ScreeningResults_PendingMatch",
                schema: "dbo",
                table: "LND_ScreeningResults",
                column: "PersonId",
                filter: "\"Outcome\" = 3 AND \"ReleasedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ScreeningResults_PendingPossible",
                schema: "dbo",
                table: "LND_ScreeningResults",
                column: "PersonId",
                filter: "\"Outcome\" = 2 AND \"ReleasedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ScreeningResults_Person_QueriedAt",
                schema: "dbo",
                table: "LND_ScreeningResults",
                columns: new[] { "PersonId", "QueriedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_ScreeningResults_QueriedByUserId",
                schema: "dbo",
                table: "LND_ScreeningResults",
                column: "QueriedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ScreeningResults_ReleasedByUserId",
                schema: "dbo",
                table: "LND_ScreeningResults",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ScreeningResults_PublicId",
                schema: "dbo",
                table: "LND_ScreeningResults",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_SessionAttendances_MemberId",
                schema: "dbo",
                table: "LND_SessionAttendances",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_SessionAttendances_PersonId",
                schema: "dbo",
                table: "LND_SessionAttendances",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_SessionAttendances_SubstitutesMemberId",
                schema: "dbo",
                table: "LND_SessionAttendances",
                column: "SubstitutesMemberId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_SessionAttendances_PublicId",
                schema: "dbo",
                table: "LND_SessionAttendances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SessionAttendances_Session_Person",
                schema: "dbo",
                table: "LND_SessionAttendances",
                columns: new[] { "SessionId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_SessionItems_DecisionId",
                schema: "dbo",
                table: "LND_SessionItems",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_SessionItems_RecommendsToBodyId",
                schema: "dbo",
                table: "LND_SessionItems",
                column: "RecommendsToBodyId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_SessionItems_PublicId",
                schema: "dbo",
                table: "LND_SessionItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SessionItems_Session_Order",
                schema: "dbo",
                table: "LND_SessionItems",
                columns: new[] { "SessionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_Sessions_ClosedByUserId",
                schema: "dbo",
                table: "LND_Sessions",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Sessions_GoverningBodyId",
                schema: "dbo",
                table: "LND_Sessions",
                column: "GoverningBodyId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Sessions_RuleVersionId",
                schema: "dbo",
                table: "LND_Sessions",
                column: "RuleVersionId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Sessions_Number",
                schema: "dbo",
                table: "LND_Sessions",
                column: "Number",
                unique: true,
                filter: "\"Number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Sessions_PublicId",
                schema: "dbo",
                table: "LND_Sessions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_SessionVotes_MemberId",
                schema: "dbo",
                table: "LND_SessionVotes",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_SessionVotes_PersonId",
                schema: "dbo",
                table: "LND_SessionVotes",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_SessionVotes_RecordedByUserId",
                schema: "dbo",
                table: "LND_SessionVotes",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_SessionVotes_Item_Person",
                schema: "dbo",
                table: "LND_SessionVotes",
                columns: new[] { "ItemId", "PersonId" },
                unique: true,
                filter: "\"ReplacedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_SessionVotes_PublicId",
                schema: "dbo",
                table: "LND_SessionVotes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SessionVotes_Replaces",
                schema: "dbo",
                table: "LND_SessionVotes",
                column: "ReplacesVoteId",
                unique: true,
                filter: "\"ReplacesVoteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Setup_ActivationId",
                schema: "dbo",
                table: "LND_Setup",
                column: "ActivationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Setup_PublicId",
                schema: "dbo",
                table: "LND_Setup",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_UserCreditLineScopes_CreditLineId",
                schema: "dbo",
                table: "LND_UserCreditLineScopes",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_UserCreditLineScopes_PublicId",
                schema: "dbo",
                table: "LND_UserCreditLineScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_UserCreditLineScopes_User_Line",
                schema: "dbo",
                table: "LND_UserCreditLineScopes",
                columns: new[] { "UserId", "CreditLineId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountingFactParts_LND_LoanOperations_TriggerOperation~",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "TriggerOperationId",
                principalSchema: "dbo",
                principalTable: "LND_LoanOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_Activations_LND_SessionItems_SessionItemId",
                schema: "dbo",
                table: "LND_Activations",
                column: "SessionItemId",
                principalSchema: "dbo",
                principalTable: "LND_SessionItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AttributionLevels_LND_AttributionMatrices_MatrixId",
                schema: "dbo",
                table: "LND_AttributionLevels",
                column: "MatrixId",
                principalSchema: "dbo",
                principalTable: "LND_AttributionMatrices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AttributionMatrices_LND_SessionItems_SessionItemId",
                schema: "dbo",
                table: "LND_AttributionMatrices",
                column: "SessionItemId",
                principalSchema: "dbo",
                principalTable: "LND_SessionItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_BureauQueries_LND_CreditApplications_ApplicationId",
                schema: "dbo",
                table: "LND_BureauQueries",
                column: "ApplicationId",
                principalSchema: "dbo",
                principalTable: "LND_CreditApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_Collaterals_LND_CreditApplications_ApplicationId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "ApplicationId",
                principalSchema: "dbo",
                principalTable: "LND_CreditApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_Collaterals_LND_LoanOperations_ReleaseOperationId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "ReleaseOperationId",
                principalSchema: "dbo",
                principalTable: "LND_LoanOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_Collaterals_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_Collaterals",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplicationChecks_LND_CreditApplicationParties_Pa~",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "PartyId",
                principalSchema: "dbo",
                principalTable: "LND_CreditApplicationParties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplicationChecks_LND_CreditApplications_Applicat~",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "ApplicationId",
                principalSchema: "dbo",
                principalTable: "LND_CreditApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplicationChecks_LND_CreditScores_CreditScoreId",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "CreditScoreId",
                principalSchema: "dbo",
                principalTable: "LND_CreditScores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplicationChecks_LND_ScreeningResults_ScreeningR~",
                schema: "dbo",
                table: "LND_CreditApplicationChecks",
                column: "ScreeningResultId",
                principalSchema: "dbo",
                principalTable: "LND_ScreeningResults",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplicationDocuments_LND_CreditApplicationParties~",
                schema: "dbo",
                table: "LND_CreditApplicationDocuments",
                column: "PartyId",
                principalSchema: "dbo",
                principalTable: "LND_CreditApplicationParties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplicationDocuments_LND_CreditApplications_Appli~",
                schema: "dbo",
                table: "LND_CreditApplicationDocuments",
                column: "ApplicationId",
                principalSchema: "dbo",
                principalTable: "LND_CreditApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplicationParties_LND_CreditApplications_Applica~",
                schema: "dbo",
                table: "LND_CreditApplicationParties",
                column: "ApplicationId",
                principalSchema: "dbo",
                principalTable: "LND_CreditApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplications_LND_CreditDecisions_ApprovalDecision~",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "ApprovalDecisionId",
                principalSchema: "dbo",
                principalTable: "LND_CreditDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplications_LND_CreditLimits_CreditLimitId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "CreditLimitId",
                principalSchema: "dbo",
                principalTable: "LND_CreditLimits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditApplications_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_CreditApplications",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditDecisions_LND_SessionItems_SessionItemId",
                schema: "dbo",
                table: "LND_CreditDecisions",
                column: "SessionItemId",
                principalSchema: "dbo",
                principalTable: "LND_SessionItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_CreditReportClaims_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_CreditReportClaims",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DebtorBlocks_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_DebtorBlocks",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DebtorMessageEvents_LND_DebtorMessages_DebtorMessageId",
                schema: "dbo",
                table: "LND_DebtorMessageEvents",
                column: "DebtorMessageId",
                principalSchema: "dbo",
                principalTable: "LND_DebtorMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DebtorMessages_LND_InstallmentNonDeductions_Installment~",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "InstallmentNonDeductionId",
                principalSchema: "dbo",
                principalTable: "LND_InstallmentNonDeductions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DebtorMessages_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DebtorMessages_LND_PriorNotices_PriorNoticeId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "PriorNoticeId",
                principalSchema: "dbo",
                principalTable: "LND_PriorNotices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DisbursementFileLines_LND_DisbursementOrders_OrderId",
                schema: "dbo",
                table: "LND_DisbursementFileLines",
                column: "OrderId",
                principalSchema: "dbo",
                principalTable: "LND_DisbursementOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DisbursementOrderResults_LND_DisbursementOrders_OrderId",
                schema: "dbo",
                table: "LND_DisbursementOrderResults",
                column: "OrderId",
                principalSchema: "dbo",
                principalTable: "LND_DisbursementOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DisbursementOrderResults_LND_InboundMessages_InboundMes~",
                schema: "dbo",
                table: "LND_DisbursementOrderResults",
                column: "InboundMessageId",
                principalSchema: "dbo",
                principalTable: "LND_InboundMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DisbursementOrderResults_LND_LoanOperations_ReversalOpe~",
                schema: "dbo",
                table: "LND_DisbursementOrderResults",
                column: "ReversalOperationId",
                principalSchema: "dbo",
                principalTable: "LND_LoanOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DisbursementOrders_LND_LoanOperations_DisbursementOpera~",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "DisbursementOperationId",
                principalSchema: "dbo",
                principalTable: "LND_LoanOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DisbursementOrders_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_DisbursementOrders",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InboundMessageEvaluations_LND_InboundMessages_InboundMe~",
                schema: "dbo",
                table: "LND_InboundMessageEvaluations",
                column: "InboundMessageId",
                principalSchema: "dbo",
                principalTable: "LND_InboundMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InboundMessages_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InboundMessages_LND_MigrationSaleReconciliations_SaleRe~",
                schema: "dbo",
                table: "LND_InboundMessages",
                column: "SaleReconciliationId",
                principalSchema: "dbo",
                principalTable: "LND_MigrationSaleReconciliations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InstallmentNonDeductions_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InstallmentNonDeductions_LND_RepaymentInstallments_Repa~",
                schema: "dbo",
                table: "LND_InstallmentNonDeductions",
                column: "RepaymentInstallmentId",
                principalSchema: "dbo",
                principalTable: "LND_RepaymentInstallments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InsuranceClaims_LND_LoanOperations_PaidOperationId",
                schema: "dbo",
                table: "LND_InsuranceClaims",
                column: "PaidOperationId",
                principalSchema: "dbo",
                principalTable: "LND_LoanOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InsuranceClaims_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_InsuranceClaims",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_IssuedDocumentEvents_LND_LoanOperations_CausingOperatio~",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                column: "CausingOperationId",
                principalSchema: "dbo",
                principalTable: "LND_LoanOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_IssuedDocumentLoans_LND_LoanOperations_LastOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "LastOperationId",
                principalSchema: "dbo",
                principalTable: "LND_LoanOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_IssuedDocumentLoans_LND_LoanOperations_PayoffOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "PayoffOperationId",
                principalSchema: "dbo",
                principalTable: "LND_LoanOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_IssuedDocumentLoans_LND_LoanTransactions_LastTransactio~",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "LastTransactionId",
                principalSchema: "dbo",
                principalTable: "LND_LoanTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_IssuedDocumentLoans_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_IssuedDocumentLoans",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanControls_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_LoanControls",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanDailyPositions_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_LoanDailyPositions",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanNumberReservations_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_LoanNumberReservations",
                column: "LoanId",
                principalSchema: "dbo",
                principalTable: "LND_Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Feature 013 (T0237; data-model §1.1): la fila única de LND_Setup, con Id = 1 y sin activar (ActivationDate nulo).
            // Idempotente: no hace nada si la fila ya existe. Es la primera fila del cerrojo de Cartera (T34).
            migrationBuilder.Sql(@"
INSERT INTO dbo.""LND_Setup"" (""Id"", ""PublicId"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
OVERRIDING SYSTEM VALUE
SELECT 1, gen_random_uuid(), FALSE, now(), 'SYSTEM'
WHERE NOT EXISTS (SELECT 1 FROM dbo.""LND_Setup"");
SELECT setval(pg_get_serial_sequence('dbo.""LND_Setup""', 'Id'), GREATEST((SELECT MAX(""Id"") FROM dbo.""LND_Setup""), 1));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LND_DisbursementOrders_LND_LoanOperations_DisbursementOpera~",
                schema: "dbo",
                table: "LND_DisbursementOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InsuranceClaims_LND_LoanOperations_PaidOperationId",
                schema: "dbo",
                table: "LND_InsuranceClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Loans_LND_MigrationLoads_MigrationLoadId",
                schema: "dbo",
                table: "LND_Loans");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MigrationLoadDifferences_LND_MigrationLoads_MigrationLo~",
                schema: "dbo",
                table: "LND_MigrationLoadDifferences");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MigrationLoadParts_LND_MigrationLoads_MigrationLoadId",
                schema: "dbo",
                table: "LND_MigrationLoadParts");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MigrationSaleReconciliations_LND_MigrationLoads_Migrati~",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AttributionMatrices_LND_SessionItems_SessionItemId",
                schema: "dbo",
                table: "LND_AttributionMatrices");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditDecisions_LND_SessionItems_SessionItemId",
                schema: "dbo",
                table: "LND_CreditDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MigrationLoadDifferences_LND_SessionItems_SessionItemId",
                schema: "dbo",
                table: "LND_MigrationLoadDifferences");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AttributionLevels_LND_AttributionMatrices_MatrixId",
                schema: "dbo",
                table: "LND_AttributionLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditApplications_LND_AttributionMatrices_AttributionM~",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditDecisions_LND_AttributionMatrices_AttributionMatr~",
                schema: "dbo",
                table: "LND_CreditDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AttributionLevels_LND_GoverningBodies_GoverningBodyId",
                schema: "dbo",
                table: "LND_AttributionLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditApplications_LND_AttributionLevels_RequiredLevelId",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditDecisions_LND_AttributionLevels_LevelId",
                schema: "dbo",
                table: "LND_CreditDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditApplications_LND_CreditLines_CreditLineId",
                schema: "dbo",
                table: "LND_CreditApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditLimits_LND_CreditLines_CreditLineId",
                schema: "dbo",
                table: "LND_CreditLimits");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditLineVersions_LND_CreditLines_CreditLineId",
                schema: "dbo",
                table: "LND_CreditLineVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanNumberReservations_LND_CreditLines_CreditLineId",
                schema: "dbo",
                table: "LND_LoanNumberReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Loans_LND_CreditLines_CreditLineId",
                schema: "dbo",
                table: "LND_Loans");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MigrationLoans_LND_CreditLines_CreditLineId",
                schema: "dbo",
                table: "LND_MigrationLoans");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditLimits_LND_CreditApplications_ApplicationId",
                schema: "dbo",
                table: "LND_CreditLimits");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Loans_LND_CreditApplications_ApplicationId",
                schema: "dbo",
                table: "LND_Loans");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_DisbursementOrders_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_DisbursementOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InboundMessages_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_InboundMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanNumberReservations_LND_Loans_LoanId",
                schema: "dbo",
                table: "LND_LoanNumberReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MigrationSaleReconciliations_LND_Loans_ResultingLoanId",
                schema: "dbo",
                table: "LND_MigrationSaleReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InboundMessages_LND_CreditDecisions_ExceptionDecisionId",
                schema: "dbo",
                table: "LND_InboundMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_DisbursementOrders_LND_DisbursementFiles_CurrentDisburs~",
                schema: "dbo",
                table: "LND_DisbursementOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InboundMessages_LND_DisbursementOrders_DisbursementOrde~",
                schema: "dbo",
                table: "LND_InboundMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InboundMessageEvaluations_LND_InboundMessages_InboundMe~",
                schema: "dbo",
                table: "LND_InboundMessageEvaluations");

            migrationBuilder.DropTable(
                name: "LND_AccountingFactParts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AttributionRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CaseParticipations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Collaterals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CommercialTermEquivalences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ConnectedGroupMembers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditApplicationChecks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditApplicationDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLineCollateralRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLineInsurances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLineRateBands",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditReportClaims",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DebtorBlocks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DebtorControls",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DebtorMessageEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DisbursementFileLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DisbursementOrderResults",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_FactTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InsolvencyProceedings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_IssuedDocumentEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_IssuedDocumentLoans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanControls",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanDailyPositions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanParties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanPayrollLinks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationArrearsHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationAuthorizations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationBalances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationBlocks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationBureauReports",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationChargedOff",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationClaims",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationCoDebtors",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationCollaterals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationCreditLimits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationInstallments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationPriorNotices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationRelatedParties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationRestructurings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationRiskData",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_NumberSequences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PrepaymentPreferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ProcessRunErrors",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PromissoryNotes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ProvisionalCollections",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_RelatedParties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_SessionAttendances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_SessionVotes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Setup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_UserCreditLineScopes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ConnectedGroups",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_BureauQueries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditScores",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ScreeningResults",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLineDocumentRequirements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DebtorMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanTransactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditApplicationParties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_GoverningBodyMembers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InstallmentNonDeductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_IssuedDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PriorNotices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ProcessRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Activations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_RepaymentInstallments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_RepaymentScheduleVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanTermsVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanOperations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InsuranceClaims",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationLoads",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_SessionItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Sessions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_GoverningBodyRuleVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AttributionMatrices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_GoverningBodies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AttributionLevels",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditApplications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLineVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Loans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanNumberReservations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLimits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ContributionBalanceLoads",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditDecisions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DisbursementFiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DisbursementOrders",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InboundMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InboundMessageEvaluations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationSaleReconciliations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationLoadDifferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationLoans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationSales",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MigrationLoadParts",
                schema: "dbo");
        }
    }
}
