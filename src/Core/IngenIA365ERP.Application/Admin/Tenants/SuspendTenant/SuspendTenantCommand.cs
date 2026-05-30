using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Admin.Tenants.SuspendTenant;

/// <summary>
/// Suspende la cooperativa (no soft-delete). Marca <c>IsActive=false</c> y
/// fija <c>SuspendedAt</c>. Los usuarios del tenant siguen existiendo pero
/// el login se rechaza por el filtro de tenant activo (FR-002).
/// </summary>
public sealed record SuspendTenantCommand(Guid TenantPublicId, string Reason) : IRequest<Result>;

public sealed class SuspendTenantCommandValidator : FluentValidation.AbstractValidator<SuspendTenantCommand>
{
    public SuspendTenantCommandValidator()
    {
        RuleFor(x => x.TenantPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
