using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class PlataformaParaCartera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_COR_NotificationTemplates_Name_Channel",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropIndex(
                name: "UK_CMP_HabeasPolicyVersions_Current",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions");

            migrationBuilder.DropIndex(
                name: "UK_CMP_HabeasPolicyVersions_TenantVersion",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions");

            migrationBuilder.AddColumn<Guid>(
                name: "NationalValuePublicId",
                schema: "dbo",
                table: "PAY_Holidays",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSeeded",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Lending");

            migrationBuilder.AddColumn<string>(
                name: "ProviderTemplateId",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ReviewPending",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidFrom",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Variables",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Plantillas previas (data-model §4.12 dice que la tabla nace vacía; por si alguna base tiene filas): un código
            // propio por fila y la vigencia desde su creación, para que el único versionado no choque.
            migrationBuilder.Sql(@"
UPDATE dbo.""COR_NotificationTemplates""
SET ""Code"" = CONCAT('PREV', ""Id""), ""ValidFrom"" = CAST(""CreatedAt"" AS date)
WHERE ""Code"" = '';
");

            migrationBuilder.AlterColumn<Guid>(
                name: "OriginUserCentralId",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "OriginActorKind",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "ScopeBranchPublicId",
                schema: "dbo",
                table: "COR_Alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScopeCreditLinePublicId",
                schema: "dbo",
                table: "COR_Alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "GeneralTreatment");

            migrationBuilder.AddColumn<string>(
                name: "ContactChannel",
                schema: "dbo",
                table: "CMP_HabeasDataConsents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EvidenceAttachmentPublicId",
                schema: "dbo",
                table: "CMP_HabeasDataConsents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                schema: "dbo",
                table: "CMP_HabeasDataConsents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PersonRegistration");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "dbo",
                table: "CMP_HabeasDataConsents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "GeneralTreatment");

            migrationBuilder.CreateTable(
                name: "COR_DataSubjectRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ContactChannel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReceivedVia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExtendedDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExtensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    DutyBasis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DutyEvidenceJson = table.Column<string>(type: "text", nullable: true),
                    ResponseText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResponseAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RespondedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COR_DataSubjectRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_DataSubjectRequests_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_DataSubjectRequests_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COR_DataSubjectRequests_SEC_Users_RespondedByUserId",
                        column: x => x.RespondedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_ExternalChannelSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    BureauCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SettingKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CredentialKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VerifiedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LastVerificationOutcome = table.Column<int>(type: "integer", nullable: true),
                    LastVerificationMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_COR_ExternalChannelSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_ExternalChannelSettings_SEC_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_IntegrationParticipants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Class = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Destination = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsTestDouble = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RegisteredByKind = table.Column<int>(type: "integer", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: true),
                    RegisteredByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_COR_IntegrationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationParticipants_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_NationalValueReplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromSequence = table.Column<long>(type: "bigint", nullable: false),
                    ToSequence = table.Column<long>(type: "bigint", nullable: false),
                    ValueCount = table.Column<int>(type: "integer", nullable: false),
                    DocumentCount = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectsSummaryJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COR_NationalValueReplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "COR_NationalValueReviews",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TargetKind = table.Column<int>(type: "integer", nullable: false),
                    TargetPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReviewedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COR_NationalValueReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_NationalValueReviews_SEC_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_RegulatoryProfileVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationTypeCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FinancialActivityAuthorized = table.Column<bool>(type: "boolean", nullable: false),
                    SupervisionLevel = table.Column<byte>(type: "smallint", nullable: false),
                    EmployeeFundCategory = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NiifGroup = table.Column<byte>(type: "smallint", nullable: true),
                    LibranzaOperatorRegistered = table.Column<bool>(type: "boolean", nullable: false),
                    LibranzaOperatorRegistrationNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LibranzaOperatorRegistrationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SupersedesPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COR_RegulatoryProfileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_RegulatoryProfileVersions_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_RestrictiveListLoads",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PublishedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EntryCount = table.Column<int>(type: "integer", nullable: false),
                    LoadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LoadedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COR_RestrictiveListLoads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_RestrictiveListLoads_SEC_Users_LoadedByUserId",
                        column: x => x.LoadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_IntegrationParticipantAdoptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    ContractName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Version = table.Column<short>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_COR_IntegrationParticipantAdoptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_IntegrationParticipantAdoptions_COR_IntegrationParticip~",
                        column: x => x.ParticipantId,
                        principalSchema: "dbo",
                        principalTable: "COR_IntegrationParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_NationalValues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(20,10)", precision: 20, scale: 10, nullable: true),
                    Unit = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourcePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSequence = table.Column<long>(type: "bigint", nullable: false),
                    SupersedesSourcePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CentralRegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KnownAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplicationId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COR_NationalValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_NationalValues_COR_NationalValueReplications_Replicatio~",
                        column: x => x.ReplicationId,
                        principalSchema: "dbo",
                        principalTable: "COR_NationalValueReplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_RegulatoryDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<short>(type: "smallint", nullable: false),
                    Verified = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourcePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSequence = table.Column<long>(type: "bigint", nullable: false),
                    SupersedesSourcePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CentralRegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KnownAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplicationId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COR_RegulatoryDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_RegulatoryDocuments_COR_NationalValueReplications_Repli~",
                        column: x => x.ReplicationId,
                        principalSchema: "dbo",
                        principalTable: "COR_NationalValueReplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "COR_RestrictiveListEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoadId = table.Column<int>(type: "integer", nullable: false),
                    EntityKind = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DocumentNumberNormalized = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FullName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NameNormalized = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Aliases = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Program = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COR_RestrictiveListEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_RestrictiveListEntries_COR_RestrictiveListLoads_LoadId",
                        column: x => x.LoadId,
                        principalSchema: "dbo",
                        principalTable: "COR_RestrictiveListLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_COR_NotificationTemplates_Code_Channel_Version",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                columns: new[] { "Code", "Channel", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_CMP_HabeasPolicyVersions_Current",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions",
                columns: new[] { "TenantId", "Purpose" },
                unique: true,
                filter: "\"EffectiveTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_CMP_HabeasPolicyVersions_TenantVersion",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions",
                columns: new[] { "TenantId", "Purpose", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CMP_HabeasConsents_Purpose",
                schema: "dbo",
                table: "CMP_HabeasDataConsents",
                columns: new[] { "PersonId", "Purpose", "ContactChannel", "ActionAt" },
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_COR_DataSubjectRequests_Person",
                schema: "dbo",
                table: "COR_DataSubjectRequests",
                columns: new[] { "PersonId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_DataSubjectRequests_RegisteredByUserId",
                schema: "dbo",
                table: "COR_DataSubjectRequests",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_DataSubjectRequests_RespondedByUserId",
                schema: "dbo",
                table: "COR_DataSubjectRequests",
                column: "RespondedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_DataSubjectRequests_Status_Due",
                schema: "dbo",
                table: "COR_DataSubjectRequests",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "UK_COR_DataSubjectRequests_PublicId",
                schema: "dbo",
                table: "COR_DataSubjectRequests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_ExternalChannelSettings_VerifiedByUser",
                schema: "dbo",
                table: "COR_ExternalChannelSettings",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ExternalChannelSettings_Key_ValidFrom",
                schema: "dbo",
                table: "COR_ExternalChannelSettings",
                columns: new[] { "SettingKey", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_ExternalChannelSettings_PublicId",
                schema: "dbo",
                table: "COR_ExternalChannelSettings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationParticipantAdoptions_Key",
                schema: "dbo",
                table: "COR_IntegrationParticipantAdoptions",
                columns: new[] { "ParticipantId", "ContractName", "Version", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationParticipantAdoptions_PublicId",
                schema: "dbo",
                table: "COR_IntegrationParticipantAdoptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationParticipants_Destination",
                schema: "dbo",
                table: "COR_IntegrationParticipants",
                columns: new[] { "Destination", "ValidFrom" },
                filter: "\"Role\" = 2 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_COR_IntegrationParticipants_RegisteredByUser",
                schema: "dbo",
                table: "COR_IntegrationParticipants",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationParticipants_Code_Role_Destination",
                schema: "dbo",
                table: "COR_IntegrationParticipants",
                columns: new[] { "Code", "Role", "Destination" },
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_IntegrationParticipants_PublicId",
                schema: "dbo",
                table: "COR_IntegrationParticipants",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_NationalValueReplications_StartedAt",
                schema: "dbo",
                table: "COR_NationalValueReplications",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "UK_COR_NationalValueReplications_PublicId",
                schema: "dbo",
                table: "COR_NationalValueReplications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_NationalValueReviews_ReviewedByUserId",
                schema: "dbo",
                table: "COR_NationalValueReviews",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_NationalValueReviews_Target",
                schema: "dbo",
                table: "COR_NationalValueReviews",
                columns: new[] { "TargetPublicId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "UK_COR_NationalValueReviews_PublicId",
                schema: "dbo",
                table: "COR_NationalValueReviews",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_NationalValues_Code_Key_ValidFrom",
                schema: "dbo",
                table: "COR_NationalValues",
                columns: new[] { "Code", "Key", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_NationalValues_ReplicationId",
                schema: "dbo",
                table: "COR_NationalValues",
                column: "ReplicationId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_NationalValues_PublicId",
                schema: "dbo",
                table: "COR_NationalValues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_NationalValues_SourcePublicId",
                schema: "dbo",
                table: "COR_NationalValues",
                column: "SourcePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_NationalValues_SourceSequence",
                schema: "dbo",
                table: "COR_NationalValues",
                column: "SourceSequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_RegulatoryDocuments_Kind_ValidFrom",
                schema: "dbo",
                table: "COR_RegulatoryDocuments",
                columns: new[] { "Kind", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_COR_RegulatoryDocuments_ReplicationId",
                schema: "dbo",
                table: "COR_RegulatoryDocuments",
                column: "ReplicationId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_RegulatoryDocuments_PublicId",
                schema: "dbo",
                table: "COR_RegulatoryDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_RegulatoryDocuments_SourcePublicId",
                schema: "dbo",
                table: "COR_RegulatoryDocuments",
                column: "SourcePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_RegulatoryDocuments_SourceSequence",
                schema: "dbo",
                table: "COR_RegulatoryDocuments",
                column: "SourceSequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_RegulatoryProfileVersions_RegisteredByUserId",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_RegulatoryProfileVersions_ValidFrom",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                column: "ValidFrom");

            migrationBuilder.CreateIndex(
                name: "UK_COR_RegulatoryProfileVersions_PublicId",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_RegulatoryProfileVersions_Supersedes",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                column: "SupersedesPublicId",
                unique: true,
                filter: "\"SupersedesPublicId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_RestrictiveListEntries_Document",
                schema: "dbo",
                table: "COR_RestrictiveListEntries",
                column: "DocumentNumberNormalized",
                filter: "\"DocumentNumberNormalized\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_RestrictiveListEntries_Load",
                schema: "dbo",
                table: "COR_RestrictiveListEntries",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_RestrictiveListEntries_PublicId",
                schema: "dbo",
                table: "COR_RestrictiveListEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_RestrictiveListLoads_LoadedByUserId",
                schema: "dbo",
                table: "COR_RestrictiveListLoads",
                column: "LoadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_RestrictiveListLoads_Code_Version",
                schema: "dbo",
                table: "COR_RestrictiveListLoads",
                columns: new[] { "ListCode", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_RestrictiveListLoads_PublicId",
                schema: "dbo",
                table: "COR_RestrictiveListLoads",
                column: "PublicId",
                unique: true);

            // Semilla de la plataforma para Cartera (data-model §4.6, C35, C36; DT §2.26). Idempotente: sólo lo que falte.
            // El arrendamiento del proceso de Cartera, libre y vencido (como los cinco de PlataformaParaInventario).
            migrationBuilder.Sql(@"
INSERT INTO dbo.""COR_BackgroundLeases"" (""Name"", ""Owner"", ""LeaseUntil"", ""PublicId"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT v.name, NULL, TIMESTAMPTZ '2000-01-01 00:00:00+00', gen_random_uuid(), FALSE, now(), 'SYSTEM'
FROM (VALUES ('lending.process')) AS v(name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.""COR_BackgroundLeases"" l WHERE l.""Name"" = v.name);
");

            // Participantes: CNT, INV y NOM habilitados; CAR registrado e inhabilitado hasta la activación de Cartera.
            // Class: Accounting 6, Inventory 8, Payroll 2, Lending 7. Role: ObligationsCollector 1, MessageDestination 2.
            // RegisteredByKind: Process (2).
            migrationBuilder.Sql(@"
INSERT INTO dbo.""COR_IntegrationParticipants"" (""Code"", ""Class"", ""Role"", ""Destination"", ""IsTestDouble"", ""IsEnabled"", ""ValidFrom"", ""ValidTo"", ""Reason"", ""RegisteredByKind"", ""RegisteredByUserId"", ""RegisteredByName"", ""PublicId"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT v.code, v.clase, v.rol, v.destino, FALSE, v.habilitado, DATE '1900-01-01', NULL, v.motivo, 2, NULL, 'Migración PlataformaParaCartera', gen_random_uuid(), FALSE, now(), 'SYSTEM'
FROM (VALUES
    ('CNT', 6, 2, 'Accounting', TRUE,  'Contabilidad consume la bandeja desde la feature 012'),
    ('INV', 8, 2, 'Inventory',  TRUE,  'Comercial recibe las respuestas de Cartera a la venta a crédito'),
    ('NOM', 2, 1, '',           TRUE,  'Nómina recauda obligaciones por descuento de nómina'),
    ('CAR', 7, 2, 'Lending',    FALSE, 'Destino de Cartera: inhabilitado hasta la activación')
) AS v(code, clase, rol, destino, habilitado, motivo)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.""COR_IntegrationParticipants"" p
    WHERE p.""Code"" = v.code AND p.""Role"" = v.rol AND p.""Destination"" = v.destino AND p.""IsDeleted"" = FALSE);
");

            // Adopciones de esos participantes (CNT no tiene: las versiones las declara su consumidor).
            migrationBuilder.Sql(@"
INSERT INTO dbo.""COR_IntegrationParticipantAdoptions"" (""ParticipantId"", ""ContractName"", ""Version"", ""ValidFrom"", ""ValidTo"", ""Reason"", ""PublicId"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
SELECT p.""Id"", v.contrato, CAST(v.version AS smallint), DATE '1900-01-01', NULL, 'Migración PlataformaParaCartera', gen_random_uuid(), FALSE, now(), 'SYSTEM'
FROM (VALUES
    ('INV', 2, 'Inventory', 'ObligacionCreada', 1),
    ('INV', 2, 'Inventory', 'ObligacionRechazada', 1),
    ('NOM', 1, '',          'ProveedorDeObligaciones', 1),
    ('CAR', 2, 'Lending',   'VentaACreditoRegistrada', 1),
    ('CAR', 2, 'Lending',   'VentaACreditoRegistrada', 2),
    ('CAR', 2, 'Lending',   'AjusteDeVentaACredito', 1),
    ('CAR', 2, 'Lending',   'ResultadoDelGiro', 1)
) AS v(code, rol, destino, contrato, version)
JOIN dbo.""COR_IntegrationParticipants"" p
  ON p.""Code"" = v.code AND p.""Role"" = v.rol AND p.""Destination"" = v.destino AND p.""ValidTo"" IS NULL AND p.""IsDeleted"" = FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.""COR_IntegrationParticipantAdoptions"" a
    WHERE a.""ParticipantId"" = p.""Id"" AND a.""ContractName"" = v.contrato AND a.""Version"" = v.version);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COR_DataSubjectRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_ExternalChannelSettings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_IntegrationParticipantAdoptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_NationalValueReviews",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_NationalValues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_RegulatoryDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_RegulatoryProfileVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_RestrictiveListEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_IntegrationParticipants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_NationalValueReplications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "COR_RestrictiveListLoads",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_COR_NotificationTemplates_Code_Channel_Version",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropIndex(
                name: "UK_CMP_HabeasPolicyVersions_Current",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions");

            migrationBuilder.DropIndex(
                name: "UK_CMP_HabeasPolicyVersions_TenantVersion",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions");

            migrationBuilder.DropIndex(
                name: "IX_CMP_HabeasConsents_Purpose",
                schema: "dbo",
                table: "CMP_HabeasDataConsents");

            migrationBuilder.DropColumn(
                name: "NationalValuePublicId",
                schema: "dbo",
                table: "PAY_Holidays");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "IsSeeded",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "Module",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "ProviderTemplateId",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "ReviewPending",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "Variables",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "dbo",
                table: "COR_NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "OriginActorKind",
                schema: "dbo",
                table: "COR_IntegrationMessages");

            migrationBuilder.DropColumn(
                name: "ScopeBranchPublicId",
                schema: "dbo",
                table: "COR_Alerts");

            migrationBuilder.DropColumn(
                name: "ScopeCreditLinePublicId",
                schema: "dbo",
                table: "COR_Alerts");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions");

            migrationBuilder.DropColumn(
                name: "ContactChannel",
                schema: "dbo",
                table: "CMP_HabeasDataConsents");

            migrationBuilder.DropColumn(
                name: "EvidenceAttachmentPublicId",
                schema: "dbo",
                table: "CMP_HabeasDataConsents");

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "dbo",
                table: "CMP_HabeasDataConsents");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "dbo",
                table: "CMP_HabeasDataConsents");

            migrationBuilder.AlterColumn<Guid>(
                name: "OriginUserCentralId",
                schema: "dbo",
                table: "COR_IntegrationMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_NotificationTemplates_Name_Channel",
                schema: "dbo",
                table: "COR_NotificationTemplates",
                columns: new[] { "TemplateName", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CMP_HabeasPolicyVersions_Current",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions",
                column: "TenantId",
                unique: true,
                filter: "\"EffectiveTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_CMP_HabeasPolicyVersions_TenantVersion",
                schema: "dbo",
                table: "CMP_HabeasDataPolicyVersions",
                columns: new[] { "TenantId", "VersionNumber" },
                unique: true);
        }
    }
}
