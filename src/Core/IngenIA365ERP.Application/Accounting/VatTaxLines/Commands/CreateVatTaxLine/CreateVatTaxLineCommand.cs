using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.VatTaxLines.Commands.CreateVatTaxLine;

public record CreateVatTaxLineCommand : IRequest<Result<Guid>>
{
    public string LineCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AccountCode { get; init; }
    public decimal TaxRate { get; init; }
    public string? BaseAccountCode { get; init; }
    public string? Sign { get; init; }
}

public class CreateVatTaxLineCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateVatTaxLineCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateVatTaxLineCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new VatTaxLine
        {
            LineCode = request.LineCode,
            Description = request.Description,
            AccountCode = request.AccountCode,
            TaxRate = request.TaxRate,
            BaseAccountCode = request.BaseAccountCode,
            Sign = request.Sign,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.VatTaxLines.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateVatTaxLineCommandValidator : AbstractValidator<CreateVatTaxLineCommand>
{
    public CreateVatTaxLineCommandValidator()
    {
        RuleFor(x => x.LineCode)
            .NotEmpty().WithMessage("Line code is required.")
            .MaximumLength(10).WithMessage("Line code must not exceed 10 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(200).WithMessage("Description must not exceed 200 characters.");

        RuleFor(x => x.AccountCode)
            .MaximumLength(20).WithMessage("Account code must not exceed 20 characters.");

        RuleFor(x => x.BaseAccountCode)
            .MaximumLength(20).WithMessage("Base account code must not exceed 20 characters.");
    }
}
