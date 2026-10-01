namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// El impacto en costos de un borrador (feature 012, I5, US16, T847; contracts/api.md §9.3; FR-045): <c>POST
/// /api/inventory/documents/{id}/cost-impact</c> corre la confirmación en modo simulación y dice qué documentos confirmados cambiarían de
/// costo, con su porción en existencia y la vendida. Es una consulta: sin clave de operación y sin guardar nada. Exige el <c>Create</c> del
/// grupo del documento y <c>Inventory.Costs.Read</c>; sin ellos, el 404 del documento. (nuevo)
/// </summary>
public sealed partial class InventarioClient
{
    public Task<ResultadoDeInventario<ImpactoEnCostosDto>> ImpactoEnCostosAsync(Guid documento, CancellationToken ct = default) =>
        EnviarAsync<ImpactoEnCostosDto>(HttpMethod.Post, $"{RutasDeGrupo.Documentos}/{documento}/cost-impact", null, null, ct);
}

/// <summary>Un documento afectado por el retroactivo, por producto (<c>CostImpactAffectedDto</c>). (nuevo)</summary>
public sealed record DocumentoAfectadoDto(Guid DocumentPublicId, string? DisplayNumber, ReferenciaDeInventarioDto Product, decimal InventoryAmount, decimal SoldAmount);

/// <summary><c>CostImpactDto</c>: si el borrador es retroactivo, los documentos afectados y el total. (nuevo)</summary>
public sealed record ImpactoEnCostosDto(bool Retroactive, IReadOnlyList<DocumentoAfectadoDto> Affected, decimal Total);
