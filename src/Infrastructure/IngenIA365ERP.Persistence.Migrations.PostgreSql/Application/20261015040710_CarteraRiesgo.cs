using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CarteraRiesgo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LND_AnalysisRatings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Subcategory = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SetByUserId = table.Column<int>(type: "integer", nullable: false),
                    SetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LiftedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LiftedByUserId = table.Column<int>(type: "integer", nullable: true),
                    LiftReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LiftedEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_AnalysisRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AnalysisRatings_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AnalysisRatings_SEC_Users_LiftedByUserId",
                        column: x => x.LiftedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AnalysisRatings_SEC_Users_SetByUserId",
                        column: x => x.SetByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanArrearsHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "smallint", nullable: false),
                    MaxDaysPastDue = table.Column<int>(type: "integer", nullable: false),
                    DaysPastDueAtMonthEnd = table.Column<int>(type: "integer", nullable: true),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: true),
                    ArrearsHistoryLoadId = table.Column<int>(type: "integer", nullable: true),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_LoanArrearsHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanArrearsHistory_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanArrearsHistory_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_LoanArrearsHistory_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_RiskPolicyVersions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Overrides = table.Column<string>(type: "text", nullable: false),
                    OverridesSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BaseDocumentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseDocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidatedByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_RiskPolicyVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_RiskPolicyVersions_LND_SessionItems_SessionItemId",
                        column: x => x.SessionItemId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RiskPolicyVersions_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_RiskPolicyVersions_SEC_Users_ValidatedByUserId",
                        column: x => x.ValidatedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MonthlyCloses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "smallint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CloseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    ActivationId = table.Column<int>(type: "integer", nullable: true),
                    PreviousCloseId = table.Column<int>(type: "integer", nullable: true),
                    SupersedesCloseId = table.Column<int>(type: "integer", nullable: true),
                    InformationCutoffAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NationalValuesWatermark = table.Column<long>(type: "bigint", nullable: false),
                    DocumentsUsedJson = table.Column<string>(type: "text", nullable: false),
                    RiskPolicyVersionId = table.Column<int>(type: "integer", nullable: true),
                    ModelBySegmentJson = table.Column<string>(type: "text", nullable: false),
                    ObligatoryModelUnavailable = table.Column<bool>(type: "boolean", nullable: false),
                    UnvalidatedParameters = table.Column<bool>(type: "boolean", nullable: false),
                    UnvalidatedParametersJson = table.Column<string>(type: "text", nullable: true),
                    IncompleteHistory = table.Column<bool>(type: "boolean", nullable: false),
                    IncompleteHistoryJson = table.Column<string>(type: "text", nullable: true),
                    LoanCount = table.Column<int>(type: "integer", nullable: false),
                    TotalPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIndividualImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalGeneralImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalsJson = table.Column<string>(type: "text", nullable: false),
                    LinesSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderedByUserId = table.Column<int>(type: "integer", nullable: true),
                    OrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_LND_MonthlyCloses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloses_LND_Activations_ActivationId",
                        column: x => x.ActivationId,
                        principalSchema: "dbo",
                        principalTable: "LND_Activations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloses_LND_MonthlyCloses_PreviousCloseId",
                        column: x => x.PreviousCloseId,
                        principalSchema: "dbo",
                        principalTable: "LND_MonthlyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloses_LND_MonthlyCloses_SupersedesCloseId",
                        column: x => x.SupersedesCloseId,
                        principalSchema: "dbo",
                        principalTable: "LND_MonthlyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloses_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloses_LND_RiskPolicyVersions_RiskPolicyVersionId",
                        column: x => x.RiskPolicyVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_RiskPolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloses_SEC_Users_OrderedByUserId",
                        column: x => x.OrderedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MonthlyCloseGeneralImpairments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MonthlyCloseId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    ModalityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubsegmentCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsEmployee = table.Column<bool>(type: "boolean", nullable: false),
                    ImpairmentModel = table.Column<int>(type: "integer", nullable: false),
                    GrossPortfolio = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AdditionalPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdditionalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CompensationOfIndividual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Delta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_LND_MonthlyCloseGeneralImpairments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseGeneralImpairments_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseGeneralImpairments_LND_MonthlyCloses_Monthl~",
                        column: x => x.MonthlyCloseId,
                        principalSchema: "dbo",
                        principalTable: "LND_MonthlyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MonthlyCloseLoans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    BatchNumber = table.Column<int>(type: "integer", nullable: true),
                    MonthlyCloseId = table.Column<int>(type: "integer", nullable: true),
                    LoanId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    OfficePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    ModalityCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SubsegmentCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SarcSegmentCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExpectedLossSegmentCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ImpairmentModel = table.Column<int>(type: "integer", nullable: false),
                    ModelBasis = table.Column<int>(type: "integer", nullable: false),
                    IsEmployee = table.Column<bool>(type: "boolean", nullable: false),
                    IsRestructured = table.Column<bool>(type: "boolean", nullable: false),
                    RestructuringCount = table.Column<int>(type: "integer", nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    IsWrittenOff = table.Column<bool>(type: "boolean", nullable: false),
                    DaysPastDueAtClose = table.Column<int>(type: "integer", nullable: false),
                    MaxDaysPastDueInMonth = table.Column<int>(type: "integer", nullable: false),
                    MonthsInE = table.Column<int>(type: "integer", nullable: true),
                    ModelRating = table.Column<int>(type: "integer", nullable: true),
                    ArrearsRating = table.Column<int>(type: "integer", nullable: false),
                    FinalRating = table.Column<int>(type: "integer", nullable: false),
                    FinalSubcategory = table.Column<int>(type: "integer", nullable: false),
                    AccountingRating = table.Column<int>(type: "integer", nullable: false),
                    RatingCause = table.Column<int>(type: "integer", nullable: false),
                    CandidatesJson = table.Column<string>(type: "text", nullable: false),
                    PreviousAccountingRating = table.Column<int>(type: "integer", nullable: true),
                    PrincipalBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentInterestBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultInterestBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SuspendedInterestBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalancesByConceptJson = table.Column<string>(type: "text", nullable: false),
                    CollateralAdmissibleValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ContributionsDeducted = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImpairmentBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImpairmentPercentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    PrincipalImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousTotalImpairment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImpairmentDelta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProbabilityOfDefault = table.Column<decimal>(type: "numeric(28,18)", precision: 28, scale: 18, nullable: true),
                    LossGivenDefault = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ExposureAtDefault = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExplanationJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_LND_MonthlyCloseLoans", x => x.Id);
                    table.CheckConstraint("CK_LND_MonthlyCloseLoans_Link", "(\"ProcessRunId\" IS NOT NULL AND \"MonthlyCloseId\" IS NULL) OR (\"ProcessRunId\" IS NULL AND \"MonthlyCloseId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseLoans_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseLoans_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseLoans_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseLoans_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseLoans_LND_MonthlyCloses_MonthlyCloseId",
                        column: x => x.MonthlyCloseId,
                        principalSchema: "dbo",
                        principalTable: "LND_MonthlyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseLoans_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MonthlyCloseReopenings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MonthlyCloseId = table.Column<int>(type: "integer", nullable: false),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReopenedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ReopenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccountingPeriodStatusJson = table.Column<string>(type: "text", nullable: false),
                    ReversalPartCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_MonthlyCloseReopenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseReopenings_LND_MonthlyCloses_MonthlyCloseId",
                        column: x => x.MonthlyCloseId,
                        principalSchema: "dbo",
                        principalTable: "LND_MonthlyCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseReopenings_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MonthlyCloseReopenings_SEC_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AnalysisRatings_LiftedByUserId",
                schema: "dbo",
                table: "LND_AnalysisRatings",
                column: "LiftedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AnalysisRatings_SetByUserId",
                schema: "dbo",
                table: "LND_AnalysisRatings",
                column: "SetByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AnalysisRatings_Loan_Active",
                schema: "dbo",
                table: "LND_AnalysisRatings",
                column: "LoanId",
                unique: true,
                filter: "\"LiftedAt\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AnalysisRatings_PublicId",
                schema: "dbo",
                table: "LND_AnalysisRatings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanArrearsHistory_Loan_Period",
                schema: "dbo",
                table: "LND_LoanArrearsHistory",
                columns: new[] { "LoanId", "PeriodYear", "PeriodMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanArrearsHistory_MigrationLoadId",
                schema: "dbo",
                table: "LND_LoanArrearsHistory",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanArrearsHistory_PublicId",
                schema: "dbo",
                table: "LND_LoanArrearsHistory",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanArrearsHistory_Run_Loan",
                schema: "dbo",
                table: "LND_LoanArrearsHistory",
                columns: new[] { "ProcessRunId", "LoanId" },
                unique: true,
                filter: "\"ProcessRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloseGeneralImpairments_BranchId",
                schema: "dbo",
                table: "LND_MonthlyCloseGeneralImpairments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloseGeneralImpairments_Dimensions",
                schema: "dbo",
                table: "LND_MonthlyCloseGeneralImpairments",
                columns: new[] { "MonthlyCloseId", "BranchId", "ModalityCode", "SubsegmentCode", "IsEmployee", "ImpairmentModel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloseGeneralImpairments_PublicId",
                schema: "dbo",
                table: "LND_MonthlyCloseGeneralImpairments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloseLoans_BranchId",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloseLoans_CreditLineId",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloseLoans_LoanId",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloseLoans_PersonId",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloseLoans_Close_Loan",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                columns: new[] { "MonthlyCloseId", "LoanId" },
                unique: true,
                filter: "\"MonthlyCloseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloseLoans_PublicId",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloseLoans_Run_Loan",
                schema: "dbo",
                table: "LND_MonthlyCloseLoans",
                columns: new[] { "ProcessRunId", "LoanId" },
                unique: true,
                filter: "\"ProcessRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloseReopenings_ProcessRunId",
                schema: "dbo",
                table: "LND_MonthlyCloseReopenings",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloseReopenings_ReopenedByUserId",
                schema: "dbo",
                table: "LND_MonthlyCloseReopenings",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloseReopenings_MonthlyCloseId",
                schema: "dbo",
                table: "LND_MonthlyCloseReopenings",
                column: "MonthlyCloseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloseReopenings_PublicId",
                schema: "dbo",
                table: "LND_MonthlyCloseReopenings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloses_ActivationId",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "ActivationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloses_CloseDate",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "CloseDate");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloses_OrderedByUserId",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "OrderedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloses_PreviousCloseId",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "PreviousCloseId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloses_ProcessRunId",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloses_RiskPolicyVersionId",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "RiskPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MonthlyCloses_SupersedesCloseId",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "SupersedesCloseId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloses_Base",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "Kind",
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloses_Period",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                columns: new[] { "Kind", "PeriodYear", "PeriodMonth", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MonthlyCloses_PublicId",
                schema: "dbo",
                table: "LND_MonthlyCloses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_RiskPolicyVersions_RegisteredByUserId",
                schema: "dbo",
                table: "LND_RiskPolicyVersions",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RiskPolicyVersions_SessionItemId",
                schema: "dbo",
                table: "LND_RiskPolicyVersions",
                column: "SessionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RiskPolicyVersions_ValidatedByUserId",
                schema: "dbo",
                table: "LND_RiskPolicyVersions",
                column: "ValidatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_RiskPolicyVersions_ValidFrom_VersionNumber",
                schema: "dbo",
                table: "LND_RiskPolicyVersions",
                columns: new[] { "ValidFrom", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_RiskPolicyVersions_PublicId",
                schema: "dbo",
                table: "LND_RiskPolicyVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_RiskPolicyVersions_VersionNumber",
                schema: "dbo",
                table: "LND_RiskPolicyVersions",
                column: "VersionNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountingFactParts_LND_MonthlyCloseReopenings_MonthlyC~",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "MonthlyCloseReopeningId",
                principalSchema: "dbo",
                principalTable: "LND_MonthlyCloseReopenings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountingFactParts_LND_MonthlyCloses_MonthlyCloseId",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "MonthlyCloseId",
                principalSchema: "dbo",
                principalTable: "LND_MonthlyCloses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LND_AccountingFactParts_LND_MonthlyCloseReopenings_MonthlyC~",
                schema: "dbo",
                table: "LND_AccountingFactParts");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AccountingFactParts_LND_MonthlyCloses_MonthlyCloseId",
                schema: "dbo",
                table: "LND_AccountingFactParts");

            migrationBuilder.DropTable(
                name: "LND_AnalysisRatings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanArrearsHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MonthlyCloseGeneralImpairments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MonthlyCloseLoans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MonthlyCloseReopenings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MonthlyCloses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_RiskPolicyVersions",
                schema: "dbo");
        }
    }
}
