using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Support;

// Feature 021, US12 (T121; FR-058 a FR-061; contracts/erp-administracion.md, «support» y «metrics»): el funcionario atiende la llamada «pagué y no
// aparece». Busca por documento o referencia y ve, juntos, la cuenta del canal (estado, último ingreso, dispositivos, bloqueo), cada intento con su
// estado en la pasarela, y lo que el ERP hizo con él (aviso, recibo, revisión, reverso). Desbloquea la cuenta o cierra sus sesiones, y ve los
// indicadores del canal junto a las cifras del ERP. El canal sólo responde lo de esta cooperativa (FR-059).

/// <summary>Un intento del canal con lo que el ERP hizo con él.</summary>
public sealed record ChannelSupportAttemptDto(
    string Reference,
    Guid PersonPublicId,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    DateTime? FinishedAt,
    string? Authorization,
    string? PaymentMethodName,
    string ErpOutcome,
    string? NoticeOutcome,
    string? NoticeReason,
    Guid? ReceiptPublicId,
    string? ReceiptNumber,
    string? ReceiptStatus,
    DateTime? ReversedAt,
    string? ReversalKind,
    bool CanReverse);

/// <summary>El resultado de la atención. Sin canal (no configurado o caído) se muestran los avisos del ERP y el motivo.</summary>
public sealed record ChannelSupportDto(
    CuentaDelCanalDto? Account,
    IReadOnlyList<ChannelSupportAttemptDto> Attempts,
    bool ChannelAvailable,
    string? ChannelError);

/// <summary><c>GET /api/channel/support?documentType=&amp;document=&amp;reference=</c> (<c>Channel.Support.View</c>).</summary>
public sealed record SearchChannelSupportQuery(string? DocumentType, string? Document, string? Reference) : IRequest<Result<ChannelSupportDto>>;

public sealed class SearchChannelSupportQueryValidator : AbstractValidator<SearchChannelSupportQuery>
{
    public SearchChannelSupportQueryValidator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Reference) || (!string.IsNullOrWhiteSpace(x.DocumentType) && !string.IsNullOrWhiteSpace(x.Document)))
            .WithName("Búsqueda").WithMessage("Busque por la referencia del pago, o por tipo y número de documento.");
        RuleFor(x => x.Reference).MaximumLength(32);
        RuleFor(x => x.Document).MaximumLength(20);
    }
}

public sealed class SearchChannelSupportQueryHandler(IApplicationDbContext db, ICanalSatelite canal, ICurrentTenantService cooperativa, AuditoriaDelCanal auditoria)
    : IRequestHandler<SearchChannelSupportQuery, Result<ChannelSupportDto>>
{
    public const string EventoBusqueda = "Channel.Support.Searched";

    public async Task<Result<ChannelSupportDto>> Handle(SearchChannelSupportQuery request, CancellationToken ct)
    {
        var referencia = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim().ToUpperInvariant();
        var tipo = request.DocumentType?.Trim().ToUpperInvariant();
        var documento = request.Document?.Trim().ToUpperInvariant();
        Guid.TryParse(cooperativa.TenantId, out var coop);

        CuentaDelCanalDto? cuenta = null;
        IReadOnlyList<IntentoDelCanalDto> intentos = [];
        string? error = null;
        if (!canal.Configurado || coop == Guid.Empty)
            error = "El canal digital no está configurado en este ambiente: sólo se muestran los pagos que llegaron al ERP.";
        else
        {
            var r = await canal.BuscarIntentosAsync(coop, referencia is null ? tipo : null, referencia is null ? documento : null, referencia, ct);
            if (r.IsSuccess) intentos = r.Value;
            else error = r.Error.Message;
            if (referencia is null && tipo is not null && documento is not null)
            {
                var c = await canal.BuscarCuentaAsync(coop, tipo, documento, ct);
                if (c.IsSuccess) cuenta = c.Value;
                else error ??= c.Error.Message;
            }
        }

        // Lo que el ERP sabe: los avisos de esas referencias, o los de la persona del documento.
        var referencias = intentos.Select(i => i.Reference).ToList();
        if (referencia is not null) referencias.Add(referencia);
        var consulta = db.ChannelPaymentNotices.AsNoTracking().Where(n => referencias.Contains(n.Reference));
        if (referencia is null && documento is not null)
        {
            var personas = await IngenIA365ERP.Application.Common.Personas.ResolutorDePersonaPorDocumento.Filtrar(db.People.AsNoTracking(), tipo, documento).Select(p => p.Id)
                .ToListAsync(ct);
            consulta = db.ChannelPaymentNotices.AsNoTracking().Where(n => referencias.Contains(n.Reference) || personas.Contains(n.PersonId));
        }
        var avisos = await consulta.OrderByDescending(n => n.ApprovedAt).Take(100).ToListAsync(ct);
        var recibos = await RecibosAsync(avisos, ct);
        var porReferencia = avisos.ToDictionary(n => n.Reference);
        var personasPublicas = await db.People.AsNoTracking().Where(p => avisos.Select(a => a.PersonId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);

        var filas = new List<ChannelSupportAttemptDto>();
        foreach (var i in intentos)
            filas.Add(Fila(i.Reference, i.PersonPublicId, i.Status, i.Total, i.CreatedAt, i.FinishedAt, i.Authorization, i.PaymentMethodName, i.ErpOutcome,
                porReferencia.GetValueOrDefault(i.Reference), recibos, i.ReversedAt, i.ReversalKind));
        // Avisos que el canal no devolvió (canal caído o búsqueda por documento de otra cuenta): se muestran igual.
        foreach (var n in avisos.Where(n => intentos.All(i => i.Reference != n.Reference)))
            filas.Add(Fila(n.Reference, personasPublicas.GetValueOrDefault(n.PersonId), "approved", n.Total, n.ApprovedAt, n.ApprovedAt, n.Authorization,
                n.PaymentMethodName, "unknown", n, recibos, n.ReversedAt, null));

        await auditoria.LecturaAsync(EventoBusqueda, "ChannelSupport", null,
            new { byReference = referencia is not null, documentType = tipo, found = filas.Count, account = cuenta is not null }, ct);
        return Result.Success(new ChannelSupportDto(cuenta, filas.OrderByDescending(f => f.CreatedAt).ToList(), error is null, error));
    }

    private async Task<Dictionary<long, (Guid PublicId, string Number, ReceiptState Status)>> RecibosAsync(IReadOnlyList<ChannelPaymentNotice> avisos,
        CancellationToken ct)
    {
        var ids = avisos.Where(a => a.ReceiptId != null).Select(a => a.ReceiptId!.Value).ToList();
        var filas = await db.IssuedReceipts.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => new { r.Id, r.PublicId, r.Number, r.Status }).ToListAsync(ct);
        return filas.ToDictionary(r => r.Id, r => (r.PublicId, r.Number, r.Status));
    }

    private static ChannelSupportAttemptDto Fila(string referencia, Guid persona, string estado, decimal total, DateTime creado, DateTime? terminado,
        string? autorizacion, string? medio, string resultadoErp, ChannelPaymentNotice? aviso,
        Dictionary<long, (Guid PublicId, string Number, ReceiptState Status)> recibos, DateTime? reversadoEnElCanal, string? tipoDeReverso)
    {
        (Guid PublicId, string Number, ReceiptState Status)? recibo = aviso?.ReceiptId is { } id && recibos.TryGetValue(id, out var r) ? r : null;
        var reversado = aviso?.ReversedAt ?? reversadoEnElCanal;
        var puedeReversar = aviso is not null && aviso.ReversedAt is null && (recibo is null || recibo.Value.Status == ReceiptState.Valid);
        return new ChannelSupportAttemptDto(referencia, persona, estado, total, creado, terminado, autorizacion, medio, resultadoErp,
            aviso?.Outcome.ToString(), aviso?.OutcomeReason, recibo?.PublicId, recibo?.Number, recibo?.Status.ToString(), reversado, tipoDeReverso, puedeReversar);
    }
}

