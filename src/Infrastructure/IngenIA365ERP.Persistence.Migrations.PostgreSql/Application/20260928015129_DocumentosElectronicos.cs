using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class DocumentosElectronicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COR_DianContingencyEvents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DetectedByKind = table.Column<int>(type: "integer", nullable: false),
                    DetectedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DetectedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeadlineHoursApplied = table.Column<short>(type: "smallint", nullable: false),
                    LegalSource = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeadlineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclaredToDianAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclaredToDianReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ClosedByKind = table.Column<int>(type: "integer", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ClosedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_COR_DianContingencyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_DianContingencyEvents_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_DianContingencyEvents_SEC_Users_DetectedByUserId",
                        column: x => x.DetectedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_DianNumberingResolutions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    BacksUpKind = table.Column<int>(type: "integer", nullable: true),
                    ResolutionNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ResolutionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    RangeFrom = table.Column<long>(type: "bigint", nullable: false),
                    RangeTo = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: false),
                    Environment = table.Column<int>(type: "integer", nullable: false),
                    LastIssuedNumber = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_COR_DianNumberingResolutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_ElectronicEmissionSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Environment = table.Column<int>(type: "integer", nullable: false),
                    SoftwareId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    TestSetId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    TestSetAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CredentialKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CredentialVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailDeliveryBy = table.Column<int>(type: "integer", nullable: false),
                    IssuerTaxId = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IssuerCheckDigit = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    IssuerBusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IssuerAddress = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IssuerMunicipalityDaneCode = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    IssuerEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_COR_ElectronicEmissionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_DianResolutionChannels",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResolutionId = table.Column<int>(type: "integer", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SoftwareId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    TechnicalKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_COR_DianResolutionChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_DianResolutionChannels_COR_DianNumberingResolutions_Res~",
                        column: x => x.ResolutionId,
                        principalSchema: "dbo",
                        principalTable: "COR_DianNumberingResolutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ElectronicDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceModule = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceDocumentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceDocumentTypeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DianDocumentTypeCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ResolutionId = table.Column<int>(type: "integer", nullable: true),
                    Prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Consecutive = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Environment = table.Column<int>(type: "integer", nullable: false),
                    EmissionSettingId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SoftwareId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CounterpartyTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CounterpartyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UniqueCode = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    UniqueCodeKind = table.Column<int>(type: "integer", nullable: true),
                    QrContent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ContingencyType = table.Column<int>(type: "integer", nullable: true),
                    ContingencyEventId = table.Column<int>(type: "integer", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailDeliveryBy = table.Column<int>(type: "integer", nullable: false),
                    EmailSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentVersionId = table.Column<int>(type: "integer", nullable: true),
                    CorrectsDocumentId = table.Column<int>(type: "integer", nullable: true),
                    WaitsForDocumentId = table.Column<int>(type: "integer", nullable: true),
                    RejectedBy = table.Column<int>(type: "integer", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TransmissionDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastOutcome = table.Column<int>(type: "integer", nullable: true),
                    LastMessagesJson = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_COR_ElectronicDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocuments_COR_DianContingencyEvents_Contingen~",
                        column: x => x.ContingencyEventId,
                        principalSchema: "dbo",
                        principalTable: "COR_DianContingencyEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocuments_COR_DianNumberingResolutions_Resolu~",
                        column: x => x.ResolutionId,
                        principalSchema: "dbo",
                        principalTable: "COR_DianNumberingResolutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocuments_COR_ElectronicDocuments_CorrectsDoc~",
                        column: x => x.CorrectsDocumentId,
                        principalSchema: "dbo",
                        principalTable: "COR_ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocuments_COR_ElectronicDocuments_WaitsForDoc~",
                        column: x => x.WaitsForDocumentId,
                        principalSchema: "dbo",
                        principalTable: "COR_ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocuments_COR_ElectronicEmissionSettings_Emis~",
                        column: x => x.EmissionSettingId,
                        principalSchema: "dbo",
                        principalTable: "COR_ElectronicEmissionSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocuments_SEC_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ElectronicDocumentVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ElectronicDocumentId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<short>(type: "smallint", nullable: false),
                    SourceDocumentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    CanonicalSchemaVersion = table.Column<short>(type: "smallint", nullable: false),
                    CanonicalSha256 = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    EconomicFingerprint = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CorrectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedFieldsJson = table.Column<string>(type: "text", nullable: true),
                    CanonicalAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignedXmlAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachedDocumentAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    GraphicPdfAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_COR_ElectronicDocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocumentVersions_COR_ElectronicDocuments_Elec~",
                        column: x => x.ElectronicDocumentId,
                        principalSchema: "dbo",
                        principalTable: "COR_ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ElectronicDocumentTransmissions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ElectronicDocumentId = table.Column<int>(type: "integer", nullable: false),
                    VersionId = table.Column<int>(type: "integer", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Operation = table.Column<int>(type: "integer", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    RequestedByKind = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequestSha256 = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    HttpStatus = table.Column<short>(type: "smallint", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DianStatusCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: true),
                    RawMessagesJson = table.Column<string>(type: "text", nullable: true),
                    TranslatedMessagesJson = table.Column<string>(type: "text", nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApplicationResponseAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_COR_ElectronicDocumentTransmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocumentTransmissions_COR_ElectronicDocumentV~",
                        column: x => x.VersionId,
                        principalSchema: "dbo",
                        principalTable: "COR_ElectronicDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocumentTransmissions_COR_ElectronicDocuments~",
                        column: x => x.ElectronicDocumentId,
                        principalSchema: "dbo",
                        principalTable: "COR_ElectronicDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_ElectronicDocumentTransmissions_SEC_Users_RequestedByUs~",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COR_DianContingencyEvents_ClosedByUserId",
                schema: "dbo",
                table: "COR_DianContingencyEvents",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_DianContingencyEvents_DetectedByUserId",
                schema: "dbo",
                table: "COR_DianContingencyEvents",
                column: "DetectedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_DianContingencyEvents_PublicId",
                schema: "dbo",
                table: "COR_DianContingencyEvents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_DianContingencyEvents_Type_Channel_Open",
                schema: "dbo",
                table: "COR_DianContingencyEvents",
                columns: new[] { "Type", "ChannelCode" },
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_COR_DianNumberingResolutions_Kind_Environment_Prefix_ValidFrom",
                schema: "dbo",
                table: "COR_DianNumberingResolutions",
                columns: new[] { "Kind", "Environment", "Prefix", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_COR_DianNumberingResolutions_Environment_Prefix_Number",
                schema: "dbo",
                table: "COR_DianNumberingResolutions",
                columns: new[] { "Environment", "Prefix", "ResolutionNumber" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_DianNumberingResolutions_PublicId",
                schema: "dbo",
                table: "COR_DianNumberingResolutions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_DianResolutionChannels_PublicId",
                schema: "dbo",
                table: "COR_DianResolutionChannels",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_DianResolutionChannels_Resolution_ValidFrom",
                schema: "dbo",
                table: "COR_DianResolutionChannels",
                columns: new[] { "ResolutionId", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_CancelledByUserId",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_ContingencyEventId",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "ContingencyEventId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_CorrectsDocumentId",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "CorrectsDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_CurrentVersionId",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_EmissionSettingId",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "EmissionSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_IssueDate",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_ResolutionId",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "ResolutionId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_Status_NextAttemptAt",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_UniqueCode",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "UniqueCode",
                filter: "\"UniqueCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocuments_WaitsForDocumentId",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "WaitsForDocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicDocuments_Environment_Prefix_Consecutive",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                columns: new[] { "Environment", "Prefix", "Consecutive" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicDocuments_PublicId",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicDocuments_Source_Kind",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                columns: new[] { "SourceModule", "SourceDocumentPublicId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocumentTransmissions_RequestedByUserId",
                schema: "dbo",
                table: "COR_ElectronicDocumentTransmissions",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_ElectronicDocumentTransmissions_VersionId",
                schema: "dbo",
                table: "COR_ElectronicDocumentTransmissions",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicDocumentTransmissions_Document_Attempt",
                schema: "dbo",
                table: "COR_ElectronicDocumentTransmissions",
                columns: new[] { "ElectronicDocumentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicDocumentTransmissions_PublicId",
                schema: "dbo",
                table: "COR_ElectronicDocumentTransmissions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicDocumentVersions_Document_Version",
                schema: "dbo",
                table: "COR_ElectronicDocumentVersions",
                columns: new[] { "ElectronicDocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicDocumentVersions_PublicId",
                schema: "dbo",
                table: "COR_ElectronicDocumentVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicEmissionSettings_PublicId",
                schema: "dbo",
                table: "COR_ElectronicEmissionSettings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_ElectronicEmissionSettings_ValidFrom",
                schema: "dbo",
                table: "COR_ElectronicEmissionSettings",
                column: "ValidFrom",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.AddForeignKey(
                name: "FK_COR_ElectronicDocuments_COR_ElectronicDocumentVersions_Curr~",
                schema: "dbo",
                table: "COR_ElectronicDocuments",
                column: "CurrentVersionId",
                principalSchema: "dbo",
                principalTable: "COR_ElectronicDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_COR_ElectronicDocuments_COR_DianNumberingResolutions_Resolu~",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_COR_ElectronicDocuments_COR_DianContingencyEvents_Contingen~",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_COR_ElectronicDocuments_COR_ElectronicDocumentVersions_Curr~",
                schema: "dbo",
                table: "COR_ElectronicDocuments");

            migrationBuilder.DropTable(
                name: "COR_DianResolutionChannels",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ElectronicDocumentTransmissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_DianNumberingResolutions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_DianContingencyEvents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ElectronicDocumentVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ElectronicDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ElectronicEmissionSettings",
                schema: "dbo");
        }
    }
}
