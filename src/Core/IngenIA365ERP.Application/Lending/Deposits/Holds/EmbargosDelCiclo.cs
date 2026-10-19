using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

/// <summary>
/// El subpaso «Embargos» del paso de captaciones (feature 014, US11, T189; FR-055, FR-065; research E164): cada día recalcula los embargos activos de las
/// personas de la tanda —lo consignado después del registro, un depósito posterior, un CDAT que venció— con <see cref="RetencionDeEmbargos"/>, antes de la
/// causación (que lee la suspensión por embargo total). Una persona se procesa una vez por día: en la tanda que trae su cuenta de captación viva de menor Id.
/// Toma el cerrojo de la persona y de sus cuentas; si alguna está ocupada, la deja para la pasada siguiente. Corre como el proceso (sin usuario). (nuevo)
/// </summary>
public sealed class EmbargosDelCiclo(IApplicationDbContext db, ICerrojoDeCartera cerrojo, RetencionDeEmbargos retencion) : ISubpasoDeCaptaciones
{
    public string Nombre => "Embargos";

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        var personas = await db.Garnishments.AsNoTracking().Where(g => g.Status == GarnishmentStatus.Active && !g.IsDeleted).Select(g => g.PersonId).Distinct()
            .ToListAsync(ct);
        if (personas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var deLaTanda = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && personas.Contains(a.HolderPersonId) && a.Nature == LineNature.Deposit)
            .Select(a => new { a.Id, a.HolderPersonId }).ToListAsync(ct);
        var fallos = new List<FalloDeCuenta>();
        foreach (var persona in deLaTanda.Select(a => a.HolderPersonId).Distinct())
        {
            var primera = await db.MemberAccounts.AsNoTracking()
                .Where(a => a.HolderPersonId == persona && a.Nature == LineNature.Deposit && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
                .MinAsync(a => (int?)a.Id, ct);
            if (primera is not { } id || !ids.Contains(id)) continue;
            try
            {
                var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
                {
                    await cerrojo.TomarSetupAsync(exclusivo: false, ct);
                    await cerrojo.BloquearDeudoresAsync([persona], ModoDeEspera.Esperar, ct);
                    var suyas = (await retencion.CuentasDeLaPersonaAsync(persona, seguir: false, ct)).Select(c => c.Id).ToList();
                    var bloqueadas = await cerrojo.BloquearCuentasAsync(suyas, ModoDeEspera.Saltar, ct);
                    if (bloqueadas.Count != suyas.Count) return Result.Success();
                    var recalculo = await retencion.RecalcularAsync(persona, d, null, "Recálculo del ciclo diario", ct);
                    return recalculo.IsFailure ? Result.Failure(recalculo.Error) : Result.Success();
                }, ct);
                if (r.IsFailure) fallos.Add(new FalloDeCuenta(id, r.Error.Code, r.Error.Message));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                fallos.Add(new FalloDeCuenta(id, ex.GetType().Name, ex.Message));
            }
            finally
            {
                db.DescartarCambios();
            }
        }
        return fallos;
    }
}
