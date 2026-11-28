using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>
/// <b>Quién recogió un crédito ya cancelado</b> y qué se hace con lo que su pagaduría gire después (feature 028, E2, T069; R7, FR-022, HU4-5, D3): el último
/// desembolso vigente (no reversado) que lo recogió <b>todo</b> —fila <c>LND_DisbursementPayoffs</c> aplicada o, antes de la 028, el pago <c>CAR</c> con la
/// clave de la recogida— (<see cref="VinculosDeRecogida"/>) y la regla de <c>Recogida.GiroPosteriorDeCreditoRecogido</c> (general, a hoy). Con ellos el
/// contrato de obligaciones arma el rechazo <c>Lending.Payment.ObligationNotPayable</c> con el sucesor en <c>data</c>
/// (<see cref="GiroPosteriorDeCreditoRecogido"/>); un crédito cancelado por otra vía, anulado o novado recibe el rechazo de siempre. Sólo lectura. (nuevo)
/// </summary>
public sealed class SucesorDeCreditoRecogido(IApplicationDbContext db, ReglasDeParametrosDeCartera parametros, IDateTimeService reloj)
{
    /// <summary>El desembolso que recogió todo <paramref name="credito"/> y lo dejó cancelado; nulo si no lo cancelaron por una recogida.</summary>
    public async Task<VinculoDeRecogida?> SucesorAsync(Loan credito, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        if (credito.Status != LoanStatus.PaidOff) return null;
        return (await VinculosDeRecogida.DeLosRecogidosAsync(db, [credito.Id], ct))
            .LastOrDefault(v => v.Clase == PayoffProductKind.Loan && v.RecogidoId == credito.Id && v.Modo == PayoffMode.Total && !v.Reversada);
    }

    /// <summary>Lo que dice <c>Recogida.GiroPosteriorDeCreditoRecogido</c> a <paramref name="fecha"/> (general); ilegible = lo de siempre.</summary>
    public async Task<string> ReglaAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.RecogidaGiroPosteriorDeCreditoRecogido, fecha, ct: ct);
        return (leido.IsSuccess ? leido.Value.Texto?.Trim() : null) switch
        {
            ParametrosDeCartera.GiroPosteriorAlCreditoNuevo => GiroPosteriorDeCreditoRecogido.AlCreditoNuevo,
            ParametrosDeCartera.GiroPosteriorAFavorDelAsociado => GiroPosteriorDeCreditoRecogido.AFavorDelAsociado,
            _ => GiroPosteriorDeCreditoRecogido.SaldoDeLaPagaduria,
        };
    }

    /// <summary>
    /// El rechazo de un pago a <paramref name="credito"/>, que ya no recibe pagos: si lo recogió el desembolso de otro, con el sucesor y la regla en
    /// <c>data</c> y el mensaje que lo dice; si no, el de siempre (<see cref="ErroresDePagos.NoPagable"/>).
    /// </summary>
    public async Task<Error> NoPagableAsync(Loan credito, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        var estado = credito.Status.ToString();
        if (await SucesorAsync(credito, ct) is not { } sucesor) return ErroresDePagos.NoPagable(credito.Number, estado);
        var regla = await ReglaAsync(reloj.HoyLocal, ct);
        return ErroresDePagos.NoPagableRecogido(credito.Number, estado, sucesor.CreditoNuevoPublicId, sucesor.CreditoNuevoNumero, sucesor.Fecha, regla);
    }
}
