namespace IngenIA365ERP.Application.Payroll.Settlements.Severance;

/// <summary>
/// Un fondo de cesantías dentro de una liquidación anual (contracts/api.md §3.2 <c>GET /</c>):
/// cuántos empleados consignan allí, cuánto, y si ya se marcó la consignación. Un empleado sin
/// fondo en la ficha cae en un bloque con <see cref="FundPublicId"/> nulo: no se esconde, porque
/// el total general tiene que igualar la cuenta por pagar del comprobante.
/// </summary>
public sealed record SeveranceFundSummaryDto(
    Guid? FundPublicId,
    string Name,
    int Employees,
    decimal Amount,
    DateOnly? DepositedAt,
    string? DepositedBy,
    string? Reference);

/// <summary>Un renglón de la lista de liquidaciones de cesantías e intereses (contracts/api.md §3.2 <c>GET /?year=&amp;status=</c>).</summary>
public sealed record SeveranceRunListItemDto(
    Guid RunPublicId,
    int Year,
    DateOnly CutoffDate,
    DateOnly? PayDate,
    int Version,
    string Status,
    int Employees,
    decimal Total,
    decimal SeveranceTotal,
    decimal InterestTotal,
    DateTime CalculatedAt,
    string CalculatedBy,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    int PaidCount,
    Guid? PostedDocumentPublicId,
    string? PostedDocumentNumber,
    IReadOnlyList<SeveranceFundSummaryDto> Funds);

/// <summary>Un empleado en la relación de consignación de su fondo (contracts/archivos.md §3.1).</summary>
public sealed record DepositScheduleLineDto(
    Guid EmployeePublicId,
    string DocumentType,
    string Document,
    string Name,
    DateOnly HireDate,
    decimal BaseSalary,
    decimal Days,
    decimal Amount,
    decimal Interest);

/// <summary>Un bloque de la relación: el fondo, sus empleados y su total; la marca de consignación si ya existe.</summary>
public sealed record DepositScheduleFundDto(
    Guid? FundPublicId,
    string? FundCode,
    string FundName,
    string? FundNit,
    string? PilaCode,
    IReadOnlyList<DepositScheduleLineDto> Lines,
    int Employees,
    decimal Total,
    decimal InterestTotal,
    DateOnly? DepositedAt,
    string? DepositedBy,
    string? Reference,
    decimal? DepositedAmount)
{
    public bool Deposited => DepositedAt is not null;
}

/// <summary>
/// La relación de consignación por fondo (contracts/api.md §3.2 <c>GET /{runId}/deposit-schedule</c>).
/// <see cref="DueDate"/> sale del parámetro <c>CESANTIAS_FECHA_LIMITE_CONSIGNACION</c> y
/// <see cref="InterestDueDate"/> de <c>INT_CESANTIAS_FECHA_LIMITE</c>, ambos leídos a la fecha de corte y
/// llevados al año siguiente al liquidado; si el parámetro no tiene vigencia, la fecha va nula y la
/// pantalla no avisa. Son avisos (D-07): ningún cálculo depende de ellos.
/// </summary>
public sealed record DepositScheduleDto(
    Guid RunPublicId,
    int Year,
    int Version,
    string Status,
    DateOnly CutoffDate,
    IReadOnlyList<DepositScheduleFundDto> Funds,
    int Employees,
    decimal GrandTotal,
    decimal InterestGrandTotal,
    DateOnly? DueDate,
    DateOnly? InterestDueDate);

/// <summary>Lo que queda registrado al marcar consignado un fondo (FR-012).</summary>
public sealed record FundDepositDto(
    Guid RunPublicId,
    Guid FundPublicId,
    string FundName,
    DateOnly DepositedAt,
    string DepositedBy,
    string? Reference,
    decimal Amount);
