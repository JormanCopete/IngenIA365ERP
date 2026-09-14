using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

public class CreatePersonCommandHandler(
    IApplicationDbContext context,
    PersonFactory personas)
    : IRequestHandler<CreatePersonCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePersonCommand request, CancellationToken ct)
    {
        var preparada = await personas.PrepareAsync(request, ct);
        if (preparada.IsFailure)
            return Result.Failure<Guid>(preparada.Error);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PersonFactory.EsColisionDeDocumento(ex))
        {
            // Dos usuarios crearon la misma persona a la vez: ambos pasaron la comprobación y
            // el índice único paró al segundo. Se le responde lo mismo que habría visto un
            // segundo después, con el nombre de quien ganó, en vez de un 500.
            var colision = await personas.TraducirColisionAsync(ex, request.TaxId, ct);
            return Result.Failure<Guid>(colision!);
        }

        return Result.Success(preparada.Value.PublicId);
    }
}
