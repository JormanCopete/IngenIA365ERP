using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales.Conecta;

/// <summary>
/// Un IngenIA Conecta falso en memoria (feature 028, T028): token, emisión idempotente por <c>Idempotency-Key</c>, consulta por id y por número,
/// artefactos y tenant, con las formas del OpenAPI de Conecta. Decide por el <b>último dígito</b> de la identificación del adquirente, como
/// el simulado: 0 y otros validado, 1 rechazado, 2 con notificaciones, 3 se registra pero la respuesta no llega (tiempo agotado), 4 la DIAN no
/// está (contingencia con CUFE y XML), 5 la conexión no sale. Cuenta las peticiones de emisión por número. (nuevo)
/// </summary>
public sealed class ConectaStubHandler : HttpMessageHandler
{
    public sealed class Doc
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Numero { get; init; } = "";
        public string Prefijo { get; init; } = "";
        public long Consecutivo { get; init; }
        public string Familia { get; init; } = "Invoice";
        public string Estado { get; set; } = "Queued";
        public string? Codigo { get; set; }
        public string Ambiente { get; init; } = "Habilitation";
        public decimal Total { get; init; }
        public DateTimeOffset Creado { get; init; } = DateTimeOffset.UtcNow;
        public int ConsultasHastaDecidir { get; set; }
        public string EstadoFinal { get; init; } = "Rendered";
        public string? MensajeDian { get; init; }
    }

    public ConcurrentDictionary<string, Doc> PorClave { get; } = new();
    public ConcurrentDictionary<Guid, Doc> PorId { get; } = new();
    public ConcurrentDictionary<string, int> EmisionesPorNumero { get; } = new();
    public List<HttpRequestMessage> Peticiones { get; } = [];
    public List<string> Cuerpos { get; } = [];

    /// <summary>Ambiente del tenant que responde Conecta.</summary>
    public string AmbienteDelTenant { get; set; } = "Habilitation";
    public string NitDelTenant { get; set; } = "900123456";
    public List<string> Faltantes { get; } = [];

    /// <summary>Las resoluciones que devuelve <c>GET /v1/tenants/{id}</c>: prefijo, contador y si recibe el número del emisor (D1).</summary>
    public List<(string Prefijo, long Contador, bool DelCliente)> Resoluciones { get; } = [("SETT", 98597, true)];
    public int TokensEmitidos { get; private set; }

    /// <summary>Cuántas consultas tarda en decidir un documento nuevo (0 = decide en la primera).</summary>
    public int ConsultasParaDecidir { get; set; }

    /// <summary>Si se fija, la próxima emisión responde este número en lugar del enviado.</summary>
    public string? NumeroForzado { get; set; }

    /// <summary>Respuestas forzadas a la próxima emisión (código y cuerpo).</summary>
    public Queue<(HttpStatusCode Codigo, string Cuerpo)> RespuestasDeEmision { get; } = new();

    /// <summary>Cuántos 401 responde la API antes de aceptar el token.</summary>
    public int NoAutorizadosPendientes { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        lock (Peticiones) Peticiones.Add(request);
        var ruta = request.RequestUri!.AbsolutePath;
        var cuerpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        lock (Cuerpos) Cuerpos.Add(cuerpo);

        if (ruta.EndsWith("/openid-connect/token", StringComparison.Ordinal))
        {
            TokensEmitidos++;
            return Json(HttpStatusCode.OK, new { access_token = $"tok-{TokensEmitidos}", expires_in = 900 });
        }
        if (request.Headers.Authorization?.Parameter is null) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        if (NoAutorizadosPendientes > 0)
        {
            NoAutorizadosPendientes--;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (request.Method == HttpMethod.Post && ruta is "/v1/invoices" or "/v1/credit-notes" or "/v1/debit-notes")
            return Emitir(request, cuerpo, ruta);
        if (request.Method == HttpMethod.Get && ruta == "/v1/documents") return Listar(request);
        if (request.Method == HttpMethod.Get && ruta.StartsWith("/v1/documents/", StringComparison.Ordinal)) return Documento(ruta);
        if (request.Method == HttpMethod.Get && ruta.EndsWith("/emission-channel", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, new
            {
                current = new { id = Guid.NewGuid(), environment = AmbienteDelTenant, kind = "ExternalProvider", providerCode = "factible", providerName = "Factible", hasCredentials = true },
                numberingLinks = new[] { new { family = "Invoice", prefix = "SETT", providerCode = "factible" } },
                missing = Faltantes,
            });
        if (request.Method == HttpMethod.Get && ruta.StartsWith("/v1/tenants/", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, new
            {
                id = Guid.NewGuid(), nit = NitDelTenant, dv = "7", legalName = "COOPERATIVA DE PRUEBA", environment = AmbienteDelTenant,
                missingToIssue = Array.Empty<string>(),
                resolutions = Resoluciones.Select(r => new
                {
                    family = "Invoice", resolutionNumber = "18760000001", prefix = r.Prefijo, rangeFrom = 1L, rangeTo = 5000000L,
                    validFrom = "2019-01-19", validTo = "2030-01-19", currentNumber = r.Contador, status = "Active", hasTechnicalKey = true,
                    clientAssignsNumber = r.DelCliente,
                }).ToArray(),
            });
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private HttpResponseMessage Emitir(HttpRequestMessage request, string cuerpo, string ruta)
    {
        var json = JsonNode.Parse(cuerpo)!;
        var numero = json["prefix"]!.GetValue<string>() + json["number"]!.GetValue<long>();
        EmisionesPorNumero.AddOrUpdate(numero, 1, (_, n) => n + 1);

        if (RespuestasDeEmision.TryDequeue(out var forzada))
            return new HttpResponseMessage(forzada.Codigo) { Content = new StringContent(forzada.Cuerpo, Encoding.UTF8, "application/problem+json") };

        var idNumero = json["customer"]!["idNumber"]!.GetValue<string>();
        var digito = idNumero.LastOrDefault(char.IsAsciiDigit);
        if (digito == '5') throw new HttpRequestException(HttpRequestError.ConnectionError, "rehusada");

        var clave = request.Headers.GetValues("Idempotency-Key").Single();
        var replay = PorClave.TryGetValue(clave, out var doc);
        if (!replay)
        {
            doc = new Doc
            {
                Numero = NumeroForzado ?? numero,
                Prefijo = json["prefix"]!.GetValue<string>(),
                Consecutivo = json["number"]!.GetValue<long>(),
                Familia = ruta switch { "/v1/credit-notes" => "CreditNote", "/v1/debit-notes" => "DebitNote", _ => "Invoice" },
                Ambiente = AmbienteDelTenant,
                ConsultasHastaDecidir = ConsultasParaDecidir,
                EstadoFinal = digito switch { '1' => "Rejected", '2' => "AcceptedWithNotices", '4' => "Contingency", _ => "Rendered" },
                MensajeDian = digito == '1' ? "FAJ43 El NIT del adquirente no coincide con el DV" : digito == '2' ? "FAJ44b notificación" : null,
                Total = 4998m,
            };
            NumeroForzado = null;
            PorClave[clave] = doc;
            PorId[doc.Id] = doc;
        }
        if (digito == '3' && !replay) throw new TaskCanceledException("tiempo agotado tras enviar", new TimeoutException());

        var familiaNota = doc!.Familia != "Invoice";
        return Json(replay ? HttpStatusCode.OK : HttpStatusCode.Accepted, new
        {
            id = doc.Id,
            documentNumber = doc.Numero,
            cufe = familiaNota ? null : (string?)null,
            cude = (string?)null,
            qrUrl = (string?)null,
            status = "Queued",
            environment = doc.Ambiente,
            issueDate = "2026-12-05",
            issueTime = "10:00:00-05:00",
            totals = new { lineExtensionAmount = 4200m, taxExclusiveAmount = 4200m, taxInclusiveAmount = 4998m, allowanceTotalAmount = 0m, chargeTotalAmount = 0m, payableAmount = doc.Total, ivaAmount = 798m, incAmount = 0m, icaAmount = 0m },
            replayed = replay,
        });
    }

    private void Avanzar(Doc d)
    {
        if (d.Estado != "Queued") return;
        if (d.ConsultasHastaDecidir-- > 0) return;
        d.Estado = d.EstadoFinal;
        if (d.Estado != "Rejected") d.Codigo = $"cufe{d.Numero.ToLowerInvariant()}{new string('0', 40)}";
    }

    private object Resumen(Doc d) => new
    {
        id = d.Id, family = d.Familia, subtypeCode = d.Familia == "Invoice" ? (d.Estado == "Contingency" ? "04" : "01") : "91",
        documentNumber = d.Numero, issueDate = "2026-12-05", issueTime = "10:00:00", currency = "COP",
        customerIdType = "13", customerIdNumber = "1", customerName = "x",
        uniqueCode = d.Codigo, uniqueCodeAlgorithm = d.Familia == "Invoice" ? "Cufe" : "Cude", status = d.Estado,
        dianTrackId = (string?)null, dianStatusCode = d.Estado == "Rejected" ? "99" : d.Codigo is null ? null : "00",
        dianStatusMessage = d.MensajeDian, environment = d.Ambiente,
        totals = new { lineExtensionAmount = 4200m, taxExclusiveAmount = 4200m, taxInclusiveAmount = 4998m, allowanceTotalAmount = 0m, chargeTotalAmount = 0m, payableAmount = d.Total, ivaAmount = 798m, incAmount = 0m, icaAmount = 0m },
        referencedDocumentId = (Guid?)null, deliveredTo = (string?)null, deliveredAt = (DateTimeOffset?)null,
        createdAt = d.Creado, updatedAt = d.Creado.AddSeconds(2), providerCode = "factible", providerDocumentId = "77", providerStatusCode = (string?)null,
    };

    private HttpResponseMessage Listar(HttpRequestMessage request)
    {
        var q = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        var encontrados = PorId.Values.Where(d => d.Prefijo == q["prefix"] && d.Consecutivo.ToString() == q["number"]).ToList();
        foreach (var d in encontrados) Avanzar(d);
        return Json(HttpStatusCode.OK, new { items = encontrados.Select(Resumen).ToArray(), nextCursor = (string?)null });
    }

    private HttpResponseMessage Documento(string ruta)
    {
        var partes = ruta.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!Guid.TryParse(partes[2], out var id) || !PorId.TryGetValue(id, out var d)) return new HttpResponseMessage(HttpStatusCode.NotFound);
        if (partes.Length == 3)
        {
            Avanzar(d);
            return Json(HttpStatusCode.OK, new
            {
                document = Resumen(d),
                artifacts = Array.Empty<object>(),
                timeline = new[] { new { at = d.Creado.AddSeconds(1), kind = "status", fromStatus = "Sent", toStatus = d.Estado, description = "" } },
            });
        }
        var artefacto = partes[3];
        var listo = artefacto switch
        {
            "xml" => d.Codigo is not null,
            "dian-response" => d.Estado is "Rendered" or "AcceptedWithNotices" or "Rejected",
            "attached" => d.Estado is "Rendered" or "AcceptedWithNotices",
            _ => false,
        };
        if (!listo) return new HttpResponseMessage(HttpStatusCode.NotFound);
        var xml = artefacto == "xml"
            ? $"<Invoice xmlns:sts=\"dian:gov:co:facturaelectronica:Structures-2-1\"><sts:QRCode>https://catalogo-vpfe-hab.dian.gov.co/document/searchqr?documentkey={d.Codigo}</sts:QRCode></Invoice>"
            : $"<{artefacto}>{d.Numero}</{artefacto}>";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml, Encoding.UTF8, "application/xml") };
    }

    private static HttpResponseMessage Json(HttpStatusCode codigo, object cuerpo) =>
        new(codigo) { Content = JsonContent.Create(cuerpo, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
}

/// <summary>Una fábrica de clientes que entrega siempre el mismo manejador falso.</summary>
public sealed class FabricaDeClientesDePrueba(HttpMessageHandler manejador) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(manejador, disposeHandler: false);
}
