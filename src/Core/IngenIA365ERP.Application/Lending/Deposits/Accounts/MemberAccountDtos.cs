using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

// Feature 014, US2 (T073–T075; contracts/api.md §4 y §10): los cuerpos y las respuestas de las cuentas de captación y de aportes, sus partes y la
// marca de exención del GMF. Los enums entran por nombre o número y salen como número (EnumPorNombreONumero). (nuevo)

/// <summary>Una parte que se pide en la apertura o al cambiar las partes (cotitular, firmante o representante legal). (nuevo)</summary>
public sealed record MemberAccountPartyInput
{
    public Guid PersonPublicId { get; init; }

    public MemberAccountPartyRole Role { get; init; }

    /// <summary>Cotitulares: fracción del saldo (0,5 = 50 %). Sin proporción en ninguno, se reparte por igual con el titular.</summary>
    public decimal? Share { get; init; }

    /// <summary>Firmantes: límite por operación en pesos; nulo = sin límite.</summary>
    public decimal? SignatureLimit { get; init; }

    /// <summary>Representante legal: <c>Parent</c>, <c>Guardian</c>, <c>CourtOrder</c> o <c>LegalEntityRepresentative</c>.</summary>
    public string? RepresentationBasis { get; init; }

    /// <summary>Representante legal: el soporte de la representación (registro civil, decisión judicial, certificado de existencia).</summary>
    public Guid? SupportAttachmentPublicId { get; init; }

    public Guid? SignatureSpecimenAttachmentPublicId { get; init; }

    public Guid? PhotoAttachmentPublicId { get; init; }
}

/// <summary>Un beneficiario con su porcentaje (0–100); todos suman 100 % (FR-034). (nuevo)</summary>
public sealed record MemberAccountBeneficiaryInput(Guid PersonPublicId, decimal Percent);

/// <summary>La declaración de origen de fondos (FR-032, FR-059). (nuevo)</summary>
public sealed record SourceOfFundsInput(Guid? DeclarationAttachmentPublicId);

/// <summary>El cuerpo de <c>POST /api/lending/accounts</c> (api.md §4, <c>OpenAccountInput</c>). (nuevo)</summary>
public sealed record OpenAccountInput
{
    public Guid LinePublicId { get; init; }

    public Guid HolderPersonPublicId { get; init; }

    public Guid OfficePublicId { get; init; }

    public HandlingMode HandlingMode { get; init; } = HandlingMode.Indistinct;

    public IReadOnlyList<MemberAccountPartyInput> Parties { get; init; } = [];

    public IReadOnlyList<MemberAccountBeneficiaryInput> Beneficiaries { get; init; } = [];

    /// <summary>La constancia firmada por el titular de sus beneficiarios (FR-034).</summary>
    public Guid? BeneficiaryRecordAttachmentPublicId { get; init; }

    /// <summary>Marcar la cuenta exenta del GMF desde la apertura (sólo ahorro, con la política de una cuenta marcada).</summary>
    public bool MarkGmfExempt { get; init; }

    public SourceOfFundsInput? SourceOfFunds { get; init; }

    /// <summary>Hoy, si viene: una cuenta no se abre con otra fecha.</summary>
    public DateOnly? OpeningDate { get; init; }
}

/// <summary>Una persona en una cuenta: su <c>PublicId</c>, su nombre y su documento. (nuevo)</summary>
public sealed record MemberAccountPersonDto(Guid PersonPublicId, string FullName, string DocumentNumber);

/// <summary>La oficina de la cuenta. (nuevo)</summary>
public sealed record MemberAccountOfficeDto(Guid PublicId, string Name);

/// <summary>Una parte de la cuenta con su vigencia (api.md §4, <c>GET /{id}/parties</c>). (nuevo)</summary>
public sealed record MemberAccountPartyDto(
    Guid PublicId,
    MemberAccountPersonDto Person,
    MemberAccountPartyRole Role,
    decimal? Share,
    decimal? SignatureLimit,
    string? RepresentationBasis,
    Guid? SupportAttachmentPublicId,
    Guid? SignatureSpecimenAttachmentPublicId,
    Guid? PhotoAttachmentPublicId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? EndReason);

/// <summary>Un beneficiario vigente: su porcentaje (0–100) y el conjunto al que pertenece. (nuevo)</summary>
public sealed record MemberAccountBeneficiaryDto(Guid PublicId, MemberAccountPersonDto Person, decimal Percent, int SetNumber, DateOnly ValidFrom,
    Guid? RecordAttachmentPublicId);

