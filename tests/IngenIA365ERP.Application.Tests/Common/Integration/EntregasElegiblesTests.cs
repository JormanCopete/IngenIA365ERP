using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T461 (feature 012, US7; contracts/mensajes.md §9, §11; T9, T11, T12, T32): el selector de elegibles. Elegible = <c>Pending</c> con
/// <c>NextAttemptAt</c> vencido y ninguna dependencia al mismo destino en <c>Pending</c>, <c>InBatch</c> o <c>Rejected</c>; lo procesado,
/// lo que no aplica y lo que falló la validación de Cartera no bloquean; orden por <c>Id</c>; destinos independientes; en un lote,
/// pasadas sucesivas sobre las <c>InBatch</c> de su <c>BatchId</c>; el destino sin consumidor (Cartera hasta IC) se salta.
/// </summary>
public class EntregasElegiblesTests
{
    private const string A = IntegrationDestinations.Accounting;
    private const string L = IntegrationDestinations.Lending;

    private readonly BandejaDePrueba _b = new();

    private EntregasElegibles Selector(params string[] destinos) =>
        new(_b.Db, (destinos.Length == 0 ? [A] : destinos).Select(d => (IDestinoDeMensajes)new DestinoFalso(d)));

    private Task<IReadOnlyList<UnidadDeConsumo>> EnLinea(EntregasElegibles selector, string destino = A, int tanda = 100) =>
        selector.EnLineaAsync(destino, BandejaDePrueba.Ahora, tanda, CancellationToken.None);

    [Fact]
    public async Task Una_pendiente_vencida_y_sin_dependencias_es_elegible()
    {
        var m = _b.Mensaje();
        _b.Entrega(m);

        var unidades = await EnLinea(Selector());

        unidades.Should().ContainSingle();
        unidades[0].MessagePublicIds.Should().Equal(m.PublicId);
        unidades[0].OriginPublicId.Should().Be(m.OriginPublicId);
        unidades[0].BatchPublicId.Should().BeNull();
    }

    [Fact]
    public async Task Una_pendiente_con_la_espera_sin_vencer_no_es_elegible()
    {
        _b.Entrega(_b.Mensaje(), proximoIntento: BandejaDePrueba.Ahora.AddSeconds(30));

        (await EnLinea(Selector())).Should().BeEmpty();
    }

    [Theory]
    [InlineData(DeliveryStatus.Pending)]
    [InlineData(DeliveryStatus.InBatch)]
    [InlineData(DeliveryStatus.Rejected)]
    public async Task Una_dependencia_pendiente_en_lote_o_rechazada_en_el_mismo_destino_bloquea(DeliveryStatus estadoDelOriginal)
    {
        var original = _b.Mensaje(numero: "CO-1");
        _b.Entrega(original, estadoDelOriginal, proximoIntento: BandejaDePrueba.Ahora.AddHours(1));
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", numero: "AN-1", relacionado: original.OriginPublicId);
        _b.Entrega(anulacion);
        _b.Depende(anulacion, original);

        (await EnLinea(Selector())).SelectMany(u => u.MessagePublicIds).Should().NotContain(anulacion.PublicId);
    }

    [Theory]
    [InlineData(DeliveryStatus.Processed)]
    [InlineData(DeliveryStatus.NotApplicable)]
    [InlineData(DeliveryStatus.ValidationFailed)]
    public async Task Lo_procesado_lo_que_no_aplica_y_lo_que_fallo_la_validacion_no_bloquean(DeliveryStatus estadoDelOriginal)
    {
        var original = _b.Mensaje();
        _b.Entrega(original, estadoDelOriginal);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", relacionado: original.OriginPublicId);
        _b.Entrega(anulacion);
        _b.Depende(anulacion, original);

        (await EnLinea(Selector())).SelectMany(u => u.MessagePublicIds).Should().Equal(anulacion.PublicId);
    }

    [Fact]
    public async Task Una_dependencia_sin_entrega_a_ese_destino_no_bloquea()
    {
        var original = _b.Mensaje();
        _b.Entrega(original, DeliveryStatus.Pending, destino: L);
        var siguiente = _b.Mensaje(tipo: "DocumentoAnulado");
        _b.Entrega(siguiente);
        _b.Depende(siguiente, original);

        (await EnLinea(Selector())).SelectMany(u => u.MessagePublicIds).Should().Equal(siguiente.PublicId);
    }

