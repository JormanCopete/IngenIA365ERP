using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

// Los DTO de tipos de documento (feature 012, T150; contracts/api.md §8). Sólo PublicId hacia afuera.

/// <summary>Una clase fija del sistema (<c>GET /document-types/classes</c>). <see cref="Operable"/> = disponible en este despliegue.</summary>
public sealed record DocumentClassDto(
    DocumentClass Class,
    DocumentClassGroup? Group,
    string EffectDescription,
    bool IsFiscal,
    FiscalDirection? FiscalDirection,
    IReadOnlyList<string> Messages,
    PostingChain Chain,
    NumberedBy NumberedBy,
    string AvailableFrom,
    bool Operable);

/// <summary>Los campos que el tipo exige (<c>requiredFields</c>). (nuevo)</summary>
public sealed record CamposObligatoriosDto(bool Counterparty, bool CostCenter, bool Reason, bool ExternalReference);

/// <summary>Un consecutivo del tipo (vigente o del historial). (nuevo)</summary>
public sealed record DocumentSequenceDto(Guid PublicId, string Prefix, long NextValue, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Un nivel de la política de aprobación vigente del tipo. (nuevo)</summary>
public sealed record NivelDePoliticaDelTipoDto(int Order, decimal Threshold, string PermissionCode);

/// <summary>La política de aprobación vigente hoy del tipo, de sólo lectura (se administra en §15.1). (nuevo)</summary>
public sealed record PoliticaDelTipoDto(Guid PublicId, int Version, IReadOnlyList<NivelDePoliticaDelTipoDto> Levels);

/// <summary>
/// El modo de paso vigente hoy del tipo (<c>posting</c>), de sólo lectura (se administra en §7). <see cref="ModeInherited"/>
/// = no hay excepción del tipo: rige el general. (nuevo)
/// </summary>
public sealed record ModoDePasoDelTipoDto(PostingMode Mode, bool ModeInherited, PostingChain Chain, string Granularity, string BatchTrigger, string? BatchTime);

/// <summary><c>DocumentTypeDto</c> (§8); <see cref="Sequences"/> sólo en el detalle (historial).</summary>
public sealed record DocumentTypeDto(
    Guid PublicId,
    string Code,
    string Name,
    DocumentClass Class,
    DocumentClassGroup? Group,
    bool IsFiscal,
    NumberedBy NumberedBy,
    string Prefix,
    CamposObligatoriosDto RequiredFields,
    IReadOnlyList<ReferenciaDto> Warehouses,
    ReferenciaDto? SalesChannel,
    bool IsTaxableWithdrawal,
    bool VatNonDeductible,
    bool AllowsFutureDate,
    DocumentSequenceDto? CurrentSequence,
    PoliticaDelTipoDto? ApprovalPolicy,
    ModoDePasoDelTipoDto? Posting,
    bool IsSeeded,
    bool IsActive,
    IReadOnlyList<DocumentSequenceDto>? Sequences = null);
