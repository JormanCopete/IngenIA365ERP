namespace IngenIA365ERP.Shared.Services.Nomina;

// Feature 010 US2: cesantías e intereses del año y la consignación por fondo (contracts/api.md §3.2).
// Espejo de Application/Payroll/Settlements/Severance/SeveranceDtos.cs; lo común del §3.1 (calcular,
// aprobar, reversar, descartar, excluidos y avisos) está en NominaDtos.Liquidaciones.cs.

public sealed record FondoEnLiquidacionDto(Guid? FundPublicId, string Name, int Employees, decimal Amount, DateOnly? DepositedAt, string? DepositedBy, string? Reference)
{
    public bool Consignado => DepositedAt is not null;
}

public sealed record LiquidacionCesantiasDto(
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
    IReadOnlyList<FondoEnLiquidacionDto> Funds)
{
    public string EstadoTexto => Status switch
    {
        "Draft" => "Borrador",
        "Stale" => "Desactualizado",
        "Superseded" => "Reemplazado",
        "Approved" => "Aprobado",
        "Reversed" => "Reversado",
        _ => Status,
    };

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
    public int FondosConsignados => Funds.Count(f => f.Consignado && f.FundPublicId is not null);
    public int FondosPorConsignar => Funds.Count(f => !f.Consignado && f.FundPublicId is not null);
}

public sealed record CalcularCesantiasRequest(int Year, DateOnly? CutoffDate = null, IReadOnlyList<Guid>? EmployeePublicIds = null);

public sealed record MarcarConsignadoRequest(DateOnly DepositedAt, string? Reference);

public sealed record LineaConsignacionDto(
    Guid EmployeePublicId, string DocumentType, string Document, string Name, DateOnly HireDate,
    decimal BaseSalary, decimal Days, decimal Amount, decimal Interest);

public sealed record FondoConsignacionDto(
    Guid? FundPublicId, string? FundCode, string FundName, string? FundNit, string? PilaCode,
    IReadOnlyList<LineaConsignacionDto> Lines, int Employees, decimal Total, decimal InterestTotal,
    DateOnly? DepositedAt, string? DepositedBy, string? Reference, decimal? DepositedAmount)
{
    public bool Consignado => DepositedAt is not null;
    public bool SinFondo => FundPublicId is null;
}

public sealed record RelacionDeConsignacionDto(
    Guid RunPublicId, int Year, int Version, string Status, DateOnly CutoffDate,
    IReadOnlyList<FondoConsignacionDto> Funds, int Employees, decimal GrandTotal, decimal InterestGrandTotal,
    DateOnly? DueDate, DateOnly? InterestDueDate)
{
    public bool TieneFondosSinConsignar => Funds.Any(f => !f.SinFondo && !f.Consignado);
}

public sealed record ConsignacionRegistradaDto(Guid RunPublicId, Guid FundPublicId, string FundName, DateOnly DepositedAt, string DepositedBy, string? Reference, decimal Amount);
