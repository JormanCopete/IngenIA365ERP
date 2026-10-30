using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;

/// <summary>
/// <c>TES_MonthEndCheckRuns</c> (H; feature 015, T1, fase 33, T0410; data-model §13.1; FR-220): el cierre del mes de los cheques girados de una cuenta propia, que corre una
/// persona una sola vez por cuenta y mes: reclasifica los emitidos y no entregados al cierre al efectivo de la cuenta (<c>ChequesReclasificadosAlCierre</c>, con su reverso el
/// primer día del mes siguiente) y pasa a cuenta por pagar al beneficiario los entregados sin cobrar más allá de <c>Cheques.DiasACuentaPorPagar</c>
/// (<c>ChequeNoCobradoACuentaPorPagar</c>), nunca a ingreso. Guarda lo que hizo con el plazo sellado; los cheques que pasaron a cuenta por pagar llevan la corrida. (nuevo)
/// </summary>
public class MonthEndCheckRun : AuditableEntity, IHechoInmutable
{
    public string Number { get; set; } = string.Empty;

    public int BankAccountId { get; set; }

    public int PeriodYear { get; set; }

    public int PeriodMonth { get; set; }

    /// <summary>El último día del mes que se cierra.</summary>
    public DateOnly CutoffDate { get; set; }

    /// <summary>El <c>Cheques.DiasACuentaPorPagar</c> vigente al corte, sellado.</summary>
    public int DaysToPayable { get; set; }

    public int ReclassifiedCount { get; set; }

    public decimal ReclassifiedAmount { get; set; }

    public int MovedToPayableCount { get; set; }

    public decimal MovedToPayableAmount { get; set; }

    /// <summary>Los cheques que trató, con su número, valor y tratamiento (<c>Reclassified</c> o <c>MovedToPayable</c>).</summary>
    public string DetailJson { get; set; } = "[]";

    /// <summary>El origen del reverso del primer día del mes siguiente (un documento aparte, para que el hecho no repita el origen de la reclasificación).</summary>
    public Guid ReversalPublicId { get; set; } = Guid.NewGuid();

    public int RunByUserId { get; set; }

    public DateTime RunAt { get; set; }
}
