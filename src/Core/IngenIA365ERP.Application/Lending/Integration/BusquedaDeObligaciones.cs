using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La implementación de <see cref="IBusquedaDeObligaciones"/> en Cartera y Captaciones (feature 015, decisión del dueño del 2026-10-24 sobre T0113): busca el número
/// exacto (sin espacios, sin distinguir mayúsculas) entre los créditos (<c>LND_Loans.Number</c>, deudor) y las cuentas de captación y aportes
/// (<c>LND_MemberAccounts.Number</c>, titular), vivos, y devuelve la persona dueña. Sólo lee. (nuevo)
/// </summary>
public sealed class BusquedaDeObligaciones(IApplicationDbContext db) : IBusquedaDeObligaciones
{
    public const int LargoMaximo = 40;

    public async Task<IReadOnlyList<ObligacionEncontradaDto>> PorNumeroAsync(BusquedaPorNumero busqueda, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(busqueda);
        var numero = (busqueda.Number ?? string.Empty).Trim().ToUpperInvariant();
        if (numero.Length == 0 || numero.Length > LargoMaximo) return [];

        var creditos = await (from l in db.Loans.AsNoTracking()
                              join p in db.People.AsNoTracking().IgnoreQueryFilters() on l.DebtorPersonId equals p.Id
                              where !l.IsDeleted && l.Number.ToUpper() == numero
                              select new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Loan, l.PublicId, l.Number, p.PublicId,
                                  (p.FirstName + " " + p.LastName).Trim(), p.TaxId, l.Status.ToString())).ToListAsync(ct);
        var cuentas = await (from a in db.MemberAccounts.AsNoTracking()
                             join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.HolderPersonId equals p.Id
                             where !a.IsDeleted && a.Number.ToUpper() == numero
                             select new ObligacionEncontradaDto(ClaseDeObligacionEncontrada.Account, a.PublicId, a.Number, p.PublicId,
                                 (p.FirstName + " " + p.LastName).Trim(), p.TaxId, a.Status.ToString())).ToListAsync(ct);
        return [.. creditos, .. cuentas];
    }
}
