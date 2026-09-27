using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// La bandeja de mensajes (feature 012, T504; api.md §25.2, <c>GET /api/inventory/messages</c>, <c>Inventory.Messages.View</c>).
/// Una fila por entrega (mensaje × destino), en <b>orden de emisión</b>, con <c>countsByStatus</c>, «espera al mensaje X»
/// (<c>blockedBy</c>, calculado), <c>destinationAvailable</c> (falso para un destino sin consumidor, como Cartera hasta IC) y el
/// <c>result</c> leído <b>sólo</b> de la entrega —nunca de tablas <c>ACC_</c> (FR-014)—. Con <see cref="Closure"/> suma toda la
/// clausura de relacionados de cualquier fecha (la vista previa del envío posterior, FR-078) y devuelve el
/// <c>cutoffMessagePublicId</c>. Filtra por el alcance de bodega (<see cref="IAlcanceDeInventario"/>) sobre el documento de origen;
/// las operaciones sin documento (cierre, reapertura, reclasificación) sólo las ve quien tiene todas las bodegas. El <c>Id</c>
/// <c>bigint</c> nunca sale (Principio VI). Con <c>status=Rejected</c> y <see cref="PrevalidationOutcome"/> mide SC-021.
/// </summary>
public sealed record ListIntegrationMessagesQuery(
    DeliveryStatus? Status = null,
    string? Destination = null,
    string? Type = null,
    Guid? Document = null,
    string? DocumentNumber = null,
    string? DocumentType = null,
    Guid? Batch = null,
    DateOnly? From = null,
    DateOnly? To = null,
    bool Closure = false,
    PrevalidationOutcome? PrevalidationOutcome = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<BandejaDeMensajesDto>>;

public sealed class ListIntegrationMessagesQueryValidator : AbstractValidator<ListIntegrationMessagesQuery>
{
    public ListIntegrationMessagesQueryValidator()
    {
        RuleFor(x => x.Destination).MaximumLength(20);
        RuleFor(x => x.Type).MaximumLength(60);
        RuleFor(x => x.DocumentNumber).MaximumLength(30);
        RuleFor(x => x.DocumentType).MaximumLength(10);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}

/// <summary>La bandeja: la página, los contadores por estado y, con clausura, el corte. (nuevo)</summary>
public sealed record BandejaDeMensajesDto(
    PagedResult<IntegrationMessageDto> Messages,
    ConteoPorEstadoDto CountsByStatus,
    Guid? CutoffMessagePublicId);

/// <summary><c>countsByStatus</c> de api.md §25.2. (nuevo)</summary>
public sealed record ConteoPorEstadoDto(int Pending, int InBatch, int Processed, int Rejected, int NotApplicable, int ValidationFailed);

/// <summary>Una entrega de la bandeja (api.md §25.2, <c>IntegrationMessageDto</c>). Sin <c>Id</c> interno.</summary>
public sealed record IntegrationMessageDto(
    Guid MessagePublicId,
    DateTime EmittedAt,
    string Type,
    int Version,
    IntegrationMessageKind Kind,
    string Destination,
    DeliveryStatus DeliveryStatus,
    DeliveryMode Mode,
    string? ScheduleKey,
    LoteDelMensajeDto? Batch,
    int Attempts,
    DateTime? NextAttemptAt,
    ErrorDelMensajeDto? LastError,
    DateTime? ProcessedAt,
    ResultadoDelMensajeDto? Result,
    OrigenDelMensajeDto Origin,
    RelacionadoDelMensajeDto? Related,
    string OriginUserName,
    PrevalidationOutcome? PrevalidationOutcome,
    IReadOnlyList<BloqueoDelMensajeDto> BlockedBy,
    bool DestinationAvailable,
    bool? PendingValidation);

public sealed record LoteDelMensajeDto(Guid BatchPublicId, long Number);

/// <summary><c>whoFixes</c> lo pone la pantalla con el mapa de Contabilidad; la plataforma no lo conoce.</summary>
public sealed record ErrorDelMensajeDto(string? Code, string? Message, string? DataJson);

/// <summary>
/// <c>result</c>: el comprobante con su ruta (<c>/contabilidad/comprobantes/{id}</c>, o el lote si fue resumido) o, sin
/// comprobante, <see cref="WithoutVoucher"/>. (nuevo)
/// </summary>
public sealed record ResultadoDelMensajeDto(
    string? VoucherTypeCode,
    string? Number,
    Guid? AccountingDocumentPublicId,
    string? Route,
    MotivoSinComprobante? WithoutVoucher);

public sealed record OrigenDelMensajeDto(
    MessageOriginKind Kind,
    string? DocumentClass,
    string? DocumentTypeCode,
    string? Number,
    Guid PublicId,
    DateOnly OperationDate);

public sealed record RelacionadoDelMensajeDto(Guid PublicId, string? DocumentClass, string? Number);

/// <summary>«Espera al mensaje X»: una dependencia que todavía bloquea en el mismo destino.</summary>
public sealed record BloqueoDelMensajeDto(Guid MessagePublicId, string Type, DeliveryStatus DeliveryStatus);

public sealed class ListIntegrationMessagesQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeInventario,
    IEnumerable<IDestinoDeMensajes> destinos) : IRequestHandler<ListIntegrationMessagesQuery, Result<BandejaDeMensajesDto>>
{
    public async Task<Result<BandejaDeMensajesDto>> Handle(ListIntegrationMessagesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeInventario.ObtenerAsync(ct);
        var visibles = VistaDeMensajes.Visibles(db, alcance);

        var sinEstado = Filtrar(visibles, request);
        var conteos = await sinEstado.GroupBy(d => d.Status).Select(g => new { g.Key, Cantidad = g.Count() }).ToListAsync(ct);
        int Conteo(DeliveryStatus s) => conteos.FirstOrDefault(c => c.Key == s)?.Cantidad ?? 0;

        var filtradas = request.Status is DeliveryStatus estado ? sinEstado.Where(d => d.Status == estado) : sinEstado;
        Guid? corte = null;
        if (request.Closure)
        {
            // La clausura suma relacionados de cualquier fecha, del mismo destino y estado, que el alcance deja ver.
            var semillas = await filtradas.Select(d => d.MessageId).Distinct().ToListAsync(ct);
            var admitidas = VistaDeMensajes.PorDestinoYEstado(visibles, request.Destination, request.Status);
            var clausura = await LotesDeIntegracion.ClausuraAsync(db, semillas, async candidatos =>
                (await admitidas.Where(d => candidatos.Contains(d.MessageId)).Select(d => d.MessageId).ToListAsync(ct)).ToHashSet(), ct);
            var ids = clausura.ToList();
            filtradas = admitidas.Where(d => ids.Contains(d.MessageId));
            if (ids.Count > 0)
            {
                var ultimo = ids.Max();
                corte = await db.IntegrationMessages.AsNoTracking().Where(m => m.Id == ultimo).Select(m => (Guid?)m.PublicId).FirstOrDefaultAsync(ct);
            }
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await filtradas.CountAsync(ct);
        var entregas = await filtradas
            .OrderBy(d => d.MessageId).ThenBy(d => d.Destination)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Include(d => d.Message)
            .ToListAsync(ct);

        var filas = await VistaDeMensajes.ProyectarAsync(db, entregas, destinos, ct);
        return Result.Success(new BandejaDeMensajesDto(
            new PagedResult<IntegrationMessageDto>(filas, pagina.SafePage, pagina.SafePageSize, total),
            new ConteoPorEstadoDto(
                Conteo(DeliveryStatus.Pending), Conteo(DeliveryStatus.InBatch), Conteo(DeliveryStatus.Processed),
                Conteo(DeliveryStatus.Rejected), Conteo(DeliveryStatus.NotApplicable), Conteo(DeliveryStatus.ValidationFailed)),
            corte));
    }

    /// <summary>Todos los filtros de api.md §25.2 salvo el estado (los contadores se cuentan sin él).</summary>
    private IQueryable<IntegrationMessageDelivery> Filtrar(IQueryable<IntegrationMessageDelivery> consulta, ListIntegrationMessagesQuery r)
    {
        if (!string.IsNullOrWhiteSpace(r.Destination)) consulta = consulta.Where(d => d.Destination == r.Destination);
        if (!string.IsNullOrWhiteSpace(r.Type)) consulta = consulta.Where(d => d.Message!.Type == r.Type);
        if (r.Document is Guid documento) consulta = consulta.Where(d => d.Message!.OriginPublicId == documento || d.Message.RelatedPublicId == documento);
        if (!string.IsNullOrWhiteSpace(r.DocumentNumber)) consulta = consulta.Where(d => d.Message!.OriginNumber == r.DocumentNumber);
        if (!string.IsNullOrWhiteSpace(r.DocumentType)) consulta = consulta.Where(d => d.Message!.OriginDocumentTypeCode == r.DocumentType);
        if (r.Batch is Guid lote)
        {
            var loteId = db.IntegrationBatches.Where(b => b.PublicId == lote).Select(b => (int?)b.Id);
            consulta = consulta.Where(d => d.BatchId != null && loteId.Contains(d.BatchId));
        }

        if (r.From is DateOnly desde) consulta = consulta.Where(d => d.Message!.OperationDate >= desde);
        if (r.To is DateOnly hasta) consulta = consulta.Where(d => d.Message!.OperationDate <= hasta);
        if (r.PrevalidationOutcome is PrevalidationOutcome validacion) consulta = consulta.Where(d => d.Message!.PrevalidationOutcome == validacion);
        return consulta;
    }
}

/// <summary>
/// El detalle de un mensaje de la bandeja (feature 012, T504; api.md §25.2, <c>GET /api/inventory/messages/{id}</c>): la fila de la
/// bandeja más el contenido (<c>payload</c>, inmutable, con su <c>payloadSha256</c>), los intentos, de qué depende y quién depende
/// de él. <see cref="Destination"/> elige la entrega si el mensaje tiene más de una. Fuera del alcance, el mismo
/// <c>Integration.Message.NotFound</c> que algo inexistente.
/// </summary>
public sealed record GetIntegrationMessageQuery(Guid MessagePublicId, string? Destination = null) : IRequest<Result<IntegrationMessageDetailDto>>;

public sealed class GetIntegrationMessageQueryValidator : AbstractValidator<GetIntegrationMessageQuery>
{
    public GetIntegrationMessageQueryValidator()
    {
        RuleFor(x => x.MessagePublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Destination).MaximumLength(20);
    }
}

/// <summary>El detalle (api.md §25.2). (nuevo)</summary>
public sealed record IntegrationMessageDetailDto(
    IntegrationMessageDto Message,
    string Payload,
    string PayloadSha256,
    IReadOnlyList<IntentoDelMensajeDto> Attempts,
    IReadOnlyList<VecinoDelMensajeDto> DependsOn,
    IReadOnlyList<VecinoDelMensajeDto> Dependents);

public sealed record IntentoDelMensajeDto(
    DateTime StartedAt,
    DateTime FinishedAt,
    DeliveryAttemptOutcome Outcome,
    string? Code,
    string? Message,
    ActorDelIntentoDto Actor,
    long? BatchNumber,
    string Instance);

public sealed record ActorDelIntentoDto(ActorKind Kind, string Name);

/// <summary>Un mensaje vecino por dependencia, con el estado de su entrega al mismo destino (nulo si no tiene).</summary>
public sealed record VecinoDelMensajeDto(Guid MessagePublicId, string Type, DeliveryStatus? Status);

public sealed class GetIntegrationMessageQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeInventario,
    IEnumerable<IDestinoDeMensajes> destinos) : IRequestHandler<GetIntegrationMessageQuery, Result<IntegrationMessageDetailDto>>
{
    public async Task<Result<IntegrationMessageDetailDto>> Handle(GetIntegrationMessageQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeInventario.ObtenerAsync(ct);
        var consulta = VistaDeMensajes.Visibles(db, alcance).Where(d => d.Message!.PublicId == request.MessagePublicId);
        if (!string.IsNullOrWhiteSpace(request.Destination)) consulta = consulta.Where(d => d.Destination == request.Destination);
        var entrega = await consulta.OrderBy(d => d.Destination).Include(d => d.Message).FirstOrDefaultAsync(ct);
        if (entrega is null) return Result.Failure<IntegrationMessageDetailDto>(ErroresDeIntegracion.MensajeNoEncontrado(request.MessagePublicId));

        var fila = (await VistaDeMensajes.ProyectarAsync(db, [entrega], destinos, ct))[0];
        var mensaje = entrega.Message!;

        var intentos = await (
                from a in db.IntegrationDeliveryAttempts.AsNoTracking()
                where a.DeliveryId == entrega.Id
                orderby a.AttemptNumber
                select new { a.StartedAt, a.FinishedAt, a.Outcome, a.ErrorCode, a.ErrorMessage, a.ActorKind, a.ActorName, a.BatchId, a.Instance })
            .ToListAsync(ct);
        var lotes = intentos.Where(i => i.BatchId != null).Select(i => i.BatchId!.Value).Distinct().ToList();
        var numeros = lotes.Count == 0
            ? new Dictionary<int, long>()
            : await db.IntegrationBatches.AsNoTracking().Where(b => lotes.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Number, ct);

        var dependeDe = await Vecinos(db.IntegrationMessageDependencies.AsNoTracking().Where(d => d.MessageId == mensaje.Id).Select(d => d.DependsOnMessageId), entrega.Destination, ct);
        var dependientes = await Vecinos(db.IntegrationMessageDependencies.AsNoTracking().Where(d => d.DependsOnMessageId == mensaje.Id).Select(d => d.MessageId), entrega.Destination, ct);

        return Result.Success(new IntegrationMessageDetailDto(
            fila,
            mensaje.PayloadJson,
            mensaje.PayloadSha256,
            intentos.Select(i => new IntentoDelMensajeDto(i.StartedAt, i.FinishedAt, i.Outcome, i.ErrorCode, i.ErrorMessage,
                new ActorDelIntentoDto(i.ActorKind, i.ActorName), i.BatchId is int b && numeros.TryGetValue(b, out var n) ? n : null, i.Instance)).ToList(),
            dependeDe,
            dependientes));
    }

    private async Task<IReadOnlyList<VecinoDelMensajeDto>> Vecinos(IQueryable<long> ids, string destino, CancellationToken ct) =>
        await (
                from m in db.IntegrationMessages.AsNoTracking()
                where ids.Contains(m.Id)
                orderby m.Id
                select new VecinoDelMensajeDto(
                    m.PublicId,
                    m.Type,
                    db.IntegrationMessageDeliveries.Where(d => d.MessageId == m.Id && d.Destination == destino).Select(d => (DeliveryStatus?)d.Status).FirstOrDefault()))
            .ToListAsync(ct);
}

/// <summary>Lo común de la bandeja y su detalle: el alcance y la proyección a <see cref="IntegrationMessageDto"/>. (nuevo)</summary>
public static class VistaDeMensajes
{
    /// <summary>
    /// Las entregas que el alcance deja ver: con todas las bodegas, todas; si no, las de un documento de origen visible
    /// (<see cref="FiltroDeAlcance.DocumentosVisibles"/>).
    /// </summary>
    public static IQueryable<IntegrationMessageDelivery> Visibles(IApplicationDbContext db, Interfaces.Security.AlcanceDeInventario alcance)
    {
        var entregas = db.IntegrationMessageDeliveries.AsNoTracking();
        if (alcance.TodasLasBodegas) return entregas;
        var documentos = db.InventoryDocuments.AsNoTracking().DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());
        return entregas.Where(d => d.Message!.OriginKind == MessageOriginKind.Document && documentos.Any(doc => doc.PublicId == d.Message.OriginPublicId));
    }

    public static IQueryable<IntegrationMessageDelivery> PorDestinoYEstado(IQueryable<IntegrationMessageDelivery> consulta, string? destino, DeliveryStatus? estado)
    {
        if (!string.IsNullOrWhiteSpace(destino)) consulta = consulta.Where(d => d.Destination == destino);
        if (estado is DeliveryStatus e) consulta = consulta.Where(d => d.Status == e);
        return consulta;
    }

    public static async Task<IReadOnlyList<IntegrationMessageDto>> ProyectarAsync(
        IApplicationDbContext db, IReadOnlyList<IntegrationMessageDelivery> entregas, IEnumerable<IDestinoDeMensajes> destinos, CancellationToken ct)
    {
        if (entregas.Count == 0) return [];
        var conConsumidor = destinos.Select(d => d.Destino).ToHashSet(StringComparer.Ordinal);

        var loteIds = entregas.Where(e => e.BatchId != null).Select(e => e.BatchId!.Value).Distinct().ToList();
        var lotes = loteIds.Count == 0
            ? new Dictionary<int, IntegrationBatch>()
            : await db.IntegrationBatches.AsNoTracking().Where(b => loteIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);

        var mensajeIds = entregas.Select(e => e.MessageId).Distinct().ToList();
        var bloqueantes = EntregasElegibles.Bloqueantes.ToArray();
        var bloqueos = await (
                from dep in db.IntegrationMessageDependencies.AsNoTracking()
                where mensajeIds.Contains(dep.MessageId)
                join x in db.IntegrationMessageDeliveries.AsNoTracking() on dep.DependsOnMessageId equals x.MessageId
                where bloqueantes.Contains(x.Status)
                join m in db.IntegrationMessages.AsNoTracking() on x.MessageId equals m.Id
                orderby m.Id
                select new { dep.MessageId, x.Destination, m.PublicId, m.Type, x.Status })
            .ToListAsync(ct);

        return entregas.Select(e =>
        {
            var m = e.Message!;
            IntegrationBatch? lote = e.BatchId is int id && lotes.TryGetValue(id, out var l) ? l : null;
            return new IntegrationMessageDto(
                m.PublicId,
                m.EmittedAt,
                m.Type,
                m.Version,
                m.Kind,
                e.Destination,
                e.Status,
                e.Mode,
                e.ScheduleKey,
                lote is null ? null : new LoteDelMensajeDto(lote.PublicId, lote.Number),
                e.Attempts,
                e.NextAttemptAt,
                e.LastErrorCode is null && e.LastErrorMessage is null ? null : new ErrorDelMensajeDto(e.LastErrorCode, e.LastErrorMessage, e.LastErrorDataJson),
                e.ProcessedAt,
                Resultado(e, lote),
                new OrigenDelMensajeDto(m.OriginKind, m.OriginDocumentClass, m.OriginDocumentTypeCode, m.OriginNumber, m.OriginPublicId, m.OperationDate),
                m.RelatedPublicId is Guid r ? new RelacionadoDelMensajeDto(r, m.RelatedDocumentClass, m.RelatedNumber) : null,
                m.OriginUserName,
                m.PrevalidationOutcome,
                bloqueos.Where(b => b.MessageId == e.MessageId && b.Destination == e.Destination)
                    .Select(b => new BloqueoDelMensajeDto(b.PublicId, b.Type, b.Status)).ToList(),
                conConsumidor.Contains(e.Destination),
                null);
        }).ToList();
    }

    /// <summary>El resultado leído de la entrega: nunca de <c>ACC_</c> (FR-014).</summary>
    private static ResultadoDelMensajeDto? Resultado(IntegrationMessageDelivery e, IntegrationBatch? lote)
    {
        if (e.Status != DeliveryStatus.Processed && e.Status != DeliveryStatus.ValidationFailed) return null;
        var sinComprobante = ReferenciasDeResultado.SinComprobante(e.ResultReference);
        if (sinComprobante is not null) return new ResultadoDelMensajeDto(null, null, null, null, sinComprobante);
        if (!Guid.TryParse(e.ResultReference, out var comprobante)) return null;

        var resumido = lote is not null
                       && (lote.Granularity == PostingGranularity.Summarized
                           || ClavesDeLote.Leer(lote.ScheduleKey)?.GranularidadDelLote == PostingGranularity.Summarized);
        var ruta = resumido ? $"/contabilidad/inventario/lotes/{lote!.PublicId:D}" : $"/contabilidad/comprobantes/{comprobante:D}";
        return new ResultadoDelMensajeDto(e.ResultVoucherTypeCode, e.ResultVoucherNumber, comprobante, ruta, null);
    }
}
