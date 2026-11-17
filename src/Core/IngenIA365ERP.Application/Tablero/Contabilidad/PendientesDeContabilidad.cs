using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Contabilidad;

// 2026-11-17 (pedido del dueño): lo que espera a Contabilidad en el tablero de inicio. Cada pendiente sólo cuenta, con el permiso
// de la acción que lo resuelve.

/// <summary>
/// Comprobantes listos para contabilizar: borradores cuadrados y con valor (uno descuadrado no se puede contabilizar; ése lo ve
/// quien lo digita en «Comprobantes en borrador»). Respeta el alcance de sucursal, como la lista de Comprobantes.
/// </summary>
public sealed class ComprobantesPorContabilizar(IApplicationDbContext db, IUserBranchScope alcance) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "contabilidad.por-contabilizar", "Comprobantes por contabilizar", "Contabilidad", "Accounting.Vouchers.Post", "/contabilidad/comprobantes");

    public async Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        await (await BorradoresContables.VisiblesAsync(db, alcance, ct))
            .CountAsync(d => d.TotalDebit > 0 && d.TotalDebit == d.TotalCredit, ct);
}

/// <summary>Meses ya terminados que siguen abiertos (el cierre mensual pendiente).</summary>
public sealed class MesesPorCerrar(IApplicationDbContext db) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "contabilidad.meses-por-cerrar", "Meses contables por cerrar", "Contabilidad", "Accounting.Periods.Close", "/contabilidad/periodos");

    public Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        db.AccountingPeriods.AsNoTracking()
            .CountAsync(p => !p.IsDeleted && !p.FiscalYear!.IsDeleted && p.Status == PeriodStatus.Open && p.EndDate < contexto.Hoy, ct);
}

/// <summary>
/// Hechos de un módulo que Contabilidad rechazó al contabilizar (entrega <c>Rejected</c>): esperan que alguien corrija la causa y
/// los reprocese desde los lotes del módulo. Reprocesar los pasa a <c>InBatch</c>, así que la cuenta baja sola.
/// </summary>
public abstract class HechosRechazadosPorContabilidad(IApplicationDbContext db, string modulo) : IPendienteDelTablero
{
    public abstract DefinicionDePendiente Definicion { get; }

    public Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        db.IntegrationMessageDeliveries.AsNoTracking()
            .CountAsync(d => !d.IsDeleted && d.Destination == IntegrationDestinations.Accounting && d.Status == DeliveryStatus.Rejected
                             && d.Message!.OriginModule == modulo, ct);
}

public sealed class HechosDeInventarioRechazados(IApplicationDbContext db) : HechosRechazadosPorContabilidad(db, ModulosDeOrigen.Inventario)
{
    public override DefinicionDePendiente Definicion { get; } = new(
        "contabilidad.rechazos-de-inventario", "Hechos de Comercial rechazados por Contabilidad", "Contabilidad",
        "Accounting.InventoryBatches.View", "/contabilidad/inventario/lotes");
}

public sealed class HechosDeCarteraRechazados(IApplicationDbContext db) : HechosRechazadosPorContabilidad(db, ModulosDeOrigen.Cartera)
{
    public override DefinicionDePendiente Definicion { get; } = new(
        "contabilidad.rechazos-de-cartera", "Hechos de Cartera rechazados por Contabilidad", "Contabilidad",
        "Accounting.LendingBatches.View", "/contabilidad/cartera/lotes");
}

public sealed class HechosDeRecaudoRechazados(IApplicationDbContext db) : HechosRechazadosPorContabilidad(db, ModulosDeOrigen.Recaudo)
{
    public override DefinicionDePendiente Definicion { get; } = new(
        "contabilidad.rechazos-de-recaudo", "Hechos de Recaudo rechazados por Contabilidad", "Contabilidad",
        "Accounting.ReceiptsMatrix.View", "/contabilidad/recaudo/lotes");
}
