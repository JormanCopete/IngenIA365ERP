using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Los ayudantes de los documentos al deudor (feature 013, US13, T0769, T0770): la cooperativa «documentos» es el escenario del desembolso
/// (<see cref="DesembolsoE2E"/>, activada) con el firmante de los documentos configurado por sus parámetros (<c>Documentos.Firmante.Nombre</c> y
/// <c>.Cargo</c>, desde hoy) y, para el correo, la autorización de tratamiento general de cada titular. Emite el <c>administrador</c> del ensayo
/// (<c>cartera.administrador</c>: todos los permisos de documentos); el <c>asesor.o1</c> lee y emite pero no verifica. Las dos clases que la usan
/// comparten la cooperativa (una por fixture). (nuevo)
/// </summary>
public static class DocumentosE2E
{
    public const string Documentos = "/api/lending/documents";
    public const string Firmante = "Ana Lucía Gerente (e2e)";
    public const string Cargo = "Gerente";

    private static readonly Dictionary<(CentralIdentityApiFixture, Guid), Task> Firmantes = [];
    private static readonly object Cerrojo = new();

    /// <summary>El escenario del desembolso en la cooperativa «documentos» con el firmante configurado.</summary>
    public static async Task<DesembolsoE2E.Escenario> EscenarioAsync(CentralIdentityApiFixture fx)
    {
        var esc = await DesembolsoE2E.EscenarioAsync(fx, "documentos");
        await FirmanteAsync(fx, esc.Coop.TenantPublicId, esc.Coop.TokenAdmin, esc.Hoy);
        return esc;
    }

    /// <summary>Configura el firmante de los documentos de una cooperativa una sola vez por fixture (dos vigencias del mismo día no caben).</summary>
    public static Task FirmanteAsync(CentralIdentityApiFixture fx, Guid cooperativa, string admin, DateOnly desde)
    {
        lock (Cerrojo)
        {
            if (!Firmantes.TryGetValue((fx, cooperativa), out var tarea))
            {
                tarea = ConfigurarAsync(fx, admin, desde);
                Firmantes[(fx, cooperativa)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task ConfigurarAsync(CentralIdentityApiFixture fx, string admin, DateOnly desde)
    {
        using var http = fx.CreateClient();
        foreach (var (clave, valor) in new[] { ("Documentos.Firmante.Nombre", Firmante), ("Documentos.Firmante.Cargo", Cargo) })
            await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/parameters/{clave}/versions", new
            {
                scopeKind = 0, value = valor, validFrom = desde, reason = "Firmante de los documentos al deudor (e2e)",
            });
    }

    /// <summary>Un pedido de emisión.</summary>
    public static object Pedido(string clase, Guid persona, Guid? credito, string entrega = "Download", DateOnly? corte = null) => new
    {
        kind = clase, personPublicId = persona, loanPublicId = credito, cutoffDate = corte, delivery = entrega,
    };

    /// <summary>Emite y exige el 201; devuelve la respuesta (<c>documentPublicId</c>, <c>number</c>, <c>verificationCode</c>, <c>status</c>…).</summary>
    public static async Task<JsonElement> EmitidoAsync(HttpClient http, string token, object pedido, Guid? clave = null)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Documentos, pedido, clave);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"emitir: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>Verifica un código con <paramref name="token"/> y exige el 200.</summary>
    public static Task<JsonElement> VerificarAsync(HttpClient http, string token, string codigo) =>
        CarteraE2E.GetAsync(http, token, $"{Documentos}/verify/{Uri.EscapeDataString(codigo)}");

    /// <summary>Sigue un enlace firmado (en este host, la ruta anónima <c>local-blob</c> de la API) y devuelve los bytes.</summary>
    public static async Task<byte[]> BajarAsync(HttpClient http, string url)
    {
        using var descarga = await http.GetAsync(Uri.TryCreate(url, UriKind.Absolute, out var absoluta) ? absoluta.PathAndQuery : url);
        descarga.StatusCode.Should().Be(HttpStatusCode.OK);
        return await descarga.Content.ReadAsByteArrayAsync();
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>El SHA-256 guardado del documento y cuántos documentos de esa clase tiene la persona, en la base.</summary>
    public static Task<string> HuellaGuardadaAsync(CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, Guid documento) =>
        DesembolsoE2E.EnLaBaseAsync(fx, esc, db => db.IssuedLendingDocuments.AsNoTracking().Where(d => d.PublicId == documento)
            .Select(d => d.ContentSha256).SingleAsync());

    public static Task<int> CuantosAsync(CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, Guid persona, Domain.Enums.Lending.IssuedDocumentKind clase) =>
        DesembolsoE2E.EnLaBaseAsync(fx, esc, db => (from d in db.IssuedLendingDocuments.AsNoTracking()
                                                    join p in db.People.AsNoTracking() on d.PersonId equals p.Id
                                                    where p.PublicId == persona && d.Kind == clase
                                                    select d.Id).CountAsync());

    /// <summary>El pago que cancela el crédito por el recaudador de prueba, con su clave de negocio; devuelve (clave, comprobante).</summary>
    public static async Task<(string Clave, JsonElement Comprobante)> CancelarAsync(HttpClient http, DesembolsoE2E.Escenario esc, Guid persona, Guid credito,
        DateOnly dia)
    {
        var paraCancelar = (await PagosE2E.ObligacionAsync(http, esc.Usuarios, persona, credito)).GetProperty("payoffAmount").GetDecimal();
        var clave = PagosE2E.Clave();
        var comprobante = await PagosE2E.AplicadoAsync(http, esc.Usuarios, PagosE2E.Pago(credito, persona, "FullPayoff", paraCancelar, dia, clave));
        comprobante.GetProperty("paidOff").GetBoolean().Should().BeTrue();
        return (clave, comprobante);
    }
}
