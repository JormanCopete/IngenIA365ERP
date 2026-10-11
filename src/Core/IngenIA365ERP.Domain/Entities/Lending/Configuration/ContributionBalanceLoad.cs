using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_ContributionBalanceLoads</c> (feature 013, T0210; data-model §2.15; D19, FR-029): saldo de aportes y de ahorro permanente
/// cargado por plantilla mientras la consulta de aportes responda «no disponible». Hecho (C42).
/// </summary>
public class ContributionBalanceLoad : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; init; }

    public DateOnly AsOfDate { get; init; }

    public decimal ContributionsBalance { get; init; }

    public decimal PermanentSavingsBalance { get; init; }

    /// <summary>Lote de la plantilla <c>lending.contribution-balances</c>.</summary>
    public Guid ImportPublicId { get; init; }

    public DateTime RegisteredAt { get; init; }

    public int RegisteredByUserId { get; init; }

    /// <summary>Fuente del saldo cargado (columna de la plantilla, C118).</summary>
    public string? Source { get; init; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --

    /// <summary>Una fila de la carga (D19): saldos no negativos y la fecha de corte que no es futura (la valida la plantilla con hoy).</summary>
    public static ContributionBalanceLoad Registrar(int personId, DateOnly corte, decimal aportes, decimal ahorroPermanente, Guid lote,
        DateTime registradaEl, int usuarioId, string? fuente = null)
    {
        if (aportes < 0m || ahorroPermanente < 0m) throw new ArgumentException("Los saldos de aportes y de ahorro permanente no son negativos.");
        return new ContributionBalanceLoad
        {
            PersonId = personId,
            AsOfDate = corte,
            ContributionsBalance = aportes,
            PermanentSavingsBalance = ahorroPermanente,
            ImportPublicId = lote,
            RegisteredAt = registradaEl,
            RegisteredByUserId = usuarioId,
            Source = fuente,
        };
    }

    /// <summary>
    /// La que usa el cupo a la fecha <paramref name="fecha"/> con lo conocido hasta <paramref name="hasta"/> (D19): la de mayor corte ≤ D
    /// registrada ≤ R; con dos del mismo corte, la registrada después.
    /// </summary>
    public static ContributionBalanceLoad? Vigente(IEnumerable<ContributionBalanceLoad> cargas, DateOnly fecha, DateTime hasta) =>
        cargas.Where(c => c.AsOfDate <= fecha && c.RegisteredAt <= hasta && !c.IsDeleted)
            .OrderByDescending(c => c.AsOfDate).ThenByDescending(c => c.RegisteredAt).ThenByDescending(c => c.Id)
            .FirstOrDefault();
}
