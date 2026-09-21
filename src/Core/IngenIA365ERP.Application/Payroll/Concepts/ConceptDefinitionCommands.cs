using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Concepts;

/// <summary>
/// Los campos editables de una versión de concepto (contracts/api.md §6). Feature 010 (D-29):
/// <see cref="AffectsVacationBase"/> y <see cref="DianElement"/> van en el contrato; nulos al revisar
/// significan «como la versión anterior» (y una cadena vacía en <c>DianElement</c> la quita). Hasta el
/// 2026-09-21 no viajaban: una versión revisada de un concepto sembrado nacía sin base de vacaciones y
/// sin ruta DIAN hasta que la semilla la pisaba en el arranque siguiente, y un concepto propio nunca las tenía.
/// </summary>
public sealed record ConceptDefinitionInput
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ConceptNature Nature { get; init; }
    public CalculationKind CalculationKind { get; init; }
    public decimal? FixedAmount { get; init; }
    public string? AmountParameterCode { get; init; }
    public bool ProrateByDays { get; init; }
    public CalculationBase? BaseKind { get; init; }
    public decimal? Percent { get; init; }
    public string? PercentParameterCode { get; init; }
    public UnitKind? UnitKind { get; init; }
    public decimal? UnitFactor { get; init; }
    public string? TableParameterCode { get; init; }
    public string? ComponentConceptCodes { get; init; }
    public bool AffectsSalaryBase { get; init; }
    public bool AffectsContributionBase { get; init; }
    public bool AffectsBenefitsBase { get; init; }
    public bool AffectsWithholdingBase { get; init; }
    public bool IsBenefitRelated { get; init; }

    /// <summary>Entra a la base de vacaciones e indemnización (CST art. 192). Nulo al revisar = hereda.</summary>
    public bool? AffectsVacationBase { get; init; }

    /// <summary>Ruta en el XML de nómina electrónica (<c>Devengados/Basico</c>…). Nulo al revisar = hereda; vacío = sin ruta.</summary>
    public string? DianElement { get; init; }

    public bool AllowsRepeatInPeriod { get; init; }
    public decimal? MaxQuantity { get; init; }
    public decimal? MaxAmount { get; init; }
    public int ApplicableClasses { get; init; }
    public bool RequiresDates { get; init; }
    public bool RequiresQuantity { get; init; }
    public bool RequiresAmount { get; init; }
    public bool IsAutomatic { get; init; }
    public bool ReducesWorkedDays { get; init; }
    public DateTime ValidFrom { get; init; }

    public PayrollConceptDefinition ToEntity(ConceptOrigin origin, int? legacyConceptId, DateTime now, string? user) => new()
    {
        Code = Code.Trim().ToUpperInvariant(),
        Name = Name.Trim(),
        Nature = Nature,
        CalculationKind = CalculationKind,
        FixedAmount = FixedAmount,
        AmountParameterCode = Limpio(AmountParameterCode),
        ProrateByDays = ProrateByDays,
        BaseKind = BaseKind,
        Percent = Percent,
        PercentParameterCode = Limpio(PercentParameterCode),
        UnitKind = UnitKind,
        UnitFactor = UnitFactor,
        TableParameterCode = Limpio(TableParameterCode),
        ComponentConceptCodes = string.IsNullOrWhiteSpace(ComponentConceptCodes) ? null : ComponentConceptCodes.Trim(),
        AffectsSalaryBase = AffectsSalaryBase,
        AffectsContributionBase = AffectsContributionBase,
        AffectsBenefitsBase = AffectsBenefitsBase,
        AffectsWithholdingBase = AffectsWithholdingBase,
        IsBenefitRelated = IsBenefitRelated,
        AffectsVacationBase = AffectsVacationBase ?? false,
        DianElement = string.IsNullOrWhiteSpace(DianElement) ? null : DianElement.Trim(),
        AllowsRepeatInPeriod = AllowsRepeatInPeriod,
        MaxQuantity = MaxQuantity,
        MaxAmount = MaxAmount,
        ApplicableClasses = ApplicableClasses,
        RequiresDates = RequiresDates,
        RequiresQuantity = RequiresQuantity,
        RequiresAmount = RequiresAmount,
        IsAutomatic = IsAutomatic,
        ReducesWorkedDays = ReducesWorkedDays,
        Origin = origin,
        LegacyConceptId = legacyConceptId,
        ValidFrom = ValidFrom.Date,
        IsActive = true,
        CreatedAt = now,
        CreatedBy = user,
    };

    private static string? Limpio(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}

