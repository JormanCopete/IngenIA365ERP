using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Spouses] — one-to-one with Person for spouse data.
/// Legacy: extracted from sys_maenit CONY* fields.
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

    [MaxLength(80)]
    public string? SpouseEmployer { get; set; }

    [MaxLength(80)]
    public string? SpouseEmployerAddress { get; set; }

    [MaxLength(30)]
    public string? SpousePhone { get; set; }

    [MaxLength(40)]
    public string? SpouseCity { get; set; }

    [MaxLength(10)]
    public string? SpouseProfession { get; set; }

    [MaxLength(60)]
    public string? SpousePosition { get; set; }

    public decimal SpouseSalary { get; set; }

    public DateOnly? SpouseDateOfBirth { get; set; }

    [MaxLength(2)]
    public string? SpouseGender { get; set; }

    [MaxLength(30)]
    public string? SpouseFax { get; set; }

    [MaxLength(2)]
    public string? SpouseMailingPref { get; set; }

    [MaxLength(120)]
    public string? SpouseMailingAddress { get; set; }

    [MaxLength(10)]
    public string? SpouseCompanyCode { get; set; }

    [MaxLength(10)]
    public string? SpouseBranchCode { get; set; }

    [MaxLength(10)]
    public string? SpouseSectionCode { get; set; }

    public DateOnly? SpouseEmployerStart { get; set; }

    [MaxLength(2)]
    public string? SpouseEducationLevel { get; set; }

    [MaxLength(2)]
    public string? SpouseSalaryType { get; set; }

    public decimal? SpouseSeverance { get; set; }

    public decimal? SpouseOtherIncome { get; set; }

    [MaxLength(120)]
    public string? SpouseOtherIncomeDesc { get; set; }

    // Navigation
    public Person Person { get; set; } = null!;
}
