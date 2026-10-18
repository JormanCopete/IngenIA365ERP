using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

/// <summary>
/// El comprobante de una operación de cuenta ya registrada (feature 014, US4, T111; contrato-de-cuentas §3.8): el que devuelve el reintento con la misma
/// clave (<c>AlreadyApplied</c>) y la consulta de una operación. Lo arma del libro: sus filas, el GMF de <c>LND_GmfMonthlyUsage</c>, los cargos que cobró y
/// los que disparó, las cuotas que cubrió, la cuenta destino de un traslado y el disponible de hoy. (nuevo)
/// </summary>
public static class ComprobantesDeCuenta
{
    public static async Task<ComprobanteDeCuentaDto> DeAsync(IApplicationDbContext db, ReglasDeMovimientos movimientos, MemberAccountOperation operacion,
        DateOnly hoy, CancellationToken ct)
    {
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == operacion.MemberAccountId, ct);
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == operacion.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var ids = filas.Where(f => f.InstallmentId is not null).Select(f => f.InstallmentId!.Value).Distinct().ToList();
        var cuotas = await db.AccountInstallments.AsNoTracking().Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var porCuota = filas.Where(f => f.InstallmentId is not null).GroupBy(f => f.InstallmentId!.Value)
            .Select(g => new DistribucionEnCuotaDto(cuotas[g.Key].PublicId, cuotas[g.Key].Periodo, g.Sum(f => f.Amount))).ToList();
        var uso = await db.GmfMonthlyUsages.AsNoTracking().Where(u => u.OperationId == operacion.Id && u.ReversesUsageId == null).FirstOrDefaultAsync(ct);
        var gmf = uso is null
            ? null
            : new GmfCalculadoDto(uso.DisposedAmount, 0m, uso.ExemptAmount, uso.TaxableBase, uso.Rate, uso.GmfAmount, uso.Policy.ToString(),
                $"GMF de {uso.GmfAmount:N0} sobre una base gravada de {uso.TaxableBase:N2} ({uso.TaxableEventCode}).");
        var cargos = await db.AccountCharges.AsNoTracking()
            .Where(c => c.TriggerOperationId == operacion.Id || c.CollectedOperationId == operacion.Id)
            .OrderBy(c => c.Id)
            .Select(c => new CargoCalculadoDto(c.ChargeCode, c.Amount, c.IsVatTaxed, c.Status.ToString())).ToListAsync(ct);
        Guid? destino = operacion.CounterpartMemberAccountId is { } contraparte
            ? await db.MemberAccounts.AsNoTracking().Where(a => a.Id == contraparte).Select(a => (Guid?)a.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var leida = await movimientos.LeerAsync(cuenta, hoy, ct);
        var clase = operacion.Kind switch
        {
            MemberAccountOperationKind.Deposit => "Deposit",
            MemberAccountOperationKind.Withdrawal or MemberAccountOperationKind.DirectDebit => "Withdrawal",
            MemberAccountOperationKind.Transfer => "Transfer",
            _ => operacion.Kind.ToString(),
        };
        return new ComprobanteDeCuentaDto(operacion.PublicId, operacion.Number, cuenta.PublicId, cuenta.Number, operacion.IdempotencyKey ?? string.Empty,
            clase, operacion.ValueDate, operacion.AccountingDate, operacion.RegisteredAt, operacion.Amount,
            filas.Select(f => new LineaDeCuentaDto((ConceptoDeCuenta)(int)f.Concept, f.Kind.ToString(), f.Amount)).ToList(), gmf, cargos, porCuota,
            ReglasDeMovimientos.Dto(leida.Disponible), null, destino, false, operacion.InputsSha256, operacion.RulesVersion, []);
    }
}
