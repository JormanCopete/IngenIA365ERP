using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I4, T734, T735, T738 (decisiones-transversales §1.3; contracts/dian.md §6.1, §7.2, §9; api.md §18.2, §18.3; FR-038, FR-058,
/// FR-063, FR-066, FR-067): el flujo fiscal de la confirmación. La factura electrónica numera con su resolución y deja su documento electrónico en
/// <c>Pending</c> con la versión 1 cuyo SHA-256 es el del canónico que la emisión volverá a armar, y el canónico queda para después del commit;
/// sin configuración no confirma; con la contingencia 03 abierta, en oficina el tipo normal nombra el de contingencia y éste numera con la
/// resolución de contingencia en <c>IssuerContingency</c>; un tipo de contingencia sin 03 abierta no confirma; un documento numerado no se
/// renumera; la nota corrige el documento electrónico del original y espera al que no está validado; y anular o hacer nota sobre un original
/// enviado sin respuesta o rechazado no procede.
/// </summary>
public class VentaElectronicaTests
{
    [Fact]
    public async Task La_factura_numera_con_su_resolucion_y_registra_su_documento_electronico_con_el_canonico_que_se_reconstruira()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();

        var (venta, r) = await e.FacturaAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var documento = e.V.Documento(venta);
        documento.Prefix.Should().Be("SETP");
        documento.Number.Should().Be(1, "el primero de la resolución 1..100");
        e.Db.DianNumberingResolutions.Single(x => x.Id == e.Resolucion.Id).LastIssuedNumber.Should().Be(1);

        var electronico = e.Electronico(venta);
        electronico.Status.Should().Be(ElectronicDocumentStatus.Pending);
        electronico.Kind.Should().Be(ElectronicDocumentKind.Invoice);
        electronico.Number.Should().Be("SETP1");
        electronico.ChannelCode.Should().Be(GuardiaDeEmisionFiscal.CanalSimulado);
        electronico.ResolutionId.Should().Be(e.Resolucion.Id);
        electronico.Versions.Should().ContainSingle().Which.VersionNumber.Should().Be(1);

        // La emisión vuelve a armar el canónico por la fuente y lo sellado: los mismos bytes.
        var reconstruccion = new ReconstruccionDelCanonico(e.Db, [e.Fuente()], new Application.ElectronicInvoicing.Canonical.ConstructorDelCanonico(e.V.K.Lector()));
        var otra = await reconstruccion.ReconstruirAsync(electronico, electronico.Versions.Single(), default);
        otra.IsSuccess.Should().BeTrue();
        otra.Value.CanonicalSha256.Should().Be(electronico.Versions.Single().CanonicalSha256);

        e.Tareas.HayPendientes.Should().BeTrue("el canónico se sube después del commit");
        e.Fiscal.RegistradoPara(venta).Should().NotBeNull();

        var (segunda, r2) = await e.FacturaAsync();
        r2.IsSuccess.Should().BeTrue();
        e.V.Documento(segunda).Number.Should().Be(2);
    }

    [Fact]
    public async Task Sin_configuracion_vigente_la_factura_no_confirma_y_no_consume_numero()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        e.Db.ElectronicEmissionSettings.Remove(e.Configuracion);
        await e.Db.SaveChangesAsync();

        var (venta, r) = await e.FacturaAsync();

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeVentas.NotReadyCode);
        e.V.Documento(venta).Number.Should().BeNull();
        e.Db.ElectronicDocuments.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_dato_DIAN_que_falta_no_confirma_con_MissingData()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        foreach (var u in e.Db.UnitsOfMeasure) u.DianUnitCode = null;
        await e.Db.SaveChangesAsync();

        var (_, r) = await e.FacturaAsync();

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("ElectronicInvoicing.Document.MissingData");
    }

    [Fact]
    public async Task Con_la_contingencia_03_abierta_en_oficina_el_tipo_normal_nombra_el_de_contingencia()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        e.NuevaResolucion(ResolutionKind.Contingency, "CFE", ResolutionKind.Invoice);
        e.Db.InventoryDocumentTypes.Add(new InventoryDocumentType
        {
            Code = "FVC", Name = "Factura de contingencia", Class = DocumentClass.SalesInvoice, FiscalPrefix = "CFE", IsContingency = true, IsActive = true,
            AllWarehouses = true,
        });
        await e.Db.SaveChangesAsync();
        e.Contingencia03();

        var (normal, r) = await e.FacturaAsync();
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeVentas.NotReadyCode);
        r.Error.Message.Should().Contain("FVC").And.Contain("CFE");
        e.V.Documento(normal).Number.Should().BeNull();

        var (contingencia, rc) = await e.FacturaAsync("FVC");
        rc.IsSuccess.Should().BeTrue(rc.IsFailure ? $"{rc.Error.Code}: {rc.Error.Message}" : null);
        e.V.Documento(contingencia).Prefix.Should().Be("CFE");
        var electronico = e.Electronico(contingencia);
        electronico.Status.Should().Be(ElectronicDocumentStatus.IssuerContingency);
        electronico.ContingencyType.Should().Be(ContingencyType.Issuer03);
        electronico.ContingencyEventId.Should().NotBeNull();
    }

    [Fact]
    public async Task Un_tipo_de_contingencia_sin_la_03_abierta_no_confirma()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        e.NuevaResolucion(ResolutionKind.Contingency, "CFE", ResolutionKind.Invoice);
        e.Db.InventoryDocumentTypes.Add(new InventoryDocumentType
        {
            Code = "FVC", Name = "Factura de contingencia", Class = DocumentClass.SalesInvoice, FiscalPrefix = "CFE", IsContingency = true, IsActive = true,
            AllWarehouses = true,
        });
        await e.Db.SaveChangesAsync();

        var (venta, r) = await e.FacturaAsync("FVC");

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().BeOneOf(ErroresDeDocumentosElectronicos.ContingencyNotOpenCode, ErroresDeVentas.NotReadyCode);
        e.V.Documento(venta).Number.Should().BeNull();
    }

    [Fact]
    public async Task Un_documento_ya_numerado_no_se_renumera()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        var paso = e.NuevoPasoFiscal();
        var documento = new InventoryDocument { Class = DocumentClass.SalesInvoice, Prefix = "SETP", Number = 7, OperationDate = VentaElectronicaDePrueba.Hoy };
        var tipo = e.Db.InventoryDocumentTypes.Single(t => t.Code == "FV");

        var r = await paso.NumerarAsync(new Application.Inventory.Documents.Efectos.ContextoDeEfecto(documento, tipo,
            ClasesDeDocumento.De(DocumentClass.SalesInvoice)), default);

        r.IsSuccess.Should().BeTrue();
        documento.Number.Should().Be(7);
        e.Db.DianNumberingResolutions.Single(x => x.Id == e.Resolucion.Id).LastIssuedNumber.Should().Be(0, "no consumió de la resolución");
    }

    // ------------------------------------------------------------------------------------------------ T738 --

    [Theory]
    [InlineData(ElectronicDocumentStatus.Sent, "ElectronicInvoicing.Document.AwaitingResponse")]
    [InlineData(ElectronicDocumentStatus.Rejected, ErroresDeVentas.FiscalUseCorrectionCode)]
    [InlineData(ElectronicDocumentStatus.Validated, ErroresDeVentas.FiscalUseCorrectionCode)]
    [InlineData(ElectronicDocumentStatus.IssuerContingency, ErroresDeVentas.FiscalUseCorrectionCode)]
    public async Task Anular_una_factura_electronica_depende_de_su_estado(ElectronicDocumentStatus estado, string codigo)
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        var (venta, r) = await e.FacturaAsync();
        r.IsSuccess.Should().BeTrue();
        e.Estado(venta, estado);

        var anulada = await new VoidInventoryDocumentCommandHandler(e.Db, e.V.K.Actor, e.V.Compras.C.Reloj, e.V.K.Vista(), e.Confirmacion())
            .Handle(new VoidInventoryDocumentCommand(venta, DocumentClassGroup.Sales, "error"), default);

        anulada.IsFailure.Should().BeTrue();
        anulada.Error.Code.Should().Be(codigo);
        var datos = System.Text.Json.JsonSerializer.Serialize(((ErrorConDatos)anulada.Error).Data);
        if (estado == ElectronicDocumentStatus.Rejected) datos.Should().Contain("/api/electronic-invoicing/documents/");
        else if (estado != ElectronicDocumentStatus.Sent) datos.Should().Contain("\"totalVoid\":true").And.Contain(ErroresDeVentas.RutaDeNotas);
    }

    [Theory]
    [InlineData(ElectronicDocumentStatus.Sent, "ElectronicInvoicing.Document.AwaitingResponse")]
    [InlineData(ElectronicDocumentStatus.Rejected, ErroresDeVentas.FiscalUseCorrectionCode)]
    public async Task La_nota_sobre_un_original_sin_respuesta_o_rechazado_no_confirma(ElectronicDocumentStatus estado, string codigo)
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        var (venta, r) = await e.FacturaAsync();
        r.IsSuccess.Should().BeTrue();
        var nota = await NotaTotalAsync(e, venta);
        e.Estado(venta, estado);

        var confirmada = await e.ConfirmarAsync(nota);

        confirmada.IsFailure.Should().BeTrue();
        confirmada.Error.Code.Should().Be(codigo);
    }

    [Fact]
    public async Task La_nota_corrige_el_documento_electronico_del_original_validado_y_espera_al_que_esta_en_contingencia()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        var (validada, r1) = await e.FacturaAsync();
        var (enContingencia, r2) = await e.FacturaAsync();
        r1.IsSuccess.Should().BeTrue();
        r2.IsSuccess.Should().BeTrue();
        e.Estado(validada, ElectronicDocumentStatus.Validated, new string('a', 96));
        e.Estado(enContingencia, ElectronicDocumentStatus.DianContingency);

        var nota1 = await NotaTotalAsync(e, validada);
        var c1 = await e.ConfirmarAsync(nota1);
        c1.IsSuccess.Should().BeTrue(c1.IsFailure ? $"{c1.Error.Code}: {c1.Error.Message}" : null);
        var electronica1 = e.Electronico(nota1);
        electronica1.Kind.Should().Be(ElectronicDocumentKind.CreditNote);
        electronica1.ResolutionId.Should().BeNull("la nota numera con su consecutivo");
        electronica1.CorrectsDocumentId.Should().Be(e.Electronico(validada).Id);
        electronica1.WaitsForDocumentId.Should().BeNull();

        var nota2 = await NotaTotalAsync(e, enContingencia);
        var c2 = await e.ConfirmarAsync(nota2);
        c2.IsSuccess.Should().BeTrue(c2.IsFailure ? $"{c2.Error.Code}: {c2.Error.Message}" : null);
        var electronica2 = e.Electronico(nota2);
        electronica2.CorrectsDocumentId.Should().Be(e.Electronico(enContingencia).Id);
        electronica2.WaitsForDocumentId.Should().Be(e.Electronico(enContingencia).Id, "su transmisión espera a la del original");
    }

    private static async Task<Guid> NotaTotalAsync(VentaElectronicaDePrueba e, Guid venta)
    {
        var borrador = await e.V.NotaAsync(new CreditNoteDraftInput(venta, "devolución", TotalVoid: true, WithReturn: false, [], [],
            CorrectionConceptCode: "2"));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? $"{borrador.Error.Code}: {borrador.Error.Message}" : null);
        return borrador.Value.PublicId;
    }
}
