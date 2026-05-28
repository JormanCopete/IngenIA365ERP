using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Spouses]. Hija de <see cref="Person"/> (1:1).
///
/// <para>
/// Contiene SOLO datos personales del conyuge (nombre, identificacion,
/// direccion, contacto basico).
/// </para>
///
/// <para>
/// Los datos del EMPLEO del conyuge (donde trabaja, salario, cargo,
/// profesion, etc.) viven en <see cref="Associate"/> bajo el prefijo
/// <c>Spouse*</c>, dado que solo tienen sentido cuando la persona es
/// asociado (decision P10).
/// </para>
/// </summary>
public class Spouse : AuditableEntity
{
    public int PersonId { get; set; }

    [MaxLength(80)]
    public string? SpouseName { get; set; }

    [MaxLength(20)]
    public string? SpouseIdNumber { get; set; }

    [MaxLength(2)]
    public string? SpouseIdType { get; set; }

    [MaxLength(80)]
    public string? SpouseIdIssuedAt { get; set; }

    public DateOnly? SpouseIdIssueDate { get; set; }

    [MaxLength(80)]
    public string? SpouseAddress { get; set; }

    [MaxLength(30)]
    public string? SpousePhone { get; set; }

    [MaxLength(40)]
    public string? SpouseCity { get; set; }

    [MaxLength(30)]
    public string? SpouseFax { get; set; }

    public DateOnly? SpouseDateOfBirth { get; set; }

    [MaxLength(2)]
    public string? SpouseGender { get; set; }

    [MaxLength(2)]
    public string? SpouseMailingPref { get; set; }

    [MaxLength(120)]
    public string? SpouseMailingAddress { get; set; }

    // === Navigation ===

    public Person Person { get; set; } = null!;
}
