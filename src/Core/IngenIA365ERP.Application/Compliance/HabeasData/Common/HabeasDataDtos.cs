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
