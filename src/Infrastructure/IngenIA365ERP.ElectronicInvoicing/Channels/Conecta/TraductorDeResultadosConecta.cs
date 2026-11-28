using System.Globalization;
using System.Net;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.ElectronicInvoicing.Channels.Conecta;

/// <summary>Cómo se interpreta un estado de Conecta (contracts/canal-conecta.md §7).</summary>
public enum LecturaDeEstadoConecta
{
    /// <summary>Todavía no hay decisión.</summary>
    EnProceso = 1,

    /// <summary>Validado por la DIAN: se buscan los artefactos.</summary>
    Validado = 2,

    /// <summary>Rechazado.</summary>
    Rechazado = 3,

    /// <summary>Contingencia: es la 04 de la DIAN sólo si hay código único y documento firmado.</summary>
    Contingencia = 4,
}

/// <summary>
/// Las traducciones puras de las respuestas de Conecta a <see cref="ResultadoDeCanal"/> (feature 028, contracts/canal-conecta.md §6 y §7):
/// una prueba por fila. Ningún código crudo se pierde: va a <c>ProviderCode</c> (≤ 20) y <c>DianStatusCode</c> (≤ 10). (nuevo)
/// </summary>
public static class TraductorDeResultadosConecta
{
    public const string ReglaAuth = "CONECTA-AUTH";
    public const string ReglaToken = "CONECTA-TOKEN";
    public const string ReglaPermiso = "CONECTA-PERMISO";
    public const string ReglaTenant = "CONECTA-TENANT";
    public const string ReglaNumeracion = "CONECTA-NUMERACION";
    public const string ReglaCanal = "CONECTA-CANAL";
    public const string ReglaClave = "CONECTA-CLAVE";
    public const string ReglaValidacion = "CONECTA-VALIDACION";
    public const string ReglaLimite = "CONECTA-LIMITE";
    public const string ReglaServidor = "CONECTA-SERVIDOR";
    public const string ReglaConexion = "CONECTA-CONEXION";
    public const string ReglaNumero = "CONECTA-NUMERO";
    public const string ReglaAmbiente = "CONECTA-AMBIENTE";
    public const string ReglaTotales = "CONECTA-TOTALES";
    public const string ReglaDuplicado = "CONECTA-DUPLICADO";
    public const string ReglaRechazo = "CONECTA-RECHAZO";
    public const string ReglaNotificacion = "CONECTA-NOTIFICACION";
    public const string ReglaConflicto = "CONECTA-CONFLICTO";
    public const string ReglaNumeracionDelCliente = "CONECTA-NUMERACION-CLIENTE";
    public const string ReglaFueraDeRango = "CONECTA-FUERA-DE-RANGO";
    public const string ReglaNumeroOcupado = "CONECTA-NUMERO-OCUPADO";

    /// <summary>El ambiente de Conecta que corresponde al del ERP: <c>Testing</c> ↔ <c>Habilitation</c>.</summary>
    public static string Ambiente(DianEnvironment ambiente) => ambiente == DianEnvironment.Production ? "Production" : "Habilitation";

