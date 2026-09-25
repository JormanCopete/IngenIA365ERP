using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>GrupoContableReclasificado</c> v1 (contracts/mensajes.md §6.13): cantidad y valor que pasan de un grupo
/// contable a otro, por bodega. Lo emite la operación <c>ChangeProductAccountingGroupCommand</c> (origen
/// <c>Operation</c>, <c>originEventKey</c> <c>Reclassification</c>) y sigue el valor general del modo de paso.
/// </summary>
public sealed record GrupoContableReclasificadoV1
{
    public const string Type = "GrupoContableReclasificado";

    public string Operation { get; init; } = "Reclasificacion";

    public Guid ProductPublicId { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string FromAccountingGroupCode { get; init; } = string.Empty;

    public string ToAccountingGroupCode { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public IReadOnlyList<ReclassificationLineV1> Lines { get; init; } = [];
}

/// <summary>Una bodega de la reclasificación, a la fecha efectiva (§6.13).</summary>
public sealed record ReclassificationLineV1
{
    public string WarehouseCode { get; init; } = string.Empty;

    public WarehouseBehavior WarehouseBehavior { get; init; }

    public Guid BranchPublicId { get; init; }

    public decimal QuantityBase { get; init; }

    public decimal Value { get; init; }
}
