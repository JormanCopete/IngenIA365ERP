using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_PaymentAgreements</c> (feature 013, K4, T0936; data-model §26.9; FR-120): un acuerdo de pago sobre lo vencido, <b>sin cambiar las
/// condiciones del crédito</b>, con su propio calendario (<see cref="Installments"/>). El ciclo:
/// <code>
/// Proposed ──aprobar (otra persona)──▶ Approved ──anular (motivo)──▶ Cancelled
///          ├─rechazar (motivo)──▶ Rejected
///          └─anular (motivo)───▶ Cancelled
/// </code>
/// El calendario no cambia después de proponerlo: cada cuota es un hecho. El cumplimiento se deriva (<see cref="CumplimientoDeAcuerdo"/>). Un
/// acuerdo que cambia condiciones no es esto: es una novedad (<c>LND_LoanNovelties.PaymentAgreementId</c>). (nuevo)
/// </summary>
public class PaymentAgreement : AuditableEntity
{
    public const string CodigoNoPropuesto = "Lending.PaymentAgreement.NotProposed";
    public const string CodigoEstadoInvalido = "Lending.PaymentAgreement.InvalidStatus";
    public const string CodigoCalendarioInvalido = "Lending.PaymentAgreement.ScheduleInvalid";
    public const string CodigoSegregacion = "Lending.Segregation.Conflict";

    /// <summary>Numerador de Cartera; único.</summary>
    public string Number { get; init; } = string.Empty;

    public int LoanId { get; init; }

    public int PersonId { get; init; }

    public PaymentAgreementStatus Status { get; private set; } = PaymentAgreementStatus.Proposed;

    public decimal PastDueAmountAtAgreement { get; init; }

    public decimal TotalAmount { get; init; }

    public DateOnly StartDate { get; init; }

    public int InstallmentCount { get; init; }

    /// <summary><c>Lending.PaymentAgreements.Create</c>.</summary>
    public int CreatedByUserId { get; init; }

    /// <summary><c>Lending.PaymentAgreements.Approve</c>.</summary>
    public int? ApprovedByUserId { get; private set; }

    public DateTime? ApprovedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public string? CancelReason { get; private set; }

    public List<PaymentAgreementInstallment> Installments { get; private set; } = [];

    /// <summary>
    /// Nulo si el calendario sirve: al menos una cuota, montos positivos y vencimientos crecientes desde el inicio. Qué cabe sobre lo vencido
    /// y que el acuerdo no cambie las condiciones lo decide el comando (<c>Lending.PaymentAgreement.ChangesTerms</c>).
    /// </summary>
    public static ProblemaDeCartera? ValidarCalendario(DateOnly inicio, IReadOnlyList<(DateOnly Vence, decimal Valor)> cuotas)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        if (cuotas.Count == 0)
            return ProblemaDeCartera.Con(CodigoCalendarioInvalido, "El acuerdo lleva al menos una cuota.", "installments", 0);
        for (var i = 0; i < cuotas.Count; i++)
        {
            if (cuotas[i].Valor <= 0m)
                return ProblemaDeCartera.Con(CodigoCalendarioInvalido, $"La cuota {i + 1} del acuerdo es mayor que cero.", "number", i + 1);
            var anterior = i == 0 ? inicio.AddDays(-1) : cuotas[i - 1].Vence;
            if (cuotas[i].Vence <= anterior)
                return ProblemaDeCartera.Con(CodigoCalendarioInvalido,
                    $"La cuota {i + 1} vence el {cuotas[i].Vence:yyyy-MM-dd}: los vencimientos van desde el inicio y crecen.", "number", i + 1);
        }
        return null;
    }

    public static PaymentAgreement Proponer(string numero, int creditoId, int personaId, decimal vencido, DateOnly inicio,
        IReadOnlyList<(DateOnly Vence, decimal Valor)> cuotas, int usuarioId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        ReglaDeCarteraException.SiHay(ValidarCalendario(inicio, cuotas));
        var acuerdo = new PaymentAgreement
        {
            Number = numero.Trim(),
            LoanId = creditoId,
            PersonId = personaId,
            PastDueAmountAtAgreement = vencido,
            TotalAmount = cuotas.Sum(c => c.Valor),
            StartDate = inicio,
            InstallmentCount = cuotas.Count,
            CreatedByUserId = usuarioId,
        };
        for (var i = 0; i < cuotas.Count; i++)
            acuerdo.Installments.Add(new PaymentAgreementInstallment { Number = i + 1, DueDate = cuotas[i].Vence, Amount = cuotas[i].Valor });
        return acuerdo;
    }

    public IReadOnlyList<CuotaDeAcuerdo> Calendario() =>
        Installments.OrderBy(c => c.Number).Select(c => new CuotaDeAcuerdo(c.Number, c.DueDate, c.Amount)).ToList();

    /// <summary>Sólo uno propuesto, y no por quien lo creó (la regla completa de segregación la aplica el comando).</summary>
    public void Aprobar(int usuarioId, DateTime instante)
    {
        if (Status != PaymentAgreementStatus.Proposed)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoNoPropuesto, "Sólo se aprueba un acuerdo propuesto.", "status", Status.ToString()));
        if (usuarioId == CreatedByUserId)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoSegregacion, "Quien propuso el acuerdo no lo aprueba.", "createdBy", CreatedByUserId));
        Status = PaymentAgreementStatus.Approved;
        ApprovedByUserId = usuarioId;
        ApprovedAt = instante;
    }

    public void Rechazar(string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (Status != PaymentAgreementStatus.Proposed)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoNoPropuesto, "Sólo se rechaza un acuerdo propuesto.", "status", Status.ToString()));
        Status = PaymentAgreementStatus.Rejected;
        RejectionReason = motivo.Trim();
    }

    public void Anular(string motivo, DateTime instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (Status is not (PaymentAgreementStatus.Proposed or PaymentAgreementStatus.Approved))
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido,
                $"El acuerdo está en «{Status}»: sólo se anula uno propuesto o aprobado.", "status", Status.ToString()));
        Status = PaymentAgreementStatus.Cancelled;
        CancelledAt = instante;
        CancelReason = motivo.Trim();
    }
}

/// <summary>
/// <c>LND_PaymentAgreementInstallments</c> (feature 013, K4, T0936; data-model §26.9): una cuota del calendario del acuerdo. <c>UK
/// (PaymentAgreementId, Number)</c>. Hecho de sólo inserción (<see cref="IHechoInmutable"/>). (nuevo)
/// </summary>
public class PaymentAgreementInstallment : AuditableEntity, IHechoInmutable
{
    public int PaymentAgreementId { get; init; }

    public int Number { get; init; }

    public DateOnly DueDate { get; init; }

    public decimal Amount { get; init; }
}
