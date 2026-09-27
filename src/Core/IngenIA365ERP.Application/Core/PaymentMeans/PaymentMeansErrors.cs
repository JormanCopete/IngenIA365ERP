using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Core.PaymentMeans;

/// <summary>
/// Los errores del catálogo de medios de pago de Core (feature 012, I3, T590–T592; contracts/api.md §22.1, §22.2, §22.5):
/// <c>Core.PaymentMeans.Invalid</c> con <c>data { field, rule }</c> para toda combinación que el medio no admite (en la
/// importación sale como error de fila dentro de <c>Import.Invalid</c>), <c>Core.PaymentMeans.InUse</c> con
/// <c>data { payments }</c>, y el «en uso» de franquicias, adquirentes y datáfonos. Los <c>NotFound</c> y
/// <c>Core.CashDenomination.InUse</c> son (nuevo). Un código repetido es <c>Catalogo.CodigoDuplicado</c>.
/// </summary>
public static class PaymentMeansErrors
{
    public const string NotFoundCode = "Core.PaymentMeans.NotFound";
    public const string InvalidCode = "Core.PaymentMeans.Invalid";
    public const string InUseCode = "Core.PaymentMeans.InUse";
    public const string CardNetworkNotFoundCode = "Core.CardNetwork.NotFound";
    public const string CardNetworkInUseCode = "Core.CardNetwork.InUse";
    public const string CardAcquirerNotFoundCode = "Core.CardAcquirer.NotFound";
    public const string CardAcquirerInUseCode = "Core.CardAcquirer.InUse";
    public const string CardTerminalNotFoundCode = "Core.CardTerminal.NotFound";
    public const string CardTerminalInUseCode = "Core.CardTerminal.InUse";
    public const string CashDenominationNotFoundCode = "Core.CashDenomination.NotFound";
    public const string CashDenominationInUseCode = "Core.CashDenomination.InUse";
    public const string BankNotFoundCode = "Core.Bank.NotFound";

    public static Error NotFound(string? codigo = null) => new(NotFoundCode,
        codigo is null ? "El medio de pago no existe." : $"No hay un medio de pago «{codigo}». Créelo en Maestros › Medios de pago.");

    /// <summary>Una combinación que el medio no admite (422, <c>data { field, rule }</c>).</summary>
    public static Error Invalid(string field, string rule, string mensaje) => new ErrorConDatos(InvalidCode, mensaje, new { field, rule });

    /// <summary>El medio tiene pagos: la clase, la red, el adquirente y el arqueo no cambian, ni se borra (422).</summary>
    public static Error InUse(int payments) => new ErrorConDatos(InUseCode,
        $"El medio tiene {payments} pago(s): su clase, franquicia, adquirente y arqueo no cambian y no se borra. Inactívelo y cree otro.",
        new { payments });

    public static Error CardNetworkNotFound(string? codigo = null) => new(CardNetworkNotFoundCode,
        codigo is null ? "La franquicia no existe." : $"No hay una franquicia «{codigo}». Créela en la hoja Franquicias o en Maestros › Medios de pago.");

    public static Error CardNetworkInUse(int paymentMeans) => new ErrorConDatos(CardNetworkInUseCode,
        $"La franquicia la usan {paymentMeans} medio(s) de pago: no se borra. Inactívela.", new { paymentMeans });

    public static Error CardAcquirerNotFound(string? codigo = null) => new(CardAcquirerNotFoundCode,
        codigo is null ? "El adquirente no existe." : $"No hay un adquirente «{codigo}». Créelo en la hoja Adquirentes o en Maestros › Medios de pago.");

    public static Error CardAcquirerInUse(int paymentMeans, int terminals) => new ErrorConDatos(CardAcquirerInUseCode,
        $"El adquirente lo usan {paymentMeans} medio(s) de pago y {terminals} datáfono(s): no se borra. Inactívelo.",
        new { paymentMeans, terminals });

    public static Error CardTerminalNotFound(string? codigo = null) => new(CardTerminalNotFoundCode,
        codigo is null ? "El datáfono no existe." : $"No hay un datáfono «{codigo}». Créelo en la hoja Datafonos o en Maestros › Medios de pago.");

    public static Error CardTerminalInUse(int payments, int cashRegisters) => new ErrorConDatos(CardTerminalInUseCode,
        $"El datáfono tiene {payments} pago(s) y lo proponen {cashRegisters} caja(s): no se borra. Inactívelo.",
        new { payments, cashRegisters });

    public static Error CashDenominationNotFound() => new(CashDenominationNotFoundCode, "La denominación no existe.");

    public static Error CashDenominationInUse(int counts) => new ErrorConDatos(CashDenominationInUseCode,
        $"La denominación está en {counts} arqueo(s): no se borra. Cierre su vigencia.", new { counts });

    public static Error BankNotFound(string? codigo = null) => new(BankNotFoundCode,
        codigo is null ? "El banco no existe." : $"No hay un banco «{codigo}». Créelo en Maestros › Bancos.");
}
