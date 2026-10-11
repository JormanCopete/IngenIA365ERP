using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US3, T0426 (T20; mensajes.md §18; api.md §20): la tesorería de prueba muestra las órdenes y anulaciones recibidas y, a pedido,
/// emite <c>ResultadoDelGiro</c> como <c>TES</c> con la persona que prueba como usuario de origen, la clave <c>Confirmation:{orden:N}</c> y el
/// mensaje de la orden; sin el doble registrado (la guarda <c>DoblesDePruebaDeCartera</c>) no existe. El consumo sin estado y la guarda de
/// producción los prueba la fase 3.
/// </summary>
public sealed class TesoreriaDePruebaTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private DisbursementResultDto _desembolso = null!;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        var s = await _d.AprobadaAsync();
        _desembolso = await _d.DesembolsadoAsync(_d.Entrada(s));
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Lista_las_ordenes_y_las_anulaciones_recibidas()
    {
        await _d.AnularOrden().Handle(new VoidDisbursementOrderCommand(_desembolso.Order!.PublicId, "otra cuenta") { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();

        var r = await _d.OrdenesDePrueba().Handle(new ListTestTreasuryOrdersQuery(), default);

        var orden = r.Value.Should().ContainSingle().Subject;
        (orden.OrderPublicId, orden.VoidRequested, orden.NetAmount).Should().Be((_desembolso.Order.PublicId, true, 3_000_000m));
        orden.OrderMessagePublicId.Should().NotBeNull();
    }

    [Fact]
    public async Task Emite_el_resultado_como_TES_con_la_persona_que_prueba_y_el_mensaje_de_la_orden()
    {
        var r = await _d.EmitirResultado().Handle(new EmitTestDisbursementResultCommand(_desembolso.Order!.PublicId, "Rejected", "TRF-1", DesembolsoDePrueba.Hoy)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        var m = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.Type == ResultadoDelGiroV1.Type);
        (m.OriginModule, m.OriginEventKey, m.RelatedPublicId).Should()
            .Be((ModulosDeOrigen.Tesoreria, ClavesDeEvento.ConfirmacionPor(_desembolso.Order.PublicId), (Guid?)_desembolso.Order.PublicId));
        (m.OriginActorKind, m.OriginUserCentralId).Should().Be((ActorKind.Person, _d.Cajera.CentralUserId));
        var giro = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.Type == OrdenDeGiroEmitidaV1.Type);
        var c = JsonDocument.Parse(m.PayloadJson).RootElement;
        c.GetProperty("orderMessagePublicId").GetGuid().Should().Be(giro.PublicId);
        (c.GetProperty("outcome").GetString(), c.GetProperty("amount").GetDecimal()).Should().Be(("Rejected", 3_000_000m));
        (await _d.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(e => e.MessageId == m.Id)).Destination.Should().Be(IntegrationDestinations.Lending);
    }

    [Fact]
    public async Task Sin_el_doble_registrado_no_existe()
    {
        (await _d.OrdenesDePrueba(dobleRegistrado: false).Handle(new ListTestTreasuryOrdersQuery(), default)).Error.Code.Should().Be("Generic.NotFound");
        (await _d.EmitirResultado(dobleRegistrado: false).Handle(new EmitTestDisbursementResultCommand(_desembolso.Order!.PublicId, "Disbursed", "T",
            DesembolsoDePrueba.Hoy) { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public void Sigue_registrandose_solo_por_la_guarda_de_los_dobles()
    {
        var fuera = new ServiceCollection();
        DoblesDePruebaDeCarteraEnDi.Registrar(fuera, pedidos: true, esProduccion: false).Should().BeTrue();
        fuera.Should().ContainSingle(x => x.ServiceType == typeof(IDestinoDeMensajes) && x.ImplementationType == typeof(TesoreriaDePrueba));

        DoblesDePruebaDeCarteraEnDi.Registrar(new ServiceCollection(), pedidos: false, esProduccion: false).Should().BeFalse();
        var produccion = () => DoblesDePruebaDeCarteraEnDi.Registrar(new ServiceCollection(), pedidos: true, esProduccion: true);
        produccion.Should().Throw<InvalidOperationException>();
    }
}
