using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Cash;

/// <summary>
/// Los documentos impresos de la caja (feature 012, I3, T626; contracts/api.md §21, §27 «Documentos QuestPDF propios»): el informe de
/// arqueo de una sesión (<c>CashCountReport</c>) y el comprobante de un movimiento de caja con firmas (<c>CashMovementReceiptReport</c>).
/// Application arma el modelo; la API los pinta con QuestPDF por este puerto (Application no conoce QuestPDF). Las rutas
/// <c>GET /cash-sessions/{id}/count-report</c> y <c>…/cash-movements/{id}/receipt</c> las publica T631. (nuevo)
/// </summary>
public interface IDocumentosDeCajaEnPdf
{
    byte[] Arqueo(CashCountReportModel modelo);

    byte[] ComprobanteDeMovimiento(CashMovementReceiptModel modelo);
}

/// <summary>La empresa en el encabezado de un documento de caja. (nuevo)</summary>
public sealed record EmpresaDeCajaDto(string Name, string Nit);

/// <summary>Una denominación contada o entregada: valor, cantidad y total. (nuevo)</summary>
public sealed record DenominacionContadaDto(decimal Value, int Quantity, decimal Amount);

/// <summary>Un lote de datáfono en el arqueo: datáfono, número, total del lote, comprobantes y lo esperado. (nuevo)</summary>
public sealed record LoteDeArqueoDto(string CardTerminalCode, string BatchNumber, decimal BatchTotal, int VoucherCount, decimal ExpectedTotal);

/// <summary>Una referencia cotejada en el arqueo (bono, transferencia, cheque). (nuevo)</summary>
public sealed record ReferenciaDeArqueoDto(string? DocumentNumber, string? Reference, decimal Amount, bool Verified, string? Note);

/// <summary>Una línea del arqueo por medio, con su detalle de captura. (nuevo)</summary>
public sealed record LineaDeArqueoDto(
    string PaymentMeansCode,
    string PaymentMeansName,
    CashCountMethod CountMethod,
    decimal Expected,
    decimal Counted,
    decimal Difference,
    decimal Tolerance,
    bool WithinTolerance,
    string? Treatment,
    string? Reason,
    IReadOnlyList<DenominacionContadaDto> Denominations,
    IReadOnlyList<LoteDeArqueoDto> TerminalBatches,
    IReadOnlyList<ReferenciaDeArqueoDto> References);

/// <summary>El modelo del informe de arqueo de una sesión (<c>CashCountReport</c>). (nuevo)</summary>
public sealed record CashCountReportModel(
    EmpresaDeCajaDto Company,
    string PointOfSale,
    string CashRegister,
    string Cashier,
    DateOnly OperatingDate,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    DateTime CountedAt,
    bool Blind,
    string? ClosedBy,
    IReadOnlyList<LineaDeArqueoDto> Lines,
    decimal TotalExpected,
    decimal TotalCounted,
    decimal TotalDifference,
    string? DifferenceDocumentNumber,
    string? DifferenceDocumentStatus);

/// <summary>El modelo del comprobante de un movimiento de caja (<c>CashMovementReceiptReport</c>). (nuevo)</summary>
public sealed record CashMovementReceiptModel(
    EmpresaDeCajaDto Company,
    string? Number,
    string Status,
    DateOnly OperationDate,
    string Kind,
    string PointOfSale,
    string CashRegister,
    string SourcePaymentMeans,
    string? TargetPaymentMeans,
    string? Destination,
    string? DestinationCashRegister,
    decimal Amount,
    string Reason,
    IReadOnlyList<DenominacionContadaDto> Denominations,
    string DeliveredBy,
    string ReceivedByLabel,
    string? ApprovedBy);

/// <summary>El modelo del informe de arqueo de una sesión visible (la del cajero o, con <c>ViewAll</c>, cualquiera). Sin arqueo, 404. (nuevo)</summary>
public sealed record GetCashCountReportQuery(Guid CashSessionPublicId) : IRequest<Result<CashCountReportModel>>;

