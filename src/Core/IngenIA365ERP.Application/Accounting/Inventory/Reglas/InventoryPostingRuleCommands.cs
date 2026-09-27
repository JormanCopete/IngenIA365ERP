using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Reglas;

// Administración de la matriz contable de Inventario (feature 012, T27, T507; contracts/contabilidad.md §2.4–§2.5;
// api.md §26.1). Las tres pasan por ReglasDeLaMatriz, el mismo punto que usa la importación, y las audita AuditBehavior
// (FR-073) con el motivo (IConMotivo). Sin clave de operación: la matriz sigue la mecánica de la 009 (§7.3). (nuevo)

/// <summary>Las dimensiones que fija una regla, como llegan en el cuerpo (<c>dimensions</c>); sucursal y centro por <c>PublicId</c>. (nuevo)</summary>
public sealed record DimensionesDeLaReglaDto(
    string? AccountingGroupCode = null,
    string? WarehouseCode = null,
    string? PointOfSaleCode = null,
    string? PaymentMeansCode = null,
    string? TaxRateCode = null,
    decimal? TaxRate = null,
    string? ReasonCode = null,
    Guid? BranchPublicId = null,
    Guid? CostCenterPublicId = null)
{
    public IReadOnlySet<DimensionDeRegla> Presentes => ReglasDeLaMatriz.Presentes(
        AccountingGroupCode, WarehouseCode, PointOfSaleCode, PaymentMeansCode, TaxRateCode, TaxRate, ReasonCode,
        BranchPublicId is not null, CostCenterPublicId is not null);
}

/// <summary>Lo que responde guardar una regla: su <c>PublicId</c> y los avisos (C8). (nuevo)</summary>
public sealed record ReglaGuardadaDto(Guid RulePublicId, IReadOnlyList<AvisoDeReglaDto> Warnings);

/// <summary>Las reglas de forma de la matriz en un validador de FluentValidation (400, <c>Validation.Invalid</c>). (nuevo)</summary>
internal static class FormaDeLaRegla
{
    public static void Agregar<T>(ValidationContext<T> contexto, string? operacion, string? rol, IReadOnlySet<DimensionDeRegla> presentes)
    {
        foreach (var error in ReglasDeLaMatriz.Forma(operacion, rol, presentes))
            contexto.AddFailure(new FluentValidation.Results.ValidationFailure("dimensions", error.Message) { ErrorCode = error.Code });
    }
}

// ============================================================================================================ crear --

/// <summary>
/// <c>POST /api/accounting/inventory/rules</c>: abre una clave nueva (§2.4). Si la clave ya existe y no se cruza, es una
/// vigencia más de ella y la retroactividad se mide como en una versión; si se cruza, <c>Overlaps</c>: para cambiar la
/// cuenta de una clave vigente se agrega una versión. (nuevo)
/// </summary>
public sealed record CreateInventoryPostingRuleCommand(
    string Operation,
    string Role,
    DimensionesDeLaReglaDto Dimensions,
    Guid AccountPublicId,
    DateOnly ValidFrom,
    string? Notes,
    string Reason,
    DateOnly? ValidTo = null) : IRequest<Result<ReglaGuardadaDto>>, IConMotivo;

public sealed class CreateInventoryPostingRuleCommandValidator : ValidadorConMotivo<CreateInventoryPostingRuleCommand>
{
    public CreateInventoryPostingRuleCommandValidator()
    {
        RuleFor(x => x.Operation).NotEmpty().WithMessage("Indicá la operación.").MaximumLength(40);
        RuleFor(x => x.Role).NotEmpty().WithMessage("Indicá el rol.").MaximumLength(40);
        RuleFor(x => x.Dimensions).NotNull();
        RuleFor(x => x.AccountPublicId).NotEmpty().WithMessage("Indicá la cuenta.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La vigencia no puede terminar antes de empezar.");
        RuleFor(x => x.Notes).MaximumLength(400);
        RuleFor(x => x).Custom((c, ctx) => FormaDeLaRegla.Agregar(ctx, c.Operation, c.Role, c.Dimensions?.Presentes ?? new HashSet<DimensionDeRegla>()));
    }
}

