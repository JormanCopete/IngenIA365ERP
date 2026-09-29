using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canonical;

/// <summary>
/// Feature 012, I4, T676 (contracts/dian.md §4; decisiones-transversales T19, T26, T52): <see cref="ConstructorDelCanonico"/> es
/// el único constructor del canónico. Determinista (mismo JSON y mismo SHA-256), con las escalas y el punto decimal del contrato,
/// la retención del comprador como retención y nunca como pago, la forma de pago crédito con su vencimiento, la contraparte de la
/// copia fiscal de mayor versión, sin resolución en las notas, y <c>ElectronicInvoicing.Document.MissingData</c> con
/// <c>data.missing[] { field, where, permission }</c> cuando falta un código DIAN.
/// </summary>
public class ConstructorDelCanonicoTests
{
    private static readonly DateOnly Fecha = new(2026, 12, 5);

    // ------------------------------------------------------------------------------------------ determinismo --

    [Fact]
    public void Dos_construcciones_del_mismo_documento_dan_el_mismo_JSON_y_el_mismo_SHA256()
    {
        var primera = Construir(Factura());
        var segunda = Construir(Factura() with { Lineas = Factura().Lineas.Reverse().ToList() });

        primera.Json.Should().Be(segunda.Json);
        primera.CanonicalSha256.Should().Be(segunda.CanonicalSha256).And.HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        primera.EconomicFingerprint.Should().Be(segunda.EconomicFingerprint).And.HaveLength(64);
        primera.CanonicalSha256.Should().Be(SerializadorCanonico.Sha256(primera.Json));
    }

    [Fact]
    public void El_JSON_tiene_las_claves_ordenadas_y_la_version_del_esquema()
    {
        var json = Construir(Factura()).Json;
        using var doc = JsonDocument.Parse(json);
        var claves = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        claves.Should().BeInAscendingOrder(StringComparer.Ordinal);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("kind").GetString().Should().Be("Invoice");
        doc.RootElement.GetProperty("dianDocumentTypeCode").GetString().Should().Be("01");
        doc.RootElement.GetProperty("environment").GetString().Should().Be("Testing");
        doc.RootElement.GetProperty("number").GetProperty("full").GetString().Should().Be("SETP990000123");
    }

