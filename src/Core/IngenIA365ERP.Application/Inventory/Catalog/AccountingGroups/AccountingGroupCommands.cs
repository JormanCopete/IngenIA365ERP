using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.AccountingGroups;

/// <summary>
/// Alta de un grupo contable (feature 012, T216; contracts/api.md §3.4; FR-027, FR-073). El código es inmutable: la matriz
/// de Contabilidad lo usa como <c>AccountingGroupCode</c> (T27); qué cuentas le tocan lo dice la matriz, nunca este módulo.
/// (nuevo)
/// </summary>
public sealed record CreateAccountingGroupCommand(string Code, string Name, string? Description)
    : IRequest<Result<AccountingGroupDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateAccountingGroupCommandValidator : AbstractValidator<CreateAccountingGroupCommand>
{
    public CreateAccountingGroupCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(300);
    }
}

public sealed class CreateAccountingGroupCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateAccountingGroupCommand, Result<AccountingGroupDto>>
{
    public async Task<Result<AccountingGroupDto>> Handle(CreateAccountingGroupCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.AccountingGroups.Where(g => g.Code == codigo).Select(g => new { g.PublicId, g.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return Result.Failure<AccountingGroupDto>(CodigoDeCatalogo.Duplicado("un grupo contable", codigo, existente.Name, existente.PublicId));

        var grupo = new AccountingGroup
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
        };
        db.AccountingGroups.Add(grupo);
        await db.SaveChangesAsync(ct);
        return Result.Success(new AccountingGroupDto(grupo.PublicId, grupo.Code, grupo.Name, grupo.Description, grupo.IsActive, 0));
    }
}

/// <summary>Edición de un grupo contable (T216; §3.4, <c>PUT /{id}</c>): nombre y descripción; el código no cambia. (nuevo)</summary>
public sealed record UpdateAccountingGroupCommand(Guid AccountingGroupPublicId, string Name, string? Description)
    : IRequest<Result<AccountingGroupDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateAccountingGroupCommandValidator : AbstractValidator<UpdateAccountingGroupCommand>
{
    public UpdateAccountingGroupCommandValidator()
    {
        RuleFor(x => x.AccountingGroupPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(300);
    }
}

public sealed class UpdateAccountingGroupCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateAccountingGroupCommand, Result<AccountingGroupDto>>
{
    public async Task<Result<AccountingGroupDto>> Handle(UpdateAccountingGroupCommand request, CancellationToken ct)
    {
        var grupo = await db.AccountingGroups.FirstOrDefaultAsync(g => g.PublicId == request.AccountingGroupPublicId, ct);
        if (grupo is null) return Result.Failure<AccountingGroupDto>(CatalogErrors.AccountingGroupNotFound());

        grupo.Name = request.Name.Trim();
        grupo.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await db.SaveChangesAsync(ct);
        var productos = await db.Products.CountAsync(p => p.AccountingGroupId == grupo.Id, ct);
        return Result.Success(new AccountingGroupDto(grupo.PublicId, grupo.Code, grupo.Name, grupo.Description, grupo.IsActive, productos));
    }
}

/// <summary>
/// Inactivar o reactivar un grupo contable con motivo (T216; §3.4): no se inactiva mientras lo usen productos
/// inventariables que no estén inactivos (<c>Inventory.AccountingGroup.InUse</c>). (nuevo)
/// </summary>
public sealed record SetAccountingGroupActiveCommand(Guid AccountingGroupPublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetAccountingGroupActiveCommandValidator : ValidadorConMotivo<SetAccountingGroupActiveCommand>
{
    public SetAccountingGroupActiveCommandValidator() => RuleFor(x => x.AccountingGroupPublicId).NotEmpty();
}

public sealed class SetAccountingGroupActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetAccountingGroupActiveCommand, Result>
{
    public async Task<Result> Handle(SetAccountingGroupActiveCommand request, CancellationToken ct) =>
        await ActivacionDeCatalogo.CambiarAsync(
            await db.AccountingGroups.FirstOrDefaultAsync(g => g.PublicId == request.AccountingGroupPublicId, ct),
            request.Active, g => g.IsActive, (g, a) => g.IsActive = a, CatalogErrors.AccountingGroupNotFound(),
            async g =>
            {
                var productos = await db.Products.CountAsync(p => p.AccountingGroupId == g.Id && p.Status != ProductStatus.Inactive
                    && (p.Kind == ProductKind.Inventoriable || p.Kind == ProductKind.Variant), ct);
                return productos == 0 ? null : CatalogErrors.AccountingGroupInUse(productos);
            },
            db.SaveChangesAsync, ct);
}

/// <summary>Los grupos contables (T216; §3.4, <c>GET /?includeInactive=</c>), por código. (nuevo)</summary>
public sealed record ListAccountingGroupsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<AccountingGroupDto>>>;

public sealed class ListAccountingGroupsQueryValidator : AbstractValidator<ListAccountingGroupsQuery>;

public sealed class ListAccountingGroupsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListAccountingGroupsQuery, Result<IReadOnlyList<AccountingGroupDto>>>
{
    public async Task<Result<IReadOnlyList<AccountingGroupDto>>> Handle(ListAccountingGroupsQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<AccountingGroupDto>>(await db.AccountingGroups.AsNoTracking()
            .Where(g => request.IncludeInactive || g.IsActive)
            .OrderBy(g => g.Code)
            .Select(g => new AccountingGroupDto(g.PublicId, g.Code, g.Name, g.Description, g.IsActive, db.Products.Count(p => p.AccountingGroupId == g.Id)))
            .ToListAsync(ct));
}
