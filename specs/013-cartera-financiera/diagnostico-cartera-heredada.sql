-- =====================================================================================
-- Diagnóstico de la cartera heredada antes de RetiroDeLaCarteraHeredada (feature 013, T0182; data-model parte 3 §13.4)
-- SÓLO LECTURA: no escribe ni bloquea nada. Se corre en cada base de cooperativa (una por cooperativa, Principio IV) y en
-- la base por defecto de la aplicación (ingenia365erp); la administrativa no tiene tablas LND_. Las bases salen de:
--   ingenia365erp_admin:  SELECT "Name", "DatabaseName" FROM dbo."ADM_Tenants" WHERE NOT "IsDeleted";
--
-- Qué contestar por base (runbook docs/operaciones/cartera-retiro-heredado.md, FR-144, Principio XII):
--   1. Filas de las 55 tablas de crédito que se retiran (−1 si la tabla no existe en esa base) y su total, contando
--      también las dadas de baja. Con filas, la guarda de la migración se niega salvo la aprobación del dueño para esa base
--      (fila COR_SystemSettings 'LND.RetiroHeredado.Aprobado'), su respaldo y el nombre del segundo revisor.
--   2. Filas de las 38 conservadas (informativo: siguen con sus filas, su código se mudó a Savings).
--   3. Datos de ahorro o aportes metidos en tablas de crédito: LND_Transactions con SavingsLineId o CdatAccrualDate no
--      nulos; LND_PayrollDeductions y LND_PayrollDeductionPeriods con ContributionAmount > 0. Si hay, el retiro se detiene
--      y decide el dueño (pregunta H3).
--   4. LND_SiplaGroupParameters: se conserva, pero su línea de crédito deja de tener llave; con filas decide el dueño (H1).
--   5. Nómina: PAY_SettlementDeductions con LoanPortfolioId no nulo (por estado) y PAY_Novelties con LoanPortfolioId no
--      nulo; y las que la copia de NominaRecaudaPorElContrato (C73) no encontraría —LoanPortfolioId sin crédito heredado
--      que lo respalde—: esas pierden la referencia a la obligación (pérdida declarada). Vale antes y después de aplicar
--      NominaRecaudaPorElContrato (no mira ObligationPublicId, que esa migración agrega).
--   6. ACC_Documents con OriginModule = 'CAR' por tipo de origen y comprobante (DS, RC, CA, PV, DN): no se tocan (son
--      inmutables), pero su existencia exige la decisión del dueño (H4: propuesta por defecto, aprobar con constancia).
--   7. Si ya existe la aprobación.
--
-- PostgreSQL (clúster):
--   k3s kubectl -n <ns> exec -i erp-db-1 -c postgres -- psql -U postgres -d <base> -f - < diagnostico-cartera-heredada.sql
--   (o pegar el bloque POSTGRESQL en psql). Los resultados salen como NOTICE «tabla | filas».
-- SQL Server: correr el bloque SQL SERVER en la base de la cooperativa (quitar el comentario del bloque).
-- =====================================================================================