public sealed class GetCashCountReportQueryHandler(IApplicationDbContext db, SesionesDeCaja sesiones)
    : IRequestHandler<GetCashCountReportQuery, Result<CashCountReportModel>>
{
    public async Task<Result<CashCountReportModel>> Handle(GetCashCountReportQuery request, CancellationToken ct)
    {
        var sesion = await sesiones.VisibleAsync(request.CashSessionPublicId, seguir: false, ct);
        if (sesion is null) return Result.Failure<CashCountReportModel>(ErroresDeCaja.SessionNotFound());
        var conteo = await db.CashCounts.AsNoTracking().Where(c => c.CashSessionId == sesion.Id && !c.IsDeleted).OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);
        if (conteo is null) return Result.Failure<CashCountReportModel>(ErroresDeCaja.SessionNotFound());

        var lineas = await db.CashCountLines.AsNoTracking().Where(l => l.CashCountId == conteo.Id && !l.IsDeleted).OrderBy(l => l.Id).ToListAsync(ct);
        var lineaIds = lineas.Select(l => l.Id).ToList();
        var denominaciones = (await db.CashCountDenominations.AsNoTracking().Where(d => lineaIds.Contains(d.CashCountLineId)).ToListAsync(ct))
            .ToLookup(d => d.CashCountLineId);
        var lotes = (await (from b in db.CashCountTerminalBatches.AsNoTracking()
                            join t in db.CardTerminals.AsNoTracking().IgnoreQueryFilters() on b.CardTerminalId equals t.Id
                            where lineaIds.Contains(b.CashCountLineId)
                            select new { b.CashCountLineId, t.Code, b.BatchNumber, b.BatchTotal, b.VoucherCount, b.ExpectedTotal }).ToListAsync(ct))
            .ToLookup(b => b.CashCountLineId);
        var referencias = (await (from r in db.CashCountReferenceChecks.AsNoTracking()
                                  join p in db.DocumentPayments.AsNoTracking() on r.DocumentPaymentId equals p.Id
                                  join d in db.InventoryDocuments.AsNoTracking() on p.DocumentId equals d.Id
                                  where lineaIds.Contains(r.CashCountLineId)
                                  select new { r.CashCountLineId, d.Prefix, d.Number, p.Reference, p.Amount, r.IsVerified, r.Note }).ToListAsync(ct))
            .ToLookup(r => r.CashCountLineId);
        var medioIds = lineas.Select(l => l.PaymentMeansId).ToList();
        var medios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => medioIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => new { m.Code, m.Name }, ct);

        var modelo = lineas.Select(l => new LineaDeArqueoDto(
            medios.GetValueOrDefault(l.PaymentMeansId)?.Code ?? string.Empty, medios.GetValueOrDefault(l.PaymentMeansId)?.Name ?? string.Empty, l.CountMethod,
            l.ExpectedAmount, l.CountedAmount, l.DifferenceAmount, l.ToleranceAmount, l.WithinTolerance,
            l.DifferenceAmount == 0m ? null : DatosDeVentasYCaja.Tratamiento(l.Treatment), l.Reason,
            denominaciones[l.Id].OrderByDescending(d => d.DenominationValue).Select(d => new DenominacionContadaDto(d.DenominationValue, d.Quantity, d.Amount)).ToList(),
            lotes[l.Id].Select(b => new LoteDeArqueoDto(b.Code, b.BatchNumber, b.BatchTotal, b.VoucherCount, b.ExpectedTotal)).ToList(),
            referencias[l.Id].Select(r => new ReferenciaDeArqueoDto(VistaDeDocumentos.NumeroVisible(r.Prefix, r.Number), r.Reference, r.Amount, r.IsVerified, r.Note)).ToList()))
            .ToList();

        var diferencia = conteo.DifferenceDocumentId is int did
            ? await db.InventoryDocuments.AsNoTracking().Where(d => d.Id == did).Select(d => new { d.Prefix, d.Number, d.Status }).FirstOrDefaultAsync(ct)
            : null;
        var cerro = sesion.ClosedByUserId is int u ? await db.Users.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == u).Select(x => x.Username).FirstOrDefaultAsync(ct) : null;
        return Result.Success(new CashCountReportModel(
            await EncabezadoDeCaja.EmpresaAsync(db, ct),
            await db.PointsOfSale.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == sesion.PointOfSaleId).Select(p => p.Code + " · " + p.Name).FirstOrDefaultAsync(ct) ?? string.Empty,
            await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == sesion.CashRegisterId).Select(c => c.Code + " · " + c.Name).FirstOrDefaultAsync(ct) ?? string.Empty,
            sesion.CashierName, sesion.OperatingDate, sesion.OpenedAt, sesion.ClosedAt, conteo.CountedAt, conteo.IsBlind, cerro, modelo,
            conteo.TotalExpected, conteo.TotalCounted, conteo.TotalDifference,
            diferencia is null ? null : VistaDeDocumentos.NumeroVisible(diferencia.Prefix, diferencia.Number),
            diferencia is null ? null : DatosDeVentasYCaja.Estado(diferencia.Status)));
    }
}

