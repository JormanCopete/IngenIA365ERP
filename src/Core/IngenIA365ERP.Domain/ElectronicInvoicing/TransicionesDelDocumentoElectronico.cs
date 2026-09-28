using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.ElectronicInvoicing;

/// <summary>Lo que le pasa a un documento electrónico: una llamada al canal o una decisión de una persona. (nuevo)</summary>
public enum EventoDelDocumentoElectronico
{
    /// <summary><c>EmitirAsync</c> sobre un <c>Pending</c> (<c>TransmissionOperation.Emit</c>).</summary>
    Emitir = 1,

    /// <summary><c>ConsultarEstadoAsync</c> (<c>TransmissionOperation.QueryStatus</c>).</summary>
    ConsultarEstado = 2,

    /// <summary>Transmitir lo expedido en contingencia 03 o 04 (<c>TransmissionOperation.TransmitContingency</c>).</summary>
    TransmitirContingencia = 3,

    /// <summary>Caso a: <c>CorrectRejectedDocumentCommand</c>.</summary>
    CorregirCasoA = 4,

    /// <summary>Caso b: <c>ReplaceRejectedDocumentCommand</c>.</summary>
    ReemplazarCasoB = 5,

    /// <summary>Caso c: <c>CancelRejectedDocumentCommand</c>.</summary>
    CancelarCasoC = 6,
}

/// <summary>
/// Lo que respondió el canal, reducido a lo que la máquina necesita: el resultado uniforme y si trajo el código único
/// (CUFE, CUDE o CUDS) y la respuesta de validación (<c>ApplicationResponse</c>). (nuevo)
/// </summary>
public sealed record RespuestaDelCanal(ChannelOutcome Resultado, bool TieneCodigoUnico = false, bool TieneRespuestaDeValidacion = false);

/// <summary>
/// El veredicto de <see cref="TransicionesDelDocumentoElectronico.Aplicar"/>: procede hacia <see cref="Hacia"/> con sus
/// efectos, o no procede con un <see cref="Codigo"/> de error nombrado (el mismo que publica la aplicación). (nuevo)
/// </summary>
public sealed record ResultadoDeTransicion
{
    public bool Procede => Codigo is null;

    /// <summary>El estado al que pasa; si no procede, el mismo del que partía.</summary>
    public ElectronicDocumentStatus Hacia { get; init; }

    public string? Codigo { get; init; }

    public string? Motivo { get; init; }

    /// <summary>Quién rechazó, si el destino es <c>Rejected</c>.</summary>
    public RejectedBy? RechazadoPor { get; init; }

    /// <summary>La contingencia que esta transición fija (04 cuando el canal la declara). Nunca borra la que ya había.</summary>
    public ContingencyType? Contingencia { get; init; }

    /// <summary>Casos a y b: nace la versión n + 1 (mismo número, mismo canal).</summary>
    public bool NuevaVersion { get; init; }

    /// <summary><c>NotFound</c>: se reenvía la <b>misma</b> versión con el <b>mismo</b> número.</summary>
    public bool ReenviarMismaVersion { get; init; }

    /// <summary>La consulta confirmó el rechazo (habilita los casos b y c, §8).</summary>
    public bool ConfirmaElRechazo { get; init; }

    /// <summary>Cuenta como falla para <c>CircuitoDeCanal</c> (§7.2).</summary>
    public bool CuentaComoFallaDelCanal { get; init; }

    internal static ResultadoDeTransicion A(ElectronicDocumentStatus hacia) => new() { Hacia = hacia };

    internal static ResultadoDeTransicion Rechazo(ElectronicDocumentStatus desde, string codigo, string motivo) =>
        new() { Hacia = desde, Codigo = codigo, Motivo = motivo };
}