/// <summary>
/// FR-028: cada forma de cálculo exige sus campos; las referencias a parámetros legales y
/// a otros conceptos existen; los compuestos no forman ciclo. Cada rechazo nombra el
/// campo o el código.
/// </summary>
public static class ConceptDefinitionRules
{
    public static async Task<Result> ValidateAsync(IApplicationDbContext db, PayrollConceptDefinition candidate, CancellationToken ct)
    {
        var falta = CamposDeLaForma(candidate);
        if (falta is not null)
            return Result.Failure(new Error("Payroll.ConceptFormIncomplete", falta));

        var parametros = new[] { candidate.AmountParameterCode, candidate.PercentParameterCode, candidate.TableParameterCode }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Replace(WellKnownConceptCodes.WorkRiskClassPlaceholder, "I", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        if (parametros.Count > 0)
        {
            var existentes = await db.PayrollLegalParameters.AsNoTracking()
                .Where(p => parametros.Contains(p.Code))
                .Select(p => new { p.Code, p.Kind })
                .Distinct()
                .ToListAsync(ct);
            foreach (var code in parametros)
            {
                var existente = existentes.FirstOrDefault(e => e.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
                if (existente is null)
                    return Result.Failure(new Error("Payroll.ConceptReferenceNotFound", $"El parámetro legal {code} no existe. Créelo en Parámetros legales antes de referenciarlo."));
                if (code.Equals(candidate.TableParameterCode?.Replace(WellKnownConceptCodes.WorkRiskClassPlaceholder, "I", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)
                    && existente.Kind != LegalParameterKind.RangeTable)
                    return Result.Failure(new Error("Payroll.ConceptReferenceNotFound", $"El parámetro {code} no es una tabla por rangos."));
            }
        }

        if (candidate.CalculationKind == CalculationKind.CompositeOfConcepts)
        {
            IReadOnlyList<ConceptDependencyGraph.Component> componentes;
            try { componentes = ConceptDependencyGraph.ParseComponents(candidate.ComponentConceptCodes); }
            catch (CalculationRefusedException ex) { return Result.Failure(new Error("Payroll.ConceptFormIncomplete", ex.Message)); }

            var vigentes = await db.PayrollConceptDefinitions.AsNoTracking()
                .Where(c => c.IsActive && (c.ValidTo == null || c.ValidTo >= candidate.ValidFrom))
                .ToListAsync(ct);
            foreach (var comp in componentes)
            {
                if (!vigentes.Any(v => v.Code.Equals(comp.Code, StringComparison.OrdinalIgnoreCase)))
                    return Result.Failure(new Error("Payroll.ConceptReferenceNotFound", $"El componente {comp.Code} no existe como concepto vigente."));
            }
            var grafo = vigentes.Where(v => !v.Code.Equals(candidate.Code, StringComparison.OrdinalIgnoreCase)).Append(candidate);
            var ciclo = ConceptDependencyGraph.FindCycle(grafo);
            if (ciclo is not null)
                return Result.Failure(new Error("Payroll.ConceptCycle", ciclo));
        }

        return Result.Success();
    }

    private static string? CamposDeLaForma(PayrollConceptDefinition c) => c.CalculationKind switch
    {
        CalculationKind.FixedAmount when c.FixedAmount is null && c.AmountParameterCode is null && !c.RequiresAmount =>
            "Valor fijo: indique el valor fijo, un parámetro legal que lo aporte o que la novedad traiga el valor.",
        CalculationKind.PercentOfBase when c.BaseKind is null => "Porcentaje sobre base: falta la base.",
        CalculationKind.PercentOfBase when c.Percent is null && c.PercentParameterCode is null => "Porcentaje sobre base: indique el porcentaje o el parámetro legal que lo aporta.",
        CalculationKind.QuantityTimesUnit when c.UnitKind is null => "Cantidad × unidad: falta la unidad (hora ordinaria, día u hora con recargo).",
        CalculationKind.QuantityTimesUnit when !c.RequiresQuantity && !c.RequiresDates && !c.IsAutomatic => "Cantidad × unidad: la novedad debe traer cantidad o fechas, o el concepto ser automático.",
        CalculationKind.RangeTable when c.BaseKind is null => "Tabla por rangos: falta la base.",
        CalculationKind.RangeTable when c.TableParameterCode is null => "Tabla por rangos: falta el parámetro legal de tipo tabla.",
        CalculationKind.CompositeOfConcepts when string.IsNullOrWhiteSpace(c.ComponentConceptCodes) => "Suma de conceptos: indique los componentes («+CODIGO*peso;…»).",
        _ => null,
    };
}

// -------------------------------------------------------------------- crear --

public sealed record CreateConceptDefinitionCommand(ConceptDefinitionInput Definition) : IRequest<Result<Guid>>;

public sealed class CreateConceptDefinitionCommandValidator : AbstractValidator<CreateConceptDefinitionCommand>
{
    public CreateConceptDefinitionCommandValidator()
    {
        RuleFor(x => x.Definition).NotNull();
        RuleFor(x => x.Definition).SetValidator(new ConceptDefinitionInputValidator());
    }
}

public sealed class ConceptDefinitionInputValidator : AbstractValidator<ConceptDefinitionInput>
{
    public ConceptDefinitionInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código es obligatorio.")
            .Matches("^[A-Za-z0-9_]{2,30}$").WithMessage("El código admite letras, dígitos y guion bajo (2 a 30).");
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(120);
        RuleFor(x => x.Nature).IsInEnum();
        RuleFor(x => x.CalculationKind).IsInEnum();
        RuleFor(x => x.ValidFrom).NotEmpty().WithMessage("La vigencia desde es obligatoria.");
        RuleFor(x => x.Percent).InclusiveBetween(0m, 100m).When(x => x.Percent is not null);
        RuleFor(x => x.UnitFactor).GreaterThan(0m).When(x => x.UnitFactor is not null);
        RuleFor(x => x.MaxQuantity).GreaterThan(0m).When(x => x.MaxQuantity is not null);
        RuleFor(x => x.MaxAmount).GreaterThan(0m).When(x => x.MaxAmount is not null);
        RuleFor(x => x.ComponentConceptCodes).MaximumLength(400);
        RuleFor(x => x.DianElement).MaximumLength(60).WithMessage("La ruta DIAN admite hasta 60 caracteres.")
            .Matches("^[A-Za-z0-9@]+(/[A-Za-z0-9@]+)*$").WithMessage("La ruta DIAN es una ruta del XML, como Devengados/Basico o Deducciones/Salud.")
            .When(x => !string.IsNullOrWhiteSpace(x.DianElement));
    }
}

public sealed class CreateConceptDefinitionCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IPayrollRunStaleMarker stale)
    : IRequestHandler<CreateConceptDefinitionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateConceptDefinitionCommand request, CancellationToken ct)
    {
        var entity = request.Definition.ToEntity(ConceptOrigin.Custom, null, clock.UtcNow, user.UserName);
        var existe = await db.PayrollConceptDefinitions.IgnoreQueryFilters().AnyAsync(c => c.Code == entity.Code, ct);
        if (existe)
            return Result.Failure<Guid>(new Error("Payroll.ConceptCodeDuplicate", $"Ya existe un concepto con el código {entity.Code}; para cambiarlo, registre una nueva versión."));

        var reglas = await ConceptDefinitionRules.ValidateAsync(db, entity, ct);
        if (reglas.IsFailure) return Result.Failure<Guid>(reglas.Error);

        db.PayrollConceptDefinitions.Add(entity);
        await stale.MarkAllDraftsStaleAsync($"concepto {entity.Code} creado", ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(entity.PublicId);
    }
}

