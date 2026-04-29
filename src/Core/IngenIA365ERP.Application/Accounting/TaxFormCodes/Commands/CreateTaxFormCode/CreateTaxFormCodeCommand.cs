using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.TaxFormCodes.Commands.CreateTaxFormCode;

public record CreateTaxFormCodeCommand : IRequest<Result<Guid>>
{
    public string? FormCode { get; init; }
    public string? Description { get; init; }
    public string? AccountCode { get; init; }
    public string? ConceptCode { get; init; }
    public string? TaxType { get; init; }
}

public class CreateTaxFormCodeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTaxFormCodeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateTaxFormCodeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new TaxFormCode
        {
            FormCode = request.FormCode,
            Description = request.Description,
            AccountCode = request.AccountCode,
            ConceptCode = request.ConceptCode,
            TaxType = request.TaxType,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.TaxFormCodes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateTaxFormCodeCommandValidator : AbstractValidator<CreateTaxFormCodeCommand>
{
    public CreateTaxFormCodeCommandValidator()
    {
        RuleFor(x => x.FormCode)
            .MaximumLength(10).WithMessage("Form code must not exceed 10 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("Description must not exceed 200 characters.");

        RuleFor(x => x.AccountCode)
            .MaximumLength(20).WithMessage("Account code must not exceed 20 characters.");

        RuleFor(x => x.ConceptCode)
            .MaximumLength(10).WithMessage("Concept code must not exceed 10 characters.");

        RuleFor(x => x.TaxType)
            .MaximumLength(4).WithMessage("Tax type must not exceed 4 characters.");
    }
}
