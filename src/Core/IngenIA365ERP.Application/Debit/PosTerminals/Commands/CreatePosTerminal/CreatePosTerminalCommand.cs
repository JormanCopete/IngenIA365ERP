using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Debit;
using MediatR;

namespace IngenIA365ERP.Application.Debit.PosTerminals.Commands.CreatePosTerminal;

public record CreatePosTerminalCommand : IRequest<Result<Guid>>
{
    public string TerminalCode { get; init; } = string.Empty;
    public int InternalCode { get; init; }
    public string? VoucherCode { get; init; }
    public string? MerchantName { get; init; }
    public string? Location { get; init; }
    public string? Status { get; init; }
}

public class CreatePosTerminalCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePosTerminalCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePosTerminalCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new PosTerminal
        {
            TerminalCode = request.TerminalCode,
            InternalCode = request.InternalCode,
            VoucherCode = request.VoucherCode,
            MerchantName = request.MerchantName,
            Location = request.Location,
            Status = request.Status,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.PosTerminals.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreatePosTerminalCommandValidator : AbstractValidator<CreatePosTerminalCommand>
{
    public CreatePosTerminalCommandValidator()
    {
        RuleFor(x => x.TerminalCode)
            .NotEmpty().WithMessage("Terminal code is required.")
            .MaximumLength(20).WithMessage("Terminal code must not exceed 20 characters.");

        RuleFor(x => x.VoucherCode)
            .MaximumLength(5).WithMessage("Voucher code must not exceed 5 characters.");

        RuleFor(x => x.MerchantName)
            .MaximumLength(100).WithMessage("Merchant name must not exceed 100 characters.");

        RuleFor(x => x.Location)
            .MaximumLength(100).WithMessage("Location must not exceed 100 characters.");

        RuleFor(x => x.Status)
            .MaximumLength(2).WithMessage("Status must not exceed 2 characters.");
    }
}
