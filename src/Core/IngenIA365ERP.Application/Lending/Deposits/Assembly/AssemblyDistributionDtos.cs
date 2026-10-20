using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Assembly;

// Feature 014, US12 (T203, T205; contracts/api.md §12; FR-045 a FR-047; research §22): lo que viaja de la asamblea. (nuevo)

/// <summary>La línea de un asociado en una simulación o en lo aplicado: base, calculado, aplicado y a dónde va.</summary>
public sealed record AssemblyDistributionLineDto(
    MemberAccountPersonDto Person,
    Guid ContributionAccountPublicId,
    string ContributionAccountNumber,
    decimal BaseAmount,
    decimal CalculatedAmount,
    decimal AppliedAmount,
    bool LimitedByMinimum,
    Guid? DestinationAccountPublicId,
    string? DestinationAccountNumber,
    string? OperationNumber,
    string? PayoutOrderNumber,
    bool WithdrawnDuringYear = false,
    decimal WithheldAmount = 0m);

/// <summary>El límite legal de la revalorización (el IPC del ejercicio): si cortó lo decidido, el valor y su fuente.</summary>
public sealed record LimiteLegalDto(bool Applied, decimal? Ipc, string? Source, DateOnly? ReferenceDate);

/// <summary>El prorrateo al fondo (US12-1): si se prorrateó y con qué factor.</summary>
public sealed record ProrrateoDto(bool Applied, decimal? Factor);

/// <summary>Los totales: base, calculado, aplicado, lo que queda en el fondo y las líneas.</summary>
public sealed record TotalesDeAsambleaDto(decimal Base, decimal Calculated, decimal Applied, decimal FundRemainder, int Lines, decimal Withheld = 0m);

/// <summary>
/// La simulación (<c>POST /{id}/simulate</c>, sin clave): las líneas, el límite del IPC, el prorrateo, los totales, la tasa aplicada, los avisos y la huella
/// que la aplicación compara (<c>expectedPreviewSha256</c>).
/// </summary>
public sealed record AssemblyDistributionSimulationDto(
    Guid PublicId,
    DateOnly AsOf,
    decimal DecidedRate,
    decimal AppliedRate,
    IReadOnlyList<AssemblyDistributionLineDto> Lines,
    LimiteLegalDto LimitedByIpc,
    ProrrateoDto ProratedToFund,
    TotalesDeAsambleaDto Totals,
    IReadOnlyList<string> Explanation,
    IReadOnlyList<string> Warnings,
    string Sha256);

/// <summary>Una decisión de asamblea (lista y detalle; el detalle trae las líneas aplicadas).</summary>
public sealed record AssemblyDistributionDto(
    Guid PublicId,
    string Number,
    AssemblyDistributionKind Kind,
    int Year,
    AssemblyDistributionStatus Status,
    RevaluationBase Base,
    decimal? Percent,
    decimal? Value,
    decimal Fund,
    SurplusDestination? Destination,
    string? DestinationLineCode,
    decimal RoundingUnit,
    Guid? SessionItemPublicId,
    string? SessionNumber,
    Guid? CorrectsPublicId,
    string? CorrectsNumber,
    decimal? LegalLimit,
    string? LegalLimitSource,
    decimal CalculatedTotal,
    decimal? ProrationFactor,
    decimal AppliedTotal,
    IReadOnlyList<Guid> SupportAttachmentPublicIds,
    string Reason,
    DateTime RegisteredAt,
    DateTime? AppliedAt,
    DateOnly? AppliedOn,
    DateTime? ReversedAt,
    string? ReversalReason,
    DateTime? VoidedAt,
    IReadOnlyList<AssemblyDistributionLineDto> Lines)
{
    /// <summary>El porcentaje o el valor, como se lee en la lista (api.md §12 <c>percentOrValue</c>).</summary>
    public string PercentOrValue => Percent is { } p
        ? string.Create(CultureInfo.InvariantCulture, $"{p * 100m:0.######} %")
        : string.Create(CultureInfo.InvariantCulture, $"{Value:N2}");
}

