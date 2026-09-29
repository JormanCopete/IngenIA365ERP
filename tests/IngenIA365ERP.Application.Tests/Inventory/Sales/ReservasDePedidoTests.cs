using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.Reservas;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I6, T864 (US14-1, FR-033, FR-052; data-model §3.2, §3.6, §14; SC-006): el pedido <b>reserva</b> al confirmar
/// (<c>ReservasDeInventario</c>, único escritor de <c>INV_Reservations</c> y de <c>Reserved</c>). P1 tiene 100 físicas en PRIN: un pedido de 94
/// deja disponibles 6 (el mismo caso que el de 4 sobre 10 del Independent Test, con la existencia de la cooperativa de prueba).
/// </summary>
public class ReservasDePedidoTests
{
    [Fact]
    public async Task Confirmar_el_pedido_reserva_sin_kardex_ni_mensajes_y_baja_el_disponible()
    {
        var c = await CicloComercialDePrueba.CrearAsync();

        var pedido = await c.PedidoAsync(94m);

        var reserva = c.ReservasDe(pedido).Should().ContainSingle().Subject;
        (reserva.Status, reserva.QuantityBase, reserva.ConsumedQuantityBase).Should().Be((ReservationStatus.Active, 94m, 0m));
        (c.Fisico(c.P1), c.Reservado(c.P1)).Should().Be((100m, 94m), "disponible = 100 − 94 = 6");
        c.KardexDe(pedido).Should().BeEmpty("el pedido no mueve existencia");
        c.MensajesDe(pedido.PublicId).Should().BeEmpty("el pedido no emite mensajes (FR-075)");
        pedido.Number.Should().NotBeNull();
    }

    [Fact]
    public async Task Un_pedido_que_no_cabe_en_el_disponible_responde_existencia_insuficiente_con_el_disponible()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        await c.PedidoAsync(94m);

        var otro = CicloComercialDePrueba.Exito(await c.GuardarAsync(c.Documento("PED", lineas: c.Linea(c.P1, 7m)), RutasDeVenta.Pedidos));
        var r = await c.ConfirmarAsync(otro.PublicId);

