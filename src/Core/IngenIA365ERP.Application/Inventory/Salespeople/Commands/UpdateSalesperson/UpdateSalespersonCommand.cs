using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.UpdateSalesperson;

public record UpdateSalespersonCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string IdNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? LastName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public int? CityId { get; init; }
    public int? SalespersonType { get; init; }
    public bool AppliesCommission { get; init; }
}

public class UpdateSalespersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateSalespersonCommand, Result>
{
    public async Task<Result> Handle(
        UpdateSalespersonCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Salespeople
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.IdNumber = request.IdNumber;
        entity.Name = request.Name;
        entity.LastName = request.LastName;
        entity.Address = request.Address;
        entity.Phone = request.Phone;
        entity.Mobile = request.Mobile;
        entity.CityId = request.CityId;
        entity.SalespersonType = request.SalespersonType;
        entity.AppliesCommission = request.AppliesCommission;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
