using IngenIA365ERP.Domain.Enums.Compliance;

namespace IngenIA365ERP.Application.Compliance.HabeasData.Common;

/// <summary>Proyección de una versión de política habeas data.</summary>
public sealed record HabeasDataPolicyDto(
    Guid PublicId,
    int VersionNumber,
    string Title,
    string Sha256Hex,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string PublishedBy,
    bool IsCurrent)
{
    /// <summary>Feature 013 (T0086): la finalidad de la versión.</summary>
    public DataAuthorizationPurpose Purpose { get; init; } = DataAuthorizationPurpose.GeneralTreatment;
}

/// <summary>Proyección detallada (incluye contenido).</summary>
public sealed record HabeasDataPolicyDetailDto(
    Guid PublicId,
    int VersionNumber,
    string Title,
    string ContentMarkdown,
    string Sha256Hex,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string PublishedBy);

/// <summary>Entrada del historial de un titular.</summary>
public sealed record HabeasDataHistoryItemDto(
    Guid PublicId,
    int PolicyVersionNumber,
    Guid PolicyVersionPublicId,
    string Action,
    DateTime ActionAt,
    string ActionBy,
    string? Channel,
    string? Notes)
{
    /// <summary>Feature 013 (T0086): la finalidad, el canal de contacto y el origen de la decisión.</summary>
    public DataAuthorizationPurpose Purpose { get; init; } = DataAuthorizationPurpose.GeneralTreatment;

    public DataAuthorizationChannel? ContactChannel { get; init; }

    public DataAuthorizationOrigin Origin { get; init; } = DataAuthorizationOrigin.PersonRegistration;
}

public static class HabeasDataErrorCodes
{
    public const string NoCurrentPolicy = "Compliance.HabeasData.NoCurrentPolicy";
    public const string AlreadyRevoked = "Compliance.HabeasData.AlreadyRevoked";
    public const string NoActiveConsent = "Compliance.HabeasData.NoActiveConsent";
    public const string Validation_EffectiveFromInPast = "Validation.HabeasData.EffectiveFromInPast";

    /// <summary>Feature 013 (T0086): el adjunto de evidencia no existe, no es de la persona o no es de tipo <c>DataAuthorization</c>.</summary>
    public const string EvidenceNotFound = "Compliance.HabeasData.EvidenceNotFound";

    /// <summary>Feature 013 (T0086): una autorización que no es migrada exige su evidencia.</summary>
    public const string EvidenceRequired = "Compliance.HabeasData.EvidenceRequired";
}

/// <summary>
/// Los valores de <c>HabeasDataConsent.Action</c> (feature 012, T46: se suma <see cref="Declined"/>, texto nuevo sin
/// migración). Los lectores tratan <see cref="Declined"/> como sin autorización, igual que <see cref="Revoked"/>. (nuevo)
/// </summary>
public static class AccionesDeConsentimiento
{
    public const string Accepted = "Accepted";
    public const string Revoked = "Revoked";

    /// <summary>El titular no autorizó el tratamiento al darse de alta (FR-011).</summary>
    public const string Declined = "Declined";
}

/// <summary>
/// La política vigente que el POS y Compras muestran al crear una persona
/// (<c>GET /api/compliance/habeas-data/policies/current</c>; feature 012, T46). (nuevo)
/// </summary>
public sealed record PoliticaVigenteDto(
    Guid PolicyVersionPublicId,
    int Version,
    string Title,
    string Text,
    DateTime PublishedAt);
