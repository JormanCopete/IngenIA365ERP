namespace IngenIA365ERP.Shared.Services.Nomina;

// Feature 010, US3: espejo de contracts/api.md §3.4 (terminación y liquidación definitiva) y §11.
// Los enums entran por nombre al enviar y se leen como int (EnumPorNombreONumero).

/// <summary>Un motivo de retiro del catálogo (<c>GET /reasons</c>).</summary>
public sealed record MotivoDeRetiroDto(
    Guid PublicId,
    string Code,
    string Name,
    bool GeneratesSeverancePay,
    bool RequiresContractEndDate,
    string? LegalBasis,
    bool IsSeeded,
    bool IsActive);

public sealed record GuardarMotivoDeRetiroRequest(string Code, string Name, bool GeneratesSeverancePay, bool RequiresContractEndDate, string? LegalBasis, bool IsActive = true);

/// <summary>Tipos de contrato DIAN (<c>DianContractType</c>): el valor es el de la tabla 5.5.x y viaja por nombre.</summary>
public static class TiposDeContratoDian
{
    public static readonly IReadOnlyList<(string Nombre, int Valor, string Texto)> Todos =
    [
        ("FixedTerm", 1, "Término fijo"),
        ("Indefinite", 2, "Término indefinido"),
        ("WorkOrLabor", 3, "Obra o labor"),
        ("Apprenticeship", 4, "Contrato de aprendizaje"),
        ("Internship", 5, "Prácticas o pasantía"),
    ];

    public static string Texto(int? valor) => Todos.FirstOrDefault(t => t.Valor == valor).Texto ?? "—";
    public static string? Nombre(int? valor) => Todos.FirstOrDefault(t => t.Valor == valor).Nombre;
    public static bool ExigeFechaDeFin(string? nombre) => nombre is "FixedTerm" or "WorkOrLabor";
}

/// <summary>Cuerpo de <c>POST /api/payroll/settlements/terminations</c>.</summary>
public sealed record RegistrarTerminacionRequest(
    Guid EmployeePublicId,
    DateOnly TerminationDate,
    string ReasonCode,
    string? ContractType,
    DateOnly? ContractEndDate,
    string? Notes);

/// <summary>Una fila de la lista de terminaciones. <c>Status</c> es <c>TerminationStatus</c> (0 registrada, 1 liquidada, 2 reintegrado, 3 anulada).</summary>
public sealed record TerminacionDto(
    Guid TerminationPublicId,
    Guid? RunPublicId,
    int? RunVersion,
    string? RunStatus,
    Guid EmployeePublicId,
    string EmployeeName,
    string Document,
    DateOnly TerminationDate,
    string ReasonCode,
    string ReasonName,
    bool GeneratesSeverancePay,
    int? ContractType,
    DateOnly? ContractEndDate,
    int Status,
    decimal Net,
    DateTime? ApprovedAt,
    string? Notes,
    Guid? SettlementDocumentAttachmentPublicId)
{
    public string EstadoTexto => Status switch { 0 => "Registrada", 1 => "Liquidada", 2 => "Reintegrado", 3 => "Anulada", _ => Status.ToString() };
    public string ContratoTexto => TiposDeContratoDian.Texto(ContractType);
    public bool EsBorrador => Status == 0 && RunStatus is ("Draft" or "Stale");
    public bool EstaLiquidada => Status == 1;
}

/// <summary>Una línea de la definitiva recién calculada (resumen; la explicación completa se lee de la corrida).</summary>
public sealed record LineaDefinitivaDto(string ConceptCode, string ConceptName, int Nature, decimal? Quantity, decimal? BaseAmount, decimal Amount, bool AffectsAccounting, string Summary);

/// <summary>Un descuento propuesto. <c>Kind</c>: 1 préstamo de la cooperativa, 2 libranza con tercero, 3 otro. <c>Status</c>: 0 propuesto, 1 ajustado, 2 aplicado, 3 reversado.</summary>
public sealed record DescuentoDefinitivaDto(
    Guid ObligationPublicId,
    int Kind,
    string Description,
    decimal? CapitalBalance,
    decimal? InterestBalance,
    decimal? DefaultBalance,
    int? PendingInstallments,
    int? CausedNotDeducted,
    decimal Proposed,
    decimal Applied,
    string? Reason,
    string? AdjustedBy,
    DateTime? AdjustedAt,
    decimal? RemainingAfter,
    int Status,
    Guid? CarteraTransactionPublicId)
{
    public string TipoTexto => Kind switch { 1 => "Crédito de la cooperativa", 2 => "Libranza con tercero", _ => "Otro" };
    public string EstadoTexto => Status switch { 0 => "Propuesto", 1 => "Ajustado", 2 => "Aplicado", 3 => "Reversado", _ => Status.ToString() };
    public bool FueAjustado => Applied < Proposed;
    public bool EsCredito => Kind == 1;
}

public sealed record DescuentosDefinitivaDto(
    Guid RunPublicId,
    decimal Net,
    IReadOnlyList<DescuentoDefinitivaDto> Items,
    decimal TotalProposed,
    decimal TotalApplied,
    decimal NetAfterDeductions,
    bool DeductionOverNet);

public sealed record AjustarDescuentoRequest(decimal Applied, string? Reason);

/// <summary>Lo que devuelve registrar o recalcular (contracts/api.md §3.4 <c>POST /</c>).</summary>
public sealed record TerminacionRegistradaDto(
    Guid TerminationPublicId,
    Guid RunPublicId,
    int Version,
    DateOnly TerminationDate,
    IReadOnlyList<LineaDefinitivaDto> Lines,
    DescuentosDefinitivaDto Deductions,
    IReadOnlyList<AvisoCorridaDto> Warnings,
    IReadOnlyList<string> Skips,
    IReadOnlyList<string> Refusals,
    TotalesCorridaDto Totals);

public sealed record AprobarDefinitivaRequest(bool Confirm, DateOnly? PostingDate = null, bool ConfirmWithoutSegregation = false);

public sealed record PagoCarteraDto(Guid ObligationPublicId, Guid? PaymentPublicId, decimal Applied, decimal? Remaining, string Description);

public sealed record DefinitivaAprobadaDto(
    Guid RunPublicId,
    Guid TerminationPublicId,
    Guid DocumentPublicId,
    string Number,
    decimal Net,
    DateOnly PostingDate,
    bool ApprovedWithoutSegregation,
    IReadOnlyList<PagoCarteraDto> PortfolioPayments,
    Guid? SettlementDocumentAttachmentPublicId);

public sealed record DefinitivaReversadaDto(
    Guid RunPublicId,
    Guid TerminationPublicId,
    Guid ReversalDocumentPublicId,
    string ReversalNumber,
    IReadOnlyList<PagoCarteraDto> PortfolioPayments,
    string Message);

public sealed record MotivoRequest(string Reason);

/// <summary>Sanción moratoria informativa (CST art. 65): nunca una línea.</summary>
public sealed record SancionMoratoriaDto(
    DateOnly TerminationDate,
    DateOnly AsOf,
    int DaysLate,
    int DaysCharged,
    decimal MonthlySalary,
    decimal DailyRate,
    decimal Penalty,
    int CapMonths,
    string Note);
