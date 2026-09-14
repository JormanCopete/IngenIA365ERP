using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Associates.Contracts;
using IngenIA365ERP.Application.Core.Associates.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociate;

/// <summary>
/// Asigna el rol Asociado a una Person existente. Crea fila en COR_Associates
/// y marca Person.IsAssociate = true. Para persona nueva y afiliación en un solo paso
/// está <c>RegisterAssociateWithPersonCommand</c> (feature 008); los dos comparten
/// <see cref="AssociateRegistrar"/>.
/// </summary>
public record RegisterAssociateCommand : AssociateInput, IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
}

public class RegisterAssociateCommandHandler(
    IApplicationDbContext context,
    AssociateRegistrar asociados)
    : IRequestHandler<RegisterAssociateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterAssociateCommand request, CancellationToken ct)
    {
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure<Guid>(new Error("Associate.PersonNotFound",
                "Persona no encontrada."));

        var preparado = await asociados.PrepareAsync(person, request, ct);
        if (preparado.IsFailure)
            return Result.Failure<Guid>(preparado.Error);

        await context.SaveChangesAsync(ct);
        return Result.Success(preparado.Value.PublicId);
    }
}

public class RegisterAssociateCommandValidator : AbstractValidator<RegisterAssociateCommand>
{
    public RegisterAssociateCommandValidator()
    {
        Include(new AssociateInputValidator());
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Persona requerida.");
    }
}
