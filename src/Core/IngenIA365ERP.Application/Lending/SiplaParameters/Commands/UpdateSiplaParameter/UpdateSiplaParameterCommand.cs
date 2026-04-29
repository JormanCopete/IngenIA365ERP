using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SiplaParameters.Commands.UpdateSiplaParameter;

public record UpdateSiplaParameterCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string ConceptCode { get; init; } = string.Empty;
    public string CompanyCode { get; init; } = string.Empty;
    public decimal MonthlyCreditMoves { get; init; }
    public decimal MaxBalance { get; init; }
    public int AccountCount { get; init; }
    public int AnnualTransactions { get; init; }
}

public class UpdateSiplaParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateSiplaParameterCommand, Result>
{
    public async Task<Result> Handle(
        UpdateSiplaParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SiplaParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ConceptCode = request.ConceptCode;
        entity.CompanyCode = request.CompanyCode;
        entity.MonthlyCreditMoves = request.MonthlyCreditMoves;
        entity.MaxBalance = request.MaxBalance;
        entity.AccountCount = request.AccountCount;
        entity.AnnualTransactions = request.AnnualTransactions;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
