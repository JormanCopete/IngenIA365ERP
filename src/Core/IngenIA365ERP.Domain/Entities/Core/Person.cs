using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_People] (sys_maenit + cnt_nit).
/// The central entity (~100 columns).
/// </summary>
public class Person : AuditableEntity
{
    // === IDENTIFICATION ===
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
    public string? PersonType { get; set; }

    [MaxLength(150)]
    public string? BusinessName { get; set; }

    [MaxLength(20)]
    public string? PreviousCode { get; set; }

    // === CONTACT ===
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

    // === DEMOGRAPHICS ===
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

    public short NaturalLegalType { get; set; }

    // === EMPLOYMENT ===
    [MaxLength(80)]
    public string? Employer { get; set; }

    public DateOnly? EmployerStartDate { get; set; }

    [MaxLength(2)]
    public string? SalaryType { get; set; }

    public decimal Salary { get; set; }
    public decimal Severance { get; set; }
    public int? ProfessionId { get; set; }
    public int? PositionId { get; set; }

    [MaxLength(100)]
    public string? SeveranceFund { get; set; }

    // === TAX & REGULATORY ===
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

    public decimal CreditLimit { get; set; }

    [MaxLength(2)]
    public string? ThirdPartyType { get; set; }

    // === BANKING ===
    [MaxLength(30)]
    public string? BankAccountNumber { get; set; }

    public int? BankId { get; set; }

    [MaxLength(2)]
    public string? BankAccountType { get; set; }

    public int? BankAccountCityId { get; set; }

    [MaxLength(20)]
    public string? NitBankCode { get; set; }

    [MaxLength(2)]
    public string? NitBankAccountType { get; set; }

    [MaxLength(30)]
    public string? NitBankAccountNumber { get; set; }

    [MaxLength(20)]
    public string? NitAdvisorId { get; set; }

    // === ROLE FLAGS ===
    public bool IsAssociate { get; set; }
    public bool IsEmployee { get; set; }
    public bool IsAdvisor { get; set; }
    public bool IsThirdParty { get; set; }
    public bool ReceivesInvoice { get; set; }

    // === STATUS FLAGS ===
    [MaxLength(2)]
    public string? Status { get; set; }

    public bool IsDisabled { get; set; }
    public bool IsInsolvent { get; set; }
    public bool IsOnVacation { get; set; }
    public bool IsOnUnpaidLeave { get; set; }
    public bool IsPensioner { get; set; }
    public bool IsInsubordinate { get; set; }
    public bool IsDeceased { get; set; }
    public bool IsFromGovernment { get; set; }
    public bool IsPublicResourceAdmin { get; set; }

    [MaxLength(10)]
    public string? PensionType { get; set; }

    [MaxLength(10)]
    public string? SeveranceType { get; set; }

    // === ONLINE ACCESS ===
    [MaxLength(20)]
    public string? InternetPassword { get; set; }

    public bool OnlineConsultation { get; set; }

    [MaxLength(2)]
    public string? ConsultationStatus { get; set; }

    [MaxLength(20)]
    public string? AffiliationCode { get; set; }

    [MaxLength(2)]
    public string? ConsultationChargeType { get; set; }

    public int ConsultationCreditLine { get; set; }

    [MaxLength(40)]
    public string? UserPassword { get; set; }

    // === RISK & COMPLIANCE ===
    public bool AuthCentralRisk { get; set; }

    [MaxLength(2)]
    public string? PosCardClass { get; set; }

    public decimal PosCardLimit { get; set; }
    public decimal InsuranceRiskRate { get; set; }
    public int ZoneTypeId { get; set; }
    public int ZoneId { get; set; }
    public bool IsSiplaExempt { get; set; }
    public DateTime? SiplaExemptDate { get; set; }

    [MaxLength(20)]
    public string? SiplaUser { get; set; }

    public bool SinglePromissoryNote { get; set; }
    public bool PledgesContributions { get; set; }
    public bool InManagement { get; set; }

    // === ACCOUNTING CENTER FLAGS ===
    public bool CpAdmin { get; set; }
    public bool CpContributions { get; set; }
    public bool CpLocal { get; set; }
    public bool CpCommission { get; set; }

    [MaxLength(20)]
    public string? ProfitCenter { get; set; }

    public bool CapacityPayPct { get; set; }

    // === OTHER INCOME DESCRIPTION ===
    [MaxLength(120)]
    public string? OtherIncomeDescription { get; set; }

    // === LEGACY AUDIT ===
    [MaxLength(20)]
    public string? LegacyUser { get; set; }

    [MaxLength(80)]
    public string? LegacyUserName { get; set; }

    public DateTime? LegacyRecordDate { get; set; }
    public DateTime? LegacySystemDate { get; set; }

    // === Navigation properties ===
    public City? City { get; set; }
    public City? MailingCity { get; set; }
    public Bank? Bank { get; set; }
    public Profession? Profession { get; set; }
    public Position? Position { get; set; }
    public Associate? Associate { get; set; }
    public Spouse? Spouse { get; set; }
    public PersonFinancial? Financial { get; set; }
    public AssociateCategory? AssociateCategory { get; set; }
    public ICollection<Beneficiary> Beneficiaries { get; set; } = [];
    public ICollection<Reference> References { get; set; } = [];
    public ICollection<CommitteeMember> CommitteeMemberships { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
