using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 009 — contabilidad NIIF (specs/009-contabilidad-niif/plan.md, data-model.md §9).
    ///
    /// <para>
    /// MIGRACION-DESTRUCTIVA-APROBADA (Principio XII). Reemplaza el modelo contable heredado de SOLIDO
    /// —33 tablas <c>ACC_*</c>— por las 29 del modelo nuevo. Los libros estaban vacíos en DEV, QA y
    /// producción el 2026-09-14 (<c>diagnostico-libros.sql</c>: 0 documentos, 0 movimientos, 9 cuentas
    /// heredadas), y la guarda del <c>Up()</c> se niega a correr si ya no lo están. No se migran
    /// movimientos de SOLIDO: la apertura se carga o se digita (clarificación Q1).
    /// </para>
    ///
    /// <para>
    /// Respaldo: <c>pg_dump</c> de cada base de cooperativa inmediatamente antes de aplicar, en cada
    /// ambiente (runbook de despliegue; en DEV el respaldo es el propio contenedor). Segundo revisor:
    /// quien el dueño designe en la revisión del PR de la feature 009 (T095); su nombre se anota aquí
    /// antes del merge a <c>develop</c>.
    /// </para>
    ///
    /// <para>
    /// Qué hace, en orden: (1) guarda; (2) vacía <c>PAY_ConceptDefinitionAccounts</c>, que apuntaba por
    /// FK a cuentas del plan heredado que desaparecen (se reparametriza tras iniciar la contabilidad,
    /// <c>docs/operaciones/nomina-primer-periodo.md</c>); (3) suelta las FK de <c>PAY_PayrollRuns</c> y
    /// <c>PAY_ConceptDefinitionAccounts</c> hacia las tablas heredadas; (4) borra las 26 tablas heredadas
    /// que cambian de nombre (generado) y las 7 que lo conservan (<c>ACC_JournalEntries</c>,
    /// <c>ACC_Documents</c>, <c>ACC_Budgets</c>, <c>ACC_BankReconciliations</c>, <c>ACC_ChartOfAccounts</c>,
    /// <c>ACC_VoucherTypes</c>, <c>ACC_AccountingPeriods</c>, a mano: el generador las habría alterado
    /// columna a columna sobre las 9 cuentas heredadas y el índice único de <c>Code</c> habría reventado);
    /// (5) crea las 29 tablas nuevas, las columnas de otros módulos (R7, R16, R17) y las FK.
    /// </para>
    ///
    /// <para>
    /// <c>Down()</c> deshace lo creado y recrea vacías las 26 tablas heredadas que conocía el snapshot;
    /// las 7 de nombre conservado no se recrean (no tenían datos; su definición heredada ya no existe en
    /// el modelo). Es irreversible en datos por definición: no había.
    /// </para>
    /// </summary>
    public partial class ContabilidadNiif : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // (1) Guarda: sólo sobre libros vacíos. Si alguien contabilizó algo entre el diagnóstico y el
            //     despliegue, esto se detiene aquí y nadie pierde un movimiento sin decidirlo.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [dbo].[ACC_JournalEntries]) OR EXISTS (SELECT 1 FROM [dbo].[ACC_Documents])
                    THROW 50009, N'ContabilidadNiif: ACC_Documents o ACC_JournalEntries tienen filas. La migracion reemplaza el modelo contable y solo se aplica sobre libros vacios (feature 009). Respalde, revise con el dueno y decida antes de continuar.', 1;
                """);

            // (2) Las cuentas por concepto de nómina apuntaban a cuentas del plan heredado, que desaparece:
            //     se vacían (borrado físico: la FK nueva no admitiría filas colgadas) y se reparametrizan
            //     tras iniciar la contabilidad (docs/operaciones/nomina-primer-periodo.md).
            migrationBuilder.Sql("DELETE FROM [dbo].[PAY_ConceptDefinitionAccounts];");

            // (3) FKs desde tablas que siguen hacia tablas heredadas que se borran.
            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_ConceptDefinitionAccounts_ACC_ChartOfAccounts_CreditAccountId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_ConceptDefinitionAccounts_ACC_ChartOfAccounts_DebitAccountId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts");

            migrationBuilder.DropTable(
                name: "ACC_AccountBalances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_AccountSubgroups",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_Amortizations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_AuxiliaryDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_BankReconciliationFlats",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_BankReconciliationMasters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_Depreciations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_DianReportFormats",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ExchangeRateHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_FinancialReportParams",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_FinancialReports",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_FinancialReportValues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_FiscalPeriods",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_GmfTaxLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_GroupNames",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_IcaTaxLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_IncomeTaxLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_JournalEntryItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_RiskCategories",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_StampTaxes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_SubgroupNames",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_TaxFormCodes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ThirdPartyAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_VatTaxLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_WithholdingTaxLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_AccountGroups",
                schema: "dbo");

            migrationBuilder.AddColumn<int>(
                name: "CreditAccountId",
                schema: "dbo",
                table: "TRS_Concepts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DebitAccountId",
                schema: "dbo",
                table: "TRS_Concepts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_PensionProviders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterestExpenseAccount",
                schema: "dbo",
                table: "LND_SavingsParameters",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvisionAccountCode",
                schema: "dbo",
                table: "LND_CreditLineParameters",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvisionExpenseAccountCode",
                schema: "dbo",
                table: "LND_CreditLineParameters",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AccountingDocumentId",
                schema: "dbo",
                table: "INV_Documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantBranchPublicId",
                schema: "dbo",
                table: "COR_Branches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PersonId",
                schema: "dbo",
                table: "COR_Banks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InterestExpenseAccount",
                schema: "dbo",
                table: "CDT_Parameters",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            // (4b) Las siete tablas heredadas que conservan el nombre, en orden de dependencia. El
            //      generador no las ve porque el snapshot anterior se podó a propósito (ver cabecera).
            migrationBuilder.DropTable(name: "ACC_JournalEntries", schema: "dbo");
            migrationBuilder.DropTable(name: "ACC_Documents", schema: "dbo");
            migrationBuilder.DropTable(name: "ACC_Budgets", schema: "dbo");
            migrationBuilder.DropTable(name: "ACC_BankReconciliations", schema: "dbo");
            migrationBuilder.DropTable(name: "ACC_ChartOfAccounts", schema: "dbo");
            migrationBuilder.DropTable(name: "ACC_VoucherTypes", schema: "dbo");
            migrationBuilder.DropTable(name: "ACC_AccountingPeriods", schema: "dbo");

            // (5) El modelo nuevo.
            migrationBuilder.CreateTable(
                name: "ACC_AccountCatalogs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImportedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntryCount = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ACC_AccountCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ChartOfAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    Nature = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    NiifItemCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsMovement = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FirstMovementAt = table.Column<DateOnly>(type: "date", nullable: true),
                    EnabledModules = table.Column<int>(type: "int", nullable: false),
                    RequiresThirdParty = table.Column<bool>(type: "bit", nullable: false),
                    RequiresCrossDocument = table.Column<bool>(type: "bit", nullable: false),
                    RequiresCostCenter = table.Column<bool>(type: "bit", nullable: false),
                    RequiresBranch = table.Column<bool>(type: "bit", nullable: false),
                    BankId = table.Column<int>(type: "int", nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TaxKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TaxConceptCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RequiresTaxBase = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ACC_ChartOfAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ChartOfAccounts_ACC_ChartOfAccounts_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_ChartOfAccounts_COR_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_CrossDocumentTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ACC_CrossDocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ExogenousFormats",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    FormatCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FormatVersion = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Applies = table.Column<bool>(type: "bit", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MinAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinorAmountsRule = table.Column<bool>(type: "bit", nullable: false),
                    MinorAmountsTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_ACC_ExogenousFormats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_FinancialStatementItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NiifGroup = table.Column<byte>(type: "tinyint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Statement = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Sign = table.Column<short>(type: "smallint", nullable: false),
                    ParentCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_ACC_FinancialStatementItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_TaxForms",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ACC_TaxForms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_VoucherTypes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Usage = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ModuleCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSeeded = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ACC_VoucherTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_WithholdingCertificates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    TaxKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    PeriodFrom = table.Column<byte>(type: "tinyint", nullable: true),
                    PeriodTo = table.Column<byte>(type: "tinyint", nullable: true),
                    Number = table.Column<long>(type: "bigint", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    VoidedByCertificateId = table.Column<int>(type: "int", nullable: true),
                    LedgerFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TotalBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSentTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ACC_WithholdingCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_WithholdingCertificates_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_AccountCatalogEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Level = table.Column<byte>(type: "tinyint", nullable: false),
                    Nature = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NiifItemCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ParentCode = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
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
                    table.PrimaryKey("PK_ACC_AccountCatalogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_AccountCatalogEntries_ACC_AccountCatalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ACC_AccountTaxRates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_ACC_AccountTaxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_AccountTaxRates_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ACC_BankStatementColumnMaps",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    DateColumn = table.Column<int>(type: "int", nullable: false),
                    DateFormat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReferenceColumn = table.Column<int>(type: "int", nullable: false),
                    DescriptionColumn = table.Column<int>(type: "int", nullable: false),
                    AmountColumn = table.Column<int>(type: "int", nullable: false),
                    DebitColumn = table.Column<int>(type: "int", nullable: false),
                    CreditColumn = table.Column<int>(type: "int", nullable: false),
                    SignConvention = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HeaderRows = table.Column<int>(type: "int", nullable: false),
                    Delimiter = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
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
                    table.PrimaryKey("PK_ACC_BankStatementColumnMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_BankStatementColumnMaps_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ExogenousConcepts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    ConceptCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_ACC_ExogenousConcepts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ExogenousConcepts_ACC_ExogenousFormats_FormatId",
                        column: x => x.FormatId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ExogenousFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ExogenousRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExportedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    XmlAttachmentPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssueCount = table.Column<int>(type: "int", nullable: false),
                    BlockingIssueCount = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ACC_ExogenousRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ExogenousRuns_ACC_ExogenousFormats_FormatId",
                        column: x => x.FormatId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ExogenousFormats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_TaxFormLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormId = table.Column<int>(type: "int", nullable: false),
                    LineCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Selector = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountPrefixes = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Sign = table.Column<short>(type: "smallint", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ACC_TaxFormLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_TaxFormLines_ACC_TaxForms_FormId",
                        column: x => x.FormId,
                        principalSchema: "dbo",
                        principalTable: "ACC_TaxForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ACC_WithholdingCertificateLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CertificateId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    ConceptCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Base = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_ACC_WithholdingCertificateLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_WithholdingCertificateLines_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_WithholdingCertificateLines_ACC_WithholdingCertificates_CertificateId",
                        column: x => x.CertificateId,
                        principalSchema: "dbo",
                        principalTable: "ACC_WithholdingCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ExogenousConceptAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    ValueField = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AccountPrefix = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Selector = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
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
                    table.PrimaryKey("PK_ACC_ExogenousConceptAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ExogenousConceptAccounts_ACC_ExogenousConcepts_ConceptId",
                        column: x => x.ConceptId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ExogenousConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ExogenousRunLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    IdType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IdNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckDigit = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Surname1 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Surname2 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Name1 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Name2 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BusinessName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MunicipalityCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    DepartmentCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Value1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value3 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value4 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value6 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value7 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value8 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value9 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Value10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IssuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ACC_ExogenousRunLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ExogenousRunLines_ACC_ExogenousConcepts_ConceptId",
                        column: x => x.ConceptId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ExogenousConcepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_ExogenousRunLines_ACC_ExogenousRuns_RunId",
                        column: x => x.RunId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ExogenousRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ACC_ExogenousRunLines_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_AccountingPeriods",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalYearId = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ACC_AccountingPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_BankReconciliations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    PeriodId = table.Column<int>(type: "int", nullable: false),
                    StatementOpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StatementClosingBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BookBalanceAtClose = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ACC_BankReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_BankReconciliations_ACC_AccountingPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_BankReconciliations_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_Documents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherTypeId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<long>(type: "bigint", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OriginModule = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SourcePublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodId = table.Column<int>(type: "int", nullable: true),
                    TotalDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    RegisteredBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PostedByUserId = table.Column<int>(type: "int", nullable: true),
                    PostedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversesDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedByDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ACC_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_Documents_ACC_AccountingPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_Documents_ACC_Documents_ReversedByDocumentId",
                        column: x => x.ReversedByDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_Documents_ACC_Documents_ReversesDocumentId",
                        column: x => x.ReversesDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_Documents_ACC_VoucherTypes_VoucherTypeId",
                        column: x => x.VoucherTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_VoucherTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_AccountingSetups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogId = table.Column<int>(type: "int", nullable: false),
                    MovementLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    Level5Length = table.Column<byte>(type: "tinyint", nullable: false),
                    Level6Length = table.Column<byte>(type: "tinyint", nullable: false),
                    NiifGroup = table.Column<byte>(type: "tinyint", nullable: false),
                    FirstFiscalYear = table.Column<int>(type: "int", nullable: false),
                    ResultAccountId = table.Column<int>(type: "int", nullable: true),
                    MainBranchId = table.Column<int>(type: "int", nullable: false),
                    FourEyes = table.Column<bool>(type: "bit", nullable: false),
                    ReconciliationDayTolerance = table.Column<int>(type: "int", nullable: false),
                    TaxTolerance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpeningDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    InitializedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InitializedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ACC_AccountingSetups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_AccountingSetups_ACC_AccountCatalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_AccountingSetups_ACC_ChartOfAccounts_ResultAccountId",
                        column: x => x.ResultAccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_AccountingSetups_ACC_Documents_OpeningDocumentId",
                        column: x => x.OpeningDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_AccountingSetups_COR_Branches_MainBranchId",
                        column: x => x.MainBranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_AssetRuns",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodId = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExecutedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ReversalDocumentId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ACC_AssetRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_AssetRuns_ACC_AccountingPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_AssetRuns_ACC_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_AssetRuns_ACC_Documents_ReversalDocumentId",
                        column: x => x.ReversalDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_FiscalYears",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ClosingDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReopenedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReopenReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ACC_FiscalYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_FiscalYears_ACC_Documents_ClosingDocumentId",
                        column: x => x.ClosingDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_FixedAssets",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AssetAccountId = table.Column<int>(type: "int", nullable: false),
                    AccumulatedAccountId = table.Column<int>(type: "int", nullable: true),
                    ExpenseAccountId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: true),
                    SupplierPersonId = table.Column<int>(type: "int", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PurchaseDocument = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ResidualValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LifeMonths = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RetiredAt = table.Column<DateOnly>(type: "date", nullable: true),
                    RetireReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RetireDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    RetireCounterAccountId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ACC_FixedAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssets_ACC_ChartOfAccounts_AccumulatedAccountId",
                        column: x => x.AccumulatedAccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssets_ACC_ChartOfAccounts_AssetAccountId",
                        column: x => x.AssetAccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssets_ACC_ChartOfAccounts_ExpenseAccountId",
                        column: x => x.ExpenseAccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssets_ACC_Documents_RetireDocumentId",
                        column: x => x.RetireDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssets_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssets_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssets_COR_People_SupplierPersonId",
                        column: x => x.SupplierPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_JournalEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: true),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    CrossDocumentTypeId = table.Column<int>(type: "int", nullable: true),
                    CrossDocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TaxBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPosted = table.Column<bool>(type: "bit", nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ACC_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_JournalEntries_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_JournalEntries_ACC_CrossDocumentTypes_CrossDocumentTypeId",
                        column: x => x.CrossDocumentTypeId,
                        principalSchema: "dbo",
                        principalTable: "ACC_CrossDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_JournalEntries_ACC_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_JournalEntries_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_JournalEntries_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_JournalEntries_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_Budgets",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalYearId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_ACC_Budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_Budgets_ACC_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalSchema: "dbo",
                        principalTable: "ACC_FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_FixedAssetInstallments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    PeriodId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ACC_FixedAssetInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssetInstallments_ACC_AccountingPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssetInstallments_ACC_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_FixedAssetInstallments_ACC_FixedAssets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "dbo",
                        principalTable: "ACC_FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ACC_BankStatementLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReconciliationId = table.Column<int>(type: "int", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    MatchKind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MatchedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DraftDocumentId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ACC_BankStatementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_BankStatementLines_ACC_BankReconciliations_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalSchema: "dbo",
                        principalTable: "ACC_BankReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ACC_BankStatementLines_ACC_Documents_DraftDocumentId",
                        column: x => x.DraftDocumentId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_BankStatementLines_ACC_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "dbo",
                        principalTable: "ACC_JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_BudgetLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BudgetId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    CostCenterId = table.Column<int>(type: "int", nullable: true),
                    Month = table.Column<byte>(type: "tinyint", nullable: false),
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
                    table.PrimaryKey("PK_ACC_BudgetLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_BudgetLines_ACC_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalSchema: "dbo",
                        principalTable: "ACC_Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ACC_BudgetLines_ACC_ChartOfAccounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_BudgetLines_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_BudgetLines_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TRS_Concepts_CreditAccountId",
                schema: "dbo",
                table: "TRS_Concepts",
                column: "CreditAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TRS_Concepts_DebitAccountId",
                schema: "dbo",
                table: "TRS_Concepts",
                column: "DebitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_WorkRiskProviders_PersonId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_SeveranceProviders_PersonId",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_PensionProviders_PersonId",
                schema: "dbo",
                table: "PAY_PensionProviders",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_HealthInsuranceProviders_PersonId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PAY_FamilyCompensationFunds_PersonId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_INV_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "INV_Documents",
                column: "AccountingDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Branches_TenantBranchPublicId",
                schema: "dbo",
                table: "COR_Branches",
                column: "TenantBranchPublicId",
                unique: true,
                filter: "[TenantBranchPublicId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COR_Banks_PersonId",
                schema: "dbo",
                table: "COR_Banks",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountCatalogEntries_Catalog_Code",
                schema: "dbo",
                table: "ACC_AccountCatalogEntries",
                columns: new[] { "CatalogId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountCatalogEntries_PublicId",
                schema: "dbo",
                table: "ACC_AccountCatalogEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountCatalogs_Code",
                schema: "dbo",
                table: "ACC_AccountCatalogs",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountCatalogs_PublicId",
                schema: "dbo",
                table: "ACC_AccountCatalogs",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountingPeriods_Dates",
                schema: "dbo",
                table: "ACC_AccountingPeriods",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountingPeriods_PublicId",
                schema: "dbo",
                table: "ACC_AccountingPeriods",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountingPeriods_Year_Month",
                schema: "dbo",
                table: "ACC_AccountingPeriods",
                columns: new[] { "FiscalYearId", "Month" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountingSetups_CatalogId",
                schema: "dbo",
                table: "ACC_AccountingSetups",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountingSetups_MainBranchId",
                schema: "dbo",
                table: "ACC_AccountingSetups",
                column: "MainBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountingSetups_OpeningDocumentId",
                schema: "dbo",
                table: "ACC_AccountingSetups",
                column: "OpeningDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountingSetups_ResultAccountId",
                schema: "dbo",
                table: "ACC_AccountingSetups",
                column: "ResultAccountId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountingSetups_PublicId",
                schema: "dbo",
                table: "ACC_AccountingSetups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountTaxRates_Account_ValidFrom",
                schema: "dbo",
                table: "ACC_AccountTaxRates",
                columns: new[] { "AccountId", "ValidFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountTaxRates_PublicId",
                schema: "dbo",
                table: "ACC_AccountTaxRates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AssetRuns_DocumentId",
                schema: "dbo",
                table: "ACC_AssetRuns",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AssetRuns_ReversalDocumentId",
                schema: "dbo",
                table: "ACC_AssetRuns",
                column: "ReversalDocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AssetRuns_Period",
                schema: "dbo",
                table: "ACC_AssetRuns",
                column: "PeriodId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AssetRuns_PublicId",
                schema: "dbo",
                table: "ACC_AssetRuns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_BankReconciliations_PeriodId",
                schema: "dbo",
                table: "ACC_BankReconciliations",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankReconciliations_Account_Period",
                schema: "dbo",
                table: "ACC_BankReconciliations",
                columns: new[] { "AccountId", "PeriodId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankReconciliations_PublicId",
                schema: "dbo",
                table: "ACC_BankReconciliations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankStatementColumnMaps_Account",
                schema: "dbo",
                table: "ACC_BankStatementColumnMaps",
                column: "AccountId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankStatementColumnMaps_PublicId",
                schema: "dbo",
                table: "ACC_BankStatementColumnMaps",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_BankStatementLines_DraftDocumentId",
                schema: "dbo",
                table: "ACC_BankStatementLines",
                column: "DraftDocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankStatementLines_Fingerprint",
                schema: "dbo",
                table: "ACC_BankStatementLines",
                columns: new[] { "ReconciliationId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankStatementLines_JournalEntry",
                schema: "dbo",
                table: "ACC_BankStatementLines",
                column: "JournalEntryId",
                unique: true,
                filter: "[JournalEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankStatementLines_PublicId",
                schema: "dbo",
                table: "ACC_BankStatementLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_BudgetLines_AccountId",
                schema: "dbo",
                table: "ACC_BudgetLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_BudgetLines_BranchId",
                schema: "dbo",
                table: "ACC_BudgetLines",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_BudgetLines_CostCenterId",
                schema: "dbo",
                table: "ACC_BudgetLines",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BudgetLines_Key",
                schema: "dbo",
                table: "ACC_BudgetLines",
                columns: new[] { "BudgetId", "AccountId", "BranchId", "CostCenterId", "Month" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BudgetLines_PublicId",
                schema: "dbo",
                table: "ACC_BudgetLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_Budgets_PublicId",
                schema: "dbo",
                table: "ACC_Budgets",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_Budgets_Year_Version",
                schema: "dbo",
                table: "ACC_Budgets",
                columns: new[] { "FiscalYearId", "Version" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ChartOfAccounts_Bank",
                schema: "dbo",
                table: "ACC_ChartOfAccounts",
                column: "BankId",
                filter: "[BankId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ChartOfAccounts_Movement_Active",
                schema: "dbo",
                table: "ACC_ChartOfAccounts",
                columns: new[] { "IsMovement", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ChartOfAccounts_Parent",
                schema: "dbo",
                table: "ACC_ChartOfAccounts",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ChartOfAccounts_Code",
                schema: "dbo",
                table: "ACC_ChartOfAccounts",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ChartOfAccounts_PublicId",
                schema: "dbo",
                table: "ACC_ChartOfAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_CrossDocumentTypes_Code",
                schema: "dbo",
                table: "ACC_CrossDocumentTypes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_CrossDocumentTypes_PublicId",
                schema: "dbo",
                table: "ACC_CrossDocumentTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Documents_Date",
                schema: "dbo",
                table: "ACC_Documents",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Documents_Origin_Source",
                schema: "dbo",
                table: "ACC_Documents",
                columns: new[] { "OriginModule", "SourcePublicId" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Documents_Period",
                schema: "dbo",
                table: "ACC_Documents",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Documents_ReversedByDocumentId",
                schema: "dbo",
                table: "ACC_Documents",
                column: "ReversedByDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Documents_ReversesDocumentId",
                schema: "dbo",
                table: "ACC_Documents",
                column: "ReversesDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Documents_Status",
                schema: "dbo",
                table: "ACC_Documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_Documents_PublicId",
                schema: "dbo",
                table: "ACC_Documents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_Documents_Type_Number",
                schema: "dbo",
                table: "ACC_Documents",
                columns: new[] { "VoucherTypeId", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ExogenousConceptAccounts_ConceptId",
                schema: "dbo",
                table: "ACC_ExogenousConceptAccounts",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExogenousConceptAccounts_PublicId",
                schema: "dbo",
                table: "ACC_ExogenousConceptAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExogenousConcepts_Format_Code",
                schema: "dbo",
                table: "ACC_ExogenousConcepts",
                columns: new[] { "FormatId", "ConceptCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExogenousConcepts_PublicId",
                schema: "dbo",
                table: "ACC_ExogenousConcepts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExogenousFormats_PublicId",
                schema: "dbo",
                table: "ACC_ExogenousFormats",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExogenousFormats_Year_Code",
                schema: "dbo",
                table: "ACC_ExogenousFormats",
                columns: new[] { "TaxYear", "FormatCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ExogenousRunLines_ConceptId",
                schema: "dbo",
                table: "ACC_ExogenousRunLines",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ExogenousRunLines_PersonId",
                schema: "dbo",
                table: "ACC_ExogenousRunLines",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ExogenousRunLines_Run_Person",
                schema: "dbo",
                table: "ACC_ExogenousRunLines",
                columns: new[] { "RunId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExogenousRunLines_PublicId",
                schema: "dbo",
                table: "ACC_ExogenousRunLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExogenousRuns_Format_Version",
                schema: "dbo",
                table: "ACC_ExogenousRuns",
                columns: new[] { "FormatId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExogenousRuns_PublicId",
                schema: "dbo",
                table: "ACC_ExogenousRuns",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FinancialStatementItems_Group_Code",
                schema: "dbo",
                table: "ACC_FinancialStatementItems",
                columns: new[] { "NiifGroup", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FinancialStatementItems_PublicId",
                schema: "dbo",
                table: "ACC_FinancialStatementItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FiscalYears_ClosingDocumentId",
                schema: "dbo",
                table: "ACC_FiscalYears",
                column: "ClosingDocumentId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FiscalYears_PublicId",
                schema: "dbo",
                table: "ACC_FiscalYears",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FiscalYears_Year",
                schema: "dbo",
                table: "ACC_FiscalYears",
                column: "Year",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssetInstallments_DocumentId",
                schema: "dbo",
                table: "ACC_FixedAssetInstallments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssetInstallments_PeriodId",
                schema: "dbo",
                table: "ACC_FixedAssetInstallments",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FixedAssetInstallments_Asset_Period",
                schema: "dbo",
                table: "ACC_FixedAssetInstallments",
                columns: new[] { "AssetId", "PeriodId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FixedAssetInstallments_PublicId",
                schema: "dbo",
                table: "ACC_FixedAssetInstallments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssets_AccumulatedAccountId",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "AccumulatedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssets_AssetAccountId",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "AssetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssets_BranchId",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssets_CostCenterId",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssets_ExpenseAccountId",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssets_RetireDocumentId",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "RetireDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FixedAssets_SupplierPersonId",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "SupplierPersonId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FixedAssets_Code",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FixedAssets_PublicId",
                schema: "dbo",
                table: "ACC_FixedAssets",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntries_Account_Date",
                schema: "dbo",
                table: "ACC_JournalEntries",
                columns: new[] { "AccountId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntries_Branch_Date",
                schema: "dbo",
                table: "ACC_JournalEntries",
                columns: new[] { "BranchId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntries_CostCenter_Date",
                schema: "dbo",
                table: "ACC_JournalEntries",
                columns: new[] { "CostCenterId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntries_CrossDocument",
                schema: "dbo",
                table: "ACC_JournalEntries",
                columns: new[] { "CrossDocumentTypeId", "CrossDocumentNumber", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntries_Document",
                schema: "dbo",
                table: "ACC_JournalEntries",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntries_Person_Account_Date",
                schema: "dbo",
                table: "ACC_JournalEntries",
                columns: new[] { "PersonId", "AccountId", "Date" });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_JournalEntries_PublicId",
                schema: "dbo",
                table: "ACC_JournalEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_TaxFormLines_FormId",
                schema: "dbo",
                table: "ACC_TaxFormLines",
                column: "FormId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TaxFormLines_PublicId",
                schema: "dbo",
                table: "ACC_TaxFormLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TaxForms_Code",
                schema: "dbo",
                table: "ACC_TaxForms",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TaxForms_PublicId",
                schema: "dbo",
                table: "ACC_TaxForms",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_VoucherTypes_Code",
                schema: "dbo",
                table: "ACC_VoucherTypes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_VoucherTypes_PublicId",
                schema: "dbo",
                table: "ACC_VoucherTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_WithholdingCertificateLines_AccountId",
                schema: "dbo",
                table: "ACC_WithholdingCertificateLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_WithholdingCertificateLines_CertificateId",
                schema: "dbo",
                table: "ACC_WithholdingCertificateLines",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_WithholdingCertificateLines_PublicId",
                schema: "dbo",
                table: "ACC_WithholdingCertificateLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_WithholdingCertificates_Person_Kind_Year",
                schema: "dbo",
                table: "ACC_WithholdingCertificates",
                columns: new[] { "PersonId", "TaxKind", "Year" });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_WithholdingCertificates_Kind_Number",
                schema: "dbo",
                table: "ACC_WithholdingCertificates",
                columns: new[] { "TaxKind", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_WithholdingCertificates_PublicId",
                schema: "dbo",
                table: "ACC_WithholdingCertificates",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_COR_Banks_COR_People_PersonId",
                schema: "dbo",
                table: "COR_Banks",
                column: "PersonId",
                principalSchema: "dbo",
                principalTable: "COR_People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_INV_Documents_ACC_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "INV_Documents",
                column: "AccountingDocumentId",
                principalSchema: "dbo",
                principalTable: "ACC_Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_ConceptDefinitionAccounts_ACC_ChartOfAccounts_CreditAccountId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts",
                column: "CreditAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_ConceptDefinitionAccounts_ACC_ChartOfAccounts_DebitAccountId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts",
                column: "DebitAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_FamilyCompensationFunds_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds",
                column: "PersonId",
                principalSchema: "dbo",
                principalTable: "COR_People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_HealthInsuranceProviders_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders",
                column: "PersonId",
                principalSchema: "dbo",
                principalTable: "COR_People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "AccountingDocumentId",
                principalSchema: "dbo",
                principalTable: "ACC_Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns",
                column: "ReversalAccountingDocumentId",
                principalSchema: "dbo",
                principalTable: "ACC_Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_PensionProviders_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_PensionProviders",
                column: "PersonId",
                principalSchema: "dbo",
                principalTable: "COR_People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_SeveranceProviders_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_SeveranceProviders",
                column: "PersonId",
                principalSchema: "dbo",
                principalTable: "COR_People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_WorkRiskProviders_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders",
                column: "PersonId",
                principalSchema: "dbo",
                principalTable: "COR_People",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TRS_Concepts_ACC_ChartOfAccounts_CreditAccountId",
                schema: "dbo",
                table: "TRS_Concepts",
                column: "CreditAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TRS_Concepts_ACC_ChartOfAccounts_DebitAccountId",
                schema: "dbo",
                table: "TRS_Concepts",
                column: "DebitAccountId",
                principalSchema: "dbo",
                principalTable: "ACC_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ACC_AccountingPeriods_ACC_FiscalYears_FiscalYearId",
                schema: "dbo",
                table: "ACC_AccountingPeriods",
                column: "FiscalYearId",
                principalSchema: "dbo",
                principalTable: "ACC_FiscalYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Las 7 tablas heredadas de nombre conservado no se recrean (ver cabecera): el modelo ya no
            // tiene su definición y no tenían datos. Las 26 restantes vuelven vacías, como las conocía
            // el snapshot anterior.
            migrationBuilder.DropForeignKey(
                name: "FK_COR_Banks_COR_People_PersonId",
                schema: "dbo",
                table: "COR_Banks");

            migrationBuilder.DropForeignKey(
                name: "FK_INV_Documents_ACC_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_ConceptDefinitionAccounts_ACC_ChartOfAccounts_CreditAccountId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_ConceptDefinitionAccounts_ACC_ChartOfAccounts_DebitAccountId",
                schema: "dbo",
                table: "PAY_ConceptDefinitionAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_FamilyCompensationFunds_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_HealthInsuranceProviders_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PayrollRuns_ACC_Documents_ReversalAccountingDocumentId",
                schema: "dbo",
                table: "PAY_PayrollRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_PensionProviders_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_SeveranceProviders_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_SeveranceProviders");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_WorkRiskProviders_COR_People_PersonId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders");

            migrationBuilder.DropForeignKey(
                name: "FK_TRS_Concepts_ACC_ChartOfAccounts_CreditAccountId",
                schema: "dbo",
                table: "TRS_Concepts");

            migrationBuilder.DropForeignKey(
                name: "FK_TRS_Concepts_ACC_ChartOfAccounts_DebitAccountId",
                schema: "dbo",
                table: "TRS_Concepts");

            migrationBuilder.DropForeignKey(
                name: "FK_ACC_AccountingPeriods_ACC_FiscalYears_FiscalYearId",
                schema: "dbo",
                table: "ACC_AccountingPeriods");

            migrationBuilder.DropTable(
                name: "ACC_AccountCatalogEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_AccountingSetups",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_AccountTaxRates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_AssetRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_BankStatementColumnMaps",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_BankStatementLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_BudgetLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ExogenousConceptAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ExogenousRunLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_FinancialStatementItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_FixedAssetInstallments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_TaxFormLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_WithholdingCertificateLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_AccountCatalogs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_BankReconciliations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_JournalEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_Budgets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ExogenousConcepts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ExogenousRuns",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_FixedAssets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_TaxForms",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_WithholdingCertificates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_CrossDocumentTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ExogenousFormats",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_ChartOfAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_FiscalYears",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_Documents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_AccountingPeriods",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ACC_VoucherTypes",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_TRS_Concepts_CreditAccountId",
                schema: "dbo",
                table: "TRS_Concepts");

            migrationBuilder.DropIndex(
                name: "IX_TRS_Concepts_DebitAccountId",
                schema: "dbo",
                table: "TRS_Concepts");

            migrationBuilder.DropIndex(
                name: "IX_PAY_WorkRiskProviders_PersonId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_SeveranceProviders_PersonId",
                schema: "dbo",
                table: "PAY_SeveranceProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_PensionProviders_PersonId",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_HealthInsuranceProviders_PersonId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders");

            migrationBuilder.DropIndex(
                name: "IX_PAY_FamilyCompensationFunds_PersonId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds");

            migrationBuilder.DropIndex(
                name: "IX_INV_Documents_AccountingDocumentId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropIndex(
                name: "IX_COR_Branches_TenantBranchPublicId",
                schema: "dbo",
                table: "COR_Branches");

            migrationBuilder.DropIndex(
                name: "IX_COR_Banks_PersonId",
                schema: "dbo",
                table: "COR_Banks");

            migrationBuilder.DropColumn(
                name: "CreditAccountId",
                schema: "dbo",
                table: "TRS_Concepts");

            migrationBuilder.DropColumn(
                name: "DebitAccountId",
                schema: "dbo",
                table: "TRS_Concepts");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_WorkRiskProviders");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_SeveranceProviders");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_PensionProviders");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_HealthInsuranceProviders");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "dbo",
                table: "PAY_FamilyCompensationFunds");

            migrationBuilder.DropColumn(
                name: "InterestExpenseAccount",
                schema: "dbo",
                table: "LND_SavingsParameters");

            migrationBuilder.DropColumn(
                name: "ProvisionAccountCode",
                schema: "dbo",
                table: "LND_CreditLineParameters");

            migrationBuilder.DropColumn(
                name: "ProvisionExpenseAccountCode",
                schema: "dbo",
                table: "LND_CreditLineParameters");

            migrationBuilder.DropColumn(
                name: "AccountingDocumentId",
                schema: "dbo",
                table: "INV_Documents");

            migrationBuilder.DropColumn(
                name: "TenantBranchPublicId",
                schema: "dbo",
                table: "COR_Branches");

            migrationBuilder.DropColumn(
                name: "PersonId",
                schema: "dbo",
                table: "COR_Banks");

            migrationBuilder.DropColumn(
                name: "InterestExpenseAccount",
                schema: "dbo",
                table: "CDT_Parameters");

            migrationBuilder.CreateTable(
                name: "ACC_AccountBalances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PeriodMonth = table.Column<byte>(type: "tinyint", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_AccountBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_AccountBalances_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ACC_AccountBalances_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ACC_AccountGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentGroupId1 = table.Column<int>(type: "int", nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FinancialStatementCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    GroupNumber = table.Column<int>(type: "int", nullable: true),
                    GroupType = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Level = table.Column<int>(type: "int", nullable: true),
                    ParentGroupId = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportOrder = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SpecialCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_AccountGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_AccountGroups_ACC_AccountGroups_ParentGroupId",
                        column: x => x.ParentGroupId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_AccountGroups_ACC_AccountGroups_ParentGroupId1",
                        column: x => x.ParentGroupId1,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ACC_Amortizations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    AmortizationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CrossAccountId = table.Column<int>(type: "int", nullable: true),
                    CrossCostCenterCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CrossInitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CrossPersonTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastPeriod = table.Column<int>(type: "int", nullable: true),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MovementAccountId = table.Column<int>(type: "int", nullable: true),
                    MovementCostCenterCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MovementInitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MovementPersonTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RemainingBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_Amortizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_Amortizations_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_Amortizations_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_AuxiliaryDocuments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    AprCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AprDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AugCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AugDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AuxiliaryType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DecCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DecDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FebCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FebDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    JanCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JanDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JulCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JulDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JunCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JunDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LegacyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MarCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MarDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MayCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MayDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NovCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NovDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OctCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OctDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OriginalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Period13Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SepCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SepDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_AuxiliaryDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_AuxiliaryDocuments_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ACC_AuxiliaryDocuments_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ACC_AuxiliaryDocuments_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ACC_BankReconciliationFlats",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PeriodCode = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TransactionDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_BankReconciliationFlats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_BankReconciliationMasters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FinalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PeriodCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_BankReconciliationMasters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_BankReconciliationMasters_COR_Banks_BankId",
                        column: x => x.BankId,
                        principalSchema: "dbo",
                        principalTable: "COR_Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_Depreciations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CrossAccountId = table.Column<int>(type: "int", nullable: true),
                    CrossCostCenterCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CrossInitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CrossPersonTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DepreciationRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastPeriod = table.Column<int>(type: "int", nullable: true),
                    MonthlyDepreciation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MovementAccountId = table.Column<int>(type: "int", nullable: true),
                    MovementCostCenterCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MovementInitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MovementPersonTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NetValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OriginalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsefulLifeMonths = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_Depreciations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_Depreciations_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_Depreciations_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_DianReportFormats",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BalanceThreshold = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: false),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DianTaxId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    FormatCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MinorTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(18,0)", precision: 18, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_DianReportFormats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ExchangeRateHistory",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    GroupNumber = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PeriodCode = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SourceAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    StatementCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    SubgroupNumber = table.Column<int>(type: "int", nullable: true),
                    TargetAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_ExchangeRateHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_FinancialReportParams",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CalculationBase = table.Column<int>(type: "int", nullable: false),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    Formula = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ParameterOption = table.Column<int>(type: "int", nullable: false),
                    ParameterValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ValueOption = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_FinancialReportParams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_FinancialReports",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentTypeCode = table.Column<int>(type: "int", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FirstName2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastName1 = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastName2 = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Municipality = table.Column<int>(type: "int", nullable: true),
                    ProcessedByLegacySystem = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SavingsAccount = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SecondName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Value1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value3 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value4 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value6 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value7 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value8 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Value9 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    VerificationDigit = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_FinancialReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_FinancialReports_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_FinancialReportValues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ValueCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValueId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_FinancialReportValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_FiscalPeriods",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PeriodMonth = table.Column<byte>(type: "tinyint", nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false, defaultValue: "O"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_FiscalPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_GmfTaxLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BaseAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LineCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Sign = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_GmfTaxLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_GroupNames",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GroupNumber = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_GroupNames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_IcaTaxLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BaseAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LineCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Sign = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_IcaTaxLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_IncomeTaxLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BaseAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LineCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Sign = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_IncomeTaxLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_JournalEntryItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CostCenterCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DocumentNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    Period = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PersonTaxId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoucherNumber = table.Column<int>(type: "int", nullable: true),
                    VoucherTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_JournalEntryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_RiskCategories",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AverageBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Day1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day11 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day12 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day13 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day14 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day15 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day16 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day17 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day18 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day19 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day20 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day21 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day22 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day23 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day24 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day25 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day26 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day27 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day28 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day29 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day3 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day30 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day31 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day4 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day6 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day7 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day8 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Day9 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PeriodCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_RiskCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_StampTaxes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    DebitAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_StampTaxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_SubgroupNames",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SubgroupNumber = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_SubgroupNames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_TaxFormCodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    AuditorCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    ConceptCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ContributorType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentTypeCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    EconomicActivity = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    FormCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepresentationCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TaxType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_TaxFormCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_ThirdPartyAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    AprCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AprDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AugCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AugDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DecCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DecDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FebCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FebDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    JanCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JanDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JulCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JulDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JunCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    JunDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MarCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MarDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MayCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MayDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NovCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NovDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OctCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OctDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Period13Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Period13Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SepCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SepDebit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_ThirdPartyAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_ThirdPartyAccounts_COR_Branches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "dbo",
                        principalTable: "COR_Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_ThirdPartyAccounts_COR_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalSchema: "dbo",
                        principalTable: "COR_CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_ThirdPartyAccounts_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ACC_VatTaxLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BaseAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LineCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Sign = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_VatTaxLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_WithholdingTaxLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BaseAccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LineCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Sign = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_WithholdingTaxLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ACC_AccountSubgroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId1 = table.Column<int>(type: "int", nullable: true),
                    ParentSubgroupId1 = table.Column<int>(type: "int", nullable: true),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FinancialStatementCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Level = table.Column<int>(type: "int", nullable: true),
                    ParentSubgroupId = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportOrder = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SpecialCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    SubgroupNumber = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACC_AccountSubgroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_AccountSubgroups_ACC_AccountGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_AccountSubgroups_ACC_AccountGroups_GroupId1",
                        column: x => x.GroupId1,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ACC_AccountSubgroups_ACC_AccountSubgroups_ParentSubgroupId",
                        column: x => x.ParentSubgroupId,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountSubgroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ACC_AccountSubgroups_ACC_AccountSubgroups_ParentSubgroupId1",
                        column: x => x.ParentSubgroupId1,
                        principalSchema: "dbo",
                        principalTable: "ACC_AccountSubgroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountBalances_AccountId_PeriodYear_PeriodMonth_BranchId_CostCenterId",
                schema: "dbo",
                table: "ACC_AccountBalances",
                columns: new[] { "AccountId", "PeriodYear", "PeriodMonth", "BranchId", "CostCenterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountBalances_BranchId",
                schema: "dbo",
                table: "ACC_AccountBalances",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountBalances_CostCenterId",
                schema: "dbo",
                table: "ACC_AccountBalances",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountBalances_Period_Account",
                schema: "dbo",
                table: "ACC_AccountBalances",
                columns: new[] { "PeriodYear", "PeriodMonth", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountBalances_PublicId",
                schema: "dbo",
                table: "ACC_AccountBalances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountGroups_ParentGroupId",
                schema: "dbo",
                table: "ACC_AccountGroups",
                column: "ParentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountGroups_ParentGroupId1",
                schema: "dbo",
                table: "ACC_AccountGroups",
                column: "ParentGroupId1");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountGroups_PublicId",
                schema: "dbo",
                table: "ACC_AccountGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountSubgroups_GroupId",
                schema: "dbo",
                table: "ACC_AccountSubgroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountSubgroups_GroupId1",
                schema: "dbo",
                table: "ACC_AccountSubgroups",
                column: "GroupId1");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountSubgroups_ParentSubgroupId",
                schema: "dbo",
                table: "ACC_AccountSubgroups",
                column: "ParentSubgroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AccountSubgroups_ParentSubgroupId1",
                schema: "dbo",
                table: "ACC_AccountSubgroups",
                column: "ParentSubgroupId1");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AccountSubgroups_PublicId",
                schema: "dbo",
                table: "ACC_AccountSubgroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Amortizations_AccountId",
                schema: "dbo",
                table: "ACC_Amortizations",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Amortizations_BranchId",
                schema: "dbo",
                table: "ACC_Amortizations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Amortizations_CostCenterId",
                schema: "dbo",
                table: "ACC_Amortizations",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_Amortizations_Natural",
                schema: "dbo",
                table: "ACC_Amortizations",
                columns: new[] { "PeriodYear", "AccountId", "BranchId", "CostCenterId", "PersonId", "DocumentCode" },
                unique: true,
                filter: "[PersonId] IS NOT NULL AND [DocumentCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_Amortizations_PublicId",
                schema: "dbo",
                table: "ACC_Amortizations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AuxiliaryDocuments_AccountId",
                schema: "dbo",
                table: "ACC_AuxiliaryDocuments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AuxiliaryDocuments_BranchId",
                schema: "dbo",
                table: "ACC_AuxiliaryDocuments",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AuxiliaryDocuments_CostCenterId",
                schema: "dbo",
                table: "ACC_AuxiliaryDocuments",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_AuxiliaryDocuments_PersonId",
                schema: "dbo",
                table: "ACC_AuxiliaryDocuments",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_AuxiliaryDocuments_PublicId",
                schema: "dbo",
                table: "ACC_AuxiliaryDocuments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankReconciliationFlats_PublicId",
                schema: "dbo",
                table: "ACC_BankReconciliationFlats",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_BankReconciliationMasters_BankId",
                schema: "dbo",
                table: "ACC_BankReconciliationMasters",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankReconciliationMasters_Natural",
                schema: "dbo",
                table: "ACC_BankReconciliationMasters",
                columns: new[] { "AccountId", "BankId", "PeriodCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_BankReconciliationMasters_PublicId",
                schema: "dbo",
                table: "ACC_BankReconciliationMasters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Depreciations_AccountId",
                schema: "dbo",
                table: "ACC_Depreciations",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Depreciations_BranchId",
                schema: "dbo",
                table: "ACC_Depreciations",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Depreciations_CostCenterId",
                schema: "dbo",
                table: "ACC_Depreciations",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_Depreciations_Natural",
                schema: "dbo",
                table: "ACC_Depreciations",
                columns: new[] { "PeriodYear", "AccountId", "BranchId", "CostCenterId", "PersonId" },
                unique: true,
                filter: "[PersonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_Depreciations_PublicId",
                schema: "dbo",
                table: "ACC_Depreciations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_DianReportFormats_Natural",
                schema: "dbo",
                table: "ACC_DianReportFormats",
                columns: new[] { "FormatId", "ConceptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_DianReportFormats_PublicId",
                schema: "dbo",
                table: "ACC_DianReportFormats",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ExchangeRateHistory_PublicId",
                schema: "dbo",
                table: "ACC_ExchangeRateHistory",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FinancialReportParams_Natural",
                schema: "dbo",
                table: "ACC_FinancialReportParams",
                columns: new[] { "FormatId", "ConceptId", "ParameterOption", "ParameterValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FinancialReportParams_PublicId",
                schema: "dbo",
                table: "ACC_FinancialReportParams",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FinancialReports_AccountId",
                schema: "dbo",
                table: "ACC_FinancialReports",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_FinancialReports_PersonId",
                schema: "dbo",
                table: "ACC_FinancialReports",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FinancialReports_Natural",
                schema: "dbo",
                table: "ACC_FinancialReports",
                columns: new[] { "FormatId", "ConceptId", "PersonId", "AccountId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FinancialReports_PublicId",
                schema: "dbo",
                table: "ACC_FinancialReports",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FinancialReportValues_Natural",
                schema: "dbo",
                table: "ACC_FinancialReportValues",
                columns: new[] { "FormatId", "ValueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FinancialReportValues_PublicId",
                schema: "dbo",
                table: "ACC_FinancialReportValues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FiscalPeriods_Natural",
                schema: "dbo",
                table: "ACC_FiscalPeriods",
                columns: new[] { "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_FiscalPeriods_PublicId",
                schema: "dbo",
                table: "ACC_FiscalPeriods",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_GmfTaxLines_LineCode",
                schema: "dbo",
                table: "ACC_GmfTaxLines",
                column: "LineCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_GmfTaxLines_PublicId",
                schema: "dbo",
                table: "ACC_GmfTaxLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_GroupNames_PublicId",
                schema: "dbo",
                table: "ACC_GroupNames",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_IcaTaxLines_LineCode",
                schema: "dbo",
                table: "ACC_IcaTaxLines",
                column: "LineCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_IcaTaxLines_PublicId",
                schema: "dbo",
                table: "ACC_IcaTaxLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_IncomeTaxLines_LineCode",
                schema: "dbo",
                table: "ACC_IncomeTaxLines",
                column: "LineCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_IncomeTaxLines_PublicId",
                schema: "dbo",
                table: "ACC_IncomeTaxLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_JournalEntryItems_JournalEntryId",
                schema: "dbo",
                table: "ACC_JournalEntryItems",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_JournalEntryItems_PublicId",
                schema: "dbo",
                table: "ACC_JournalEntryItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_RiskCategories_Natural",
                schema: "dbo",
                table: "ACC_RiskCategories",
                columns: new[] { "AccountCode", "PeriodCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_RiskCategories_PublicId",
                schema: "dbo",
                table: "ACC_RiskCategories",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_StampTaxes_Grade",
                schema: "dbo",
                table: "ACC_StampTaxes",
                column: "Grade",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_StampTaxes_PublicId",
                schema: "dbo",
                table: "ACC_StampTaxes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_SubgroupNames_PublicId",
                schema: "dbo",
                table: "ACC_SubgroupNames",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_TaxFormCodes_PublicId",
                schema: "dbo",
                table: "ACC_TaxFormCodes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ThirdPartyAccounts_AccountId",
                schema: "dbo",
                table: "ACC_ThirdPartyAccounts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ThirdPartyAccounts_BranchId",
                schema: "dbo",
                table: "ACC_ThirdPartyAccounts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ThirdPartyAccounts_CostCenterId",
                schema: "dbo",
                table: "ACC_ThirdPartyAccounts",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ACC_ThirdPartyAccounts_PersonId",
                schema: "dbo",
                table: "ACC_ThirdPartyAccounts",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ThirdPartyAccounts_Natural",
                schema: "dbo",
                table: "ACC_ThirdPartyAccounts",
                columns: new[] { "PeriodYear", "AccountId", "PersonId", "BranchId", "CostCenterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_ThirdPartyAccounts_PublicId",
                schema: "dbo",
                table: "ACC_ThirdPartyAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_VatTaxLines_LineCode",
                schema: "dbo",
                table: "ACC_VatTaxLines",
                column: "LineCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_VatTaxLines_PublicId",
                schema: "dbo",
                table: "ACC_VatTaxLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_WithholdingTaxLines_LineCode",
                schema: "dbo",
                table: "ACC_WithholdingTaxLines",
                column: "LineCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_ACC_WithholdingTaxLines_PublicId",
                schema: "dbo",
                table: "ACC_WithholdingTaxLines",
                column: "PublicId",
                unique: true);
        }
    }
}
