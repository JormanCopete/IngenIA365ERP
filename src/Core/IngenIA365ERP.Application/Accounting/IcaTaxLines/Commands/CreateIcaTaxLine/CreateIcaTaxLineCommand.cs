using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.IcaTaxLines.Commands.CreateIcaTaxLine;

public record CreateIcaTaxLineCommand : IRequest<Result<Guid>>
{
    public string LineCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AccountCode { get; init; }
    public decimal TaxRate { get; init; }
    public string? BaseAccountCode { get; init; }
    public string? Sign { get; init; }
}

public class CreateIcaTaxLineCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateIcaTaxLineCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateIcaTaxLineCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new IcaTaxLine
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

        context.IcaTaxLines.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateIcaTaxLineCommandValidator : AbstractValidator<CreateIcaTaxLineCommand>
{
    public CreateIcaTaxLineCommandValidator()
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
