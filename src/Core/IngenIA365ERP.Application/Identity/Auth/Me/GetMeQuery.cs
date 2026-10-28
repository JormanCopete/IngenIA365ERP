using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Auth.Me;

/// <summary>
/// T070 — Información del usuario autenticado y su sesión actual. Cubre los
/// datos para renderizar <c>TenantSwitcher.razor</c> y la pantalla de perfil
/// sin extra round-trips.
/// </summary>
public sealed record GetMeQuery() : IRequest<Result<MeResult>>;

public sealed record MeResult(
    Guid CentralUserId,
    string Email,
    bool IsGlobalMasterAdmin,
    bool MfaEnabled,
    ActiveTenantSummary? ActiveTenant,
    IReadOnlyList<AvailableTenantSummary> AvailableTenants,
    Guid? DefaultTenantPublicId,

    // Feature 003 (FR-110): null cuando MFA no está activo.
    int? RecoveryCodesRemaining = null,

    // Feature 017 (FR-029/FR-030): si falta registrar el documento, el usuario con que ya puede entrar
    // y, por cooperativa, desde cuándo lo exige (sólo las que fijaron fecha).
    bool DocumentPending = false,
    string? DocumentUsername = null,
    IReadOnlyList<DocumentRequiredFrom>? DocumentRequiredFrom = null);

/// <summary>Desde qué fecha una cooperativa exige el documento para entrar (feature 017, D-04).</summary>
public sealed record DocumentRequiredFrom(Guid TenantId, DateOnly From);

public sealed record ActiveTenantSummary(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin,
    bool IsMfaRequiredByPolicy);

public sealed record AvailableTenantSummary(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin);
