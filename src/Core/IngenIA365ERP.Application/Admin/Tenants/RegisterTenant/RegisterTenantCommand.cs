using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Admin.Tenants.RegisterTenant;

/// <summary>
/// Registra una cooperativa-tenant. Operación SaaS-global — solo accesible
/// con permiso <c>Admin.Tenants.Create</c>. La provisión del schema físico y
/// el seed de built-in roles ocurre en
/// <c>ProvisionTenantSchemaCommand</c> (T073).
/// </summary>
public sealed record RegisterTenantCommand(
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
    long StorageLimitMb) : IRequest<Result<Guid>>;
