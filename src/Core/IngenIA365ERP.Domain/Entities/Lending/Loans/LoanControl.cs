using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Loans;

/// <summary>
/// <c>LND_LoanControls</c> (feature 013; data-model §5.2; FR-066, FR-079, T6, T34): una fila por crédito, <b>sin saldos</b>: cerrojo y
/// marca de agua. Causar el día d exige <see cref="LastAccruedDate"/> = d − 1 (una fecha a la vez y en orden, FR-066, SC-020); un
/// crédito que falla en la corrida no avanza su marca (T5). No es hecho; sin diferencias de auditoría (C46).
/// </summary>
[SinDiffDeAuditoria]
public class LoanControl : AuditableEntity
{
    /// <summary>Único sin filtro.</summary>
    public int LoanId { get; set; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>Loan</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Loans.Loan? Loan { get; set; }

    /// <summary>Última fecha causada; al nacer, la fecha valor del desembolso o el corte (la causación del día d son los intereses de (d−1, d], T6).</summary>
    public DateOnly LastAccruedDate { get; set; }

    /// <summary>Última secuencia asignada a una operación del crédito.</summary>
    public int Sequence { get; set; }

    /// <summary>Para el <c>RegisteredAt</c> monótono por crédito (T34).</summary>
    public DateTime LastRegisteredAt { get; set; }

    /// <summary>Última posición diaria escrita (§6.1).</summary>
    public DateOnly? LastPositionDate { get; set; }
}
