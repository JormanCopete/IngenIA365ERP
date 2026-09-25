using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>AjusteDeCostoReconocido</c> v1 (contracts/mensajes.md §6.10): sólo la diferencia de costo, partida en
/// existencia y vendido. <b>Uno por documento afectado</b>, con <c>originEventKey</c>
/// <c>Confirmation:{affectedDocumentPublicId:N}</c>: cada parte sigue el destino del mensaje de ese documento.
/// </summary>
public sealed record AjusteDeCostoReconocidoV1
{
    public const string Type = "AjusteDeCostoReconocido";

    public string Operation { get; init; } = "AjusteDeCosto";

    /// <summary>La <c>ReasonCode</c> de su contrapartida.</summary>
    public KardexReason Reason { get; init; }

    /// <summary>Fecha de las líneas de ajuste del kardex y del comprobante.</summary>
    public DateOnly EffectiveDate { get; init; }

    /// <summary>Igual a <c>related</c> del sobre.</summary>
    public DocumentRefV1 AffectedDocument { get; init; } = new();

    public IReadOnlyList<CostDifferenceLineV1> Lines { get; init; } = [];
}
