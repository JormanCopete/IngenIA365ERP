using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Feature 005 — novedades y liquidación de nómina. Crea las 14 tablas nuevas
    /// (<c>PAY_PayrollPlans</c>, <c>PAY_ConceptDefinitions</c>,
    /// <c>PAY_ConceptDefinitionAccounts</c>, <c>PAY_LegalParameters</c>,
    /// <c>PAY_LegalParameterRanges</c>, <c>PAY_Novelties</c>, <c>PAY_RecurringNovelties</c>,
    /// <c>PAY_EmployeeWithholdingRates</c>, <c>PAY_EmployeeTaxDeductions</c>,
    /// <c>PAY_PayrollRuns</c>, <c>PAY_PayrollRunEmployees</c>, <c>PAY_PayrollRunLines</c>,
    /// <c>PAY_PayrollPayments</c>, <c>PAY_PayslipDeliveries</c>) y añade a
    /// <c>PAY_PayPeriods</c> y <c>PAY_Employees</c> las columnas del plan de nómina,
    /// la aprobación y el procedimiento de retención.
    ///
    /// <para>
    /// <b>Lleva un relleno de datos dentro del DDL, y es a propósito.</b>
    /// <c>PayrollPlanId</c> es NOT NULL con clave foránea a <c>PAY_PayrollPlans</c>.
    /// En una base con períodos o empleados ya registrados, la columna nace con 0 y
    /// la clave foránea no se puede crear hasta que cada fila apunte a un plan real.
    /// Por eso, entre añadir la columna y crear la clave, el Up inserta el plan
    /// <c>DEFAULT</c> si no existe y rellena el 0 con su Id. No es una carga de
    /// negocio: es lo que hace válido el esquema que esta misma migración declara.
    /// La semilla <c>PayrollPlansSeeder</c> hace lo mismo para las cooperativas
    /// nuevas, que nacen sin filas y no pasan por aquí.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente</b>: el INSERT lleva <c>NOT EXISTS</c> y los UPDATE sólo tocan
    /// filas con <c>PayrollPlanId = 0</c>, que después de la clave foránea no pueden
    /// existir. <b>Reversible</b>: el Down retira columnas y tablas; el plan
    /// <c>DEFAULT</c> se va con su tabla, así que no hay nada más que deshacer.
    /// </para>
    ///
    /// <para>
    /// Las tareas T026–T029 de la feature preveían cuatro migraciones. Van en una
    /// porque <c>dotnet ef migrations add</c> diferencia contra el snapshot completo
    /// y partirla exigiría fabricar a mano tres snapshots intermedios.
    /// </para>
    /// </summary>
    public partial class NominaNovedadesYLiquidacion : Migration
    {
        /// <summary>Autor del plan por defecto cuando lo crea esta migración y no la semilla.</summary>
        private const string MarcaDeOrigen = "migracion:NominaNovedadesYLiquidacion";
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "dbo",
                table: "PAY_PayPeriods",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                schema: "dbo",
                table: "PAY_PayPeriods",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollPlanId",
                schema: "dbo",
                table: "PAY_PayPeriods",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RunPublicId",
                schema: "dbo",
                table: "PAY_PayPeriods",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeClass",
                schema: "dbo",
                table: "PAY_Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayrollPlanEffectiveFrom",
                schema: "dbo",
                table: "PAY_Employees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayrollPlanId",
                schema: "dbo",
                table: "PAY_Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte>(
                name: "WithholdingProcedure",
                schema: "dbo",
                table: "PAY_Employees",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.CreateTable(
                name: "PAY_ConceptDefinitionAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConceptCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CostCenterId = table.Column<int>(type: "integer", nullable: true),
                    DebitAccountId = table.Column<int>(type: "integer", nullable: false),
                    CreditAccountId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PAY_ConceptDefinitionAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_ConceptDefinitionAccounts_ACC_ChartOfAccounts_CreditAcc~",
                        column: x => x.CreditAccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_ConceptDefinitionAccounts_ACC_ChartOfAccounts_DebitAcco~",
                        column: x => x.DebitAccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_ConceptDefinitionAccounts_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_ConceptDefinitions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Nature = table.Column<int>(type: "integer", nullable: false),
                    CalculationKind = table.Column<int>(type: "integer", nullable: false),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountParameterCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProrateByDays = table.Column<bool>(type: "boolean", nullable: false),
                    BaseKind = table.Column<int>(type: "integer", nullable: true),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    PercentParameterCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    UnitKind = table.Column<int>(type: "integer", nullable: true),
                    UnitFactor = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    TableParameterCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ComponentConceptCodes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    AffectsSalaryBase = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsContributionBase = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsBenefitsBase = table.Column<bool>(type: "boolean", nullable: false),
                    AffectsWithholdingBase = table.Column<bool>(type: "boolean", nullable: false),
                    IsBenefitRelated = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsRepeatInPeriod = table.Column<bool>(type: "boolean", nullable: false),
                    MaxQuantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ApplicableClasses = table.Column<int>(type: "integer", nullable: false),
                    RequiresDates = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresQuantity = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAmount = table.Column<bool>(type: "boolean", nullable: false),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    ReducesWorkedDays = table.Column<bool>(type: "boolean", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    LegacyConceptId = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_PAY_ConceptDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_EmployeeTaxDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Percent = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_PAY_EmployeeTaxDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_EmployeeTaxDeductions_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_EmployeeWithholdingRates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_PAY_EmployeeWithholdingRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_EmployeeWithholdingRates_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_LegalParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_PAY_LegalParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PayrollPlans",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Periodicity = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_PAY_PayrollPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PayrollRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayPeriodId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CalculatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    InputsHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    TotalEarnings = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEmployerContributions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalProvisions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalNet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RoundingAdjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccountingDocumentId = table.Column<int>(type: "integer", nullable: true),
                    ReversalAccountingDocumentId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedWithoutSegregation = table.Column<bool>(type: "boolean", nullable: false),
                    ExceptionsJson = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_PAY_PayrollRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollRuns_PAY_PayPeriods_PayPeriodId",
                        column: x => x.PayPeriodId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_RecurringNovelties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ConceptCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalInstallments = table.Column<int>(type: "integer", nullable: true),
                    InstallmentsIssued = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeactivationReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_PAY_RecurringNovelties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_RecurringNovelties_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_Novelties",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayPeriodId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ConceptDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    ConceptCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DaysInPeriod = table.Column<int>(type: "integer", nullable: false),
                    CarryOverDays = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    SupersedesNoveltyId = table.Column<int>(type: "integer", nullable: true),
                    RecurringNoveltyId = table.Column<int>(type: "integer", nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetroactiveOfPeriodId = table.Column<int>(type: "integer", nullable: true),
                    LoanPortfolioId = table.Column<int>(type: "integer", nullable: true),
                    CarriedFromNoveltyId = table.Column<int>(type: "integer", nullable: true),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: true),
                    InstallmentTotal = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_PAY_Novelties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_Novelties_PAY_ConceptDefinitions_ConceptDefinitionId",
                        column: x => x.ConceptDefinitionId,
                        principalSchema: "dbo",
                        principalTable: "PAY_ConceptDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_Novelties_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_Novelties_PAY_PayPeriods_PayPeriodId",
                        column: x => x.PayPeriodId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_LegalParameterRanges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LegalParameterId = table.Column<int>(type: "integer", nullable: false),
                    FromValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ToValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    FixedValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PAY_LegalParameterRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_LegalParameterRanges_PAY_LegalParameters_LegalParameter~",
                        column: x => x.LegalParameterId,
                        principalSchema: "dbo",
                        principalTable: "PAY_LegalParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PayrollRunEmployees",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollRunId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    PayrollPlanId = table.Column<int>(type: "integer", nullable: false),
                    DaysWorked = table.Column<int>(type: "integer", nullable: false),
                    SalaryTranchesJson = table.Column<string>(type: "text", nullable: false),
                    EmployeeClass = table.Column<int>(type: "integer", nullable: false),
                    TotalEarnings = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEmployerContributions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalProvisions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Flags = table.Column<int>(type: "integer", nullable: false),
                    ChangedFromPreviousRun = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_PAY_PayrollRunEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollRunEmployees_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollRunEmployees_PAY_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PayrollPayments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollRunEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PaidBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsReverted = table.Column<bool>(type: "boolean", nullable: false),
                    RevertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevertedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RevertReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_PAY_PayrollPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollPayments_PAY_PayrollRunEmployees_PayrollRunEmplo~",
                        column: x => x.PayrollRunEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRunEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PayrollRunLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollRunEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ConceptDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    ConceptCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ConceptName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Nature = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Factor = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    RangeFrom = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    RangeTo = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LegalParameterId = table.Column<int>(type: "integer", nullable: true),
                    NoveltyId = table.Column<int>(type: "integer", nullable: true),
                    ExplanationJson = table.Column<string>(type: "text", nullable: false),
                    AffectsAccounting = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PAY_PayrollRunLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PayrollRunLines_PAY_PayrollRunEmployees_PayrollRunEmplo~",
                        column: x => x.PayrollRunEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRunEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PayslipDeliveries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollRunEmployeeId = table.Column<int>(type: "integer", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PAY_PayslipDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PayslipDeliveries_PAY_PayrollRunEmployees_PayrollRunEmp~",
                        column: x => x.PayrollRunEmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PayrollRunEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayPeriods_Plan_StartDate",
                schema: "dbo",
                table: "PAY_PayPeriods",
                columns: new[] { "PayrollPlanId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Employees_PayrollPlan",
                schema: "dbo",
                table: "PAY_Employees",
                column: "PayrollPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ConceptDefinitionAccounts_CostCenterId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ConceptDefinitionAccounts_CreditAccountId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts",
                column: "CreditAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ConceptDefinitionAccounts_DebitAccountId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts",
                column: "DebitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ConceptDefinitionAccounts_PublicId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PAY_ConceptDefinitionAccounts_CostCenter",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts",
                columns: new[] { "ConceptCode", "CostCenterId" },
                unique: true,
                filter: "\"CostCenterId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PAY_ConceptDefinitionAccounts_Default",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts",
                column: "ConceptCode",
                unique: true,
                filter: "\"CostCenterId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ConceptDefinitions_Code_ValidTo",
                schema: "dbo",
                table: "PAY_ConceptDefinitions",
                columns: new[] { "Code", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ConceptDefinitions_PublicId",
                schema: "dbo",
                table: "PAY_ConceptDefinitions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ConceptDefinitions_Code_ValidFrom",
                schema: "dbo",
                table: "PAY_ConceptDefinitions",
                columns: new[] { "Code", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmployeeTaxDeductions_Employee_Kind_ValidFrom",
                schema: "dbo",
                table: "PAY_EmployeeTaxDeductions",
                columns: new[] { "EmployeeId", "Kind", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmployeeTaxDeductions_PublicId",
                schema: "dbo",
                table: "PAY_EmployeeTaxDeductions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmployeeWithholdingRates_Employee_ValidFrom",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates",
                columns: new[] { "EmployeeId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_EmployeeWithholdingRates_PublicId",
                schema: "dbo",
                table: "PAY_EmployeeWithholdingRates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_LegalParameterRanges_Parameter_Order",
                schema: "dbo",
                table: "PAY_LegalParameterRanges",
                columns: new[] { "LegalParameterId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_LegalParameterRanges_PublicId",
                schema: "dbo",
                table: "PAY_LegalParameterRanges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_LegalParameters_PublicId",
                schema: "dbo",
                table: "PAY_LegalParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_LegalParameters_Code_ValidFrom",
                schema: "dbo",
                table: "PAY_LegalParameters",
                columns: new[] { "Code", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_ConceptDefinitionId",
                schema: "dbo",
                table: "PAY_Novelties",
                column: "ConceptDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_EmployeeId",
                schema: "dbo",
                table: "PAY_Novelties",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_ImportBatch",
                schema: "dbo",
                table: "PAY_Novelties",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_Period_Concept",
                schema: "dbo",
                table: "PAY_Novelties",
                columns: new[] { "PayPeriodId", "ConceptDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_Period_Employee_Status",
                schema: "dbo",
                table: "PAY_Novelties",
                columns: new[] { "PayPeriodId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_Novelties_PublicId",
                schema: "dbo",
                table: "PAY_Novelties",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollPayments_PublicId",
                schema: "dbo",
                table: "PAY_PayrollPayments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PAY_PayrollPayments_RunEmployee_Vigente",
                schema: "dbo",
                table: "PAY_PayrollPayments",
                column: "PayrollRunEmployeeId",
                unique: true,
                filter: "\"IsReverted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollPlans_PublicId",
                schema: "dbo",
                table: "PAY_PayrollPlans",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollPlans_Code",
                schema: "dbo",
                table: "PAY_PayrollPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PAY_PayrollPlans_Default",
                schema: "dbo",
                table: "PAY_PayrollPlans",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRunEmployees_EmployeeId",
                schema: "dbo",
                table: "PAY_PayrollRunEmployees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRunEmployees_PublicId",
                schema: "dbo",
                table: "PAY_PayrollRunEmployees",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRunEmployees_Run_Employee",
                schema: "dbo",
                table: "PAY_PayrollRunEmployees",
                columns: new[] { "PayrollRunId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRunLines_ConceptCode",
                schema: "dbo",
                table: "PAY_PayrollRunLines",
                column: "ConceptCode");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRunLines_PublicId",
                schema: "dbo",
                table: "PAY_PayrollRunLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRunLines_RunEmployee_Order",
                schema: "dbo",
                table: "PAY_PayrollRunLines",
                columns: new[] { "PayrollRunEmployeeId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRuns_Period_Status",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "PayPeriodId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayrollRuns_PublicId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PayrollRuns_Period_Version",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                columns: new[] { "PayPeriodId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayslipDeliveries_PublicId",
                schema: "dbo",
                table: "PAY_PayslipDeliveries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PayslipDeliveries_RunEmployee",
                schema: "dbo",
                table: "PAY_PayslipDeliveries",
                column: "PayrollRunEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_RecurringNovelties_Employee_Active",
                schema: "dbo",
                table: "PAY_RecurringNovelties",
                columns: new[] { "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_RecurringNovelties_PublicId",
                schema: "dbo",
                table: "PAY_RecurringNovelties",
                column: "PublicId",
                unique: true);

            // Ver el encabezado de la clase: el plan por defecto tiene que existir y las
            // filas ya registradas tienen que apuntar a él ANTES de crear las claves
            // foráneas de abajo. gen_random_uuid() está en el core desde PostgreSQL 13.
            migrationBuilder.Sql($"""
                INSERT INTO "dbo"."PAY_PayrollPlans"
                    ("Code", "Name", "Periodicity", "IsDefault", "IsActive",
                     "PublicId", "IsDeleted", "CreatedAt", "CreatedBy")
                SELECT 'DEFAULT', 'Nómina general', 30, TRUE, TRUE,
                       gen_random_uuid(), FALSE, now() at time zone 'utc', '{MarcaDeOrigen}'
                WHERE NOT EXISTS (SELECT 1 FROM "dbo"."PAY_PayrollPlans" WHERE "IsDefault" = TRUE);

                UPDATE "dbo"."PAY_PayPeriods"
                   SET "PayrollPlanId" = (SELECT "Id" FROM "dbo"."PAY_PayrollPlans" WHERE "IsDefault" = TRUE LIMIT 1)
                 WHERE "PayrollPlanId" = 0;

                UPDATE "dbo"."PAY_Employees"
                   SET "PayrollPlanId" = (SELECT "Id" FROM "dbo"."PAY_PayrollPlans" WHERE "IsDefault" = TRUE LIMIT 1)
                 WHERE "PayrollPlanId" = 0;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_Employees_PAY_PayrollPlans_PayrollPlanId",
                schema: "dbo",
                table: "PAY_Employees",
                column: "PayrollPlanId",
                principalSchema: "dbo",
                principalTable: "PAY_PayrollPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayPeriods_PAY_PayrollPlans_PayrollPlanId",
                schema: "dbo",
                table: "PAY_PayPeriods",
                column: "PayrollPlanId",
                principalSchema: "dbo",
                principalTable: "PAY_PayrollPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PAY_Employees_PAY_PayrollPlans_PayrollPlanId",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayPeriods_PAY_PayrollPlans_PayrollPlanId",
                schema: "dbo",
                table: "PAY_PayPeriods");

            migrationBuilder.DropTable(
                name: "PAY_ConceptDefinitionAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_EmployeeTaxDeductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_EmployeeWithholdingRates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_LegalParameterRanges",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_Novelties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PayrollPayments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PayrollPlans",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PayrollRunLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PayslipDeliveries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_RecurringNovelties",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_LegalParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_ConceptDefinitions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PayrollRunEmployees",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PayrollRuns",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PayPeriods_Plan_StartDate",
                schema: "dbo",
                table: "PAY_PayPeriods");

            migrationBuilder.DropIndex(
                name: "IX_PAY_Employees_PayrollPlan",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "dbo",
                table: "PAY_PayPeriods");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "dbo",
                table: "PAY_PayPeriods");

            migrationBuilder.DropColumn(
                name: "PayrollPlanId",
                schema: "dbo",
                table: "PAY_PayPeriods");

            migrationBuilder.DropColumn(
                name: "RunPublicId",
                schema: "dbo",
                table: "PAY_PayPeriods");

            migrationBuilder.DropColumn(
                name: "EmployeeClass",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "PayrollPlanEffectiveFrom",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "PayrollPlanId",
                schema: "dbo",
                table: "PAY_Employees");

            migrationBuilder.DropColumn(
                name: "WithholdingProcedure",
                schema: "dbo",
                table: "PAY_Employees");
        }
    }
}
