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
/// La cuota causada enviada a su canal (api.md §1 <c>sent</c>): en qué va, la pagaduría, el archivo, el período y el valor de la línea. 2026-11-27 (feature
/// 028, E2, T064, T066; R6): con la cuota que nombra la línea o la novedad, el convenio, lo corriente y lo vencido de la línea, la corrida de la nómina
/// propia y la frase que lo dice. (nuevo)
/// </summary>
public sealed record PayoffSentInstallmentDto(SentInstallmentState State, string? Paymaster, string? File, string? Period, decimal? LineAmount)
{
    public int? InstallmentNumber { get; init; }
    public string? Agreement { get; init; }
    public decimal? CurrentAmount { get; init; }
    public decimal? PastDueAmount { get; init; }
    public Guid? PayrollRunPublicId { get; init; }

    /// <summary>«La cuota 7 ya se envió a la pagaduría X en el archivo ADP-000123 de noviembre de 2026 y no ha respondido.»</summary>
    public string? Text { get; init; }

    /// <summary>La de una cuota enviada (nula sin nada enviado).</summary>
    public static PayoffSentInstallmentDto? De(Payroll.CuotaEnviada? e) => e is null || e.Estado == SentInstallmentState.None
        ? null
        : new PayoffSentInstallmentDto(e.Estado, e.Pagaduria, e.Archivo, e.Periodo, e.Total)
        {
            InstallmentNumber = e.Cuota, Agreement = e.Convenio, CurrentAmount = e.Corriente, PastDueAmount = e.Vencido, PayrollRunPublicId = e.Corrida,
            Text = Payroll.TextosDeCuotaEnviada.Frase(e),
        };
}

/// <summary>
/// La cuota causada del crédito a la fecha proyectada (api.md §1 <c>currentInstallment</c>): su número, vencimiento y valor pendiente, qué se hace con ella
/// y si el asesor puede elegirlo. 2026-11-27 (feature 028, E2, T066; HU4): el tratamiento es el que rige (<c>Recogida.CuotaCausada</c> con la línea del
/// crédito), <see cref="Choosable"/> con «Preguntar», <see cref="Sent"/> la cuota enviada, <see cref="Left"/> lo que la cuota debería a su canal si se deja
/// (nulo si no se puede dejar) y las explicaciones de los dos tratamientos. (nuevo)
/// </summary>
public sealed record PayoffCurrentInstallmentDto(int Number, DateOnly? DueDate, decimal Amount, InstallmentTreatment Treatment, bool Choosable,
    PayoffSentInstallmentDto? Sent)
{
    /// <summary>Lo que la cuota deja debiendo a su canal si se deja («todo menos la cuota causada»); nulo si no se puede dejar.</summary>
    public decimal? Left { get; init; }

    /// <summary>Lo que <c>Recogida.CuotaCausada</c> dice para la línea del crédito: <c>Incluir</c>, <c>Excluir</c> o <c>Preguntar</c>.</summary>
    public string? Parameter { get; init; }

    /// <summary>Qué pasa con la cuota con el tratamiento que rige.</summary>
    public string? Explanation { get; init; }

    /// <summary>Qué pasa con la cuota si se incluye (para «Preguntar»).</summary>
    public string? IncludedExplanation { get; init; }

    /// <summary>Qué pasa con la cuota si se deja a su canal (nula si no se puede dejar).</summary>
    public string? LeftExplanation { get; init; }

    /// <summary>Por qué no se puede dejar aunque la línea lo pida (vivienda, sin cuota causada a la fecha); nula si se puede o la línea dice «Incluir».</summary>
    public string? NotLeftReason { get; init; }
}

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

    /// <summary>2026-11-27 (feature 028, T051): el interés explicado renglón a renglón, con los días de la reconstrucción. (nuevo)</summary>
    public IReadOnlyList<PayoffInterestLineDto> InterestExplanation { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, T051): lo reconocido al inicio del ciclo y no causado que se libera; informativo, no cambia <see cref="ToPayOff"/>. (nuevo)</summary>
    public decimal RecognizedReleased { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066): el desglose de <see cref="ToPayOffExcludingCurrentInstallment"/> (todo menos la cuota causada); nulo si no se puede
    /// dejar la cuota. (nuevo)
    /// </summary>
    public PayoffBreakdownDto? BreakdownExcludingCurrentInstallment { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T066): con qué tratamiento de la cuota causada se marcó (nulo si no se marcó). (nuevo)</summary>
    public InstallmentTreatment? SelectedInstallmentTreatment { get; init; }
}

/// <summary>
/// 2026-11-27 (feature 028, T051; HU5): un renglón de la explicación del interés de una recogida: su clase (<c>Accrued</c>, <c>FullInstallment</c>,
/// <c>Proportional</c>, <c>Returned</c>, <c>RecognizedReleased</c>), la cuota, los días, el valor y el texto. (nuevo)
/// </summary>
public sealed record PayoffInterestLineDto(string Kind, int? Installment, int? Days, decimal Amount, string Text)
{
    public static IReadOnlyList<PayoffInterestLineDto> De(IEnumerable<RenglonDeIntereses> renglones) =>
        renglones.Select(r => new PayoffInterestLineDto(r.Clase, r.Cuota, r.Dias, r.Valor, r.Texto)).ToList();
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