    /// <summary>¿El ambiente que respondió Conecta es el del ERP?</summary>
    public static bool MismoAmbiente(DianEnvironment ambiente, string? deConecta) =>
        string.Equals(Ambiente(ambiente), deConecta?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>La lectura de un <c>DocumentStatus</c> de Conecta.</summary>
    public static LecturaDeEstadoConecta Leer(string? estado) => estado?.Trim() switch
    {
        "Accepted" or "AcceptedWithNotices" or "Rendered" or "Delivered" or "Archived" => LecturaDeEstadoConecta.Validado,
        "Rejected" => LecturaDeEstadoConecta.Rechazado,
        "DianUnavailable" or "Contingency" => LecturaDeEstadoConecta.Contingencia,
        _ => LecturaDeEstadoConecta.EnProceso,
    };

    /// <summary>
    /// Una respuesta no exitosa de una emisión (§6). El 401 lo resuelve antes el canal (renueva el token una vez); si llega aquí, la credencial no
    /// sirve. El 409 de número en conflicto (pedido D1) no se traduce aquí: el canal consulta.
    /// </summary>
    public static ResultadoDeCanal DeError(HttpStatusCode estado, ProblemaConecta? problema, long duracionMs)
    {
        var codigo = (int)estado;
        var texto = problema?.Texto ?? $"Conecta respondió HTTP {codigo.ToString(CultureInfo.InvariantCulture)}.";
        var crudo = Recortar($"HTTP {codigo.ToString(CultureInfo.InvariantCulture)}", 20);
        var dian = Recortar(problema?.DianCode, 10);

        ResultadoDeCanal Con(ChannelOutcome outcome, params MensajeDelCanal[] mensajes) =>
            new(outcome, null, null, null, null, null, mensajes, [], null, crudo, dian, duracionMs);

        return codigo switch
        {
            401 => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaAuth, texto, "Conecta no aceptó la credencial del ERP: verifíquela en Facturación electrónica.")),
            403 => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaPermiso, texto,
                "La credencial del ERP no puede emitir para esta cooperativa en Conecta (tenant, rol o IP): revísela con el administrador de Conecta.")),
            404 => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaTenant, texto, "Conecta no conoce a la cooperativa configurada.")),
            409 when problema?.EsDelTipo("numbering-out-of-range") == true => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaFueraDeRango, texto,
                $"El número {problema.Prefix}{problema.Number} no cae en una numeración vigente de Conecta (rango o fechas): revise la resolución en " +
                "Conecta y en el ERP. Conecta no consumió nada; el documento sigue pendiente.")),
            400 when problema?.EsDelTipo("client-numbering") == true => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaNumeracionDelCliente, texto,
                $"El prefijo {problema.Prefix} no tiene encendida en Conecta la numeración del emisor (clientAssignsNumber): pídala al " +
                "administrador de Conecta. El documento sigue pendiente.")),
            409 when Contiene(problema, "idempotencia") => Con(ChannelOutcome.InvalidData, Rechazo(ReglaClave, texto,
                "Conecta recibió la misma clave para otro documento: es un defecto, avise a soporte.")),
            409 when Contiene(problema, "numeración") || problema?.Prefix is not null => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaNumeracion, texto,
                $"La numeración {(problema?.Prefix ?? string.Empty)} no está vigente o se agotó en Conecta.")),
            409 => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaCanal, texto, "El canal de emisión de la cooperativa en Conecta no está listo.")),
            400 or 413 => Con(ChannelOutcome.InvalidData, Rechazo(ReglaValidacion, texto, "Conecta no pudo leer el documento: avise a soporte.")),
            422 => Con(ChannelOutcome.InvalidData, Validacion(problema, texto)),
            429 => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaLimite, texto, "Conecta pidió esperar (límite de peticiones); se reintenta.")),
            >= 500 => Con(ChannelOutcome.InProcess, new MensajeDelCanal(ReglaServidor, TipoDeMensajeDelCanal.Notificacion, texto,
                "Conecta falló al responder: se consulta antes de reenviar.")),
            _ => Con(ChannelOutcome.ChannelUnavailable, Rechazo(ReglaServidor, texto, "Conecta respondió algo inesperado.")),
        };
    }

    /// <summary>Una excepción de transporte (§6, regla 2): si la petición no salió, <c>ChannelUnavailable</c>; si pudo llegar, <c>InProcess</c>.</summary>
    public static ResultadoDeCanal DeExcepcion(Exception ex, bool emision, long duracionMs)
    {
        var salio = PudoSalir(ex);
        var texto = $"CONECTA: {ex.GetType().Name}{(ex is HttpRequestException h && h.HttpRequestError != HttpRequestError.Unknown ? $" ({h.HttpRequestError})" : string.Empty)}.";
        if (!salio || !emision)
            return ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable, duracionMs,
                Rechazo(ReglaConexion, texto, "No hubo conexión con Conecta; se reintenta."));
        return ResultadoDeCanal.Sin(ChannelOutcome.InProcess, duracionMs,
            new MensajeDelCanal(ReglaConexion, TipoDeMensajeDelCanal.Notificacion, texto, "Conecta no respondió a tiempo; se consulta antes de reenviar."));
    }

    /// <summary>¿La petición pudo haber llegado? No llegó si falló el nombre, la conexión o el TLS.</summary>
    public static bool PudoSalir(Exception ex) => ex switch
    {
        HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError } => false,
        HttpRequestException { InnerException: System.Net.Sockets.SocketException } => false,
        _ => true,
    };

    /// <summary>El resultado de un documento decidido o no (§7), con los artefactos que ya se descargaron.</summary>
    public static ResultadoDeCanal DeDocumento(DocumentoConecta d, IReadOnlyList<ArtefactoDelCanal> artefactos, string? qr, DateTimeOffset? validadoEn,
        long duracionMs)
    {
        var crudo = Recortar(d.ProviderStatusCode is { Length: > 0 } p ? $"{d.Status}/{p}" : d.Status, 20);
        var dian = Recortar(d.DianStatusCode, 10);
        var referencia = d.Id.ToString("D");
        var tipoDeCodigo = d.UniqueCodeAlgorithm?.ToUpperInvariant() switch
        {
            "CUFE" => "CUFE",
            "CUDE" => "CUDE",
            "CUDS" => "CUDS",
            _ => null,
        };
        var tieneCodigo = !string.IsNullOrWhiteSpace(d.UniqueCode);

        ResultadoDeCanal EnProceso(string motivo) => new(ChannelOutcome.InProcess, null, null, null, null, null,
            [new MensajeDelCanal(ReglaNotificacion, TipoDeMensajeDelCanal.Notificacion, $"CONECTA: {d.Status}. {motivo}", motivo)],
            [], referencia, crudo, dian, duracionMs);

        switch (Leer(d.Status))
        {
            case LecturaDeEstadoConecta.Validado:
            {
                var completo = tieneCodigo
                    && artefactos.Any(a => a.Tipo == TipoDeArtefacto.ApplicationResponse)
                    && artefactos.Any(a => a.Tipo == TipoDeArtefacto.XmlFirmado)
                    && artefactos.Any(a => a.Tipo == TipoDeArtefacto.AttachedDocument);
                if (!completo) return EnProceso("La DIAN decidió y Conecta todavía prepara los archivos; se vuelve a consultar.");
                var conNotificaciones = string.Equals(d.Status, "AcceptedWithNotices", StringComparison.Ordinal);
                IReadOnlyList<MensajeDelCanal> mensajes = conNotificaciones
                    ? [new MensajeDelCanal(d.DianStatusCode ?? ReglaNotificacion, TipoDeMensajeDelCanal.Notificacion,
                        d.DianStatusMessage ?? "La DIAN validó el documento con notificaciones.", "Validado por la DIAN con notificaciones.")]
                    : [];
                return new ResultadoDeCanal(conNotificaciones ? ChannelOutcome.ValidatedWithNotices : ChannelOutcome.Validated,
                    d.UniqueCode, tipoDeCodigo ?? "CUFE", qr, validadoEn ?? d.UpdatedAt, d.SubtypeCode, mensajes, artefactos, referencia, crudo, dian,
                    duracionMs);
            }
            case LecturaDeEstadoConecta.Rechazado:
            {
                // Sin respuesta de la DIAN y con código del proveedor: lo rechazó el proveedor, nunca llegó a la DIAN.
                var porElProveedor = !string.IsNullOrWhiteSpace(d.ProviderStatusCode) && !artefactos.Any(a => a.Tipo == TipoDeArtefacto.ApplicationResponse);
                var texto = d.DianStatusMessage ?? "Rechazado sin mensaje.";
                return new ResultadoDeCanal(porElProveedor ? ChannelOutcome.InvalidData : ChannelOutcome.Rejected, null, null, null, null, d.SubtypeCode,
                    [Rechazo(d.DianStatusCode ?? d.ProviderStatusCode ?? ReglaRechazo, $"CONECTA: {texto}", texto)],
                    artefactos.Where(a => a.Tipo == TipoDeArtefacto.ApplicationResponse).ToList(), referencia, crudo, dian, duracionMs);
            }
            case LecturaDeEstadoConecta.Contingencia:
            {
                var firmado = artefactos.FirstOrDefault(a => a.Tipo == TipoDeArtefacto.XmlFirmado);
                if (!tieneCodigo || firmado is null)
                    return EnProceso("El canal no ha podido transmitir; no es la contingencia de la DIAN mientras no haya documento firmado.");
                return new ResultadoDeCanal(ChannelOutcome.DianUnavailable, d.UniqueCode, tipoDeCodigo ?? "CUFE", qr, null, d.SubtypeCode,
                    [new MensajeDelCanal(ReglaNotificacion, TipoDeMensajeDelCanal.Notificacion, "CONECTA: la DIAN no está disponible.",
                        "La DIAN no responde: el documento se expide en contingencia y se transmite cuando vuelva.")],
                    [firmado], referencia, crudo, dian, duracionMs);
            }
            default:
                return EnProceso("Sin decisión todavía.");
        }
    }

    /// <summary>Los mensajes de una validación local o de traducción al proveedor (422).</summary>
    private static MensajeDelCanal[] Validacion(ProblemaConecta? problema, string texto)
    {
        var mensajes = new List<MensajeDelCanal>();
        foreach (var regla in problema?.RuleIds ?? [])
            mensajes.Add(Rechazo(regla, texto, $"Conecta no aceptó el documento por la regla {regla}: {texto}"));
        foreach (var error in problema?.Errors ?? [])
            mensajes.Add(Rechazo(ReglaValidacion, error, error));
        if (problema?.RuleId is { Length: > 0 } una && mensajes.Count == 0)
            mensajes.Add(Rechazo(una, texto, texto));
        if (mensajes.Count == 0) mensajes.Add(Rechazo(ReglaValidacion, texto, texto));
        return [.. mensajes];
    }

    private static bool Contiene(ProblemaConecta? p, string palabra) =>
        (p?.Title?.Contains(palabra, StringComparison.OrdinalIgnoreCase) ?? false) || (p?.Detail?.Contains(palabra, StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>Recorta a lo que cabe en la bitácora.</summary>
    public static string? Recortar(string? texto, int largo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= largo ? texto : texto[..largo];

    public static MensajeDelCanal Rechazo(string regla, string texto, string traduccion) =>
        new(regla, TipoDeMensajeDelCanal.Rechazo, texto, traduccion);
}
