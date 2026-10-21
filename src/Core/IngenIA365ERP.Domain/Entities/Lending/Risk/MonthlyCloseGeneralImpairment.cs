using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Risk;

/// <summary>
/// <c>LND_MonthlyCloseGeneralImpairments</c> (feature 013, US9, T0696; data-model §23.3, C42; FR-104): el deterioro general de un cierre por
/// las dimensiones que la matriz necesita (sucursal, modalidad, sub-segmento, empleado y modelo), con el mínimo del perfil, el adicional de la
/// política, su uso para compensar el individual y la diferencia frente al cierre anterior. En el base viene de las filas generales de
/// <c>LND_MigrationRiskData</c>. Hecho. (nuevo)
/// </summary>
public class MonthlyCloseGeneralImpairment : AuditableEntity, IHechoInmutable
{
    public int MonthlyCloseId { get; init; }

    public int BranchId { get; init; }

    public string ModalityCode { get; init; } = string.Empty;

    public string? SubsegmentCode { get; init; }

    public bool IsEmployee { get; init; }

    public ImpairmentModel ImpairmentModel { get; init; }

    /// <summary>La base: cartera bruta.</summary>
    public decimal GrossPortfolio { get; init; }

    /// <summary>Mínimo del perfil acotado por <c>DETERIORO_GENERAL_MINIMO</c>.</summary>
    public decimal MinimumPercentage { get; init; }

    /// <summary>Adicional de la política.</summary>
    public decimal AdditionalPercentage { get; init; }

    public decimal MinimumAmount { get; init; }

    public decimal AdditionalAmount { get; init; }

    public decimal CompensationOfIndividual { get; init; }

    public decimal TotalAmount { get; init; }

    public decimal PreviousAmount { get; init; }

    public decimal Delta { get; init; }

    /// <summary>Norma y política aplicadas.</summary>
    public string SourceJson { get; init; } = "{}";
}
