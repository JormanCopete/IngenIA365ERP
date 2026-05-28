using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.CreateSalesperson;

/// <summary>
/// Asigna el rol Vendedor a una Person existente. Crea fila en INV_Salespeople
/// y marca Person.IsSalesperson = true. La persona debe estar registrada
/// previamente en /maestros/personas.
/// </summary>
public record CreateSalespersonCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
    public int? SalespersonType { get; init; }
    public bool AppliesCommission { get; init; }
}

public class CreateSalespersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateSalespersonCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateSalespersonCommand request, CancellationToken ct)
    {
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure<Guid>(new Error("Salesperson.PersonNotFound",
                "Persona no encontrada."));

        var existing = await context.Salespeople.AsNoTracking()
            .AnyAsync(s => s.PersonId == person.Id && !s.IsDeleted, ct);
        if (existing)
            return Result.Failure<Guid>(new Error("Salesperson.AlreadyExists",
                "Esta persona ya esta registrada como vendedor."));

        var entity = new Salesperson
        {
            PersonId = person.Id,
            SalespersonType = request.SalespersonType,
            AppliesCommission = request.AppliesCommission,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.Salespeople.Add(entity);

        person.IsSalesperson = true;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success(entity.PublicId);
    }
}

public class CreateSalespersonCommandValidator : AbstractValidator<CreateSalespersonCommand>
{
    public CreateSalespersonCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Persona requerida.");
    }
}
