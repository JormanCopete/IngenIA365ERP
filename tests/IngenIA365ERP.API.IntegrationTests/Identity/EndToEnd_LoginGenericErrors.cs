using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity;

/// <summary>
/// FR-041 — Respuesta genérica ante credenciales inválidas (anti-enumeración):
/// el login con un email que NO existe y el login con la password incorrecta
/// de un usuario que SÍ existe (el master sembrado) deben producir exactamente
/// la misma respuesta — mismo status HTTP y mismo envelope
/// <c>{ code = Identity.InvalidCredentials, message }</c> — sin filtrar si la
/// cuenta existe o no.
/// </summary>
public sealed class EndToEnd_LoginGenericErrors(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Email_inexistente_y_password_mala_devuelven_la_misma_respuesta_generica()
    {
        using var http = fx.CreateClient();

        // 1) Email inexistente: no hay ninguna cuenta central con este correo.
        var respInexistente = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = "no.existe@integration.test",
            password = "Cualquier-Password-2026!",
        });

        // 2) Password incorrecta de un usuario REAL (el master sembrado).
        var respPasswordMala = await http.PostAsJsonAsync("/api/auth/login", new
        {
            email = CentralIdentityApiFixture.MasterEmail,
            password = "Password-Incorrecta-2026!",
        });

        // Mismo status en ambos casos: 401, que es lo que promete el contrato
        // para Identity.InvalidCredentials (specs/002/contracts/auth.md:87).
        // Esta línea decía 422 y explicaba por qué — estaba documentando el
        // defecto en vez de cazarlo.
        Assert.Equal(HttpStatusCode.Unauthorized, respInexistente.StatusCode);
        Assert.Equal(respInexistente.StatusCode, respPasswordMala.StatusCode);

        var cuerpoInexistente = await ReadJsonAsync(respInexistente);
        var cuerpoPasswordMala = await ReadJsonAsync(respPasswordMala);

        // Mismo código de error genérico en ambos casos (FR-041).
        Assert.Equal("Identity.InvalidCredentials",
            cuerpoInexistente.GetProperty("code").GetString());
        Assert.Equal("Identity.InvalidCredentials",
            cuerpoPasswordMala.GetProperty("code").GetString());

        // Mismo mensaje genérico: no distingue "usuario no existe" de
        // "contraseña incorrecta".
        Assert.Equal(
            cuerpoInexistente.GetProperty("message").GetString(),
            cuerpoPasswordMala.GetProperty("message").GetString());

        // El envelope no filtra existencia: sin tokens ni pistas de la cuenta.
        Assert.False(cuerpoInexistente.TryGetProperty("accessToken", out _));
        Assert.False(cuerpoPasswordMala.TryGetProperty("accessToken", out _));

        // Cuerpos idénticos salvo traceId (único por request): comparamos el
        // JSON normalizado sin esa propiedad.
        Assert.Equal(
            SinTraceId(cuerpoInexistente),
            SinTraceId(cuerpoPasswordMala));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }

    /// <summary>
    /// Serializa el envelope ordenando propiedades y excluyendo <c>traceId</c>,
    /// para poder afirmar que ambas respuestas son byte a byte equivalentes.
    /// </summary>
    private static string SinTraceId(JsonElement envelope)
    {
        var props = envelope.EnumerateObject()
            .Where(p => !string.Equals(p.Name, "traceId", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={p.Value.GetRawText()}");
        return string.Join(";", props);
    }
}
