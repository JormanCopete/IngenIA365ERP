using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Memberships.SuspendMembership;

/// <summary>
/// T093 — Suspende una membresía (Active → Suspended). Quien suspende debe
/// ser tenant admin del tenant indicado, o master admin.
/// </summary>
public sealed record SuspendMembershipCommand(
    Guid TenantPublicId,
    Guid MembershipPublicId
) : IRequest<Result>;
