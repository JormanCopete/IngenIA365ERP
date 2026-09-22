namespace IngenIA365ERP.Shared.Services.Nomina;

// ============================================================================
// Feature 010 (N2, US5): planilla PILA (contracts/api.md §7). Los enums viajan como número (EnumPorNombreONumero).
// ============================================================================

public sealed record DatosDelAportanteDto(
    string ContributorType, string ContributorClass, string PresentationForm, string? BranchCode, string? BranchName,
    string? ArlPilaCode, string? EconomicActivityCode, string? DivipolaDepartment, string? DivipolaMunicipality,
    string? OperatorCode, string? OperatorName, string PlanillaType, string? NitLastTwoDigits, bool Complete, IReadOnlyList<string> Missing);

public sealed class DatosDelAportanteRequest
{
    public string ContributorType { get; set; } = "1";
    public string ContributorClass { get; set; } = "B";
    public string PresentationForm { get; set; } = "U";
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string? ArlPilaCode { get; set; }
    public string? EconomicActivityCode { get; set; }
    public string? DivipolaDepartment { get; set; }
    public string? DivipolaMunicipality { get; set; }
    public string? OperatorCode { get; set; }
    public string? OperatorName { get; set; }

    public static DatosDelAportanteRequest Desde(DatosDelAportanteDto d) => new()
    {
        ContributorType = d.ContributorType, ContributorClass = d.ContributorClass, PresentationForm = d.PresentationForm, BranchCode = d.BranchCode, BranchName = d.BranchName,
        ArlPilaCode = d.ArlPilaCode, EconomicActivityCode = d.EconomicActivityCode, DivipolaDepartment = d.DivipolaDepartment, DivipolaMunicipality = d.DivipolaMunicipality,
        OperatorCode = d.OperatorCode, OperatorName = d.OperatorName,
    };
}

public sealed record LayoutPilaDto(string Code, string Version, DateOnly ValidFrom, DateOnly? ValidTo, int Type1Fields, int Type1Length, int Type2Fields, int Type2Length, string Source, bool Verified, int UnverifiedFields);

public sealed record FechaLimitePilaDto(DateOnly? DueDate, string Rule, string NitDigits, string? Note);

/// <summary>Severidad: 1 bloqueante, 2 alerta (<c>PilaIssueSeverity</c>).</summary>
public sealed record InconsistenciaPilaDto(int Severity, string Code, byte? Field, string Message, Guid? EmployeePublicId, string? EmployeeName, string? Link)
{
    public bool Bloqueante => Severity == 1;
    public string SeveridadTexto => Bloqueante ? "Bloqueante" : "Alerta";
}

public sealed record FuentePilaDto(Guid RunPublicId, string Kind, string Status, int Version, string Label);

public sealed record ValidacionPilaDto(bool CanGenerate, int Blocking, int Warnings, IReadOnlyList<InconsistenciaPilaDto> Issues, IReadOnlyList<FuentePilaDto> Sources, int Contributors, int Lines);

public sealed record TotalesPilaDto(decimal Pension, decimal Health, decimal Arl, decimal Ccf, decimal Sena, decimal Icbf, decimal Fsp, decimal Total);

public sealed record CuadrePilaFilaDto(string Subsystem, decimal FileTotal, decimal LedgerTotal, decimal Difference)
{
    public string SubsistemaTexto => Subsystem switch
    {
        "Pension" => "Pensión", "Health" => "Salud", "Fsp" => "Fondo de solidaridad", "Arl" => "Riesgos laborales", "Ccf" => "Caja de compensación", "Sena" => "SENA", "Icbf" => "ICBF", _ => Subsystem,
    };
}

public sealed record CuadrePilaDto(IReadOnlyList<CuadrePilaFilaDto> BySubsystem, bool Balanced, string? Note);

public sealed record PilaGeneradaDto(Guid GenerationPublicId, int Version, int Status, string? FileName, int Contributors, int Lines, TotalesPilaDto Totals, CuadrePilaDto Reconciliation, IReadOnlyList<InconsistenciaPilaDto> Issues, DateOnly? DueDate);

/// <summary>Estado: 0 validada (con bloqueantes), 1 generada, 2 cargada, 3 reemplazada.</summary>
public sealed record GeneracionPilaDto(
    Guid GenerationPublicId, string Period, short Year, byte Month, int Version, int Status, string LayoutCode,
    DateTime GeneratedAt, string GeneratedBy, TotalesPilaDto Totals, bool Balanced, int Contributors, int Lines, int Blocking, int Warnings,
    string? FileName, DateOnly? DueDate, DateTime? UploadedAt, string? UploadedBy, string? OperatorReference, DateOnly? OperatorFilingDate, DateOnly? PaidAt)
{
    public string EstadoTexto => Status switch { 0 => "Con inconsistencias", 1 => "Generada", 2 => "Cargada", 3 => "Reemplazada", _ => Status.ToString() };
    public bool Generada => Status == 1;
    public bool Cargada => Status == 2;
}

public sealed record LineaPilaDto(
    int LineNumber, Guid EmployeePublicId, string EmployeeName, string Document, string ContributorType, string SubType, IReadOnlyList<string> Novelties,
    int DaysPension, int DaysHealth, int DaysArl, int DaysCcf, decimal Salary,
    decimal IbcPension, decimal IbcHealth, decimal IbcArl, decimal IbcCcf,
    decimal Pension, decimal Fsp, decimal Health, decimal Arl, decimal Ccf, decimal Sena, decimal Icbf, decimal Total, bool Exempt,
    IReadOnlyDictionary<string, string> Fields)
{
    public string NovedadesTexto => string.Join(" ", Novelties);
}

public sealed record DetalleDePilaDto(GeneracionPilaDto Summary, IReadOnlyList<LineaPilaDto> Lines, IReadOnlyList<InconsistenciaPilaDto> Issues, CuadrePilaDto Reconciliation, IReadOnlyList<FuentePilaDto> Sources, bool ExemptionApplied);

public sealed record ExplicacionDeCampoPilaDto(int Field, string Name, string Value, string Source, string? Detail, decimal? Amount);

public sealed record ExplicacionDeLineaPilaDto(int LineNumber, Guid EmployeePublicId, string EmployeeName, string RecordText, IReadOnlyList<ExplicacionDeCampoPilaDto> Fields);

public sealed record MarcarCargadaRequest(DateTime UploadedAt, string OperatorReference, DateOnly? OperatorFilingDate, DateOnly? PaidAt);

public sealed record PilaCargadaDto(Guid GenerationPublicId, int Status, DateTime UploadedAt, string OperatorReference);
