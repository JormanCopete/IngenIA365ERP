using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Saas.RegisterTenantWithAdmin;

/// <summary>
/// T112 — Variante del <c>RegisterTenantCommand</c> de Fase 0 que además emite
/// una invitación con <c>InviteAsTenantAdmin=true</c> para
/// <see cref="FirstAdminEmail"/>. Operación atómica: si la invitación falla,
/// el tenant igual queda creado (sería caso a recuperar manualmente; rara vez
/// debería ocurrir porque el envío de correo es fail-soft en este flujo).
/// </summary>
public sealed record RegisterTenantWithAdminCommand(
    string Name,
    string SchemaName,
    string? Subdomain,
    string Nit,
    string LegalName,
    string? LegalAddress,
    string? TaxRegime,
    string ContactEmail,
    string? ContactPhone,
    string PlanType,
    int MaxUsers,
    long StorageLimitMb,
    string FirstAdminEmail
) : IRequest<Result<RegisterTenantWithAdminResult>>;

public sealed record RegisterTenantWithAdminResult(
    Guid TenantPublicId,
    Guid InvitationPublicId,
    DateTime InvitationExpiresAt);
