using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Security;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Shared.Services.Auditoria;

/// <summary>
/// Feature 009, US7 (FR-051): cada opción a la que entra la persona queda en la auditoría. El
/// layout lo llama en cada cambio de ruta; aquí se descarta la misma ruta consecutiva (un F5 o un
/// re-render no son dos ingresos), se encola y se manda a <c>POST /api/audit/access</c> en orden,
/// con tres intentos y espera creciente. Si los tres fallan, el evento se pierde —la auditoría
/// nunca frena a quien trabaja—, queda en el log y se avisa una sola vez por sesión (Principio IX).
/// Sin sesión no manda nada: no hay a quién atribuírselo.
/// </summary>
public sealed class RegistroDeAccesos(
    HttpClient http,
    CentralAuthClient auth,
    INotificationService notification,
    ILogger<RegistroDeAccesos> logger,
    IReadOnlyList<TimeSpan>? esperas = null)
{
    public const string Ruta = "/api/audit/access";
    public const string Aviso = "El registro de accesos no está disponible; puede seguir trabajando y quedará en el log.";

    private static readonly TimeSpan[] EsperasPorDefecto = [TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)];

    private readonly IReadOnlyList<TimeSpan> _esperas = esperas ?? EsperasPorDefecto;
    private readonly Queue<Acceso> _cola = new();
    private readonly object _candado = new();
    private Task? _trabajo;
    private string? _ultimaRuta;
    private bool _avisado;

    private sealed record Acceso(string Route, string Title);

    /// <summary>Cuántos intentos se hacen por evento (uno por espera, más el primero).</summary>
    public int Intentos => _esperas.Count;

    /// <summary>Registra el ingreso a una ruta; el título se deriva de la ruta si no viene.</summary>
    public void Registrar(string ruta, string? titulo = null)
    {
        var limpia = Normalizar(ruta);
        if (string.IsNullOrEmpty(limpia)) return;
        lock (_candado)
        {
            if (string.Equals(_ultimaRuta, limpia, StringComparison.Ordinal)) return;
            _ultimaRuta = limpia;
            _cola.Enqueue(new Acceso(limpia, string.IsNullOrWhiteSpace(titulo) ? TituloDe(limpia) : titulo.Trim()));
            _trabajo ??= Task.Run(ProcesarAsync);
        }
    }

    /// <summary>Espera a que la cola se vacíe (pruebas y cierre ordenado).</summary>
    public async Task VaciarAsync()
    {
        Task? t;
        lock (_candado) t = _trabajo;
        if (t is not null) await t;
    }

    private async Task ProcesarAsync()
    {
        while (true)
        {
            Acceso siguiente;
            lock (_candado)
            {
                if (_cola.Count == 0) { _trabajo = null; return; }
                siguiente = _cola.Dequeue();
            }
            await EnviarAsync(siguiente);
        }
    }

    private async Task EnviarAsync(Acceso acceso)
    {
        var token = auth.CurrentAccessToken;
        if (token is null) return;

        for (var intento = 0; intento < _esperas.Count; intento++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, Ruta) { Content = JsonContent.Create(new { route = acceso.Route, title = acceso.Title }) };
                using var resp = await http.SendAsync(req);
                if (resp.IsSuccessStatusCode) return;
                // Un 4xx no mejora reintentando: sin cooperativa activa o sin sesión válida no hay a quién atribuir el acceso.
                if ((int)resp.StatusCode is >= 400 and < 500 && resp.StatusCode != HttpStatusCode.RequestTimeout)
                {
                    logger.LogDebug("Acceso a {Ruta} no registrado: HTTP {Status}", acceso.Route, (int)resp.StatusCode);
                    return;
                }
                logger.LogWarning("Acceso a {Ruta}: HTTP {Status} en el intento {Intento}", acceso.Route, (int)resp.StatusCode, intento + 1);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Acceso a {Ruta}: error de red en el intento {Intento}", acceso.Route, intento + 1);
            }
            catch (TaskCanceledException ex)
            {
                logger.LogWarning(ex, "Acceso a {Ruta}: tiempo agotado en el intento {Intento}", acceso.Route, intento + 1);
            }
            if (intento < _esperas.Count - 1 && _esperas[intento] > TimeSpan.Zero) await Task.Delay(_esperas[intento]);
        }

        logger.LogError("Acceso a {Ruta} ({Titulo}) perdido tras {Intentos} intentos", acceso.Route, acceso.Title, _esperas.Count);
        if (_avisado) return;
        _avisado = true;
        await notification.WarningAsync(Aviso);
    }

    /// <summary>Sólo la ruta, sin esquema, host ni consulta; «/» para la raíz.</summary>
    public static string Normalizar(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return string.Empty;
        var texto = ruta.Trim();
        if (Uri.TryCreate(texto, UriKind.Absolute, out var absoluta)) texto = absoluta.AbsolutePath;
        var corte = texto.IndexOfAny(['?', '#']);
        if (corte >= 0) texto = texto[..corte];
        if (!texto.StartsWith('/')) texto = "/" + texto;
        return texto.Length > 1 ? texto.TrimEnd('/') : texto;
    }

    /// <summary>«/contabilidad/plan-de-cuentas» → «Contabilidad › Plan de cuentas»; los identificadores se omiten.</summary>
    public static string TituloDe(string ruta)
    {
        var partes = ruta.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !Guid.TryParse(p, out _) && !p.All(char.IsDigit))
            .Select(p => p.Replace('-', ' ').Replace('_', ' '))
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..])
            .ToList();
        return partes.Count == 0 ? "Inicio" : string.Join(" › ", partes);
    }
}
