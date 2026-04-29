using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Statements.Queries;

// DTOs
public record PersonStatementDto
{
    public string PersonName { get; init; } = string.Empty;
    public string IdentificationNumber { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Month { get; init; }
    public List<CreditSummaryDto> Credits { get; init; } = [];
    public List<SavingsSummaryDto> Savings { get; init; } = [];
    public List<CdtSummaryDto> CDTs { get; init; } = [];
}

public record CreditSummaryDto
{
    public long PortfolioNumber { get; init; }
    public string CreditLine { get; init; } = string.Empty;
    public decimal CurrentBalance { get; init; }
    public decimal InterestBalance { get; init; }
    public decimal InstallmentAmount { get; init; }
    public int PaidInstallments { get; init; }
}

public record SavingsSummaryDto
{
    public long AccountNumber { get; init; }
    public string SavingsLine { get; init; } = string.Empty;
    public decimal Balance { get; init; }
}

public record CdtSummaryDto
{
    public string CertificateNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Rate { get; init; }
    public DateOnly? MaturityDate { get; init; }
}

// Query
public record GetPersonStatementQuery(Guid PersonPublicId, int Year, int Month)
    : IRequest<Result<PersonStatementDto>>;

public class GetPersonStatementQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPersonStatementQuery, Result<PersonStatementDto>>
{
    public async Task<Result<PersonStatementDto>> Handle(
        GetPersonStatementQuery request,
        CancellationToken cancellationToken)
    {
        var person = await context.People
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted,
                cancellationToken);

        if (person is null)
            return Result.Failure<PersonStatementDto>(new Error("Statement.PersonNotFound", "Persona no encontrada."));

        // Credits
        var credits = await context.LoanPortfolios
            .AsNoTracking()
            .Where(p => p.PersonId == person.Id && !p.IsDeleted && p.CurrentBalance > 0)
            .Include(p => p.CreditLine)
            .Select(p => new CreditSummaryDto
            {
                PortfolioNumber = p.PortfolioNumber,
                CreditLine = p.CreditLine != null ? p.CreditLine.Description : "",
                CurrentBalance = p.CurrentBalance,
                InterestBalance = p.InterestBalanceCurrent,
                InstallmentAmount = p.InstallmentAmount,
                PaidInstallments = p.PaidInstallments
            })
            .ToListAsync(cancellationToken);

        // Savings
        var savings = await context.SavingsAccounts
            .AsNoTracking()
            .Where(sa => sa.PersonCode == person.TaxId && !sa.IsDeleted)
            .ToListAsync(cancellationToken);

        var savingsLineIds = savings.Select(s => s.SavingsLineId).Distinct().ToList();
        var savingsParams = await context.SavingsParameters
            .AsNoTracking()
            .Where(sp => savingsLineIds.Contains(sp.SavingsLineId))
            .ToDictionaryAsync(sp => sp.SavingsLineId, cancellationToken);

        var savingsDtos = savings.Select(sa => new SavingsSummaryDto
        {
            AccountNumber = sa.AccountNumber,
            SavingsLine = savingsParams.TryGetValue(sa.SavingsLineId, out var sp) ? sp.Name : "",
            Balance = 0 // Balance would come from running transaction sum
        }).ToList();

        // CDTs
        var cdts = await context.Certificates
            .AsNoTracking()
            .Where(c => c.PersonId == person.Id && c.Status != "C" && !c.IsDeleted)
            .Select(c => new CdtSummaryDto
            {
                CertificateNumber = c.CertificateNumber,
                Amount = c.Amount,
                Rate = c.InterestRate,
                MaturityDate = c.MaturityDate
            })
            .ToListAsync(cancellationToken);

        return Result.Success(new PersonStatementDto
        {
            PersonName = $"{person.FirstName} {person.LastName}",
            IdentificationNumber = person.TaxId,
            Year = request.Year,
            Month = request.Month,
            Credits = credits,
            Savings = savingsDtos,
            CDTs = cdts
        });
    }
}
