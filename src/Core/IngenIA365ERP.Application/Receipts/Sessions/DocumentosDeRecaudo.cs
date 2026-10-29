using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Sessions;

// Feature 015, US2 (T0096; FR-033, FR-156): los documentos impresos de la caja de Recaudo —el cierre de la sesión, el cierre del día del punto y el acta del
// arqueo sorpresivo—, cada uno con su número y su código de verificación. Application arma el modelo y la API lo pinta con QuestPDF por este puerto
// (Application no conoce QuestPDF). (nuevo)

/// <summary>Pinta los documentos de la caja de Recaudo. (nuevo)</summary>
public interface IDocumentosDeRecaudoEnPdf
{
    byte[] CierreDeSesion(CierreDeSesionModel modelo);

    byte[] CierreDelDia(CierreDelDiaModel modelo);

    byte[] Acta(ActaDeArqueoModel modelo);

    /// <summary>El recibo de caja en tirilla de 80 mm o carta (fase 6, T0118).</summary>
    byte[] Recibo(CashDesk.ReciboModel modelo);

    /// <summary>El documento de anulación de un recibo (fase 10, T0179).</summary>
    byte[] Anulacion(Voidings.AnulacionModel modelo);

    /// <summary>Feature 015, R1 (T0374): el egreso de caja firmado. (nuevo)</summary>
    byte[] Egreso(Disbursements.EgresoModel modelo);
}

/// <summary>La empresa del encabezado. (nuevo)</summary>
public sealed record EmpresaDelDocumentoDto(string Name, string Nit);

/// <summary>Una línea del arqueo en el documento. (nuevo)</summary>
public sealed record LineaDelDocumentoDto(string MeansCode, string MeansName, decimal Expected, decimal Counted, decimal Difference, decimal Tolerance,
    string Treatment, string? Reason, bool Approved);

/// <summary>El cierre de una sesión. (nuevo)</summary>
public sealed record CierreDeSesionModel(EmpresaDelDocumentoDto Company, string Number, string VerificationCode, string CollectionPoint, string CashBox,
    string Cashier, DateOnly BusinessDate, DateTime OpenedAt, DateTime? ClosedAt, decimal OpeningBase, bool Blind, bool Deferred, string? Witness,
    string State, IReadOnlyList<LineaDelDocumentoDto> Lines);

/// <summary>El cierre del día del punto. (nuevo)</summary>
public sealed record CierreDelDiaModel(EmpresaDelDocumentoDto Company, string Number, string VerificationCode, string CollectionPoint, DateOnly BusinessDate,
    int Sessions, int Receipts, int Voidings, string ClosedBy, bool ByDelegate, DateTime ClosedAt, IReadOnlyList<DayCloseLineDto> Lines);

/// <summary>El acta de un arqueo sorpresivo. (nuevo)</summary>
public sealed record ActaDeArqueoModel(EmpresaDelDocumentoDto Company, string Number, string VerificationCode, string Title, string CollectionPoint,
    string CashBox, string Cashier, string CountedBy, string? Witness, DateTime CountedAt, IReadOnlyList<LineaDelDocumentoDto> Lines);

/// <summary>Lo común de los documentos. (nuevo)</summary>
internal static class DocumentosDeCajaDeRecaudo
{
    public static async Task<EmpresaDelDocumentoDto> EmpresaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var e = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id)
            .Select(c => new { c.Name, c.TaxId, c.TaxIdCheckDigit }).FirstOrDefaultAsync(ct);
        return e is null ? new(string.Empty, string.Empty) : new(e.Name, e.TaxIdCheckDigit is { Length: > 0 } dv ? $"{e.TaxId}-{dv}" : e.TaxId);
    }

    public static async Task<IReadOnlyList<LineaDelDocumentoDto>> LineasAsync(IApplicationDbContext db, int arqueoId, CancellationToken ct) =>
        await (from l in db.ReceiptsCashCountLines.AsNoTracking()
               join m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on l.PaymentMeansId equals m.Id
               where l.CashCountId == arqueoId
               orderby l.Id
               select new LineaDelDocumentoDto(m.Code, m.Name, l.Expected, l.Counted, l.Difference, l.Tolerance, l.Treatment.ToString(), l.Reason,
                   l.ApprovedAt != null)).ToListAsync(ct);

    public static async Task<(string Punto, string Caja)> PuntoYCajaAsync(IApplicationDbContext db, int cajaId, CancellationToken ct)
    {
        var r = await (from c in db.CashBoxes.AsNoTracking()
                       join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                       where c.Id == cajaId
                       select new { Punto = p.Code + " · " + p.Name, Caja = c.Code + " · " + c.Name }).FirstAsync(ct);
        return (r.Punto, r.Caja);
    }
}

