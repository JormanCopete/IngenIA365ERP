using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_PaymentPromises</c> (feature 013, K4, T0936; data-model §26.8; FR-119): la promesa de pago de una gestión —una por gestión,
/// <c>UK (CollectionActionId)</c>—, con la fecha, el valor y la tolerancia vigente (<c>Cobranza.ToleranciaDePromesa</c>) sellada. Su estado no
/// se guarda: lo deriva <see cref="EstadoDePromesa"/>. Hecho de sólo inserción (<see cref="IHechoInmutable"/>). (nuevo)
/// </summary>
public class PaymentPromise : AuditableEntity, IHechoInmutable
{
    public int CollectionActionId { get; init; }

    public int LoanId { get; init; }

    public DateOnly PromisedDate { get; init; }

    public decimal PromisedAmount { get; init; }

    public decimal ToleranceAmount { get; init; }

    public static PaymentPromise Prometer(int gestionId, int creditoId, DateOnly fecha, decimal valor, decimal tolerancia)
    {
        if (valor <= 0m) throw new ArgumentOutOfRangeException(nameof(valor), valor, "Lo prometido es mayor que cero.");
        if (tolerancia < 0m || tolerancia >= valor)
            throw new ArgumentOutOfRangeException(nameof(tolerancia), tolerancia, "La tolerancia va de cero hasta menos de lo prometido.");
        return new PaymentPromise
        {
            CollectionActionId = gestionId,
            LoanId = creditoId,
            PromisedDate = fecha,
            PromisedAmount = valor,
            ToleranceAmount = tolerancia,
        };
    }

    /// <summary>Lo que mira el cálculo, con el instante de la gestión y si fue anulada.</summary>
    public PromesaParaEstado ParaEstado(DateTime gestionRegistradaEnUtc, bool gestionAnulada) =>
        new(gestionRegistradaEnUtc, PromisedDate, PromisedAmount, ToleranceAmount, gestionAnulada);
}