// ------------------------------------------------------------------ revisar --

/// <summary>FR-029: revisar crea una versión nueva desde <c>ValidFrom</c> y cierra la anterior el día antes. El código no cambia.</summary>
public sealed record ReviseConceptDefinitionCommand(string Code, ConceptDefinitionInput Definition) : IRequest<Result<Guid>>;

public sealed class ReviseConceptDefinitionCommandValidator : AbstractValidator<ReviseConceptDefinitionCommand>
{
    public ReviseConceptDefinitionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Definition).NotNull().SetValidator(new ConceptDefinitionInputValidator());
    }
}

public sealed class ReviseConceptDefinitionCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IPayrollRunStaleMarker stale)
    : IRequestHandler<ReviseConceptDefinitionCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ReviseConceptDefinitionCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var versiones = await db.PayrollConceptDefinitions.Where(c => c.Code == code).OrderByDescending(c => c.ValidFrom).ToListAsync(ct);
        if (versiones.Count == 0)
            return Result.Failure<Guid>(new Error("Payroll.ConceptNotFound", $"No existe el concepto {code}."));
        if (!request.Definition.Code.Trim().Equals(code, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<Guid>(new Error("Payroll.ConceptSeedProtected", "El código de un concepto no cambia entre versiones: cree otro concepto."));

        var actual = versiones[0];
        var desde = request.Definition.ValidFrom.Date;
        if (desde <= actual.ValidFrom)
            return Result.Failure<Guid>(new Error("Payroll.ConceptVersionOverlap",
                $"La versión nueva debe empezar después del {actual.ValidFrom:dd/MM/yyyy}, inicio de la versión vigente."));

        var nueva = request.Definition.ToEntity(actual.Origin, actual.LegacyConceptId, clock.UtcNow, user.UserName);
        // Lo que el cliente no manda se hereda de la versión vigente (D-29): la base de vacaciones y la ruta
        // DIAN no cambian por revisar un nombre o un factor.
        if (request.Definition.AffectsVacationBase is null) nueva.AffectsVacationBase = actual.AffectsVacationBase;
        if (request.Definition.DianElement is null) nueva.DianElement = actual.DianElement;
        var reglas = await ConceptDefinitionRules.ValidateAsync(db, nueva, ct);
        if (reglas.IsFailure) return Result.Failure<Guid>(reglas.Error);

        actual.ValidTo = desde.AddDays(-1);
        actual.UpdatedAt = clock.UtcNow;
        actual.UpdatedBy = user.UserName;
        db.PayrollConceptDefinitions.Add(nueva);
        await stale.MarkAllDraftsStaleAsync($"concepto {code} revisado desde {desde:yyyy-MM-dd}", ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(nueva.PublicId);
    }
}

// --------------------------------------------------------------- desactivar --

/// <summary>Desactivar cierra la vigencia; nunca borra (un concepto de semilla tampoco).</summary>
public sealed record DeactivateConceptDefinitionCommand(string Code, DateTime ValidTo) : IRequest<Result>;

public sealed class DeactivateConceptDefinitionCommandValidator : AbstractValidator<DeactivateConceptDefinitionCommand>
{
    public DeactivateConceptDefinitionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.ValidTo).NotEmpty();
    }
}

public sealed class DeactivateConceptDefinitionCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IPayrollRunStaleMarker stale)
    : IRequestHandler<DeactivateConceptDefinitionCommand, Result>
{
    public async Task<Result> Handle(DeactivateConceptDefinitionCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var actual = await db.PayrollConceptDefinitions.Where(c => c.Code == code).OrderByDescending(c => c.ValidFrom).FirstOrDefaultAsync(ct);
        if (actual is null)
            return Result.Failure(new Error("Payroll.ConceptNotFound", $"No existe el concepto {code}."));
        if (code.Equals(WellKnownConceptCodes.BasicSalary, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new Error("Payroll.ConceptSeedProtected", "El salario básico no se desactiva: sin él no hay liquidación."));

        var hasta = request.ValidTo.Date;
        if (hasta < actual.ValidFrom)
            return Result.Failure(new Error("Payroll.ConceptVersionOverlap", $"La fecha de cierre no puede ser anterior al inicio de la versión ({actual.ValidFrom:dd/MM/yyyy})."));

        actual.ValidTo = hasta;
        if (hasta < clock.UtcNow.Date) actual.IsActive = false;
        actual.UpdatedAt = clock.UtcNow;
        actual.UpdatedBy = user.UserName;
        await stale.MarkAllDraftsStaleAsync($"concepto {code} desactivado desde {hasta:yyyy-MM-dd}", ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ------------------------------------------------------------------ cuentas --

public sealed record ConceptAccountRow(Guid? CostCenterPublicId, string DebitAccountCode, string CreditAccountCode);

/// <summary>Cuentas por concepto (y por centro de costo), con FK a <c>ChartOfAccounts</c> resueltas por código de cuenta (H-12).</summary>
public sealed record SetConceptAccountsCommand(string Code, IReadOnlyList<ConceptAccountRow> Rows) : IRequest<Result>;

public sealed class SetConceptAccountsCommandValidator : AbstractValidator<SetConceptAccountsCommand>
{
    public SetConceptAccountsCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Rows).NotNull();
        RuleForEach(x => x.Rows).ChildRules(r =>
        {
            r.RuleFor(x => x.DebitAccountCode).NotEmpty().WithMessage("La cuenta débito es obligatoria.");
            r.RuleFor(x => x.CreditAccountCode).NotEmpty().WithMessage("La cuenta crédito es obligatoria.");
        });
        RuleFor(x => x.Rows).Must(rows => rows.Select(r => r.CostCenterPublicId).Distinct().Count() == rows.Count)
            .WithMessage("No repita el centro de costo (una fila por defecto y una por centro).");
    }
}

public sealed class SetConceptAccountsCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<SetConceptAccountsCommand, Result>
{
    public async Task<Result> Handle(SetConceptAccountsCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var concepto = await db.PayrollConceptDefinitions.AsNoTracking().Where(c => c.Code == code).Select(c => new { c.Nature }).FirstOrDefaultAsync(ct);
        if (concepto is null)
            return Result.Failure(new Error("Payroll.ConceptNotFound", $"No existe el concepto {code}."));

        var codigosCuenta = request.Rows.SelectMany(r => new[] { r.DebitAccountCode.Trim(), r.CreditAccountCode.Trim() }).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => codigosCuenta.Contains(a.Code) && !a.IsDeleted).ToListAsync(ct);
        var faltan = codigosCuenta.Where(c => !cuentas.Any(x => x.Code == c)).ToList();
        if (faltan.Count > 0)
            return Result.Failure(new Error("Payroll.AccountNotFound", $"Cuentas contables inexistentes: {string.Join(", ", faltan)}."));
        // Feature 009 (FR-016): una parametrizacion solo admite cuentas de movimiento, activas y habilitadas para Nomina.
        foreach (var cuenta in cuentas)
        {
            if (Accounting.Accounts.AccountEligibility.Reparo(cuenta, Accounting.Posting.ModuloContable.Nomina) is { } reparo)
                return Result.Failure(Accounting.Posting.AccountingErrors.AccountNotEligible(cuenta.Code, Accounting.Posting.ModuloContable.Nomina, reparo));
        }
        // Feature 009 (FR-088): en aportes y provisiones el tercero es la entidad (EPS, fondo, ARL, caja) vinculada al
        // empleado; si la cuenta lo exige, toda entidad del catálogo tiene que tener su persona antes de parametrizar.
        var entidad = Services.TercerosDeNomina.EntidadDe(code, concepto.Nature);
        if (entidad != Services.EntidadInstitucional.Ninguna && cuentas.Any(c => c.RequiresThirdParty))
        {
            var sinVinculo = await Services.VinculosInstitucionales.SinPersonaAsync(db, entidad, ct);
            if (sinVinculo is not null)
                return Result.Failure(new ErrorConDatos(Accounting.Posting.AccountingErrors.InstitutionalLinkMissing(sinVinculo).Code,
                    $"{Accounting.Posting.AccountingErrors.InstitutionalLinkMissing(sinVinculo).Message} Se vincula en {Services.TercerosDeNomina.Pantalla(entidad)}.",
                    new { entity = sinVinculo, entityKind = entidad.ToString() }));
        }
        var cuentaPorCodigo = cuentas.ToDictionary(a => a.Code, a => a.Id);

        var ccIds = request.Rows.Where(r => r.CostCenterPublicId is not null).Select(r => r.CostCenterPublicId!.Value).Distinct().ToList();
        var centros = await db.CostCenters.AsNoTracking().Where(c => ccIds.Contains(c.PublicId)).Select(c => new { c.Id, c.PublicId }).ToListAsync(ct);
        if (centros.Count != ccIds.Count)
            return Result.Failure(new Error("Payroll.CostCenterNotFound", "Alguno de los centros de costo indicados no existe."));
        var ccPorPublicId = centros.ToDictionary(c => c.PublicId, c => c.Id);

        var ahora = clock.UtcNow;
        var existentes = await db.PayrollConceptDefinitionAccounts.Where(a => a.ConceptCode == code).ToListAsync(ct);
        var deseadas = request.Rows.Select(r => new
        {
            CostCenterId = r.CostCenterPublicId is { } pid ? (int?)ccPorPublicId[pid] : null,
            Debit = cuentaPorCodigo[r.DebitAccountCode.Trim()],
            Credit = cuentaPorCodigo[r.CreditAccountCode.Trim()],
        }).ToList();

        foreach (var fila in existentes)
        {
            var deseada = deseadas.FirstOrDefault(d => d.CostCenterId == fila.CostCenterId);
            if (deseada is null)
            {
                // Retirar = marcar; la fila sigue para la historia de lo aprobado con ella (Principio VII).
                fila.IsDeleted = true;
                fila.DeletedAt = ahora;
                fila.DeletedBy = user.UserName;
                continue;
            }
            fila.DebitAccountId = deseada.Debit;
            fila.CreditAccountId = deseada.Credit;
            fila.UpdatedAt = ahora;
            fila.UpdatedBy = user.UserName;
        }
        foreach (var d in deseadas.Where(d => !existentes.Any(e => e.CostCenterId == d.CostCenterId)))
        {
            db.PayrollConceptDefinitionAccounts.Add(new PayrollConceptDefinitionAccount
            {
                ConceptCode = code, CostCenterId = d.CostCenterId, DebitAccountId = d.Debit, CreditAccountId = d.Credit,
                CreatedAt = ahora, CreatedBy = user.UserName,
            });
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ------------------------------------------------------------ reaplicar semilla --

/// <summary>Lo que Persistence implementa para volver a sembrar conceptos y parámetros de nómina en la cooperativa activa sin tocar lo existente.</summary>
public interface IPayrollSeedApplier
{
    Task<IReadOnlyList<(string Seeder, int Inserted)>> ReapplyAsync(CancellationToken ct);
}

public sealed record ReapplyConceptSeedCommand : IRequest<Result<IReadOnlyList<SeedReapplyEntryDto>>>;

public sealed record SeedReapplyEntryDto(string Seeder, int Inserted);

public sealed class ReapplyConceptSeedCommandValidator : AbstractValidator<ReapplyConceptSeedCommand>;

public sealed class ReapplyConceptSeedCommandHandler(IPayrollSeedApplier applier)
    : IRequestHandler<ReapplyConceptSeedCommand, Result<IReadOnlyList<SeedReapplyEntryDto>>>
{
    public async Task<Result<IReadOnlyList<SeedReapplyEntryDto>>> Handle(ReapplyConceptSeedCommand request, CancellationToken ct)
    {
        var resultado = await applier.ReapplyAsync(ct);
        return Result.Success<IReadOnlyList<SeedReapplyEntryDto>>(resultado.Select(r => new SeedReapplyEntryDto(r.Seeder, r.Inserted)).ToList());
    }
}
