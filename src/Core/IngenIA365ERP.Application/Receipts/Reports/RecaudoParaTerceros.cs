using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Reports;

/// <summary>
/// Lo recaudado para un tercero en un concepto y un documento cruce (el período), con cuántos recibos: lo que se liquida al tercero desde Tesorería. (nuevo)
/// </summary>
public sealed record RecaudoParaTercerosDto(Guid ThirdPartyPublicId, string ThirdPartyName, string ThirdPartyTaxId, string ConceptCode, string ConceptName,
    string CrossDocument, int Receipts, decimal Collected, DateOnly FirstDate, DateOnly LastDate);

/// <summary>Un pago recibido para un tercero: el recibo, quién pagó y cuánto. (nuevo)</summary>
public sealed record PagoParaTerceroDto(DateOnly Date, string ReceiptNumber, Guid PayerPublicId, string PayerName, string PayerTaxId, string ConceptCode,
    Guid ThirdPartyPublicId, string ThirdPartyName, string CrossDocument, decimal Amount);

/// <summary>
/// <c>GET /api/receipts/third-party-collections?from=&amp;to=&amp;thirdParty=</c> (<c>Receipts.Reports.View</c>; 2026-11-23, pedido del dueño, el «solidario»): lo
/// recibido con los conceptos varios marcados «recaudo para terceros» (<c>MiscConcept.CollectedForThirdParty</c>) en recibos vigentes del rango, por tercero,
/// concepto y documento cruce, con sus pagos. Al alcance de oficinas de la persona. Lo que se le paga al tercero lo dice Tesorería (cuentas por pagar por tercero y
/// documento cruce, que la matriz lleva a la cuenta del concepto); la pantalla «Recaudo para terceros» junta las dos cosas y liquida con una orden de pago. (nuevo)
/// </summary>
public sealed record ListThirdPartyCollectionsQuery(DateOnly? From = null, DateOnly? To = null, Guid? ThirdPartyPublicId = null)
    : IRequest<Result<RecaudoParaTercerosResultado>>;

public sealed record RecaudoParaTercerosResultado(DateOnly From, DateOnly To, IReadOnlyList<RecaudoParaTercerosDto> Summary, IReadOnlyList<PagoParaTerceroDto> Payments);

public sealed class ListThirdPartyCollectionsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<ListThirdPartyCollectionsQuery, Result<RecaudoParaTercerosResultado>>
{
    public async Task<Result<RecaudoParaTercerosResultado>> Handle(ListThirdPartyCollectionsQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var hasta = request.To ?? hoy;
        var desde = request.From ?? new DateOnly(hasta.Year, hasta.Month, 1);
        if (desde > hasta) return Result.Failure<RecaudoParaTercerosResultado>(new Error("Receipts.Report.InvalidRange", "La fecha inicial es posterior a la final."));
        var pagos = await PagosAsync(db, await alcance.ObtenerAsync(ct), desde, hasta, request.ThirdPartyPublicId, ct);
        var resumen = pagos.GroupBy(p => (p.ThirdPartyPublicId, p.ConceptCode, p.CrossDocument))
            .Select(g => new RecaudoParaTercerosDto(g.Key.ThirdPartyPublicId, g.First().ThirdPartyName, g.First().ThirdPartyTaxIdOculto, g.Key.ConceptCode,
                g.First().ConceptName, g.Key.CrossDocument, g.Select(p => p.ReceiptNumber).Distinct().Count(), g.Sum(p => p.Amount), g.Min(p => p.Date), g.Max(p => p.Date)))
            .OrderBy(r => r.ThirdPartyName, StringComparer.CurrentCulture).ThenBy(r => r.ConceptCode, StringComparer.Ordinal).ThenBy(r => r.CrossDocument, StringComparer.Ordinal)
            .ToList();
        return Result.Success(new RecaudoParaTercerosResultado(desde, hasta, resumen, pagos.Select(p => p.Dto).ToList()));
    }

    internal sealed record Pago(PagoParaTerceroDto Dto, string ConceptName, string ThirdPartyTaxIdOculto, int BranchId)
    {
        public Guid ThirdPartyPublicId => Dto.ThirdPartyPublicId;
        public string ThirdPartyName => Dto.ThirdPartyName;
        public string ConceptCode => Dto.ConceptCode;
        public string CrossDocument => Dto.CrossDocument;
        public string ReceiptNumber => Dto.ReceiptNumber;
        public DateOnly Date => Dto.Date;
        public decimal Amount => Dto.Amount;
    }

