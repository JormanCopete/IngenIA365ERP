using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing;

/// <summary>
/// Feature 012, I4, T678 (contracts/dian.md §10.4; api.md §24.3): un caso por cada motivo <c>Blocked</c> con su código
/// <c>ElectronicInvoicing.Readiness.*</c>; <c>NonElectronic</c> sólo sin obligación y con clase de venta; el documento soporte no depende de
/// <c>Dian.ObligadaAFacturar</c>; <c>SIMULADO</c> en producción bloquea; un tipo que el canal no declara bloquea; y nunca <c>Blocked</c> sin
/// motivos que digan dónde se arregla y con qué permiso.
/// </summary>
public class GuardiaDeEmisionFiscalTests
{
    private static readonly DateOnly Hoy = EmisionDePrueba.Hoy;

    private readonly EmisionDePrueba _e = new();

    private InventoryDocumentType Tipo(DocumentClass clase, string? prefijo = null, bool contingencia = false)
    {
        var codigo = $"T{(int)clase}{(contingencia ? "C" : "")}";
        var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, FiscalPrefix = prefijo, IsContingency = contingencia, IsActive = true };
        _e.Db.InventoryDocumentTypes.Add(tipo);
        _e.Db.SaveChanges();
        return tipo;
    }

    private Task<EvaluacionFiscal> Evaluar(InventoryDocumentType tipo, CashRegister? caja = null, EntregaDelComercio entrega = EntregaDelComercio.I4) =>
        _e.Guardia(entrega).EvaluarAsync(Hoy, tipo, caja, default);

    /// <summary>Lista para emitir factura con el canal simulado.</summary>
    private InventoryDocumentType ListaParaFacturar()
    {
        _e.Obligada(true);
        _e.Configuracion();
        _e.Resolucion(ResolutionKind.Invoice, "SETP");
        return Tipo(DocumentClass.SalesInvoice, "SETP");
    }

    private static void Bloqueada(EvaluacionFiscal ev, string codigo)
    {
        ev.Veredicto.Should().Be(VeredictoFiscal.Blocked);
        ev.Motivos.Select(m => m.Code).Should().Contain(codigo);
        ev.Motivos.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Page) && !string.IsNullOrWhiteSpace(m.Permission)
            && !string.IsNullOrWhiteSpace(m.Message), "nunca Blocked sin decir dónde se arregla y con qué permiso");
        ev.Admite(DocumentClass.SalesInvoice).Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------ veredictos --

    [Fact]
    public async Task Lista_responde_Electronic_con_el_canal_y_la_resolucion()
    {
        var tipo = ListaParaFacturar();

        var ev = await Evaluar(tipo);

        ev.Veredicto.Should().Be(VeredictoFiscal.Electronic);
        ev.Motivos.Should().BeEmpty();
        ev.Canal.Should().Be(GuardiaDeEmisionFiscal.CanalSimulado);
        ev.Resolucion.Should().NotBeNullOrEmpty();
        ev.Admite(DocumentClass.SalesInvoice).Should().BeTrue();
        ev.Admite(DocumentClass.NonElectronicSalesReceipt).Should().BeFalse("una obligada no confirma el comprobante no electrónico");
    }

    [Fact]
    public async Task Las_notas_no_necesitan_resolucion()
    {
        _e.Obligada(true);
        _e.Configuracion();

        var ev = await Evaluar(Tipo(DocumentClass.CreditNote));

        ev.Veredicto.Should().Be(VeredictoFiscal.Electronic);
    }

    [Fact]
    public async Task Sin_obligacion_y_clase_de_venta_responde_NonElectronic()
    {
        _e.Obligada(false);

        (await Evaluar(Tipo(DocumentClass.NonElectronicSalesReceipt))).Veredicto.Should().Be(VeredictoFiscal.NonElectronic);
        var electronica = await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP"));
        electronica.Veredicto.Should().Be(VeredictoFiscal.NonElectronic);
        electronica.Admite(DocumentClass.SalesInvoice).Should().BeFalse("la clase no corresponde: la venta responde FiscalClassMismatch");
    }

    [Fact]
    public async Task Obligada_con_un_tipo_no_electronico_bloquea_con_ClassNotAllowed()
    {
        _e.Obligada(true);

        Bloqueada(await Evaluar(Tipo(DocumentClass.NonElectronicSalesReceipt)), GuardiaDeEmisionFiscal.ClassNotAllowedCode);
    }

    [Fact]
    public async Task Mientras_la_entrega_vigente_sea_I3_una_obligada_queda_bloqueada_por_I4NotActive()
    {
        var tipo = ListaParaFacturar();

        var ev = await Evaluar(tipo, entrega: EntregaDelComercio.I3);

        Bloqueada(ev, GuardiaDeEmisionFiscal.ObligadaSinI4Code);
    }

    [Fact]
    public async Task El_documento_soporte_no_depende_de_la_obligacion()
    {
        _e.Obligada(false);
        var ds = Tipo(DocumentClass.SupportDocument, "DS");

        Bloqueada(await Evaluar(ds), GuardiaDeEmisionFiscal.NoSettingsCode);

        _e.Configuracion();
        _e.Resolucion(ResolutionKind.SupportDocument, "DS");
        (await Evaluar(ds)).Veredicto.Should().Be(VeredictoFiscal.Electronic, "sin obligación de facturar, el DS igual se emite");
    }

    // ------------------------------------------------------------------------------- un caso por motivo --

    [Fact]
    public async Task Sin_configuracion_vigente_NoSettings()
    {
        _e.Obligada(true);
        _e.Configuracion(desde: new DateOnly(2027, 1, 1));

        Bloqueada(await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP")), GuardiaDeEmisionFiscal.NoSettingsCode);
    }

    [Fact]
    public async Task Emision_desactivada_Disabled()
    {
        _e.Obligada(true);
        _e.Configuracion(ajuste: s => s.IsEnabled = false);
        _e.Resolucion(ResolutionKind.Invoice, "SETP");

        Bloqueada(await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP")), GuardiaDeEmisionFiscal.DisabledCode);
    }

    [Fact]
    public async Task Credencial_sin_verificar_CredentialNotVerified()
    {
        _e.Obligada(true);
        _e.Configuracion(ajuste: s => s.CredentialVerifiedAt = null);
        _e.Resolucion(ResolutionKind.Invoice, "SETP");

        Bloqueada(await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP")), GuardiaDeEmisionFiscal.CredentialNotVerifiedCode);
    }

    [Fact]
    public async Task Simulado_en_produccion_bloquea()
    {
        _e.Obligada(true);
        _e.Configuracion(ambiente: DianEnvironment.Production);
        _e.Resolucion(ResolutionKind.Invoice, "SETP", ambiente: DianEnvironment.Production);

        Bloqueada(await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP")), GuardiaDeEmisionFiscal.SimulatedInProductionCode);
    }

    [Fact]
    public async Task Canal_sin_adaptador_en_la_instalacion_ChannelUnknown()
    {
        _e.Obligada(true);
        _e.Configuracion(canal: "RETIRADO");
        _e.Resolucion(ResolutionKind.Invoice, "SETP", canal: "RETIRADO");

        Bloqueada(await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP")), GuardiaDeEmisionFiscal.ChannelUnknownCode);
        Bloqueada(await _e.Guardia(conCanales: false).EvaluarAsync(Hoy, Tipo(DocumentClass.CreditNote), null, default),
            GuardiaDeEmisionFiscal.ChannelUnknownCode);
    }

    [Fact]
    public async Task Tipo_que_el_canal_no_declara_DocumentTypeNotSupported()
    {
        var sinPos = new CanalDePrueba("SINPOS", CanalDePrueba.Completas(sin: ElectronicDocumentKind.PosEquivalent));
        var e = new EmisionDePrueba(sinPos);
        e.Obligada(true);
        e.Configuracion(canal: "SINPOS");
        e.Resolucion(ResolutionKind.PosEquivalent, "POS", canal: "SINPOS");
        var tipo = new InventoryDocumentType { Code = "DEP", Name = "DEP", Class = DocumentClass.PosEquivalentDocument, FiscalPrefix = "POS" };

        var ev = await e.Guardia().EvaluarAsync(Hoy, tipo, null, default);

        Bloqueada(ev, GuardiaDeEmisionFiscal.DocumentTypeNotSupportedCode);
    }

    [Fact]
    public async Task Produccion_con_software_propio_sin_set_de_pruebas_TestSetPending()
    {
        _e.Obligada(true);
        _e.Configuracion(canal: "PROV", ambiente: DianEnvironment.Production,
            ajuste: s => { s.Mode = EmissionMode.OwnSoftware; s.SoftwareId = "sw-1"; s.TestSetAcceptedAt = null; });
        _e.Resolucion(ResolutionKind.Invoice, "FE", canal: "PROV", ambiente: DianEnvironment.Production);

        var ev = await Evaluar(Tipo(DocumentClass.SalesInvoice, "FE"));

        Bloqueada(ev, GuardiaDeEmisionFiscal.TestSetPendingCode);
        ev.Motivos.Should().ContainSingle("con el set aceptado sería lo único que falta");
    }

    [Fact]
    public async Task Faltan_datos_del_emisor_CompanyDataIncomplete()
    {
        _e.Obligada(true);
        _e.Configuracion(ajuste: s => s.IssuerMunicipalityDaneCode = "");
        _e.Resolucion(ResolutionKind.Invoice, "SETP");

        Bloqueada(await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP")), GuardiaDeEmisionFiscal.CompanyDataIncompleteCode);
    }

    [Fact]
    public async Task Sin_resolucion_vencida_o_agotada_NoResolution()
    {
        _e.Obligada(true);
        _e.Configuracion();
        _e.Resolucion(ResolutionKind.Invoice, "VEN", validaHasta: new DateOnly(2026, 11, 30));
        _e.Resolucion(ResolutionKind.Invoice, "AGO", desde: 1, hasta: 1);

        Bloqueada(await Evaluar(Tipo(DocumentClass.SalesInvoice, "NADA")), GuardiaDeEmisionFiscal.NoResolutionCode);
        var vencida = await Evaluar(Tipo(DocumentClass.SalesInvoiceFromShipments, "VEN"));
        Bloqueada(vencida, GuardiaDeEmisionFiscal.NoResolutionCode);
        vencida.Motivos.Single().Message.Should().Contain("vencida");
        // Agotarla: el único escritor es el numerador.
        await new IngenIA365ERP.Application.ElectronicInvoicing.Numeracion.NumeradorFiscal(_e.Db,
                NSubstitute.Substitute.For<IngenIA365ERP.Application.Inventory.Common.ICerrojoDeInventario>())
            .NumerarAsync(new(ElectronicDocumentKind.Invoice, "AGO", DianEnvironment.Testing, GuardiaDeEmisionFiscal.CanalSimulado, null, Hoy));
        await _e.Db.SaveChangesAsync(); // la confirmación guarda el número junto con el documento
        var ev = await _e.Guardia().EvaluarAsync(Hoy, new InventoryDocumentType { Code = "X", Class = DocumentClass.SalesInvoice, FiscalPrefix = "AGO" }, null, default);
        Bloqueada(ev, GuardiaDeEmisionFiscal.NoResolutionCode);
        ev.Motivos.Single().Message.Should().Contain("agotó");
    }

    [Fact]
    public async Task Resolucion_no_asociada_al_canal_ResolutionNotLinked()
    {
        _e.Obligada(true);
        _e.Configuracion();
        _e.Resolucion(ResolutionKind.Invoice, "SETP", canal: "PROV");

        Bloqueada(await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP")), GuardiaDeEmisionFiscal.ResolutionNotLinkedCode);
    }

    [Fact]
    public async Task Contingencia_03_abierta_sin_resolucion_de_contingencia_NoContingencyResolution()
    {
        var tipo = ListaParaFacturar();
        _e.Contingencia03();

        Bloqueada(await Evaluar(tipo), GuardiaDeEmisionFiscal.NoContingencyResolutionCode);

        _e.Resolucion(ResolutionKind.Contingency, "CFE", respalda: ResolutionKind.Invoice);
        (await Evaluar(tipo)).Veredicto.Should().Be(VeredictoFiscal.Electronic, "con la de contingencia que respalda a la factura, sale");
    }

    [Fact]
    public async Task Contingencia_03_abierta_y_la_caja_sin_el_tipo_de_contingencia_DocumentTypeMissing()
    {
        _e.Obligada(true);
        _e.Configuracion();
        _e.Resolucion(ResolutionKind.PosEquivalent, "POS");
        _e.Resolucion(ResolutionKind.Contingency, "CPOS", respalda: ResolutionKind.PosEquivalent);
        _e.Contingencia03();
        var venta = Tipo(DocumentClass.PosEquivalentDocument, "POS");
        var caja = new CashRegister { Code = "C1", Name = "Caja 1", PointOfSaleId = 1, WarehouseId = 1 };
        _e.Db.CashRegisters.Add(caja);
        _e.Db.SaveChanges();
        _e.Db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType { CashRegisterId = caja.Id, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = venta.Id });
        _e.Db.SaveChanges();

        Bloqueada(await Evaluar(venta, caja), GuardiaDeEmisionFiscal.DocumentTypeMissingCode);

        var contingencia = Tipo(DocumentClass.PosEquivalentDocument, "CPOS", contingencia: true);
        _e.Db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType
        {
            CashRegisterId = caja.Id, Role = CashRegisterDocumentRole.PosSaleContingency, DocumentTypeId = contingencia.Id,
        });
        _e.Db.SaveChanges();
        (await Evaluar(venta, caja)).Veredicto.Should().Be(VeredictoFiscal.Electronic);
        (await Evaluar(contingencia, caja)).Veredicto.Should().Be(VeredictoFiscal.Electronic, "el tipo de contingencia numera con la resolución Contingency");
    }

    [Fact]
    public async Task Varios_motivos_se_listan_juntos()
    {
        _e.Obligada(true);
        _e.Configuracion(ajuste: s => { s.IsEnabled = false; s.CredentialVerifiedAt = null; });

        var ev = await Evaluar(Tipo(DocumentClass.SalesInvoice, "SETP"));

        ev.Motivos.Select(m => m.Code).Should().Contain([GuardiaDeEmisionFiscal.DisabledCode, GuardiaDeEmisionFiscal.CredentialNotVerifiedCode,
            GuardiaDeEmisionFiscal.NoResolutionCode]);
    }
}