/// <summary>Los indicadores del canal (los del canal) junto a las cifras del ERP en el mismo rango.</summary>
public sealed record ChannelMetricsDto(
    DateOnly From,
    DateOnly To,
    MetricasDelCanalDto? Channel,
    string? ChannelError,
    int EnabledPeople,
    int NoticesApplied,
    int NoticesInReview,
    decimal ValueApplied,
    int Reversals);

/// <summary><c>GET /api/channel/metrics?from=&amp;to=</c> (<c>Channel.Metrics.View</c>; FR-060). Máximo un año.</summary>
public sealed record GetChannelMetricsQuery(DateOnly From, DateOnly To) : IRequest<Result<ChannelMetricsDto>>;

public sealed class GetChannelMetricsQueryValidator : AbstractValidator<GetChannelMetricsQuery>
{
    public GetChannelMetricsQueryValidator()
    {
        RuleFor(x => x).Must(x => x.From <= x.To && x.To.DayNumber - x.From.DayNumber <= 366)
            .WithName("Rango").WithMessage("El rango va de una fecha a otra igual o posterior, de máximo un año.");
    }
}

public sealed class GetChannelMetricsQueryHandler(IApplicationDbContext db, ICanalSatelite canal, ICurrentTenantService cooperativa)
    : IRequestHandler<GetChannelMetricsQuery, Result<ChannelMetricsDto>>
{
    public async Task<Result<ChannelMetricsDto>> Handle(GetChannelMetricsQuery request, CancellationToken ct)
    {
        MetricasDelCanalDto? delCanal = null;
        string? error = null;
        if (canal.Configurado && Guid.TryParse(cooperativa.TenantId, out var coop))
        {
            var r = await canal.MetricasAsync(coop, request.From, request.To, ct);
            if (r.IsSuccess) delCanal = r.Value;
            else error = r.Error.Message;
        }
        else
            error = "El canal digital no está configurado en este ambiente: sólo se muestran las cifras del ERP.";

        // El rango es de fechas de Colombia: [desde 00:00, hasta + 1 día 00:00) en UTC.
        var desde = request.From.ToDateTime(TimeOnly.MinValue).AddHours(5);
        var hasta = request.To.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(5);
        var avisos = await db.ChannelPaymentNotices.AsNoTracking().Where(n => n.ApprovedAt >= desde && n.ApprovedAt < hasta)
            .Select(n => new { n.Outcome, n.Total, n.ReversedAt }).ToListAsync(ct);
        var habilitadas = await db.ChannelEnablements.AsNoTracking().CountAsync(e => e.Status == EnablementStatus.Enabled, ct);
        var vigentes = avisos.Where(a => a.ReversedAt is null).ToList();
        return Result.Success(new ChannelMetricsDto(request.From, request.To, delCanal, error, habilitadas,
            vigentes.Count(a => a.Outcome == NoticeOutcome.Applied), vigentes.Count(a => a.Outcome == NoticeOutcome.InReview), vigentes.Sum(a => a.Total),
            avisos.Count(a => a.ReversedAt is not null)));
    }
}
