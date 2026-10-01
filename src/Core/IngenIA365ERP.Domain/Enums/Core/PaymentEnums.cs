namespace IngenIA365ERP.Domain.Enums.Core;

// Medios de pago de Core (feature 012, T027; decisiones-transversales §2.5). Los usa el mensaje de
// ventas de la parte A (contracts/mensajes.md) y el catálogo de medios de pago de US5 sólo los
// consume. Se guardan como int y un valor nunca se renumera.

public enum PaymentMeansClass
{
    Cash = 1, CreditCard = 2, DebitCard = 3, AssociateCredit = 4, CustomerCredit = 5,
    BankDeposit = 6, Transfer = 7, Voucher = 8, Check = 9, Other = 99,
}

/// <summary>Cómo se arquea un medio al cerrar la caja.</summary>
public enum CashCountMethod { PhysicalCount = 1, VoucherTotal = 2, ByReference = 3, None = 4 }

public enum PaymentReferenceKind { Approval = 1, Receipt = 2, Deposit = 3, VoucherNumber = 4, CheckNumber = 5, Other = 99 }

public enum CardKind { Credit = 1, Debit = 2, Both = 3 }
