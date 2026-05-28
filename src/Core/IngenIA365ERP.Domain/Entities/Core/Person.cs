using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_People] (sys_maenit + cnt_nit + inv_Vendedor unificadas).
///
/// <para>
/// Tabla MAESTRA centralizada de personas. Solo contiene datos VERDADERAMENTE
/// comunes a cualquier persona (identificacion, contacto basico, demografia,
/// datos fiscales/contables y banca de proveedor/recaudo de cliente).
/// </para>
///
/// <para>
/// Los datos especificos de cada ROL viven en tablas hijas con FK a Person:
///   - <see cref="Associate"/>           = rol asociado de la cooperativa.
///   - <see cref="Payroll.Employee"/>    = rol empleado interno (planilla cooperativa).
///   - <see cref="Spouse"/>              = info personal del conyuge (no laboral).
///   - <see cref="PersonFinancial"/>     = capacidad de pago / scoring crediticio.
///   - <see cref="Inventory.Salesperson"/> = rol vendedor.
/// </para>
///
/// <para>Los flags <c>Is*</c> indican que roles tiene la persona; las tablas hijas
/// existen solo si el flag esta en true.</para>
/// </summary>
public class Person : AuditableEntity
{
    // === IDENTIFICACION ===

    [MaxLength(20)]
    public string? LegacyCode { get; set; }

    [MaxLength(150)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string TaxId { get; set; } = string.Empty;

    [MaxLength(2)]
    public string? TaxIdCheckDigit { get; set; }

    [MaxLength(40)]
    public string? IdIssuedAt { get; set; }

    [MaxLength(2)]
    public string IdType { get; set; } = "C";

    public DateOnly? IdIssueDate { get; set; }

    [MaxLength(2)]
    public string? PersonType { get; set; } // 01 = Natural, 02 = Juridica

    [MaxLength(150)]
    public string? BusinessName { get; set; }

    [MaxLength(20)]
    public string? PreviousCode { get; set; }

    public short NaturalLegalType { get; set; }

    // === CONTACTO ===

    [MaxLength(120)]
    public string? Address { get; set; }

    [MaxLength(40)]
    public string? Phone1 { get; set; }

    [MaxLength(40)]
    public string? Phone2 { get; set; }

    [MaxLength(30)]
    public string? Fax { get; set; }

    [MaxLength(30)]
    public string? Mobile { get; set; }

    [MaxLength(120)]
    public string? Email { get; set; }

    public int? CityId { get; set; }

    [MaxLength(120)]
    public string? MailingAddress { get; set; }

    [MaxLength(2)]
    public string? MailingPreference { get; set; }

    public int? MailingCityId { get; set; }

    [MaxLength(2)]
    public string? EmailType { get; set; }

    [MaxLength(20)]
    public string? DaneCityCode { get; set; }

    // === DEMOGRAFIA ===

    [MaxLength(2)]
    public string? Gender { get; set; }

    [MaxLength(2)]
    public string? MaritalStatus { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(2)]
    public string? EducationLevel { get; set; }

    [MaxLength(4)]
    public string? SocialStratum { get; set; }

    [MaxLength(2)]
    public string? HousingType { get; set; }

    public bool HasVehicle { get; set; }
    public int VehicleType { get; set; }
    public bool IsHeadOfHousehold { get; set; }

    [MaxLength(2)]
    public string? WorkShift { get; set; }

    // === DATOS FISCALES / CONTABLES (cnt_nit fusionado, decision P4) ===

    public bool WithholdingExempt { get; set; }
    public bool IcaWithholdingExempt { get; set; }

    [MaxLength(2)]
    public string? TaxRegime { get; set; }

    [MaxLength(6)]
    public string? IcaType { get; set; }

    public bool IsLargeContributor { get; set; }
    public decimal? IcaRate { get; set; }

    [MaxLength(6)]
    public string? DataOrigin { get; set; }

    public short PaymentDays { get; set; }
    public bool HasTaxLien { get; set; }
    public bool HasSpecialPrice { get; set; }
    public bool IsEmployerClient { get; set; }
    public bool SourceWithholding { get; set; }
    public bool NaturalHasRut { get; set; }

    [MaxLength(20)]
    public string? CiiuCode { get; set; }

    [MaxLength(2)]
    public string? ThirdPartyType { get; set; }

    // Withholding auxiliar
    public bool WithholdingAux { get; set; }
    public decimal? WithholdingAuxAmount { get; set; }
    public decimal? WithholdingAuxPct { get; set; }

    [MaxLength(20)]
    public string? WithholdingAuxAccount { get; set; }

    // === BANCA DEL PROVEEDOR / RECAUDO DE CLIENTES (decision P5c) ===
    // Para pagos a proveedores y recaudos de clientes. La banca del rol
    // empleado (nomina) y del rol asociado (depositos) viven en sus
    // respectivas tablas hijas.

    [MaxLength(20)]
    public string? SupplierBankCode { get; set; }

    [MaxLength(2)]
    public string? SupplierBankAccountType { get; set; }

    [MaxLength(30)]
    public string? SupplierBankAccountNumber { get; set; }

    [MaxLength(20)]
    public string? SupplierAdvisorId { get; set; }

    // === FLAGS DE ROL ===

    public bool IsAssociate { get; set; }
    public bool IsEmployee { get; set; }
    public bool IsAdvisor { get; set; }
    public bool IsThirdParty { get; set; }
    public bool ReceivesInvoice { get; set; }

    // Nuevos (decision P7 + P6)
    public bool IsCustomer { get; set; }
    public bool IsSupplier { get; set; }
    public bool IsSalesperson { get; set; }

    // === ESTADO ===

    [MaxLength(2)]
    public string? Status { get; set; }

    public bool IsDisabled { get; set; }
    public bool IsInsolvent { get; set; }
    public bool IsDeceased { get; set; }

    // === LEGACY AUDIT (preservado del SOLIDO original) ===

    [MaxLength(20)]
    public string? LegacyUser { get; set; }

    [MaxLength(80)]
    public string? LegacyUserName { get; set; }

    public DateTime? LegacyRecordDate { get; set; }
    public DateTime? LegacySystemDate { get; set; }

    // === Navigation properties ===

    public City? City { get; set; }
    public City? MailingCity { get; set; }
    public Associate? Associate { get; set; }
    public Spouse? Spouse { get; set; }
    public PersonFinancial? Financial { get; set; }
    public AssociateCategory? AssociateCategory { get; set; }
    public ICollection<Beneficiary> Beneficiaries { get; set; } = [];
    public ICollection<Reference> References { get; set; } = [];
    public ICollection<CommitteeMember> CommitteeMemberships { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
