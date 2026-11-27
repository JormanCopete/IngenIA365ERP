using IngenIA365ERP.Application.Lending.PersonStatement;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;

namespace IngenIA365ERP.Application.Lending.Payoffs;

// 2026-11-27 (pedido del dueño; feature 028, HU1, T015; api.md §1): los DTO de los productos para recoger. Sólo PublicId hacia afuera
// (Principio VI); los enums salen como número. (nuevo)

/// <summary>
/// El crédito nuevo que se simula (api.md §1 <c>newLoan</c>): su monto, para el neto a girar, y su cuota, para compararla con las que se liberan. (nuevo)
/// </summary>
public sealed record PayoffNewLoanInput(decimal Amount, decimal? MonthlyInstallment = null);

/// <summary>
/// Lo que cuesta recoger un producto, por concepto (api.md §1 <c>breakdown</c>; <see cref="DesgloseDeRecogida"/>): lo devuelto por proporcionales, el
/// anticipo y el saldo a favor se descuentan. (nuevo)
/// </summary>
public sealed record PayoffBreakdownDto(
    decimal Principal,
    decimal CurrentInterest,
    decimal InterestReturned,
    decimal InterestCharged,
    decimal DefaultInterest,
    decimal Insurance,
    decimal Administration,
    decimal OtherCharges,
    decimal Advance,
    decimal CreditBalance)
{
    public static PayoffBreakdownDto De(DesgloseDeRecogida d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return new(d.Capital, d.InteresCorriente, d.InteresDevuelto, d.InteresCobradoPorProporcionales, d.Mora, d.Seguro, d.Administracion, d.OtrosCargos,
            d.Anticipo, d.SaldoAFavor);
    }

    public static PayoffBreakdownDto Vacio { get; } = De(DesgloseDeRecogida.Vacio);
}

/// <summary>
/// La cuota causada enviada a su canal (api.md §1 <c>sent</c>): en qué va, la pagaduría, el archivo, el período y el valor de la línea. Llega con la
/// entrega E2 (R6); en E1 va nula. (nuevo)
/// </summary>
public sealed record PayoffSentInstallmentDto(SentInstallmentState State, string? Paymaster, string? File, string? Period, decimal? LineAmount);

/// <summary>
/// La cuota causada del crédito a la fecha proyectada (api.md §1 <c>currentInstallment</c>): su número, vencimiento y valor pendiente, qué se hace con ella
/// y si el asesor puede elegirlo. En E1 siempre se incluye en la recogida y no se elige (<c>Recogida.CuotaCausada</c> llega con E2). (nuevo)
/// </summary>
public sealed record PayoffCurrentInstallmentDto(int Number, DateOnly? DueDate, decimal Amount, InstallmentTreatment Treatment, bool Choosable,
    PayoffSentInstallmentDto? Sent);

/// <summary>
/// Un producto del deudor que el crédito nuevo podría recoger (api.md §1): en E1 sólo créditos (<see cref="PayoffProductKind.Loan"/>); las cuentas
/// llegan con E3 y las facturas con E4. Trae lo que cuesta recogerlo a D con lo conocido hasta R, su desglose, el modo de intereses, la cuota causada, la
/// huella de la liquidación, si se puede recoger (y por qué no) y, si la consulta trajo una selección, lo que se recoge de él. (nuevo)
/// </summary>
public sealed record PayoffCandidateDto(
    PayoffProductKind Kind,
    Guid ProductPublicId,
    string Number,
    string Line,
    LoanOrigin? Origin,
    RepaymentChannel? Channel,
    int DaysPastDue,
    decimal? Installment,
    bool Eligible,
    string? BlockReason,
    IReadOnlyList<PayoffMode> AdmittedModes,
    decimal ToPayOff,
    PayoffBreakdownDto Breakdown,
    string? InterestMode,
    string? InterestModeReason,
    PayoffCurrentInstallmentDto? CurrentInstallment,
    decimal? ToPayOffExcludingCurrentInstallment,
    string? InOtherApplication,
    string Fingerprint,
    IReadOnlyList<string> Warnings)
{
    /// <summary>El estado del crédito (vigente, pendiente de giro, castigado).</summary>
    public LoanStatus? Status { get; init; }

    /// <summary>Por qué no se puede recoger, en palabras; nulo si se puede.</summary>
    public string? BlockMessage { get; init; }

    /// <summary>El modo de intereses explicado (por qué se cobra el corrido o la cuota completa); nulo si no hay nada que explicar.</summary>
    public string? InterestModeExplanation { get; init; }

    /// <summary>Si la consulta lo trae marcado y se cuenta en el resumen.</summary>
    public bool Selected { get; init; }

    /// <summary>Cómo se marcó (total o parcial); nulo si no se marcó.</summary>
    public PayoffMode? SelectedMode { get; init; }

    /// <summary>Lo que se recoge de él según lo marcado: todo en un total, el valor pedido en un parcial (topado en <see cref="ToPayOff"/>).</summary>
    public decimal? SelectedAmount { get; init; }
}

/// <summary>Lo que queda de la capacidad de pago después de recoger (api.md §1 <c>capacityAfter</c>). Lo calcula la radicación (T025); aquí va nulo. (nuevo)</summary>
public sealed record PayoffCapacityDto(decimal Available, bool Declared);

/// <summary>
/// El resumen de lo marcado (api.md §1 <c>summary</c>): el total que se recoge, el neto a girar si se dio el crédito nuevo, la suma de las cuotas que se
/// liberan (la próxima cuota de cada crédito recogido total) y los avisos (lo marcado que no se cuenta, el neto en cero o negativo). (nuevo)
/// </summary>
public sealed record PayoffSummaryDto(
    decimal SelectedTotal,
    decimal? NetToDisburse,
    decimal ReleasedInstallments,
    PayoffCapacityDto? CapacityAfter,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Cuántos productos marcados se cuentan.</summary>
    public int SelectedCount { get; init; }

    /// <summary>El monto del crédito nuevo, si se dio.</summary>
    public decimal? NewLoanAmount { get; init; }

    /// <summary>La cuota del crédito nuevo, si se dio: para compararla con <see cref="ReleasedInstallments"/>.</summary>
    public decimal? NewInstallment { get; init; }

    /// <summary>El neto a girar queda en cero o negativo: así no se puede radicar (FR, HU1-5).</summary>
    public bool NetNotPositive => NetToDisburse is <= 0m;
}

/// <summary>
/// <c>PayoffCandidatesDto</c> (api.md §1): la fecha proyectada de desembolso (D), hasta cuándo se tomó la información (R), los productos del deudor y el
/// resumen de lo marcado. Lleva la persona y, para imprimir, el membrete de la cooperativa. (nuevo)
/// </summary>
public sealed record PayoffCandidatesDto(DateOnly Date, DateTime RecordedAsOf, IReadOnlyList<PayoffCandidateDto> Products, PayoffSummaryDto Summary)
{
    /// <summary>El deudor: nombre, documento y contacto (para la pantalla y el PDF).</summary>
    public PersonaDelEstadoDeCuentaDto? Person { get; init; }

    /// <summary>La línea del crédito nuevo («código · nombre»), si se dio.</summary>
    public string? NewLoanLine { get; init; }

    /// <summary>El membrete de la cooperativa; sólo cuando se pide para imprimir.</summary>
    public MembreteDto? Letterhead { get; init; }
}
