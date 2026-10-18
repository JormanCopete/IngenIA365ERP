using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

// Administración de la matriz contable de Cartera (feature 013, T0642, T52; contracts/contabilidad.md §3.5–§3.6; api.md §23.1). Las tres
// pasan por ReglasDeLaMatrizDeCartera, el mismo punto que usa la importación, y las audita AuditBehavior con el motivo (IConMotivo). Sin
// clave de operación: la matriz sigue la mecánica de la 009 (§21.1). Molde: InventoryPostingRuleCommands (012). (nuevo)

/// <summary>Las dimensiones que fija una regla, como llegan en el cuerpo (<c>dimensions</c>); sucursal y centro por <c>PublicId</c>. (nuevo)</summary>
public sealed record DimensionesDeReglaDeCarteraDto(
    string? CreditLineCode = null,
    string? ModalityCode = null,
    string? SubsegmentCode = null,
    string? RiskCategory = null,
    string? ConceptCode = null,
    bool? IsEmployee = null,
    bool? InGracePeriod = null,
    bool? OnBehalfOfAssociate = null,
    bool? Restructured = null,
    string? CollectorClass = null,
    string? ReasonCode = null,
    Guid? BranchPublicId = null,
    Guid? CostCenterPublicId = null,
    string? LineNature = null,
    string? DepositClass = null,
    string? IssueTermBand = null,
    string? ContributionClass = null,
    string? AccountFlag = null)
{
    public IReadOnlySet<DimensionDeCartera> PresentesCon(string? operacion) => ReglasDeLaMatrizDeCartera.Presentes(
        operacion, CreditLineCode, ModalityCode, SubsegmentCode, RiskCategory, ConceptCode, IsEmployee, InGracePeriod, OnBehalfOfAssociate,
        Restructured, CollectorClass, ReasonCode, BranchPublicId is not null, LineNature, DepositClass, IssueTermBand, ContributionClass, AccountFlag);
}

/// <summary>Lo que responde guardar una regla: su <c>PublicId</c> y los avisos (§3.6). (nuevo)</summary>
public sealed record ReglaDeCarteraGuardadaDto(Guid RulePublicId, IReadOnlyList<AvisoDeReglaDeCarteraDto> Warnings);

/// <summary>Las reglas de forma de la matriz en un validador de FluentValidation (400, <c>Validation.Invalid</c>). (nuevo)</summary>
internal static class FormaDeLaReglaDeCartera
{
    public static void Agregar<T>(
        ValidationContext<T> contexto, string? operacion, string? rol, string? motivo, IReadOnlySet<DimensionDeCartera> presentes,
        string? claseDeCaptacion = null)
    {
        foreach (var error in ReglasDeLaMatrizDeCartera.Forma(operacion, rol, motivo, presentes, claseDeCaptacion))
            contexto.AddFailure(new FluentValidation.Results.ValidationFailure("dimensions", error.Message) { ErrorCode = error.Code });
    }
}

// ============================================================================================================ crear --

/// <summary>
/// <c>POST /api/accounting/lending/rules</c>: abre una clave nueva (§3.5). Si la clave ya existe y no se cruza, es una vigencia más de ella
/// y la retroactividad se mide igual; si se cruza, <c>Overlaps</c>: para cambiar la cuenta de una clave vigente se agrega una versión.
/// El hecho va en <c>*</c> (o vacío) salvo en los roles de resultado. (nuevo)
/// </summary>
public sealed record CreateLendingPostingRuleCommand(
    string? Operation,
    string Role,
    DimensionesDeReglaDeCarteraDto Dimensions,
    Guid AccountPublicId,
    DateOnly ValidFrom,
    string? Notes,
    string Reason,
    DateOnly? ValidTo = null) : IRequest<Result<ReglaDeCarteraGuardadaDto>>, IConMotivo;

