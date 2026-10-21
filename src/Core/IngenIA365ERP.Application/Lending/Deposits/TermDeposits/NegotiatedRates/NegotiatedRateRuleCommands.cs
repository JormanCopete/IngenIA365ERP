using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;

/// <summary>Los criterios de una regla de tasa negociada (api.md §3; data-model §2.4): nulo = no restringe. (nuevo)</summary>
public sealed record NegotiatedRateCriteriaDto(
    int? MinSeniorityMonths = null,
    decimal? AmountFrom = null,
    decimal? AmountTo = null,
    int? TermDaysFrom = null,
    int? TermDaysTo = null,
    decimal? BaseRateFrom = null,
    decimal? BaseRateTo = null,
    decimal? HolderBalanceFrom = null,
    decimal? HolderBalanceTo = null,
    Guid? OfficePublicId = null,
    string? Channel = null);

/// <summary>Una regla de tasa negociada (api.md §3, <c>NegotiatedRateRuleDto</c>). (nuevo)</summary>
public sealed record NegotiatedRateRuleDto(
    Guid PublicId,
    string Code,
    int Version,
    int Priority,
    NegotiatedRateCriteriaDto Criteria,
    decimal MaxMarginEa,
    bool? KeepsMarginOnRollover,
    DateOnly ValidFrom,
    bool IsActive,
    string Reason);

/// <summary>El alta (o la versión nueva) de una regla: con el mismo código, versiona la existente. (nuevo)</summary>
public sealed record NegotiatedRateRuleInput(
    string Code,
    int Priority,
    NegotiatedRateCriteriaDto Criteria,
    decimal MaxMarginEa,
    bool? KeepsMarginOnRollover,
    DateOnly ValidFrom,
    string Reason);

/// <summary>Las reglas vigentes de una línea a una fecha (<c>GET /api/lending/lines/{id}/negotiated-rate-rules?date=</c>, <c>Lending.DepositLines.View</c>). (nuevo)</summary>
public sealed record ListNegotiatedRateRulesQuery(Guid LinePublicId, DateOnly? Date = null) : IRequest<Result<IReadOnlyList<NegotiatedRateRuleDto>>>;

public sealed class ListNegotiatedRateRulesQueryHandler(IApplicationDbContext db, NegociacionDeTasa negociacion, IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria, IDateTimeService reloj) : IRequestHandler<ListNegotiatedRateRulesQuery, Result<IReadOnlyList<NegotiatedRateRuleDto>>>
{
    public async Task<Result<IReadOnlyList<NegotiatedRateRuleDto>>> Handle(ListNegotiatedRateRulesQuery request, CancellationToken ct)
    {
        var linea = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, request.LinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<IReadOnlyList<NegotiatedRateRuleDto>>(linea.Error);
        var reglas = await negociacion.ReglasVigentesAsync(linea.Value.Id, request.Date ?? reloj.HoyLocal, null, ct);
        return Result.Success<IReadOnlyList<NegotiatedRateRuleDto>>(await ReglasDeTasaNegociada.DtosAsync(db, reglas, ct));
    }
}

