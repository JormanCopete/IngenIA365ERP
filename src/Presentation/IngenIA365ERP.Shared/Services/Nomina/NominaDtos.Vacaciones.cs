namespace IngenIA365ERP.Shared.Services.Nomina;

// Feature 010 US4 — vacaciones (contracts/api.md §5 y §3.3). Los enums viajan como número:
// VacationMovementKind (1 disfrute, 2 compensación, 3 ajuste, 4 pago al retiro),
// VacationMovementStatus (0 registrado, 1 liquidado, 2 anulado), SemanaLaboral (0 L–S, 1 L–V).
// Aprobar, reversar, descartar y los excluidos usan el juego común de NominaDtos.Liquidaciones.cs.

public sealed record SaldoVacacionesDto(
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

public sealed record SaldoVacacionesDetalleDto(
    SaldoVacacionesDto Balance,
    IReadOnlyList<PasoDto> Explanation,
    decimal? MaxCompensableDays,
    decimal? CompensablePercent);

public sealed record DiaSaltadoDto(DateOnly Date, string Reason, string Text);

public sealed record MovimientoVacacionesDto(
    Guid MovementPublicId,
    Guid EmployeePublicId,
    int Kind,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal BusinessDays,
    int CalendarDays,
    string WeekPolicyUsed,
    IReadOnlyList<DiaSaltadoDto> Skipped,
    int Status,
    Guid? RunPublicId,
    string? RunStatus,
    decimal? Amount,
    string? Notes,
    string? CancelReason,
    string CreatedBy,
    DateTime CreatedAt)
{
    public string TipoTexto => TipoDeMovimiento(Kind);
    public string EstadoTexto => EstadoDeMovimiento(Status);
    public bool EsAnulable => Status == 0;

    public static string TipoDeMovimiento(int kind) => kind switch
    {
        1 => "Disfrute", 2 => "Compensación en dinero", 3 => "Ajuste", 4 => "Pago al retiro", _ => kind.ToString(),
    };

    public static string EstadoDeMovimiento(int status) => status switch
    {
        0 => "Registrado", 1 => "Liquidado", 2 => "Anulado", _ => status.ToString(),
    };
}

public sealed record VistaPreviaHabilesDto(
    DateOnly From,
    DateOnly To,
    int WorkingDays,
    int CalendarDays,
    int WorkWeek,
    IReadOnlyList<DiaSaltadoDto> Skipped,
    IReadOnlyList<AvisoCorridaDto>? Warnings = null)
{
    public string SemanaTexto => WorkWeek == 1 ? "lunes a viernes" : "lunes a sábado";

    /// <summary>Un año del rango sin festivos cargados (<c>Payroll.Holiday.YearNotLoaded</c>) y otros avisos del conteo.</summary>
    public IReadOnlyList<AvisoCorridaDto> Avisos => Warnings ?? [];
}

public sealed record NovedadDeVacacionesDto(Guid PeriodPublicId, string PeriodLabel, DateOnly From, DateOnly To, int Days, bool Retroactive, Guid? RetroactiveOfPeriodPublicId, string ConceptCode);

public sealed record VacacionesCalculadasDto(
    Guid RunPublicId,
    Guid MovementPublicId,
    int Version,
    int MovementKind,
    DateOnly CutoffDate,
    DateOnly? From,
    DateOnly? To,
    decimal WorkingDays,
    int CalendarDays,
    IReadOnlyList<DiaSaltadoDto> Skipped,
    decimal Amount,
    TotalesCorridaDto Totals,
    IReadOnlyList<NovedadDeVacacionesDto> Novelties,
    IReadOnlyList<BloqueoDto> Blockers,
    IReadOnlyList<ExcluidoDeLiquidacionDto> Excluded,
    IReadOnlyList<AvisoCorridaDto> Warnings);

public sealed record LiquidacionVacacionesDto(
    Guid RunPublicId,
    Guid? MovementPublicId,
    Guid EmployeePublicId,
    string EmployeeName,
    string Document,
    int? Kind,
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
    IReadOnlyList<AvisoCorridaDto> Warnings)
{
    public string TipoTexto => Kind is { } k ? MovimientoVacacionesDto.TipoDeMovimiento(k) : "—";
    public bool EsBorrador => Status is "Draft" or "Stale";
    public bool EstaAprobada => Status == "Approved";
    /// <summary>
    /// Sólo <c>Draft</c> se aprueba: el servidor rechaza <c>Stale</c> con <c>Payroll.Settlement.NotDraft</c>
    /// (toda vigencia nueva de política, parámetro legal o concepto deja los borradores desactualizados,
    /// D-19). Hasta el 2026-09-21 las pantallas ofrecían «Aprobar» con <see cref="EsBorrador"/>, que
    /// incluye <c>Stale</c>, y la persona recibía el rechazo con el nombre del estado en inglés.
    /// </summary>
    public bool SePuedeAprobar => Status == "Draft";
    public bool EstaDesactualizada => Status == "Stale";

    public string EstadoTexto => Status switch
    {
        "Draft" => "Borrador", "Stale" => "Desactualizado", "Superseded" => "Reemplazado/descartado",
        "Approved" => "Aprobado", "Reversed" => "Reversado", _ => Status,
    };

    public string FechasTexto => From is { } f ? (To is { } t ? $"{f:dd/MM/yyyy} – {t:dd/MM/yyyy}" : f.ToString("dd/MM/yyyy")) : "—";
}

public sealed record RegistrarVacacionesRequest(
    Guid EmployeePublicId,
    string Kind,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? CompensationDays = null,
    DateOnly? PaymentDate = null,
    string? Notes = null,
    bool AcceptRetroactive = false);

public sealed record MovimientoCreadoDto(Guid MovementPublicId);
