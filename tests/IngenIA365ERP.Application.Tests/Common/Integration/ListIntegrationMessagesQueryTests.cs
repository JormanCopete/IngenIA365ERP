using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T464 (feature 012, US7; api.md §25.2; FR-014, FR-078; Principio VI): la bandeja. Filtros de §25.2, orden de emisión,
/// <c>countsByStatus</c>, <c>blockedBy</c> calculado, <c>destinationAvailable = false</c> para Cartera, <c>result</c> leído sólo de la
/// entrega (sin tablas <c>ACC_</c>: el contexto de prueba ni siquiera las llena), <c>closure=true</c> con la clausura y su corte, el
/// detalle con contenido, intentos y vecinos, y ningún <c>Id</c> <c>bigint</c> en el JSON.
/// </summary>
public class ListIntegrationMessagesQueryTests
{
    private readonly BandejaDePrueba _b = new();
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly IDestinoDeMensajes[] _destinos = [new DestinoFalso(IntegrationDestinations.Accounting)];

    public ListIntegrationMessagesQueryTests()
    {
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
    }

    private Task<Application.Common.Models.Result<BandejaDeMensajesDto>> Listar(ListIntegrationMessagesQuery q) =>
        new ListIntegrationMessagesQueryHandler(_b.Db, _alcance, _destinos).Handle(q, default);

    [Fact]
    public async Task Sale_en_orden_de_emision_con_los_contadores_por_estado()
    {
        var a = _b.Mensaje(numero: "CO-1");
        var b = _b.Mensaje(numero: "CO-2");
        var c = _b.Mensaje(numero: "CO-3");
        _b.Entrega(c, DeliveryStatus.Rejected);
        _b.Entrega(a, DeliveryStatus.Processed);
        _b.Entrega(b, DeliveryStatus.Pending);

        var r = await Listar(new ListIntegrationMessagesQuery());

        r.Value.Messages.Items.Select(m => m.MessagePublicId).Should().Equal(a.PublicId, b.PublicId, c.PublicId);
        r.Value.CountsByStatus.Should().Be(new ConteoPorEstadoDto(1, 0, 1, 1, 0, 0));
    }

