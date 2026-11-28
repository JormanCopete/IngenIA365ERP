using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Credentials;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ElectronicInvoicing.Channels.Conecta;

/// <summary>
/// El canal de IngenIA Conecta (feature 028; contracts/canal-conecta.md): un mismo adaptador registrado con dos códigos,
/// <c>CONECTA-PRUEBAS</c> (sólo <c>Testing</c>, tenant de Conecta en <c>Habilitation</c>) y <c>CONECTA</c> (sólo <c>Production</c>). Envía el
/// canónico traducido (<see cref="TraductorAConecta"/>) con <c>Idempotency-Key</c>, espera la decisión unos segundos consultando (nunca
/// reenviando, regla 7), y trae código único, estado y los tres artefactos. Cumple las ocho reglas de §3.3 de <c>dian.md</c>
/// (<c>CanalConectaConformidadTests</c>).
///
/// <para>
/// La URL no sale de la base: la vigencia guarda la clave de un extremo y el adaptador la busca en la lista blanca de
/// <c>ElectronicInvoicing:Channels:{canal}:Endpoints</c>. La credencial (<c>clientId</c>, <c>clientSecret</c>) llega resuelta del Secret.
/// Nada secreto se registra. Mientras Conecta no acepte el número del ERP (pedido D1), <c>AcceptsErpNumber</c> queda en falso y la verificación
/// lo dice: así nadie lo activa antes. (nuevo)
/// </para>
/// </summary>
public sealed class CanalConecta : ICanalDeEmisionElectronica, IDeclaraParametrosDelCanal, IDeclaraEsquemaDeCredenciales
{
    public const string CampoClientId = "clientId";
    public const string CampoClientSecret = "clientSecret";

    public const string ReglaExtremo = "CONECTA-EXTREMO";
    public const string ReglaCredencial = "CONECTA-CREDENCIAL";
    public const string ReglaNoOfrece = "CONECTA-NO-OFRECE";
    public const string ReglaNumeracionDelErp = "CONECTA-NUMERACION-DEL-ERP";
    public const string ReglaVerificacion = "CONECTA-VERIFICACION";

    /// <summary>El espacio de nombres de las claves de idempotencia derivadas (UUID v5). Fijo: cambiarlo duplicaría documentos.</summary>
    private static readonly Guid EspacioDeClaves = Guid.Parse("6f1c3c8e-2b0a-5e7d-9c41-028c0ec7a365");

    private readonly IHttpClientFactory _http;
    private readonly IOptions<ElectronicInvoicingOptions> _opciones;
    private readonly TokensDeConecta _tokens;
    private readonly ILogger _logger;
    private readonly TimeProvider _reloj;

    public CanalConecta(string codigo, IHttpClientFactory http, IOptions<ElectronicInvoicingOptions> opciones, TokensDeConecta tokens,
        ILogger<CanalConecta> logger, TimeProvider? reloj = null)
    {
        if (!EsquemaDelCanonico.EsConecta(codigo)) throw new ArgumentException($"«{codigo}» no es un canal de Conecta.", nameof(codigo));
        ChannelCode = codigo.Trim().ToUpperInvariant();
        _http = http;
        _opciones = opciones;
        _tokens = tokens;
        _logger = logger;
        _reloj = reloj ?? TimeProvider.System;
        EsDeProduccion = ChannelCode == EsquemaDelCanonico.CanalConecta;
        AmbienteExigido = EsDeProduccion ? DianEnvironment.Production : DianEnvironment.Testing;
    }

    public string ChannelCode { get; }

    public CapacidadesDelCanal Capacidades { get; } = new(
        new HashSet<ElectronicDocumentKind> { ElectronicDocumentKind.Invoice, ElectronicDocumentKind.CreditNote, ElectronicDocumentKind.DebitNote },
        AceptaNumeroDelErp: true,
        ContingenciaDelFacturador: false,
        InformaContingenciaDian: true,
        EsAsincrono: true,
        DevuelvePdf: true,
        PuedeEnviarCorreo: true,
        ConsultaRangos: false,
        ConsultaAdquirente: false,
        AdmiteClaveDeIdempotencia: true);

    public DianEnvironment AmbienteExigido { get; }

    public bool EsDeProduccion { get; }

    public IReadOnlyList<string> Extremos => [.. Opciones.Endpoints.Keys.Order(StringComparer.Ordinal)];

    public string Nombre => EsDeProduccion ? "IngenIA Conecta – producción (validez legal)" : "IngenIA Conecta – pruebas";

    public IReadOnlyList<string> CamposDeCredencial { get; } = [CampoClientId, CampoClientSecret];

