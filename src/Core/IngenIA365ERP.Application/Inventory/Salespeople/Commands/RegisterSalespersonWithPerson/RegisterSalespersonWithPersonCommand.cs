using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.RegisterSalespersonWithPerson;

/// <summary>
/// Persona nueva y vendedor en un solo paso (feature 008, fase 2; <c>POST /api/inventory/salespeople/with-person</c>,
/// <c>Inventory.Salespeople.Manage</c> y <c>Core.People.Create</c>, con <c>Idempotency-Key</c>). Un comando —un evento de
/// auditoría— y un <c>SaveChangesAsync</c>: persona, fila de <c>INV_Salespeople</c> y <c>IsSalesperson</c> quedan las tres o
/// ninguna. Comparte reglas con <c>CreatePersonCommand</c> por <see cref="AltaConAutorizacion"/> y con
/// <c>CreateSalespersonCommand</c> por <see cref="RolDeVendedor"/>. Un documento ya registrado responde
/// <c>Person.TaxIdDuplicate</c> o <c>Person.TaxIdDeleted</c>, como en Empleados y Asociados. (nuevo)
/// </summary>
public sealed record RegisterSalespersonWithPersonCommand(
    PersonInput Person,
    SalespersonInput Salesperson,
    AutorizacionAlCrear? Authorization = null)
    : IRequest<Result<RegisterSalespersonWithPersonResult>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>Los datos del rol: lo de <c>CreateSalespersonCommand</c> que no es la persona. (nuevo)</summary>
public sealed record SalespersonInput(int? SalespersonType, bool? AppliesCommission);

public sealed record RegisterSalespersonWithPersonResult(Guid PersonPublicId, Guid SalespersonPublicId);

public sealed class RegisterSalespersonWithPersonCommandHandler(AltaConAutorizacion altas, RolDeVendedor rol)
    : IRequestHandler<RegisterSalespersonWithPersonCommand, Result<RegisterSalespersonWithPersonResult>>
{
    public async Task<Result<RegisterSalespersonWithPersonResult>> Handle(RegisterSalespersonWithPersonCommand request, CancellationToken ct)
    {
        // Persona nueva: no tiene fila de vendedor que restaurar.
        var alta = await altas.GuardarAsync(request.Person, request.Authorization,
            persona => Task.FromResult(rol.Asignar(persona, null, request.Salesperson.SalespersonType, request.Salesperson.AppliesCommission)), ct);
        return alta.IsSuccess
            ? Result.Success(new RegisterSalespersonWithPersonResult(alta.Value.Persona.PublicId, alta.Value.Rol.Fila.PublicId))
            : Result.Failure<RegisterSalespersonWithPersonResult>(alta.Error);
    }
}

public sealed class RegisterSalespersonWithPersonCommandValidator : AbstractValidator<RegisterSalespersonWithPersonCommand>
{
    public RegisterSalespersonWithPersonCommandValidator()
    {
        RuleFor(x => x.Person).NotNull().WithMessage("Faltan los datos de la persona.")
            .SetValidator(new PersonInputValidator());
        RuleFor(x => x.Salesperson).NotNull().WithMessage("Faltan los datos del vendedor.");
        RuleFor(x => x.Salesperson.SalespersonType).GreaterThanOrEqualTo(0)
            .When(x => x.Salesperson?.SalespersonType is not null);
        RuleFor(x => x.Authorization!).SetValidator(new AutorizacionAlCrearValidator()).When(x => x.Authorization is not null);
    }
}