    [Fact]
    public async Task Los_filtros_de_la_bandeja()
    {
        var lote = _b.Lote(estado: BatchStatus.Running, numero: 5);
        var enLote = _b.Mensaje(numero: "CO-1");
        _b.Entrega(enLote, DeliveryStatus.InBatch, lote: lote);
        var rechazadaSinRespuesta = _b.Mensaje(numero: "CO-2", validacion: PrevalidationOutcome.NoResponse);
        _b.Entrega(rechazadaSinRespuesta, DeliveryStatus.Rejected);
        var vieja = _b.Mensaje(numero: "AJ-1", tipo: "AjusteInventarioAprobado", tipoDeDocumento: "AJUS", fecha: BandejaDePrueba.Fecha.AddDays(-30));
        _b.Entrega(vieja, DeliveryStatus.Processed);

        (await Listar(new(Status: DeliveryStatus.Rejected, PrevalidationOutcome: PrevalidationOutcome.NoResponse))).Value.Messages.Items
            .Select(m => m.MessagePublicId).Should().Equal(rechazadaSinRespuesta.PublicId);
        (await Listar(new(Batch: lote.PublicId))).Value.Messages.Items.Should().ContainSingle()
            .Which.Batch.Should().Be(new LoteDelMensajeDto(lote.PublicId, 5));
        (await Listar(new(Type: "AjusteInventarioAprobado"))).Value.Messages.Items.Should().ContainSingle();
        (await Listar(new(DocumentType: "AJUS"))).Value.Messages.Items.Should().ContainSingle();
        (await Listar(new(DocumentNumber: "CO-2"))).Value.Messages.Items.Should().ContainSingle();
        (await Listar(new(Document: enLote.OriginPublicId))).Value.Messages.Items.Should().ContainSingle();
        (await Listar(new(From: BandejaDePrueba.Fecha.AddDays(-1), To: BandejaDePrueba.Fecha))).Value.Messages.Items.Should().HaveCount(2);
        (await Listar(new(Destination: IntegrationDestinations.Lending))).Value.Messages.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task BlockedBy_dice_a_quien_espera_y_Lending_no_esta_disponible()
    {
        var original = _b.Mensaje(numero: "CO-1");
        _b.Entrega(original, DeliveryStatus.Rejected);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", numero: "AN-1", relacionado: original.OriginPublicId);
        _b.Entrega(anulacion);
        _b.Depende(anulacion, original);
        var credito = _b.Mensaje(tipo: "VentaACreditoRegistrada", clave: $"Confirmation:{Guid.NewGuid():N}");
        _b.Entrega(credito, destino: IntegrationDestinations.Lending, modo: DeliveryMode.Always);

        var filas = (await Listar(new())).Value.Messages.Items;

        var fila = filas.Single(m => m.MessagePublicId == anulacion.PublicId);
        fila.BlockedBy.Should().ContainSingle().Which.Should().Be(new BloqueoDelMensajeDto(original.PublicId, "CompraRecibida", DeliveryStatus.Rejected));
        fila.Related!.PublicId.Should().Be(original.OriginPublicId);
        fila.DestinationAvailable.Should().BeTrue();
        filas.Single(m => m.MessagePublicId == credito.PublicId).DestinationAvailable.Should().BeFalse("Cartera no tiene consumidor hasta IC");
        filas.Single(m => m.MessagePublicId == original.PublicId).BlockedBy.Should().BeEmpty();
    }

    [Fact]
    public async Task El_resultado_sale_solo_de_la_entrega_con_su_ruta()
    {
        var comprobante = Guid.NewGuid();
        var e = _b.Entrega(_b.Mensaje(numero: "CO-1"), DeliveryStatus.Processed);
        e.ResultReference = comprobante.ToString("D");
        e.ResultVoucherTypeCode = "EI";
        e.ResultVoucherNumber = "EI-44";
        var lote = _b.Lote(estado: BatchStatus.Running, numero: 3);
        lote.Granularity = Domain.Enums.Inventory.PostingGranularity.Summarized;
        var resumido = _b.Entrega(_b.Mensaje(numero: "AJ-1"), DeliveryStatus.Processed, lote: lote);
        resumido.ResultReference = Guid.NewGuid().ToString("D");
        resumido.ResultVoucherTypeCode = "AC";
        var informativo = _b.Entrega(_b.Mensaje(tipo: "SaldoInicialCargado", numero: "SI-1"), DeliveryStatus.Processed);
        informativo.ResultReference = ReferenciasDeResultado.Informativo;
        await _b.Db.SaveChangesAsync();

        var filas = (await Listar(new())).Value.Messages.Items;

        filas[0].Result.Should().Be(new ResultadoDelMensajeDto("EI", "EI-44", comprobante, $"/contabilidad/comprobantes/{comprobante:D}", null));
        filas[1].Result!.Route.Should().Be($"/contabilidad/inventario/lotes/{lote.PublicId:D}");
        filas[2].Result.Should().Be(new ResultadoDelMensajeDto(null, null, null, null, MotivoSinComprobante.Informational));
    }

    [Fact]
    public async Task Con_clausura_suma_los_relacionados_de_cualquier_fecha_y_devuelve_el_corte()
    {
        var traslado = _b.Mensaje(numero: "TR-1");
        _b.Entrega(traslado, DeliveryStatus.NotApplicable);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", numero: "AN-1", relacionado: traslado.OriginPublicId, fecha: BandejaDePrueba.Fecha.AddDays(45));
        _b.Entrega(anulacion, DeliveryStatus.NotApplicable);
        _b.Depende(anulacion, traslado);
        var suelto = _b.Mensaje(numero: "TR-9", fecha: BandejaDePrueba.Fecha.AddDays(45));
        _b.Entrega(suelto, DeliveryStatus.NotApplicable);

        var sin = await Listar(new(Status: DeliveryStatus.NotApplicable, Destination: IntegrationDestinations.Accounting,
            From: BandejaDePrueba.Fecha, To: BandejaDePrueba.Fecha));
        var con = await Listar(new(Status: DeliveryStatus.NotApplicable, Destination: IntegrationDestinations.Accounting,
            From: BandejaDePrueba.Fecha, To: BandejaDePrueba.Fecha, Closure: true));

        sin.Value.Messages.Items.Should().ContainSingle();
        sin.Value.CutoffMessagePublicId.Should().BeNull();
        con.Value.Messages.Items.Select(m => m.MessagePublicId).Should().Equal(traslado.PublicId, anulacion.PublicId);
        con.Value.CutoffMessagePublicId.Should().Be(anulacion.PublicId);
    }

    [Fact]
    public async Task Fuera_del_alcance_de_bodega_no_se_ve()
    {
        _b.Entrega(_b.Mensaje());
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);

        (await Listar(new())).Value.Messages.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task El_detalle_trae_el_contenido_los_intentos_y_los_vecinos()
    {
        var original = _b.Mensaje(numero: "CO-1");
        _b.Entrega(original, DeliveryStatus.Processed);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", numero: "AN-1", relacionado: original.OriginPublicId, payload: "{\"operation\":\"Anulacion\"}");
        var entrega = _b.Entrega(anulacion, DeliveryStatus.Rejected, intentos: 1);
        _b.Depende(anulacion, original);
        _b.Db.IntegrationDeliveryAttempts.Add(new IntegrationDeliveryAttempt
        {
            DeliveryId = entrega.Id, MessageId = anulacion.Id, AttemptNumber = 1, StartedAt = BandejaDePrueba.Ahora, FinishedAt = BandejaDePrueba.Ahora,
            Outcome = DeliveryAttemptOutcome.Rejected, ErrorCode = "Accounting.InventoryRule.Missing", ActorKind = ActorKind.Process,
            ActorName = "Proceso de integración", Instance = "api-1",
        });
        await _b.Db.SaveChangesAsync();

        var detalle = (await new GetIntegrationMessageQueryHandler(_b.Db, _alcance, _destinos).Handle(new GetIntegrationMessageQuery(anulacion.PublicId), default)).Value;
        var delOriginal = (await new GetIntegrationMessageQueryHandler(_b.Db, _alcance, _destinos).Handle(new GetIntegrationMessageQuery(original.PublicId), default)).Value;

        detalle.Payload.Should().Be("{\"operation\":\"Anulacion\"}");
        detalle.PayloadSha256.Should().Be(anulacion.PayloadSha256);
        detalle.Attempts.Should().ContainSingle().Which.Should().Match<IntentoDelMensajeDto>(i =>
            i.Outcome == DeliveryAttemptOutcome.Rejected && i.Code == "Accounting.InventoryRule.Missing" && i.Actor.Kind == ActorKind.Process);
        detalle.DependsOn.Should().ContainSingle().Which.Should().Be(new VecinoDelMensajeDto(original.PublicId, "CompraRecibida", DeliveryStatus.Processed));
        delOriginal.Dependents.Should().ContainSingle().Which.Should().Be(new VecinoDelMensajeDto(anulacion.PublicId, "DocumentoAnulado", DeliveryStatus.Rejected));
    }

    [Fact]
    public async Task El_detalle_de_un_mensaje_inexistente_es_NotFound()
    {
        var r = await new GetIntegrationMessageQueryHandler(_b.Db, _alcance, _destinos).Handle(new GetIntegrationMessageQuery(Guid.NewGuid()), default);

        r.Error.Code.Should().Be("Integration.Message.NotFound");
    }

    [Fact]
    public async Task Ningun_Id_bigint_sale_en_el_JSON()
    {
        var original = _b.Mensaje();
        _b.Entrega(original, DeliveryStatus.Rejected);
        var anulacion = _b.Mensaje(tipo: "DocumentoAnulado", relacionado: original.OriginPublicId);
        _b.Entrega(anulacion);
        _b.Depende(anulacion, original);

        var bandeja = (await Listar(new(Closure: true))).Value;
        var detalle = (await new GetIntegrationMessageQueryHandler(_b.Db, _alcance, _destinos).Handle(new GetIntegrationMessageQuery(anulacion.PublicId), default)).Value;

        foreach (var json in new[] { JsonSerializer.Serialize(bandeja, JsonSerializerOptions.Web), JsonSerializer.Serialize(detalle, JsonSerializerOptions.Web) })
        {
            using var doc = JsonDocument.Parse(json);
            PropiedadesId(doc.RootElement).Should().BeEmpty("el Id interno es el orden y nunca sale (Principio VI)");
        }
    }

    private static IEnumerable<string> PropiedadesId(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name.Equals("id", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("messageId", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Equals("batchId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("cutoffMessageId", StringComparison.OrdinalIgnoreCase))
                        yield return p.Name;
                    foreach (var h in PropiedadesId(p.Value)) yield return h;
                }

                break;
            case JsonValueKind.Array:
                foreach (var i in e.EnumerateArray())
                foreach (var h in PropiedadesId(i)) yield return h;
                break;
        }
    }
}
