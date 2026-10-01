using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Application.Tests.Inventory.Purchasing;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Channels.Simulado;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing;

/// <summary>
/// Feature 012, I5, T772 (FR-050, US13-4, T42; contracts/api.md §14.8 y §24.7; dian.md §4.3, §5.1 y §14.3): la emisión de los eventos RADIAN
/// 030 y 032 desde el ERP con el canal simulado. Factura de contado → <c>Inventory.RadianEvent.NotApplicable</c>; 032 sin 030 hecho ni pedido
/// → <c>.OutOfOrder</c>; 032 sin recepción confirmada → <c>.ReceiptNotConfirmed</c>; evento registrado o en emisión → <c>.AlreadyRegistered</c>;
/// canal sin eventos → <c>ElectronicInvoicing.NotReady</c> con <c>ElectronicInvoicing.Readiness.EventsNotSupported</c>; el ciclo
/// <c>Pending → Emitted</c> al validarse (CUDE, fuente <c>Erp</c>) y <c>→ Rejected → Pending</c> al reintentar (mismo número, versión 2); con
/// los dos emitidos, la alerta <c>Compras.EventosRadianFaltantes</c> queda atendida. La numeración propia (T802): prefijo fijo por tipo del
/// catálogo DIAN y consecutivo por ambiente y prefijo.
/// </summary>
public class EmisionDeEventosRadianTests
{
    /// <summary>Un CUFE de 96 caracteres hexadecimales, distinto por número de factura (el CUFE es único).</summary>
    private static string Cufe(string numero) =>
        Convert.ToHexString(System.Security.Cryptography.SHA384.HashData(System.Text.Encoding.UTF8.GetBytes(numero))).ToLowerInvariant();

    /// <summary>La cooperativa de compras con la emisión electrónica configurada por el canal simulado real (o uno sin eventos).</summary>
    private sealed class Escenario
    {
        public ComprasDePrueba C { get; private init; } = null!;
        public Guid Cooperativa { get; } = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c092");
        public ICurrentTenantService Tenant { get; } = Substitute.For<ICurrentTenantService>();
        public ICredencialesDeCanal Credenciales { get; } = Substitute.For<ICredencialesDeCanal>();
        public ISender Sender { get; } = Substitute.For<ISender>();
        public ICanalesDeEmision Canales { get; private init; } = null!;
        public List<UploadAttachmentCommand> Subidas { get; } = [];

        public static async Task<Escenario> CrearAsync(bool canalConEventos = true)
        {
            var c = await ComprasDePrueba.CrearAsync();
            ICanalDeEmisionElectronica canal = canalConEventos
                ? new CanalSimulado(Options.Create(new ElectronicInvoicingOptions()), new MemoriaDelCanalSimulado())
                : new CanalDePrueba(GuardiaDeEmisionFiscal.CanalSimulado, CanalDePrueba.Completas());
            var e = new Escenario { C = c, Canales = new RegistroDeCanales(canal) };
            e.Tenant.TenantId.Returns(e.Cooperativa.ToString());
            e.Credenciales.ResolverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(x => Result.Success(new CredencialesDeCanal(CredencialesDeCanal.ClaveDe(e.Cooperativa, x.Arg<string>()),
                    new Dictionary<string, string>())));
            e.Sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>())
                .Returns(x => { e.Subidas.Add(x.Arg<UploadAttachmentCommand>()); return Result.Success(Guid.NewGuid()); });

            c.C.Db.ElectronicEmissionSettings.Add(new ElectronicEmissionSetting
            {
                Mode = EmissionMode.TechnologyProvider, ChannelCode = GuardiaDeEmisionFiscal.CanalSimulado, Environment = DianEnvironment.Testing,
                CredentialKey = CredencialesDeCanal.ClaveDe(e.Cooperativa, GuardiaDeEmisionFiscal.CanalSimulado),
                CredentialVerifiedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                IssuerTaxId = "900555111", IssuerCheckDigit = "3", IssuerBusinessName = "Cooperativa de prueba", IssuerAddress = "Calle 1",
                IssuerMunicipalityDaneCode = "76001", IssuerEmail = "facturas@coop.co", IsEnabled = true, ValidFrom = new DateOnly(2026, 1, 1),
                Reason = "prueba",
            });
            await c.C.Db.SaveChangesAsync();
            return e;
        }

