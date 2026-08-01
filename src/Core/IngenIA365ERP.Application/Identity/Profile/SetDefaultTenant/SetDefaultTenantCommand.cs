using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.SetDefaultTenant;

/// <summary>
/// T083 — Fija o limpia la empresa por defecto del usuario. <c>null</c> para
/// limpiar; si no nulo, debe haber membresía Active con ese tenant.
/// </summary>
public sealed record SetDefaultTenantCommand(Guid? TenantPublicId)
    : IRequest<Result>;
