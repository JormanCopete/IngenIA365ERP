using FluentValidation;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Consultas;

/// <summary>Una línea propuesta de un comprobante de Cartera: cuenta, sucursal, débito, crédito, tercero y cruce.</summary>
public sealed record LineaPropuestaDeCarteraDto(string AccountCode, string AccountName, string? Branch, decimal Debit, decimal Credit, string? ThirdParty, string? CrossDocument);

/// <summary>Un comprobante propuesto: el grupo (nulo si es por hecho), el tipo, la fecha contable, sus hechos y sus líneas con totales.</summary>
public sealed record ComprobantePropuestoDeCarteraDto(
    string? GroupKey,
    string VoucherTypeCode,
    DateOnly Date,
    IReadOnlyList<Guid> Facts,
    IReadOnlyList<LineaPropuestaDeCarteraDto> Lines,
    decimal Debit,
    decimal Credit);

/// <summary>Un hecho (o unidad) que quedaría fuera del lote, con sus hallazgos y quién los corrige.</summary>
public sealed record HechoExcluidoDeCarteraDto(Guid OriginPublicId, IReadOnlyList<Guid> Facts, string Type, string? Number, IReadOnlyList<HallazgoDeCarteraDto> Errors);

/// <summary>La vista previa de un lote de Cartera (§10.5): el corte (el <c>PublicId</c> del último hecho), los comprobantes propuestos y lo excluido.</summary>
public sealed record VistaPreviaDeLoteDeCarteraDto(
    Guid? CutoffMessagePublicId,
    IReadOnlyList<ComprobantePropuestoDeCarteraDto> Vouchers,
    IReadOnlyList<HechoExcluidoDeCarteraDto> Excluded);

