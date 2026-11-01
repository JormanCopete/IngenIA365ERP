using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>
/// El alcance de sucursal de quien consulta, dicho a la persona (feature 009, FR-035). Las consultas contables filtran en silencio
/// por <see cref="IUserBranchScope"/>; con oficinas asignadas que ninguna agencia tiene vinculadas el alcance queda vacío a propósito
/// y los informes salían vacíos sin explicación. <see cref="SinSucursales"/> es ese caso. (nuevo)
/// </summary>
public sealed record MiAlcanceDeSucursalDto(bool Restringido, IReadOnlyList<string> Sucursales)
{
    /// <summary>Tiene oficinas asignadas y ninguna corresponde a una agencia: no ve ningún movimiento.</summary>
    public bool SinSucursales => Restringido && Sucursales.Count == 0;
}

public sealed record MiAlcanceDeSucursalQuery : IRequest<MiAlcanceDeSucursalDto>;

public sealed class MiAlcanceDeSucursalQueryValidator : AbstractValidator<MiAlcanceDeSucursalQuery>;

public sealed class MiAlcanceDeSucursalQueryHandler(IApplicationDbContext db, IUserBranchScope alcance)
    : IRequestHandler<MiAlcanceDeSucursalQuery, MiAlcanceDeSucursalDto>
{
    public async Task<MiAlcanceDeSucursalDto> Handle(MiAlcanceDeSucursalQuery request, CancellationToken ct)
    {
        var a = await alcance.ObtenerAsync(ct);
        if (!a.Restringido) return new MiAlcanceDeSucursalDto(false, []);

        var ids = a.Sucursales.ToList();
        var nombres = await db.Branches.AsNoTracking()
            .Where(b => ids.Contains(b.Id) && !b.IsDeleted)
            .OrderBy(b => b.Name)
            .Select(b => b.Name)
            .ToListAsync(ct);
        return new MiAlcanceDeSucursalDto(true, nombres);
    }
}