-- ======================== POSTGRESQL ========================
DO $$
DECLARE
    t text;
    n bigint;
    total bigint := 0;
    retiradas text[] := ARRAY[
        'LND_AccrualEntries','LND_AccrualPeriods','LND_ApplicationAssets','LND_ApplicationCodebtors',
        'LND_ApplicationExtras','LND_ApplicationReferences','LND_ApplicationStatuses','LND_CollectionCases',
        'LND_CollectionDateEntries','LND_CollectionMasters','LND_CollectionNoticeDetails','LND_CollectionNoticeParams',
        'LND_CollectionNotices','LND_CollectionPeriods','LND_CreditLineAudit','LND_CreditLineParameters',
        'LND_CreditParameters','LND_DeductionValues','LND_DefaultLiquidations','LND_DefaultRecords',
        'LND_ExtraPaymentBalances','LND_ExtraPayments','LND_Guarantees','LND_HousingApplicationParams',
        'LND_HousingParameters','LND_InsuranceBeneficiaries','LND_InsurancePolicies','LND_InterestRates',
        'LND_InvoiceDetails','LND_InvoiceLineItems','LND_InvoiceMasters','LND_LoanApplications',
        'LND_LoanPortfolios','LND_LoanRestructurings','LND_OnlineQueries','LND_PayrollDeductionConcepts',
        'LND_PayrollDeductionEntries','LND_PayrollDeductionPeriods','LND_PayrollDeductions','LND_PendingInstallments',
        'LND_PeriodicityParameters','LND_PortfolioAccounts','LND_PortfolioBalances','LND_PortfolioClassifications',
        'LND_PortfolioInvoices','LND_PreviousInstallments','LND_ProvisionParameters','LND_RatesByTerm',
        'LND_RecreationApplications','LND_RiskAssessments','LND_ScoringParameters','LND_ScoringRanges',
        'LND_ShortLongTermPortfolio','LND_TermRates','LND_Transactions'];
    conservadas text[] := ARRAY[
        'LND_SavingsAccounts','LND_SavingsParameters','LND_DepositAccounts','LND_DepositEntries',
        'LND_DepositAudit','LND_DepositSeals','LND_DepositSignatures','LND_Cashiers',
        'LND_CashBases','LND_Checkbooks','LND_CheckClearing','LND_ContributionReductions',
        'LND_ContributionReductionParams','LND_AssociateWithdrawals','LND_WithdrawalStatuses','LND_SiplaParameters',
        'LND_SiplaGroupParameters','LND_UnusualTransactions','LND_UnusualTransactionEntries','LND_MoneyLaunderingDeclarations',
        'LND_Minutes','LND_MinuteAttendees','LND_Blacklist','LND_TransactionCodes',
        'LND_Documents','LND_ActivityEnrollments','LND_AssociateActivities','LND_AssociateDiseases',
        'LND_BiometricRecords','LND_PersonAssets','LND_Subsidies','LND_AuxiliaryApplications',
        'LND_AuxiliaryApplicationLines','LND_AuxAppInstallments','LND_AuxAppInstallmentBeneficiaries','LND_Zones',
        'LND_SubZones','LND_ZoneTypes'];
    r record;
