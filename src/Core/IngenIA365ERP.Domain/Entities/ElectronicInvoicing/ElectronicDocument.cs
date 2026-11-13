using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.Entities.ElectronicInvoicing;

/// <summary>
/// Un documento electrónico ante la DIAN (<c>COR_ElectronicDocuments</c>; feature 012, I4, T693; data-model §18;
/// contracts/dian.md §5 y §6). <b>Uno por número fiscal</b>: la identidad es el número; el rechazado y su reemplazo
/// (caso b) comparten la fila y las versiones van aparte (<see cref="Versions"/>). La plataforma no lee tablas
/// <c>INV_</c>: el documento comercial es <see cref="SourceModule"/> + <see cref="SourceDocumentPublicId"/>.
///
/// <para>
/// El canal se <b>sella</b> al numerar (<see cref="EmissionSettingId"/>, <see cref="Mode"/>, <see cref="ChannelCode"/>,
/// <see cref="SoftwareId"/>): reintentos, casos a y b y la transmisión de contingencia salen por aquí (FR-064). El estado
/// cambia sólo por <see cref="AplicarEvento"/>, que delega en <see cref="TransicionesDelDocumentoElectronico"/>. Las
/// columnas de reintento y arrendamiento van con <see cref="NoAuditarAttribute"/> no por secretas sino por ruido: su
/// historia es <c>COR_ElectronicDocumentTransmissions</c>. Nunca se renumera.
/// </para>
/// </summary>
public class ElectronicDocument : AuditableEntity
{
    /// <summary><c>INV</c> (máx. 3).</summary>
    public string SourceModule { get; set; } = string.Empty;

    /// <summary>El documento comercial vigente; en el caso b pasa al reemplazo (el original queda en la versión 1).</summary>
    public Guid SourceDocumentPublicId { get; set; }

    /// <summary>Para la bandeja sin leer <c>INV_</c> (máx. 10).</summary>
    public string SourceDocumentTypeCode { get; set; } = string.Empty;

    public ElectronicDocumentKind Kind { get; set; }

    /// <summary>01, 03, 04, 05, 20, 91, 92, 94, 95, 96 (de <c>CatalogoDian</c>; máx. 2).</summary>
    public string DianDocumentTypeCode { get; set; } = string.Empty;

    /// <summary>Nulo en notas y eventos.</summary>
    public int? ResolutionId { get; set; }

    public DianNumberingResolution? Resolution { get; set; }

    /// <summary>Máx. 10.</summary>
    public string Prefix { get; set; } = string.Empty;

    public long Consecutive { get; set; }

    /// <summary>Prefijo + consecutivo (máx. 20).</summary>
    public string Number { get; set; } = string.Empty;

    public DianEnvironment Environment { get; set; }

    /// <summary>La configuración de emisión sellada al numerar.</summary>
    public int EmissionSettingId { get; set; }

    public ElectronicEmissionSetting? EmissionSetting { get; set; }

    /// <summary>Sellado.</summary>
    public EmissionMode Mode { get; set; }

    /// <summary>Sellado (máx. 40).</summary>
    public string ChannelCode { get; set; } = string.Empty;

    /// <summary>Sellado (máx. 36).</summary>
    public string? SoftwareId { get; set; }

    /// <summary>Instante de expedición (se transmite con −05:00).</summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>Fecha local.</summary>
    public DateOnly IssueDate { get; set; }

    /// <summary>Copia para la bandeja (máx. 20); cambia sólo con el caso a.</summary>
    public string? CounterpartyTaxId { get; set; }

    /// <summary>Copia para la bandeja (máx. 200); cambia sólo con el caso a.</summary>
    public string? CounterpartyName { get; set; }

    /// <summary>Monto (18,2).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>CUFE, CUDE o CUDS (máx. 96).</summary>
    public string? UniqueCode { get; set; }

    public UniqueCodeKind? UniqueCodeKind { get; set; }

    /// <summary>Tal como lo devuelve el canal (máx. 1000).</summary>
    public string? QrContent { get; set; }

    public ElectronicDocumentStatus Status { get; set; } = ElectronicDocumentStatus.Pending;