/// <summary>
/// La máquina de estados del documento electrónico (feature 012, I4, T695; contracts/dian.md §5.1; data-model §18),
/// pura. Todo cambio de <c>COR_ElectronicDocuments.Status</c> pasa por aquí (lo aplica
/// <c>ElectronicDocument.AplicarEvento</c>); lo que no está en la tabla se rechaza con su código. Además de las filas
/// de §5.1 admite las que el contrato pide en otras secciones: la consulta desde <c>Pending</c> (§6.3, el proceso que
/// cae entre la llamada y el registro consulta antes de reenviar; y la DIAN que valida tarde un pendiente lo deja
/// validado), la consulta que confirma un rechazo antes de los casos b y c (§8) y el reintento de la transmisión de
/// contingencia cuando el canal o la DIAN no responden (§6.3). Invariantes:
/// <list type="bullet">
/// <item>nunca <c>Validated</c> sin código único y respuesta de validación: sin ellos el resultado es ambiguo y queda
/// <c>Sent</c> (se consulta);</item>
/// <item>un <c>Sent</c> sólo se consulta: no se reenvía ni admite los casos a, b o c;</item>
/// <item>nada sale de un estado final, y del <c>Rejected</c> sólo por los casos a, b o c; b y c exigen el rechazo
/// confirmado;</item>
/// <item>la contingencia 04 sólo la declara el canal, con el documento firmado y su código único;</item>
/// <item>nunca se renumera: ninguna transición cambia el número (eso ni siquiera pasa por aquí).</item>
/// </list>
/// (nuevo)
/// </summary>
public static class TransicionesDelDocumentoElectronico
{
    public const string CodigoFinal = "ElectronicInvoicing.Document.Final";
    public const string CodigoEsperaRespuesta = "ElectronicInvoicing.Document.AwaitingResponse";
    public const string CodigoNoRechazado = "ElectronicInvoicing.Document.NotRejected";
    public const string CodigoRechazoSinConfirmar = "ElectronicInvoicing.Document.RejectionNotConfirmed";

    /// <summary>La contingencia 03 se transmite sólo con su evento cerrado (§5.1). (nuevo)</summary>
    public const string CodigoContingenciaAbierta = "ElectronicInvoicing.Contingency.StillOpen";

    /// <summary>Cualquier otra combinación que la tabla no admite. (nuevo)</summary>
    public const string CodigoTransicionNoPermitida = "ElectronicInvoicing.Document.TransitionNotAllowed";

    /// <summary>Estados de los que no se sale.</summary>
    public static bool EsFinal(ElectronicDocumentStatus estado) =>
        estado is ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices
            or ElectronicDocumentStatus.CancelledWithoutReplacement;

    /// <summary>
    /// El estado con que nace el documento al confirmarse el comercial: <c>Pending</c>, o <c>IssuerContingency</c> si hay
    /// una contingencia 03 abierta para el canal sellado y se numeró con la resolución de contingencia (§7.2).
    /// </summary>
    public static (ElectronicDocumentStatus Estado, ContingencyType? Contingencia) AlNumerar(bool contingencia03Abierta) =>
        contingencia03Abierta
            ? (ElectronicDocumentStatus.IssuerContingency, ContingencyType.Issuer03)
            : (ElectronicDocumentStatus.Pending, null);

    /// <summary>Decide la transición.</summary>
    /// <param name="desde">El estado actual.</param>
    /// <param name="evento">Lo que pasó.</param>
    /// <param name="respuesta">Lo que respondió el canal; obligatoria en los eventos del canal, ignorada en los casos.</param>
    /// <param name="eventoDeContingenciaCerrado">En <c>IssuerContingency</c>: el evento 03 ya se cerró.</param>
    /// <param name="rechazoConfirmado">Una consulta posterior al rechazo lo confirmó (casos b y c, §8).</param>
    public static ResultadoDeTransicion Aplicar(
        ElectronicDocumentStatus desde,
        EventoDelDocumentoElectronico evento,
        RespuestaDelCanal? respuesta = null,
        bool eventoDeContingenciaCerrado = false,
        bool rechazoConfirmado = false)
    {
        if (EsFinal(desde))
            return ResultadoDeTransicion.Rechazo(desde, CodigoFinal, $"El documento está {desde}: no cambia más.");

        return evento switch
        {
            EventoDelDocumentoElectronico.CorregirCasoA or EventoDelDocumentoElectronico.ReemplazarCasoB
                or EventoDelDocumentoElectronico.CancelarCasoC => Caso(desde, evento, rechazoConfirmado),
            _ => DelCanal(desde, evento, respuesta ?? throw new ArgumentNullException(nameof(respuesta),
                $"El evento {evento} es una llamada al canal y necesita su respuesta."), eventoDeContingenciaCerrado),
        };
    }

