using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// La estrategia de <c>PurchaseOrder</c> (feature 012, I5, T787; FR-036, FR-048; contracts/api.md §14.9). (nuevo)
/// <list type="bullet">
/// <item>la forma de §14.9: proveedor (<c>Inventory.Document.FieldRequired</c> con <c>counterparty</c>), bodega que recibe,
/// <c>expectedDate</c>, condiciones de pago en sus notas, y por línea el precio pactado (<c>Validation.Invalid</c> sin él) con sus
/// descuentos; las líneas tomadas de una solicitud dejan el vínculo <c>FromOrder</c> orden ← solicitud (lo escribe el borrador y aquí se
/// vuelve a exigir que la solicitud esté confirmada);</item>
/// <item>impuestos y totales con <see cref="CalculoTributarioDeCompra"/> a la fecha, como estimación: <b>el límite de monto y la
/// política se miden con su <c>Total</c></b> (§1.3; <c>Inventory.Purchases.Confirm</c>: por encima del máximo del permiso queda
/// <c>PendingApproval</c> o, sin nivel que forzar, <c>Inventory.Approval.AmountExceedsLimit</c>);</item>
/// <item><b>no</b> escribe kardex ni emite mensajes, y no guarda la foto de impuestos (la factura del proveedor la guarda): hereda de la
/// base el efecto y los mensajes vacíos. Confirmada, lo pendiente de recibir se calcula por sus vínculos (<see
/// cref="Purchasing.PendientesDeCompra"/>) y cuenta como «por recibir» en la posición de reposición hasta que se cierre su saldo.</item>
/// </list>
/// </summary>
public sealed class EfectoDeOrdenDeCompra(
    IMaestrosDelDocumento maestros,
    CalculoTributarioDeCompra calculo,
    VinculosDeCompra vinculos) : EfectoDeClaseBase
{
    public override DocumentClass Clase => DocumentClass.PurchaseOrder;

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var avisos = new List<Error>(await ReglasDeCompra.ProductosComprablesAsync(contexto.Documento, maestros, ct));
        avisos.AddRange(SinPrecio(contexto.Documento));
        return avisos;
    }

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return Result.Success();
        var documento = contexto.Documento;

        var comunes = await ReglasDeCompra.ComunesAsync(contexto, maestros, ct);
        if (comunes is not null) return Result.Failure(comunes);
        if (documento.ExpectedDate is null) return Result.Failure(InventoryErrors.FieldRequired(BorradorDeCompra.CampoEntregaEsperada));
        var productos = await ReglasDeCompra.ProductosComprablesAsync(documento, maestros, ct);
        if (productos.Count > 0) return Result.Failure(productos[0]);
        if (SinPrecio(documento).FirstOrDefault() is { } sinPrecio) return Result.Failure(sinPrecio);

        // Las solicitudes de origen siguen aprobadas (pudieron anularse entre el borrador y la confirmación).
        foreach (var solicitud in await vinculos.OrigenesAsync(documento, DocumentLinkKind.FromOrder, ct))
        {
            if (solicitud.Status == DocumentStatus.Confirmed) continue;
            var linea = documento.Lines.Where(l => !l.IsDeleted).Select(l => l.LineNumber).DefaultIfEmpty(1).Min();
            return Result.Failure(ErroresDeCompras.RequestNotConfirmed(linea, solicitud.PublicId,
                VistaDeDocumentos.NumeroVisible(solicitud.Prefix, solicitud.Number), solicitud.Status));
        }

        var calculado = await calculo.CalcularAsync(documento, contexto.Tipo, ct);
        if (calculado.IsFailure) return Result.Failure(calculado.Error);
        CalculoTributarioDeCompra.AplicarTotales(documento, calculado.Value.Totales);
        return Result.Success();
    }

    /// <summary>El <c>Total</c> de la orden (§1.3): lo que mide el límite de monto de <c>Inventory.Purchases.Confirm</c>.</summary>
    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    private static IEnumerable<Error> SinPrecio(InventoryDocument documento) =>
        documento.Lines.Where(l => !l.IsDeleted && l.UnitPrice <= 0m).OrderBy(l => l.LineNumber)
            .Select(l => ErroresDeCompras.OrderPriceRequired(l.LineNumber));
}
