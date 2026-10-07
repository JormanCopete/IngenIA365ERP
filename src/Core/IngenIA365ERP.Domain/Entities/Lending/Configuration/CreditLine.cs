using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLines</c> (feature 013, T0210; data-model §1.2; FR-019, FR-021, FR-032): la identidad estable de una línea de
/// crédito. Todo lo que cambia con el tiempo va en su versión (<see cref="CreditLineVersion"/>). El código es la nomenclatura de la
/// cooperativa (<c>CodigoDeCatalogo</c>) y no cambia: es la dimensión <c>CreditLineCode</c> de la matriz contable (T52). Con
/// créditos vivos no se borra: se cierra a créditos nuevos. Forma sin comportamiento: lo da US1.
/// </summary>
public class CreditLine : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Destino del crédito que la línea financia (FR-019).</summary>
    public string? Purpose { get; set; }

    public CreditLineStatus Status { get; set; } = CreditLineStatus.Draft;

    public CreditLineClosedReason? ClosedReason { get; set; }

    public DateTime? ClosedAt { get; set; }

    public int? ClosedByUserId { get; set; }

    public string? CloseNotes { get; set; }

    /// <summary>Día desde el que la versión vigente supera la usura de su certificación (FR-021, US1-5); nulo = no bloqueada.</summary>
    public DateOnly? UsuryBlockedSince { get; set; }

    /// <summary>El valor nacional <c>USURA</c> que la bloqueó (copia de <c>COR_NationalValues</c>, sin FK).</summary>
    public Guid? UsuryBlockedByValuePublicId { get; set; }
}
