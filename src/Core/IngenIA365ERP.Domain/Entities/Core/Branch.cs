using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Branches] (sys_agencia).
/// </summary>
public class Branch : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? ShortName { get; set; }

    // Feature 009 (R7): vinculo con la oficina registrada en ADM_Branches (por PublicId, sin FK entre bases)
    // para traducir las sucursales asignadas a un usuario al alcance contable.
    public Guid? TenantBranchPublicId { get; set; }

    /// <summary>
    /// Municipio DIVIPOLA de la sucursal (feature 012, T24; T176), referido por código a <c>COR_Cities.DaneCode</c> sin FK:
    /// el código es el dato. ReteICA de compras lo propone al documento (FR-050) y la carga de plantillas lo exige.
    /// </summary>
    [MaxLength(5)]
    public string? MunicipalityDaneCode { get; set; }
}
