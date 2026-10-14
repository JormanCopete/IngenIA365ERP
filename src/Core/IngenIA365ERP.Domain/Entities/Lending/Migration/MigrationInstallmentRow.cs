using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationInstallments</c> (feature 013, K1; data-model §29.3, parte <c>installments</c>): una cuota pendiente al corte
/// de un crédito de origen. Hecho de alto volumen (<see cref="AuditableEntityLong"/>, sin diff de auditoría). Al activar forma el
/// plan inicial: se conserva el capital por cuota y el motor causa el interés desde el corte (D5). Columnas de plantillas.md §5.3.2.
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

    /// <summary>Interés corriente causado y no pagado al corte, incluido el que está en suspenso (<c>interesCorriente</c>).</summary>
    public decimal CurrentInterest { get; init; }

    /// <summary>Interés de mora causado y no pagado al corte (<c>interesDeMora</c>).</summary>
    public decimal DefaultInterest { get; init; }

    public decimal Insurance { get; init; }

    /// <summary>Otros cargos causados y no pagados (<c>otros</c>): honorarios de cobranza y otros cargos.</summary>
    public decimal OtherCharges { get; init; }

    /// <summary>Informativo (<c>interesProyectado</c>): el interés que el sistema de origen proyectaba para la cuota. El motor causa el suyo
    /// desde el corte (D5).</summary>
    public decimal? ProjectedInterest { get; init; }
}
