using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

// Feature 012, I4, T715 (api.md §24.4): la bandeja de documentos electrónicos y su detalle.

/// <summary>El documento comercial que originó el electrónico, como lo describe su módulo. (nuevo)</summary>
public sealed record OrigenDelDocumentoDto(string Module, Guid DocumentPublicId, string? DocumentClass, string? Route);

/// <summary>
/// Lo que la bandeja le pregunta al módulo fuente (feature 012, I4, T715): qué documentos comerciales ve quien consulta (el alcance de bodega y
/// punto de venta es del módulo: la plataforma no lee <c>INV_</c>) y cómo se describen (clase y ruta de la pantalla). Inventario lo implementa con
/// <c>ConsultaDeEmisionDeInventario</c> sobre <c>IAlcanceDeInventario</c>. (nuevo)
/// </summary>
public interface IConsultaDeFuenteElectronica
{
    string SourceModule { get; }

    /// <summary>Los <c>PublicId</c> de los documentos comerciales visibles, como consulta componible; nula = sin restricción.</summary>
    Task<IQueryable<Guid>?> VisiblesAsync(CancellationToken ct);

    /// <summary>Clase y ruta de cada documento comercial pedido.</summary>
    Task<IReadOnlyDictionary<Guid, OrigenDelDocumentoDto>> DescribirAsync(IReadOnlyCollection<Guid> documentos, CancellationToken ct);
}

/// <summary>Una fila de la bandeja (api.md §24.4 <c>ElectronicDocumentDto</c>). (nuevo)</summary>
public sealed record ElectronicDocumentDto(
    Guid ElectronicDocumentPublicId,
    ElectronicDocumentKind Kind,
    string DianDocumentTypeCode,
    string Prefix,
    long Consecutive,
    string Number,
    DianEnvironment Environment,
    string ChannelCode,
    OrigenDelDocumentoDto Source,
    string? CounterpartyName,
    DateTime IssuedAt,
    decimal Total,
    ElectronicDocumentStatus Status,
    ContingencyType? ContingencyType,
    RejectedBy? RejectedBy,
    string? UniqueCode,
    UniqueCodeKind? UniqueCodeKind,
    int AttemptCount,
    DateTime? NextAttemptAt,
    DateTime? TransmissionDeadline,
    string? LastMessage);

public sealed record ReferenciaDeResolucionDto(Guid PublicId, string Number, string Prefix);

public sealed record ArtefactoDeVersionDto(string Artifact, Guid AttachmentPublicId, string? FileName);

public sealed record VersionElectronicaDto(
    int VersionNumber,
    DocumentVersionReason Reason,
    Guid SourceDocumentPublicId,
    string CanonicalSha256,
    DateTime CreatedAt,
    IReadOnlyList<ArtefactoDeVersionDto> Artifacts,
    JsonElement? PartySnapshotChange);

public sealed record SolicitanteDto(ActorKind Kind, string Name);

public sealed record MensajeDeTransmisionDto(string Rule, string Kind, string Text, string? Translation);

public sealed record TransmisionElectronicaDto(
    int AttemptNumber,
    TransmissionOperation Operation,
    string ChannelCode,
    DateTime RequestedAt,
    SolicitanteDto RequestedBy,
    int DurationMs,
    ChannelOutcome Outcome,
    string? ProviderCode,
    string? DianStatusCode,
    IReadOnlyList<MensajeDeTransmisionDto> Messages,
    string? ExternalReference,
    Guid? ApplicationResponseAttachmentPublicId);

public sealed record DocumentoCorregidoDto(Guid ElectronicDocumentPublicId, string Number, string? UniqueCode);

public sealed record CorreccionDelDocumentoDto(Guid ElectronicDocumentPublicId, ElectronicDocumentKind Kind, string Number, ElectronicDocumentStatus Status);

public sealed record ContingenciaDelDocumentoDto(Guid ContingencyPublicId, ContingencyType Type, DateTime StartedAt, DateTime? EndedAt, DateTime? DeadlineAt);

public sealed record CancelacionDelDocumentoDto(string? Reason, string? ByName, DateTime? At);

