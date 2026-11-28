using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Channels.Conecta;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales.Conecta;

/// <summary>Lo común de las pruebas del canal de Conecta contra el stub.</summary>
internal static class ConectaDePrueba
{
    public static readonly Guid Tenant = Guid.Parse("266f0ee4-20b4-4676-8d76-946ade8e62b4");

    public static ElectronicInvoicingOptions Opciones(bool aceptaNumero = true)
    {
        var o = new ElectronicInvoicingOptions();
        foreach (var codigo in new[] { "CONECTA-PRUEBAS", "CONECTA" })
            o.Channels[codigo] = new OpcionesDeCanal
            {
                SyncWaitSeconds = 1,
                PollIntervalMs = 50,
                ConectaAceptaNumeroDelErp = aceptaNumero,
                Endpoints = new(StringComparer.OrdinalIgnoreCase) { ["QA"] = "https://conecta-qa.prueba", ["HTTP"] = "http://conecta.prueba" },
            };
        return o;
    }

    public static CanalConecta Canal(ConectaStubHandler stub, string codigo = "CONECTA-PRUEBAS", ElectronicInvoicingOptions? opciones = null) =>
        new(codigo, new FabricaDeClientesDePrueba(stub), Options.Create(opciones ?? Opciones()), new TokensDeConecta(), NullLogger<CanalConecta>.Instance);

    public static CredencialesDeCanal Credencial(string canal = "CONECTA-PRUEBAS") =>
        new(CredencialesDeCanal.ClaveDe(Guid.NewGuid(), canal), new Dictionary<string, string> { ["clientId"] = "erp-coop", ["clientSecret"] = "s3cr3t" });

    public static ContextoDeCanal Contexto(Guid cooperativa, DocumentoElectronicoCanonico d, int version = 1, string extremo = "QA",
        CredencialesDeCanal? credencial = null, bool correo = false) => new(
        cooperativa, "900123456", "7", EmissionMode.TechnologyProvider, d.Environment, null, null, null,
        ContextoDeCanal.ClaveDeIdempotencia(cooperativa, d.Environment, d.Number.Prefix, d.Number.Consecutive, version),
        credencial ?? Credencial(), new ParametrosDelCanal(Tenant, extremo, correo));
}

/// <summary>
/// Feature 028, T028: <see cref="CanalConecta"/> cumple las ocho reglas de §3.3 de <c>dian.md</c> contra un Conecta falso en memoria, con la
/// misma tabla de dígitos del simulado.
/// </summary>
public sealed class CanalConectaConformidadTests : ConformidadDelCanal
{
    private readonly ConectaStubHandler _stub = new();
    private readonly ElectronicInvoicingOptions _opciones = ConectaDePrueba.Opciones();
    private CanalConecta? _canal;

    protected override ICanalDeEmisionElectronica Canal => _canal ??= ConectaDePrueba.Canal(_stub, opciones: _opciones);

    protected override OpcionesDeCanal Opciones => _opciones.Canal("CONECTA-PRUEBAS");

    protected override DocumentoElectronicoCanonico Documento(CasoDeConformidad caso, long consecutivo) => caso switch
    {
        CasoDeConformidad.Rechazado => Factura("16000111", consecutivo, "SETT"),
        CasoDeConformidad.ConNotificaciones => Factura("16000112", consecutivo, "SETT"),
        CasoDeConformidad.Ambiguo => Factura("16000113", consecutivo, "SETT"),
        CasoDeConformidad.DianNoDisponible => Factura("16000114", consecutivo, "SETT"),
        CasoDeConformidad.SinSalir => Factura("16000115", consecutivo, "SETT"),
        _ => Factura("16000110", consecutivo, "SETT"),
    };

    protected override ContextoDeCanal Contexto(DocumentoElectronicoCanonico documento, int version = 1) =>
        ConectaDePrueba.Contexto(Cooperativa, documento, version);

