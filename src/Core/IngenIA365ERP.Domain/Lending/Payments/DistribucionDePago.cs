using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Payments;

// Feature 013, US5 (T0534): lo que recibe y devuelve AplicadorDePagos. No son tablas: el comprobante guardado es la operación Payment con
// sus filas PaymentAllocation, AdvanceReceived y CreditBalanceRecognized (data-model §7.8). (nuevo)

/// <summary>
/// Lo que se le pide al aplicador: modalidad (FR-071), valor recibido, fecha valor y, según la modalidad, la elección del deudor ante un
/// abono extraordinario (FR-075) o el número de la cuota específica.
/// </summary>
public sealed record SolicitudDeDistribucion(PaymentModality Modalidad, decimal Valor, DateOnly FechaValor)
{
    /// <summary>La elección del deudor (interactivo) o su preferencia vigente (sin interacción); nula: el abono queda como saldo a favor (D24).</summary>
    public PrepaymentChoice? Eleccion { get; init; }

    /// <summary>La cuota de <see cref="PaymentModality.SpecificInstallment"/>.</summary>
    public int? Cuota { get; init; }
}

/// <summary>Lo que un pago cubre de un concepto en una cuota (sin cuota: el capital de un abono extraordinario, que regenera el plan).</summary>
public sealed record LineaDeDistribucion(int? Cuota, LoanConcept Concepto, decimal Monto);

/// <summary>
/// La distribución de un pago (FR-071–FR-077; T31, M10): lo que cubre por cuota y concepto en el orden de la prelación, el anticipo que
/// recibe (y de qué cuota, D8), el anticipo del mismo crédito que usa una cancelación (D7), el saldo a favor de un abono sin elección
/// (D24), el excedente que vuelve al recaudador y nunca entra a Cartera (D5), el plan regenerado de un abono extraordinario y los avisos.
/// Se cumple <c>Valor = Σ Lineas + Anticipo + SaldoAFavor + Excedente − Σ AnticipoUsado</c>. (nuevo)
/// </summary>
public sealed record DistribucionDePago
{
    public required PaymentModality Modalidad { get; init; }

    public required decimal Valor { get; init; }

    public required DateOnly FechaValor { get; init; }

    /// <summary>La elección con que se distribuyó (la de la solicitud).</summary>
    public PrepaymentChoice? Eleccion { get; init; }

    public int? Cuota { get; init; }

    /// <summary>Lo cubierto por cuota y concepto, en el orden de la prelación.</summary>
    public IReadOnlyList<LineaDeDistribucion> Lineas { get; init; } = [];

    /// <summary>El anticipo del mismo crédito que usó una cancelación (concepto <see cref="LoanConcept.Advance"/>, por su cuota; D7).</summary>
    public IReadOnlyList<LineaDeDistribucion> AnticipoUsado { get; init; } = [];

    /// <summary>Lo que queda como anticipo (FR-074, D8).</summary>
    public decimal Anticipo { get; init; }

    /// <summary>La cuota del anticipo: la del ciclo pagada antes de vencer o la específica (D8); nula, un anticipo general.</summary>
    public int? CuotaDelAnticipo { get; init; }

    /// <summary>El abono extraordinario sin elección ni preferencia (D24).</summary>
    public decimal SaldoAFavor { get; init; }

    /// <summary>Lo que supera lo exigible de la modalidad: vuelve al recaudador (D5, FR-076).</summary>
    public decimal Excedente { get; init; }

    /// <summary>El plan regenerado por un abono extraordinario con elección (FR-075); nulo si el plan no cambia.</summary>
    public PlanDePagos? PlanNuevo { get; init; }

    /// <summary>Después del pago no queda nada cobrable: el crédito queda cancelado.</summary>
    public bool Cancela { get; init; }

    /// <summary>Códigos de aviso (<c>Lending.Payment.AdvanceDoesNotReduceInterest</c>, <c>.PreferenceAssumed</c>, <c>.TreatedAsPayoff</c>).</summary>
    public IReadOnlyList<string> Avisos { get; init; } = [];

    /// <summary>Lo recibido menos el excedente.</summary>
    public decimal Aplicado => Valor - Excedente;

    /// <summary>El abono extraordinario a capital (la línea de capital sin cuota).</summary>
    public decimal AbonoACapital => Lineas.Where(l => l.Concepto == LoanConcept.Principal && l.Cuota is null).Sum(l => l.Monto);

    public bool RequiereRegenerarElPlan => PlanNuevo is not null;
}

/// <summary>
/// El anticipo aplicado a una cuota en su vencimiento (FR-074, D6; data-model §7.9): lo que cubre de cada concepto, de qué anticipo salió
/// (primero el de la cuota, D8, luego el general) y lo que sobró del anticipo de la cuota una vez cubierta, que pasa al anticipo general.
/// No es una operación: es derivada (M10) y la registra el paso (5) del ciclo diario. (nuevo)
/// </summary>
public sealed record AplicacionDeAnticipo(
    DateOnly Fecha,
    int Cuota,
    IReadOnlyList<LineaDeDistribucion> Lineas,
    IReadOnlyList<LineaDeDistribucion> Usado,
    decimal Liberado)
{
    /// <summary>La cuota quedó cubierta del todo.</summary>
    public bool Cubierta { get; init; }
}