BEGIN
    RAISE NOTICE '--- 1. Las 55 tablas de crédito que se retiran (filas, incluidas las de baja) ---';
    FOREACH t IN ARRAY retiradas LOOP
        IF to_regclass(format('dbo.%I', t)) IS NULL THEN
            RAISE NOTICE '% | -1 (no existe)', t;
        ELSE
            EXECUTE format('SELECT count(*) FROM dbo.%I', t) INTO n;
            total := total + n;
            RAISE NOTICE '% | %', t, n;
        END IF;
    END LOOP;
    RAISE NOTICE 'TOTAL filas en las 55 retiradas | %', total;

    RAISE NOTICE '--- 2. Las 38 conservadas (informativo) ---';
    FOREACH t IN ARRAY conservadas LOOP
        IF to_regclass(format('dbo.%I', t)) IS NULL THEN
            RAISE NOTICE '% | -1 (no existe)', t;
        ELSE
            EXECUTE format('SELECT count(*) FROM dbo.%I', t) INTO n;
            RAISE NOTICE '% | %', t, n;
        END IF;
    END LOOP;

    RAISE NOTICE '--- 3. Ahorro o aportes dentro de tablas de crédito ---';
    IF to_regclass('dbo."LND_Transactions"') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM dbo."LND_Transactions" WHERE "SavingsLineId" IS NOT NULL OR "CdatAccrualDate" IS NOT NULL' INTO n;
        RAISE NOTICE 'LND_Transactions con SavingsLineId o CdatAccrualDate | %', n;
    END IF;
    IF to_regclass('dbo."LND_PayrollDeductions"') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM dbo."LND_PayrollDeductions" WHERE "ContributionAmount" > 0' INTO n;
        RAISE NOTICE 'LND_PayrollDeductions con ContributionAmount > 0 | %', n;
    END IF;
    IF to_regclass('dbo."LND_PayrollDeductionPeriods"') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM dbo."LND_PayrollDeductionPeriods" WHERE "ContributionAmount" > 0' INTO n;
        RAISE NOTICE 'LND_PayrollDeductionPeriods con ContributionAmount > 0 | %', n;
    END IF;

    RAISE NOTICE '--- 4. SIPLA por grupo (se conserva sin llave a la línea) ---';
    IF to_regclass('dbo."LND_SiplaGroupParameters"') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM dbo."LND_SiplaGroupParameters"' INTO n;
        RAISE NOTICE 'LND_SiplaGroupParameters | %', n;
    END IF;

    RAISE NOTICE '--- 5. Nómina ---';
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'dbo'
               AND table_name = 'PAY_SettlementDeductions' AND column_name = 'LoanPortfolioId') THEN
        FOR r IN EXECUTE 'SELECT "Status" AS estado, count(*) AS filas FROM dbo."PAY_SettlementDeductions"
                          WHERE "LoanPortfolioId" IS NOT NULL GROUP BY "Status" ORDER BY "Status"' LOOP
            RAISE NOTICE 'PAY_SettlementDeductions con LoanPortfolioId, estado % | %', r.estado, r.filas;
        END LOOP;
        IF to_regclass('dbo."LND_LoanPortfolios"') IS NOT NULL THEN
            EXECUTE 'SELECT count(*) FROM dbo."PAY_SettlementDeductions" d
                     WHERE d."LoanPortfolioId" IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM dbo."LND_LoanPortfolios" l WHERE l."Id" = d."LoanPortfolioId")' INTO n;
            RAISE NOTICE 'PAY_SettlementDeductions que la copia de C73 no encontraría | %', n;
        END IF;
    ELSE
        RAISE NOTICE 'PAY_SettlementDeductions.LoanPortfolioId | -1 (la columna ya no existe: el retiro está aplicado)';
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'dbo'
               AND table_name = 'PAY_Novelties' AND column_name = 'LoanPortfolioId') THEN
        EXECUTE 'SELECT count(*) FROM dbo."PAY_Novelties" WHERE "LoanPortfolioId" IS NOT NULL' INTO n;
        RAISE NOTICE 'PAY_Novelties con LoanPortfolioId | %', n;
        IF to_regclass('dbo."LND_LoanPortfolios"') IS NOT NULL THEN
            EXECUTE 'SELECT count(*) FROM dbo."PAY_Novelties" v
                     WHERE v."LoanPortfolioId" IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM dbo."LND_LoanPortfolios" l WHERE l."Id" = v."LoanPortfolioId")' INTO n;
            RAISE NOTICE 'PAY_Novelties que la copia de C73 no encontraría | %', n;
        END IF;
    ELSE
        RAISE NOTICE 'PAY_Novelties.LoanPortfolioId | -1 (la columna ya no existe: el retiro está aplicado)';
    END IF;

    RAISE NOTICE '--- 6. Comprobantes con origen CAR (no se tocan) ---';
    IF to_regclass('dbo."ACC_Documents"') IS NOT NULL THEN
        FOR r IN EXECUTE 'SELECT coalesce(d."SourceType", ''(sin tipo)'') AS origen, coalesce(v."Code", ''?'') AS comprobante, count(*) AS filas
                          FROM dbo."ACC_Documents" d LEFT JOIN dbo."ACC_VoucherTypes" v ON v."Id" = d."VoucherTypeId"
                          WHERE d."OriginModule" = ''CAR''
                          GROUP BY 1, 2 ORDER BY 1, 2' LOOP
            RAISE NOTICE 'ACC_Documents CAR, origen %, comprobante % | %', r.origen, r.comprobante, r.filas;
        END LOOP;
        EXECUTE 'SELECT count(*) FROM dbo."ACC_Documents" WHERE "OriginModule" = ''CAR''' INTO n;
        RAISE NOTICE 'TOTAL ACC_Documents con OriginModule CAR | %', n;
    END IF;

    RAISE NOTICE '--- 7. Aprobación ---';
    IF to_regclass('dbo."COR_SystemSettings"') IS NOT NULL THEN
        EXECUTE 'SELECT count(*) FROM dbo."COR_SystemSettings" WHERE "SettingKey" = ''LND.RetiroHeredado.Aprobado'' AND NOT "IsDeleted"' INTO n;
        RAISE NOTICE 'Aprobacion LND.RetiroHeredado.Aprobado viva | %', n;
    END IF;
END $$;

