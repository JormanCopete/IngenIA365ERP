using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Associates.Contracts;
using IngenIA365ERP.Application.Core.Associates.Services;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociateWithPerson;

/// <summary>
/// Persona nueva y asociado en un solo paso (feature 008, US2, FR-001). Un comando —un evento de
/// auditoría (FR-014)— y un <c>SaveChangesAsync</c>: persona, afiliación y bandera
/// <c>IsAssociate</c> quedan las tres o ninguna. Comparte reglas con <c>CreatePersonCommand</c> y
/// <c>RegisterAssociateCommand</c> a través de <see cref="PersonFactory"/> y
/// <see cref="AssociateRegistrar"/>.
/// </summary>
public sealed record RegisterAssociateWithPersonCommand(PersonInput Person, AssociateInput Associate)
    : IRequest<Result<RegisterAssociateWithPersonResult>>;

public sealed record RegisterAssociateWithPersonResult(Guid PersonPublicId, Guid AssociatePublicId);

public sealed class RegisterAssociateWithPersonCommandHandler(
    IApplicationDbContext context,
    PersonFactory personas,
    AssociateRegistrar asociados)
    : IRequestHandler<RegisterAssociateWithPersonCommand, Result<RegisterAssociateWithPersonResult>>
{
    public async Task<Result<RegisterAssociateWithPersonResult>> Handle(
        RegisterAssociateWithPersonCommand request, CancellationToken ct)
    {
        var persona = await personas.PrepareAsync(request.Person, ct);
        if (persona.IsFailure)
            return Result.Failure<RegisterAssociateWithPersonResult>(persona.Error);

        var asociado = await asociados.PrepareAsync(persona.Value, request.Associate, ct);
        if (asociado.IsFailure)
            return Result.Failure<RegisterAssociateWithPersonResult>(asociado.Error);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PersonFactory.EsColisionDeDocumento(ex))
        {
            var colision = await personas.TraducirColisionAsync(ex, request.Person.TaxId, ct);
            return Result.Failure<RegisterAssociateWithPersonResult>(colision!);
        }

        return Result.Success(new RegisterAssociateWithPersonResult(persona.Value.PublicId, asociado.Value.PublicId));
    }
}

public sealed class RegisterAssociateWithPersonCommandValidator : AbstractValidator<RegisterAssociateWithPersonCommand>
{
    public RegisterAssociateWithPersonCommandValidator()
    {
        RuleFor(x => x.Person).NotNull().WithMessage("Faltan los datos de la persona.")
            .SetValidator(new PersonInputValidator());
        RuleFor(x => x.Associate).NotNull().WithMessage("Faltan los datos de la afiliación.")
            .SetValidator(new AssociateInputValidator());
    }
}
