using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>
/// El ajuste de causación por fecha valor <b>enlazado</b> a la operación que lo provoca (feature 014, US5, T133; research R04; FR-062): una consignación, un
/// retiro, un traslado o un reverso con fecha valor en días ya causados de la ventana abierta cambia los saldos de esos días; el motor vuelve a calcular la
/// ventana hasta el último día causado con lo que ya se sabe y la diferencia contra lo que está en el libro entra en la <b>misma</b> operación como
/// <c>AccrualAdjustment</c> fechado el último día causado, con su hecho <c>AjusteDeCausacionDeCaptacion</c> (causa <c>ValueDatedOperation</c>, los días que
/// cambiaron y el hecho principal de la operación). Si la fecha valor cae en un período ya liquidado no se reliquida ese período (research E97). Sin causación
/// previa o en una cuenta que no causa, no hace nada. Corre en la transacción y bajo el cerrojo de quien llama, después de que la operación guardó sus filas;
/// guarda. (nuevo)
/// </summary>
public sealed class AjusteDeCausacionDeCuenta(IApplicationDbContext db, LectorDeInteres lector, EmisionDeCartera emision, RegistroDeCuentas registro)
{
    public async Task<Result<decimal>> AjustarAsync(MemberAccount cuenta, MemberAccountOperation operacion, string claseDeLaOperacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(operacion);
        if (!LiquidacionDeIntereses.GanaInteres(cuenta)) return Result.Success(0m);
        var control = db.MemberAccountControls.Local.FirstOrDefault(c => c.MemberAccountId == cuenta.Id)
                      ?? await db.MemberAccountControls.FirstOrDefaultAsync(c => c.MemberAccountId == cuenta.Id, ct);
        if (control is null) return Result.Success(0m);
        var ultimo = control.LastAccruedDate;
        if (operacion.ValueDate > ultimo) return Result.Success(0m);

        var versiones = await lector.VersionesAsync(cuenta.CreditLineId, ct);
        var version = LectorDeInteres.VersionDelDia(versiones, cuenta, ultimo, DateTime.MaxValue);
        if (version is null) return Result.Success(0m);
        var inicio = LectorDeInteres.InicioDeLaVentana(cuenta, control, version.Version, ultimo);
        if (ultimo <= inicio || operacion.ValueDate > ultimo) return Result.Success(0m);

        var fallecido = await Deceased.TitularesFallecidos.EsAsync(db, cuenta.HolderPersonId, ct);
        var ventana = await lector.VentanaAsync(cuenta, inicio, ultimo, DateTime.MaxValue, fallecido, ct);
        if (ventana.IsFailure) return Result.Failure<decimal>(ventana.Error);
        var enElLibro = await lector.CausadoEnElLibroAsync(cuenta.Id, inicio, ultimo, ct);
        var ajuste = ventana.Value.Interes.Causado - enElLibro;
        if (ajuste == 0m) return Result.Success(0m);

        // Los días que cambiaron: lo que causó cada uno según el libro y lo que causa ahora.
        var clases = LectorDeInteres.ClasesDeLoCausado.ToArray();
        var previos = (await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => t.MemberAccountId == cuenta.Id && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind)
                            && t.ValueDate > inicio && t.ValueDate <= ultimo && !t.IsDeleted)
                .Select(t => new { t.ValueDate, t.Amount }).ToListAsync(ct))
            .GroupBy(t => t.ValueDate).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        var dias = ventana.Value.Interes.Dias
            .Select(d => new AccrualDayV1 { Date = d.Dia, Previous = previos.GetValueOrDefault(d.Dia), Recalculated = d.Causado })
            .Where(d => d.Previous != d.Recalculated).ToList();

        var hecho = await emision.HechoDeCuentaAsync(cuenta, operacion.AccountingDate, ct);
        if (hecho.IsFailure) return Result.Failure<decimal>(hecho.Error);
        var exenta = await db.GmfExemptAccounts.AsNoTracking()
            .AnyAsync(g => g.MemberAccountId == cuenta.Id && !g.IsDeleted && g.ValidFrom <= ultimo && (g.ValidTo == null || g.ValidTo >= ultimo), ct);
        var unidad = await emision.AjusteDeCausacionAsync(new AjusteAEmitir(new HechoDeCuentaMarcada(hecho.Value, exenta), operacion, ajuste, dias,
            EmisionDeCartera.CausaOperacionFechadaAtras, claseDeLaOperacion), ct);
        if (unidad.IsFailure) return Result.Failure<decimal>(unidad.Error);

        registro.RegistrarTransacciones([new MemberAccountTransaction
        {
            MemberAccountId = cuenta.Id,
            Operation = operacion,
            OperationId = operacion.Id == 0 ? null : operacion.Id,
            Kind = MemberAccountTransactionKind.AccrualAdjustment,
            Concept = MemberAccountConcept.InterestPayable,
            Amount = ajuste,
            ValueDate = ultimo,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = operacion.RegisteredAt,
            MessagePublicId = unidad.Value.De(AjusteDeCausacionDeCaptacionV1.Type),
        }]);
        await db.SaveChangesAsync(ct);
        return Result.Success(ajuste);
    }
}