-- ======================== SQL SERVER ========================
-- (quitar el comentario del bloque para usarlo)
/*
SET NOCOUNT ON;
DECLARE @t sysname, @n bigint, @total bigint = 0, @sql nvarchar(max);

PRINT N'--- 1. Las 55 tablas de crédito que se retiran (filas, incluidas las de baja) ---';
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT v FROM (VALUES
 (N'LND_AccrualEntries'),(N'LND_AccrualPeriods'),(N'LND_ApplicationAssets'),
 (N'LND_ApplicationCodebtors'),(N'LND_ApplicationExtras'),(N'LND_ApplicationReferences'),
 (N'LND_ApplicationStatuses'),(N'LND_CollectionCases'),(N'LND_CollectionDateEntries'),
 (N'LND_CollectionMasters'),(N'LND_CollectionNoticeDetails'),(N'LND_CollectionNoticeParams'),
 (N'LND_CollectionNotices'),(N'LND_CollectionPeriods'),(N'LND_CreditLineAudit'),
 (N'LND_CreditLineParameters'),(N'LND_CreditParameters'),(N'LND_DeductionValues'),
 (N'LND_DefaultLiquidations'),(N'LND_DefaultRecords'),(N'LND_ExtraPaymentBalances'),
 (N'LND_ExtraPayments'),(N'LND_Guarantees'),(N'LND_HousingApplicationParams'),
 (N'LND_HousingParameters'),(N'LND_InsuranceBeneficiaries'),(N'LND_InsurancePolicies'),
 (N'LND_InterestRates'),(N'LND_InvoiceDetails'),(N'LND_InvoiceLineItems'),
 (N'LND_InvoiceMasters'),(N'LND_LoanApplications'),(N'LND_LoanPortfolios'),
 (N'LND_LoanRestructurings'),(N'LND_OnlineQueries'),(N'LND_PayrollDeductionConcepts'),
 (N'LND_PayrollDeductionEntries'),(N'LND_PayrollDeductionPeriods'),(N'LND_PayrollDeductions'),
 (N'LND_PendingInstallments'),(N'LND_PeriodicityParameters'),(N'LND_PortfolioAccounts'),
 (N'LND_PortfolioBalances'),(N'LND_PortfolioClassifications'),(N'LND_PortfolioInvoices'),
 (N'LND_PreviousInstallments'),(N'LND_ProvisionParameters'),(N'LND_RatesByTerm'),
 (N'LND_RecreationApplications'),(N'LND_RiskAssessments'),(N'LND_ScoringParameters'),
 (N'LND_ScoringRanges'),(N'LND_ShortLongTermPortfolio'),(N'LND_TermRates'),
 (N'LND_Transactions')) x(v);
OPEN c; FETCH NEXT FROM c INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@t)) IS NULL
        PRINT @t + N' | -1 (no existe)';
    ELSE
    BEGIN
        SET @sql = N'SELECT @n = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@t);
        EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @n OUTPUT;
        SET @total += @n;
        PRINT @t + N' | ' + CAST(@n AS nvarchar(20));
    END
    FETCH NEXT FROM c INTO @t;
END
CLOSE c; DEALLOCATE c;
PRINT N'TOTAL filas en las 55 retiradas | ' + CAST(@total AS nvarchar(20));

PRINT N'--- 2. Las 38 conservadas (informativo) ---';
DECLARE k CURSOR LOCAL FAST_FORWARD FOR SELECT v FROM (VALUES
 (N'LND_SavingsAccounts'),(N'LND_SavingsParameters'),(N'LND_DepositAccounts'),
 (N'LND_DepositEntries'),(N'LND_DepositAudit'),(N'LND_DepositSeals'),
 (N'LND_DepositSignatures'),(N'LND_Cashiers'),(N'LND_CashBases'),
 (N'LND_Checkbooks'),(N'LND_CheckClearing'),(N'LND_ContributionReductions'),
 (N'LND_ContributionReductionParams'),(N'LND_AssociateWithdrawals'),(N'LND_WithdrawalStatuses'),
 (N'LND_SiplaParameters'),(N'LND_SiplaGroupParameters'),(N'LND_UnusualTransactions'),
 (N'LND_UnusualTransactionEntries'),(N'LND_MoneyLaunderingDeclarations'),(N'LND_Minutes'),
 (N'LND_MinuteAttendees'),(N'LND_Blacklist'),(N'LND_TransactionCodes'),
 (N'LND_Documents'),(N'LND_ActivityEnrollments'),(N'LND_AssociateActivities'),
 (N'LND_AssociateDiseases'),(N'LND_BiometricRecords'),(N'LND_PersonAssets'),
 (N'LND_Subsidies'),(N'LND_AuxiliaryApplications'),(N'LND_AuxiliaryApplicationLines'),
 (N'LND_AuxAppInstallments'),(N'LND_AuxAppInstallmentBeneficiaries'),(N'LND_Zones'),
 (N'LND_SubZones'),(N'LND_ZoneTypes')) x(v);
OPEN k; FETCH NEXT FROM k INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@t)) IS NULL
        PRINT @t + N' | -1 (no existe)';
    ELSE
    BEGIN
        SET @sql = N'SELECT @n = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@t);
        EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @n OUTPUT;
        PRINT @t + N' | ' + CAST(@n AS nvarchar(20));
    END
    FETCH NEXT FROM k INTO @t;
END
CLOSE k; DEALLOCATE k;

PRINT N'--- 3. Ahorro o aportes dentro de tablas de crédito ---';
IF OBJECT_ID(N'dbo.LND_Transactions') IS NOT NULL
    EXEC (N'SELECT COUNT_BIG(*) AS [LND_Transactions con SavingsLineId o CdatAccrualDate] FROM dbo.LND_Transactions WHERE SavingsLineId IS NOT NULL OR CdatAccrualDate IS NOT NULL');
IF OBJECT_ID(N'dbo.LND_PayrollDeductions') IS NOT NULL
    EXEC (N'SELECT COUNT_BIG(*) AS [LND_PayrollDeductions con ContributionAmount > 0] FROM dbo.LND_PayrollDeductions WHERE ContributionAmount > 0');
IF OBJECT_ID(N'dbo.LND_PayrollDeductionPeriods') IS NOT NULL
    EXEC (N'SELECT COUNT_BIG(*) AS [LND_PayrollDeductionPeriods con ContributionAmount > 0] FROM dbo.LND_PayrollDeductionPeriods WHERE ContributionAmount > 0');

PRINT N'--- 4. SIPLA por grupo (se conserva sin llave a la línea) ---';
IF OBJECT_ID(N'dbo.LND_SiplaGroupParameters') IS NOT NULL
    SELECT COUNT_BIG(*) AS [LND_SiplaGroupParameters] FROM dbo.LND_SiplaGroupParameters;

PRINT N'--- 5. Nómina ---';
IF COL_LENGTH(N'dbo.PAY_SettlementDeductions', N'LoanPortfolioId') IS NOT NULL
BEGIN
    EXEC (N'SELECT [Status] AS Estado, COUNT_BIG(*) AS [PAY_SettlementDeductions con LoanPortfolioId] FROM dbo.PAY_SettlementDeductions WHERE LoanPortfolioId IS NOT NULL GROUP BY [Status] ORDER BY [Status]');
    IF OBJECT_ID(N'dbo.LND_LoanPortfolios') IS NOT NULL
        EXEC (N'SELECT COUNT_BIG(*) AS [PAY_SettlementDeductions que la copia de C73 no encontraria] FROM dbo.PAY_SettlementDeductions d WHERE d.LoanPortfolioId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LND_LoanPortfolios l WHERE l.Id = d.LoanPortfolioId)');
END
ELSE PRINT N'PAY_SettlementDeductions.LoanPortfolioId | -1 (la columna ya no existe: el retiro está aplicado)';
IF COL_LENGTH(N'dbo.PAY_Novelties', N'LoanPortfolioId') IS NOT NULL
BEGIN
    EXEC (N'SELECT COUNT_BIG(*) AS [PAY_Novelties con LoanPortfolioId] FROM dbo.PAY_Novelties WHERE LoanPortfolioId IS NOT NULL');
    IF OBJECT_ID(N'dbo.LND_LoanPortfolios') IS NOT NULL
        EXEC (N'SELECT COUNT_BIG(*) AS [PAY_Novelties que la copia de C73 no encontraria] FROM dbo.PAY_Novelties v WHERE v.LoanPortfolioId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.LND_LoanPortfolios l WHERE l.Id = v.LoanPortfolioId)');
END
ELSE PRINT N'PAY_Novelties.LoanPortfolioId | -1 (la columna ya no existe: el retiro está aplicado)';

PRINT N'--- 6. Comprobantes con origen CAR (no se tocan) ---';
IF OBJECT_ID(N'dbo.ACC_Documents') IS NOT NULL
BEGIN
    SELECT COALESCE(d.SourceType, N'(sin tipo)') AS Origen, COALESCE(v.Code, N'?') AS Comprobante, COUNT_BIG(*) AS Filas
    FROM dbo.ACC_Documents d LEFT JOIN dbo.ACC_VoucherTypes v ON v.Id = d.VoucherTypeId
    WHERE d.OriginModule = N'CAR'
    GROUP BY COALESCE(d.SourceType, N'(sin tipo)'), COALESCE(v.Code, N'?')
    ORDER BY 1, 2;
    SELECT COUNT_BIG(*) AS [TOTAL ACC_Documents con OriginModule CAR] FROM dbo.ACC_Documents WHERE OriginModule = N'CAR';
END

PRINT N'--- 7. Aprobación ---';
IF OBJECT_ID(N'dbo.COR_SystemSettings') IS NOT NULL
    SELECT COUNT_BIG(*) AS [Aprobacion LND.RetiroHeredado.Aprobado viva] FROM dbo.COR_SystemSettings
    WHERE SettingKey = N'LND.RetiroHeredado.Aprobado' AND IsDeleted = 0;
*/
