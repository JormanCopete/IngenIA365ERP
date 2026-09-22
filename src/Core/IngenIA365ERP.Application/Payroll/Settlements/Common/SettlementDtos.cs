using IngenIA365ERP.Application.Payroll.Runs;

namespace IngenIA365ERP.Application.Payroll.Settlements.Common;

/// <summary>Un aviso que la pantalla muestra sin bloquear (contracts/api.md §3.5): código estable, texto y datos para desarmarlo.</summary>
public sealed record WarningDto(string Code, string Message, object? Data);

/// <summary>Un empleado que quedó fuera de la liquidación, con el porqué (<c>SalarioIntegral</c>, <c>AprendizLectiva</c>, <c>YaPagadaEnDefinitiva</c>, <c>SinDiasEnElSemestre</c>…).</summary>
public sealed record ExcludedEmployeeDto(Guid EmployeePublicId, string Name, string ReasonCode, string Reason);

/// <summary>Lo que devuelve calcular o recalcular cualquiera de las cuatro liquidaciones (contracts/api.md §3.1 <c>POST /</c>).</summary>
public sealed record SettlementCalculatedDto(
    Guid RunPublicId,
    int Version,
    string Kind,
    DateOnly CutoffDate,
    int Employees,
    RunTotalsDto Totals,
    IReadOnlyList<RunBlockerDto> Blockers,
    IReadOnlyList<ExcludedEmployeeDto> Excluded,
    IReadOnlyList<WarningDto> Warnings);

/// <summary>Resultado de aprobar: el comprobante NM y el total.</summary>
public sealed record SettlementApprovedDto(
    Guid RunPublicId,
    Guid DocumentPublicId,
    string Number,
    decimal Total,
    DateOnly PostingDate,
    bool ApprovedWithoutSegregation);

/// <summary>Resultado de reversar: el comprobante espejo.</summary>
public sealed record SettlementReversedDto(Guid RunPublicId, Guid ReversalDocumentPublicId, string ReversalNumber);

/// <summary>Resultado de descartar el borrador.</summary>
public sealed record SettlementDiscardedDto(Guid RunPublicId, string Reason);

/// <summary>Cuadre de la provisión de un rubro en una liquidación (contracts/api.md §2 <c>balance-check</c>): lo acumulado, lo que la liquidación consumió, lo liberado y la diferencia al gasto.</summary>
public sealed record ProvisionCheckDto(string ProvisionCode, decimal Accrued, decimal Consumed, decimal Released, decimal Difference);