/// <summary>
/// La vista previa de un lote de Cartera (feature 013, T0649; contracts/contabilidad.md §10.5; <c>POST /api/accounting/lending/batches/preview</c>,
/// <c>Accounting.LendingBatches.View</c>, la ruta es de la API). Con los hechos del alcance en orden de emisión arma cada unidad con el mismo
/// constructor del consumidor —la matriz, los tipos y las personas cargados una vez—, las valida todas con
/// <see cref="AccountingPoster.ValidarVariosAsync"/>, excluye las que fallan y a sus dependientes, y propone los comprobantes: uno por unidad, o
/// uno por grupo resumido (<see cref="AgrupadorDeResumidosDeCartera"/>, <see cref="PostLendingSummaryGroupCommandHandler.Resumen"/>), con la
/// fecha contable de sus hechos. Sólo los hechos de Cartera; los informativos no proponen comprobante. No numera, no agrega ni guarda. (nuevo)
/// </summary>
public sealed record PreviewLendingBatchQuery(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<VistaPreviaDeLoteDeCarteraDto>>;

public sealed class PreviewLendingBatchQueryValidator : AbstractValidator<PreviewLendingBatchQuery>
{
    public PreviewLendingBatchQueryValidator()
    {
        RuleFor(x => x.MessagePublicIds).NotNull();
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
    }
}

public sealed class PreviewLendingBatchQueryHandler(
    IApplicationDbContext db, IMensajesEntrantes entrantes, AccountingPoster poster, ResolutorDeReglasDeCartera resolutor,
    TiposDeComprobanteDeCartera tipos, IDateTimeService reloj)
    : IRequestHandler<PreviewLendingBatchQuery, Result<VistaPreviaDeLoteDeCarteraDto>>
{
    public async Task<Result<VistaPreviaDeLoteDeCarteraDto>> Handle(PreviewLendingBatchQuery request, CancellationToken ct)
    {
        if (request.MessagePublicIds.Count == 0) return Result.Success(new VistaPreviaDeLoteDeCarteraDto(null, [], []));
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Failure<VistaPreviaDeLoteDeCarteraDto>(leidos.Error);
        var mensajes = leidos.Value.Where(m => m.OriginModule == ModulosDeOrigen.Cartera).ToList();
        if (mensajes.Count == 0) return Result.Success(new VistaPreviaDeLoteDeCarteraDto(null, [], []));
        var corte = mensajes[^1].MessageId;

        var unidades = AgrupadorDeResumidosDeCartera.Unidades(mensajes, IntegrationDestinations.Accounting)
            .Where(u => !ConstructorDeLineasDeCartera.EsInformativa(u.Mensajes.Select(HechoDeCartera.De).ToList()))
            .ToList();
        var comoHechos = unidades.Select(u => (IReadOnlyList<HechoDeCartera>)u.Mensajes.Select(HechoDeCartera.De).ToList()).ToList();
        var catalogos = await CatalogosDeCartera.CargarAsync(db, resolutor, tipos, comoHechos, reloj.TodayUtc, ct);
        var construidas = unidades.Select((u, i) => (u.Unidad, u.Mensajes, C: ConstructorDeLineasDeCartera.Construir(comoHechos[i], catalogos))).ToList();

        var errores = new Dictionary<Guid, List<HallazgoDeCarteraDto>>();
        foreach (var x in construidas.Where(x => !x.C.EsValida))
            errores[x.Unidad.OriginPublicId] = x.C.Fallos.Select(f => new HallazgoDeCarteraDto(f.FactType, f.Role, f.Combination, f.AccountCode, f.Error.Code,
                f.Error.Message, QuienCorrigeEnCartera.De(f.Error.Code))).ToList();
        var conLineas = construidas.Where(x => x.C.EsValida && !x.C.SinLineas).ToList();
        var validaciones = await poster.ValidarVariosAsync(conLineas.Select(x => x.C.Request!).ToList(), ct);
        for (var i = 0; i < conLineas.Count; i++)
            if (!validaciones[i].EsValido)
                errores[conLineas[i].Unidad.OriginPublicId] = validaciones[i].Errores.Select(e => EvaluateLendingPostingQueryHandler.Hallazgo(e, conLineas[i].C)).ToList();

        bool cambio;
        do
        {
            cambio = false;
            foreach (var x in construidas)
            {
                if (errores.ContainsKey(x.Unidad.OriginPublicId)) continue;
                var original = ConsumoDeCartera.Originales(x.Mensajes).FirstOrDefault(errores.ContainsKey);
                if (original == Guid.Empty) continue;
                var espera = AccountingErrors.LendingFactWaitingForOriginal(original);
                errores[x.Unidad.OriginPublicId] = [new HallazgoDeCarteraDto(x.C.Principal.Tipo, null, null, null, espera.Code, espera.Message, QuienCorrigeEnCartera.De(espera.Code))];
                cambio = true;
            }
        } while (cambio);

        var incluidas = conLineas.Where(x => !errores.ContainsKey(x.Unidad.OriginPublicId)).ToList();
        var nombres = await NombresAsync(incluidas.Select(x => x.C).ToList(), ct);
        var comprobantes = new List<ComprobantePropuestoDeCarteraDto>();
        foreach (var x in incluidas.Where(x => AgrupadorDeResumidosDeCartera.ClaveDe(x.Mensajes) is null))
            comprobantes.Add(Propuesto(null, x.C.Request!, x.Mensajes.Select(m => m.MessageId).ToList(), nombres));
        foreach (var grupo in incluidas.Where(x => AgrupadorDeResumidosDeCartera.ClaveDe(x.Mensajes) is not null)
                     .GroupBy(x => (Clave: AgrupadorDeResumidosDeCartera.ClaveDe(x.Mensajes)!, Tipo: x.C.Tipo!.VoucherTypeCode)))
        {
            var resumen = PostLendingSummaryGroupCommandHandler.Resumen(grupo.Select(x => x.C).ToList(), Guid.Empty, null);
            comprobantes.Add(Propuesto(grupo.Key.Clave, resumen, grupo.SelectMany(x => x.Mensajes).Select(m => m.MessageId).ToList(), nombres));
        }

        var excluidos = construidas.Where(x => errores.ContainsKey(x.Unidad.OriginPublicId))
            .Select(x => new HechoExcluidoDeCarteraDto(x.Unidad.OriginPublicId, x.Mensajes.Select(m => m.MessageId).ToList(), x.C.Principal.Tipo,
                x.C.Principal.Sobre.Origin.Number, errores[x.Unidad.OriginPublicId]))
            .ToList();
        return Result.Success(new VistaPreviaDeLoteDeCarteraDto(corte, comprobantes, excluidos));
    }

    private static ComprobantePropuestoDeCarteraDto Propuesto(string? clave, PostingRequest request, IReadOnlyList<Guid> hechos, Nombres n)
    {
        var lineas = request.Lines.Select(l => new LineaPropuestaDeCarteraDto(
            l.AccountCode ?? string.Empty, n.Cuentas.GetValueOrDefault(l.AccountCode ?? string.Empty, string.Empty),
            l.BranchId is { } b ? n.Sucursales.GetValueOrDefault(b) : null, l.Debit, l.Credit,
            l.PersonId is { } p ? n.Personas.GetValueOrDefault(p) : null,
            l.CrossDocumentType is null ? null : $"{l.CrossDocumentType} {l.CrossDocumentNumber}")).ToList();
        return new ComprobantePropuestoDeCarteraDto(clave, request.VoucherTypeCode, request.Date, hechos, lineas, lineas.Sum(l => l.Debit), lineas.Sum(l => l.Credit));
    }

    private sealed record Nombres(IReadOnlyDictionary<string, string> Cuentas, IReadOnlyDictionary<int, string> Personas, IReadOnlyDictionary<int, string> Sucursales);

    private async Task<Nombres> NombresAsync(IReadOnlyList<ConstruccionDeCartera> construidas, CancellationToken ct)
    {
        var cuentas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in construidas.SelectMany(c => c.Mapa)) cuentas.TryAdd(l.AccountCode, l.AccountName);
        var idsDePersona = construidas.SelectMany(c => c.Request!.Lines).Where(l => l.PersonId is not null).Select(l => l.PersonId!.Value).Distinct().ToList();
        var personas = idsDePersona.Count == 0
            ? new Dictionary<int, string>()
            : await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => idsDePersona.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => (p.BusinessName ?? (p.FirstName + " " + p.LastName)).Trim(), ct);
        var sucursales = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        return new Nombres(cuentas, personas, sucursales);
    }
}

