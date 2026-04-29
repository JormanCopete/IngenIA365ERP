using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.VoucherTypes.Commands.CreateVoucherType;

public record CreateVoucherTypeCommand : IRequest<Result<Guid>>
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? DocumentType { get; init; }
    public bool ControlSequential { get; init; }
    public bool IsActive { get; init; } = true;
}

public class CreateVoucherTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateVoucherTypeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateVoucherTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new VoucherType
        {
            Code = request.Code,
            Name = request.Name,
            ShortName = request.ShortName,
            DocumentType = request.DocumentType,
            ControlSequential = request.ControlSequential,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.VoucherTypes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateVoucherTypeCommandValidator : AbstractValidator<CreateVoucherTypeCommand>
{
    public CreateVoucherTypeCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(4).WithMessage("Code must not exceed 4 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(40).WithMessage("Short name must not exceed 40 characters.");
    }
}