/// <summary><c>GET /api/receipts/sessions/{id}/close-report</c> (<c>Receipts.Session.View</c>): el modelo del cierre de la sesión. (nuevo)</summary>
public sealed record GetSessionCloseReportQuery(Guid SessionPublicId) : IRequest<Result<CierreDeSesionModel>>;

public sealed class GetSessionCloseReportQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<GetSessionCloseReportQuery, Result<CierreDeSesionModel>>
{
    public async Task<Result<CierreDeSesionModel>> Handle(GetSessionCloseReportQuery request, CancellationToken ct)
    {
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<CierreDeSesionModel>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var arqueo = await db.ReceiptsCashCounts.AsNoTracking().Where(c => c.CashSessionId == sesion.Id && c.Kind != CashCountKind.Surprise)
            .OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);
        if (arqueo is null) return Result.Failure<CierreDeSesionModel>(ErroresDeRecaudo.SesionNoCerrada());
        var (punto, caja) = await DocumentosDeCajaDeRecaudo.PuntoYCajaAsync(db, sesion.CashBoxId, ct);
        var dia = await db.CollectionPointDays.AsNoTracking().Where(d => d.Id == sesion.CollectionPointDayId).Select(d => d.BusinessDate).FirstAsync(ct);
        var testigo = arqueo.WitnessUserId is { } t ? await SesionesDeCaja.NombreAsync(db, t, ct) : null;
        return Result.Success(new CierreDeSesionModel(await DocumentosDeCajaDeRecaudo.EmpresaAsync(db, ct), sesion.PublicId.ToString("N")[..12].ToUpperInvariant(),
            arqueo.VerificationCode, punto, caja, await SesionesDeCaja.NombreAsync(db, sesion.CashierUserId, ct), dia, sesion.OpenedAt, sesion.ClosedAt,
            sesion.OpeningBase, arqueo.IsBlind, arqueo.Kind == CashCountKind.Deferred, testigo, sesion.Status.ToString(),
            await DocumentosDeCajaDeRecaudo.LineasAsync(db, arqueo.Id, ct)));
    }
}

/// <summary><c>GET /api/receipts/collection-points/{id}/days/{date}/report</c> (<c>Receipts.Session.View</c>): el modelo del cierre del día. (nuevo)</summary>
public sealed record GetDayCloseReportQuery(Guid CollectionPointPublicId, DateOnly BusinessDate) : IRequest<Result<CierreDelDiaModel>>;

public sealed class GetDayCloseReportQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetDayCloseReportQuery, Result<CierreDelDiaModel>>
{
    public async Task<Result<CierreDelDiaModel>> Handle(GetDayCloseReportQuery request, CancellationToken ct)
    {
        var punto = await Setup.AlcanceDePuntos.PuntoVisibleAsync(db, alcance, request.CollectionPointPublicId, seguir: false, ct);
        if (punto is null) return Result.Failure<CierreDelDiaModel>(ErroresDeRecaudo.NoEncontrado("El punto de recaudo"));
        var cierre = await (from c in db.ReceiptsDayCloses.AsNoTracking()
                            join d in db.CollectionPointDays.AsNoTracking() on c.CollectionPointDayId equals d.Id
                            where d.CollectionPointId == punto.Id && d.BusinessDate == request.BusinessDate
                            select c).FirstOrDefaultAsync(ct);
        if (cierre is null) return Result.Failure<CierreDelDiaModel>(ErroresDeRecaudo.NoEncontrado("El cierre del día"));
        var dto = await CierresDelDia.DtoAsync(db, cierre, ct);
        return Result.Success(new CierreDelDiaModel(await DocumentosDeCajaDeRecaudo.EmpresaAsync(db, ct),
            $"{punto.Code}-{request.BusinessDate:yyyyMMdd}", cierre.VerificationCode, $"{punto.Code} · {punto.Name}", request.BusinessDate, cierre.SessionCount,
            cierre.ReceiptCount, cierre.VoidingCount, await SesionesDeCaja.NombreAsync(db, cierre.ClosedByUserId, ct), dto.ByDelegate, cierre.ClosedAt, dto.Lines));
    }
}

