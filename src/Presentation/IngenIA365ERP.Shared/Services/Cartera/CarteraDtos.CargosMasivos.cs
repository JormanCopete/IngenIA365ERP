namespace IngenIA365ERP.Shared.Services.Cartera;

// 2026-11-25 (pedido del dueño; feature 026): los DTOs de la pantalla de cargos masivos (/api/lending/bulk-charges; specs/026-cargos-masivos/contracts/api.md).
// Las respuestas traen los enums como número (EnumPorNombreONumero) y aquí se leen como int; el pedido los manda por nombre. (nuevo)

/// <summary>Los filtros sobre los asociados activos (vacíos = todos). Tipos de descuento «C»/«N»; períodos «M»/«Q».</summary>
public sealed record FiltrosDeCargoMasivoDto(
    IReadOnlyList<Guid>? BranchPublicIds = null,
    IReadOnlyList<string>? DeductionTypes = null,
    IReadOnlyList<string>? DeductionPeriods = null,
    IReadOnlyList<Guid>? EmployerCompanyPublicIds = null);

/// <summary>Los datos de una corrida para crear o cambiar el borrador. Los enums van por nombre; nulos = los de la versión de la línea.</summary>
public sealed record CargoMasivoRequest(
    Guid CreditLinePublicId,
    int Period,
    DateOnly ChargeDate,
    DateOnly FirstDueDate,
    decimal? Amount,
    int? Installments,
    string? Periodicity,
    string ChannelMode,
    string? RepaymentChannel,
    string? PaymentCycle,
    string CounterpartMode,
    Guid? CounterpartPersonPublicId,
    string Source,
    FiltrosDeCargoMasivoDto? Filters);

public sealed record ConfirmacionDeCargoMasivoRequest(string ExpectedPreviewSha256);

/// <summary>Una línea de crédito marcada para cargos masivos, con sus valores por defecto.</summary>
public sealed record LineaDeCargoMasivoDto(
    Guid CreditLinePublicId,
    string Code,
    string Name,
    decimal MinAmount,
    decimal MaxAmount,
    decimal? DefaultAmount,
    int DefaultInstallments,
    int? MaxInstallments,
    int DefaultPeriodicity,
    IReadOnlyList<int> Periodicities,
    int DefaultRepaymentChannel,
    IReadOnlyList<int> RepaymentChannels,
    bool ReportsToBureaus,
    bool EntersCollection);

public sealed record OpcionesDeCargoMasivoDto(IReadOnlyList<LineaDeCargoMasivoDto> Lines, bool SegregationRequired);

public sealed record MotivoConteoDeCargoMasivoDto(string Reason, int Count);

public sealed record ContabilidadDeCargoMasivoDto(string Outcome, bool Blocked, IReadOnlyList<string> Messages, string VoucherType);

public sealed record CargoMasivoDto(
    Guid PublicId,
    string Number,
    int Status,
    Guid CreditLinePublicId,
    string CreditLineCode,
    string CreditLineName,
    int Period,
    DateOnly ChargeDate,
    DateOnly FirstDueDate,
    decimal Amount,
    int Installments,
    int Periodicity,
    int ChannelMode,
    int? RepaymentChannel,
    int? PaymentCycle,
    int CounterpartMode,
    Guid? CounterpartPersonPublicId,
    string? CounterpartPersonName,
    int Source,
    FiltrosDeCargoMasivoDto? Filters,
    int FileRows,
    string? PreviewSha256,
    DateTime? PreviewedAt,
    int TotalLines,
    int PendingCount,
    int CreatedCount,
    int SkippedCount,
    int FailedCount,
    int VoidedCount,
    decimal TotalAmount,
    IReadOnlyList<MotivoConteoDeCargoMasivoDto>? SkippedByReason,
    ContabilidadDeCargoMasivoDto? Accounting,
    bool CanConfirm,
    string? PreparedByName,
    string? ConfirmedByName,
    DateTime? ConfirmedAt,
    DateTime? CompletedAt,
    Guid? ProcessRunPublicId,
    string? ProcessRunStatus,
    long? BatchNumber,
    string? VoidReason,
    DateTime? VoidedAt,
    DateTime CreatedAt);

public sealed record CargoMasivoEnListaDto(
    Guid PublicId,
    string Number,
    int Status,
    string CreditLineCode,
    string CreditLineName,
    int Period,
    DateOnly ChargeDate,
    int CreatedCount,
    int SkippedCount,
    int FailedCount,
    int VoidedCount,
    int PendingCount,
    decimal TotalAmount,
    DateTime CreatedAt);

public sealed record LineaDeCorridaDeCargoMasivoDto(
    Guid PublicId,
    Guid PersonPublicId,
    string PersonDocument,
    string PersonName,
    string? BranchName,
    decimal Amount,
    int Installments,
    DateOnly FirstDueDate,
    int RepaymentChannel,
    int Status,
    string? Reason,
    string? ReasonDetail,
    string? Warning,
    Guid? LoanPublicId,
    string? LoanNumber,
    int? SourceRow,
    DateTime? ProcessedAt);

public sealed record PaginaDeLineasDeCargoMasivoDto(IReadOnlyList<LineaDeCorridaDeCargoMasivoDto> Items, int Total, int Page, int PageSize);

/// <summary>Los textos de la pantalla de cargos masivos: estados, motivos, formas de pago, periodicidades y ciclos. (nuevo)</summary>
public static class TextosDeCargosMasivos
{
    public const int Borrador = 1, EnCola = 2, EnCurso = 3, Terminada = 4, Anulando = 5, Anulada = 6, Descartada = 7;
    public const int LineaPendiente = 1, LineaCreada = 2, LineaOmitida = 3, LineaFallida = 4, LineaAnulada = 5;

