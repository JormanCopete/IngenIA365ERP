namespace IngenIA365ERP.Domain.Enums.Accounting;

/// <summary>
/// Módulos que pueden parametrizar y mover una cuenta de movimiento (feature 009, FR-010).
/// Banderas: una cuenta puede estar habilitada para varios.
/// </summary>
[Flags]
public enum AccountingModules
{
    None = 0,
    Accounting = 1,
    Payroll = 2,
    Lending = 4,
    Inventory = 8,
    Treasury = 16,
    Savings = 32,
    Assets = 64,
}

/// <summary>Naturaleza de la cuenta: la trae el catálogo y la heredan las auxiliares (FR-011).</summary>
public enum AccountNature { Debit = 1, Credit = 2 }

/// <summary>Quién creó la cuenta: el catálogo (niveles 1–4, no se edita) o la empresa (auxiliares).</summary>
public enum AccountOrigin { Catalog = 1, Company = 2 }

/// <summary>De dónde salió un catálogo PUC: transcrito de la norma o importado por la cooperativa.</summary>
public enum CatalogSource { Official = 1, Imported = 2 }

/// <summary>Para qué sirve un tipo de comprobante. Los reservados no se digitan a mano (FR-019).</summary>
public enum VoucherUsage { Manual = 1, Module = 2, Closing = 3, Opening = 4, Assets = 5 }

public enum PeriodStatus { Open = 1, Closed = 2 }

/// <summary>Borrador → Contabilizado → Reversado. Un contabilizado nunca vuelve a borrador (Principio XI).</summary>
public enum DocumentStatus { Draft = 1, Posted = 2, Reversed = 3 }

public enum DocumentKind { Regular = 1, Opening = 2, Closing = 3, Reversal = 4 }

public enum TaxKind { None = 0, Withholding = 1, Vat = 2, Ica = 3, Gmf = 4, IncomeTax = 5 }

public enum FinancialStatementKind { FinancialPosition = 1, IncomeStatement = 2, EquityChanges = 3, CashFlow = 4 }

public enum ReconciliationStatus { Open = 1, Closed = 2, Outdated = 3 }

public enum MatchKind { Auto = 1, Manual = 2 }

/// <summary>Cómo viene el valor en el extracto: una columna con signo, o débito y crédito separados.</summary>
public enum SignConvention { SingleAmountDebitPositive = 1, SingleAmountCreditPositive = 2, SeparateColumns = 3 }

public enum BudgetStatus { Draft = 1, Approved = 2, Superseded = 3 }

public enum CertificateStatus { Current = 1, Outdated = 2, Voided = 3 }

public enum TaxFormSelector { Base = 1, Amount = 2 }

public enum ExogenousOrigin { Seed = 1, Custom = 2 }

public enum ExogenousRunStatus { Working = 1, Exported = 2 }

public enum FixedAssetKind { Asset = 1, Deferred = 2 }

public enum FixedAssetStatus { Active = 1, FullyDepreciated = 2, Retired = 3 }

public enum InstallmentStatus { Pending = 1, Posted = 2, Reversed = 3 }

public enum AssetRunStatus { Posted = 1, Reversed = 2 }
