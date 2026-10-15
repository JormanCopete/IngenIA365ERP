using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_Waivers</c> (feature 013, K3, T0871; data-model §25.4; FR-094): una condonación sobre un crédito, por concepto (sus líneas,
/// <see cref="WaiverLine"/>), con la fecha de la liquidación base, los días de mora y si el deudor es vinculado sellados al radicar —son las
/// condiciones de la atribución (FR-040)—. El ciclo es el de la novedad:
/// <code>
/// Draft ──radicar──▶ Submitted ──decisión──▶ Approved ──aplicar──▶ Applied
///                              └──────────▶ Rejected
/// Draft · Submitted · Approved ──anular (el motivo va a la auditoría)──▶ Cancelled
/// </code>
/// Las líneas se arman sólo en <c>Draft</c> y el total es su suma; aplicarla deja la operación <c>Waiver</c> y queda congelada. No es hecho.
/// (nuevo)
/// </summary>
public class Waiver : AuditableEntity
{
    public const string CodigoNoEditable = "Lending.Waiver.NotEditable";
    public const string CodigoEstadoInvalido = "Lending.Waiver.InvalidStatus";
    public const string CodigoNoAprobada = "Lending.Waiver.NotApproved";

    /// <summary>Numerador de Cartera; único.</summary>
    public string Number { get; init; } = string.Empty;

    public int LoanId { get; init; }

    public WaiverStatus Status { get; private set; } = WaiverStatus.Draft;

    /// <summary>Fecha de la liquidación base.</summary>
    public DateOnly AsOfDate { get; init; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Días de mora al pedirla: condición de la atribución.</summary>
    public int DaysPastDueAtRequest { get; init; }

    /// <summary>¿Deudor vinculado al pedirla? Condición de la atribución (FR-040).</summary>
    public bool IsRelatedParty { get; init; }

    /// <summary>Suma de las líneas.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary><c>Lending.Condonations.Request</c>.</summary>
    public int RequestedByUserId { get; init; }

    public DateTime RequestedAt { get; init; }

    public int? FinalDecisionId { get; private set; }

    /// <summary>Operación <c>Waiver</c>; emite <c>CondonacionAprobada</c> por concepto (<c>CN</c>).</summary>
    public long? AppliedOperationId { get; private set; }

    /// <summary>Navegación para enlazar en el mismo guardado la operación todavía sin Id.</summary>
    public LoanOperation? AppliedOperation { get; private set; }

    /// <summary><c>Lending.Condonations.Apply</c>.</summary>
    public int? AppliedByUserId { get; private set; }

    public DateTime? AppliedAt { get; private set; }

    /// <summary>Las líneas por concepto y cuota.</summary>
    public List<WaiverLine> Lines { get; private set; } = [];

    // ------------------------------------------------------------------------------------------- comportamiento (T0871) --

    private static readonly IReadOnlyDictionary<WaiverStatus, WaiverStatus[]> Transiciones = new Dictionary<WaiverStatus, WaiverStatus[]>
    {
        [WaiverStatus.Draft] = [WaiverStatus.Submitted, WaiverStatus.Cancelled],
        [WaiverStatus.Submitted] = [WaiverStatus.Approved, WaiverStatus.Rejected, WaiverStatus.Cancelled, WaiverStatus.Draft],
        [WaiverStatus.Approved] = [WaiverStatus.Applied, WaiverStatus.Cancelled],
    };

    public static bool PuedePasar(WaiverStatus desde, WaiverStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    public bool Editable => Status == WaiverStatus.Draft;

    /// <summary>¿Condona capital? Sólo con nivel de excepción (<c>Lending.Waiver.PrincipalRequiresException</c>).</summary>
    public bool CondonaCapital => Lines.Any(l => l.Concept == LoanConcept.Principal);

    /// <summary>Crea la condonación en borrador con las condiciones de la atribución selladas.</summary>
    public static Waiver Nueva(string numero, int creditoId, DateOnly fechaBase, string motivo, int diasDeMora, bool vinculado, int usuarioId, DateTime instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentOutOfRangeException.ThrowIfNegative(diasDeMora);
        return new Waiver
        {
            Number = numero,
            LoanId = creditoId,
            AsOfDate = fechaBase,
            Reason = motivo.Trim(),
            DaysPastDueAtRequest = diasDeMora,
            IsRelatedParty = vinculado,
            RequestedByUserId = usuarioId,
            RequestedAt = instante,
        };
    }

    public ProblemaDeCartera? ExigirEditable() => Editable
        ? null
        : ProblemaDeCartera.Con(CodigoNoEditable, "La condonación ya no se puede modificar: sólo se edita en borrador.", "status", Status.ToString());

    public ProblemaDeCartera? PuedeIrA(WaiverStatus hacia)
    {
        if (PuedePasar(Status, hacia)) return null;
        if (hacia == WaiverStatus.Applied)
            return ProblemaDeCartera.Con(CodigoNoAprobada, "Sólo se aplica una condonación aprobada.", "status", Status.ToString());
        return new ProblemaDeCartera(CodigoEstadoInvalido, $"La condonación está en «{Status}» y no puede pasar a «{hacia}».",
            new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() });
    }

    /// <summary>Agrega una línea (concepto, cuota, monto y saldo disponible a la fecha base); sólo en borrador y sin repetir concepto y cuota.</summary>
    public WaiverLine AgregarLinea(LoanConcept concepto, int? cuota, decimal monto, decimal disponible)
    {
        ReglaDeCarteraException.SiHay(ExigirEditable());
        ReglaDeCarteraException.SiHay(WaiverLine.Validar(concepto, monto, disponible));
        if (Lines.Any(l => l.Concept == concepto && l.InstallmentNumber == cuota))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido,
                $"La condonación ya tiene una línea de {concepto}" + (cuota is { } c ? $" en la cuota {c}." : " en todas las cuotas."), "concept", concepto.ToString()));
        var linea = new WaiverLine { WaiverId = Id, Concept = concepto, InstallmentNumber = cuota, Amount = monto, AvailableAtRequest = disponible };
        Lines.Add(linea);
        TotalAmount = Lines.Sum(l => l.Amount);
        return linea;
    }

    /// <summary>Quita una línea del borrador.</summary>
    public void QuitarLinea(WaiverLine linea)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ReglaDeCarteraException.SiHay(ExigirEditable());
        Lines.Remove(linea);
        TotalAmount = Lines.Sum(l => l.Amount);
    }

    /// <summary><c>Draft</c> → <c>Submitted</c>, con al menos una línea.</summary>
    public void Radicar()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WaiverStatus.Submitted));
        if (Lines.Count == 0)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido, "La condonación lleva al menos un concepto.", "concepts", 0));
        Status = WaiverStatus.Submitted;
    }

    public void Aprobar(int decisionId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WaiverStatus.Approved));
        Status = WaiverStatus.Approved;
        FinalDecisionId = decisionId;
    }

    public void Rechazar(int decisionId)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WaiverStatus.Rejected));
        Status = WaiverStatus.Rejected;
        FinalDecisionId = decisionId;
    }

    /// <summary>K3 (T0884): <c>Submitted</c> → <c>Draft</c> con la decisión <c>Return</c>: vuelve a borrador para corregir sus líneas.</summary>
    public void Devolver()
    {
        if (Status != WaiverStatus.Submitted)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido, "Sólo se devuelve una condonación radicada.", "status", Status.ToString()));
        Status = WaiverStatus.Draft;
    }

    /// <summary><c>Approved</c> → <c>Applied</c> con la operación <c>Waiver</c>; desde aquí queda congelada.</summary>
    public void Aplicar(LoanOperation operacion, int usuarioId, DateTime instante)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        ReglaDeCarteraException.SiHay(PuedeIrA(WaiverStatus.Applied));
        if (operacion.Kind != LoanOperationKind.Waiver)
            throw new ArgumentException("Una condonación se aplica con una operación Waiver.", nameof(operacion));
        Status = WaiverStatus.Applied;
        AppliedOperation = operacion;
        if (operacion.Id > 0) AppliedOperationId = operacion.Id;
        AppliedByUserId = usuarioId;
        AppliedAt = instante;
    }

    /// <summary>Anular antes de aplicar; la tabla no guarda el motivo: va a la auditoría del comando.</summary>
    public void Cancelar()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(WaiverStatus.Cancelled));
        Status = WaiverStatus.Cancelled;
    }
}