/// <summary>
/// La vista previa de un lote manual de Cartera por su alcance (§10.2, §10.5): las entregas <c>InBatch</c> sin lote de hechos <c>CAR</c> del
/// rango, el horario y la sucursal, en orden de emisión, delegadas en <see cref="PreviewLendingBatchQuery"/>. Lo que muestra es lo que el lote
/// manual (<c>OrderIntegrationBatchCommand</c> con <c>OriginModule = CAR</c> y el <c>cutoffMessagePublicId</c>) procesará. (nuevo)
/// </summary>
public sealed record PreviewLendingBatchScopeQuery(DateOnly From, DateOnly To, string? ScheduleKey, Guid? BranchPublicId)
    : IRequest<Result<VistaPreviaDeLoteDeCarteraDto>>;

public sealed class PreviewLendingBatchScopeQueryValidator : AbstractValidator<PreviewLendingBatchScopeQuery>
{
    public PreviewLendingBatchScopeQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.ScheduleKey).MaximumLength(60);
    }
}

public sealed class PreviewLendingBatchScopeQueryHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<PreviewLendingBatchScopeQuery, Result<VistaPreviaDeLoteDeCarteraDto>>
{
    public async Task<Result<VistaPreviaDeLoteDeCarteraDto>> Handle(PreviewLendingBatchScopeQuery request, CancellationToken ct)
    {
        var alcance = new AlcanceDeLote(request.From, request.To, null, request.ScheduleKey, request.BranchPublicId, ModulosDeOrigen.Cartera);
        var mensajes = await alcance.Entregas(db).AsNoTracking().Select(d => d.Message!.PublicId).ToListAsync(ct);
        return await sender.Send(new PreviewLendingBatchQuery(mensajes), ct);
    }
}

