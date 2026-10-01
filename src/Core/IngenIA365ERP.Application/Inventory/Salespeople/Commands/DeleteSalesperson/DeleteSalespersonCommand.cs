using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.DeleteSalesperson;

/// <summary>
/// Retira el rol vendedor (feature 012, T425; FR-031; contracts/api.md §31, <c>POST /api/inventory/salespeople/{id}/retire</c>,
/// <c>Inventory.Salespeople.Manage</c>, con <c>Idempotency-Key</c> y motivo obligatorio): baja lógica de la fila y
/// <c>IsSalesperson = false</c> en el mismo <c>SaveChangesAsync</c> (<see cref="RolDeVendedor"/>). Un vendedor inexistente o
/// ya retirado es 404. Volver a darle el rol lo restaura con el mismo <c>PublicId</c>.
/// </summary>
public sealed record DeleteSalespersonCommand(Guid SalespersonPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteSalespersonCommandValidator : ValidadorConMotivo<DeleteSalespersonCommand>
{
    public DeleteSalespersonCommandValidator()
    {
        RuleFor(x => x.SalespersonPublicId).NotEmpty();
    }
}

public sealed class DeleteSalespersonCommandHandler(IApplicationDbContext db, RolDeVendedor rol) : IRequestHandler<DeleteSalespersonCommand, Result>
{
    public async Task<Result> Handle(DeleteSalespersonCommand request, CancellationToken ct)
    {
        var fila = await db.Salespeople.FirstOrDefaultAsync(s => s.PublicId == request.SalespersonPublicId && !s.IsDeleted, ct);
        if (fila is null) return Result.Failure(Error.NotFound);

        var persona = await db.People.FirstOrDefaultAsync(p => p.Id == fila.PersonId, ct);
        await rol.RetirarAsync(fila, persona, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