/// <summary>La marca de exención del GMF vigente de la cuenta. (nuevo)</summary>
public sealed record GmfExemptMarkDto(Guid PublicId, DateOnly Since);

/// <summary>Las marcas de pantalla de la cuenta (api.md §4, <c>marks</c>). (nuevo)</summary>
public sealed record MemberAccountMarksDto(bool RequiresDataUpdate, bool Inactive, bool Garnished, bool DeceasedHolder, bool OverdrawnTechnical);

/// <summary>El saldo de la cuenta por concepto (Σ del libro) y el del titular (data-model §0.2). (nuevo)</summary>
public sealed record MemberAccountBalancesDto(IReadOnlyDictionary<string, decimal> ByConcept, decimal Balance);

/// <summary>Un aviso de una respuesta exitosa (p. ej. <c>Lending.Screening.Blocked</c> con el resultado que el oficial debe liberar). (nuevo)</summary>
public sealed record MemberAccountWarningDto(string Code, string Message, Guid? ScreeningResultPublicId = null);

/// <summary>
/// Una cuenta con todo lo suyo (api.md §4, <c>MemberAccountDto</c>). <see cref="OpeningBlockers"/> dice por qué sigue en apertura: <c>ScreeningMatch</c>
/// (coincidencia en listas sin liberar) o <c>InitialDeposit</c> (el depósito mínimo de apertura que llega por el contrato). (nuevo)
/// </summary>
public sealed record MemberAccountDto(
    Guid PublicId,
    string Number,
    LineNature Nature,
    DepositClass? DepositClass,
    MemberAccountStatus Status,
    LendingLineRefDto Line,
    MemberAccountOfficeDto? Office,
    MemberAccountPersonDto Holder,
    HandlingMode HandlingMode,
    IReadOnlyList<MemberAccountPartyDto> Parties,
    IReadOnlyList<MemberAccountBeneficiaryDto> Beneficiaries,
    GmfExemptMarkDto? GmfExempt,
    MemberAccountBalancesDto Balances,
    DateOnly OpenedOn,
    DateOnly? ActivatedOn,
    DateOnly? LastAccruedDate,
    IReadOnlyList<string> OpeningBlockers,
    MemberAccountMarksDto Marks,
    IReadOnlyList<MemberAccountWarningDto> Warnings);

/// <summary>Una cuenta en la lista (api.md §4, <c>MemberAccountListItemDto</c>). (nuevo)</summary>
public sealed record MemberAccountListItemDto(
    Guid PublicId,
    string Number,
    LineNature Nature,
    DepositClass? DepositClass,
    LendingLineRefDto Line,
    MemberAccountOfficeDto? Office,
    MemberAccountPersonDto Holder,
    MemberAccountStatus Status,
    decimal Balance,
    decimal Available,
    IReadOnlyList<string> Marks);

/// <summary>Las partes de la cuenta a una fecha (api.md §4, <c>GET /{id}/parties</c>). (nuevo)</summary>
public sealed record MemberAccountPartiesDto(
    DateOnly Date,
    HandlingMode HandlingMode,
    IReadOnlyList<MemberAccountPartyDto> Parties,
    IReadOnlyList<MemberAccountBeneficiaryDto> Beneficiaries,
    bool RequiresDataUpdate,
    IReadOnlyList<MemberAccountPartyDto> History);

/// <summary>Los conteos de cuentas por estado y por línea, sin titulares (api.md §4, <c>GET /counts</c>). (nuevo)</summary>
public sealed record MemberAccountCountsDto(IReadOnlyList<MemberAccountCountByStatusDto> ByStatus, IReadOnlyList<MemberAccountCountByLineDto> ByLine);

public sealed record MemberAccountCountByStatusDto(MemberAccountStatus Status, int Accounts, decimal Balance);

public sealed record MemberAccountCountByLineDto(LendingLineRefDto Line, int Accounts, decimal Balance);

/// <summary>Una marca de exención del GMF (api.md §10). (nuevo)</summary>
public sealed record GmfExemptAccountDto(
    Guid PublicId,
    MemberAccountPersonDto Person,
    Guid AccountPublicId,
    string AccountNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? MarkedBy,
    string Reason,
    string? EndReason);
