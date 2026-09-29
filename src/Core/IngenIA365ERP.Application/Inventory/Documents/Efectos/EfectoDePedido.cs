using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Sales.Reservas;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// <c>SalesOrder</c>, el pedido (feature 012, I6, T879; FR-033, FR-052; data-model §14; contracts/api.md §18.4):
/// <list type="bullet">
/// <item>antes de la aprobación: productos activos y no bloqueados; el monto que se aprueba es su total;</item>
/// <item>bloquea la fila de <c>INV_StockBalances</c> de cada (producto inventariable, bodega) en el orden canónico del cerrojo (T15): dos pedidos
/// simultáneos del último disponible no se pasan;</item>
/// <item>dentro del cerrojo sella <c>ValidUntil</c> = fecha del pedido + <c>Ventas.ReservaDiasVencimiento</c> vigente a esa fecha (cambiar el
/// parámetro después no mueve las reservas vivas) y reserva cada línea por <see cref="ReservasDeInventario.ReservarAsync"/>: sin disponible,
/// <c>Inventory.Stock.Insufficient</c> con <c>data.available</c>;</item>
/// <item>no escribe kardex ni emite mensajes (FR-075: la factura desde el pedido sella su propio modo);</item>
/// <item>su anulación libera lo reservado que quede (<see cref="ReservasDeInventario.LiberarAsync"/>), sin mensajes.</item>
/// </list>
/// Scoped: recuerda por documento los productos inventariables entre la validación y el efecto. (nuevo)
/// </summary>
public sealed class EfectoDePedido(IMaestrosDelDocumento maestros, ReservasDeInventario reservas, ILectorDeParametros parametros) : EfectoDeClaseBase
{
    private readonly Dictionary<Guid, IReadOnlySet<int>> _inventariables = [];

    public override DocumentClass Clase => DocumentClass.SalesOrder;

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.EsAnulacion ? contexto.Original! : contexto.Documento;
        var productos = await ReglasDeLineasDeVenta.ProductosAsync(documento, maestros, ct);
        if (productos.IsFailure && !contexto.EsAnulacion) return Result.Failure(productos.Error);
        if (productos.IsSuccess) _inventariables[contexto.Documento.PublicId] = productos.Value;
        return Result.Success();
    }

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto)
    {
        var comun = base.Cerrojo(contexto);
        var documento = contexto.EsAnulacion ? contexto.Original! : contexto.Documento;
        var inventariables = _inventariables.GetValueOrDefault(contexto.Documento.PublicId);
        return comun with { Existencias = ReservasDeInventario.Claves(documento, inventariables) };
    }

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var pedido = contexto.Documento;
        var dias = await parametros.LeerComoAsync<int>(ParametrosDeInventario.Modulo, ParametrosDeInventario.VentasReservaDiasVencimiento, pedido.OperationDate, ct: ct);
        if (dias.IsFailure) return Result.Failure(dias.Error);
        var vence = pedido.OperationDate.AddDays(Math.Max(0, dias.Value));
        pedido.ValidUntil = vence;
        return await reservas.ReservarAsync(pedido, _inventariables.GetValueOrDefault(pedido.PublicId) ?? new HashSet<int>(), vence, ct);
    }

    public override async Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        await reservas.LiberarAsync(contexto.Original!, contexto.Documento, contexto.Documento.Reason ?? "Anulado el pedido.", ct);
        return Result.Success();
    }
}
