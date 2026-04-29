using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.Cards.Commands.BlockCard;

public record BlockCardCommand : IRequest<Result>
{
    public Guid CardPublicId { get; init; }
    public string BlockType { get; init; } = "T"; // T=temporary, D=definitive
    public string? Reason { get; init; }
}

public class BlockCardCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<BlockCardCommand, Result>
{
    public async Task<Result> Handle(
        BlockCardCommand request,
        CancellationToken cancellationToken)
    {
        var card = await context.DebitCards
            .FirstOrDefaultAsync(c => c.PublicId == request.CardPublicId && !c.IsDeleted, cancellationToken);

        if (card is null)
            return Result.Failure(new Error("DebitCard.NotFound",
                "Tarjeta debito no encontrada."));

        if (card.Status == "B" && request.BlockType == "T")
            return Result.Failure(new Error("DebitCard.AlreadyBlocked",
                "La tarjeta ya esta bloqueada temporalmente."));

        if (card.Status == "D")
            return Result.Failure(new Error("DebitCard.PermanentlyBlocked",
                "La tarjeta esta bloqueada definitivamente y no se puede modificar."));

        // Update card status
        card.Status = request.BlockType == "D" ? "D" : "B"; // B=bloqueada temporal, D=bloqueada definitiva
        card.BlockReasonId = request.Reason is not null ? 1 : 0; // Simplified reason mapping
        card.BlockedByUserId = currentUser.UserName;
        card.LastEventDate = DateOnly.FromDateTime(dateTime.UtcNow);
        card.UpdatedAt = dateTime.UtcNow;
        card.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public class BlockCardCommandValidator : AbstractValidator<BlockCardCommand>
{
    public BlockCardCommandValidator()
    {
        RuleFor(x => x.CardPublicId)
            .NotEmpty().WithMessage("La tarjeta es requerida.");

        RuleFor(x => x.BlockType)
            .NotEmpty().WithMessage("El tipo de bloqueo es requerido.")
            .Must(t => t is "T" or "D")
            .WithMessage("El tipo de bloqueo debe ser T (temporal) o D (definitivo).");
    }
}
