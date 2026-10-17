using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Lending.Housing;

/// <summary>
/// Lo que un estado en pesos de un crédito en UVR lleva de su valoración (feature 013, K6b, T1054; FR-024): la UVR del día con que se valoró (y si
/// es una proyección, sólo para mostrar), el estado tal como lo calculó el motor en UVR (saldos, plan y pagos en UVR), la serie y el redondeo de la
/// línea en pesos. La simulación y la aplicación de un pago sobre el estado en pesos la usan para distribuir en UVR (<see cref="ConversionUvr.Distribuir"/>).
/// (nuevo)
/// </summary>
public sealed record ValoracionEnUvr(UvrDelDia UvrDelDia, EstadoDelCredito Estado, SerieUvr Serie, RedondeoDeCartera RedondeoDePesos)
{
    /// <summary>La UVR del día de la valoración.</summary>
    public decimal Valor => UvrDelDia.Valor;

    /// <summary>¿Se valoró con una UVR proyectada (la fecha no tiene UVR publicada)? Sólo para mostrar: nunca se causa ni se contabiliza.</summary>
    public bool Proyectada => UvrDelDia.Proyectada;

    /// <summary>Los saldos en UVR por cuota y concepto.</summary>
    public IReadOnlyList<SaldoDeCuota> SaldosEnUvr => Estado.Saldos;

    /// <summary>El capital en UVR.</summary>
    public decimal CapitalEnUvr => Estado.Saldo(Enums.Lending.LoanConcept.Principal);
}
