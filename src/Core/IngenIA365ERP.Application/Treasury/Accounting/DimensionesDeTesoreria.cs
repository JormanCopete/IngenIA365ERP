using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Accounting;

/// <summary>
/// Lo que Tesorería ofrece a Contabilidad para la plantilla y la completitud de su matriz (feature 015, US8, T0260; FR-143): las cuentas propias (por código), los
/// orígenes de las órdenes y las clases de orden que admite o que ya recibió. Sólo lectura. (nuevo)
/// </summary>
public sealed class DimensionesDeTesoreria(IApplicationDbContext db) : IDimensionesDeTesoreria
{
    public async Task<IReadOnlyList<DimensionDeMatrizDto>> DimensionesAsync(CancellationToken ct)
    {
        var lista = new List<DimensionDeMatrizDto>();
        var cuentas = await db.OwnBankAccounts.AsNoTracking().Select(c => new { c.Code, c.Name }).ToListAsync(ct);
        lista.AddRange(cuentas.Select(c => new DimensionDeMatrizDto("OwnBankAccountCode", c.Code, c.Name)));
        lista.AddRange(Enum.GetNames<PaymentOrderOrigin>().Select(o => new DimensionDeMatrizDto("OrderOrigin", o, o)));
        var clases = await db.PaymentOrders.AsNoTracking().Select(o => new { o.OrderKind, o.Origin }).Distinct().ToListAsync(ct);
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in clases.Where(c => !string.IsNullOrWhiteSpace(c.OrderKind)))
            if (vistas.Add(c.OrderKind)) lista.Add(new DimensionDeMatrizDto("OrderKind", c.OrderKind, c.OrderKind, c.Origin.ToString()));
        foreach (var c in OrdenesDePagoDeTesoreria.ClasesDeRecaudo)
            if (vistas.Add(c)) lista.Add(new DimensionDeMatrizDto("OrderKind", c, c, nameof(PaymentOrderOrigin.Receipts)));
        return lista;
    }
}