/// <summary>
/// Registra una regla de tasa negociada o su versión nueva (feature 014, US6, T171; api.md §3, <c>POST /api/lending/lines/{id}/negotiated-rate-rules</c>,
/// <c>Lending.DepositLines.Manage</c>): la línea es de captación y su versión vigente admite tasa negociada (<c>Lending.NegotiatedRate.LineDoesNotAllow</c>),
/// sin vigencia retroactiva (<c>Lending.NegotiatedRate.Retroactive</c>), el código es de catálogo y el margen es mayor que cero con seis decimales a lo sumo.
/// Con un código que ya existe nace la versión siguiente (data-model §2.4). (nuevo)
/// </summary>
public sealed record AddNegotiatedRateRuleCommand(Guid LinePublicId, NegotiatedRateRuleInput Input) : IRequest<Result<NegotiatedRateRuleDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AddNegotiatedRateRuleCommandValidator : AbstractValidator<AddNegotiatedRateRuleCommand>
{
    public AddNegotiatedRateRuleCommandValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Code).NotEmpty().MaximumLength(10).Matches("^[A-Z0-9_-]+$").WithMessage("El código va en mayúsculas, sin espacios.")
            .When(x => x.Input is not null);
        RuleFor(x => x.Input.MaxMarginEa).GreaterThan(0m).Must(EquivalenciaDeTasas.EsTasaPactable).WithMessage("El margen lleva a lo sumo seis decimales.")
            .When(x => x.Input is not null);
        RuleFor(x => x.Input.Reason).NotEmpty().MaximumLength(NegotiatedRateRule.LargoDelMotivo).When(x => x.Input is not null);
        RuleFor(x => x.Input.Criteria).NotNull().When(x => x.Input is not null);
        RuleFor(x => x.Input.Criteria.Channel).MaximumLength(NegotiatedRateRule.LargoDelCanal).When(x => x.Input?.Criteria is not null);
        RuleFor(x => x.Input.Criteria).Must(c => (c.AmountFrom is null || c.AmountTo is null || c.AmountFrom <= c.AmountTo)
                                                  && (c.TermDaysFrom is null || c.TermDaysTo is null || c.TermDaysFrom <= c.TermDaysTo)
                                                  && (c.BaseRateFrom is null || c.BaseRateTo is null || c.BaseRateFrom <= c.BaseRateTo)
                                                  && (c.HolderBalanceFrom is null || c.HolderBalanceTo is null || c.HolderBalanceFrom <= c.HolderBalanceTo))
            .WithMessage("Cada rango va de menor a mayor.").When(x => x.Input?.Criteria is not null);
    }
}

public sealed class AddNegotiatedRateRuleCommandHandler(IApplicationDbContext db, IContextoDeLineasDeCaptacion contexto, IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria, IDateTimeService reloj) : IRequestHandler<AddNegotiatedRateRuleCommand, Result<NegotiatedRateRuleDto>>
{
    public async Task<Result<NegotiatedRateRuleDto>> Handle(AddNegotiatedRateRuleCommand request, CancellationToken ct)
    {
        if (contexto.Entrega < Domain.Lending.EntregaDeCartera.K9) return Result.Failure<NegotiatedRateRuleDto>(ErroresDeCdat.EntregaNoDisponible("La tasa negociada"));
        var e = request.Input;
        var hoy = reloj.HoyLocal;
        var linea = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, request.LinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<NegotiatedRateRuleDto>(linea.Error);
        var version = linea.Value.Nature == LineNature.Deposit
            ? await LectorDeLineasDeCaptacion.VigenteOProximaDeCaptacionAsync(db, linea.Value.Id, e.ValidFrom, ct)
            : null;
        if (version is not { AllowsNegotiatedRate: true }) return Result.Failure<NegotiatedRateRuleDto>(ErroresDeCdat.LineaNoNegocia(linea.Value.Code));
        if (e.ValidFrom < hoy)
            return Result.Failure<NegotiatedRateRuleDto>(new ErrorConDatos(ErroresDeCdat.CodigoReglaRetroactiva,
                $"Una regla de tasa negociada rige desde hoy ({ErroresDeCuentas.Fecha(hoy)}) o después.", new { validFrom = e.ValidFrom }));
        int? oficina = null;
        if (e.Criteria.OfficePublicId is { } publica)
        {
            oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == publica && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            if (oficina is null) return Result.Failure<NegotiatedRateRuleDto>(Error.NotFound);
        }
        var codigo = e.Code.Trim().ToUpperInvariant();
        var ultima = await db.NegotiatedRateRules.AsNoTracking().Where(r => r.CreditLineId == linea.Value.Id && r.Code == codigo && !r.IsDeleted)
            .MaxAsync(r => (int?)r.VersionNumber, ct) ?? 0;
        var regla = new NegotiatedRateRule
        {
            CreditLineId = linea.Value.Id, Code = codigo, VersionNumber = ultima + 1, ValidFrom = e.ValidFrom, RegisteredAt = reloj.UtcNow, IsActive = true,
            Priority = e.Priority, MinSeniorityMonths = e.Criteria.MinSeniorityMonths, MinAmount = e.Criteria.AmountFrom, MaxAmount = e.Criteria.AmountTo,
            MinTermDays = e.Criteria.TermDaysFrom, MaxTermDays = e.Criteria.TermDaysTo, MinBaseRate = e.Criteria.BaseRateFrom, MaxBaseRate = e.Criteria.BaseRateTo,
            MinHolderTotalBalance = e.Criteria.HolderBalanceFrom, MaxHolderTotalBalance = e.Criteria.HolderBalanceTo, BranchId = oficina,
            Channel = string.IsNullOrWhiteSpace(e.Criteria.Channel) ? null : e.Criteria.Channel.Trim(), MaxSpread = e.MaxMarginEa,
            KeepsMarginOnRollover = e.KeepsMarginOnRollover, Reason = LectorDeCdat.Recortar(e.Reason, NegotiatedRateRule.LargoDelMotivo),
        };
        db.NegotiatedRateRules.Add(regla);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ReglasDeTasaNegociada.DtosAsync(db, [regla], ct))[0]);
    }
}

