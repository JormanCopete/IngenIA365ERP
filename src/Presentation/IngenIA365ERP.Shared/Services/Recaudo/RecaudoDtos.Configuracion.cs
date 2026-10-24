namespace IngenIA365ERP.Shared.Services.Recaudo;

// Feature 015, US1 y US12 (T0063, T0077; contracts/api.md §2): lo que la configuración y el alcance de Recaudo devuelven y reciben. Espejo de los DTO de
// Application/Receipts/Setup y Application/Receipts/Security; los enums viajan como número. (nuevo)

public sealed record PuntoDeRecaudoDto(Guid PublicId, Guid BranchPublicId, string BranchName, string Code, string Name, int Status, DateOnly ValidFrom,
    DateOnly? ValidTo, string? DocumentPrefix, bool IsMainCashBoxVault, int CashBoxCount);

public sealed record CajaDeRecaudoDto(Guid PublicId, Guid CollectionPointPublicId, string CollectionPointCode, string Code, string Name, int BaseMode,
    decimal BaseAmount, decimal? CashWarnCap, decimal? CashBlockCap, decimal? DailyCashOutCap, bool IsMainCashBox, int Status, DateOnly ValidFrom,
    DateOnly? ValidTo, int ActiveCashiers);

public sealed record CajeroDto(Guid UserPublicId, string UserName, Guid PersonPublicId, string PersonName, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record MedioDelPuntoDto(Guid PaymentMeansPublicId, string Code, string Name, int Class, int CountMethod, decimal ToleranceAmount,
    bool AllowsChange, bool RequiresReference, DateOnly ValidFrom, DateOnly? ValidTo, bool IsActive);

public sealed record TipoDeDocumentoDeRecaudoDto(Guid PublicId, string Code, string Name, int Kind, bool UsesPointPrefix, int Status);

public sealed record ConceptoVarioDto(Guid PublicId, string Code, string Name, int Direction, bool RequiresThirdParty, bool RequiresCrossDocument,
    bool RequiresInvoice, bool RequiresApproval, decimal? MaxAmount, bool IsSystem, int Status);

public sealed record TalonarioDto(Guid PublicId, Guid CollectionPointPublicId, string CollectionPointCode, int FromNumber, int ToNumber,
    string RegisteredBy, int Status, DateTime CreatedAt);

public sealed record SuplenciaDto(Guid PublicId, Guid CollectionPointPublicId, string CollectionPointCode, Guid SupervisorUserPublicId,
    string SupervisorName, Guid DelegateUserPublicId, string DelegateName, DateOnly ValidFrom, DateOnly? ValidTo, string Reason);

public sealed record ReglaDeModalidadDto(Guid PublicId, int Channel, int Target, int? LoanModality, int? AccountModality, bool OnlyWhenInstallmentsDue,
    int Priority, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record CustodioDto(Guid UserPublicId, string UserName, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record BovedaDto(Guid PublicId, Guid BranchPublicId, string BranchName, int Mode, Guid? MainCashBoxPublicId, string? MainCashBoxCode,
    int Status, IReadOnlyList<CustodioDto> Custodians);

// ------------------------------------------------------------------------------------------------------------- cuerpos --

public sealed record CrearPuntoRequest(Guid BranchPublicId, string Code, string Name, DateOnly? ValidFrom, string? DocumentPrefix, bool IsMainCashBoxVault);

public sealed record EditarPuntoRequest(string Name, string? DocumentPrefix, bool IsMainCashBoxVault, DateOnly? ValidTo);

public sealed record CrearCajaRequest(Guid CollectionPointPublicId, string Code, string Name, int BaseMode, decimal BaseAmount, decimal? CashWarnCap,
    decimal? CashBlockCap, decimal? DailyCashOutCap, bool IsMainCashBox, DateOnly? ValidFrom);

public sealed record EditarCajaRequest(string Name, int BaseMode, decimal BaseAmount, decimal? CashWarnCap, decimal? CashBlockCap, decimal? DailyCashOutCap,
    bool IsMainCashBox, DateOnly? ValidTo);

public sealed record AsignarCajeroRequest(Guid UserPublicId, DateOnly? ValidFrom, DateOnly? ValidTo);

public sealed record RetirarCajeroRequest(DateOnly? ValidTo);

public sealed record OfrecerMedioRequest(Guid PaymentMeansPublicId, DateOnly? ValidFrom);

public sealed record CrearTipoDeDocumentoRequest(string Code, string Name, int Kind, bool UsesPointPrefix);

public sealed record EditarTipoDeDocumentoRequest(string Name, bool UsesPointPrefix, int Status);

public sealed record CrearConceptoRequest(string Code, string Name, int Direction, bool RequiresThirdParty, bool RequiresCrossDocument, bool RequiresInvoice,
    bool RequiresApproval, decimal? MaxAmount);

public sealed record EditarConceptoRequest(string Name, bool RequiresThirdParty, bool RequiresCrossDocument, bool RequiresInvoice, bool RequiresApproval,
    decimal? MaxAmount);

public sealed record RegistrarTalonarioRequest(Guid CollectionPointPublicId, int FromNumber, int ToNumber);

public sealed record MotivoDeRecaudoRequest(string Reason);

public sealed record RegistrarSuplenciaRequest(Guid CollectionPointPublicId, Guid SupervisorUserPublicId, Guid DelegateUserPublicId, DateOnly ValidFrom,
    DateOnly? ValidTo, string Reason);

public sealed record ReglaDeModalidadInput(int Channel, int Target, int? LoanModality, int? AccountModality, bool OnlyWhenInstallmentsDue, int Priority);

public sealed record FijarReglasDeModalidadRequest(IReadOnlyList<ReglaDeModalidadInput> Rules);

public sealed record CrearBovedaRequest(Guid BranchPublicId, int Mode, Guid? MainCashBoxPublicId);

public sealed record EditarBovedaRequest(int Mode, Guid? MainCashBoxPublicId, int Status);

public sealed record FijarCustodiosRequest(IReadOnlyList<Guid> UserPublicIds);

// -------------------------------------------------------------------------------------------------------------- alcance --

public sealed record UsuarioDelAlcanceDeRecaudoDto(Guid UserPublicId, string Name);

public sealed record ElementoDelAlcanceDto(Guid PublicId, string Code, string Name);

public sealed record AlcanceDeRecaudoDto(UsuarioDelAlcanceDeRecaudoDto User, bool HasAll, IReadOnlyList<ElementoDelAlcanceDto> Offices,
    IReadOnlyList<ElementoDelAlcanceDto> CollectionPoints, IReadOnlyList<ElementoDelAlcanceDto> CashBoxes, IReadOnlyList<ElementoDelAlcanceDto> Paymasters);

public sealed record FijarAlcanceDeRecaudoRequest(IReadOnlyList<Guid> CollectionPointPublicIds, IReadOnlyList<Guid> CashBoxPublicIds,
    IReadOnlyList<Guid> PaymasterPersonPublicIds);

/// <summary>Una oficina de la cooperativa (<c>COR_Branches</c>), para los desplegables. (nuevo)</summary>
public sealed class OficinaDeRecaudoItem
{
    public Guid PublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}

