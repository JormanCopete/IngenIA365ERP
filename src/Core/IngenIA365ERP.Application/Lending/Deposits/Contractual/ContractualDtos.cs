using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Contractual;

// Feature 014, US7 (T182, T185; api.md §4; FR-024, FR-074, FR-075): las entradas y respuestas del ahorro contractual. Las tasas viajan como fracción
// (0,06 = 6 % EA); los enums entran por nombre o número. (nuevo)

/// <summary>
/// La instrucción al vencimiento del contractual (api.md §4, <c>PUT /{id}/maturity-instruction</c>): trasladar a una cuenta de ahorro a la vista viva del titular
/// o girar a un beneficiario (el titular con su cuenta bancaria registrada si no se nombra otra). Sin instrucción, al vencer queda a disposición. (nuevo)
/// </summary>
public sealed record ContractualMaturityInstructionInput(CloseDeliveryKind Kind, Guid? TargetAccountPublicId = null, WithdrawalPayeeInput? Payee = null);

/// <summary>
/// El contrato que se pide al abrir un ahorro contractual (api.md §4, <c>OpenAccountInput.contractual</c>; FR-074): la cuota (no menos que la mínima de la línea)
/// y el plazo en meses (en el rango de la línea); la periodicidad, las tasas, la tolerancia y el efecto del incumplimiento los pone la línea y se sellan. (nuevo)
/// </summary>
public sealed record ContractualTermsInput(decimal Installment, int TermMonths, ContractualMaturityInstructionInput? MaturityInstruction = null);

/// <summary>
/// La instrucción al vencimiento guardada en <c>LND_MemberAccounts.MaturityInstructionJson</c> (research §19): la sellada es hecho y no se edita; ésta la
/// cambia el titular hasta el vencimiento. (nuevo)
/// </summary>
public sealed record InstruccionDeContractual(
    CloseDeliveryKind Kind,
    Guid? TargetAccountPublicId,
    Guid? PayeePersonPublicId,
    Guid? BankPublicId,
    byte? AccountType,
    string? AccountNumber,
    int? Usuario,
    DateTime Dada,
    string Motivo)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public static InstruccionDeContractual? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var leida = JsonSerializer.Deserialize<InstruccionDeContractual>(json, Json);
            return leida is { Kind: CloseDeliveryKind.TransferToAccount or CloseDeliveryKind.DisbursementOrder } ? leida : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>El contrato en la ficha de la cuenta (api.md §4, <c>MemberAccountDto.contractual</c>). (nuevo)</summary>
public sealed record ContractualSummaryDto(
    decimal Installment,
    RepaymentPeriodicity Periodicity,
    int Installments,
    int PaidInstallments,
    int ArrearsInstallments,
    ContractualStatus Status,
    DateOnly? MaturityDate);

/// <summary>Una versión de las condiciones selladas del contractual: la de la apertura, la del incumplimiento o la migrada. (nuevo)</summary>
public sealed record ContractualTermsDto(
    int Version,
    AccountTermsCause Cause,
    DateOnly EffectiveDate,
    decimal Installment,
    RepaymentPeriodicity Periodicity,
    int Installments,
    int TermMonths,
    DateOnly StartDate,
    DateOnly MaturityDate,
    decimal BaseRate,
    decimal AppliedRate,
    decimal? BonusRate,
    decimal? BreachRate,
    int? ToleratedLateInstallments,
    ContractualBreachEffect? BreachEffect,
    BonusRecognition? BonusRecognition,
    IssueTermBand? IssueTermBand,
    decimal CommittedAmount,
    decimal? ProjectedFinalAmount,
    int? MigratedPaidInstallments,
    string RulesVersion);

/// <summary>Una cuota del plan del contractual con su estado (generada o todavía no, pagada, atrasada). (nuevo)</summary>
public sealed record ContractualInstallmentDto(
    int Number,
    DateOnly DueDate,
    decimal Amount,
    decimal Paid,
    decimal Pending,
    string Status,
    bool Late,
    Guid? InstallmentPublicId);

/// <summary>El cumplimiento a hoy: cuotas pagadas y atrasadas, la tolerancia de la línea, los días de gracia y si el contrato está completo. (nuevo)</summary>
public sealed record ContractualComplianceDto(int Generated, int Paid, int Late, IReadOnlyList<int> LateNumbers, decimal LateAmount, int Tolerance,
    int GraceDays, bool ExceedsTolerance, bool Complete);

/// <summary>
/// Lo que el contrato lleva y lo proyectado (FR-024): los aportes a hoy con su interés a la tasa base y, si la línea bonifica, la bonificación a hoy; lo que
/// daría al vencimiento si se cumple; la bonificación provisionada (P26, con <c>Provisioned</c>). (nuevo)
/// </summary>
public sealed record ContractualProjectionDto(
    decimal ContributionsToDate,
    decimal InterestAtBaseToDate,
    decimal? BonusToDate,
    decimal ValueAtMaturityAtBase,
    decimal? ValueAtMaturityIfFulfilled,
    decimal? ProjectedBonus,
    decimal ProvisionedBonus,
    string Explanation);

/// <summary>El ahorro contractual (api.md §4, <c>GET /{id}/contractual</c>; la pantalla <c>ContratoDeAhorro</c>). (nuevo)</summary>
public sealed record ContractualDto(
    Guid AccountPublicId,
    string AccountNumber,
    MemberAccountStatus AccountStatus,
    ContractualStatus Status,
    ContractualTermsDto Terms,
    IReadOnlyList<ContractualTermsDto> History,
    IReadOnlyList<ContractualInstallmentDto> Installments,
    ContractualComplianceDto Compliance,
    ContractualProjectionDto Projection,
    ContractualMaturityInstructionDto? Instruction,
    DateOnly? MaturedOn,
    ContractualMaturityDestinations AllowedMaturityDestinations);

/// <summary>La instrucción al vencimiento que rige: trasladar a una cuenta o girar, con su destino legible. (nuevo)</summary>
public sealed record ContractualMaturityInstructionDto(CloseDeliveryKind Kind, Guid? TargetAccountPublicId, string? TargetAccountNumber,
    MemberAccountPersonDto? Payee, string? Reason, DateTime? GivenAt);
