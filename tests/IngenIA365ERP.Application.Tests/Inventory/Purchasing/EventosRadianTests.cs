using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T331 (FR-050, US9-4, T42; contracts/api.md §14.8): los eventos RADIAN de una factura del proveedor. Sólo a
/// crédito; el 032 exige el 030; la fecha entre la emisión y hoy; un evento hecho no se repite y un registro externo se corrige
/// con antes y después en la auditoría; la revisión programada levanta <c>Compras.EventosRadianFaltantes</c> a los días del
/// parámetro, una por factura, y la alerta se atiende sola cuando no queda ninguno pendiente; anular el registro no toca los eventos.
/// </summary>
public class EventosRadianTests
{
    private static RegisterExternalRadianEventCommandHandler Registrar(ComprasDePrueba c) =>
        new(c.C.Db, c.K.Actor, c.C.Reloj, c.K.Vista(), c.Alertas, Auditoria(c));

    private static InventoryAuditEmitter Auditoria(ComprasDePrueba c)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(CooperativaDePrueba.PublicIdN);
        var servicios = new ServiceCollection()
            .AddSingleton(Substitute.For<IAuditService>())
            .AddSingleton<IApplicationDbContext>(c.C.Db)
            .AddSingleton(tenant)
            .AddSingleton(NominaTestData.UsuarioDePrueba("compras@coop", 7))
            .BuildServiceProvider();
        return new InventoryAuditEmitter(servicios, NullLogger<InventoryAuditEmitter>.Instance);
    }

    private static async Task<InventoryDocumentDto> FacturaAsync(ComprasDePrueba c, bool credito = true, int diasAtras = 0, string numero = "1")
    {
        var flete = c.C.Db.Products.Any(p => p.Code == "FLETE") ? c.C.Db.Products.Single(p => p.Code == "FLETE").PublicId : await c.ServicioAsync();
        return await c.ConfirmadoAsync(c.Factura(ComprasDePrueba.Documento(numero, credito: credito, emision: CatalogoDePrueba.Hoy.AddDays(-diasAtras)),
            lineas: [c.Linea(flete, 1m, 100_000m)]));
    }

    private static RegisterExternalRadianEventCommand Evento(InventoryDocumentDto factura, SupplierInvoiceEventCode codigo, DateOnly? fecha = null,
        string fuente = SupplierInvoiceEvent.FuenteDian, bool corregir = false, string? notas = null) =>
        new(factura.PublicId, new RegistrarEventoRadianRequest(codigo, fecha ?? CatalogoDePrueba.Hoy, fuente, null, notas, corregir));

    [Fact]
    public async Task Solo_las_facturas_a_credito()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var contado = await FacturaAsync(c, credito: false);
        (await Registrar(c).Handle(Evento(contado, SupplierInvoiceEventCode.Receipt030), default)).Error.Code.Should().Be("Inventory.RadianEvent.NotApplicable");
    }

    [Fact]
    public async Task El_032_antes_del_030_la_fecha_fuera_de_rango_y_el_repetido_no_se_registran()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var factura = await FacturaAsync(c, diasAtras: 5);

        (await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.GoodsReceived032), default)).Error.Code.Should().Be("Inventory.RadianEvent.OutOfOrder");
        (await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.Receipt030, CatalogoDePrueba.Hoy.AddDays(-6)), default))
            .Error.Code.Should().Be("Inventory.RadianEvent.DateInvalid");
        (await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.Receipt030, CatalogoDePrueba.Hoy.AddDays(1)), default))
            .Error.Code.Should().Be("Inventory.RadianEvent.DateInvalid");
        (await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.Receipt030, fuente: "Erp"), default)).Error.Code.Should().Be("Validation.Invalid");

        var hecho = await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.Receipt030), default);
        hecho.IsSuccess.Should().BeTrue(hecho.IsFailure ? hecho.Error.Message : null);
        hecho.Value.Single(e => e.EventCode == SupplierInvoiceEventCode.Receipt030).Status.Should().Be(SupplierInvoiceEventStatus.RegisteredExternally);
        hecho.Value.Single(e => e.EventCode == SupplierInvoiceEventCode.Receipt030).Source.Should().Be(SupplierInvoiceEvent.FuenteDian);
        (await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.Receipt030), default)).Error.Code.Should().Be("Inventory.RadianEvent.AlreadyRegistered");
    }

    [Fact]
    public async Task Corregir_un_registro_externo_guarda_antes_y_despues_en_la_auditoria()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var factura = await FacturaAsync(c, diasAtras: 5);
        (await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.Receipt030, CatalogoDePrueba.Hoy.AddDays(-2)), default)).IsSuccess.Should().BeTrue();

        var corregido = await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.Receipt030, CatalogoDePrueba.Hoy.AddDays(-1),
            SupplierInvoiceEvent.FuenteProveedor, corregir: true, notas: "Se había anotado la fecha del correo"), default);
        corregido.IsSuccess.Should().BeTrue(corregido.IsFailure ? corregido.Error.Message : null);
        corregido.Value.Single(e => e.EventCode == SupplierInvoiceEventCode.Receipt030).Date.Should().Be(CatalogoDePrueba.Hoy.AddDays(-1));

        var fila = c.C.Db.AuditOutbox.Should().ContainSingle().Subject;
        var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
        evento.Action.Should().Be(RegisterExternalRadianEventCommandHandler.AccionDeCorreccion);
        evento.OldValuesJson.Should().Contain("DianPortal");
        evento.NewValuesJson.Should().Contain("SupplierPortal");
    }

    [Fact]
    public async Task La_revision_levanta_la_alerta_a_los_dias_del_parametro_y_registrar_los_dos_la_atiende()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var vieja = await FacturaAsync(c, diasAtras: 3, numero: "V1");
        await FacturaAsync(c, diasAtras: 2, numero: "N1");
        await FacturaAsync(c, credito: false, diasAtras: 10, numero: "C1");

        var revision = new RevisionDeEventosRadian(c.C.Db, c.C.Reloj, c.K.Lector(), c.Alertas);
        (await revision.RevisarAsync(default)).Should().Be(1);
        var alerta = c.Alertas.Levantadas.Should().ContainSingle().Subject;
        alerta.TypeCode.Should().Be("Compras.EventosRadianFaltantes");
        alerta.DedupKey.Should().Be($"RadianPendiente:{vieja.PublicId:D}");
        alerta.EntityPublicId.Should().Be(vieja.PublicId);

        // Repetir la pasada suma la ocurrencia de la misma alerta (misma condición).
        await revision.RevisarAsync(default);
        c.Alertas.Levantadas.Should().HaveCount(2).And.OnlyContain(a => a.DedupKey == alerta.DedupKey);

        await Registrar(c).Handle(Evento(vieja, SupplierInvoiceEventCode.Receipt030), default);
        c.Alertas.Atendidas.Should().BeEmpty("todavía falta el 032");
        await Registrar(c).Handle(Evento(vieja, SupplierInvoiceEventCode.GoodsReceived032), default);
        c.Alertas.Atendidas.Should().Equal(alerta.DedupKey);
    }

    [Fact]
    public async Task Con_otro_plazo_en_el_parametro_la_alerta_espera()
    {
        var c = await ComprasDePrueba.CrearAsync();
        c.K.Parametro(ParametrosDeInventario.ComprasDiasAlertaEventosRadian, "10");
        await FacturaAsync(c, diasAtras: 5);
        (await new RevisionDeEventosRadian(c.C.Db, c.C.Reloj, c.K.Lector(), c.Alertas).RevisarAsync(default)).Should().Be(0);
    }

    [Fact]
    public async Task Anular_el_registro_de_la_factura_no_toca_los_eventos()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var factura = await FacturaAsync(c, diasAtras: 1);
        await Registrar(c).Handle(Evento(factura, SupplierInvoiceEventCode.Receipt030), default);

        (await c.Anular().Handle(new VoidInventoryDocumentCommand(factura.PublicId, DocumentClassGroup.Purchases, "registro errado"), default)).IsSuccess.Should().BeTrue();
        var eventos = await c.C.Db.SupplierInvoiceEvents.Where(e => e.DocumentId == c.Documento(factura.PublicId).Id).ToListAsync();
        eventos.Should().HaveCount(2);
        eventos.Single(e => e.EventCode == SupplierInvoiceEventCode.Receipt030).Status.Should().Be(SupplierInvoiceEventStatus.RegisteredExternally);
        eventos.Single(e => e.EventCode == SupplierInvoiceEventCode.GoodsReceived032).Status.Should().Be(SupplierInvoiceEventStatus.Pending);
    }

    [Fact]
    public async Task La_lista_de_eventos_respeta_el_alcance_y_la_clase()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var factura = await FacturaAsync(c);
        var lista = await new ListRadianEventsQueryHandler(c.C.Db, c.K.Vista()).Handle(new ListRadianEventsQuery(factura.PublicId), default);
        lista.Value.Should().HaveCount(2);
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        (await new ListRadianEventsQueryHandler(c.C.Db, c.K.Vista()).Handle(new ListRadianEventsQuery(recepcion.PublicId), default))
            .Error.Code.Should().Be("Inventory.Document.NotFound");
    }
}
