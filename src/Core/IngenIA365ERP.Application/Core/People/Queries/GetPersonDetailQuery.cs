using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Queries;

// --- DTOs ---

public record AssociateDataDto(
    DateOnly? JoinDate,
    decimal ContributionRate,
    string? Status,
    string? CategoryRating,
    string? DeductionType,
    string? EmployerCompanyName,
    string? BranchName,
    string? SectionName);

public record SpouseDataDto(
    string? SpouseName,
    string? SpouseIdNumber,
    string? SpouseEmployer,
    decimal SpouseSalary,
    string? SpousePhone);

public record FinancialDataDto(
    decimal DebtCapacity,
    decimal OtherIncome,
    decimal TotalAssets,
    decimal VariableIncome,
    decimal CreditScore,
    decimal CreditBureauScore,
    string? CreditBureauRating,
    decimal ExternalDebtPayment,
    decimal ExternalDebtBalance);

public record PortfolioSummaryDto(
    int ActiveLoans,
    decimal TotalLoanBalance,
    int SavingsAccounts,
    decimal TotalSavingsBalance,
    decimal TotalContributions,
    decimal TotalOverdueAmount);

public record PersonDetailDto(
    Guid PublicId,
    string FirstName,
    string LastName,
    string? BusinessName,
    string TaxId,
    string IdType,
    string? Address,
    string? Phone1,
    string? Mobile,
    string? Email,
    string? Gender,
    string? MaritalStatus,
    DateOnly? DateOfBirth,
    string? Employer,
    decimal Salary,
    bool IsAssociate,
    bool IsEmployee,
    string? Status,
    AssociateDataDto? AssociateData,
    SpouseDataDto? SpouseData,
    FinancialDataDto? FinancialData,
    PortfolioSummaryDto PortfolioSummary);

// --- Query ---

public record GetPersonDetailQuery(Guid PublicId) : IRequest<Result<PersonDetailDto>>;

public class GetPersonDetailQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPersonDetailQuery, Result<PersonDetailDto>>
{
    public async Task<Result<PersonDetailDto>> Handle(GetPersonDetailQuery request, CancellationToken ct)
    {
        var person = await context.People
            .AsNoTracking()
            .Include(p => p.Associate)
                .ThenInclude(a => a!.EmployerCompany)
            .Include(p => p.Associate)
                .ThenInclude(a => a!.Branch)
            .Include(p => p.Associate)
                .ThenInclude(a => a!.Section)
            .Include(p => p.Spouse)
            .Include(p => p.Financial)
            .FirstOrDefaultAsync(p => p.PublicId == request.PublicId && !p.IsDeleted, ct);

        if (person is null)
            return Result.Failure<PersonDetailDto>(new Error("Person.NotFound",
                "Persona no encontrada."));

        // Associate data
        AssociateDataDto? associateData = null;
        if (person.Associate is not null)
        {
            var a = person.Associate;
            associateData = new AssociateDataDto(
                a.JoinDate,
                a.ContributionRate,
                a.Status,
                a.CategoryRating,
                a.DeductionType,
                a.EmployerCompany?.Name,
                a.Branch?.Name,
                a.Section?.Name);
        }

        // Spouse data
        SpouseDataDto? spouseData = null;
        if (person.Spouse is not null)
        {
            var s = person.Spouse;
            spouseData = new SpouseDataDto(
                s.SpouseName,
                s.SpouseIdNumber,
                s.SpouseEmployer,
                s.SpouseSalary,
                s.SpousePhone);
        }

        // Financial data
        FinancialDataDto? financialData = null;
        if (person.Financial is not null)
        {
            var f = person.Financial;
            financialData = new FinancialDataDto(
                f.DebtCapacity,
                f.OtherIncome,
                f.TotalAssets,
                f.VariableIncome,
                f.CreditScore,
                f.CreditBureauScore,
                f.CreditBureauRating,
                f.ExternalDebtPayment,
                f.ExternalDebtBalance);
        }

        // Portfolio summary
        var activeLoans = await context.LoanPortfolios
            .AsNoTracking()
            .Where(lp => lp.PersonId == person.Id && !lp.IsDeleted && lp.CurrentBalance > 0)
            .CountAsync(ct);

        var totalLoanBalance = await context.LoanPortfolios
            .AsNoTracking()
            .Where(lp => lp.PersonId == person.Id && !lp.IsDeleted && lp.CurrentBalance > 0)
            .SumAsync(lp => lp.CurrentBalance, ct);

        var personCode = person.LegacyCode ?? person.TaxId;
        var savingsCount = await context.SavingsAccounts
            .AsNoTracking()
            .Where(sa => sa.PersonCode == personCode && !sa.IsDeleted)
            .CountAsync(ct);

        // SavingsAccount doesn't store balance directly; set to 0 for now
        var totalSavings = 0m;

        var totalOverdue = await context.LoanPortfolios
            .AsNoTracking()
            .Where(lp => lp.PersonId == person.Id && !lp.IsDeleted && lp.DaysOverdue > 0)
            .SumAsync(lp => lp.DefaultBalanceCurrent + lp.InterestBalanceCurrent, ct);

        var portfolioSummary = new PortfolioSummaryDto(
            activeLoans,
            totalLoanBalance,
            savingsCount,
            totalSavings,
            0m, // contributions - loaded separately if needed
            totalOverdue);

        var dto = new PersonDetailDto(
            person.PublicId,
            person.FirstName,
            person.LastName,
            person.BusinessName,
            person.TaxId,
            person.IdType,
            person.Address,
            person.Phone1,
            person.Mobile,
            person.Email,
            person.Gender,
            person.MaritalStatus,
            person.DateOfBirth,
            person.Employer,
            person.Salary,
            person.IsAssociate,
            person.IsEmployee,
            person.Status,
            associateData,
            spouseData,
            financialData,
            portfolioSummary);

        return Result.Success(dto);
    }
}
