using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CarteraCobranza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentAgreementId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CampaignId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LND_CollectionActions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    ContactRole = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDirectContact = table.Column<bool>(type: "boolean", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CollectorUserId = table.Column<int>(type: "integer", nullable: false),
                    DebtorMessageId = table.Column<int>(type: "integer", nullable: true),
                    VoidsActionId = table.Column<int>(type: "integer", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WindowDecision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WindowReasonCodes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_LND_CollectionActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CollectionActions_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectionActions_LND_CollectionActions_VoidsActionId",
                        column: x => x.VoidsActionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CollectionActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectionActions_LND_DebtorMessages_DebtorMessageId",
                        column: x => x.DebtorMessageId,
                        principalSchema: "dbo",
                        principalTable: "LND_DebtorMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectionActions_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectionActions_SEC_Users_CollectorUserId",
                        column: x => x.CollectorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionStages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    MinDaysPastDue = table.Column<int>(type: "integer", nullable: false),
                    MaxDaysPastDue = table.Column<int>(type: "integer", nullable: true),
                    IsLegal = table.Column<bool>(type: "boolean", nullable: false),
                    SuggestedActionsJson = table.Column<string>(type: "text", nullable: true),
                    TemplateCodesJson = table.Column<string>(type: "text", nullable: true),
                    CollectionFeeRuleJson = table.Column<string>(type: "text", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_CollectionStages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectorAssignmentRules",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    OfficePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    StageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreditLineId = table.Column<int>(type: "integer", nullable: true),
                    PaymasterPersonId = table.Column<int>(type: "integer", nullable: true),
                    MinBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Distribution = table.Column<int>(type: "integer", nullable: false),
                    AssignWholeDebtor = table.Column<bool>(type: "boolean", nullable: false),
                    CollectorsJson = table.Column<string>(type: "text", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_CollectorAssignmentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CollectorAssignmentRules_COR_People_PaymasterPersonId",
                        column: x => x.PaymasterPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectorAssignmentRules_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LegalProcesses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    LawyerPersonId = table.Column<int>(type: "integer", nullable: false),
                    AssignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Authority = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CaseNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ProcessType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SuspendedByInsolvencyId = table.Column<int>(type: "integer", nullable: true),
                    FeeAgreementJson = table.Column<string>(type: "text", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_LND_LegalProcesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LegalProcesses_COR_People_LawyerPersonId",
                        column: x => x.LawyerPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LegalProcesses_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LegalProcesses_LND_InsolvencyProceedings_SuspendedByIns~",
                        column: x => x.SuspendedByInsolvencyId,
                        principalSchema: "dbo",
                        principalTable: "LND_InsolvencyProceedings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LegalProcesses_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MessageCampaigns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    SelectionCriteriaJson = table.Column<string>(type: "text", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TargetCount = table.Column<int>(type: "integer", nullable: false),
                    QueuedCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ScheduledByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MessageCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MessageCampaigns_COR_NotificationTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "dbo",
                        principalTable: "COR_NotificationTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MessageCampaigns_SEC_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MessageCampaigns_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MessageCampaigns_SEC_Users_ScheduledByUserId",
                        column: x => x.ScheduledByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PaymentAgreements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PastDueAmountAtAgreement = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InstallmentCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_PaymentAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PaymentAgreements_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PaymentAgreements_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PaymentAgreements_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PaymentAgreements_SEC_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PaymentPromises",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CollectionActionId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PromisedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PromisedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ToleranceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_LND_PaymentPromises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PaymentPromises_LND_CollectionActions_CollectionActionId",
                        column: x => x.CollectionActionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CollectionActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PaymentPromises_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionStageHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    CollectionStageId = table.Column<int>(type: "integer", nullable: false),
                    StageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EnteredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DaysPastDueAtEntry = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PreviousEntryId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_CollectionStageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CollectionStageHistory_LND_CollectionStageHistory_Previ~",
                        column: x => x.PreviousEntryId,
                        principalSchema: "dbo",
                        principalTable: "LND_CollectionStageHistory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectionStageHistory_LND_CollectionStages_CollectionS~",
                        column: x => x.CollectionStageId,
                        principalSchema: "dbo",
                        principalTable: "LND_CollectionStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectionStageHistory_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectionStageHistory_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectionStageHistory_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectorAssignments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    CollectorUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CollectorAssignmentRuleId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    PreviousAssignmentId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_CollectorAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CollectorAssignments_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectorAssignments_LND_CollectorAssignmentRules_Colle~",
                        column: x => x.CollectorAssignmentRuleId,
                        principalSchema: "dbo",
                        principalTable: "LND_CollectorAssignmentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectorAssignments_LND_CollectorAssignments_PreviousA~",
                        column: x => x.PreviousAssignmentId,
                        principalSchema: "dbo",
                        principalTable: "LND_CollectorAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectorAssignments_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectorAssignments_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CollectorAssignments_SEC_Users_CollectorUserId",
                        column: x => x.CollectorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LegalProcessActions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LegalProcessId = table.Column<int>(type: "integer", nullable: false),
                    ActionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ChargeableToDebtor = table.Column<bool>(type: "boolean", nullable: false),
                    ChargeOperationId = table.Column<long>(type: "bigint", nullable: true),
                    AttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_LegalProcessActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LegalProcessActions_LND_LegalProcesses_LegalProcessId",
                        column: x => x.LegalProcessId,
                        principalSchema: "dbo",
                        principalTable: "LND_LegalProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LegalProcessActions_LND_LoanOperations_ChargeOperationId",
                        column: x => x.ChargeOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LegalProcessActions_SEC_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PaymentAgreementInstallments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentAgreementId = table.Column<int>(type: "integer", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_LND_PaymentAgreementInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PaymentAgreementInstallments_LND_PaymentAgreements_Paym~",
                        column: x => x.PaymentAgreementId,
                        principalSchema: "dbo",
                        principalTable: "LND_PaymentAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_PaymentAgreementId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "PaymentAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DebtorMessages_CampaignId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "CampaignId",
                filter: "\"CampaignId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionActions_CollectorUserId",
                schema: "dbo",
                table: "LND_CollectionActions",
                column: "CollectorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionActions_DebtorMessageId",
                schema: "dbo",
                table: "LND_CollectionActions",
                column: "DebtorMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionActions_Loan_OccurredAt",
                schema: "dbo",
                table: "LND_CollectionActions",
                columns: new[] { "LoanId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionActions_Person_OccurredAt",
                schema: "dbo",
                table: "LND_CollectionActions",
                columns: new[] { "PersonId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionActions_PublicId",
                schema: "dbo",
                table: "LND_CollectionActions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionActions_VoidsActionId",
                schema: "dbo",
                table: "LND_CollectionActions",
                column: "VoidsActionId",
                unique: true,
                filter: "\"VoidsActionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionStageHistory_ActorUserId",
                schema: "dbo",
                table: "LND_CollectionStageHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionStageHistory_CollectionStageId",
                schema: "dbo",
                table: "LND_CollectionStageHistory",
                column: "CollectionStageId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionStageHistory_Loan_EnteredOn",
                schema: "dbo",
                table: "LND_CollectionStageHistory",
                columns: new[] { "LoanId", "EnteredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionStageHistory_PreviousEntryId",
                schema: "dbo",
                table: "LND_CollectionStageHistory",
                column: "PreviousEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionStageHistory_ProcessRunId",
                schema: "dbo",
                table: "LND_CollectionStageHistory",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionStageHistory_Migration",
                schema: "dbo",
                table: "LND_CollectionStageHistory",
                column: "LoanId",
                unique: true,
                filter: "\"Source\" = 'Migration'");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionStageHistory_PublicId",
                schema: "dbo",
                table: "LND_CollectionStageHistory",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionStages_Validity",
                schema: "dbo",
                table: "LND_CollectionStages",
                columns: new[] { "ValidFrom", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionStages_Code_ValidFrom",
                schema: "dbo",
                table: "LND_CollectionStages",
                columns: new[] { "Code", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionStages_PublicId",
                schema: "dbo",
                table: "LND_CollectionStages",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignmentRules_CreditLineId",
                schema: "dbo",
                table: "LND_CollectorAssignmentRules",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignmentRules_PaymasterPersonId",
                schema: "dbo",
                table: "LND_CollectorAssignmentRules",
                column: "PaymasterPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignmentRules_Priority_ValidFrom",
                schema: "dbo",
                table: "LND_CollectorAssignmentRules",
                columns: new[] { "Priority", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectorAssignmentRules_PublicId",
                schema: "dbo",
                table: "LND_CollectorAssignmentRules",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignments_ActorUserId",
                schema: "dbo",
                table: "LND_CollectorAssignments",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignments_Collector_AssignedOn",
                schema: "dbo",
                table: "LND_CollectorAssignments",
                columns: new[] { "CollectorUserId", "AssignedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignments_Loan_AssignedOn",
                schema: "dbo",
                table: "LND_CollectorAssignments",
                columns: new[] { "LoanId", "AssignedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignments_Person_AssignedOn",
                schema: "dbo",
                table: "LND_CollectorAssignments",
                columns: new[] { "PersonId", "AssignedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignments_PreviousAssignmentId",
                schema: "dbo",
                table: "LND_CollectorAssignments",
                column: "PreviousAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectorAssignments_RuleId",
                schema: "dbo",
                table: "LND_CollectorAssignments",
                column: "CollectorAssignmentRuleId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectorAssignments_PublicId",
                schema: "dbo",
                table: "LND_CollectorAssignments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LegalProcessActions_Process_ActionDate",
                schema: "dbo",
                table: "LND_LegalProcessActions",
                columns: new[] { "LegalProcessId", "ActionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LegalProcessActions_RecordedByUserId",
                schema: "dbo",
                table: "LND_LegalProcessActions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LegalProcessActions_ChargeOperationId",
                schema: "dbo",
                table: "LND_LegalProcessActions",
                column: "ChargeOperationId",
                unique: true,
                filter: "\"ChargeOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LegalProcessActions_PublicId",
                schema: "dbo",
                table: "LND_LegalProcessActions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LegalProcesses_LawyerPersonId",
                schema: "dbo",
                table: "LND_LegalProcesses",
                column: "LawyerPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LegalProcesses_Loan_Status",
                schema: "dbo",
                table: "LND_LegalProcesses",
                columns: new[] { "LoanId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LegalProcesses_PersonId",
                schema: "dbo",
                table: "LND_LegalProcesses",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LegalProcesses_SuspendedByInsolvencyId",
                schema: "dbo",
                table: "LND_LegalProcesses",
                column: "SuspendedByInsolvencyId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LegalProcesses_Number",
                schema: "dbo",
                table: "LND_LegalProcesses",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LegalProcesses_PublicId",
                schema: "dbo",
                table: "LND_LegalProcesses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MessageCampaigns_CancelledByUserId",
                schema: "dbo",
                table: "LND_MessageCampaigns",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MessageCampaigns_CreatedByUserId",
                schema: "dbo",
                table: "LND_MessageCampaigns",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MessageCampaigns_ScheduledByUserId",
                schema: "dbo",
                table: "LND_MessageCampaigns",
                column: "ScheduledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MessageCampaigns_Status_ScheduledFor",
                schema: "dbo",
                table: "LND_MessageCampaigns",
                columns: new[] { "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_MessageCampaigns_TemplateId",
                schema: "dbo",
                table: "LND_MessageCampaigns",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MessageCampaigns_Code",
                schema: "dbo",
                table: "LND_MessageCampaigns",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MessageCampaigns_PublicId",
                schema: "dbo",
                table: "LND_MessageCampaigns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PaymentAgreementInstallments_Agreement_Number",
                schema: "dbo",
                table: "LND_PaymentAgreementInstallments",
                columns: new[] { "PaymentAgreementId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PaymentAgreementInstallments_PublicId",
                schema: "dbo",
                table: "LND_PaymentAgreementInstallments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PaymentAgreements_ApprovedByUserId",
                schema: "dbo",
                table: "LND_PaymentAgreements",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PaymentAgreements_CreatedByUserId",
                schema: "dbo",
                table: "LND_PaymentAgreements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PaymentAgreements_Loan_Status",
                schema: "dbo",
                table: "LND_PaymentAgreements",
                columns: new[] { "LoanId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_PaymentAgreements_PersonId",
                schema: "dbo",
                table: "LND_PaymentAgreements",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PaymentAgreements_Number",
                schema: "dbo",
                table: "LND_PaymentAgreements",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PaymentAgreements_PublicId",
                schema: "dbo",
                table: "LND_PaymentAgreements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PaymentPromises_Loan_PromisedDate",
                schema: "dbo",
                table: "LND_PaymentPromises",
                columns: new[] { "LoanId", "PromisedDate" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_PaymentPromises_CollectionActionId",
                schema: "dbo",
                table: "LND_PaymentPromises",
                column: "CollectionActionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PaymentPromises_PublicId",
                schema: "dbo",
                table: "LND_PaymentPromises",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_DebtorMessages_LND_MessageCampaigns_CampaignId",
                schema: "dbo",
                table: "LND_DebtorMessages",
                column: "CampaignId",
                principalSchema: "dbo",
                principalTable: "LND_MessageCampaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanNovelties_LND_PaymentAgreements_PaymentAgreementId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "PaymentAgreementId",
                principalSchema: "dbo",
                principalTable: "LND_PaymentAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LND_DebtorMessages_LND_MessageCampaigns_CampaignId",
                schema: "dbo",
                table: "LND_DebtorMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanNovelties_LND_PaymentAgreements_PaymentAgreementId",
                schema: "dbo",
                table: "LND_LoanNovelties");

            migrationBuilder.DropTable(
                name: "LND_CollectionStageHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectorAssignments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LegalProcessActions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MessageCampaigns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PaymentAgreementInstallments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PaymentPromises",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionStages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectorAssignmentRules",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LegalProcesses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PaymentAgreements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionActions",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanNovelties_PaymentAgreementId",
                schema: "dbo",
                table: "LND_LoanNovelties");

            migrationBuilder.DropIndex(
                name: "IX_LND_DebtorMessages_CampaignId",
                schema: "dbo",
                table: "LND_DebtorMessages");

            migrationBuilder.DropColumn(
                name: "PaymentAgreementId",
                schema: "dbo",
                table: "LND_LoanNovelties");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                schema: "dbo",
                table: "LND_DebtorMessages");
        }
    }
}
