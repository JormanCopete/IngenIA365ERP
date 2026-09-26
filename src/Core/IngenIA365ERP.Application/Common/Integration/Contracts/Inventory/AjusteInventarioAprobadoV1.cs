namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>AjusteInventarioAprobado</c> v1 (contracts/mensajes.md §6.5): cantidades y costo de un ajuste positivo o
/// negativo, consumo interno, baja o ensamble (el ensamble trae entradas y salidas), más base e IVA en el retiro
/// gravado. <see cref="Operation"/> es <c>AjustePositivo</c>, <c>AjusteNegativo</c>, <c>ConsumoInterno</c>,
/// <c>RetiroGravado</c>, <c>Baja</c> o <c>Ensamble</c>.
/// </summary>
public sealed record AjusteInventarioAprobadoV1
{
    public const string Type = "AjusteInventarioAprobado";

    public string Operation { get; init; } = string.Empty;

    /// <summary><c>INV_AdjustmentCauses.Code</c>; obligatorio con <c>AjusteNegativo</c> y <c>Baja</c>.</summary>
    public string? CauseCode { get; init; }

    /// <summary>Conteo o traslado que lo originó. Informativo: no es dependencia.</summary>
    public DocumentRefV1? SourceDocument { get; init; }

    public IReadOnlyList<CostLineV1> Lines { get; init; } = [];

    /// <summary>Sólo con <c>RetiroGravado</c>.</summary>
    public TaxableWithdrawalV1? TaxableWithdrawal { get; init; }
}

/// <summary><c>taxableWithdrawal</c> (§6.5): base a la lista general vigente e IVA <c>Generated</c> del producto.</summary>
public sealed record TaxableWithdrawalV1
{
    public Guid PriceListPublicId { get; init; }

    public decimal Base { get; init; }

    public IReadOnlyList<TaxLineV1> Taxes { get; init; } = [];
}