/// <summary>El detalle (api.md §24.4 <c>ElectronicDocumentDetailDto</c>): la fila más versiones, transmisiones, correcciones y contingencia. (nuevo)</summary>
public sealed record ElectronicDocumentDetailDto(
    ElectronicDocumentDto Document,
    ReferenciaDeResolucionDto? Resolution,
    string? QrContent,
    DateTime? ValidatedAt,
    DateTime? DeliveredAt,
    DateTime? EmailSentAt,
    EmailDeliveryBy EmailDeliveryBy,
    IReadOnlyList<VersionElectronicaDto> Versions,
    IReadOnlyList<TransmisionElectronicaDto> Transmissions,
    DocumentoCorregidoDto? CorrectsDocument,
    IReadOnlyList<CorreccionDelDocumentoDto> CorrectedBy,
    ContingenciaDelDocumentoDto? Contingency,
    CancelacionDelDocumentoDto? Cancellation);

/// <summary>
/// La bandeja (api.md §24.4 <c>GET /documents</c>): filtros por estado, tipo, fechas de expedición, prefijo, número, módulo, contingencia y
/// <c>overdue</c> —los que llevan más de <c>Dian.MinutosAlertaSinValidar</c> sin validar—, con el alcance de bodega y punto de venta que decide
/// cada módulo fuente (<see cref="IConsultaDeFuenteElectronica"/>). Del más reciente al más antiguo. (nuevo)
/// </summary>
public sealed record ListElectronicDocumentsQuery(
    ElectronicDocumentStatus? Status = null,
    ElectronicDocumentKind? Kind = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Prefix = null,
    string? Number = null,
    string? SourceModule = null,
    bool? Contingency = null,
    bool? Overdue = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ElectronicDocumentDto>>>;

public sealed class ListElectronicDocumentsQueryValidator : AbstractValidator<ListElectronicDocumentsQuery>
{
    public ListElectronicDocumentsQueryValidator()
    {
        RuleFor(x => x.Prefix).MaximumLength(10);
        RuleFor(x => x.Number).MaximumLength(20);
        RuleFor(x => x.SourceModule).MaximumLength(3);
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithMessage("La fecha inicial no puede ser posterior a la final.");
    }
}

public sealed class ListElectronicDocumentsQueryHandler(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    IEnumerable<IConsultaDeFuenteElectronica> fuentes)
    : IRequestHandler<ListElectronicDocumentsQuery, Result<PagedResult<ElectronicDocumentDto>>>
{
    public async Task<Result<PagedResult<ElectronicDocumentDto>>> Handle(ListElectronicDocumentsQuery request, CancellationToken ct)
    {
        var q = await ConsultaDeDocumentosElectronicos.ConAlcanceAsync(db.ElectronicDocuments.AsNoTracking(), fuentes, ct);

        if (request.Status is { } estado) q = q.Where(d => d.Status == estado);
        if (request.Kind is { } tipo) q = q.Where(d => d.Kind == tipo);
        if (request.From is { } desde) q = q.Where(d => d.IssueDate >= desde);
        if (request.To is { } hasta) q = q.Where(d => d.IssueDate <= hasta);
        if (!string.IsNullOrWhiteSpace(request.Prefix))
        {
            var prefijo = request.Prefix.Trim().ToUpperInvariant();
            q = q.Where(d => d.Prefix == prefijo);
        }
        if (!string.IsNullOrWhiteSpace(request.Number))
        {
            var numero = request.Number.Trim().ToUpperInvariant();
            q = q.Where(d => d.Number.Contains(numero));
        }
        if (!string.IsNullOrWhiteSpace(request.SourceModule))
        {
            var modulo = request.SourceModule.Trim().ToUpperInvariant();
            q = q.Where(d => d.SourceModule == modulo);
        }
        if (request.Contingency is { } conContingencia)
            q = conContingencia ? q.Where(d => d.ContingencyType != null) : q.Where(d => d.ContingencyType == null);
        if (request.Overdue == true)
        {
            var minutos = await parametros.LeerComoAsync<int>(ParametrosDeFacturacionElectronica.Modulo,
                ParametrosDeFacturacionElectronica.MinutosAlertaSinValidar, reloj.HoyLocal, ct: ct);
            var limite = reloj.UtcNow.AddMinutes(-(minutos.IsSuccess ? minutos.Value : 0));
            q = q.Where(d => (d.Status == ElectronicDocumentStatus.Pending || d.Status == ElectronicDocumentStatus.Sent) && d.IssuedAt < limite);
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await q.LongCountAsync(ct);
        var filas = await q.OrderByDescending(d => d.IssuedAt).ThenByDescending(d => d.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);
        var origenes = await ConsultaDeDocumentosElectronicos.OrigenesAsync(filas, fuentes, ct);
        return Result.Success(new PagedResult<ElectronicDocumentDto>(
            filas.Select(d => ConsultaDeDocumentosElectronicos.Fila(d, origenes)).ToList(), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>El detalle de un documento electrónico (api.md §24.4 <c>GET /documents/{id}</c>). Fuera del alcance → no existe. (nuevo)</summary>
public sealed record GetElectronicDocumentQuery(Guid ElectronicDocumentPublicId) : IRequest<Result<ElectronicDocumentDetailDto>>;

public sealed class GetElectronicDocumentQueryHandler(IApplicationDbContext db, IEnumerable<IConsultaDeFuenteElectronica> fuentes)
    : IRequestHandler<GetElectronicDocumentQuery, Result<ElectronicDocumentDetailDto>>
{
    public async Task<Result<ElectronicDocumentDetailDto>> Handle(GetElectronicDocumentQuery request, CancellationToken ct)
    {
        var q = await ConsultaDeDocumentosElectronicos.ConAlcanceAsync(db.ElectronicDocuments.AsNoTracking(), fuentes, ct);
        var d = await q
            .Include(x => x.Versions)
            .Include(x => x.Transmissions)
            .Include(x => x.Resolution)
            .Include(x => x.ContingencyEvent)
            .Include(x => x.CorrectsDocument)
            .FirstOrDefaultAsync(x => x.PublicId == request.ElectronicDocumentPublicId, ct);
        if (d is null) return Result.Failure<ElectronicDocumentDetailDto>(ErroresDeDocumentosElectronicos.NotFound());

        var origenes = await ConsultaDeDocumentosElectronicos.OrigenesAsync([d], fuentes, ct);
        var adjuntos = d.Versions
            .SelectMany(v => new[] { v.CanonicalAttachmentPublicId, v.SignedXmlAttachmentPublicId, v.AttachedDocumentAttachmentPublicId, v.GraphicPdfAttachmentPublicId })
            .Where(id => id is not null).Select(id => id!.Value).ToList();
        var nombres = await db.Attachments.AsNoTracking().Where(a => adjuntos.Contains(a.PublicId))
            .ToDictionaryAsync(a => a.PublicId, a => a.FileName, ct);

        var versiones = d.Versions.OrderBy(v => v.VersionNumber).Select(v => new VersionElectronicaDto(
            v.VersionNumber, v.Reason, v.SourceDocumentPublicId, v.CanonicalSha256, v.CreatedAt,
            new (string Artefacto, Guid? Id)[]
            {
                (ArtefactosElectronicos.Canonical, v.CanonicalAttachmentPublicId),
                (ArtefactosElectronicos.SignedXml, v.SignedXmlAttachmentPublicId),
                (ArtefactosElectronicos.AttachedDocument, v.AttachedDocumentAttachmentPublicId),
                (ArtefactosElectronicos.GraphicRepresentation, v.GraphicPdfAttachmentPublicId),
            }.Where(a => a.Id is not null)
             .Select(a => new ArtefactoDeVersionDto(a.Artefacto, a.Id!.Value, nombres.GetValueOrDefault(a.Id.Value)))
             .ToList(),
            ConsultaDeDocumentosElectronicos.Json(v.ChangedFieldsJson))).ToList();

        var transmisiones = d.Transmissions.OrderBy(t => t.AttemptNumber).Select(t => new TransmisionElectronicaDto(
            t.AttemptNumber, t.Operation, t.ChannelCode, t.RequestedAt, new SolicitanteDto(t.RequestedByKind, t.RequestedByName), t.DurationMs,
            t.Outcome, t.ProviderCode, t.DianStatusCode, ConsultaDeDocumentosElectronicos.Mensajes(t.TranslatedMessagesJson), t.ExternalReference,
            t.ApplicationResponseAttachmentPublicId)).ToList();

        var corregidoPor = await db.ElectronicDocuments.AsNoTracking().Where(x => x.CorrectsDocumentId == d.Id)
            .OrderBy(x => x.Id)
            .Select(x => new CorreccionDelDocumentoDto(x.PublicId, x.Kind, x.Number, x.Status))
            .ToListAsync(ct);

        CancelacionDelDocumentoDto? cancelacion = null;
        if (d.Status == ElectronicDocumentStatus.CancelledWithoutReplacement)
        {
            var quien = d.CancelledByUserId is int u
                ? await db.Users.AsNoTracking().Where(x => x.Id == u).Select(x => x.Username).FirstOrDefaultAsync(ct)
                : null;
            cancelacion = new CancelacionDelDocumentoDto(d.RejectionReason, quien ?? d.UpdatedBy, d.UpdatedAt);
        }

        return Result.Success(new ElectronicDocumentDetailDto(
            ConsultaDeDocumentosElectronicos.Fila(d, origenes),
            d.Resolution is { } r ? new ReferenciaDeResolucionDto(r.PublicId, r.ResolutionNumber, r.Prefix) : null,
            d.QrContent, d.ValidatedAt, d.DeliveredAt, d.EmailSentAt, d.EmailDeliveryBy,
            versiones,
            transmisiones,
            d.CorrectsDocument is { } c ? new DocumentoCorregidoDto(c.PublicId, c.Number, c.UniqueCode) : null,
            corregidoPor,
            d.ContingencyEvent is { } e ? new ContingenciaDelDocumentoDto(e.PublicId, e.Type, e.StartedAt, e.EndedAt, e.DeadlineAt) : null,
            cancelacion));
    }
}

/// <summary>Piezas compartidas de la bandeja y el detalle. (nuevo)</summary>
public static class ConsultaDeDocumentosElectronicos
{
    /// <summary>Restringe a lo que cada módulo fuente deja ver; un módulo sin fuente registrada no se restringe.</summary>
    public static async Task<IQueryable<ElectronicDocument>> ConAlcanceAsync(IQueryable<ElectronicDocument> q,
        IEnumerable<IConsultaDeFuenteElectronica> fuentes, CancellationToken ct)
    {
        foreach (var fuente in fuentes)
        {
            if (await fuente.VisiblesAsync(ct) is not { } visibles) continue;
            var modulo = fuente.SourceModule;
            q = q.Where(d => d.SourceModule != modulo || visibles.Contains(d.SourceDocumentPublicId));
        }
        return q;
    }

    public static async Task<IReadOnlyDictionary<Guid, OrigenDelDocumentoDto>> OrigenesAsync(IReadOnlyList<ElectronicDocument> filas,
        IEnumerable<IConsultaDeFuenteElectronica> fuentes, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, OrigenDelDocumentoDto>();
        foreach (var fuente in fuentes)
        {
            var ids = filas.Where(f => f.SourceModule == fuente.SourceModule).Select(f => f.SourceDocumentPublicId).Distinct().ToList();
            if (ids.Count == 0) continue;
            foreach (var (id, origen) in await fuente.DescribirAsync(ids, ct)) resultado[id] = origen;
        }
        return resultado;
    }

    public static ElectronicDocumentDto Fila(ElectronicDocument d, IReadOnlyDictionary<Guid, OrigenDelDocumentoDto> origenes) => new(
        d.PublicId, d.Kind, d.DianDocumentTypeCode, d.Prefix, d.Consecutive, d.Number, d.Environment, d.ChannelCode,
        origenes.GetValueOrDefault(d.SourceDocumentPublicId) ?? new OrigenDelDocumentoDto(d.SourceModule, d.SourceDocumentPublicId, null, null),
        d.CounterpartyName, d.IssuedAt, d.TotalAmount, d.Status, d.ContingencyType, d.RejectedBy, d.UniqueCode, d.UniqueCodeKind,
        d.AttemptCount, d.NextAttemptAt, d.TransmissionDeadline, Mensajes(d.LastMessagesJson).Select(m => m.Translation ?? m.Text).FirstOrDefault());

    /// <summary>Los mensajes traducidos guardados (<c>[{ regla, tipo, texto, traduccion }]</c>).</summary>
    public static IReadOnlyList<MensajeDeTransmisionDto> Mensajes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return doc.RootElement.EnumerateArray().Select(m => new MensajeDeTransmisionDto(
                Texto(m, "regla") ?? string.Empty, Texto(m, "tipo") ?? string.Empty, Texto(m, "texto") ?? string.Empty, Texto(m, "traduccion"))).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static JsonElement? Json(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
