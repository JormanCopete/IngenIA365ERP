using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales.Reservas;

/// <summary>Lo que una salida consume de la reserva de una línea de pedido: la línea del pedido y la cantidad en unidad base. (nuevo)</summary>
public sealed record ConsumoDeReserva(int OrderLineId, decimal QuantityBase);

/// <summary>
/// <b>El único escritor</b> de <c>INV_Reservations</c> y de <c>INV_StockBalances.Reserved</c> (feature 012, I6, T877; FR-033, FR-052; data-model
/// §3.2, §3.6, §14; decisiones-transversales §1.3, T15). Todo corre <b>dentro del cerrojo</b> que tomó quien lo llama —la fila exclusiva de
/// <c>INV_StockBalances</c> de cada (producto, bodega), en el orden canónico— y nunca guarda: el <c>SaveChanges</c> es del ciclo común o del
/// comando de proceso. Cuatro operaciones:
/// <list type="bullet">
/// <item><see cref="ReservarAsync"/> (confirmar el pedido): una reserva <c>Active</c> por línea inventariable, con el vencimiento sellado, si el
/// disponible (<c>Physical − Reserved</c>) alcanza para todo el pedido —salvo <c>Existencias.StockNegativoPermitido</c> en la bodega—; si no,
/// <c>Inventory.Stock.Insufficient</c> con <c>data.available</c> y todas las líneas que no caben, sin tocar nada;</item>
/// <item><see cref="ConsumirAsync"/> (la remisión o la factura desde el pedido, en su misma transacción): parcial suma
/// <c>ConsumedQuantityBase</c>; total la deja <c>Consumed</c> con la salida en <c>ReleasedByDocumentId</c>;</item>
/// <item><see cref="LiberarAsync"/> (anular el pedido): lo que queda vuelve al disponible y la reserva queda <c>Released</c> con el motivo;</item>
/// <item><see cref="VencerAsync"/> (el proceso de reservas vencidas): las <c>Active</c> con <c>ExpiresOn</c> anterior a hoy quedan <c>Expired</c>.</item>
/// </list>
/// La reconstrucción (<c>RebuildInventoryProjectionsCommand</c>) recalcula <c>Reserved</c> desde las reservas <c>Active</c> y la verificación de
/// integridad lo compara; lo vigila <c>SoloLasReservasEscribenLoReservado</c>. Las filas de existencia las crea el cerrojo antes de bloquearlas:
/// sin fila no hay existencia que reservar. (nuevo)
/// </summary>
public sealed class ReservasDeInventario(IApplicationDbContext db, RegistroDeKardex registro, IDateTimeService reloj)
{
    /// <summary>Lo que falta: el pedido pide más de lo disponible.</summary>
    private sealed record Faltante(InventoryDocumentLine Linea, decimal Disponible);