    [Fact]
    public void Escalas_fijas_y_punto_decimal_aunque_la_cultura_del_proceso_use_coma()
    {
        var anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-CO");
        try
        {
            var json = Construir(Factura()).Json;

            json.Should().Contain("\"quantity\":2.0000");            // 18,4
            json.Should().Contain("\"unitPrice\":2100.000000");      // 18,6
            json.Should().Contain("\"lineExtension\":4200.00");      // 18,2
            json.Should().Contain("\"rate\":0.190000");              // 9,6, fracción
            json.Should().Contain("\"amount\":758.10");
            json.Should().Contain("\"issuedAt\":\"2026-12-05T10:14:22-05:00\"");
            json.Should().NotContain("2,0000").And.NotContain("758,1");
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    // ------------------------------------------------------------------------------------------ totales y retención --

    [Fact]
    public void Payable_es_el_Total_amountDue_el_AmountDue_y_la_retencion_va_en_withholdings_nunca_como_pago()
    {
        var c = Construir(Factura()).Documento;

        c.Totals.LineExtension.Should().Be(4200.00m);
        c.Totals.Allowances.Should().Be(210.00m);
        c.Totals.TaxExclusive.Should().Be(3990.00m);
        c.Totals.Taxes.Should().Be(758.10m);
        c.Totals.TaxInclusive.Should().Be(4748.10m);
        c.Totals.Payable.Should().Be(4748.10m);
        c.Totals.Withholdings.Should().Be(99.75m);
        c.Totals.AmountDue.Should().Be(4648.35m);

        c.Withholdings.Should().ContainSingle().Which.Should().Be(new RetencionCanonica("06", 0.025m, 3990.00m, 99.75m));
        c.Payments.Should().ContainSingle().Which.DianPaymentMeansCode.Should().Be("10");
        c.Payments.Sum(p => p.Amount).Should().Be(4648.35m, "se paga lo que queda después de la retención, y la retención no es un medio");
        c.PaymentForm.Should().Be("Cash");
        c.DueDate.Should().BeNull();
    }

    [Fact]
    public void El_redondeo_del_total_se_informa_en_rounding()
    {
        var entrada = Factura() with { Totales = Factura().Totales with { Total = 4748.00m, AmountDue = 4648.25m } };
        entrada = entrada with { Pagos = [entrada.Pagos[0] with { Amount = 4648.25m }] };

        var c = Construir(entrada).Documento;

        c.Totals.TaxInclusive.Should().Be(4748.10m);
        c.Totals.Payable.Should().Be(4748.00m);
        c.Totals.Rounding.Should().Be(-0.10m);
    }

    // ------------------------------------------------------------------------------------------ forma de pago --

    [Theory]
    [InlineData(PaymentMeansClass.AssociateCredit)]
    [InlineData(PaymentMeansClass.CustomerCredit)]
    public void Un_medio_de_credito_es_forma_Credit_y_exige_el_vencimiento(PaymentMeansClass clase)
    {
        var aCredito = Factura() with { Pagos = [new PagoDeEntrada("CRED", "Crédito", clase, "1", 4648.35m, null)] };

        var sinVencimiento = ConstructorDelCanonico.Construir(aCredito, Contexto());
        sinVencimiento.IsFailure.Should().BeTrue();
        Faltantes(sinVencimiento.Error).Should().Contain(f => f.Field == "dueDate");

        var conVencimiento = Construir(aCredito with { DueDate = Fecha.AddDays(30) }).Documento;
        conVencimiento.PaymentForm.Should().Be("Credit");
        conVencimiento.DueDate.Should().Be(Fecha.AddDays(30));
    }

    // ------------------------------------------------------------------------------------------ contraparte --

    [Fact]
    public void La_contraparte_sale_de_la_copia_fiscal_de_mayor_version()
    {
        var v1 = Foto() with { Version = 1, LegalName = "NOMBRE VIEJO", Email = "viejo@correo.co" };
        var v2 = Foto() with { Version = 2, LegalName = "NOMBRE CORREGIDO", Email = "nuevo@correo.co" };

        var c = Construir(Factura() with { Contrapartes = [v2, v1] }).Documento;

        c.Counterparty.Name.Should().Be("NOMBRE CORREGIDO");
        c.Counterparty.ReceptionEmail.Should().Be("nuevo@correo.co");
        c.Counterparty.PartySnapshotVersion.Should().Be(2);
        c.Counterparty.Role.Should().Be("Buyer");
        c.Counterparty.IsFinalConsumer.Should().BeFalse();
    }

    [Fact]
    public void Las_responsabilidades_y_el_tributo_salen_de_las_marcas_por_el_catalogo()
    {
        var comun = Construir(Factura()).Documento.Counterparty;
        comun.Responsibilities.Should().Equal("R-99-PN");
        comun.TaxSchemeCode.Should().Be("ZZ");

        var grande = Construir(Factura() with { Contrapartes = [Foto() with { IsLargeContributor = true, IsVatWithholdingAgent = true, IsVatResponsible = true }] })
            .Documento.Counterparty;
        grande.Responsibilities.Should().Equal("O-13", "O-23");
        grande.TaxSchemeCode.Should().Be("01");
    }

    [Fact]
    public void El_proveedor_del_documento_soporte_es_Supplier()
    {
        var ds = Factura() with { Kind = ElectronicDocumentKind.SupportDocument, DocumentClass = "SupportDocument" };

        var c = Construir(ds, Contexto() with { Resolucion = Resolucion(ResolutionKind.SupportDocument) }).Documento;

        c.Counterparty.Role.Should().Be("Supplier");
        c.DianDocumentTypeCode.Should().Be("05");
    }

    // ------------------------------------------------------------------------------------------ notas --

    [Fact]
    public void Una_nota_no_lleva_resolucion_y_referencia_el_documento_corregido()
    {
        var nota = Factura() with
        {
            Kind = ElectronicDocumentKind.CreditNote,
            DocumentClass = "CreditNote",
            Correccion = new CorreccionDeEntrada(Guid.NewGuid(), "SETP990000123", Fecha, "2"),
        };
        var contexto = Contexto() with
        {
            Resolucion = Resolucion(ResolutionKind.Invoice),
            Prefijo = "NC",
            Consecutivo = 1,
            Corregido = new DocumentoCorregidoCanonico("SETP990000123", "cufe-de-la-factura", Fecha, null),
        };

        var c = Construir(nota, contexto).Documento;

        c.Resolution.Should().BeNull("las notas no tienen resolución, aunque llegue una");
        c.DianDocumentTypeCode.Should().Be("91");
        c.References.Corrected.Should().Be(new DocumentoCorregidoCanonico("SETP990000123", "cufe-de-la-factura", Fecha, "2"));
    }

    /// <summary>
    /// I6, T888 (contracts/dian.md, fila <c>DebitNote</c>): la nota débito es el tipo 92, sin resolución (consecutivo propio, T16), con la
    /// referencia a la factura que corrige —número, CUFE y fecha— y su concepto de corrección de nota débito.
    /// </summary>
    [Fact]
    public void La_nota_debito_es_el_tipo_92_y_referencia_la_factura_con_su_concepto()
    {
        var nota = Factura() with
        {
            Kind = ElectronicDocumentKind.DebitNote,
            DocumentClass = "DebitNote",
            Correccion = new CorreccionDeEntrada(Guid.NewGuid(), "SETP990000123", Fecha, "1"),
        };
        var contexto = Contexto() with
        {
            Resolucion = Resolucion(ResolutionKind.Invoice),
            Prefijo = "NDV",
            Consecutivo = 1,
            Corregido = new DocumentoCorregidoCanonico("SETP990000123", "cufe-de-la-factura", Fecha, null),
        };

        var c = Construir(nota, contexto).Documento;

        c.Resolution.Should().BeNull();
        c.DianDocumentTypeCode.Should().Be("92");
        c.References.Corrected.Should().Be(new DocumentoCorregidoCanonico("SETP990000123", "cufe-de-la-factura", Fecha, "1"));
    }

    [Fact]
    public void Una_nota_sin_concepto_de_correccion_es_un_dato_faltante()
    {
        var nota = Factura() with
        {
            Kind = ElectronicDocumentKind.CreditNote,
            DocumentClass = "CreditNote",
            Correccion = new CorreccionDeEntrada(Guid.NewGuid(), "SETP990000123", Fecha, null),
        };

        var r = ConstructorDelCanonico.Construir(nota, Contexto() with { Resolucion = null, Prefijo = "NC", Consecutivo = 1 });

        r.IsFailure.Should().BeTrue();
        Faltantes(r.Error).Should().Contain(f => f.Field == "references.corrected.correctionConceptCode");
    }

    [Fact]
    public void Una_factura_sin_resolucion_es_un_dato_faltante()
    {
        var r = ConstructorDelCanonico.Construir(Factura(), Contexto() with { Resolucion = null });

        r.IsFailure.Should().BeTrue();
        Faltantes(r.Error).Should().Contain(f => f.Field == "resolution" && f.Permission == "ElectronicInvoicing.Resolutions.Manage");
    }

    // ------------------------------------------------------------------------------------------ contingencias --

    [Fact]
    public void La_contingencia_cambia_el_tipo_de_documento_y_la_03_lleva_su_bloque()
    {
        Construir(Factura(), Contexto() with { Contingencia = ContingencyType.Dian04 }).Documento.DianDocumentTypeCode.Should().Be("04");

        var papel = Construir(Factura(), Contexto() with { Contingencia = ContingencyType.Issuer03 }).Documento;
        papel.DianDocumentTypeCode.Should().Be("03");
        papel.Contingency.Should().NotBeNull();
        papel.Contingency!.Type.Should().Be("Issuer03");
        papel.Contingency.PaperNumber.Should().Be("SETP990000123");
    }

    // ------------------------------------------------------------------------------------------ datos faltantes --

    [Fact]
    public void Falta_el_codigo_DIAN_de_la_unidad_y_del_medio_de_pago()
    {
        var entrada = Factura() with
        {
            Lineas = [Factura().Lineas[0] with { UnitCode = "KILO", DianUnitCode = null }],
            Pagos = [Factura().Pagos[0] with { DianPaymentMeansCode = "" }],
        };

        var r = ConstructorDelCanonico.Construir(entrada, Contexto());

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeFacturacionElectronica.MissingDataCode);
        var faltantes = Faltantes(r.Error);
        faltantes.Should().Contain(f => f.Field == "lines[1].unitCode" && f.Permission == "Inventory.Catalog.Manage" && f.Where.Contains("KILO"));
        faltantes.Should().Contain(f => f.Field == "payments[1].dianPaymentMeansCode" && f.Permission == "Core.PaymentMeans.Manage");
        faltantes.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.Where));
    }

    [Fact]
    public void Un_codigo_que_no_esta_en_el_catalogo_vigente_tambien_falta()
    {
        var entrada = Factura() with { Lineas = [Factura().Lineas[0] with { DianUnitCode = "XYZ" }] };

        var r = ConstructorDelCanonico.Construir(entrada, Contexto());

        Faltantes(r.Error).Should().Contain(f => f.Field == "lines[1].unitCode");
    }

    [Fact]
    public void Falta_el_tributo_DIAN_de_un_impuesto()
    {
        var entrada = Factura() with { Impuestos = [Factura().Impuestos[0] with { DianTaxCode = null }, Factura().Impuestos[1]] };

        var r = ConstructorDelCanonico.Construir(entrada, Contexto());

        Faltantes(r.Error).Should().Contain(f => f.Field == "lines[1].taxes.dianTaxCode" && f.Permission == "Core.Taxes.Manage");
    }

    // ------------------------------------------------------------------------------------------ huella --

    [Fact]
    public void La_huella_economica_es_la_de_Domain_sobre_la_vista_fiscal_del_canonico()
    {
        var construido = Construir(Factura());

        construido.EconomicFingerprint.Should().Be(HuellaEconomica.Calcular(ConstructorDelCanonico.DatosFiscales(construido.Documento)));

        var otraDireccion = Construir(Factura() with { Contrapartes = [Foto() with { Address = "OTRA 1-2" }] });
        otraDireccion.EconomicFingerprint.Should().Be(construido.EconomicFingerprint, "la dirección no es económica");
        otraDireccion.CanonicalSha256.Should().NotBe(construido.CanonicalSha256);
    }

    [Fact]
    public void El_serializador_lee_lo_que_escribe()
    {
        var construido = Construir(Factura());

        var leido = SerializadorCanonico.Leer(construido.Json)!;

        SerializadorCanonico.Serializar(leido).Should().Be(construido.Json);
    }

    // ------------------------------------------------------------------------------------------ datos --

    private static CanonicoConstruido Construir(EntradaDeDocumentoElectronico entrada, ContextoDelCanonico? contexto = null)
    {
        var r = ConstructorDelCanonico.Construir(entrada, contexto ?? Contexto());
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    private static IReadOnlyList<DatoFaltante> Faltantes(Error error)
    {
        error.Code.Should().Be(ErroresDeFacturacionElectronica.MissingDataCode);
        var datos = JsonSerializer.SerializeToElement(((ErrorConDatos)error).Data);
        return datos.GetProperty("missing").EnumerateArray()
            .Select(m => new DatoFaltante(
                m.GetProperty("field").GetString()!,
                m.GetProperty("where").GetString()!,
                m.GetProperty("permission").ValueKind == JsonValueKind.Null ? null : m.GetProperty("permission").GetString(),
                m.GetProperty("message").GetString()!))
            .ToList();
    }

    private static FotoFiscalDeEntrada Foto() => new()
    {
        Version = 1,
        OrganizationType = "2",
        IdTypeCode = "13",
        TaxId = "16000111",
        LegalName = "ANA PÉREZ",
        Address = "CALLE 5 # 10-20",
        MunicipalityDaneCode = "76001",
        Email = "ana@correo.co",
    };

    /// <summary>La factura del ejemplo de contracts/dian.md §4.2: 2 × 2.100, 5 % de descuento, IVA 19 % y retención en la fuente 2,5 %.</summary>
    private static EntradaDeDocumentoElectronico Factura() => new()
    {
        SourceModule = "INV",
        DocumentPublicId = new Guid("7d0b1a8e-0000-4000-8000-000000000001"),
        DocumentClass = "SalesInvoice",
        DocumentTypeCode = "FV",
        DocumentNumber = "SETP990000123",
        Kind = ElectronicDocumentKind.Invoice,
        OperationDate = Fecha,
        ConfirmedAtUtc = new DateTime(2026, 12, 5, 15, 14, 22, DateTimeKind.Utc),
        Currency = "COP",
        ExchangeRate = 1m,
        Contrapartes = [Foto()],
        Lineas =
        [
            new LineaDeEntrada(1, "ARZ-001", "ARROZ 500 G", 2m, "UND", "94", 2100m, 4200m, 210m),
            new LineaDeEntrada(2, "SAL-001", "SAL 1 KG", 1m, "UND", "94", 0m, 0m, 0m),
        ],
        Impuestos =
        [
            new ImpuestoDeEntrada(1, "IVA19", "01", false, 0.19m, null, null, 3990m, 758.10m),
            new ImpuestoDeEntrada(null, "RTF25", "06", true, 0.025m, null, null, 3990m, 99.75m),
        ],
        Pagos = [new PagoDeEntrada("EFE", "Efectivo", PaymentMeansClass.Cash, "10", 4648.35m, null)],
        Totales = new TotalesDeEntrada(4200m, 210m, 758.10m, 99.75m, 4748.10m, 4648.35m),
    };

    private static DianNumberingResolution Resolucion(ResolutionKind tipo) => new()
    {
        Kind = tipo,
        ResolutionNumber = "18760000001",
        ResolutionDate = new DateOnly(2026, 10, 1),
        Prefix = "SETP",
        RangeFrom = 990000000,
        RangeTo = 995000000,
        ValidFrom = new DateOnly(2026, 10, 1),
        ValidTo = new DateOnly(2027, 10, 1),
        Environment = DianEnvironment.Testing,
    };

    private static ContextoDelCanonico Contexto() => new()
    {
        Configuracion = new ElectronicEmissionSetting
        {
            Mode = EmissionMode.TechnologyProvider,
            ChannelCode = "SIMULADO",
            Environment = DianEnvironment.Testing,
            IssuerTaxId = "890300001",
            IssuerCheckDigit = "3",
            IssuerBusinessName = "COOPERATIVA DE PRUEBA",
            IssuerAddress = "CRA 1 # 2-3",
            IssuerMunicipalityDaneCode = "76001",
            IssuerEmail = "facturacion@coop.co",
            ValidFrom = new DateOnly(2026, 10, 1),
        },
        Resolucion = Resolucion(ResolutionKind.Invoice),
        Prefijo = "SETP",
        Consecutivo = 990000123,
        Emisor = new CondicionTributariaDelEmisor(ResponsableDeIva: true, GranContribuyente: false, AgenteDeRetencionIva: false, Autorretenedor: false),
    };
}