    /// <summary>Los pagos para terceros de recibos vigentes del rango, al alcance de oficinas. Lo usan la consulta y la vista del centro de informes.</summary>
    internal static async Task<IReadOnlyList<Pago>> PagosAsync(IApplicationDbContext db, AlcanceDeRecaudo alcance, DateOnly desde, DateOnly hasta,
        Guid? tercero, CancellationToken ct)
    {
        var conceptos = await db.MiscConcepts.AsNoTracking().IgnoreQueryFilters().Where(c => c.CollectedForThirdParty)
            .ToDictionaryAsync(c => c.Id, c => (c.Code, c.Name), ct);
        if (conceptos.Count == 0) return [];
        var ids = conceptos.Keys.ToList();
        var filas = await (from a in db.ReceiptApplications.AsNoTracking()
                           join r in db.IssuedReceipts.AsNoTracking() on a.ReceiptId equals r.Id
                           where a.MiscConceptId != null && ids.Contains(a.MiscConceptId.Value) && a.Status == ApplicationState.Applied
                                 && r.Status == ReceiptState.Valid && r.ValueDate >= desde && r.ValueDate <= hasta
                           select new { a.MiscConceptId, a.BeneficiaryPersonId, a.CrossDocumentReference, a.Amount, r.Number, r.ValueDate, r.PayerPersonId, r.BranchId })
            .ToListAsync(ct);
        filas = filas.Where(f => alcance.IncluyeOficina(f.BranchId)).ToList();
        var personas = filas.Select(f => f.BeneficiaryPersonId).Concat(filas.Select(f => f.PayerPersonId)).Distinct().ToList();
        var deLasPersonas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, Nombre = (p.BusinessName ?? (p.FirstName + " " + p.LastName)).Trim(), p.TaxId })
            .ToDictionaryAsync(p => p.Id, ct);
        return filas
            .Where(f => tercero is null || deLasPersonas.GetValueOrDefault(f.BeneficiaryPersonId)?.PublicId == tercero)
            .OrderBy(f => f.ValueDate).ThenBy(f => f.Number, StringComparer.Ordinal)
            .Select(f =>
            {
                var t = deLasPersonas.GetValueOrDefault(f.BeneficiaryPersonId);
                var p = deLasPersonas.GetValueOrDefault(f.PayerPersonId);
                var (codigo, nombre) = conceptos[f.MiscConceptId!.Value];
                return new Pago(new PagoParaTerceroDto(f.ValueDate, f.Number, p?.PublicId ?? Guid.Empty, p?.Nombre ?? string.Empty, p?.TaxId ?? string.Empty, codigo,
                    t?.PublicId ?? Guid.Empty, t?.Nombre ?? string.Empty, f.CrossDocumentReference ?? f.Number, f.Amount), nombre, t?.TaxId ?? string.Empty, f.BranchId);
            }).ToList();
    }
}

// 2026-11-23 (pedido del dueño): la vista «Recaudo para terceros» del centro de informes de Recaudo: cada pago recibido para un tercero en el rango, con
// quién pagó, el concepto y el documento cruce, y el total. La pantalla /recaudo/recaudo-para-terceros lo resume y liquida al tercero.
public sealed partial class ReceiptsReportQueryHandler
{
    private async Task<TablaExportable> RecaudoParaTercerosAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var pagos = (await ListThirdPartyCollectionsQueryHandler.PagosAsync(db, a.Alcance, desde, hasta, null, ct))
            .Where(p => a.Oficina is null || p.BranchId == a.Oficina).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Fecha", TipoDeColumna.Fecha), new("Recibo"), new("Pagó"), new("Documento"), new("Concepto"), new("Tercero"), new("Documento cruce"),
            new("Valor", TipoDeColumna.Moneda), new("_tercero", Clave: "_tercero"),
        };
        var filas = pagos.Select(p => new FilaExportable([p.Dto.Date, p.Dto.ReceiptNumber, p.Dto.PayerName, p.Dto.PayerTaxId, p.Dto.ConceptCode, p.Dto.ThirdPartyName,
            p.Dto.CrossDocument, p.Dto.Amount, p.Dto.ThirdPartyPublicId])).ToList();
        return new TablaExportable("Recaudo para terceros", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", null, null, null, null, null, null, pagos.Sum(p => p.Dto.Amount), null]), []);
    }
}
