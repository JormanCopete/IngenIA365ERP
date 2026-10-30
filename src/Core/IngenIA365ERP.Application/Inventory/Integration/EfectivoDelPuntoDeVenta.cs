using IngenIA365ERP.Application.Common.Integration.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// La implementación de Comercial de <see cref="IEfectivoDelPuntoDeVenta"/> (feature 015, R1, T0388; E-03, enmienda de la 012): los pagos en efectivo de los documentos
/// confirmados del punto de venta (<c>INV_DocumentPayments</c> de clase efectivo), recibidos y devueltos, de la contraparte del documento. Sólo lee tablas de
/// Inventario y la persona. (nuevo)
/// </summary>
public sealed class EfectivoDelPuntoDeVenta(IApplicationDbContext db) : IEfectivoDelPuntoDeVenta
{
    public async Task<IReadOnlyList<EfectivoDelPuntoDeVentaDto>> PorPersonaAsync(Guid personPublicId, int anio, int mes, CancellationToken ct)
    {
        var desde = new DateOnly(anio, mes, 1);
        var todos = await PorPeriodoAsync(desde, desde.AddMonths(1).AddDays(-1), ct);
        return todos.Where(t => t.PersonPublicId == personPublicId).ToList();
    }

    public async Task<IReadOnlyList<EfectivoDelPuntoDeVentaDto>> PorPeriodoAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var filas = await (from p in db.DocumentPayments.AsNoTracking()
                           join d in db.InventoryDocuments.AsNoTracking() on p.DocumentId equals d.Id
                           join persona in db.People.AsNoTracking().IgnoreQueryFilters() on d.CounterpartyPersonId equals persona.Id
                           join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on d.BranchId equals b.Id
                           where p.MeansClass == PaymentMeansClass.Cash && d.Status == DocumentStatus.Confirmed
                                 && d.OperationDate >= desde && d.OperationDate <= hasta
                           select new { d.PublicId, d.Prefix, d.Number, d.OperationDate, Persona = persona.PublicId, p.Direction, p.Amount, Oficina = b.PublicId })
            .ToListAsync(ct);
        return filas.GroupBy(f => (f.PublicId, Numero: f.Prefix + (f.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty), f.OperationDate, f.Persona, f.Oficina))
            .Select(g => new EfectivoDelPuntoDeVentaDto(g.Key.PublicId, g.Key.Numero, g.Key.OperationDate, g.Key.Persona,
                g.Where(x => x.Direction == PaymentDirection.Received).Sum(x => x.Amount), g.Where(x => x.Direction == PaymentDirection.Refunded).Sum(x => x.Amount),
                g.Key.Oficina))
            .ToList();
    }
}
