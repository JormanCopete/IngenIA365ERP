using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_PensionProviders] (nom_pensiones).</summary>
public class PensionProvider : AuditableEntity
{
    /// <summary>Código alfanumérico (hasta 10) que elige la cooperativa; único en la tabla. Numérico hasta el 2026-09-12.</summary>
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string TaxId { get; set; } = string.Empty;

    public int CheckDigit { get; set; }

    // Feature 009 (FR-088): persona del maestro que representa a la entidad como tercero contable.
    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    /// <summary>
    /// Cuenta auxiliar propia de la entidad para la nómina (p. ej. el 245005xx de cada EPS): cuando la
    /// tiene, reemplaza la cuenta crédito del concepto al contabilizar las líneas de la entidad
    /// (aportes, provisiones y las deducciones de ley del empleado; ver <c>TercerosDeNomina.EntidadDeLaCuenta</c>).
    /// Nula = la del concepto. Auxiliar, activa y habilitada para Nómina.
    /// </summary>
    public int? PayrollAccountId { get; set; }
    public IngenIA365ERP.Domain.Entities.Accounting.ChartOfAccount? PayrollAccount { get; set; }

    /// <summary>
    /// Feature 010 (US5): código de la administradora en el listado del operador de la PILA
    /// (Res. 2388/2016; seis posiciones, p. ej. <c>EPS010</c>, <c>230301</c>). Es distinto del
    /// <see cref="Code"/> de la cooperativa: aquél es su nomenclatura, éste lo asigna el
    /// Ministerio. Sin él, el cotizante queda con inconsistencia bloqueante (<c>Pila.SinCodigoPila</c>).
    /// </summary>
    [MaxLength(6)]
    public string? PilaCode { get; set; }

    /// <summary>Feature 010: administradora de ahorro individual (ACCAI, Ley 2381 de 2024) en lugar de AFP.</summary>
    public bool IsAccai { get; set; }
}
