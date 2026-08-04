using IngenIA365ERP.Application.Common.Interfaces.Database;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Saas.DatabaseAdmin;

/// <summary>
/// Feature 004 (T042) — estado del motor activo y migraciones pendientes por
/// alcance, para la consola master (soporta verificar FR-011 sin acceso al
/// servidor). Nunca expone cadenas de conexion.
/// </summary>
public sealed record GetDatabaseStatusQuery : IRequest<Result<DatabaseStatusDto>>;

public sealed class GetDatabaseStatusQueryHandler(IDatabaseStatusReader reader)
    : IRequestHandler<GetDatabaseStatusQuery, Result<DatabaseStatusDto>>
{
    public async Task<Result<DatabaseStatusDto>> Handle(GetDatabaseStatusQuery request, CancellationToken ct)
        => Result.Success(await reader.GetStatusAsync(ct));
}
