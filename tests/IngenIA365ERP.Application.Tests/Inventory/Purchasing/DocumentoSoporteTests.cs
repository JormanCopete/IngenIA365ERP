using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.ElectronicInvoicing;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, I4, T740, T741, T742 (data-model §9.5; api.md §14.3, §14.7; contracts/dian.md §14; FR-051, FR-063): el documento soporte y su
/// nota de ajuste sobre el modelo de la factura y la nota del proveedor. El DS a un vendedor no obligado numera con su resolución (aunque la
/// cooperativa no esté obligada a facturar), no nace con eventos RADIAN, emite <c>FacturaProveedorRegistrada</c> con <c>kind =
/// SupportDocument</c> y deja su documento electrónico; a un obligado no confirma; no se anula (se corrige con su nota); la nota de ajuste emite
/// con signo y corrige el documento electrónico del DS. Por operación, la recepción de un no obligado propone el DS; por semana, un comando reúne
/// las recepciones de la semana en un DS por proveedor; la compra directa se soporta con DS.
/// </summary>
public class DocumentoSoporteTests
{
    private sealed class Escenario
    {
        public ComprasDePrueba C { get; init; } = null!;
        public IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => C.C.Db;
        public DianNumberingResolution Resolucion { get; set; } = null!;

        public EfectosDeClase Efectos()
        {
            var registro = C.K.Registro();
            var reversion = new ReversionDeKardex(Db, registro);
            var emision = new EmisionDeInventario(Db);
            var maestros = C.K.Maestros();
            var vinculos = C.Vinculos();
            var diferencias = new DiferenciasDePrecioDeCompra(Db, vinculos);
            return new EfectosDeClase(
            [
                new EfectoRecepcionDeCompra(registro, reversion, emision, maestros, C.Calculo(), Db, C.ContraOrden()),
                new EfectoFacturaDeProveedor(registro, emision, maestros, C.Calculo(), vinculos, diferencias, Db),
                new EfectoDeDocumentoSoporte(registro, emision, maestros, C.Calculo(), vinculos, diferencias, Db),
                new EfectoDeNotaDeAjusteDeDocumentoSoporte(registro, emision, maestros, C.Calculo(), vinculos, diferencias, Db),
            ], EntregaDelComercio.I4);
        }

        public SaveInventoryDraftCommandHandler Guardar() =>
            new(Db, C.K.Maestros(), C.K.Alcance, C.K.Actor, C.C.Reloj, Efectos(), C.K.Vista(), [C.Borrador()]);

        private ConfirmacionDeDocumento SinPasoFiscal() => new(Db, C.K.Maestros(), C.K.Actor, C.C.Reloj, Efectos(), C.K.Motor, C.K.Cerrojo,
            new Numerador(Db, C.K.Cerrojo), new EmisorDeMensajes(Db, C.K.Actor, C.C.Reloj), C.K.Lector(), C.K.Vista(), [], []);

        public ConfirmacionDeDocumento Confirmacion()
        {
            var servicios = new ServiceCollection();
            servicios.AddSingleton<IFuenteDeDocumentoElectronico>(_ => new FuenteDeEmisionDeInventario(Db, C.K.Actor, C.C.Reloj, C.K.Lector(), SinPasoFiscal()));
            servicios.AddSingleton(_ => Guardar());
            var proveedor = servicios.BuildServiceProvider();
            var guardia = new GuardiaDeEmisionFiscal(C.K.Lector(), Db, new CanalesDePrueba(new CanalDePrueba(GuardiaDeEmisionFiscal.CanalSimulado, CanalDePrueba.Completas())),
                EntregaDelComercio.I4);
            var paso = new EmisionFiscalDeLaConfirmacion(Db, guardia, new NumeradorFiscal(Db, C.K.Cerrojo), new RegistroDeDocumentoElectronico(Db, C.C.Reloj),
                new ConstructorDelCanonico(C.K.Lector()), proveedor);
            return new ConfirmacionDeDocumento(Db, C.K.Maestros(), C.K.Actor, C.C.Reloj, Efectos(), C.K.Motor, C.K.Cerrojo,
                new Numerador(Db, C.K.Cerrojo), new EmisorDeMensajes(Db, C.K.Actor, C.C.Reloj), C.K.Lector(), C.K.Vista(), [paso], [],
                avisosAlConfirmar: [new PropuestaDeDocumentoSoporte(Db, C.K.Lector(), proveedor, C.CompraDirecta)]);
        }

