using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La anulación de una venta con su documento contrario (feature 012, I3, T610; FR-066; data-model §14): sólo el comprobante y la nota
/// no electrónicos (<see cref="ClasesDeDocumento.SeAnulaConAnulacion"/>); un fiscal electrónico se corrige con su nota
/// (<c>Inventory.Document.FiscalUseCorrection</c> con <c>data.route</c>). La mercancía vuelve (o sale, si anula una nota con devolución) al
/// costo del original por <see cref="ReversionDeKardex"/>; los bonos que usó la venta vuelven a estar disponibles (<c>Released</c>, con la
/// anulación que los liberó); emite <c>DocumentoAnulado</c> con el contenido del original en signo contrario. Lo comparten
/// <see cref="SalidaPorVenta"/> y <see cref="DevolucionDeCliente"/>: la anulación no tiene estrategia propia (T17). Scoped. (nuevo)
/// </summary>
public sealed class AnulacionDeVenta(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IApplicationDbContext db,
    IDateTimeService reloj)
{
    private readonly Dictionary<Guid, PreparacionDelRegistro> _preparados = [];
    private readonly Dictionary<Guid, ReversionHecha> _revertidos = [];

    /// <summary>Antes de la aprobación: la clase del original se anula con documento contrario; prepara lo que se bloquea.</summary>
    public async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var original = contexto.Original!;
        // I4 (T724, T725): un fiscal electrónico rechazado por la DIAN se anula sin efecto fiscal (casos b y c); cualquier otro, con su nota.
        if (!ClasesDeDocumento.SeAnulaConAnulacion(original.Class) && emision.CasoFiscalDe(contexto.Documento.PublicId) is null)
            return Result.Failure(ErroresDeVentas.FiscalUseCorrection(original.Class));
        var movimientos = await reversion.MovimientosAsync(contexto.Documento, original, ct);
        if (movimientos.Count == 0) return Result.Success();
        var preparado = await registro.PrepararAsync(contexto.Documento, movimientos, ct);
        if (preparado.IsFailure) return Result.Failure(preparado.Error);
        _preparados[contexto.Documento.PublicId] = preparado.Value;
        return Result.Success();
    }

    /// <summary>Lo que bloquea la anulación, sumado a lo común.</summary>
    public PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto, PedidoDeCerrojo comun) =>
        _preparados.TryGetValue(contexto.Documento.PublicId, out var p)
            ? p.Cerrojo with { Bodegas = comun.Bodegas.Concat(p.Cerrojo.Bodegas).Distinct().ToList() }
            : comun;

    /// <summary>Dentro del cerrojo: revierte las cantidades al costo del original y libera los bonos de la venta.</summary>
    public async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var original = contexto.Original!;
        var revertido = await reversion.RevertirAsync(contexto.Documento, original, ct);
        if (revertido.IsFailure) return Result.Failure(revertido.Error);
        _revertidos[contexto.Documento.PublicId] = revertido.Value;

        var pagos = await db.DocumentPayments.AsNoTracking().Where(p => p.DocumentId == original.Id && !p.IsDeleted).Select(p => p.Id).ToListAsync(ct);
        foreach (var bono in await db.VoucherRedemptions.Where(v => pagos.Contains(v.DocumentPaymentId) && v.Status == VoucherRedemptionStatus.Active).ToListAsync(ct))
            bono.Liberar(contexto.Documento.Id, reloj.UtcNow, "Anulada la venta que lo usó.");
        return Result.Success();
    }

    /// <summary><c>DocumentoAnulado</c> y, si la reversión dejó diferencias, sus <c>AjusteDeCostoReconocido</c>.</summary>
    public async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var diferencias = _revertidos.TryGetValue(contexto.Documento.PublicId, out var hecha) ? hecha.Diferencias : [];
        // I4 (T724, T725): sin efecto fiscal, DocumentoAnulado lleva fiscalCase y el crédito se ajusta con VoidingByDianRejection.
        var caso = emision.CasoFiscalDe(contexto.Documento.PublicId);
        var contenidos = new List<object>(await emision.AnulacionAsync(contexto.Documento, contexto.Original!, diferencias, ct,
            caso is { } c ? RechazoFiscalEnCurso.Texto(c) : null));
        // I3 (T656): anular una venta a crédito ajusta todo su crédito en Cartera (Voiding).
        contenidos.AddRange(await emision.AjustesDeVentaACreditoAsync(contexto.Documento, contexto.Original!, null, ct,
            caso is null ? null : "VoidingByDianRejection"));
        return contenidos;
    }
}
