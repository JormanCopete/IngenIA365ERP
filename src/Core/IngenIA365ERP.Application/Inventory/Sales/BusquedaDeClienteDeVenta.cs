using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common.Text;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// 2026-11-24 (pedido del dueño, filtros de Ventas): el cliente de un documento de venta por documento o nombre, una sola búsqueda para la
/// lista de documentos de venta (y las cotizaciones) y el detalle de ventas por medio de pago. Busca en la persona —todos los términos en
/// su texto de búsqueda, sin tildes ni mayúsculas y en cualquier orden, como el buscador de personas— y en la copia fiscal del documento
/// (el nombre y la identificación con que salió). Con menos letras de las que pide el buscador, «contiene» sobre la identificación y los
/// nombres, como antes.
/// </summary>
public static class BusquedaDeClienteDeVenta
{
    public static IQueryable<InventoryDocument> DelCliente(this IQueryable<InventoryDocument> consulta, IApplicationDbContext db, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return consulta;
        var plano = texto.Trim().ToLower();
        var patrones = NormalizadorDeBusqueda.PatronesContiene(texto);
        var escape = NormalizadorDeBusqueda.CaracterDeEscape.ToString();

        IQueryable<Person> personas = db.People;
        if (patrones.Count > 0)
        {
            foreach (var patron in patrones) personas = personas.Where(p => EF.Functions.Like(p.SearchText, patron, escape));
        }
        else
        {
            personas = personas.Where(p => p.TaxId.ToLower().Contains(plano) || p.FirstName.ToLower().Contains(plano)
                || p.LastName.ToLower().Contains(plano) || (p.BusinessName != null && p.BusinessName.ToLower().Contains(plano)));
        }

        return consulta.Where(d => personas.Any(p => p.Id == d.CounterpartyPersonId)
            || db.DocumentPartySnapshots.Any(s => s.DocumentId == d.Id && (s.TaxId.ToLower().Contains(plano) || s.LegalName.ToLower().Contains(plano))));
    }
}
