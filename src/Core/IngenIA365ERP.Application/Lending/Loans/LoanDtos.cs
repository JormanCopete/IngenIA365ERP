using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Loans;

// Feature 013, US4 (T0504; api.md §8.1): los DTO de las consultas del crédito. Sólo PublicId hacia afuera (Principio VI). (nuevo)

/// <summary>Una referencia por PublicId con su texto visible. (nuevo)</summary>
public sealed record RefDto(Guid PublicId, string Name);

/// <summary>La línea del crédito. (nuevo)</summary>
public sealed record LoanLineDto(Guid PublicId, string Code, string Name);

/// <summary>Un crédito en la lista: los saldos de la posición diaria vigente (nunca la autoridad). (nuevo)</summary>
public sealed record LoanListItemDto(
    Guid PublicId,
    string Number,
    RefDto Debtor,
    LoanLineDto Line,
    RefDto Office,
    string Status,
    string Origin,
    decimal DisbursedAmount,
    decimal PrincipalBalance,
    int DaysPastDue,
    DateOnly? PositionDate,
    string? InformativeRating,
    IReadOnlyList<string> Marks);

/// <summary>Las condiciones vigentes del crédito. (nuevo)</summary>
public sealed record LoanTermsDto(
    int Version,
    string RateKind,
    decimal? EffectiveAnnual,
    string? IndexCode,
    decimal? Margin,
    string DayCount,
    string RateEquivalence,
    string Rounding,
    string AllocationOrder,
    string PrepaymentPreference,
    string RulesVersion)
{
    /// <summary>K6b (bloque 2; FR-024): la unidad del capital sellada (<c>Pesos</c> o <c>Uvr</c>).</summary>
    public string DenominationUnit { get; init; } = nameof(Domain.Enums.Lending.DenominationUnit.Pesos);
}

/// <summary>Montos aprobado, desembolsado y tramos. (nuevo)</summary>
public sealed record LoanAmountsDto(decimal Approved, decimal Disbursed, int Tranches);

/// <summary>Las marcas del crédito. (nuevo)</summary>
public sealed record LoanMarksDto(bool Restructured, int RestructuringCount, bool ValidationFailed, bool Suspended, bool UsuryCapped);

/// <summary>Los vínculos del crédito. (nuevo)</summary>
public sealed record LoanLinksDto(Guid? Application, Guid? OriginLoan, string? OriginLinkKind);

/// <summary>Una garantía del crédito (sin datos del dueño más que su nombre). (nuevo)</summary>
public sealed record LoanCollateralDto(Guid PublicId, string Kind, string Description, decimal AdmissibleValue, string Status);

/// <summary>Un bloqueo vigente del deudor. (nuevo)</summary>
public sealed record LoanBlockDto(Guid PublicId, string Kind, DateOnly Since, bool Automatic);

/// <summary><c>LoanDto</c> (api.md §8.1). La etapa de cobranza y la libranza llegan con K2 y K4. (nuevo)</summary>
public sealed record LoanDto(
    Guid PublicId,
    string Number,
    string Status,
    string Origin,
    RefDto Debtor,
    IReadOnlyList<RefDto> CoDebtors,
    RefDto Office,
    LoanLineDto Line,
    string Modality,
    string? Subsegment,
    LoanTermsDto? Terms,
    LoanAmountsDto Amounts,
    LoanMarksDto Marks,
    LoanLinksDto Links,
    IReadOnlyList<LoanCollateralDto> Collaterals,
    Guid? PromissoryNote,
    IReadOnlyList<LoanBlockDto> Blocks,
    string? InvoiceReference)
{
    /// <summary>
    /// Feature 013, US6 (T0603; FR-052, B8): la validación fallida de la venta que originó el crédito —código y motivo—, y si se confirmó por
    /// excepción, cuándo. Nula si el crédito nunca tuvo la marca.
    /// </summary>
    public LoanValidationFailureDto? ValidationFailure { get; init; }
}

/// <summary>La validación fallida de la obligación de una venta (T0603): código, motivo y la confirmación por excepción si la hubo. (nuevo)</summary>
public sealed record LoanValidationFailureDto(string Code, string? Reason, DateTime? ConfirmedByExceptionAt);

/// <summary>Una cuota del plan guardado. (nuevo)</summary>
public sealed record ScheduleInstallmentDto(int Number, DateOnly PeriodStart, DateOnly DueDate, DateOnly ExigibleDate, decimal OpeningPrincipal,
    decimal Principal, decimal Interest, decimal GraceInterest, decimal Insurance, decimal OtherCharges, decimal Total, decimal ClosingPrincipal,
    bool IsExtraordinary, bool IsGraceInstallment)
{
    /// <summary>K6b (bloque 2): la cuota en UVR de un plan en UVR (capital, interés, seguro, cuota y saldo final); nula en pesos.</summary>
    public UvrInstallmentAmountsDto? Uvr { get; init; }
}

