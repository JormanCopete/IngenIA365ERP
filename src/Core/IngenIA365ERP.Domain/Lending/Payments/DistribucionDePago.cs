using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
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

    /// <summary>
    /// La cuota de <see cref="PaymentModality.SpecificInstallment"/>. 2026-11-27 (feature 028, R4): en <see cref="PaymentModality.PayoffExceptCurrentInstallment"/>,
    /// la cuota causada que se deja, opcional: sin ella la decide el aplicador (<see cref="AplicadorDePagos.CuotaCausada"/>); con ella tiene que ser esa
    /// misma —la que sella la decisión del pago—, o la cancelación es total.
    /// </summary>
    public int? Cuota { get; init; }

    /// <summary>
    /// 2026-11-16 (pedido del dueño): al cancelar (<see cref="PaymentModality.FullPayoff"/> o un abono que alcanza el capital por vencer), cobrar el
    /// interés completo de la cuota en curso y no sólo el causado hasta la fecha valor (<c>Pagos.InteresesAlPagarAntesDelVencimiento</c> =
    /// «CuotaCompleta», ya resuelto contra el tope legal del prepago y la vivienda por quien pide). Falso: el corrido, lo de siempre. (nuevo)
    /// </summary>
    public bool CuotaCompleta { get; init; }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5, HU5; D3, FR-017): la línea del crédito causa por <b>período calendario</b> (<c>Causacion.Periodo =
    /// PeriodoCalendario</c>, leído a la fecha valor y sellado en la decisión del pago). Un abono cubre primero lo causado de la cuota del tramo en curso
    /// (como exigible); una cancelación no cobra lo causado de una cuota cuyo tramo no ha empezado (<see cref="DistribucionDePago.InteresNoCobrado"/>).
    /// Falso —un pago que no lo trae— es el ciclo de la cuota, lo de siempre. (nuevo)
    /// </summary>
    public bool PeriodoCalendario { get; init; }
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

    /// <summary>Códigos de aviso (<c>Lending.Payment.AdvanceDoesNotReduceInterest</c>, <c>.PreferenceAssumed</c>, <c>.TreatedAsPayoff</c>,
    /// <c>.FullInstallmentInterestCharged</c>).</summary>
    public IReadOnlyList<string> Avisos { get; init; } = [];

    /// <summary>
    /// 2026-11-16 (pedido del dueño): el interés de la cuota en curso que la causación todavía no había causado y que la cancelación cobra con
    /// <see cref="SolicitudDeDistribucion.CuotaCompleta"/>, por cuota y concepto (la corriente o su suspenso). La reconstrucción lo <b>causa</b> en la
    /// fecha valor (filas <see cref="LoanTransactionKind.UnaccruedInstallmentInterest"/>) antes de asentar las líneas, que ya lo incluyen. Vacío con el
    /// corrido o si los fondos no alcanzan lo demás. (nuevo)
    /// </summary>
    public IReadOnlyList<LineaDeDistribucion> InteresPorCausar { get; init; } = [];

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5, HU5; D3, FR-017): con <see cref="SolicitudDeDistribucion.PeriodoCalendario"/>, el interés corriente causado día a
    /// día de las cuotas cuyo tramo del calendario no ha empezado a la fecha valor de una cancelación: no se cobra. La reconstrucción lo <b>devuelve</b> en
    /// la fecha valor (filas <see cref="LoanTransactionKind.RecognitionRelease"/> negativas en la corriente de esa cuota, el mecanismo de devolución de la
    /// 024) y el crédito queda en cero. Vacío en todo lo demás. (nuevo)
    /// </summary>
    public IReadOnlyList<LineaDeDistribucion> InteresNoCobrado { get; init; } = [];

    /// <summary>
    /// 2026-11-27 (feature 028, R4, T059; D13-A): con <see cref="PaymentModality.PayoffExceptCurrentInstallment"/> y los fondos que cubren todo lo demás, la
    /// cuota causada que el pago dejó en el crédito <b>tal como se envió</b> —capital, interés del período completo, seguro y administración— y
    /// <b>congelada</b>: desde el día siguiente a la fecha valor no causa más interés corriente mientras no esté vencida (la reconstrucción la respeta,
    /// <see cref="ContextoDeReconstruccion.Congelar"/>). Nula en todo lo demás: también si la cancelación terminó siendo total o los fondos no alcanzaron.
    /// (nuevo)
    /// </summary>
    public int? CuotaDejada { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, R4, T059; D13-A): el interés del período de <see cref="CuotaDejada"/> que la causación todavía no había causado —el mismo cálculo
    /// que la cuota completa (<see cref="EstadoDelCredito.InteresPorCausarDeLaCuotaEnCurso"/>), sobre el saldo con que se proyectó la cuota—, por cuota y
    /// concepto. La reconstrucción lo <b>causa</b> en la fecha valor (filas <see cref="LoanTransactionKind.UnaccruedInstallmentInterest"/>) y queda
    /// debiéndose en esa cuota: el pago no lo cubre. Vacío sin cuota dejada o si el interés del período ya estaba causado. (nuevo)
    /// </summary>
    public IReadOnlyList<LineaDeDistribucion> InteresDeLaCuotaDejada { get; init; } = [];

    /// <summary>2026-11-16: el total de <see cref="InteresPorCausar"/>. (nuevo)</summary>
    public decimal InteresDeLaCuotaEnCurso => InteresPorCausar.Sum(l => l.Monto);

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

/// <summary>
/// 2026-11-27 (feature 028, R4, T059): lo que cuesta la cancelación de todo menos la cuota causada sin aplicarla
/// (<see cref="AplicadorDePagos.ValorParaCancelarSalvoCuota"/>): la cuota que se deja (nula si la cancelación es total), lo que cuesta cancelar lo demás con
/// el anticipo del mismo crédito ya descontado, lo que queda en la cuota para que su canal la pague al peso, el interés del período que se causa en ella y los
/// avisos. (nuevo)
/// </summary>
public sealed record ValorSalvoCuota(
    int? Cuota,
    decimal ParaCancelar,
    decimal QuedaEnLaCuota,
    IReadOnlyList<LineaDeDistribucion> InteresFijado,
    IReadOnlyList<string> Avisos);
