using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_WriteOffBatches</c> (feature 013, K3, T0872; data-model §25.6; FR-095, FR-096): el lote de castigo que arma el jefe de cartera
/// (<c>ArmarLoteDeCastigo</c>), aprueba el consejo con acta (un punto de sesión) y aplica otra persona (<c>AplicarCastigo</c>):
/// <code>
/// Draft ──enviar al consejo──▶ Submitted ──punto del acta──▶ Approved ──aplicar──▶ Applied ──reporte al supervisor (una vez)
///                                         └──────────────▶ Rejected
/// Draft · Submitted ──anular (el motivo va a la auditoría)──▶ Cancelled
/// </code>
/// Los ítems se agregan y quitan sólo en <c>Draft</c>; enviarlo lo congela. Los totales salen de los ítems incluidos. Aplicarlo sella si el
/// tipo de entidad debe reportar los castigos al supervisor y hasta cuándo; el reporte se registra una sola vez. No es hecho. Su
/// comportamiento de aplicación lo completa US14. (nuevo)
/// </summary>
public class WriteOffBatch : AuditableEntity
{
    public const string CodigoNoBorrador = "Lending.WriteOffBatch.NotDraft";
    public const string CodigoAprobacionRequerida = "Lending.WriteOff.ApprovalRequired";
    public const string CodigoEstadoInvalido = "Lending.WriteOffBatch.InvalidStatus";

    /// <summary>Numerador de Cartera; único.</summary>
    public string Number { get; init; } = string.Empty;

    public WriteOffBatchStatus Status { get; private set; } = WriteOffBatchStatus.Draft;

