using System.Diagnostics;
using System.Globalization;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ElectronicInvoicing.Channels.Simulado;

/// <summary>
/// El canal simulado (feature 012, I4, T727; contracts/dian.md §3.4): obligatorio para CI, las e2e y el ensayo de la cooperativa mientras no
/// haya proveedor contratado ni software propio. <b>Sólo admite el ambiente de pruebas</b> (en producción responde <c>InvalidData</c>; la
/// configuración y la guardia ya lo impiden antes). Decide por el <b>último dígito</b> de la identificación de la contraparte:
/// <list type="table">
/// <item><term>1</term><description><c>Rejected</c> con una regla de ejemplo (la versión 2 en adelante, la corrección de los casos a o b, se valida);</description></item>
/// <item><term>2</term><description><c>ValidatedWithNotices</c>;</description></item>
/// <item><term>3</term><description><c>InProcess</c>, <c>NotFound</c> en la primera consulta, y el reenvío se valida;</description></item>
/// <item><term>4</term><description><c>DianUnavailable</c> con el documento firmado, su código único y el tipo DIAN de contingencia; al transmitirlo se valida con el mismo código;</description></item>
/// <item><term>5</term><description><c>ChannelUnavailable</c> (la petición no sale) y, durante <c>SimulatedOutageSeconds</c>, <c>ProbarAsync</c> también falla;</description></item>
/// <item><term>otro</term><description><c>Validated</c>.</description></item>
/// </list>
/// Produce CUFE/CUDE/CUDS de 96 caracteres, el contenido del QR y los tres artefactos (XML firmado, <c>ApplicationResponse</c>,
/// <c>AttachedDocument</c>), todos marcados «SIMULADO». Recuerda por cooperativa, ambiente y número lo recibido
/// (<see cref="MemoriaDelCanalSimulado"/>): un «ya existe» se traduce en consulta (regla 3). El retardo de
/// <c>ElectronicInvoicing:Channels:SIMULADO:DelayMilliseconds</c> ensaya la espera del POS. Cumple la batería
/// <c>ConformidadDelCanal</c>. (nuevo)
/// </summary>
public sealed class CanalSimulado(IOptions<ElectronicInvoicingOptions> opciones, MemoriaDelCanalSimulado memoria, TimeProvider? reloj = null)
    : ICanalDeEmisionElectronica
{
    /// <summary>El código del canal; es el mismo que la guardia reconoce como simulado.</summary>
    public const string Codigo = "SIMULADO";

    /// <summary>La regla de ejemplo del rechazo (dígito 1).</summary>
    public const string ReglaDeRechazo = "SIM-FAK24";

    /// <summary>La notificación de ejemplo (dígito 2).</summary>
    public const string ReglaDeNotificacion = "SIM-FAJ44b";

    /// <summary>El rechazo de un ambiente que no es el de pruebas.</summary>
    public const string ReglaDeAmbiente = "SIM-AMBIENTE";

    /// <summary>El rechazo de un tipo que el canal no emite.</summary>
    public const string ReglaDeTipo = "SIM-TIPO";

    private const string Marca = "SIMULADO";
    private static readonly Regex VersionDeLaClave = new(@":v(?<n>\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public string ChannelCode => Codigo;

    public CapacidadesDelCanal Capacidades { get; } = new(
        new HashSet<ElectronicDocumentKind>
        {
            ElectronicDocumentKind.Invoice, ElectronicDocumentKind.CreditNote, ElectronicDocumentKind.DebitNote, ElectronicDocumentKind.PosEquivalent,
            ElectronicDocumentKind.PosAdjustmentNote, ElectronicDocumentKind.SupportDocument, ElectronicDocumentKind.SupportDocumentAdjustmentNote,
        },
        AceptaNumeroDelErp: true,
        ContingenciaDelFacturador: true,
        InformaContingenciaDian: true,
        EsAsincrono: true,
        DevuelvePdf: false,
        PuedeEnviarCorreo: false,
        // No conoce las resoluciones de la cooperativa: no puede proponer la clave técnica de ninguna (se digita, §9).
        ConsultaRangos: false,
        ConsultaAdquirente: true,
        AdmiteClaveDeIdempotencia: true);

    private OpcionesDeCanal Opciones => opciones.Value.Canal(Codigo);

    // ---------------------------------------------------------------------------------------------------- emisión --

    public async Task<ResultadoDeCanal> EmitirAsync(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        await EsperarAsync(ct);

        if (documento.Environment != DianEnvironment.Testing || contexto.Environment != DianEnvironment.Testing)
            return Invalido(ReglaDeAmbiente, "SIMULADO: el canal simulado sólo admite el ambiente de pruebas.",
                "El canal simulado no emite en producción: configure el canal real.", cronometro);
        if (!Capacidades.Emite(documento.Kind))
            return Invalido(ReglaDeTipo, $"SIMULADO: el canal no emite documentos {documento.Kind}.",
                $"El canal simulado no emite {documento.Kind}.", cronometro);

        var digito = UltimoDigito(documento.Counterparty.TaxId);
        if (digito == 5)
        {
            memoria.AnotarCaida(contexto.TenantPublicId, _reloj.GetUtcNow());
            return ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable, cronometro.ElapsedMilliseconds,
                new MensajeDelCanal("SIM-CONEXION", TipoDeMensajeDelCanal.Rechazo, "SIMULADO: no se pudo conectar con el canal.",
                    "No hubo conexión con el canal; se reintenta."));
        }

        var version = Version(contexto.IdempotencyKey);
        var registro = memoria.Obtener(contexto.TenantPublicId, documento.Environment, documento.Number.Full);
        lock (registro.Candado)
        {
            registro.Recepciones++;
            switch (registro.Estado)
            {
                // «Ya existe»: se responde lo que diría la consulta (regla 3).
                case EstadoSimulado.Validado or EstadoSimulado.ValidadoConNotificaciones:
                    return Resultado(registro, cronometro);
                case EstadoSimulado.Rechazado when version <= registro.VersionRechazada:
                    return Resultado(registro, cronometro);
                // Dígito 4 transmitido al volver la DIAN: se valida con el código que ya tenía.
                case EstadoSimulado.FirmadoEnContingencia:
                    return Validar(registro, documento, contexto, version, notificaciones: false, cronometro);
            }

            if (digito == 3 && !registro.YaQuedoAmbiguo)
            {
                registro.YaQuedoAmbiguo = true;
                registro.Estado = EstadoSimulado.Ambiguo;
                return ResultadoDeCanal.Sin(ChannelOutcome.InProcess, cronometro.ElapsedMilliseconds,
                    new MensajeDelCanal("SIM-TIEMPO", TipoDeMensajeDelCanal.Notificacion, "SIMULADO: se agotó el tiempo esperando la respuesta.",
                        "El canal no respondió a tiempo; se consulta antes de reenviar."));
            }

            if (digito == 4)
            {
                var codigo = CodigoUnico(documento, contexto, version);
                registro.Estado = EstadoSimulado.FirmadoEnContingencia;
                registro.CodigoUnico = codigo;
                registro.TipoDeCodigo = TipoDeCodigo(documento);
                registro.TipoDian = TipoDian(documento, ContingencyType.Dian04);
                registro.ReferenciaExterna = Referencia();
                registro.Qr = Qr(documento, codigo);
                registro.Artefactos = [Artefacto(TipoDeArtefacto.XmlFirmado, documento, codigo, "Invoice")];
                registro.Mensajes = [new MensajeDelCanal("SIM-DIAN-04", TipoDeMensajeDelCanal.Notificacion,
                    "SIMULADO: la DIAN no está disponible; el documento quedó firmado y se transmite al restablecerse.",
                    "La DIAN no está disponible: el documento se entrega con la leyenda de contingencia.")];
                return Resultado(registro, cronometro);
            }

            if (digito == 1 && version <= 1)
            {
                registro.Estado = EstadoSimulado.Rechazado;
                registro.VersionRechazada = version;
                registro.CodigoUnico = null;
                registro.ReferenciaExterna = Referencia();
                registro.Artefactos = [Artefacto(TipoDeArtefacto.ApplicationResponse, documento, null, "ApplicationResponse")];
                registro.Mensajes = [new MensajeDelCanal(ReglaDeRechazo, TipoDeMensajeDelCanal.Rechazo,
                    "SIMULADO: el nombre o razón social del adquirente no coincide con el registrado (regla de ejemplo).",
                    "La DIAN rechazó el documento: el nombre del comprador no coincide con el registrado. Corrija los datos del comprador.")];
                return Resultado(registro, cronometro);
            }

            return Validar(registro, documento, contexto, version, notificaciones: digito == 2, cronometro);
        }
    }

    // ---------------------------------------------------------------------------------------------------- consulta --

    public async Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        await EsperarAsync(ct);

        var registro = memoria.Buscar(contexto.TenantPublicId, referencia.Environment, referencia.Numero);
        if (registro is null) return NoEncontrado(cronometro);
        lock (registro.Candado)
        {
            switch (registro.Estado)
            {
                case EstadoSimulado.NoRecibido:
                    return NoEncontrado(cronometro);
                case EstadoSimulado.Ambiguo:
                    // Dígito 3: la primera consulta no lo encuentra; el reenvío de la misma versión lo valida.
                    registro.Estado = EstadoSimulado.NoRecibido;
                    return NoEncontrado(cronometro);
                default:
                    return Resultado(registro, cronometro);
            }
        }
    }

    public async Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        await EsperarAsync(ct);

        var registro = memoria.Buscar(contexto.TenantPublicId, referencia.Environment, referencia.Numero);
        if (registro is null) return NoEncontrado(cronometro);
        lock (registro.Candado)
        {
            var artefacto = registro.Artefactos.FirstOrDefault(a => a.Tipo == tipo);
            if (artefacto is null) return NoEncontrado(cronometro);
            var completo = Resultado(registro, cronometro);
            return completo with { Artefactos = [artefacto] };
        }
    }

    // ---------------------------------------------------------------------------------------------------- otras --

    public async Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        await EsperarAsync(ct);
        if (contexto.Environment != DianEnvironment.Testing)
            return Invalido(ReglaDeAmbiente, "SIMULADO: el canal simulado sólo admite el ambiente de pruebas.",
                "El canal simulado no emite en producción.", cronometro);

        var codigo = Hex(SHA384.HashData(Encoding.UTF8.GetBytes(
            $"{Marca}|{contexto.TenantPublicId:N}|{evento.Environment}|{evento.EventCode}|{evento.Number.Full}|{evento.ReferencedInvoice.Number}")));
        var respuesta = new ArtefactoDelCanal(TipoDeArtefacto.ApplicationResponse, $"{evento.Number.Full}-evento-{Marca}.xml", "application/xml",
            Xml("ApplicationResponse", evento.Number.Full, codigo, "Evento RADIAN " + evento.EventCode));
        return new ResultadoDeCanal(ChannelOutcome.Validated, codigo, "CUDE", null, _reloj.GetUtcNow(), evento.DianDocumentTypeCode, [],
            [respuesta], Referencia(), "SIM-00", "00", cronometro.ElapsedMilliseconds);
    }

    public async Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        await EsperarAsync(ct);

        var caida = memoria.UltimaCaida(contexto.TenantPublicId);
        if (caida is { } desde && _reloj.GetUtcNow() - desde < TimeSpan.FromSeconds(Math.Max(0, Opciones.SimulatedOutageSeconds)))
            return ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable, cronometro.ElapsedMilliseconds,
                new MensajeDelCanal("SIM-CONEXION", TipoDeMensajeDelCanal.Rechazo, "SIMULADO: el canal sigue sin responder.",
                    "El canal sigue sin responder."));

        return ResultadoDeCanal.Sin(ChannelOutcome.Validated, cronometro.ElapsedMilliseconds,
            new MensajeDelCanal("SIM-PRUEBA", TipoDeMensajeDelCanal.Notificacion, "SIMULADO: el canal simulado responde; no pide credenciales.",
                "El canal simulado responde."));
    }

    public async Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        await EsperarAsync(ct);
        return ResultadoDeCanal.Sin(ChannelOutcome.InvalidData, cronometro.ElapsedMilliseconds,
            new MensajeDelCanal("SIM-RANGOS", TipoDeMensajeDelCanal.Rechazo, "SIMULADO: el canal simulado no consulta rangos de numeración.",
                "El canal simulado no trae la clave técnica: digítela."));
    }

    public async Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto,
        CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        await EsperarAsync(ct);
        return ResultadoDeCanal.Sin(ChannelOutcome.Validated, cronometro.ElapsedMilliseconds,
            new MensajeDelCanal("Acquirer.Name", TipoDeMensajeDelCanal.Notificacion,
                $"{tipoDeIdentificacionDian}|{numero}|ADQUIRENTE {Marca} {numero}", "Datos del adquirente del canal simulado."));
    }

    // ---------------------------------------------------------------------------------------------------- piezas --

    private ResultadoDeCanal Validar(RegistroSimulado registro, DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, int version,
        bool notificaciones, Stopwatch cronometro)
    {
        var codigo = registro.CodigoUnico ?? CodigoUnico(documento, contexto, version);
        registro.Estado = notificaciones ? EstadoSimulado.ValidadoConNotificaciones : EstadoSimulado.Validado;
        registro.CodigoUnico = codigo;
        registro.TipoDeCodigo = TipoDeCodigo(documento);
        registro.TipoDian = registro.TipoDian ?? TipoDian(documento, null);
        registro.ReferenciaExterna ??= Referencia();
        registro.ValidadoEn = _reloj.GetUtcNow();
        registro.Qr = Qr(documento, codigo);
        registro.Artefactos =
        [
            Artefacto(TipoDeArtefacto.XmlFirmado, documento, codigo, "Invoice"),
            Artefacto(TipoDeArtefacto.ApplicationResponse, documento, codigo, "ApplicationResponse"),
            Artefacto(TipoDeArtefacto.AttachedDocument, documento, codigo, "AttachedDocument"),
        ];
        registro.Mensajes = notificaciones
            ? [new MensajeDelCanal(ReglaDeNotificacion, TipoDeMensajeDelCanal.Notificacion,
                "SIMULADO: el correo del adquirente no está registrado en el RUT (notificación de ejemplo).",
                "La DIAN validó el documento con una observación: revise el correo del comprador.")]
            : [];
        return Resultado(registro, cronometro);
    }

    private static ResultadoDeCanal Resultado(RegistroSimulado r, Stopwatch cronometro) => new(
        r.Estado switch
        {
            EstadoSimulado.Validado => ChannelOutcome.Validated,
            EstadoSimulado.ValidadoConNotificaciones => ChannelOutcome.ValidatedWithNotices,
            EstadoSimulado.Rechazado => ChannelOutcome.Rejected,
            EstadoSimulado.FirmadoEnContingencia => ChannelOutcome.DianUnavailable,
            _ => ChannelOutcome.NotFound,
        },
        r.CodigoUnico,
        r.CodigoUnico is null ? null : r.TipoDeCodigo,
        r.Qr,
        r.Estado is EstadoSimulado.Validado or EstadoSimulado.ValidadoConNotificaciones ? r.ValidadoEn : null,
        r.TipoDian,
        r.Mensajes,
        r.Artefactos,
        r.ReferenciaExterna,
        r.Estado switch
        {
            EstadoSimulado.Rechazado => "SIM-99",
            EstadoSimulado.FirmadoEnContingencia => "SIM-208",
            _ => "SIM-00",
        },
        r.Estado switch
        {
            EstadoSimulado.Rechazado => "99",
            EstadoSimulado.FirmadoEnContingencia => null,
            _ => "00",
        },
        cronometro.ElapsedMilliseconds);

    private static ResultadoDeCanal NoEncontrado(Stopwatch cronometro) =>
        ResultadoDeCanal.Sin(ChannelOutcome.NotFound, cronometro.ElapsedMilliseconds,
            new MensajeDelCanal("SIM-NOENCONTRADO", TipoDeMensajeDelCanal.Notificacion, "SIMULADO: el número no se encuentra.",
                "El canal no tiene ese documento: se reenvía la misma versión."));

    private static ResultadoDeCanal Invalido(string regla, string texto, string traduccion, Stopwatch cronometro) =>
        ResultadoDeCanal.Sin(ChannelOutcome.InvalidData, cronometro.ElapsedMilliseconds,
            new MensajeDelCanal(regla, TipoDeMensajeDelCanal.Rechazo, texto, traduccion));

    private async Task EsperarAsync(CancellationToken ct)
    {
        var retardo = Opciones.DelayMilliseconds;
        if (retardo > 0) await Task.Delay(TimeSpan.FromMilliseconds(retardo), _reloj, ct);
        else ct.ThrowIfCancellationRequested();
    }

    /// <summary>El último dígito de la identificación; -1 si no tiene dígitos.</summary>
    internal static int UltimoDigito(string? identificacion)
    {
        if (string.IsNullOrEmpty(identificacion)) return -1;
        for (var i = identificacion.Length - 1; i >= 0; i--)
            if (char.IsAsciiDigit(identificacion[i])) return identificacion[i] - '0';
        return -1;
    }

    private static int Version(string? claveDeIdempotencia)
    {
        if (claveDeIdempotencia is null) return 1;
        var m = VersionDeLaClave.Match(claveDeIdempotencia);
        return m.Success && int.TryParse(m.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 1;
    }

    private static string CodigoUnico(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, int version)
    {
        var huella = SerializadorCanonico.Sha256(SerializadorCanonico.Serializar(documento));
        return Hex(SHA384.HashData(Encoding.UTF8.GetBytes(
            $"{Marca}|{contexto.TenantPublicId:N}|{documento.Environment}|{documento.Number.Full}|v{version}|{huella}")));
    }

    private static string TipoDeCodigo(DocumentoElectronicoCanonico documento)
    {
        var fecha = DateOnly.FromDateTime(documento.IssuedAt.DateTime);
        var tipo = CatalogoDian.Embebido.TipoDeDocumento(documento.Kind, null, fecha);
        if (tipo is not null) return tipo.CodigoUnico.ToString().ToUpperInvariant();
        return documento.Kind switch
        {
            ElectronicDocumentKind.Invoice => "CUFE",
            ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote => "CUDS",
            _ => "CUDE",
        };
    }

    private static string TipoDian(DocumentoElectronicoCanonico documento, ContingencyType? contingencia)
    {
        var fecha = DateOnly.FromDateTime(documento.IssuedAt.DateTime);
        var tipo = CatalogoDian.Embebido.TipoDeDocumento(documento.Kind, contingencia, fecha);
        return contingencia is null || tipo is null ? (string.IsNullOrWhiteSpace(documento.DianDocumentTypeCode) ? tipo?.Codigo ?? string.Empty : documento.DianDocumentTypeCode)
            : tipo.Codigo;
    }

    private static string Qr(DocumentoElectronicoCanonico documento, string codigo) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{Marca}|NumFac={documento.Number.Full}|FecFac={documento.IssuedAt:yyyy-MM-dd}|NitFac={documento.Issuer.TaxId}|DocAdq={documento.Counterparty.TaxId}|ValTolFac={documento.Totals.Payable:0.00}|CodigoUnico={codigo}");

    private static string Referencia() => $"SIM-{Guid.NewGuid():N}";

    private static ArtefactoDelCanal Artefacto(TipoDeArtefacto tipo, DocumentoElectronicoCanonico documento, string? codigo, string raiz)
    {
        var tramo = tipo switch
        {
            TipoDeArtefacto.XmlFirmado => "xml-firmado",
            TipoDeArtefacto.ApplicationResponse => "application-response",
            _ => "attached-document",
        };
        return new ArtefactoDelCanal(tipo, $"{documento.Number.Full}-{tramo}-{Marca}.xml", "application/xml",
            Xml(raiz, documento.Number.Full, codigo, documento.Kind.ToString()));
    }

    private static byte[] Xml(string raiz, string numero, string? codigo, string descripcion) => Encoding.UTF8.GetBytes(
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!-- {Marca}: documento de ejemplo del canal simulado; no tiene validez fiscal. -->
        <{raiz} simulado="true">
          <Marca>{Marca}</Marca>
          <Numero>{SecurityElement.Escape(numero)}</Numero>
          <Tipo>{SecurityElement.Escape(descripcion)}</Tipo>
          <CodigoUnico>{SecurityElement.Escape(codigo ?? string.Empty)}</CodigoUnico>
        </{raiz}>
        """);

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
