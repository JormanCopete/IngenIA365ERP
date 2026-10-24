using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

// Feature 015, US3 (T0118, T0119, T0121; FR-044, FR-045, FR-177, FR-190): consultar recibos con alcance, su detalle (enmascarado con pagador tercero) y el
// modelo del documento —tirilla de 80 mm o carta, «copia» al reimprimir—. (nuevo)

/// <summary>Lo común del recibo. (nuevo)</summary>
public static class RecibosDeCaja
{
    /// <summary>Un número de producto enmascarado: sólo los últimos cuatro (FR-177).</summary>
    public static string? Enmascarar(string? numero) =>
        string.IsNullOrEmpty(numero) || numero.Length <= 4 ? numero : new string('•', numero.Length - 4) + numero[^4..];

    public static async Task<ReceiptDto> DtoAsync(IApplicationDbContext db, Receipt r, CancellationToken ct, bool enmascarar = false)
    {
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == r.PayerPersonId, ct);
        var efectua = r.DeliveredByPersonId is { } d
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == d).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var conceptos = await db.MiscConcepts.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var masked = enmascarar && r.PayerIsThirdParty;
        var aplicaciones = (await db.ReceiptApplications.AsNoTracking().Where(a => a.ReceiptId == r.Id).OrderBy(a => a.Ordinal).ToListAsync(ct))
            .Select(a => new AplicacionDelReciboDto(a.PublicId, a.Ordinal, a.Target, masked ? Enmascarar(a.ProductNumber) : a.ProductNumber, a.Amount, a.LoanModality,
                a.AccountModality, a.PrepaymentChoice, a.AdvanceNoticeAccepted, a.MiscConceptId is { } c ? conceptos.GetValueOrDefault(c) : null,
                a.ModuleVoucherNumber, a.FinalityUntil, a.Status)).ToList();
        var medios = await (from m in db.ReceiptMeans.AsNoTracking()
                            join p in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on m.PaymentMeansId equals p.Id
                            where m.ReceiptId == r.Id
                            orderby m.Ordinal
                            select new MedioDelReciboDto(m.PublicId, p.Code, m.Amount, m.Reference, m.Status)).ToListAsync(ct);
        return new ReceiptDto(r.PublicId, r.Number, r.Status, r.Channel, r.ValueDate, r.RegisteredAt, pagador.PublicId, OperacionesDeCaja.Nombre(pagador), efectua,
            r.PayerIsThirdParty, masked, r.TotalApplied, r.TotalMeans, r.ChangeGiven, r.VerificationCode, aplicaciones, medios);
    }

    /// <summary>El recibo, si su oficina o su caja está al alcance.</summary>
    public static async Task<Receipt?> VisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId, CancellationToken ct)
    {
        var r = await db.IssuedReceipts.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (r is null) return null;
        var mio = await alcance.ObtenerAsync(ct);
        var enCaja = r.CashBoxId is { } caja && r.CollectionPointId is { } punto && mio.IncluyeCaja(caja, punto);
        return enCaja || mio.IncluyeOficina(r.BranchId) ? r : null;
    }
}

/// <summary><c>GET /api/receipts/receipts?from=&amp;to=&amp;person=&amp;number=&amp;session=</c> (<c>Receipts.Receipt.View</c>): los recibos al alcance. (nuevo)</summary>
public sealed record ListReceiptsQuery(DateOnly? From = null, DateOnly? To = null, Guid? PersonPublicId = null, string? Number = null, Guid? SessionPublicId = null)
    : IRequest<Result<IReadOnlyList<ReceiptDto>>>;

public sealed class ListReceiptsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<ListReceiptsQuery, Result<IReadOnlyList<ReceiptDto>>>
{
    private const int Maximo = 200;

    public async Task<Result<IReadOnlyList<ReceiptDto>>> Handle(ListReceiptsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var persona = request.PersonPublicId is { } p
            ? await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;
        var sesion = request.SessionPublicId is { } s
            ? await db.ReceiptsCashSessions.AsNoTracking().Where(x => x.PublicId == s).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;
        var numero = request.Number?.Trim();
        var filas = await db.IssuedReceipts.AsNoTracking()
            .Where(r => (request.From == null || r.ValueDate >= request.From) && (request.To == null || r.ValueDate <= request.To)
                        && (persona == null || r.PayerPersonId == persona) && (sesion == null || r.CashSessionId == sesion)
                        && (string.IsNullOrEmpty(numero) || r.Number == numero))
            .OrderByDescending(r => r.Id).Take(Maximo * 2).ToListAsync(ct);
        var lista = new List<ReceiptDto>();
        foreach (var r in filas.Where(r => mio.IncluyeOficina(r.BranchId)
                     || (r.CashBoxId is { } caja && r.CollectionPointId is { } punto && mio.IncluyeCaja(caja, punto))).Take(Maximo))
            lista.Add(await RecibosDeCaja.DtoAsync(db, r, ct, enmascarar: true));
        return Result.Success<IReadOnlyList<ReceiptDto>>(lista);
    }
}

/// <summary><c>GET /api/receipts/receipts/{id}</c> (<c>Receipts.Receipt.View</c>): el detalle; con pagador tercero, enmascarado. (nuevo)</summary>
public sealed record GetReceiptQuery(Guid PublicId) : IRequest<Result<ReceiptDto>>;

public sealed class GetReceiptQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetReceiptQuery, Result<ReceiptDto>>
{
    public async Task<Result<ReceiptDto>> Handle(GetReceiptQuery request, CancellationToken ct) =>
        await RecibosDeCaja.VisibleAsync(db, alcance, request.PublicId, ct) is { } r
            ? Result.Success(await RecibosDeCaja.DtoAsync(db, r, ct, enmascarar: true))
            : Result.Failure<ReceiptDto>(ErroresDeRecaudo.NoEncontrado("El recibo"));
}

