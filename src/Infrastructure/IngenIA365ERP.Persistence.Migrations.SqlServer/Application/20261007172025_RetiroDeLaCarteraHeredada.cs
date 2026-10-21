using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Feature 013 — Cartera financiera, retiro de la cartera heredada (specs/013-cartera-financiera/tasks.md T0197, T0198;
    /// data-model.md parte 3 §13; decisiones-transversales.md T86–T89, C103; FR-144).
    ///
    /// <para>
    /// Destructiva con guarda (Principio XII; la cabecera de aprobación, con el diagnóstico, el respaldo y el segundo revisor, va
    /// al principio de <c>Up()</c>). Borra las 55 tablas <c>LND_*</c> del crédito heredado; las 38 conservadas de ahorros, aportes,
    /// SIPLA, zonas y servicios del asociado siguen con sus filas (su código se mudó a <c>Savings</c>). Se genera sobre el
    /// <b>modelo intermedio</b> —sin las 55 entidades y sin ninguna entidad <c>LND_</c> nueva—, después de <c>ValoresNacionales</c>,
    /// <c>PlataformaParaCartera</c>, <c>NominaRecaudaPorElContrato</c> y <c>ComercioConCartera</c> y antes de <c>CarteraNucleo</c>
    /// (C103), en commit propio.
    /// </para>
    ///
    /// <para>
    /// Qué hace, en orden: (1) guarda (SQL Server <c>THROW 50013</c>): cuenta las filas —incluidas las dadas de baja— de las 55 tablas
    /// (saltando las que no existan), de <c>LND_SiplaGroupParameters</c>, de <c>PAY_SettlementDeductions</c> y <c>PAY_Novelties</c>
    /// con <c>LoanPortfolioId</c> no nulo y de <c>ACC_Documents</c> con <c>OriginModule = 'CAR'</c>; si algo es mayor que cero y la
    /// base no trae la fila viva <c>COR_SystemSettings.SettingKey = 'LND.RetiroHeredado.Aprobado'</c>, se niega nombrando cada tabla
    /// con su cantidad, sin tocar nada; (2) suelta las 40 llaves de §13.2 y sus índices; (3) borra
    /// <c>PAY_SettlementDeductions.LoanPortfolioId</c> (después de su FK, su IX y <c>UK_PAY_SettlementDeductions_Termination_Loan</c>)
    /// y <c>PAY_Novelties.LoanPortfolioId</c>; (4) borra las 55 tablas. Los <c>ACC_Documents</c> con origen <c>CAR</c> no se tocan
    /// (son inmutables; pregunta H4).
    /// </para>
    ///
    /// <para>
    /// <c>Down()</c> recrea vacías las 55 tablas, sus llaves y las columnas de Nómina (generado por EF). Es irreversible en datos.
    /// </para>
    /// </summary>
    public partial class RetiroDeLaCarteraHeredada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MIGRACION-DESTRUCTIVA-APROBADA (Principio XII; feature 013, T0198; data-model parte 3 §13.3–§13.4).
            //   Diagnóstico: pendiente (T0183). Lo corre el dueño con specs/013-cartera-financiera/diagnostico-cartera-heredada.sql
            //     en la base local y en DEV y QA —cada base de cooperativa e ingenia365erp— antes de fusionar; aquí se anotan la
            //     fecha, los ambientes y las bases revisadas, las filas por tabla retirada, los ACC_Documents con origen CAR y la
            //     decisión tomada donde hubo filas. Corrido sólo en local el 2026-10-07 (PostgreSQL 5432: ingenia365erp, coop_alfa,
            //     coop_beta): 0 filas en las 55, en SIPLA, en Nómina con LoanPortfolioId y en ACC_Documents CAR; sin aprobación.
            //   Diagnóstico (2026-10-21): DEV y QA (ingenia365erp y coop_prueba) y producción (erp-pdn: ingenia365erp y cooflopal): 93 tablas LND_ por base,
            //     0 filas en las 55 retiradas y en las 38 conservadas; 0 filas en PAY_SettlementDeductions y PAY_Novelties con LoanPortfolioId y 0 ACC_Documents con
            //     origen CAR; sin aprobación (la guarda pasa sola). Resultados en docs/operaciones/cartera-retiro-heredado.md.
            //   Respaldo: pg_dump -Fc de cada base en /root/respaldos (erp-nonprod: erp-dev-*, erp-qa-*; erp-pdn: erp-pdn-ingenia365erp, erp-pdn-ingenia365erp_admin y
            //     erp-pdn-cooflopal), con sufijo 20261021-pre-f013-f014.dump y cabecera PGDMP.
            //   Segundo revisor: Jorman Copete (dueño), con su aprobación del 2026-10-21 para la promoción (decisiones D15 y H7).
            // Con la guarda, sólo se pierden filas que el dueño aprobó perder (fila LND.RetiroHeredado.Aprobado de la cooperativa).

            // (1) Guarda: sólo sobre la cartera heredada vacía, o con la aprobación del dueño en la base.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [dbo].[COR_SystemSettings]
                               WHERE [SettingKey] = N'LND.RetiroHeredado.Aprobado' AND [IsDeleted] = 0)
                BEGIN
                    DECLARE @tabla sysname, @filas bigint, @conFilas nvarchar(max), @sql nvarchar(max), @mensaje nvarchar(2048);
                    -- Las 55 tablas de credito, contando tambien las filas dadas de baja (COUNT_BIG(*) no mira IsDeleted).
                    DECLARE tablas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT t FROM (VALUES (N'LND_AccrualEntries'), (N'LND_AccrualPeriods'), (N'LND_ApplicationAssets'), (N'LND_ApplicationCodebtors'), (N'LND_ApplicationExtras'), (N'LND_ApplicationReferences'), (N'LND_ApplicationStatuses'), (N'LND_CollectionCases'), (N'LND_CollectionDateEntries'), (N'LND_CollectionMasters'), (N'LND_CollectionNoticeDetails'), (N'LND_CollectionNoticeParams'), (N'LND_CollectionNotices'), (N'LND_CollectionPeriods'), (N'LND_CreditLineAudit'), (N'LND_CreditLineParameters'), (N'LND_CreditParameters'), (N'LND_DeductionValues'), (N'LND_DefaultLiquidations'), (N'LND_DefaultRecords'), (N'LND_ExtraPaymentBalances'), (N'LND_ExtraPayments'), (N'LND_Guarantees'), (N'LND_HousingApplicationParams'), (N'LND_HousingParameters'), (N'LND_InsuranceBeneficiaries'), (N'LND_InsurancePolicies'), (N'LND_InterestRates'), (N'LND_InvoiceDetails'), (N'LND_InvoiceLineItems'), (N'LND_InvoiceMasters'), (N'LND_LoanApplications'), (N'LND_LoanPortfolios'), (N'LND_LoanRestructurings'), (N'LND_OnlineQueries'), (N'LND_PayrollDeductionConcepts'), (N'LND_PayrollDeductionEntries'), (N'LND_PayrollDeductionPeriods'), (N'LND_PayrollDeductions'), (N'LND_PendingInstallments'), (N'LND_PeriodicityParameters'), (N'LND_PortfolioAccounts'), (N'LND_PortfolioBalances'), (N'LND_PortfolioClassifications'), (N'LND_PortfolioInvoices'), (N'LND_PreviousInstallments'), (N'LND_ProvisionParameters'), (N'LND_RatesByTerm'), (N'LND_RecreationApplications'), (N'LND_RiskAssessments'), (N'LND_ScoringParameters'), (N'LND_ScoringRanges'), (N'LND_ShortLongTermPortfolio'), (N'LND_TermRates'), (N'LND_Transactions')) AS v(t);
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
                    -- SIPLA por grupo se conserva, pero su linea deja de tener llave: con filas, decide el dueno (H1).
                    IF OBJECT_ID(N'[dbo].[LND_SiplaGroupParameters]', N'U') IS NOT NULL
                    BEGIN
                        SELECT @filas = COUNT_BIG(*) FROM [dbo].[LND_SiplaGroupParameters];
                        IF @filas > 0
                            SET @conFilas = CONCAT(@conFilas, CASE WHEN @conFilas IS NULL THEN N'' ELSE N', ' END,
                                                   N'LND_SiplaGroupParameters (', @filas, N' filas)');
                    END
                    -- Nomina: la columna heredada que se borra (SQL dinamico, como el resto de la guarda).
                    SET @sql = N'SELECT @n = COUNT_BIG(*) FROM [dbo].[PAY_SettlementDeductions] WHERE [LoanPortfolioId] IS NOT NULL';
                    EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @filas OUTPUT;
                    IF @filas > 0
                        SET @conFilas = CONCAT(@conFilas, CASE WHEN @conFilas IS NULL THEN N'' ELSE N', ' END,
                                               N'PAY_SettlementDeductions.LoanPortfolioId (', @filas, N' filas)');
                    SET @sql = N'SELECT @n = COUNT_BIG(*) FROM [dbo].[PAY_Novelties] WHERE [LoanPortfolioId] IS NOT NULL';
                    EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @filas OUTPUT;
                    IF @filas > 0
                        SET @conFilas = CONCAT(@conFilas, CASE WHEN @conFilas IS NULL THEN N'' ELSE N', ' END,
                                               N'PAY_Novelties.LoanPortfolioId (', @filas, N' filas)');
                    -- Comprobantes del credito heredado: no se tocan (son inmutables), pero su existencia exige la decision del dueno (H4).
                    SELECT @filas = COUNT_BIG(*) FROM [dbo].[ACC_Documents] WHERE [OriginModule] = N'CAR';
                    IF @filas > 0
                        SET @conFilas = CONCAT(@conFilas, CASE WHEN @conFilas IS NULL THEN N'' ELSE N', ' END,
                                               N'ACC_Documents con OriginModule CAR (', @filas, N' filas)');
                    IF @conFilas IS NOT NULL
                    BEGIN
                        SET @mensaje = CONCAT(N'RetiroDeLaCarteraHeredada: la cartera heredada tiene filas: ', @conFilas,
                            N'. La migracion borra las 55 tablas de credito y la columna LoanPortfolioId de Nomina; solo se aplica sobre tablas vacias o con la aprobacion del dueno para esta cooperativa (fila COR_SystemSettings.SettingKey = ''LND.RetiroHeredado.Aprobado''), previo respaldo y segundo revisor (feature 013, FR-144, Principio XII; runbook docs/operaciones/cartera-retiro-heredado.md).');
                        THROW 50013, @mensaje, 1;
                    END
                END
                """);

            // (2) Las 40 llaves de data-model §13.2: hacia LND_CreditLineParameters (32 de las retiradas y la de
            //     LND_SiplaGroupParameters, que se conserva con CreditLineId como dato), hacia LND_LoanPortfolios (5),
            //     LND_LoanPortfolios → COR_People y PAY_SettlementDeductions → LND_LoanPortfolios; así el orden de los
            //     borrados no importa.
            migrationBuilder.DropForeignKey(
                name: "FK_LND_SiplaGroupParameters_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_SiplaGroupParameters");

            migrationBuilder.DropForeignKey(
                name: "FK_PAY_SettlementDeductions_LND_LoanPortfolios_LoanPortfolioId",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_AccrualEntries_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_AccrualEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_ApplicationStatuses_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_ApplicationStatuses");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CollectionDateEntries_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_CollectionDateEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CollectionMasters_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_CollectionMasters");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CollectionNoticeDetails_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_CollectionNoticeDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditLineAudit_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_CreditLineAudit");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_CreditParameters_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_CreditParameters");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_DefaultLiquidations_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_DefaultLiquidations");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_ExtraPaymentBalances_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_ExtraPaymentBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_HousingParameters_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_HousingParameters");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InsuranceBeneficiaries_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_InsuranceBeneficiaries");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InsurancePolicies_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_InsurancePolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_InvoiceLineItems_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_InvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanApplications_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_LoanApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanPortfolios_COR_People_PersonId",
                schema: "dbo",
                table: "LND_LoanPortfolios");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanPortfolios_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_LoanPortfolios");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_LoanRestructurings_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_LoanRestructurings");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_OnlineQueries_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_OnlineQueries");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PayrollDeductionConcepts_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_PayrollDeductionConcepts");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PayrollDeductions_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_PayrollDeductions");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PortfolioBalances_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_PortfolioBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PortfolioInvoices_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_PortfolioInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PreviousInstallments_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_PreviousInstallments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_RatesByTerm_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_RatesByTerm");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_RecreationApplications_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_RecreationApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_RiskAssessments_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_RiskAssessments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_ShortLongTermPortfolio_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_ShortLongTermPortfolio");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_TermRates_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_TermRates");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_DefaultRecords_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_DefaultRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_DefaultRecords_LND_LoanPortfolios_LoanPortfolioId",
                schema: "dbo",
                table: "LND_DefaultRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_ExtraPayments_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_ExtraPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_ExtraPayments_LND_LoanPortfolios_LoanPortfolioId",
                schema: "dbo",
                table: "LND_ExtraPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Guarantees_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_Guarantees");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Guarantees_LND_LoanPortfolios_LoanPortfolioId",
                schema: "dbo",
                table: "LND_Guarantees");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PendingInstallments_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_PendingInstallments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_PendingInstallments_LND_LoanPortfolios_LoanPortfolioId",
                schema: "dbo",
                table: "LND_PendingInstallments");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Transactions_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_LND_Transactions_LND_LoanPortfolios_LoanPortfolioId",
                schema: "dbo",
                table: "LND_Transactions");

            // Los índices que dependían de esas llaves (en SQL Server, antes que la columna).
            migrationBuilder.DropIndex(
                name: "IX_PAY_SettlementDeductions_LoanPortfolioId",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Loan",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropIndex(
                name: "IX_LND_SiplaGroupParameters_CreditLineId",
                schema: "dbo",
                table: "LND_SiplaGroupParameters");

            // (3) Nómina suelta la columna heredada; la historia quedó en ObligationPublicId/ObligationNumber
            //     (NominaRecaudaPorElContrato).
            migrationBuilder.DropColumn(
                name: "LoanPortfolioId",
                schema: "dbo",
                table: "PAY_SettlementDeductions");

            migrationBuilder.DropColumn(
                name: "LoanPortfolioId",
                schema: "dbo",
                table: "PAY_Novelties");

            // (4) Las 55 tablas de crédito (generado). Las 38 conservadas y ACC_Documents no se tocan.
            migrationBuilder.DropTable(
                name: "LND_AccrualEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_AccrualPeriods",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ApplicationAssets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ApplicationCodebtors",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ApplicationExtras",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ApplicationReferences",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ApplicationStatuses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionCases",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionDateEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionMasters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionNoticeDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionNoticeParams",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionNotices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CollectionPeriods",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLineAudit",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DeductionValues",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DefaultLiquidations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_DefaultRecords",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ExtraPaymentBalances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ExtraPayments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Guarantees",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_HousingApplicationParams",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_HousingParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InsuranceBeneficiaries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InsurancePolicies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InterestRates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InvoiceDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InvoiceLineItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_InvoiceMasters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanApplications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanRestructurings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_OnlineQueries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionConcepts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductionPeriods",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PayrollDeductions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PendingInstallments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PeriodicityParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PortfolioAccounts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PortfolioBalances",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PortfolioClassifications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PortfolioInvoices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_PreviousInstallments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ProvisionParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_RatesByTerm",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_RecreationApplications",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_RiskAssessments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ScoringParameters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ScoringRanges",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_ShortLongTermPortfolio",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_TermRates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_Transactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_LoanPortfolios",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "LND_CreditLineParameters",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recrea VACÍAS las 55 tablas, sus llaves y las columnas LoanPortfolioId de Nómina (generado). No restaura datos: lo
            // borrado sólo vuelve desde el respaldo pre-f013 (runbook docs/operaciones/cartera-retiro-heredado.md, «Restauración»).
            migrationBuilder.AddColumn<int>(
                name: "LoanPortfolioId",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoanPortfolioId",
                schema: "dbo",
                table: "PAY_Novelties",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LND_AccrualPeriods",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccrualPeriodNumber = table.Column<int>(type: "int", nullable: false),
                    AccrualScope = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AccrualService = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AccruedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PaymentCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsuryRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AccrualPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ApplicationAssets",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ApplicationNumber = table.Column<int>(type: "int", nullable: false),
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
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ApplicationAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ApplicationCodebtors",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    CodeudorCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmployerDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EmployerDeductionsCash = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MonthlyAvailable = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    OtherIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ParafiscalDeduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCapacityPct = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PensionDeduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PensionIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonalExpenses = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RentalIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ThirdPartyDebts = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VariableIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ApplicationCodebtors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ApplicationExtras",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApplicationNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ApplicationExtras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ApplicationReferences",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ApplicationNumber = table.Column<int>(type: "int", nullable: false),
                    CityCode = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ApplicationReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionCases",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyFrom = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CompanyTo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditLineFrom = table.Column<int>(type: "int", nullable: false),
                    CreditLineTo = table.Column<int>(type: "int", nullable: false),
                    DaysFrom = table.Column<int>(type: "int", nullable: false),
                    DaysTo = table.Column<int>(type: "int", nullable: false),
                    DeductionClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailSent = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsCumulative = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsManaged = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LegalCollection = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ManagementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ManagementStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PromiseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TotalOverdueAmount = table.Column<decimal>(type: "decimal(16,3)", precision: 16, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CollectionCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionNoticeParams",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatorName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DaysFrom = table.Column<int>(type: "int", nullable: false),
                    DaysTo = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Detail1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detail2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    NoticeCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SignatureImage = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CollectionNoticeParams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionNotices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CityCode = table.Column<int>(type: "int", nullable: false),
                    ConceptClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DaysFrom = table.Column<int>(type: "int", nullable: true),
                    DaysTo = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Detail1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Detail2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    NoticeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NoticeNumber = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SendToCodeudor = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TrailingLegend = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CollectionNotices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionPeriods",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyFrom = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CompanyTo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditLineFrom = table.Column<int>(type: "int", nullable: false),
                    CreditLineTo = table.Column<int>(type: "int", nullable: false),
                    DaysFrom = table.Column<int>(type: "int", nullable: false),
                    DaysTo = table.Column<int>(type: "int", nullable: false),
                    DeductionClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastPersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LegalCollection = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SortOrder = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CollectionPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLineParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountInterestAdvance = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AccountInterestCxC = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AccountInterestDefault = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AccountInterestIncome = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AccountInterestOrderCredit = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AccountInterestOrderDebit = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AccountStatement = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AccrualRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    AdditionalChargesConcept = table.Column<int>(type: "int", nullable: false),
                    AdminClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AdminConceptCode = table.Column<int>(type: "int", nullable: false),
                    AdminForm = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AdminRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    AdminValueMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminValueMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AffectsFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AllowExtension = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    BalanceConsult = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BasePercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BlockOverdueAssociate = table.Column<int>(type: "int", nullable: false),
                    BlockProjectionDate = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CalculateVat = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CalculationBase = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CapitalAtRisk = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CapitalForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CapitalizationConcept = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CategoryA = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CategoryB = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CategoryC = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CategoryD = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CategoryE = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CdatInterestRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosingInterest = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ColumnCount = table.Column<int>(type: "int", nullable: false),
                    ColumnTitle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConceptId = table.Column<int>(type: "int", nullable: false),
                    ConsecutiveCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditLimitAvailable = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditLimitCalcMethod = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreditLimitType = table.Column<int>(type: "int", nullable: false),
                    CreditLimitValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    DebitCreditFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DefaultInterestFlag = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DiscountConceptId = table.Column<int>(type: "int", nullable: false),
                    DtfRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EquivalentRate = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExportCifin = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExtraPaymentApply = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExtraRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    FinancialInterest = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    FogaClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    FogaContribution = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    FormatId = table.Column<int>(type: "int", nullable: false),
                    GracePeriodFlag = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    GracePeriodMonths = table.Column<int>(type: "int", nullable: false),
                    GuaranteeClass = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    GuarantorRequired = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    HousingLoan = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IncomeTaxFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InstallmentType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    InsuranceConceptCode = table.Column<int>(type: "int", nullable: false),
                    InsuranceRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    InsuranceValueMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceValueMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestConceptCode = table.Column<int>(type: "int", nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    InterestType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    InterestTypeCode = table.Column<int>(type: "int", nullable: false),
                    InternetEnabled = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InvoiceEnabled = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InvoiceGroup = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsVatLine = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LegacyLinCred = table.Column<int>(type: "int", nullable: true),
                    LiquidateDefaultDays = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    MaturityBehavior = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxTerm = table.Column<int>(type: "int", nullable: false),
                    MinContribution = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ModifyInstallmentType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    MonthsInAdvance = table.Column<int>(type: "int", nullable: false),
                    PayrollCompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    PayrollConceptCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    PeaceSalvoSeniority = table.Column<int>(type: "int", nullable: false),
                    PendingContribution = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PrimaryVoucher = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ProjectionItem = table.Column<int>(type: "int", nullable: false),
                    ProvisionAccountCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    ProvisionExpenseAccountCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SavingsCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SecondaryCostCenter = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ShowBalance = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    SumGuarantee = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SuperintendencyEquivalent = table.Column<int>(type: "int", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPriorInterest = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VatAccount = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    VatLineId = table.Column<int>(type: "int", nullable: false),
                    VatPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CreditLineParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_DeductionValues",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ConceptCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsAdditional = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DeductionValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_HousingApplicationParams",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrencyType = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisbursementType = table.Column<int>(type: "int", nullable: false),
                    HasSubsidy = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    HousingClass = table.Column<int>(type: "int", nullable: false),
                    HousingType = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    NetworkEntity = table.Column<int>(type: "int", nullable: false),
                    NetworkValue = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SocialInterest = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_HousingApplicationParams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_InterestRates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditLineCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PortfolioBalance = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    PortfolioClass = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_InterestRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_InvoiceDetails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DetailCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DetailText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_InvoiceDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_InvoiceMasters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountingPeriod = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cycle = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EncodedData = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InvoiceCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    InvoiceConsecutive = table.Column<long>(type: "bigint", nullable: false),
                    InvoicedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastPaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LastPaymentDate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_InvoiceMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccumulatedAmount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ConceptCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EndDate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(12,0)", precision: 12, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PayrollDeductionEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionPeriods",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ArrearsExtras = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ArrearsFrom = table.Column<int>(type: "int", nullable: false),
                    ArrearsTo = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ContributionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeductionClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DefaultAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExtraAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAdditional = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LoanAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PayrollDeductionPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_PeriodicityParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DayCount = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DeductionClass = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EndDay = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StartDay = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PeriodicityParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_PortfolioAccounts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeductionClass = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuaranteeType = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordClass = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PortfolioAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_PortfolioClassifications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountingPeriod = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ConceptCode = table.Column<int>(type: "int", nullable: false),
                    ContributionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditClass = table.Column<int>(type: "int", nullable: false),
                    CreditLineCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DaysOverdue = table.Column<int>(type: "int", nullable: false),
                    DeductionClass = table.Column<int>(type: "int", nullable: false),
                    DefaultBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultOrderBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuaranteeClass = table.Column<int>(type: "int", nullable: false),
                    InterestBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestProvision = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OrderBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PersonName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PortfolioNumber = table.Column<int>(type: "int", nullable: false),
                    ProvisionBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(10,0)", precision: 10, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TotalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PortfolioClassifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ProvisionParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateB = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    RateC = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    RateD = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    RateE = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ProvisionParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ScoringParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CriterionCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CriterionName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Percentage = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SubItemCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SubItemName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ScoringParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_ScoringRanges",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CriterionCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Equality = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RangeEnd = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RangeStart = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ScoreValue = table.Column<int>(type: "int", nullable: false),
                    SubItemCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ScoringRanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LND_AccrualEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccrualPeriod = table.Column<int>(type: "int", nullable: false),
                    AffectsCapital = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AffectsExtras = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AffectsInterest = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AppliesExtras = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AppliesToSavings = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AppliesToServices = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Authorization = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FixedInstallments = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Installments = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    SystemDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_AccrualEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_AccrualEntries_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_ApplicationStatuses",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccountNumber = table.Column<int>(type: "int", nullable: false),
                    ApplicationComplete = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ApplicationNumber = table.Column<int>(type: "int", nullable: false),
                    AssetCashBank = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetHousing = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetQuality = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AssetReceivables = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetSavings = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetVehicle = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalAtRisk = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    CashPayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CifinScore = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    ClaAbr1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAbr2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAbr3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAbr4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAbr5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAgo1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAgo2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAgo3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAgo4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaAgo5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaDic1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaDic2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaDic3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaDic4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaDic5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaEne1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaEne2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaEne3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaEne4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaEne5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaFeb1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaFeb2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaFeb3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaFeb4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaFeb5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJul1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJul2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJul3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJul4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJul5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJun1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJun2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJun3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJun4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaJun5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMar1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMar2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMar3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMar4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMar5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMay1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMay2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMay3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMay4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaMay5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaNov1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaNov2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaNov3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaNov4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaNov5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaOct1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaOct2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaOct3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaOct4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaOct5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaSep1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaSep2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaSep3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaSep4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ClaSep5 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ContingencyLevel = table.Column<decimal>(type: "decimal(15,6)", precision: 15, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DataUpdated = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DatacreditoOverdue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DatacreditoRating = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DatacreditoScore = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    DebtConsolidation = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiscoveredAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Equity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Evaluation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExternalDebtBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExternalDebtInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HasApplication = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    HasOtherDocs = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    HasPayrollDeduction = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    HasPromissory = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IncomeProof = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IndebtednessLevel = table.Column<decimal>(type: "decimal(15,6)", precision: 15, scale: 6, nullable: false),
                    InsuranceExtra = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LiabilityBankLoans = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiabilityDebts = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiabilityMortgage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiabilityOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ObligationsOk = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PaymasterDeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PaymasterPercentage = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    PaymentCapacity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayrollPayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PercentageFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PersonalExpenses = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceVerification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    StudyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalAssets = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLiabilities = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLiabilitiesEquity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ApplicationStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ApplicationStatuses_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionDateEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    CollectionCaseId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PromiseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CollectionDateEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CollectionDateEntries_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionMasters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccumulatedDefault = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectionCaseId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DaysOverdue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ManagementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OverdueAdmin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdueCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdueExtras = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdueInsurance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdueInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdueOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TotalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CollectionMasters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CollectionMasters_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_CollectionNoticeDetails",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Address1 = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Address2 = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Address3 = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Address4 = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AdminBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    City1 = table.Column<int>(type: "int", nullable: false),
                    City2 = table.Column<int>(type: "int", nullable: false),
                    City3 = table.Column<int>(type: "int", nullable: false),
                    City4 = table.Column<int>(type: "int", nullable: false),
                    Codeudor1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConceptClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cycle = table.Column<int>(type: "int", nullable: false),
                    DaysOverdue = table.Column<int>(type: "int", nullable: false),
                    DefaultBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaturityDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NoticeNumber = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OnlyDefaulted = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    OtherBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TotalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CollectionNoticeDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CollectionNoticeDetails_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditLineAudit",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: true),
                    AccountCode_New = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    AccountCode_Old = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    AccrualRate_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AccrualRate_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AdminMax_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdminMax_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdminMin_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdminMin_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdminRate_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdminRate_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AffectsFlag_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AffectsFlag_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AllowExtension_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AllowExtension_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CapitalForm_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CapitalForm_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditLimit_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreditLimit_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DebitCreditFlag_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DebitCreditFlag_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DefaultInterest_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DefaultInterest_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description_New = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Description_Old = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtraRate_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExtraRate_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FinancialInterest_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    FinancialInterest_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    GracePeriod_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    GracePeriod_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    GuaranteeClass_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    GuaranteeClass_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    InsMax_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InsMax_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InsMin_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InsMin_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InstallmentType_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    InstallmentType_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    InsuranceRate_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InsuranceRate_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InterestRate_New = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: true),
                    InterestRate_Old = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: true),
                    InterestType_New = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    InterestType_Old = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaxAmount_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxAmount_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MaxTerm_New = table.Column<int>(type: "int", nullable: true),
                    MaxTerm_Old = table.Column<int>(type: "int", nullable: true),
                    MinContribution_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MinContribution_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ShortName_New = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ShortName_Old = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaxRate_New = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxRate_Old = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId_New = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UserId_Old = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UserName_New = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UserName_Old = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CreditLineAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditLineAudit_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LND_CreditParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceCreditLineId = table.Column<int>(type: "int", nullable: false),
                    AdvanceRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceTerm1 = table.Column<int>(type: "int", nullable: false),
                    AdvanceTerm2 = table.Column<int>(type: "int", nullable: false),
                    AdvanceTerm3 = table.Column<int>(type: "int", nullable: false),
                    AdvanceTerm4 = table.Column<int>(type: "int", nullable: false),
                    AdvanceTerm5 = table.Column<int>(type: "int", nullable: false),
                    AdvanceTerm6 = table.Column<int>(type: "int", nullable: false),
                    AdvanceTerm7 = table.Column<int>(type: "int", nullable: false),
                    AdvanceType = table.Column<int>(type: "int", nullable: false),
                    Amount1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount3 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount4 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount6 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount7 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(10,0)", precision: 10, nullable: false),
                    CutoffDay = table.Column<int>(type: "int", nullable: false),
                    DeductionClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpirationDays = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaintenanceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaintenanceLineId = table.Column<int>(type: "int", nullable: false),
                    Period1 = table.Column<int>(type: "int", nullable: true),
                    Period2 = table.Column<int>(type: "int", nullable: true),
                    Period3 = table.Column<int>(type: "int", nullable: true),
                    Period4 = table.Column<int>(type: "int", nullable: true),
                    Period5 = table.Column<int>(type: "int", nullable: true),
                    Period6 = table.Column<int>(type: "int", nullable: false),
                    Period7 = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    Term1 = table.Column<int>(type: "int", nullable: true),
                    Term2 = table.Column<int>(type: "int", nullable: true),
                    Term3 = table.Column<int>(type: "int", nullable: true),
                    Term4 = table.Column<int>(type: "int", nullable: true),
                    Term5 = table.Column<int>(type: "int", nullable: true),
                    Term6 = table.Column<int>(type: "int", nullable: false),
                    Term7 = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_CreditParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditParameters_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_DefaultLiquidations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccrualPeriod = table.Column<int>(type: "int", nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeductionClass = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GraceDays = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastLiquidationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LiquidationAmount = table.Column<int>(type: "int", nullable: false),
                    LiquidationBase = table.Column<int>(type: "int", nullable: false),
                    LiquidationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LiquidationDays = table.Column<int>(type: "int", nullable: false),
                    LiquidationRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    LiquidationType = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UsuryRate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DefaultLiquidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DefaultLiquidations_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_ExtraPaymentBalances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraNumber = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ExtraPaymentBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ExtraPaymentBalances_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_HousingParameters",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrencyType = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisbursementType = table.Column<int>(type: "int", nullable: false),
                    HasSubsidy = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    HousingClass = table.Column<int>(type: "int", nullable: false),
                    HousingType = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    NetworkEntity = table.Column<int>(type: "int", nullable: false),
                    NetworkValue = table.Column<long>(type: "bigint", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SocialInterest = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_HousingParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_HousingParameters_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_InsuranceBeneficiaries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    BeneficiaryId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    InsurancePercentage = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_InsuranceBeneficiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_InsuranceBeneficiaries_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_InsurancePolicies",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InsuranceAmount = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    InsuranceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsHistorical = table.Column<bool>(type: "bit", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_InsurancePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_InsurancePolicies_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_InvoiceLineItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccrualPeriod = table.Column<int>(type: "int", nullable: false),
                    CapitalBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    InsuranceBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    InterestBalance = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_InvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_InvoiceLineItems_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanApplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AdditionalContribution = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminConceptCode = table.Column<int>(type: "int", nullable: false),
                    AdminForm = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AdminInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminRate = table.Column<decimal>(type: "decimal(12,5)", precision: 12, scale: 5, nullable: false),
                    AdminType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ApplicationNumber = table.Column<int>(type: "int", nullable: false),
                    ApprovalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetCashBank = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetContributions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetHousing = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetReceivables = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetSavings = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AssetVehicle = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AuthorizedFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AuthorizingUser = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AuthorizingUserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AvailableCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvailableMonthly = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CadastralAppraisal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalAtRisk = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalizationType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CashLabel = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CdatNumber = table.Column<int>(type: "int", nullable: false),
                    ClosingInterestType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Codeudor1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CommercialAppraisal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ConceptRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContingencyLevel = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContractType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ContributionsAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CoopEntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DebtConsolidation = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionCapacity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisbursedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DisbursementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DiscountCompany = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DiscoveredAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DtfRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    EmployerEntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployerName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmployerPayrollDeduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EntryUserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Equity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraInAdvance = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExtraInMonth = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExtraPaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FirstPaymentFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GracePeriodCycle = table.Column<int>(type: "int", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    GracePeriodEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GracePeriodFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GracePeriodInstallment = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GracePeriodStart = table.Column<int>(type: "int", nullable: false),
                    GracePeriodStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GuaranteeDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuaranteeType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    HasVehicle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IndebtednessLevel = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InsuranceConceptCode = table.Column<int>(type: "int", nullable: false),
                    InsuranceExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InsuranceInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsurancePercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    InsuranceType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    InsurerIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InsurerName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InterestInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(7,6)", precision: 7, scale: 6, nullable: false),
                    InterestType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsInsured = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LegacyNumero = table.Column<int>(type: "int", nullable: true),
                    LiabilityBankLoans = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiabilityDebts = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiabilityMortgage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LiabilityOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinutesDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MinutesNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MonthlyDeductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyFixedExpenses = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyPayments = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherConceptCode = table.Column<int>(type: "int", nullable: false),
                    OtherIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    OverdueBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OwnsHouse = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ParafiscalDeduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymasterDeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PaymasterPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCapacity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCapacityDebtPickup = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PaymentCapacityPct = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PaymentCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PayrollCapacity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayrollDeductionNumber = table.Column<int>(type: "int", nullable: false),
                    PayrollLabel = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayrollPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PensionDeduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PensionIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PersonalExpenses = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    PromissoryNumber = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedFromPaymasterDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RentalIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SecondPaymentType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SentToPaymaster = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SentToPaymasterDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SolicitedCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SolicitedDeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SolicitedDtfRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SolicitedInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SolicitedInterestRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SolicitedPeriodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SolicitedSpreadPoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SolicitedTerm = table.Column<int>(type: "int", nullable: false),
                    SpouseDependents = table.Column<int>(type: "int", nullable: false),
                    SpouseEmployer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SpouseEmployerAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SpouseEmployerCity = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    SpouseIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SpouseName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SpousePhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SpouseSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SpouseWorks = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SpreadPoints = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    ThirdPartyDebts = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAssets = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLiabilities = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLiabilitiesEquity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VariableIncome = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VehicleDescription = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_LoanApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanApplications_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanPortfolios",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    AdditionalCharges = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminAccrued = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminAppliedPayroll = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminBalanceCurrent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminBalanceMonthly = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminBalancePayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminFeeRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    AdminInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ApplicationNumber = table.Column<int>(type: "int", nullable: false),
                    ApprovalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AuthCreditBureau = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CapitalAccrued = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalAppliedPayroll = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalBalanceCurrent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalBalanceMonthly = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalBalancePayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CardNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CifinDaysOverdue = table.Column<int>(type: "int", nullable: false),
                    CifinEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CifinOverdueBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CifinOverdueInstallments = table.Column<int>(type: "int", nullable: false),
                    CifinStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Codeudor1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Codeudor4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContributionsAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CxcInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DaysOverdue = table.Column<int>(type: "int", nullable: false),
                    DeductionType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DefaultAppliedPayroll = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultBalanceCurrent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultBalanceMonthly = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultBalancePayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisbursementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DiscountCompany = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DiscountStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DocumentNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DtfRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraInAdvance = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExtraInMonth = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExtraPaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FirstPaymentFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GracePeriodDays = table.Column<int>(type: "int", nullable: false),
                    GracePeriodInstallment = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GracePeriodStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GuaranteeType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    GuaranteeValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HasLawsuit = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IncludeAutoDebit = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InitialGracePeriod = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    InsuranceAccrued = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceAppliedPayroll = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceBalanceCurrent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceBalanceMonthly = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceBalancePayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceForm = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    InsuranceInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    InterestAccrued = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestAppliedPayroll = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestBalanceCurrent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestBalanceMonthly = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestBalancePayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestInstallmentAmt = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    InterestType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsFopep = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsRestructured = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsWrittenOff = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LastAccrualDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastDefaultDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LawyerCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    LawyerNotes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LegacyLinCred = table.Column<int>(type: "int", nullable: true),
                    LegacyNumero = table.Column<long>(type: "bigint", nullable: true),
                    LegalCollection = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    LessAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LocalClearing = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OrderInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherClearing = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidInstallments = table.Column<int>(type: "int", nullable: false),
                    PaidInstallmentsAgency = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCycle = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    PaymentPeriodicity = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    PayrollCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PendingInstallmentCount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    ProposedInterestDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProvisionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProvisionRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RestructureCategory = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    RestructureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SearchNumber = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SecondPaymentType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SpreadPoints = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: false),
                    TotalPriorInterest = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    TransferConcept = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WriteOffApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WriteOffCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WriteOffDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WriteOffInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WriteOffMinutes = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_LoanPortfolios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanPortfolios_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LND_LoanPortfolios_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_LoanRestructurings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OriginalCreditLineId = table.Column<int>(type: "int", nullable: false),
                    OriginalPersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginalPortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestructureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SystemDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TimesRestructured = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_LoanRestructurings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_LoanRestructurings_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_OnlineQueries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ItemCode = table.Column<int>(type: "int", nullable: false),
                    LegacyConsecutivo = table.Column<int>(type: "int", nullable: true),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueryAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QueryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VoucherNumber = table.Column<long>(type: "bigint", nullable: false),
                    VoucherType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_OnlineQueries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_OnlineQueries_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductionConcepts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraConceptCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    InterestConceptCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PayrollConceptCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PayrollDeductionConcepts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductionConcepts_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_PayrollDeductions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AdminAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CodeudorCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ContributionAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContributionApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExtraAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InsuranceApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAdditional = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LoanAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LoanApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherApplied = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PayrollDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PayrollDeductions_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_PortfolioBalances",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CifinStatus = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeductionClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FinalCategory = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    FinalLastPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitialCategory = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InitialLastPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OpenInstallments = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCycle = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Period = table.Column<int>(type: "int", nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PortfolioBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PortfolioBalances_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_PortfolioInvoices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    InvoiceNumber = table.Column<decimal>(type: "decimal(20,0)", precision: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VoucherType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PortfolioInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PortfolioInvoices_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_PreviousInstallments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccountingPeriod = table.Column<int>(type: "int", nullable: false),
                    AccrualPeriod = table.Column<int>(type: "int", nullable: false),
                    AdvanceAdmin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AdvanceExtra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceInsurance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TransactionSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PreviousInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PreviousInstallments_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_RatesByTerm",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiscountType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RateValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TermEnd = table.Column<int>(type: "int", nullable: false),
                    TermStart = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_RatesByTerm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_RatesByTerm_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_RecreationApplications",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AdditionalInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ApplicationNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditNumber = table.Column<int>(type: "int", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FullOrPartial = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_RecreationApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_RecreationApplications_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_RiskAssessments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AdminAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    CapitalPayment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    InstallmentNumber = table.Column<int>(type: "int", nullable: false),
                    InsuranceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Period = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioClass = table.Column<int>(type: "int", nullable: false),
                    PortfolioNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    TotalBalance = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_RiskAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_RiskAssessments_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_ShortLongTermPortfolio",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    AffectsFlag = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CostCenterName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    FogaClass = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Month1 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month10 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month11 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month12 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month2 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month3 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month4 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month5 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month6 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month7 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month8 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Month9 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MoreThan12 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Period = table.Column<int>(type: "int", nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PersonName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ShortLongTermPortfolio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ShortLongTermPortfolio_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_TermRates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AmountEnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountStart = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuaranteeType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxTerm = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SeniorityEnd = table.Column<int>(type: "int", nullable: false),
                    SeniorityStart = table.Column<int>(type: "int", nullable: false),
                    TermEnd = table.Column<int>(type: "int", nullable: false),
                    TermStart = table.Column<int>(type: "int", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_TermRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_TermRates_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LND_DefaultRecords",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccountingPeriod = table.Column<int>(type: "int", nullable: false),
                    AccrualPeriod = table.Column<int>(type: "int", nullable: false),
                    AccruedAdmin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedDefault = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedExtra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInsurance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdminBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CapitalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CxcFlag = table.Column<int>(type: "int", nullable: false),
                    DaysOverdue = table.Column<int>(type: "int", nullable: false),
                    DefaultBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraNumber = table.Column<int>(type: "int", nullable: false),
                    InsuranceBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsManaged = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    LastLiquidationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LoanPortfolioId = table.Column<int>(type: "int", nullable: true),
                    OtherBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAdmin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidDefault = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidExtra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidInsurance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PriorAdminBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorCapitalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorDefaultBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorExtraBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorInsuranceBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorInterestBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorOtherBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_DefaultRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_DefaultRecords_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LND_DefaultRecords_LND_LoanPortfolios_LoanPortfolioId",
                        column: x => x.LoanPortfolioId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanPortfolios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LND_ExtraPayments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ChargesAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExtraNumber = table.Column<int>(type: "int", nullable: false),
                    ExtraType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LoanPortfolioId = table.Column<int>(type: "int", nullable: true),
                    PaymentCycle = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PaymentsAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_ExtraPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_ExtraPayments_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LND_ExtraPayments_LND_LoanPortfolios_LoanPortfolioId",
                        column: x => x.LoanPortfolioId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanPortfolios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LND_Guarantees",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CadastralAppraisal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CdatNumber = table.Column<int>(type: "int", nullable: false),
                    CommercialAppraisal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GuaranteeCancelDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GuaranteeDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GuaranteeStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GuaranteeStatus = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GuaranteeType = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Guarantor1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Guarantor2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Guarantor3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Guarantor4 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HasInsurance = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    InsuredPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    InsurerIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InsurerName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LoanPortfolioId = table.Column<int>(type: "int", nullable: true),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Term = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Guarantees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_Guarantees_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LND_Guarantees_LND_LoanPortfolios_LoanPortfolioId",
                        column: x => x.LoanPortfolioId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanPortfolios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LND_PendingInstallments",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccountingPeriod = table.Column<int>(type: "int", nullable: false),
                    AccrualPeriod = table.Column<int>(type: "int", nullable: false),
                    AccruedAdmin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedAll = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AccruedCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedExtra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInsurance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedNoExtra = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AccruedOnlyExtra = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    AccruedOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceExtra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAdmin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceExtra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceInsurance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Cycle = table.Column<int>(type: "int", nullable: false),
                    DaysOverdue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DaysToMaturity = table.Column<int>(type: "int", nullable: false),
                    DeductionClass = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DeductionType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    DefaultInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultInterestAccrued = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultInterestBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultInterestPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DtfRate = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    EntryReason = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ExtraNumber = table.Column<int>(type: "int", nullable: false),
                    ExtraPaymentForm = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    GuaranteeClass = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    IsAdvancePayment = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LastDefaultDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LegacyCodigoTer = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    LoanPortfolioId = table.Column<int>(type: "int", nullable: true),
                    ObligationInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAdmin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidCapital = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidExtra = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidInsurance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidInterest = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Periodicity = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PriorAdminBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorCapitalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorExtraBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorInsuranceBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorInterestBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriorOtherBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProcessDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SpreadPoints = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    SystemDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalInstallment = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_PendingInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_PendingInstallments_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LND_PendingInstallments_LND_LoanPortfolios_LoanPortfolioId",
                        column: x => x.LoanPortfolioId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanPortfolios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LND_Transactions",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreditLineId = table.Column<int>(type: "int", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    AuxiliaryApplicationId = table.Column<int>(type: "int", nullable: false),
                    BankCode = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    BranchId = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CdatAccrualDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodeudorCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CostCenterId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CrossDocumentNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CrossDocumentType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Cycles = table.Column<int>(type: "int", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DependsOnSequence = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DocumentNumber = table.Column<long>(type: "bigint", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExtraNumber = table.Column<int>(type: "int", nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InterestRate = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsAdvancePayment = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    LegacySecuencia = table.Column<long>(type: "bigint", nullable: true),
                    LoanPortfolioId = table.Column<int>(type: "int", nullable: true),
                    LocalCheckAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherCheckAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OverdraftAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    OverdraftUser = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Period = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    PersonCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PortfolioNumber = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReliquidatedInstallment = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReliquidationFlag = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SavingsLineId = table.Column<int>(type: "int", nullable: true),
                    SearchCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    SecondaryAccount = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    SecondaryCostCenter = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SystemDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TransactionCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TransactionSource = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserFullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VoucherType = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    WithholdingBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LND_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_Transactions_LND_CreditLineParameters_CreditLineId",
                        column: x => x.CreditLineId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditLineParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LND_Transactions_LND_LoanPortfolios_LoanPortfolioId",
                        column: x => x.LoanPortfolioId,
                        principalSchema: "dbo",
                        principalTable: "LND_LoanPortfolios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PAY_SettlementDeductions_LoanPortfolioId",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                column: "LoanPortfolioId");

            migrationBuilder.CreateIndex(
                name: "UK_PAY_SettlementDeductions_Termination_Loan",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                columns: new[] { "TerminationId", "LoanPortfolioId" },
                unique: true,
                filter: "[LoanPortfolioId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LND_SiplaGroupParameters_CreditLineId",
                schema: "dbo",
                table: "LND_SiplaGroupParameters",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_AccrualEntries_CreditLineId",
                schema: "dbo",
                table: "LND_AccrualEntries",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccrualEntries_PublicId",
                schema: "dbo",
                table: "LND_AccrualEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AccrualPeriods_PublicId",
                schema: "dbo",
                table: "LND_AccrualPeriods",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ApplicationAssets_PublicId",
                schema: "dbo",
                table: "LND_ApplicationAssets",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ApplicationCodebtors_PublicId",
                schema: "dbo",
                table: "LND_ApplicationCodebtors",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ApplicationExtras_PublicId",
                schema: "dbo",
                table: "LND_ApplicationExtras",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ApplicationReferences_PublicId",
                schema: "dbo",
                table: "LND_ApplicationReferences",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ApplicationStatuses_CreditLineId",
                schema: "dbo",
                table: "LND_ApplicationStatuses",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ApplicationStatuses_PublicId",
                schema: "dbo",
                table: "LND_ApplicationStatuses",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionCases_PublicId",
                schema: "dbo",
                table: "LND_CollectionCases",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionDateEntries_CreditLineId",
                schema: "dbo",
                table: "LND_CollectionDateEntries",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionDateEntries_PublicId",
                schema: "dbo",
                table: "LND_CollectionDateEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionMasters_CreditLineId",
                schema: "dbo",
                table: "LND_CollectionMasters",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionMasters_PublicId",
                schema: "dbo",
                table: "LND_CollectionMasters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CollectionNoticeDetails_CreditLineId",
                schema: "dbo",
                table: "LND_CollectionNoticeDetails",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionNoticeDetails_PublicId",
                schema: "dbo",
                table: "LND_CollectionNoticeDetails",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionNoticeParams_Code",
                schema: "dbo",
                table: "LND_CollectionNoticeParams",
                column: "NoticeCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionNoticeParams_PublicId",
                schema: "dbo",
                table: "LND_CollectionNoticeParams",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionNotices_PublicId",
                schema: "dbo",
                table: "LND_CollectionNotices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_CollectionPeriods_PublicId",
                schema: "dbo",
                table: "LND_CollectionPeriods",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLineAudit_CreditLineId",
                schema: "dbo",
                table: "LND_CreditLineAudit",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditLineAudit_PublicId",
                schema: "dbo",
                table: "LND_CreditLineAudit",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_CreditLineParameters",
                column: "CreditLineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditLineParameters_PublicId",
                schema: "dbo",
                table: "LND_CreditLineParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditParameters_CreditLineId",
                schema: "dbo",
                table: "LND_CreditParameters",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditParameters_PublicId",
                schema: "dbo",
                table: "LND_CreditParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_DeductionValues_PublicId",
                schema: "dbo",
                table: "LND_DeductionValues",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DefaultLiquidations_CreditLineId",
                schema: "dbo",
                table: "LND_DefaultLiquidations",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DefaultLiquidations_PublicId",
                schema: "dbo",
                table: "LND_DefaultLiquidations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_DefaultRecords_CreditLineId",
                schema: "dbo",
                table: "LND_DefaultRecords",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_DefaultRecords_LoanPortfolioId",
                schema: "dbo",
                table: "LND_DefaultRecords",
                column: "LoanPortfolioId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_DefaultRecords_PublicId",
                schema: "dbo",
                table: "LND_DefaultRecords",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ExtraPaymentBalances_CreditLineId",
                schema: "dbo",
                table: "LND_ExtraPaymentBalances",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ExtraPaymentBalances_PublicId",
                schema: "dbo",
                table: "LND_ExtraPaymentBalances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ExtraPayments_CreditLineId",
                schema: "dbo",
                table: "LND_ExtraPayments",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_ExtraPayments_LoanPortfolioId",
                schema: "dbo",
                table: "LND_ExtraPayments",
                column: "LoanPortfolioId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ExtraPayments_PublicId",
                schema: "dbo",
                table: "LND_ExtraPayments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_Guarantees_CreditLineId",
                schema: "dbo",
                table: "LND_Guarantees",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Guarantees_LoanPortfolioId",
                schema: "dbo",
                table: "LND_Guarantees",
                column: "LoanPortfolioId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Guarantees_PublicId",
                schema: "dbo",
                table: "LND_Guarantees",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_HousingApplicationParams_PublicId",
                schema: "dbo",
                table: "LND_HousingApplicationParams",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_HousingAppParams_App",
                schema: "dbo",
                table: "LND_HousingApplicationParams",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_HousingParameters_CreditLineId",
                schema: "dbo",
                table: "LND_HousingParameters",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_HousingParameters_PublicId",
                schema: "dbo",
                table: "LND_HousingParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_InsuranceBeneficiaries_CreditLineId",
                schema: "dbo",
                table: "LND_InsuranceBeneficiaries",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InsuranceBeneficiaries_PublicId",
                schema: "dbo",
                table: "LND_InsuranceBeneficiaries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_InsurancePolicies_CreditLineId",
                schema: "dbo",
                table: "LND_InsurancePolicies",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InsurancePolicies_PublicId",
                schema: "dbo",
                table: "LND_InsurancePolicies",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_InterestRates_PublicId",
                schema: "dbo",
                table: "LND_InterestRates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_InvoiceDetails_Code",
                schema: "dbo",
                table: "LND_InvoiceDetails",
                column: "DetailCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_InvoiceDetails_PublicId",
                schema: "dbo",
                table: "LND_InvoiceDetails",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_InvoiceLineItems_CreditLineId",
                schema: "dbo",
                table: "LND_InvoiceLineItems",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_InvoiceLineItems_PublicId",
                schema: "dbo",
                table: "LND_InvoiceLineItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_InvoiceMasters_PublicId",
                schema: "dbo",
                table: "LND_InvoiceMasters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanApplications_CreditLineId",
                schema: "dbo",
                table: "LND_LoanApplications",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanApplications_Number",
                schema: "dbo",
                table: "LND_LoanApplications",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanApplications_PublicId",
                schema: "dbo",
                table: "LND_LoanApplications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanPortfolios_CreditLineId",
                schema: "dbo",
                table: "LND_LoanPortfolios",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanPortfolios_PersonId",
                schema: "dbo",
                table: "LND_LoanPortfolios",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanPortfolios_PersonId_CreditLineId_PortfolioNumber",
                schema: "dbo",
                table: "LND_LoanPortfolios",
                columns: new[] { "PersonId", "CreditLineId", "PortfolioNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanPortfolios_PublicId",
                schema: "dbo",
                table: "LND_LoanPortfolios",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_LoanRestructurings_CreditLineId",
                schema: "dbo",
                table: "LND_LoanRestructurings",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_LoanRestructurings_PublicId",
                schema: "dbo",
                table: "LND_LoanRestructurings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_OnlineQueries_CreditLineId",
                schema: "dbo",
                table: "LND_OnlineQueries",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_OnlineQueries_PublicId",
                schema: "dbo",
                table: "LND_OnlineQueries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductionConcepts_CreditLineId",
                schema: "dbo",
                table: "LND_PayrollDeductionConcepts",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionConcepts_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionConcepts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionEntries_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionEntries",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductionPeriods_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductionPeriods",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PayrollDeductions_CreditLineId",
                schema: "dbo",
                table: "LND_PayrollDeductions",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PayrollDeductions_PublicId",
                schema: "dbo",
                table: "LND_PayrollDeductions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PendingInstallments_CreditLineId",
                schema: "dbo",
                table: "LND_PendingInstallments",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_PendingInstallments_LoanPortfolioId",
                schema: "dbo",
                table: "LND_PendingInstallments",
                column: "LoanPortfolioId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PendingInstallments_PublicId",
                schema: "dbo",
                table: "LND_PendingInstallments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PeriodicityParameters_PublicId",
                schema: "dbo",
                table: "LND_PeriodicityParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PortfolioAccounts_PublicId",
                schema: "dbo",
                table: "LND_PortfolioAccounts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioBalances_CreditLineId",
                schema: "dbo",
                table: "LND_PortfolioBalances",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PortfolioBalances_PublicId",
                schema: "dbo",
                table: "LND_PortfolioBalances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_PortfolioClassifications_PublicId",
                schema: "dbo",
                table: "LND_PortfolioClassifications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PortfolioInvoices_CreditLineId",
                schema: "dbo",
                table: "LND_PortfolioInvoices",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PortfolioInvoices_PublicId",
                schema: "dbo",
                table: "LND_PortfolioInvoices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_PreviousInstallments_CreditLineId",
                schema: "dbo",
                table: "LND_PreviousInstallments",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_PreviousInstallments_PublicId",
                schema: "dbo",
                table: "LND_PreviousInstallments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ProvisionParameters_PublicId",
                schema: "dbo",
                table: "LND_ProvisionParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_RatesByTerm_CreditLineId",
                schema: "dbo",
                table: "LND_RatesByTerm",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_RatesByTerm_PublicId",
                schema: "dbo",
                table: "LND_RatesByTerm",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_RecreationApplications_CreditLineId",
                schema: "dbo",
                table: "LND_RecreationApplications",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_RecreationApplications_PublicId",
                schema: "dbo",
                table: "LND_RecreationApplications",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_RiskAssessments_CreditLineId",
                schema: "dbo",
                table: "LND_RiskAssessments",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_RiskAssessments_PublicId",
                schema: "dbo",
                table: "LND_RiskAssessments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ScoringParameters_PublicId",
                schema: "dbo",
                table: "LND_ScoringParameters",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_ScoringRanges_PublicId",
                schema: "dbo",
                table: "LND_ScoringRanges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_ShortLongTermPortfolio_CreditLineId",
                schema: "dbo",
                table: "LND_ShortLongTermPortfolio",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_ShortLongTermPortfolio_PublicId",
                schema: "dbo",
                table: "LND_ShortLongTermPortfolio",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_TermRates_CreditLineId",
                schema: "dbo",
                table: "LND_TermRates",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_TermRates_PublicId",
                schema: "dbo",
                table: "LND_TermRates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LND_Transactions_CreditLineId",
                schema: "dbo",
                table: "LND_Transactions",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Transactions_LoanPortfolioId",
                schema: "dbo",
                table: "LND_Transactions",
                column: "LoanPortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_Transactions_PersonLine",
                schema: "dbo",
                table: "LND_Transactions",
                columns: new[] { "PersonCode", "CreditLineId", "PortfolioNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_Transactions_TransactionDate",
                schema: "dbo",
                table: "LND_Transactions",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "UK_LND_Transactions_PublicId",
                schema: "dbo",
                table: "LND_Transactions",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LND_SiplaGroupParameters_LND_CreditLineParameters_CreditLineId",
                schema: "dbo",
                table: "LND_SiplaGroupParameters",
                column: "CreditLineId",
                principalSchema: "dbo",
                principalTable: "LND_CreditLineParameters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PAY_SettlementDeductions_LND_LoanPortfolios_LoanPortfolioId",
                schema: "dbo",
                table: "PAY_SettlementDeductions",
                column: "LoanPortfolioId",
                principalSchema: "dbo",
                principalTable: "LND_LoanPortfolios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
