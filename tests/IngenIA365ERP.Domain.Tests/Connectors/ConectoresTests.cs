using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Domain.Connectors;
using IngenIA365ERP.Domain.Enums.Connectors;

namespace IngenIA365ERP.Domain.Tests.Connectors;

/// <summary>
/// Feature 020 (T139; R15, R16, R17): la plantilla del cuerpo, el mapa de respuestas, la validación de la definición y la defensa contra
/// SSRF de los conectores externos. Todo puro: ninguna prueba toca la red.
/// </summary>
public class RenderizadorDePlantillaTests
{
    private static readonly IReadOnlySet<string> SinCredenciales = new HashSet<string>(StringComparer.Ordinal);

    private static Dictionary<string, object?> Valores() => new(StringComparer.Ordinal)
    {
        ["vehiculo.placa"] = "ABC123",
        ["vehiculo.radioMovil"] = "101",
        ["pago.valor"] = 50000.4m,
        ["pago.recibo"] = "RC-77",
        ["pago.fecha"] = new DateOnly(2026, 12, 5),
        ["servicio.nombre"] = "Radio \"plus\"",
        ["envio.id"] = "0f1e2d3c-0000-0000-0000-000000000001",
        ["propietario.documento"] = "123456",
    };

    [Fact]
    public void Renderiza_formatos_entero_decimal_fecha_y_texto_escapado()
    {
        const string plantilla =
            "{\"movil\":{{vehiculo.radioMovil:int}},\"valor\":{{pago.valor:int}},\"exacto\":{{pago.valor:dec2}},\"fecha\":\"{{pago.fecha:date(dd/MM/yyyy)}}\",\"obs\":\"{{servicio.nombre}}\"}";

        var (texto, faltan) = RenderizadorDePlantilla.Renderizar(plantilla, Valores());

        faltan.Should().BeEmpty();
        texto.Should().Be("{\"movil\":101,\"valor\":50000,\"exacto\":50000.40,\"fecha\":\"05/12/2026\",\"obs\":\"Radio \\\"plus\\\"\"}");
        // El resultado es JSON válido aunque el texto traiga comillas.
        var act = () => JsonDocument.Parse(texto!);
        act.Should().NotThrow();
    }

    [Fact]
    public void Una_variable_sin_valor_no_renderiza_y_dice_cual_falta()
    {
        var (texto, faltan) = RenderizadorDePlantilla.Renderizar("{\"factura\":\"{{factura.numero}}\",\"placa\":\"{{vehiculo.placa}}\"}", Valores());

        texto.Should().BeNull();
        faltan.Should().Equal("factura.numero");
    }

    [Fact]
    public void Validar_rechaza_variables_desconocidas_y_formatos_inventados()
    {
        var problemas = RenderizadorDePlantilla.Validar("{{vehiculo.color}} {{pago.valor:moneda}}", false, SinCredenciales, "el cuerpo");

        problemas.Select(p => p.Codigo).Should().BeEquivalentTo(["Connector.UnknownVariable", "Connector.UnknownFormat"]);
    }

    [Fact]
    public void Una_credencial_fuera_de_la_autenticacion_se_rechaza()
    {
        var campos = new HashSet<string>(StringComparer.Ordinal) { "password" };

        RenderizadorDePlantilla.Validar("{\"clave\":\"{{cred.password}}\"}", false, campos, "el cuerpo")
            .Should().ContainSingle(p => p.Codigo == "Connector.CredentialOutsideAuth");
        RenderizadorDePlantilla.Validar("{\"clave\":\"{{cred.password}}\"}", true, campos, "el inicio de sesión").Should().BeEmpty();
        RenderizadorDePlantilla.Validar("{\"clave\":\"{{cred.pin}}\"}", true, campos, "el inicio de sesión")
            .Should().ContainSingle(p => p.Codigo == "Connector.UnknownVariable");
    }

    [Fact]
    public void Un_marcador_mal_cerrado_es_error_de_sintaxis() =>
        RenderizadorDePlantilla.Validar("{\"placa\":\"{{vehiculo.placa}\"}", false, SinCredenciales, "el cuerpo")
            .Should().Contain(p => p.Codigo == "Connector.TemplateSyntax");

