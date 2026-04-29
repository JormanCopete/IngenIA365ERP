using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_ChartOfAccounts] (cnt_maecuen).</summary>
public class ChartOfAccount : AuditableEntity
{
    public string? LegacyCode { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Nature { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte Level { get; set; }
    public decimal Rate { get; set; }
    public bool RequiresDocument { get; set; }
    public bool ManagesCostCenter { get; set; }
    public bool RequiresThirdParty { get; set; }
    public bool IsOperationalAsset { get; set; }
    public bool AppliesToLending { get; set; }
    public bool AppliesToSavingsCDT { get; set; }
    public bool AppliesToInventory { get; set; }
    public bool AppliesToTreasury { get; set; }
    public bool AppliesToPayroll { get; set; }
    public bool AppliesToAccounting { get; set; }
    public bool AppliesToInvoicing { get; set; }
    public string? CostCenterCode { get; set; }
    public string? FixedAssetGroup { get; set; }
    public string? FixedAssetClass { get; set; }
    public int Status { get; set; }
    public string? CashFlowCode { get; set; }
    public string? BankReconciliationCode { get; set; }
    public string? WithholdingType { get; set; }
    public string? AccountBelongsTo { get; set; }
    public int? ReportFormatId { get; set; }
    public int? ConceptId { get; set; }
    public int? SourceId { get; set; }
    public string? AccountGroup { get; set; }

    // Withholding / tax line references
    public string? WithholdingLineCode { get; set; }
    public string? IcaLineCode { get; set; }
    public string? VatLineCode { get; set; }
    public string? SalesWithholdingLineCode { get; set; }
    public bool IncomeTaxDeclaration { get; set; }
    public string? GmfLineCode { get; set; }
    public string? IcaBaseLineCode { get; set; }
    public string? GmfBaseLineCode { get; set; }

    // Financial statement grouping
    public int? FinStmtCashFlowNumber { get; set; }
    public int? FinStmtCashFlowSubgroup { get; set; }
    public int? FinStmtChangeNumber { get; set; }
    public int? FinStmtChangeSubgroup { get; set; }
    public int? FinStmtFinPosNumber { get; set; }
    public int? FinStmtFinPosSubgroup { get; set; }
    public int? FinStmtBalSheetNumber { get; set; }
    public int? FinStmtBalSheetSubgroup { get; set; }
    public int? FinStmtEquityNumber { get; set; }
    public int? FinStmtEquitySubgroup { get; set; }
    public int? FinStmtIncomeNumber { get; set; }

    // Navigation
    public ICollection<AccountBalance> Balances { get; set; } = [];
    public ICollection<JournalEntry> JournalEntries { get; set; } = [];
    public ICollection<AuxiliaryDocument> AuxiliaryDocuments { get; set; } = [];
    public ICollection<ThirdPartyAccount> ThirdPartyAccounts { get; set; } = [];
    public ICollection<Amortization> Amortizations { get; set; } = [];
    public ICollection<Depreciation> Depreciations { get; set; } = [];
    public ICollection<Budget> Budgets { get; set; } = [];
    public ICollection<BankReconciliation> BankReconciliations { get; set; } = [];
    public ICollection<BankReconciliationMaster> BankReconciliationMasters { get; set; } = [];
    public ICollection<FinancialReport> FinancialReports { get; set; } = [];
}
