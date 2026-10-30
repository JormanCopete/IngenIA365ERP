using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Configuration;

/// <summary>
/// <c>RCD_CollectionPoints</c> (feature 015, T0070; US1; FR-021, FR-026, FR-180; data-model §1.2): un punto de recaudo de una oficina (<c>COR_Branches</c>),
/// con su código de la cooperativa (<c>CodigoDeCatalogo</c>, único entre vivos), su prefijo opcional de numeración y su vigencia. Se inactiva, no se borra.
/// (nuevo)
/// </summary>
public class CollectionPoint : AuditableEntity
{
    /// <summary>La oficina (<c>COR_Branches.Id</c>).</summary>
    public int BranchId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ReceiptsCatalogStatus Status { get; set; } = ReceiptsCatalogStatus.Active;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>El prefijo de los números de los documentos del punto, si el tipo lo usa (FR-026).</summary>
    public string? DocumentPrefix { get; set; }

    /// <summary>La oficina usa su caja principal como bóveda (FR-180).</summary>
    public bool IsMainCashBoxVault { get; set; }

    /// <summary>Si el punto se ofrece en la fecha.</summary>
    public bool VigenteEn(DateOnly fecha) =>
        Status == ReceiptsCatalogStatus.Active && ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}

/// <summary>Dos vigencias <c>[desde, hasta]</c> (nulo = abierta) se cruzan. (nuevo)</summary>
public static class VigenciaDeRecaudo
{
    public static bool SeCruzan(DateOnly desdeA, DateOnly? hastaA, DateOnly desdeB, DateOnly? hastaB) =>
        desdeA <= (hastaB ?? DateOnly.MaxValue) && desdeB <= (hastaA ?? DateOnly.MaxValue);

    public static bool Incluye(DateOnly desde, DateOnly? hasta, DateOnly fecha) => desde <= fecha && (hasta is null || hasta >= fecha);
}
