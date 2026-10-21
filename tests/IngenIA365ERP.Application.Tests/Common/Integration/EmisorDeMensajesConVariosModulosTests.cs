using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T0028 (feature 013; T10, T11, T13; contracts/mensajes.md §1, §6; quickstart §1.5 «Mensajes»): la bandeja con varios emisores. El
/// módulo de origen lo da el tipo en el catálogo agregado (<c>INV</c>, <c>CAR</c>, <c>TES</c>, <c>REC</c>), nunca la solicitud; una
/// solicitud no mezcla módulos; un tipo con varios destinos exige el destino; el usuario de origen se resuelve persona →
/// <c>UsuarioDeOrigen</c> → proceso, y el proceso sólo en los tipos que lo admiten; <c>CadenaDeOrden</c> es la raíz de la cadena; la
/// clase del documento viaja como texto. Los bytes de los contratos INV los sigue fijando <c>ContratosDeMensajesTests</c>, sin cambios.
/// </summary>
public class EmisorDeMensajesConVariosModulosTests
{
    private static readonly DateTime Ahora = new(2026, 12, 16, 14, 20, 31, DateTimeKind.Utc);
    private static readonly DateOnly Fecha = new(2026, 12, 15);
    private static readonly Guid Sucursal = Guid.NewGuid();
    private static readonly Guid UsuarioCentral = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public EmisorDeMensajesConVariosModulosTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Persona());
    }

    private static Actor Persona() => new(
        ActorKind.Person, 7, Guid.NewGuid(), UsuarioCentral, "Jefe de cartera", "jefe@coop.test", ExecutionChannel.Web, "POST /api/lending", "10.0.0.1", null);

    private void ActuaElProceso() =>
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.ProcesoDeIntegracion(Actor.OrigenDeTarea("lending.process")));

    private EmisorDeMensajes Emisor() => new(_db, _actor, _reloj);

    private static OrigenDeEmision Operacion(Guid publicId, string clase = "LendingActivation", string numero = "2026-12-01") =>
        new(MessageOriginKind.Operation, publicId, clase, null, numero, Fecha, Sucursal);

    private static OrigenDeEmision Documento(Guid publicId, string clase, string tipo, string numero) =>
        new(MessageOriginKind.Document, publicId, clase, tipo, numero, Fecha, Sucursal, PersonPublicId: Guid.NewGuid());

    private static readonly ModoDeEntrega EnLinea = new ModoDeEntrega.Sellado(DeliveryMode.Online);

    private IntegrationMessageDelivery EntregaDe(IntegrationMessage mensaje) =>
        _db.IntegrationMessageDeliveries.Local.Single(d => ReferenceEquals(d.Message, mensaje));

    // ------------------------------------------------------------------------------------ el módulo del tipo --

    [Fact]
    public async Task El_modulo_de_origen_sale_del_tipo_en_el_catalogo()
    {
        var car = await Emisor().EmitirAsync(new SolicitudDeEmision(Operacion(Guid.NewGuid()), ClavesDeEvento.Confirmacion,
            [new CarteraActivadaV1()], EnLinea));
        var inv = await Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid(), "PosEquivalentDocument", "DEPOS", "PV01-1"),
            ClavesDeEvento.Confirmacion, [new VentaFacturadaV1()], EnLinea));
        var tes = await Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid(), "TreasuryResult", "TES", "TS-1"),
            ClavesDeEvento.ConfirmacionPor(Guid.NewGuid()), [new ResultadoDelGiroV1()], EnLinea));
        var rec = await Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid(), "DeductionResult", "REC", "RD-1"),
            ClavesDeEvento.ConfirmacionPor(Guid.NewGuid()), [new ResultadoDeDescuentosV1()], EnLinea));

        car.Single().OriginModule.Should().Be(ModulosDeOrigen.Cartera);
        inv.Single().OriginModule.Should().Be(ModulosDeOrigen.Inventario);
        tes.Single().OriginModule.Should().Be(ModulosDeOrigen.Tesoreria);
        rec.Single().OriginModule.Should().Be(ModulosDeOrigen.Recaudo);
        EntregaDe(tes.Single()).Destination.Should().Be(IntegrationDestinations.Lending);
        EntregaDe(rec.Single()).Destination.Should().Be(IntegrationDestinations.Lending);
    }

    [Fact]
    public async Task Una_solicitud_que_mezcla_modulos_se_rechaza_y_no_escribe_nada()
    {
        var emitir = () => Emisor().EmitirAsync(new SolicitudDeEmision(Operacion(Guid.NewGuid()), ClavesDeEvento.Confirmacion,
            [new CarteraActivadaV1(), new VentaFacturadaV1()], EnLinea));

        await emitir.Should().ThrowAsync<ArgumentException>().WithMessage("*mezcla*");
        _db.IntegrationMessages.Local.Should().BeEmpty();
        _db.IntegrationMessageDeliveries.Local.Should().BeEmpty();
    }

    [Fact]
    public void Un_tipo_con_varios_destinos_exige_el_destino_y_uno_que_admita()
    {
        var conVarios = new TipoDeMensaje(typeof(PagoReversadoPorCarteraV1), PagoReversadoPorCarteraV1.Type, 1, ModulosDeOrigen.Cartera,
            new HashSet<string>([IntegrationDestinations.Receipts, "Contributions"], StringComparer.Ordinal),
            IntegrationMessageKind.Business, FormaDeClaveDeEvento.Confirmacion, AdmiteProceso: false);

        var sinDestino = () => EmisorDeMensajes.ResolverDestino(conVarios, null);
        var ajeno = () => EmisorDeMensajes.ResolverDestino(conVarios, IntegrationDestinations.Treasury);

        sinDestino.Should().Throw<ArgumentException>().WithMessage("*la solicitud dice cuál*");
        ajeno.Should().Throw<ArgumentException>().WithMessage("*no admite el destino Treasury*");
        EmisorDeMensajes.ResolverDestino(conVarios, "Contributions").Should().Be("Contributions");

        // Feature 014 (D19): PagoReversadoPorCartera admite Recaudo y Tesorería; el tipo de un solo destino es DescuentosPorPagaduria.
        var deUno = CatalogoDeMensajes.Buscar(typeof(DescuentosPorPagaduriaV1))!;
        EmisorDeMensajes.ResolverDestino(deUno, null).Should().Be(IntegrationDestinations.Receipts);
        EmisorDeMensajes.ResolverDestino(deUno, IntegrationDestinations.Receipts).Should().Be(IntegrationDestinations.Receipts);
        var deDos = CatalogoDeMensajes.Buscar(typeof(PagoReversadoPorCarteraV1))!;
        EmisorDeMensajes.ResolverDestino(deDos, IntegrationDestinations.Treasury).Should().Be(IntegrationDestinations.Treasury);
    }

    [Fact]
    public async Task El_destino_de_la_solicitud_va_a_la_entrega_y_uno_que_el_tipo_no_admite_se_rechaza()
    {
        var reverso = Documento(Guid.NewGuid(), "PaymentReversal", "REV", "REV-2026-000063");

        var mal = () => Emisor().EmitirAsync(new SolicitudDeEmision(reverso, ClavesDeEvento.Confirmacion, [new PagoReversadoPorCarteraV1()],
            EnLinea, Destino: IntegrationDestinations.Inventory));
        await mal.Should().ThrowAsync<ArgumentException>();
        _db.IntegrationMessages.Local.Should().BeEmpty();

        var emitido = (await Emisor().EmitirAsync(new SolicitudDeEmision(reverso, ClavesDeEvento.Confirmacion, [new PagoReversadoPorCarteraV1()],
            EnLinea, Destino: IntegrationDestinations.Receipts))).Single();

        var entrega = EntregaDe(emitido);
        entrega.Destination.Should().Be(IntegrationDestinations.Receipts);
        entrega.Mode.Should().Be(DeliveryMode.Always, "lo que no va a Contabilidad se entrega siempre");
        entrega.Status.Should().Be(DeliveryStatus.Pending);
    }

    // -------------------------------------------------------------------------------------- usuario de origen --

    [Fact]
    public async Task La_persona_que_actua_es_el_usuario_de_origen()
    {
        var emitido = (await Emisor().EmitirAsync(new SolicitudDeEmision(Operacion(Guid.NewGuid()), ClavesDeEvento.Confirmacion,
            [new CarteraActivadaV1()], EnLinea, UsuarioDeOrigen: new UserRefV1 { CentralUserId = Guid.NewGuid(), Name = "Otro" }))).Single();

        emitido.OriginActorKind.Should().Be(ActorKind.Person);
        emitido.OriginUserCentralId.Should().Be(UsuarioCentral, "la persona que actúa gana al usuario de origen de la solicitud");
        emitido.OriginUserName.Should().Be("Jefe de cartera");
    }

    [Fact]
    public async Task Sin_persona_y_con_usuario_de_origen_el_mensaje_va_a_nombre_de_ese_usuario()
    {
        ActuaElProceso();
        var cajero = Guid.NewGuid();
        var desembolso = Guid.NewGuid();

        var emitido = (await Emisor().EmitirAsync(new SolicitudDeEmision(
            Documento(Guid.NewGuid(), "DisbursementReversal", "DRV", "DRV-2026-000003"), ClavesDeEvento.Confirmacion,
            [new DesembolsoReversadoV1()], EnLinea,
            Relacionado: new DocumentoRelacionado(desembolso, "LoanDisbursement", "DES-2026-000120"),
            UsuarioDeOrigen: new UserRefV1 { CentralUserId = cajero, Name = "Tesorera de ensayo" }))).Single();

        emitido.OriginActorKind.Should().Be(ActorKind.Person, "DesembolsoReversado no admite proceso, pero lleva el usuario de Tesorería");
        emitido.OriginUserCentralId.Should().Be(cajero);
        emitido.OriginUserName.Should().Be("Tesorera de ensayo");
    }

    [Fact]
    public async Task Sin_persona_ni_usuario_de_origen_un_tipo_que_admite_proceso_lo_sella_como_proceso()
    {
        ActuaElProceso();
        var cierre = Guid.NewGuid();

        var emitido = (await Emisor().EmitirAsync(new SolicitudDeEmision(Operacion(cierre, "MonthlyClose", "2026-12"),
            ClavesDeEvento.Cierre(1), [new CierreDeCarteraRealizadoV1 { Year = 2026, Month = 12, CloseVersion = 1 }], EnLinea))).Single();

        emitido.OriginActorKind.Should().Be(ActorKind.Process);
        emitido.OriginUserCentralId.Should().BeNull();
        emitido.OriginUserName.Should().Be(Actor.NombreDelProceso).And.Be("Proceso de integración");
    }

    [Fact]
    public async Task Un_usuario_de_origen_sin_identidad_central_cuenta_como_si_no_estuviera()
    {
        ActuaElProceso();

        var emitido = (await Emisor().EmitirAsync(new SolicitudDeEmision(
            Documento(Guid.NewGuid(), "InboundMessage", "RCB", "PV01-2207"), ClavesDeEvento.Confirmacion, [new ObligacionCreadaV1()], EnLinea,
            UsuarioDeOrigen: new UserRefV1 { CentralUserId = null, Name = "Proceso de integración" }))).Single();

        emitido.OriginActorKind.Should().Be(ActorKind.Process, "el mensaje consumido lo originó el proceso y ObligacionCreada lo admite");
        emitido.OriginUserCentralId.Should().BeNull();
    }

    [Fact]
    public async Task Sin_persona_ni_usuario_de_origen_un_tipo_de_Inventario_lanza_y_no_escribe_nada()
    {
        ActuaElProceso();

        var emitir = () => Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid(), "PosEquivalentDocument", "DEPOS", "PV01-9"),
            ClavesDeEvento.Confirmacion, [new VentaFacturadaV1()], EnLinea));

        await emitir.Should().ThrowAsync<InvalidOperationException>().WithMessage("*VentaFacturada*proceso*");
        _db.IntegrationMessages.Local.Should().BeEmpty();
        _db.IntegrationMessageDeliveries.Local.Should().BeEmpty();
        _db.IntegrationMessageDependencies.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_tipo_de_Cartera_que_no_admite_proceso_tambien_lanza_sin_persona()
    {
        ActuaElProceso();

        var emitir = () => Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid(), "PaymentApplication", "PAG", "PAG-1"),
            ClavesDeEvento.Confirmacion, [new PagoAplicadoV1()], EnLinea));

        await emitir.Should().ThrowAsync<InvalidOperationException>("PagoAplicado nunca lo origina el proceso (T11, C14)");
        _db.IntegrationMessages.Local.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------ cadena y clase como texto --

    [Fact]
    public async Task La_cadena_de_orden_es_la_raiz_y_ordena_las_respuestas_de_una_venta()
    {
        var venta = Guid.NewGuid();
        var primera = (await Emisor().EmitirAsync(new SolicitudDeEmision(
            Documento(Guid.NewGuid(), "InboundMessage", "RCB", "PV01-2207"), ClavesDeEvento.ConfirmacionPor(Guid.NewGuid()),
            [new ObligacionRechazadaV1()], EnLinea, CadenaDeOrden: venta))).Single();
        await _db.SaveChangesAsync(); // la primera respuesta ya está en la base cuando se consume el reproceso que da la segunda
        var segunda = (await Emisor().EmitirAsync(new SolicitudDeEmision(
            Documento(Guid.NewGuid(), "InboundMessage", "RCB", "PV01-2207"), ClavesDeEvento.Confirmacion,
            [new ObligacionCreadaV1()], EnLinea, CadenaDeOrden: venta))).Single();

        primera.ChainRootPublicId.Should().Be(venta);
        segunda.ChainRootPublicId.Should().Be(venta);
        _db.IntegrationMessageDependencies.Local.Where(d => ReferenceEquals(d.Message, segunda)).Select(d => d.DependsOnMessage)
            .Should().Contain(primera, "las respuestas de una venta llegan a Comercial en orden aunque su origen (el recibo) sea otro");
        EntregaDe(segunda).Destination.Should().Be(IntegrationDestinations.Inventory);
    }

    [Fact]
    public async Task La_clase_del_documento_viaja_como_texto()
    {
        var orden = Guid.NewGuid();

        var emitido = (await Emisor().EmitirAsync(new SolicitudDeEmision(
            Documento(orden, "DisbursementOrder", "OG", "OG-2026-000088"), ClavesDeEvento.ConfirmacionPor(orden),
            [new AnulacionDeOrdenDeGiroV1 { OrderPublicId = orden, OrderNumber = "OG-2026-000088", Reason = "Otra cuenta" }], EnLinea,
            Relacionado: new DocumentoRelacionado(orden, "DisbursementOrder", "OG-2026-000088")))).Single();

        emitido.OriginDocumentClass.Should().Be("DisbursementOrder");
        emitido.RelatedDocumentClass.Should().Be("DisbursementOrder");
        emitido.OriginModule.Should().Be(ModulosDeOrigen.Cartera);
        EntregaDe(emitido).Destination.Should().Be(IntegrationDestinations.Treasury);
    }
}
