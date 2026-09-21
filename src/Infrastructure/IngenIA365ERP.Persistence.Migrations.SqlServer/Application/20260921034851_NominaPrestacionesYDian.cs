using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 010, entrega N1 (prestaciones, retiro, procedimiento 2). Migración <b>aditiva</b>:
    /// no borra tablas ni datos hacia adelante, así que no lleva el marcador de destructiva (D-13);
    /// producción exige de todos modos respaldo por cooperativa y segundo revisor, porque toca
    /// <c>PAY_PayrollRuns</c>, la tabla más viva de nómina.
    ///
    /// <para>
    /// <b>Esquema</b> (data-model §1 y §2): la corrida gana <c>Kind</c> con default 0 <b>en la
    /// base</b> —toda corrida existente queda <c>Ordinary</c> sin migración de datos—, el período
    /// pasa a nullable y el único <c>UK_PAY_PayrollRuns_Period_Version</c> se reemplaza por cinco
    /// filtrados por tipo; columnas nuevas en líneas, novedades, porcentajes P2, conceptos, ficha
    /// del empleado y persona; y diez tablas nuevas (políticas, festivos, saldos iniciales,
    /// movimientos de vacaciones, motivos y terminaciones, descuentos de la definitiva, cálculos P2
    /// y sus meses, consignaciones a fondos).
    /// </para>
    ///
    /// <para>
    /// <b>Datos</b> (idempotentes, <c>INSERT … WHERE NOT EXISTS</c> / <c>UPDATE … WHERE</c> con la
    /// condición exacta): copia <c>Payroll.ApplyEmployerExemption</c> → <c>Exonerada114_1</c> y
    /// <c>Payroll.AllowSameUserApproval</c> → <c>AllowSameUserApproval</c> a
    /// <c>PAY_CompanyPolicies</c> con vigencia 2026-01-01; rellena
    /// <c>PAY_Employees.DisbursementBankId</c> desde <c>COR_Banks.LegacyCode = PayrollBankId</c>;
    /// precisa <c>Source</c> de SMMLV, auxilio, UVT, salud del aprendiz y FSP <b>sólo donde</b>
    /// sigue el texto genérico de la semilla (una vigencia editada a mano no se pisa); y cierra la
    /// <c>FSP_TABLA</c> vigente el 2027-03-31 si sigue abierta (Ley 2381 de 2024 desde abril).
    /// </para>
    ///
    /// <para>
    /// <b>Reversible sólo sin corridas con <c>Kind ≠ 0</c></b>: con <c>PayPeriodId</c> NULL el
    /// índice viejo no se puede recrear, así que <c>Down()</c> falla con mensaje <b>antes de tocar
    /// nada</b> si las hay. Las precisiones de <c>Source</c> y el cierre de la FSP quedan: son
    /// correcciones de dato, no esquema.
    /// </para>
    /// </summary>
    public partial class NominaPrestacionesYDian : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_PAY_PayrollRuns_Period_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.AlterColumn<int>(
                name: "PayPeriodId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CutoffDate",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PayDate",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Semester",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TerminationId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VacationMovementId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Year",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SettlementDeductionId",
                schema: "dbo",
                table: "PAY_PayrollRunLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VacationMovementId",
                schema: "dbo",
                table: "PAY_Novelties",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceCalculationId",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprenticeStage",
                schema: "dbo",
                table: "PAY_Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ColombianAbroad",
                schema: "dbo",
                table: "PAY_Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DianContractType",
                schema: "dbo",
                table: "PAY_Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DianPaymentMethodCode",
                schema: "dbo",
                table: "PAY_Employees",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisbursementBankId",
                schema: "dbo",
                table: "PAY_Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EconomicActivityCode",
                schema: "dbo",
                table: "PAY_Employees",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ForeignNotRequiredToContributePension",
                schema: "dbo",
                table: "PAY_Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HighRiskPension",
                schema: "dbo",
                table: "PAY_Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PensionTransitionRegime",
                schema: "dbo",
                table: "PAY_Employees",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PilaContributorSubType",
                schema: "dbo",
                table: "PAY_Employees",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PilaContributorType",
                schema: "dbo",
                table: "PAY_Employees",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkAddress",
                schema: "dbo",
                table: "PAY_Employees",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkCenterCode",
                schema: "dbo",
                table: "PAY_Employees",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkMunicipalityDaneCode",
                schema: "dbo",
                table: "PAY_Employees",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AffectsVacationBase",
                schema: "dbo",
                table: "PAY_ConceptDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DianElement",
                schema: "dbo",
                table: "PAY_ConceptDefinitions",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherNames",
                schema: "dbo",
                table: "COR_People",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondLastName",
                schema: "dbo",
                table: "COR_People",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PAY_CompanyPolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_CompanyPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_EmployeeBenefitOpeningBalances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PendingVacationDays = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    AccruedSeverance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedSeveranceInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedServiceBonus = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ServiceBonusDaysAccrued = table.Column<int>(type: "int", nullable: true),
                    SeveranceDaysAccrued = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AdjustsBalanceId = table.Column<int>(type: "int", nullable: true),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ConsumedByRunId = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_EmployeeBenefitOpeningBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_EmployeeBenefitOpeningBalances_PAY_EmployeeBenefitOpeningBalances_AdjustsBalanceId",
                        column: x => x.AdjustsBalanceId,
                        principalSchema: "dbo",
                        principalTable: "PAY_EmployeeBenefitOpeningBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_EmployeeBenefitOpeningBalances_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_EmployeeBenefitOpeningBalances_PAY_PayrollRuns_ConsumedByRunId",
                        column: x => x.ConsumedByRunId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_Holidays",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_Holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_SeveranceFundDeposits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollRunId = table.Column<int>(type: "int", nullable: false),
                    SeveranceFundId = table.Column<int>(type: "int", nullable: false),
                    DepositedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    DepositedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_SeveranceFundDeposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_SeveranceFundDeposits_PAY_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_SeveranceFundDeposits_PAY_SeveranceProviders_SeveranceFundId",
                        column: x => x.SeveranceFundId,
                        principalSchema: "dbo",
                        principalTable: "PAY_SeveranceProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_TerminationReasons",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    GeneratesSeverancePay = table.Column<bool>(type: "bit", nullable: false),
                    RequiresContractEndDate = table.Column<bool>(type: "bit", nullable: false),
                    LegalBasis = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_TerminationReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_VacationMovements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BusinessDays = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CalendarDays = table.Column<int>(type: "int", nullable: false),
                    WeekPolicyUsed = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SkippedDaysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PayrollRunId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_VacationMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_VacationMovements_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_VacationMovements_PAY_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_WithholdingRateCalculations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    TargetYear = table.Column<short>(type: "smallint", nullable: false),
                    TargetSemester = table.Column<byte>(type: "tinyint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalculatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MonthsConsidered = table.Column<byte>(type: "tinyint", nullable: false),
                    Divisor = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    TotalGrossIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalMandatoryContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeclaredDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalExemptIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepuratedBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageMonthlyBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UvtValueUsed = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AverageInUvt = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TheoreticalWithholding = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RatePercent = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    DepurationSequence = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TableParameterId = table.Column<int>(type: "int", nullable: false),
                    PlanTableUsed = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ResultingRateId = table.Column<int>(type: "int", nullable: true),
                    ExplanationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_WithholdingRateCalculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_WithholdingRateCalculations_PAY_EmployeeWithholdingRates_ResultingRateId",
                        column: x => x.ResultingRateId,
                        principalSchema: "dbo",
                        principalTable: "PAY_EmployeeWithholdingRates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_WithholdingRateCalculations_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_WithholdingRateCalculations_PAY_LegalParameters_TableParameterId",
                        column: x => x.TableParameterId,
                        principalSchema: "dbo",
                        principalTable: "PAY_LegalParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_EmploymentTerminations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TerminationReasonId = table.Column<int>(type: "int", nullable: false),
                    ContractTypeAtTermination = table.Column<int>(type: "int", nullable: true),
                    ContractEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SettlementDocumentAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignedDocumentAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReinstatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReinstatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReinstateReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_EmploymentTerminations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_EmploymentTerminations_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_EmploymentTerminations_PAY_TerminationReasons_TerminationReasonId",
                        column: x => x.TerminationReasonId,
                        principalSchema: "dbo",
                        principalTable: "PAY_TerminationReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_WithholdingRateCalculationMonths",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CalculationId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
                    GrossIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MandatoryContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedSpecialRuns = table.Column<bool>(type: "bit", nullable: false),
                    SourceRunsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_WithholdingRateCalculationMonths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_WithholdingRateCalculationMonths_PAY_WithholdingRateCalculations_CalculationId",
                        column: x => x.CalculationId,
                        principalSchema: "dbo",
                        principalTable: "PAY_WithholdingRateCalculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_SettlementDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TerminationId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    LoanPortfolioId = table.Column<int>(type: "int", nullable: true),
                    RecurringNoveltyId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProposedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProposedBreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppliedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustmentReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AdjustedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdjustedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CarteraTransactionPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RemainingBalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAY_SettlementDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_SettlementDeductions_LND_LoanPortfolios_LoanPortfolioId",
                        column: x => x.LoanPortfolioId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_SettlementDeductions_PAY_EmploymentTerminations_TerminationId",
                        column: x => x.TerminationId,
                        principalSchema: "dbo",
                        principalTable: "PAY_EmploymentTerminations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_SettlementDeductions_PAY_RecurringNovelties_RecurringNoveltyId",
                        column: x => x.RecurringNoveltyId,
                        principalSchema: "dbo",
                        principalTable: "PAY_RecurringNovelties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRuns_Kind_Status",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRuns_TerminationId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "TerminationId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRuns_VacationMovementId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "VacationMovementId");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_Ordinary_Period_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "PayPeriodId", "Version" },
                unique: true,
                filter: "[Kind] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_ServiceBonus_Year_Semester_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "Year", "Semester", "Version" },
                unique: true,
                filter: "[Kind] = 1");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_Settlement_Employee_Cutoff_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "EmployeeId", "CutoffDate", "Version" },
                unique: true,
                filter: "[Kind] = 4");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_Severance_Year_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "Year", "Version" },
                unique: true,
                filter: "[Kind] = 2");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_Vacation_Movement_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "VacationMovementId", "Version" },
                unique: true,
                filter: "[Kind] = 3");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRunLines_SettlementDeductionId",
                schema: "dbo",
                table: "PAY_PayrollRunLines",
                column: "SettlementDeductionId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_VacationMovement",
                schema: "dbo",
                table: "PAY_Novelties",
                column: "VacationMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmployeeWithholdingRates_SourceCalculationId",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates",
                column: "SourceCalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Employees_DisbursementBank",
                schema: "dbo",
                table: "PAY_Employees",
                column: "DisbursementBankId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_CompanyPolicies_PublicId",
                schema: "dbo",
                table: "PAY_CompanyPolicies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_CompanyPolicies_Key_ValidFrom",
                schema: "dbo",
                table: "PAY_CompanyPolicies",
                columns: new[] { "Key", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmployeeBenefitOpeningBalances_AdjustsBalanceId",
                schema: "dbo",
                table: "PAY_EmployeeBenefitOpeningBalances",
                column: "AdjustsBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmployeeBenefitOpeningBalances_ConsumedByRunId",
                schema: "dbo",
                table: "PAY_EmployeeBenefitOpeningBalances",
                column: "ConsumedByRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmployeeBenefitOpeningBalances_PublicId",
                schema: "dbo",
                table: "PAY_EmployeeBenefitOpeningBalances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_EmployeeBenefitOpeningBalances_Employee_AsOf_Kind",
                schema: "dbo",
                table: "PAY_EmployeeBenefitOpeningBalances",
                columns: new[] { "EmployeeId", "AsOfDate", "Kind" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmploymentTerminations_Date",
                schema: "dbo",
                table: "PAY_EmploymentTerminations",
                column: "TerminationDate");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmploymentTerminations_PublicId",
                schema: "dbo",
                table: "PAY_EmploymentTerminations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmploymentTerminations_TerminationReasonId",
                schema: "dbo",
                table: "PAY_EmploymentTerminations",
                column: "TerminationReasonId");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_EmploymentTerminations_Employee_Registered",
                schema: "dbo",
                table: "PAY_EmploymentTerminations",
                column: "EmployeeId",
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_EmploymentTerminations_Employee_Settled",
                schema: "dbo",
                table: "PAY_EmploymentTerminations",
                column: "EmployeeId",
                unique: true,
                filter: "[Status] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Holidays_PublicId",
                schema: "dbo",
                table: "PAY_Holidays",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Holidays_Year",
                schema: "dbo",
                table: "PAY_Holidays",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_Holidays_Date",
                schema: "dbo",
                table: "PAY_Holidays",
                column: "Date",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_SettlementDeductions_LoanPortfolioId",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                column: "LoanPortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_SettlementDeductions_PublicId",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_SettlementDeductions_RecurringNoveltyId",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                column: "RecurringNoveltyId");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Libranza",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                columns: new[] { "TerminationId", "RecurringNoveltyId" },
                unique: true,
                filter: "[RecurringNoveltyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Loan",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                columns: new[] { "TerminationId", "LoanPortfolioId" },
                unique: true,
                filter: "[LoanPortfolioId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_SeveranceFundDeposits_PublicId",
                schema: "dbo",
                table: "PAY_SeveranceFundDeposits",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_SeveranceFundDeposits_SeveranceFundId",
                schema: "dbo",
                table: "PAY_SeveranceFundDeposits",
                column: "SeveranceFundId");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SeveranceFundDeposits_Run_Fund",
                schema: "dbo",
                table: "PAY_SeveranceFundDeposits",
                columns: new[] { "PayrollRunId", "SeveranceFundId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_TerminationReasons_PublicId",
                schema: "dbo",
                table: "PAY_TerminationReasons",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_TerminationReasons_Code",
                schema: "dbo",
                table: "PAY_TerminationReasons",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_VacationMovements_Employee_Start",
                schema: "dbo",
                table: "PAY_VacationMovements",
                columns: new[] { "EmployeeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_VacationMovements_PayrollRunId",
                schema: "dbo",
                table: "PAY_VacationMovements",
                column: "PayrollRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_VacationMovements_PublicId",
                schema: "dbo",
                table: "PAY_VacationMovements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_VacationMovements_Status",
                schema: "dbo",
                table: "PAY_VacationMovements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_WithholdingRateCalculationMonths_PublicId",
                schema: "dbo",
                table: "PAY_WithholdingRateCalculationMonths",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_WithholdingRateCalculationMonths_Calculation_Year_Month",
                schema: "dbo",
                table: "PAY_WithholdingRateCalculationMonths",
                columns: new[] { "CalculationId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_WithholdingRateCalculations_PublicId",
                schema: "dbo",
                table: "PAY_WithholdingRateCalculations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_WithholdingRateCalculations_ResultingRateId",
                schema: "dbo",
                table: "PAY_WithholdingRateCalculations",
                column: "ResultingRateId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_WithholdingRateCalculations_Status",
                schema: "dbo",
                table: "PAY_WithholdingRateCalculations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_WithholdingRateCalculations_TableParameterId",
                schema: "dbo",
                table: "PAY_WithholdingRateCalculations",
                column: "TableParameterId");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_WithholdingRateCalculations_Employee_Target_Version",
                schema: "dbo",
                table: "PAY_WithholdingRateCalculations",
                columns: new[] { "EmployeeId", "TargetYear", "TargetSemester", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_Employees_COR_Banks_DisbursementBankId",
                schema: "dbo",
                table: "PAY_Employees",
                column: "DisbursementBankId",
                principalSchema: "dbo",
                principalTable: "COR_Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_EmployeeWithholdingRates_PAY_WithholdingRateCalculations_SourceCalculationId",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates",
                column: "SourceCalculationId",
                principalSchema: "dbo",
                principalTable: "PAY_WithholdingRateCalculations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_Novelties_PAY_VacationMovements_VacationMovementId",
                schema: "dbo",
                table: "PAY_Novelties",
                column: "VacationMovementId",
                principalSchema: "dbo",
                principalTable: "PAY_VacationMovements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollRunLines_PAY_SettlementDeductions_SettlementDeductionId",
                schema: "dbo",
                table: "PAY_PayrollRunLines",
                column: "SettlementDeductionId",
                principalSchema: "dbo",
                principalTable: "PAY_SettlementDeductions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollRuns_PAY_Employees_EmployeeId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "EmployeeId",
                principalSchema: "dbo",
                principalTable: "PAY_Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollRuns_PAY_EmploymentTerminations_TerminationId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "TerminationId",
                principalSchema: "dbo",
                principalTable: "PAY_EmploymentTerminations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollRuns_PAY_VacationMovements_VacationMovementId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "VacationMovementId",
                principalSchema: "dbo",
                principalTable: "PAY_VacationMovements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ----------------------------------------------------------------- datos (idempotentes)

            // (1) Las dos políticas que hoy viven en COR_SystemSettings pasan a PAY_CompanyPolicies con
            //     vigencia desde el 1 de enero de 2026. Sólo si el valor es un booleano legible y la clave
            //     no tiene ya una vigencia viva (una registrada a mano por la pantalla no se pisa). El
            //     lector cae a COR_SystemSettings si la clave no existe, así que una cooperativa sin la
            //     fila tampoco se queda sin política.
            migrationBuilder.Sql("""
                INSERT INTO dbo.[PAY_CompanyPolicies] ([Key], [Value], [ValidFrom], [ValidTo], [Notes], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
                SELECT 'Exonerada114_1', LOWER(LTRIM(RTRIM(s.[SettingValue]))), '2026-01-01', NULL,
                       N'Copiada de COR_SystemSettings.Payroll.ApplyEmployerExemption por la migracion NominaPrestacionesYDian',
                       NEWID(), 0, SYSUTCDATETIME(), 'Migration'
                FROM dbo.[COR_SystemSettings] s
                WHERE s.[SettingKey] = 'Payroll.ApplyEmployerExemption' AND s.[IsDeleted] = 0
                  AND LOWER(LTRIM(RTRIM(s.[SettingValue]))) IN ('true', 'false')
                  AND NOT EXISTS (SELECT 1 FROM dbo.[PAY_CompanyPolicies] p WHERE p.[Key] = 'Exonerada114_1' AND p.[IsDeleted] = 0);
                """);
            migrationBuilder.Sql("""
                INSERT INTO dbo.[PAY_CompanyPolicies] ([Key], [Value], [ValidFrom], [ValidTo], [Notes], [PublicId], [IsDeleted], [CreatedAt], [CreatedBy])
                SELECT 'AllowSameUserApproval', LOWER(LTRIM(RTRIM(s.[SettingValue]))), '2026-01-01', NULL,
                       N'Copiada de COR_SystemSettings.Payroll.AllowSameUserApproval por la migracion NominaPrestacionesYDian',
                       NEWID(), 0, SYSUTCDATETIME(), 'Migration'
                FROM dbo.[COR_SystemSettings] s
                WHERE s.[SettingKey] = 'Payroll.AllowSameUserApproval' AND s.[IsDeleted] = 0
                  AND LOWER(LTRIM(RTRIM(s.[SettingValue]))) IN ('true', 'false')
                  AND NOT EXISTS (SELECT 1 FROM dbo.[PAY_CompanyPolicies] p WHERE p.[Key] = 'AllowSameUserApproval' AND p.[IsDeleted] = 0);
                """);

            // (2) Banco de dispersión: el código del legado (PayrollBankId, nvarchar(4)) se resuelve a la
            //     fila de COR_Banks por LegacyCode. Lo que no cruce queda NULL y la ficha lo pide.
            migrationBuilder.Sql("""
                UPDATE e SET e.[DisbursementBankId] = b.[Id]
                FROM dbo.[PAY_Employees] e
                JOIN dbo.[COR_Banks] b ON b.[IsDeleted] = 0 AND LTRIM(RTRIM(b.[LegacyCode])) = LTRIM(RTRIM(e.[PayrollBankId]))
                WHERE e.[DisbursementBankId] IS NULL
                  AND e.[PayrollBankId] IS NOT NULL AND LTRIM(RTRIM(e.[PayrollBankId])) <> '';
                """);

            // (3) Source exacto (norma y artículo) SÓLO donde la fila todavía dice el texto genérico de la
            //     semilla 2026. Los textos genéricos son los de PayrollLegalParametersSeeder a la fecha;
            //     una vigencia con Source editado a mano no cumple la condición y no se toca.
            migrationBuilder.Sql("""
                UPDATE dbo.[PAY_LegalParameters] SET [Source] = N'Decreto 1469 de 2025 (salario mínimo 2026; Decreto 159 de 2026, mismo valor)'
                WHERE [Code] = 'SMMLV' AND [Source] = N'Decreto de salario mínimo y auxilio de transporte 2026';
                UPDATE dbo.[PAY_LegalParameters] SET [Source] = N'Decreto 1470 de 2025 (auxilio de transporte 2026)'
                WHERE [Code] = 'AUX_TRANSPORTE' AND [Source] = N'Decreto de salario mínimo y auxilio de transporte 2026';
                UPDATE dbo.[PAY_LegalParameters] SET [Source] = N'Resolución DIAN 000238 de 2025 (UVT 2026)'
                WHERE [Code] = 'UVT' AND [Source] = N'Resolución DIAN que fija la UVT 2026';
                UPDATE dbo.[PAY_LegalParameters] SET [Source] = N'Ley 2466 de 2025 art. 21'
                WHERE [Code] = 'SALUD_APRENDIZ_PCT' AND [Source] = N'Ley 789 de 2002 art. 30 y Decreto 933 de 2003';
                UPDATE dbo.[PAY_LegalParameters] SET [Source] = N'Ley 797 de 2003 art. 8'
                WHERE [Code] = 'FSP_TABLA' AND [Source] = N'Ley 100 de 1993, Ley 797 de 2003 y Decreto 1072 de 2015';
                """);

            // (4) La tabla del fondo de solidaridad de la Ley 797 rige hasta el 2027-03-31 (Ley 2381 de 2024
            //     desde abril; la nueva la trae la semilla en Revisiones()). Sólo si la vigente sigue abierta.
            migrationBuilder.Sql("""
                UPDATE dbo.[PAY_LegalParameters] SET [ValidTo] = '2027-03-31'
                WHERE [Code] = 'FSP_TABLA' AND [ValidTo] IS NULL AND [IsDeleted] = 0
                  AND [ValidFrom] < '2027-04-01';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Guarda (D-13): con una corrida especial (PayPeriodId NULL) el índice único por período no se
            // puede recrear, y borrar las tablas nuevas se llevaría primas, cesantías, vacaciones y
            // definitivas aprobadas. Se detiene aquí, antes de tocar nada; quien revierta decide.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.[PAY_PayrollRuns] WHERE [Kind] <> 0)
                    THROW 50010, N'NominaPrestacionesYDian: existen corridas con Kind distinto de 0 (prima, cesantias, vacaciones o definitiva). La migracion solo es reversible sin liquidaciones especiales (feature 010, D-13). Respalde, reverse o descarte esas corridas con el dueno y decida antes de continuar.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_Employees_COR_Banks_DisbursementBankId",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_EmployeeWithholdingRates_PAY_WithholdingRateCalculations_SourceCalculationId",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_Novelties_PAY_VacationMovements_VacationMovementId",
                schema: "dbo",
                table: "PAY_Novelties");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRunLines_PAY_SettlementDeductions_SettlementDeductionId",
                schema: "dbo",
                table: "PAY_PayrollRunLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_PAY_Employees_EmployeeId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_PAY_EmploymentTerminations_TerminationId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_PAY_VacationMovements_VacationMovementId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropTable(
                name: "PAY_CompanyPolicies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_EmployeeBenefitOpeningBalances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_Holidays",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_SettlementDeductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_SeveranceFundDeposits",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_VacationMovements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_WithholdingRateCalculationMonths",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_EmploymentTerminations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_WithholdingRateCalculations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_TerminationReasons",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PayrollRuns_Kind_Status",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PayrollRuns_TerminationId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PayrollRuns_VacationMovementId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "UK_PAY_PayrollRuns_Ordinary_Period_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "UK_PAY_PayrollRuns_ServiceBonus_Year_Semester_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "UK_PAY_PayrollRuns_Settlement_Employee_Cutoff_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "UK_PAY_PayrollRuns_Severance_Year_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "UK_PAY_PayrollRuns_Vacation_Movement_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PayrollRunLines_SettlementDeductionId",
                schema: "dbo",
                table: "PAY_PayrollRunLines");

            migrationBuilder.DropIndex(
                name: "IX_PAY_Novelties_VacationMovement",
                schema: "dbo",
                table: "PAY_Novelties");

            migrationBuilder.DropIndex(
                name: "IX_PAY_EmployeeWithholdingRates_SourceCalculationId",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates");

            migrationBuilder.DropIndex(
                name: "IX_PAY_Employees_DisbursementBank",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "CutoffDate",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PayDate",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "Semester",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "TerminationId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "VacationMovementId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "Year",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "SettlementDeductionId",
                schema: "dbo",
                table: "PAY_PayrollRunLines");

            migrationBuilder.DropColumn(
                name: "VacationMovementId",
                schema: "dbo",
                table: "PAY_Novelties");

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates");

            migrationBuilder.DropColumn(
                name: "SourceCalculationId",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates");

            migrationBuilder.DropColumn(
                name: "ApprenticeStage",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "ColombianAbroad",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "DianContractType",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "DianPaymentMethodCode",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "DisbursementBankId",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "EconomicActivityCode",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "ForeignNotRequiredToContributePension",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "HighRiskPension",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "PensionTransitionRegime",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "PilaContributorSubType",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "PilaContributorType",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "WorkAddress",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "WorkCenterCode",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "WorkMunicipalityDaneCode",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "AffectsVacationBase",
                schema: "dbo",
                table: "PAY_ConceptDefinitions");

            migrationBuilder.DropColumn(
                name: "DianElement",
                schema: "dbo",
                table: "PAY_ConceptDefinitions");

            migrationBuilder.DropColumn(
                name: "OtherNames",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.DropColumn(
                name: "SecondLastName",
                schema: "dbo",
                table: "COR_People");

            migrationBuilder.AlterColumn<int>(
                name: "PayPeriodId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_Period_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "PayPeriodId", "Version" },
                unique: true);
        }
    }
}
