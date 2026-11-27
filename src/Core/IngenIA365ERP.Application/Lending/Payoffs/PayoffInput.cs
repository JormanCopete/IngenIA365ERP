using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>
/// Un producto que el crédito nuevo recoge con su desembolso (feature 028, R1; api.md): un crédito, una cuenta de aportes o ahorros, o una factura a
/// crédito de Comercial. Los créditos y las cuentas se nombran por su <c>PublicId</c>; la factura, por su documento cruce (<see cref="Reference"/>).
/// <see cref="Amount"/> es el valor de un parcial o de un abono; <see cref="InstallmentTreatment"/>, la elección del asesor sobre la cuota causada de
/// un crédito cuando <c>Recogida.CuotaCausada</c> dice «Preguntar». Lo usan <c>CreditApplicationInput.Payoffs</c> y <c>DisbursementInput.Payoffs</c>;
/// la lista vieja <c>ownLoanPayoffs</c> se sigue aceptando y se traduce (<see cref="RecogidasPedidas.Unificar"/>). (nuevo)
/// </summary>
public sealed record PayoffInput(
    PayoffProductKind Kind,
    Guid? ProductPublicId,
    PayoffMode Mode,
    decimal? Amount = null,
    string? Reference = null,
    InstallmentTreatment? InstallmentTreatment = null);

/// <summary>
/// La traducción de lo que pide el cuerpo de la solicitud o del desembolso a una sola lista de <see cref="PayoffInput"/> (feature 028, R1): la lista
/// nueva <c>payoffs</c> tal cual, o la vieja <c>ownLoanPayoffs</c> como créditos (<c>Full</c> → <c>Total</c>, <c>Partial</c> → <c>Partial</c> con su
/// valor). Las dos a la vez, o un modo viejo desconocido, es un cuerpo que no se puede leer: <see cref="CodigoCuerpoInvalido"/>, el mismo 400 de un
/// JSON ilegible. Una lista vacía cuenta como no enviada. (nuevo)
/// </summary>
public static class RecogidasPedidas
{
    /// <summary>El código de un cuerpo que no se puede leer (el que pone el manejador de excepciones a un JSON ilegible).</summary>
    public const string CodigoCuerpoInvalido = "Request.BodyInvalid";

    /// <summary>Los modos de la lista vieja (<c>OwnLoanPayoffInput.Mode</c>).</summary>
    public const string ModoViejoTotal = "Full";

    /// <inheritdoc cref="ModoViejoTotal"/>
    public const string ModoViejoParcial = "Partial";

    /// <summary>Una sola lista de recogidas a partir de las dos del cuerpo.</summary>
    public static Result<IReadOnlyList<PayoffInput>> Unificar(IReadOnlyList<PayoffInput>? payoffs, IReadOnlyList<OwnLoanPayoffInput>? ownLoanPayoffs)
    {
        var nuevas = payoffs ?? [];
        var viejas = ownLoanPayoffs ?? [];
        if (nuevas.Count > 0 && viejas.Count > 0)
            return Result.Failure<IReadOnlyList<PayoffInput>>(CodigoCuerpoInvalido,
                "La solicitud no tiene el formato esperado: envíe los productos que recoge en «payoffs» o en «ownLoanPayoffs», no en los dos.");
        if (viejas.Count == 0) return Result.Success(nuevas);

        var traducidas = new List<PayoffInput>(viejas.Count);
        foreach (var vieja in viejas)
        {
            var modo = ModoDeLaListaVieja(vieja.Mode);
            if (modo is null)
                return Result.Failure<IReadOnlyList<PayoffInput>>(CodigoCuerpoInvalido,
                    $"La solicitud no tiene el formato esperado: «{vieja.Mode}» no es un modo de recogida. Admite: {ModoViejoTotal}, {ModoViejoParcial}.");
            traducidas.Add(new PayoffInput(PayoffProductKind.Loan, vieja.LoanPublicId, modo.Value,
                modo == PayoffMode.Partial ? vieja.Amount : null));
        }
        return Result.Success<IReadOnlyList<PayoffInput>>(traducidas);
    }

    /// <summary><c>Full</c> (o vacío, el defecto del registro viejo) → <c>Total</c>; <c>Partial</c> → <c>Partial</c>; otro, nulo.</summary>
    private static PayoffMode? ModoDeLaListaVieja(string? modo) =>
        string.IsNullOrWhiteSpace(modo) || string.Equals(modo.Trim(), ModoViejoTotal, StringComparison.OrdinalIgnoreCase) ? PayoffMode.Total
        : string.Equals(modo.Trim(), ModoViejoParcial, StringComparison.OrdinalIgnoreCase) ? PayoffMode.Partial
        : null;
}
