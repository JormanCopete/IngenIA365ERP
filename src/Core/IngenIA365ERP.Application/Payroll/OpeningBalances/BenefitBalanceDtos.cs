using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.OpeningBalances;

/// <summary>Una liquidación aprobada que consumió el saldo (la reversión lo libera).</summary>
public sealed record BenefitBalanceConsumerDto(Guid RunPublicId, PayrollRunKind Kind);

/// <summary>
/// Una fila de <c>PAY_EmployeeBenefitOpeningBalances</c>: el saldo de apertura o un ajuste
/// posterior. Los valores de un ajuste son el saldo <b>completo</b> corregido, no la diferencia.
/// </summary>
public sealed record BenefitBalanceRowDto(
    Guid PublicId,
    OpeningBalanceKind Kind,
    DateOnly AsOfDate,
    decimal PendingVacationDays,
    decimal AccruedSeverance,
    decimal AccruedSeveranceInterest,
    decimal AccruedServiceBonus,
    int? ServiceBonusDaysAccrued,
    int? SeveranceDaysAccrued,
    string? Notes,
    Guid? AdjustsBalancePublicId,
    string? AdjustmentReason,
    BenefitBalanceConsumerDto? ConsumedBy,
    string? CreatedBy,
    DateTime CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt);

/// <summary>Una fila del listado (contracts/api.md §4): el empleado y su saldo vigente, si lo tiene.</summary>
public sealed record BenefitBalanceSummaryDto(
    Guid EmployeePublicId,
    string Name,
    string Document,
    DateTime HireDate,
    bool HiredBeforeStart,
    DateOnly? AsOfDate,
    decimal? PendingVacationDays,
    decimal? AccruedSeverance,
    decimal? AccruedSeveranceInterest,
    decimal? AccruedServiceBonus,
    IReadOnlyList<BenefitBalanceConsumerDto> ConsumedBy,
    bool IsEditable,
    DateTime? UpdatedAt,
    string? UpdatedBy);

/// <summary>El saldo vigente de un empleado y su historial completo (apertura y ajustes).</summary>
public sealed record BenefitBalanceDetailDto(
    Guid EmployeePublicId,
    string Name,
    string Document,
    DateTime HireDate,
    DateOnly? PayrollStartDate,
    bool HiredBeforeStart,
    BenefitBalanceRowDto? Current,
    IReadOnlyList<BenefitBalanceRowDto> History);

public static class BenefitBalanceErrors
{
    public static Error Consumed(IReadOnlyList<Guid> corridas) =>
        new ErrorConDatos("Payroll.BenefitBalance.Consumed",
            $"El saldo inicial ya lo consumió {(corridas.Count == 1 ? "una liquidación aprobada" : $"{corridas.Count} liquidaciones aprobadas")}: no se reemplaza. Registrá un ajuste con motivo (adjustments) o reversá la liquidación.",
            new { runPublicIds = corridas });

    public static Error AsOfAfterFirstRun(DateOnly asOf, DateOnly primera) =>
        new("Payroll.BenefitBalance.AsOfAfterFirstRun",
            $"La fecha de corte {asOf:dd/MM/yyyy} es posterior a la primera corrida aprobada del empleado ({primera:dd/MM/yyyy}). El saldo inicial es lo que traía antes de que la nómina corriera aquí.");

    public static Error NegativeValue(string campo) =>
        new("Payroll.BenefitBalance.NegativeValue", $"{campo} no puede ser negativo. Un saldo a favor de la cooperativa no es un saldo inicial de prestaciones.");

    public static readonly Error EmployeeNotFound = new("Payroll.Employee.NotFound", "No existe el empleado indicado.");

    public static readonly Error NoBalanceToAdjust = new("Payroll.BenefitBalance.NoBalanceToAdjust",
        "El empleado no tiene saldo inicial: registralo primero (PUT); un ajuste corrige uno que ya existe.");

    public static Error AsOfDuplicate(DateOnly asOf) =>
        new("Payroll.BenefitBalance.AsOfDuplicate", $"Ya hay un ajuste del saldo con corte {asOf:dd/MM/yyyy}; elegí otra fecha de corte.");
}
