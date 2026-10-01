using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>DiferenciaDeArqueoAprobada</c> v1 (contracts/mensajes.md §6.15): un mensaje por arqueo, con una línea por
/// medio con diferencia (también la aceptada dentro de la tolerancia, con motivo; T50).
/// </summary>
public sealed record DiferenciaDeArqueoAprobadaV1
{
    public const string Type = "DiferenciaDeArqueoAprobada";

    public string Operation { get; init; } = "DiferenciaDeArqueo";

    public string PointOfSaleCode { get; init; } = string.Empty;

    public string CashRegisterCode { get; init; } = string.Empty;

    public Guid CashSessionPublicId { get; init; }

    public CashierV1 Cashier { get; init; } = new();

    /// <summary>Nula si todo quedó dentro de la tolerancia.</summary>
    public ApprovalRefV1? Approval { get; init; }

    public IReadOnlyList<CashCountDifferenceLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>cashier</c> (§6.15). <see cref="PersonPublicId"/> es obligatorio si alguna línea es <c>ShortageToCashier</c>.
/// </summary>
public sealed record CashierV1
{
    public Guid? CentralUserId { get; init; }

    public Guid? PersonPublicId { get; init; }

    public string Name { get; init; } = string.Empty;
}

/// <summary>Un medio con diferencia (§6.15). <c>Difference = Counted − Expected</c>: positivo sobra, negativo falta.</summary>
public sealed record CashCountDifferenceLineV1
{
    public string PaymentMeansCode { get; init; } = string.Empty;

    public PaymentMeansClass PaymentMeansClass { get; init; }

    public CashCountMethod CountMethod { get; init; }

    public decimal Expected { get; init; }

    public decimal Counted { get; init; }

    public decimal Difference { get; init; }

    public decimal ToleranceAmount { get; init; }

    public bool WithinTolerance { get; init; }

    public CashDifferenceTreatment Treatment { get; init; }

    public string Reason { get; init; } = string.Empty;
}
