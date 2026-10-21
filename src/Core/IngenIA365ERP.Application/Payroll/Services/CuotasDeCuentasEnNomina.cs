using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Novelties;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Las cuotas de aportes y de ahorro como descuentos de la nómina ordinaria (feature 014, US3, T089; E-02, R18; contrato-de-cuentas §6.1): antes de leer los
/// insumos del cálculo pregunta al contrato de cuentas (<see cref="IContratoDeCuentas.ListarExigiblesAsync"/>, recaudador Nómina) las cuotas exigibles de
/// las personas del plan que vencen hasta el fin del período (las atrasadas también) y deja <b>una</b> novedad <see cref="NoveltyOrigin.AccountDeduction"/>
/// por empleado y cuenta, con el concepto que la Cartera dice (<c>DESC_APORTES</c> para aportes, <c>DESC_AHORROS</c> para el ahorro permanente y el
/// contractual) y la suma de lo pendiente. Mismo patrón que <see cref="CuotasDeCarteraEnNomina"/>: recalcular reemplaza la novedad cuyo valor cambió
/// (la anterior queda <c>Superseded</c>), anula la que la Cartera ya no informa y, si la Cartera no responde, el cálculo sigue y avisa. Nómina nunca
/// lee tablas de la Cartera: sólo el puerto (<c>LasCuentasSeTocanSoloPorElContrato</c>). (nuevo)
/// </summary>
public sealed class CuotasDeCuentasEnNomina(
    IApplicationDbContext db,
    IContratoDeCuentas contrato,
    IDateTimeService clock,
    ICurrentUserService user,
    ILogger<CuotasDeCuentasEnNomina> logger)
{
    /// <summary>Motivo con que se anula la cuota que la Cartera ya no informa para el período.</summary>
    public const string AnuladaPorCartera = "La Cartera ya no informa esta cuota de aportes o de ahorro para el período: se anuló al recalcular.";

    /// <summary>El aviso cuando la Cartera no respondió por las cuotas de las cuentas.</summary>
    public const string AvisoCarteraNoResponde =
        "La Cartera no respondió al consultar las cuotas de aportes y de ahorro del período: esos descuentos quedaron como estaban. Revise y recalcule antes de aprobar.";

    public async Task<CuotasDeCarteraMaterializadas> MaterializeAsync(PayPeriod period, CancellationToken ct)
    {
        var inicio = period.StartDate.Date;
        var fin = period.EndDate.Date;
        var empleados = await (
            from e in db.Employees.AsNoTracking()
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where e.PayrollPlanId == period.PayrollPlanId
                  && (e.PayrollPlanEffectiveFrom == null || e.PayrollPlanEffectiveFrom <= fin)
                  && e.JoinDate <= fin
                  && e.TerminationDate >= inicio
            select new { Employee = e, PersonPublicId = p.PublicId }).ToListAsync(ct);
        if (empleados.Count == 0) return CuotasDeCarteraMaterializadas.Nada;

        RespuestaDelContratoDto<ExigiblesDeCuentasDto> respuesta;
        try
        {
            respuesta = await contrato.ListarExigiblesAsync(new SolicitudDeExigibles(Recaudador.Nomina,
                empleados.Select(x => x.PersonPublicId).Distinct().ToList(), clock.HoyLocal, DateOnly.FromDateTime(inicio), DateOnly.FromDateTime(fin)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "La Cartera no respondió al consultar las cuotas de cuentas del período {Period}", period.PublicId);
            return new CuotasDeCarteraMaterializadas(0, 0, 0, [AvisoCarteraNoResponde], false);
        }
        if (respuesta.Result == ResultadoDelContrato.Rejected)
            return new CuotasDeCarteraMaterializadas(0, 0, 0, [$"Cuotas de aportes y de ahorro: {respuesta.Message}"], false);
        var cuotas = respuesta.Value?.Installments ?? [];

        var empleadoPorPersona = empleados.GroupBy(x => x.PersonPublicId).ToDictionary(g => g.Key, g => g.First().Employee);
        var idsEmpleados = empleados.Select(x => x.Employee.Id).ToList();
        var vigentes = await db.PayrollNovelties
            .Where(n => n.PayPeriodId == period.Id && n.Origin == NoveltyOrigin.AccountDeduction && n.Status == NoveltyStatus.Active
                        && idsEmpleados.Contains(n.EmployeeId))
            .ToListAsync(ct);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "sistema";
        var avisos = new List<string>();
        int creadas = 0, reemplazadas = 0, anuladas = 0;
        var informadas = new HashSet<(int EmployeeId, Guid Account)>();

        // Una novedad por empleado y cuenta: las cuotas de la cuenta que vencen hasta el fin del período se suman, en la prelación de la Cartera.
        foreach (var grupo in cuotas.GroupBy(c => (c.PersonPublicId, c.AccountPublicId)).OrderBy(g => g.Min(c => c.Order)).ThenBy(g => g.Key.AccountPublicId))
        {
            if (!empleadoPorPersona.TryGetValue(grupo.Key.PersonPublicId, out var empleado)) continue;
            var valor = grupo.Sum(c => c.Pending);
            if (valor <= 0m) continue;
            var primera = grupo.OrderBy(c => c.DueDate).First();
            var codigo = string.IsNullOrWhiteSpace(primera.PayrollConcept)
                ? primera.Nature == NaturalezaDeCuenta.Contribution ? WellKnownConceptCodes.ContributionDeduction : WellKnownConceptCodes.SavingsDeduction
                : primera.PayrollConcept;
            informadas.Add((empleado.Id, grupo.Key.AccountPublicId));

            var anterior = vigentes.FirstOrDefault(n => n.EmployeeId == empleado.Id && n.AccountPublicId == grupo.Key.AccountPublicId);
            if (anterior is not null && anterior.Amount == valor && anterior.ConceptCode == codigo) continue;

            var concepto = await NoveltyRules.ResolveConceptAsync(db, codigo, period.EndDate, empleado.EmployeeClass, ct);
            if (concepto.IsFailure)
            {
                avisos.Add($"Cuota de la cuenta {primera.AccountNumber}: {concepto.Error.Message}");
                continue;
            }

            var nueva = new PayrollNovelty
            {
                PayPeriodId = period.Id,
                EmployeeId = empleado.Id,
                ConceptDefinitionId = concepto.Value.Id,
                ConceptCode = concepto.Value.Code,
                Amount = valor,
                Notes = grupo.Count() == 1 ? $"Cuota {primera.Period} de la cuenta {primera.AccountNumber}" : $"Cuotas pendientes de la cuenta {primera.AccountNumber}",
                Status = NoveltyStatus.Active,
                Origin = NoveltyOrigin.AccountDeduction,
                AccountPublicId = grupo.Key.AccountPublicId,
                AccountNumber = primera.AccountNumber,
                CreatedAt = ahora,
                CreatedBy = quien,
            };
            if (anterior is not null)
            {
                anterior.Status = NoveltyStatus.Superseded;
                anterior.StatusReason = "La Cartera informó otro valor para las cuotas de la cuenta: se reemplazó al recalcular.";
                anterior.UpdatedAt = ahora;
                anterior.UpdatedBy = quien;
                nueva.SupersedesNoveltyId = anterior.Id;
                reemplazadas++;
            }
            else creadas++;
            db.PayrollNovelties.Add(nueva);
        }

        foreach (var sobrante in vigentes.Where(n => n.AccountPublicId is null || !informadas.Contains((n.EmployeeId, n.AccountPublicId.Value))))
        {
            sobrante.Status = NoveltyStatus.Cancelled;
            sobrante.StatusReason = AnuladaPorCartera;
            sobrante.UpdatedAt = ahora;
            sobrante.UpdatedBy = quien;
            anuladas++;
        }

        if (creadas + reemplazadas + anuladas > 0) await db.SaveChangesAsync(ct);
        return new CuotasDeCarteraMaterializadas(creadas, reemplazadas, anuladas, avisos, false);
    }
}
