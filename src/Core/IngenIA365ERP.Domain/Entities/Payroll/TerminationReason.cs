using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_TerminationReasons]. Catálogo de motivos de retiro (feature 010, R7) con
/// semilla del programa (<c>RENUNCIA</c>, <c>DESP_SINJC</c>, <c>DESP_JC</c>, <c>VENC_TERM</c>,
/// <c>MUTUO_ACDO</c>, <c>FIN_OBRA</c>, <c>PER_PRUEBA</c>, <c>MUERTE</c>, <c>PENSION</c>). La
/// cooperativa agrega los suyos, siempre sin indemnización: si un motivo genera indemnización lo
/// dice la ley (CST art. 64), no un catálogo. Un sembrado no se elimina ni cambia de marca; la
/// semilla corrige nombre y base legal de los suyos y nunca toca <see cref="GeneratesSeverancePay"/>
/// de uno existente.
/// </summary>
public class TerminationReason : AuditableEntity
{
    /// <summary>Código de catálogo (<c>CodigoDeCatalogo</c>: 10, mayúsculas, único).</summary>
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>«Genera indemnización» (despido sin justa causa).</summary>
    public bool GeneratesSeverancePay { get; set; }

    /// <summary>Exige fecha de fin del contrato (vencimiento del término fijo, fin de la obra).</summary>
    public bool RequiresContractEndDate { get; set; }

    [MaxLength(120)]
    public string? LegalBasis { get; set; }

    public bool IsSeeded { get; set; }
    public bool IsActive { get; set; } = true;
}
