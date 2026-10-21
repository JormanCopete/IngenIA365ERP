using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Novelties;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>Lo que dejó la materialización de las cuotas de Cartera en un período, con sus avisos para el cálculo.</summary>
public sealed record CuotasDeCarteraMaterializadas(int Creadas, int Reemplazadas, int Anuladas, IReadOnlyList<string> Avisos, bool MigradosSinActivar)
{
    public static CuotasDeCarteraMaterializadas Nada { get; } = new(0, 0, 0, [], false);
}

/// <summary>
/// La nómina ordinaria como pagaduría interna de Cartera (feature 013, T0162; R106, C55, FR-112; contracts/interfaces.md §6.3): antes
/// de leer los insumos del cálculo pregunta <see cref="IConsultaDeObligaciones.CuotasDeNominaAsync"/> por las personas del plan y el
/// período y convierte cada cuota en <b>una</b> novedad <see cref="NoveltyOrigin.LoanDeduction"/> por empleado, obligación y período
/// (concepto <c>DESC_CARTERA</c>, con <c>ObligationPublicId</c> y <c>ObligationNumber</c>). Hasta la 013 el cargador leía los
/// descuentos que la cartera heredada dejaba en <c>PayrollDeductionEntries</c>.
/// <para>
/// Sigue el patrón del materializador de recurrentes de la 006: recalcular <b>reemplaza</b> la novedad cuyo valor cambió (la anterior
/// queda <c>Superseded</c> y la nueva la nombra, Principio XI) y anula la que Cartera ya no informa; descartar el borrador las anula
/// con <see cref="RecurringNoveltiesMaterializerReason"/> y el próximo cálculo las vuelve a generar. Si Cartera falla o no responde,
/// el cálculo no se bloquea: las novedades del período quedan como estaban y se avisa. Con personas «migradas, sin activar» avisa
/// <c>Payroll.LendingPendingActivation</c>. (nuevo)
/// </para>
/// </summary>
public sealed class CuotasDeCarteraEnNomina(
    IApplicationDbContext db,
    IConsultaDeObligaciones obligaciones,
    IDateTimeService clock,
    ICurrentUserService user,
    ILogger<CuotasDeCarteraEnNomina> logger)
{
    /// <summary>El motivo con que descartar el borrador anula las novedades generadas (el mismo de las recurrentes).</summary>
    public static string RecurringNoveltiesMaterializerReason => Novelties.RecurringNovelties.RecurringNoveltiesMaterializer.AnuladaPorDescarte;

    /// <summary>Motivo con que se anula la cuota que Cartera ya no informa para el período.</summary>
    public const string AnuladaPorCartera = "Cartera ya no informa esta cuota para el período: se anuló al recalcular.";

    /// <summary>El aviso del cálculo con personas en una carga de Cartera sin activar (data-model §10.2, <c>Payroll.LendingPendingActivation</c>).</summary>
    public const string AvisoCarteraPorActivar = "Hay empleados con créditos en una carga de Cartera sin activar: esta nómina no les descuenta cuotas.";

    /// <summary>El aviso cuando Cartera no respondió (<c>Payroll.Settlement.PortfolioUnavailable</c> en la ordinaria).</summary>
    public const string AvisoCarteraNoResponde = "Cartera no respondió al consultar las cuotas del período: los descuentos de Cartera quedaron como estaban. Revise Cartera y recalcule antes de aprobar.";

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

        CuotasDeNominaDto cuotas;
        try
        {
            cuotas = await obligaciones.CuotasDeNominaAsync(new ConsultaDeCuotasDeNomina(
                empleados.Select(x => x.PersonPublicId).Distinct().ToList(), DateOnly.FromDateTime(inicio), DateOnly.FromDateTime(fin)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cartera no respondió al consultar las cuotas de nómina del período {Period}", period.PublicId);
            return new CuotasDeCarteraMaterializadas(0, 0, 0, [AvisoCarteraNoResponde], false);
        }

        var avisos = new List<string>();
        var migrados = cuotas.MigratedNotActivated.Count > 0;
        if (migrados) avisos.Add(AvisoCarteraPorActivar);

        var empleadoPorPersona = empleados.GroupBy(x => x.PersonPublicId).ToDictionary(g => g.Key, g => g.First().Employee);
        var idsEmpleados = empleados.Select(x => x.Employee.Id).ToList();
        var vigentes = await db.PayrollNovelties
            .Where(n => n.PayPeriodId == period.Id && n.Origin == NoveltyOrigin.LoanDeduction && n.Status == NoveltyStatus.Active
                        && idsEmpleados.Contains(n.EmployeeId))
            .ToListAsync(ct);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "sistema";
        int creadas = 0, reemplazadas = 0, anuladas = 0;
        var informadas = new HashSet<(int EmployeeId, Guid Obligation)>();

        // Una novedad por empleado, obligación y período: las cuotas de una obligación que vencen en el período se suman.
        foreach (var grupo in cuotas.Installments.GroupBy(c => (c.PersonPublicId, c.ObligationPublicId)).OrderBy(g => g.Min(c => c.Order)).ThenBy(g => g.Key.ObligationPublicId))
        {
            if (!empleadoPorPersona.TryGetValue(grupo.Key.PersonPublicId, out var empleado)) continue;
            var valor = grupo.Sum(c => c.Amount);
            if (valor <= 0m) continue;
            var primera = grupo.OrderBy(c => c.DueDate).First();
            informadas.Add((empleado.Id, grupo.Key.ObligationPublicId));

            var anterior = vigentes.FirstOrDefault(n => n.EmployeeId == empleado.Id && n.ObligationPublicId == grupo.Key.ObligationPublicId);
            if (anterior is not null && anterior.Amount == valor && anterior.ObligationNumber == primera.ObligationNumber) continue;

            var concepto = await NoveltyRules.ResolveConceptAsync(db, WellKnownConceptCodes.LoanDeduction, period.EndDate, empleado.EmployeeClass, ct);
            if (concepto.IsFailure)
            {
                avisos.Add($"Cuota de Cartera del crédito {primera.ObligationNumber}: {concepto.Error.Message}");
                continue;
            }

            var nueva = new PayrollNovelty
            {
                PayPeriodId = period.Id,
                EmployeeId = empleado.Id,
                ConceptDefinitionId = concepto.Value.Id,
                ConceptCode = concepto.Value.Code,
                Amount = valor,
                Notes = grupo.Count() == 1 && primera.InstallmentNumber is { } n
                    ? $"Cuota {n} del crédito {primera.ObligationNumber}"
                    : $"Cuotas del período del crédito {primera.ObligationNumber}",
                Status = NoveltyStatus.Active,
                Origin = NoveltyOrigin.LoanDeduction,
                ObligationPublicId = grupo.Key.ObligationPublicId,
                ObligationNumber = primera.ObligationNumber,
                InstallmentNumber = grupo.Count() == 1 ? primera.InstallmentNumber : null,
                CreatedAt = ahora,
                CreatedBy = quien,
            };
            if (anterior is not null)
            {
                // Nunca se borra: la anterior queda reemplazada y la nueva la nombra (Principio XI).
                anterior.Status = NoveltyStatus.Superseded;
                anterior.StatusReason = "Cartera informó otro valor para la cuota del período: se reemplazó al recalcular.";
                anterior.UpdatedAt = ahora;
                anterior.UpdatedBy = quien;
                nueva.SupersedesNoveltyId = anterior.Id;
                reemplazadas++;
            }
            else creadas++;
            db.PayrollNovelties.Add(nueva);
        }

        foreach (var sobrante in vigentes.Where(n => n.ObligationPublicId is null || !informadas.Contains((n.EmployeeId, n.ObligationPublicId.Value))))
        {
            sobrante.Status = NoveltyStatus.Cancelled;
            sobrante.StatusReason = AnuladaPorCartera;
            sobrante.UpdatedAt = ahora;
            sobrante.UpdatedBy = quien;
            anuladas++;
        }

        if (creadas + reemplazadas + anuladas > 0) await db.SaveChangesAsync(ct);
        return new CuotasDeCarteraMaterializadas(creadas, reemplazadas, anuladas, avisos, migrados);
    }
}
