namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>Lo que devuelve <c>GET /api/admin/permissions/mine</c> (feature 008).</summary>
public sealed record PermisosMiosDto(bool IsGlobalMasterAdmin, IReadOnlyList<string> Permissions);
