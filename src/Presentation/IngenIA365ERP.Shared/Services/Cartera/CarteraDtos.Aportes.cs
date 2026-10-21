namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US3 (T094): los espejos de contracts/api.md §7 (aportes) y §18.3 (grupos de archivo y equivalencias de pagaduría). Los enums de
// salida viajan como número (ContributionRequestKind, Status, Cause) y la pantalla los traduce con TextosDeAportes; los de entrada, por nombre. (nuevo)

// ------------------------------------------------------------------------------------------------------------ §7 aportes --

/// <summary>El saldo de aportes por clase.</summary>
public sealed record AportesPorClaseDto(decimal Minimum, decimal Ordinary, decimal Extraordinary, decimal Revalued);

/// <summary>Una cuota vencida con saldo.</summary>
public sealed record CuotaEnMoraDeAportesDto(string AccountNumber, string Period, DateOnly DueDate, decimal Pending);

/// <summary>El resumen de aportes de una persona: el mismo saldo de la consulta de aportes de la Cartera.</summary>
public sealed record ResumenDeAportesDto(
    PersonaDeCuentaDto Person,
    DateOnly Date,
    bool Available,
    AportesPorClaseDto ByClass,
    decimal Total,
    decimal MinimumNonReducible,
    decimal PermanentSavings,
    IReadOnlyList<CuotaEnMoraDeAportesDto> InstallmentsInArrears,
    IReadOnlyList<Guid> AccountPublicIds);

/// <summary>Una cuota exigible de aportes, ahorro permanente o contractual.</summary>
public sealed record CuotaDeCuentaDto(
    Guid PublicId,
    Guid AccountPublicId,
    string AccountNumber,
    string Nature,
    string Period,
    DateOnly DueDate,
    decimal Amount,
    decimal Paid,
    decimal Pending,
    string Status,
    decimal? ContributionShare,
    decimal? PermanentSavingsShare,
    string? NotDeductedReason);

/// <summary>Una solicitud de devolución sin retiro (Kind 1) o de cesión entre asociados (Kind 2).</summary>
public sealed record SolicitudDeAportesDto(
    Guid PublicId,
    int Kind,
    int Status,
    Guid AccountPublicId,
    string AccountNumber,
    PersonaDeCuentaDto Holder,
    PersonaDeCuentaDto? Target,
    decimal Amount,
    int? Cause,
    Guid? SessionItemPublicId,
    string Reason,
    DateTime RequestedAt,
    DateTime? DecidedAt,
    string? DecisionReason,
    string? OperationNumber,
    string? PayoutOrderNumber);

/// <summary>El cuerpo de pedir una devolución: la cuenta de aportes, el valor, la causa (ParticipationExcess u Other) y el punto del acta.</summary>
public sealed record DevolucionDeAportesRequest(Guid AccountPublicId, decimal Amount, string Cause, Guid? SessionItemPublicId, string Reason);

/// <summary>El cuerpo de pedir una cesión: la cuenta que cede, la persona que recibe, el valor y el punto del acta.</summary>
public sealed record CesionDeAportesRequest(Guid FromAccountPublicId, Guid ToPersonPublicId, decimal Amount, Guid? SessionItemPublicId, string Reason);

/// <summary>El cuerpo de aprobar una devolución: en K7, por orden de giro (DisbursementOrder) a Tesorería.</summary>
public sealed record AprobarDevolucionDeAportesRequest(string Form, string Reason);

// --------------------------------------------------------------------------------------------- §18.3 pagadurías --

/// <summary>Un formato por referencia.</summary>
public sealed record FormatoReferidoDto(Guid PublicId, string Code);

/// <summary>El calendario propio de un grupo; nulo = el de la versión.</summary>
public sealed record CalendarioDeGrupoDto(int? CutoffDay, int? SendLeadDays, int? PayDay);

/// <summary>Un grupo de archivo de la versión que rige.</summary>
public sealed record GrupoDeArchivoDto(Guid PublicId, string Code, string Name, FormatoReferidoDto OutboundFormat, FormatoReferidoDto ResultFormat,
    CalendarioDeGrupoDto Calendar, int Order, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Un ítem de una equivalencia: línea (o «*»), naturaleza, concepto (o «Installment») y prioridad.</summary>
public sealed record MiembroDeEquivalenciaDto(string LineCode, string Nature, string Concept, int Priority);

/// <summary>Una equivalencia de concepto de la versión que rige.</summary>
public sealed record EquivalenciaDeConceptoDto(Guid PublicId, string PaymasterConceptCode, string Description, string? FileGroupCode,
    IReadOnlyList<MiembroDeEquivalenciaDto> Members, string Split, int Order, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Un grupo de archivo por fijar.</summary>
public sealed record GrupoDeArchivoRequest(string Code, string Name, Guid OutboundFormatPublicId, Guid ResultFormatPublicId, int? CutoffDay = null,
    int? PayDay = null, int? SendLeadDays = null);

/// <summary>El cuerpo de fijar los grupos de archivo desde una fecha.</summary>
public sealed record GruposDeArchivoRequest(DateOnly ValidFrom, IReadOnlyList<GrupoDeArchivoRequest> Groups, string Reason);

/// <summary>Un ítem por fijar (la naturaleza por nombre: Placement, Deposit, Contribution).</summary>
public sealed record MiembroDeEquivalenciaRequest(string Nature, string LineCode, string Concept, int Priority = 1);

/// <summary>Una equivalencia por fijar (el reparto por nombre: Priority o Proportional).</summary>
public sealed record EquivalenciaDeConceptoRequest(string PaymasterConceptCode, string Description, string Split, IReadOnlyList<MiembroDeEquivalenciaRequest> Members,
    string? FileGroupCode = null, int Order = 1);

/// <summary>El cuerpo de fijar las equivalencias desde una fecha.</summary>
public sealed record EquivalenciasDeConceptoRequest(DateOnly ValidFrom, IReadOnlyList<EquivalenciaDeConceptoRequest> Equivalences, string Reason);

/// <summary>Los textos de aportes para la pantalla. (nuevo)</summary>
public static class TextosDeAportes
{
    public const int Devolucion = 1;
    public const int Cesion = 2;

    public static string EstadoDeSolicitud(int estado) => estado switch
    {
        1 => "Por aprobar",
        2 => "Aprobada",
        3 => "Negada",
        _ => estado.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string Causa(int? causa) => causa switch
    {
        1 => "Exceso de participación",
        99 => "Otra",
        null => "—",
        _ => causa.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string EstadoDeCuota(string estado) => estado switch
    {
        "Pending" => "Pendiente",
        "PartiallyPaid" => "Abonada",
        "Paid" => "Pagada",
        "NotDeducted" => "No descontada",
        "Cancelled" => "Anulada",
        _ => estado,
    };

    public static string Naturaleza(string naturaleza) => naturaleza switch
    {
        "Contribution" => "Aportes",
        "Deposit" => "Ahorro",
        "Placement" => "Crédito",
        _ => naturaleza,
    };

    public static string Reparto(string reparto) => reparto == "Proportional" ? "A prorrata" : "Por prioridad";
}
