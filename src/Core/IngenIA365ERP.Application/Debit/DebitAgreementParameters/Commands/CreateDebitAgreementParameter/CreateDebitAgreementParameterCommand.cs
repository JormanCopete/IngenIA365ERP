using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Debit;
using MediatR;

namespace IngenIA365ERP.Application.Debit.DebitAgreementParameters.Commands.CreateDebitAgreementParameter;

public record CreateDebitAgreementParameterCommand : IRequest<Result<Guid>>
{
    public string AgreementCode { get; init; } = string.Empty;
    public int TokenRS { get; init; }
    public string? Name { get; init; }
}

public class CreateDebitAgreementParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDebitAgreementParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateDebitAgreementParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new DebitAgreementParameter
        {
            AgreementCode = request.AgreementCode,
            TokenRS = request.TokenRS,
            Name = request.Name,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.DebitAgreementParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateDebitAgreementParameterCommandValidator : AbstractValidator<CreateDebitAgreementParameterCommand>
{
    public CreateDebitAgreementParameterCommandValidator()
    {
        RuleFor(x => x.AgreementCode)
            .NotEmpty().WithMessage("Agreement code is required.")
            .MaximumLength(10).WithMessage("Agreement code must not exceed 10 characters.");

        RuleFor(x => x.Name)
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}
