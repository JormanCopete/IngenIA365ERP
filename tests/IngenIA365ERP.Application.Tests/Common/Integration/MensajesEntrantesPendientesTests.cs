using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T0029, lectura sin consumir (feature 013; T19; contracts/mensajes.md §15.4): <c>IMensajesEntrantes.PendientesAsync(destino, tipos,
/// hastaFechaDeOperacion, página)</c> lista lo que todavía espera en el destino —para conciliar las ventas acumuladas antes de activar
/// Cartera (FR-143)— sin consumir, sin crear intentos y sin cambiar ninguna entrega; y todo <c>MensajeEntrante</c> trae la clase del
/// documento de origen y del relacionado como texto, que es lo único que ve un consumidor que no es Inventario.
/// </summary>
public class MensajesEntrantesPendientesTests
{
    private static readonly string[] Ventas = [VentaACreditoRegistradaV1.Type, AjusteDeVentaACreditoV1.Type];

    private readonly BandejaDePrueba _b = new();

    private Domain.Entities.Integration.Transactions.IntegrationMessage Venta(DateOnly fecha, string tipo = "VentaACreditoRegistrada", Guid? relacionado = null) =>
        _b.Mensaje(tipo: tipo, clave: $"Confirmation:{Guid.NewGuid():N}", fecha: fecha, numero: "PV01-1", relacionado: relacionado,
            payload: Encoding.UTF8.GetString(OpcionesDeMensajes.Serializar(new VentaACreditoRegistradaV1 { ThirdPartyKind = "Associate" })));

    [Fact]
    public async Task Lista_lo_que_espera_hasta_la_fecha_en_orden_sin_consumir_ni_intentar()
    {
        var corte = BandejaDePrueba.Fecha;
        var primera = Venta(corte.AddDays(-3));
        _b.Entrega(primera, DeliveryStatus.Pending, IntegrationDestinations.Lending, modo: DeliveryMode.Always);
        var ajuste = Venta(corte.AddDays(-1), AjusteDeVentaACreditoV1.Type, relacionado: primera.OriginPublicId);
        _b.Entrega(ajuste, DeliveryStatus.Rejected, IntegrationDestinations.Lending, modo: DeliveryMode.Always);
        var yaProcesada = Venta(corte.AddDays(-2));
        _b.Entrega(yaProcesada, DeliveryStatus.Processed, IntegrationDestinations.Lending, modo: DeliveryMode.Always);
        var despuesDelCorte = Venta(corte.AddDays(1));
        _b.Entrega(despuesDelCorte, DeliveryStatus.Pending, IntegrationDestinations.Lending, modo: DeliveryMode.Always);
        var aContabilidad = _b.Mensaje(fecha: corte.AddDays(-5));
        _b.Entrega(aContabilidad, DeliveryStatus.Pending);
        var antes = await _b.Db.IntegrationMessageDeliveries.AsNoTracking().Select(d => new { d.Id, d.Status, d.Attempts, d.NextAttemptAt }).ToListAsync();

        var r = await new MensajesEntrantes(_b.Db).PendientesAsync(IntegrationDestinations.Lending, Ventas, corte, new PageRequest(1, 50), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Select(m => m.MessageId).Should().Equal(primera.PublicId, ajuste.PublicId);
        r.Value.TotalCount.Should().Be(2);
        r.Value.Items[0].Payload.Should().BeOfType<VentaACreditoRegistradaV1>();
        r.Value.Items[1].Entrega.Status.Should().Be(DeliveryStatus.Rejected);

        _b.Olvidar();
        (await _b.Db.IntegrationDeliveryAttempts.CountAsync()).Should().Be(0, "listar no es consumir");
        (await _b.Db.IntegrationMessageDeliveries.AsNoTracking().Select(d => new { d.Id, d.Status, d.Attempts, d.NextAttemptAt }).ToListAsync())
            .Should().BeEquivalentTo(antes);
    }

    [Fact]
    public async Task Pagina_en_orden_de_emision()
    {
        var corte = BandejaDePrueba.Fecha;
        var mensajes = Enumerable.Range(0, 5).Select(i => Venta(corte.AddDays(-i))).ToList();
        foreach (var m in mensajes) _b.Entrega(m, DeliveryStatus.Pending, IntegrationDestinations.Lending, modo: DeliveryMode.Always);

        var segunda = await new MensajesEntrantes(_b.Db).PendientesAsync(IntegrationDestinations.Lending, Ventas, corte, new PageRequest(2, 2), default);

        segunda.Value.TotalCount.Should().Be(5);
        segunda.Value.Items.Select(m => m.MessageId).Should().Equal(mensajes[2].PublicId, mensajes[3].PublicId);
    }

    [Fact]
    public async Task Un_contenido_alterado_tampoco_se_lista()
    {
        var m = Venta(BandejaDePrueba.Fecha);
        _b.Entrega(m, DeliveryStatus.Pending, IntegrationDestinations.Lending, modo: DeliveryMode.Always);
        m.GetType().GetProperty(nameof(m.PayloadJson))!.SetValue(m, "{\"thirdPartyKind\":\"Customer\"}");
        await _b.Db.SaveChangesAsync();

        var r = await new MensajesEntrantes(_b.Db).PendientesAsync(IntegrationDestinations.Lending, Ventas, BandejaDePrueba.Fecha, new PageRequest(), default);

        r.Error.Code.Should().Be("Integration.Message.PayloadAltered");
    }

    [Fact]
    public async Task La_clase_del_origen_y_del_relacionado_llegan_como_texto()
    {
        var orden = Guid.NewGuid();
        var resultado = _b.Mensaje(tipo: ResultadoDelGiroV1.Type, clave: $"Confirmation:{orden:N}", relacionado: orden,
            payload: Encoding.UTF8.GetString(OpcionesDeMensajes.Serializar(new ResultadoDelGiroV1 { OrderPublicId = orden, Outcome = "Disbursed" })));
        Clases(resultado, "TreasuryResult", "DisbursementOrder");
        _b.Entrega(resultado, DeliveryStatus.Pending, IntegrationDestinations.Lending, modo: DeliveryMode.Always);

        var leidos = await new MensajesEntrantes(_b.Db).LeerAsync([resultado.PublicId], IntegrationDestinations.Lending, default);

        var leido = leidos.Value.Single();
        leido.OriginDocumentClassText.Should().Be("TreasuryResult");
        leido.RelatedDocumentClassText.Should().Be("DisbursementOrder");
        leido.Envelope.Origin.DocumentClass.Should().BeNull("el sobre tipa la clase con el enum de Inventario: fuera de Inventario llega nula");
        leido.Payload.Should().BeOfType<ResultadoDelGiroV1>().Which.Outcome.Should().Be("Disbursed");
    }

    private void Clases(Domain.Entities.Integration.Transactions.IntegrationMessage mensaje, string origen, string relacionado)
    {
        mensaje.GetType().GetProperty(nameof(mensaje.OriginDocumentClass))!.SetValue(mensaje, origen);
        mensaje.GetType().GetProperty(nameof(mensaje.RelatedDocumentClass))!.SetValue(mensaje, relacionado);
        _b.Db.SaveChanges();
    }
}
