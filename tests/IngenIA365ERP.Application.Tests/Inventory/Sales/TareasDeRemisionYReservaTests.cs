using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Sales.Reservas;
using IngenIA365ERP.Application.Inventory.Sales.Shipments;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I6, T868 (FR-022, FR-047, FR-052; contracts/api.md §13.4; decisiones-transversales T47): las tareas programadas de I6 —vencer
/// reservas y alertar remisiones sin facturar— corren por comandos de proceso sin ruta, son idempotentes y sólo desde I6; el cierre del período
/// no pasa con remisiones sin facturar hasta aceptarlas con permiso y motivo.
/// </summary>
public class TareasDeRemisionYReservaTests
{
    private static readonly DateOnly Hoy = Catalog.CatalogoDePrueba.Hoy;

    // ---------------------------------------------------------------------------------------------- reservas --

    [Fact]
    public async Task Vencer_las_reservas_es_idempotente_y_solo_toca_las_activas_vencidas()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var vencida = await c.PedidoAsync(2m);                 // vence el 10/10 (15 días)
        var consumida = await c.PedidoAsync(3m);
        await c.RemisionAsync(3m, consumida);                   // consumida entera: Consumed
        var anulada = await c.PedidoAsync(1m);
        (await c.AnularAsync(anulada.PublicId)).IsSuccess.Should().BeTrue();
        c.V.Compras.C.Reloj.HoyLocal.Returns(Hoy.AddDays(16));
        var vigente = await c.PedidoAsync(4m);                  // confirmado hoy (+16): vence después
        var vencer = new ReleaseExpiredReservationsCommandHandler(c.Db, c.Reservas(), c.V.K.Cerrojo, c.V.Compras.C.Reloj);

        var primera = await vencer.Handle(new ReleaseExpiredReservationsCommand(), default);
        var segunda = await vencer.Handle(new ReleaseExpiredReservationsCommand(), default);

