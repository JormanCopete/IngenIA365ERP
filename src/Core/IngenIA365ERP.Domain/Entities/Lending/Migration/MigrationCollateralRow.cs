using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationCollaterals</c> (feature 013, K1; data-model §29.3, parte <c>collaterals</c>; plantillas.md §5.3.7): una garantía o un pagaré y
/// el crédito que respalda; llave crédito + tipo + referencia. Una garantía que respalda varios créditos viene en una fila por crédito con la misma
/// referencia y el mismo tipo. Hecho sin diff de auditoría. Al activar nacen <c>LND_Collaterals</c> y, con <c>PromissoryNote</c>, el pagaré con su
/// canal y su id externo sellados. El codeudor no viene aquí (parte <c>co-debtors</c>).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationCollateralRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>El crédito de <c>loans</c> o <c>charged-off</c> (<c>numeroDeCredito</c>).</summary>
    public string SourceLoanNumber { get; init; } = string.Empty;

    /// <summary>Cualquier <see cref="Enums.Lending.CollateralKind"/> salvo <c>CoDebtor</c> (<c>tipo</c>).</summary>
    public CollateralKind CollateralKind { get; init; }

    /// <summary>Número del pagaré, matrícula, placa o certificado del fondo (<c>referencia</c>, hasta 40).</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>Valor de la garantía (<c>valor</c>).</summary>
    public decimal Value { get; init; }

    public DateOnly ConstitutedOn { get; init; }

    public DateOnly? ValidUntil { get; init; }

    public CollateralStatus Status { get; init; } = CollateralStatus.Valid;

    public string? Description { get; init; }

    /// <summary>Prenda e hipoteca: avalúo, póliza y aseguradora.</summary>
    public decimal? AppraisalValue { get; init; }

    public DateOnly? AppraisalDate { get; init; }

    public string? PolicyNumber { get; init; }

    public DateOnly? PolicyExpiresOn { get; init; }

    public int? InsurerPersonId { get; init; }

    /// <summary>Con <c>PromissoryNote</c> (<c>clasePagare</c>).</summary>
    public PromissoryNoteKind? PromissoryNoteKind { get; init; }

    /// <summary>Con <c>Dematerialized</c>: se sellan como <c>CustodianChannelCode</c> y <c>ExternalNoteId</c> del pagaré.</summary>
    public string? CustodianChannelCode { get; init; }

    public string? ExternalNoteId { get; init; }

    /// <summary>Con <c>GuaranteeFund</c>: el fondo, la cobertura y la comisión (fracciones).</summary>
    public int? GuaranteeFundPersonId { get; init; }

    public decimal? CoveragePercentage { get; init; }

    public decimal? FeePercentage { get; init; }
}