        public async Task<Result<InventoryDocumentDto>> GuardarAsync(SaveInventoryDraftRequest borrador) =>
            await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Purchases, borrador), default);

        public Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento) =>
            Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(documento, DocumentClassGroup.Purchases), default);

        public async Task<Guid> RecepcionConfirmadaAsync(Domain.Entities.Core.Person? proveedor = null, DateOnly? fecha = null)
        {
            var g = await GuardarAsync(C.Recepcion(proveedor, fecha: fecha, lineas: [C.Linea(C.P1, 3m, 15_600m, C.Docena)]));
            g.IsSuccess.Should().BeTrue(g.IsFailure ? $"{g.Error.Code}: {g.Error.Message}" : null);
            var r = await ConfirmarAsync(g.Value.PublicId);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
            return g.Value.PublicId;
        }

        public async Task<SaveInventoryDraftRequest> SoporteContraAsync(Guid recepcion)
        {
            var lineas = await C.LineasAsync(recepcion);
            return new SaveInventoryDraftRequest(C.Tipo("DS"), null, null, null, null, null, null, null, null, null, null, null, null,
                lineas.Select(l => new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, l.Quantity, UnitPrice: l.UnitPrice, ReceiptLinePublicId: l.PublicId)).ToList(),
                SupplierPersonPublicId: C.ProveedorA.PublicId);
        }
    }

    private static async Task<Escenario> CrearAsync(bool obligadaAFacturar = false)
    {
        var e = new Escenario { C = await ComprasDePrueba.CrearAsync() };
        var db = e.Db;
        var ds = new InventoryDocumentType { Code = "DS", Name = "Documento soporte", Class = DocumentClass.SupportDocument, FiscalPrefix = "DSE", IsActive = true, AllWarehouses = true };
        var nds = new InventoryDocumentType { Code = "NDS", Name = "Nota de ajuste del DS", Class = DocumentClass.SupportDocumentAdjustmentNote, IsActive = true, AllWarehouses = true };
        nds.Sequences.Add(new DocumentSequence { DocumentType = nds, Prefix = "NDS", NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
        db.InventoryDocumentTypes.AddRange(ds, nds);
        db.ElectronicEmissionSettings.Add(new ElectronicEmissionSetting
        {
            Mode = EmissionMode.TechnologyProvider, ChannelCode = GuardiaDeEmisionFiscal.CanalSimulado, Environment = DianEnvironment.Testing,
            CredentialKey = "k.SIMULADO.json", CredentialVerifiedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            IssuerTaxId = "900555444", IssuerCheckDigit = "1", IssuerBusinessName = "Cooperativa", IssuerAddress = "Calle 1",
            IssuerMunicipalityDaneCode = "76001", IssuerEmail = "fe@coop.co", IsEnabled = true, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        e.Resolucion = new DianNumberingResolution
        {
            Kind = ResolutionKind.SupportDocument, ResolutionNumber = "18764DS", ResolutionDate = new DateOnly(2025, 12, 20), Prefix = "DSE", RangeTo = 100,
            ValidFrom = new DateOnly(2026, 1, 1), ValidTo = new DateOnly(2026, 12, 31), Environment = DianEnvironment.Testing, IsActive = true,
        };
        e.Resolucion.RangeFrom = 1;
        e.Resolucion.Channels.Add(new DianResolutionChannel { Resolution = e.Resolucion, ChannelCode = GuardiaDeEmisionFiscal.CanalSimulado, ValidFrom = new DateOnly(2026, 1, 1) });
        db.DianNumberingResolutions.Add(e.Resolucion);
        foreach (var u in db.UnitsOfMeasure) u.DianUnitCode ??= "94";
        foreach (var t in db.TaxDefinitions) t.DianTaxCode ??= "01";
        foreach (var p in db.People) p.IdType ??= "NIT";
        e.C.ProveedorB.IsObligatedToInvoice = true;
        await db.SaveChangesAsync();
        e.C.Parametro(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.ObligadaAFacturar, obligadaAFacturar ? "true" : "false");
        return e;
    }

    [Fact]
    public async Task El_documento_soporte_numera_con_su_resolucion_emite_y_no_depende_de_la_obligacion_de_facturar()
    {
        var e = await CrearAsync(obligadaAFacturar: false);
        // Semanal: la recepción no propone el DS y aquí se arma a mano.
        e.C.Parametro(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.DocumentoSoporteGeneracion, PropuestaDeDocumentoSoporte.GeneracionSemanal);
        var recepcion = await e.RecepcionConfirmadaAsync();

        var borrador = await e.GuardarAsync(await e.SoporteContraAsync(recepcion));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? $"{borrador.Error.Code}: {borrador.Error.Message}" : null);
        var r = await e.ConfirmarAsync(borrador.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var ds = e.C.Documento(borrador.Value.PublicId);
        ds.Prefix.Should().Be("DSE");
        ds.Number.Should().Be(1);
        e.Db.SupplierInvoiceEvents.Where(x => x.DocumentId == ds.Id).Should().BeEmpty("el DS no nace con eventos RADIAN");
        e.C.ContenidoDe(ds.PublicId, ClasesDeDocumento.FacturaProveedorRegistrada).Should().Contain("\"kind\":\"SupportDocument\"").And.Contain("\"number\":\"1\"");
        var electronico = e.Db.ElectronicDocuments.Single(x => x.SourceDocumentPublicId == ds.PublicId);
        electronico.Kind.Should().Be(ElectronicDocumentKind.SupportDocument);
        electronico.Status.Should().Be(ElectronicDocumentStatus.Pending);

        var anular = await e.C.Anular().Handle(new VoidInventoryDocumentCommand(ds.PublicId, DocumentClassGroup.Purchases, "error"), default);
        anular.IsFailure.Should().BeTrue();
        anular.Error.Code.Should().Be(ErroresDeVentas.FiscalUseCorrectionCode);
        System.Text.Json.JsonSerializer.Serialize(((ErrorConDatos)anular.Error).Data).Should().Contain(nameof(DocumentClass.SupportDocumentAdjustmentNote));

        // La nota de ajuste: contra el DS, con signo negativo, y su documento electrónico corrige el del DS.
        var lineas = await e.C.LineasAsync(ds.PublicId);
        var nota = await e.GuardarAsync(new SaveInventoryDraftRequest(e.C.Tipo("NDS"), null, null, null, null, null, null, null, null, null, null, null, null,
            [new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 0m, InvoiceLinePublicId: lineas[0].PublicId, Amount: 1_000m)],
            SupplierPersonPublicId: e.C.ProveedorA.PublicId, SupplierInvoicePublicId: ds.PublicId,
            CorrectionConceptCode: Application.ElectronicInvoicing.Catalogs.CatalogoDian.Embebido
                .ConceptosDeCorreccion(Application.ElectronicInvoicing.Catalogs.ClaseDeNotaDian.NotaDeAjusteDelDocumentoSoporte, Catalog.CatalogoDePrueba.Hoy)
                .First(c => !c.EsAnulacion).Codigo));
        nota.IsSuccess.Should().BeTrue(nota.IsFailure ? $"{nota.Error.Code}: {nota.Error.Message}" : null);
        var rn = await e.ConfirmarAsync(nota.Value.PublicId);
        rn.IsSuccess.Should().BeTrue(rn.IsFailure ? $"{rn.Error.Code}: {rn.Error.Message}" : null);
        e.C.ContenidoDe(nota.Value.PublicId, ClasesDeDocumento.FacturaProveedorRegistrada).Should().Contain("\"kind\":\"SupportDocumentAdjustmentNote\"");
        var electronicaNota = e.Db.ElectronicDocuments.Single(x => x.SourceDocumentPublicId == nota.Value.PublicId);
        electronicaNota.Kind.Should().Be(ElectronicDocumentKind.SupportDocumentAdjustmentNote);
        electronicaNota.CorrectsDocumentId.Should().Be(electronico.Id);
    }

    [Fact]
    public async Task A_un_proveedor_obligado_a_facturar_no_se_le_hace_documento_soporte()
    {
        var e = await CrearAsync();
        var recepcion = await e.RecepcionConfirmadaAsync(e.C.ProveedorB);
        var pedido = await e.SoporteContraAsync(recepcion) with { SupplierPersonPublicId = e.C.ProveedorB.PublicId };
        var borrador = await e.GuardarAsync(pedido);
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? $"{borrador.Error.Code}: {borrador.Error.Message}" : null);

        var r = await e.ConfirmarAsync(borrador.Value.PublicId);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(EfectoDeDocumentoSoporte.SupplierObligatedCode);
        e.C.Documento(borrador.Value.PublicId).Number.Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------------ T741 --

    [Fact]
    public async Task Por_operacion_la_recepcion_de_un_no_obligado_propone_su_documento_soporte()
    {
        var e = await CrearAsync();
        var g = await e.GuardarAsync(e.C.Recepcion(lineas: [e.C.Linea(e.C.P1, 3m, 15_600m, e.C.Docena)]));
        var r = await e.ConfirmarAsync(g.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var aviso = r.Value.Warnings.Single(w => w.Code == PropuestaDeDocumentoSoporte.ProposedCode);
        var propuesto = e.Db.InventoryDocuments.Single(d => d.Class == DocumentClass.SupportDocument);
        propuesto.Status.Should().Be(DocumentStatus.Draft);
        System.Text.Json.JsonSerializer.Serialize(aviso.Data).Should().Contain(propuesto.PublicId.ToString());
        var recepcion = e.C.Documento(g.Value.PublicId);
        e.Db.DocumentLinks.Should().Contain(l => l.SourceDocumentId == recepcion.Id && l.TargetDocumentId == propuesto.Id && l.Kind == DocumentLinkKind.InvoiceOfReceipt);

        // Un obligado no propone.
        var g2 = await e.GuardarAsync(e.C.Recepcion(e.C.ProveedorB, lineas: [e.C.Linea(e.C.P1, 1m, 15_600m, e.C.Docena)]));
        var r2 = await e.ConfirmarAsync(g2.Value.PublicId);
        r2.Value.Warnings.Should().NotContain(w => w.Code == PropuestaDeDocumentoSoporte.ProposedCode);
    }

    // ------------------------------------------------------------------------------------------------ T742 --

    [Fact]
    public async Task Por_semana_reune_las_recepciones_de_la_semana_en_un_documento_soporte_por_proveedor()
    {
        var e = await CrearAsync();
        e.C.Parametro(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.DocumentoSoporteGeneracion, PropuestaDeDocumentoSoporte.GeneracionSemanal);
        var r1 = await e.RecepcionConfirmadaAsync();
        var r2 = await e.RecepcionConfirmadaAsync();
        await e.RecepcionConfirmadaAsync(e.C.ProveedorB);
        e.Db.InventoryDocuments.Where(d => d.Class == DocumentClass.SupportDocument).Should().BeEmpty("con Semanal la recepción no propone");

        var handler = new GenerateWeeklySupportDocumentsCommandHandler(e.Db, e.C.C.Reloj, e.C.K.Lector(), e.Guardar());
        var r = await handler.Handle(new GenerateWeeklySupportDocumentsCommand(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var propuesto = r.Value.Should().ContainSingle("sólo el proveedor no obligado").Subject;
        propuesto.ReceiptPublicIds.Should().BeEquivalentTo([r1, r2]);
        e.C.Documento(propuesto.SupportDocumentPublicId).Class.Should().Be(DocumentClass.SupportDocument);

        (await handler.Handle(new GenerateWeeklySupportDocumentsCommand(), default)).Value.Should().BeEmpty("las recepciones ya tienen su DS");
    }

    [Fact]
    public async Task La_compra_directa_a_un_no_obligado_se_soporta_con_el_documento_soporte()
    {
        var e = await CrearAsync();
        var handler = new ConfirmDirectPurchaseCommandHandler(e.Db, e.Guardar(), e.Confirmacion(), e.C.CompraDirecta, e.C.K.Vista());

        var r = await handler.Handle(new ConfirmDirectPurchaseCommand(e.C.Recepcion(lineas: [e.C.Linea(e.C.P1, 2m, 15_600m, e.C.Docena)]),
            new DirectPurchaseInvoiceRequest(e.C.Tipo("DS"), null)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var ds = e.Db.InventoryDocuments.Where(d => d.Class == DocumentClass.SupportDocument).ToList();
        ds.Should().ContainSingle("la recepción no propone otro: la compra directa ya lo trae");
        ds[0].Status.Should().Be(DocumentStatus.Confirmed);
        ds[0].Prefix.Should().Be("DSE");
    }
}
