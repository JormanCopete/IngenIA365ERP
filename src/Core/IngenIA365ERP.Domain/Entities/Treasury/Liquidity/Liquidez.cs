using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Liquidity;

// Feature 015, T2 (fase 43, T0460; FR-137, FR-138, FR-139; data-model §13.2): el flujo de caja proyectado con sus categorías configurables y la foto de la posición
// diaria con sus componentes y los que faltaron. Reemplazan al flujo heredado (que sólo sumaba facturas). (nuevo)

/// <summary>
/// <c>TES_CashFlowCategories</c>: una categoría del flujo de caja de la cooperativa (código de catálogo, FR-138): su sentido, de dónde sale lo proyectado (digitado o un
/// componente automático) y su orden. (nuevo)
/// </summary>
public class CashFlowCategory : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CashFlowDirection Direction { get; set; }

    public CashFlowCategorySource Source { get; set; } = CashFlowCategorySource.Manual;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// <c>TES_CashFlowProjections</c>: lo proyectado a mano de una categoría para un período (semana o mes que empieza en <see cref="PeriodStart"/>). Una sola viva por
/// categoría, clase y período; corregirla la reemplaza. (nuevo)
/// </summary>
public class CashFlowProjection : AuditableEntity
{
    public int CategoryId { get; set; }

    public CashFlowCategory? Category { get; set; }

    public CashFlowPeriodKind PeriodKind { get; set; }

    public DateOnly PeriodStart { get; set; }

    public decimal Amount { get; set; }

    public string? Notes { get; set; }

    public int RegisteredByUserId { get; set; }
}

/// <summary>
/// <c>TES_LiquiditySnapshots</c>: la foto de la posición de un día (FR-137) —bancos, bóvedas, cajas, compromisos y recaudos esperados— con el detalle de sus componentes y
/// los que no respondieron. Es el origen del <c>SaldoDelFondoDeLiquidez</c> que se informa a Captaciones (FR-139). Inmutable. (nuevo)
/// </summary>
public class LiquiditySnapshot : AuditableEntity, IHechoInmutable
{
    public string Number { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public DateTime TakenAt { get; set; }

    public int TakenByUserId { get; set; }

    public decimal BanksTotal { get; set; }

    public decimal VaultsTotal { get; set; }

    public decimal CashSessionsTotal { get; set; }

    public decimal CommitmentsTotal { get; set; }

    /// <summary>Nulo si la consulta de Cartera no respondió.</summary>
    public decimal? ExpectedLending { get; set; }

    /// <summary>Nulo si la consulta de Captaciones no respondió.</summary>
    public decimal? ExpectedDeposits { get; set; }

    /// <summary>Lo disponible: bancos + bóvedas + cajas − compromisos.</summary>
    public decimal Available { get; set; }

    /// <summary>Lo disponible más lo esperado que sí respondió.</summary>
    public decimal Projected { get; set; }

    public decimal LiquidityFundTotal { get; set; }

    public string ComponentsJson { get; set; } = "[]";

    public string MissingComponentsJson { get; set; } = "[]";

    /// <summary>El <c>SaldoDelFondoDeLiquidez</c> emitido con la foto, si había cuentas del fondo.</summary>
    public Guid? FundMessagePublicId { get; set; }
}
