namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US2 (T079; contracts/api.md §4 y §10 de la 014): lo que las pantallas de cuentas mandan y reciben por /api/lending/accounts y
// /api/lending/gmf-exemptions. Los pedidos son clases editables con los nombres del contrato; los enums viajan y llegan como número (los textos están
// en TextosDeCaptaciones). (nuevo)

// --------------------------------------------------------------------------------------------------------------- pedidos --

/// <summary>Una parte de la cuenta: cotitular (2), firmante (3) o representante legal (4).</summary>
public sealed class ParteDeCuentaRequest
{
    public Guid PersonPublicId { get; set; }
    public int Role { get; set; } = TextosDeCaptaciones.Cotitular;
    public decimal? Share { get; set; }
    public decimal? SignatureLimit { get; set; }
    public string? RepresentationBasis { get; set; }
    public Guid? SupportAttachmentPublicId { get; set; }
    public Guid? SignatureSpecimenAttachmentPublicId { get; set; }
    public Guid? PhotoAttachmentPublicId { get; set; }

    /// <summary>Sólo para la pantalla (no viaja con nombre propio en el contrato).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Un beneficiario con su porcentaje (0–100).</summary>
public sealed class BeneficiarioDeCuentaRequest
{
    public Guid PersonPublicId { get; set; }
    public decimal Percent { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string Nombre { get; set; } = string.Empty;
}

public sealed record OrigenDeFondosRequest(Guid? DeclarationAttachmentPublicId);

/// <summary>El cuerpo de <c>POST /api/lending/accounts</c>.</summary>
public sealed class AbrirCuentaRequest
{
    public Guid LinePublicId { get; set; }
    public Guid HolderPersonPublicId { get; set; }
    public Guid OfficePublicId { get; set; }
    public int HandlingMode { get; set; } = TextosDeCaptaciones.Indistinto;
    public List<ParteDeCuentaRequest> Parties { get; set; } = [];
    public List<BeneficiarioDeCuentaRequest> Beneficiaries { get; set; } = [];
    public Guid? BeneficiaryRecordAttachmentPublicId { get; set; }
    public bool MarkGmfExempt { get; set; }
    public OrigenDeFondosRequest? SourceOfFunds { get; set; }

    /// <summary>US7 (T185): el contrato del ahorro contractual (sólo en una línea contractual).</summary>
    public ContratoDeAhorroRequest? Contractual { get; set; }
}

/// <summary>El cuerpo de <c>PUT /api/lending/accounts/{id}/parties</c>: el conjunto completo.</summary>
public sealed class CambiarPartesRequest
{
    public int HandlingMode { get; set; } = TextosDeCaptaciones.Indistinto;
    public List<ParteDeCuentaRequest> Parties { get; set; } = [];
    public List<BeneficiarioDeCuentaRequest> Beneficiaries { get; set; } = [];
    public Guid? EvidenceAttachmentPublicId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>El cuerpo de <c>POST /api/lending/gmf-exemptions</c>.</summary>
public sealed record MarcarExencionRequest(Guid AccountPublicId, DateOnly? ValidFrom, string Reason, bool ReplaceCurrent = false);

/// <summary>El cuerpo de <c>POST /api/lending/gmf-exemptions/{id}/end</c>.</summary>
public sealed record TerminarExencionRequest(DateOnly? ValidTo, string Reason);

// ----------------------------------------------------------------------------------------------------------- respuestas --

public sealed record PersonaDeCuentaDto(Guid PersonPublicId, string FullName, string DocumentNumber);

public sealed record OficinaDeCuentaDto(Guid PublicId, string Name);

public sealed record LineaDeCuentaDto(Guid PublicId, string Code, string Name);

public sealed record ParteDeCuentaDto(
    Guid PublicId,
    PersonaDeCuentaDto Person,
    int Role,
    decimal? Share,
    decimal? SignatureLimit,
    string? RepresentationBasis,
    Guid? SupportAttachmentPublicId,
    Guid? SignatureSpecimenAttachmentPublicId,
    Guid? PhotoAttachmentPublicId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? EndReason);

public sealed record BeneficiarioDeCuentaDto(Guid PublicId, PersonaDeCuentaDto Person, decimal Percent, int SetNumber, DateOnly ValidFrom,
    Guid? RecordAttachmentPublicId);

public sealed record MarcaDeGmfDto(Guid PublicId, DateOnly Since);

public sealed record MarcasDeCuentaDto(bool RequiresDataUpdate, bool Inactive, bool Garnished, bool DeceasedHolder, bool OverdrawnTechnical);

public sealed record SaldosDeCuentaDto(IReadOnlyDictionary<string, decimal> ByConcept, decimal Balance);

public sealed record AvisoDeCuentaDto(string Code, string Message, Guid? ScreeningResultPublicId);

public sealed record CuentaDto(
    Guid PublicId,
    string Number,
    int Nature,
    int? DepositClass,
    int Status,
    LineaDeCuentaDto Line,
    OficinaDeCuentaDto? Office,
    PersonaDeCuentaDto Holder,
    int HandlingMode,
    IReadOnlyList<ParteDeCuentaDto> Parties,
    IReadOnlyList<BeneficiarioDeCuentaDto> Beneficiaries,
    MarcaDeGmfDto? GmfExempt,
    SaldosDeCuentaDto Balances,
    DateOnly OpenedOn,
    DateOnly? ActivatedOn,
    DateOnly? LastAccruedDate,
    IReadOnlyList<string> OpeningBlockers,
    MarcasDeCuentaDto Marks,
    IReadOnlyList<AvisoDeCuentaDto> Warnings,
    ResumenDeContratoDto? Contractual = null);

public sealed record CuentaEnListaDto(
    Guid PublicId,
    string Number,
    int Nature,
    int? DepositClass,
    LineaDeCuentaDto Line,
    OficinaDeCuentaDto? Office,
    PersonaDeCuentaDto Holder,
    int Status,
    decimal Balance,
    decimal Available,
    IReadOnlyList<string> Marks);

public sealed record PartesDeCuentaDto(
    DateOnly Date,
    int HandlingMode,
    IReadOnlyList<ParteDeCuentaDto> Parties,
    IReadOnlyList<BeneficiarioDeCuentaDto> Beneficiaries,
    bool RequiresDataUpdate,
    IReadOnlyList<ParteDeCuentaDto> History);

public sealed record ExencionDeGmfDto(
    Guid PublicId,
    PersonaDeCuentaDto Person,
    Guid AccountPublicId,
    string AccountNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? MarkedBy,
    string Reason,
    string? EndReason);
