using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CaptacionesAportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Sent",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles");

            migrationBuilder.DropIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Version",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles");

            migrationBuilder.DropIndex(
                name: "UK_LND_MigrationLoads_Activated",
                schema: "dbo",
                table: "LND_MigrationLoads");

            migrationBuilder.DropIndex(
                name: "UK_LND_MigrationLoads_Applied",
                schema: "dbo",
                table: "LND_MigrationLoads");

            migrationBuilder.DropIndex(
                name: "UK_LND_MigrationLoads_Draft",
                schema: "dbo",
                table: "LND_MigrationLoads");

            migrationBuilder.DropIndex(
                name: "UK_LND_Activations_MigrationLoadId",
                schema: "dbo",
                table: "LND_Activations");

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                schema: "dbo",
                table: "PAY_Novelties",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountPublicId",
                schema: "dbo",
                table: "PAY_Novelties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContributionsActivatedAt",
                schema: "dbo",
                table: "LND_Setup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ContributionsActivationDate",
                schema: "dbo",
                table: "LND_Setup",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContributionsActivationId",
                schema: "dbo",
                table: "LND_Setup",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepositsActivatedAt",
                schema: "dbo",
                table: "LND_Setup",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DepositsActivationDate",
                schema: "dbo",
                table: "LND_Setup",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepositsActivationId",
                schema: "dbo",
                table: "LND_Setup",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberAccountId",
                schema: "dbo",
                table: "LND_ProcessRunErrors",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileGroupCode",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FileGroupId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AllocationPriority",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymasterConceptCode",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ResultFormatId",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "OutboundFormatId",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeIdentification",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "EmployeeIdAtPaymaster");

            migrationBuilder.AddColumn<bool>(
                name: "IncludesAccountInstallments",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UsesFileGroups",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AccountCount",
                schema: "dbo",
                table: "LND_MigrationLoads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Nature",
                schema: "dbo",
                table: "LND_MigrationLoads",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "CausingAccountOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Nature",
                schema: "dbo",
                table: "LND_CreditLines",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxNegotiatedSpread",
                schema: "dbo",
                table: "LND_AttributionRules",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SessionItemId",
                schema: "dbo",
                table: "LND_Activations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "MigrationLoadId",
                schema: "dbo",
                table: "LND_Activations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Nature",
                schema: "dbo",
                table: "LND_Activations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "AccountCount",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TriggerAccountOperationId",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DepositInsuranceEnrolled",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DepositsFromAssociatesOnly",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGmfWithholdingAgent",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LiquidityFundCategory",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MayTakeDeposits",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AccountFlag",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContributionClass",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepositClass",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssueTermBand",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LineNature",
                schema: "dbo",
                table: "ACC_LendingPostingRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "COR_AssociateInboundMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessagePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MessageVersion = table.Column<short>(type: "smallint", nullable: false),
                    AssociateId = table.Column<int>(type: "integer", nullable: false),
                    WithdrawalPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    WithdrawalNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Cause = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AlreadyWithdrawn = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_COR_AssociateInboundMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COR_AssociateInboundMessages_COR_Associates_AssociateId",
                        column: x => x.AssociateId,
                        principalSchema: "dbo",
                        principalTable: "COR_Associates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_ContributionLineVersions",
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
                    MinimumMode = table.Column<int>(type: "integer", nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinimumFraction = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    InstallmentMode = table.Column<int>(type: "integer", nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InstallmentFraction = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    InstallmentRoundingUnit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentRoundingMode = table.Column<int>(type: "integer", nullable: false),
                    InstallmentPeriodicity = table.Column<int>(type: "integer", nullable: false),
                    InstallmentDueDay = table.Column<byte>(type: "smallint", nullable: true),
                    MinimumFirst = table.Column<bool>(type: "boolean", nullable: false),
                    PermanentSavingsLineId = table.Column<int>(type: "integer", nullable: true),
                    PermanentSavingsFraction = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    AllowsExtraordinary = table.Column<bool>(type: "boolean", nullable: false),
                    MaxParticipationFraction = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ExcessAction = table.Column<int>(type: "integer", nullable: false),
                    AllowsTransferBetweenMembers = table.Column<bool>(type: "boolean", nullable: false),
                    RefundTermDays = table.Column<int>(type: "integer", nullable: false),
                    LossDeductionEnabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LND_ContributionLineVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ContributionLineVersions_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ContributionLineVersions_PermanentSavingsLine",
                        column: x => x.PermanentSavingsLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DepositLineVersions",
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
                    DepositClass = table.Column<int>(type: "integer", nullable: false),
                    RequiresAssociate = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsCoHolders = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsMinors = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsLegalEntities = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsPartialWithdrawals = table.Column<bool>(type: "boolean", nullable: false),
                    BranchPublicIdsJson = table.Column<string>(type: "text", nullable: true),
                    RateKind = table.Column<int>(type: "integer", nullable: false),
                    AnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    IndexCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IndexKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Margin = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RateFloor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    RateCeiling = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ResetPeriodicity = table.Column<int>(type: "integer", nullable: true),
                    RateTierMode = table.Column<int>(type: "integer", nullable: true),
                    DeclaresAgreedRate = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsNegotiatedRate = table.Column<bool>(type: "boolean", nullable: false),
                    KeepsNegotiatedMarginOnRollover = table.Column<bool>(type: "boolean", nullable: false),
                    RateChangeNoticeDays = table.Column<int>(type: "integer", nullable: false),
                    BalanceMethod = table.Column<int>(type: "integer", nullable: false),
                    DayCount = table.Column<int>(type: "integer", nullable: false),
                    RateEquivalence = table.Column<int>(type: "integer", nullable: false),
                    RoundingUnit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoundingMode = table.Column<int>(type: "integer", nullable: false),
                    RoundingMoment = table.Column<int>(type: "integer", nullable: false),
                    MinOpeningAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinBalanceForInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SettlementPeriodicity = table.Column<int>(type: "integer", nullable: false),
                    SettlementDay = table.Column<byte>(type: "smallint", nullable: true),
                    WithholdingRateParameterCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsGmfSubject = table.Column<bool>(type: "boolean", nullable: false),
                    MaxWithdrawalPerOperation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxWithdrawalPerDay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxCashPerOperation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxCashPerDay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InactivityDays = table.Column<int>(type: "integer", nullable: true),
                    AccruesWhileInactive = table.Column<bool>(type: "boolean", nullable: false),
                    SuspendsAccrualOnTotalGarnishment = table.Column<bool>(type: "boolean", nullable: false),
                    SuspendsAccrualOnDeceased = table.Column<bool>(type: "boolean", nullable: false),
                    MinTermDays = table.Column<int>(type: "integer", nullable: true),
                    MaxTermDays = table.Column<int>(type: "integer", nullable: true),
                    InterestPeriodicities = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InterestTimings = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AutoRollover = table.Column<bool>(type: "boolean", nullable: true),
                    MaxRollovers = table.Column<int>(type: "integer", nullable: true),
                    RolloverRateRule = table.Column<int>(type: "integer", nullable: true),
                    CapitalizesOnRollover = table.Column<bool>(type: "boolean", nullable: true),
                    GraceBusinessDays = table.Column<int>(type: "integer", nullable: true),
                    GraceAccrues = table.Column<bool>(type: "boolean", nullable: true),
                    AfterMaturityTreatment = table.Column<int>(type: "integer", nullable: true),
                    MoveToDemandLineId = table.Column<int>(type: "integer", nullable: true),
                    EarlyRedemptionAllowed = table.Column<bool>(type: "boolean", nullable: true),
                    EarlyRedemptionPenaltyMode = table.Column<int>(type: "integer", nullable: true),
                    PenaltyAnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    PenaltyDiscountFraction = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    MinDaysBeforeRedemption = table.Column<int>(type: "integer", nullable: true),
                    InstallmentPeriodicity = table.Column<int>(type: "integer", nullable: true),
                    MinInstallmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinTermMonths = table.Column<int>(type: "integer", nullable: true),
                    MaxTermMonths = table.Column<int>(type: "integer", nullable: true),
                    BonusAnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    BreachAnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ToleratedLateInstallments = table.Column<int>(type: "integer", nullable: true),
                    BreachEffect = table.Column<int>(type: "integer", nullable: true),
                    BonusRecognition = table.Column<int>(type: "integer", nullable: true),
                    WithdrawableOnlyOnMembershipWithdrawal = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("PK_LND_DepositLineVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DepositLineVersions_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DepositLineVersions_LND_CreditLines_MoveToDemandLineId",
                        column: x => x.MoveToDemandLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MemberAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    Nature = table.Column<int>(type: "integer", nullable: false),
                    DepositClass = table.Column<int>(type: "integer", nullable: true),
                    HolderPersonId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    HandlingMode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    OpenedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    InactiveSince = table.Column<DateOnly>(type: "date", nullable: true),
                    MaturedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosingRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsGarnished = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresDataUpdate = table.Column<bool>(type: "boolean", nullable: false),
                    DataUpdateRequiredSince = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractualStatus = table.Column<int>(type: "integer", nullable: true),
                    TechnicalNegativeSince = table.Column<DateOnly>(type: "date", nullable: true),
                    AccrualSuspendedSince = table.Column<DateOnly>(type: "date", nullable: true),
                    AccrualSuspensionReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SourceAccountNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_LND_MemberAccounts", x => x.Id);
                    table.CheckConstraint("CK_LND_MemberAccounts_Class", "(\"Nature\" = 2 AND \"DepositClass\" IS NOT NULL) OR (\"Nature\" = 3 AND \"DepositClass\" IS NULL)");
                    table.CheckConstraint("CK_LND_MemberAccounts_Nature", "\"Nature\" <> 1");
                    table.ForeignKey(
                        name: "FK_LND_MemberAccounts_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccounts_COR_People_HolderPersonId",
                        column: x => x.HolderPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccounts_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccounts_LND_MigrationLoads_MigrationLoadId",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccounts_SEC_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccounts_SEC_Users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollAgreementFileGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollAgreementVersionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OutboundFormatId = table.Column<int>(type: "integer", nullable: false),
                    ResultFormatId = table.Column<int>(type: "integer", nullable: false),
                    CutoffDay = table.Column<byte>(type: "smallint", nullable: true),
                    PaymentDay = table.Column<byte>(type: "smallint", nullable: true),
                    SendLeadDays = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollAgreementFileGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementFileGroups_COR_BankFileFormats_Outbound~",
                        column: x => x.OutboundFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementFileGroups_COR_BankFileFormats_ResultFo~",
                        column: x => x.ResultFormatId,
                        principalSchema: "dbo",
                        principalTable: "COR_BankFileFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementFileGroups_LND_PayrollAgreementVersions~",
                        column: x => x.PayrollAgreementVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreementVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionUnmatchedLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollDeductionFileId = table.Column<int>(type: "integer", nullable: false),
                    InboundMessageId = table.Column<int>(type: "integer", nullable: false),
                    PayerEmployeeId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymasterConceptCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PaymasterResultCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DeductionDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollDeductionUnmatchedLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionUnmatchedLines_LND_InboundMessages_Inbo~",
                        column: x => x.InboundMessageId,
                        principalSchema: "dbo",
                        principalTable: "LND_InboundMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionUnmatchedLines_LND_PayrollDeductionFile~",
                        column: x => x.PayrollDeductionFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PayrollAccountDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollRunId = table.Column<int>(type: "integer", nullable: false),
                    PayrollRunEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    PayrollRunLineId = table.Column<int>(type: "integer", nullable: true),
                    AccountPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConceptCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NotDeductedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NotDeductedReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AppliedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CarteraReceiptPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CarteraReversalPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ResultMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_PayrollAccountDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollAccountDeductions_PAY_PayrollRunEmployees_Payrol~",
                        column: x => x.PayrollRunEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRunEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollAccountDeductions_PAY_PayrollRunLines_PayrollRun~",
                        column: x => x.PayrollRunLineId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRunLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollAccountDeductions_PAY_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DepositLineCharges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepositLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: true),
                    FreeOperationsPerPeriod = table.Column<int>(type: "integer", nullable: true),
                    FreePeriod = table.Column<int>(type: "integer", nullable: true),
                    CalculationMode = table.Column<int>(type: "integer", nullable: false),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Percentage = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    PercentageBase = table.Column<int>(type: "integer", nullable: true),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExemptIfAverageBalanceAtLeast = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExemptIfMinimumBalanceAtLeast = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExemptIfSeniorityMonthsAtLeast = table.Column<int>(type: "integer", nullable: true),
                    ExemptEmployees = table.Column<bool>(type: "boolean", nullable: false),
                    ExemptGmfExemptAccount = table.Column<bool>(type: "boolean", nullable: false),
                    ExemptMinors = table.Column<bool>(type: "boolean", nullable: false),
                    ExemptBranchPublicIdsJson = table.Column<string>(type: "text", nullable: true),
                    ShortfallPolicy = table.Column<int>(type: "integer", nullable: false),
                    IsVatTaxed = table.Column<bool>(type: "boolean", nullable: false),
                    CommercialItemCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_LND_DepositLineCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DepositLineCharges_Version",
                        column: x => x.DepositLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_DepositLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DepositLineRateTiers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepositLineVersionId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    MinBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinTermDays = table.Column<int>(type: "integer", nullable: true),
                    MaxTermDays = table.Column<int>(type: "integer", nullable: true),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_LND_DepositLineRateTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DepositLineRateTiers_Version",
                        column: x => x.DepositLineVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_DepositLineVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccountChargeExemptions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    ChargeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_LND_AccountChargeExemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AccountChargeExemptions_LND_CreditDecisions_DecisionId",
                        column: x => x.DecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountChargeExemptions_LND_MemberAccounts_MemberAccoun~",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountChargeExemptions_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_DirectDebitMandates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    AuthorizedByPersonId = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    AllowsPartialDebit = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorizationAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_LND_DirectDebitMandates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DirectDebitMandates_COR_People_AuthorizedByPersonId",
                        column: x => x.AuthorizedByPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DirectDebitMandates_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DirectDebitMandates_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_DirectDebitMandates_SEC_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_GmfExemptAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    MarkedByUserId = table.Column<int>(type: "integer", nullable: true),
                    MarkReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
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
                    table.PrimaryKey("PK_LND_GmfExemptAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_GmfExemptAccounts_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_GmfExemptAccounts_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_GmfExemptAccounts_SEC_Users_EndedByUserId",
                        column: x => x.EndedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_GmfExemptAccounts_SEC_Users_MarkedByUserId",
                        column: x => x.MarkedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MemberAccountControls",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    LastAccruedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastSettledThrough = table.Column<DateOnly>(type: "date", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    LastRegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastPositionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastHolderActivityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastInstallmentPeriod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
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
                    table.PrimaryKey("PK_LND_MemberAccountControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountControls_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MemberAccountDailyPositions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    PositionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    CreditLineId = table.Column<int>(type: "integer", nullable: false),
                    Nature = table.Column<int>(type: "integer", nullable: false),
                    DepositClass = table.Column<int>(type: "integer", nullable: true),
                    IssueTermBand = table.Column<int>(type: "integer", nullable: true),
                    Principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ContributionMinimum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ContributionOrdinary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ContributionExtraordinary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ContributionRevaluation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Bonus = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChargeReceivable = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestPayable = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    HeldAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GarnishedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ClearingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingYearToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GmfMonthToDate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAnnualRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    DaysToMaturity = table.Column<int>(type: "integer", nullable: true),
                    InstallmentsInArrears = table.Column<int>(type: "integer", nullable: false),
                    ArrearsAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsInactive = table.Column<bool>(type: "boolean", nullable: false),
                    IsGarnished = table.Column<bool>(type: "boolean", nullable: false),
                    IsGmfExempt = table.Column<bool>(type: "boolean", nullable: false),
                    IsAccrualSuspended = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LND_MemberAccountDailyPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountDailyPositions_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountDailyPositions_LND_CreditLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountDailyPositions_LND_MemberAccounts_MemberAc~",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MemberAccountParties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Share = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    SignatureLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RepresentationBasis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RepresentationSupportAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignatureSpecimenAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhotoAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeneficiarySetNumber = table.Column<int>(type: "integer", nullable: true),
                    BeneficiaryRecordAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    EndedByUserId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MemberAccountParties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountParties_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountParties_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountParties_SEC_Users_EndedByUserId",
                        column: x => x.EndedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollAgreementConceptEquivalences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollAgreementVersionId = table.Column<int>(type: "integer", nullable: false),
                    FileGroupId = table.Column<int>(type: "integer", nullable: true),
                    PaymasterConceptCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AllocationRule = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_LND_PayrollAgreementConceptEquivalences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementConceptEquivalences_LND_PayrollAgreemen~",
                        column: x => x.FileGroupId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreementFileGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollAgreementConceptEquivalences_LND_PayrollAgreeme~1",
                        column: x => x.PayrollAgreementVersionId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollAgreementVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccountCharges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    DepositLineChargeId = table.Column<int>(type: "integer", nullable: false),
                    ChargeCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: true),
                    TriggerOperationId = table.Column<long>(type: "bigint", nullable: true),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AppliedOperationId = table.Column<long>(type: "bigint", nullable: false),
                    CollectedOperationId = table.Column<long>(type: "bigint", nullable: true),
                    WaivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaivedByUserId = table.Column<int>(type: "integer", nullable: true),
                    WaiverReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WaiverDecisionId = table.Column<int>(type: "integer", nullable: true),
                    ReversedByOperationId = table.Column<long>(type: "bigint", nullable: true),
                    IsVatTaxed = table.Column<bool>(type: "boolean", nullable: false),
                    ChargeMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommercialInvoiceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CommercialInvoiceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_LND_AccountCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AccountCharges_LND_CreditDecisions_WaiverDecisionId",
                        column: x => x.WaiverDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountCharges_LND_DepositLineCharges_DepositLineCharge~",
                        column: x => x.DepositLineChargeId,
                        principalSchema: "dbo",
                        principalTable: "LND_DepositLineCharges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountCharges_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountCharges_SEC_Users_WaivedByUserId",
                        column: x => x.WaivedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccountInstallmentNonDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstallmentId = table.Column<long>(type: "bigint", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectorCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReferenceKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_LND_AccountInstallmentNonDeductions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccountInstallments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    PeriodYear = table.Column<short>(type: "smallint", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "smallint", nullable: false),
                    SubPeriodNumber = table.Column<byte>(type: "smallint", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ContributionShare = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PermanentSavingsShare = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LinkedInstallmentId = table.Column<long>(type: "bigint", nullable: true),
                    GeneratedByProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    GeneratedByOperationId = table.Column<long>(type: "bigint", nullable: true),
                    RulesVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InputsSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CancelledByOperationId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_LND_AccountInstallments", x => x.Id);
                    table.CheckConstraint("CK_LND_AccountInstallments_Amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_LND_AccountInstallments_Origin", "(\"GeneratedByProcessRunId\" IS NOT NULL AND \"GeneratedByOperationId\" IS NULL) OR (\"GeneratedByProcessRunId\" IS NULL AND \"GeneratedByOperationId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_AccountInstallments_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountInstallments_LND_ProcessRuns_GeneratedByProcessR~",
                        column: x => x.GeneratedByProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountInstallments_Linked",
                        column: x => x.LinkedInstallmentId,
                        principalSchema: "dbo",
                        principalTable: "LND_AccountInstallments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionFileAccountLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollDeductionFileId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    AccountInstallmentId = table.Column<long>(type: "bigint", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeIdAtPaymaster = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PaymasterConceptCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AllocationPriority = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CurrentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PastDueAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NoveltyKind = table.Column<int>(type: "integer", nullable: false),
                    PreviousAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollDeductionFileAccountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileAccountLines_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileAccountLines_LND_AccountInstallment~",
                        column: x => x.AccountInstallmentId,
                        principalSchema: "dbo",
                        principalTable: "LND_AccountInstallments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileAccountLines_LND_MemberAccounts_Mem~",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionFileAccountLines_LND_PayrollDeductionFi~",
                        column: x => x.PayrollDeductionFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionAccountResults",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollDeductionFileAccountLineId = table.Column<int>(type: "integer", nullable: false),
                    PayrollDeductionFileId = table.Column<int>(type: "integer", nullable: false),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    InboundMessageId = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    DeductedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NotDeductedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymasterResultCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NonDeductionReason = table.Column<int>(type: "integer", nullable: true),
                    TransferDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SupersedesResultId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_PayrollDeductionAccountResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionAccountResults_LND_InboundMessages_Inbo~",
                        column: x => x.InboundMessageId,
                        principalSchema: "dbo",
                        principalTable: "LND_InboundMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionAccountResults_LND_MemberAccounts_Membe~",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionAccountResults_LND_PayrollDeductionAcco~",
                        column: x => x.SupersedesResultId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionAccountResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionAccountResults_LND_PayrollDeductionFile~",
                        column: x => x.PayrollDeductionFileAccountLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFileAccountLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionAccountResults_LND_PayrollDeductionFil~1",
                        column: x => x.PayrollDeductionFileId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccountPayoutOrders",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryPersonId = table.Column<int>(type: "integer", nullable: false),
                    BeneficiaryTaxIdType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BeneficiaryTaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BeneficiaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DestinationBankId = table.Column<int>(type: "integer", nullable: true),
                    DestinationAccountType = table.Column<byte>(type: "smallint", nullable: true),
                    DestinationAccountNumber = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResultDueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    MembershipWithdrawalId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_AccountPayoutOrders", x => x.Id);
                    table.CheckConstraint("CK_LND_AccountPayoutOrders_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_LND_AccountPayoutOrders_COR_Banks_DestinationBankId",
                        column: x => x.DestinationBankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountPayoutOrders_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountPayoutOrders_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AccountPayoutOrders_SEC_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_ContributionRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    TargetPersonId = table.Column<int>(type: "integer", nullable: true),
                    TargetMemberAccountId = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundCause = table.Column<int>(type: "integer", nullable: true),
                    SessionItemPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    PayoutOrderId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_ContributionRequests", x => x.Id);
                    table.CheckConstraint("CK_LND_ContributionRequests_Amount", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_LND_ContributionRequests_COR_People_TargetPersonId",
                        column: x => x.TargetPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ContributionRequests_LND_AccountPayoutOrders_PayoutOrde~",
                        column: x => x.PayoutOrderId,
                        principalSchema: "dbo",
                        principalTable: "LND_AccountPayoutOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ContributionRequests_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ContributionRequests_SEC_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ContributionRequests_SEC_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_ContributionRequests_TargetAccount",
                        column: x => x.TargetMemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_GmfMonthlyUsage",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "smallint", nullable: false),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    TaxableEventCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DisposedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExemptAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxableBase = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GmfAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Policy = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    ReversesUsageId = table.Column<long>(type: "bigint", nullable: true),
                    Origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_LND_GmfMonthlyUsage", x => x.Id);
                    table.CheckConstraint("CK_LND_GmfMonthlyUsage_Month", "\"Month\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_LND_GmfMonthlyUsage_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_GmfMonthlyUsage_LND_GmfMonthlyUsage_ReversesUsageId",
                        column: x => x.ReversesUsageId,
                        principalSchema: "dbo",
                        principalTable: "LND_GmfMonthlyUsage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_GmfMonthlyUsage_LND_MemberAccounts_MemberAccountId",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_IssuedDocumentAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IssuedDocumentId = table.Column<int>(type: "integer", nullable: false),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    LastOperationId = table.Column<long>(type: "bigint", nullable: true),
                    LastTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    BalanceAtCutoff = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PreviousIssuedDocumentId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_IssuedDocumentAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocumentAccounts_LND_IssuedDocuments_IssuedDocume~",
                        column: x => x.IssuedDocumentId,
                        principalSchema: "dbo",
                        principalTable: "LND_IssuedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocumentAccounts_LND_MemberAccounts_MemberAccount~",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_IssuedDocumentAccounts_PreviousDocument",
                        column: x => x.PreviousIssuedDocumentId,
                        principalSchema: "dbo",
                        principalTable: "LND_IssuedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MemberAccountOperations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    CounterpartMemberAccountId = table.Column<int>(type: "integer", nullable: true),
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
                    ContractVersion = table.Column<short>(type: "smallint", nullable: true),
                    PaymentMeansClass = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceiptBranchPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptMeansReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ClearingUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    ClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CashOriginDeclarationAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorizedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AuthorizationDecisionId = table.Column<int>(type: "integer", nullable: true),
                    SignedByPersonIdsJson = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    RelatedLoanOperationId = table.Column<long>(type: "bigint", nullable: true),
                    MembershipWithdrawalId = table.Column<int>(type: "integer", nullable: true),
                    InboundMessageId = table.Column<int>(type: "integer", nullable: true),
                    PayrollDeductionAccountResultId = table.Column<int>(type: "integer", nullable: true),
                    MigrationLoadId = table.Column<int>(type: "integer", nullable: true),
                    ReversesOperationId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByOperationId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_LND_MemberAccountOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_LND_CreditDecisions_Authorizati~",
                        column: x => x.AuthorizationDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_LND_InboundMessages_InboundMess~",
                        column: x => x.InboundMessageId,
                        principalSchema: "dbo",
                        principalTable: "LND_InboundMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_LND_LoanOperations_RelatedLoanO~",
                        column: x => x.RelatedLoanOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_LND_MemberAccounts_CounterpartM~",
                        column: x => x.CounterpartMemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_LND_MemberAccounts_MemberAccoun~",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_LND_MigrationLoads_MigrationLoa~",
                        column: x => x.MigrationLoadId,
                        principalSchema: "dbo",
                        principalTable: "LND_MigrationLoads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_PayrollDeductionAccountResult",
                        column: x => x.PayrollDeductionAccountResultId,
                        principalSchema: "dbo",
                        principalTable: "LND_PayrollDeductionAccountResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_ReversedBy",
                        column: x => x.ReversedByOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccountOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_Reverses",
                        column: x => x.ReversesOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccountOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_SEC_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountOperations_SEC_Users_AuthorizedByUserId",
                        column: x => x.AuthorizedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MemberAccountTransactions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<long>(type: "bigint", nullable: true),
                    ProcessRunId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Concept = table.Column<int>(type: "integer", nullable: false),
                    InstallmentId = table.Column<long>(type: "bigint", nullable: true),
                    SchedulePeriodNumber = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AccountingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReversesTransactionId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_LND_MemberAccountTransactions", x => x.Id);
                    table.CheckConstraint("CK_LND_MemberAccountTransactions_Amount", "\"Amount\" <> 0");
                    table.CheckConstraint("CK_LND_MemberAccountTransactions_Origin", "(\"OperationId\" IS NOT NULL AND \"ProcessRunId\" IS NULL) OR (\"OperationId\" IS NULL AND \"ProcessRunId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountTransactions_Installment",
                        column: x => x.InstallmentId,
                        principalSchema: "dbo",
                        principalTable: "LND_AccountInstallments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountTransactions_LND_MemberAccountOperations_O~",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccountOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountTransactions_LND_MemberAccountTransactions~",
                        column: x => x.ReversesTransactionId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccountTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountTransactions_LND_MemberAccounts_MemberAcco~",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MemberAccountTransactions_LND_ProcessRuns_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalSchema: "dbo",
                        principalTable: "LND_ProcessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MembershipWithdrawals",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    ContributionAccountId = table.Column<int>(type: "integer", nullable: false),
                    Cause = table.Column<int>(type: "integer", nullable: false),
                    CauseDetail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupportAttachmentPublicIdsJson = table.Column<string>(type: "text", nullable: false),
                    FiledOn = table.Column<DateOnly>(type: "date", nullable: false),
                    FiledByUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalSessionItemId = table.Column<int>(type: "integer", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DesistedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DesistReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ApprovedMessagePublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementVersion = table.Column<int>(type: "integer", nullable: false),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SettledByUserId = table.Column<int>(type: "integer", nullable: true),
                    OffsetOperationId = table.Column<long>(type: "bigint", nullable: true),
                    ContributionsTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SavingsTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LoansOffsetTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LossDeductionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RefundDueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RefundHeldByInstitutionalMinimum = table.Column<bool>(type: "boolean", nullable: false),
                    RefundDecisionId = table.Column<int>(type: "integer", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SettlementIssuedDocumentId = table.Column<int>(type: "integer", nullable: true),
                    ToRedoReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PendingLoanBalanceHandling = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_LND_MembershipWithdrawals", x => x.Id);
                    table.CheckConstraint("CK_LND_MembershipWithdrawals_Refund", "\"RefundAmount\" IS NULL OR \"RefundAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_ApprovalSessionItem",
                        column: x => x.ApprovalSessionItemId,
                        principalSchema: "dbo",
                        principalTable: "LND_SessionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_ContributionAccount",
                        column: x => x.ContributionAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_OffsetOperation",
                        column: x => x.OffsetOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccountOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_RefundDecision",
                        column: x => x.RefundDecisionId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_SEC_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_SEC_Users_FiledByUserId",
                        column: x => x.FiledByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_SEC_Users_SettledByUserId",
                        column: x => x.SettledByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawals_SettlementDocument",
                        column: x => x.SettlementIssuedDocumentId,
                        principalSchema: "dbo",
                        principalTable: "LND_IssuedDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LND_MembershipWithdrawalLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MembershipWithdrawalId = table.Column<int>(type: "integer", nullable: false),
                    SettlementVersion = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    MemberAccountId = table.Column<int>(type: "integer", nullable: true),
                    LoanId = table.Column<int>(type: "integer", nullable: true),
                    Concept = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountOperationId = table.Column<long>(type: "bigint", nullable: true),
                    LoanOperationId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_LND_MembershipWithdrawalLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawalLines_Account",
                        column: x => x.MemberAccountId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawalLines_AccountOperation",
                        column: x => x.AccountOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccountOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawalLines_LND_Loans_LoanId",
                        column: x => x.LoanId,
                        principalSchema: "dbo",
                        principalTable: "LND_Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawalLines_LoanOperation",
                        column: x => x.LoanOperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_MembershipWithdrawalLines_Withdrawal",
                        column: x => x.MembershipWithdrawalId,
                        principalSchema: "dbo",
                        principalTable: "LND_MembershipWithdrawals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_AccountPublicId",
                schema: "dbo",
                table: "PAY_Novelties",
                column: "AccountPublicId",
                filter: "\"AccountPublicId\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Setup_ContributionsActivationId",
                schema: "dbo",
                table: "LND_Setup",
                column: "ContributionsActivationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Setup_DepositsActivationId",
                schema: "dbo",
                table: "LND_Setup",
                column: "DepositsActivationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ProcessRunErrors_MemberAccountId",
                schema: "dbo",
                table: "LND_ProcessRunErrors",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProcessRunErrors_Run_Account_Attempt",
                schema: "dbo",
                table: "LND_ProcessRunErrors",
                columns: new[] { "ProcessRunId", "MemberAccountId", "AttemptNumber" },
                unique: true,
                filter: "\"MemberAccountId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LND_ProcessRunErrors_Subject",
                schema: "dbo",
                table: "LND_ProcessRunErrors",
                sql: "\"LoanId\" IS NULL OR \"MemberAccountId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFiles_FileGroupId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "FileGroupId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Sent",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                columns: new[] { "PayrollAgreementId", "PeriodYear", "PeriodMonth", "SubPeriodNumber", "FileGroupCode" },
                unique: true,
                filter: "\"Status\" = 3 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Version",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                columns: new[] { "PayrollAgreementId", "PeriodYear", "PeriodMonth", "SubPeriodNumber", "FileGroupCode", "Version" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LND_PayrollAgreementVersions_Formats",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                sql: "(\"UsesFileGroups\" = FALSE AND \"OutboundFormatId\" IS NOT NULL AND \"ResultFormatId\" IS NOT NULL) OR (\"UsesFileGroups\" = TRUE AND \"OutboundFormatId\" IS NULL AND \"ResultFormatId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoads_Activated",
                schema: "dbo",
                table: "LND_MigrationLoads",
                columns: new[] { "Nature", "Status" },
                unique: true,
                filter: "\"Status\" = 4 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoads_Applied",
                schema: "dbo",
                table: "LND_MigrationLoads",
                columns: new[] { "Nature", "Status" },
                unique: true,
                filter: "\"Status\" = 2 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MigrationLoads_Draft",
                schema: "dbo",
                table: "LND_MigrationLoads",
                columns: new[] { "Nature", "Status" },
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentEvents_CausingAccountOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                column: "CausingAccountOperationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LND_IssuedDocumentEvents_Cause",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                sql: "\"CausingOperationId\" IS NULL OR \"CausingAccountOperationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLines_Nature_Status",
                schema: "dbo",
                table: "LND_CreditLines",
                columns: new[] { "Nature", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLines_ActiveContribution",
                schema: "dbo",
                table: "LND_CreditLines",
                column: "Nature",
                unique: true,
                filter: "\"Nature\" = 3 AND \"Status\" = 2 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Activations_MigrationLoadId",
                schema: "dbo",
                table: "LND_Activations",
                column: "MigrationLoadId",
                unique: true,
                filter: "\"MigrationLoadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Activations_Nature",
                schema: "dbo",
                table: "LND_Activations",
                column: "Nature",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountingFactParts_AccTrigger_Branch_Fact",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                columns: new[] { "TriggerAccountOperationId", "BranchId", "FactType" },
                unique: true,
                filter: "\"TriggerAccountOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_AssociateInboundMessages_Associate",
                schema: "dbo",
                table: "COR_AssociateInboundMessages",
                column: "AssociateId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_AssociateInboundMessages_Message",
                schema: "dbo",
                table: "COR_AssociateInboundMessages",
                column: "MessagePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_COR_AssociateInboundMessages_PublicId",
                schema: "dbo",
                table: "COR_AssociateInboundMessages",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountChargeExemptions_ApprovedByUserId",
                schema: "dbo",
                table: "LND_AccountChargeExemptions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountChargeExemptions_DecisionId",
                schema: "dbo",
                table: "LND_AccountChargeExemptions",
                column: "DecisionId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountChargeExemptions_Account_Code",
                schema: "dbo",
                table: "LND_AccountChargeExemptions",
                columns: new[] { "MemberAccountId", "ChargeCode" },
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountChargeExemptions_PublicId",
                schema: "dbo",
                table: "LND_AccountChargeExemptions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountCharges_Account_Status",
                schema: "dbo",
                table: "LND_AccountCharges",
                columns: new[] { "MemberAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountCharges_AppliedOperationId",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "AppliedOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountCharges_ChargeMessage",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "ChargeMessagePublicId",
                filter: "\"ChargeMessagePublicId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountCharges_CollectedOperationId",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "CollectedOperationId",
                filter: "\"CollectedOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountCharges_DepositLineChargeId",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "DepositLineChargeId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountCharges_ReversedByOperationId",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "ReversedByOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountCharges_WaivedByUserId",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "WaivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountCharges_WaiverDecisionId",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "WaiverDecisionId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountCharges_Account_Charge_Period",
                schema: "dbo",
                table: "LND_AccountCharges",
                columns: new[] { "MemberAccountId", "DepositLineChargeId", "PeriodFrom" },
                unique: true,
                filter: "\"PeriodFrom\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountCharges_PublicId",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountCharges_Trigger_Charge",
                schema: "dbo",
                table: "LND_AccountCharges",
                columns: new[] { "TriggerOperationId", "DepositLineChargeId" },
                unique: true,
                filter: "\"TriggerOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountInstallmentNonDeductions_Inst_Collector_Key",
                schema: "dbo",
                table: "LND_AccountInstallmentNonDeductions",
                columns: new[] { "InstallmentId", "CollectorCode", "ReferenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountInstallmentNonDeductions_PublicId",
                schema: "dbo",
                table: "LND_AccountInstallmentNonDeductions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountInstallments_Account_DueDate",
                schema: "dbo",
                table: "LND_AccountInstallments",
                columns: new[] { "MemberAccountId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountInstallments_CancelledByOperationId",
                schema: "dbo",
                table: "LND_AccountInstallments",
                column: "CancelledByOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountInstallments_DueDate",
                schema: "dbo",
                table: "LND_AccountInstallments",
                column: "DueDate",
                filter: "\"CancelledAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountInstallments_GeneratedByOperationId",
                schema: "dbo",
                table: "LND_AccountInstallments",
                column: "GeneratedByOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountInstallments_Linked",
                schema: "dbo",
                table: "LND_AccountInstallments",
                column: "LinkedInstallmentId",
                filter: "\"LinkedInstallmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountInstallments_ProcessRun",
                schema: "dbo",
                table: "LND_AccountInstallments",
                column: "GeneratedByProcessRunId",
                filter: "\"GeneratedByProcessRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountInstallments_Account_Period",
                schema: "dbo",
                table: "LND_AccountInstallments",
                columns: new[] { "MemberAccountId", "PeriodYear", "PeriodMonth", "SubPeriodNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountInstallments_PublicId",
                schema: "dbo",
                table: "LND_AccountInstallments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountPayoutOrders_Account_Status",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                columns: new[] { "MemberAccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountPayoutOrders_BeneficiaryPersonId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountPayoutOrders_DestinationBankId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "DestinationBankId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountPayoutOrders_IssuedByUserId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountPayoutOrders_MembershipWithdrawalId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "MembershipWithdrawalId",
                filter: "\"MembershipWithdrawalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccountPayoutOrders_Status_ResultDueOn",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                columns: new[] { "Status", "ResultDueOn" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountPayoutOrders_Number",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountPayoutOrders_Operation",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccountPayoutOrders_PublicId",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionLineVersions_Line_ValidFrom",
                schema: "dbo",
                table: "LND_ContributionLineVersions",
                columns: new[] { "CreditLineId", "ValidFrom", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionLineVersions_PermanentSavingsLineId",
                schema: "dbo",
                table: "LND_ContributionLineVersions",
                column: "PermanentSavingsLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContributionLineVersions_Line_Version",
                schema: "dbo",
                table: "LND_ContributionLineVersions",
                columns: new[] { "CreditLineId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContributionLineVersions_PublicId",
                schema: "dbo",
                table: "LND_ContributionLineVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionRequests_DecidedByUserId",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionRequests_MemberAccountId",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionRequests_PayoutOrderId",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "PayoutOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionRequests_RequestedByUserId",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionRequests_Status_Kind",
                schema: "dbo",
                table: "LND_ContributionRequests",
                columns: new[] { "Status", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionRequests_TargetMemberAccountId",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "TargetMemberAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ContributionRequests_TargetPersonId",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "TargetPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContributionRequests_Operation",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "OperationId",
                unique: true,
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContributionRequests_PublicId",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositLineCharges_PublicId",
                schema: "dbo",
                table: "LND_DepositLineCharges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositLineCharges_Version_Code",
                schema: "dbo",
                table: "LND_DepositLineCharges",
                columns: new[] { "DepositLineVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositLineRateTiers_PublicId",
                schema: "dbo",
                table: "LND_DepositLineRateTiers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositLineRateTiers_Version_Order",
                schema: "dbo",
                table: "LND_DepositLineRateTiers",
                columns: new[] { "DepositLineVersionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DepositLineVersions_Line_ValidFrom",
                schema: "dbo",
                table: "LND_DepositLineVersions",
                columns: new[] { "CreditLineId", "ValidFrom", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_DepositLineVersions_MoveToDemandLineId",
                schema: "dbo",
                table: "LND_DepositLineVersions",
                column: "MoveToDemandLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositLineVersions_Line_Version",
                schema: "dbo",
                table: "LND_DepositLineVersions",
                columns: new[] { "CreditLineId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositLineVersions_PublicId",
                schema: "dbo",
                table: "LND_DepositLineVersions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DirectDebitMandates_AuthorizedByPersonId",
                schema: "dbo",
                table: "LND_DirectDebitMandates",
                column: "AuthorizedByPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DirectDebitMandates_LoanId",
                schema: "dbo",
                table: "LND_DirectDebitMandates",
                column: "LoanId",
                filter: "\"LoanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DirectDebitMandates_RevokedByUserId",
                schema: "dbo",
                table: "LND_DirectDebitMandates",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DirectDebitMandates_Account_Loan",
                schema: "dbo",
                table: "LND_DirectDebitMandates",
                columns: new[] { "MemberAccountId", "LoanId" },
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"LoanId\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DirectDebitMandates_PublicId",
                schema: "dbo",
                table: "LND_DirectDebitMandates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_GmfExemptAccounts_EndedByUserId",
                schema: "dbo",
                table: "LND_GmfExemptAccounts",
                column: "EndedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_GmfExemptAccounts_MarkedByUserId",
                schema: "dbo",
                table: "LND_GmfExemptAccounts",
                column: "MarkedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_GmfExemptAccounts_MemberAccountId",
                schema: "dbo",
                table: "LND_GmfExemptAccounts",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_GmfExemptAccounts_LivePerson",
                schema: "dbo",
                table: "LND_GmfExemptAccounts",
                column: "PersonId",
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_GmfExemptAccounts_PublicId",
                schema: "dbo",
                table: "LND_GmfExemptAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_GmfMonthlyUsage_MemberAccountId",
                schema: "dbo",
                table: "LND_GmfMonthlyUsage",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_GmfMonthlyUsage_OperationId",
                schema: "dbo",
                table: "LND_GmfMonthlyUsage",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_GmfMonthlyUsage_Person_Month",
                schema: "dbo",
                table: "LND_GmfMonthlyUsage",
                columns: new[] { "PersonId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_GmfMonthlyUsage_PublicId",
                schema: "dbo",
                table: "LND_GmfMonthlyUsage",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_GmfMonthlyUsage_Reverses",
                schema: "dbo",
                table: "LND_GmfMonthlyUsage",
                column: "ReversesUsageId",
                unique: true,
                filter: "\"ReversesUsageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentAccounts_LastOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentAccounts",
                column: "LastOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentAccounts_LastTransactionId",
                schema: "dbo",
                table: "LND_IssuedDocumentAccounts",
                column: "LastTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentAccounts_MemberAccountId",
                schema: "dbo",
                table: "LND_IssuedDocumentAccounts",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_IssuedDocumentAccounts_PreviousIssuedDocumentId",
                schema: "dbo",
                table: "LND_IssuedDocumentAccounts",
                column: "PreviousIssuedDocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocumentAccounts_Document_Account",
                schema: "dbo",
                table: "LND_IssuedDocumentAccounts",
                columns: new[] { "IssuedDocumentId", "MemberAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_IssuedDocumentAccounts_PublicId",
                schema: "dbo",
                table: "LND_IssuedDocumentAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountControls_MemberAccountId",
                schema: "dbo",
                table: "LND_MemberAccountControls",
                column: "MemberAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountControls_PublicId",
                schema: "dbo",
                table: "LND_MemberAccountControls",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountDailyPositions_BranchId",
                schema: "dbo",
                table: "LND_MemberAccountDailyPositions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountDailyPositions_CreditLineId",
                schema: "dbo",
                table: "LND_MemberAccountDailyPositions",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountDailyPositions_Date_Branch_Line",
                schema: "dbo",
                table: "LND_MemberAccountDailyPositions",
                columns: new[] { "PositionDate", "BranchId", "CreditLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountDailyPositions_Date_Nature",
                schema: "dbo",
                table: "LND_MemberAccountDailyPositions",
                columns: new[] { "PositionDate", "Nature" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountDailyPositions_Account_Date_Version",
                schema: "dbo",
                table: "LND_MemberAccountDailyPositions",
                columns: new[] { "MemberAccountId", "PositionDate", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountDailyPositions_PublicId",
                schema: "dbo",
                table: "LND_MemberAccountDailyPositions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_Account_ValueDate",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                columns: new[] { "MemberAccountId", "ValueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_ActorUserId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_AuthorizationDecisionId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "AuthorizationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_AuthorizedByUserId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "AuthorizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_Clearing",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "ClearingUntil",
                filter: "\"ClearedAt\" IS NULL AND \"ClearingUntil\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_Counterpart",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "CounterpartMemberAccountId",
                filter: "\"CounterpartMemberAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_InboundMessageId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "InboundMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_MembershipWithdrawal",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "MembershipWithdrawalId",
                filter: "\"MembershipWithdrawalId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_MigrationLoadId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "MigrationLoadId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_PayrollDeductionAccountResultId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "PayrollDeductionAccountResultId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_RelatedLoanOperation",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "RelatedLoanOperationId",
                filter: "\"RelatedLoanOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountOperations_ReversedByOperationId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "ReversedByOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountOperations_Account_Sequence",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                columns: new[] { "MemberAccountId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountOperations_Collector_Key",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                columns: new[] { "CollectorCode", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountOperations_Number",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountOperations_PublicId",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountOperations_Reverses",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "ReversesOperationId",
                unique: true,
                filter: "\"ReversesOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountParties_EndedByUserId",
                schema: "dbo",
                table: "LND_MemberAccountParties",
                column: "EndedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountParties_Person_Role",
                schema: "dbo",
                table: "LND_MemberAccountParties",
                columns: new[] { "PersonId", "Role" });

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountParties_Account_Person_Role",
                schema: "dbo",
                table: "LND_MemberAccountParties",
                columns: new[] { "MemberAccountId", "PersonId", "Role" },
                unique: true,
                filter: "\"ValidTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountParties_Holder",
                schema: "dbo",
                table: "LND_MemberAccountParties",
                column: "MemberAccountId",
                unique: true,
                filter: "\"Role\" = 1 AND \"ValidTo\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountParties_PublicId",
                schema: "dbo",
                table: "LND_MemberAccountParties",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccounts_Branch_Status",
                schema: "dbo",
                table: "LND_MemberAccounts",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccounts_ClosedByUserId",
                schema: "dbo",
                table: "LND_MemberAccounts",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccounts_Holder_Status",
                schema: "dbo",
                table: "LND_MemberAccounts",
                columns: new[] { "HolderPersonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccounts_Line_Status",
                schema: "dbo",
                table: "LND_MemberAccounts",
                columns: new[] { "CreditLineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccounts_MigrationLoadId",
                schema: "dbo",
                table: "LND_MemberAccounts",
                column: "MigrationLoadId",
                filter: "\"MigrationLoadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccounts_OpenedByUserId",
                schema: "dbo",
                table: "LND_MemberAccounts",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccounts_SourceAccountNumber",
                schema: "dbo",
                table: "LND_MemberAccounts",
                column: "SourceAccountNumber",
                filter: "\"SourceAccountNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccounts_LiveContribution",
                schema: "dbo",
                table: "LND_MemberAccounts",
                column: "HolderPersonId",
                unique: true,
                filter: "\"Nature\" = 3 AND \"Status\" <> 6 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccounts_LivePermanent",
                schema: "dbo",
                table: "LND_MemberAccounts",
                columns: new[] { "HolderPersonId", "CreditLineId" },
                unique: true,
                filter: "\"DepositClass\" = 3 AND \"Status\" <> 6 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccounts_Number",
                schema: "dbo",
                table: "LND_MemberAccounts",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccounts_PublicId",
                schema: "dbo",
                table: "LND_MemberAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountTransactions_Account_AccDate",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                columns: new[] { "MemberAccountId", "AccountingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountTransactions_Account_ValueDate",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                columns: new[] { "MemberAccountId", "ValueDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountTransactions_AccountingDate",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                column: "AccountingDate");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountTransactions_InstallmentId",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                column: "InstallmentId",
                filter: "\"InstallmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountTransactions_MessagePublicId",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                column: "MessagePublicId",
                filter: "\"MessagePublicId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountTransactions_OperationId",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                column: "OperationId",
                filter: "\"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MemberAccountTransactions_ProcessRunId",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                column: "ProcessRunId",
                filter: "\"ProcessRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountTransactions_Accrual",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                columns: new[] { "MemberAccountId", "Concept", "ValueDate" },
                unique: true,
                filter: "\"Kind\" = 6 AND \"ReversesTransactionId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountTransactions_PublicId",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MemberAccountTransactions_Reverses",
                schema: "dbo",
                table: "LND_MemberAccountTransactions",
                column: "ReversesTransactionId",
                unique: true,
                filter: "\"ReversesTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawalLines_Account",
                schema: "dbo",
                table: "LND_MembershipWithdrawalLines",
                column: "MemberAccountId",
                filter: "\"MemberAccountId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawalLines_AccountOperationId",
                schema: "dbo",
                table: "LND_MembershipWithdrawalLines",
                column: "AccountOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawalLines_Loan",
                schema: "dbo",
                table: "LND_MembershipWithdrawalLines",
                column: "LoanId",
                filter: "\"LoanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawalLines_LoanOperationId",
                schema: "dbo",
                table: "LND_MembershipWithdrawalLines",
                column: "LoanOperationId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MembershipWithdrawalLines_PublicId",
                schema: "dbo",
                table: "LND_MembershipWithdrawalLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MembershipWithdrawalLines_Version_Line",
                schema: "dbo",
                table: "LND_MembershipWithdrawalLines",
                columns: new[] { "MembershipWithdrawalId", "SettlementVersion", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_ApprovalSessionItemId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "ApprovalSessionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_ContributionAccountId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "ContributionAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_DecidedByUserId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_FiledByUserId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "FiledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_OffsetOperationId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "OffsetOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_RefundDecisionId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "RefundDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_SettledByUserId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "SettledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_SettlementIssuedDocumentId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "SettlementIssuedDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_MembershipWithdrawals_Status",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MembershipWithdrawals_Approved",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "PersonId",
                unique: true,
                filter: "\"Status\" = 2 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MembershipWithdrawals_Filed",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "PersonId",
                unique: true,
                filter: "\"Status\" = 1 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_MembershipWithdrawals_Number",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MembershipWithdrawals_PublicId",
                schema: "dbo",
                table: "LND_MembershipWithdrawals",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollAgreementConceptEquivalences_FileGroupId",
                schema: "dbo",
                table: "LND_PayrollAgreementConceptEquivalences",
                column: "FileGroupId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementConceptEquivalences_PublicId",
                schema: "dbo",
                table: "LND_PayrollAgreementConceptEquivalences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementConceptEquivalences_Version_Code",
                schema: "dbo",
                table: "LND_PayrollAgreementConceptEquivalences",
                columns: new[] { "PayrollAgreementVersionId", "PaymasterConceptCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollAgreementFileGroups_OutboundFormatId",
                schema: "dbo",
                table: "LND_PayrollAgreementFileGroups",
                column: "OutboundFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollAgreementFileGroups_ResultFormatId",
                schema: "dbo",
                table: "LND_PayrollAgreementFileGroups",
                column: "ResultFormatId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementFileGroups_PublicId",
                schema: "dbo",
                table: "LND_PayrollAgreementFileGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollAgreementFileGroups_Version_Code",
                schema: "dbo",
                table: "LND_PayrollAgreementFileGroups",
                columns: new[] { "PayrollAgreementVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionAccountResults_Account_TransferDue",
                schema: "dbo",
                table: "LND_PayrollDeductionAccountResults",
                columns: new[] { "MemberAccountId", "TransferDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionAccountResults_FileId",
                schema: "dbo",
                table: "LND_PayrollDeductionAccountResults",
                column: "PayrollDeductionFileId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionAccountResults_InboundMessageId",
                schema: "dbo",
                table: "LND_PayrollDeductionAccountResults",
                column: "InboundMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionAccountResults_SupersedesResultId",
                schema: "dbo",
                table: "LND_PayrollDeductionAccountResults",
                column: "SupersedesResultId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionAccountResults_Line_Message",
                schema: "dbo",
                table: "LND_PayrollDeductionAccountResults",
                columns: new[] { "PayrollDeductionFileAccountLineId", "InboundMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionAccountResults_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionAccountResults",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFileAccountLines_AccountId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileAccountLines",
                column: "MemberAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFileAccountLines_AccountInstallmentId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileAccountLines",
                column: "AccountInstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionFileAccountLines_PersonId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileAccountLines",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFileAccountLines_File_Installment",
                schema: "dbo",
                table: "LND_PayrollDeductionFileAccountLines",
                columns: new[] { "PayrollDeductionFileId", "AccountInstallmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFileAccountLines_File_Line",
                schema: "dbo",
                table: "LND_PayrollDeductionFileAccountLines",
                columns: new[] { "PayrollDeductionFileId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFileAccountLines_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionFileAccountLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionUnmatchedLines_File_Message",
                schema: "dbo",
                table: "LND_PayrollDeductionUnmatchedLines",
                columns: new[] { "PayrollDeductionFileId", "InboundMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionUnmatchedLines_InboundMessageId",
                schema: "dbo",
                table: "LND_PayrollDeductionUnmatchedLines",
                column: "InboundMessageId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionUnmatchedLines_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionUnmatchedLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollAccountDeductions_PayrollRunEmployeeId",
                schema: "dbo",
                table: "PAY_PayrollAccountDeductions",
                column: "PayrollRunEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollAccountDeductions_PayrollRunLineId",
                schema: "dbo",
                table: "PAY_PayrollAccountDeductions",
                column: "PayrollRunLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollAccountDeductions_PublicId",
                schema: "dbo",
                table: "PAY_PayrollAccountDeductions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollAccountDeductions_Run_Employee_Account",
                schema: "dbo",
                table: "PAY_PayrollAccountDeductions",
                columns: new[] { "PayrollRunId", "PayrollRunEmployeeId", "AccountPublicId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountingFactParts_LND_MemberAccountOperations_Trigger~",
                schema: "dbo",
                table: "LND_AccountingFactParts",
                column: "TriggerAccountOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_IssuedDocumentEvents_LND_MemberAccountOperations_Causin~",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents",
                column: "CausingAccountOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_PayrollDeductionFiles_LND_PayrollAgreementFileGroups_Fi~",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                column: "FileGroupId",
                principalSchema: "dbo",
                principalTable: "LND_PayrollAgreementFileGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_ProcessRunErrors_LND_MemberAccounts_MemberAccountId",
                schema: "dbo",
                table: "LND_ProcessRunErrors",
                column: "MemberAccountId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_Setup_LND_Activations_ContributionsActivationId",
                schema: "dbo",
                table: "LND_Setup",
                column: "ContributionsActivationId",
                principalSchema: "dbo",
                principalTable: "LND_Activations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_Setup_LND_Activations_DepositsActivationId",
                schema: "dbo",
                table: "LND_Setup",
                column: "DepositsActivationId",
                principalSchema: "dbo",
                principalTable: "LND_Activations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountCharges_AppliedOperation",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "AppliedOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountCharges_CollectedOperation",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "CollectedOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountCharges_ReversedByOperation",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "ReversedByOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountCharges_TriggerOperation",
                schema: "dbo",
                table: "LND_AccountCharges",
                column: "TriggerOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountInstallmentNonDeductions_LND_AccountInstallments~",
                schema: "dbo",
                table: "LND_AccountInstallmentNonDeductions",
                column: "InstallmentId",
                principalSchema: "dbo",
                principalTable: "LND_AccountInstallments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountInstallments_CancelledByOperation",
                schema: "dbo",
                table: "LND_AccountInstallments",
                column: "CancelledByOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountInstallments_GeneratedByOperation",
                schema: "dbo",
                table: "LND_AccountInstallments",
                column: "GeneratedByOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountPayoutOrders_LND_MemberAccountOperations_Operati~",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "OperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_AccountPayoutOrders_MembershipWithdrawal",
                schema: "dbo",
                table: "LND_AccountPayoutOrders",
                column: "MembershipWithdrawalId",
                principalSchema: "dbo",
                principalTable: "LND_MembershipWithdrawals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_ContributionRequests_LND_MemberAccountOperations_Operat~",
                schema: "dbo",
                table: "LND_ContributionRequests",
                column: "OperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_GmfMonthlyUsage_LND_MemberAccountOperations_OperationId",
                schema: "dbo",
                table: "LND_GmfMonthlyUsage",
                column: "OperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_IssuedDocumentAccounts_LastOperation",
                schema: "dbo",
                table: "LND_IssuedDocumentAccounts",
                column: "LastOperationId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_IssuedDocumentAccounts_LastTransaction",
                schema: "dbo",
                table: "LND_IssuedDocumentAccounts",
                column: "LastTransactionId",
                principalSchema: "dbo",
                principalTable: "LND_MemberAccountTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_MemberAccountOperations_MembershipWithdrawal",
                schema: "dbo",
                table: "LND_MemberAccountOperations",
                column: "MembershipWithdrawalId",
                principalSchema: "dbo",
                principalTable: "LND_MembershipWithdrawals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // T044 (migración de datos, a mano; se conserva al regenerar): las cinco dimensiones de captaciones van al final de la clave de toda regla
            // que ya existía, en «*». Sólo las de 14 posiciones (13 separadores): repetirla no hace nada.
            migrationBuilder.Sql(
                """
                UPDATE dbo."ACC_LendingPostingRules"
                SET "DimensionKey" = "DimensionKey" || '|*|*|*|*|*'
                WHERE length("DimensionKey") - length(replace("DimensionKey", '|', '')) = 13;
                """);

            // T089 (a mano; se conserva al regenerar): Nómina adopta el contrato de cuentas v1 (ProveedorDeCuentas) para consignar aportes y ahorros; sin
            // la adopción el contrato rechaza a Nómina. Sólo si existe el participante NOM vigente y no la tiene ya.
            migrationBuilder.Sql(
                """
                INSERT INTO dbo."COR_IntegrationParticipantAdoptions" ("ParticipantId", "ContractName", "Version", "ValidFrom", "ValidTo", "Reason", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
                SELECT p."Id", 'ProveedorDeCuentas', CAST(1 AS smallint), DATE '1900-01-01', NULL, 'Migración CaptacionesAportes', gen_random_uuid(), FALSE, now(), 'SYSTEM'
                FROM dbo."COR_IntegrationParticipants" p
                WHERE p."Code" = 'NOM' AND p."Role" = 1 AND p."Destination" = '' AND p."ValidTo" IS NULL AND p."IsDeleted" = FALSE
                  AND NOT EXISTS (
                    SELECT 1 FROM dbo."COR_IntegrationParticipantAdoptions" a
                    WHERE a."ParticipantId" = p."Id" AND a."ContractName" = 'ProveedorDeCuentas' AND a."Version" = 1);
                """);

            // T098 (a mano; se conserva al regenerar): el participante ASO, destino Associates, adopta RetiroDeAsociadoAprobado v1 (mensajes.md §1.4, §7;
            // research C16, E-04). Clase Associates 9, rol MessageDestination 2, registrado por el proceso (2). Idempotente: sólo lo que falte.
            migrationBuilder.Sql(
                """
                INSERT INTO dbo."COR_IntegrationParticipants" ("Code", "Class", "Role", "Destination", "IsTestDouble", "IsEnabled", "ValidFrom", "ValidTo", "Reason", "RegisteredByKind", "RegisteredByUserId", "RegisteredByName", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
                SELECT 'ASO', 9, 2, 'Associates', FALSE, TRUE, DATE '1900-01-01', NULL, 'Asociados recibe el retiro del asociado aprobado en Cartera', 2, NULL, 'Migración CaptacionesAportes', gen_random_uuid(), FALSE, now(), 'SYSTEM'
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo."COR_IntegrationParticipants" p
                    WHERE p."Code" = 'ASO' AND p."Role" = 2 AND p."Destination" = 'Associates' AND p."IsDeleted" = FALSE);

                INSERT INTO dbo."COR_IntegrationParticipantAdoptions" ("ParticipantId", "ContractName", "Version", "ValidFrom", "ValidTo", "Reason", "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
                SELECT p."Id", 'RetiroDeAsociadoAprobado', CAST(1 AS smallint), DATE '1900-01-01', NULL, 'Migración CaptacionesAportes', gen_random_uuid(), FALSE, now(), 'SYSTEM'
                FROM dbo."COR_IntegrationParticipants" p
                WHERE p."Code" = 'ASO' AND p."Role" = 2 AND p."Destination" = 'Associates' AND p."ValidTo" IS NULL AND p."IsDeleted" = FALSE
                  AND NOT EXISTS (
                    SELECT 1 FROM dbo."COR_IntegrationParticipantAdoptions" a
                    WHERE a."ParticipantId" = p."Id" AND a."ContractName" = 'RetiroDeAsociadoAprobado' AND a."Version" = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // T098 (a mano): se retiran la adopción y el participante ASO que sembró esta migración.
            migrationBuilder.Sql(
                """
                DELETE FROM dbo."COR_IntegrationParticipantAdoptions"
                WHERE "ContractName" = 'RetiroDeAsociadoAprobado' AND "Reason" = 'Migración CaptacionesAportes';
                DELETE FROM dbo."COR_IntegrationParticipants"
                WHERE "Code" = 'ASO' AND "RegisteredByName" = 'Migración CaptacionesAportes'
                  AND NOT EXISTS (SELECT 1 FROM dbo."COR_IntegrationParticipantAdoptions" a WHERE a."ParticipantId" = "COR_IntegrationParticipants"."Id");
                """);

            // T089 (a mano): se retira la adopción de ProveedorDeCuentas que hizo esta migración.
            migrationBuilder.Sql(
                """
                DELETE FROM dbo."COR_IntegrationParticipantAdoptions"
                WHERE "ContractName" = 'ProveedorDeCuentas' AND "Reason" = 'Migración CaptacionesAportes';
                """);

            // T044 (a mano): la clave vuelve a sus 14 posiciones antes de quitar las columnas.
            migrationBuilder.Sql(
                """
                UPDATE dbo."ACC_LendingPostingRules"
                SET "DimensionKey" = left("DimensionKey", length("DimensionKey") - 10)
                WHERE length("DimensionKey") - length(replace("DimensionKey", '|', '')) = 18 AND right("DimensionKey", 10) = '|*|*|*|*|*';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AccountingFactParts_LND_MemberAccountOperations_Trigger~",
                schema: "dbo",
                table: "LND_AccountingFactParts");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_IssuedDocumentEvents_LND_MemberAccountOperations_Causin~",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PayrollDeductionFiles_LND_PayrollAgreementFileGroups_Fi~",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_ProcessRunErrors_LND_MemberAccounts_MemberAccountId",
                schema: "dbo",
                table: "LND_ProcessRunErrors");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Setup_LND_Activations_ContributionsActivationId",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Setup_LND_Activations_DepositsActivationId",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AccountInstallments_LND_MemberAccounts_MemberAccountId",
                schema: "dbo",
                table: "LND_AccountInstallments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MemberAccountOperations_LND_MemberAccounts_CounterpartM~",
                schema: "dbo",
                table: "LND_MemberAccountOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MemberAccountOperations_LND_MemberAccounts_MemberAccoun~",
                schema: "dbo",
                table: "LND_MemberAccountOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MembershipWithdrawals_ContributionAccount",
                schema: "dbo",
                table: "LND_MembershipWithdrawals");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PayrollDeductionAccountResults_LND_MemberAccounts_Membe~",
                schema: "dbo",
                table: "LND_PayrollDeductionAccountResults");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PayrollDeductionFileAccountLines_LND_MemberAccounts_Mem~",
                schema: "dbo",
                table: "LND_PayrollDeductionFileAccountLines");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AccountInstallments_CancelledByOperation",
                schema: "dbo",
                table: "LND_AccountInstallments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AccountInstallments_GeneratedByOperation",
                schema: "dbo",
                table: "LND_AccountInstallments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_MembershipWithdrawals_OffsetOperation",
                schema: "dbo",
                table: "LND_MembershipWithdrawals");

            migrationBuilder.DropTable(
                name: "COR_AssociateInboundMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AccountChargeExemptions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AccountCharges",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AccountInstallmentNonDeductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ContributionLineVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ContributionRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DepositLineRateTiers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DirectDebitMandates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_GmfExemptAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_GmfMonthlyUsage",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_IssuedDocumentAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MemberAccountControls",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MemberAccountDailyPositions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MemberAccountParties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MembershipWithdrawalLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollAgreementConceptEquivalences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionUnmatchedLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PayrollAccountDeductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DepositLineCharges",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AccountPayoutOrders",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MemberAccountTransactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollAgreementFileGroups",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DepositLineVersions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MemberAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MemberAccountOperations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MembershipWithdrawals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionAccountResults",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionFileAccountLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AccountInstallments",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_PAY_Novelties_AccountPublicId",
                schema: "dbo",
                table: "PAY_Novelties");

            migrationBuilder.DropIndex(
                name: "IX_LND_Setup_ContributionsActivationId",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropIndex(
                name: "IX_LND_Setup_DepositsActivationId",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropIndex(
                name: "IX_LND_ProcessRunErrors_MemberAccountId",
                schema: "dbo",
                table: "LND_ProcessRunErrors");

            migrationBuilder.DropIndex(
                name: "UK_LND_ProcessRunErrors_Run_Account_Attempt",
                schema: "dbo",
                table: "LND_ProcessRunErrors");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LND_ProcessRunErrors_Subject",
                schema: "dbo",
                table: "LND_ProcessRunErrors");

            migrationBuilder.DropIndex(
                name: "IX_LND_PayrollDeductionFiles_FileGroupId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles");

            migrationBuilder.DropIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Sent",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles");

            migrationBuilder.DropIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Version",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LND_PayrollAgreementVersions_Formats",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions");

            migrationBuilder.DropIndex(
                name: "UK_LND_MigrationLoads_Activated",
                schema: "dbo",
                table: "LND_MigrationLoads");

            migrationBuilder.DropIndex(
                name: "UK_LND_MigrationLoads_Applied",
                schema: "dbo",
                table: "LND_MigrationLoads");

            migrationBuilder.DropIndex(
                name: "UK_LND_MigrationLoads_Draft",
                schema: "dbo",
                table: "LND_MigrationLoads");

            migrationBuilder.DropIndex(
                name: "IX_LND_IssuedDocumentEvents_CausingAccountOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LND_IssuedDocumentEvents_Cause",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents");

            migrationBuilder.DropIndex(
                name: "IX_LND_CreditLines_Nature_Status",
                schema: "dbo",
                table: "LND_CreditLines");

            migrationBuilder.DropIndex(
                name: "UK_LND_CreditLines_ActiveContribution",
                schema: "dbo",
                table: "LND_CreditLines");

            migrationBuilder.DropIndex(
                name: "UK_LND_Activations_MigrationLoadId",
                schema: "dbo",
                table: "LND_Activations");

            migrationBuilder.DropIndex(
                name: "UK_LND_Activations_Nature",
                schema: "dbo",
                table: "LND_Activations");

            migrationBuilder.DropIndex(
                name: "UK_LND_AccountingFactParts_AccTrigger_Branch_Fact",
                schema: "dbo",
                table: "LND_AccountingFactParts");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                schema: "dbo",
                table: "PAY_Novelties");

            migrationBuilder.DropColumn(
                name: "AccountPublicId",
                schema: "dbo",
                table: "PAY_Novelties");

            migrationBuilder.DropColumn(
                name: "ContributionsActivatedAt",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropColumn(
                name: "ContributionsActivationDate",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropColumn(
                name: "ContributionsActivationId",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropColumn(
                name: "DepositsActivatedAt",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropColumn(
                name: "DepositsActivationDate",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropColumn(
                name: "DepositsActivationId",
                schema: "dbo",
                table: "LND_Setup");

            migrationBuilder.DropColumn(
                name: "MemberAccountId",
                schema: "dbo",
                table: "LND_ProcessRunErrors");

            migrationBuilder.DropColumn(
                name: "FileGroupCode",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles");

            migrationBuilder.DropColumn(
                name: "FileGroupId",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles");

            migrationBuilder.DropColumn(
                name: "AllocationPriority",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines");

            migrationBuilder.DropColumn(
                name: "PaymasterConceptCode",
                schema: "dbo",
                table: "LND_PayrollDeductionFileLines");

            migrationBuilder.DropColumn(
                name: "EmployeeIdentification",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions");

            migrationBuilder.DropColumn(
                name: "IncludesAccountInstallments",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions");

            migrationBuilder.DropColumn(
                name: "UsesFileGroups",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions");

            migrationBuilder.DropColumn(
                name: "AccountCount",
                schema: "dbo",
                table: "LND_MigrationLoads");

            migrationBuilder.DropColumn(
                name: "Nature",
                schema: "dbo",
                table: "LND_MigrationLoads");

            migrationBuilder.DropColumn(
                name: "CausingAccountOperationId",
                schema: "dbo",
                table: "LND_IssuedDocumentEvents");

            migrationBuilder.DropColumn(
                name: "Nature",
                schema: "dbo",
                table: "LND_CreditLines");

            migrationBuilder.DropColumn(
                name: "MaxNegotiatedSpread",
                schema: "dbo",
                table: "LND_AttributionRules");

            migrationBuilder.DropColumn(
                name: "Nature",
                schema: "dbo",
                table: "LND_Activations");

            migrationBuilder.DropColumn(
                name: "AccountCount",
                schema: "dbo",
                table: "LND_AccountingFactParts");

            migrationBuilder.DropColumn(
                name: "TriggerAccountOperationId",
                schema: "dbo",
                table: "LND_AccountingFactParts");

            migrationBuilder.DropColumn(
                name: "DepositInsuranceEnrolled",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions");

            migrationBuilder.DropColumn(
                name: "DepositsFromAssociatesOnly",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions");

            migrationBuilder.DropColumn(
                name: "IsGmfWithholdingAgent",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions");

            migrationBuilder.DropColumn(
                name: "LiquidityFundCategory",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions");

            migrationBuilder.DropColumn(
                name: "MayTakeDeposits",
                schema: "dbo",
                table: "COR_RegulatoryProfileVersions");

            migrationBuilder.DropColumn(
                name: "AccountFlag",
                schema: "dbo",
                table: "ACC_LendingPostingRules");

            migrationBuilder.DropColumn(
                name: "ContributionClass",
                schema: "dbo",
                table: "ACC_LendingPostingRules");

            migrationBuilder.DropColumn(
                name: "DepositClass",
                schema: "dbo",
                table: "ACC_LendingPostingRules");

            migrationBuilder.DropColumn(
                name: "IssueTermBand",
                schema: "dbo",
                table: "ACC_LendingPostingRules");

            migrationBuilder.DropColumn(
                name: "LineNature",
                schema: "dbo",
                table: "ACC_LendingPostingRules");

            migrationBuilder.AlterColumn<int>(
                name: "ResultFormatId",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OutboundFormatId",
                schema: "dbo",
                table: "LND_PayrollAgreementVersions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SessionItemId",
                schema: "dbo",
                table: "LND_Activations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MigrationLoadId",
                schema: "dbo",
                table: "LND_Activations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Sent",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                columns: new[] { "PayrollAgreementId", "PeriodYear", "PeriodMonth", "SubPeriodNumber" },
                unique: true,
                filter: "\"Status\" = 3 AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionFiles_Agreement_Period_Version",
                schema: "dbo",
                table: "LND_PayrollDeductionFiles",
                columns: new[] { "PayrollAgreementId", "PeriodYear", "PeriodMonth", "SubPeriodNumber", "Version" },
                unique: true);

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
                name: "UK_LND_Activations_MigrationLoadId",
                schema: "dbo",
                table: "LND_Activations",
                column: "MigrationLoadId",
                unique: true);
        }
    }
}
