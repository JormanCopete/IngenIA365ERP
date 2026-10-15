using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CarteraNovedades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NoveltyId",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoveltyId",
                schema: "dbo",
                table: "LND_LoanOperations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WaiverId",
                schema: "dbo",
                table: "LND_LoanOperations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WriteOffBatchItemId",
                schema: "dbo",
                table: "LND_LoanOperations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgreementNoveltyId",
                schema: "dbo",
                table: "LND_InsolvencyProceedings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LND_LoanNovelties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    Mechanism = table.Column<int>(type: "integer", nullable: false),
                    Cause = table.Column<int>(type: "integer", nullable: false),
                    CauseDeterminationJson = table.Column<string>(type: "text", nullable: false),
                    InsolvencyProceedingId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProposedTermsJson = table.Column<string>(type: "text", nullable: false),
                    SimulationJson = table.Column<string>(type: "text", nullable: false),
                    SimulationSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RatingAtRequest = table.Column<int>(type: "integer", nullable: false),
                    RestructuringCountAfter = table.Column<int>(type: "integer", nullable: true),
                    FinalDecisionId = table.Column<int>(type: "integer", nullable: true),
                    ConditionsJson = table.Column<string>(type: "text", nullable: true),
                    PromissoryNoteId = table.Column<int>(type: "integer", nullable: true),
                    AppliedOperationId = table.Column<long>(type: "bigint", nullable: true),
                    NewTermsVersionId = table.Column<int>(type: "integer", nullable: true),
                    NewScheduleVersionId = table.Column<int>(type: "integer", nullable: true),
                    ResultingLoanId = table.Column<int>(type: "integer", nullable: true),
                    AppliedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanNovelties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_LND_CreditDecisions_FinalDecisionId",
                        column: x => x.FinalDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_LND_InsolvencyProceedings_InsolvencyProce~",
                        column: x => x.InsolvencyProceedingId,
                        principalSchema: "dbo",
                        principalTable: "LND_InsolvencyProceedings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_LND_LoanOperations_AppliedOperationId",
                        column: x => x.AppliedOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_LND_LoanTermsVersions_NewTermsVersionId",
                        column: x => x.NewTermsVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanTermsVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_LND_Loans_ResultingLoanId",
                        column: x => x.ResultingLoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_LND_PromissoryNotes_PromissoryNoteId",
                        column: x => x.PromissoryNoteId,
                        principalSchema: "dbo",
                        principalTable: "LND_PromissoryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_LND_RepaymentScheduleVersions_NewSchedule~",
                        column: x => x.NewScheduleVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_RepaymentScheduleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_SEC_Users_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNovelties_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PortfolioEvaluations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveYear = table.Column<short>(type: "smallint", nullable: false),
                    EffectiveMonth = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriteriaJson = table.Column<string>(type: "text", nullable: false),
                    PreparedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecommendationSessionItemId = table.Column<int>(type: "integer", nullable: true),
                    ApprovalSessionItemId = table.Column<int>(type: "integer", nullable: true),
                    AppliedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoanCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_PortfolioEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PortfolioEvaluations_LND_SessionItems_ApprovalSessionIt~",
                        column: x => x.ApprovalSessionItemId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PortfolioEvaluations_LND_SessionItems_RecommendationSes~",
                        column: x => x.RecommendationSessionItemId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PortfolioEvaluations_SEC_Users_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PortfolioEvaluations_SEC_Users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_Waivers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DaysPastDueAtRequest = table.Column<int>(type: "integer", nullable: false),
                    IsRelatedParty = table.Column<bool>(type: "boolean", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalDecisionId = table.Column<int>(type: "integer", nullable: true),
                    AppliedOperationId = table.Column<long>(type: "bigint", nullable: true),
                    AppliedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_LND_Waivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_Waivers_LND_CreditDecisions_FinalDecisionId",
                        column: x => x.FinalDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Waivers_LND_LoanOperations_AppliedOperationId",
                        column: x => x.AppliedOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Waivers_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Waivers_SEC_Users_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_Waivers_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_WriteOffBatches",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PreparedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PreparedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionItemId = table.Column<int>(type: "integer", nullable: true),
                    AppliedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoanCount = table.Column<int>(type: "integer", nullable: false),
                    TotalPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalOther = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SupervisorReportRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SupervisorReportDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SupervisorReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupervisorReportReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SupervisorReportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_WriteOffBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_WriteOffBatches_LND_SessionItems_SessionItemId",
                        column: x => x.SessionItemId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_WriteOffBatches_SEC_Users_AppliedByUserId",
                        column: x => x.AppliedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_WriteOffBatches_SEC_Users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanNoveltyConsents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanNoveltyId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    CollateralId = table.Column<int>(type: "integer", nullable: true),
                    PartyRole = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SignedAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefusalResolution = table.Column<int>(type: "integer", nullable: true),
                    ResolutionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CoverageAfterJson = table.Column<string>(type: "text", nullable: true),
                    ExceptionDecisionId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanNoveltyConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanNoveltyConsents_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNoveltyConsents_LND_Collaterals_CollateralId",
                        column: x => x.CollateralId,
                        principalSchema: "dbo",
                        principalTable: "LND_Collaterals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNoveltyConsents_LND_CreditDecisions_ExceptionDecisi~",
                        column: x => x.ExceptionDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNoveltyConsents_LND_LoanNovelties_LoanNoveltyId",
                        column: x => x.LoanNoveltyId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanNovelties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNoveltyConsents_SEC_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanNoveltyLoans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanNoveltyId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_LoanNoveltyLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanNoveltyLoans_LND_LoanNovelties_LoanNoveltyId",
                        column: x => x.LoanNoveltyId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanNovelties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanNoveltyLoans_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PortfolioEvaluationLoans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PortfolioEvaluationId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    RuleRatingAtCutoff = table.Column<int>(type: "integer", nullable: false),
                    ProposedRating = table.Column<int>(type: "integer", nullable: false),
                    ProposedSubcategory = table.Column<int>(type: "integer", nullable: true),
                    Justification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CriteriaScoresJson = table.Column<string>(type: "text", nullable: true),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_LND_PortfolioEvaluationLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PortfolioEvaluationLoans_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PortfolioEvaluationLoans_LND_PortfolioEvaluations_Portf~",
                        column: x => x.PortfolioEvaluationId,
                        principalSchema: "dbo",
                        principalTable: "LND_PortfolioEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_WaiverLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WaiverId = table.Column<int>(type: "integer", nullable: false),
                    Concept = table.Column<int>(type: "integer", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableAtRequest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_LND_WaiverLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_WaiverLines_LND_Waivers_WaiverId",
                        column: x => x.WaiverId,
                        principalSchema: "dbo",
                        principalTable: "LND_Waivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_WriteOffBatchItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WriteOffBatchId = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequirementsJson = table.Column<string>(type: "text", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    IrrecoverabilityAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImpairmentAtCutoff = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WriteOffOperationId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_LND_WriteOffBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_WriteOffBatchItems_LND_LoanOperations_WriteOffOperation~",
                        column: x => x.WriteOffOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_WriteOffBatchItems_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_WriteOffBatchItems_LND_WriteOffBatches_WriteOffBatchId",
                        column: x => x.WriteOffBatchId,
                        principalSchema: "dbo",
                        principalTable: "LND_WriteOffBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanTermsVersions_NoveltyId",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                column: "NoveltyId",
                filter: "\"NoveltyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_NoveltyId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "NoveltyId",
                filter: "\"NoveltyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_WaiverId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "WaiverId",
                filter: "\"WaiverId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanOperations_WriteOffBatchItemId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "WriteOffBatchItemId",
                filter: "\"WriteOffBatchItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_InsolvencyProceedings_AgreementNoveltyId",
                schema: "dbo",
                table: "LND_InsolvencyProceedings",
                column: "AgreementNoveltyId",
                filter: "\"AgreementNoveltyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_AppliedByUserId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_AppliedOperationId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "AppliedOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_FinalDecisionId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "FinalDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_InsolvencyProceedingId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "InsolvencyProceedingId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_Loan_Status",
                schema: "dbo",
                table: "LND_LoanNovelties",
                columns: new[] { "LoanId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_NewScheduleVersionId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "NewScheduleVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_NewTermsVersionId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "NewTermsVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_PromissoryNoteId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "PromissoryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_RequestedByUserId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNovelties_ResultingLoanId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "ResultingLoanId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNovelties_Number",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNovelties_PublicId",
                schema: "dbo",
                table: "LND_LoanNovelties",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNoveltyConsents_CollateralId",
                schema: "dbo",
                table: "LND_LoanNoveltyConsents",
                column: "CollateralId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNoveltyConsents_ExceptionDecisionId",
                schema: "dbo",
                table: "LND_LoanNoveltyConsents",
                column: "ExceptionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNoveltyConsents_PersonId",
                schema: "dbo",
                table: "LND_LoanNoveltyConsents",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNoveltyConsents_RecordedByUserId",
                schema: "dbo",
                table: "LND_LoanNoveltyConsents",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNoveltyConsents_Novelty_Person_Collateral",
                schema: "dbo",
                table: "LND_LoanNoveltyConsents",
                columns: new[] { "LoanNoveltyId", "PersonId", "CollateralId" },
                unique: true,
                filter: "\"CollateralId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNoveltyConsents_Novelty_Person_NoCollateral",
                schema: "dbo",
                table: "LND_LoanNoveltyConsents",
                columns: new[] { "LoanNoveltyId", "PersonId" },
                unique: true,
                filter: "\"CollateralId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNoveltyConsents_PublicId",
                schema: "dbo",
                table: "LND_LoanNoveltyConsents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanNoveltyLoans_LoanId",
                schema: "dbo",
                table: "LND_LoanNoveltyLoans",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNoveltyLoans_Novelty_Loan",
                schema: "dbo",
                table: "LND_LoanNoveltyLoans",
                columns: new[] { "LoanNoveltyId", "LoanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanNoveltyLoans_PublicId",
                schema: "dbo",
                table: "LND_LoanNoveltyLoans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioEvaluationLoans_LoanId",
                schema: "dbo",
                table: "LND_PortfolioEvaluationLoans",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PortfolioEvaluationLoans_Evaluation_Loan",
                schema: "dbo",
                table: "LND_PortfolioEvaluationLoans",
                columns: new[] { "PortfolioEvaluationId", "LoanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PortfolioEvaluationLoans_PublicId",
                schema: "dbo",
                table: "LND_PortfolioEvaluationLoans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioEvaluations_AppliedByUserId",
                schema: "dbo",
                table: "LND_PortfolioEvaluations",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioEvaluations_ApprovalItemId",
                schema: "dbo",
                table: "LND_PortfolioEvaluations",
                column: "ApprovalSessionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioEvaluations_Cutoff_Status",
                schema: "dbo",
                table: "LND_PortfolioEvaluations",
                columns: new[] { "CutoffDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioEvaluations_Effective",
                schema: "dbo",
                table: "LND_PortfolioEvaluations",
                columns: new[] { "EffectiveYear", "EffectiveMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioEvaluations_PreparedByUserId",
                schema: "dbo",
                table: "LND_PortfolioEvaluations",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioEvaluations_RecommendationItemId",
                schema: "dbo",
                table: "LND_PortfolioEvaluations",
                column: "RecommendationSessionItemId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PortfolioEvaluations_Number",
                schema: "dbo",
                table: "LND_PortfolioEvaluations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PortfolioEvaluations_PublicId",
                schema: "dbo",
                table: "LND_PortfolioEvaluations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_WaiverLines_PublicId",
                schema: "dbo",
                table: "LND_WaiverLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_WaiverLines_Waiver_Concept_Installment",
                schema: "dbo",
                table: "LND_WaiverLines",
                columns: new[] { "WaiverId", "Concept", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_Waivers_AppliedByUserId",
                schema: "dbo",
                table: "LND_Waivers",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Waivers_AppliedOperationId",
                schema: "dbo",
                table: "LND_Waivers",
                column: "AppliedOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Waivers_FinalDecisionId",
                schema: "dbo",
                table: "LND_Waivers",
                column: "FinalDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Waivers_Loan_Status",
                schema: "dbo",
                table: "LND_Waivers",
                columns: new[] { "LoanId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_Waivers_RequestedByUserId",
                schema: "dbo",
                table: "LND_Waivers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Waivers_Number",
                schema: "dbo",
                table: "LND_Waivers",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Waivers_PublicId",
                schema: "dbo",
                table: "LND_Waivers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_WriteOffBatches_AppliedByUserId",
                schema: "dbo",
                table: "LND_WriteOffBatches",
                column: "AppliedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_WriteOffBatches_PreparedByUserId",
                schema: "dbo",
                table: "LND_WriteOffBatches",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_WriteOffBatches_SessionItemId",
                schema: "dbo",
                table: "LND_WriteOffBatches",
                column: "SessionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_WriteOffBatches_Status",
                schema: "dbo",
                table: "LND_WriteOffBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_LND_WriteOffBatches_Number",
                schema: "dbo",
                table: "LND_WriteOffBatches",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_WriteOffBatches_PublicId",
                schema: "dbo",
                table: "LND_WriteOffBatches",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_WriteOffBatchItems_LoanId",
                schema: "dbo",
                table: "LND_WriteOffBatchItems",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_WriteOffBatchItems_WriteOffOperationId",
                schema: "dbo",
                table: "LND_WriteOffBatchItems",
                column: "WriteOffOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_WriteOffBatchItems_Batch_Loan",
                schema: "dbo",
                table: "LND_WriteOffBatchItems",
                columns: new[] { "WriteOffBatchId", "LoanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_WriteOffBatchItems_PublicId",
                schema: "dbo",
                table: "LND_WriteOffBatchItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_InsolvencyProceedings_LND_LoanNovelties_AgreementNovelt~",
                schema: "dbo",
                table: "LND_InsolvencyProceedings",
                column: "AgreementNoveltyId",
                principalSchema: "dbo",
                principalTable: "LND_LoanNovelties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanOperations_LND_LoanNovelties_NoveltyId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "NoveltyId",
                principalSchema: "dbo",
                principalTable: "LND_LoanNovelties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanOperations_LND_Waivers_WaiverId",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "WaiverId",
                principalSchema: "dbo",
                principalTable: "LND_Waivers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanOperations_LND_WriteOffBatchItems_WriteOffBatchItem~",
                schema: "dbo",
                table: "LND_LoanOperations",
                column: "WriteOffBatchItemId",
                principalSchema: "dbo",
                principalTable: "LND_WriteOffBatchItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_LoanTermsVersions_LND_LoanNovelties_NoveltyId",
                schema: "dbo",
                table: "LND_LoanTermsVersions",
                column: "NoveltyId",
                principalSchema: "dbo",
                principalTable: "LND_LoanNovelties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LND_InsolvencyProceedings_LND_LoanNovelties_AgreementNovelt~",
                schema: "dbo",
                table: "LND_InsolvencyProceedings");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanOperations_LND_LoanNovelties_NoveltyId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanOperations_LND_Waivers_WaiverId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanOperations_LND_WriteOffBatchItems_WriteOffBatchItem~",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanTermsVersions_LND_LoanNovelties_NoveltyId",
                schema: "dbo",
                table: "LND_LoanTermsVersions");

            migrationBuilder.DropTable(
                name: "LND_LoanNoveltyConsents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanNoveltyLoans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PortfolioEvaluationLoans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_WaiverLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_WriteOffBatchItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanNovelties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PortfolioEvaluations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Waivers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_WriteOffBatches",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanTermsVersions_NoveltyId",
                schema: "dbo",
                table: "LND_LoanTermsVersions");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanOperations_NoveltyId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanOperations_WaiverId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropIndex(
                name: "IX_LND_LoanOperations_WriteOffBatchItemId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropIndex(
                name: "IX_LND_InsolvencyProceedings_AgreementNoveltyId",
                schema: "dbo",
                table: "LND_InsolvencyProceedings");

            migrationBuilder.DropColumn(
                name: "NoveltyId",
                schema: "dbo",
                table: "LND_LoanTermsVersions");

            migrationBuilder.DropColumn(
                name: "NoveltyId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropColumn(
                name: "WaiverId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropColumn(
                name: "WriteOffBatchItemId",
                schema: "dbo",
                table: "LND_LoanOperations");

            migrationBuilder.DropColumn(
                name: "AgreementNoveltyId",
                schema: "dbo",
                table: "LND_InsolvencyProceedings");
        }
    }
}
