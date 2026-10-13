using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Lo que dejó una conciliación de las ventas de la carga: cuántas por desenlace y las filas escritas de la versión del cuadre. (nuevo)</summary>
public sealed record ConciliacionDeVentas(
    int Migradas,
    int CanceladasAntesDelCorte,
    int NoMigradasConSaldo,
    int FueraDeCartera,
    IReadOnlyList<MigrationSaleReconciliation> Filas);

/// <summary>
/// El clasificador de FR-143 (feature 013, US6, T0594; T83, FR-055; mensajes.md §15.4; data-model §21.5): las ventas a crédito acumuladas en la bandeja
/// <b>sin consumir</b> con fecha hasta el corte de la carga (<see cref="IMensajesEntrantes.PendientesAsync"/> con el destino <c>Lending</c>; nunca
/// las consume ni las intenta) y sus ajustes, cruzadas por factura y pago con las partes <c>loans</c> y <c>sales</c> de la carga:
/// <list type="bullet">
/// <item><c>Migrated</c>: un crédito de la parte <c>loans</c> lleva esa factura;</item>
/// <item><c>CancelledBeforeCutoff</c>: la parte <c>sales</c> la carga como cancelada antes del corte;</item>
/// <item><c>NotMigratedWithBalance</c>: ninguna de las dos (diferencia a aceptar con acta, D17);</item>
/// <item><c>OutsideLendingD29</c>: sólo por marca desde la lista, que se copia de la versión anterior del cuadre (la fase de US8 es dueña de las marcas,
/// las diferencias y su aceptación; notas de coordinación PE, contradicción 12).</item>
/// </list>
/// Cuenta los ajustes de cada venta hasta y después del corte. Una factura y un pago no originan dos filas (FR-055). Las ventas con
/// <c>CreditWithoutLending</c> no aparecen: Comercial no las emite. Escribe las filas de <c>LND_MigrationSaleReconciliations</c> de la versión del
/// cuadre sin guardar; lo llaman <c>ApplyMigrationLoadCommand</c> y <c>ActivateLendingCommand</c> (US8). (nuevo)
/// </summary>
public sealed class ConciliacionDeVentasDeLaCarga(IApplicationDbContext db, IMensajesEntrantes entrantes)
{
    /// <summary>Los tipos que se concilian.</summary>
    public static IReadOnlyList<string> Tipos { get; } = [VentaACreditoRegistradaV1.Type, AjusteDeVentaACreditoV1.Type];

    public const int TamañoDePagina = PageRequest.MaxPageSize;

