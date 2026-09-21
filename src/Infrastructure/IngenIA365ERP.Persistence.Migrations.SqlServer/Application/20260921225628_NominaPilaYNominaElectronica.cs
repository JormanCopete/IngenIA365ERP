using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 010, entrega N2 (PILA y procedimiento 2) más las tablas de nómina electrónica de N3
    /// (D-12: una sola migración para N2+N3). Migración <b>aditiva</b> y reversible: ocho tablas nuevas
    /// (<c>PAY_PilaSettings</c>, <c>PAY_PilaGenerations</c>, <c>PAY_PilaGenerationLines</c>, <c>PAY_PilaIssues</c>,
    /// <c>PAY_ElectronicPayrollSettings</c>, <c>PAY_ElectronicPayrollNumberingRanges</c>, <c>PAY_ElectronicPayrollDocuments</c>,
    /// <c>PAY_ElectronicPayrollTransmissions</c>), <c>PilaCode</c> en los cinco catálogos institucionales y
    /// <c>IsAccai</c> en pensiones. No borra nada hacia adelante: sin marcador de destructiva (D-13); producción
    /// exige igual respaldo por cooperativa (<c>pg_dump -Fc</c>) y segundo revisor (Principio XII).
    /// </summary>
    public partial class NominaPilaYNominaElectronica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAccai",
                schema: "dbo",
                table: "PAY_PensionProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_PensionProviders",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PAY_ElectronicPayrollNumberingRanges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentType = table.Column<short>(type: "smallint", nullable: false),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RangeFrom = table.Column<long>(type: "bigint", nullable: false),
                    RangeTo = table.Column<long>(type: "bigint", nullable: false),
                    LastIssuedNumber = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_PAY_ElectronicPayrollNumberingRanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_ElectronicPayrollSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployerTaxId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    EmployerCheckDigit = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    EmployerBusinessName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmployerMunicipalityDaneCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    EmployerAddress = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EmployerCountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    SoftwareId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    TestSetId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    TestSetStatus = table.Column<int>(type: "int", nullable: false),
                    TestSetAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TestSetResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CertificateSecretName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PinSecretName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CertificateThumbprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CertificateExpiresAt = table.Column<DateOnly>(type: "date", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EnabledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnabledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_PAY_ElectronicPayrollSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PilaGenerations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LayoutVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExemptionApplied = table.Column<bool>(type: "bit", nullable: false),
                    ContributorCount = table.Column<int>(type: "int", nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    TotalIbcHealth = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIbcPension = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIbcWorkRisk = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIbcFamilyCompensation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalHealth = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPension = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSolidarityFund = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalWorkRisk = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalFamilyCompensation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSena = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIcbf = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReconciliationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Balanced = table.Column<bool>(type: "bit", nullable: false),
                    SourceRunsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FileAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BlockingIssueCount = table.Column<int>(type: "int", nullable: false),
                    WarningCount = table.Column<int>(type: "int", nullable: false),
                    ProposedPaymentDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OperatorFilingNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OperatorFilingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaidAt = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_PAY_PilaGenerations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PilaSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContributorType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ContributorClass = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    PresentationForm = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BranchName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ArlPilaCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    MunicipalityDaneCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    EconomicActivityCode = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    OperatorName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    OperatorCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PlanillaType = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
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
                    table.PrimaryKey("PK_PAY_PilaSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PAY_ElectronicPayrollDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: false),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
                    DocumentType = table.Column<short>(type: "smallint", nullable: false),
                    NoteType = table.Column<int>(type: "int", nullable: true),
                    AdjustsDocumentId = table.Column<int>(type: "int", nullable: true),
                    ReplacedByDocumentId = table.Column<int>(type: "int", nullable: true),
                    NumberingRangeId = table.Column<int>(type: "int", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Consecutive = table.Column<long>(type: "bigint", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    GenerationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GenerationTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Cune = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastTransmissionId = table.Column<int>(type: "int", nullable: true),
                    TotalAccrued = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalNet = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WorkedDays = table.Column<int>(type: "int", nullable: false),
                    PaymentDatesJson = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SourceRunsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UnsignedXmlAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignedXmlAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ZipAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApplicationResponseAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GraphicPdfAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ZipKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DianStatusCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DianStatusDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TranslatedErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QrUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_PAY_ElectronicPayrollDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_ElectronicPayrollDocuments_PAY_ElectronicPayrollDocuments_AdjustsDocumentId",
                        column: x => x.AdjustsDocumentId,
                        principalSchema: "dbo",
                        principalTable: "PAY_ElectronicPayrollDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_ElectronicPayrollDocuments_PAY_ElectronicPayrollNumberingRanges_NumberingRangeId",
                        column: x => x.NumberingRangeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_ElectronicPayrollNumberingRanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_ElectronicPayrollDocuments_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PilaGenerationLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GenerationId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ContributorType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ContributorSubType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    NoveltyFlags = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DaysHealth = table.Column<byte>(type: "tinyint", nullable: false),
                    DaysPension = table.Column<byte>(type: "tinyint", nullable: false),
                    DaysWorkRisk = table.Column<byte>(type: "tinyint", nullable: false),
                    DaysFamilyCompensation = table.Column<byte>(type: "tinyint", nullable: false),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IbcHealth = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IbcPension = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IbcWorkRisk = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IbcFamilyCompensation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HealthRate = table.Column<decimal>(type: "decimal(9,7)", precision: 9, scale: 7, nullable: false),
                    PensionRate = table.Column<decimal>(type: "decimal(9,7)", precision: 9, scale: 7, nullable: false),
                    WorkRiskRate = table.Column<decimal>(type: "decimal(9,7)", precision: 9, scale: 7, nullable: false),
                    FamilyCompensationRate = table.Column<decimal>(type: "decimal(9,7)", precision: 9, scale: 7, nullable: false),
                    SenaRate = table.Column<decimal>(type: "decimal(9,7)", precision: 9, scale: 7, nullable: false),
                    IcbfRate = table.Column<decimal>(type: "decimal(9,7)", precision: 9, scale: 7, nullable: false),
                    Health = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Pension = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SolidarityFund = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SubsistenceFund = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WorkRisk = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FamilyCompensation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sena = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Icbf = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Exempt = table.Column<bool>(type: "bit", nullable: false),
                    FieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordText = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
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
                    table.PrimaryKey("PK_PAY_PilaGenerationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PilaGenerationLines_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_PilaGenerationLines_PAY_PilaGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PilaGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAY_PilaIssues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GenerationId = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FieldNumber = table.Column<byte>(type: "tinyint", nullable: true),
                    EmployeeId = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LinkRoute = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_PAY_PilaIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_PilaIssues_PAY_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "dbo",
                        principalTable: "PAY_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAY_PilaIssues_PAY_PilaGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalSchema: "dbo",
                        principalTable: "PAY_PilaGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAY_ElectronicPayrollTransmissions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    Environment = table.Column<int>(type: "int", nullable: false),
                    ServiceCorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceHttpStatus = table.Column<short>(type: "smallint", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DianStatusCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsValid = table.Column<bool>(type: "bit", nullable: true),
                    StatusDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    StatusMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RawErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TranslatedErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_PAY_ElectronicPayrollTransmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PAY_ElectronicPayrollTransmissions_PAY_ElectronicPayrollDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "PAY_ElectronicPayrollDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UK_PAY_WorkRiskProviders_PilaCode",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                column: "PilaCode",
                unique: true,
                filter: "[PilaCode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SeveranceProviders_PilaCode",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                column: "PilaCode",
                unique: true,
                filter: "[PilaCode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PensionProviders_PilaCode",
                schema: "dbo",
                table: "PAY_PensionProviders",
                column: "PilaCode",
                unique: true,
                filter: "[PilaCode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_HealthInsuranceProviders_PilaCode",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                column: "PilaCode",
                unique: true,
                filter: "[PilaCode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_FamilyCompensationFunds_PilaCode",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                column: "PilaCode",
                unique: true,
                filter: "[PilaCode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ElectronicPayrollDocuments_AdjustsDocumentId",
                schema: "dbo",
                table: "PAY_ElectronicPayrollDocuments",
                column: "AdjustsDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ElectronicPayrollDocuments_Employee_Period_Type",
                schema: "dbo",
                table: "PAY_ElectronicPayrollDocuments",
                columns: new[] { "EmployeeId", "Year", "Month", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ElectronicPayrollDocuments_NumberingRangeId",
                schema: "dbo",
                table: "PAY_ElectronicPayrollDocuments",
                column: "NumberingRangeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_ElectronicPayrollDocuments_Status",
                schema: "dbo",
                table: "PAY_ElectronicPayrollDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ElectronicPayrollDocuments_Cune",
                schema: "dbo",
                table: "PAY_ElectronicPayrollDocuments",
                column: "Cune",
                unique: true,
                filter: "[Cune] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ElectronicPayrollDocuments_Env_Prefix_Consecutive",
                schema: "dbo",
                table: "PAY_ElectronicPayrollDocuments",
                columns: new[] { "Environment", "Prefix", "Consecutive" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ElectronicPayrollDocuments_PublicId",
                schema: "dbo",
                table: "PAY_ElectronicPayrollDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ElectronicPayrollNumberingRanges_PublicId",
                schema: "dbo",
                table: "PAY_ElectronicPayrollNumberingRanges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ElectronicPayrollNumberingRanges_Type_Env_Prefix_From",
                schema: "dbo",
                table: "PAY_ElectronicPayrollNumberingRanges",
                columns: new[] { "DocumentType", "Environment", "Prefix", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ElectronicPayrollSettings_PublicId",
                schema: "dbo",
                table: "PAY_ElectronicPayrollSettings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ElectronicPayrollTransmissions_Document_Attempt",
                schema: "dbo",
                table: "PAY_ElectronicPayrollTransmissions",
                columns: new[] { "DocumentId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_ElectronicPayrollTransmissions_PublicId",
                schema: "dbo",
                table: "PAY_ElectronicPayrollTransmissions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PilaGenerationLines_EmployeeId",
                schema: "dbo",
                table: "PAY_PilaGenerationLines",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PilaGenerationLines_Generation_Employee",
                schema: "dbo",
                table: "PAY_PilaGenerationLines",
                columns: new[] { "GenerationId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PilaGenerationLines_Generation_Line",
                schema: "dbo",
                table: "PAY_PilaGenerationLines",
                columns: new[] { "GenerationId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PilaGenerationLines_PublicId",
                schema: "dbo",
                table: "PAY_PilaGenerationLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PilaGenerations_Period_Status",
                schema: "dbo",
                table: "PAY_PilaGenerations",
                columns: new[] { "Year", "Month", "Status" });

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PilaGenerations_Period_Version",
                schema: "dbo",
                table: "PAY_PilaGenerations",
                columns: new[] { "Year", "Month", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PilaGenerations_PublicId",
                schema: "dbo",
                table: "PAY_PilaGenerations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PilaIssues_EmployeeId",
                schema: "dbo",
                table: "PAY_PilaIssues",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PilaIssues_Generation_Severity",
                schema: "dbo",
                table: "PAY_PilaIssues",
                columns: new[] { "GenerationId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PilaIssues_PublicId",
                schema: "dbo",
                table: "PAY_PilaIssues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_PAY_PilaSettings_PublicId",
                schema: "dbo",
                table: "PAY_PilaSettings",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PAY_ElectronicPayrollSettings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_ElectronicPayrollTransmissions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PilaGenerationLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PilaIssues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PilaSettings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_ElectronicPayrollDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_PilaGenerations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PAY_ElectronicPayrollNumberingRanges",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_PAY_WorkRiskProviders_PilaCode",
                schema: "dbo",
                table: "PAY_WorkRiskProviders");

            migrationBuilder.DropIndex(
                name: "UK_PAY_SeveranceProviders_PilaCode",
                schema: "dbo",
                table: "PAY_SeveranceProviders");

            migrationBuilder.DropIndex(
                name: "UK_PAY_PensionProviders_PilaCode",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropIndex(
                name: "UK_PAY_HealthInsuranceProviders_PilaCode",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders");

            migrationBuilder.DropIndex(
                name: "UK_PAY_FamilyCompensationFunds_PilaCode",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds");

            migrationBuilder.DropColumn(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_WorkRiskProviders");

            migrationBuilder.DropColumn(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_SeveranceProviders");

            migrationBuilder.DropColumn(
                name: "IsAccai",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropColumn(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropColumn(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders");

            migrationBuilder.DropColumn(
                name: "PilaCode",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds");
        }
    }
}