/// <summary>El formato del documento del recibo. (nuevo)</summary>
public enum FormatoDeRecibo
{
    Thermal = 1,
    Letter = 2,
}

/// <summary>Una línea del documento del recibo. (nuevo)</summary>
public sealed record LineaDelReciboModel(int Ordinal, string Description, string? Product, decimal Amount, string? ModuleVoucher, string? Choice, bool NoticeAccepted,
    DateOnly? FinalityUntil);

/// <summary>Un medio del documento del recibo, con lo que cubre de cada aplicación. (nuevo)</summary>
public sealed record MedioDelReciboModel(string Code, decimal Amount, string? Reference, IReadOnlyList<(int Aplicacion, decimal Valor)> Covers);

/// <summary>El modelo del documento del recibo (FR-044, FR-045, FR-177, FR-190). (nuevo)</summary>
public sealed record ReciboModel(
    EmpresaDelDocumentoDto Company,
    FormatoDeRecibo Format,
    bool Copy,
    string Number,
    string VerificationCode,
    string Office,
    string? CollectionPoint,
    string? CashBox,
    string? Cashier,
    DateOnly ValueDate,
    DateTime RegisteredAt,
    string Payer,
    string? DeliveredBy,
    bool PayerIsThirdParty,
    bool Voided,
    decimal TotalApplied,
    decimal TotalMeans,
    decimal ChangeGiven,
    string? SourceOfFunds,
    IReadOnlyList<LineaDelReciboModel> Lines,
    IReadOnlyList<MedioDelReciboModel> Means);

/// <summary><c>GET /api/receipts/receipts/{id}/document?format=thermal|letter&amp;copy=</c> (<c>Receipts.Receipt.View</c>): el modelo del documento. (nuevo)</summary>
public sealed record GetReceiptDocumentQuery(Guid PublicId, FormatoDeRecibo Format, bool Copy) : IRequest<Result<ReciboModel>>;

public sealed class GetReceiptDocumentQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetReceiptDocumentQuery, Result<ReciboModel>>
{
    public async Task<Result<ReciboModel>> Handle(GetReceiptDocumentQuery request, CancellationToken ct)
    {
        var r = await RecibosDeCaja.VisibleAsync(db, alcance, request.PublicId, ct);
        if (r is null) return Result.Failure<ReciboModel>(ErroresDeRecaudo.NoEncontrado("El recibo"));
        var dto = await RecibosDeCaja.DtoAsync(db, r, ct, enmascarar: true);
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == r.BranchId).Select(b => b.Name).FirstAsync(ct);
        string? punto = null, caja = null;
        if (r.CashBoxId is { } c) (punto, caja) = await DocumentosDeCajaDeRecaudo.PuntoYCajaAsync(db, c, ct);
        var cajero = r.CashierUserId is { } u ? await SesionesDeCaja.NombreAsync(db, u, ct) : null;
        var efectua = r.DeliveredByPersonId is { } d
            ? OperacionesDeCaja.Nombre(await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == d, ct))
            : null;
        var origen = r.SourceOfFundsDeclarationId is { } s
            ? await db.SourceOfFundsDeclarations.AsNoTracking().Where(x => x.Id == s).Select(x => x.Origin).FirstOrDefaultAsync(ct)
            : null;
        var aplicaciones = await db.ReceiptApplications.AsNoTracking().Where(a => a.ReceiptId == r.Id).ToListAsync(ct);
        var ids = aplicaciones.Select(a => a.Id).ToList();
        var porciones = await db.ReceiptApplicationPortions.AsNoTracking().Where(p => ids.Contains(p.ReceiptApplicationId)).ToListAsync(ct);
        var medios = await db.ReceiptMeans.AsNoTracking().Where(m => m.ReceiptId == r.Id).OrderBy(m => m.Ordinal).ToListAsync(ct);
        var lineas = dto.Applications.Select(a => new LineaDelReciboModel(a.Ordinal, Descripcion(a), a.ProductNumber, a.Amount, a.ModuleVoucherNumber,
            a.Choice switch { 1 => "Reducir el plazo", 2 => "Reducir la cuota", _ => null }, a.AdvanceNoticeAccepted, a.FinalityUntil)).ToList();
        var mediosDelDocumento = medios.Select((m, i) => new MedioDelReciboModel(dto.Means[i].MeansCode, m.Amount, m.Reference,
            porciones.Where(p => p.ReceiptMeansId == m.Id)
                .Select(p => (aplicaciones.First(a => a.Id == p.ReceiptApplicationId).Ordinal, p.Amount)).ToList())).ToList();
        return Result.Success(new ReciboModel(await DocumentosDeCajaDeRecaudo.EmpresaAsync(db, ct), request.Format, request.Copy, r.Number, r.VerificationCode,
            oficina, punto, caja, cajero, r.ValueDate, r.RegisteredAt, dto.PayerName, efectua, r.PayerIsThirdParty, r.Status == ReceiptState.Voided, r.TotalApplied,
            r.TotalMeans, r.ChangeGiven, origen, lineas, mediosDelDocumento));
    }

    private static string Descripcion(AplicacionDelReciboDto a) => a.Target switch
    {
        ApplicationTarget.Loan => "Pago de crédito",
        ApplicationTarget.Account => "Consignación",
        ApplicationTarget.MiscConcept => $"Concepto {a.MiscConceptCode}",
        _ => a.Target.ToString(),
    };
}
