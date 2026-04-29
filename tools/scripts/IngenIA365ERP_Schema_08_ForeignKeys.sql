-- ============================================================
-- IngenIA365ERP — Foreign Keys
-- Generated: 2026-03-20
-- Total: ~155 FK constraints grouped by module
-- ============================================================

SET NOCOUNT ON;
GO

-- ============================================================
-- === COR_ Foreign Keys (Core / Associates) ===
-- ============================================================

-- Geographic hierarchy
ALTER TABLE [dbo].[COR_Departments]
    ADD CONSTRAINT [FK_COR_Departments_COR_Countries_CountryId]
    FOREIGN KEY ([CountryId]) REFERENCES [dbo].[COR_Countries]([Id]);
GO

ALTER TABLE [dbo].[COR_Cities]
    ADD CONSTRAINT [FK_COR_Cities_COR_Departments_DepartmentId]
    FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[COR_Departments]([Id]);
GO

-- People
ALTER TABLE [dbo].[COR_People]
    ADD CONSTRAINT [FK_COR_People_COR_Cities_CityId]
    FOREIGN KEY ([CityId]) REFERENCES [dbo].[COR_Cities]([Id]);
GO

-- Associates
ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_EmployerCompanies_EmployerCompanyId]
    FOREIGN KEY ([EmployerCompanyId]) REFERENCES [dbo].[COR_EmployerCompanies]([Id]);
GO

ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches]([Id]);
GO

ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_Sections_SectionId]
    FOREIGN KEY ([SectionId]) REFERENCES [dbo].[COR_Sections]([Id]);
GO

ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_WithdrawalReasons_WithdrawalReasonId]
    FOREIGN KEY ([WithdrawalReasonId]) REFERENCES [dbo].[COR_WithdrawalReasons]([Id]);
GO

ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_Advisors_AdvisorId]
    FOREIGN KEY ([AdvisorId]) REFERENCES [dbo].[COR_Advisors]([Id]);
GO

ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_Committees_CommitteeId]
    FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees]([Id]);
GO

