using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contractual;

/// <summary>
/// Las cuotas exigibles del ahorro contractual (feature 014, US7, T182; data-model §3.5; FR-074, FR-087): una por cada cuota del plan del contrato, con el valor
/// sellado, que nace cuando empieza su período —la primera al abrir (<see cref="AperturaDeContractual"/>), las siguientes desde el vencimiento de la anterior,
/// en el subpaso de cuotas del paso de captaciones (<c>GeneradorDeCuotas</c>)—. El período de la cuota es el mes de su vencimiento y, si en el mes vencen
/// varias (quincenal, decadal, semanal), el orden dentro del mes. Sólo mientras la cuenta esté activa o inactiva y el contrato vigente o incumplido a tasa de
/// castigo (el que sigue corriendo); la cuenta que vence, se cancela o se cumple no genera más. Idempotente por el único de la cuota y la marca del control. No
/// emite hechos: una cuota exigible no mueve dinero. (nuevo)
/// </summary>
public sealed class GeneradorDeCuotasDeContractual(IApplicationDbContext db, RegistroDeCuentas registro, LectorDeContractual lector)
{
    /// <summary>El período de una cuota del plan: el año y el mes de su vencimiento y su orden entre las del mismo mes.</summary>
    public static PeriodoDeCuota Periodo(PlanDeContractualCalculado plan, CuotaDeContractual cuota)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(cuota);
        var v = cuota.Vencimiento;
        var sub = 1 + plan.Cuotas.Count(q => q.Numero < cuota.Numero && q.Vencimiento.Year == v.Year && q.Vencimiento.Month == v.Month);
        var anterior = cuota.Numero == 1 ? plan.Condiciones.Inicio : plan.Cuotas.First(q => q.Numero == cuota.Numero - 1).Vencimiento;
        return new PeriodoDeCuota(v.Year, v.Month, sub, anterior.AddDays(1), v, v);
    }

    /// <summary>La marca ordenable del período de la cuota (<c>LastInstallmentPeriod</c>).</summary>
    public static string Clave(PlanDeContractualCalculado plan, CuotaDeContractual cuota) => Periodo(plan, cuota).Clave;

    /// <summary>La cuota exigible de una cuota del plan, con su origen (la corrida o la operación).</summary>
    public static AccountInstallment Cuota(MemberAccount cuenta, PlanDeContractualCalculado plan, CuotaDeContractual cuota, int? corrida, long? operacion,
        string huella)
    {
        var p = Periodo(plan, cuota);
        return new AccountInstallment
        {
            MemberAccountId = cuenta.Id,
            MemberAccount = cuenta.Id == 0 ? cuenta : null,
            PeriodYear = (short)p.Anio,
            PeriodMonth = (byte)p.Mes,
            SubPeriodNumber = (byte)p.Subperiodo,
            DueDate = cuota.Vencimiento,
            Amount = plan.Condiciones.Cuota,
            GeneratedByProcessRunId = corrida,
            GeneratedByOperationId = operacion,
            RulesVersion = VersionesDeReglas.Vigente,
            InputsSha256 = new HuellaDeCartera().Texto("contract", huella).Entero("installment", cuota.Numero).Fecha("due", cuota.Vencimiento)
                .Decimal("amount", plan.Condiciones.Cuota, 2).Calcular(),
        };
    }

    /// <summary>¿Genera cuotas la cuenta? Activa o inactiva, con su contrato vigente o incumplido a tasa de castigo (sigue corriendo hasta su vencimiento).</summary>
    public static bool Genera(MemberAccount cuenta, ContractualBreachEffect? efecto) =>
        cuenta.DepositClass == DepositClass.Contractual && cuenta.Status is MemberAccountStatus.Active or MemberAccountStatus.Inactive
        && (cuenta.ContractualStatus == ContractualStatus.Current
            || (cuenta.ContractualStatus == ContractualStatus.Breached && (efecto ?? ContractualBreachEffect.BreachRate) == ContractualBreachEffect.BreachRate));

    /// <summary>Las cuotas del plan que ya empezaron a <paramref name="d"/> y la cuenta no tiene, en su propia transacción; devuelve las generadas.</summary>
    public async Task<IReadOnlyList<AccountInstallment>> GenerarAsync(LendingProcessRun corrida, int cuentaId, DateOnly d, CancellationToken ct) =>
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == cuentaId, ct);
            var leido = await lector.LeerAsync(cuenta, ct);
            if (leido.Inicial is not { } inicial || !Genera(cuenta, inicial.BreachEffect)) return (IReadOnlyList<AccountInstallment>)[];
            var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuentaId, ct);
            var plan = LectorDeContractual.Plan(inicial);
            var existentes = (await db.AccountInstallments.AsNoTracking().Where(c => c.MemberAccountId == cuentaId).Select(c => c.DueDate).ToListAsync(ct))
                .ToHashSet();
            var generadas = new List<AccountInstallment>();
            foreach (var cuota in plan.CuotasEmpezadas(d))
            {
                var clave = Clave(plan, cuota);
                if (existentes.Contains(cuota.Vencimiento) || (control.LastInstallmentPeriod is { } marca && string.CompareOrdinal(clave, marca) <= 0))
                {
                    RegistroDeCuentas.MarcarCuotaGenerada(control, clave);
                    continue;
                }
                generadas.Add(Cuota(cuenta, plan, cuota, corrida.Id, null, inicial.InputsSha256));
                RegistroDeCuentas.MarcarCuotaGenerada(control, clave);
            }
            if (generadas.Count > 0) registro.RegistrarCuotas(generadas);
            await db.SaveChangesAsync(ct);
            return (IReadOnlyList<AccountInstallment>)generadas;
        }, ct);
}
