using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports;

// --- DTOs ---

public record PersonLoanSummaryDto(
    Guid LoanPublicId,
    string PortfolioNumber,
    string CreditLineName,
    decimal ApprovedAmount,
    decimal CurrentBalance,
    decimal OverdueAmount,
    decimal RemainingInstallments,
    DateTime? DisbursementDate,
    string Status);

public record PersonSavingsSummaryDto(
    Guid AccountPublicId,
    string SavingsLineId,
    string AccountNumber,
    decimal Balance,
    string Status);

public record PersonCdtSummaryDto(
    Guid CdtPublicId,
    string CertificateNumber,
    decimal Amount,
    decimal InterestRate,
    int Term,
    DateOnly? MaturityDate,
    string Status);

public record PersonContributionDto(
    string ConceptName,
    decimal Balance);

public record PersonPortfolioDto(
    Guid PersonPublicId,
    string PersonName,
    string TaxId,
    List<PersonLoanSummaryDto> Loans,
    List<PersonSavingsSummaryDto> Savings,
    List<PersonCdtSummaryDto> Cdts,
    List<PersonContributionDto> Contributions,
    decimal TotalDebt,
    decimal TotalSavings,
    decimal TotalCdts);

// --- Query ---

public record GetPersonPortfolioReportQuery(
    Guid PersonPublicId) : IRequest<Result<PersonPortfolioDto>>;

public class GetPersonPortfolioReportQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPersonPortfolioReportQuery, Result<PersonPortfolioDto>>
{
    public async Task<Result<PersonPortfolioDto>> Handle(GetPersonPortfolioReportQuery request, CancellationToken ct)
    {
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);

        if (person is null)
            return Result.Failure<PersonPortfolioDto>(new Error("Person.NotFound", "Persona no encontrada"));

        // Loans
        var loans = await context.LoanPortfolios.AsNoTracking()
            .Where(lp => lp.PersonId == person.Id && !lp.IsDeleted)
            .ToListAsync(ct);

        var loanDtos = loans.Select(l => new PersonLoanSummaryDto(
            l.PublicId,
            l.PortfolioNumber.ToString(),
            "", // CreditLineName — would require join to CreditLineParameter
            l.ApprovedAmount,
            l.CurrentBalance,
            l.InterestBalanceCurrent + l.DefaultBalanceCurrent,
            l.PendingInstallmentCount,
            l.DisbursementDate.ToDateTime(TimeOnly.MinValue),
            l.IsWrittenOff == "S" ? "Castigado" : "Vigente")).ToList();

        // Savings — join on PersonCode (legacy string key)
        var personCode = person.LegacyCode ?? person.TaxId;
        var savings = await context.SavingsAccounts.AsNoTracking()
            .Where(sa => sa.PersonCode == personCode && !sa.IsDeleted)
            .ToListAsync(ct);

        var savingsDtos = savings.Select(s => new PersonSavingsSummaryDto(
            s.PublicId,
            s.SavingsLineId.ToString(),
            s.AccountNumber.ToString(),
            0m, // Balance not stored on SavingsAccount entity directly
            "Activa")).ToList();

        // CDTs
        var cdts = await context.Certificates.AsNoTracking()
            .Where(c => c.PersonId == person.Id && !c.IsDeleted)
            .ToListAsync(ct);

        var cdtDtos = cdts.Select(c => new PersonCdtSummaryDto(
            c.PublicId,
            c.CertificateNumber,
            c.Amount,
            c.InterestRate,
            c.Term,
            c.MaturityDate,
            c.Status)).ToList();

        return Result.Success(new PersonPortfolioDto(
            person.PublicId,
            NombreDePersona.Completo(person),
            person.TaxId,
            loanDtos, savingsDtos, cdtDtos,
            [], // Contributions loaded separately if needed
            loanDtos.Sum(l => l.CurrentBalance),
            savingsDtos.Sum(s => s.Balance),
            cdtDtos.Sum(c => c.Amount)));
    }
}
