using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ContributionReductions].</summary>
public class ContributionReduction : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int SavingsLineId { get; set; }
    public int Period { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal DayBalance1 { get; set; }
    public decimal DayBalance2 { get; set; }
    public decimal DayBalance3 { get; set; }
    public decimal DayBalance4 { get; set; }
    public decimal DayBalance5 { get; set; }
    public decimal DayBalance6 { get; set; }
    public decimal DayBalance7 { get; set; }
    public decimal DayBalance8 { get; set; }
    public decimal DayBalance9 { get; set; }
    public decimal DayBalance10 { get; set; }
    public decimal DayBalance11 { get; set; }
    public decimal DayBalance12 { get; set; }
    public decimal DayBalance13 { get; set; }
    public decimal DayBalance14 { get; set; }
    public decimal DayBalance15 { get; set; }
    public decimal DayBalance16 { get; set; }
    public decimal DayBalance17 { get; set; }
    public decimal DayBalance18 { get; set; }
    public decimal DayBalance19 { get; set; }
    public decimal DayBalance20 { get; set; }
    public decimal DayBalance21 { get; set; }
    public decimal DayBalance22 { get; set; }
    public decimal DayBalance23 { get; set; }
    public decimal DayBalance24 { get; set; }
    public decimal DayBalance25 { get; set; }
    public decimal DayBalance26 { get; set; }
    public decimal DayBalance27 { get; set; }
    public decimal DayBalance28 { get; set; }
    public decimal DayBalance29 { get; set; }
    public decimal DayBalance30 { get; set; }
    public decimal DayBalance31 { get; set; }
    public decimal Average { get; set; }
}
