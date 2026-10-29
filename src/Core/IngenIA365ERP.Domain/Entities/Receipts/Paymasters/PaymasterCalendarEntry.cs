using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Receipts.Paymasters;

/// <summary>
/// <c>RCD_PaymasterCalendar</c> (feature 015, R1, T0383; FR-082; data-model §8.1): la fecha de pago de la nómina que informa una pagaduría para un período y la fecha legal
/// de giro calculada con el calendario hábil (<c>Libranza.DiasHabilesGiro</c> sellado al registrarla). Es un aviso informativo: no bloquea nada. Una por pagaduría y
/// período; se corrige registrándola otra vez (queda en la auditoría). (nuevo)
/// </summary>
public class PaymasterCalendarEntry : AuditableEntity
{
    public int PaymasterPersonId { get; set; }

    public int PeriodYear { get; set; }

    public int PeriodMonth { get; set; }

    /// <summary>La subdivisión del período (quincena), si la pagaduría paga más de una vez al mes; 0 si no.</summary>
    public int SubPeriod { get; set; }

    public DateOnly PayrollPayDate { get; set; }

    public int BusinessDays { get; set; }

    public DateOnly LegalRemittanceDate { get; set; }

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public string? Note { get; set; }
}