public sealed class CreateInventoryPostingRuleCommandHandler(IApplicationDbContext db, ReglasDeLaMatriz reglas)
    : IRequestHandler<CreateInventoryPostingRuleCommand, Result<ReglaGuardadaDto>>
{
    public async Task<Result<ReglaGuardadaDto>> Handle(CreateInventoryPostingRuleCommand request, CancellationToken ct)
    {
        var d = request.Dimensions ?? new DimensionesDeLaReglaDto();
        var cuenta = await db.ChartOfAccounts.AsNoTracking().Where(a => a.PublicId == request.AccountPublicId && !a.IsDeleted)
            .Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
        if (cuenta is null) return Result.Failure<ReglaGuardadaDto>(AccountingErrors.AccountNotFound(request.AccountPublicId.ToString()));

        var sucursal = await ReglasDeMatrizComunes.SucursalAsync(db, d.BranchPublicId, ct);
        if (sucursal.IsFailure) return Result.Failure<ReglaGuardadaDto>(sucursal.Error);
        var centro = await ReglasDeMatrizComunes.CentroAsync(db, d.CostCenterPublicId, ct);
        if (centro.IsFailure) return Result.Failure<ReglaGuardadaDto>(centro.Error);

        var propuesta = new ReglaPropuesta(request.Operation, request.Role, cuenta.Value, request.ValidFrom, request.ValidTo,
            d.AccountingGroupCode, d.WarehouseCode, d.PointOfSaleCode, d.PaymentMeansCode, d.TaxRateCode, d.TaxRate, d.ReasonCode,
            sucursal.Value, centro.Value);
        var clave = propuesta.Clave;
        var versiones = await db.InventoryPostingRules.Where(r => r.DimensionKey == clave && !r.IsDeleted).ToListAsync(ct);

        var catalogos = await reglas.CargarAsync(ct);
        var validacion = await reglas.ValidarAsync(propuesta, catalogos, versiones, esVersion: false, ct);
        if (!validacion.EsValida) return Result.Failure<ReglaGuardadaDto>(validacion.Errores[0]);

        var regla = propuesta.ComoRegla(ReglasDeMatrizComunes.Notas(request.Notes, request.Reason));
        db.InventoryPostingRules.Add(regla);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReglaGuardadaDto(regla.PublicId, validacion.Avisos));
    }
}

// ========================================================================================================== versión --

/// <summary>
/// <c>POST /api/accounting/inventory/rules/{id}/versions</c>: otra fila con la misma clave y otra cuenta desde
/// <see cref="ValidFrom"/>; la vigente se cierra la víspera (molde de <c>AddPolicyVersionCommand</c>). Una versión que
/// empieza en o antes de lo ya contabilizado de la operación, <c>RetroactiveOverPosted</c>. (nuevo)
/// </summary>
public sealed record AddInventoryPostingRuleVersionCommand(
    Guid RulePublicId,
    Guid AccountPublicId,
    DateOnly ValidFrom,
    string? Notes,
    string Reason) : IRequest<Result<ReglaGuardadaDto>>, IConMotivo;

public sealed class AddInventoryPostingRuleVersionCommandValidator : ValidadorConMotivo<AddInventoryPostingRuleVersionCommand>
{
    public AddInventoryPostingRuleVersionCommandValidator()
    {
        RuleFor(x => x.RulePublicId).NotEmpty();
        RuleFor(x => x.AccountPublicId).NotEmpty().WithMessage("Indicá la cuenta.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige.");
        RuleFor(x => x.Notes).MaximumLength(400);
    }
}

public sealed class AddInventoryPostingRuleVersionCommandHandler(IApplicationDbContext db, ReglasDeLaMatriz reglas)
    : IRequestHandler<AddInventoryPostingRuleVersionCommand, Result<ReglaGuardadaDto>>
{
    public async Task<Result<ReglaGuardadaDto>> Handle(AddInventoryPostingRuleVersionCommand request, CancellationToken ct)
    {
        var base_ = await db.InventoryPostingRules.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RulePublicId && !r.IsDeleted, ct);
        if (base_ is null) return Result.Failure<ReglaGuardadaDto>(AccountingErrors.InventoryRuleNotFound);

        var cuenta = await db.ChartOfAccounts.AsNoTracking().Where(a => a.PublicId == request.AccountPublicId && !a.IsDeleted)
            .Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
        if (cuenta is null) return Result.Failure<ReglaGuardadaDto>(AccountingErrors.AccountNotFound(request.AccountPublicId.ToString()));

        var propuesta = new ReglaPropuesta(base_.Operation, base_.Role, cuenta.Value, request.ValidFrom, null,
            base_.AccountingGroupCode, base_.WarehouseCode, base_.PointOfSaleCode, base_.PaymentMeansCode, base_.TaxRateCode, base_.TaxRate,
            base_.ReasonCode, base_.BranchId, base_.CostCenterId);
        var versiones = await db.InventoryPostingRules.Where(r => r.DimensionKey == base_.DimensionKey && !r.IsDeleted).ToListAsync(ct);

        var catalogos = await reglas.CargarAsync(ct);
        var validacion = await reglas.ValidarAsync(propuesta, catalogos, versiones, esVersion: true, ct);
        if (!validacion.EsValida) return Result.Failure<ReglaGuardadaDto>(validacion.Errores[0]);

        foreach (var anterior in validacion.ACerrar) anterior.CerrarVigencia(request.ValidFrom.AddDays(-1));
        var regla = propuesta.ComoRegla(ReglasDeMatrizComunes.Notas(request.Notes, request.Reason));
        db.InventoryPostingRules.Add(regla);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReglaGuardadaDto(regla.PublicId, validacion.Avisos));
    }
}

