using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;

/// <summary>Quién detectó la contingencia: una persona o el proceso (api.md §24.6 <c>detectedBy</c>). (nuevo)</summary>
public sealed record QuienDetectoDto(ActorKind Kind, string Name);

/// <summary>Los documentos del evento: cuántos, cuántos siguen por transmitir, cuántos quedaron validados y cuántos rechazados. (nuevo)</summary>
public sealed record DocumentosDeLaContingenciaDto(int Total, int Pending, int Transmitted, int Rejected);

/// <summary>Un evento de contingencia (api.md §24.6 <c>DianContingencyEventDto</c>). (nuevo)</summary>
public sealed record DianContingencyEventDto(
    Guid ContingencyPublicId,
    ContingencyType Type,
    string ChannelCode,
    DateTime StartedAt,
    DateTime? EndedAt,
    bool IsOpen,
    QuienDetectoDto DetectedBy,
    string Reason,
    DateTime? DeadlineAt,
    DocumentosDeLaContingenciaDto Documents);

/// <summary>Una evidencia del evento: la declaración, el cierre o un archivo subido (api.md §24.6 <c>evidence[]</c>). (nuevo)</summary>
public sealed record EvidenciaDeContingenciaDto(DateTime At, string ByName, string Note);

/// <summary>El detalle de un evento: el evento, sus documentos, su evidencia y la norma con que se fijó el plazo. (nuevo)</summary>
public sealed record DianContingencyEventDetailDto(
    DianContingencyEventDto Contingency,
    IReadOnlyList<ElectronicDocumentDto> Documents,
    IReadOnlyList<EvidenciaDeContingenciaDto> Evidence,
    short DeadlineHoursApplied,
    string LegalSource,
    string? ClosedByName,
    string? CloseReason);

/// <summary>
/// La bandeja de contingencias (feature 012, I4, T726; api.md §24.6 <c>GET /contingencies?isOpen=&amp;type=&amp;from=&amp;to=</c>,
/// <c>ElectronicInvoicing.Contingencies.View</c>): de la más reciente a la más antigua, con el conteo de sus documentos. Las fechas filtran
/// por el inicio (fecha UTC). (nuevo)
/// </summary>
public sealed record ListContingenciesQuery(bool? IsOpen = null, ContingencyType? Type = null, DateOnly? From = null, DateOnly? To = null)
    : IRequest<Result<IReadOnlyList<DianContingencyEventDto>>>;

public sealed class ListContingenciesQueryValidator : AbstractValidator<ListContingenciesQuery>
{
    public ListContingenciesQueryValidator() =>
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithMessage("La fecha inicial no puede ser posterior a la final.");
}

public sealed class ListContingenciesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListContingenciesQuery, Result<IReadOnlyList<DianContingencyEventDto>>>
{
    public async Task<Result<IReadOnlyList<DianContingencyEventDto>>> Handle(ListContingenciesQuery request, CancellationToken ct)
    {
        var q = db.DianContingencyEvents.AsNoTracking();
        if (request.IsOpen is { } abierta)
            q = abierta ? q.Where(e => e.Status == ContingencyEventStatus.Open) : q.Where(e => e.Status != ContingencyEventStatus.Open);
        if (request.Type is { } tipo) q = q.Where(e => e.Type == tipo);
        if (request.From is { } desde)
        {
            var d = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(e => e.StartedAt >= d);
        }
        if (request.To is { } hasta)
        {
            var h = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(e => e.StartedAt < h);
        }

        var eventos = await q.OrderByDescending(e => e.StartedAt).ThenByDescending(e => e.Id).ToListAsync(ct);
        var estados = await ConsultasDeContingencias.EstadosAsync(db, eventos.Select(e => e.Id).ToList(), ct);
        IReadOnlyList<DianContingencyEventDto> filas = eventos.Select(e => ConsultasDeContingencias.Dto(e, estados.GetValueOrDefault(e.Id) ?? [])).ToList();
        return Result.Success(filas);
    }
}

/// <summary>
/// El detalle de una contingencia (feature 012, I4, T726; api.md §24.6 <c>GET /contingencies/{id}</c>): el evento, sus documentos
/// —con el alcance de bodega y punto de venta de cada módulo fuente— y la evidencia (la declaración, el cierre y los archivos subidos con dueño
/// <c>DianContingencyEvent</c>). La bitácora de transmisiones de cada documento está en su detalle. (nuevo)
/// </summary>
public sealed record GetContingencyQuery(Guid ContingencyPublicId) : IRequest<Result<DianContingencyEventDetailDto>>;

