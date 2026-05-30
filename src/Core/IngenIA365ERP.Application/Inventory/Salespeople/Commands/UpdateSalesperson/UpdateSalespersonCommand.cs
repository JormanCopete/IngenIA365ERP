using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.UpdateSalesperson;

/// <summary>
/// Actualiza datos del rol vendedor (SalespersonType, AppliesCommission).
/// Datos personales (nombre, contacto) se editan en /maestros/personas.
/// </summary>
public record UpdateSalespersonCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int? SalespersonType { get; init; }
    public bool AppliesCommission { get; init; }
}

public class UpdateSalespersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateSalespersonCommand, Result>
{
    public async Task<Result> Handle(UpdateSalespersonCommand request, CancellationToken ct)
    {
        var entity = await context.Salespeople
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, ct);

        if (entity is null)
            return Result.Failure(new Error("Salesperson.NotFound", "Vendedor no encontrado."));

        entity.SalespersonType = request.SalespersonType;
        entity.AppliesCommission = request.AppliesCommission;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
