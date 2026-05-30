using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Admin.Tenants.ActivateTenant;

public sealed record ActivateTenantCommand(Guid TenantPublicId) : IRequest<Result>;

public sealed class ActivateTenantCommandValidator
    : FluentValidation.AbstractValidator<ActivateTenantCommand>
{
    public ActivateTenantCommandValidator() => RuleFor(x => x.TenantPublicId).NotEmpty();
}
