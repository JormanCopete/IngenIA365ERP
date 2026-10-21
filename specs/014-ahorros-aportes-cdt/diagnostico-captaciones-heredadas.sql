-- ============================================================================
-- Diagnóstico previo al retiro de las captaciones heredadas (feature 014, R28)
-- Contexto : antes de aplicar la migración destructiva RetiroDeCaptacionesHeredadas
--            hay que saber, base por base, si alguna tabla heredada de ahorros,
--            aportes, CDT, caja, SIPLA o bienestar tiene filas.
-- Modo     : SÓLO LECTURA. Ejecutar en la base de cada cooperativa, en local, DEV,
--            QA y producción, y guardar el resultado con el respaldo.
-- Motor    : PostgreSQL. En SQL Server, reemplazar las comillas dobles por
--            corchetes y COUNT(*) funciona igual.
-- Una tabla que no exista en una base se informa como error: quitarla de la lista
-- para esa base y anotarlo.
-- ============================================================================

SELECT 'LND_ActivityEnrollments' AS tabla, COUNT(*) AS filas FROM "LND_ActivityEnrollments"
UNION ALL SELECT 'LND_AssociateActivities', COUNT(*) FROM "LND_AssociateActivities"
UNION ALL SELECT 'LND_AssociateDiseases', COUNT(*) FROM "LND_AssociateDiseases"
UNION ALL SELECT 'LND_AssociateWithdrawals', COUNT(*) FROM "LND_AssociateWithdrawals"
UNION ALL SELECT 'LND_AuxAppInstallmentBeneficiaries', COUNT(*) FROM "LND_AuxAppInstallmentBeneficiaries"
UNION ALL SELECT 'LND_AuxAppInstallments', COUNT(*) FROM "LND_AuxAppInstallments"
UNION ALL SELECT 'LND_AuxiliaryApplicationLines', COUNT(*) FROM "LND_AuxiliaryApplicationLines"
UNION ALL SELECT 'LND_AuxiliaryApplications', COUNT(*) FROM "LND_AuxiliaryApplications"
UNION ALL SELECT 'LND_BiometricRecords', COUNT(*) FROM "LND_BiometricRecords"
UNION ALL SELECT 'LND_Blacklist', COUNT(*) FROM "LND_Blacklist"
UNION ALL SELECT 'LND_CashBases', COUNT(*) FROM "LND_CashBases"
UNION ALL SELECT 'LND_Cashiers', COUNT(*) FROM "LND_Cashiers"
UNION ALL SELECT 'LND_CheckClearing', COUNT(*) FROM "LND_CheckClearing"
UNION ALL SELECT 'LND_Checkbooks', COUNT(*) FROM "LND_Checkbooks"
UNION ALL SELECT 'LND_ContributionReductionParams', COUNT(*) FROM "LND_ContributionReductionParams"
UNION ALL SELECT 'LND_ContributionReductions', COUNT(*) FROM "LND_ContributionReductions"
UNION ALL SELECT 'LND_DepositAccounts', COUNT(*) FROM "LND_DepositAccounts"
UNION ALL SELECT 'LND_DepositAudit', COUNT(*) FROM "LND_DepositAudit"
UNION ALL SELECT 'LND_DepositEntries', COUNT(*) FROM "LND_DepositEntries"
UNION ALL SELECT 'LND_DepositSeals', COUNT(*) FROM "LND_DepositSeals"
UNION ALL SELECT 'LND_DepositSignatures', COUNT(*) FROM "LND_DepositSignatures"
UNION ALL SELECT 'LND_Documents', COUNT(*) FROM "LND_Documents"
UNION ALL SELECT 'LND_MinuteAttendees', COUNT(*) FROM "LND_MinuteAttendees"
UNION ALL SELECT 'LND_Minutes', COUNT(*) FROM "LND_Minutes"
UNION ALL SELECT 'LND_MoneyLaunderingDeclarations', COUNT(*) FROM "LND_MoneyLaunderingDeclarations"
UNION ALL SELECT 'LND_PersonAssets', COUNT(*) FROM "LND_PersonAssets"
UNION ALL SELECT 'LND_SavingsAccounts', COUNT(*) FROM "LND_SavingsAccounts"
UNION ALL SELECT 'LND_SavingsParameters', COUNT(*) FROM "LND_SavingsParameters"
UNION ALL SELECT 'LND_SiplaGroupParameters', COUNT(*) FROM "LND_SiplaGroupParameters"
UNION ALL SELECT 'LND_SiplaParameters', COUNT(*) FROM "LND_SiplaParameters"
UNION ALL SELECT 'LND_SubZones', COUNT(*) FROM "LND_SubZones"
UNION ALL SELECT 'LND_Subsidies', COUNT(*) FROM "LND_Subsidies"
UNION ALL SELECT 'LND_TransactionCodes', COUNT(*) FROM "LND_TransactionCodes"
UNION ALL SELECT 'LND_UnusualTransactionEntries', COUNT(*) FROM "LND_UnusualTransactionEntries"
UNION ALL SELECT 'LND_UnusualTransactions', COUNT(*) FROM "LND_UnusualTransactions"
UNION ALL SELECT 'LND_WithdrawalStatuses', COUNT(*) FROM "LND_WithdrawalStatuses"
UNION ALL SELECT 'LND_ZoneTypes', COUNT(*) FROM "LND_ZoneTypes"
UNION ALL SELECT 'LND_Zones', COUNT(*) FROM "LND_Zones"
UNION ALL SELECT 'CDT_AssociateReferences', COUNT(*) FROM "CDT_AssociateReferences"
UNION ALL SELECT 'CDT_Audit', COUNT(*) FROM "CDT_Audit"
UNION ALL SELECT 'CDT_CertificateEntries', COUNT(*) FROM "CDT_CertificateEntries"
UNION ALL SELECT 'CDT_Certificates', COUNT(*) FROM "CDT_Certificates"
UNION ALL SELECT 'CDT_ParameterAudit', COUNT(*) FROM "CDT_ParameterAudit"
UNION ALL SELECT 'CDT_Parameters', COUNT(*) FROM "CDT_Parameters"
UNION ALL SELECT 'CDT_RatesByTerm', COUNT(*) FROM "CDT_RatesByTerm"
ORDER BY filas DESC, tabla;
