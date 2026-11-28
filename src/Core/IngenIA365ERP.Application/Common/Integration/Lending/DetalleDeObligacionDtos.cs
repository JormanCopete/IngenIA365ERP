namespace IngenIA365ERP.Application.Common.Integration.Lending;

// 2026-11-16 (pedido del dueño): el detalle de una obligación que la caja de Recaudo muestra antes de cobrar (IContratoDeObligaciones.DetalleAsync). Es una
// lectura nueva junto a las del contrato: no cambia ningún DTO de ContratoDeObligacionesDtos.cs ni la versión del contrato. Propiedades en inglés (C26). (nuevo)

/// <summary>Pide el detalle de una obligación a una fecha. (nuevo)</summary>
public sealed record SolicitudDeDetalle(
    Recaudador Collector,
    Guid ObligationPublicId,
    DateOnly Date);

/// <summary>
/// El detalle de una obligación a la fecha (nuevo): lo exigible, lo vencido, lo que vale cancelarla, el anticipo y el saldo a favor, la cuota regular y el próximo
/// vencimiento, sus saldos por concepto y sus cuotas con lo que cuesta anticipar cada una. <see cref="Status"/> va en español (Vigente, Castigado…);
/// <see cref="AdmittedModalities"/> son las mismas del listado (§3.5: sin la cuota específica si la línea no la permite fuera de orden, sin anticipo en un
/// castigado) y <see cref="AllowsSpecificInstallmentOutOfOrder"/> dice si la línea deja pagar una cuota con anteriores impagas. <see cref="Notices"/>, en español.
/// </summary>
public sealed record DetalleDeObligacionDto(
    Guid ObligationPublicId,
    string Number,
    string CreditLineName,
    string Status,
    int DaysPastDue,
    decimal DueToday,
    decimal PastDue,
    decimal PayoffAmount,
    decimal Advance,
    decimal CreditBalance,
    decimal? RegularInstallment,
    DateOnly? NextDueDate,
    IReadOnlyList<SaldoPorConceptoDto> Balances,
    IReadOnlyList<CuotaDeObligacionDto> Installments,
    IReadOnlyList<ModalidadDePago> AdmittedModalities,
    bool AllowsSpecificInstallmentOutOfOrder,
    IReadOnlyList<string> Notices)
{
    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): lo reconocido al inicio del ciclo y todavía no causado de la cuota del ciclo: la caja la muestra
    /// como la cuota del período y la precarga completa. (nuevo)
    /// </summary>
    public decimal RecognizedPending { get; init; }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): lo exigible sin lo causado de la cuota del período calendario en curso —la mora para las reglas de la caja—; igual a
    /// <see cref="DueToday"/> con el ciclo de la cuota. Nulo se lee como <c>DueToday</c>. (nuevo)
    /// </summary>
    public decimal? DueWithoutPeriodInProgress { get; init; }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): la fracción del interés de una cuota anticipada que la cooperativa descuenta en caja para la línea
    /// (<c>Pagos.DescuentoPorPagoAnticipado</c>); cero sin descuento. La aplica Recaudo: Cartera recibe la cuota completa. (nuevo)
    /// </summary>
    public decimal AdvanceDiscountFraction { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, T065; R6, FR-023): la cuota del período que ya va en un archivo de libranza o en la nómina propia (como en el listado); nula si
    /// no hay nada enviado. (nuevo)
    /// </summary>
    public CuotaEnviadaDto? SentInstallment { get; init; }
}

/// <summary>Un saldo de la obligación por concepto: el concepto (<c>ConceptoDeObligacion</c> por nombre), su nombre en español y el valor. (nuevo)</summary>
public sealed record SaldoPorConceptoDto(string Concept, string Name, decimal Amount);

/// <summary>
/// Una cuota de la obligación a la fecha (nuevo). <see cref="Status"/>: <c>PastDue</c> (vencida con saldo), <c>DueToday</c> (vence hoy con saldo),
/// <c>Upcoming</c> (por vencer), <c>Covered</c> (por vencer y cubierta por su anticipo) o <c>Paid</c>. Capital, interés y seguro son los del plan;
/// <see cref="Other"/>, los otros cargos que el libro lleva en la cuota. <see cref="Owed"/> es lo que la cuota todavía debe —lo exigible de una vencida, lo que
/// valdrá al vencer menos su anticipo una por vencer—; <see cref="AdvanceApplied"/>, lo anticipado a la cuota; y <see cref="ValueToAnticipate"/>, lo que cuesta
/// anticiparla hoy con la cuota específica (sólo en <c>Upcoming</c>; cero en las demás).
/// </summary>
public sealed record CuotaDeObligacionDto(
    int Number,
    DateOnly DueDate,
    string Status,
    decimal Principal,
    decimal Interest,
    decimal Insurance,
    decimal Other,
    decimal Total,
    decimal Owed,
    decimal AdvanceApplied,
    decimal ValueToAnticipate)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): la administración de la cuota del plan, aparte del seguro y dentro del total. (nuevo)</summary>
    public decimal Administration { get; init; }

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): lo reconocido al inicio del ciclo y todavía no causado de la cuota (interés, seguro y
    /// administración): con lo causado, la cuota del período completa. Cero si no está reconocida. (nuevo)
    /// </summary>
    public decimal Recognized { get; init; }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): lo que de una cuota por vencer ya es exigible —el interés, el seguro y los cargos causados de la cuota del período
    /// calendario en curso, que entran en <c>DueToday</c>—. Si el mismo recibo paga antes lo exigible, anticipar esta cuota cuesta
    /// <see cref="ValueToAnticipate"/> menos esto. Cero en las demás. (nuevo)
    /// </summary>
    public decimal DueInPeriod { get; init; }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): la cuota es la del período en curso (la próxima que vence o, por período calendario, la del tramo en curso): su período
    /// ya empezó y no lleva descuento por pago anticipado. (nuevo)
    /// </summary>
    public bool InCurrentPeriod { get; init; }
}
