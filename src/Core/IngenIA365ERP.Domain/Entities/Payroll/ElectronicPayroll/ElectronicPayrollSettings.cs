using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.ElectronicPayroll;

/// <summary>
/// Habilitación de la nómina electrónica de la cooperativa (feature 010, US6; data-model §2.9).
/// Fila única. <b>Ningún secreto vive aquí</b>: <see cref="CertificateSecretName"/> y
/// <see cref="PinSecretName"/> son sólo los nombres de los Secrets de Kubernetes que el
/// servicio central lee; el ERP nunca ve el <c>.p12</c>, su contraseña ni el PIN. La entidad
/// nace en N2 para que la migración de N2+N3 sea una (D-12); la lógica llega con N3.
/// </summary>
public class ElectronicPayrollSettings : AuditableEntity
{
    [MaxLength(15)]
    public string EmployerTaxId { get; set; } = string.Empty;

    [MaxLength(1)]
    public string EmployerCheckDigit { get; set; } = string.Empty;

    [MaxLength(150)]
    public string EmployerBusinessName { get; set; } = string.Empty;

    [MaxLength(5)]
    public string EmployerMunicipalityDaneCode { get; set; } = string.Empty;

    [MaxLength(120)]
    public string EmployerAddress { get; set; } = string.Empty;

    [MaxLength(2)]
    public string EmployerCountryCode { get; set; } = "CO";

    public ElectronicPayrollMode Mode { get; set; } = ElectronicPayrollMode.OwnSoftware;

    public DianEnvironment Environment { get; set; } = DianEnvironment.Testing;

    [MaxLength(36)]
    public string? SoftwareId { get; set; }

    [MaxLength(36)]
    public string? TestSetId { get; set; }

    public TestSetStatus TestSetStatus { get; set; } = TestSetStatus.NotStarted;

    public DateTime? TestSetAcceptedAt { get; set; }

    public string? TestSetResultJson { get; set; }

    /// <summary>Sólo el nombre del Secret (<c>nomina-electronica-{slug}-certificado</c>).</summary>
    [MaxLength(120)]
    public string? CertificateSecretName { get; set; }

    /// <summary>Sólo el nombre del Secret (<c>nomina-electronica-{slug}-pin</c>).</summary>
    [MaxLength(120)]
    public string? PinSecretName { get; set; }

    [MaxLength(64)]
    public string? CertificateThumbprint { get; set; }

    public DateOnly? CertificateExpiresAt { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? EnabledAt { get; set; }

    [MaxLength(100)]
    public string? EnabledBy { get; set; }
}

/// <summary>
/// Rango de numeración interna del empleador por tipo de documento y ambiente. El último
/// número emitido se llama <see cref="LastIssuedNumber"/> y no «NextNumber» a propósito: ese
/// nombre es el del contrato de contabilidad y lo vigila una prueba de arquitectura.
/// </summary>
public class ElectronicPayrollNumberingRange : AuditableEntity
{
    /// <summary>102 documento individual, 103 nota de ajuste.</summary>
    public short DocumentType { get; set; }

    public DianEnvironment Environment { get; set; }

    [MaxLength(10)]
    public string Prefix { get; set; } = string.Empty;

    public long RangeFrom { get; set; }
    public long RangeTo { get; set; }

    public long LastIssuedNumber { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public bool IsActive { get; set; } = true;
}
