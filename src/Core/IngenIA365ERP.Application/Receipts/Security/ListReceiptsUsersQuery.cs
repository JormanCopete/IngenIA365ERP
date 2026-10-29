using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Security;

/// <summary>Un usuario de la cooperativa como lo eligen las pantallas de Recaudo: id público y nombre. (nuevo)</summary>
public sealed record UsuarioDeRecaudoDto(Guid PublicId, string Username, bool IsActive);

/// <summary>La lista con la forma de página que usan las pantallas. (nuevo)</summary>
public sealed record UsuariosDeRecaudoDto(IReadOnlyList<UsuarioDeRecaudoDto> Items, int Page, int PageSize, long TotalCount);

/// <summary>
/// <c>GET /api/receipts/users</c> (<c>Receipts.Setup.View</c>; decisión del dueño del 2026-10-23, DT §2.7): los usuarios activos de la cooperativa, sólo id
/// público y nombre, para elegir cajeros, custodios, suplentes y el usuario del alcance sin pedir <c>Security.Users.View</c>. (nuevo)
/// </summary>
public sealed record ListReceiptsUsersQuery(string? Search = null) : IRequest<Result<UsuariosDeRecaudoDto>>;

public sealed class ListReceiptsUsersQueryHandler(IApplicationDbContext db) : IRequestHandler<ListReceiptsUsersQuery, Result<UsuariosDeRecaudoDto>>
{
    public const int Maximo = 500;

    public async Task<Result<UsuariosDeRecaudoDto>> Handle(ListReceiptsUsersQuery request, CancellationToken ct)
    {
        var texto = request.Search?.Trim();
        var usuarios = await db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Where(u => string.IsNullOrEmpty(texto) || u.Username.Contains(texto))
            .OrderBy(u => u.Username)
            .Take(Maximo)
            .Select(u => new UsuarioDeRecaudoDto(u.PublicId, u.Username, u.IsActive))
            .ToListAsync(ct);
        return Result.Success(new UsuariosDeRecaudoDto(usuarios, 1, Maximo, usuarios.Count));
    }
}
