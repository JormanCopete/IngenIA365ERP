using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Statements.Commands.GenerateStatements;

// DTOs
public record StatementGenerationResultDto
{
    public int StatementsGenerated { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
}

// Command
public record GenerateStatementsCommand(int Year, int Month) : IRequest<Result<StatementGenerationResultDto>>;

// Handler
public class GenerateStatementsCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<GenerateStatementsCommand, Result<StatementGenerationResultDto>>
{
    public async Task<Result<StatementGenerationResultDto>> Handle(
        GenerateStatementsCommand request,
        CancellationToken cancellationToken)
    {
        var periodCode = request.Year * 100 + request.Month;

        // Get all associates with active products (by PersonId)
        var associateIds = new HashSet<int>();

        // From loan portfolios
        var loanPersonIds = await context.LoanPortfolios
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.CurrentBalance > 0 && !p.IsDeleted)
            .Select(p => p.PersonId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in loanPersonIds) associateIds.Add(id);

        // From CDTs
        var cdtPersonIds = await context.Certificates
            .AsNoTracking()
            .Where(c => c.Status != "C" && !c.IsDeleted && !c.IsDeleted)
            .Select(c => c.PersonId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Statement generation is typically a report-generation process
        // In a real implementation, this would create PDF/data records per associate
        // For now, we record the count of statements that would be generated

        return Result.Success(new StatementGenerationResultDto
        {
            StatementsGenerated = associateIds.Count,
            Year = request.Year,
            Month = request.Month
        });
    }
}
