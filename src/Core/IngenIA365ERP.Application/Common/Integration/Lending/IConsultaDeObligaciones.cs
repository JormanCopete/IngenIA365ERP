namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La consulta de obligaciones que Cartera ofrece a Nómina, Personas, Aportes y Cumplimiento (feature 013, T0150; FR-061, FR-112;
/// contracts/interfaces.md §6.1). No escribe ni aplica el alcance de Cartera. Una excepción o el tiempo agotado es «Cartera no
/// responde» y lo resuelve quien llama con su política (Nómina avisa <c>Payroll.Settlement.PortfolioUnavailable</c> y no bloquea).
/// Antes de activar Cartera la implementación registrada es <see cref="ConsultaDeObligacionesSinActivar"/>. (nuevo)
/// </summary>
public interface IConsultaDeObligaciones
{
    /// <summary>FR-061: por persona y por obligación, como deudor o codeudor.</summary>
    Task<ObligacionesDePersonasDto> ObligacionesAsync(ConsultaDeObligaciones consulta, CancellationToken ct);

    /// <summary>FR-112: la cuota a descontar de cada empleado en un período de la nómina ordinaria.</summary>
    Task<CuotasDeNominaDto> CuotasDeNominaAsync(ConsultaDeCuotasDeNomina consulta, CancellationToken ct);
}

/// <summary>Pide las obligaciones de varias personas a una fecha. (nuevo)</summary>
public sealed record ConsultaDeObligaciones(
    IReadOnlyList<Guid> PersonPublicIds,
    DateOnly Date,
    bool IncludeAsCoDebtor,
    bool IncludeClosed = false);

/// <summary>Las obligaciones de cada persona pedida (una entrada por persona). (nuevo)</summary>
public sealed record ObligacionesDePersonasDto(
    EstadoDeCartera LendingStatus,
    IReadOnlyList<ObligacionesDePersonaDto> People);

/// <summary>Las obligaciones de una persona y si está en una carga aplicada antes de activar (C26). (nuevo)</summary>
public sealed record ObligacionesDePersonaDto(
    Guid PersonPublicId,
    bool MigratedNotActivated,
    IReadOnlyList<ObligacionDto> Obligations);

/// <summary>Una obligación con sus saldos a la fecha. (nuevo)</summary>
public sealed record ObligacionDto(
    Guid ObligationPublicId,
    string Number,
    string CreditLineCode,
    string CreditLineName,
    ClaseDeObligacion Kind,
    Guid? PaymasterPublicId,
    RolEnLaObligacion Role,
    EstadoDeObligacion Status,
    bool IsLive,
    DateOnly? ClosedOn,
    decimal TotalBalance,
    decimal DueAmount,
    int AccruedInstallments,
    int DaysPastDue,
    decimal Principal,
    decimal CurrentInterest,
    decimal DefaultInterest,
    int PendingInstallments,
    decimal InstallmentAmount);

/// <summary>
/// Pide las cuotas a descontar por nómina en un período. <paramref name="TodosLosCanales"/> (feature 014, US4; E245): las cuotas de todos los créditos vivos del
/// deudor sea cual sea su canal de pago —lo que necesita el débito automático—; por defecto, sólo los de nómina propia. (nuevo)
/// </summary>
public sealed record ConsultaDeCuotasDeNomina(
    IReadOnlyList<Guid> PersonPublicIds, DateOnly PeriodStart, DateOnly PeriodEnd, bool TodosLosCanales = false);

/// <summary>Las cuotas del período y las personas «migradas, sin activar» (C26). (nuevo)</summary>
public sealed record CuotasDeNominaDto(
    EstadoDeCartera LendingStatus,
    IReadOnlyList<CuotaDeNominaDto> Installments,
    IReadOnlyList<Guid> MigratedNotActivated);

/// <summary>Una cuota a descontar en el período, con su prelación entre obligaciones (FR-111). (nuevo)</summary>
public sealed record CuotaDeNominaDto(
    Guid PersonPublicId,
    Guid ObligationPublicId,
    string ObligationNumber,
    string CreditLineCode,
    int? InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal PastDueIncluded,
    int Order);
