namespace IngenIA365ERP.Domain.Enums.Core;

// Catálogo tributario de Core (feature 012, T026; decisiones-transversales §2.5). Se guardan como
// int y un valor nunca se renumera. Distinto de Enums.Accounting.TaxKind (el de la cuenta contable
// de impuesto, feature 009): éste describe la definición del impuesto en COR_TaxDefinitions.

public enum TaxKind { Iva = 1, Inc = 2, ReteFuente = 3, ReteIva = 4, ReteIca = 5, Ica = 6, Other = 99 }

public enum TaxCalculationForm { PercentOfBase = 1, PercentOfTax = 2, AmountPerUnit = 3 }

public enum TaxTreatment { Generated = 1, Deductible = 2, AddedToCost = 3, WithholdingApplied = 4, WithholdingSuffered = 5 }

public enum TaxAppliesTo { Purchases = 1, Sales = 2, Both = 3 }

public enum VatSaleTreatment { Taxed = 1, Exempt = 2, Excluded = 3 }