    public static IReadOnlyList<(int Valor, string Nombre, string Texto)> Estados { get; } =
    [
        (Borrador, "Draft", "Borrador"), (EnCola, "Queued", "En cola"), (EnCurso, "Running", "En curso"), (Terminada, "Completed", "Terminada"),
        (Anulando, "Voiding", "Anulando"), (Anulada, "Voided", "Anulada"), (Descartada, "Discarded", "Descartada"),
    ];

    public static string Estado(int estado) => Estados.FirstOrDefault(e => e.Valor == estado).Texto ?? estado.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>La corrida sigue trabajando en segundo plano: la pantalla se actualiza sola.</summary>
    public static bool Trabajando(int estado) => estado is EnCola or EnCurso or Anulando;

    public static IReadOnlyList<(int Valor, string Nombre, string Texto)> EstadosDeLinea { get; } =
    [
        (LineaPendiente, "Pending", "Pendiente"), (LineaCreada, "Created", "Creada"), (LineaOmitida, "Skipped", "Omitida"),
        (LineaFallida, "Failed", "Fallida"), (LineaAnulada, "Voided", "Anulada"),
    ];

    public static string EstadoDeLinea(int estado) => EstadosDeLinea.FirstOrDefault(e => e.Valor == estado).Texto ?? estado.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Motivo(string? motivo) => motivo switch
    {
        null or "" => "—",
        "Withdrawn" => "Retirado",
        "Deceased" => "Fallecido",
        "WithdrawalInProgress" => "Retiro en trámite",
        "AlreadyCharged" => "Ya tiene el cargo del período",
        "NotAssociate" => "No es asociado",
        "NoPayrollAgreement" => "Sin pagaduría",
        "NoOwnPayrollAgreement" => "Sin convenio de nómina propia",
        "ChannelNotAllowed" => "Forma de pago sustituida",
        "LineRule" => "Regla de la línea",
        "HasMovements" => "Tiene movimientos",
        "Voided" => "Anulado",
        _ => motivo,
    };

    public static IReadOnlyList<(int Valor, string Nombre, string Texto)> FormasDePago { get; } =
    [
        (1, "Cashier", "Caja"), (2, "OwnPayroll", "Nómina propia"), (3, "PaymasterDeduction", "Pagaduría (libranza)"), (4, "DirectDebit", "Débito automático"),
    ];

    public static string FormaDePago(int? forma) => forma is null ? "—" : FormasDePago.FirstOrDefault(f => f.Valor == forma).Texto ?? forma.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string? NombreDeFormaDePago(int? forma) => forma is null ? null : FormasDePago.FirstOrDefault(f => f.Valor == forma).Nombre;

    public static IReadOnlyList<(int Valor, string Nombre, string Texto)> Periodicidades { get; } =
    [
        (0, "SinglePayment", "Pago único"), (7, "Weekly", "Semanal"), (10, "TenDay", "Decadal"), (15, "Biweekly", "Quincenal"), (30, "Monthly", "Mensual"),
        (60, "Bimonthly", "Bimestral"), (90, "Quarterly", "Trimestral"), (180, "SemiAnnual", "Semestral"), (360, "Annual", "Anual"),
    ];

    public static string Periodicidad(int periodicidad) => Periodicidades.FirstOrDefault(p => p.Valor == periodicidad).Texto ?? periodicidad.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string? NombreDePeriodicidad(int periodicidad) => Periodicidades.FirstOrDefault(p => p.Valor == periodicidad).Nombre;

    public static IReadOnlyList<(int Valor, string Nombre, string Texto)> CiclosDePago { get; } =
    [
        (1, "FirstFortnight", "Primera quincena (1 al 15)"), (2, "SecondFortnight", "Segunda quincena (16 al fin)"),
        (11, "FirstTenDay", "Primera década (1 al 10)"), (12, "SecondTenDay", "Segunda década (11 al 20)"), (13, "ThirdTenDay", "Tercera década (21 al fin)"),
        (21, "FirstWeek", "Semana 1 del mes (1 al 7)"), (22, "SecondWeek", "Semana 2 (8 al 14)"), (23, "ThirdWeek", "Semana 3 (15 al 21)"),
        (24, "FourthWeek", "Semana 4 (22 al 28)"), (25, "FifthWeek", "Semana 5 (29 al fin)"),
    ];

    public static string CicloDePago(int? ciclo) => ciclo is null ? "Sin ciclo" : CiclosDePago.FirstOrDefault(c => c.Valor == ciclo).Texto ?? ciclo.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string? NombreDeCiclo(int? ciclo) => ciclo is null ? null : CiclosDePago.FirstOrDefault(c => c.Valor == ciclo).Nombre;

    /// <summary>El resultado de la validación previa contable (<c>PrevalidationOutcome</c>).</summary>
    public static string ResultadoContable(string? resultado) => resultado switch
    {
        "Postable" => "Se puede contabilizar",
        "NotPostable" => "Falta la regla contable",
        "NoResponse" => "Contabilidad no respondió",
        "NotApplicable" => "No pasa a contabilidad",
        null or "" => "—",
        _ => resultado,
    };

    /// <summary>El período aaaamm como texto «12/2026».</summary>
    public static string Periodo(int periodo) => periodo <= 0 ? "—" : $"{periodo % 100:00}/{periodo / 100}";
}
