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

    /// <summary>
    /// 2026-11-16 (pedido del dueño): si al cancelar se cobra el interés completo de la cuota en curso (<c>Pagos.InteresesAlPagarAntesDelVencimiento</c>
    /// = «CuotaCompleta», ya resuelto contra el tope legal del prepago y la vivienda al aplicar el pago y sellado en su decisión). Falso —un pago
    /// anterior que no lo trae— es el corrido. (nuevo)
    /// </summary>
    public bool CuotaCompleta { get; init; }

    /// <summary>
    /// 2026-11-16 (pedido del dueño): el orden de rubros de la empresa (<c>Pagos.PrelacionDeRubros</c>) que rigió al aplicar el pago, sellado en su
    /// decisión: la reconstrucción lo reparte con él (<see cref="Prelacion.ConRubros"/>) aunque el parámetro cambie después, y los anticipos que se
    /// aplican al vencer usan el último sellado. Nulo —un pago anterior al parámetro, o de una línea con prelación propia— es la prelación de la
    /// línea, la de siempre. (nuevo)
    /// </summary>
    public OrdenDeRubros? Rubros { get; init; }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5, HU5): el pago se aplicó con <c>Causacion.Periodo = PeriodoCalendario</c>, sellado en su decisión
    /// (<see cref="SolicitudDeDistribucion.PeriodoCalendario"/>). Falso —un pago anterior que no lo trae— es el ciclo de la cuota. (nuevo)
    /// </summary>
    public bool PeriodoCalendario { get; init; }
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

    /// <summary>
    /// 2026-11-16 (decisión del dueño): si el saldo a favor aplicado como abono termina cancelando, cobra el interés completo de la cuota en curso, como
    /// cualquier cancelación (<see cref="ContenidoDePago.CuotaCompleta"/>, ya resuelto contra el tope legal del prepago y la vivienda al registrarla). Falso
    /// —una resolución anterior que no lo trae— es el corrido. (nuevo)
    /// </summary>
    public bool CuotaCompleta { get; init; }

    /// <summary>2026-11-16: el orden de rubros sellado al registrar el abono (<see cref="ContenidoDePago.Rubros"/>); nulo, la prelación de la línea. (nuevo)</summary>
    public OrdenDeRubros? Rubros { get; init; }
}