    protected override int? PeticionesAlTransporte(DocumentoElectronicoCanonico documento) =>
        _stub.EmisionesPorNumero.TryGetValue(documento.Number.Full, out var n) ? n : 0;
}

/// <summary>Feature 028, T022–T026: el adaptador de Conecta, más allá de la conformidad.</summary>
public class CanalConectaTests
{
    private static readonly Guid Cooperativa = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");
    private readonly ConectaStubHandler _stub = new();

    private static DocumentoElectronicoCanonico Factura(string taxId = "16000110", long consecutivo = 98594, DianEnvironment ambiente = DianEnvironment.Testing) => new()
    {
        Kind = ElectronicDocumentKind.Invoice,
        DianDocumentTypeCode = "01",
        OperationTypeCode = "10",
        Environment = ambiente,
        Number = new NumeroCanonico("SETT", consecutivo, $"SETT{consecutivo}"),
        IssuedAt = new DateTimeOffset(2026, 12, 5, 10, 0, 0, TimeSpan.FromHours(-5)),
        Currency = "COP",
        PaymentForm = "Cash",
        Payments = [new PagoCanonico("10", 3000m, null), new PagoCanonico("48", 1998m, "0543")],
        Issuer = new ParteCanonica { TaxId = "900123456", CheckDigit = "7", IdTypeCode = "31", PersonTypeCode = "1", Name = "Coop", Address = new DireccionCanonica("Cra 1", "76520", "CO", "PALMIRA", "76", "VALLE DEL CAUCA") },
        Counterparty = new ParteCanonica
        {
            Role = "Buyer", TaxId = taxId, IdTypeCode = "13", PersonTypeCode = "2", Name = "ANA MARÍA PÉREZ GÓMEZ", GivenNames = "ANA MARÍA", FamilyNames = "PÉREZ GÓMEZ",
            Responsibilities = ["R-99-PN"], TaxSchemeCode = "ZZ", ReceptionEmail = "ana@correo.co",
            Address = new DireccionCanonica("Calle 5", "76001", "CO", "CALI", "76", "VALLE DEL CAUCA"),
        },
        Lines =
        [
            new LineaCanonica
            {
                LineNumber = 1, ProductCode = "ARZ-001", Description = "ARROZ", Quantity = 2m, UnitCode = "94", UnitPrice = 2100m, LineExtension = 4200m,
                Allowances = [new DescuentoCanonico(null, 0.05m, 4200m, 210m)],
                Taxes = [new ImpuestoCanonico("01", 0.19m, null, null, 3990m, 758.10m)],
            },
        ],
        Withholdings = [new RetencionCanonica("06", 0.025m, 3990m, 99.75m)],
        Totals = new TotalesCanonicos { LineExtension = 4200m, Allowances = 210m, TaxExclusive = 3990m, Taxes = 758.10m, TaxInclusive = 4748.10m, Payable = 4998m, AmountDue = 4998m },
        Notes = ["Placa: ABC123"],
        Source = new OrigenCanonico("INV", Guid.Parse("7d0b1a8e-0000-4000-8000-000000000001"), "SalesInvoice", "SETT98594"),
    };

    private JsonNode UltimaEmision() => JsonNode.Parse(_stub.Cuerpos.Last(c => c.Contains("\"tenantId\"", StringComparison.Ordinal)))!;

    // ------------------------------------------------------------------------------------------ traducción (T022) --

