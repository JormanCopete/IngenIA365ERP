using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Tenants.UpdateTenantMfaPolicy;

/// <summary>
/// T099 — Activa o desactiva la política "MFA obligatorio" para una empresa.
/// Al cambiar el flag, publica IMembershipChangedNotifier.PublishForTenant
/// MembersAsync para invalidar la cache de cada miembro: el flag
/// IsMfaRequiredByTenant se recompute en próximos logins.
/// </summary>
public sealed record UpdateTenantMfaPolicyCommand(
    Guid TenantPublicId,
    bool IsRequired
) : IRequest<Result>;
