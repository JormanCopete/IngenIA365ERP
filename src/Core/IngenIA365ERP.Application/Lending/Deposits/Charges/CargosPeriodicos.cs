using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Charges;

/// <summary>
/// El subpaso «cargos periódicos» del paso de captaciones (feature 014, US4, T116; research R14, D35; FR-128): en cada tanda, las cuentas de captación
/// activas o inactivas cuya versión vigente de la línea trae cargos habilitados <c>Periodic</c> —o <c>Inactivity</c> si la cuenta está inactiva—. Cada
/// cargo se causa el <b>último día de su período</b> (<see cref="CargosDeCuenta.PeriodoDe"/> con su periodicidad, mensual si no tiene) con los saldos
/// promedio y mínimo del período, una sola vez por cuenta, cargo y período (la fila del cargo con <c>PeriodFrom</c> lo hace idempotente). Cada cuenta en
/// su propia transacción con el cerrojo del proceso (salta la ocupada y la deja para la pasada siguiente); lo que falla queda en
/// <c>LND_ProcessRunErrors</c>. (nuevo)
/// </summary>
public sealed class CargosPeriodicos(
    IApplicationDbContext db,
    ReglasDeMovimientos movimientos,
    CargosDeCuentas cargos,
    ICerrojoDeCartera cerrojo,
    ILogger<CargosPeriodicos>? logger = null) : ISubpasoDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "CargosPeriodicos";

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var candidatas = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Nature == LineNature.Deposit && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive))
            .OrderBy(a => a.Id).ToListAsync(ct);
        var fallos = new List<FalloDeCuenta>();
        foreach (var cuenta in candidatas)
        {
            try
            {
                await CausarAsync(cuenta, d, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.CargoPeriodicoFallido] La cuenta {Cuenta} no causó sus cargos del {Dia}.", cuenta.PublicId, d);
                fallos.Add(new FalloDeCuenta(cuenta.Id, "Lending.Savings.PeriodicChargeFailed", ex.Message));
            }
        }
        return fallos;
    }

    /// <summary>Los cargos periódicos (y de inactividad) de una cuenta que cierran período el día <paramref name="d"/>; devuelve cuántos causó.</summary>
    public async Task<int> CausarAsync(MemberAccount cuenta, DateOnly d, CancellationToken ct)
    {
        var leida = await movimientos.LeerAsync(cuenta, d, ct);
        var disparadores = cuenta.Status == MemberAccountStatus.Inactive
            ? new[] { ChargeTrigger.Periodic, ChargeTrigger.Inactivity }
            : [ChargeTrigger.Periodic];
        var catalogo = await cargos.CatalogoAsync(leida.Version, disparadores, ct);
        var vencen = catalogo.Select(def => (Def: def, Periodo: CargosDeCuenta.PeriodoDe(def.Periodicity ?? RepaymentPeriodicity.Monthly, d)))
            .Where(x => x.Periodo.Hasta == d).ToList();
        if (vencen.Count == 0) return 0;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            if ((await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Saltar, ct)).Count == 0) return 0;
            var causados = 0;
            foreach (var (def, periodo) in vencen)
            {
                var desde = periodo.Desde;
                if (await db.AccountCharges.AsNoTracking().AnyAsync(c => c.MemberAccountId == cuenta.Id && c.ChargeCode == def.Code && c.PeriodFrom == desde
                                                                         && !c.IsDeleted, ct))
                    continue;
                var actual = await movimientos.LeerAsync(cuenta, d, ct);
                // El período cuenta desde la apertura si la cuenta se abrió dentro de él.
                var inicio = cuenta.OpenedOn > desde ? cuenta.OpenedOn : desde;
                var contexto = await cargos.ContextoAsync(actual, def, 0m, inicio, d, 0, actual.Disponible.Disponible, ct);
                var calculo = CargosDeCuenta.Calcular(def.Definicion(), contexto);
                if (!calculo.SeCausa) continue;
                var causado = await cargos.CausarAsync(actual, new CargoPropuesto(def, calculo, desde, periodo.Hasta), d, null, null, null, ct);
                if (causado.IsFailure) throw new InvalidOperationException($"{causado.Error.Code}: {causado.Error.Message}");
                causados++;
            }
            return causados;
        }, ct);
    }
}
