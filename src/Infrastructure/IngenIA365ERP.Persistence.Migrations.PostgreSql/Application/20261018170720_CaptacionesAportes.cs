using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    /// <summary>
    /// Feature 014 — Cartera financiera, captaciones: la migración par de los aportes (K7; research C08; tasks.md T012, T021–T028, T044, T053, T055,
    /// T056). Lleva la naturaleza de la línea común (<c>LND_CreditLines.Nature</c>, defecto colocación, con <c>IX_LND_CreditLines_Nature_Status</c> y
    /// <c>UK_LND_CreditLines_ActiveContribution</c>), el libro de cuentas (<c>LND_MemberAccounts</c>, <c>LND_MemberAccountParties</c>,
    /// <c>LND_AccountPayoutOrders</c>, <c>LND_MemberAccountOperations</c>, <c>LND_MemberAccountTransactions</c>, <c>LND_MemberAccountDailyPositions</c>,
    /// <c>LND_MemberAccountControls</c>), las columnas nuevas de data-model §1.4, §1.5 y §1.8 en tablas existentes (todas nulas o con defecto; los
    /// formatos de la versión del convenio pasan a nulos con <c>CK_LND_PayrollAgreementVersions_Formats</c>, y los tres únicos de la carga de
    /// migración pasan a <c>(Nature, Status)</c>), las cinco dimensiones de captaciones de <c>ACC_LendingPostingRules</c> (T044) con la
    /// <b>migración de datos</b> que agrega <c>|*|*|*|*|*</c> a la clave de las reglas que ya existían (SQL a mano al final de <c>Up</c>, que se
    /// conserva al regenerar), las fechas de activación de aportes y captaciones en <c>LND_Setup</c> (T053) y la activación sin carga ni acta
    /// (<c>LND_Activations.MigrationLoadId</c> y <c>SessionItemId</c> nulos, el único de la carga filtrado; T055). <b>Regenerable hasta el
    /// congelamiento de la fase final</b> (T056): las tareas fundacionales y de US1/US2/US3/US10 le suman sus tablas regenerándola, nunca editándola a
    /// mano salvo el SQL de datos marcado. Aditiva.
    /// </summary>
    public partial class CaptacionesAportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        name: "FK_LND_AccountPayoutOrders_LND_MemberAccountOperations_Operati~",
                        column: x => x.OperationId,
                        principalSchema: "dbo",
                        principalTable: "LND_MemberAccountOperations",
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

            // T044 (migración de datos, a mano; se conserva al regenerar): las cinco dimensiones de captaciones van al final de la clave de toda regla
            // que ya existía, en «*». Sólo las de 14 posiciones (13 separadores): repetirla no hace nada.
            migrationBuilder.Sql(
                """
                UPDATE dbo."ACC_LendingPostingRules"
                SET "DimensionKey" = "DimensionKey" || '|*|*|*|*|*'
                WHERE length("DimensionKey") - length(replace("DimensionKey", '|', '')) = 13;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.DropTable(
                name: "LND_AccountPayoutOrders",
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
                name: "LND_MemberAccountTransactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MemberAccountOperations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MemberAccounts",
                schema: "dbo");

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
