using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.CreateRole;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.Plantillas;

/// <summary>Resultado de crear un rol desde una plantilla (contracts/api.md §1.5).</summary>
/// <param name="Omitted">Códigos de la plantilla que el catálogo de la cooperativa no tiene.</param>
public sealed record RoleFromTemplateDto(
    Guid RolePublicId,
    string Code,
    string Name,
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<string> Omitted);

/// <summary>
/// <c>POST /api/admin/roles/from-template</c> (feature 012, T128; FR-093, T48; <c>Security.Roles.Create</c>, con
/// <c>Idempotency-Key</c>): crea un rol normal y editable (<c>IsBuiltIn = false</c>) con la intersección entre la
/// plantilla y el catálogo sembrado. Mismas reglas que <see cref="CreateRoleCommand"/>; código repetido →
/// <c>Security.Roles.CodeAlreadyExists</c> (422); plantilla inexistente → <c>Generic.NotFound</c> (404).
/// </summary>
public sealed record CreateRoleFromTemplateCommand(
    string TemplateKey,
    string Code,
    string Name,
    string? Description)
    : IRequest<Result<RoleFromTemplateDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateRoleFromTemplateCommandValidator : AbstractValidator<CreateRoleFromTemplateCommand>
{
    public CreateRoleFromTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateKey).NotEmpty().WithMessage("Elegí un perfil sugerido.").MaximumLength(60);
        RuleFor(x => x.Code).CodigoDeRol();
        RuleFor(x => x.Name).NombreDeRol();
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

/// <summary>
/// Expande la plantilla y delega el alta en <see cref="CreateRoleCommandHandler"/>, que es quien sabe crear un rol
/// (código único por cooperativa, vínculos, auditoría de campos): aquí no se duplica esa regla.
/// </summary>
public sealed class CreateRoleFromTemplateCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateRoleFromTemplateCommand, Result<RoleFromTemplateDto>>
{
    public async Task<Result<RoleFromTemplateDto>> Handle(CreateRoleFromTemplateCommand request, CancellationToken ct)
    {
        var plantilla = PerfilesSugeridos.Buscar(request.TemplateKey);
        if (plantilla is null)
            return Result.Failure<RoleFromTemplateDto>("Generic.NotFound", "No existe ese perfil sugerido.");

        var permisos = await db.Permissions
            .Select(p => new { p.PublicId, p.Resource, p.Action })
            .ToListAsync(ct);
        var porCodigo = permisos
            .GroupBy(p => $"{p.Resource}.{p.Action}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().PublicId, StringComparer.OrdinalIgnoreCase);

        var (codigos, omitidos) = PerfilesSugeridos.Expandir(plantilla, porCodigo.Keys);

        var alta = await new CreateRoleCommandHandler(db, currentUser).Handle(
            new CreateRoleCommand(request.Code, request.Name, request.Description,
                codigos.Select(c => porCodigo[c]).ToList()),
            ct);
        if (alta.IsFailure)
            return Result.Failure<RoleFromTemplateDto>(alta.Error.Code, alta.Error.Message);

        return Result.Success(new RoleFromTemplateDto(alta.Value, request.Code, request.Name, codigos, omitidos));
    }
}