        r.Error.Code.Should().Be("Inventory.Stock.Insufficient");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { available = 6m }, o => o.ExcludingMissingMembers());
        c.Reservado(c.P1).Should().Be(94m, "un rechazo no deja nada a medio escribir");
    }

    [Fact]
    public async Task La_remision_y_la_factura_desde_el_pedido_consumen_la_reserva_en_su_misma_transaccion()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var pedido = await c.PedidoAsync(10m);

        var remision = await c.RemisionAsync(4m, pedido);
        var parcial = c.ReservasDe(pedido).Single();
        (parcial.Status, parcial.ConsumedQuantityBase).Should().Be((ReservationStatus.Active, 4m));
        (c.Fisico(c.P1), c.Reservado(c.P1)).Should().Be((96m, 6m), "salen 4 de lo reservado: el disponible no cambia por la remisión");
        c.KardexDe(remision).Should().ContainSingle(k => k.QuantityBase == -4m);

        var lineaDelPedido = pedido.Lines.Single(l => !l.IsDeleted).PublicId;
        var factura = await c.FacturaAsync("FV", [pedido.PublicId], c.Linea(c.P1, 6m) with { OriginLinePublicId = lineaDelPedido });
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(factura));

        var total = c.ReservasDe(pedido).Single();
        (total.Status, total.ConsumedQuantityBase, total.ReleasedByDocumentId).Should().Be((ReservationStatus.Consumed, 10m, c.Doc(factura).Id));
        (c.Fisico(c.P1), c.Reservado(c.P1)).Should().Be((90m, 0m));
    }

    [Fact]
    public async Task Ninguna_salida_pasa_de_lo_pendiente_del_pedido()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var pedido = await c.PedidoAsync(5m);
        var linea = pedido.Lines.Single(l => !l.IsDeleted).PublicId;
        await c.RemisionAsync(3m, pedido);

        var borrador = CicloComercialDePrueba.Exito(await c.GuardarAsync(
            c.Documento("REM", origenes: [pedido.PublicId], lineas: c.Linea(c.P1, 3m) with { OriginLinePublicId = linea }), RutasDeVenta.Remisiones));
        var r = await c.ConfirmarAsync(borrador.PublicId);

        r.Error.Code.Should().Be("Inventory.Order.ExceedsPending");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { lines = new[] { new { OrderLinePublicId = linea, Pending = 2m, Requested = 3m } } });
    }

    [Fact]
    public async Task Anular_el_pedido_libera_la_reserva_con_su_motivo_y_sin_mensajes()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var pedido = await c.PedidoAsync(8m);

        var anulado = await c.AnularAsync(pedido.PublicId, "El cliente desistió");

        anulado.IsSuccess.Should().BeTrue(anulado.IsFailure ? $"{anulado.Error.Code}: {anulado.Error.Message}" : null);
        var reserva = c.ReservasDe(pedido).Single();
        (reserva.Status, reserva.ReleaseReason, reserva.ReleasedByDocumentId).Should().Be((ReservationStatus.Released, "El cliente desistió",
            c.Doc(anulado.Value.VoidingDocumentPublicId).Id));
        c.Reservado(c.P1).Should().Be(0m);
        c.MensajesDe(anulado.Value.VoidingDocumentPublicId).Should().BeEmpty();
    }

    [Fact]
    public async Task El_vencimiento_se_sella_al_confirmar_y_el_proceso_vence_solo_las_reservas_vencidas()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        c.V.Parametro(ParametrosDeInventario.VentasReservaDiasVencimiento, "5");
        var pedido = await c.PedidoAsync(3m);
        c.V.Parametro(ParametrosDeInventario.VentasReservaDiasVencimiento, "30");
        var hoy = Catalog.CatalogoDePrueba.Hoy;

        c.Doc(pedido.PublicId).ValidUntil.Should().Be(hoy.AddDays(5));
        c.ReservasDe(pedido).Single().ExpiresOn.Should().Be(hoy.AddDays(5), "cambiar el parámetro después no mueve la reserva viva");

        var vencer = new ReleaseExpiredReservationsCommandHandler(c.Db, c.Reservas(), c.V.K.Cerrojo, c.V.Compras.C.Reloj);
        (await vencer.Handle(new ReleaseExpiredReservationsCommand(), default)).Value.Should().Be(0, "el día que vence todavía reserva");

        c.V.Compras.C.Reloj.HoyLocal.Returns(hoy.AddDays(6));
        (await vencer.Handle(new ReleaseExpiredReservationsCommand(), default)).Value.Should().Be(1);
        (c.ReservasDe(pedido).Single().Status, c.Reservado(c.P1)).Should().Be((ReservationStatus.Expired, 0m));
    }

    [Fact]
    public async Task La_reconstruccion_y_la_verificacion_recalculan_lo_reservado_desde_las_reservas_vivas()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        await c.PedidoAsync(9m);
        var fila = c.Db.StockBalances.Single(s => s.ProductId == c.V.K.ProductoId(c.P1) && s.WarehouseId == c.V.K.Principal.Id);
        fila.Reserved = 2m; // una proyección dañada
        await c.Db.SaveChangesAsync();

        var verificacion = await new VerificacionDeIntegridad(c.Db).VerificarAsync(new AlcanceDeVerificacion(null, null), default);
        verificacion.Incidentes.Should().ContainSingle(i => i.Field == "Reserved" && i.Expected == 9m && i.Actual == 2m);

        var reconstruida = await new RebuildInventoryProjectionsCommandHandler(c.Db, c.V.K.Alcance, c.V.K.Cerrojo, c.V.Compras.C.Reloj)
            .Handle(new RebuildInventoryProjectionsCommand([c.P1], null, "Reserva dañada"), default);
        reconstruida.IsSuccess.Should().BeTrue();
        c.Reservado(c.P1).Should().Be(9m);
        (await new VerificacionDeIntegridad(c.Db).VerificarAsync(new AlcanceDeVerificacion(null, null), default)).Incidentes.Should().BeEmpty();
    }
}
