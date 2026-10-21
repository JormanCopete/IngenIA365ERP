namespace IngenIA365ERP.Shared.Services.Compliance;

// Espejos de cumplimiento (feature 013, fase 2, T0094; contracts/api.md §22.1 y §22.3): las cargas de listas restrictivas y sus entradas, y
// las solicitudes del titular con sus plazos en días hábiles. Los enums llegan como número y se mandan por nombre (TextosDePlataforma). (nuevos)

/// <summary>Una carga de lista restrictiva. <c>Status</c>: Active, Replaced o Scheduled (derivado).</summary>
public sealed record CargaDeListaRestrictivaDto(
    Guid PublicId,
    string ListCode,
    string Source,
    DateOnly? PublishedOn,
    DateOnly ValidFrom,
    int Version,
    string Status,
    int Rows,
    string Sha256,
    string LoadedBy,
    DateTime LoadedAt);

/// <summary>Una entrada de una carga. <c>EntityKind</c>: 1 persona, 2 organización, 3 otra.</summary>
public sealed record EntradaDeListaRestrictivaDto(
    Guid PublicId,
    int EntityKind,
    string? DocumentType,
    string? DocumentNumber,
    string FullName,
    string? Aliases,
    string? Program,
    string? Notes);

public sealed record TitularDto(Guid PublicId, string? Name, string? TaxId);

/// <summary>
/// Una solicitud del titular. <c>Kind</c>, <c>Purpose</c>, <c>ContactChannel</c>, <c>Status</c> y <c>Outcome</c> son los números de sus enums;
/// <c>Channel</c> es por dónde llegó. La constancia del deber (<c>DutyEvidenceJson</c>) sólo viene en el detalle.
/// </summary>
public sealed record SolicitudDelTitularDto(
    Guid PublicId,
    TitularDto Person,
    int Kind,
    int? Purpose,
    int? ContactChannel,
    string Channel,
    DateTime ReceivedAt,
    DateOnly DueDate,
    bool Extended,
    DateOnly? ExtendedDueDate,
    int Status,
    int? Outcome,
    string Description,
    string? ResponseText,
    DateTime? RespondedAt,
    string? RespondedBy,
    string? DutyBasis,
    string? DutyEvidenceJson)
{
    /// <summary>La fecha que manda: la prorrogada si la hay.</summary>
    public DateOnly VenceEl => ExtendedDueDate ?? DueDate;

    /// <summary>Sigue abierta (recibida o prorrogada).</summary>
    public bool Abierta => Status is 1 or 2;
}

public sealed record RadicarSolicitudRequest(
    Guid PersonPublicId,
    string Kind,
    string? Purpose,
    string? ContactChannel,
    string Channel,
    DateTime ReceivedAt,
    string Description,
    IReadOnlyList<Guid>? AttachmentPublicIds);

public sealed record ProrrogaDeSolicitudRequest(string Reason);

public sealed record RespuestaDeSolicitudRequest(string Outcome, string Answer, Guid? AttachmentPublicId);
