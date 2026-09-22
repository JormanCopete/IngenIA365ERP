using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

namespace IngenIA365ERP.Application.Payroll.Vacations;

/// <summary>Un renglón de <c>GET /api/payroll/vacations/balances</c>: el saldo derivado de un empleado (contracts/api.md §5).</summary>
public sealed record VacationBalanceSummaryDto(
    Guid EmployeePublicId,
    string Name,
    string Document,
    DateTime HireDate,
    DateOnly AsOf,
    decimal AccruedDays,
    decimal OpeningDays,
    decimal EnjoyedDays,
    decimal CompensatedDays,
    decimal AdjustedDays,
    decimal SettlementPaidDays,
    decimal PendingDays,
    DateOnly? LastEnjoymentTo,
    int WorkedDays,
    int SuspensionDays);

/// <summary>El saldo de un empleado con la explicación paso a paso (<c>GET /employees/{id}/balance</c>).</summary>
public sealed record VacationBalanceDetailDto(
    VacationBalanceSummaryDto Balance,
    IReadOnlyList<ExplanationStep> Explanation,
    decimal? MaxCompensableDays,
    decimal? CompensablePercent);

/// <summary>Un movimiento de vacaciones tal como lo ve la pantalla (<c>GET /employees/{id}/movements</c>).</summary>
public sealed record VacationMovementDto(
    Guid MovementPublicId,
    Guid EmployeePublicId,
    VacationMovementKind Kind,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal BusinessDays,
    int CalendarDays,
    string WeekPolicyUsed,
    IReadOnlyList<SkippedDayDto> Skipped,
    VacationMovementStatus Status,
    Guid? RunPublicId,
    string? RunStatus,
    decimal? Amount,
    string? Notes,
    string? CancelReason,
    string CreatedBy,
    DateTime CreatedAt)
{
    public static VacationMovementDto From(VacationMovement m, PayrollRun? run) => new(
        m.PublicId, m.Employee?.PublicId ?? Guid.Empty, m.Kind, m.StartDate, m.EndDate, m.BusinessDays, m.CalendarDays, m.WeekPolicyUsed,
        SkippedDayDto.Leer(m.SkippedDaysJson), m.Status, run?.PublicId, run?.Status.ToString(), run?.TotalNet,
        m.Notes, m.CancelReason, m.CreatedBy ?? string.Empty, m.CreatedAt);
}

/// <summary>Un día del rango que no cuenta como hábil: <c>Sunday</c>, <c>Saturday</c> o <c>Holiday:&lt;nombre&gt;</c> (contracts/api.md §5), con el texto para la pantalla.</summary>
public sealed record SkippedDayDto(DateOnly Date, string Reason, string Text)
{
    public static SkippedDayDto From(DiaSaltado d) => new(DateOnly.FromDateTime(d.Fecha), Codigo(d.Motivo), d.Motivo);

    private static string Codigo(string motivo) =>
        motivo.StartsWith("domingo", StringComparison.OrdinalIgnoreCase) ? "Sunday"
        : motivo.StartsWith("sábado", StringComparison.OrdinalIgnoreCase) ? "Saturday"
        : motivo.StartsWith("festivo: ", StringComparison.OrdinalIgnoreCase) ? "Holiday:" + motivo["festivo: ".Length..]
        : motivo.StartsWith("festivo", StringComparison.OrdinalIgnoreCase) ? "Holiday"
        : motivo;

    /// <summary>Lo que se guarda en <c>PAY_VacationMovements.SkippedDaysJson</c>: <c>[{ fecha, motivo }]</c>.</summary>
    public static string Escribir(IEnumerable<SkippedDayDto> dias) =>
        System.Text.Json.JsonSerializer.Serialize(dias.Select(d => new { fecha = d.Date, motivo = d.Text, codigo = d.Reason }), Runs.RunJson.Options);

    public static IReadOnlyList<SkippedDayDto> Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var lista = new List<SkippedDayDto>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (!e.TryGetProperty("fecha", out var f) || !DateOnly.TryParse(f.GetString(), out var fecha)) continue;
                var motivo = e.TryGetProperty("motivo", out var m) ? m.GetString() ?? string.Empty : string.Empty;
                var codigo = e.TryGetProperty("codigo", out var c) ? c.GetString() ?? Codigo(motivo) : Codigo(motivo);
                lista.Add(new SkippedDayDto(fecha, codigo, motivo));
            }
            return lista;
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}

/// <summary>La vista previa obligatoria antes de guardar un disfrute (FR-015; <c>POST /working-days</c>).</summary>
public sealed record WorkingDaysPreviewDto(
    DateOnly From,
    DateOnly To,
    int WorkingDays,
    int CalendarDays,
    SemanaLaboral WorkWeek,
    IReadOnlyList<SkippedDayDto> Skipped,
    IReadOnlyList<WarningDto> Warnings);

/// <summary>Una novedad que la aprobación dejará (o dejó) en un período cubierto por el disfrute.</summary>
public sealed record VacationNoveltyDto(Guid PeriodPublicId, string PeriodLabel, DateOnly From, DateOnly To, int Days, bool Retroactive, Guid? RetroactiveOfPeriodPublicId, string ConceptCode);
