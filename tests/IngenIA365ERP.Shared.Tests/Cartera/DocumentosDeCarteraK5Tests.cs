using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// T0975 (feature 013, K5, US13; contracts/api.md §15): el cliente de documentos manda el período del extracto y el año del certificado tributario
/// con su <c>Idempotency-Key</c>, y la verificación lleva la huella del PDF en <c>?sha256=</c> —nunca el archivo— y lee <c>pdfMatches</c>; la
/// pantalla ofrece las seis clases y nombra los datos esenciales nuevos. Ninguna petición pone <c>Authorization</c> a mano. (nuevo)
/// </summary>
public class DocumentosDeCarteraK5Tests
{
    private static readonly Guid Persona = Guid.Parse("44444444-5555-6666-7777-888888888888");
    private static readonly Guid Credito = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private const string Huella = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private readonly ServidorDeCartera _servidor = new();
    private readonly CarteraClient _cartera;

    public DocumentosDeCarteraK5Tests()
    {
        var (http, auth) = _servidor.Sesion();
        _cartera = new CarteraClient(http, auth);
    }

    [Fact]
    public async Task El_extracto_y_el_certificado_viajan_con_su_periodo_o_año_y_su_clave()
    {
        string? cuerpo = null;
        _servidor.Responder = r =>
        {
            cuerpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return ServidorDeCartera.Json(HttpStatusCode.Created, new { documentPublicId = Guid.NewGuid(), number = "EX-000001", verificationCode = "X", status = "Valid" });
        };

        await _cartera.EmitirDocumentoAsync(new EmitirDocumentoRequest("Statement", Persona, Credito, null,
            new PeriodoDeDocumentoRequest(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)), null, "Download"), new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle().Subject;
        vista.Ruta.Should().Be("/api/lending/documents");
        vista.Clave.Should().NotBeNullOrEmpty();
        vista.Authorization.Should().BeNull();
        using (var json = JsonDocument.Parse(cuerpo!))
        {
            json.RootElement.GetProperty("kind").GetString().Should().Be("Statement");
            json.RootElement.GetProperty("period").GetProperty("from").GetString().Should().Be("2026-10-01");
            json.RootElement.GetProperty("period").GetProperty("to").GetString().Should().Be("2026-12-31");
        }

        await _cartera.EmitirDocumentoAsync(new EmitirDocumentoRequest("TaxCertificate", Persona, null, null, null, 2025, "Email"), new ClaveDeOperacion());
        using (var json = JsonDocument.Parse(cuerpo!))
        {
            json.RootElement.GetProperty("year").GetInt32().Should().Be(2025);
            json.RootElement.GetProperty("loanPublicId").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task La_verificacion_manda_la_huella_y_lee_si_coincide()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new
        {
            authentic = true, status = "Valid", kind = "TaxCertificate", number = "CT-000001", issuedAt = DateTime.UtcNow,
            holder = new { fullName = "Ana", documentNumberMasked = "****1234" }, essentialData = new { year = 2025 }, voided = (object?)null, pdfMatches = true,
        });

        var r = await _cartera.VerificarDocumentoAsync("7K2M-Q9XD-4HTP-B1RW", Huella);

        _servidor.Vistas.Single().Ruta.Should().Be($"/api/lending/documents/verify/7K2M-Q9XD-4HTP-B1RW?sha256={Huella}");
        r.Value!.PdfMatches.Should().BeTrue();

        await _cartera.VerificarDocumentoAsync("7K2M-Q9XD-4HTP-B1RW");
        _servidor.Vistas[^1].Ruta.Should().Be("/api/lending/documents/verify/7K2M-Q9XD-4HTP-B1RW", "sin huella no se compara");
    }

    [Fact]
    public void La_pantalla_ofrece_las_seis_clases_y_nombra_los_datos_nuevos()
    {
        TextosDeDocumentos.ClasesQueSeEmiten.Select(c => c.Valor).Should().Contain(["Statement", "TaxCertificate"]).And.HaveCount(6);
        TextosDeDocumentos.EsDeLaPersona("TaxCertificate").Should().BeTrue();
        TextosDeDocumentos.LlevaCredito("Statement").Should().BeTrue();
        TextosDeDocumentos.ComparacionDelPdf(true).Should().Contain("idéntico");
        TextosDeDocumentos.ComparacionDelPdf(false).Should().Contain("NO es el emitido");
        TextosDeDocumentos.ComparacionDelPdf(null).Should().BeNull();

        using var datos = JsonDocument.Parse("""{"year":2025,"totalInterestPaid":125000,"periodFrom":"2026-10-01","loans":[{"number":"CR-1","interestPaid":125000,"balanceAtYearEnd":0}]}""");
        var filas = TextosDeDocumentos.DatosEsenciales(datos.RootElement);
        filas.Select(f => f.Rotulo).Should().Contain(["Año", "Intereses pagados", "Desde", "Créditos"]);
        filas.Single(f => f.Rotulo == "Créditos").Valor.Should().Contain("CR-1").And.Contain("intereses pagados");
    }
}
