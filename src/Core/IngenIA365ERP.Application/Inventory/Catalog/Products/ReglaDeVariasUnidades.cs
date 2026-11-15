using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// Dejar de permitir varias unidades (<c>Productos.VariasUnidades = NoPermitir</c>) con productos que todavía tienen alternas se
/// rechaza con la lista de esos productos (decisión del dueño del 2026-11-15: no se borran en masa; se retiran primero). Corre dentro
/// de <c>AddParameterVersionCommand</c>, antes del <c>SaveChanges</c>: un rechazo no deja la vigencia. (nuevo)
/// </summary>
public sealed class ReglaDeVariasUnidades(IApplicationDbContext db) : IEfectoDeAltaDeParametro
{
    private const int MaximoEnElMensaje = 10;

    public async Task<Result> AplicarAsync(AltaDeParametro alta, string motivo, string? fuenteLegal, CancellationToken ct)
    {
        if (alta.Definicion.Modulo != ParametrosDeInventario.Modulo
            || alta.Definicion.Clave != ParametrosDeInventario.ProductosVariasUnidades
            || alta.Valor != ParametrosDeInventario.VariasUnidadesNoPermitir)
            return Result.Success();

        var conAlternas = db.Products.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Units.Any(u => !u.IsDeleted))
            .OrderBy(p => p.Code)
            .Select(p => p.Code);
        var total = await conAlternas.CountAsync(ct);
        if (total == 0) return Result.Success();
        return Result.Failure(CatalogErrors.ProductsStillHaveUnits(await conAlternas.Take(MaximoEnElMensaje).ToListAsync(ct), total));
    }
}
