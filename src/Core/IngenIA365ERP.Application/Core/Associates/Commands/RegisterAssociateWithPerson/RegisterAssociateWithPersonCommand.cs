using FluentValidation;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Associates.Contracts;
using IngenIA365ERP.Application.Core.Associates.Services;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using MediatR;

namespace IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociateWithPerson;

/// <summary>
/// Persona nueva y asociado en un solo paso (feature 008, US2, FR-001). Un comando —un evento de
/// auditoría (FR-014)— y un <c>SaveChangesAsync</c>: persona, afiliación y bandera
/// <c>IsAssociate</c> quedan las tres o ninguna. Comparte reglas con <c>CreatePersonCommand</c> y
/// <c>RegisterAssociateCommand</c> a través de <see cref="PersonFactory"/> y
/// <see cref="AssociateRegistrar"/>.
/// </summary>
/// <remarks>Feature 012 (T46, T175): <paramref name="Authorization"/> es la autorización de datos del titular, opcional.</remarks>
public sealed record RegisterAssociateWithPersonCommand(PersonInput Person, AssociateInput Associate, AutorizacionAlCrear? Authorization = null)
    : IRequest<Result<RegisterAssociateWithPersonResult>>;

public sealed record RegisterAssociateWithPersonResult(Guid PersonPublicId, Guid AssociatePublicId);

public sealed class RegisterAssociateWithPersonCommandHandler(
    AltaConAutorizacion altas,
    AssociateRegistrar asociados)
    : IRequestHandler<RegisterAssociateWithPersonCommand, Result<RegisterAssociateWithPersonResult>>
{
    public async Task<Result<RegisterAssociateWithPersonResult>> Handle(
        RegisterAssociateWithPersonCommand request, CancellationToken ct)
    {
        var alta = await altas.GuardarAsync(request.Person, request.Authorization,
            persona => asociados.PrepareAsync(persona, request.Associate, ct), ct);
        return alta.IsSuccess
            ? Result.Success(new RegisterAssociateWithPersonResult(alta.Value.Persona.PublicId, alta.Value.Rol.PublicId))
            : Result.Failure<RegisterAssociateWithPersonResult>(alta.Error);
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
        RuleFor(x => x.Authorization!).SetValidator(new AutorizacionAlCrearValidator()).When(x => x.Authorization is not null);
    }
}