    private OpcionesDeCanal Opciones => _opciones.Value.Canal(ChannelCode);

    /// <summary>La clave que viaja a Conecta: UUID v5 de la clave del ERP (determinista, 36 caracteres, cabe en sus 64).</summary>
    public static string ClaveParaConecta(string claveDelErp) => UuidV5(EspacioDeClaves, claveDelErp).ToString("D");

    // ------------------------------------------------------------------------------------------ emisión --

    public async Task<ResultadoDeCanal> EmitirAsync(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        var preparada = Preparar(contexto, cronometro);
        if (preparada.Falla is { } falla) return falla;
        var sesion = preparada.Sesion!;

        if (documento.Environment != contexto.Environment)
            return Invalido(TraductorDeResultadosConecta.ReglaAmbiente, "CONECTA: el documento y el canal son de ambientes distintos.",
                "El documento es de un ambiente distinto al del canal: no se envía.", cronometro);

        var claveDelErp = contexto.IdempotencyKey ?? ContextoDeCanal.ClaveDeIdempotencia(contexto.TenantPublicId, documento.Environment,
            documento.Number.Prefix, documento.Number.Consecutive, 1);
        var (peticion, errores) = TraductorAConecta.Traducir(documento, sesion.Tenant, sesion.EnviarCorreo, claveDelErp);
        if (peticion is null)
            return new ResultadoDeCanal(ChannelOutcome.InvalidData, null, null, null, null, null, errores, [], null, "CONECTA", null,
                cronometro.ElapsedMilliseconds);

        HttpResponseMessage? respuesta;
        ResultadoDeCanal? sinToken;
        try
        {
            (respuesta, sinToken) = await EnviarAsync(sesion, emision: true, () =>
            {
                var m = new HttpRequestMessage(HttpMethod.Post, new Uri(sesion.Extremo, peticion.Ruta))
                {
                    Content = JsonContent.Create(peticion.Cuerpo, peticion.Cuerpo.GetType(), options: JsonDeConecta.Opciones),
                };
                m.Headers.Add("Idempotency-Key", ClaveParaConecta(claveDelErp));
                return m;
            }, cronometro, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Registrar("emitir", documento.Number.Full, null, cronometro, ex);
            return TraductorDeResultadosConecta.DeExcepcion(ex, emision: true, cronometro.ElapsedMilliseconds);
        }
        if (sinToken is not null) return sinToken;

        using (respuesta)
        {
            Registrar("emitir", documento.Number.Full, (int)respuesta!.StatusCode, cronometro);
            if (respuesta.StatusCode == HttpStatusCode.Conflict)
            {
                var problema = ProblemaConecta.Leer(await respuesta.Content.ReadAsStringAsync(ct));
                // Pedido D1: el número ya está en Conecta con otra clave. «Ya existe» no es un error (regla 3): se consulta. Pero sólo se adopta
                // si es este documento: un número que ocupa otro (emitido desde Conecta, o una resolución del ERP que se cruza con su
                // contador) no se toma como propio.
                if (problema?.EsDelTipo("number-conflict") == true || problema?.Type?.Contains("number-conflict", StringComparison.OrdinalIgnoreCase) == true)
                    return await AdoptarSiEsElMismoAsync(sesion, documento, problema, cronometro, ct);
                return TraductorDeResultadosConecta.DeError(respuesta.StatusCode, problema, cronometro.ElapsedMilliseconds);
            }
            if (!respuesta.IsSuccessStatusCode)
                return TraductorDeResultadosConecta.DeError(respuesta.StatusCode, ProblemaConecta.Leer(await respuesta.Content.ReadAsStringAsync(ct)),
                    cronometro.ElapsedMilliseconds);

            EmisionConecta? emitido;
            try
            {
                emitido = await respuesta.Content.ReadFromJsonAsync<EmisionConecta>(JsonDeConecta.Opciones, ct);
            }
            catch (System.Text.Json.JsonException)
            {
                emitido = null;
            }
            if (emitido is null)
                return ResultadoDeCanal.Sin(ChannelOutcome.InProcess, cronometro.ElapsedMilliseconds,
                    new MensajeDelCanal(TraductorDeResultadosConecta.ReglaServidor, TipoDeMensajeDelCanal.Notificacion,
                        "CONECTA: respuesta de la emisión ilegible.", "Conecta respondió algo ilegible: se consulta antes de reenviar."));

            if (!string.Equals(emitido.DocumentNumber, documento.Number.Full, StringComparison.OrdinalIgnoreCase))
                return Invalido(TraductorDeResultadosConecta.ReglaNumero,
                    $"CONECTA: se envió el número {documento.Number.Full} y Conecta registró {emitido.DocumentNumber}.",
                    "Conecta numeró distinto al ERP: el documento no se da por emitido. Avise a soporte.", cronometro, emitido.Id);
            if (!TraductorDeResultadosConecta.MismoAmbiente(contexto.Environment, emitido.Environment))
                return Invalido(TraductorDeResultadosConecta.ReglaAmbiente,
                    $"CONECTA: el documento es de {contexto.Environment} y Conecta lo registró en {emitido.Environment}.",
                    "La cooperativa en Conecta está en otro ambiente que el canal: pruebas y producción no se mezclan.", cronometro, emitido.Id);

            var avisos = new List<MensajeDelCanal>(peticion.Avisos);
            if (emitido.Totals is { } totales && totales.PayableAmount != documento.Totals.Payable)
                avisos.Add(new MensajeDelCanal(TraductorDeResultadosConecta.ReglaTotales, TipoDeMensajeDelCanal.Notificacion,
                    string.Create(CultureInfo.InvariantCulture,
                        $"CONECTA: total a pagar del ERP {documento.Totals.Payable:0.00}, de Conecta {totales.PayableAmount:0.00}."),
                    "El total que calculó Conecta difiere del ERP: revise el documento (caso b si hace falta)."));

            var resultado = await EsperarDecisionAsync(sesion, emitido.Id, emitido.QrUrl, cronometro, ct);
            return avisos.Count == 0 ? resultado : resultado with { Mensajes = [.. avisos, .. resultado.Mensajes] };
        }
    }

    /// <summary>
    /// Ante <c>number-conflict</c>: busca el documento que ocupa el número y lo adopta sólo si su total a pagar es el de este documento. Si
    /// no se ve todavía (la carrera la detectó el índice de Conecta), queda en proceso y se consulta después.
    /// </summary>
    private async Task<ResultadoDeCanal> AdoptarSiEsElMismoAsync(Sesion sesion, DocumentoElectronicoCanonico documento, ProblemaConecta problema,
        Stopwatch cronometro, CancellationToken ct)
    {
        var (existente, sinDoc) = await BuscarPorNumeroAsync(sesion, Referencia(documento), cronometro, ct);
        if (sinDoc is not null)
            return sinDoc.Outcome == ChannelOutcome.NotFound
                ? ResultadoDeCanal.Sin(ChannelOutcome.InProcess, cronometro.ElapsedMilliseconds,
                    new MensajeDelCanal(TraductorDeResultadosConecta.ReglaNotificacion, TipoDeMensajeDelCanal.Notificacion,
                        $"CONECTA: {problema.Texto}", "Conecta dice que el número ya está en uso, pero todavía no lo muestra: se consulta en seguida."))
                : sinDoc;
        if (existente!.Totals is { } totales && totales.PayableAmount != documento.Totals.Payable)
            return Invalido(TraductorDeResultadosConecta.ReglaNumeroOcupado,
                string.Create(CultureInfo.InvariantCulture,
                    $"CONECTA: el número {documento.Number.Full} lo ocupa en Conecta otro documento ({existente.Id:D}, {existente.Status}, total {totales.PayableAmount:0.00}; el del ERP es {documento.Totals.Payable:0.00})."),
                "El número ya lo usa en Conecta otro documento: no se adopta como propio. Revise la numeración del ERP frente al contador de Conecta y avise a soporte.",
                cronometro, existente.Id);
        return await InterpretarAsync(sesion, existente, null, cronometro, ct);
    }

    /// <summary>Consulta hasta <c>SyncWaitSeconds</c> por el id (consultar no es reenviar). Sin decisión, <c>InProcess</c> con la referencia.</summary>
    private async Task<ResultadoDeCanal> EsperarDecisionAsync(Sesion sesion, Guid id, string? qr, Stopwatch cronometro, CancellationToken ct)
    {
        var limite = TimeSpan.FromSeconds(Math.Max(0, Opciones.SyncWaitSeconds));
        var intervalo = TimeSpan.FromMilliseconds(Math.Max(50, Opciones.PollIntervalMs));
        var desde = _reloj.GetTimestamp();
        ResultadoDeCanal? ultimo = null;
        do
        {
            ultimo = await PorIdAsync(sesion, id, qr, cronometro, ct);
            if (ultimo.Outcome != ChannelOutcome.InProcess && ultimo.Outcome != ChannelOutcome.ChannelUnavailable) return ultimo;
            if (_reloj.GetElapsedTime(desde) + intervalo > limite) break;
            await Task.Delay(intervalo, _reloj, ct);
        }
        while (true);

        return ResultadoDeCanal.Sin(ChannelOutcome.InProcess, cronometro.ElapsedMilliseconds,
                new MensajeDelCanal(TraductorDeResultadosConecta.ReglaNotificacion, TipoDeMensajeDelCanal.Notificacion,
                    "CONECTA: recibido, sin decisión todavía.", "Conecta recibió el documento; la decisión de la DIAN se consulta en seguida."))
            with { ExternalReference = id.ToString("D") };
    }

    // ------------------------------------------------------------------------------------------ consulta --

    public async Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        var preparada = Preparar(contexto, cronometro);
        if (preparada.Falla is { } falla) return falla;
        var sesion = preparada.Sesion!;
        try
        {
            if (Guid.TryParse(referencia.ExternalReference, out var id)) return await PorIdAsync(sesion, id, null, cronometro, ct);

            var (documento, sinDoc) = await BuscarPorNumeroAsync(sesion, referencia, cronometro, ct);
            if (sinDoc is not null) return sinDoc;
            return await InterpretarAsync(sesion, documento!, null, cronometro, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Registrar("consultar", referencia.Numero, null, cronometro, ex);
            return TraductorDeResultadosConecta.DeExcepcion(ex, emision: false, cronometro.ElapsedMilliseconds);
        }
    }

    private async Task<ResultadoDeCanal> PorIdAsync(Sesion sesion, Guid id, string? qr, Stopwatch cronometro, CancellationToken ct)
    {
        var (respuesta, sinToken) = await EnviarAsync(sesion, emision: false,
            () => new HttpRequestMessage(HttpMethod.Get, new Uri(sesion.Extremo, $"/v1/documents/{id:D}")), cronometro, ct);
        if (sinToken is not null) return sinToken;
        using (respuesta)
        {
            if (respuesta!.StatusCode == HttpStatusCode.NotFound) return ResultadoDeCanal.Sin(ChannelOutcome.NotFound, cronometro.ElapsedMilliseconds);
            if (!respuesta.IsSuccessStatusCode) return NoConsultable(respuesta.StatusCode, cronometro);
            var detalle = await respuesta.Content.ReadFromJsonAsync<DetalleConecta>(JsonDeConecta.Opciones, ct);
            if (detalle?.Document is null) return NoConsultable(respuesta.StatusCode, cronometro);
            return await InterpretarAsync(sesion, detalle.Document, qr, cronometro, ct, detalle.Timeline);
        }
    }

    private async Task<(DocumentoConecta?, ResultadoDeCanal?)> BuscarPorNumeroAsync(Sesion sesion, ReferenciaDeEnvio referencia, Stopwatch cronometro,
        CancellationToken ct)
    {
        var ruta = string.Create(CultureInfo.InvariantCulture,
            $"/v1/documents?prefix={Uri.EscapeDataString(referencia.Prefix)}&number={referencia.Consecutive}&limit=10");
        var (respuesta, sinToken) = await EnviarAsync(sesion, emision: false,
            () => new HttpRequestMessage(HttpMethod.Get, new Uri(sesion.Extremo, ruta)), cronometro, ct);
        if (sinToken is not null) return (null, sinToken);
        using (respuesta)
        {
            if (!respuesta!.IsSuccessStatusCode) return (null, NoConsultable(respuesta.StatusCode, cronometro));
            var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaConecta>(JsonDeConecta.Opciones, ct);
            var mismos = (pagina?.Items ?? [])
                .Where(d => string.Equals(d.DocumentNumber, referencia.Numero, StringComparison.OrdinalIgnoreCase))
                .Where(d => TraductorDeResultadosConecta.MismoAmbiente(referencia.Environment, d.Environment))
                .OrderByDescending(d => d.CreatedAt)
                .ToList();
            if (mismos.Count == 0) return (null, ResultadoDeCanal.Sin(ChannelOutcome.NotFound, cronometro.ElapsedMilliseconds));
            var vivos = mismos.Where(d => TraductorDeResultadosConecta.Leer(d.Status) != LecturaDeEstadoConecta.Rechazado).ToList();
            if (vivos.Count > 1)
                return (null, new ResultadoDeCanal(ChannelOutcome.InvalidData, null, null, null, null, null,
                    [TraductorDeResultadosConecta.Rechazo(TraductorDeResultadosConecta.ReglaDuplicado,
                        $"CONECTA: hay {vivos.Count} documentos vivos con el número {referencia.Numero}.",
                        "Conecta tiene el mismo número dos veces: avise a soporte antes de corregir.")],
                    [], null, "DUPLICADO", null, cronometro.ElapsedMilliseconds));
            return (vivos.FirstOrDefault() ?? mismos[0], null);
        }
    }

    /// <summary>Lee el estado y, si hace falta, descarga los artefactos que lo sostienen (§7, §8).</summary>
    private async Task<ResultadoDeCanal> InterpretarAsync(Sesion sesion, DocumentoConecta d, string? qr, Stopwatch cronometro, CancellationToken ct,
        IReadOnlyList<EntradaDeLineaDeTiempoConecta>? lineaDeTiempo = null)
    {
        var artefactos = new List<ArtefactoDelCanal>();
        var lectura = TraductorDeResultadosConecta.Leer(d.Status);
        var tieneCodigo = !string.IsNullOrWhiteSpace(d.UniqueCode);
        if (lectura == LecturaDeEstadoConecta.Validado && tieneCodigo)
        {
            foreach (var tipo in new[] { TipoDeArtefacto.ApplicationResponse, TipoDeArtefacto.XmlFirmado, TipoDeArtefacto.AttachedDocument })
            {
                var a = await ArtefactoAsync(sesion, d, tipo, cronometro, ct);
                if (a is null) break;
                artefactos.Add(a);
            }
        }
        else if (lectura == LecturaDeEstadoConecta.Rechazado)
        {
            if (await ArtefactoAsync(sesion, d, TipoDeArtefacto.ApplicationResponse, cronometro, ct) is { } respuestaDian) artefactos.Add(respuestaDian);
        }
        else if (lectura == LecturaDeEstadoConecta.Contingencia && tieneCodigo)
        {
            if (await ArtefactoAsync(sesion, d, TipoDeArtefacto.XmlFirmado, cronometro, ct) is { } firmado) artefactos.Add(firmado);
        }

        qr ??= Qr(artefactos.FirstOrDefault(a => a.Tipo == TipoDeArtefacto.XmlFirmado));
        var validadoEn = lineaDeTiempo?
            .Where(e => e.ToStatus is "Accepted" or "AcceptedWithNotices")
            .Select(e => (DateTimeOffset?)e.At)
            .FirstOrDefault();
        return TraductorDeResultadosConecta.DeDocumento(d, artefactos, qr, validadoEn, cronometro.ElapsedMilliseconds);
    }

    private async Task<ArtefactoDelCanal?> ArtefactoAsync(Sesion sesion, DocumentoConecta d, TipoDeArtefacto tipo, Stopwatch cronometro, CancellationToken ct)
    {
        var (ruta, sufijo) = tipo switch
        {
            TipoDeArtefacto.XmlFirmado => ("xml", "xml"),
            TipoDeArtefacto.ApplicationResponse => ("dian-response", "respuesta-dian"),
            _ => ("attached", "attached"),
        };
        var (respuesta, sinToken) = await EnviarAsync(sesion, emision: false,
            () => new HttpRequestMessage(HttpMethod.Get, new Uri(sesion.Extremo, $"/v1/documents/{d.Id:D}/{ruta}")), cronometro, ct);
        if (sinToken is not null) return null;
        using (respuesta)
        {
            if (!respuesta!.IsSuccessStatusCode) return null;
            var bytes = await respuesta.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0) return null;
            var tipoDeContenido = respuesta.Content.Headers.ContentType?.MediaType ?? "application/xml";
            var extension = tipoDeContenido.Contains("zip", StringComparison.OrdinalIgnoreCase) ? "zip" : "xml";
            return new ArtefactoDelCanal(tipo, $"{d.DocumentNumber}-{sufijo}.{extension}", tipoDeContenido, bytes);
        }
    }

