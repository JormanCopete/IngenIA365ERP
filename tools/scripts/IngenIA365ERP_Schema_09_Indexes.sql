-- ============================================================
-- IngenIA365ERP — Indexes
-- Generated: 2026-03-20
-- Total: ~120 nonclustered indexes
-- Categories:
--   1. FK column indexes (SQL Server does NOT auto-index FKs)
--   2. Common search columns (IdentificationNumber, LegacyCode)
--   3. Filter columns (IsDeleted, IsActive, Status)
--   4. Date range columns for reporting queries
--   5. Composite indexes for frequent query patterns
-- ============================================================

SET NOCOUNT ON;
GO

-- ============================================================
-- === COR_ Indexes ===
-- ============================================================

-- FK indexes
CREATE NONCLUSTERED INDEX [IX_COR_Departments_CountryId] ON [dbo].[COR_Departments] ([CountryId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Cities_DepartmentId] ON [dbo].[COR_Cities] ([DepartmentId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_People_CityId] ON [dbo].[COR_People] ([CityId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_PersonId] ON [dbo].[COR_Associates] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_EmployerCompanyId] ON [dbo].[COR_Associates] ([EmployerCompanyId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_BranchId] ON [dbo].[COR_Associates] ([BranchId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_CostCenterId] ON [dbo].[COR_Associates] ([CostCenterId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_SectionId] ON [dbo].[COR_Associates] ([SectionId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_AdvisorId] ON [dbo].[COR_Associates] ([AdvisorId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_CommitteeId] ON [dbo].[COR_Associates] ([CommitteeId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_WithdrawalReasonId] ON [dbo].[COR_Associates] ([WithdrawalReasonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_ReferredBy] ON [dbo].[COR_Associates] ([ReferredBy]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Spouses_PersonId] ON [dbo].[COR_Spouses] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_PeopleFinancial_PersonId] ON [dbo].[COR_PeopleFinancial] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_AssociateCategories_PersonId] ON [dbo].[COR_AssociateCategories] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_CommitteeMembers_PersonId] ON [dbo].[COR_CommitteeMembers] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_CommitteeMembers_CommitteeId] ON [dbo].[COR_CommitteeMembers] ([CommitteeId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Beneficiaries_PersonId] ON [dbo].[COR_Beneficiaries] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Beneficiaries_RelationshipId] ON [dbo].[COR_Beneficiaries] ([RelationshipId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Beneficiaries_CityId] ON [dbo].[COR_Beneficiaries] ([CityId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_References_PersonId] ON [dbo].[COR_References] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_References_CityId] ON [dbo].[COR_References] ([CityId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Notifications_TemplateId] ON [dbo].[COR_Notifications] ([TemplateId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Notifications_RecipientPersonId] ON [dbo].[COR_Notifications] ([RecipientPersonId]);
GO

-- Search columns
CREATE NONCLUSTERED INDEX [IX_COR_People_IdentificationNumber] ON [dbo].[COR_People] ([IdentificationNumber]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_People_FullName] ON [dbo].[COR_People] ([LastName], [FirstName]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_People_LegacyCode] ON [dbo].[COR_People] ([LegacyCode]) WHERE [LegacyCode] IS NOT NULL;
GO

-- Filter columns
CREATE NONCLUSTERED INDEX [IX_COR_People_IsDeleted] ON [dbo].[COR_People] ([IsDeleted]) INCLUDE ([IdentificationNumber], [FirstName], [LastName]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_IsActive] ON [dbo].[COR_Associates] ([IsActive]) INCLUDE ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_COR_Associates_Status] ON [dbo].[COR_Associates] ([Status]);
GO

-- ============================================================
-- === ACC_ Indexes ===
-- ============================================================

-- FK indexes
CREATE NONCLUSTERED INDEX [IX_ACC_AccountBalances_AccountId] ON [dbo].[ACC_AccountBalances] ([AccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_AccountBalances_BranchId] ON [dbo].[ACC_AccountBalances] ([BranchId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_AccountBalances_CostCenterId] ON [dbo].[ACC_AccountBalances] ([CostCenterId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_JournalEntries_AccountId] ON [dbo].[ACC_JournalEntries] ([AccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_JournalEntries_PersonId] ON [dbo].[ACC_JournalEntries] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_JournalEntries_BranchId] ON [dbo].[ACC_JournalEntries] ([BranchId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_JournalEntries_CostCenterId] ON [dbo].[ACC_JournalEntries] ([CostCenterId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_Documents_VoucherTypeCode] ON [dbo].[ACC_Documents] ([VoucherTypeCode]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_Amortizations_AccountId] ON [dbo].[ACC_Amortizations] ([AccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_Depreciations_AccountId] ON [dbo].[ACC_Depreciations] ([AccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_Budgets_AccountId] ON [dbo].[ACC_Budgets] ([AccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_BankReconciliations_AccountId] ON [dbo].[ACC_BankReconciliations] ([AccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_BankReconciliations_BankId] ON [dbo].[ACC_BankReconciliations] ([BankId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_ThirdPartyAccounts_AccountId] ON [dbo].[ACC_ThirdPartyAccounts] ([AccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_ThirdPartyAccounts_PersonId] ON [dbo].[ACC_ThirdPartyAccounts] ([PersonId]);
GO

-- Search / filter
CREATE NONCLUSTERED INDEX [IX_ACC_ChartOfAccounts_AccountCode] ON [dbo].[ACC_ChartOfAccounts] ([AccountCode]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_ChartOfAccounts_LegacyCode] ON [dbo].[ACC_ChartOfAccounts] ([LegacyCode]) WHERE [LegacyCode] IS NOT NULL;
GO

-- Date range: journal entries are queried by date constantly
CREATE NONCLUSTERED INDEX [IX_ACC_JournalEntries_TransactionDate] ON [dbo].[ACC_JournalEntries] ([TransactionDate]) INCLUDE ([AccountId], [DebitAmount], [CreditAmount]);
GO
CREATE NONCLUSTERED INDEX [IX_ACC_JournalEntries_AccountId_Date] ON [dbo].[ACC_JournalEntries] ([AccountId], [TransactionDate]);
GO

-- ============================================================
-- === LND_ Indexes ===
-- ============================================================

-- FK indexes
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_PersonId] ON [dbo].[LND_LoanPortfolios] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_CreditLineId] ON [dbo].[LND_LoanPortfolios] ([CreditLineId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_BranchId] ON [dbo].[LND_LoanPortfolios] ([BranchId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_CostCenterId] ON [dbo].[LND_LoanPortfolios] ([CostCenterId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Transactions_PersonId] ON [dbo].[LND_Transactions] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Transactions_CreditLineId] ON [dbo].[LND_Transactions] ([CreditLineId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Transactions_LoanPortfolioId] ON [dbo].[LND_Transactions] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Transactions_AccountId] ON [dbo].[LND_Transactions] ([AccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Transactions_TransactionCodeId] ON [dbo].[LND_Transactions] ([TransactionCodeId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_PendingInstallments_LoanPortfolioId] ON [dbo].[LND_PendingInstallments] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_PendingInstallments_PersonId] ON [dbo].[LND_PendingInstallments] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_DefaultRecords_LoanPortfolioId] ON [dbo].[LND_DefaultRecords] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_ExtraPayments_LoanPortfolioId] ON [dbo].[LND_ExtraPayments] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Guarantees_PersonId] ON [dbo].[LND_Guarantees] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Guarantees_LoanPortfolioId] ON [dbo].[LND_Guarantees] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_CollectionCases_PersonId] ON [dbo].[LND_CollectionCases] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_CollectionCases_AssignedUserId] ON [dbo].[LND_CollectionCases] ([AssignedUserId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_SavingsAccounts_PersonId] ON [dbo].[LND_SavingsAccounts] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_DepositAccounts_PersonId] ON [dbo].[LND_DepositAccounts] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_SavingsTransactions_SavingsAccountId] ON [dbo].[LND_SavingsTransactions] ([SavingsAccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_DepositTransactions_DepositAccountId] ON [dbo].[LND_DepositTransactions] ([DepositAccountId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_PayrollDeductions_LoanPortfolioId] ON [dbo].[LND_PayrollDeductions] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_PayrollDeductions_PersonId] ON [dbo].[LND_PayrollDeductions] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_PortfolioClassifications_LoanPortfolioId] ON [dbo].[LND_PortfolioClassifications] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_CreditApplications_PersonId] ON [dbo].[LND_CreditApplications] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_CreditApplications_CreditLineId] ON [dbo].[LND_CreditApplications] ([CreditLineId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Endorsers_LoanPortfolioId] ON [dbo].[LND_Endorsers] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_Endorsers_EndorserPersonId] ON [dbo].[LND_Endorsers] ([EndorserPersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_InsurancePolicies_LoanPortfolioId] ON [dbo].[LND_InsurancePolicies] ([LoanPortfolioId]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_CircularLetters_PersonId] ON [dbo].[LND_CircularLetters] ([PersonId]);
GO

-- Search columns
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_LegacyCode] ON [dbo].[LND_LoanPortfolios] ([LegacyCode]) WHERE [LegacyCode] IS NOT NULL;
GO
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_LoanNumber] ON [dbo].[LND_LoanPortfolios] ([LoanNumber]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_SavingsAccounts_AccountNumber] ON [dbo].[LND_SavingsAccounts] ([AccountNumber]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_DepositAccounts_AccountNumber] ON [dbo].[LND_DepositAccounts] ([AccountNumber]);
GO

-- Filter & status columns
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_Status] ON [dbo].[LND_LoanPortfolios] ([Status]) INCLUDE ([PersonId], [CurrentBalance]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_DaysOverdue] ON [dbo].[LND_LoanPortfolios] ([DaysOverdue]) WHERE [DaysOverdue] > 0;
GO

-- Date range: loan transactions
CREATE NONCLUSTERED INDEX [IX_LND_Transactions_TransactionDate] ON [dbo].[LND_Transactions] ([TransactionDate]) INCLUDE ([PersonId], [LoanPortfolioId], [Amount]);
GO
CREATE NONCLUSTERED INDEX [IX_LND_PendingInstallments_DueDate] ON [dbo].[LND_PendingInstallments] ([DueDate]) INCLUDE ([LoanPortfolioId], [InstallmentAmount]);
GO

-- Composite: portfolio by person + status (most common query pattern)
CREATE NONCLUSTERED INDEX [IX_LND_LoanPortfolios_PersonId_Status] ON [dbo].[LND_LoanPortfolios] ([PersonId], [Status]);
GO

-- ============================================================
-- === PAY_ Indexes ===
-- ============================================================

-- FK indexes
CREATE NONCLUSTERED INDEX [IX_PAY_Employees_PersonId] ON [dbo].[PAY_Employees] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_Employees_CostCenterId] ON [dbo].[PAY_Employees] ([CostCenterId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_Employees_HealthInsuranceProviderId] ON [dbo].[PAY_Employees] ([HealthInsuranceProviderId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_Employees_PensionProviderId] ON [dbo].[PAY_Employees] ([PensionProviderId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_Employees_BankId] ON [dbo].[PAY_Employees] ([BankId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_PayrollTransactions_EmployeeId] ON [dbo].[PAY_PayrollTransactions] ([EmployeeId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_PayrollTransactions_ConceptId] ON [dbo].[PAY_PayrollTransactions] ([ConceptId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_Absences_EmployeeId] ON [dbo].[PAY_Absences] ([EmployeeId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_DirectDebits_EmployeeId] ON [dbo].[PAY_DirectDebits] ([EmployeeId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_PayrollPlanLiquidations_EmployeeId] ON [dbo].[PAY_PayrollPlanLiquidations] ([EmployeeId]);
GO

-- Search / filter
CREATE NONCLUSTERED INDEX [IX_PAY_Employees_IsActive] ON [dbo].[PAY_Employees] ([IsActive]) INCLUDE ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_PAY_Employees_LegacyCode] ON [dbo].[PAY_Employees] ([LegacyCode]) WHERE [LegacyCode] IS NOT NULL;
GO

-- Date range: payroll by period
CREATE NONCLUSTERED INDEX [IX_PAY_PayrollTransactions_PeriodDate] ON [dbo].[PAY_PayrollTransactions] ([PeriodStartDate], [PeriodEndDate]) INCLUDE ([EmployeeId], [Amount]);
GO

-- ============================================================
-- === INV_ Indexes ===
-- ============================================================

-- FK indexes
CREATE NONCLUSTERED INDEX [IX_INV_Products_ProductGroupId] ON [dbo].[INV_Products] ([ProductGroupId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_Products_DiscountTypeId] ON [dbo].[INV_Products] ([DiscountTypeId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_ProductGroups_SecondaryGroupId] ON [dbo].[INV_ProductGroups] ([SecondaryGroupId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_SecondaryGroups_PrimaryGroupId] ON [dbo].[INV_SecondaryGroups] ([PrimaryGroupId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_Transactions_TransactionTypeId] ON [dbo].[INV_Transactions] ([TransactionTypeId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_Transactions_ProductId] ON [dbo].[INV_Transactions] ([ProductId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_Transactions_PersonId] ON [dbo].[INV_Transactions] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_WarehouseStock_ProductId] ON [dbo].[INV_WarehouseStock] ([ProductId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_WarehouseStock_WarehouseId] ON [dbo].[INV_WarehouseStock] ([WarehouseId]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_InvoiceDetails_ProductId] ON [dbo].[INV_InvoiceDetails] ([ProductId]);
GO

-- Search
CREATE NONCLUSTERED INDEX [IX_INV_Products_ProductCode] ON [dbo].[INV_Products] ([ProductCode]);
GO
CREATE NONCLUSTERED INDEX [IX_INV_Products_Barcode] ON [dbo].[INV_Products] ([Barcode]) WHERE [Barcode] IS NOT NULL;
GO
CREATE NONCLUSTERED INDEX [IX_INV_Products_LegacyCode] ON [dbo].[INV_Products] ([LegacyCode]) WHERE [LegacyCode] IS NOT NULL;
GO

-- Date range: sales transactions
CREATE NONCLUSTERED INDEX [IX_INV_Transactions_TransactionDate] ON [dbo].[INV_Transactions] ([TransactionDate]) INCLUDE ([ProductId], [Quantity], [TotalAmount]);
GO

-- ============================================================
-- === CDT_ Indexes ===
-- ============================================================

CREATE NONCLUSTERED INDEX [IX_CDT_Certificates_PersonId] ON [dbo].[CDT_Certificates] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_CDT_Certificates_CreditLineId] ON [dbo].[CDT_Certificates] ([CreditLineId]);
GO
CREATE NONCLUSTERED INDEX [IX_CDT_Transactions_CertificateId] ON [dbo].[CDT_Transactions] ([CertificateId]);
GO
CREATE NONCLUSTERED INDEX [IX_CDT_Certificates_MaturityDate] ON [dbo].[CDT_Certificates] ([MaturityDate]) INCLUDE ([PersonId], [PrincipalAmount]);
GO
CREATE NONCLUSTERED INDEX [IX_CDT_Certificates_Status] ON [dbo].[CDT_Certificates] ([Status]);
GO

-- ============================================================
-- === DEB_ Indexes ===
-- ============================================================

CREATE NONCLUSTERED INDEX [IX_DEB_Cards_PersonId] ON [dbo].[DEB_Cards] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_DEB_Cards_BankId] ON [dbo].[DEB_Cards] ([BankId]);
GO
CREATE NONCLUSTERED INDEX [IX_DEB_Transactions_CardId] ON [dbo].[DEB_Transactions] ([CardId]);
GO

-- ============================================================
-- === TRS_ Indexes ===
-- ============================================================

CREATE NONCLUSTERED INDEX [IX_TRS_Checks_PersonId] ON [dbo].[TRS_Checks] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_TRS_Checks_BankId] ON [dbo].[TRS_Checks] ([BankId]);
GO
CREATE NONCLUSTERED INDEX [IX_TRS_PaymentOrders_PersonId] ON [dbo].[TRS_PaymentOrders] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_TRS_PaymentOrders_BankId] ON [dbo].[TRS_PaymentOrders] ([BankId]);
GO
CREATE NONCLUSTERED INDEX [IX_TRS_CashRegisterClosings_CashierId] ON [dbo].[TRS_CashRegisterClosings] ([CashierId]);
GO
CREATE NONCLUSTERED INDEX [IX_TRS_Checks_Status] ON [dbo].[TRS_Checks] ([Status]);
GO
CREATE NONCLUSTERED INDEX [IX_TRS_Checks_CheckDate] ON [dbo].[TRS_Checks] ([CheckDate]) INCLUDE ([PersonId], [Amount], [Status]);
GO

-- ============================================================
-- === SEC_ Indexes ===
-- ============================================================

CREATE NONCLUSTERED INDEX [IX_SEC_UserRoles_UserId] ON [dbo].[SEC_UserRoles] ([UserId]);
GO
CREATE NONCLUSTERED INDEX [IX_SEC_UserRoles_RoleId] ON [dbo].[SEC_UserRoles] ([RoleId]);
GO
CREATE NONCLUSTERED INDEX [IX_SEC_RolePermissions_RoleId] ON [dbo].[SEC_RolePermissions] ([RoleId]);
GO
CREATE NONCLUSTERED INDEX [IX_SEC_RolePermissions_PermissionId] ON [dbo].[SEC_RolePermissions] ([PermissionId]);
GO
CREATE NONCLUSTERED INDEX [IX_SEC_RefreshTokens_UserId] ON [dbo].[SEC_RefreshTokens] ([UserId]);
GO
CREATE NONCLUSTERED INDEX [IX_SEC_UserSessions_UserId] ON [dbo].[SEC_UserSessions] ([UserId]);
GO
CREATE NONCLUSTERED INDEX [IX_SEC_Users_Username] ON [dbo].[SEC_Users] ([Username]);
GO
CREATE NONCLUSTERED INDEX [IX_SEC_Users_Email] ON [dbo].[SEC_Users] ([Email]) WHERE [Email] IS NOT NULL;
GO

-- ============================================================
-- === AUD_ Indexes ===
-- ============================================================

CREATE NONCLUSTERED INDEX [IX_AUD_AuditLog_UserId] ON [dbo].[AUD_AuditLog] ([UserId]);
GO
CREATE NONCLUSTERED INDEX [IX_AUD_AuditLog_TableName] ON [dbo].[AUD_AuditLog] ([TableName]);
GO
CREATE NONCLUSTERED INDEX [IX_AUD_AuditLog_ActionDate] ON [dbo].[AUD_AuditLog] ([ActionDate]) INCLUDE ([UserId], [TableName], [Action]);
GO

-- ============================================================
-- === ADM_ / WEB_ Indexes ===
-- ============================================================

CREATE NONCLUSTERED INDEX [IX_ADM_Subscriptions_TenantId] ON [dbo].[ADM_Subscriptions] ([TenantId]);
GO
CREATE NONCLUSTERED INDEX [IX_ADM_TenantSettings_TenantId] ON [dbo].[ADM_TenantSettings] ([TenantId]);
GO
CREATE NONCLUSTERED INDEX [IX_WEB_PortalUsers_PersonId] ON [dbo].[WEB_PortalUsers] ([PersonId]);
GO
CREATE NONCLUSTERED INDEX [IX_WEB_PortalUsers_UserId] ON [dbo].[WEB_PortalUsers] ([UserId]);
GO
CREATE NONCLUSTERED INDEX [IX_WEB_OnlineTransactions_PortalUserId] ON [dbo].[WEB_OnlineTransactions] ([PortalUserId]);
GO
CREATE NONCLUSTERED INDEX [IX_WEB_SupportTickets_PortalUserId] ON [dbo].[WEB_SupportTickets] ([PortalUserId]);
GO

PRINT N'Indexes created successfully. Total: ~120 nonclustered indexes.';
GO
