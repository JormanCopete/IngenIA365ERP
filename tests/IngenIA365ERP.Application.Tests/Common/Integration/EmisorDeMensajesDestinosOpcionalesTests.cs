using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// Feature 020 (T019; R12; contracts/mensajes.md §1 de la 020): un tipo con destinos opcionales crea una entrega más por cada destino
/// con participante habilitado y vigente al emitir (<c>Always</c>, <c>Pending</c>); sin participante no deja rastro; la entrega
/// obligatoria no cambia; sin registro (los emisores de las pruebas viejas) no hay opcionales.
/// </summary>
public class EmisorDeMensajesDestinosOpcionalesTests
{
    private static readonly DateTime Ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 12, 5);
    private static readonly Guid Sucursal = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly IRegistroDeIntegracion _registro = Substitute.For<IRegistroDeIntegracion>();

    public EmisorDeMensajesDestinosOpcionalesTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _reloj.HoyLocal.Returns(Hoy);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Ana Cajera", "ana@coop.test", ExecutionChannel.Web, "POST /api/x", "10.0.0.1", null));
    }

    private static SolicitudDeEmision Anulacion() => new(
        new OrigenDeEmision(MessageOriginKind.Document, Guid.NewGuid(), nameof(DocumentClass.Voiding), "AN", "AN-1", Hoy, Sucursal),
        "Confirmation", [new DocumentoAnuladoV1()], new ModoDeEntrega.Heredado(Guid.NewGuid()),
        KindDelOriginal: IntegrationMessageKind.Informational);

    private List<IntegrationMessageDelivery> Entregas(IntegrationMessage mensaje) =>
        _db.IntegrationMessageDeliveries.Local.Where(d => ReferenceEquals(d.Message, mensaje)).ToList();

    [Fact]
    public void El_catalogo_declara_Transporte_opcional_en_los_cinco_mensajes_de_pago_y_anulacion()
    {
        var conTransporte = CatalogoDeMensajes.Todos
            .Where(t => t.DestinosOpcionales.Contains(IntegrationDestinations.Transport))
            .Select(t => t.Type)
            .Order(StringComparer.Ordinal)
            .ToList();

        conTransporte.Should().BeEquivalentTo(["DocumentoAnulado", "PagoAplicado", "PagoReversado", "ReciboAnulado", "ReciboEmitido"]);
    }

    [Fact]
    public async Task Sin_participante_no_deja_rastro()
    {
        _registro.DestinoHabilitadoAsync(IntegrationDestinations.Transport, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(false);

        var mensaje = (await new EmisorDeMensajes(_db, _actor, _reloj, _registro).EmitirAsync(Anulacion())).Single();

        Entregas(mensaje).Should().ContainSingle().Which.Destination.Should().Be(IntegrationDestinations.Accounting);
    }

    [Fact]
    public async Task Con_participante_crea_una_entrega_mas_pendiente_y_siempre()
    {
        _registro.DestinoHabilitadoAsync(IntegrationDestinations.Transport, Hoy, Arg.Any<CancellationToken>()).Returns(true);

        var mensaje = (await new EmisorDeMensajes(_db, _actor, _reloj, _registro).EmitirAsync(Anulacion())).Single();

        var entregas = Entregas(mensaje);
        entregas.Should().HaveCount(2);
        var opcional = entregas.Single(d => d.Destination == IntegrationDestinations.Transport);
        opcional.Mode.Should().Be(DeliveryMode.Always);
        opcional.Status.Should().Be(DeliveryStatus.Pending);
        opcional.NextAttemptAt.Should().Be(Ahora);
        opcional.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task La_entrega_obligatoria_no_cambia()
    {
        _registro.DestinoHabilitadoAsync(IntegrationDestinations.Transport, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        var sinOpcional = new EmisorDeMensajes(TestDbContextFactory.Create(), _actor, _reloj);

        var conRegistro = (await new EmisorDeMensajes(_db, _actor, _reloj, _registro).EmitirAsync(Anulacion())).Single();
        var obligatoria = Entregas(conRegistro).Single(d => d.Destination == IntegrationDestinations.Accounting);

        obligatoria.Mode.Should().Be(DeliveryMode.Always);
        obligatoria.Status.Should().Be(DeliveryStatus.Pending);
        (await sinOpcional.EmitirAsync(Anulacion())).Should().ContainSingle();
    }

    [Fact]
    public async Task El_registro_se_pregunta_una_vez_por_emisor()
    {
        _registro.DestinoHabilitadoAsync(IntegrationDestinations.Transport, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        var emisor = new EmisorDeMensajes(_db, _actor, _reloj, _registro);

        await emisor.EmitirAsync(Anulacion());
        await emisor.EmitirAsync(Anulacion());

        await _registro.Received(1).DestinoHabilitadoAsync(IntegrationDestinations.Transport, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