/// <summary><c>GET /api/receipts/surprise-counts/{id}/minutes</c> (<c>Receipts.Session.View</c>): el acta de un arqueo sorpresivo. (nuevo)</summary>
public sealed record GetSurpriseCountMinutesQuery(Guid CountPublicId) : IRequest<Result<ActaDeArqueoModel>>;

public sealed class GetSurpriseCountMinutesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<GetSurpriseCountMinutesQuery, Result<ActaDeArqueoModel>>
{
    public async Task<Result<ActaDeArqueoModel>> Handle(GetSurpriseCountMinutesQuery request, CancellationToken ct)
    {
        var arqueo = await db.ReceiptsCashCounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.CountPublicId && c.CashSessionId != null, ct);
        CashSession? sesion = null;
        if (arqueo is not null)
        {
            var sesionPublicId = await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.Id == arqueo.CashSessionId).Select(s => s.PublicId).FirstAsync(ct);
            sesion = await SesionesDeCaja.VisibleAsync(db, alcance, sesionPublicId, seguir: false, ct);
        }
        if (arqueo is null || sesion is null) return Result.Failure<ActaDeArqueoModel>(ErroresDeRecaudo.NoEncontrado("El arqueo"));
        var (punto, caja) = await DocumentosDeCajaDeRecaudo.PuntoYCajaAsync(db, sesion.CashBoxId, ct);
        var testigo = arqueo.WitnessUserId is { } t ? await SesionesDeCaja.NombreAsync(db, t, ct) : null;
        var titulo = arqueo.Kind == CashCountKind.Surprise ? "Acta de arqueo sorpresivo" : "Acta de arqueo diferido";
        return Result.Success(new ActaDeArqueoModel(await DocumentosDeCajaDeRecaudo.EmpresaAsync(db, ct), arqueo.PublicId.ToString("N")[..12].ToUpperInvariant(),
            arqueo.VerificationCode, titulo, punto, caja, await SesionesDeCaja.NombreAsync(db, sesion.CashierUserId, ct),
            await SesionesDeCaja.NombreAsync(db, arqueo.CountedByUserId, ct), testigo, arqueo.CountedAt, await DocumentosDeCajaDeRecaudo.LineasAsync(db, arqueo.Id, ct)));
    }
}

/// <summary><c>GET /api/receipts/sessions/{id}/counts</c> (<c>Receipts.Session.View</c>): los arqueos de la sesión, el último primero. (nuevo)</summary>
public sealed record ListSessionCountsQuery(Guid SessionPublicId) : IRequest<Result<IReadOnlyList<CashCountDto>>>;

public sealed class ListSessionCountsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListSessionCountsQuery, Result<IReadOnlyList<CashCountDto>>>
{
    public async Task<Result<IReadOnlyList<CashCountDto>>> Handle(ListSessionCountsQuery request, CancellationToken ct)
    {
        var sesion = await SesionesDeCaja.VisibleAsync(db, alcance, request.SessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<IReadOnlyList<CashCountDto>>(ErroresDeRecaudo.NoEncontrado("La sesión"));
        var arqueos = await db.ReceiptsCashCounts.AsNoTracking().Where(c => c.CashSessionId == sesion.Id).OrderByDescending(c => c.Id).ToListAsync(ct);
        var lista = new List<CashCountDto>();
        foreach (var a in arqueos) lista.Add(await SesionesDeCaja.ArqueoDtoAsync(db, a, sesion.Status, ct));
        return Result.Success<IReadOnlyList<CashCountDto>>(lista);
    }
}