// ======================================================================================================= desactivar --

/// <summary>
/// <c>POST /api/accounting/inventory/rules/{id}/deactivate</c>: fija <c>ValidTo</c> con motivo y nunca borra (la regla
/// vieja hace falta para el espejo de una anulación, §6). No puede terminar antes de lo ya contabilizado de su operación
/// (<c>RetroactiveOverPosted</c>), ni alargarse sobre otra versión de la clave (<c>Overlaps</c>). (nuevo)
/// </summary>
public sealed record DeactivateInventoryPostingRuleCommand(Guid RulePublicId, DateOnly ValidTo, string Reason) : IRequest<Result>, IConMotivo;

public sealed class DeactivateInventoryPostingRuleCommandValidator : ValidadorConMotivo<DeactivateInventoryPostingRuleCommand>
{
    public DeactivateInventoryPostingRuleCommandValidator()
    {
        RuleFor(x => x.RulePublicId).NotEmpty();
        RuleFor(x => x.ValidTo).NotEqual(default(DateOnly)).WithMessage("Indicá hasta cuándo rige.");
    }
}

public sealed class DeactivateInventoryPostingRuleCommandHandler(IApplicationDbContext db, ReglasDeLaMatriz reglas)
    : IRequestHandler<DeactivateInventoryPostingRuleCommand, Result>
{
    public async Task<Result> Handle(DeactivateInventoryPostingRuleCommand request, CancellationToken ct)
    {
        var regla = await db.InventoryPostingRules.FirstOrDefaultAsync(r => r.PublicId == request.RulePublicId && !r.IsDeleted, ct);
        if (regla is null) return Result.Failure(AccountingErrors.InventoryRuleNotFound);
        if (request.ValidTo < regla.ValidFrom.AddDays(-1)) return Result.Failure(AccountingErrors.InventoryRuleValidToInvalid(regla.ValidFrom, request.ValidTo));

        // Alargar una vigencia no puede pisar otra versión de la misma clave.
        var otras = await db.InventoryPostingRules.AsNoTracking()
            .Where(r => r.DimensionKey == regla.DimensionKey && !r.IsDeleted && r.Id != regla.Id).ToListAsync(ct);
        var pisada = otras.FirstOrDefault(v => v.ValidFrom > regla.ValidFrom && v.ValidFrom <= request.ValidTo);
        if (pisada is not null)
            return Result.Failure(AccountingErrors.InventoryRuleOverlaps(new
            {
                rulePublicId = pisada.PublicId, validFrom = pisada.ValidFrom, validTo = pisada.ValidTo, dimensionKey = pisada.DimensionKey,
            }));

        var catalogos = await reglas.CargarAsync(ct);
        if (await reglas.RetroactividadDelCierreAsync(regla, request.ValidTo, catalogos, ct) is { } retro) return Result.Failure(retro);

        regla.CerrarVigencia(request.ValidTo);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Lo que comparten los comandos y la importación de la matriz (nuevo).</summary>
internal static class ReglasDeMatrizComunes
{
    /// <summary>«Quién decidió y por qué»: las notas, o el motivo si no vinieron.</summary>
    public static string Notas(string? notas, string? motivo) =>
        !string.IsNullOrWhiteSpace(notas) ? notas.Trim() : motivo?.Trim() ?? string.Empty;

    public static async Task<Result<int?>> SucursalAsync(IApplicationDbContext db, Guid? publicId, CancellationToken ct)
    {
        if (publicId is not { } id) return Result.Success<int?>(null);
        var sucursal = await db.Branches.AsNoTracking().Where(b => b.PublicId == id && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        return sucursal is null
            ? Result.Failure<int?>(AccountingErrors.InventoryRuleDimensionCodeUnknown(RolesDeCuenta.NombreDe(DimensionDeRegla.Branch), id.ToString()))
            : Result.Success(sucursal);
    }

    public static async Task<Result<int?>> CentroAsync(IApplicationDbContext db, Guid? publicId, CancellationToken ct)
    {
        if (publicId is not { } id) return Result.Success<int?>(null);
        var centro = await db.CostCenters.AsNoTracking().Where(c => c.PublicId == id && !c.IsDeleted).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        return centro is null
            ? Result.Failure<int?>(AccountingErrors.InventoryRuleDimensionCodeUnknown(RolesDeCuenta.NombreDe(DimensionDeRegla.CostCenter), id.ToString()))
            : Result.Success(centro);
    }
}
