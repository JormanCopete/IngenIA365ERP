using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.SiplaParameters.Commands.CreateSiplaParameter;

public record CreateSiplaParameterCommand : IRequest<Result<Guid>>
{
    public string ConceptCode { get; init; } = string.Empty;
    public string CompanyCode { get; init; } = string.Empty;
    public decimal MonthlyCreditMoves { get; init; }
    public decimal MaxBalance { get; init; }
    public int AccountCount { get; init; }
    public int AnnualTransactions { get; init; }
}

public class CreateSiplaParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateSiplaParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateSiplaParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new SiplaParameter
        {
            ConceptCode = request.ConceptCode,
            CompanyCode = request.CompanyCode,
            MonthlyCreditMoves = request.MonthlyCreditMoves,
            MaxBalance = request.MaxBalance,
            AccountCount = request.AccountCount,
            AnnualTransactions = request.AnnualTransactions,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.SiplaParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateSiplaParameterCommandValidator : AbstractValidator<CreateSiplaParameterCommand>
{
    public CreateSiplaParameterCommandValidator()
    {
        RuleFor(x => x.ConceptCode)
            .MaximumLength(3).WithMessage("ConceptCode must not exceed 3 characters.");

        RuleFor(x => x.CompanyCode)
            .MaximumLength(5).WithMessage("CompanyCode must not exceed 5 characters.");
    }
}