        public ConstructorDelCanonico Constructor() => new(C.K.Lector());

        public FuenteDeEmisionDeInventario Fuente() => new(C.C.Db, C.K.Actor, C.C.Reloj, C.K.Lector(), C.Confirmacion(),
            eventosRadian: new EventosRadianDeInventario(C.C.Db, C.K.Vista(), C.K.Actor, C.C.Reloj, C.Alertas));

        public EmitRadianEventCommandHandler Emitir(EntregaDelComercio entrega = EntregaDelComercio.I5) => new(
            C.C.Db, [Fuente()], new GuardiaDeEmisionFiscal(C.K.Lector(), C.C.Db, Canales, entrega), new NumeradorFiscal(C.C.Db, C.K.Cerrojo),
            Constructor(), C.C.Reloj, C.Alertas);

        /// <summary>El intento del procesador (T805) contra el canal sellado.</summary>
        public EmitElectronicDocumentCommandHandler Procesar()
        {
            var db = C.C.Db;
            var fuentes = new IFuenteDeDocumentoElectronico[] { Fuente() };
            var artefactos = new GuardadoDeArtefactos(Sender, new ReconstruccionDelCanonico(db, fuentes, Constructor()), C.Alertas,
                NullLogger<GuardadoDeArtefactos>.Instance);
            var intento = new IntentoAnteElCanal(db, Canales, Credenciales, Tenant, C.C.Reloj, C.K.Actor, new ArrendamientoEnMemoria(db),
                new EsperasDeReintento([TimeSpan.FromSeconds(15)], TimeSpan.FromHours(1), TimeSpan.FromMinutes(2)), artefactos,
                new ContingenciaDeLaDian(db, C.K.Lector(), C.Alertas), C.Alertas, NullLogger<IntentoAnteElCanal>.Instance,
                eventosRadian: new CicloDelEventoRadian(db, fuentes, Constructor(), artefactos));
            return new EmitElectronicDocumentCommandHandler(intento);
        }

        /// <summary>Una factura a crédito, electrónica (con CUFE), contra una recepción confirmada del proveedor.</summary>
        public async Task<InventoryDocumentDto> FacturaConRecepcionAsync(string numero = "4521", bool credito = true,
            Domain.Entities.Core.Person? proveedor = null)
        {
            var recepcion = await C.RecepcionDeTresDocenasAsync(proveedor);
            return await C.ConfirmadoAsync(await C.FacturaContraAsync(recepcion,
                ComprasDePrueba.Documento(numero, cufe: Cufe(numero), electronica: true, credito: credito), proveedor: proveedor));
        }

        /// <summary>Una factura a crédito de un servicio, sin recepción.</summary>
        public async Task<InventoryDocumentDto> FacturaDeServicioAsync()
        {
            var flete = await C.ServicioAsync();
            return await C.ConfirmadoAsync(C.Factura(ComprasDePrueba.Documento("S1", cufe: Cufe("S1"), electronica: true, credito: true),
                lineas: [C.Linea(flete, 1m, 100_000m)]));
        }

        public Task<Result<RadianEmissionDto>> EmitirAsync(InventoryDocumentDto factura, params SupplierInvoiceEventCode[] codigos) =>
            Emitir().Handle(new EmitRadianEventCommand(factura.PublicId, codigos), default);

        /// <param name="ahora">«Reintentar ahora»: el reloj de la prueba no avanza, así que la espera entre intentos se adelanta.</param>
        public Task<Result<ElectronicTransmissionResultDto>> ProcesarAsync(Guid documento, bool ahora = false) =>
            Procesar().Handle(new EmitElectronicDocumentCommand(documento, ahora), default);