/// <summary>El modelo del comprobante de un movimiento de caja cuya sesión es visible. (nuevo)</summary>
public sealed record GetCashMovementReceiptQuery(Guid DocumentPublicId) : IRequest<Result<CashMovementReceiptModel>>;

public sealed class GetCashMovementReceiptQueryHandler(IApplicationDbContext db, SesionesDeCaja sesiones, IAlcanceDeInventario alcance)
    : IRequestHandler<GetCashMovementReceiptQuery, Result<CashMovementReceiptModel>>
{
    public async Task<Result<CashMovementReceiptModel>> Handle(GetCashMovementReceiptQuery request, CancellationToken ct)
    {
        var fila = await (from m in db.CashMovementDetails.AsNoTracking()
                          join d in db.InventoryDocuments.AsNoTracking() on m.DocumentId equals d.Id
                          join s in db.CashSessions.AsNoTracking() on m.CashSessionId equals s.Id
                          where d.PublicId == request.DocumentPublicId && !m.IsDeleted
                          select new { Detalle = m, Documento = d, Sesion = s.PublicId }).FirstOrDefaultAsync(ct);
        if (fila is null || await sesiones.VisibleAsync(fila.Sesion, seguir: false, ct) is not { } sesion)
            return Result.Failure<CashMovementReceiptModel>(InventoryErrors.DocumentNotFound());

        var dto = (await VistaDeMovimientosDeCaja.DtosAsync(db, [(fila.Detalle, fila.Documento)], ct))[0];
        var aprobadores = await new DatosDeVentasYCaja(db, alcance).AprobadoresAsync([dto.DocumentPublicId], ct);
        var denominaciones = new List<DenominacionContadaDto>();
        if (!string.IsNullOrWhiteSpace(fila.Detalle.DenominationsJson))
        {
            using var json = JsonDocument.Parse(fila.Detalle.DenominationsJson);
            foreach (var d in json.RootElement.EnumerateArray())
            {
                var cantidad = d.GetProperty("quantity").GetInt32();
                var total = d.GetProperty("amount").GetDecimal();
                denominaciones.Add(new DenominacionContadaDto(cantidad == 0 ? 0m : total / cantidad, cantidad, total));
            }
        }
        var recibe = dto.Destination switch
        {
            CashMovementDestination.Safe => "Recibe (caja fuerte)",
            CashMovementDestination.Deposit => "Recibe (para consignar)",
            CashMovementDestination.Register => $"Recibe (caja {dto.DestinationCashRegisterCode})",
            _ => dto.Kind == CashMovementKind.BaseIncome ? "Recibe (cajero)" : "Revisa",
        };
        return Result.Success(new CashMovementReceiptModel(
            await EncabezadoDeCaja.EmpresaAsync(db, ct), dto.Number, DatosDeVentasYCaja.Estado(dto.Status), fila.Documento.OperationDate,
            DatosDeVentasYCaja.TipoDeMovimiento(dto.Kind),
            await db.PointsOfSale.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == sesion.PointOfSaleId).Select(p => p.Code).FirstOrDefaultAsync(ct) ?? string.Empty,
            await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == sesion.CashRegisterId).Select(c => c.Code).FirstOrDefaultAsync(ct) ?? string.Empty,
            dto.SourcePaymentMeansCode, dto.TargetPaymentMeansCode, dto.Destination is null ? null : DatosDeVentasYCaja.Destino(dto.Destination),
            dto.DestinationCashRegisterCode, dto.Amount, dto.Reason, denominaciones, sesion.CashierName, recibe, aprobadores.GetValueOrDefault(dto.DocumentPublicId)));
    }
}

/// <summary>El encabezado común de los documentos de caja: la empresa con su NIT. (nuevo)</summary>
public static class EncabezadoDeCaja
{
    public static async Task<EmpresaDeCajaDto> EmpresaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id)
            .Select(c => new { c.Name, c.TaxId, c.TaxIdCheckDigit }).FirstOrDefaultAsync(ct);
        if (empresa is null) return new EmpresaDeCajaDto(string.Empty, string.Empty);
        return new EmpresaDeCajaDto(empresa.Name, empresa.TaxIdCheckDigit is { Length: > 0 } dv ? $"{empresa.TaxId}-{dv}" : empresa.TaxId);
    }
}
