using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Receipts.Operations;

/// <summary>
/// <c>RCD_PayerControls</c> (feature 015, T0036; data-model §3.1, §0.5 nivel 5): la fila de cerrojo de una persona en Recaudo. Sin datos de
/// negocio: serializa las operaciones de Recaudo sobre la misma persona (pagador, quien efectúa el pago y beneficiarios). La crea
/// <c>CerrojoDeRecaudo</c> si falta, como <c>LND_DebtorControls</c> en Cartera; una por persona (<c>UK_RCD_PayerControls_PersonId</c>).
/// </summary>
public class PayerControl : AuditableEntity
{
    /// <summary>La persona (<c>COR_People.Id</c>).</summary>
    public int PersonId { get; set; }
}
