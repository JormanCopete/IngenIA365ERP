using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Admin.Tenants.UpdateTenant;

/// <summary>
/// Edita datos legales / comerciales del tenant. NO cambia <c>SchemaName</c>
/// (es identidad de máquina). NO cambia el estado (suspend/activate viven
/// en comandos dedicados).
/// </summary>
public sealed record UpdateTenantCommand(
    Guid TenantPublicId,
    string Name,
    string LegalName,
    string? LegalAddress,
    string? TaxRegime,
    string ContactEmail,
    string? ContactPhone,
    string PlanType,
    int MaxUsers,
    long StorageLimitMb) : IRequest<Result>;
