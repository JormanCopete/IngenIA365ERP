using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.Common;
using IngenIA365ERP.Application.Security.Roles.CreateRole;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.Plantillas;

/// <summary>Un rol creado desde una plantilla de Cartera.</summary>
public sealed record RolSugeridoCreadoDto(string TemplateKey, Guid RolePublicId, string Code, IReadOnlyList<string> Omitted);

/// <summary>Lo que hizo la creación en bloque (api.md §19.4): los creados, las plantillas cuyo rol ya existía y las claves desconocidas.</summary>
public sealed record SuggestedLendingRolesDto(
    IReadOnlyList<RolSugeridoCreadoDto> Created,
    IReadOnlyList<string> AlreadyExisting,
    IReadOnlyList<string> Omitted);

/// <summary>
/// Crea en bloque los roles de Cartera desde sus plantillas (feature 013, T0300, T61; api.md §2.5, §19.4,
/// <c>POST /api/lending/activation/suggested-roles</c>, <c>Security.Roles.Create</c>, con <c>Idempotency-Key</c>). Sin
/// <see cref="TemplateKeys"/>, las trece <c>cartera.*</c> de <see cref="PerfilesSugeridos"/>. Cada rol es de la cooperativa, normal y
/// editable (<c>IsBuiltIn = false</c>), con la intersección entre la plantilla y el catálogo sembrado —<c>cartera.administrador</c> sin
/// <c>Lending.Decisions.*</c> ni <c>Lending.Sessions.Vote</c> (C86)—. Es idempotente: una plantilla cuyo rol ya existe (mismo código) va en
/// <c>alreadyExisting</c> y no se toca; una clave desconocida o que no es de Cartera va en <c>omitted</c>. La usan
/// <c>UsuariosDeCartera</c> de las e2e y el requisito <c>LendingRolesCreated</c> de la activación (US8). (nuevo)
/// </summary>
public sealed record CreateSuggestedLendingRolesCommand(IReadOnlyList<string>? TemplateKeys = null)
    : IRequest<Result<SuggestedLendingRolesDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    /// <summary>El código del rol de una plantilla: <c>cartera.comite-credito</c> → <c>CARTERA-COMITE-CREDITO</c>.</summary>
    public static string CodigoDeRol(string clave) => clave.Trim().Replace('.', '-').ToUpperInvariant();
}

public sealed class CreateSuggestedLendingRolesCommandValidator : AbstractValidator<CreateSuggestedLendingRolesCommand>
{
    public CreateSuggestedLendingRolesCommandValidator() =>
        RuleForEach(x => x.TemplateKeys).NotEmpty().MaximumLength(60).When(x => x.TemplateKeys is not null);
}

/// <summary>Delega cada alta en <see cref="CreateRoleCommandHandler"/>, que es quien sabe crear un rol; aquí no se duplica esa regla.</summary>
public sealed class CreateSuggestedLendingRolesCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    : IRequestHandler<CreateSuggestedLendingRolesCommand, Result<SuggestedLendingRolesDto>>
{
    public async Task<Result<SuggestedLendingRolesDto>> Handle(CreateSuggestedLendingRolesCommand request, CancellationToken ct)
    {
        var deCartera = PerfilesSugeridos.DelModulo(PerfilesSugeridos.ModuloCartera);
        var pedidas = request.TemplateKeys is { Count: > 0 } claves
            ? claves.Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : deCartera.Select(p => p.Key).ToList();

        var permisos = await db.Permissions.AsNoTracking().Where(p => !p.IsDeleted).Select(p => new { p.PublicId, p.Resource, p.Action }).ToListAsync(ct);
        var porCodigo = permisos
            .GroupBy(p => $"{p.Resource}.{p.Action}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().PublicId, StringComparer.OrdinalIgnoreCase);

        var creados = new List<RolSugeridoCreadoDto>();
        var existentes = new List<string>();
        var omitidas = new List<string>();
        foreach (var clave in pedidas)
        {
            var plantilla = deCartera.FirstOrDefault(p => string.Equals(p.Key, clave, StringComparison.OrdinalIgnoreCase));
            if (plantilla is null)
            {
                omitidas.Add(clave);
                continue;
            }

            var (codigos, omitidos) = PerfilesSugeridos.Expandir(plantilla, porCodigo.Keys);
            var codigo = CreateSuggestedLendingRolesCommand.CodigoDeRol(plantilla.Key);
            var alta = await new CreateRoleCommandHandler(db, currentUser).Handle(
                new CreateRoleCommand(codigo, plantilla.Actor, plantilla.Description, codigos.Select(c => porCodigo[c]).ToList()), ct);
            if (alta.IsFailure)
            {
                if (alta.Error.Code != RoleErrorCodes.CodeAlreadyExists) return Result.Failure<SuggestedLendingRolesDto>(alta.Error);
                existentes.Add(plantilla.Key);
                continue;
            }
            creados.Add(new RolSugeridoCreadoDto(plantilla.Key, alta.Value, codigo, omitidos));
        }

        return Result.Success(new SuggestedLendingRolesDto(creados, existentes, omitidas));
    }
}
