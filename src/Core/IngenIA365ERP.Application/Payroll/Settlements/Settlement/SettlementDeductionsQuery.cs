using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

/// <summary>La propuesta de descuentos de una definitiva (contracts/api.md §3.4 <c>GET /{runId}/deductions</c>).</summary>
public sealed record GetSettlementDeductionsQuery(Guid RunPublicId) : IRequest<Result<SettlementDeductionsDto>>;

public sealed class GetSettlementDeductionsQueryValidator : AbstractValidator<GetSettlementDeductionsQuery>
{
    public GetSettlementDeductionsQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class GetSettlementDeductionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSettlementDeductionsQuery, Result<SettlementDeductionsDto>>
{
    public async Task<Result<SettlementDeductionsDto>> Handle(GetSettlementDeductionsQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<SettlementDeductionsDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Settlement) return Result.Failure<SettlementDeductionsDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Settlement));
        return Result.Success(await SettlementDeductionsReader.LeerAsync(db, run, ct));
    }
}

/// <summary>
/// Arma la propuesta de descuentos desde <c>PAY_SettlementDeductions</c> y las líneas de la corrida:
/// el neto antes de descuentos es el neto de la corrida más lo que las líneas de descuento ya le
/// quitaron. Lo comparten registrar, ajustar, recalcular y la consulta.
/// </summary>
public static class SettlementDeductionsReader
{
    private sealed record Desglose(decimal? Capital, decimal? Intereses, decimal? Mora, int? CuotasPendientes, decimal? SaldoTotal, int? CuotasCausadas, int? CuotasDescontadas);

    public static async Task<SettlementDeductionsDto> LeerAsync(IApplicationDbContext db, PayrollRun run, CancellationToken ct)
    {
        var deducciones = run.TerminationId is { } tid
            ? await db.SettlementDeductions.AsNoTracking().Where(d => d.TerminationId == tid).OrderBy(d => d.Kind).ThenBy(d => d.Id).ToListAsync(ct)
            : [];
        var fila = await db.PayrollRunEmployees.AsNoTracking().Include(e => e.Lines).FirstOrDefaultAsync(e => e.PayrollRunId == run.Id, ct);
        return Armar(run, fila, deducciones);
    }

    public static SettlementDeductionsDto Armar(PayrollRun run, PayrollRunEmployee? fila, IReadOnlyList<SettlementDeduction> deducciones)
    {
        var lineasDescuento = fila?.Lines.Where(l => l.SettlementDeductionId != null).ToList() ?? [];
        var descontado = lineasDescuento.Sum(l => l.Amount);
        var netoDespues = fila?.NetPay ?? 0m;
        var netoAntes = netoDespues + descontado;
        var items = deducciones.Select(d => Item(d, run)).ToList();
        var propuesto = items.Sum(i => i.Proposed);
        var aplicado = items.Sum(i => i.Applied);
        var sobreNeto = fila is not null && fila.Flags.HasFlag(RunEmployeeFlag.DeductionOverNet);
        return new SettlementDeductionsDto(run.PublicId, netoAntes, items, propuesto, aplicado, netoAntes - aplicado, sobreNeto);
    }

    public static SettlementDeductionItemDto Item(SettlementDeduction d, PayrollRun run)
    {
        Desglose? x = null;
        if (!string.IsNullOrWhiteSpace(d.ProposedBreakdownJson))
        {
            try { x = JsonSerializer.Deserialize<Desglose>(d.ProposedBreakdownJson, RunJson.Options); }
            catch (JsonException) { x = null; }
        }
        var cuotasCausadasSinDescontar = x?.CuotasCausadas is { } causadas && x.CuotasDescontadas is { } descontadas ? Math.Max(0, causadas - descontadas) : (int?)null;
        decimal? saldoQueQueda = d.Status is SettlementDeductionStatus.Applied or SettlementDeductionStatus.Reverted
            ? d.RemainingBalanceAfter
            : d.Kind == SettlementDeductionKind.CooperativeLoan && x?.SaldoTotal is { } saldo ? Math.Max(0m, saldo - d.AppliedAmount) : null;
        return new SettlementDeductionItemDto(
            d.PublicId, d.Kind, d.Description,
            x?.Capital, x?.Intereses, x?.Mora, x?.CuotasPendientes, cuotasCausadasSinDescontar,
            d.ProposedAmount, d.AppliedAmount, d.AdjustmentReason, d.AdjustedBy, d.AdjustedAt,
            saldoQueQueda, d.Status, d.CarteraTransactionPublicId);
    }
}