/// <summary>Los valores en UVR de una cuota del plan guardado (K6b; <c>LND_RepaymentInstallments.*Uvr</c>). (nuevo)</summary>
public sealed record UvrInstallmentAmountsDto(decimal? OpeningPrincipal, decimal? Principal, decimal? Interest, decimal? Insurance, decimal? Total,
    decimal? ClosingPrincipal);

/// <summary>Una versión del plan (api.md §8.1, <c>GET /{id}/schedule</c>). (nuevo)</summary>
public sealed record RepaymentScheduleDto(int Version, DateOnly ValidFrom, string Reason, string RulesVersion, int FirstRegeneratedInstallment,
    decimal PrincipalBase, IReadOnlyList<ScheduleInstallmentDto> Installments)
{
    /// <summary>K6b (bloque 2): en un plan en UVR, la UVR con que se valoraron en pesos sus cuotas; nula en pesos.</summary>
    public decimal? UvrValue { get; init; }
}

/// <summary>El recaudador de una operación. (nuevo)</summary>
public sealed record OperationCollectorDto(string? Code, string? Class);

/// <summary>Una operación del crédito (api.md §8.1). (nuevo)</summary>
public sealed record LoanOperationDto(
    Guid PublicId,
    string Number,
    string Kind,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal Amount,
    OperationCollectorDto? Collector,
    string? MeansClass,
    Guid? Payer,
    string? ReceiptReference,
    Guid? ReversedBy,
    Guid? Reverses,
    string Actor);

/// <summary>Una línea de la distribución de una operación. (nuevo)</summary>
public sealed record OperationDistributionDto(int? Installment, string Concept, decimal Amount);

/// <summary>Un mensaje que dejó la operación. (nuevo)</summary>
public sealed record OperationMessageDto(string Type, Guid MessagePublicId, string Status);

/// <summary>
/// Lo que el pago (o su reverso) trae además de la operación (feature 013, US5, T0560; FR-077, FR-082): la clave del recaudador, la modalidad, la
/// elección del abono y si salió de la preferencia, el excedente devuelto al recaudador, la fecha en que queda en firme, el recibo como referencia
/// (número, fecha y referencia del medio), el motivo de un reverso y la validación previa contable sellada. (nuevo)
/// </summary>
public sealed record OperationReceiptDto(
    string? IdempotencyKey,
    string? Modality,
    string? PrepaymentChoice,
    bool? ChoiceFromPreference,
    decimal? Excess,
    DateOnly? FinalityDate,
    string? ReceiptNumber,
    DateOnly? ReceiptDate,
    string? ReceiptMeansReference,
    string? Reason,
    string? PrevalidationOutcome);

/// <summary>El comprobante de aplicación: la operación, su distribución, sus mensajes y, en un pago o su reverso, el recibo (FR-077). (nuevo)</summary>
public sealed record LoanOperationDetailDto(LoanOperationDto Operation, IReadOnlyList<OperationDistributionDto> Distribution, IReadOnlyList<OperationMessageDto> Messages,
    OperationReceiptDto? Receipt = null);

/// <summary>Una fila del libro (sólo lectura). (nuevo)</summary>
public sealed record LoanTransactionDto(
    Guid PublicId,
    string Kind,
    string Concept,
    int? Installment,
    decimal Amount,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    Guid? OperationPublicId,
    Guid? ProcessRunPublicId,
    Guid? ReversesTransaction,
    Guid? ReversedBy,
    Guid? MessagePublicId)
{
    /// <summary>K6b (bloque 2): en un crédito en UVR, el monto en UVR del movimiento y la UVR de su fecha valor; nulos en pesos.</summary>
    public decimal? UvrAmount { get; init; }

    public decimal? UvrValue { get; init; }
}

/// <summary>El crédito en el alcance de quien consulta (404 genérico con <c>Lending.Access.OutOfScope</c> si no). (nuevo)</summary>
public static class CreditoEnAlcance
{
    public static async Task<Result<Loan>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, Guid loanPublicId,
        CancellationToken ct)
    {
        var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == loanPublicId && !l.IsDeleted, ct);
        if (credito is null) return Result.Failure<Loan>(LecturaDelCredito.Inexistente());
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, LecturaDelCredito.Inexistente(), auditoria, "Loan", credito.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<Loan>(dentro.Error) : Result.Success(credito);
    }
}
