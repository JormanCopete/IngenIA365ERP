using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Payroll;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Reports;

/// <summary>Una cifra del tablero: lo que mide, su valor y cuántos documentos. Sin datos personales. (nuevo)</summary>
public sealed record CifraDelTableroDto(string Key, string Label, decimal Amount, int Count);

/// <summary>El tablero de caja del día (FR-155): cifras agregadas de las oficinas al alcance, nunca personas ni documentos. (nuevo)</summary>
public sealed record TableroDeCajaDto(DateOnly Date, IReadOnlyList<CifraDelTableroDto> Figures, IReadOnlyList<string> Notes);

/// <summary>
/// <c>GET /api/receipts/dashboard?date=</c> (<c>Receipts.Reports.View</c>) (nuevo; feature 015, R1, T0401; FR-155, FR-081): el tablero de caja sin datos personales —el
/// recaudo del día por canal, Nómina y débito automático de sólo lectura, los egresos, las sesiones abiertas, lo pendiente (partidas, saldos a favor, recaudo pendiente,
/// consignaciones por verificar, anulaciones por aprobar) y las pagadurías morosas—. Sólo cifras y conteos.
/// </summary>
public sealed record GetReceiptsDashboardQuery(DateOnly? Date = null) : IRequest<Result<TableroDeCajaDto>>;

public sealed class GetReceiptsDashboardQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IDateTimeService reloj,
    IRecaudoDeNominaParaConsolidado? nomina = null,
    IDebitoAutomaticoParaConsolidado? debitoAutomatico = null) : IRequestHandler<GetReceiptsDashboardQuery, Result<TableroDeCajaDto>>
{
    public async Task<Result<TableroDeCajaDto>> Handle(GetReceiptsDashboardQuery request, CancellationToken ct)
    {
        var dia = request.Date ?? reloj.HoyLocal;
        var mio = await alcance.ObtenerAsync(ct);
        var recibos = (await db.IssuedReceipts.AsNoTracking().Where(r => r.ValueDate == dia && r.Status != ReceiptState.Voided)
            .Select(r => new { r.BranchId, r.Channel, r.TotalApplied }).ToListAsync(ct)).Where(r => mio.IncluyeOficina(r.BranchId)).ToList();
        var egresos = (await db.CashDisbursements.AsNoTracking().Where(e => e.OperationDate == dia && e.Status == CashDisbursementStatus.Delivered)
            .Select(e => new { e.BranchId, e.Amount }).ToListAsync(ct)).Where(e => mio.IncluyeOficina(e.BranchId)).ToList();
        var sesiones = await (from s in db.ReceiptsCashSessions.AsNoTracking()
                              join c in db.CashBoxes.AsNoTracking() on s.CashBoxId equals c.Id
                              join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                              where s.Status == CashSessionState.Open || s.Status == CashSessionState.ForgottenOpen
                              select new { p.BranchId, s.Status }).ToListAsync(ct);
        sesiones = sesiones.Where(s => mio.IncluyeOficina(s.BranchId)).ToList();
        var partidas = (await db.UnidentifiedItems.AsNoTracking()
            .Where(p => p.Status == UnidentifiedItemState.Open || p.Status == UnidentifiedItemState.PartiallyIdentified)
            .Select(p => new { p.BranchId, p.Balance }).ToListAsync(ct)).Where(p => mio.IncluyeOficina(p.BranchId)).ToList();
        var saldos = (await db.PayerCreditBalances.AsNoTracking().Where(s => s.Status == PayerCreditBalanceState.Open && s.Balance > 0m)
            .Select(s => new { s.BranchId, s.Balance }).ToListAsync(ct)).Where(s => mio.IncluyeOficina(s.BranchId)).ToList();
        var pendientes = (await db.PendingApplications.AsNoTracking().Where(p => p.Status == PendingApplicationState.Pending)
            .Select(p => new { p.BranchId, p.Amount }).ToListAsync(ct)).Where(p => mio.IncluyeOficina(p.BranchId)).ToList();
        var reportadas = (await db.ReportedDeposits.AsNoTracking().Where(d => d.Status == ReportedDepositStatus.ToVerify)
            .Select(d => new { d.BranchId, d.Amount }).ToListAsync(ct)).Where(d => mio.IncluyeOficina(d.BranchId)).ToList();
        var anulacionesPorAprobar = await db.ReceiptVoidings.AsNoTracking().CountAsync(v => v.Status == ReceiptVoidingState.PendingApproval, ct);
        var morosas = (await Paymasters.CalendarioDePagadurias.FilasAsync(db, mio, null, null, dia, ct)).Where(f => f.IsLate).ToList();
        var deNomina = nomina is null ? [] : await nomina.PorDiaAsync(dia, dia, ct);
        var debitos = debitoAutomatico is null ? [] : await debitoAutomatico.PorDiaAsync(dia, dia, ct);

        CifraDelTableroDto Canal(string clave, string rotulo, params ReceiptChannel[] canales)
        {
            var de = recibos.Where(r => canales.Contains(r.Channel)).ToList();
            return new CifraDelTableroDto(clave, rotulo, de.Sum(r => r.TotalApplied), de.Count);
        }

        var cifras = new List<CifraDelTableroDto>
        {
            Canal("cashDesk", "Recaudo en caja", ReceiptChannel.CashDesk),
            Canal("bank", "Recaudo por bancos", ReceiptChannel.Bank),
            Canal("paymaster", "Recaudo de pagadurías", ReceiptChannel.Paymaster),
            new("payroll", "Descontado por Nómina", deNomina.Sum(n => n.Loans + n.Accounts), deNomina.Sum(n => n.Deductions)),
            new("directDebit", "Débito automático", debitos.Sum(d => d.Amount), debitos.Sum(d => d.Operations)),
            new("disbursements", "Egresos de caja", egresos.Sum(e => e.Amount), egresos.Count),
            new("openSessions", "Sesiones abiertas", 0m, sesiones.Count(s => s.Status == CashSessionState.Open)),
            new("forgottenSessions", "Sesiones olvidadas", 0m, sesiones.Count(s => s.Status == CashSessionState.ForgottenOpen)),
            new("unidentifiedItems", "Partidas por identificar", partidas.Sum(p => p.Balance), partidas.Count),
            new("payerCreditBalances", "Saldos a favor de pagadores", saldos.Sum(s => s.Balance), saldos.Count),
            new("pendingApplications", "Recaudo pendiente de aplicar", pendientes.Sum(p => p.Amount), pendientes.Count),
            new("reportedDeposits", "Consignaciones por verificar", reportadas.Sum(d => d.Amount), reportadas.Count),
            new("voidingsToApprove", "Anulaciones por aprobar", 0m, anulacionesPorAprobar),
            new("delinquentPaymasters", "Pagadurías morosas", morosas.Sum(m => m.Pending), morosas.Count),
        };
        return Result.Success(new TableroDeCajaDto(dia, cifras,
            ["Cifras agregadas sin datos personales. Nómina y débito automático son de toda la cooperativa y de sólo lectura."]));
    }
}
