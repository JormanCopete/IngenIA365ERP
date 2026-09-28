using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canonical;

/// <summary>
/// Feature 012, I4, T704 (contracts/dian.md §4.1 y §8.3; api.md §24.5): Inventario entrega a la plataforma la entrada neutral de su
/// documento —líneas con la unidad DIAN, foto tributaria separando impuestos de retenciones, pagos con su clase y su medio DIAN,
/// todas las versiones de la copia fiscal, el documento que corrige una nota— y prepara el borrador de reemplazo del caso b sin
/// que la plataforma lea tablas <c>INV_</c>.
/// </summary>
public class FuenteDeEmisionDeInventarioTests
{
    private static readonly DateOnly Fecha = new(2026, 12, 5);

    [Fact]
    public async Task Lee_la_factura_confirmada_como_entrada_neutral()
    {
        await using var e = await EscenarioAsync();

        var r = await e.Fuente.LeerAsync(e.Factura.PublicId, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var entrada = r.Value;
        entrada.SourceModule.Should().Be("INV");
        entrada.Kind.Should().Be(ElectronicDocumentKind.Invoice);
        entrada.DocumentClass.Should().Be("SalesInvoice");
        entrada.DocumentNumber.Should().Be("SETP990000123");
        entrada.Lineas.Should().ContainSingle().Which.Should().Be(
            new LineaDeEntrada(1, "ARZ-001", "Arroz 500 g", 2m, "UND", "94", 2100m, 4200m, 210m));
        entrada.Impuestos.Should().HaveCount(2);
        entrada.Impuestos.Single(i => !i.EsRetencion).LineNumber.Should().Be(1);
        entrada.Impuestos.Single(i => i.EsRetencion).Should().Match<ImpuestoDeEntrada>(i => i.LineNumber == null && i.DianTaxCode == "06");
        entrada.Pagos.Should().ContainSingle().Which.Should().Be(new PagoDeEntrada("EFE", "Efectivo", PaymentMeansClass.Cash, "10", 4648.35m, null));
        entrada.Totales.Should().Be(new TotalesDeEntrada(4200m, 210m, 758.10m, 99.75m, 4748.10m, 4648.35m));
        entrada.Contrapartes.Select(c => c.Version).Should().BeEquivalentTo([1, 2], "el constructor elige la de mayor versión");
        entrada.Correccion.Should().BeNull();
    }

    [Fact]
    public async Task La_entrada_de_la_fuente_construye_el_canonico()
    {
        await using var e = await EscenarioAsync();
        var entrada = (await e.Fuente.LeerAsync(e.Factura.PublicId, default)).Value;

        var r = ConstructorDelCanonico.Construir(entrada, new ContextoDelCanonico
        {
            Configuracion = new Domain.Entities.ElectronicInvoicing.ElectronicEmissionSetting
            {
                IssuerTaxId = "890300001", IssuerCheckDigit = "3", IssuerBusinessName = "COOPERATIVA", IssuerAddress = "CRA 1",
                IssuerMunicipalityDaneCode = "76001", IssuerEmail = "fe@coop.co",
            },
            Resolucion = new Domain.Entities.ElectronicInvoicing.DianNumberingResolution { Prefix = "SETP", RangeFrom = 1, RangeTo = 999999999 },
            Prefijo = "SETP",
            Consecutivo = 990000123,
        });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Documento.Counterparty.Name.Should().Be("ANA PÉREZ CORREGIDA");
        r.Value.Documento.Counterparty.PartySnapshotVersion.Should().Be(2);
        r.Value.Documento.Withholdings.Should().ContainSingle();
    }

    [Fact]
    public async Task Una_nota_trae_el_documento_que_corrige_y_su_concepto()
    {
        await using var e = await EscenarioAsync();
        var nota = new InventoryDocument
        {
            Class = DocumentClass.CreditNote, DocumentTypeId = e.Tipo.Id, Prefix = "NC", Number = 7, OperationDate = Fecha.AddDays(1),
            CorrectionConceptCode = "2", Currency = "COP",
        };
        nota.Confirmar(1, new DateTime(2026, 12, 6, 15, 0, 0, DateTimeKind.Utc));
        e.Db.InventoryDocuments.Add(nota);
        await e.Db.SaveChangesAsync();
        e.Db.DocumentLinks.Add(new DocumentLink { SourceDocumentId = e.Factura.Id, TargetDocumentId = nota.Id, Kind = DocumentLinkKind.NoteOf });
        await e.Db.SaveChangesAsync();

        var entrada = (await e.Fuente.LeerAsync(nota.PublicId, default)).Value;

        entrada.Kind.Should().Be(ElectronicDocumentKind.CreditNote);
        entrada.Correccion.Should().Be(new CorreccionDeEntrada(e.Factura.PublicId, "SETP990000123", Fecha, "2"));
    }

    [Fact]
    public async Task Un_borrador_o_una_clase_no_electronica_no_se_leen()
    {
        await using var e = await EscenarioAsync();
        var borrador = new InventoryDocument { Class = DocumentClass.SalesInvoice, DocumentTypeId = e.Tipo.Id, OperationDate = Fecha };
        var ajuste = new InventoryDocument { Class = DocumentClass.PositiveAdjustment, DocumentTypeId = e.Tipo.Id, OperationDate = Fecha };
        ajuste.Confirmar(1, DateTime.UtcNow);
        e.Db.InventoryDocuments.AddRange(borrador, ajuste);
        await e.Db.SaveChangesAsync();

        (await e.Fuente.LeerAsync(borrador.PublicId, default)).IsFailure.Should().BeTrue();
        (await e.Fuente.LeerAsync(ajuste.PublicId, default)).IsFailure.Should().BeTrue();
        (await e.Fuente.LeerAsync(Guid.NewGuid(), default)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task El_borrador_de_reemplazo_es_de_la_misma_clase_precargado_vinculado_y_uno_solo()
    {
        await using var e = await EscenarioAsync();

        var r = await e.Fuente.CrearBorradorDeReemplazoAsync(e.Factura.PublicId, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.SourceModule.Should().Be("INV");
        r.Value.EditRoute.Should().Be($"/api/inventory/sales/invoices/{r.Value.ReplacementDraftPublicId}");
        var borrador = await e.Db.InventoryDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == r.Value.ReplacementDraftPublicId);
        borrador.Class.Should().Be(DocumentClass.SalesInvoice);
        borrador.Status.Should().Be(DocumentStatus.Draft);
        borrador.Number.Should().BeNull("el borrador no consume número");
        borrador.CounterpartyPersonId.Should().Be(e.Factura.CounterpartyPersonId);
        borrador.Lines.Should().ContainSingle().Which.Quantity.Should().Be(2m);
        (await e.Db.DocumentLinks.CountAsync(l => l.SourceDocumentId == e.Factura.Id && l.TargetDocumentId == borrador.Id && l.Kind == DocumentLinkKind.ReplacementOf))
            .Should().Be(1);

        var otra = await e.Fuente.CrearBorradorDeReemplazoAsync(e.Factura.PublicId, default);
        otra.IsFailure.Should().BeTrue();
        otra.Error.Code.Should().Be(ErroresDeFacturacionElectronica.ReplacementDraftExistsCode);
    }

    [Fact]
    public async Task Confirmar_un_reemplazo_exige_el_borrador_vinculado_de_la_misma_clase()
    {
        await using var e = await EscenarioAsync();
        var suelto = new InventoryDocument { Class = DocumentClass.SalesInvoice, DocumentTypeId = e.Tipo.Id, OperationDate = Fecha };
        e.Db.InventoryDocuments.Add(suelto);
        await e.Db.SaveChangesAsync();

        var r = await e.Fuente.ConfirmarReemplazoAsync(e.Factura.PublicId, suelto.PublicId, _ => { }, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeFacturacionElectronica.NotReplacementDraftCode);
    }

    // ------------------------------------------------------------------------------------------ caso a (T722) --

    [Fact]
    public async Task La_contraparte_del_maestro_sale_con_la_version_siguiente_y_registrarla_la_agrega_con_su_motivo()
    {
        await using var e = await EscenarioAsync();
        e.Db.People.Add(new IngenIA365ERP.Domain.Entities.Core.Person
        {
            Id = 42, FirstName = "ANA", LastName = "PÉREZ", TaxId = "16000111", Email = "facturas.ana@correo.co", Status = "A", CreatedBy = "test",
        });
        await e.Db.SaveChangesAsync();

        var r = await e.Fuente.ContraparteDelMaestroAsync(e.Factura.PublicId, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var foto = r.Value!;
        foto.Version.Should().Be(3, "la vigente es la 2");
        foto.TaxId.Should().Be("16000111");
        foto.Email.Should().Be("facturas.ana@correo.co");

        (await e.Fuente.RegistrarVersionDeContraparteAsync(e.Factura.PublicId, foto, "El correo estaba mal", default)).IsSuccess.Should().BeTrue();
        await e.Db.SaveChangesAsync();

        var leida = await e.Fuente.LeerAsync(e.Factura.PublicId, default);
        var vigente = leida.Value.Contrapartes.MaxBy(f => f.Version)!;
        vigente.Should().Be(foto, "la copia registrada se lee igual: el canónico que se reconstruya da los mismos bytes");
        (await e.Db.DocumentPartySnapshots.SingleAsync(s => s.Version == 3)).ChangeReason.Should().Be("El correo estaba mal");

        var otra = () => e.Fuente.RegistrarVersionDeContraparteAsync(e.Factura.PublicId, foto, "otra vez", default);
        await otra.Should().ThrowAsync<InvalidOperationException>("la versión tiene que ser la siguiente a la vigente");
    }

    [Fact]
    public async Task El_permiso_de_confirmar_es_el_de_ventas_o_el_de_compras_segun_el_tipo()
    {
        await using var e = await EscenarioAsync();

        e.Fuente.PermisoDeConfirmar(ElectronicDocumentKind.Invoice).Should().Be("Inventory.Sales.Confirm");
        e.Fuente.PermisoDeConfirmar(ElectronicDocumentKind.PosEquivalent).Should().Be("Inventory.Sales.Confirm");
        e.Fuente.PermisoDeConfirmar(ElectronicDocumentKind.SupportDocument).Should().Be("Inventory.Purchases.Confirm");
        e.Fuente.PermisoDeConfirmar(ElectronicDocumentKind.SupportDocumentAdjustmentNote).Should().Be("Inventory.Purchases.Confirm");
    }

    // ------------------------------------------------------------------------------------------ escenario --

    private sealed class Reloj : IDateTimeService
    {
        public DateTime UtcNow => new(2026, 12, 10, 15, 0, 0, DateTimeKind.Utc);
        public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
    }

    private sealed record Escenario(TestApplicationDbContext Db, FuenteDeEmisionDeInventario Fuente, InventoryDocumentType Tipo, InventoryDocument Factura)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static async Task<Escenario> EscenarioAsync()
    {
        var db = TestDbContextFactory.Create();
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 1, Guid.NewGuid(), Guid.NewGuid(), "Cajera",
            null, ExecutionChannel.Web, "prueba", null, null));
        var fuente = new FuenteDeEmisionDeInventario(db, actor, new Reloj(), Substitute.For<ILectorDeParametros>(), confirmacion: null!);

        var tipo = new InventoryDocumentType { Code = "FV", Name = "Factura", Class = DocumentClass.SalesInvoice, IsActive = true };
        var unidad = new UnitOfMeasure { Code = "UND", Name = "Unidad", DianUnitCode = "94" };
        db.InventoryDocumentTypes.Add(tipo);
        db.UnitsOfMeasure.Add(unidad);
        await db.SaveChangesAsync();
        var producto = new Product { Code = "ARZ-001", Name = "Arroz 500 g", CategoryId = 1, BaseUnitId = unidad.Id };
        db.Products.Add(producto);
        await db.SaveChangesAsync();

        var factura = new InventoryDocument
        {
            Class = DocumentClass.SalesInvoice, DocumentTypeId = tipo.Id, Prefix = "SETP", Number = 990000123, OperationDate = Fecha,
            CounterpartyPersonId = 42, Currency = "COP", BranchId = 1,
            Subtotal = 4200m, DiscountTotal = 210m, TaxTotal = 758.10m, WithholdingTotal = 99.75m, Total = 4748.10m, AmountDue = 4648.35m,
        };
        factura.Lines.Add(new InventoryDocumentLine
        {
            Document = factura, LineNumber = 1, ProductId = producto.Id, UnitId = unidad.Id, Quantity = 2m, UnitPrice = 2100m,
            GrossAmount = 4200m, DiscountAmount = 210m, NetAmount = 3990m,
        });
        factura.Confirmar(1, new DateTime(2026, 12, 5, 15, 14, 22, DateTimeKind.Utc));
        db.InventoryDocuments.Add(factura);
        await db.SaveChangesAsync();

        var lineaId = factura.Lines.Single().Id;
        db.DocumentTaxLines.AddRange(
            new DocumentTaxLine { DocumentId = factura.Id, DocumentLineId = lineaId, TaxRateCode = "IVA19", Treatment = TaxTreatment.Generated, Rate = 0.19m, Base = 3990m, Amount = 758.10m, DianTaxCode = "01" },
            new DocumentTaxLine { DocumentId = factura.Id, TaxRateCode = "RTF25", Treatment = TaxTreatment.WithholdingSuffered, Rate = 0.025m, Base = 3990m, Amount = 99.75m, DianTaxCode = "06" });
        db.DocumentPayments.Add(new DocumentPayment
        {
            DocumentId = factura.Id, LineNumber = 1, MeansCode = "EFE", MeansName = "Efectivo", MeansClass = PaymentMeansClass.Cash,
            DianPaymentMeansCode = "10", Amount = 4648.35m,
        });
        db.DocumentPartySnapshots.AddRange(
            new DocumentPartySnapshot { DocumentId = factura.Id, Version = 1, PersonId = 42, DianOrganizationType = "2", DianIdTypeCode = "13", TaxId = "16000111", LegalName = "ANA PÉREZ" },
            new DocumentPartySnapshot { DocumentId = factura.Id, Version = 2, PersonId = 42, DianOrganizationType = "2", DianIdTypeCode = "13", TaxId = "16000111", LegalName = "ANA PÉREZ CORREGIDA" });
        await db.SaveChangesAsync();

        return new Escenario(db, fuente, tipo, factura);
    }
}