    private static ResultadoDeTransicion Caso(ElectronicDocumentStatus desde, EventoDelDocumentoElectronico caso, bool rechazoConfirmado)
    {
        if (desde == ElectronicDocumentStatus.Sent)
            return ResultadoDeTransicion.Rechazo(desde, CodigoEsperaRespuesta, "El documento se envió y no tiene respuesta: se consulta antes de corregirlo.");
        if (desde != ElectronicDocumentStatus.Rejected)
            return ResultadoDeTransicion.Rechazo(desde, CodigoNoRechazado, $"Los casos a, b y c sólo aplican a un documento rechazado (está {desde}).");

        return caso switch
        {
            EventoDelDocumentoElectronico.CorregirCasoA => ResultadoDeTransicion.A(ElectronicDocumentStatus.Pending) with { NuevaVersion = true },
            _ when !rechazoConfirmado => ResultadoDeTransicion.Rechazo(desde, CodigoRechazoSinConfirmar,
                "Reemplazar o cancelar exige el rechazo confirmado por la consulta de estado."),
            EventoDelDocumentoElectronico.ReemplazarCasoB => ResultadoDeTransicion.A(ElectronicDocumentStatus.Pending) with { NuevaVersion = true },
            _ => ResultadoDeTransicion.A(ElectronicDocumentStatus.CancelledWithoutReplacement),
        };
    }

    private static ResultadoDeTransicion DelCanal(
        ElectronicDocumentStatus desde, EventoDelDocumentoElectronico evento, RespuestaDelCanal respuesta, bool eventoCerrado)
    {
        var r = respuesta.Resultado;
        return (desde, evento) switch
        {
            (ElectronicDocumentStatus.Pending, EventoDelDocumentoElectronico.Emitir) => r switch
            {
                ChannelOutcome.NotFound => NoPermitida(desde, evento, r),
                ChannelOutcome.DianUnavailable when respuesta.TieneCodigoUnico =>
                    ResultadoDeTransicion.A(ElectronicDocumentStatus.DianContingency) with { Contingencia = ContingencyType.Dian04 },
                ChannelOutcome.DianUnavailable => ResultadoDeTransicion.A(ElectronicDocumentStatus.Sent),
                ChannelOutcome.ChannelUnavailable => FallaDelCanal(ElectronicDocumentStatus.Pending),
                _ => Definitivo(respuesta),
            },

            (ElectronicDocumentStatus.Pending, EventoDelDocumentoElectronico.ConsultarEstado) => r switch
            {
                ChannelOutcome.NotFound => ResultadoDeTransicion.A(ElectronicDocumentStatus.Pending) with { ReenviarMismaVersion = true },
                ChannelOutcome.ChannelUnavailable => FallaDelCanal(ElectronicDocumentStatus.Pending),
                ChannelOutcome.DianUnavailable => ResultadoDeTransicion.A(ElectronicDocumentStatus.Pending),
                _ => Definitivo(respuesta),
            },

            (ElectronicDocumentStatus.Sent, EventoDelDocumentoElectronico.ConsultarEstado) => r switch
            {
                ChannelOutcome.NotFound => ResultadoDeTransicion.A(ElectronicDocumentStatus.Pending) with { ReenviarMismaVersion = true },
                ChannelOutcome.ChannelUnavailable => FallaDelCanal(ElectronicDocumentStatus.Sent),
                ChannelOutcome.DianUnavailable => ResultadoDeTransicion.A(ElectronicDocumentStatus.Sent),
                _ => Definitivo(respuesta),
            },

            (ElectronicDocumentStatus.Sent, _) => ResultadoDeTransicion.Rechazo(desde, CodigoEsperaRespuesta,
                "El documento se envió y no tiene respuesta: sólo se consulta."),

            (ElectronicDocumentStatus.IssuerContingency, EventoDelDocumentoElectronico.TransmitirContingencia) when !eventoCerrado =>
                ResultadoDeTransicion.Rechazo(desde, CodigoContingenciaAbierta, "La contingencia 03 se transmite al cerrarse su evento."),

            (ElectronicDocumentStatus.IssuerContingency or ElectronicDocumentStatus.DianContingency,
                EventoDelDocumentoElectronico.TransmitirContingencia) => r switch
            {
                ChannelOutcome.NotFound => NoPermitida(desde, evento, r),
                ChannelOutcome.ChannelUnavailable => FallaDelCanal(desde),
                ChannelOutcome.DianUnavailable => ResultadoDeTransicion.A(desde),
                _ => Definitivo(respuesta),
            },

            (ElectronicDocumentStatus.Rejected, EventoDelDocumentoElectronico.ConsultarEstado) => r switch
            {
                ChannelOutcome.Rejected => ResultadoDeTransicion.A(ElectronicDocumentStatus.Rejected) with { RechazadoPor = RejectedBy.Dian, ConfirmaElRechazo = true },
                ChannelOutcome.InvalidData or ChannelOutcome.NotFound =>
                    ResultadoDeTransicion.A(ElectronicDocumentStatus.Rejected) with { RechazadoPor = RejectedBy.Channel, ConfirmaElRechazo = true },
                ChannelOutcome.ChannelUnavailable => FallaDelCanal(ElectronicDocumentStatus.Rejected),
                ChannelOutcome.InProcess or ChannelOutcome.DianUnavailable => ResultadoDeTransicion.A(ElectronicDocumentStatus.Rejected),
                _ => NoPermitida(desde, evento, r),
            },

            _ => NoPermitida(desde, evento, r),
        };
    }

