using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Permissions;

/// <summary>
/// Los permisos efectivos del usuario en la cooperativa activa, para que la interfaz oculte lo
/// que la API negaría (feature 008, FR-011). Se sirve en <c>GET /api/admin/permissions/mine</c>
/// y no en <c>/api/auth/me</c>: esa ruta está exenta de cooperativa y los permisos son por
/// cooperativa. El cliente lo pide una vez por cooperativa y lo vuelve a pedir al cambiar.
/// </summary>
public sealed record GetMyPermissionsQuery : IRequest<Result<MyPermissionsDto>>;

/// <param name="IsGlobalMasterAdmin">El maestro de la plataforma: sin permisos por cooperativa, el cliente lo trata como «todo».</param>
/// <param name="Permissions">Códigos <c>Recurso.Acción</c>, ordenados, sin repetidos.</param>
public sealed record MyPermissionsDto(bool IsGlobalMasterAdmin, IReadOnlyList<string> Permissions);

public sealed class GetMyPermissionsQueryHandler(ICurrentUserPermissions permisos)
    : IRequestHandler<GetMyPermissionsQuery, Result<MyPermissionsDto>>
{
    public async Task<Result<MyPermissionsDto>> Handle(GetMyPermissionsQuery request, CancellationToken ct)
    {
        if (permisos.EsMaestroGlobal)
            return Result.Success(new MyPermissionsDto(true, []));

        var codigos = await permisos.ListAsync(ct);
        var ordenados = codigos
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Result.Success(new MyPermissionsDto(false, ordenados));
    }
}