    /// <summary>Las filas de existencia que bloquea el documento (una por producto y bodega de sus líneas vivas).</summary>
    public static IReadOnlyList<ClaveDeExistencia> Claves(InventoryDocument documento, IReadOnlySet<int>? inventariables = null) =>
        documento.WarehouseId is not int bodega
            ? []
            : documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m && (inventariables is null || inventariables.Contains(l.ProductId)))
                .Select(l => new ClaveDeExistencia(l.ProductId, bodega)).Distinct().ToList();

    /// <summary>Lo reservado que sigue vivo de una reserva.</summary>
    public static decimal Pendiente(Reservation r) => r.Status == ReservationStatus.Active ? r.QuantityBase - r.ConsumedQuantityBase : 0m;

    // ---------------------------------------------------------------------------------------------- reservar --

    /// <summary>
    /// Reserva las líneas inventariables del pedido hasta <paramref name="vence"/> (inclusive). Todo o nada: si alguna no cabe responde
    /// <c>Inventory.Stock.Insufficient</c> con cada línea y su disponible real, sin crear ninguna reserva.
    /// </summary>
    public async Task<Result> ReservarAsync(InventoryDocument pedido, IReadOnlySet<int> inventariables, DateOnly vence, CancellationToken ct)
    {
        if (pedido.WarehouseId is not int bodega) return Result.Success();
        var lineas = pedido.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m && inventariables.Contains(l.ProductId))
            .OrderBy(l => l.ProductId).ThenBy(l => l.LineNumber).ToList();
        if (lineas.Count == 0) return Result.Success();

        var parametros = await registro.LeerParametrosAsync(pedido.OperationDate, [bodega], ct);
        if (parametros.IsFailure) return Result.Failure(parametros.Error);
        var negativo = parametros.Value.NegativoPermitido(bodega);

        var productos = lineas.Select(l => l.ProductId).Distinct().ToList();
        var existencias = (await db.StockBalances.Where(s => productos.Contains(s.ProductId) && s.WarehouseId == bodega).ToListAsync(ct))
            .ToDictionary(s => s.ProductId);

        // Primera pasada: el disponible que va quedando por producto (varias líneas del mismo producto se suman).
        var usado = new Dictionary<int, decimal>();
        var faltantes = new List<Faltante>();
        foreach (var linea in lineas)
        {
            var fila = existencias.GetValueOrDefault(linea.ProductId);
            var disponible = (fila is null ? 0m : fila.Physical - fila.Reserved) - usado.GetValueOrDefault(linea.ProductId);
            if (!negativo && (fila is null || disponible < linea.QuantityBase))
            {
                faltantes.Add(new Faltante(linea, Math.Max(0m, disponible)));
                continue;
            }
            usado[linea.ProductId] = usado.GetValueOrDefault(linea.ProductId) + linea.QuantityBase;
        }
        if (faltantes.Count > 0) return Result.Failure(await ErrorAsync(faltantes, bodega, ct));

        // Segunda pasada: las reservas y la proyección, en la fila ya bloqueada.
        foreach (var linea in lineas)
        {
            if (!existencias.TryGetValue(linea.ProductId, out var fila)) continue; // sin fila (negativo permitido y nunca hubo existencia): no hay qué proyectar
            db.Reservations.Add(new Reservation
            {
                DocumentId = pedido.Id,
                DocumentLineId = linea.Id,
                ProductId = linea.ProductId,
                WarehouseId = bodega,
                QuantityBase = linea.QuantityBase,
                ExpiresOn = vence,
                Status = ReservationStatus.Active,
            });
            fila.Reserved += linea.QuantityBase;
        }
        return Result.Success();
    }

    // ---------------------------------------------------------------------------------------------- consumir --

    /// <summary>
    /// La salida <paramref name="salida"/> (remisión o factura desde el pedido) consume de la reserva de cada línea de pedido a lo sumo lo que
    /// le queda; lo que se pida de más ya no estaba reservado (venció o se liberó) y sale del disponible como cualquier salida. Una reserva
    /// consumida entera queda <c>Consumed</c> con la salida que consumió la última parte.
    /// </summary>
    public async Task ConsumirAsync(InventoryDocument salida, IReadOnlyList<ConsumoDeReserva> consumos, CancellationToken ct)
    {
        if (consumos.Count == 0) return;
        var lineas = consumos.Select(c => c.OrderLineId).Distinct().ToList();
        var reservas = await db.Reservations.Where(r => lineas.Contains(r.DocumentLineId) && r.Status == ReservationStatus.Active && !r.IsDeleted).ToListAsync(ct);
        if (reservas.Count == 0) return;
        var filas = await FilasAsync(reservas, ct);
        foreach (var consumo in consumos.GroupBy(c => c.OrderLineId))
        {
            var pedida = consumo.Sum(c => c.QuantityBase);
            foreach (var r in reservas.Where(r => r.DocumentLineId == consumo.Key).OrderBy(r => r.Id))
            {
                if (pedida <= 0m) break;
                var toma = Math.Min(pedida, Pendiente(r));
                if (toma <= 0m) continue;
                r.ConsumedQuantityBase += toma;
                pedida -= toma;
                if (filas.TryGetValue((r.ProductId, r.WarehouseId), out var fila)) fila.Reserved -= toma;
                if (Pendiente(r) == 0m)
                {
                    r.Status = ReservationStatus.Consumed;
                    r.ReleasedAt = reloj.UtcNow;
                    r.ReleasedByDocumentId = salida.Id == 0 ? null : salida.Id;
                }
            }
        }
    }

    // ----------------------------------------------------------------------------------------------- liberar --

    /// <summary>Anular el pedido: lo que queda reservado vuelve al disponible y cada reserva viva queda <c>Released</c> con el motivo.</summary>
    public async Task LiberarAsync(InventoryDocument pedido, InventoryDocument anulacion, string motivo, CancellationToken ct)
    {
        var reservas = await db.Reservations.Where(r => r.DocumentId == pedido.Id && r.Status == ReservationStatus.Active && !r.IsDeleted).ToListAsync(ct);
        await CerrarAsync(reservas, ReservationStatus.Released, anulacion.Id == 0 ? null : anulacion.Id, motivo, ct);
    }

    // ------------------------------------------------------------------------------------------------ vencer --

    /// <summary>Las reservas vivas vencidas a <paramref name="hoy"/> (<c>ExpiresOn</c> anterior), para que el proceso bloquee sus filas.</summary>
    public async Task<IReadOnlyList<Reservation>> VencidasAsync(DateOnly hoy, CancellationToken ct) =>
        await db.Reservations.Where(r => r.Status == ReservationStatus.Active && r.ExpiresOn < hoy && !r.IsDeleted).OrderBy(r => r.Id).ToListAsync(ct);

    /// <summary>Vence las reservas dadas (ya bloqueadas sus filas): lo que quedaba vuelve al disponible y quedan <c>Expired</c>. Devuelve cuántas.</summary>
    public async Task<int> VencerAsync(IReadOnlyList<Reservation> vencidas, CancellationToken ct)
    {
        var vivas = vencidas.Where(r => r.Status == ReservationStatus.Active).ToList();
        await CerrarAsync(vivas, ReservationStatus.Expired, null, "Venció la reserva del pedido.", ct);
        return vivas.Count;
    }

    // ------------------------------------------------------------------------------------------------- apoyo --

    private async Task CerrarAsync(IReadOnlyList<Reservation> reservas, ReservationStatus estado, int? documento, string motivo, CancellationToken ct)
    {
        if (reservas.Count == 0) return;
        var filas = await FilasAsync(reservas, ct);
        var texto = motivo.Trim();
        if (texto.Length > Reservation.LargoDelMotivo) texto = texto[..Reservation.LargoDelMotivo];
        foreach (var r in reservas)
        {
            if (filas.TryGetValue((r.ProductId, r.WarehouseId), out var fila)) fila.Reserved -= Pendiente(r);
            r.Status = estado;
            r.ReleasedAt = reloj.UtcNow;
            r.ReleasedByDocumentId = documento;
            r.ReleaseReason = texto;
        }
    }

    private async Task<Dictionary<(int, int), Domain.Entities.Inventory.Projections.StockBalance>> FilasAsync(IReadOnlyList<Reservation> reservas, CancellationToken ct)
    {
        var productos = reservas.Select(r => r.ProductId).Distinct().ToList();
        var bodegas = reservas.Select(r => r.WarehouseId).Distinct().ToList();
        return (await db.StockBalances.Where(s => productos.Contains(s.ProductId) && bodegas.Contains(s.WarehouseId)).ToListAsync(ct))
            .ToDictionary(s => (s.ProductId, s.WarehouseId));
    }

    private async Task<Error> ErrorAsync(IReadOnlyList<Faltante> faltantes, int bodega, CancellationToken ct)
    {
        var ids = faltantes.Select(f => f.Linea.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.PublicId, p.Code }, ct);
        var bodegaPublica = await db.Warehouses.AsNoTracking().Where(w => w.Id == bodega).Select(w => w.PublicId).FirstOrDefaultAsync(ct);
        return InventoryErrors.StockInsufficient(faltantes.OrderBy(f => f.Linea.LineNumber).Select(f => new InventoryErrors.LineaSinExistencia(
            f.Linea.LineNumber, productos.GetValueOrDefault(f.Linea.ProductId)?.PublicId ?? Guid.Empty, productos.GetValueOrDefault(f.Linea.ProductId)?.Code ?? string.Empty,
            bodegaPublica, null, f.Linea.QuantityBase, f.Disponible)).ToList());
    }
}