public sealed class CreateLendingPostingRuleCommandValidator : ValidadorConMotivo<CreateLendingPostingRuleCommand>
{
    public CreateLendingPostingRuleCommandValidator()
    {
        RuleFor(x => x.Operation).MaximumLength(40);
        RuleFor(x => x.Role).NotEmpty().WithMessage("Indique el rol.").MaximumLength(40);
        RuleFor(x => x.Dimensions).NotNull();
        RuleFor(x => x.AccountPublicId).NotEmpty().WithMessage("Indique la cuenta.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La vigencia no puede terminar antes de empezar.");
        RuleFor(x => x.Notes).MaximumLength(300);
        RuleFor(x => x).Custom((c, ctx) => FormaDeLaReglaDeCartera.Agregar(ctx, c.Operation, c.Role, c.Dimensions?.ReasonCode,
            c.Dimensions?.PresentesCon(c.Operation) ?? new HashSet<DimensionDeCartera>(), c.Dimensions?.DepositClass));
    }
}

public sealed class CreateLendingPostingRuleCommandHandler(IApplicationDbContext db, ReglasDeLaMatrizDeCartera reglas)
    : IRequestHandler<CreateLendingPostingRuleCommand, Result<ReglaDeCarteraGuardadaDto>>
{
    public async Task<Result<ReglaDeCarteraGuardadaDto>> Handle(CreateLendingPostingRuleCommand request, CancellationToken ct)
    {
        var d = request.Dimensions ?? new DimensionesDeReglaDeCarteraDto();
        var cuenta = await db.ChartOfAccounts.AsNoTracking().Where(a => a.PublicId == request.AccountPublicId && !a.IsDeleted)
            .Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
        if (cuenta is null) return Result.Failure<ReglaDeCarteraGuardadaDto>(AccountingErrors.AccountNotFound(request.AccountPublicId.ToString()));

        var sucursal = await ReglasDeMatrizDeCarteraComunes.SucursalAsync(db, d.BranchPublicId, ct);
        if (sucursal.IsFailure) return Result.Failure<ReglaDeCarteraGuardadaDto>(sucursal.Error);
        var centro = await ReglasDeMatrizDeCarteraComunes.CentroAsync(db, d.CostCenterPublicId, ct);
        if (centro.IsFailure) return Result.Failure<ReglaDeCarteraGuardadaDto>(centro.Error);

        var propuesta = new ReglaPropuestaDeCartera(
            ReglaPropuestaDeCartera.Valor(request.Operation) ?? LendingPostingRule.Cualquiera, request.Role.Trim(), cuenta.Value, request.ValidFrom,
            request.ValidTo, d.CreditLineCode, d.ModalityCode, d.SubsegmentCode, d.RiskCategory, d.ConceptCode, d.IsEmployee, d.InGracePeriod,
            d.OnBehalfOfAssociate, d.Restructured, d.CollectorClass, d.ReasonCode, sucursal.Value, centro.Value, d.LineNature, d.DepositClass,
            d.IssueTermBand, d.ContributionClass, d.AccountFlag);
        var clave = propuesta.Clave;
        var versiones = await db.LendingPostingRules.Where(r => r.DimensionKey == clave && !r.IsDeleted).ToListAsync(ct);

        var catalogos = await reglas.CargarAsync(ct);
        var validacion = await reglas.ValidarAsync(propuesta, catalogos, versiones, esVersion: false, ct);
        if (!validacion.EsValida) return Result.Failure<ReglaDeCarteraGuardadaDto>(validacion.Errores[0]);

        var regla = propuesta.ComoRegla(ReglasDeMatrizDeCarteraComunes.Notas(request.Notes, request.Reason));
        db.LendingPostingRules.Add(regla);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReglaDeCarteraGuardadaDto(regla.PublicId, validacion.Avisos));
    }
}

// ========================================================================================================== versión --

/// <summary>
/// <c>POST /api/accounting/lending/rules/{id}/versions</c>: otra fila con la misma clave y otra cuenta (y, si se quiere, otro centro de
/// costo, que no es parte de la clave) desde <see cref="ValidFrom"/>; la vigente se cierra la víspera. Una versión que empieza en o antes de
/// lo ya contabilizado que casa con la clave, <c>RetroactiveOverPosted</c>. (nuevo)
/// </summary>
public sealed record AddLendingPostingRuleVersionCommand(
    Guid RulePublicId,
    Guid AccountPublicId,
    DateOnly ValidFrom,
    string? Notes,
    string Reason,
    Guid? CostCenterPublicId = null) : IRequest<Result<ReglaDeCarteraGuardadaDto>>, IConMotivo;

