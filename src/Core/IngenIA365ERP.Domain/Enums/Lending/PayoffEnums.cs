namespace IngenIA365ERP.Domain.Enums.Lending;

// 2026-11-27 (pedido del dueño; feature 028): la recogida de créditos y productos en el desembolso (LND_CreditApplicationPayoffs,
// LND_DisbursementPayoffs). Se guardan por su número: un valor nunca se renumera (EnumeracionesDeCarteraTests). (nuevo)

/// <summary>Qué clase de producto recoge el crédito nuevo: otro crédito, una cuenta (aportes o ahorros) o una factura a crédito de Comercial. (nuevo)</summary>
public enum PayoffProductKind
{
    Loan = 1,
    Account = 2,
    CommercialReceivable = 3,
}

/// <summary>
/// Cómo se recoge un producto: todo (<c>Total</c>), una parte (<c>Partial</c>: lo exigible y el resto a capital, D7), las cuotas pendientes de una
/// cuenta (<c>Installments</c>) o un abono a un ahorro o aporte extraordinario (<c>Deposit</c>). (nuevo)
/// </summary>
public enum PayoffMode
{
    Total = 1,
    Partial = 2,
    Installments = 3,
    Deposit = 4,
}

/// <summary>Qué se hace con la cuota causada del crédito recogido (<c>Recogida.CuotaCausada</c>): se incluye o queda tal como se envió. (nuevo)</summary>
public enum InstallmentTreatment
{
    Include = 1,
    Exclude = 2,
}

/// <summary>
/// Por qué lo que se recoge al desembolsar difiere de lo proyectado en la solicitud. Banderas: una diferencia puede tener varias causas a la vez.
/// <c>CurrentInstallment</c> y <c>Parameters</c> las marca quien compara (el desembolso), no el comparador de desgloses. (nuevo)
/// </summary>
[Flags]
public enum PayoffDifferenceCause
{
    None = 0,
    Payments = 1,
    Accrual = 2,
    Arrears = 4,
    CurrentInstallment = 8,
    Parameters = 16,
    ProductClosed = 32,
    Other = 64,
}

/// <summary>
/// En qué va la cuota causada frente a su canal: sin envío, en un archivo de libranza enviado y sin respuesta, descontada y pendiente de giro, no
/// descontada, o en la nómina propia del período en curso. (nuevo)
/// </summary>
public enum SentInstallmentState
{
    None = 0,
    SentWithoutResponse = 1,
    DeductedPendingTransfer = 2,
    NotDeducted = 3,
    OwnPayroll = 4,
}
