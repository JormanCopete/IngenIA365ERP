using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.Settlements.Vacation;

/// <summary>
/// Lo que devuelve registrar o recalcular una liquidación de vacaciones (contracts/api.md §3.3
/// <c>POST /</c>): el movimiento y la corrida creados en la misma acción, los hábiles y
/// calendario congelados, los días saltados, el valor y las novedades que la aprobación dejará
/// en cada período cubierto.
/// </summary>
public sealed record VacationCalculatedDto(
    Guid RunPublicId,
    Guid MovementPublicId,
    int Version,
    VacationMovementKind MovementKind,
    DateOnly CutoffDate,
    DateOnly? From,
    DateOnly? To,
    decimal WorkingDays,
    int CalendarDays,
    IReadOnlyList<SkippedDayDto> Skipped,
    decimal Amount,
    RunTotalsDto Totals,
    IReadOnlyList<VacationNoveltyDto> Novelties,
    IReadOnlyList<RunBlockerDto> Blockers,
    IReadOnlyList<ExcludedEmployeeDto> Excluded,
    IReadOnlyList<WarningDto> Warnings);

/// <summary>Un renglón de <c>GET /api/payroll/settlements/vacations</c>.</summary>
public sealed record VacationRunRowDto(
    Guid RunPublicId,
    Guid? MovementPublicId,
    Guid EmployeePublicId,
    string EmployeeName,
    string Document,
    VacationMovementKind? Kind,
    DateOnly? From,
    DateOnly? To,
    decimal WorkingDays,
    int CalendarDays,
    decimal? CompensatedDays,
    decimal Amount,
    string Status,
    int Version,
    DateOnly CutoffDate,
    DateOnly? PayDate,
    DateTime CalculatedAt,
    string CalculatedBy,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    Guid? AccountingDocumentPublicId,
    string? AccountingDocumentNumber,
    IReadOnlyList<RunWarningDto> Warnings);
