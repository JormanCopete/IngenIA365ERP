using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Cash;

// 2026-11-25 (pedido del dueño): que nadie abra por error la caja del punto de venta que usa otra persona. Con Caja.SoloCajerosAsignados encendido (el
// defecto), una caja con cajeros vigentes sólo la abren ellos. Sus cajeros son los propios (INV_CashRegisterCashiers) y, si la caja está unida a una caja
// de Recaudo (caja única, RCD_CashBoxes.PosCashRegisterPublicId), también los de esa caja: así la caja única se configura en un solo sitio. Una caja sin
// ninguno la sigue abriendo cualquiera con acceso al punto, para que encender el parámetro no deje sin caja a quien todavía no la configuró. (nuevo)

/// <summary>Un cajero vigente de una caja del punto de venta: propio, o de la caja de Recaudo unida (<see cref="CajaDeRecaudo"/> dice cuál). (nuevo)</summary>
public sealed record CajeroDeLaCaja(int UserId, Guid UserPublicId, string Nombre, string? CajaDeRecaudo);

/// <summary>Quién abre una caja del punto de venta: el único sitio que lo decide. (nuevo)</summary>
public static class CajerosDeLaCaja
{
    /// <summary>Los cajeros vigentes de cada caja, por su <c>Id</c>: los propios primero y luego los de la caja de Recaudo unida, sin repetir usuario.</summary>
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<CajeroDeLaCaja>>> VigentesAsync(IApplicationDbContext db,
        IReadOnlyCollection<CashRegister> cajas, DateOnly hoy, CancellationToken ct)
    {
        if (cajas.Count == 0) return new Dictionary<int, IReadOnlyList<CajeroDeLaCaja>>();
        var ids = cajas.Select(c => c.Id).ToList();
        var publicos = cajas.Select(c => (Guid?)c.PublicId).ToList();
        var propios = (await db.CashRegisterCashiers.AsNoTracking().Where(c => ids.Contains(c.CashRegisterId)).ToListAsync(ct))
            .Where(c => c.VigenteEn(hoy)).Select(c => (Caja: c.CashRegisterId, c.UserId, Recaudo: (string?)null)).ToList();
        var unidas = await db.CashBoxes.AsNoTracking()
            .Where(b => publicos.Contains(b.PosCashRegisterPublicId) && b.Status == ReceiptsCatalogStatus.Active)
            .Select(b => new { b.Id, b.Code, Pos = b.PosCashRegisterPublicId!.Value }).ToListAsync(ct);
        var idsDeRecaudo = unidas.Select(b => b.Id).ToList();
        var deRecaudo = idsDeRecaudo.Count == 0
            ? []
            : (await db.CashBoxCashiers.AsNoTracking().Where(c => idsDeRecaudo.Contains(c.CashBoxId)).ToListAsync(ct)).Where(c => c.VigenteEn(hoy)).ToList();
        var porPublico = cajas.ToDictionary(c => c.PublicId, c => c.Id);
        var heredados = (from c in deRecaudo
                         join b in unidas on c.CashBoxId equals b.Id
                         select (Caja: porPublico[b.Pos], c.UserId, Recaudo: (string?)b.Code)).ToList();
        var todos = propios.Concat(heredados).ToList();

        var usuarios = todos.Select(t => t.UserId).Distinct().ToList();
        var datos = await (from u in db.Users.AsNoTracking().IgnoreQueryFilters()
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on u.PersonId equals p.Id into pp
                           from p in pp.DefaultIfEmpty()
                           where usuarios.Contains(u.Id)
                           select new
                           {
                               u.Id, u.PublicId, u.Username,
                               FirstName = p == null ? null : p.FirstName, OtherNames = p == null ? null : p.OtherNames,
                               LastName = p == null ? null : p.LastName, SecondLastName = p == null ? null : p.SecondLastName,
                               BusinessName = p == null ? null : p.BusinessName,
                           }).ToListAsync(ct);
        var nombres = datos.ToDictionary(d => d.Id, d =>
        {
            var nombre = d.FirstName is null && d.BusinessName is null
                ? string.Empty
                : PersonFactory.NombreVisible(d.FirstName, d.OtherNames, d.LastName, d.SecondLastName, d.BusinessName);
            return (d.PublicId, Nombre: string.IsNullOrWhiteSpace(nombre) ? d.Username : nombre);
        });
        // Un cajero cuyo usuario no se encuentra sigue contando (la restricción no se pierde); sólo se muestra sin nombre.
        return todos.GroupBy(t => t.Caja).ToDictionary(g => g.Key, g => (IReadOnlyList<CajeroDeLaCaja>)g
            .DistinctBy(t => t.UserId).Select(t => nombres.TryGetValue(t.UserId, out var n)
                ? new CajeroDeLaCaja(t.UserId, n.PublicId, n.Nombre, t.Recaudo)
                : new CajeroDeLaCaja(t.UserId, Guid.Empty, $"usuario {t.UserId}", t.Recaudo)).ToList());
    }

    /// <summary>Los cajeros vigentes de una caja.</summary>
    public static async Task<IReadOnlyList<CajeroDeLaCaja>> DeAsync(IApplicationDbContext db, CashRegister caja, DateOnly hoy, CancellationToken ct) =>
        (await VigentesAsync(db, [caja], hoy, ct)).GetValueOrDefault(caja.Id) ?? [];

    /// <summary>Si el usuario puede abrir la caja: sin la regla, o sin cajeros asignados, cualquiera; si no, sólo uno de ellos.</summary>
    public static bool PuedeAbrir(bool soloAsignados, IReadOnlyList<CajeroDeLaCaja> cajeros, int usuario) =>
        !soloAsignados || cajeros.Count == 0 || cajeros.Any(c => c.UserId == usuario);

    /// <summary>El rechazo al abrir, o nulo si puede.</summary>
    public static Error? Rechazo(bool soloAsignados, string codigoDeCaja, IReadOnlyList<CajeroDeLaCaja> cajeros, int usuario) =>
        PuedeAbrir(soloAsignados, cajeros, usuario) ? null : ErroresDeCaja.CashierNotAssigned(codigoDeCaja, cajeros.Select(c => c.Nombre).ToList());
}