    /// <summary>Queda aunque después se valide: el historial muestra que fue de contingencia.</summary>
    public ContingencyType? ContingencyType { get; set; }

    public int? ContingencyEventId { get; set; }

    public DianContingencyEvent? ContingencyEvent { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? ValidatedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    /// <summary>Sellado de la configuración.</summary>
    public EmailDeliveryBy EmailDeliveryBy { get; set; } = EmailDeliveryBy.Erp;

    public DateTime? EmailSentAt { get; set; }

    /// <summary>La versión vigente (<c>COR_ElectronicDocumentVersions</c>).</summary>
    public int? CurrentVersionId { get; set; }

    /// <summary>Notas y notas de ajuste: el documento que corrigen.</summary>
    public int? CorrectsDocumentId { get; set; }

    public ElectronicDocument? CorrectsDocument { get; set; }

    /// <summary>
    /// El documento que debe quedar validado antes de transmitir éste: la nota sobre un original en contingencia y la
    /// factura de <c>invoice-instead</c> tras su nota de ajuste (FR-066; contracts/api.md §18.3.1). (nuevo)
    /// </summary>
    public int? WaitsForDocumentId { get; set; }

    public ElectronicDocument? WaitsForDocument { get; set; }

    public RejectedBy? RejectedBy { get; set; }

    /// <summary>Motivo del caso c (máx. 500).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Responsable del caso c (<c>SEC_Users</c>).</summary>
    public int? CancelledByUserId { get; set; }

    [NoAuditar]
    public int AttemptCount { get; set; }

    /// <summary>Espera 15 s, 1, 2, 5, 15, 30, 60 min y luego cada hora (valores técnicos en <c>ElectronicInvoicing:Retries</c>).</summary>
    [NoAuditar]
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>Arrendamiento por fila: el intento en línea del POS y el procesador no toman el mismo documento.</summary>
    [NoAuditar]
    public DateTime? LeaseUntil { get; set; }

    /// <summary>Máx. 100.</summary>
    [NoAuditar]
    public string? LeaseOwner { get; set; }

    /// <summary>
    /// Feature 023 (data-model §1; research R6): intentos <b>transitorios</b> seguidos (canal no disponible, en proceso sin respuesta, no
    /// encontrado) desde la última respuesta definitiva o el último reproceso. Es lo que se compara con <c>Dian.TopeDeIntentos</c>;
    /// <see cref="AttemptCount"/> sigue siendo la historia completa. (nuevo)
    /// </summary>
    [NoAuditar]
    public int TransientFailures { get; set; }

    /// <summary>Feature 023: agotó el tope de intentos transitorios. Con valor, <see cref="NextAttemptAt"/> es nulo y nadie lo intenta solo. (nuevo)</summary>
    [NoAuditar]
    public DateTime? DeadAt { get; set; }

    /// <summary>Feature 023: el tope vigente cuando murió (informativo: el tope puede cambiar después). (nuevo)</summary>
    [NoAuditar]
    public int? DeadAttemptLimit { get; set; }

    /// <summary>
    /// Feature 023 (contracts/mensajeria.md §3): el último aviso que el broker confirmó. El barrido no republica lo que ya está en la cola
    /// salvo que lleve más de <c>Mensajeria:RepublicarTrasSegundos</c> sin atenderse. No es fuente de verdad de nada. (nuevo)
    /// </summary>
    [NoAuditar]
    public DateTime? EnqueuedAt { get; set; }

    /// <summary>
    /// Feature 023 (research R7): orden durable del <b>seguimiento</b> —subir el canónico, guardar la representación gráfica y entregar al
    /// comprador— fuera de la petición y del intento ante el canal. Nula = nada pendiente. (nuevo)
    /// </summary>
    [NoAuditar]
    public DateTime? FollowUpDueAt { get; set; }

    /// <summary>Feature 023: intentos fallidos seguidos del seguimiento; se compara con el mismo <c>Dian.TopeDeIntentos</c>. (nuevo)</summary>
    [NoAuditar]
    public int FollowUpAttempts { get; set; }

    /// <summary>Feature 023: el seguimiento agotó su tope. (nuevo)</summary>
    [NoAuditar]
    public DateTime? FollowUpDeadAt { get; set; }

    /// <summary>
    /// Feature 023: último motivo del seguimiento (máx. 500). Las marcas de lo ya hecho son las de siempre: el canónico y la representación en
    /// la versión, <see cref="EmailSentAt"/> y <see cref="DeliveredAt"/>. (nuevo)
    /// </summary>
    [NoAuditar]
    public string? FollowUpLastError { get; set; }

    /// <summary>Contingencias: cierre del evento + <c>Dian.PlazoContingenciaHoras</c>, según el tipo (<see cref="PlazoDeContingencia"/>).</summary>
    public DateTime? TransmissionDeadline { get; set; }

    public ChannelOutcome? LastOutcome { get; set; }

    /// <summary>Mensajes traducidos del último intento, para la pantalla.</summary>
    public string? LastMessagesJson { get; set; }

    public ICollection<ElectronicDocumentVersion> Versions { get; set; } = [];

    public ICollection<ElectronicDocumentTransmission> Transmissions { get; set; } = [];

    /// <summary>¿El estado ya no cambia?</summary>
    public bool EsFinal => TransicionesDelDocumentoElectronico.EsFinal(Status);

    /// <summary>Feature 023: ¿agotó el tope y espera a una persona?</summary>
    public bool EstaMuerto => DeadAt is not null;

    /// <summary>Feature 023: ¿el seguimiento agotó su tope?</summary>
    public bool SeguimientoMuerto => FollowUpDeadAt is not null;

    /// <summary>Fija el estado con que nace al numerarse (<c>Pending</c>, o <c>IssuerContingency</c> con la 03 abierta).</summary>
    public void Iniciar(bool contingencia03Abierta)
    {
        var (estado, contingencia) = TransicionesDelDocumentoElectronico.AlNumerar(contingencia03Abierta);
        Status = estado;
        if (contingencia is not null) ContingencyType = contingencia;
    }

    /// <summary>
    /// Aplica un evento por la máquina de estados. Si no procede, el documento no cambia. Si procede, fija el estado,
    /// quién rechazó, la contingencia 04 (sin borrar nunca la que ya tenía), el último resultado y las fechas de envío y
    /// validación. No toca la versión: los casos a y b la crea la aplicación (<see cref="ResultadoDeTransicion.NuevaVersion"/>).
    /// </summary>
    public ResultadoDeTransicion AplicarEvento(
        EventoDelDocumentoElectronico evento,
        RespuestaDelCanal? respuesta,
        DateTime ahora,
        bool eventoDeContingenciaCerrado = false,
        bool rechazoConfirmado = false)
    {
        var r = TransicionesDelDocumentoElectronico.Aplicar(Status, evento, respuesta, eventoDeContingenciaCerrado, rechazoConfirmado);
        if (!r.Procede) return r;

        Status = r.Hacia;
        if (r.RechazadoPor is not null) RejectedBy = r.RechazadoPor;
        if (r.Contingencia is not null) ContingencyType ??= r.Contingencia;
        if (respuesta is not null) LastOutcome = respuesta.Resultado;

        if (r.Hacia == ElectronicDocumentStatus.Sent) SentAt ??= ahora;
        if (r.Hacia is ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices) ValidatedAt = ahora;
        if (r.Hacia == ElectronicDocumentStatus.Pending && r.NuevaVersion) RejectedBy = null;
        return r;
    }

    /// <summary>Caso c: queda <c>CancelledWithoutReplacement</c> con motivo y responsable.</summary>
    public ResultadoDeTransicion CancelarSinReemplazo(string motivo, int usuarioId, bool rechazoConfirmado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        var r = TransicionesDelDocumentoElectronico.Aplicar(Status, EventoDelDocumentoElectronico.CancelarCasoC, rechazoConfirmado: rechazoConfirmado);
        if (!r.Procede) return r;

        Status = r.Hacia;
        RejectionReason = motivo.Trim();
        CancelledByUserId = usuarioId;
        return r;
    }
}
