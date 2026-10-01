using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.CreateSalesperson;

/// <summary>
/// Da el rol vendedor a una persona del maestro (feature 012, T424; FR-031, FR-092; data-model §11; contracts/api.md §31,
/// <c>POST /api/inventory/salespeople</c>, <c>Inventory.Salespeople.Manage</c>, con <c>Idempotency-Key</c>). Si la persona
/// tiene una fila retirada, la <b>restaura</b> con el mismo <c>salespersonPublicId</c>; si no, crea una. Pone
/// <c>Person.IsSalesperson = true</c> en el mismo <c>SaveChangesAsync</c>, por <see cref="RolDeVendedor"/>. La persona no se
/// crea aquí: se da de alta en <c>PersonaDialog</c>. Errores: <c>Inventory.Salesperson.AlreadyActive</c> (rol vivo) y
/// <c>Core.Person.NotFound</c> (inexistente o eliminada).
/// </summary>
public sealed record CreateSalespersonCommand : IRequest<Result<CreateSalespersonResultDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid PersonPublicId { get; init; }

    public int? SalespersonType { get; init; }

    /// <summary>Nulo: no en una nueva, lo que tenía en una restaurada.</summary>
    public bool? AppliesCommission { get; init; }

    /// <summary>Opcional; si viene, la auditoría lo copia al evento.</summary>
    public string Reason { get; init; } = string.Empty;

    public Guid OperationKey { get; init; }
}

/// <summary>Lo que responde el alta (§31): el vendedor y si se restauró uno retirado. (nuevo)</summary>
public sealed record CreateSalespersonResultDto(Guid SalespersonPublicId, bool Restored);

public sealed class CreateSalespersonCommandValidator : AbstractValidator<CreateSalespersonCommand>
{
    public CreateSalespersonCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elegí la persona.");
        RuleFor(x => x.SalespersonType).GreaterThanOrEqualTo(0).When(x => x.SalespersonType is not null);
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<CreateSalespersonCommand>.LargoMaximo);
    }
}

public sealed class CreateSalespersonCommandHandler(IApplicationDbContext db, RolDeVendedor rol)
    : IRequestHandler<CreateSalespersonCommand, Result<CreateSalespersonResultDto>>
{
    public async Task<Result<CreateSalespersonResultDto>> Handle(CreateSalespersonCommand request, CancellationToken ct)
    {
        var persona = await rol.PersonaVivaAsync(request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<CreateSalespersonResultDto>(ErroresDeVendedores.PersonaInexistente());

        var asignacion = rol.Asignar(persona, await rol.FilaDeAsync(persona.Id, ct), request.SalespersonType, request.AppliesCommission);
        if (asignacion.IsFailure) return Result.Failure<CreateSalespersonResultDto>(asignacion.Error);

        await db.SaveChangesAsync(ct);
        return Result.Success(new CreateSalespersonResultDto(asignacion.Value.Fila.PublicId, asignacion.Value.Restaurada));
    }
}
