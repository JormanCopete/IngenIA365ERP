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
    bool IsCurrent);

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
    string? Notes);

public static class HabeasDataErrorCodes
{
    public const string NoCurrentPolicy = "Compliance.HabeasData.NoCurrentPolicy";
    public const string AlreadyRevoked = "Compliance.HabeasData.AlreadyRevoked";
    public const string NoActiveConsent = "Compliance.HabeasData.NoActiveConsent";
    public const string Validation_EffectiveFromInPast = "Validation.HabeasData.EffectiveFromInPast";
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