/// <summary>
/// Termina una regla de tasa negociada (api.md §3, <c>POST /…/negotiated-rate-rules/{ruleId}/end</c>): una versión nueva del mismo código, inactiva, desde el día
/// siguiente a <c>validTo</c> (sin retroactividad). (nuevo)
/// </summary>
public sealed record EndNegotiatedRateRuleCommand(Guid LinePublicId, Guid RulePublicId, DateOnly ValidTo, string Reason)
    : IRequest<Result<NegotiatedRateRuleDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class EndNegotiatedRateRuleCommandValidator : ValidadorConMotivo<EndNegotiatedRateRuleCommand>
{
    public EndNegotiatedRateRuleCommandValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.RulePublicId).NotEmpty();
    }
}

public sealed class EndNegotiatedRateRuleCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<EndNegotiatedRateRuleCommand, Result<NegotiatedRateRuleDto>>
{
    public async Task<Result<NegotiatedRateRuleDto>> Handle(EndNegotiatedRateRuleCommand request, CancellationToken ct)
    {
        var linea = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, request.LinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<NegotiatedRateRuleDto>(linea.Error);
        var regla = await db.NegotiatedRateRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PublicId == request.RulePublicId && r.CreditLineId == linea.Value.Id && !r.IsDeleted, ct);
        if (regla is null) return Result.Failure<NegotiatedRateRuleDto>(new Error(ErroresDeCdat.CodigoReglaInexistente, "La regla de tasa negociada no existe."));
        var desde = request.ValidTo.AddDays(1);
        if (desde < reloj.HoyLocal)
            return Result.Failure<NegotiatedRateRuleDto>(new ErrorConDatos(ErroresDeCdat.CodigoReglaRetroactiva,
                "Una regla se termina desde hoy o después: no se termina hacia atrás.", new { validTo = request.ValidTo }));
        var ultima = await db.NegotiatedRateRules.AsNoTracking().Where(r => r.CreditLineId == regla.CreditLineId && r.Code == regla.Code && !r.IsDeleted)
            .MaxAsync(r => r.VersionNumber, ct);
        var fin = new NegotiatedRateRule
        {
            CreditLineId = regla.CreditLineId, Code = regla.Code, VersionNumber = ultima + 1, ValidFrom = desde, RegisteredAt = reloj.UtcNow, IsActive = false,
            Priority = regla.Priority, MaxSpread = regla.MaxSpread, Reason = LectorDeCdat.Recortar(request.Reason, NegotiatedRateRule.LargoDelMotivo),
        };
        db.NegotiatedRateRules.Add(fin);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ReglasDeTasaNegociada.DtosAsync(db, [fin], ct))[0]);
    }
}

/// <summary>Lo común de las reglas: su forma de respuesta. (nuevo)</summary>
public static class ReglasDeTasaNegociada
{
    public static async Task<IReadOnlyList<NegotiatedRateRuleDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<NegotiatedRateRule> reglas, CancellationToken ct)
    {
        var oficinas = reglas.Where(r => r.BranchId is not null).Select(r => r.BranchId!.Value).Distinct().ToList();
        var publicas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        return reglas.OrderBy(r => r.Priority).ThenBy(r => r.Code, StringComparer.Ordinal).Select(r => new NegotiatedRateRuleDto(r.PublicId, r.Code, r.VersionNumber,
            r.Priority,
            new NegotiatedRateCriteriaDto(r.MinSeniorityMonths, r.MinAmount, r.MaxAmount, r.MinTermDays, r.MaxTermDays, r.MinBaseRate, r.MaxBaseRate,
                r.MinHolderTotalBalance, r.MaxHolderTotalBalance, r.BranchId is { } o ? publicas.GetValueOrDefault(o) : null, r.Channel),
            r.MaxSpread, r.KeepsMarginOnRollover, r.ValidFrom, r.IsActive, r.Reason)).ToList();
    }
}
