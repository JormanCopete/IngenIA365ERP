using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollDeductionFiles</c> (feature 013, K2; data-model §24.6 y diagrama §32.4; FR-113; T78): el archivo de descuentos de una
/// pagaduría para un período. Su <b>contenido es inmutable</b> (convenio, versión, período, versión del archivo, formato sellado, conteos,
/// totales, huella y adjunto: sólo <c>init</c>); lo único que avanza es el estado y las columnas de cada transición, que se escriben una vez:
/// <code>
/// Generated ──regenerar──▶ Superseded (+ la versión siguiente Generated)
/// Generated ──anular con motivo──▶ Voided
/// Generated ──EnvioDeDescuentosConfirmado──▶ Sent
/// </code>
/// Un <c>Sent</c> no se regenera (<c>Lending.PayrollFile.AlreadySent</c>: la corrección viaja como novedad del período siguiente, I9). No es
/// <see cref="IHechoInmutable"/> porque el estado cambia; la inmutabilidad del contenido la dan los <c>init</c> y las transiciones de aquí. (nuevo)
/// </summary>
public class PayrollDeductionFile : AuditableEntity
{
    public int PayrollAgreementId { get; init; }

    /// <summary>La versión del convenio vigente al período.</summary>
    public int PayrollAgreementVersionId { get; init; }

    public short PeriodYear { get; init; }

    public byte PeriodMonth { get; init; }

    /// <summary>Quincena o semana del mes; 1 en mensual.</summary>
    public byte SubPeriodNumber { get; init; } = 1;

    /// <summary>Regenerar antes del envío crea la siguiente.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Numerador de Cartera; único.</summary>
    public string Number { get; init; } = string.Empty;

    public PayrollFileStatus Status { get; private set; } = PayrollFileStatus.Generated;

    /// <summary>El formato de ida con que se escribió, sellado.</summary>
    public int OutboundFormatId { get; init; }

    /// <summary>Último <c>Sent</c> del convenio contra el que <c>NovedadesDeLibranza</c> calculó las novedades; nulo en el primero.</summary>
    public int? BaseFileId { get; init; }

    public int LineCount { get; init; }

    public decimal TotalAmount { get; init; }

    public int NewCount { get; init; }

    public int ChangeCount { get; init; }

    public int CancellationCount { get; init; }

    public int UnchangedCount { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string FileSha256 { get; init; } = string.Empty;

    /// <summary>Adjunto <c>LendingPayrollDeductionFile</c>, no borrable, subido antes de guardar la fila (sin FK).</summary>
    public Guid FileAttachmentPublicId { get; init; }

    public int GeneratedByUserId { get; init; }

    public DateTime GeneratedAt { get; init; }

    /// <summary>El <c>DescuentosPorPagaduria</c> emitido a Recaudo en la misma transacción.</summary>
    public Guid DeliveryMessagePublicId { get; init; }

    [EscrituraUnica]
    public int? SupersededByFileId { get; private set; }

    [EscrituraUnica]
    public DateTime? SupersededAt { get; private set; }

    [EscrituraUnica]
    public DateTime? SentAt { get; private set; }

    [EscrituraUnica]
    public Guid? SentConfirmationMessagePublicId { get; private set; }

    /// <summary>El radicado que informó Recaudo al confirmar el envío.</summary>
    [EscrituraUnica]
    public string? PaymasterReference { get; private set; }

    [EscrituraUnica]
    public DateTime? VoidedAt { get; private set; }

    [EscrituraUnica]
    public int? VoidedByUserId { get; private set; }

    [EscrituraUnica]
    public string? VoidReason { get; private set; }

    /// <summary>El primer <c>ResultadoDeDescuentos</c> recibido.</summary>
    [EscrituraUnica]
    public DateTime? FirstResultAt { get; private set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0824) --

    /// <summary>
    /// Regenerar antes del envío (§32.4): éste queda <c>Superseded</c> y apunta a la versión siguiente. Su archivo y sus líneas se quedan.
    /// </summary>
    public void Reemplazar(int nuevoArchivoId, DateTime instante)
    {
        if (Status == PayrollFileStatus.Sent)
            throw new InvalidOperationException($"El archivo {Number} ya se envió: no se regenera (la corrección va en el período siguiente).");
        ExigirGenerado("regenerar");
        Status = PayrollFileStatus.Superseded;
        SupersededByFileId = nuevoArchivoId;
        SupersededAt = instante;
    }

    /// <summary>Anular, sólo desde <c>Generated</c> y con motivo (§32.4).</summary>
    public void Anular(int usuarioId, DateTime instante, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ExigirGenerado("anular");
        Status = PayrollFileStatus.Voided;
        VoidedAt = instante;
        VoidedByUserId = usuarioId;
        VoidReason = motivo.Trim();
    }

    /// <summary>
    /// Recaudo confirmó el envío a la pagaduría (<c>EnvioDeDescuentosConfirmado</c>): <c>Generated</c> → <c>Sent</c>. Devuelve falso si ya
    /// estaba <c>Sent</c> (el consumo se procesa sin efecto); un reemplazado o anulado lo rechaza quien consume (<c>Lending.PayrollFile.Superseded</c>).
    /// </summary>
    public bool MarcarEnviado(DateTime instante, Guid mensaje, string? radicado)
    {
        if (Status == PayrollFileStatus.Sent) return false;
        ExigirGenerado("marcar enviado");
        Status = PayrollFileStatus.Sent;
        SentAt = instante;
        SentConfirmationMessagePublicId = mensaje;
        PaymasterReference = string.IsNullOrWhiteSpace(radicado) ? null : radicado.Trim();
        return true;
    }

    /// <summary>Anota el primer resultado; los siguientes no lo cambian.</summary>
    public void AnotarPrimerResultado(DateTime instante) => FirstResultAt ??= instante;

    private void ExigirGenerado(string accion)
    {
        if (Status != PayrollFileStatus.Generated)
            throw new InvalidOperationException($"El archivo {Number} está {Status}: sólo uno generado se puede {accion}.");
    }
}