    [Fact]
    public async Task Envia_el_numero_del_ERP_el_tenant_los_porcentajes_y_los_nombres_separados()
    {
        var d = Factura();
        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.Validated, string.Join(" | ", r.Mensajes.Select(m => m.Texto)));
        var j = UltimaEmision();
        j["tenantId"]!.GetValue<Guid>().Should().Be(ConectaDePrueba.Tenant);
        j["prefix"]!.GetValue<string>().Should().Be("SETT");
        j["number"]!.GetValue<long>().Should().Be(98594, "D1: el número es el del ERP");
        j["issueDate"]!.GetValue<string>().Should().Be("2026-12-05");
        j["invoiceType"]!.GetValue<string>().Should().Be("01");
        j["paymentMeans"]!["code"]!.GetValue<string>().Should().Be("1");
        j["paymentMeans"]!["method"]!.GetValue<string>().Should().Be("10", "D2: el de mayor valor");
        j["notes"]!.AsArray().Select(n => n!.GetValue<string>()).Should().Contain("Medios de pago: 10 3000.00; 48 1998.00");
        var c = j["customer"]!;
        c["givenNames"]!.GetValue<string>().Should().Be("ANA MARÍA");
        c["familyNames"]!.GetValue<string>().Should().Be("PÉREZ GÓMEZ");
        c["regime"]!.GetValue<string>().Should().Be("49");
        c["organizationType"]!.GetValue<string>().Should().Be("2");
        c["address"]!["city"]!.GetValue<string>().Should().Be("CALI");
        c["address"]!["departmentCode"]!.GetValue<string>().Should().Be("76");
        c["email"].Should().BeNull("D9: con entrega del ERP el correo no viaja");
        var tributo = j["lines"]![0]!["taxes"]![0]!;
        tributo["rate"]!.GetValue<decimal>().Should().Be(19m, "Conecta recibe porcentaje");
        j["lines"]![0]!["allowances"]![0]!["percent"]!.GetValue<decimal>().Should().Be(5m);
        j["metadata"]!["erpNumber"]!.GetValue<string>().Should().Be("SETT98594");
        j["metadata"]!["withholdings"]!.GetValue<string>().Should().Be("06:0.025:3990.00:99.75", "D3: la retención informativa no tiene dónde ir");
        r.Mensajes.Should().Contain(m => m.Regla == TraductorAConecta.ReglaMedios && m.Tipo == TipoDeMensajeDelCanal.Notificacion);
    }

    [Fact]
    public async Task Con_entrega_del_canal_el_correo_si_viaja()
    {
        var d = Factura();
        await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, correo: true), default);

        UltimaEmision()["customer"]!["email"]!.GetValue<string>().Should().Be("ana@correo.co");
    }

    [Fact]
    public async Task Un_tributo_por_unidad_no_se_envia()
    {
        var d = Factura() with { Lines = [Factura().Lines[0] with { Taxes = [new ImpuestoCanonico("22", null, 66m, 2m, 0m, 132m)] }] };

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.InvalidData);
        r.Mensajes.Should().ContainSingle(m => m.Regla == TraductorAConecta.ReglaTributoPorUnidad);
        _stub.EmisionesPorNumero.Should().BeEmpty("no se llama a Conecta");
    }

    [Fact]
    public async Task La_nota_credito_va_a_su_ruta_con_la_factura_referida_y_una_discrepancia_de_al_menos_20()
    {
        var d = Factura() with
        {
            Kind = ElectronicDocumentKind.CreditNote, DianDocumentTypeCode = "91", OperationTypeCode = "20",
            Number = new NumeroCanonico("NCT", 7, "NCT7"), Notes = ["Anula"],
            References = new ReferenciasCanonicas { Corrected = new DocumentoCorregidoCanonico("SETT98594", "cufe-1", new DateOnly(2026, 12, 1), "2") },
        };

        await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        _stub.Peticiones.Should().Contain(p => p.RequestUri!.AbsolutePath == "/v1/credit-notes");
        var j = UltimaEmision();
        j["referencedInvoice"]!["cufe"]!.GetValue<string>().Should().Be("cufe-1");
        j["discrepancy"]!["code"]!.GetValue<string>().Should().Be("2");
        j["discrepancy"]!["description"]!.GetValue<string>().Length.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public async Task Una_nota_sobre_una_factura_sin_CUFE_no_se_envia()
    {
        var d = Factura() with
        {
            Kind = ElectronicDocumentKind.CreditNote, Number = new NumeroCanonico("NCT", 8, "NCT8"),
            References = new ReferenciasCanonicas { Corrected = new DocumentoCorregidoCanonico("SETT1", null, new DateOnly(2026, 12, 1), "2") },
        };

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.InvalidData);
        r.Mensajes.Should().Contain(m => m.Regla == TraductorAConecta.ReglaReferencia);
    }

    // ------------------------------------------------------------------------------------------ salvaguardas --

    [Fact]
    public async Task Si_Conecta_numera_distinto_el_documento_no_se_da_por_emitido()
    {
        _stub.NumeroForzado = "SETT98600";
        var d = Factura();

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.InvalidData);
        r.Mensajes.Should().Contain(m => m.Regla == TraductorDeResultadosConecta.ReglaNumero);
    }

    [Fact]
    public async Task Si_el_tenant_de_Conecta_es_de_otro_ambiente_no_se_mezcla()
    {
        _stub.AmbienteDelTenant = "Production";
        var d = Factura();

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.InvalidData);
        r.Mensajes.Should().Contain(m => m.Regla == TraductorDeResultadosConecta.ReglaAmbiente);
    }

    [Fact]
    public async Task El_canal_de_pruebas_no_emite_un_documento_de_produccion_ni_llama()
    {
        var d = Factura(ambiente: DianEnvironment.Production);

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.InvalidData);
        _stub.Peticiones.Should().BeEmpty();
    }

    [Theory]
    [InlineData("OTRO")]
    [InlineData("HTTP")]
    public async Task Un_extremo_fuera_de_la_lista_o_sin_https_no_se_llama(string extremo)
    {
        var d = Factura();

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, extremo: extremo), default);

        r.Outcome.Should().Be(ChannelOutcome.ChannelUnavailable);
        r.Mensajes.Should().Contain(m => m.Regla == CanalConecta.ReglaExtremo);
        _stub.Peticiones.Should().BeEmpty("el secreto nunca viaja a un host que no esté en la lista");
    }

    [Fact]
    public async Task Sin_credencial_completa_no_se_llama()
    {
        var d = Factura();
        var sinSecreto = new CredencialesDeCanal("x", new Dictionary<string, string> { ["clientId"] = "erp" });

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, credencial: sinSecreto), default);

        r.Outcome.Should().Be(ChannelOutcome.ChannelUnavailable);
        _stub.Peticiones.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------ transporte (T024, T025) --

    [Fact]
    public async Task Un_401_renueva_el_token_una_vez_y_repite_sin_duplicar()
    {
        _stub.NoAutorizadosPendientes = 1;
        var d = Factura();

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.Validated);
        _stub.TokensEmitidos.Should().Be(2);
        _stub.PorClave.Should().ContainSingle();
    }

    [Fact]
    public async Task El_token_se_reutiliza_entre_llamadas()
    {
        var canal = ConectaDePrueba.Canal(_stub);
        var credencial = ConectaDePrueba.Credencial();
        foreach (var n in new long[] { 1, 2, 3 })
        {
            var d = Factura(consecutivo: n);
            await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, credencial: credencial), default);
        }

        _stub.TokensEmitidos.Should().Be(1);
    }

    [Fact]
    public async Task La_misma_version_reenviada_lleva_la_misma_clave_y_otra_version_otra()
    {
        var d = Factura();
        var canal = ConectaDePrueba.Canal(_stub);
        await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, version: 1), default);
        await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, version: 1), default);
        await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, version: 2), default);

        var claves = _stub.Peticiones.Where(p => p.Method == HttpMethod.Post && p.RequestUri!.AbsolutePath == "/v1/invoices")
            .Select(p => p.Headers.GetValues("Idempotency-Key").Single()).ToList();
        claves.Should().HaveCount(3);
        claves[0].Should().Be(claves[1]).And.NotBe(claves[2]);
        claves.Should().OnlyContain(c => c.Length == 36 && c.All(ch => char.IsAsciiHexDigit(ch) || ch == '-'));
        _stub.PorClave.Should().HaveCount(2);
    }

    [Fact]
    public void La_clave_para_Conecta_es_un_UUID_v5_determinista()
    {
        var a = CanalConecta.ClaveParaConecta("3f2a:Testing:SETT1:v1");
        var b = CanalConecta.ClaveParaConecta("3f2a:Testing:SETT1:v1");
        var c = CanalConecta.ClaveParaConecta("3f2a:Testing:SETT1:v2");

        a.Should().Be(b).And.NotBe(c);
        Guid.Parse(a).ToString("D")[14].Should().Be('5', "versión 5");
    }

    [Fact]
    public async Task Sin_decision_en_la_espera_queda_InProcess_con_la_referencia_y_la_consulta_por_id_decide_despues()
    {
        _stub.ConsultasParaDecidir = 1000;
        var d = Factura();
        var canal = ConectaDePrueba.Canal(_stub);

        var r = await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.InProcess);
        Guid.TryParse(r.ExternalReference, out var id).Should().BeTrue();
        _stub.PorId[id].ConsultasHastaDecidir = 0;
        var consulta = await canal.ConsultarEstadoAsync(new ReferenciaDeEnvio("SETT", 98594, DianEnvironment.Testing, null, r.ExternalReference),
            ConectaDePrueba.Contexto(Cooperativa, d), default);
        consulta.Outcome.Should().Be(ChannelOutcome.Validated);
        consulta.QrContent.Should().StartWith("https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey=");
        consulta.Artefactos.Select(a => a.Tipo).Should().BeEquivalentTo([TipoDeArtefacto.ApplicationResponse, TipoDeArtefacto.XmlFirmado, TipoDeArtefacto.AttachedDocument]);
    }

    // ------------------------------------------------------------------------------------------ errores RFC 9457 (T023) --

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"title\":\"Falta la cabecera\"}", ChannelOutcome.InvalidData, "CONECTA-VALIDACION")]
    [InlineData(HttpStatusCode.Forbidden, "{\"title\":\"x\"}", ChannelOutcome.ChannelUnavailable, "CONECTA-PERMISO")]
    [InlineData(HttpStatusCode.NotFound, "", ChannelOutcome.ChannelUnavailable, "CONECTA-TENANT")]
    [InlineData(HttpStatusCode.Conflict, "{\"title\":\"Numeración agotada o no vigente.\",\"prefix\":\"SETT\"}", ChannelOutcome.ChannelUnavailable, "CONECTA-NUMERACION")]
    [InlineData(HttpStatusCode.Conflict, "{\"title\":\"El canal de emisión del tenant no está listo.\"}", ChannelOutcome.ChannelUnavailable, "CONECTA-CANAL")]
    [InlineData(HttpStatusCode.Conflict, "{\"title\":\"Clave de idempotencia reutilizada para otro tipo de documento.\"}", ChannelOutcome.InvalidData, "CONECTA-CLAVE")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "{\"title\":\"El documento no supera el validador local.\",\"ruleIds\":[\"FAJ43\",\"FAK24\"]}", ChannelOutcome.InvalidData, "FAJ43")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "{\"title\":\"proveedor\",\"provider\":\"factible\",\"errors\":[\"apellidos obligatorio\"]}", ChannelOutcome.InvalidData, "CONECTA-VALIDACION")]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"title\":\"límite\"}", ChannelOutcome.ChannelUnavailable, "CONECTA-LIMITE")]
    [InlineData(HttpStatusCode.Conflict, "{\"type\":\"https://conecta.ingenia365.com/problemas/numbering-out-of-range\",\"title\":\"El número no está en una numeración vigente.\",\"status\":409,\"prefix\":\"SETT\",\"number\":98594}", ChannelOutcome.ChannelUnavailable, "CONECTA-FUERA-DE-RANGO")]
    [InlineData(HttpStatusCode.BadRequest, "{\"type\":\"https://conecta.ingenia365.com/problemas/client-numbering\",\"title\":\"Este prefijo no recibe el número del emisor.\",\"status\":400,\"prefix\":\"SETT\"}", ChannelOutcome.ChannelUnavailable, "CONECTA-NUMERACION-CLIENTE")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", ChannelOutcome.InProcess, "CONECTA-SERVIDOR")]
    public async Task Los_errores_de_Conecta_se_traducen_a_su_resultado(HttpStatusCode codigo, string cuerpo, ChannelOutcome esperado, string regla)
    {
        _stub.RespuestasDeEmision.Enqueue((codigo, cuerpo));
        var d = Factura();

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(esperado);
        r.Mensajes.Should().Contain(m => m.Regla == regla);
        r.ProviderCode.Should().Be($"HTTP {(int)codigo}");
    }

    [Fact]
    public async Task Un_409_de_numero_en_conflicto_consulta_en_vez_de_fallar()
    {
        var d = Factura();
        var canal = ConectaDePrueba.Canal(_stub);
        await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);
        _stub.RespuestasDeEmision.Enqueue((HttpStatusCode.Conflict, "{\"type\":\"https://docs.conecta.ingenia365.com/errors/number-conflict\"}"));

        var r = await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, version: 2), default);

        r.Outcome.Should().Be(ChannelOutcome.Validated, "el número ya está en Conecta: se responde lo que diga la consulta");
    }

    /// <summary>El cuerpo real del 409 de Conecta v0.1.4: «status» va dos veces (el código HTTP y el estado del existente).</summary>
    private static string NumeroOcupado(Guid? id, string? estado) =>
        "{\"type\":\"https://conecta.ingenia365.com/problemas/number-conflict\",\"title\":\"El número ya lo usa otro documento.\",\"status\":409," +
        "\"prefix\":\"SETT\",\"number\":98594,\"documentId\":" + (id is null ? "null" : $"\"{id:D}\"") + ",\"status\":" +
        (estado is null ? "null" : $"\"{estado}\"") + "}";

    [Fact]
    public async Task El_409_real_de_Conecta_con_status_repetido_se_lee_y_adopta_el_documento_propio()
    {
        var d = Factura();
        var canal = ConectaDePrueba.Canal(_stub);
        var primero = await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);
        _stub.RespuestasDeEmision.Enqueue((HttpStatusCode.Conflict, NumeroOcupado(Guid.Parse(primero.ExternalReference!), "Rendered")));

        var r = await canal.EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d, version: 2), default);

        r.Outcome.Should().Be(ChannelOutcome.Validated, string.Join(" | ", r.Mensajes.Select(m => m.Texto)));
    }

    [Fact]
    public async Task Un_numero_que_ocupa_otro_documento_en_Conecta_no_se_adopta()
    {
        var ajeno = new ConectaStubHandler.Doc { Numero = "SETT98594", Prefijo = "SETT", Consecutivo = 98594, Total = 123_456m };
        _stub.PorId[ajeno.Id] = ajeno;
        _stub.RespuestasDeEmision.Enqueue((HttpStatusCode.Conflict, NumeroOcupado(ajeno.Id, "Rendered")));
        var d = Factura();

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.InvalidData);
        r.UniqueCode.Should().BeNull("el CUFE de otro documento nunca se toma como propio");
        r.Mensajes.Should().Contain(m => m.Regla == TraductorDeResultadosConecta.ReglaNumeroOcupado);
    }

    [Fact]
    public async Task Un_numero_ocupado_que_Conecta_todavia_no_muestra_queda_en_proceso()
    {
        _stub.RespuestasDeEmision.Enqueue((HttpStatusCode.Conflict, NumeroOcupado(null, null)));
        var d = Factura();

        var r = await ConectaDePrueba.Canal(_stub).EmitirAsync(d, ConectaDePrueba.Contexto(Cooperativa, d), default);

        r.Outcome.Should().Be(ChannelOutcome.InProcess);
    }

    // ------------------------------------------------------------------------------------------ verificación --

    [Fact]
    public async Task Verificar_falla_si_un_prefijo_enlazado_no_recibe_el_numero_del_ERP_y_muestra_el_contador()
    {
        var bien = await ConectaDePrueba.Canal(_stub).ProbarAsync(ContextoDeVerificacion(), default);
        bien.Mensajes.Single().Texto.Should().Contain("SETT (va en 98597)");

        _stub.Resoluciones[0] = ("SETT", 98597, false);
        var r = await ConectaDePrueba.Canal(_stub).ProbarAsync(ContextoDeVerificacion(), default);

        r.Outcome.Should().Be(ChannelOutcome.ChannelUnavailable);
        r.Mensajes.Should().Contain(m => m.Regla == TraductorDeResultadosConecta.ReglaNumeracionDelCliente);
    }

    private ContextoDeCanal ContextoDeVerificacion() => ConectaDePrueba.Contexto(Cooperativa, Factura());

    [Fact]
    public async Task Verificar_resume_el_canal_y_valida()
    {
        var r = await ConectaDePrueba.Canal(_stub).ProbarAsync(ContextoDeVerificacion(), default);

        r.Outcome.Should().Be(ChannelOutcome.Validated, string.Join(" | ", r.Mensajes.Select(m => m.Texto)));
        r.Mensajes.Single().Texto.Should().Contain("Habilitation").And.Contain("proveedor factible").And.Contain("SETT");
    }

    [Fact]
    public async Task Verificar_falla_mientras_Conecta_no_acepte_el_numero_del_ERP()
    {
        var r = await ConectaDePrueba.Canal(_stub, opciones: ConectaDePrueba.Opciones(aceptaNumero: false)).ProbarAsync(ContextoDeVerificacion(), default);

        r.Outcome.Should().Be(ChannelOutcome.ChannelUnavailable);
        r.Mensajes.Should().Contain(m => m.Regla == CanalConecta.ReglaNumeracionDelErp);
    }

    [Fact]
    public async Task Verificar_falla_con_otro_ambiente_otro_NIT_o_algo_pendiente_en_Conecta()
    {
        _stub.AmbienteDelTenant = "Production";
        _stub.NitDelTenant = "800121426";
        _stub.Faltantes.Add("Credenciales del proveedor");

        var r = await ConectaDePrueba.Canal(_stub).ProbarAsync(ContextoDeVerificacion(), default);

        r.Outcome.Should().Be(ChannelOutcome.ChannelUnavailable);
        r.Mensajes.Where(m => m.Tipo == TipoDeMensajeDelCanal.Rechazo).Should().HaveCount(3);
    }

    [Fact]
    public void Los_dos_modos_declaran_su_ambiente_y_su_esquema_de_credencial()
    {
        var pruebas = ConectaDePrueba.Canal(_stub);
        var real = ConectaDePrueba.Canal(_stub, "CONECTA");

        pruebas.AmbienteExigido.Should().Be(DianEnvironment.Testing);
        pruebas.EsDeProduccion.Should().BeFalse();
        real.AmbienteExigido.Should().Be(DianEnvironment.Production);
        real.EsDeProduccion.Should().BeTrue();
        real.CamposDeCredencial.Should().Equal("clientId", "clientSecret");
        pruebas.Extremos.Should().Equal("HTTP", "QA");
        pruebas.Capacidades.ContingenciaDelFacturador.Should().BeFalse("D8");
        pruebas.Capacidades.Tipos.Should().BeEquivalentTo([ElectronicDocumentKind.Invoice, ElectronicDocumentKind.CreditNote, ElectronicDocumentKind.DebitNote]);
    }
}
