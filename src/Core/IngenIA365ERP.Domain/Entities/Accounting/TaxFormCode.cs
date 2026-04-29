using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_TaxFormCodes] (cnt_codforimpu).</summary>
public class TaxFormCode : AuditableEntity
{
    public string? FormCode { get; set; }
    public string? Description { get; set; }
    public string? AccountCode { get; set; }
    public string? ConceptCode { get; set; }
    public string? TaxType { get; set; }
    public string? EconomicActivity { get; set; }
    public string? ContributorType { get; set; }
    public string? DocumentTypeCode { get; set; }
    public string? RepresentationCode { get; set; }
    public string? AuditorCode { get; set; }
}
