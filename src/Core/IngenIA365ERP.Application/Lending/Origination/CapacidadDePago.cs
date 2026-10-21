using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// La capacidad de pago de una parte (FR-036): ingresos, egresos, cuotas propias (créditos vivos con la cooperativa), cuotas con terceros y la
/// cuota simulada, todo mensual. <see cref="Declarada"/> falso si la parte no declaró su ingreso: entonces no se puede afirmar que alcance.
/// (nuevo)
/// </summary>
public sealed record CapacidadCalculada(
    bool Declarada,
    decimal Ingresos,
    decimal Egresos,
    decimal CuotasPropias,
    decimal CuotasExternas,
    decimal CuotaSimulada)
{
    /// <summary>Lo que queda después de todo: negativo = no alcanza.</summary>
    public decimal Disponible => Ingresos - Egresos - CuotasPropias - CuotasExternas - CuotaSimulada;

    public bool Alcanza => Declarada && Disponible >= 0m;

    /// <summary>Las cuotas (propias, externas y la simulada) sobre el ingreso: la fuente <c>Application.PaymentCapacityRatio</c> del puntaje.</summary>
    public decimal? Relacion => Declarada && Ingresos > 0m
        ? decimal.Round((CuotasPropias + CuotasExternas + CuotaSimulada) / Ingresos, 6, MidpointRounding.AwayFromZero)
        : null;
}

/// <summary>
/// La capacidad de descuento por nómina propia (FR-110 desde K1): la fracción del ingreso que la ley deja descontar
/// (<c>LIBRANZA_CAPACIDAD_DESCUENTO</c>), lo que ya descuentan los créditos por nómina y la cuota simulada. (nuevo)
/// </summary>
public sealed record CapacidadDeDescuento(decimal Ingreso, decimal Fraccion, decimal DescuentosVigentes, decimal CuotaSimulada, string Fuente)
{
    public decimal Permitido => decimal.Round(Ingreso * Fraccion, 2, MidpointRounding.AwayFromZero);

    public decimal Disponible => Permitido - DescuentosVigentes - CuotaSimulada;

    public bool Alcanza => Ingreso > 0m && Disponible >= 0m;
}

/// <summary>
/// Calcula la capacidad de pago y la de descuento (feature 013, T0391; FR-036, FR-110): <c>ingresos − egresos − cuotas propias y externas − la
/// cuota simulada</c>. Las cuotas propias son, por cada crédito vivo de la persona como deudora, la próxima cuota de su plan vigente (la última
/// versión de <c>LND_RepaymentScheduleVersions</c>) a la fecha; las de nómina propia son las de los créditos con
/// <c>LND_LoanPayrollLinks.IsOwnPayroll</c>. Sólo lee. (nuevo)
/// </summary>
public sealed class CapacidadDePago(IApplicationDbContext db, IValoresNacionales valores)
{
    private static readonly LoanStatus[] Vivos = [LoanStatus.PendingDisbursement, LoanStatus.Active];

    public async Task<CapacidadCalculada> DePagoAsync(CreditApplicationParty parte, decimal cuotaSimulada, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parte);
        var propias = await CuotasPropiasAsync(parte.PersonId, fecha, soloNomina: false, ct);
        return new CapacidadCalculada(parte.MonthlyIncome is not null, parte.MonthlyIncome ?? 0m, parte.MonthlyExpenses ?? 0m, propias,
            parte.ExternalInstallments ?? 0m, cuotaSimulada);
    }

    /// <summary>
    /// La capacidad de descuento del solicitante en nómina propia contra el límite legal vigente a la fecha. Sin el límite replicado,
    /// <c>NationalValues.Missing</c> (el lector alerta).
    /// </summary>
    public async Task<Result<CapacidadDeDescuento>> DeDescuentoAsync(CreditApplicationParty solicitante, decimal cuotaSimulada, DateOnly fecha,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitante);
        var limite = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.LibranzaCapacidadDescuento, fecha, null, ct);
        if (limite.IsFailure) return Result.Failure<CapacidadDeDescuento>(limite.Error);
        var nomina = await CuotasPropiasAsync(solicitante.PersonId, fecha, soloNomina: true, ct);
        return Result.Success(new CapacidadDeDescuento(solicitante.MonthlyIncome ?? 0m, limite.Value.Valor ?? 0m, nomina, cuotaSimulada,
            limite.Value.Fuente));
    }

    /// <summary>La próxima cuota del plan vigente de cada crédito vivo de la persona (de nómina propia, si se pide), sumadas.</summary>
    public async Task<decimal> CuotasPropiasAsync(int personId, DateOnly fecha, bool soloNomina, CancellationToken ct)
    {
        var creditos = await db.Loans.AsNoTracking()
            .Where(l => !l.IsDeleted && l.DebtorPersonId == personId && Vivos.Contains(l.Status))
            .Select(l => l.Id)
            .ToListAsync(ct);
        if (soloNomina && creditos.Count > 0)
        {
            var deNomina = await db.LoanPayrollLinks.AsNoTracking()
                .Where(p => !p.IsDeleted && creditos.Contains(p.LoanId) && p.IsOwnPayroll)
                .Select(p => p.LoanId)
                .ToListAsync(ct);
            creditos = creditos.Intersect(deNomina).ToList();
        }
        if (creditos.Count == 0) return 0m;

        var planes = await db.RepaymentScheduleVersions.AsNoTracking()
            .Where(v => creditos.Contains(v.LoanId) && !v.IsDeleted)
            .Select(v => new { v.Id, v.LoanId, v.VersionNumber })
            .ToListAsync(ct);
        var vigentes = planes.GroupBy(p => p.LoanId).Select(g => g.MaxBy(p => p.VersionNumber)!.Id).ToArray();
        if (vigentes.Length == 0) return 0m;

        var proximas = await db.RepaymentInstallments.AsNoTracking()
            .Where(c => vigentes.Contains(c.ScheduleVersionId) && c.DueDate >= fecha)
            .Select(c => new { c.ScheduleVersionId, c.DueDate, c.Total })
            .ToListAsync(ct);
        return proximas.GroupBy(c => c.ScheduleVersionId).Sum(g => g.MinBy(c => c.DueDate)!.Total);
    }
}
