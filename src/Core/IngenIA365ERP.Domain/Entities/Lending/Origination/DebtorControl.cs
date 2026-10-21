using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_DebtorControls</c> (feature 013; data-model §2.13; T34, FR-041, FR-058): una fila por persona con algo en Cartera, sin saldos.
/// Es la fila del cerrojo por deudor que serializa reserva, aprobación, desembolso y asignación de cupo. La crea el cerrojo si falta
/// (§0.10) y nunca se da de baja. Sin diff de auditoría (C46); no es hecho.
/// </summary>
[SinDiffDeAuditoria]
public class DebtorControl : AuditableEntity
{
    public int PersonId { get; set; }
}