    /// <summary>Fecha a la que se verifican los requisitos.</summary>
    public DateOnly CutoffDate { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>Jefe de cartera (<c>Lending.WriteOffs.Prepare</c>).</summary>
    public int PreparedByUserId { get; init; }

    public DateTime PreparedAt { get; init; }

    /// <summary>La aprobación (o el rechazo) del consejo con acta.</summary>
    public int? SessionItemId { get; private set; }

    /// <summary><c>Lending.WriteOffs.Apply</c>.</summary>
    public int? AppliedByUserId { get; private set; }

    public DateTime? AppliedAt { get; private set; }

    public int LoanCount { get; private set; }

    public decimal TotalPrincipal { get; private set; }

    public decimal TotalInterest { get; private set; }

    public decimal TotalOther { get; private set; }

    /// <summary>Del perfil regulatorio vigente, sellado al aplicar.</summary>
    public bool SupervisorReportRequired { get; private set; }

    /// <summary>Alerta <c>Cartera.PlazoRegulatorioPorVencer</c>.</summary>
    public DateOnly? SupervisorReportDueDate { get; private set; }

    public DateTime? SupervisorReportedAt { get; private set; }

    public string? SupervisorReportReference { get; private set; }

    public Guid? SupervisorReportAttachmentPublicId { get; private set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0872) --

    private static readonly IReadOnlyDictionary<WriteOffBatchStatus, WriteOffBatchStatus[]> Transiciones =
        new Dictionary<WriteOffBatchStatus, WriteOffBatchStatus[]>
        {
            [WriteOffBatchStatus.Draft] = [WriteOffBatchStatus.Submitted, WriteOffBatchStatus.Cancelled],
            [WriteOffBatchStatus.Submitted] = [WriteOffBatchStatus.Approved, WriteOffBatchStatus.Rejected, WriteOffBatchStatus.Cancelled],
            [WriteOffBatchStatus.Approved] = [WriteOffBatchStatus.Applied],
        };

    public static bool PuedePasar(WriteOffBatchStatus desde, WriteOffBatchStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>Nulo en borrador; si no, <see cref="CodigoNoBorrador"/>.</summary>
    public ProblemaDeCartera? ExigirBorrador() => Status == WriteOffBatchStatus.Draft
        ? null
        : ProblemaDeCartera.Con(CodigoNoBorrador, "El lote ya no admite cambios: sólo se arma en borrador.", "status", Status.ToString());

    public ProblemaDeCartera? PuedeIrA(WriteOffBatchStatus hacia)
    {
        if (PuedePasar(Status, hacia)) return null;
        if (hacia == WriteOffBatchStatus.Applied)
            return ProblemaDeCartera.Con(CodigoAprobacionRequerida, "El lote se aplica con la aprobación del consejo con acta.", "status", Status.ToString());
        return new ProblemaDeCartera(CodigoEstadoInvalido, $"El lote está en «{Status}» y no puede pasar a «{hacia}».",
            new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() });
    }

    /// <summary>Recalcula el conteo y los totales con los ítems incluidos (los rechazados no cuentan); sólo en borrador.</summary>
    public void Recalcular(IEnumerable<WriteOffBatchItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        ReglaDeCarteraException.SiHay(ExigirBorrador());
        var incluidos = items.Where(i => i.Status == WriteOffItemStatus.Included).ToList();
        LoanCount = incluidos.Count;
        TotalPrincipal = incluidos.Sum(i => i.PrincipalAmount);
        TotalInterest = incluidos.Sum(i => i.InterestAmount);
        TotalOther = incluidos.Sum(i => i.OtherAmount);
    }

    /// <summary><c>Draft</c> → <c>Submitted</c>: se congela para llevarlo al consejo; con al menos un crédito incluido.</summary>
    public void Enviar()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WriteOffBatchStatus.Submitted));
        if (LoanCount == 0)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido, "El lote lleva al menos un crédito que cumple.", "loanCount", 0));
        Status = WriteOffBatchStatus.Submitted;
    }

    /// <summary><c>Submitted</c> → <c>Approved</c> por el punto del acta del consejo.</summary>
    public void Aprobar(int puntoDeActaId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WriteOffBatchStatus.Approved));
        Status = WriteOffBatchStatus.Approved;
        SessionItemId = puntoDeActaId;
    }

    /// <summary><c>Submitted</c> → <c>Rejected</c> por el punto del acta del consejo.</summary>
    public void Rechazar(int puntoDeActaId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WriteOffBatchStatus.Rejected));
        Status = WriteOffBatchStatus.Rejected;
        SessionItemId = puntoDeActaId;
    }

    /// <summary>
    /// <c>Approved</c> → <c>Applied</c>: quién y cuándo, y si el tipo de entidad debe reportar los castigos al supervisor con su plazo
    /// (sellado del perfil regulatorio vigente).
    /// </summary>
    public void Aplicar(int usuarioId, DateTime instante, bool reporteRequerido, DateOnly? plazoDelReporte)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WriteOffBatchStatus.Applied));
        if (reporteRequerido && plazoDelReporte is null)
            throw new ArgumentException("Un castigo que se reporta al supervisor lleva su plazo.", nameof(plazoDelReporte));
        Status = WriteOffBatchStatus.Applied;
        AppliedByUserId = usuarioId;
        AppliedAt = instante;
        SupervisorReportRequired = reporteRequerido;
        SupervisorReportDueDate = reporteRequerido ? plazoDelReporte : null;
    }

    /// <summary>Registra, una sola vez, el reporte de los castigos al supervisor de un lote aplicado que lo requiere.</summary>
    public void RegistrarReporteAlSupervisor(DateTime instante, string referencia, Guid? adjunto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referencia);
        if (Status != WriteOffBatchStatus.Applied || !SupervisorReportRequired)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido,
                "Sólo se registra el reporte de un lote aplicado que debe reportarse al supervisor.", "status", Status.ToString()));
        if (SupervisorReportedAt is not null)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido,
                "El reporte al supervisor ya se registró.", "supervisorReportedAt", SupervisorReportedAt));
        SupervisorReportedAt = instante;
        SupervisorReportReference = referencia.Trim();
        SupervisorReportAttachmentPublicId = adjunto;
    }

    /// <summary>Anular antes de la decisión del consejo; la tabla no guarda el motivo: va a la auditoría del comando.</summary>
    public void Cancelar()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WriteOffBatchStatus.Cancelled));
        Status = WriteOffBatchStatus.Cancelled;
    }
}