/// <summary>Los errores de la asamblea (api.md §12; research §22). (nuevo)</summary>
public static class ErroresDeAsamblea
{
    public const string CodigoInexistente = "Lending.AssemblyDistribution.NotFound";
    public const string CodigoActaDuplicada = "Lending.AssemblyDistribution.DuplicateSession";
    public const string CodigoYaAplicada = "Lending.AssemblyDistribution.AlreadyApplied";
    public const string CodigoNoAplicada = "Lending.AssemblyDistribution.NotApplied";
    public const string CodigoNoEsBorrador = "Lending.AssemblyDistribution.NotDraft";
    public const string CodigoInvalida = "Lending.AssemblyDistribution.Invalid";
    public const string CodigoCorreccionInvalida = "Lending.AssemblyDistribution.CorrectionInvalid";
    public const string CodigoSinSoporte = "Lending.AssemblyDistribution.SupportRequired";
    public const string CodigoSinAsociados = "Lending.AssemblyDistribution.NothingToApply";
    public const string CodigoGirosEnCurso = "Lending.AssemblyDistribution.PayoutsInProgress";
    public const string CodigoPagoNoReversable = "Lending.AssemblyDistribution.PaymentNotReversible";
    public const string CodigoSinActivar = "Lending.AssemblyDistribution.ContributionsNotActivated";
    public const string CodigoVistaPreviaCambio = "Lending.Savings.PreviewChanged";

    public static Error Inexistente() => new(CodigoInexistente, "La decisión de asamblea no existe.");

    public static Error ActaDuplicada(string numero) => new ErrorConDatos(CodigoActaDuplicada,
        $"El acta ya respalda la decisión {numero}, aplicada: para corregirla, revérsela entera y registre otra que la corrija.", new { number = numero });

    public static Error YaAplicada(string numero) => new ErrorConDatos(CodigoYaAplicada,
        $"La decisión {numero} ya está aplicada: no se aplica dos veces. Para corregirla, revérsela entera y registre otra.", new { number = numero });

    public static Error NoAplicada(AssemblyDistributionStatus estado) => new ErrorConDatos(CodigoNoAplicada,
        $"La decisión está «{estado}»: sólo se reversa una aplicada.", new { status = estado.ToString() });

    public static Error NoEsBorrador(AssemblyDistributionStatus estado) => new ErrorConDatos(CodigoNoEsBorrador,
        $"La decisión está «{estado}»: sólo se simula, se descarta o se aplica una en borrador.", new { status = estado.ToString() });

    public static Error Invalida(string campo, string mensaje) => new ErrorConDatos(CodigoInvalida, mensaje, new { field = campo });

    public static Error CorreccionInvalida(string mensaje) => new ErrorConDatos(CodigoCorreccionInvalida, mensaje, new { field = "correctsPublicId" });

    public static Error SinSoporte() => new ErrorConDatos(CodigoSinSoporte,
        "Adjunte el acta firmada de la asamblea (y, si lo hay, el proyecto de distribución aprobado por el consejo) antes de aplicar.",
        new { field = "supportAttachmentPublicIds" });

    public static Error SinAsociados() => new(CodigoSinAsociados, "Ningún asociado recibe nada con esta decisión: no hay nada que aplicar.");

    public static Error GirosEnCurso(int vivos) => new ErrorConDatos(CodigoGirosEnCurso,
        $"{vivos} orden(es) de giro de la decisión siguen vivas o ya se giraron: no se reversa entera mientras Tesorería no las rechace o anule.",
        new { liveOrders = vivos });

    public static Error PagoNoReversable() => new(CodigoPagoNoReversable,
        "Los excedentes pagados en dinero ya salieron a Tesorería: la decisión no se reversa entera; corrija con otra decisión.");

    public static Error SinActivar() => new(CodigoSinActivar, "Los aportes de la Cartera no están activados: la asamblea no se aplica sin ellos.");

    public static Error VistaPreviaCambio(AssemblyDistributionSimulationDto vista) => new ErrorConDatos(CodigoVistaPreviaCambio,
        "La simulación cambió desde que la vio (otro movimiento, otro IPC u otra regla): revísela y aplique otra vez.", new { preview = vista });
}