    [Fact]
    public async Task Destinos_independientes_una_venta_rechazada_en_Contabilidad_no_detiene_su_credito()
    {
        var venta = Guid.NewGuid();
        var factura = _b.Mensaje(origen: venta, tipo: "VentaFacturada");
        _b.Entrega(factura, DeliveryStatus.Rejected);
        var credito = _b.Mensaje(origen: venta, tipo: "VentaACreditoRegistrada", clave: $"Confirmation:{Guid.NewGuid():N}");
        _b.Entrega(credito, DeliveryStatus.Pending, destino: L, modo: DeliveryMode.Always);
        _b.Depende(credito, factura);

        var unidades = await EnLinea(Selector(A, L), L);

        unidades.SelectMany(u => u.MessagePublicIds).Should().Equal(credito.PublicId);
    }

    [Fact]
    public async Task Los_elegibles_salen_por_Id_ascendente_y_agrupados_en_unidades()
    {
        var venta = Guid.NewGuid();
        var primera = _b.Mensaje(origen: venta, tipo: "VentaFacturada");
        var otra = _b.Mensaje(tipo: "CompraRecibida");
        var segunda = _b.Mensaje(origen: venta, tipo: "CostoDeVentaReconocido");
        _b.Entrega(otra);
        _b.Entrega(segunda);
        _b.Entrega(primera);

        var unidades = await EnLinea(Selector());

        unidades.Should().HaveCount(2);
        unidades[0].OriginPublicId.Should().Be(venta, "la unidad sale en la posición de su primer mensaje");
        unidades[0].MessagePublicIds.Should().Equal(primera.PublicId, segunda.PublicId);
        unidades[1].MessagePublicIds.Should().Equal(otra.PublicId);
    }

    [Fact]
    public async Task Una_tanda_no_parte_una_unidad()
    {
        var venta = Guid.NewGuid();
        var factura = _b.Mensaje(origen: venta, tipo: "VentaFacturada");
        var costo = _b.Mensaje(origen: venta, tipo: "CostoDeVentaReconocido");
        _b.Entrega(factura);
        _b.Entrega(costo);

        var unidades = await EnLinea(Selector(), tanda: 1);

        unidades.Should().ContainSingle().Which.MessagePublicIds.Should().Equal(factura.PublicId, costo.PublicId);
    }

    [Fact]
    public async Task Las_entregas_por_lotes_no_salen_en_linea()
    {
        _b.Entrega(_b.Mensaje(), DeliveryStatus.InBatch, horario: "COMPRA|HoraDiaria|23:00|PorDocumento");

        (await EnLinea(Selector())).Should().BeEmpty();
    }

    [Fact]
    public async Task En_un_lote_las_pasadas_sucesivas_toman_solo_lo_que_ya_tiene_sus_dependencias()
    {
        var lote = _b.Lote(estado: BatchStatus.Running);
        var original = _b.Mensaje(numero: "CO-1");
        var entregaOriginal = _b.Entrega(original, DeliveryStatus.InBatch, lote: lote);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", numero: "AN-1", relacionado: original.OriginPublicId);
        _b.Entrega(anulacion, DeliveryStatus.InBatch, lote: lote);
        _b.Depende(anulacion, original);
        var ajena = _b.Mensaje(numero: "CO-9");
        _b.Entrega(ajena, DeliveryStatus.InBatch, horario: "COMPRA|HoraDiaria|23:00|PorDocumento");

        var selector = Selector();
        var primera = await selector.DelLoteAsync(lote.PublicId, BandejaDePrueba.Ahora, 100, CancellationToken.None);
        primera.SelectMany(u => u.MessagePublicIds).Should().Equal(original.PublicId);
        primera[0].BatchPublicId.Should().Be(lote.PublicId);

        entregaOriginal.Status = DeliveryStatus.Processed;
        await _b.Db.SaveChangesAsync();

        var segunda = await selector.DelLoteAsync(lote.PublicId, BandejaDePrueba.Ahora, 100, CancellationToken.None);
        segunda.SelectMany(u => u.MessagePublicIds).Should().Equal(anulacion.PublicId);
    }

    [Fact]
    public async Task El_destino_Lending_sin_consumidor_se_salta()
    {
        _b.Entrega(_b.Mensaje(tipo: "VentaACreditoRegistrada", clave: $"Confirmation:{Guid.NewGuid():N}"), destino: L, modo: DeliveryMode.Always);

        var selector = Selector(A);

        selector.TieneConsumidor(L).Should().BeFalse();
        selector.DestinosConConsumidor.Should().Equal(A);
        (await EnLinea(selector, L)).Should().BeEmpty();
    }
}
