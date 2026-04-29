using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.VoucherTypes.Commands.UpdateVoucherType;

public record UpdateVoucherTypeCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? DocumentType { get; init; }
    public bool ControlSequential { get; init; }
}

public class UpdateVoucherTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateVoucherTypeCommand, Result>
{
    public async Task<Result> Handle(
        UpdateVoucherTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.VoucherTypes
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.DocumentType = request.DocumentType;
        entity.ControlSequential = request.ControlSequential;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