ALTER TABLE [dbo].[COR_Associates]
    ADD CONSTRAINT [FK_COR_Associates_COR_People_ReferredBy]
    FOREIGN KEY ([ReferredBy]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Spouses
ALTER TABLE [dbo].[COR_Spouses]
    ADD CONSTRAINT [FK_COR_Spouses_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- People Financial
ALTER TABLE [dbo].[COR_PeopleFinancial]
    ADD CONSTRAINT [FK_COR_PeopleFinancial_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Associate Categories
ALTER TABLE [dbo].[COR_AssociateCategories]
    ADD CONSTRAINT [FK_COR_AssociateCategories_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Committee Members
ALTER TABLE [dbo].[COR_CommitteeMembers]
    ADD CONSTRAINT [FK_COR_CommitteeMembers_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[COR_CommitteeMembers]
    ADD CONSTRAINT [FK_COR_CommitteeMembers_COR_Committees_CommitteeId]
    FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees]([Id]);
GO

-- Beneficiaries
ALTER TABLE [dbo].[COR_Beneficiaries]
    ADD CONSTRAINT [FK_COR_Beneficiaries_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[COR_Beneficiaries]
    ADD CONSTRAINT [FK_COR_Beneficiaries_COR_Relationships_RelationshipId]
    FOREIGN KEY ([RelationshipId]) REFERENCES [dbo].[COR_Relationships]([Id]);
GO

ALTER TABLE [dbo].[COR_Beneficiaries]
    ADD CONSTRAINT [FK_COR_Beneficiaries_COR_Cities_CityId]
    FOREIGN KEY ([CityId]) REFERENCES [dbo].[COR_Cities]([Id]);
GO

-- References
ALTER TABLE [dbo].[COR_References]
    ADD CONSTRAINT [FK_COR_References_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[COR_References]
    ADD CONSTRAINT [FK_COR_References_COR_Cities_CityId]
    FOREIGN KEY ([CityId]) REFERENCES [dbo].[COR_Cities]([Id]);
GO

-- Cultural Activities, Sports, Courses, Recreational Events
ALTER TABLE [dbo].[COR_CulturalActivities]
    ADD CONSTRAINT [FK_COR_CulturalActivities_COR_Committees_CommitteeId]
    FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees]([Id]);
GO

ALTER TABLE [dbo].[COR_Sports]
    ADD CONSTRAINT [FK_COR_Sports_COR_Committees_CommitteeId]
    FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees]([Id]);
GO

ALTER TABLE [dbo].[COR_Courses]
    ADD CONSTRAINT [FK_COR_Courses_COR_Entities_EntityId]
    FOREIGN KEY ([EntityId]) REFERENCES [dbo].[COR_Entities]([Id]);
GO

ALTER TABLE [dbo].[COR_Courses]
    ADD CONSTRAINT [FK_COR_Courses_COR_Committees_CommitteeId]
    FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees]([Id]);
GO

ALTER TABLE [dbo].[COR_RecreationalEvents]
    ADD CONSTRAINT [FK_COR_RecreationalEvents_COR_Committees_CommitteeId]
    FOREIGN KEY ([CommitteeId]) REFERENCES [dbo].[COR_Committees]([Id]);
GO

-- Notifications
ALTER TABLE [dbo].[COR_Notifications]
    ADD CONSTRAINT [FK_COR_Notifications_COR_NotificationTemplates_TemplateId]
    FOREIGN KEY ([TemplateId]) REFERENCES [dbo].[COR_NotificationTemplates]([Id]);
GO

ALTER TABLE [dbo].[COR_Notifications]
    ADD CONSTRAINT [FK_COR_Notifications_COR_People_RecipientPersonId]
    FOREIGN KEY ([RecipientPersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- ============================================================
-- === ACC_ Foreign Keys (Accounting) ===
-- ============================================================

-- Account Balances
ALTER TABLE [dbo].[ACC_AccountBalances]
    ADD CONSTRAINT [FK_ACC_AccountBalances_ACC_ChartOfAccounts_AccountId]
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts]([Id]);
GO

ALTER TABLE [dbo].[ACC_AccountBalances]
    ADD CONSTRAINT [FK_ACC_AccountBalances_COR_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches]([Id]);
GO

ALTER TABLE [dbo].[ACC_AccountBalances]
    ADD CONSTRAINT [FK_ACC_AccountBalances_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

-- Journal Entries
ALTER TABLE [dbo].[ACC_JournalEntries]
    ADD CONSTRAINT [FK_ACC_JournalEntries_ACC_ChartOfAccounts_AccountId]
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts]([Id]);
GO

ALTER TABLE [dbo].[ACC_JournalEntries]
    ADD CONSTRAINT [FK_ACC_JournalEntries_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[ACC_JournalEntries]
    ADD CONSTRAINT [FK_ACC_JournalEntries_COR_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches]([Id]);
GO

ALTER TABLE [dbo].[ACC_JournalEntries]
    ADD CONSTRAINT [FK_ACC_JournalEntries_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

-- Documents
-- NOTE: ACC_Documents.VoucherTypeCode references ACC_VoucherTypes.Code (natural key),
-- not the surrogate Id. This is intentional to preserve legacy lookup by code.
ALTER TABLE [dbo].[ACC_Documents]
    ADD CONSTRAINT [FK_ACC_Documents_ACC_VoucherTypes_VoucherTypeCode]
    FOREIGN KEY ([VoucherTypeCode]) REFERENCES [dbo].[ACC_VoucherTypes]([Code]);
GO

-- Amortizations
ALTER TABLE [dbo].[ACC_Amortizations]
    ADD CONSTRAINT [FK_ACC_Amortizations_ACC_ChartOfAccounts_AccountId]
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts]([Id]);
GO

ALTER TABLE [dbo].[ACC_Amortizations]
    ADD CONSTRAINT [FK_ACC_Amortizations_COR_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches]([Id]);
GO

ALTER TABLE [dbo].[ACC_Amortizations]
    ADD CONSTRAINT [FK_ACC_Amortizations_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

-- Depreciations
ALTER TABLE [dbo].[ACC_Depreciations]
    ADD CONSTRAINT [FK_ACC_Depreciations_ACC_ChartOfAccounts_AccountId]
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts]([Id]);
GO

ALTER TABLE [dbo].[ACC_Depreciations]
    ADD CONSTRAINT [FK_ACC_Depreciations_COR_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches]([Id]);
GO

ALTER TABLE [dbo].[ACC_Depreciations]
    ADD CONSTRAINT [FK_ACC_Depreciations_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

-- Budgets
ALTER TABLE [dbo].[ACC_Budgets]
    ADD CONSTRAINT [FK_ACC_Budgets_ACC_ChartOfAccounts_AccountId]
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts]([Id]);
GO

ALTER TABLE [dbo].[ACC_Budgets]
    ADD CONSTRAINT [FK_ACC_Budgets_COR_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches]([Id]);
GO

ALTER TABLE [dbo].[ACC_Budgets]
    ADD CONSTRAINT [FK_ACC_Budgets_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

-- Bank Reconciliations
ALTER TABLE [dbo].[ACC_BankReconciliations]
    ADD CONSTRAINT [FK_ACC_BankReconciliations_ACC_ChartOfAccounts_AccountId]
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts]([Id]);
GO

ALTER TABLE [dbo].[ACC_BankReconciliations]
    ADD CONSTRAINT [FK_ACC_BankReconciliations_COR_Banks_BankId]
    FOREIGN KEY ([BankId]) REFERENCES [dbo].[COR_Banks]([Id]);
GO

-- Third-Party Accounts
ALTER TABLE [dbo].[ACC_ThirdPartyAccounts]
    ADD CONSTRAINT [FK_ACC_ThirdPartyAccounts_ACC_ChartOfAccounts_AccountId]
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts]([Id]);
GO

ALTER TABLE [dbo].[ACC_ThirdPartyAccounts]
    ADD CONSTRAINT [FK_ACC_ThirdPartyAccounts_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[ACC_ThirdPartyAccounts]
    ADD CONSTRAINT [FK_ACC_ThirdPartyAccounts_COR_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches]([Id]);
GO

ALTER TABLE [dbo].[ACC_ThirdPartyAccounts]
    ADD CONSTRAINT [FK_ACC_ThirdPartyAccounts_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

-- ============================================================
-- === LND_ Foreign Keys (Lending / Cartera) ===
-- ============================================================

-- Loan Portfolios
ALTER TABLE [dbo].[LND_LoanPortfolios]
    ADD CONSTRAINT [FK_LND_LoanPortfolios_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[LND_LoanPortfolios]
    ADD CONSTRAINT [FK_LND_LoanPortfolios_LND_CreditLineParameters_CreditLineId]
    FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters]([Id]);
GO

ALTER TABLE [dbo].[LND_LoanPortfolios]
    ADD CONSTRAINT [FK_LND_LoanPortfolios_COR_Branches_BranchId]
    FOREIGN KEY ([BranchId]) REFERENCES [dbo].[COR_Branches]([Id]);
GO

ALTER TABLE [dbo].[LND_LoanPortfolios]
    ADD CONSTRAINT [FK_LND_LoanPortfolios_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

-- Transactions
ALTER TABLE [dbo].[LND_Transactions]
    ADD CONSTRAINT [FK_LND_Transactions_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[LND_Transactions]
    ADD CONSTRAINT [FK_LND_Transactions_LND_CreditLineParameters_CreditLineId]
    FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters]([Id]);
GO

ALTER TABLE [dbo].[LND_Transactions]
    ADD CONSTRAINT [FK_LND_Transactions_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

ALTER TABLE [dbo].[LND_Transactions]
    ADD CONSTRAINT [FK_LND_Transactions_ACC_ChartOfAccounts_AccountId]
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[ACC_ChartOfAccounts]([Id]);
GO

ALTER TABLE [dbo].[LND_Transactions]
    ADD CONSTRAINT [FK_LND_Transactions_LND_TransactionCodes_TransactionCodeId]
    FOREIGN KEY ([TransactionCodeId]) REFERENCES [dbo].[LND_TransactionCodes]([Id]);
GO

-- Pending Installments
ALTER TABLE [dbo].[LND_PendingInstallments]
    ADD CONSTRAINT [FK_LND_PendingInstallments_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

ALTER TABLE [dbo].[LND_PendingInstallments]
    ADD CONSTRAINT [FK_LND_PendingInstallments_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Default Records
ALTER TABLE [dbo].[LND_DefaultRecords]
    ADD CONSTRAINT [FK_LND_DefaultRecords_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

-- Extra Payments
ALTER TABLE [dbo].[LND_ExtraPayments]
    ADD CONSTRAINT [FK_LND_ExtraPayments_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

-- Guarantees
ALTER TABLE [dbo].[LND_Guarantees]
    ADD CONSTRAINT [FK_LND_Guarantees_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[LND_Guarantees]
    ADD CONSTRAINT [FK_LND_Guarantees_LND_CreditLineParameters_CreditLineId]
    FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters]([Id]);
GO

ALTER TABLE [dbo].[LND_Guarantees]
    ADD CONSTRAINT [FK_LND_Guarantees_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

-- Collection Cases
ALTER TABLE [dbo].[LND_CollectionCases]
    ADD CONSTRAINT [FK_LND_CollectionCases_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[LND_CollectionCases]
    ADD CONSTRAINT [FK_LND_CollectionCases_SEC_Users_AssignedUserId]
    FOREIGN KEY ([AssignedUserId]) REFERENCES [dbo].[SEC_Users]([Id]);
GO

-- Savings Accounts
ALTER TABLE [dbo].[LND_SavingsAccounts]
    ADD CONSTRAINT [FK_LND_SavingsAccounts_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Deposit Accounts
ALTER TABLE [dbo].[LND_DepositAccounts]
    ADD CONSTRAINT [FK_LND_DepositAccounts_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Savings Transactions
ALTER TABLE [dbo].[LND_SavingsTransactions]
    ADD CONSTRAINT [FK_LND_SavingsTransactions_LND_SavingsAccounts_SavingsAccountId]
    FOREIGN KEY ([SavingsAccountId]) REFERENCES [dbo].[LND_SavingsAccounts]([Id]);
GO

ALTER TABLE [dbo].[LND_SavingsTransactions]
    ADD CONSTRAINT [FK_LND_SavingsTransactions_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Deposit Transactions
ALTER TABLE [dbo].[LND_DepositTransactions]
    ADD CONSTRAINT [FK_LND_DepositTransactions_LND_DepositAccounts_DepositAccountId]
    FOREIGN KEY ([DepositAccountId]) REFERENCES [dbo].[LND_DepositAccounts]([Id]);
GO

ALTER TABLE [dbo].[LND_DepositTransactions]
    ADD CONSTRAINT [FK_LND_DepositTransactions_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Payroll Deductions
ALTER TABLE [dbo].[LND_PayrollDeductions]
    ADD CONSTRAINT [FK_LND_PayrollDeductions_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

ALTER TABLE [dbo].[LND_PayrollDeductions]
    ADD CONSTRAINT [FK_LND_PayrollDeductions_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Portfolio Classifications
ALTER TABLE [dbo].[LND_PortfolioClassifications]
    ADD CONSTRAINT [FK_LND_PortfolioClassifications_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

ALTER TABLE [dbo].[LND_PortfolioClassifications]
    ADD CONSTRAINT [FK_LND_PortfolioClassifications_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Credit Applications
ALTER TABLE [dbo].[LND_CreditApplications]
    ADD CONSTRAINT [FK_LND_CreditApplications_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[LND_CreditApplications]
    ADD CONSTRAINT [FK_LND_CreditApplications_LND_CreditLineParameters_CreditLineId]
    FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters]([Id]);
GO

-- Loan Restructurings
ALTER TABLE [dbo].[LND_LoanRestructurings]
    ADD CONSTRAINT [FK_LND_LoanRestructurings_LND_LoanPortfolios_OriginalLoanId]
    FOREIGN KEY ([OriginalLoanId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

ALTER TABLE [dbo].[LND_LoanRestructurings]
    ADD CONSTRAINT [FK_LND_LoanRestructurings_LND_LoanPortfolios_NewLoanId]
    FOREIGN KEY ([NewLoanId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

-- Insurance Policies
ALTER TABLE [dbo].[LND_InsurancePolicies]
    ADD CONSTRAINT [FK_LND_InsurancePolicies_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

ALTER TABLE [dbo].[LND_InsurancePolicies]
    ADD CONSTRAINT [FK_LND_InsurancePolicies_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Interest Rate Changes
ALTER TABLE [dbo].[LND_InterestRateChanges]
    ADD CONSTRAINT [FK_LND_InterestRateChanges_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

-- Endorsers (Codeudores)
ALTER TABLE [dbo].[LND_Endorsers]
    ADD CONSTRAINT [FK_LND_Endorsers_LND_LoanPortfolios_LoanPortfolioId]
    FOREIGN KEY ([LoanPortfolioId]) REFERENCES [dbo].[LND_LoanPortfolios]([Id]);
GO

ALTER TABLE [dbo].[LND_Endorsers]
    ADD CONSTRAINT [FK_LND_Endorsers_COR_People_EndorserPersonId]
    FOREIGN KEY ([EndorserPersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- Circular Letters
ALTER TABLE [dbo].[LND_CircularLetters]
    ADD CONSTRAINT [FK_LND_CircularLetters_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- ============================================================
-- === PAY_ Foreign Keys (Payroll) ===
-- ============================================================

ALTER TABLE [dbo].[PAY_Employees]
    ADD CONSTRAINT [FK_PAY_Employees_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[PAY_Employees]
    ADD CONSTRAINT [FK_PAY_Employees_COR_CostCenters_CostCenterId]
    FOREIGN KEY ([CostCenterId]) REFERENCES [dbo].[COR_CostCenters]([Id]);
GO

ALTER TABLE [dbo].[PAY_Employees]
    ADD CONSTRAINT [FK_PAY_Employees_PAY_HealthInsuranceProviders_HealthInsuranceProviderId]
    FOREIGN KEY ([HealthInsuranceProviderId]) REFERENCES [dbo].[PAY_HealthInsuranceProviders]([Id]);
GO

ALTER TABLE [dbo].[PAY_Employees]
    ADD CONSTRAINT [FK_PAY_Employees_PAY_PensionProviders_PensionProviderId]
    FOREIGN KEY ([PensionProviderId]) REFERENCES [dbo].[PAY_PensionProviders]([Id]);
GO

ALTER TABLE [dbo].[PAY_Employees]
    ADD CONSTRAINT [FK_PAY_Employees_PAY_WorkRiskProviders_WorkRiskProviderId]
    FOREIGN KEY ([WorkRiskProviderId]) REFERENCES [dbo].[PAY_WorkRiskProviders]([Id]);
GO

ALTER TABLE [dbo].[PAY_Employees]
    ADD CONSTRAINT [FK_PAY_Employees_PAY_SeveranceProviders_SeveranceProviderId]
    FOREIGN KEY ([SeveranceProviderId]) REFERENCES [dbo].[PAY_SeveranceProviders]([Id]);
GO

ALTER TABLE [dbo].[PAY_Employees]
    ADD CONSTRAINT [FK_PAY_Employees_COR_Banks_BankId]
    FOREIGN KEY ([BankId]) REFERENCES [dbo].[COR_Banks]([Id]);
GO

-- Payroll Transactions
ALTER TABLE [dbo].[PAY_PayrollTransactions]
    ADD CONSTRAINT [FK_PAY_PayrollTransactions_PAY_Employees_EmployeeId]
    FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees]([Id]);
GO

ALTER TABLE [dbo].[PAY_PayrollTransactions]
    ADD CONSTRAINT [FK_PAY_PayrollTransactions_PAY_PayrollConcepts_ConceptId]
    FOREIGN KEY ([ConceptId]) REFERENCES [dbo].[PAY_PayrollConcepts]([Id]);
GO

-- Absences
ALTER TABLE [dbo].[PAY_Absences]
    ADD CONSTRAINT [FK_PAY_Absences_PAY_Employees_EmployeeId]
    FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees]([Id]);
GO

ALTER TABLE [dbo].[PAY_Absences]
    ADD CONSTRAINT [FK_PAY_Absences_PAY_PayrollConcepts_ConceptId]
    FOREIGN KEY ([ConceptId]) REFERENCES [dbo].[PAY_PayrollConcepts]([Id]);
GO

-- Direct Debits
ALTER TABLE [dbo].[PAY_DirectDebits]
    ADD CONSTRAINT [FK_PAY_DirectDebits_PAY_Employees_EmployeeId]
    FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees]([Id]);
GO

ALTER TABLE [dbo].[PAY_DirectDebits]
    ADD CONSTRAINT [FK_PAY_DirectDebits_PAY_PayrollConcepts_ConceptId]
    FOREIGN KEY ([ConceptId]) REFERENCES [dbo].[PAY_PayrollConcepts]([Id]);
GO

-- Payroll Plan Liquidations
ALTER TABLE [dbo].[PAY_PayrollPlanLiquidations]
    ADD CONSTRAINT [FK_PAY_PayrollPlanLiquidations_PAY_Employees_EmployeeId]
    FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[PAY_Employees]([Id]);
GO

-- ============================================================
-- === INV_ Foreign Keys (Inventory) ===
-- ============================================================

ALTER TABLE [dbo].[INV_Products]
    ADD CONSTRAINT [FK_INV_Products_INV_ProductGroups_ProductGroupId]
    FOREIGN KEY ([ProductGroupId]) REFERENCES [dbo].[INV_ProductGroups]([Id]);
GO

ALTER TABLE [dbo].[INV_Products]
    ADD CONSTRAINT [FK_INV_Products_INV_DiscountTypes_DiscountTypeId]
    FOREIGN KEY ([DiscountTypeId]) REFERENCES [dbo].[INV_DiscountTypes]([Id]);
GO

ALTER TABLE [dbo].[INV_ProductGroups]
    ADD CONSTRAINT [FK_INV_ProductGroups_INV_SecondaryGroups_SecondaryGroupId]
    FOREIGN KEY ([SecondaryGroupId]) REFERENCES [dbo].[INV_SecondaryGroups]([Id]);
GO

ALTER TABLE [dbo].[INV_SecondaryGroups]
    ADD CONSTRAINT [FK_INV_SecondaryGroups_INV_PrimaryGroups_PrimaryGroupId]
    FOREIGN KEY ([PrimaryGroupId]) REFERENCES [dbo].[INV_PrimaryGroups]([Id]);
GO

ALTER TABLE [dbo].[INV_Transactions]
    ADD CONSTRAINT [FK_INV_Transactions_INV_TransactionTypes_TransactionTypeId]
    FOREIGN KEY ([TransactionTypeId]) REFERENCES [dbo].[INV_TransactionTypes]([Id]);
GO

ALTER TABLE [dbo].[INV_Transactions]
    ADD CONSTRAINT [FK_INV_Transactions_INV_Products_ProductId]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[INV_Products]([Id]);
GO

ALTER TABLE [dbo].[INV_Transactions]
    ADD CONSTRAINT [FK_INV_Transactions_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[INV_WarehouseStock]
    ADD CONSTRAINT [FK_INV_WarehouseStock_INV_Products_ProductId]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[INV_Products]([Id]);
GO

ALTER TABLE [dbo].[INV_WarehouseStock]
    ADD CONSTRAINT [FK_INV_WarehouseStock_INV_Warehouses_WarehouseId]
    FOREIGN KEY ([WarehouseId]) REFERENCES [dbo].[INV_Warehouses]([Id]);
GO

ALTER TABLE [dbo].[INV_InvoiceDetails]
    ADD CONSTRAINT [FK_INV_InvoiceDetails_INV_Products_ProductId]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[INV_Products]([Id]);
GO

-- ============================================================
-- === CDT_ Foreign Keys (Certificates of Deposit) ===
-- ============================================================

ALTER TABLE [dbo].[CDT_Certificates]
    ADD CONSTRAINT [FK_CDT_Certificates_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[CDT_Certificates]
    ADD CONSTRAINT [FK_CDT_Certificates_LND_CreditLineParameters_CreditLineId]
    FOREIGN KEY ([CreditLineId]) REFERENCES [dbo].[LND_CreditLineParameters]([Id]);
GO

ALTER TABLE [dbo].[CDT_Transactions]
    ADD CONSTRAINT [FK_CDT_Transactions_CDT_Certificates_CertificateId]
    FOREIGN KEY ([CertificateId]) REFERENCES [dbo].[CDT_Certificates]([Id]);
GO

ALTER TABLE [dbo].[CDT_Transactions]
    ADD CONSTRAINT [FK_CDT_Transactions_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

-- ============================================================
-- === DEB_ Foreign Keys (Debit Cards) ===
-- ============================================================

ALTER TABLE [dbo].[DEB_Cards]
    ADD CONSTRAINT [FK_DEB_Cards_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[DEB_Cards]
    ADD CONSTRAINT [FK_DEB_Cards_COR_Banks_BankId]
    FOREIGN KEY ([BankId]) REFERENCES [dbo].[COR_Banks]([Id]);
GO

ALTER TABLE [dbo].[DEB_Transactions]
    ADD CONSTRAINT [FK_DEB_Transactions_DEB_Cards_CardId]
    FOREIGN KEY ([CardId]) REFERENCES [dbo].[DEB_Cards]([Id]);
GO

-- ============================================================
-- === TRS_ Foreign Keys (Treasury) ===
-- ============================================================

ALTER TABLE [dbo].[TRS_Checks]
    ADD CONSTRAINT [FK_TRS_Checks_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[TRS_Checks]
    ADD CONSTRAINT [FK_TRS_Checks_COR_Banks_BankId]
    FOREIGN KEY ([BankId]) REFERENCES [dbo].[COR_Banks]([Id]);
GO

ALTER TABLE [dbo].[TRS_PaymentOrders]
    ADD CONSTRAINT [FK_TRS_PaymentOrders_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[TRS_PaymentOrders]
    ADD CONSTRAINT [FK_TRS_PaymentOrders_COR_Banks_BankId]
    FOREIGN KEY ([BankId]) REFERENCES [dbo].[COR_Banks]([Id]);
GO

ALTER TABLE [dbo].[TRS_CashRegisterClosings]
    ADD CONSTRAINT [FK_TRS_CashRegisterClosings_SEC_Users_CashierId]
    FOREIGN KEY ([CashierId]) REFERENCES [dbo].[SEC_Users]([Id]);
GO

-- ============================================================
-- === SEC_ Foreign Keys (Security) ===
-- ============================================================

ALTER TABLE [dbo].[SEC_UserRoles]
    ADD CONSTRAINT [FK_SEC_UserRoles_SEC_Users_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]);
GO

ALTER TABLE [dbo].[SEC_UserRoles]
    ADD CONSTRAINT [FK_SEC_UserRoles_SEC_Roles_RoleId]
    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SEC_Roles]([Id]);
GO

ALTER TABLE [dbo].[SEC_RolePermissions]
    ADD CONSTRAINT [FK_SEC_RolePermissions_SEC_Roles_RoleId]
    FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SEC_Roles]([Id]);
GO

ALTER TABLE [dbo].[SEC_RolePermissions]
    ADD CONSTRAINT [FK_SEC_RolePermissions_SEC_Permissions_PermissionId]
    FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[SEC_Permissions]([Id]);
GO

ALTER TABLE [dbo].[SEC_RefreshTokens]
    ADD CONSTRAINT [FK_SEC_RefreshTokens_SEC_Users_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]);
GO

ALTER TABLE [dbo].[SEC_UserSessions]
    ADD CONSTRAINT [FK_SEC_UserSessions_SEC_Users_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]);
GO

-- ============================================================
-- === AUD_ Foreign Keys (Audit) ===
-- ============================================================

ALTER TABLE [dbo].[AUD_AuditLog]
    ADD CONSTRAINT [FK_AUD_AuditLog_SEC_Users_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]);
GO

-- ============================================================
-- === ADM_ Foreign Keys (Administration / Multi-Tenant) ===
-- ============================================================

ALTER TABLE [dbo].[ADM_Subscriptions]
    ADD CONSTRAINT [FK_ADM_Subscriptions_ADM_Tenants_TenantId]
    FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants]([Id]);
GO

ALTER TABLE [dbo].[ADM_TenantSettings]
    ADD CONSTRAINT [FK_ADM_TenantSettings_ADM_Tenants_TenantId]
    FOREIGN KEY ([TenantId]) REFERENCES [dbo].[ADM_Tenants]([Id]);
GO

-- ============================================================
-- === WEB_ Foreign Keys (Web / Portal) ===
-- ============================================================

ALTER TABLE [dbo].[WEB_PortalUsers]
    ADD CONSTRAINT [FK_WEB_PortalUsers_COR_People_PersonId]
    FOREIGN KEY ([PersonId]) REFERENCES [dbo].[COR_People]([Id]);
GO

ALTER TABLE [dbo].[WEB_PortalUsers]
    ADD CONSTRAINT [FK_WEB_PortalUsers_SEC_Users_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[SEC_Users]([Id]);
GO

ALTER TABLE [dbo].[WEB_OnlineTransactions]
    ADD CONSTRAINT [FK_WEB_OnlineTransactions_WEB_PortalUsers_PortalUserId]
    FOREIGN KEY ([PortalUserId]) REFERENCES [dbo].[WEB_PortalUsers]([Id]);
GO

ALTER TABLE [dbo].[WEB_SupportTickets]
    ADD CONSTRAINT [FK_WEB_SupportTickets_WEB_PortalUsers_PortalUserId]
    FOREIGN KEY ([PortalUserId]) REFERENCES [dbo].[WEB_PortalUsers]([Id]);
GO

ALTER TABLE [dbo].[WEB_SupportTickets]
    ADD CONSTRAINT [FK_WEB_SupportTickets_SEC_Users_AssignedUserId]
    FOREIGN KEY ([AssignedUserId]) REFERENCES [dbo].[SEC_Users]([Id]);
GO

PRINT N'Foreign keys created successfully. Total: ~155 constraints.';
GO