public sealed class GetContingencyQueryHandler(IApplicationDbContext db, IEnumerable<IConsultaDeFuenteElectronica> fuentes)
    : IRequestHandler<GetContingencyQuery, Result<DianContingencyEventDetailDto>>
{
    public async Task<Result<DianContingencyEventDetailDto>> Handle(GetContingencyQuery request, CancellationToken ct)
    {
        var e = await db.DianContingencyEvents.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ContingencyPublicId, ct);
        if (e is null) return Result.Failure<DianContingencyEventDetailDto>(ErroresDeContingencias.NotFound());

        var estados = await ConsultasDeContingencias.EstadosAsync(db, [e.Id], ct);
        var q = await ConsultaDeDocumentosElectronicos.ConAlcanceAsync(db.ElectronicDocuments.AsNoTracking(), fuentes, ct);
        var filas = await q.Where(d => d.ContingencyEventId == e.Id).OrderBy(d => d.Prefix).ThenBy(d => d.Consecutive).ToListAsync(ct);
        var origenes = await ConsultaDeDocumentosElectronicos.OrigenesAsync(filas, fuentes, ct);

        var evidencia = new List<EvidenciaDeContingenciaDto> { new(e.StartedAt, e.DetectedByName, e.Reason) };
        if (e.EndedAt is { } fin) evidencia.Add(new(fin, e.ClosedByName ?? string.Empty, e.CloseReason ?? string.Empty));
        var archivos = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerEntityType == AdjuntosDeModulo.EventoDeContingencia && a.OwnerEntityPublicId == e.PublicId && !a.IsDeleted)
            .Select(a => new { a.CreatedAt, a.CreatedBy, a.FileName })
            .ToListAsync(ct);
        evidencia.AddRange(archivos.Select(a => new EvidenciaDeContingenciaDto(a.CreatedAt, a.CreatedBy ?? string.Empty, a.FileName)));

        return Result.Success(new DianContingencyEventDetailDto(
            ConsultasDeContingencias.Dto(e, estados.GetValueOrDefault(e.Id) ?? []),
            filas.Select(d => ConsultaDeDocumentosElectronicos.Fila(d, origenes)).ToList(),
            evidencia.OrderBy(x => x.At).ToList(),
            e.DeadlineHoursApplied,
            e.LegalSource,
            e.ClosedByName,
            e.CloseReason));
    }
}

/// <summary>Piezas compartidas por los comandos y las consultas de contingencias. (nuevo)</summary>
public static class ConsultasDeContingencias
{
    /// <summary>Los estados de los documentos de cada evento.</summary>
    public static async Task<Dictionary<int, List<ElectronicDocumentStatus>>> EstadosAsync(IApplicationDbContext db, IReadOnlyList<int> eventos,
        CancellationToken ct)
    {
        if (eventos.Count == 0) return [];
        var filas = await db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.ContingencyEventId != null && eventos.Contains(d.ContingencyEventId.Value))
            .Select(d => new { Evento = d.ContingencyEventId!.Value, d.Status })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.Evento).ToDictionary(g => g.Key, g => g.Select(f => f.Status).ToList());
    }

    public static DianContingencyEventDto Dto(DianContingencyEvent e, IReadOnlyList<ElectronicDocumentStatus> estados) => new(
        e.PublicId,
        e.Type,
        e.ChannelCode,
        e.StartedAt,
        e.EndedAt,
        e.EstaAbierto,
        new QuienDetectoDto(e.DetectedByKind, e.DetectedByName),
        e.Reason,
        e.DeadlineAt,
        new DocumentosDeLaContingenciaDto(
            estados.Count,
            estados.Count(s => s is ElectronicDocumentStatus.IssuerContingency or ElectronicDocumentStatus.DianContingency
                or ElectronicDocumentStatus.Pending or ElectronicDocumentStatus.Sent),
            estados.Count(s => s is ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices),
            estados.Count(s => s is ElectronicDocumentStatus.Rejected or ElectronicDocumentStatus.CancelledWithoutReplacement)));

    /// <summary>El motivo con la nota de evidencia, si la hay.</summary>
    public static string ConEvidencia(string motivo, string? nota) =>
        string.IsNullOrWhiteSpace(nota) ? motivo.Trim() : $"{motivo.Trim()} — Evidencia: {nota.Trim()}";
}