public sealed class AddLendingPostingRuleVersionCommandValidator : ValidadorConMotivo<AddLendingPostingRuleVersionCommand>
{
    public AddLendingPostingRuleVersionCommandValidator()
    {
        RuleFor(x => x.RulePublicId).NotEmpty();
        RuleFor(x => x.AccountPublicId).NotEmpty().WithMessage("Indique la cuenta.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige.");
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class AddLendingPostingRuleVersionCommandHandler(IApplicationDbContext db, ReglasDeLaMatrizDeCartera reglas)
    : IRequestHandler<AddLendingPostingRuleVersionCommand, Result<ReglaDeCarteraGuardadaDto>>
{
    public async Task<Result<ReglaDeCarteraGuardadaDto>> Handle(AddLendingPostingRuleVersionCommand request, CancellationToken ct)
    {
        var base_ = await db.LendingPostingRules.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RulePublicId && !r.IsDeleted, ct);
        if (base_ is null) return Result.Failure<ReglaDeCarteraGuardadaDto>(AccountingErrors.LendingRuleNotFound);

        var cuenta = await db.ChartOfAccounts.AsNoTracking().Where(a => a.PublicId == request.AccountPublicId && !a.IsDeleted)
            .Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
        if (cuenta is null) return Result.Failure<ReglaDeCarteraGuardadaDto>(AccountingErrors.AccountNotFound(request.AccountPublicId.ToString()));
        int? centro = base_.CostCenterId;
        if (request.CostCenterPublicId is not null)
        {
            var otro = await ReglasDeMatrizDeCarteraComunes.CentroAsync(db, request.CostCenterPublicId, ct);
            if (otro.IsFailure) return Result.Failure<ReglaDeCarteraGuardadaDto>(otro.Error);
            centro = otro.Value;
        }

        var propuesta = new ReglaPropuestaDeCartera(base_.Operation, base_.Role, cuenta.Value, request.ValidFrom, null,
            base_.CreditLineCode, base_.ModalityCode, base_.SubsegmentCode, base_.RiskCategory, base_.ConceptCode, base_.IsEmployee,
            base_.InGracePeriod, base_.OnBehalfOfAssociate, base_.Restructured, base_.CollectorClass, base_.ReasonCode, base_.BranchId, centro,
            base_.LineNature, base_.DepositClass, base_.IssueTermBand, base_.ContributionClass, base_.AccountFlag);
        var versiones = await db.LendingPostingRules.Where(r => r.DimensionKey == base_.DimensionKey && !r.IsDeleted).ToListAsync(ct);

        var catalogos = await reglas.CargarAsync(ct);
        var validacion = await reglas.ValidarAsync(propuesta, catalogos, versiones, esVersion: true, ct);
        if (!validacion.EsValida) return Result.Failure<ReglaDeCarteraGuardadaDto>(validacion.Errores[0]);

        foreach (var anterior in validacion.ACerrar) anterior.CerrarVigencia(request.ValidFrom.AddDays(-1));
        var regla = propuesta.ComoRegla(ReglasDeMatrizDeCarteraComunes.Notas(request.Notes, request.Reason));
        db.LendingPostingRules.Add(regla);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReglaDeCarteraGuardadaDto(regla.PublicId, validacion.Avisos));
    }
}

// ======================================================================================================= desactivar --

/// <summary>
/// <c>POST /api/accounting/lending/rules/{id}/deactivate</c>: fija <c>ValidTo</c> con motivo y nunca borra (una regla vieja hace falta para
/// explicar lo contabilizado, §3.1). No puede terminar antes de lo ya contabilizado que la usó (<c>RetroactiveOverPosted</c>), ni alargarse
/// sobre otra versión de la clave (<c>Overlaps</c>). (nuevo)
/// </summary>
public sealed record DeactivateLendingPostingRuleCommand(Guid RulePublicId, DateOnly ValidTo, string Reason) : IRequest<Result>, IConMotivo;

