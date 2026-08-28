using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Tenants.GetTenantMfaPolicy;

public sealed record GetTenantMfaPolicyQuery(Guid TenantPublicId)
    : IRequest<Result<TenantMfaPolicyDto>>;

public sealed record TenantMfaPolicyDto(
    Guid TenantPublicId,
    bool IsRequired,
    DateTime? ActivatedAt,
    DateTime? DeactivatedAt,
    IReadOnlyList<string>? MetodosAceptados = null);