    public async Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        var cronometro = Stopwatch.StartNew();
        var preparada = Preparar(contexto, cronometro);
        if (preparada.Falla is { } falla) return falla;
        var sesion = preparada.Sesion!;
        try
        {
            DocumentoConecta? documento;
            if (Guid.TryParse(referencia.ExternalReference, out var id))
                documento = new DocumentoConecta(id, string.Empty, null, referencia.Numero, null, referencia.UniqueCode, null, string.Empty, null, null,
                    null, TraductorDeResultadosConecta.Ambiente(referencia.Environment), null, default, default);
            else
            {
                var (encontrado, sinDoc) = await BuscarPorNumeroAsync(sesion, referencia, cronometro, ct);
                if (sinDoc is not null) return sinDoc;
                documento = encontrado;
            }
            var artefacto = await ArtefactoAsync(sesion, documento!, tipo, cronometro, ct);
            return artefacto is null
                ? ResultadoDeCanal.Sin(ChannelOutcome.NotFound, cronometro.ElapsedMilliseconds)
                : new ResultadoDeCanal(ChannelOutcome.Validated, referencia.UniqueCode, null, null, null, null, [], [artefacto],
                    documento!.Id.ToString("D"), null, null, cronometro.ElapsedMilliseconds);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return TraductorDeResultadosConecta.DeExcepcion(ex, emision: false, cronometro.ElapsedMilliseconds);
        }
    }

    // ------------------------------------------------------------------------------------------ verificación --

    public async Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        var preparada = Preparar(contexto, cronometro);
        if (preparada.Falla is { } falla) return falla;
        var sesion = preparada.Sesion!;
        try
        {
            var fallas = new List<MensajeDelCanal>();
            var (r1, sinToken) = await EnviarAsync(sesion, emision: false,
                () => new HttpRequestMessage(HttpMethod.Get, new Uri(sesion.Extremo, $"/v1/tenants/{sesion.Tenant:D}/emission-channel")), cronometro, ct);
            if (sinToken is not null) return sinToken;
            PanoramaDelCanalConecta? panorama;
            using (r1)
            {
                if (!r1!.IsSuccessStatusCode)
                    return TraductorDeResultadosConecta.DeError(r1.StatusCode, ProblemaConecta.Leer(await r1.Content.ReadAsStringAsync(ct)),
                        cronometro.ElapsedMilliseconds);
                panorama = await r1.Content.ReadFromJsonAsync<PanoramaDelCanalConecta>(JsonDeConecta.Opciones, ct);
            }
            var (r2, _) = await EnviarAsync(sesion, emision: false,
                () => new HttpRequestMessage(HttpMethod.Get, new Uri(sesion.Extremo, $"/v1/tenants/{sesion.Tenant:D}")), cronometro, ct);
            TenantConecta? tenant = null;
            using (r2)
            {
                if (r2 is { IsSuccessStatusCode: true }) tenant = await r2.Content.ReadFromJsonAsync<TenantConecta>(JsonDeConecta.Opciones, ct);
            }

            var esperado = TraductorDeResultadosConecta.Ambiente(contexto.Environment);
            var ambiente = panorama?.Current?.Environment ?? tenant?.Environment;
            if (!TraductorDeResultadosConecta.MismoAmbiente(contexto.Environment, ambiente))
                fallas.Add(Falla(TraductorDeResultadosConecta.ReglaAmbiente,
                    $"La cooperativa en Conecta está en {ambiente ?? "un ambiente desconocido"} y el modo {ChannelCode} exige {esperado}."));
            foreach (var falta in panorama?.Missing ?? [])
                fallas.Add(Falla(ReglaVerificacion, $"Conecta dice que le falta: {falta}"));
            if (tenant is not null && !string.IsNullOrWhiteSpace(contexto.IssuerTaxId)
                && !string.Equals(tenant.Nit.Trim(), contexto.IssuerTaxId.Trim(), StringComparison.Ordinal))
                fallas.Add(Falla(ReglaVerificacion, $"La cooperativa de Conecta tiene el NIT {tenant.Nit} y el emisor configurado es {contexto.IssuerTaxId}."));
            if (!Opciones.ConectaAceptaNumeroDelErp)
                fallas.Add(Falla(ReglaNumeracionDelErp,
                    "Conecta todavía no acepta el número que asigna el ERP (pedido D1 de la feature 028): el canal no se puede activar."));
            else if (tenant?.Resolutions is { } resoluciones)
                foreach (var prefijo in (panorama?.NumberingLinks ?? []).Select(l => l.Prefix).Distinct(StringComparer.OrdinalIgnoreCase))
                    if (!resoluciones.Any(r => string.Equals(r.Prefix, prefijo, StringComparison.OrdinalIgnoreCase) && r.ClientAssignsNumber))
                        fallas.Add(Falla(TraductorDeResultadosConecta.ReglaNumeracionDelCliente,
                            $"El prefijo {prefijo} no recibe en Conecta el número del ERP (clientAssignsNumber apagado): pídalo al administrador de Conecta."));

            var canal = panorama?.Current is { } c
                ? $"{(c.Kind == "ExternalProvider" ? $"proveedor {c.ProviderCode}" : "motor propio")}"
                : "sin canal";
            // Con cada prefijo, el contador de Conecta: la numeración del ERP no debe ocupar números que Conecta ya usó.
            var prefijos = string.Join(", ", (panorama?.NumberingLinks ?? []).Select(l => l.Prefix).Distinct(StringComparer.Ordinal)
                .Select(p => tenant?.Resolutions?.FirstOrDefault(r => string.Equals(r.Prefix, p, StringComparison.OrdinalIgnoreCase)) is { } r
                    ? string.Create(CultureInfo.InvariantCulture, $"{p} (va en {r.CurrentNumber})")
                    : p));
            var resumen = $"Conecta ({sesion.Clave}) · cooperativa {tenant?.LegalName ?? sesion.Tenant.ToString("D")} · {ambiente} · {canal}" +
                (prefijos.Length > 0 ? $" · prefijos enlazados: {prefijos}" : string.Empty);

            return fallas.Count == 0
                ? ResultadoDeCanal.Sin(ChannelOutcome.Validated, cronometro.ElapsedMilliseconds,
                    new MensajeDelCanal(ReglaVerificacion, TipoDeMensajeDelCanal.Notificacion, resumen, resumen))
                : ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable, cronometro.ElapsedMilliseconds,
                    [new MensajeDelCanal(ReglaVerificacion, TipoDeMensajeDelCanal.Notificacion, resumen, resumen), .. fallas]);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Registrar("probar", sesion.Tenant.ToString("D"), null, cronometro, ex);
            return TraductorDeResultadosConecta.DeExcepcion(ex, emision: false, cronometro.ElapsedMilliseconds);
        }

        static MensajeDelCanal Falla(string regla, string texto) => TraductorDeResultadosConecta.Rechazo(regla, texto, texto);
    }

    // ------------------------------------------------------------------------------------------ lo que no ofrece --

    public Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct) => NoOfrece("eventos RADIAN");

    public Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct) => NoOfrece("la consulta de rangos");

    public Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto, CancellationToken ct) =>
        NoOfrece("la consulta del adquirente");

    private static Task<ResultadoDeCanal> NoOfrece(string que) =>
        Task.FromResult(ResultadoDeCanal.Sin(ChannelOutcome.InvalidData, 0,
            TraductorDeResultadosConecta.Rechazo(ReglaNoOfrece, $"CONECTA: el canal no ofrece {que}.", $"IngenIA Conecta todavía no ofrece {que} al ERP.")));

    // ------------------------------------------------------------------------------------------ transporte --

    private sealed record Sesion(Uri Extremo, string Clave, Guid Tenant, string ClientId, string ClientSecret, bool EnviarCorreo);

    private sealed record Preparacion(Sesion? Sesion, ResultadoDeCanal? Falla);

    /// <summary>El ambiente, el tenant, el extremo de la lista blanca y la credencial; sin llamar a nadie.</summary>
    private Preparacion Preparar(ContextoDeCanal contexto, Stopwatch cronometro)
    {
        ResultadoDeCanal No(ChannelOutcome outcome, string regla, string texto) =>
            ResultadoDeCanal.Sin(outcome, cronometro.ElapsedMilliseconds, TraductorDeResultadosConecta.Rechazo(regla, $"CONECTA: {texto}", texto));

        if (contexto.Environment != AmbienteExigido)
            return new(null, No(ChannelOutcome.InvalidData, TraductorDeResultadosConecta.ReglaAmbiente,
                $"El canal {ChannelCode} sólo opera en el ambiente {(EsDeProduccion ? "de producción" : "de pruebas")}."));
        if (contexto.Canal?.TenantId is not { } tenant || tenant == Guid.Empty)
            return new(null, No(ChannelOutcome.ChannelUnavailable, TraductorDeResultadosConecta.ReglaTenant,
                "Falta la cooperativa de Conecta en la configuración de emisión."));
        var clave = contexto.Canal.Endpoint?.Trim();
        var url = clave is null ? null : Opciones.Endpoints.FirstOrDefault(e => string.Equals(e.Key, clave, StringComparison.OrdinalIgnoreCase)).Value;
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var extremo) || !ExtremoSeguro(extremo))
            return new(null, No(ChannelOutcome.ChannelUnavailable, ReglaExtremo,
                $"El servicio «{clave}» no está habilitado para el canal {ChannelCode} en esta instalación."));
        if (contexto.Credenciales?.Valores is not { } valores
            || !valores.TryGetValue(CampoClientId, out var clientId) || string.IsNullOrWhiteSpace(clientId)
            || !valores.TryGetValue(CampoClientSecret, out var secreto) || string.IsNullOrWhiteSpace(secreto))
            return new(null, No(ChannelOutcome.ChannelUnavailable, ReglaCredencial,
                "Falta la credencial de Conecta (clientId y clientSecret) en el secreto de facturación electrónica."));
        return new(new Sesion(extremo, clave!.ToUpperInvariant(), tenant, clientId.Trim(), secreto, contexto.Canal.CanalEntregaCorreo), null);
    }

    /// <summary>Sólo https, salvo un servidor local (pruebas y desarrollo).</summary>
    public static bool ExtremoSeguro(Uri extremo) => extremo.Scheme == Uri.UriSchemeHttps || extremo.IsLoopback;

    /// <summary>
    /// Pide el token y envía; ante un 401 renueva el token <b>una</b> vez y repite (la petición no fue aceptada: no es un reintento de la emisión).
    /// </summary>
    private async Task<(HttpResponseMessage?, ResultadoDeCanal?)> EnviarAsync(Sesion sesion, bool emision, Func<HttpRequestMessage> crear,
        Stopwatch cronometro, CancellationToken ct)
    {
        var cliente = _http.CreateClient(emision ? DependencyInjection.ClienteDeEmision(ChannelCode) : DependencyInjection.ClienteDeConsulta(ChannelCode));
        var deToken = _http.CreateClient(DependencyInjection.ClienteDeConsulta(ChannelCode));
        for (var intento = 0; ; intento++)
        {
            var token = await _tokens.ObtenerAsync(deToken, sesion.Extremo, sesion.ClientId, sesion.ClientSecret, renovar: intento > 0, ct);
            if (!token.Exito)
                return (null, ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable, cronometro.ElapsedMilliseconds,
                    TraductorDeResultadosConecta.Rechazo(TraductorDeResultadosConecta.ReglaToken, $"CONECTA: {token.Falla}", token.Falla!)));

            using var peticion = crear();
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            peticion.Headers.Add("X-Tenant-Id", sesion.Tenant.ToString("D"));
            var respuesta = await cliente.SendAsync(peticion, ct);
            if (respuesta.StatusCode != HttpStatusCode.Unauthorized || intento > 0) return (respuesta, null);
            respuesta.Dispose();
        }
    }

    private static ResultadoDeCanal NoConsultable(HttpStatusCode estado, Stopwatch cronometro) =>
        ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable, cronometro.ElapsedMilliseconds,
            new MensajeDelCanal(TraductorDeResultadosConecta.ReglaServidor, TipoDeMensajeDelCanal.Notificacion,
                $"CONECTA: la consulta respondió HTTP {(int)estado}.", "No se pudo consultar a Conecta; se vuelve a intentar.")) with
        {
            ProviderCode = $"HTTP {(int)estado}",
        };

    private ResultadoDeCanal Invalido(string regla, string texto, string traduccion, Stopwatch cronometro, Guid? id = null) =>
        new(ChannelOutcome.InvalidData, null, null, null, null, null, [TraductorDeResultadosConecta.Rechazo(regla, texto, traduccion)], [],
            id?.ToString("D"), "CONECTA", null, cronometro.ElapsedMilliseconds);

    private static ReferenciaDeEnvio Referencia(DocumentoElectronicoCanonico d) => new(d.Number.Prefix, d.Number.Consecutive, d.Environment);

    /// <summary>El <c>sts:QRCode</c> del XML firmado (anexo 1.9), si lo trae.</summary>
    public static string? Qr(ArtefactoDelCanal? xml)
    {
        if (xml is null) return null;
        try
        {
            using var flujo = new MemoryStream(xml.Bytes);
            var doc = XDocument.Load(flujo);
            return doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "QRCode")?.Value.Trim() is { Length: > 0 } qr ? qr : null;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private void Registrar(string operacion, string numero, int? http, Stopwatch cronometro, Exception? ex = null)
    {
        if (ex is null)
            _logger.LogInformation("[FE.Conecta] {Canal} {Operacion} {Numero} HTTP {Http} {Ms} ms", ChannelCode, operacion, numero, http,
                cronometro.ElapsedMilliseconds);
        else
            _logger.LogWarning("[FE.Conecta.Transporte] {Canal} {Operacion} {Numero} {Error} {Ms} ms", ChannelCode, operacion, numero,
                ex.GetType().Name, cronometro.ElapsedMilliseconds);
    }

    /// <summary>UUID versión 5 (RFC 9562 §5.5): SHA-1 del espacio y el nombre.</summary>
    private static Guid UuidV5(Guid espacio, string nombre)
    {
        var bytesDelEspacio = espacio.ToByteArray(bigEndian: true);
        var bytesDelNombre = Encoding.UTF8.GetBytes(nombre);
        var hash = SHA1.HashData([.. bytesDelEspacio, .. bytesDelNombre]);
        var u = hash[..16];
        u[6] = (byte)((u[6] & 0x0F) | 0x50);
        u[8] = (byte)((u[8] & 0x3F) | 0x80);
        return new Guid(u, bigEndian: true);
    }
}
