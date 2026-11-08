namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>Lo que devuelve <c>GET /api/admin/permissions/mine</c> (feature 008).</summary>
/// <param name="EnabledModules">Feature 020 (R1): los módulos habilitables encendidos en la cooperativa activa (<c>TRN</c>…).</param>
public sealed record PermisosMiosDto(bool IsGlobalMasterAdmin, IReadOnlyList<string> Permissions, IReadOnlyList<string>? EnabledModules = null);