    [Fact]
    public void Sanear_oculta_credenciales_y_datos_personales()
    {
        var saneado = RenderizadorDePlantilla.Sanear("{\"doc\":\"{{propietario.documento}}\",\"placa\":\"{{vehiculo.placa}}\",\"u\":\"{{cred.username}}\"}",
            Valores(), VariablesDeConector.Sensibles);

        saneado.Should().Be("{\"doc\":\"***\",\"placa\":\"ABC123\",\"u\":\"***\"}");
    }
}

public class MapaDeRespuestasTests
{
    private static readonly IReadOnlyList<ReglaDeRespuesta> Reglas =
    [
        new(200, null, ConnectorOutcome.Done),
        new(400, "ya tiene vigencia", ConnectorOutcome.DoneWithWarning),
        new(400, "No hay créditos", ConnectorOutcome.ManualReview),
        new(401, null, ConnectorOutcome.ReAuth),
        new(500, null, ConnectorOutcome.Retry),
        new(0, null, ConnectorOutcome.Retry),
    ];

    [Theory]
    [InlineData(200, "{\"ok\":true}", ConnectorOutcome.Done)]
    [InlineData(400, "{\"error\":\"El vehículo YA TIENE VIGENCIA hasta la fecha de corte\"}", ConnectorOutcome.DoneWithWarning)]
    [InlineData(400, "{\"error\":\"no hay creditos disponibles\"}", ConnectorOutcome.ManualReview)]
    [InlineData(401, null, ConnectorOutcome.ReAuth)]
    [InlineData(500, "error interno", ConnectorOutcome.Retry)]
    [InlineData(0, null, ConnectorOutcome.Retry)]
    public void Clasifica_por_codigo_y_texto_sin_mayusculas_ni_tildes(int status, string? cuerpo, ConnectorOutcome esperado) =>
        MapaDeRespuestas.Clasificar(Reglas, status, cuerpo).Should().Be(esperado);

    [Theory]
    [InlineData(400, "{\"error\":\"vehículo no existe\"}")]
    [InlineData(418, null)]
    [InlineData(302, "movido")]
    public void Lo_no_previsto_es_revisar_a_mano(int status, string? cuerpo) =>
        MapaDeRespuestas.Clasificar(Reglas, status, cuerpo).Should().Be(ConnectorOutcome.ManualReview);

    [Fact]
    public void Las_reglas_se_evaluan_en_orden()
    {
        IReadOnlyList<ReglaDeRespuesta> reglas = [new(400, null, ConnectorOutcome.Retry), new(400, "vigencia", ConnectorOutcome.Done)];

        MapaDeRespuestas.Clasificar(reglas, 400, "ya tiene vigencia").Should().Be(ConnectorOutcome.Retry);
    }
}

public class DefinicionDeConectorTests
{
    private const string Valida = """
        {
          "auth": { "kind": "LoginToken", "method": "POST", "path": "/v1/login",
            "body": "{\"username\":\"{{cred.username}}\",\"password\":\"{{cred.password}}\",\"permanent\":false}",
            "tokenPath": "$.token", "expiresInSeconds": 86400, "header": "Authorization", "prefix": "Bearer " },
          "operation": { "method": "POST", "path": "/v1/asignaciones/add",
            "body": "{\"movil\":{{vehiculo.radioMovil:int}},\"placa\":\"{{vehiculo.placa}}\",\"valor\":{{pago.valor:int}},\"externalID\":\"{{envio.id}}\"}" },
          "credentialFields": [ { "name": "username", "label": "Usuario", "secret": false }, { "name": "password", "label": "Clave", "secret": true } ],
          "responses": [ { "status": 200, "outcome": "Done" }, { "status": 401, "outcome": "ReAuth" }, { "status": 500, "outcome": "Retry" } ]
        }
        """;

    [Fact]
    public void La_definicion_de_referencia_es_valida_con_los_valores_por_defecto()
    {
        var d = DefinicionDeConector.Leer(Valida, out var problema);

        problema.Should().BeNull();
        d!.Validar().Should().BeEmpty();
        d.Auth.Kind.Should().Be(ConnectorAuthKind.LoginToken);
        d.MaxAttempts.Should().Be(DefinicionDeConector.IntentosPorDefecto);
        d.TimeoutSeconds.Should().Be(DefinicionDeConector.EsperaPorDefecto);
    }