        (primera.Value, segunda.Value).Should().Be((1, 0));
        c.ReservasDe(vencida).Single().Status.Should().Be(ReservationStatus.Expired);
        c.ReservasDe(consumida).Single().Status.Should().Be(ReservationStatus.Consumed);
        c.ReservasDe(anulada).Single().Status.Should().Be(ReservationStatus.Released);
        c.ReservasDe(vigente).Single().Status.Should().Be(ReservationStatus.Active);
        c.Reservado(c.P1).Should().Be(4m);
    }

    [Fact]
    public async Task Las_tareas_de_I6_corren_una_vez_al_dia_desde_I6_y_mandan_su_comando_de_proceso()
    {
        var ahora = new DateTimeOffset(2026, 10, 1, 6, 0, 0, TimeSpan.FromHours(-5));
        new TareaDeReservasVencidas(EntregaDelComercio.I5).DebeCorrer(ahora, null).Should().BeFalse("antes de I6 no hay pedidos");
        var reservas = new TareaDeReservasVencidas(EntregaDelComercio.I6);
        var remisiones = new TareaDeRemisionesSinFacturar(EntregaDelComercio.I6);
        (reservas.DebeCorrer(ahora, null), reservas.DebeCorrer(ahora, ahora.AddHours(-1)), reservas.DebeCorrer(ahora, ahora.AddDays(-1)))
            .Should().Be((true, false, true));
        (remisiones.DebeCorrer(ahora, null), new TareaDeRemisionesSinFacturar(EntregaDelComercio.I5).DebeCorrer(ahora, null)).Should().Be((true, false));

        var sender = Substitute.For<ISender>();
        var servicios = new ServiceCollection().AddSingleton(sender).BuildServiceProvider();
        await reservas.EjecutarAsync(servicios, default);
        await remisiones.EjecutarAsync(servicios, default);

        await sender.Received(1).Send(Arg.Any<ReleaseExpiredReservationsCommand>(), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Any<RaiseUnbilledShipmentAlertsCommand>(), Arg.Any<CancellationToken>());
        typeof(ReleaseExpiredReservationsCommand).GetInterfaces().Should().NotContain(typeof(IngenIA365ERP.Application.Common.Behaviors.IOperacionIdempotente), "un comando de proceso sin ruta no lleva clave");
    }

    // --------------------------------------------------------------------------------------------- remisiones --

    [Fact]
    public async Task La_remision_vieja_con_saldo_levanta_su_alerta_una_sola_vez()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        var vieja = await c.RemisionAsync(2m);
        var facturada = await c.RemisionAsync(1m);
        CicloComercialDePrueba.Exito(await c.ConfirmarAsync(await c.FacturaAsync("FVR", [facturada.PublicId])));
        c.V.Compras.C.Reloj.HoyLocal.Returns(Hoy.AddDays(31));
        var nueva = await c.RemisionAsync(1m);
        var alertar = new RaiseUnbilledShipmentAlertsCommandHandler(c.Db, c.V.K.Lector(), c.V.Alertas, c.V.Compras.C.Reloj);

        var primera = await alertar.Handle(new RaiseUnbilledShipmentAlertsCommand(), default);

        primera.Value.Should().Be(1, "sólo la de hace 31 días con saldo: la facturada no, la de hoy tampoco");
        var alerta = c.V.Alertas.Levantadas.Should().ContainSingle().Subject;
        (alerta.TypeCode, alerta.DedupKey, alerta.EntityPublicId).Should().Be((TiposDeAlerta.RemisionSinFacturar, $"RemisionSinFacturar:{vieja.PublicId}", vieja.PublicId));
        nueva.Should().NotBeNull();

        // Lo que deja el servicio real de alertas: la alerta con su condición. Ya levantada, no se vuelve a levantar.
        c.Db.Alerts.Add(new Alert { TypeCode = alerta.TypeCode, Module = "Inventory", Subject = alerta.Subject, Body = alerta.Body, DedupKey = alerta.DedupKey! });
        await c.Db.SaveChangesAsync();
        (await alertar.Handle(new RaiseUnbilledShipmentAlertsCommand(), default)).Value.Should().Be(0);
        c.V.Alertas.Levantadas.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------------------------------------- cierre --

    private static async Task<CicloComercialDePrueba> ConInicioAsync()
    {
        var c = await CicloComercialDePrueba.CrearAsync();
        c.Db.InventorySetups.Add(new InventorySetup
        {
            StartDate = new DateOnly(2026, 9, 1), LastClosedDate = new DateOnly(2026, 8, 31), StartedAt = DateTime.UtcNow,
            StartedByUserId = CreditoDePrueba.Cajera,
        });
        await c.Db.SaveChangesAsync();
        return c;
    }

    private static CloseInventoryPeriodCommandHandler Cerrar(CicloComercialDePrueba c) =>
        new(c.Db, new RevisionDeCierre(c.Db, c.V.Compras.C.Reloj), new ValorizadoALaFecha(c.Db, c.V.K.Lector()), c.V.K.Cerrojo,
            new EmisorDeMensajes(c.Db, c.V.K.Actor, c.V.Compras.C.Reloj), c.V.K.Actor, c.V.K.Permisos, c.V.Compras.C.Reloj);

    [Fact]
    public async Task El_cierre_con_remisiones_sin_facturar_exige_aceptarlas_con_permiso_y_motivo()
    {
        var c = await ConInicioAsync();
        var pedido = await c.PedidoAsync(5m);
        var remision = await c.RemisionAsync(4m, pedido);
        var directa = await c.RemisionAsync(1m);
        c.V.Compras.C.Reloj.HoyLocal.Returns(new DateOnly(2026, 10, 5));

        var previa = await new GetPeriodCloseCheckQueryHandler(new RevisionDeCierre(c.Db, c.V.Compras.C.Reloj)).Handle(new GetPeriodCloseCheckQuery(2026, 9), default);
        var sinAceptar = await Cerrar(c).Handle(new CloseInventoryPeriodCommand(2026, 9, AcknowledgeWarnings: true), default);
        c.V.K.Permisos.HasPermissionAsync(CloseInventoryPeriodCommandHandler.PermisoDeRemisiones, Arg.Any<CancellationToken>()).Returns(false);
        var sinPermiso = await Cerrar(c).Handle(new CloseInventoryPeriodCommand(2026, 9, true, AcceptUnbilledShipments: true, Reason: "Se facturan en octubre"), default);
        c.V.K.Permisos.HasPermissionAsync(CloseInventoryPeriodCommandHandler.PermisoDeRemisiones, Arg.Any<CancellationToken>()).Returns(true);
        var cerrado = await Cerrar(c).Handle(new CloseInventoryPeriodCommand(2026, 9, true, AcceptUnbilledShipments: true, Reason: "Se facturan en octubre"), default);

        previa.Value.UnbilledShipments.Select(r => (r.DocumentPublicId, r.Value, r.ValueSource)).Should().BeEquivalentTo(
            [(remision.PublicId, 8000m, "Order"), (directa.PublicId, 2000m, "PriceList")], "4 × 2.000 al precio del pedido y 1 × 2.000 de la lista");
        previa.Value.UnbilledTotal.Should().Be(10000m);
        sinAceptar.Error.Code.Should().Be("Inventory.Period.UnbilledShipmentsNotAccepted");
        sinPermiso.Error.Code.Should().Be("Inventory.Period.AcceptUnbilledNotAllowed");
        cerrado.IsSuccess.Should().BeTrue(cerrado.IsFailure ? $"{cerrado.Error.Code}: {cerrado.Error.Message}" : null);
        var periodo = c.Db.InventoryPeriods.AsNoTracking().Single(p => p.Year == 2026 && p.Month == 9);
        (periodo.UnbilledShipmentsAcceptedByUserId, periodo.UnbilledShipmentsAcceptedReason).Should().Be((CreditoDePrueba.Cajera, "Se facturan en octubre"));
        periodo.UnbilledShipmentsJson.Should().Contain(remision.PublicId.ToString());
        var lista = await new ListInventoryPeriodsQueryHandler(c.Db, c.V.Compras.C.Reloj).Handle(new ListInventoryPeriodsQuery(2026), default);
        lista.Value.Periods.Single(p => p.Month == 9).UnbilledShipmentsAccepted.Should().BeEquivalentTo(new { Count = 2, Value = 10000m, Reason = "Se facturan en octubre" },
            o => o.ExcludingMissingMembers());
    }
}