    public async Task<Result<ConciliacionDeVentas>> ConciliarAsync(MigrationLoad carga, int checkVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        var hastaElCorte = await PendientesAsync(carga.CutoffDate, ct);
        if (hastaElCorte.IsFailure) return Result.Failure<ConciliacionDeVentas>(hastaElCorte.Error);
        var todos = await PendientesAsync(DateOnly.MaxValue, ct);
        if (todos.IsFailure) return Result.Failure<ConciliacionDeVentas>(todos.Error);

        var partes = db.MigrationLoadParts.AsNoTracking().Where(p => p.MigrationLoadId == carga.Id && !p.IsDeleted).Select(p => p.Id);
        var creditos = await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId) && r.InvoiceNumber != null)
            .Select(r => new { r.Id, r.InvoiceNumber }).ToListAsync(ct);
        var canceladas = await db.MigrationSaleRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId))
            .Select(r => new { r.Id, r.InvoiceNumber }).ToListAsync(ct);
        var anteriores = await db.MigrationSaleReconciliations.AsNoTracking()
            .Where(r => r.MigrationLoadId == carga.Id && r.CheckVersion < checkVersion && r.MarkedFromList && !r.IsDeleted)
            .OrderByDescending(r => r.CheckVersion).ToListAsync(ct);
        var deLasVentas = hastaElCorte.Value.Select(m => m.Envelope.PersonPublicId).OfType<Guid>().Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => deLasVentas.Contains(p.PublicId))
            .ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);

        var ajustes = todos.Value.Select(m => m.Payload as AjusteDeVentaACreditoV1 is { } a ? (Ajuste: a, Fecha: m.Envelope.Origin.OperationDate) : default)
            .Where(x => x.Ajuste is not null).ToList();
        var filas = new List<MigrationSaleReconciliation>();
        var vistos = new HashSet<Guid>();
        foreach (var m in hastaElCorte.Value)
        {
            if (VentaRecibida.De(m) is not { } venta || !vistos.Add(venta.Pago.PaymentPublicId)) continue;
            var factura = venta.NumeroDeFactura;
            var migrado = creditos.FirstOrDefault(r => string.Equals(r.InvoiceNumber, factura, StringComparison.OrdinalIgnoreCase));
            var cancelada = migrado is null ? canceladas.FirstOrDefault(r => string.Equals(r.InvoiceNumber, factura, StringComparison.OrdinalIgnoreCase)) : null;
            var marca = anteriores.FirstOrDefault(r => r.CreditPaymentPublicId == venta.Pago.PaymentPublicId);
            var desenlace = migrado is not null ? SaleReconciliationOutcome.Migrated
                : cancelada is not null ? SaleReconciliationOutcome.CancelledBeforeCutoff
                : marca is not null ? marca.Outcome
                : SaleReconciliationOutcome.NotMigratedWithBalance;
            var suyos = ajustes.Where(x => x.Ajuste.OriginalMessageId == m.MessageId || x.Ajuste.OriginalCreditPaymentPublicId == venta.Pago.PaymentPublicId).ToList();
            var fila = new MigrationSaleReconciliation
            {
                MigrationLoadId = carga.Id,
                CheckVersion = checkVersion,
                MessagePublicId = m.MessageId,
                SaleDocumentPublicId = venta.DocumentoPublicId,
                InvoiceNumber = factura.Length <= 30 ? factura : factura[..30],
                CreditPaymentPublicId = venta.Pago.PaymentPublicId,
                SaleDate = venta.Fecha,
                Amount = venta.Pago.Amount,
                PersonId = m.Envelope.PersonPublicId is { } p && personas.TryGetValue(p, out var id) ? id : null,
                Outcome = desenlace,
                MigrationLoanRowId = migrado?.Id,
                MigrationSaleRowId = cancelada?.Id,
                MarkedFromList = migrado is null && cancelada is null && marca is not null,
                MarkReason = migrado is null && cancelada is null ? marca?.MarkReason : null,
                MarkedByUserId = migrado is null && cancelada is null ? marca?.MarkedByUserId : null,
                MarkedAt = migrado is null && cancelada is null ? marca?.MarkedAt : null,
                AdjustmentsUpToCutoff = suyos.Count(x => x.Fecha <= carga.CutoffDate),
                AdjustmentsAfterCutoff = suyos.Count(x => x.Fecha > carga.CutoffDate),
            };
            db.MigrationSaleReconciliations.Add(fila);
            filas.Add(fila);
        }
        return Result.Success(new ConciliacionDeVentas(
            filas.Count(f => f.Outcome == SaleReconciliationOutcome.Migrated),
            filas.Count(f => f.Outcome == SaleReconciliationOutcome.CancelledBeforeCutoff),
            filas.Count(f => f.Outcome == SaleReconciliationOutcome.NotMigratedWithBalance),
            filas.Count(f => f.Outcome == SaleReconciliationOutcome.OutsideLendingD29),
            filas));
    }

    /// <summary>Todos los pendientes de los dos tipos hasta la fecha, página por página (no consume nada).</summary>
    private async Task<Result<IReadOnlyList<MensajeEntrante>>> PendientesAsync(DateOnly hasta, CancellationToken ct)
    {
        var salida = new List<MensajeEntrante>();
        for (var pagina = 1; ; pagina++)
        {
            var leidos = await entrantes.PendientesAsync(IntegrationDestinations.Lending, Tipos, hasta, new PageRequest(pagina, TamañoDePagina), ct);
            if (leidos.IsFailure) return Result.Failure<IReadOnlyList<MensajeEntrante>>(leidos.Error);
            salida.AddRange(leidos.Value.Items);
            if (leidos.Value.Items.Count < TamañoDePagina || salida.Count >= leidos.Value.TotalCount) break;
        }
        return Result.Success<IReadOnlyList<MensajeEntrante>>(salida);
    }
}