        public async Task<List<SupplierInvoiceEvent>> EventosAsync(InventoryDocumentDto factura)
        {
            C.C.Db.DescartarCambios();
            var id = C.Documento(factura.PublicId).Id;
            return await C.C.Db.SupplierInvoiceEvents.AsNoTracking().Where(e => e.DocumentId == id).OrderBy(e => e.EventCode).ToListAsync();
        }

        public async Task<ElectronicDocument> DocumentoAsync(Guid publicId)
        {
            C.C.Db.DescartarCambios();
            return await C.C.Db.ElectronicDocuments.AsNoTracking().Include(d => d.Versions).Include(d => d.Transmissions)
                .SingleAsync(d => d.PublicId == publicId);
        }
    }

    [Fact]
    public async Task Factura_de_contado_no_aplica()
    {
        var s = await Escenario.CrearAsync();
        var contado = await s.FacturaConRecepcionAsync(credito: false);

        var r = await s.EmitirAsync(contado, SupplierInvoiceEventCode.Receipt030);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Inventory.RadianEvent.NotApplicable");
        s.C.C.Db.ElectronicDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task El_032_sin_el_030_hecho_ni_pedido_esta_fuera_de_orden()
    {
        var s = await Escenario.CrearAsync();
        var factura = await s.FacturaConRecepcionAsync();

        var r = await s.EmitirAsync(factura, SupplierInvoiceEventCode.GoodsReceived032);

        r.Error.Code.Should().Be("Inventory.RadianEvent.OutOfOrder");
    }

    [Fact]
    public async Task El_032_sin_recepcion_confirmada_enlazada_no_se_emite()
    {
        var s = await Escenario.CrearAsync();
        var servicio = await s.FacturaDeServicioAsync();

        var r = await s.EmitirAsync(servicio, SupplierInvoiceEventCode.Receipt030, SupplierInvoiceEventCode.GoodsReceived032);

        r.Error.Code.Should().Be("Inventory.RadianEvent.ReceiptNotConfirmed");
        s.C.C.Db.ElectronicDocuments.Should().BeEmpty("con un error no se crea ningún evento");
        // El 030 solo sí sale: no depende de la recepción.
        (await s.EmitirAsync(servicio, SupplierInvoiceEventCode.Receipt030)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Un_evento_registrado_por_fuera_o_ya_en_emision_no_se_vuelve_a_pedir()
    {
        var s = await Escenario.CrearAsync();
        var factura = await s.FacturaConRecepcionAsync();
        var registrar = new RegisterExternalRadianEventCommandHandler(s.C.C.Db, s.C.K.Actor, s.C.C.Reloj, s.C.K.Vista(), s.C.Alertas, null!);
        (await registrar.Handle(new RegisterExternalRadianEventCommand(factura.PublicId,
            new RegistrarEventoRadianRequest(SupplierInvoiceEventCode.Receipt030, CatalogoDePrueba.Hoy, SupplierInvoiceEvent.FuenteDian)), default))
            .IsSuccess.Should().BeTrue();

        (await s.EmitirAsync(factura, SupplierInvoiceEventCode.Receipt030)).Error.Code.Should().Be("Inventory.RadianEvent.AlreadyRegistered");

        // Con el 030 hecho por fuera, el 032 sale solo; pedirlo otra vez mientras espera respuesta también es «ya registrado».
        (await s.EmitirAsync(factura, SupplierInvoiceEventCode.GoodsReceived032)).IsSuccess.Should().BeTrue();
        (await s.EmitirAsync(factura, SupplierInvoiceEventCode.GoodsReceived032)).Error.Code.Should().Be("Inventory.RadianEvent.AlreadyRegistered");
    }

    [Fact]
    public async Task Un_canal_sin_eventos_no_esta_listo()
    {
        var s = await Escenario.CrearAsync(canalConEventos: false);
        var factura = await s.FacturaConRecepcionAsync();

        var r = await s.EmitirAsync(factura, SupplierInvoiceEventCode.Receipt030);

        r.Error.Code.Should().Be("ElectronicInvoicing.NotReady");
        JsonSerializer.Serialize(((ErrorConDatos)r.Error).Data).Should().Contain("ElectronicInvoicing.Readiness.EventsNotSupported");
        (await s.EventosAsync(factura)).Should().OnlyContain(e => e.Status == SupplierInvoiceEventStatus.Pending && e.ElectronicDocumentPublicId == null);
    }

    [Fact]
    public async Task Antes_de_I5_la_emision_no_esta_activa()
    {
        var s = await Escenario.CrearAsync();
        var factura = await s.FacturaConRecepcionAsync();

        var r = await s.Emitir(EntregaDelComercio.I4).Handle(new EmitRadianEventCommand(factura.PublicId, [SupplierInvoiceEventCode.Receipt030]), default);

        r.Error.Code.Should().Be("ElectronicInvoicing.NotReady");
        JsonSerializer.Serialize(((ErrorConDatos)r.Error).Data).Should().Contain(GuardiaDeEmisionFiscal.EventsNotActiveCode);
    }

    [Fact]
    public async Task Los_dos_eventos_se_emiten_en_orden_quedan_emitidos_y_la_alerta_se_atiende_sola()
    {
        var s = await Escenario.CrearAsync();
        var factura = await s.FacturaConRecepcionAsync();

        var r = await s.EmitirAsync(factura, SupplierInvoiceEventCode.GoodsReceived032, SupplierInvoiceEventCode.Receipt030);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Events.Select(e => e.EventCode).Should().Equal(SupplierInvoiceEventCode.Receipt030, SupplierInvoiceEventCode.GoodsReceived032);
        r.Value.Events.Should().OnlyContain(e => e.Status == ElectronicDocumentStatus.Pending);
        var acuse = await s.DocumentoAsync(r.Value.Events[0].ElectronicDocumentPublicId);
        var recibo = await s.DocumentoAsync(r.Value.Events[1].ElectronicDocumentPublicId);

        // T802: numeración propia, prefijo fijo por tipo y consecutivo por ambiente y prefijo; tipo 96; el canal vigente sellado.
        acuse.Kind.Should().Be(ElectronicDocumentKind.RadianEvent030);
        acuse.Number.Should().Be("EV0301");
        recibo.Number.Should().Be("EV0321");
        acuse.DianDocumentTypeCode.Should().Be("96");
        acuse.ChannelCode.Should().Be(GuardiaDeEmisionFiscal.CanalSimulado);
        acuse.SourceDocumentPublicId.Should().Be(factura.PublicId);
        acuse.Versions.Should().ContainSingle().Which.CanonicalSha256.Should().HaveLength(64);
        recibo.WaitsForDocumentId.Should().Be(acuse.Id, "el 032 sale después del 030");

        var eventos = await s.EventosAsync(factura);
        eventos.Should().OnlyContain(e => e.Status == SupplierInvoiceEventStatus.Pending);
        eventos.Select(e => e.ElectronicDocumentPublicId).Should().Equal(acuse.PublicId, recibo.PublicId);

        // El procesador: el 032 espera; el 030 se valida y queda emitido.
        (await s.ProcesarAsync(recibo.PublicId)).Value.Attempted.Should().BeFalse();
        var primero = await s.ProcesarAsync(acuse.PublicId);
        primero.IsSuccess.Should().BeTrue(primero.IsFailure ? $"{primero.Error.Code}: {primero.Error.Message}" : null);
        primero.Value.Status.Should().Be(ElectronicDocumentStatus.Validated);
        eventos = await s.EventosAsync(factura);
        var e030 = eventos.Single(e => e.EventCode == SupplierInvoiceEventCode.Receipt030);
        e030.Status.Should().Be(SupplierInvoiceEventStatus.Emitted);
        e030.Source.Should().Be(SupplierInvoiceEvent.FuenteErp);
        e030.Cude.Should().HaveLength(96).And.Be((await s.DocumentoAsync(acuse.PublicId)).UniqueCode);
        e030.EventDate.Should().Be(CatalogoDePrueba.Hoy);
        s.C.Alertas.Atendidas.Should().NotContain(RevisionDeEventosRadian.ClaveDeLaAlerta(factura.PublicId), "todavía falta el 032");

        var segundo = await s.ProcesarAsync(recibo.PublicId, ahora: true);
        segundo.Value.Status.Should().Be(ElectronicDocumentStatus.Validated);
        (await s.EventosAsync(factura)).Should().OnlyContain(e => e.Status == SupplierInvoiceEventStatus.Emitted);
        s.C.Alertas.Atendidas.Should().Contain(RevisionDeEventosRadian.ClaveDeLaAlerta(factura.PublicId));
        s.Subidas.Should().Contain(u => u.FileName == "EV0301-v1-canonico.json" && u.OwnerEntityType == "ElectronicPurchaseDocument");

        // Otra factura: el consecutivo sigue por prefijo.
        var otra = await s.FacturaConRecepcionAsync("4522");
        var siguiente = await s.EmitirAsync(otra, SupplierInvoiceEventCode.Receipt030);
        (await s.DocumentoAsync(siguiente.Value.Events[0].ElectronicDocumentPublicId)).Number.Should().Be("EV0302");
    }

    [Fact]
    public async Task Un_evento_rechazado_queda_rechazado_y_al_reintentarlo_vuelve_a_pendiente_con_el_mismo_numero()
    {
        var s = await Escenario.CrearAsync();
        // El canal simulado rechaza la versión 1 cuando la identificación del proveedor termina en 1 (800999111).
        var factura = await s.FacturaConRecepcionAsync(proveedor: s.C.ProveedorB);

        var pedido = await s.EmitirAsync(factura, SupplierInvoiceEventCode.Receipt030);
        pedido.IsSuccess.Should().BeTrue(pedido.IsFailure ? $"{pedido.Error.Code}: {pedido.Error.Message}" : null);
        var id = pedido.Value.Events[0].ElectronicDocumentPublicId;
        (await s.ProcesarAsync(id)).Value.Status.Should().Be(ElectronicDocumentStatus.Rejected);
        (await s.EventosAsync(factura)).Single(e => e.EventCode == SupplierInvoiceEventCode.Receipt030).Status
            .Should().Be(SupplierInvoiceEventStatus.Rejected);

        var reintento = await s.EmitirAsync(factura, SupplierInvoiceEventCode.Receipt030);
        reintento.IsSuccess.Should().BeTrue(reintento.IsFailure ? $"{reintento.Error.Code}: {reintento.Error.Message}" : null);
        reintento.Value.Events[0].ElectronicDocumentPublicId.Should().Be(id, "el reintento conserva el documento y su número");
        var documento = await s.DocumentoAsync(id);
        documento.Status.Should().Be(ElectronicDocumentStatus.Pending);
        documento.Number.Should().Be("EV0301");
        documento.Versions.Select(v => v.VersionNumber).Should().BeEquivalentTo(new short[] { 1, 2 });
        (await s.EventosAsync(factura)).Single(e => e.EventCode == SupplierInvoiceEventCode.Receipt030).Status
            .Should().Be(SupplierInvoiceEventStatus.Pending);

        (await s.ProcesarAsync(id)).Value.Status.Should().Be(ElectronicDocumentStatus.Validated);
        (await s.EventosAsync(factura)).Single(e => e.EventCode == SupplierInvoiceEventCode.Receipt030).Status
            .Should().Be(SupplierInvoiceEventStatus.Emitted);
        s.C.Alertas.Atendidas.Should().NotContain(RevisionDeEventosRadian.ClaveDeLaAlerta(factura.PublicId), "falta el 032");
    }
}
