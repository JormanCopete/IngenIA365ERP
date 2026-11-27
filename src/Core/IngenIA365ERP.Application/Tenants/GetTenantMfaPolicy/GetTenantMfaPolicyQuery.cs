using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;

namespace IngenIA365ERP.Application.Tenants.GetTenantMfaPolicy;

public sealed record GetTenantMfaPolicyQuery(Guid TenantPublicId)
    : IRequest<Result<TenantMfaPolicyDto>>;

public sealed record TenantMfaPolicyDto(
    Guid TenantPublicId,
    bool IsRequired,
    DateTime? ActivatedAt,
    DateTime? DeactivatedAt,
    IReadOnlyList<string>? MetodosAceptados = null,
    bool PermitirRecuperacionPorCorreo = false,
    int HorasDeDemora = TenantMfaPolicy.DemoraPorDefectoEnHoras,
    bool PermitirPasesDeIngreso = false,
    int HorasMaximasDelPase = LoginPass.HorasMaximas);
