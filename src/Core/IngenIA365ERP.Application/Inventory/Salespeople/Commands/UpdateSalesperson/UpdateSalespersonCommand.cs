using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.UpdateSalesperson;

/// <summary>
/// Cambia los datos del rol (feature 012, T425; contracts/api.md §31, <c>PUT /api/inventory/salespeople/{id}</c>,
/// <c>Inventory.Salespeople.Manage</c>, con <c>Idempotency-Key</c>): sólo <c>salespersonType</c> y
/// <c>appliesCommission</c>. Los datos personales se editan en Personas. Un vendedor inexistente o retirado es 404.
/// </summary>
public sealed record UpdateSalespersonCommand : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid PublicId { get; init; }

    public int? SalespersonType { get; init; }

    public bool AppliesCommission { get; init; }

    public string Reason { get; init; } = string.Empty;

    public Guid OperationKey { get; init; }
}

public sealed class UpdateSalespersonCommandValidator : AbstractValidator<UpdateSalespersonCommand>
{
    public UpdateSalespersonCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.SalespersonType).GreaterThanOrEqualTo(0).When(x => x.SalespersonType is not null);
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<UpdateSalespersonCommand>.LargoMaximo);
    }
}

public sealed class UpdateSalespersonCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSalespersonCommand, Result>
{
    public async Task<Result> Handle(UpdateSalespersonCommand request, CancellationToken ct)
    {
        var fila = await db.Salespeople.FirstOrDefaultAsync(s => s.PublicId == request.PublicId && !s.IsDeleted, ct);
        if (fila is null) return Result.Failure(Error.NotFound);

        fila.SalespersonType = request.SalespersonType;
        fila.AppliesCommission = request.AppliesCommission;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
