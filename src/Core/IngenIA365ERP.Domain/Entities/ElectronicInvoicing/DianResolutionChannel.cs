using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.ElectronicInvoicing;

/// <summary>
/// La asociación de una resolución (prefijo) a un canal o software desde una fecha (<c>COR_DianResolutionChannels</c>;
/// feature 012, I4, T692; data-model §18; FR-064, FR-065). Una sola vigente por resolución, sin cruces.
/// <see cref="TechnicalKey"/> sólo en resoluciones de factura, con <see cref="NoAuditarAttribute"/>, y se responde
/// enmascarada (últimos 4).
/// </summary>
public class DianResolutionChannel : AuditableEntity
{
    public int ResolutionId { get; set; }

    public DianNumberingResolution? Resolution { get; set; }

    /// <summary>Máx. 40.</summary>
    public string ChannelCode { get; set; } = string.Empty;

    /// <summary>Máx. 36.</summary>
    public string? SoftwareId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Clave técnica de la resolución de factura (máx. 100).</summary>
    [NoAuditar]
    public string? TechnicalKey { get; set; }

    /// <summary>Vigente a la fecha (inclusive en los dos extremos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
