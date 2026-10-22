using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.AssociateGroups;

/// <summary>
/// Grupos del asociado (<c>COR_AssociateGroups</c>): la clasificación libre de la cooperativa —zona, empresa, gremio,
/// agencia…— que reemplaza al centro de costo en la ficha del asociado. No lleva datos contables ni de nómina.
/// </summary>
public sealed record AssociateGroupDto(Guid PublicId, string Code, string Name, string? Description, bool IsActive);

public static class AssociateGroupErrors
{
    public static Error NotFound() => new("AssociateGroup.NotFound", "El grupo del asociado no existe.");

    public static Error Inactive() => new("AssociateGroup.Inactive", "El grupo del asociado está inactivo: elija uno activo.");

    public static Error InUse(int asociados) => new ErrorConDatos("AssociateGroup.InUse",
        $"El grupo lo tienen {asociados} asociado(s): cámbielos de grupo antes de inactivarlo.", new { associates = asociados });
}

/// <summary>Alta de un grupo del asociado. El código es obligatorio, único entre vivos y no cambia después.</summary>
public sealed record CreateAssociateGroupCommand(string Code, string Name, string? Description) : IRequest<Result<AssociateGroupDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateAssociateGroupCommandValidator : AbstractValidator<CreateAssociateGroupCommand>
{
    public CreateAssociateGroupCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(200);
    }
}

public sealed class CreateAssociateGroupCommandHandler(IApplicationDbContext db, IDateTimeService dateTime, ICurrentUserService currentUser)
    : IRequestHandler<CreateAssociateGroupCommand, Result<AssociateGroupDto>>
{
    public async Task<Result<AssociateGroupDto>> Handle(CreateAssociateGroupCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.AssociateGroups.AsNoTracking().Where(g => g.Code == codigo)
            .Select(g => new { g.PublicId, g.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return Result.Failure<AssociateGroupDto>(CodigoDeCatalogo.Duplicado("un grupo del asociado", codigo, existente.Name, existente.PublicId));

        var grupo = new AssociateGroup
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        db.AssociateGroups.Add(grupo);
        await db.SaveChangesAsync(ct);
        return Result.Success(new AssociateGroupDto(grupo.PublicId, grupo.Code, grupo.Name, grupo.Description, grupo.IsActive));
    }
}

/// <summary>Cambia el nombre y la descripción; el código no cambia.</summary>
public sealed record UpdateAssociateGroupCommand(Guid AssociateGroupPublicId, string Name, string? Description) : IRequest<Result<AssociateGroupDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateAssociateGroupCommandValidator : AbstractValidator<UpdateAssociateGroupCommand>
{
    public UpdateAssociateGroupCommandValidator()
    {
        RuleFor(x => x.AssociateGroupPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(200);
    }
}

public sealed class UpdateAssociateGroupCommandHandler(IApplicationDbContext db, IDateTimeService dateTime, ICurrentUserService currentUser)
    : IRequestHandler<UpdateAssociateGroupCommand, Result<AssociateGroupDto>>
{
    public async Task<Result<AssociateGroupDto>> Handle(UpdateAssociateGroupCommand request, CancellationToken ct)
    {
        var grupo = await db.AssociateGroups.FirstOrDefaultAsync(g => g.PublicId == request.AssociateGroupPublicId, ct);
        if (grupo is null) return Result.Failure<AssociateGroupDto>(AssociateGroupErrors.NotFound());

        grupo.Name = request.Name.Trim();
        grupo.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        grupo.UpdatedAt = dateTime.UtcNow;
        grupo.UpdatedBy = currentUser.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success(new AssociateGroupDto(grupo.PublicId, grupo.Code, grupo.Name, grupo.Description, grupo.IsActive));
    }
}

/// <summary>Inactivar o reactivar con motivo. Un grupo que tienen asociados vivos no se inactiva (se cambian de grupo antes).</summary>
public sealed record SetAssociateGroupActiveCommand(Guid AssociateGroupPublicId, bool Active, string Reason)
    : IRequest<Result>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetAssociateGroupActiveCommandValidator : ValidadorConMotivo<SetAssociateGroupActiveCommand>
{
    public SetAssociateGroupActiveCommandValidator() => RuleFor(x => x.AssociateGroupPublicId).NotEmpty();
}

public sealed class SetAssociateGroupActiveCommandHandler(IApplicationDbContext db, IDateTimeService dateTime, ICurrentUserService currentUser)
    : IRequestHandler<SetAssociateGroupActiveCommand, Result>
{
    public async Task<Result> Handle(SetAssociateGroupActiveCommand request, CancellationToken ct)
    {
        var grupo = await db.AssociateGroups.FirstOrDefaultAsync(g => g.PublicId == request.AssociateGroupPublicId, ct);
        if (grupo is null) return Result.Failure(AssociateGroupErrors.NotFound());
        if (grupo.IsActive == request.Active) return Result.Success();

        if (!request.Active)
        {
            var asociados = await db.Associates.AsNoTracking().CountAsync(a => a.AssociateGroupId == grupo.Id && !a.IsDeleted, ct);
            if (asociados > 0) return Result.Failure(AssociateGroupErrors.InUse(asociados));
        }

        grupo.IsActive = request.Active;
        grupo.UpdatedAt = dateTime.UtcNow;
        grupo.UpdatedBy = currentUser.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Los grupos por código; <c>search</c> busca en código y nombre.</summary>
public sealed record ListAssociateGroupsQuery(bool IncludeInactive = false, string? Search = null) : IRequest<Result<IReadOnlyList<AssociateGroupDto>>>;

public sealed class ListAssociateGroupsQueryValidator : AbstractValidator<ListAssociateGroupsQuery>
{
    public ListAssociateGroupsQueryValidator() => RuleFor(x => x.Search).MaximumLength(80);
}

public sealed class ListAssociateGroupsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListAssociateGroupsQuery, Result<IReadOnlyList<AssociateGroupDto>>>
{
    public async Task<Result<IReadOnlyList<AssociateGroupDto>>> Handle(ListAssociateGroupsQuery request, CancellationToken ct)
    {
        var consulta = db.AssociateGroups.AsNoTracking().Where(g => request.IncludeInactive || g.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var termino = request.Search.Trim().ToLower();
            consulta = consulta.Where(g => g.Code.ToLower().Contains(termino) || g.Name.ToLower().Contains(termino));
        }
        return Result.Success<IReadOnlyList<AssociateGroupDto>>(await consulta.OrderBy(g => g.Code)
            .Select(g => new AssociateGroupDto(g.PublicId, g.Code, g.Name, g.Description, g.IsActive))
            .ToListAsync(ct));
    }
}

public sealed record GetAssociateGroupQuery(Guid AssociateGroupPublicId) : IRequest<Result<AssociateGroupDto>>;

public sealed class GetAssociateGroupQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAssociateGroupQuery, Result<AssociateGroupDto>>
{
    public async Task<Result<AssociateGroupDto>> Handle(GetAssociateGroupQuery request, CancellationToken ct)
    {
        var dto = await db.AssociateGroups.AsNoTracking().Where(g => g.PublicId == request.AssociateGroupPublicId)
            .Select(g => new AssociateGroupDto(g.PublicId, g.Code, g.Name, g.Description, g.IsActive))
            .FirstOrDefaultAsync(ct);
        return dto is null ? Result.Failure<AssociateGroupDto>(AssociateGroupErrors.NotFound()) : Result.Success(dto);
    }
}