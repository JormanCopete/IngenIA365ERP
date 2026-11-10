using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Platform.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Platform.Modules;

// Feature 020 (US1; R1; FR-001 a FR-004; contracts/api.md §1): el maestro habilita y deshabilita módulos por cooperativa. Las rutas
// /api/saas/tenants/{tenantId}/modules van tras el filtro de maestro; el handler lo vuelve a exigir.

/// <summary>Un módulo habilitable y su estado en una cooperativa.</summary>
public sealed record TenantModuleDto(string Code, string Name, bool Enabled, DateTime? ChangedAt, string? ChangedBy, string? Reason);

/// <summary>Los módulos habilitables de una cooperativa, con su estado (los que nunca se tocaron salen apagados).</summary>
public sealed record GetTenantModulesQuery(Guid TenantPublicId) : IRequest<Result<IReadOnlyList<TenantModuleDto>>>;

public sealed class GetTenantModulesQueryHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario)
    : IRequestHandler<GetTenantModulesQuery, Result<IReadOnlyList<TenantModuleDto>>>
{
    public async Task<Result<IReadOnlyList<TenantModuleDto>>> Handle(GetTenantModulesQuery request, CancellationToken ct)
    {
        var maestro = MaestroDePlataforma.De(usuario);
        if (maestro.IsFailure) return Result.Failure<IReadOnlyList<TenantModuleDto>>(maestro.Error);
        if (!await admin.Tenants.AsNoTracking().AnyAsync(t => t.PublicId == request.TenantPublicId, ct))
            return Result.Failure<IReadOnlyList<TenantModuleDto>>(ErroresDeModulos.CooperativaNoEncontrada(request.TenantPublicId));

        var filas = await admin.TenantModules.AsNoTracking().Where(m => m.TenantId == request.TenantPublicId).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TenantModuleDto>>(ModulosHabilitables.Todos
            .Select(m =>
            {
                var fila = filas.FirstOrDefault(f => string.Equals(f.ModuleCode, m.Codigo, StringComparison.OrdinalIgnoreCase));
                return new TenantModuleDto(m.Codigo, m.Nombre, fila?.IsEnabled ?? false, fila?.ChangedAt, fila?.ChangedBy, fila?.Reason);
            })
            .ToList());
    }
}

/// <summary>
/// Habilita o deshabilita un módulo en una cooperativa, con motivo (FR-001). Deshabilitar no toca ningún dato (FR-003). El
/// aprovisionamiento en la base de la cooperativa (participantes de la bandeja, catálogos sembrados) lo hace la tarea programada
/// <see cref="TareaDeAprovisionamientoDeModulos"/> en su siguiente pasada: una petición no escribe en otra cooperativa.
/// </summary>
public sealed record SetTenantModuleCommand(Guid TenantPublicId, string Code, bool Enabled, string Reason) : IRequest<Result>;

public sealed class SetTenantModuleCommandValidator : AbstractValidator<SetTenantModuleCommand>
{
    public SetTenantModuleCommandValidator()
    {
        RuleFor(x => x.TenantPublicId).NotEqual(Guid.Empty).WithMessage("Indique la cooperativa.");
        RuleFor(x => x.Code).Must(ModulosHabilitables.Existe)
            .WithMessage(x => $"«{x.Code}» no es un módulo habilitable. Son: {string.Join(", ", ModulosHabilitables.Todos.Select(m => m.Codigo))}.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo del cambio.").MaximumLength(500);
    }
}

public sealed class SetTenantModuleCommandHandler(
    IAdminDbContext admin,
    ICurrentCentralUserContext usuario,
    IModulosHabilitados modulos,
    IDateTimeService reloj,
    IAuditAppendOnlyWriter? auditoria = null,
    ILogger<SetTenantModuleCommandHandler>? logger = null) : IRequestHandler<SetTenantModuleCommand, Result>
{
    public async Task<Result> Handle(SetTenantModuleCommand request, CancellationToken ct)
    {
        var maestro = MaestroDePlataforma.De(usuario);
        if (maestro.IsFailure) return Result.Failure(maestro.Error);
        if (!await admin.Tenants.AsNoTracking().AnyAsync(t => t.PublicId == request.TenantPublicId, ct))
            return Result.Failure(ErroresDeModulos.CooperativaNoEncontrada(request.TenantPublicId));

        var codigo = ModulosHabilitables.Buscar(request.Code)!.Codigo;
        var fila = await admin.TenantModules.FirstOrDefaultAsync(m => m.TenantId == request.TenantPublicId && m.ModuleCode == codigo, ct);
        var antes = fila?.IsEnabled ?? false;
        if (fila is null)
        {
            fila = TenantModule.Nuevo(request.TenantPublicId, codigo);
            fila.CreatedBy = maestro.Value.Nombre;
            admin.TenantModules.Add(fila);
        }

        var ahora = reloj.UtcNow;
        fila.Cambiar(request.Enabled, ahora, maestro.Value.Nombre, request.Reason);
        await admin.SaveChangesAsync(ct);
        modulos.Invalidar(request.TenantPublicId);

        await AuditoriaDePlataforma.RegistrarAsync(auditoria, maestro.Value,
            request.Enabled ? AuditoriaDePlataforma.ModuloHabilitado : AuditoriaDePlataforma.ModuloDeshabilitado,
            nameof(TenantModule), fila.PublicId, request.TenantPublicId,
            new { module = codigo, before = antes, enabled = request.Enabled, reason = request.Reason.Trim() }, ahora, logger, ct);
        return Result.Success();
    }
}

/// <summary>Los errores de la habilitación de módulos.</summary>
public static class ErroresDeModulos
{
    public static Error CooperativaNoEncontrada(Guid id) =>
        new("Tenant.NotFound", $"No existe la cooperativa {id:D}.");
}