    [Fact]
    public void Un_json_roto_no_se_lee()
    {
        DefinicionDeConector.Leer("{ \"auth\": ", out var problema).Should().BeNull();
        problema!.Codigo.Should().Be("Connector.DefinitionInvalid");
    }

    [Fact]
    public void Una_credencial_en_el_cuerpo_de_la_operacion_impide_publicar()
    {
        var json = Valida.Replace("\\\"placa\\\":\\\"{{vehiculo.placa}}\\\"", "\\\"placa\\\":\\\"{{cred.password}}\\\"", StringComparison.Ordinal);
        var d = DefinicionDeConector.Leer(json, out _)!;

        d.Validar().Should().Contain(p => p.Codigo == "Connector.CredentialOutsideAuth");
    }

    [Fact]
    public void Sin_ninguna_respuesta_hecho_no_se_publica()
    {
        var d = DefinicionDeConector.Leer(Valida, out _)! with { Responses = [new(500, null, ConnectorOutcome.Retry)] };

        d.Validar().Should().Contain(p => p.Mensaje.Contains("«hecho»"));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(51, 30)]
    [InlineData(12, 0)]
    [InlineData(12, 61)]
    public void Intentos_y_espera_tienen_tope(int intentos, int espera)
    {
        var d = DefinicionDeConector.Leer(Valida, out _)! with { MaxAttempts = intentos, TimeoutSeconds = espera };

        d.Validar().Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("{\"token\":\"abc\"}", "$.token", "abc")]
    [InlineData("{\"data\":{\"token\":\"xyz\"}}", "$.data.token", "xyz")]
    [InlineData("{\"otro\":1}", "$.token", null)]
    [InlineData("no es json", "$.token", null)]
    public void Lee_el_token_por_su_ruta(string cuerpo, string ruta, string? esperado) =>
        DefinicionDeConector.LeerToken(cuerpo, ruta).Should().Be(esperado);
}

public class DireccionSeguraTests
{
    [Theory]
    [InlineData("https://proveedor.example.com")]
    [InlineData("https://proveedor.example.com:8443")]
    [InlineData("https://proveedor.example.com/api")]
    [InlineData("https://8.8.8.8")]
    public void Acepta_https_publico(string url) => DireccionSegura.ValidarUrlBase(url).Should().BeNull();

    [Theory]
    [InlineData("http://proveedor.example.com", "Connector.InsecureUrl")]
    [InlineData("https://usuario:clave@proveedor.example.com", "Connector.InsecureUrl")]
    [InlineData("https://proveedor.example.com/?token=1", "Connector.InsecureUrl")]
    [InlineData("no es url", "Connector.InsecureUrl")]
    [InlineData("https://localhost:5001", "Connector.PrivateAddress")]
    [InlineData("https://api.default.svc.cluster.local", "Connector.PrivateAddress")]
    [InlineData("https://10.0.0.5", "Connector.PrivateAddress")]
    [InlineData("https://192.168.1.10", "Connector.PrivateAddress")]
    [InlineData("https://172.20.0.1", "Connector.PrivateAddress")]
    [InlineData("https://169.254.169.254", "Connector.PrivateAddress")]
    [InlineData("https://100.100.1.1", "Connector.PrivateAddress")]
    [InlineData("https://[::1]", "Connector.PrivateAddress")]
    [InlineData("https://[::ffff:10.0.0.1]", "Connector.PrivateAddress")]
    public void Rechaza_lo_inseguro_y_lo_interno(string url, string codigo) =>
        DireccionSegura.ValidarUrlBase(url)!.Codigo.Should().Be(codigo);

    [Fact]
    public void Con_redes_privadas_permitidas_solo_se_exige_https() =>
        DireccionSegura.ValidarUrlBase("https://localhost:5001", permitirRedesPrivadas: true).Should().BeNull();

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("172.32.0.1", true)]
    [InlineData("192.168.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:192.168.0.1", false)]
    [InlineData("2001:4860:4860::8888", true)]
    public void Mira_cada_ip_resuelta(string ip, bool permitida) =>
        DireccionSegura.EsIpPermitida(IPAddress.Parse(ip)).Should().Be(permitida);
}
