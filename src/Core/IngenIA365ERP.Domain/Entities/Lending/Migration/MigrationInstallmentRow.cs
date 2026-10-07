using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationInstallments</c> (feature 013, K1; data-model §29.3, parte <c>installments</c>): una cuota pendiente al corte
/// de un crédito de origen. Hecho de alto volumen (<see cref="AuditableEntityLong"/>, sin diff de auditoría). Al activar forma el
/// plan inicial: se conserva el capital por cuota y el motor causa el interés desde el corte (D5).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationInstallmentRow : AuditableEntityLong, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    public int InstallmentNumber { get; init; }

    public DateOnly DueDate { get; init; }

    public decimal Principal { get; init; }

    public decimal Interest { get; init; }

    public decimal Insurance { get; init; }

    public decimal OtherCharges { get; init; }
}
