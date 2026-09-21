namespace IngenIA365ERP.Shared.Services.Nomina;

// Feature 010, US1 — prima de servicios (contracts/api.md §3.1). Espejo de lo que responde
// /api/payroll/settlements/service-bonus; el detalle por empleado, la relación de pago y los
// comprobantes son los DTOs de la 005 sobre /api/payroll/runs/{runId}.

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

/// <summary>Quien quedó fuera de la liquidación y por qué (<c>SettlementReasonCodes</c>).</summary>
public sealed record ExcluidoDePrimaDto(Guid EmployeePublicId, string Name, string ReasonCode, string Reason)
{
    public string RazonTexto => ReasonCode switch
    {
        "SalarioIntegral" => "Salario integral",
        "AprendizLectiva" => "Aprendiz en etapa lectiva",
        "Pasante" => "Pasante sin contrato de aprendizaje",
        "YaPagadaEnDefinitiva" => "Prima ya pagada en la definitiva",
        "SinDiasEnElSemestre" => "Sin días en el semestre",
        "RetiradoConDefinitiva" => "Retirado con definitiva aprobada",
        _ => ReasonCode,
    };
}

/// <summary>Lo que responde calcular o recalcular: la corrida nueva, sus totales, bloqueos, excluidos y avisos.</summary>
public sealed record ResultadoPrimaDto(
    Guid RunPublicId,
    int Version,
    string Kind,
    DateOnly CutoffDate,
    int Employees,
    TotalesCorridaDto Totals,
    IReadOnlyList<BloqueoDto> Blockers,
    IReadOnlyList<ExcluidoDePrimaDto> Excluded,
    IReadOnlyList<AvisoCorridaDto> Warnings);

/// <summary>Aprobar: confirmación explícita, fecha del comprobante (por defecto el corte, D-04) y las dos confirmaciones opcionales.</summary>
public sealed record AprobarPrimaRequest(bool Confirm, DateOnly? PostingDate = null, bool ConfirmEmpty = false, bool ConfirmWithoutSegregation = false);

public sealed record PrimaAprobadaDto(Guid RunPublicId, Guid DocumentPublicId, string Number, decimal Total, DateOnly PostingDate, bool ApprovedWithoutSegregation);

public sealed record PrimaReversadaDto(Guid RunPublicId, Guid ReversalDocumentPublicId, string ReversalNumber);

public sealed record PrimaDescartadaDto(Guid RunPublicId, string Reason);
