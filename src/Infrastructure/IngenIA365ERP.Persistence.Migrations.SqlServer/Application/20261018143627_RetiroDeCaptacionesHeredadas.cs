using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 014 — Cartera financiera, captaciones: retiro de las captaciones heredadas (specs/014-ahorros-aportes-cdt/tasks.md
    /// T005, T006; research.md R28 y C19; FR-119, FR-120).
    ///
    /// <para>
    /// Destructiva con guarda (Principio XII; la cabecera de aprobación, con el diagnóstico, el respaldo y el segundo revisor, va
    /// al principio de <c>Up()</c>). Borra las 38 tablas <c>LND_*</c> que la 013 conservó —ahorros, depósitos, aportes, retiros,
    /// caja, SIPLA, zonas y bienestar— y las 7 <c>CDT_*</c> del CDT heredado: 45 en total, sin llaves desde ninguna tabla viva. Va
    /// <b>al principio</b> de la 014 (C19), después de todas las migraciones de la 013 y antes de crear ninguna tabla de captaciones,
    /// generada sobre el modelo sin las 45 entidades (T007) y sin ninguna entidad nueva, en commit propio.
    /// </para>
    ///
    /// <para>
    /// Qué hace, en orden: (1) guarda: cuenta las filas —incluidas las dadas de baja— de las 45 tablas (saltando las que no existan);
    /// si alguna tiene filas y la base no trae la fila viva <c>COR_SystemSettings.SettingKey = 'LND.CaptacionesHeredadas.RetiroAprobado'</c>,
    /// se niega nombrando cada tabla con su cantidad, sin tocar nada (PostgreSQL <c>RAISE EXCEPTION</c>, SQL Server
    /// <c>THROW 50014</c>); (2) borra las 45 tablas.
    /// </para>
    ///
    /// <para>
    /// <c>Down()</c> recrea vacías las 45 tablas con sus índices (generado por EF). Es irreversible en datos.
    /// </para>
    /// </summary>
    public partial class RetiroDeCaptacionesHeredadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MIGRACION-DESTRUCTIVA-APROBADA (Principio XII; feature 014, T006; research R28, C19).
            //   Diagnóstico: parcial (T005). Lo corre el dueño con specs/014-ahorros-aportes-cdt/diagnostico-captaciones-heredadas.sql en
            //     cada base de cooperativa de DEV, QA y producción antes de fusionar, y anota aquí y en
            //     docs/operaciones/captaciones-retiro-heredado.md la fecha, el ambiente, las bases, las filas por tabla y la decisión
            //     tomada donde hubo filas. Corrido sólo en local el 2026-10-18 (PostgreSQL 5433, contenedor ingenia365-dev-postgres:
            //     ingenia365erp; la administrativa ingenia365erp_admin no tiene ninguna de las 45): 0 filas en las 45; sin aprobación.
            //   Respaldo: pendiente: <base>-AAAAMMDD-pre-f014.dump de cada base de cooperativa antes de aplicarla en cada ambiente.
            //   Segundo revisor: pendiente (lo nombra el dueño, como T1083 de la 013).
            // Con la guarda, sólo se pierden filas que el dueño aprobó perder (fila LND.CaptacionesHeredadas.RetiroAprobado de la cooperativa).

            // (1) Guarda: sólo sobre las captaciones heredadas vacías, o con la aprobación del dueño en la base.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [dbo].[COR_SystemSettings]
                               WHERE [SettingKey] = N'LND.CaptacionesHeredadas.RetiroAprobado' AND [IsDeleted] = 0)
                BEGIN
                    DECLARE @tabla sysname, @filas bigint, @conFilas nvarchar(max), @sql nvarchar(max), @mensaje nvarchar(2048);
                    -- Las 45 tablas, contando tambien las filas dadas de baja (COUNT_BIG(*) no mira IsDeleted).
                    DECLARE tablas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT t FROM (VALUES (N'CDT_AssociateReferences'), (N'CDT_Audit'), (N'CDT_CertificateEntries'), (N'CDT_Certificates'), (N'CDT_ParameterAudit'), (N'CDT_Parameters'), (N'CDT_RatesByTerm'), (N'LND_ActivityEnrollments'), (N'LND_AssociateActivities'), (N'LND_AssociateDiseases'), (N'LND_AssociateWithdrawals'), (N'LND_AuxAppInstallmentBeneficiaries'), (N'LND_AuxAppInstallments'), (N'LND_AuxiliaryApplicationLines'), (N'LND_AuxiliaryApplications'), (N'LND_BiometricRecords'), (N'LND_Blacklist'), (N'LND_CashBases'), (N'LND_Cashiers'), (N'LND_CheckClearing'), (N'LND_Checkbooks'), (N'LND_ContributionReductionParams'), (N'LND_ContributionReductions'), (N'LND_DepositAccounts'), (N'LND_DepositAudit'), (N'LND_DepositEntries'), (N'LND_DepositSeals'), (N'LND_DepositSignatures'), (N'LND_Documents'), (N'LND_MinuteAttendees'), (N'LND_Minutes'), (N'LND_MoneyLaunderingDeclarations'), (N'LND_PersonAssets'), (N'LND_SavingsAccounts'), (N'LND_SavingsParameters'), (N'LND_SiplaGroupParameters'), (N'LND_SiplaParameters'), (N'LND_SubZones'), (N'LND_Subsidies'), (N'LND_TransactionCodes'), (N'LND_UnusualTransactionEntries'), (N'LND_UnusualTransactions'), (N'LND_WithdrawalStatuses'), (N'LND_ZoneTypes'), (N'LND_Zones')) AS v(t);
                    OPEN tablas;
                    FETCH NEXT FROM tablas INTO @tabla;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        IF OBJECT_ID(N'[dbo].' + QUOTENAME(@tabla), N'U') IS NOT NULL
                        BEGIN
                            SET @sql = N'SELECT @n = COUNT_BIG(*) FROM [dbo].' + QUOTENAME(@tabla);
                            EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @filas OUTPUT;
                            IF @filas > 0
                                SET @conFilas = CONCAT(@conFilas, CASE WHEN @conFilas IS NULL THEN N'' ELSE N', ' END,
                                                       @tabla, N' (', @filas, N' filas)');
                        END
                        FETCH NEXT FROM tablas INTO @tabla;
                    END
                    CLOSE tablas;
                    DEALLOCATE tablas;
                    IF @conFilas IS NOT NULL
                    BEGIN
                        SET @mensaje = CONCAT(N'RetiroDeCaptacionesHeredadas: las captaciones heredadas tienen filas: ', @conFilas,
                            N'. La migracion borra las 38 tablas LND_ que la feature 013 conservo (ahorros, aportes, caja, SIPLA, zonas y bienestar) y las 7 CDT_; solo se aplica sobre tablas vacias o con la aprobacion del dueno para esta cooperativa (fila COR_SystemSettings.SettingKey = ''LND.CaptacionesHeredadas.RetiroAprobado''), previo respaldo y segundo revisor (feature 014, R28, FR-119, Principio XII; runbook docs/operaciones/captaciones-retiro-heredado.md).');
                        THROW 50014, @mensaje, 1;
                    END
                END
                """);

            // (2) Las 45 tablas: ninguna tabla viva tiene llave hacia ellas (EF no generó ningún DropForeignKey).
            migrationBuilder.DropTable(
                name: "CDT_AssociateReferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDT_Audit",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDT_CertificateEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDT_ParameterAudit",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDT_Parameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDT_RatesByTerm",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ActivityEnrollments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AssociateActivities",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AssociateDiseases",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AssociateWithdrawals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AuxAppInstallmentBeneficiaries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AuxAppInstallments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AuxiliaryApplicationLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AuxiliaryApplications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_BiometricRecords",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Blacklist",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CashBases",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Cashiers",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Checkbooks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CheckClearing",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ContributionReductionParams",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ContributionReductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DepositAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DepositAudit",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DepositEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DepositSeals",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DepositSignatures",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Documents",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MinuteAttendees",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Minutes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_MoneyLaunderingDeclarations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PersonAssets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_SavingsAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_SavingsParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_SiplaGroupParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_SiplaParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Subsidies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_SubZones",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_TransactionCodes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_UnusualTransactionEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_UnusualTransactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_WithdrawalStatuses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Zones",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ZoneTypes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDT_Certificates",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CDT_Audit",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccrualDateNew = table.Column<DateOnly>(type: "date", nullable: true),
                    AccrualDateOld = table.Column<DateOnly>(type: "date", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AddressNew = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AddressOld = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AmountNew = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountOld = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CancellationDateNew = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationDateOld = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByNew = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CancelledByOld = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InterestRateNew = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    InterestRateOld = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    IsCapitalizedNew = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsCapitalizedOld = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IssueDateNew = table.Column<DateOnly>(type: "date", nullable: true),
                    IssueDateOld = table.Column<DateOnly>(type: "date", nullable: true),
                    LegalRepIdNew = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LegalRepIdOld = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LegalRepNameNew = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LegalRepNameOld = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaturityDateNew = table.Column<DateOnly>(type: "date", nullable: true),
                    MaturityDateOld = table.Column<DateOnly>(type: "date", nullable: true),
                    MobileNew = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MobileOld = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    PhoneNew = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PhoneOld = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PreviousCertIdNew = table.Column<int>(type: "int", nullable: true),
                    PreviousCertIdOld = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StatusNew = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    StatusOld = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    TermNew = table.Column<int>(type: "int", nullable: true),
                    TermOld = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CDT_Audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CDT_Certificates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccrualDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryId1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BeneficiaryId2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BeneficiaryId3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BeneficiaryId4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BeneficiaryId5 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BeneficiaryName1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BeneficiaryName2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BeneficiaryName3 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BeneficiaryName4 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BeneficiaryName5 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BeneficiaryPct1 = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    BeneficiaryPct2 = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    BeneficiaryPct3 = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    BeneficiaryPct4 = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    BeneficiaryPct5 = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    BusinessAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CancellationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterestRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    IsCapitalized = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LegalRepId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LegalRepName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Periodicity = table.Column<int>(type: "int", nullable: true),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PreviousCertificateId = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RenewalType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SignatoryId1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SignatoryId2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SignatoryId3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SignatoryName1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SignatoryName2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SignatoryName3 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CDT_Certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CDT_ParameterAudit",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnnualRateNew = table.Column<decimal>(type: "decimal(12,9)", precision: 12, scale: 9, nullable: true),
                    AnnualRateOld = table.Column<decimal>(type: "decimal(12,9)", precision: 12, scale: 9, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionNew = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DescriptionOld = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InterestConceptNew = table.Column<int>(type: "int", nullable: true),
                    InterestConceptOld = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MinRateNew = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    MinRateOld = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    MinWithholdingAmtNew = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinWithholdingAmtOld = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MonthlyIncrementNew = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    MonthlyIncrementOld = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentMethodNew = table.Column<int>(type: "int", nullable: true),
                    PaymentMethodOld = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WithholdingConceptNew = table.Column<int>(type: "int", nullable: true),
                    WithholdingConceptOld = table.Column<int>(type: "int", nullable: true),
                    WithholdingRateNew = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    WithholdingRateOld = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CDT_ParameterAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CDT_Parameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnnualRate = table.Column<decimal>(type: "decimal(12,9)", precision: 12, scale: 9, nullable: true),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    InterestConceptId = table.Column<int>(type: "int", nullable: true),
                    InterestExpenseAccount = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    InterestPaymentType = table.Column<int>(type: "int", nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: true),
                    InterestType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false, defaultValue: "S"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinWithholdingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinimumRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    MonthlyIncrement = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Term = table.Column<int>(type: "int", nullable: true),
                    TreasuryAccount = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WithholdingConceptId = table.Column<int>(type: "int", nullable: true),
                    WithholdingRate = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CDT_Parameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CDT_RatesByTerm",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AmountRangeEnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountRangeStart = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InterestRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TermEnd = table.Column<int>(type: "int", nullable: false),
                    TermStart = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CDT_RatesByTerm", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ActivityEnrollments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntryType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ActivityEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AssociateActivities",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActivityEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivityStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivityType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attended = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnrollmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AssociateActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AssociateDiseases",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiseaseCode = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AssociateDiseases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AssociateWithdrawals",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CurrentStatus = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PriorClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PriorStatus = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WithdrawalReasonId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AssociateWithdrawals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AuxAppInstallmentBeneficiaries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    BeneficiaryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LineCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AuxAppInstallmentBeneficiaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AuxAppInstallments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeathDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsGenerated = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    LineCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AuxAppInstallments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AuxiliaryApplicationLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Installments = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LineCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AuxiliaryApplicationLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AuxiliaryApplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ApplicationRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovalRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsApproved = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsClosed = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyIdSolAux = table.Column<int>(type: "int", nullable: true),
                    LineCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    LineRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AuxiliaryApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_BiometricRecords",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FingerprintCode = table.Column<decimal>(type: "decimal(10,0)", precision: 10, nullable: false),
                    FingerprintData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    FingerprintString = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PhotoImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SignatureImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_BiometricRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_Blacklist",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsImported = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ListCode = table.Column<int>(type: "int", nullable: false),
                    Nationality = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PersonName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PersonType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Blacklist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_CashBases",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashierCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeliveredByUser = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedByUser = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CashBases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_Cashiers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashierCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OpenDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PasswordTimeout = table.Column<int>(type: "int", nullable: false),
                    PrinterName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoucherConsecutive = table.Column<int>(type: "int", nullable: false),
                    VoucherType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Cashiers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_Checkbooks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsBlocked = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RangeEnd = table.Column<long>(type: "bigint", nullable: false),
                    RangeStart = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Checkbooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_CheckClearing",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BankCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CheckNumber = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClearingDays = table.Column<int>(type: "int", nullable: false),
                    ClearingType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DepositDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Plaza = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CheckClearing", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ContributionReductionParams",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccumulationConcept = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CutoffPeriod = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExcessAmount = table.Column<decimal>(type: "decimal(15,0)", precision: 15, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MemoDetail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReductionVoucher = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SignatoryName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WithholdingAccumConcept = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ContributionReductionParams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ContributionReductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Average = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DayBalance1 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance10 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance11 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance12 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance13 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance14 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance15 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance16 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance17 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance18 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance19 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance2 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance20 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance21 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance22 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance23 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance24 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance25 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance26 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance27 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance28 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance29 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance3 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance30 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance31 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance4 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance5 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance6 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance7 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance8 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DayBalance9 = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SavingsLineId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ContributionReductions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_DepositAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    AutoDebit = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BeneficiaryId1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId5 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryName1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName3 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName4 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName5 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryPct1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryPct2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryPct3 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryPct4 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryPct5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CancellationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CancelledByUser = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CellPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CommercialAddress = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DepositLineId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExemptionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FirstDeductionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HasProtector = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    HasSeal = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsExempt = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LegacyNumCuenta = table.Column<long>(type: "bigint", nullable: true),
                    LegalRepresentative = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisteredSignatures = table.Column<int>(type: "int", nullable: false),
                    RepresentativeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiredSignatures = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SignatoryId1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignatoryId2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignatoryId3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignatoryName1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignatoryName2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignatoryName3 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SystemDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DepositAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_DepositAudit",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Address_New = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Address_Old = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AuditUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AutoDebit_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AutoDebit_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CellPhone_New = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CellPhone_Old = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreationDate_New = table.Column<DateOnly>(type: "date", nullable: true),
                    CreationDate_Old = table.Column<DateOnly>(type: "date", nullable: true),
                    Cycle_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Cycle_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DeductionType_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DeductionType_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DepositLineId = table.Column<int>(type: "int", nullable: true),
                    EntryDate_New = table.Column<DateOnly>(type: "date", nullable: true),
                    EntryDate_Old = table.Column<DateOnly>(type: "date", nullable: true),
                    Exempt_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Exempt_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ExemptionDate_New = table.Column<DateOnly>(type: "date", nullable: true),
                    ExemptionDate_Old = table.Column<DateOnly>(type: "date", nullable: true),
                    FirstDeduction_New = table.Column<DateOnly>(type: "date", nullable: true),
                    FirstDeduction_Old = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegalRep_New = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LegalRep_Old = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Periodicity_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Periodicity_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Phone_New = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Phone_Old = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Protector_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Protector_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegSignatures_New = table.Column<int>(type: "int", nullable: true),
                    RegSignatures_Old = table.Column<int>(type: "int", nullable: true),
                    RepName_New = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RepName_Old = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReqSignatures_New = table.Column<int>(type: "int", nullable: true),
                    ReqSignatures_Old = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Seal_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Seal_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Status_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Status_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserName_New = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserName_Old = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DepositAudit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_DepositEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DepositLineId = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    FirstDeductionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DepositEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_DepositSeals",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SealImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DepositSeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_DepositSignatures",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsRequired = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SignatureImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SignatureNumber = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DepositSignatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_Documents",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    BeneficiaryCheckId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ClosedInPortfolio = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    DocumentSequence = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IsClosed = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsVoided = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LegacyCompronte = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    LegacyNumeroDomto = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoucherType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_MinuteAttendees",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MinutesNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinutesType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_MinuteAttendees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_Minutes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClosingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MinutesNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinutesType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    OpeningDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Minutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_MoneyLaunderingDeclarations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AffectedProduct = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClientIdType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ClientSurname = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeclarationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeclarationId = table.Column<long>(type: "bigint", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    EconomicActivity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OperationDetail = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TransactionSequence = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoucherType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_MoneyLaunderingDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_PersonAssets",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AssetClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AssetValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CityCode = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Model = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PersonAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_SavingsAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<long>(type: "bigint", nullable: false),
                    AutoDebit = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BeneficiaryId1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryId5 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BeneficiaryName1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName3 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName4 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryName5 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BeneficiaryPct1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryPct2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryPct3 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryPct4 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BeneficiaryPct5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CellPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CommercialAddress = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExemptionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FirstDeductionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HasProtector = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    HasSeal = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyNumCuenta = table.Column<long>(type: "bigint", nullable: true),
                    LegalRepresentative = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisteredSignatures = table.Column<int>(type: "int", nullable: false),
                    RepresentativeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequiredSignatures = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SavingsLineId = table.Column<int>(type: "int", nullable: false),
                    SignatoryId1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignatoryId2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignatoryId3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignatoryName1 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignatoryName2 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SignatoryName3 = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UniqueAccount = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_SavingsAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_SavingsParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CheckWithdrawalGmf = table.Column<int>(type: "int", nullable: false),
                    ClearingDays = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    Consecutive = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    FourPerMillCeiling = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FourPerMillConcept = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    FourPerMillForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    FourPerMillVoucher = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    GraceDays = table.Column<int>(type: "int", nullable: false),
                    InterestConceptCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    InterestConceptId = table.Column<int>(type: "int", nullable: false),
                    InterestExpenseAccount = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    InterestPaymentPeriod = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InterestPaymentRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyLinCred = table.Column<int>(type: "int", nullable: true),
                    LiquidationForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ManagesPapForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    MaxCashAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxWithdrawalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinAccountBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinInterestBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinTransactionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinWithholdingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OtherClearingDays = table.Column<int>(type: "int", nullable: false),
                    PaymentForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SavingsLineId = table.Column<int>(type: "int", nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    TreasuryAccount = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ValidateWithdrawal = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    WithdrawalCeiling = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithholdingConceptCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    WithholdingRate = table.Column<decimal>(type: "decimal(9,5)", precision: 9, scale: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_SavingsParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_SiplaGroupParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ConceptCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_SiplaGroupParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_SiplaParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCount = table.Column<int>(type: "int", nullable: false),
                    AnnualTransactions = table.Column<int>(type: "int", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ConceptCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaxBalance = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    MonthlyCreditMoves = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_SiplaParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_Subsidies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    AssociateCeiling = table.Column<int>(type: "int", nullable: false),
                    AssociateEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssociatePeriodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AssociateStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CommitteeCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ControlsAssociateCeiling = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ControlsCeiling = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ControlsSeniority = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentCeiling = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InInstallments = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SeniorityYearsEnd = table.Column<int>(type: "int", nullable: false),
                    SeniorityYearsStart = table.Column<int>(type: "int", nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SubsidyClass = table.Column<int>(type: "int", nullable: false),
                    SubsidyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    TaxAccount = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    TaxExpenseAccount = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Subsidies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_SubZones",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_SubZones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_TransactionCodes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AdjustAccrual = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DebitCreditFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCodMovto = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_TransactionCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_UnusualTransactionEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentStatus = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PriorStatus = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegisteredBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UnusualTransactionId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_UnusualTransactionEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_UnusualTransactions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCount = table.Column<int>(type: "int", nullable: false),
                    AnnualTransactions = table.Column<int>(type: "int", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ConceptCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: true),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaxBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyCreditMoves = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UnusualType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoucherType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_UnusualTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_WithdrawalStatuses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_WithdrawalStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_Zones",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubZoneId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ZoneId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Zones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ZoneTypes",
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
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ZoneTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ZoneTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CDT_AssociateReferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CertificateId = table.Column<int>(type: "int", nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenceMobile = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReferenceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferencePhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RelationshipType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CDT_AssociateReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CDT_AssociateReferences_CDT_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalSchema: "dbo",
                        principalTable: "CDT_Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CDT_CertificateEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CertificateId1 = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CertificateId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    CurrentRate = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OpeningDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    PreviousRate = table.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CDT_CertificateEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CDT_CertificateEntries_CDT_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalSchema: "dbo",
                        principalTable: "CDT_Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CDT_CertificateEntries_CDT_Certificates_CertificateId1",
                        column: x => x.CertificateId1,
                        principalSchema: "dbo",
                        principalTable: "CDT_Certificates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CDT_AssociateReferences_CertificateId",
                schema: "dbo",
                table: "CDT_AssociateReferences",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_CDT_AssociateReferences_PublicId",
                schema: "dbo",
                table: "CDT_AssociateReferences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_Audit_PublicId",
                schema: "dbo",
                table: "CDT_Audit",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_CertificateEntries_CertificateId",
                schema: "dbo",
                table: "CDT_CertificateEntries",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_CDT_CertificateEntries_CertificateId1",
                schema: "dbo",
                table: "CDT_CertificateEntries",
                column: "CertificateId1");

            migrationBuilder.CreateIndex(
                name: "IX_CDT_CertificateEntries_PublicId",
                schema: "dbo",
                table: "CDT_CertificateEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_Certificates_CertificateNumber",
                schema: "dbo",
                table: "CDT_Certificates",
                column: "CertificateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_Certificates_PublicId",
                schema: "dbo",
                table: "CDT_Certificates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_ParameterAudit_PublicId",
                schema: "dbo",
                table: "CDT_ParameterAudit",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_Parameters_CreditLineId",
                schema: "dbo",
                table: "CDT_Parameters",
                column: "CreditLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_Parameters_PublicId",
                schema: "dbo",
                table: "CDT_Parameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_RatesByTerm_CreditLineId_AmountRangeStart_AmountRangeEnd_TermStart_TermEnd",
                schema: "dbo",
                table: "CDT_RatesByTerm",
                columns: new[] { "CreditLineId", "AmountRangeStart", "AmountRangeEnd", "TermStart", "TermEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDT_RatesByTerm_PublicId",
                schema: "dbo",
                table: "CDT_RatesByTerm",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ActivityEnrollments_PublicId",
                schema: "dbo",
                table: "LND_ActivityEnrollments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AssociateActivities_PublicId",
                schema: "dbo",
                table: "LND_AssociateActivities",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AssociateDiseases_PublicId",
                schema: "dbo",
                table: "LND_AssociateDiseases",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AssociateWithdrawals_PublicId",
                schema: "dbo",
                table: "LND_AssociateWithdrawals",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AuxAppInstallmentBeneficiaries_PublicId",
                schema: "dbo",
                table: "LND_AuxAppInstallmentBeneficiaries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AuxAppInstallments_PublicId",
                schema: "dbo",
                table: "LND_AuxAppInstallments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AuxiliaryApplicationLines_PublicId",
                schema: "dbo",
                table: "LND_AuxiliaryApplicationLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AuxiliaryApplications_PublicId",
                schema: "dbo",
                table: "LND_AuxiliaryApplications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_BiometricRecords_Person",
                schema: "dbo",
                table: "LND_BiometricRecords",
                column: "PersonCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_BiometricRecords_PublicId",
                schema: "dbo",
                table: "LND_BiometricRecords",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Blacklist_PublicId",
                schema: "dbo",
                table: "LND_Blacklist",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CashBases_PublicId",
                schema: "dbo",
                table: "LND_CashBases",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Cashiers_PublicId",
                schema: "dbo",
                table: "LND_Cashiers",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Checkbooks_PublicId",
                schema: "dbo",
                table: "LND_Checkbooks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CheckClearing_PublicId",
                schema: "dbo",
                table: "LND_CheckClearing",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContribRedParams_Period",
                schema: "dbo",
                table: "LND_ContributionReductionParams",
                column: "CutoffPeriod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContributionReductionParams_PublicId",
                schema: "dbo",
                table: "LND_ContributionReductionParams",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ContributionReductions_PublicId",
                schema: "dbo",
                table: "LND_ContributionReductions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositAccounts_AccountNumber",
                schema: "dbo",
                table: "LND_DepositAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositAccounts_PublicId",
                schema: "dbo",
                table: "LND_DepositAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositAudit_PublicId",
                schema: "dbo",
                table: "LND_DepositAudit",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DepositEntries_Account_Date",
                schema: "dbo",
                table: "LND_DepositEntries",
                columns: new[] { "AccountNumber", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_DepositEntries_PersonCode",
                schema: "dbo",
                table: "LND_DepositEntries",
                column: "PersonCode");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositEntries_PublicId",
                schema: "dbo",
                table: "LND_DepositEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositSeals_Account",
                schema: "dbo",
                table: "LND_DepositSeals",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositSeals_PublicId",
                schema: "dbo",
                table: "LND_DepositSeals",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DepositSignatures_PublicId",
                schema: "dbo",
                table: "LND_DepositSignatures",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Documents_PublicId",
                schema: "dbo",
                table: "LND_Documents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MinuteAttendees_PublicId",
                schema: "dbo",
                table: "LND_MinuteAttendees",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Minutes_PublicId",
                schema: "dbo",
                table: "LND_Minutes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_MoneyLaunderingDeclarations_PublicId",
                schema: "dbo",
                table: "LND_MoneyLaunderingDeclarations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PersonAssets_PublicId",
                schema: "dbo",
                table: "LND_PersonAssets",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SavingsAccounts_AccountNumber",
                schema: "dbo",
                table: "LND_SavingsAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SavingsAccounts_PublicId",
                schema: "dbo",
                table: "LND_SavingsAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SavingsParameters_LineId",
                schema: "dbo",
                table: "LND_SavingsParameters",
                column: "SavingsLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SavingsParameters_PublicId",
                schema: "dbo",
                table: "LND_SavingsParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SiplaGroupParameters_PublicId",
                schema: "dbo",
                table: "LND_SiplaGroupParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SiplaParameters_PublicId",
                schema: "dbo",
                table: "LND_SiplaParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Subsidies_Code",
                schema: "dbo",
                table: "LND_Subsidies",
                column: "SubsidyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Subsidies_PublicId",
                schema: "dbo",
                table: "LND_Subsidies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SubZones_Code",
                schema: "dbo",
                table: "LND_SubZones",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_SubZones_PublicId",
                schema: "dbo",
                table: "LND_SubZones",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_TransactionCodes_Code",
                schema: "dbo",
                table: "LND_TransactionCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_TransactionCodes_PublicId",
                schema: "dbo",
                table: "LND_TransactionCodes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_UnusualTransactionEntries_PublicId",
                schema: "dbo",
                table: "LND_UnusualTransactionEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_UnusualTransactions_PublicId",
                schema: "dbo",
                table: "LND_UnusualTransactions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_WithdrawalStatuses_PublicId",
                schema: "dbo",
                table: "LND_WithdrawalStatuses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_Zones_PublicId",
                schema: "dbo",
                table: "LND_Zones",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ZoneTypes_PublicId",
                schema: "dbo",
                table: "LND_ZoneTypes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ZoneTypes_TypeId",
                schema: "dbo",
                table: "LND_ZoneTypes",
                column: "ZoneTypeId",
                unique: true);
        }
    }
}
