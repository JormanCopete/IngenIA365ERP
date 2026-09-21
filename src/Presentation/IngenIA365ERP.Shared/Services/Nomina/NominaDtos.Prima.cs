namespace IngenIA365ERP.Shared.Services.Nomina;

// Feature 010, US1 — prima de servicios (contracts/api.md §3.1). Espejo de lo que responde
// /api/payroll/settlements/service-bonus; el detalle por empleado, la relación de pago y los
// comprobantes son los DTOs de la 005 sobre /api/payroll/runs/{runId}, y calcular, aprobar,
// reversar y descartar usan el juego común de NominaDtos.Liquidaciones.cs.

/// <summary>Una fila de la lista de primas: cada versión con su estado.</summary>
public sealed record PrimaResumenDto(
    Guid RunPublicId,
    int Year,
    int Semester,
    int Version,
    string Status,
    DateOnly CutoffDate,
    DateOnly? PayDate,
    int Employees,
    decimal Total,
    DateTime CalculatedAt,
    string CalculatedBy,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? ReversedAt,
    DateTime? DiscardedAt,
    int PaidCount,
    Guid? PostedDocumentPublicId,
    string? PostedDocumentNumber)
{
    public string Etiqueta => $"{Year}-{(Semester == 1 ? "I" : "II")}";

    public string SemestreTexto => Semester == 1 ? "Primer semestre (enero–junio)" : "Segundo semestre (julio–diciembre)";

    public string EstadoTexto => Status switch
    {
        "Draft" => "Borrador",
        "Stale" => "Desactualizado",
        "Superseded" => DiscardedAt is null ? "Reemplazado" : "Descartado",
        "Approved" => "Aprobado",
        "Reversed" => "Reversado",
        _ => Status,
    };

    public bool EsBorrador => Status is "Draft" or "Stale";
}

public sealed record CalcularPrimaRequest(int Year, int Semester, IReadOnlyList<Guid>? EmployeePublicIds = null);
