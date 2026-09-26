using FluentValidation;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Consultas;

/// <summary>
/// La vista previa de un lote (feature 012, T517; contracts/contabilidad.md §5.5; api.md §26.4; FR-077), detrás de
/// <see cref="IContabilidadParaInventario.PrevisualizarLoteAsync"/>. Con los mensajes del alcance en orden de emisión arma cada
/// documento con el mismo constructor del consumidor, los valida todos con <see cref="AccountingPoster.ValidarVariosAsync"/>,
/// excluye los que fallan y a sus relacionados del lote, y propone los comprobantes: uno por documento, o uno por grupo resumido
/// (<see cref="AgrupadorDeResumidos"/>, <see cref="PostInventorySummaryGroupCommandHandler.Resumen"/>). Todo comprobante lleva
/// la fecha de sus documentos. <c>cutoffMessagePublicId</c> es el <c>PublicId</c> del último mensaje: el <c>Id</c> interno no
/// sale. No numera, no agrega ni guarda nada. (nuevo)
/// </summary>
public sealed record PreviewInventoryBatchQuery(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<VistaPreviaDeLoteDto>>;

public sealed class PreviewInventoryBatchQueryValidator : AbstractValidator<PreviewInventoryBatchQuery>
{
    public PreviewInventoryBatchQueryValidator()
    {
        RuleFor(x => x.MessagePublicIds).NotNull();
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
    }
}

public sealed class PreviewInventoryBatchQueryHandler(
    IApplicationDbContext db, IMensajesEntrantes entrantes, AccountingPoster poster, ResolutorDeReglas resolutor,
    TiposDeComprobanteDeInventario tipos, IDateTimeService reloj)
    : IRequestHandler<PreviewInventoryBatchQuery, Result<VistaPreviaDeLoteDto>>
{
    public async Task<Result<VistaPreviaDeLoteDto>> Handle(PreviewInventoryBatchQuery request, CancellationToken ct)
    {
        if (request.MessagePublicIds.Count == 0) return Result.Success(new VistaPreviaDeLoteDto(null, [], [], []));
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Failure<VistaPreviaDeLoteDto>(leidos.Error);
        var mensajes = leidos.Value;
        var corte = mensajes[^1].MessageId;

        var unidades = AgrupadorDeResumidos.Unidades(mensajes, IntegrationDestinations.Accounting)
            .Where(u => !ConstructorDeLineasDeInventario.EsInformativa(u.Mensajes.Select(MensajeDeUnidad.De).ToList()))
            .ToList();
        var comoMensajes = unidades.Select(u => (IReadOnlyList<MensajeDeUnidad>)u.Mensajes.Select(MensajeDeUnidad.De).ToList()).ToList();
        var catalogos = await CatalogosDelConstructor.CargarAsync(db, resolutor, tipos, comoMensajes, reloj.TodayUtc, ct);
        var construidas = unidades.Select((u, i) => (u.Unidad, u.Mensajes, C: ConstructorDeLineasDeInventario.Construir(comoMensajes[i], catalogos))).ToList();

        // Documento por documento: los que fallan quedan fuera con sus errores y quién los corrige.
        var errores = new Dictionary<Guid, List<ErrorDeExclusionDto>>();
        foreach (var x in construidas.Where(x => !x.C.EsValida))
            errores[x.Unidad.OriginPublicId] = x.C.Fallos.Select(f => new ErrorDeExclusionDto(f.DocumentLines.FirstOrDefault() is var l && l > 0 ? l : null,
                f.AccountCode, f.Error.Code, QuienCorrige.De(f.Error.Code, x.C.Principal.Sobre.Origin.DocumentClass, f.DeLaCuenta))).ToList();
        var conLineas = construidas.Where(x => x.C.EsValida && !x.C.ValorCero).ToList();
        var validaciones = await poster.ValidarVariosAsync(conLineas.Select(x => x.C.Request!).ToList(), ct);
        for (var i = 0; i < conLineas.Count; i++)
        {
            if (validaciones[i].EsValido) continue;
            var c = conLineas[i].C;
            errores[conLineas[i].Unidad.OriginPublicId] = validaciones[i].Errores.Select(e =>
            {
                var h = EvaluateInventoryPostingQueryHandler.Hallazgo(e, c, c.Principal.Sobre.Origin.DocumentClass);
                return new ErrorDeExclusionDto(h.DocumentLines.FirstOrDefault() is var l && l > 0 ? l : null, h.AccountCode, h.Rule, h.WhoFixes);
            }).ToList();
        }

        // Sus relacionados del lote también quedan fuera: esperan al original.
        bool cambio;
        do
        {
            cambio = false;
            foreach (var x in construidas)
            {
                if (errores.ContainsKey(x.Unidad.OriginPublicId)) continue;
                var original = ConsumoDeInventario.Originales(x.Mensajes).FirstOrDefault(errores.ContainsKey);
                if (original == Guid.Empty) continue;
                var espera = AccountingErrors.InventoryMessageWaitingForOriginal(original);
                errores[x.Unidad.OriginPublicId] = [new ErrorDeExclusionDto(null, null, espera.Code, null)];
                cambio = true;
            }
        } while (cambio);

        var incluidas = conLineas.Where(x => !errores.ContainsKey(x.Unidad.OriginPublicId)).ToList();
        var nombres = await NombresAsync(construidas.Select(x => x.C).ToList(), ct);

        var comprobantes = new List<ComprobantePropuestoDto>();
        foreach (var x in incluidas.Where(x => !AgrupadorDeResumidos.EsResumida(x.Mensajes)))
            comprobantes.Add(Propuesto(x.C.Request!, x.C.Principal.Sobre, PostingGranularity.PerDocument, 1, nombres));
        foreach (var grupo in incluidas.Where(x => AgrupadorDeResumidos.EsResumida(x.Mensajes)).GroupBy(x => AgrupadorDeResumidos.ClaveDe(x.Mensajes)))
        {
            var cs = grupo.Select(x => x.C).ToList();
            var resumen = PostInventorySummaryGroupCommandHandler.Resumen(cs, Guid.Empty, null, nombres.Sucursal(cs[0].Principal.Sobre.BranchPublicId), cs[0].Fecha);
            comprobantes.Add(Propuesto(resumen, cs[0].Principal.Sobre, PostingGranularity.Summarized, cs.Count, nombres));
        }

        var documentos = construidas
            .Select(x => x.C.Principal.Sobre)
            .Select((s, i) => new DocumentoDeLoteDto(s.Origin.PublicId, s.Origin.DocumentClass?.ToString(), s.Origin.DocumentTypeCode, s.Origin.Number,
                construidas[i].C.Fecha, nombres.Sucursal(s.BranchPublicId), construidas[i].C.TotalDebito))
            .ToList();
        var excluidos = construidas.Where(x => errores.ContainsKey(x.Unidad.OriginPublicId))
            .Select(x => new DocumentoExcluidoDto(x.Unidad.OriginPublicId, x.C.Principal.Sobre.Origin.Number, errores[x.Unidad.OriginPublicId]))
            .ToList();
        return Result.Success(new VistaPreviaDeLoteDto(corte, documentos, comprobantes, excluidos));
    }

    private static ComprobantePropuestoDto Propuesto(
        PostingRequest request, Common.Integration.Contracts.Inventory.IntegrationEnvelopeV1 sobre, PostingGranularity granularidad, int documentos, Nombres n)
    {
        var lineas = request.Lines.Select(l => new LineaPropuestaDto(
            new CuentaPropuestaDto(l.AccountCode ?? string.Empty, n.Cuentas.GetValueOrDefault(l.AccountCode ?? string.Empty, string.Empty)),
            l.Debit, l.Credit,
            l.PersonId is { } p ? n.Personas.GetValueOrDefault(p) : null,
            l.CrossDocumentType is null ? null : $"{l.CrossDocumentType} {l.CrossDocumentNumber}",
            l.TaxBase)).ToList();
        return new ComprobantePropuestoDto(request.VoucherTypeCode, request.Date, n.Sucursal(sobre.BranchPublicId),
            sobre.CostCenterPublicId is { } c ? n.Centros.GetValueOrDefault(c) : null, granularidad, documentos, lineas,
            new TotalesPropuestosDto(lineas.Sum(l => l.Debit), lineas.Sum(l => l.Credit)));
    }

    private sealed record Nombres(
        IReadOnlyDictionary<string, string> Cuentas, IReadOnlyDictionary<int, string> Personas,
        IReadOnlyDictionary<Guid, string> Sucursales, IReadOnlyDictionary<Guid, string> Centros)
    {
        public string? Sucursal(Guid publicId) => Sucursales.GetValueOrDefault(publicId);
    }

    private async Task<Nombres> NombresAsync(IReadOnlyList<ConstruccionDeUnidad> construidas, CancellationToken ct)
    {
        var cuentas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in construidas.SelectMany(c => c.Mapa)) cuentas.TryAdd(l.AccountCode, l.AccountName);
        var idsDePersona = construidas.Where(c => c.Request is not null).SelectMany(c => c.Request!.Lines)
            .Where(l => l.PersonId is not null).Select(l => l.PersonId!.Value).Distinct().ToList();
        var personas = idsDePersona.Count == 0
            ? new Dictionary<int, string>()
            : (await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => idsDePersona.Contains(p.Id))
                .Select(p => new { p.Id, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName }).ToListAsync(ct))
                .ToDictionary(p => p.Id, p => PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName));
        var sucursales = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.PublicId, b => b.Name, ct);
        var centros = await db.CostCenters.AsNoTracking().ToDictionaryAsync(c => c.PublicId, c => c.Name, ct);
        return new Nombres(cuentas, personas, sucursales, centros);
    }
}
