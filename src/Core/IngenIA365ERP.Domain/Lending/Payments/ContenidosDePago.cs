using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payments;

// Feature 013, US5 (T0535): lo que la reconstrucción lee del Contenido de una operación de pago o de resolución de un saldo. La
// distribución NO se guarda como entrada: se recalcula desde la decisión (modalidad, valor, fecha valor, elección), así un pago fechado
// atrás o un reverso redistribuyen los posteriores (D4, FR-081). (nuevo)

/// <summary>
/// La decisión de un pago (<c>LoanOperation</c> <c>Payment</c> o <c>ProvisionalCollection</c>): modalidad, valor recibido y, según la
/// modalidad, la elección aplicada (la del deudor o su preferencia, D24) o la cuota específica.
/// </summary>
public sealed record ContenidoDePago(PaymentModality Modalidad, decimal Valor)
{
    public PrepaymentChoice? Eleccion { get; init; }

    public int? Cuota { get; init; }
}

/// <summary>
/// La resolución de un saldo a favor o de un anticipo (<c>CreditBalanceResolution</c>, FR-076, FR-083; data-model §7.10): de qué concepto
/// (<see cref="LoanConcept.CreditBalance"/> o <see cref="LoanConcept.Advance"/>), cuánto y si se aplica como abono extraordinario a capital
/// con la elección del deudor (sólo un saldo nacido de un abono sin preferencia). Devolverlo por el recaudador o aplicarlo a otra
/// obligación sólo lo baja aquí: el pago en la otra obligación es su propia operación.
/// </summary>
public sealed record ContenidoDeResolucion(LoanConcept Concepto, decimal Monto)
{
    public bool ComoAbonoACapital { get; init; }

    public PrepaymentChoice? Eleccion { get; init; }
}
