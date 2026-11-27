using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// Un crédito que la solicitud recoge, como lo miden las validaciones de la originación (2026-11-27, pedido del dueño; feature 028, R8, T025; FR-008):
/// las validaciones miden la situación <b>después de recoger</b>. Las reglas, en un solo sitio:
/// <list type="bullet">
/// <item><b>la cuota</b>: un crédito recogido <b>total</b> deja de pagar su próxima cuota, que sale de la capacidad de pago y de la de descuento (nómina
/// propia o libranza, si el crédito descontaba por ella). Un <b>parcial no reduce la cuota</b>: el abono a capital puede reducir el plazo, no la cuota,
/// según lo que elija el deudor (D7); contarla sería optimista;</item>
/// <item><b>el capital</b>: lo que se recoge sale del endeudamiento consolidado, del cupo y de la concentración: en un total, todo el capital del crédito
/// que cuenta esa medida; en un parcial, el valor pedido, topado en ese capital. Nunca por debajo de cero: lo que se libera de un crédito no pasa de lo
/// que ese crédito sumaba. El valor parcial cuenta entero como capital porque un crédito que se puede recoger está al día y no tiene nada vencido que
/// cubrir primero.</item>
/// </list>
/// El monto consolidado con que se enruta la solicitud (quién decide) no cambia: sigue siendo lo pedido más la exposición sin restar lo recogido (más
/// exposición, nivel más alto: conservador) y así la huella del enrutamiento no se mueve con la proyección. (nuevo)
/// </summary>
/// <param name="LoanId">El crédito recogido.</param>
/// <param name="Number">Su número visible.</param>
/// <param name="Total">Si se recoge total (si no, parcial).</param>
/// <param name="ValorParcial">El valor pedido de un parcial.</param>
public sealed record RecogidaEnLaValidacion(int LoanId, string Number, bool Total, decimal? ValorParcial)
{
    /// <summary>Lo que se libera de un crédito que suma <paramref name="capitalDelCredito"/> en la medida: todo en un total; el valor pedido, topado, en un parcial.</summary>
    public decimal CapitalQueLibera(decimal capitalDelCredito) =>
        capitalDelCredito <= 0m ? 0m : Total ? capitalDelCredito : Math.Min(Math.Max(0m, ValorParcial ?? 0m), capitalDelCredito);

    /// <summary>Los créditos recogidos total: los que dejan de pagar su próxima cuota.</summary>
    public static IReadOnlyCollection<int> Totales(IReadOnlyList<RecogidaEnLaValidacion> recogidas) =>
        recogidas.Where(r => r.Total).Select(r => r.LoanId).ToHashSet();

    /// <summary>El capital que liberan las recogidas en una medida que cuenta <paramref name="capitalDe"/> por crédito (cero si no lo cuenta).</summary>
    public static decimal Liberado(IReadOnlyList<RecogidaEnLaValidacion> recogidas, Func<int, decimal> capitalDe) =>
        recogidas.Sum(r => r.CapitalQueLibera(capitalDe(r.LoanId)));

    /// <summary>Las recogidas vivas de créditos de la solicitud (las filas de <c>LND_CreditApplicationPayoffs</c>), en su orden.</summary>
    public static async Task<IReadOnlyList<RecogidaEnLaValidacion>> DeLaSolicitudAsync(IApplicationDbContext db, int solicitudId, CancellationToken ct) =>
        await db.CreditApplicationPayoffs.AsNoTracking()
            .Where(f => f.CreditApplicationId == solicitudId && !f.IsDeleted && f.ProductKind == PayoffProductKind.Loan && f.LoanId != null)
            .OrderBy(f => f.Order).ThenBy(f => f.Id)
            .Select(f => new RecogidaEnLaValidacion(f.LoanId!.Value, f.ProductNumber, f.Mode == PayoffMode.Total, f.RequestedAmount))
            .ToListAsync(ct);
}