/// <summary><c>LendingPostingDto</c>: el recibo de un hecho de Cartera y su comprobante (§4.3, §21.1). Nunca el <c>Id</c> interno.</summary>
public sealed record LendingPostingDto(
    Guid MessagePublicId,
    string Type,
    int Version,
    Guid SourcePublicId,
    string? SourceDocumentClass,
    string? SourceDocumentNumber,
    string? LoanNumber,
    Guid? RelatedMessagePublicId,
    DateOnly OperationDate,
    Guid? AccountingDocumentPublicId,
    string? VoucherTypeCode,
    long? VoucherNumber,
    MotivoSinComprobante? WithoutVoucher,
    Guid? BatchPublicId,
    ActorKind OriginActorKind,
    string OriginUserName,
    string ActorName,
    DateTime ProcessedAt);

/// <summary>
/// Los recibos de los hechos de Cartera (feature 013, T0649; contracts/contabilidad.md §21.1; <c>GET /api/accounting/lending/postings?document=</c>
/// con <c>Accounting.Vouchers.View</c>): de un comprobante, de un origen, de un hecho, de un lote o de un rango de fechas contables, en orden de
/// proceso. En un resumido, los recibos de su comprobante son los hechos que reúne. (nuevo)
/// </summary>
public sealed record ListLendingPostingsQuery(
    Guid? Document = null,
    Guid? Source = null,
    Guid? Message = null,
    Guid? Batch = null,
    DateOnly? From = null,
    DateOnly? To = null,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<LendingPostingDto>>>;

public sealed class ListLendingPostingsQueryValidator : AbstractValidator<ListLendingPostingsQuery>
{
    public ListLendingPostingsQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListLendingPostingsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListLendingPostingsQuery, Result<PagedResult<LendingPostingDto>>>
{
    public async Task<Result<PagedResult<LendingPostingDto>>> Handle(ListLendingPostingsQuery request, CancellationToken ct)
    {
        var q = db.LendingPostings.AsNoTracking().Where(p => !p.IsDeleted);
        if (request.Document is { } documento) q = q.Where(p => p.AccountingDocument != null && p.AccountingDocument.PublicId == documento);
        if (request.Source is { } origen) q = q.Where(p => p.SourcePublicId == origen);
        if (request.Message is { } mensaje) q = q.Where(p => p.MessagePublicId == mensaje || p.RelatedMessagePublicId == mensaje);
        if (request.Batch is { } lote) q = q.Where(p => p.BatchPublicId == lote);
        if (request.From is { } desde) q = q.Where(p => p.OperationDate >= desde);
        if (request.To is { } hasta) q = q.Where(p => p.OperationDate <= hasta);

        var pagina = request.Pagina ?? new PageRequest();
        var total = await q.CountAsync(ct);
        var filas = await q.OrderBy(p => p.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Select(p => new
            {
                p.MessagePublicId, p.MessageType, p.MessageVersion, p.SourcePublicId, p.SourceDocumentClass, p.SourceDocumentNumber, p.LoanNumber,
                p.RelatedMessagePublicId, p.OperationDate, p.NoVoucherReason, p.BatchPublicId, p.OriginActorKind, p.OriginUserName, p.ActorName,
                p.ProcessedAt,
                Documento = p.AccountingDocument == null ? null : new { p.AccountingDocument.PublicId, Tipo = p.AccountingDocument.VoucherType!.Code, p.AccountingDocument.Number },
            })
            .ToListAsync(ct);
        var items = filas.Select(f => new LendingPostingDto(f.MessagePublicId, f.MessageType, f.MessageVersion, f.SourcePublicId, f.SourceDocumentClass,
                f.SourceDocumentNumber, f.LoanNumber, f.RelatedMessagePublicId, f.OperationDate, f.Documento?.PublicId, f.Documento?.Tipo, f.Documento?.Number,
                ConsumoDeCartera.MotivoDe(f.NoVoucherReason), f.BatchPublicId, f.OriginActorKind, f.OriginUserName, f.ActorName, f.ProcessedAt))
            .ToList();
        return Result.Success(new PagedResult<LendingPostingDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}
