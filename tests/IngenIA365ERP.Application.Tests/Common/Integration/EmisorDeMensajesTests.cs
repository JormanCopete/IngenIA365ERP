using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T013 (feature 012; decisiones-transversales T7, T9; FR-071; contracts/mensajes.md §9–§11): el emisor agrega mensaje,
/// entregas y dependencias a la unidad de trabajo <b>sin guardar</b>; cada entrega nace con el estado de §11; un
/// relacionado copia el modo de su original sin leer el parámetro; las aristas van al último mensaje de cada cadena y
/// siempre hacia atrás; la clave de evento es una de §10.1; el hash es el de los bytes guardados; el usuario de origen
/// es la persona de <see cref="IActorActual"/>; y una doble emisión no llega a la base. InMemory con reloj y actor
/// falsos. El índice único en los dos motores lo recorre la e2e de la bandeja cuando exista la migración (T186).
/// </summary>
public class EmisorDeMensajesTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 1, 12, 9, 418, DateTimeKind.Utc);
    private static readonly DateOnly Fecha = new(2026, 11, 14);
    private static readonly Guid Sucursal = Guid.NewGuid();
    private static readonly Guid UsuarioCentral = Guid.NewGuid();
    private const string Horario = "DEPOS|CierreDeTurno||PerDocument";

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public EmisorDeMensajesTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Persona());
    }

    private static Actor Persona() => new(
        ActorKind.Person, 7, Guid.NewGuid(), UsuarioCentral, "Ana Cajera", "ana@coop.test", ExecutionChannel.Pos, "POST /api/inventory/sales", "10.0.0.1", null);

    private EmisorDeMensajes Emisor() => new(_db, _actor, _reloj);

    private static OrigenDeEmision Documento(Guid publicId, string clase = nameof(DocumentClass.PosEquivalentDocument), string numero = "PV01-1532") =>
        new(MessageOriginKind.Document, publicId, clase, "DEPOS", numero, Fecha, Sucursal, WarehouseCode: "B01PV", PersonPublicId: Guid.NewGuid());

    private static SolicitudDeEmision Venta(Guid venta, ModoDeEntrega modo) => new(
        Documento(venta), ClavesDeEvento.Confirmacion, [new VentaFacturadaV1(), new CostoDeVentaReconocidoV1()], modo,
        ValidacionPrevia: PrevalidationOutcome.NotApplicable);

    private static SolicitudDeEmision Nota(Guid nota, Guid venta, ModoDeEntrega modo) => new(
        Documento(nota, nameof(DocumentClass.CreditNote), "NC-7"), ClavesDeEvento.Confirmacion,
        [new NotaCreditoEmitidaV1(), new DevolucionRegistradaV1 { Operation = "DevolucionDeCliente" }], modo,
        Relacionado: new DocumentoRelacionado(venta, nameof(DocumentClass.PosEquivalentDocument), "PV01-1532"));

    private IntegrationMessageDelivery EntregaDe(IntegrationMessage mensaje, string destino = IntegrationDestinations.Accounting) =>
        _db.IntegrationMessageDeliveries.Local.Single(d => ReferenceEquals(d.Message, mensaje) && d.Destination == destino);

    private List<IntegrationMessage> DependenciasDe(IntegrationMessage mensaje) =>
        _db.IntegrationMessageDependencies.Local.Where(d => ReferenceEquals(d.Message, mensaje)).Select(d => d.DependsOnMessage!).ToList();

    // ------------------------------------------------------------------------------------- sin guardar --

    [Fact]
    public async Task Agrega_mensaje_entrega_y_dependencias_sin_llamar_a_SaveChanges()
    {
        var emitidos = await Emisor().EmitirAsync(Venta(Guid.NewGuid(), new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        emitidos.Should().HaveCount(2);
        _db.ChangeTracker.Entries<IntegrationMessage>().Should().OnlyContain(e => e.State == EntityState.Added).And.HaveCount(2);
        _db.ChangeTracker.Entries<IntegrationMessageDelivery>().Should().OnlyContain(e => e.State == EntityState.Added).And.HaveCount(2);
        (await _db.IntegrationMessages.CountAsync()).Should().Be(0, "el emisor no guarda: guarda el SaveChanges del documento");
        (await _db.IntegrationMessageDeliveries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_mensaje_lleva_las_columnas_del_sobre()
    {
        var venta = Guid.NewGuid();
        var origen = Documento(venta) with { CostCenterPublicId = Guid.NewGuid(), FiscalUniqueCode = "CUFE-1" };

        var mensaje = (await Emisor().EmitirAsync(new SolicitudDeEmision(origen, "Confirmation", [new VentaFacturadaV1()],
            new ModoDeEntrega.Sellado(DeliveryMode.Online), ValidacionPrevia: PrevalidationOutcome.Postable))).Single();

        mensaje.Type.Should().Be("VentaFacturada");
        mensaje.Version.Should().Be(1);
        mensaje.Kind.Should().Be(IntegrationMessageKind.Business);
        mensaje.OriginModule.Should().Be("INV");
        mensaje.OriginKind.Should().Be(MessageOriginKind.Document);
        mensaje.OriginPublicId.Should().Be(venta);
        mensaje.OriginDocumentClass.Should().Be("PosEquivalentDocument");
        mensaje.OriginDocumentTypeCode.Should().Be("DEPOS");
        mensaje.OriginNumber.Should().Be("PV01-1532");
        mensaje.FiscalUniqueCode.Should().Be("CUFE-1");
        mensaje.ChainRootPublicId.Should().Be(venta, "un original es la raíz de su cadena");
        mensaje.OperationDate.Should().Be(Fecha);
        mensaje.BranchPublicId.Should().Be(Sucursal);
        mensaje.CostCenterPublicId.Should().Be(origen.CostCenterPublicId);
        mensaje.WarehouseCode.Should().Be("B01PV");
        mensaje.PersonPublicId.Should().Be(origen.PersonPublicId);
        mensaje.Currency.Should().Be("COP");
        mensaje.ExchangeRate.Should().Be(1m);
        mensaje.PrevalidationOutcome.Should().Be(PrevalidationOutcome.Postable);
        mensaje.EmittedAt.Should().Be(Ahora);
        mensaje.RelatedPublicId.Should().BeNull();
    }

    // ------------------------------------------------------------------------------ estado inicial §11 --

    [Fact]
    public async Task En_linea_nace_Pending_para_entregar_ya()
    {
        var mensajes = await Emisor().EmitirAsync(Venta(Guid.NewGuid(), new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        foreach (var mensaje in mensajes)
        {
            var entrega = EntregaDe(mensaje);
            entrega.Mode.Should().Be(DeliveryMode.Online);
            entrega.Status.Should().Be(DeliveryStatus.Pending);
            entrega.NextAttemptAt.Should().Be(Ahora);
            entrega.ScheduleKey.Should().BeNull();
            entrega.BatchScopeKey.Should().BeNull();
            entrega.Attempts.Should().Be(0);
        }
    }

    [Fact]
    public async Task Por_lotes_nace_InBatch_con_sus_claves_y_sin_lote()
    {
        var sesion = ClavesDeLote.SesionDeCaja(Guid.NewGuid());

        var mensajes = await Emisor().EmitirAsync(Venta(Guid.NewGuid(), new ModoDeEntrega.Sellado(DeliveryMode.Batch, Horario, sesion)));

        foreach (var mensaje in mensajes)
        {
            var entrega = EntregaDe(mensaje);
            entrega.Mode.Should().Be(DeliveryMode.Batch);
            entrega.Status.Should().Be(DeliveryStatus.InBatch);
            entrega.ScheduleKey.Should().Be(Horario);
            entrega.BatchScopeKey.Should().Be(sesion);
            entrega.BatchId.Should().BeNull("el lote la toma después");
            entrega.NextAttemptAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Por_lotes_sin_ScheduleKey_es_un_defecto()
    {
        var emitir = () => Emisor().EmitirAsync(Venta(Guid.NewGuid(), new ModoDeEntrega.Sellado(DeliveryMode.Batch)));

        await emitir.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Lo_que_no_pasa_nace_NotApplicable_y_se_conserva()
    {
        var mensajes = await Emisor().EmitirAsync(Venta(Guid.NewGuid(), new ModoDeEntrega.Sellado(DeliveryMode.NotPosted)));

        mensajes.Select(m => EntregaDe(m)).Should().OnlyContain(e => e.Mode == DeliveryMode.NotPosted && e.Status == DeliveryStatus.NotApplicable);
    }

    [Fact]
    public async Task Un_informativo_se_entrega_siempre_aunque_el_documento_no_pase()
    {
        var saldo = new SolicitudDeEmision(Documento(Guid.NewGuid(), nameof(DocumentClass.OpeningBalance), "SI-1"), "Confirmation",
            [new SaldoInicialCargadoV1 { CutoffDate = Fecha }], new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));

        var mensaje = (await Emisor().EmitirAsync(saldo)).Single();

        mensaje.Kind.Should().Be(IntegrationMessageKind.Informational);
        var entrega = EntregaDe(mensaje);
        entrega.Mode.Should().Be(DeliveryMode.Always);
        entrega.Status.Should().Be(DeliveryStatus.Pending);
    }

    [Fact]
    public async Task Lo_de_Cartera_nace_Always_Pending_en_su_propio_destino()
    {
        var pago = Guid.NewGuid();
        var credito = new SolicitudDeEmision(Documento(Guid.NewGuid()), ClavesDeEvento.ConfirmacionPor(pago),
            [new VentaACreditoRegistradaV1()], new ModoDeEntrega.Sellado(DeliveryMode.Batch, Horario));

        var mensaje = (await Emisor().EmitirAsync(credito)).Single();

        mensaje.OriginEventKey.Should().Be($"Confirmation:{pago:N}");
        _db.IntegrationMessageDeliveries.Local.Should().ContainSingle();
        var entrega = EntregaDe(mensaje, IntegrationDestinations.Lending);
        entrega.Mode.Should().Be(DeliveryMode.Always);
        entrega.Status.Should().Be(DeliveryStatus.Pending);
        entrega.ScheduleKey.Should().BeNull("el modo de paso contable no alcanza a Cartera");
    }

    [Fact]
    public async Task Una_anulacion_hereda_el_Kind_de_su_original()
    {
        var saldo = Guid.NewGuid();
        var anulacion = new SolicitudDeEmision(Documento(Guid.NewGuid(), nameof(DocumentClass.Voiding), "AN-1"), "Confirmation",
            [new DocumentoAnuladoV1()], new ModoDeEntrega.Heredado(saldo),
            Relacionado: new DocumentoRelacionado(saldo, nameof(DocumentClass.OpeningBalance), "SI-1"),
            KindDelOriginal: IntegrationMessageKind.Informational);

        var mensaje = (await Emisor().EmitirAsync(anulacion)).Single();

        mensaje.Kind.Should().Be(IntegrationMessageKind.Informational);
        EntregaDe(mensaje).Mode.Should().Be(DeliveryMode.Always);

        var sinKind = () => Emisor().EmitirAsync(anulacion with { KindDelOriginal = null, Origen = Documento(Guid.NewGuid(), "Voiding", "AN-2") });
        await sinKind.Should().ThrowAsync<ArgumentException>();
    }

    // ------------------------------------------------------------------------------- herencia del modo --

    [Fact]
    public async Task Un_relacionado_copia_Mode_y_ScheduleKey_de_la_entrega_de_su_original()
    {
        var venta = Guid.NewGuid();
        await Emisor().EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Batch, Horario, ClavesDeLote.SesionDeCaja(Guid.NewGuid()))));
        await _db.SaveChangesAsync();
        _db.DescartarCambios();

        var otraSesion = ClavesDeLote.SesionDeCaja(Guid.NewGuid());
        var nota = await Emisor().EmitirAsync(Nota(Guid.NewGuid(), venta, new ModoDeEntrega.Heredado(venta, otraSesion)));

        foreach (var mensaje in nota)
        {
            var entrega = EntregaDe(mensaje);
            entrega.Mode.Should().Be(DeliveryMode.Batch);
            entrega.Status.Should().Be(DeliveryStatus.InBatch);
            entrega.ScheduleKey.Should().Be(Horario, "copia el horario sellado del original");
            entrega.BatchScopeKey.Should().Be(otraSesion, "el ámbito del lote es el de la sesión donde se hizo la nota");
            mensaje.ChainRootPublicId.Should().Be(venta);
            mensaje.RelatedPublicId.Should().Be(venta);
        }
    }

    [Fact]
    public async Task Un_relacionado_no_lee_el_parametro_del_modo()
    {
        typeof(EmisorDeMensajes).GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Should().NotContain(typeof(ILectorDeParametros), "el modo lo sella el documento o se hereda del original (FR-075, FR-079)");

        var venta = Guid.NewGuid();
        await Emisor().EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.NotPosted)));
        await _db.SaveChangesAsync();

        var nota = await Emisor().EmitirAsync(Nota(Guid.NewGuid(), venta, new ModoDeEntrega.Heredado(venta)));

        nota.Select(m => EntregaDe(m)).Should().OnlyContain(e => e.Mode == DeliveryMode.NotPosted && e.Status == DeliveryStatus.NotApplicable);
    }

    [Fact]
    public async Task Un_derivado_de_un_derivado_conserva_la_raiz_de_la_cadena()
    {
        var venta = Guid.NewGuid();
        var nota = Guid.NewGuid();
        var emisor = Emisor();
        await emisor.EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Online)));
        await emisor.EmitirAsync(Nota(nota, venta, new ModoDeEntrega.Heredado(venta)));
        await _db.SaveChangesAsync();

        var anulacionDeLaNota = new SolicitudDeEmision(Documento(Guid.NewGuid(), nameof(DocumentClass.Voiding), "AN-9"), "Confirmation",
            [new DocumentoAnuladoV1()], new ModoDeEntrega.Heredado(nota),
            Relacionado: new DocumentoRelacionado(nota, nameof(DocumentClass.CreditNote), "NC-7"), KindDelOriginal: IntegrationMessageKind.Business);

        var mensaje = (await Emisor().EmitirAsync(anulacionDeLaNota)).Single();

        mensaje.ChainRootPublicId.Should().Be(venta);
        EntregaDe(mensaje).Mode.Should().Be(DeliveryMode.Online);
    }

    [Fact]
    public async Task Heredar_de_un_original_sin_entrega_contable_es_un_defecto()
    {
        var emitir = () => Emisor().EmitirAsync(Nota(Guid.NewGuid(), Guid.NewGuid(), new ModoDeEntrega.Heredado(Guid.NewGuid())));

        await emitir.Should().ThrowAsync<InvalidOperationException>();
    }

    // ------------------------------------------------------------------------------------- aristas §9 --

    [Fact]
    public async Task Los_mensajes_de_una_unidad_no_dependen_entre_si()
    {
        var mensajes = await Emisor().EmitirAsync(Venta(Guid.NewGuid(), new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        mensajes.Should().OnlyContain(m => DependenciasDe(m).Count == 0);
    }

    [Fact]
    public async Task Un_relacionado_apunta_al_ultimo_mensaje_de_la_cadena_de_su_original_y_siempre_hacia_atras()
    {
        var venta = Guid.NewGuid();
        var deLaVenta = await Emisor().EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Online)));
        await _db.SaveChangesAsync();
        var ultimoDeLaVenta = deLaVenta.OrderBy(m => m.Id).Last();

        var nota = await Emisor().EmitirAsync(Nota(Guid.NewGuid(), venta, new ModoDeEntrega.Heredado(venta)));

        foreach (var mensaje in nota)
            DependenciasDe(mensaje).Should().ContainSingle().Which.Should().BeSameAs(ultimoDeLaVenta);

        await _db.SaveChangesAsync();
        var aristas = await _db.IntegrationMessageDependencies.ToListAsync();
        aristas.Should().HaveCount(2);
        aristas.Should().OnlyContain(a => a.DependsOnMessageId < a.MessageId, "el orden causal es el del Id");
        aristas.Should().OnlyContain(a => a.DependsOnMessageId == ultimoDeLaVenta.Id);
    }

    [Fact]
    public async Task Un_evento_posterior_de_un_documento_espera_a_los_anteriores_del_mismo_documento()
    {
        var periodo = Guid.NewGuid();
        var operacion = new OrigenDeEmision(MessageOriginKind.Operation, periodo, null, null, "2026-10", new DateOnly(2026, 10, 31), Sucursal);
        var cierre = (await Emisor().EmitirAsync(new SolicitudDeEmision(operacion, ClavesDeEvento.Cierre(1),
            [new PeriodoInventarioCerradoV1 { Year = 2026, Month = 10, ClosingVersion = 1 }], new ModoDeEntrega.Sellado(DeliveryMode.Online)))).Single();
        await _db.SaveChangesAsync();

        var reapertura = (await Emisor().EmitirAsync(new SolicitudDeEmision(operacion, ClavesDeEvento.Reapertura(1),
            [new PeriodoInventarioReabiertoV1 { Year = 2026, Month = 10, ReopenedClosingVersion = 1, Reason = "Faltó una factura" }],
            new ModoDeEntrega.Sellado(DeliveryMode.Online)))).Single();

        DependenciasDe(reapertura).Should().ContainSingle().Which.Should().BeSameAs(cierre);
        reapertura.OriginKind.Should().Be(MessageOriginKind.Operation);
    }

    [Fact]
    public async Task Un_derivado_espera_a_cada_origen_y_un_ajuste_de_costo_al_documento_afectado()
    {
        var remision1 = Guid.NewGuid();
        var remision2 = Guid.NewGuid();
        var emisor = Emisor();
        var r1 = (await emisor.EmitirAsync(new SolicitudDeEmision(Documento(remision1, nameof(DocumentClass.Shipment), "RM-1"), "Confirmation",
            [new CostoDeVentaReconocidoV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)))).Single();
        var r2 = (await emisor.EmitirAsync(new SolicitudDeEmision(Documento(remision2, nameof(DocumentClass.Shipment), "RM-2"), "Confirmation",
            [new CostoDeVentaReconocidoV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)))).Single();
        await _db.SaveChangesAsync();

        var factura = (await Emisor().EmitirAsync(new SolicitudDeEmision(
            Documento(Guid.NewGuid(), nameof(DocumentClass.SalesInvoiceFromShipments), "FE-10"), "Confirmation",
            [new VentaFacturadaV1()], new ModoDeEntrega.Heredado(remision1), CadenasDeLasQueDepende: [remision1, remision2]))).Single();

        DependenciasDe(factura).Should().HaveCount(2)
            .And.Contain(m => ReferenceEquals(m, r1)).And.Contain(m => ReferenceEquals(m, r2));

        var ajuste = (await Emisor().EmitirAsync(new SolicitudDeEmision(
            Documento(Guid.NewGuid(), nameof(DocumentClass.CostAdjustment), "AC-3"), ClavesDeEvento.ConfirmacionPor(remision2),
            [new AjusteDeCostoReconocidoV1 { Reason = KardexReason.Retroactive }], new ModoDeEntrega.Heredado(remision2),
            Relacionado: new DocumentoRelacionado(remision2, nameof(DocumentClass.Shipment), "RM-2")))).Single();

        DependenciasDe(ajuste).Should().ContainSingle().Which.Should().BeSameAs(r2);
    }

    [Fact]
    public async Task Cada_cadena_aporta_tambien_su_ultimo_mensaje_del_mismo_destino()
    {
        var venta = Guid.NewGuid();
        var pago = Guid.NewGuid();
        var emisor = Emisor();
        var credito = (await emisor.EmitirAsync(new SolicitudDeEmision(Documento(venta), ClavesDeEvento.ConfirmacionPor(pago),
            [new VentaACreditoRegistradaV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)))).Single();
        var deLaVenta = await emisor.EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Online)));
        await _db.SaveChangesAsync();

        var ajuste = (await Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid(), nameof(DocumentClass.CreditNote), "NC-8"),
            ClavesDeEvento.ConfirmacionPor(pago), [new AjusteDeVentaACreditoV1 { AdjustmentClass = "CreditNote", Amount = -10000.00m }],
            new ModoDeEntrega.Heredado(venta),
            Relacionado: new DocumentoRelacionado(venta, nameof(DocumentClass.PosEquivalentDocument), "PV01-1532")))).Single();

        var ultimoDeLaVenta = deLaVenta.OrderBy(m => m.Id).Last();
        DependenciasDe(ajuste).Should().HaveCount(2)
            .And.Contain(m => ReferenceEquals(m, ultimoDeLaVenta))
            .And.Contain(m => ReferenceEquals(m, credito), "sin la arista del mismo destino, el ajuste no esperaría a la venta a crédito que ajusta");
    }

    [Fact]
    public async Task Dos_eventos_del_mismo_guardado_se_ven_sin_estar_en_la_base()
    {
        var venta = Guid.NewGuid();
        var emisor = Emisor();
        var deLaVenta = await emisor.EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        var credito = (await emisor.EmitirAsync(new SolicitudDeEmision(Documento(venta), ClavesDeEvento.ConfirmacionPor(Guid.NewGuid()),
            [new VentaACreditoRegistradaV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)))).Single();

        DependenciasDe(credito).Should().ContainSingle().Which.Should().BeSameAs(deLaVenta[^1]);
        await _db.SaveChangesAsync();
        (await _db.IntegrationMessageDependencies.SingleAsync()).DependsOnMessageId.Should().BeLessThan(credito.Id);
    }

    // ------------------------------------------------------------------------------ claves de §10.1 --

    [Theory]
    [InlineData("confirmation")]
    [InlineData("Confirmation:")]
    [InlineData("Confirm")]
    [InlineData("Close:1")]
    [InlineData("")]
    public async Task Una_clave_fuera_de_10_1_no_se_emite(string clave)
    {
        var emitir = () => Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid()), clave, [new VentaFacturadaV1()],
            new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        await emitir.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Cada_tipo_admite_solo_su_forma_de_clave()
    {
        var ajusteConConfirmacion = () => Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid()), "Confirmation",
            [new AjusteDeCostoReconocidoV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)));
        var creditoSinPago = () => Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid()), "Confirmation",
            [new VentaACreditoRegistradaV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)));
        var reclasificacionConConfirmacion = () => Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid()), "Confirmation",
            [new GrupoContableReclasificadoV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)));
        var cierreSinVersion = () => Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid()), "Close:0",
            [new PeriodoInventarioCerradoV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        await ajusteConConfirmacion.Should().ThrowAsync<ArgumentException>();
        await creditoSinPago.Should().ThrowAsync<ArgumentException>();
        await reclasificacionConConfirmacion.Should().ThrowAsync<ArgumentException>();
        await cierreSinVersion.Should().ThrowAsync<ArgumentException>();

        var reclasificacion = await Emisor().EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, Guid.NewGuid(), null, null, "CAMBIO-1", Fecha, Sucursal),
            ClavesDeEvento.Reclasificacion, [new GrupoContableReclasificadoV1()], new ModoDeEntrega.Sellado(DeliveryMode.Online)));
        reclasificacion.Single().OriginEventKey.Should().Be("Reclassification");
        ClavesDeEvento.Cierre(3).Should().Be("Close:3");
        ClavesDeEvento.Reapertura(3).Should().Be("Reopen:3");
    }

    [Fact]
    public async Task Un_record_que_no_esta_en_el_catalogo_no_se_emite()
    {
        var emitir = () => Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid()), "Confirmation", [new DocumentRefV1()],
            new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        await emitir.Should().ThrowAsync<ArgumentException>();
    }

    // ------------------------------------------------------------------------------------ contenido --

    [Fact]
    public async Task El_hash_es_el_SHA256_de_los_bytes_guardados()
    {
        var contenido = new CompraRecibidaV1 { SupplierDeliveryReference = "Remisión 45 «ñ»" };

        var mensaje = (await Emisor().EmitirAsync(new SolicitudDeEmision(Documento(Guid.NewGuid(), nameof(DocumentClass.PurchaseReceipt), "EI-1"),
            "Confirmation", [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Online)))).Single();

        var bytes = OpcionesDeMensajes.Serializar(contenido);
        mensaje.PayloadJson.Should().Be(Encoding.UTF8.GetString(bytes));
        mensaje.PayloadSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(mensaje.PayloadJson))));
        mensaje.PayloadSha256.Should().HaveLength(64);
        mensaje.PayloadJson.Should().Contain("Remisión 45 «ñ»", "se guarda el texto tal cual, sin escapar");
    }

    // ------------------------------------------------------------------------------ usuario de origen --

    [Fact]
    public async Task El_usuario_de_origen_sale_de_IActorActual()
    {
        var mensaje = (await Emisor().EmitirAsync(Venta(Guid.NewGuid(), new ModoDeEntrega.Sellado(DeliveryMode.Online))))[0];

        mensaje.OriginUserCentralId.Should().Be(UsuarioCentral);
        mensaje.OriginUserName.Should().Be("Ana Cajera");
    }

    [Fact]
    public async Task El_actor_de_proceso_nunca_firma_un_mensaje()
    {
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.ProcesoDeIntegracion(Actor.OrigenDeLote("L-1")));

        var emitir = () => Emisor().EmitirAsync(Venta(Guid.NewGuid(), new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        await emitir.Should().ThrowAsync<InvalidOperationException>();
        _db.IntegrationMessages.Local.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------------- doble emisión --

    [Fact]
    public async Task Emitir_dos_veces_el_mismo_evento_en_la_misma_unidad_falla_antes_de_guardar()
    {
        var venta = Guid.NewGuid();
        var emisor = Emisor();
        await emisor.EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        var otraVez = () => emisor.EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        await otraVez.Should().ThrowAsync<InvalidOperationException>();
        _db.IntegrationMessages.Local.Should().HaveCount(2, "la segunda emisión no agregó nada");
    }

    [Fact]
    public async Task Emitir_otra_vez_un_evento_ya_guardado_falla()
    {
        var venta = Guid.NewGuid();
        await Emisor().EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Online)));
        await _db.SaveChangesAsync();

        var otraVez = () => Emisor().EmitirAsync(Venta(venta, new ModoDeEntrega.Sellado(DeliveryMode.Online)));

        await otraVez.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void La_base_rechaza_la_doble_emision_con_un_indice_unico(bool postgres)
    {
        var mensaje = TipoDelModelo<IntegrationMessage>(postgres);
        mensaje.GetTableName().Should().Be("COR_IntegrationMessages");
        var unico = mensaje.GetIndexes().Single(i => i.GetDatabaseName() == "UK_COR_IntegrationMessages_Origin_Type_EventKey");
        unico.IsUnique.Should().BeTrue();
        unico.Properties.Select(p => p.Name).Should().Equal(
            nameof(IntegrationMessage.OriginPublicId), nameof(IntegrationMessage.Type), nameof(IntegrationMessage.OriginEventKey));

        var entrega = TipoDelModelo<IntegrationMessageDelivery>(postgres);
        entrega.GetIndexes().Single(i => i.GetDatabaseName() == "UK_COR_IntegrationMessageDeliveries_Message_Destination")
            .IsUnique.Should().BeTrue();
        TipoDelModelo<IntegrationMessageDependency>(postgres).GetIndexes()
            .Single(i => i.GetDatabaseName() == "UK_COR_IntegrationMessageDependencies_Message_DependsOn").IsUnique.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void El_contenido_es_texto_en_los_dos_motores_y_el_hash_mide_64(bool postgres)
    {
        var mensaje = TipoDelModelo<IntegrationMessage>(postgres);

        var payload = mensaje.FindProperty(nameof(IntegrationMessage.PayloadJson))!;
        payload.GetColumnType().Should().Be(postgres ? "text" : "nvarchar(max)", "texto y no jsonb, por paridad");
        var hash = mensaje.FindProperty(nameof(IntegrationMessage.PayloadSha256))!;
        hash.GetMaxLength().Should().Be(64);
        hash.IsFixedLength().Should().BeTrue();
        mensaje.FindProperty(nameof(IntegrationMessage.ExchangeRate))!.GetPrecision().Should().Be(18);
        mensaje.FindProperty(nameof(IntegrationMessage.ExchangeRate))!.GetScale().Should().Be(6);
        mensaje.FindProperty(nameof(IntegrationMessage.Id))!.ClrType.Should().Be(typeof(long), "el Id bigint es el orden");

        var entrega = TipoDelModelo<IntegrationMessageDelivery>(postgres);
        entrega.FindProperty(nameof(IntegrationMessageDelivery.BatchId))!.GetContainingForeignKeys()
            .Should().BeEmpty("la FK a COR_IntegrationBatches entra en I2");
        entrega.GetForeignKeys().Should().OnlyContain(fk => fk.DeleteBehavior == DeleteBehavior.Restrict);
        TipoDelModelo<IntegrationMessageDependency>(postgres).GetForeignKeys()
            .Should().HaveCount(2).And.OnlyContain(fk => fk.DeleteBehavior == DeleteBehavior.Restrict);
        typeof(IntegrationMessageDelivery).GetCustomAttributes(typeof(SinDiffDeAuditoriaAttribute), inherit: false).Should().ContainSingle();
    }

    private static IEntityType TipoDelModelo<T>(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres
            ? builder.UseNpgsql("Host=localhost;Database=x;Username=y;Password=z")
            : builder.UseSqlServer("Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True");
        using var db = new ApplicationDbContext(builder.Options);
        return db.Model.FindEntityType(typeof(T))!;
    }

    // ------------------------------------------------------------------------------------ claves de lote --

    [Fact]
    public void Las_claves_de_lote_tienen_su_formato()
    {
        ClavesDeLote.Horario("DEPOS", "CierreDePeriodo", new TimeOnly(22, 0), "Summarized").Should().Be("DEPOS|CierreDePeriodo|22:00|Summarized");
        ClavesDeLote.Horario("FE", "CierreDeTurno", null, "PerDocument").Should().Be("FE|CierreDeTurno||PerDocument");
        ClavesDeLote.Periodo(2026, 3).Should().Be("Period:2026-03");
        var sesion = Guid.NewGuid();
        ClavesDeLote.SesionDeCaja(sesion).Should().Be($"CashSession:{sesion:D}");
    }
}