    /// <summary>Validado, validado con notificaciones, rechazado o —si falta el código o la respuesta— enviado.</summary>
    private static ResultadoDeTransicion Definitivo(RespuestaDelCanal respuesta) => respuesta.Resultado switch
    {
        ChannelOutcome.Validated or ChannelOutcome.ValidatedWithNotices
            when !(respuesta.TieneCodigoUnico && respuesta.TieneRespuestaDeValidacion) => ResultadoDeTransicion.A(ElectronicDocumentStatus.Sent),
        ChannelOutcome.Validated => ResultadoDeTransicion.A(ElectronicDocumentStatus.Validated),
        ChannelOutcome.ValidatedWithNotices => ResultadoDeTransicion.A(ElectronicDocumentStatus.ValidatedWithNotices),
        ChannelOutcome.Rejected => ResultadoDeTransicion.A(ElectronicDocumentStatus.Rejected) with { RechazadoPor = RejectedBy.Dian },
        ChannelOutcome.InvalidData => ResultadoDeTransicion.A(ElectronicDocumentStatus.Rejected) with { RechazadoPor = RejectedBy.Channel },
        ChannelOutcome.InProcess => ResultadoDeTransicion.A(ElectronicDocumentStatus.Sent),
        _ => throw new ArgumentOutOfRangeException(nameof(respuesta), respuesta.Resultado, "No es un resultado definitivo."),
    };

    private static ResultadoDeTransicion FallaDelCanal(ElectronicDocumentStatus queda) =>
        ResultadoDeTransicion.A(queda) with { CuentaComoFallaDelCanal = true };

    private static ResultadoDeTransicion NoPermitida(ElectronicDocumentStatus desde, EventoDelDocumentoElectronico evento, ChannelOutcome resultado) =>
        ResultadoDeTransicion.Rechazo(desde, CodigoTransicionNoPermitida, $"{desde} no admite {evento} con resultado {resultado}.");
}
