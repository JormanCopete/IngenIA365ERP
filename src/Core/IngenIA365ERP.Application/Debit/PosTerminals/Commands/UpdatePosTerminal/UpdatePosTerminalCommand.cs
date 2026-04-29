using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.PosTerminals.Commands.UpdatePosTerminal;

public record UpdatePosTerminalCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string TerminalCode { get; init; } = string.Empty;
    public int InternalCode { get; init; }
    public string? VoucherCode { get; init; }
    public string? MerchantName { get; init; }
    public string? Location { get; init; }
    public string? Status { get; init; }
}

public class UpdatePosTerminalCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePosTerminalCommand, Result>
{
    public async Task<Result> Handle(
        UpdatePosTerminalCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PosTerminals
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.TerminalCode = request.TerminalCode;
        entity.InternalCode = request.InternalCode;
        entity.VoucherCode = request.VoucherCode;
        entity.MerchantName = request.MerchantName;
        entity.Location = request.Location;
        entity.Status = request.Status;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