public sealed class DeactivateLendingPostingRuleCommandValidator : ValidadorConMotivo<DeactivateLendingPostingRuleCommand>
{
    public DeactivateLendingPostingRuleCommandValidator()
    {
        RuleFor(x => x.RulePublicId).NotEmpty();
        RuleFor(x => x.ValidTo).NotEqual(default(DateOnly)).WithMessage("Indique hasta cuándo rige.");
    }
}

public sealed class DeactivateLendingPostingRuleCommandHandler(IApplicationDbContext db, ReglasDeLaMatrizDeCartera reglas)
    : IRequestHandler<DeactivateLendingPostingRuleCommand, Result>
{
    public async Task<Result> Handle(DeactivateLendingPostingRuleCommand request, CancellationToken ct)
    {
        var regla = await db.LendingPostingRules.FirstOrDefaultAsync(r => r.PublicId == request.RulePublicId && !r.IsDeleted, ct);
        if (regla is null) return Result.Failure(AccountingErrors.LendingRuleNotFound);
        if (request.ValidTo < regla.ValidFrom.AddDays(-1)) return Result.Failure(AccountingErrors.LendingRuleValidToInvalid(regla.ValidFrom, request.ValidTo));

        // Alargar una vigencia no puede pisar otra versión de la misma clave.
        var otras = await db.LendingPostingRules.AsNoTracking()
            .Where(r => r.DimensionKey == regla.DimensionKey && !r.IsDeleted && r.Id != regla.Id).ToListAsync(ct);
        var pisada = otras.FirstOrDefault(v => v.ValidFrom > regla.ValidFrom && v.ValidFrom <= request.ValidTo);
        if (pisada is not null)
            return Result.Failure(AccountingErrors.LendingRuleOverlaps(new
            {
                rulePublicId = pisada.PublicId, validFrom = pisada.ValidFrom, validTo = pisada.ValidTo, dimensionKey = pisada.DimensionKey,
            }));

        if (await reglas.RetroactividadDelCierreAsync(regla, request.ValidTo, ct) is { } retro) return Result.Failure(retro);

        // El motivo queda en la auditoría del comando (IConMotivo); la regla no se borra.
        regla.CerrarVigencia(request.ValidTo);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Lo que comparten los comandos y la importación de la matriz de Cartera. (nuevo)</summary>
internal static class ReglasDeMatrizDeCarteraComunes
{
    /// <summary>«Quién decidió y por qué»: las notas, o el motivo si no vinieron.</summary>
    public static string Notas(string? notas, string? motivo) =>
        !string.IsNullOrWhiteSpace(notas) ? notas.Trim() : motivo?.Trim() ?? string.Empty;

    public static async Task<Result<int?>> SucursalAsync(IApplicationDbContext db, Guid? publicId, CancellationToken ct)
    {
        if (publicId is not { } id) return Result.Success<int?>(null);
        var sucursal = await db.Branches.AsNoTracking().Where(b => b.PublicId == id && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        return sucursal is null
            ? Result.Failure<int?>(AccountingErrors.LendingRuleDimensionCodeUnknown(RolesDeCuentaDeCartera.NombreDe(DimensionDeCartera.Branch), id.ToString()))
            : Result.Success(sucursal);
    }

    public static async Task<Result<int?>> CentroAsync(IApplicationDbContext db, Guid? publicId, CancellationToken ct)
    {
        if (publicId is not { } id) return Result.Success<int?>(null);
        var centro = await db.CostCenters.AsNoTracking().Where(c => c.PublicId == id && !c.IsDeleted).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        return centro is null
            ? Result.Failure<int?>(AccountingErrors.LendingRuleDimensionCodeUnknown("costCenter", id.ToString()))
            : Result.Success(centro);
    }
}
