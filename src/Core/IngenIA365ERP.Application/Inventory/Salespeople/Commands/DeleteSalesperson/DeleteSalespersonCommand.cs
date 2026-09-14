using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.DeleteSalesperson;

public record DeleteSalespersonCommand(Guid PublicId) : IRequest<Result>;

public class DeleteSalespersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteSalespersonCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSalespersonCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Salespeople
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.IsDeleted = true;
        entity.DeletedAt = dateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName;

        // La bandera derivada la escribe quien crea o retira la fila hija (Principio V,
        // feature 008): hasta el 2026-09-13 la ficha se eliminaba y «Vendedor» quedaba encendido.
        var person = await context.People
            .FirstOrDefaultAsync(p => p.Id == entity.PersonId && !p.IsDeleted, cancellationToken);
        if (person is not null && person.IsSalesperson)
        {
            person.IsSalesperson = false;
            person.UpdatedAt = dateTime.UtcNow;
            person.UpdatedBy = currentUser.UserName;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
