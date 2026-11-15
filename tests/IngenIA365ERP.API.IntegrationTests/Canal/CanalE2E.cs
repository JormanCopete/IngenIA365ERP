using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;
using IngenIA365ERP.API.IntegrationTests.Treasury;
using IngenIA365ERP.Application.Channel.Common;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IngenIA365ERP.API.IntegrationTests.CanalDigital;

/// <summary>
/// Feature 021, fase 18 (T152–T154): ayudantes de las pruebas de integración del canal digital. Firman tokens de servicio con la llave de la corrida
/// (<see cref="CentralIdentityApiFixture.LlaveDelCanal"/>) y arman una cooperativa con Recaudo activo, una asociada con dos créditos desembolsados
/// (<see cref="CajaE2E.PreparadaAsync"/>), un convenio de pasarela, el canal encendido, un destino de pago, el concepto de créditos publicado y la
/// asociada habilitada. El satélite es <see cref="CanalSateliteDePruebas"/>: configurado y sin intentos, como un canal que no tiene el aviso.
/// </summary>
public static class CanalE2E
{
    public const string Emisor = "ingenia365-canal";
    public const string Audiencia = "ingenia365-erp";

    /// <summary>Un token de servicio del canal para <paramref name="sujeto"/> (la cooperativa o <c>platform</c>).</summary>
    public static string Token(CentralIdentityApiFixture fx, string sujeto, TimeSpan? vida = null, string? jti = null, ECDsa? llave = null)
    {
        var ahora = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emisor, Audience = Audiencia, IssuedAt = ahora, NotBefore = ahora, Expires = ahora + (vida ?? TimeSpan.FromSeconds(60)),
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(llave ?? fx.LlaveDelCanal) { KeyId = CentralIdentityApiFixture.KidDelCanal },
                SecurityAlgorithms.EcdsaSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = sujeto,
                [JwtRegisteredClaimNames.Jti] = jti ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            },
        });
    }

    /// <summary>Una petición a la API de servicio con un token recién firmado (cada token es de un solo uso).</summary>
    public static Task<HttpResponseMessage> ServicioAsync(CentralIdentityApiFixture fx, HttpClient http, Guid cooperativa, HttpMethod metodo, string ruta,
        object? cuerpo = null)
    {
        var peticion = new HttpRequestMessage(metodo, $"/api/canal/v1/tenants/{cooperativa}{ruta}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(fx, cooperativa.ToString()));
        if (cuerpo is not null) peticion.Content = JsonContent.Create(cuerpo);
        return http.SendAsync(peticion);
    }

    /// <summary>Una referencia del canal válida (código, fecha, secuencia y dígito de control).</summary>
    public static string Referencia(int secuencia, DateOnly fecha)
    {
        var cuerpo = $"TST{fecha:yyMMdd}{secuencia.ToString("X8", CultureInfo.InvariantCulture)}";
        return cuerpo + ReferenciaDelCanal.DigitoDeControl(cuerpo);
    }

    public sealed record Escenario(CajaE2E.Caja Caja, Guid Convenio, Guid Destino, Guid CuentaPropia)
    {
        public string Token => Caja.Token;
        public Guid Cooperativa => Caja.Escenario.Coop.TenantPublicId;
        public Guid Persona => Caja.Persona;
        public DateOnly Hoy => Caja.Escenario.Hoy;
    }

    public static async Task<Escenario> PreparadoAsync(CentralIdentityApiFixture fx, HttpClient http, string nombre)
    {
        var c = await CajaE2E.PreparadaAsync(fx, http, nombre);
        var t = c.Token;
        var cuenta = await TesoreriaE2E.CuentaPropiaAsync(http, t, c.Escenario, $"PAS{nombre.ToUpperInvariant()[..Math.Min(5, nombre.Length)]}", 1,
            c.Escenario.CuentaDeOrigen);
        var formato = await FormatoDePasarelaAsync(http, t, nombre);
        var convenio = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/receipts/collection-agreements", new
        {
            code = $"AVAL{nombre.ToUpperInvariant()[..Math.Min(4, nombre.Length)]}", name = "Pasarela de pruebas", kind = "Gateway", ownBankAccountPublicId = cuenta,
            formatPublicId = formato, referenceMode = "ByProduct", referenceLength = 0, checkDigit = "Modulo10",
        })).GetProperty("publicId").GetGuid();

        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Put, "/api/channel/settings", new
        {
            displayName = "Cooperativa de pruebas", primaryColor = "#1F4E79", paymentSessionMinutes = 15, requireOtpToPay = false, inactivityMinutes = 5,
            enablementPolicy = "Manual", allowNonAssociates = true, smsEnabled = false, dueReminderDays = 3, surplusPolicy = "AdvanceElseCreditBalance",
        });
        foreach (var tipo in new[] { "Terms", "DataPolicy" })
            await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/channel/legal-texts", new { kind = tipo, content = $"Texto de prueba de {tipo}." });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/channel/settings/enable");

        // Corte a medianoche: todo reverso de estas pruebas va por Tesorería.
        var destino = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/channel/destinations", new
        {
            name = "Avalpay de pruebas", collectionAgreementPublicId = convenio, offerPse = true, offerCard = false, reversalCutoffLocalTime = "00:00:00",
            isActive = true,
        })).GetProperty("publicId").GetGuid();
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/channel/concepts", new
        {
            kind = "Loan", lineCode = (string?)null, destinationPublicId = destino, allowDue = true, allowPartial = true, partialMinimum = 10_000m,
            allowPrepayment = false, allowPayoff = true, canCombine = true, defaultPrepaymentChoice = (string?)null, publishStatement = false,
            publishCertificate = false, publishTaxCertificate = false,
        });
        await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, "/api/channel/enablements", new { personPublicId = c.Persona });
        return new Escenario(c, convenio, destino, cuenta);
    }

    private static async Task<Guid> FormatoDePasarelaAsync(HttpClient http, string token, string nombre)
    {
        var definicion = $$"""
            { "code": "PAS-{{nombre.ToUpperInvariant()}}", "name": "Reporte de la pasarela", "scope": "PaymentGatewayFile", "validFrom": "2026-01-01", "kind": "Delimited",
              "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF", "uppercase": false, "stripAccents": false, "amountFormat": "Point2",
              "fileName": "PASARELA.csv", "contentType": "text/csv",
              "records": {
                "header": { "enabled": false, "fields": [] },
                "detail": { "enabled": true, "fields": [
                  { "order": 1, "name": "Fecha", "source": "MovementDate", "format": "yyyy-MM-dd", "required": true },
                  { "order": 2, "name": "Valor", "source": "Amount", "required": true },
                  { "order": 3, "name": "Referencia", "source": "MovementReference" },
                  { "order": 4, "name": "Secuencia", "source": "BankSequence" },
                  { "order": 5, "name": "Estado", "source": "Outcome" } ] },
                "trailer": { "enabled": false, "fields": [] } } }
            """;
        var resp = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/bank-file-formats", JsonDocument.Parse(definicion).RootElement);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("formatPublicId").GetGuid();
    }

    /// <summary>El cuerpo de un aviso de pago aprobado por <paramref name="valor"/> a un crédito. Es el mismo para la misma referencia, como el
    /// reenvío del canal: otro cuerpo con la misma referencia es otra huella y el ERP lo rechaza (<c>Operation.KeyReused</c>).</summary>
    public static object Aviso(Escenario e, string referencia, Guid credito, decimal valor, string modalidad = "Partial") => new
    {
        reference = referencia, destinationPublicId = e.Destino, total = valor, currency = "COP",
        approvedAt = new DateTimeOffset(e.Hoy.ToDateTime(new TimeOnly(9, 30)), TimeSpan.FromHours(-5)),
        gateway = new { requestId = 733112, internalReference = 1598634446, authorization = "3324451", receipt = "R1", franchise = "_PSE_", paymentMethodName = "PSE",
            issuerName = "Banco de pruebas" },
        items = new[] { new { conceptKey = $"LOAN:{credito:D}", modality = modalidad, amount = valor, prepaymentChoice = (string?)null } },
        payer = new { ip = "190.0.0.1", userAgent = "Navegador de pruebas", accountId = new Guid(MD5.HashData(System.Text.Encoding.UTF8.GetBytes(referencia))) },
    };

    public static Task<HttpResponseMessage> AvisarAsync(CentralIdentityApiFixture fx, HttpClient http, Escenario e, object aviso) =>
        ServicioAsync(fx, http, e.Cooperativa, HttpMethod.Post, $"/people/{e.Persona}/payment-notices", aviso);
}
