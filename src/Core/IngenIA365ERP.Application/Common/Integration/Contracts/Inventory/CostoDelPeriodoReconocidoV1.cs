namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>CostoDelPeriodoReconocido</c> v1 (2026-11-18, pedido del dueño): el costo que los documentos con
/// <c>Contabilidad.CostoDeVenta = AlCierreDelPeriodo</c> dejaron para el cierre del mes de inventario, en un solo mensaje. Lo emite el
/// cierre del período (origen: el envío <c>INV_PeriodCostPostings</c>, clave <c>Confirmation</c>) y, con <see cref="IsReversal"/>, la
/// reapertura, con los importes con el signo contrario.
///
/// <para>
/// Cada parte es un contenido del catálogo ya sumado —<c>CostoDeVentaReconocido</c>, <c>DevolucionRegistrada</c> de cliente o
/// <c>AjusteDeCostoReconocido</c> por motivo— con la sucursal y el centro de costo del sobre que lo habría llevado: Contabilidad desglosa
/// cada parte como su tipo, con las mismas reglas de la matriz, como hace con el espejo de un <c>DocumentoAnulado</c>.
/// </para>
/// </summary>
public sealed record CostoDelPeriodoReconocidoV1
{
    public const string Type = "CostoDelPeriodoReconocido";

    /// <summary>La operación que da el tipo de comprobante: la del costo de venta.</summary>
    public string Operation { get; init; } = "CostoDeVenta";

    public int Year { get; init; }

    public int Month { get; init; }

    public int ClosingVersion { get; init; }

    /// <summary>La reversión de la reapertura: los importes de las partes van con el signo contrario.</summary>
    public bool IsReversal { get; init; }

    public IReadOnlyList<DeferredCostPartV1> Parts { get; init; } = [];
}

/// <summary>Una parte del costo del período: un contenido del catálogo sumado, con la sucursal y el centro de su sobre.</summary>
public sealed record DeferredCostPartV1
{
    public string Type { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    public Guid BranchPublicId { get; init; }

    public Guid? CostCenterPublicId { get; init; }

    public object? Content { get; init; }
}
