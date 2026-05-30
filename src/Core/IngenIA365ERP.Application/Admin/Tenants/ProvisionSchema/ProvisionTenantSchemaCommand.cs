using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Admin.Tenants.ProvisionSchema;

/// <summary>
/// T073 — Provisión completa de un tenant recién registrado. Idempotente:
/// se puede ejecutar varias veces sobre el mismo tenant sin efectos
/// secundarios.
///
/// Acciones:
///  1. Clonar los roles built-in plantilla (TenantId NULL) al tenant
///     concreto — cada cooperativa parte con CompanyAdmin, Auditor,
///     Operator, ReadOnly propios + sus permisos asociados.
///  2. Crear la sucursal matriz (HQ) con código <c>MAT</c> si aún no
///     existe.
///
/// Se ejecuta normalmente justo después de <c>RegisterTenantCommand</c>
/// pero es independiente: si llega un tenant huérfano (creado a mano
/// en BD), este command lo completa.
/// </summary>
public sealed record ProvisionTenantSchemaCommand(Guid TenantPublicId)
    : IRequest<Result<ProvisionTenantSchemaResult>>;

public sealed record ProvisionTenantSchemaResult(
    int RolesCloned,
    int RolePermissionsCloned,
    bool HeadquartersCreated);

public sealed class ProvisionTenantSchemaCommandValidator
    : AbstractValidator<ProvisionTenantSchemaCommand>
{
    public ProvisionTenantSchemaCommandValidator() =>
        RuleFor(x => x.TenantPublicId).NotEmpty();
}
