using FluentValidation;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Budgets;

// Escritura del presupuesto (feature 009 E2, US9, FR-061..FR-064; contracts/api.md §10). Las
// reglas de versionado viven en un solo sitio, EscrituraDePresupuesto.AplicarAsync: un borrador
// se corrige en su sitio; un aprobado no se toca —se crea la versión siguiente, ya aprobada, con
// el motivo, y la anterior queda Superseded— para que el informe pueda mostrar el presupuesto
// inicial (versión 1) junto al vigente. El presupuesto no escribe asientos: no toca el libro.
//
// Alcance de sucursal (FR-035, decisión 6): quien tiene sucursales asignadas sólo ve y escribe
// líneas de esas sucursales; una línea sin sucursal es de la empresa y siempre entra. Los comandos
// que reemplazan líneas (modificar, copiar) conservan intactas las que están fuera del alcance:
// si las retiraran, el usuario de una sucursal borraría, sin verlo, lo presupuestado para otra.

// ------------------------------------------------------------------------------------- crear --

/// <summary>Crea la versión 1 del año en borrador. El ejercicio tiene que existir y el año no puede tener ya presupuesto.</summary>
public sealed record CreateBudgetCommand(int Year, IReadOnlyList<BudgetLineInput> Lines) : IRequest<Result<BudgetDto>>;

public sealed class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
{
    public CreateBudgetCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(DateTime.UnixEpoch.Year, DateTime.MaxValue.Year);
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.Amounts).NotNull());
    }
}

public sealed class CreateBudgetCommandHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<CreateBudgetCommand, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(CreateBudgetCommand request, CancellationToken ct)
    {
        var ejercicio = await EscrituraDePresupuesto.EjercicioAsync(db, request.Year, ct);
        if (ejercicio is null) return Result.Failure<BudgetDto>(BudgetErrors.FiscalYearNotFound);
        if (await db.Budgets.AnyAsync(b => b.FiscalYearId == ejercicio.Id && !b.IsDeleted, ct)) return Result.Failure<BudgetDto>(BudgetErrors.AlreadyExists);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var sucursales = await alcance.ObtenerAsync(ct);
        var lineas = await LineasDePresupuesto.ResolverAsync(db, request.Lines, sucursales, ahora, quien, ct);
        if (lineas.IsFailure) return Result.Failure<BudgetDto>(lineas.Error);

        var presupuesto = EscrituraDePresupuesto.NuevaVersion(ejercicio, 1, BudgetStatus.Draft, null, lineas.Value, ahora, quien);
        db.Budgets.Add(presupuesto);
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Budget.Created", nameof(Budget), presupuesto.PublicId, null,
            new { request.Year, version = 1, status = presupuesto.Status.ToString(), lines = request.Lines.Count }, ct);
        return await ArmadoDePresupuesto.ArmarAsync(db, request.Year, null, sucursales, ct);
    }
}

// --------------------------------------------------------------------------------- modificar --

/// <summary>
/// Reemplaza las líneas de la vigente. Sobre un borrador, en su sitio; sobre una aprobada exige
/// <see cref="Reason"/> y crea la versión siguiente, aprobada, dejando la anterior <c>Superseded</c>.
/// Con alcance de sucursal, sólo reemplaza las líneas de las sucursales asignadas (y las de la
/// empresa); las demás siguen como estaban.
/// </summary>
public sealed record UpdateBudgetCommand(int Year, IReadOnlyList<BudgetLineInput> Lines, string? Reason) : IRequest<Result<BudgetDto>>;

public sealed class UpdateBudgetCommandValidator : AbstractValidator<UpdateBudgetCommand>
{
    public UpdateBudgetCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(DateTime.UnixEpoch.Year, DateTime.MaxValue.Year);
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(x => x.Amounts).NotNull());
        RuleFor(x => x.Reason).MaximumLength(200);
    }
}

public sealed class UpdateBudgetCommandHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<UpdateBudgetCommand, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(UpdateBudgetCommand request, CancellationToken ct)
    {
        var ejercicio = await EscrituraDePresupuesto.EjercicioAsync(db, request.Year, ct);
        if (ejercicio is null) return Result.Failure<BudgetDto>(BudgetErrors.FiscalYearNotFound);
        var vigente = await ArmadoDePresupuesto.VigenteAsync(db, ejercicio.Id, ct);
        if (vigente is null) return Result.Failure<BudgetDto>(BudgetErrors.NotFound);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var sucursales = await alcance.ObtenerAsync(ct);
        var lineas = await LineasDePresupuesto.ResolverAsync(db, request.Lines, sucursales, ahora, quien, ct);
        if (lineas.IsFailure) return Result.Failure<BudgetDto>(lineas.Error);

        var (dentro, fuera) = LineasDePresupuesto.PartirPorAlcance(vigente.Lineas, sucursales);
        var antes = new { version = vigente.Presupuesto.Version, status = vigente.Presupuesto.Status.ToString() };
        var r = EscrituraDePresupuesto.Aplicar(db, ejercicio, vigente, conservar: fuera, retirar: dentro, agregar: lineas.Value,
            request.Reason, permitirVersionar: true, ahora, quien);
        if (r.IsFailure) return Result.Failure<BudgetDto>(r.Error);
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Budget.Updated", nameof(Budget), r.Value.PublicId, antes,
            new { request.Year, version = r.Value.Version, status = r.Value.Status.ToString(), reason = r.Value.ChangeReason, lines = request.Lines.Count }, ct);
        return await ArmadoDePresupuesto.ArmarAsync(db, request.Year, null, sucursales, ct);
    }
}

// ----------------------------------------------------------------------------------- aprobar --

/// <summary>Borrador → aprobado, con quién y cuándo. Desde ahí cualquier cambio versiona.</summary>
public sealed record ApproveBudgetCommand(int Year) : IRequest<Result<BudgetDto>>;

public sealed class ApproveBudgetCommandValidator : AbstractValidator<ApproveBudgetCommand>
{
    public ApproveBudgetCommandValidator() => RuleFor(x => x.Year).InclusiveBetween(DateTime.UnixEpoch.Year, DateTime.MaxValue.Year);
}

public sealed class ApproveBudgetCommandHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<ApproveBudgetCommand, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(ApproveBudgetCommand request, CancellationToken ct)
    {
        var ejercicio = await EscrituraDePresupuesto.EjercicioAsync(db, request.Year, ct);
        if (ejercicio is null) return Result.Failure<BudgetDto>(BudgetErrors.FiscalYearNotFound);
        var vigente = await ArmadoDePresupuesto.VigenteAsync(db, ejercicio.Id, ct);
        if (vigente is null) return Result.Failure<BudgetDto>(BudgetErrors.NotFound);
        if (vigente.Presupuesto.Status != BudgetStatus.Draft) return Result.Failure<BudgetDto>(BudgetErrors.NotDraft);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var p = vigente.Presupuesto;
        p.Status = BudgetStatus.Approved;
        p.ApprovedAt = ahora;
        p.ApprovedBy = quien;
        p.UpdatedAt = ahora;
        p.UpdatedBy = quien;
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Budget.Approved", nameof(Budget), p.PublicId, new { status = "Draft" },
            new { request.Year, p.Version, status = "Approved", approvedBy = quien }, ct);
        return await ArmadoDePresupuesto.ArmarAsync(db, request.Year, null, await alcance.ObtenerAsync(ct), ct);
    }
}

// ------------------------------------------------------------------------------------ copiar --

/// <summary>
/// Copia la vigente de otro año como borrador de éste, ajustada en un porcentaje y redondeada a
/// pesos (<c>Math.Round(x, 0, AwayFromZero)</c>). Crea la versión 1 si el año no tiene presupuesto,
/// reemplaza el borrador si lo tiene y se niega si la vigente ya está aprobada (<c>NotDraft</c>):
/// copiar encima de un aprobado no es una corrección con motivo, es empezar de nuevo.
/// </summary>
public sealed record CopyBudgetCommand(int Year, int PreviousYear, decimal AdjustPercent) : IRequest<Result<BudgetDto>>;

public sealed class CopyBudgetCommandValidator : AbstractValidator<CopyBudgetCommand>
{
    /// <summary>Más de diez veces el presupuesto anterior no es un ajuste; y con ese tope <c>Amount × factor</c> nunca desborda el <c>decimal</c>.</summary>
    public const decimal AjusteMaximo = 1000m;

    public CopyBudgetCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(DateTime.UnixEpoch.Year, DateTime.MaxValue.Year);
        RuleFor(x => x.PreviousYear).InclusiveBetween(DateTime.UnixEpoch.Year, DateTime.MaxValue.Year).NotEqual(x => x.Year)
            .WithMessage("El año del que se copia debe ser distinto del año destino.");
        // Un ajuste de −100 % o menos deja el presupuesto en cero o lo vuelve negativo: no es un ajuste, es borrarlo.
        RuleFor(x => x.AdjustPercent).GreaterThan(-100m).LessThanOrEqualTo(AjusteMaximo)
            .WithMessage($"El ajuste va entre −100 % (exclusivo) y {AjusteMaximo:N0} %.");
    }
}

public sealed class CopyBudgetCommandHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<CopyBudgetCommand, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(CopyBudgetCommand request, CancellationToken ct)
    {
        var ejercicio = await EscrituraDePresupuesto.EjercicioAsync(db, request.Year, ct);
        if (ejercicio is null) return Result.Failure<BudgetDto>(BudgetErrors.FiscalYearNotFound);
        var origen = await EscrituraDePresupuesto.EjercicioAsync(db, request.PreviousYear, ct);
        var fuente = origen is null ? null : await ArmadoDePresupuesto.VigenteAsync(db, origen.Id, ct);
        if (fuente is null) return Result.Failure<BudgetDto>(BudgetErrors.SourceNotFound);

        var vigente = await ArmadoDePresupuesto.VigenteAsync(db, ejercicio.Id, ct);
        if (vigente is not null && vigente.Presupuesto.Status != BudgetStatus.Draft) return Result.Failure<BudgetDto>(BudgetErrors.NotDraft);

        // La copia trae el año entero: quien sólo ve algunas sucursales no puede copiar lo de las otras.
        var sucursales = await alcance.ObtenerAsync(ct);
        if (fuente.Lineas.Any(l => l.BranchId is { } s && !sucursales.Permite(s))) return Result.Failure<BudgetDto>(BudgetErrors.SourceOutOfScope(request.PreviousYear));

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var factor = 1m + request.AdjustPercent / 100m;
        var copiadas = fuente.Lineas
            .Select(l => LineasDePresupuesto.Nueva(l.AccountId, l.BranchId, l.CostCenterId, l.Month, LineasDePresupuesto.APesos(l.Amount * factor), ahora, quien))
            .Where(l => l.Amount != 0m)
            .ToList();
        if (copiadas.Any(l => l.Amount > LineasDePresupuesto.TopeDeMonto)) return Result.Failure<BudgetDto>(BudgetErrors.AmountTooLarge);

        Budget destino;
        if (vigente is null)
        {
            destino = EscrituraDePresupuesto.NuevaVersion(ejercicio, 1, BudgetStatus.Draft, null, copiadas, ahora, quien);
            db.Budgets.Add(destino);
        }
        else
        {
            var (dentro, fuera) = LineasDePresupuesto.PartirPorAlcance(vigente.Lineas, sucursales);
            var r = EscrituraDePresupuesto.Aplicar(db, ejercicio, vigente, conservar: fuera, retirar: dentro, agregar: copiadas, null, permitirVersionar: false, ahora, quien);
            if (r.IsFailure) return Result.Failure<BudgetDto>(r.Error);
            destino = r.Value;
        }
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Budget.Copied", nameof(Budget), destino.PublicId, null,
            new { request.Year, from = request.PreviousYear, fromVersion = fuente.Presupuesto.Version, adjustPercent = request.AdjustPercent, lines = copiadas.Count }, ct);
        return await ArmadoDePresupuesto.ArmarAsync(db, request.Year, null, sucursales, ct);
    }
}

// -------------------------------------------------------------------------------- distribuir --

/// <summary>
/// Reparte un total anual en las doce cuotas de una (cuenta, sucursal, centro) y las guarda en la
/// vigente con las mismas reglas que modificar (borrador en su sitio; aprobado exige motivo y
/// versiona). Si el año no tiene presupuesto, nace el borrador con esa sola cuenta: la pantalla
/// distribuye antes de guardar nada. Modos: <c>equal</c> (total/12 a pesos, la diferencia en
/// diciembre), <c>percent</c> (porcentajes que suman 100 ± 0,01; la diferencia en el último mes con
/// porcentaje) y <c>manual</c> (pesos que deben sumar el total).
/// </summary>
public sealed record DistributeBudgetCommand(int Year, Guid AccountPublicId, Guid? BranchPublicId, Guid? CostCenterPublicId,
    decimal Total, string Mode /* equal|manual|percent */, IReadOnlyList<decimal>? Values, string? Reason) : IRequest<Result<BudgetDto>>;

public sealed class DistributeBudgetCommandValidator : AbstractValidator<DistributeBudgetCommand>
{
    public DistributeBudgetCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(DateTime.UnixEpoch.Year, DateTime.MaxValue.Year);
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Total).GreaterThan(0m).LessThanOrEqualTo(LineasDePresupuesto.TopeDeMonto)
            .WithMessage($"El total a distribuir va entre 1 y {LineasDePresupuesto.TopeDeMonto:N0} pesos.");
        RuleFor(x => x.Mode).NotEmpty().Must(m => DistribucionDeCuotas.Modos.Contains(m ?? string.Empty))
            .WithMessage("El modo de distribución es equal, percent o manual.");
        RuleFor(x => x.Reason).MaximumLength(200);
    }
}

public sealed class DistributeBudgetCommandHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<DistributeBudgetCommand, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(DistributeBudgetCommand request, CancellationToken ct)
    {
        var cuotas = DistribucionDeCuotas.Calcular(request.Total, request.Mode, request.Values);
        if (cuotas.IsFailure) return Result.Failure<BudgetDto>(cuotas.Error);

        var ejercicio = await EscrituraDePresupuesto.EjercicioAsync(db, request.Year, ct);
        if (ejercicio is null) return Result.Failure<BudgetDto>(BudgetErrors.FiscalYearNotFound);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var sucursales = await alcance.ObtenerAsync(ct);
        var lineas = await LineasDePresupuesto.ResolverAsync(db, [new BudgetLineInput(request.AccountPublicId, request.BranchPublicId, request.CostCenterPublicId, cuotas.Value)], sucursales, ahora, quien, ct);
        if (lineas.IsFailure) return Result.Failure<BudgetDto>(lineas.Error);
        var nuevas = lineas.Value;
        var clave = nuevas.Count > 0 ? (nuevas[0].AccountId, nuevas[0].BranchId, nuevas[0].CostCenterId) : default;

        var vigente = await ArmadoDePresupuesto.VigenteAsync(db, ejercicio.Id, ct);
        Budget destino;
        if (vigente is null)
        {
            destino = EscrituraDePresupuesto.NuevaVersion(ejercicio, 1, BudgetStatus.Draft, null, nuevas, ahora, quien);
            db.Budgets.Add(destino);
        }
        else
        {
            var mismaClave = vigente.Lineas.Where(l => (l.AccountId, l.BranchId, l.CostCenterId) == clave).ToList();
            var otras = vigente.Lineas.Except(mismaClave).ToList();
            var r = EscrituraDePresupuesto.Aplicar(db, ejercicio, vigente, conservar: otras, retirar: mismaClave, agregar: nuevas, request.Reason, permitirVersionar: true, ahora, quien);
            if (r.IsFailure) return Result.Failure<BudgetDto>(r.Error);
            destino = r.Value;
        }
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Budget.Distributed", nameof(Budget), destino.PublicId, null,
            new { request.Year, version = destino.Version, status = destino.Status.ToString(), request.AccountPublicId, request.Total, mode = request.Mode, amounts = cuotas.Value, reason = destino.ChangeReason }, ct);
        return await ArmadoDePresupuesto.ArmarAsync(db, request.Year, null, sucursales, ct);
    }
}

/// <summary>Las doce cuotas de un total anual. Puro: sin base de datos, para probarse con números a mano.</summary>
public static class DistribucionDeCuotas
{
    public const string Igual = "equal";
    public const string Porcentual = "percent";
    public const string Manual = "manual";
    public static readonly HashSet<string> Modos = new(StringComparer.OrdinalIgnoreCase) { Igual, Porcentual, Manual };

    /// <summary>Los porcentajes se aceptan si suman 100 con esta tolerancia (redondeos de la pantalla).</summary>
    public const decimal ToleranciaPorcentual = 0.01m;

    /// <summary>
    /// El total y las cuotas van en pesos enteros y bajo el tope; los porcentajes sí admiten
    /// decimales (33,33 %) porque la cuota que sale de ellos se redondea a pesos y la diferencia
    /// va al último mes con porcentaje. Se comprueba antes de sumar: con doce valores sin tope,
    /// la suma misma podía desbordar el <c>decimal</c>.
    /// </summary>
    public static Result<IReadOnlyList<decimal>> Calcular(decimal total, string mode, IReadOnlyList<decimal>? values)
    {
        if (total <= 0m) return Fallo("el total debe ser mayor que cero.");
        if (total > LineasDePresupuesto.TopeDeMonto) return Result.Failure<IReadOnlyList<decimal>>(BudgetErrors.AmountTooLarge);
        if (!LineasDePresupuesto.EsEnPesos(total)) return Result.Failure<IReadOnlyList<decimal>>(BudgetErrors.DecimalsNotAllowed);
        var meses = ArmadoDePresupuesto.Meses;
        var cuotas = new decimal[meses];

        if (string.Equals(mode, Igual, StringComparison.OrdinalIgnoreCase))
        {
            var cuota = LineasDePresupuesto.APesos(total / meses);
            for (var i = 0; i < meses - 1; i++) cuotas[i] = cuota;
            cuotas[meses - 1] = total - cuota * (meses - 1);
            return Result.Success<IReadOnlyList<decimal>>(cuotas);
        }

        if (values is null || values.Count != meses) return Fallo($"se necesitan {meses} valores, uno por mes.");
        if (values.Any(v => v < 0m)) return Result.Failure<IReadOnlyList<decimal>>(BudgetErrors.NegativeAmount);
        if (values.Any(v => v > LineasDePresupuesto.TopeDeMonto)) return Result.Failure<IReadOnlyList<decimal>>(BudgetErrors.AmountTooLarge);

        if (string.Equals(mode, Porcentual, StringComparison.OrdinalIgnoreCase))
        {
            var suma = values.Sum();
            if (Math.Abs(suma - 100m) > ToleranciaPorcentual) return Fallo($"los porcentajes suman {suma:N2} y deben sumar 100.");
            for (var i = 0; i < meses; i++) cuotas[i] = LineasDePresupuesto.APesos(total * values[i] / 100m);
            var ultimoConPorcentaje = Enumerable.Range(0, meses).Last(i => values[i] > 0m);
            cuotas[ultimoConPorcentaje] += total - cuotas.Sum();
            return Result.Success<IReadOnlyList<decimal>>(cuotas);
        }

        if (string.Equals(mode, Manual, StringComparison.OrdinalIgnoreCase))
        {
            if (values.Any(v => !LineasDePresupuesto.EsEnPesos(v))) return Result.Failure<IReadOnlyList<decimal>>(BudgetErrors.DecimalsNotAllowed);
            var suma = values.Sum();
            if (suma != total) return Fallo($"los valores suman {suma:N2} y el total es {total:N2}.");
            return Result.Success<IReadOnlyList<decimal>>(values.ToArray());
        }

        return Fallo($"el modo «{mode}» no existe; use equal, percent o manual.");
    }

    private static Result<IReadOnlyList<decimal>> Fallo(string detalle) => Result.Failure<IReadOnlyList<decimal>>(BudgetErrors.InvalidDistribution(detalle));
}

/// <summary>Convierte las filas de entrada en líneas listas para guardar, con todas las comprobaciones del contrato.</summary>
public static class LineasDePresupuesto
{
    /// <summary>
    /// Lo más que puede valer una cuota: trece nueves, holgado para cualquier cooperativa y muy por
    /// debajo de lo que admite la columna (<c>numeric(18,2)</c>), así que el 500 por desborde de la
    /// base ya no puede darse. Es una constante con nombre para que la regla se lea aquí y no en
    /// la migración.
    /// </summary>
    public const decimal TopeDeMonto = 9_999_999_999_999m;

    /// <summary>Redondeo a pesos del contrato (§10): sin decimales, la mitad hacia arriba.</summary>
    public static decimal APesos(decimal valor) => Math.Round(valor, 0, MidpointRounding.AwayFromZero);

    /// <summary>Si el valor ya está en pesos enteros (el presupuesto no admite centavos).</summary>
    public static bool EsEnPesos(decimal valor) => decimal.Truncate(valor) == valor;

    public static BudgetLine Nueva(int accountId, int? branchId, int? costCenterId, byte month, decimal amount, DateTime ahora, string quien) =>
        new() { AccountId = accountId, BranchId = branchId, CostCenterId = costCenterId, Month = month, Amount = amount, CreatedAt = ahora, CreatedBy = quien };

    /// <summary>
    /// Las líneas vivas de la vigente partidas por el alcance: <c>Dentro</c> las que quien opera
    /// puede tocar (sin sucursal, o de una sucursal asignada) y <c>Fuera</c> las que no ve y por
    /// eso hay que conservar tal cual al reemplazar. Sin restricción, todo está dentro.
    /// </summary>
    public static (List<BudgetLine> Dentro, List<BudgetLine> Fuera) PartirPorAlcance(IReadOnlyList<BudgetLine> lineas, AlcanceDeSucursales alcance)
    {
        var dentro = new List<BudgetLine>();
        var fuera = new List<BudgetLine>();
        foreach (var l in lineas) (l.BranchId is null || alcance.Permite(l.BranchId.Value) ? dentro : fuera).Add(l);
        return (dentro, fuera);
    }

    /// <summary>
    /// Cuentas de movimiento activas (FR-061; <c>AccountNotMovement</c> con el código), sucursal y
    /// centro existentes —la sucursal, además, dentro del alcance de quien opera (FR-035)—, doce
    /// valores por fila en pesos enteros, no negativos y bajo el tope, y ninguna (cuenta, sucursal,
    /// centro) repetida —se comprueba aquí porque en PostgreSQL el índice único con NULL no lo
    /// frena—. Los meses en cero no se guardan: el DTO los reconstruye.
    /// </summary>
    public static async Task<Result<List<BudgetLine>>> ResolverAsync(IApplicationDbContext db, IReadOnlyList<BudgetLineInput> inputs, AlcanceDeSucursales alcance,
        DateTime ahora, string quien, CancellationToken ct)
    {
        var cuentaIds = inputs.Select(i => i.AccountPublicId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking()
            .Where(a => cuentaIds.Contains(a.PublicId) && !a.IsDeleted)
            .Select(a => new { a.Id, a.PublicId, a.Code, a.IsMovement, a.IsActive })
            .ToDictionaryAsync(a => a.PublicId, ct);

        var sucursalIds = inputs.Where(i => i.BranchPublicId is not null).Select(i => i.BranchPublicId!.Value).Distinct().ToList();
        var sucursales = sucursalIds.Count == 0
            ? []
            : await db.Branches.AsNoTracking().Where(b => sucursalIds.Contains(b.PublicId) && !b.IsDeleted).ToDictionaryAsync(b => b.PublicId, b => new { b.Id, b.Name }, ct);
        var centroIds = inputs.Where(i => i.CostCenterPublicId is not null).Select(i => i.CostCenterPublicId!.Value).Distinct().ToList();
        var centros = centroIds.Count == 0
            ? []
            : await db.CostCenters.AsNoTracking().Where(c => centroIds.Contains(c.PublicId) && !c.IsDeleted).ToDictionaryAsync(c => c.PublicId, c => c.Id, ct);

        var claves = new HashSet<(int, int?, int?)>();
        var lineas = new List<BudgetLine>();
        foreach (var input in inputs)
        {
            if (!cuentas.TryGetValue(input.AccountPublicId, out var cuenta)) return Result.Failure<List<BudgetLine>>(BudgetErrors.AccountNotFound(input.AccountPublicId));
            if (!cuenta.IsMovement || !cuenta.IsActive) return Result.Failure<List<BudgetLine>>(BudgetErrors.AccountNotMovement(cuenta.Code));
            if (input.Amounts is null || input.Amounts.Count != ArmadoDePresupuesto.Meses) return Result.Failure<List<BudgetLine>>(BudgetErrors.TwelveAmountsRequired(cuenta.Code));
            if (input.Amounts.Any(a => a < 0m)) return Result.Failure<List<BudgetLine>>(BudgetErrors.NegativeAmount);
            if (input.Amounts.Any(a => a > TopeDeMonto)) return Result.Failure<List<BudgetLine>>(BudgetErrors.AmountTooLarge);
            if (input.Amounts.Any(a => !EsEnPesos(a))) return Result.Failure<List<BudgetLine>>(BudgetErrors.DecimalsNotAllowed);

            int? sucursalId = null;
            if (input.BranchPublicId is { } s)
            {
                if (!sucursales.TryGetValue(s, out var sucursal)) return Result.Failure<List<BudgetLine>>(BudgetErrors.BranchNotFound);
                if (!alcance.Permite(sucursal.Id)) return Result.Failure<List<BudgetLine>>(BudgetErrors.BranchOutOfScope(sucursal.Name));
                sucursalId = sucursal.Id;
            }
            int? centroId = null;
            if (input.CostCenterPublicId is { } c)
            {
                if (!centros.TryGetValue(c, out var id)) return Result.Failure<List<BudgetLine>>(BudgetErrors.CostCenterNotFound);
                centroId = id;
            }
            if (!claves.Add((cuenta.Id, sucursalId, centroId))) return Result.Failure<List<BudgetLine>>(BudgetErrors.LineDuplicate(cuenta.Code));

            for (var mes = 0; mes < ArmadoDePresupuesto.Meses; mes++)
            {
                if (input.Amounts[mes] == 0m) continue;
                lineas.Add(Nueva(cuenta.Id, sucursalId, centroId, (byte)(mes + 1), input.Amounts[mes], ahora, quien));
            }
        }
        return Result.Success(lineas);
    }
}

/// <summary>El versionado, en un solo sitio (§10): borrador en su sitio; aprobado → versión n+1 aprobada con motivo, la anterior <c>Superseded</c>.</summary>
public static class EscrituraDePresupuesto
{
    public static Task<FiscalYear?> EjercicioAsync(IApplicationDbContext db, int year, CancellationToken ct) =>
        db.FiscalYears.AsNoTracking().FirstOrDefaultAsync(f => f.Year == year && !f.IsDeleted, ct);

    public static Budget NuevaVersion(FiscalYear ejercicio, int version, BudgetStatus estado, string? motivo, IEnumerable<BudgetLine> lineas, DateTime ahora, string quien)
    {
        var presupuesto = new Budget
        {
            FiscalYearId = ejercicio.Id,
            Version = version,
            Status = estado,
            ApprovedAt = estado == BudgetStatus.Approved ? ahora : null,
            ApprovedBy = estado == BudgetStatus.Approved ? quien : null,
            ChangeReason = motivo,
            CreatedAt = ahora,
            CreatedBy = quien,
        };
        foreach (var l in lineas) presupuesto.Lines.Add(l);
        return presupuesto;
    }

    /// <summary>
    /// Deja la vigente con <paramref name="conservar"/> + <paramref name="agregar"/> y sin
    /// <paramref name="retirar"/>. Sobre un borrador se retira (baja lógica) y se agrega en su sitio;
    /// sobre un aprobado, si <paramref name="permitirVersionar"/>, exige motivo y crea la versión
    /// siguiente con copias de lo conservado más lo nuevo; si no, <c>NotDraft</c>. No guarda.
    /// </summary>
    public static Result<Budget> Aplicar(IApplicationDbContext db, FiscalYear ejercicio, ArmadoDePresupuesto.Vigente vigente,
        IReadOnlyList<BudgetLine> conservar, IReadOnlyList<BudgetLine> retirar, IReadOnlyList<BudgetLine> agregar,
        string? reason, bool permitirVersionar, DateTime ahora, string quien)
    {
        var actual = vigente.Presupuesto;
        if (actual.Status == BudgetStatus.Draft)
        {
            foreach (var l in retirar) { l.IsDeleted = true; l.DeletedAt = ahora; l.DeletedBy = quien; }
            foreach (var l in agregar) { l.BudgetId = actual.Id; db.BudgetLines.Add(l); }
            actual.UpdatedAt = ahora;
            actual.UpdatedBy = quien;
            return Result.Success(actual);
        }

        if (!permitirVersionar) return Result.Failure<Budget>(BudgetErrors.NotDraft);
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure<Budget>(BudgetErrors.ReasonRequired);

        var copias = conservar.Select(l => LineasDePresupuesto.Nueva(l.AccountId, l.BranchId, l.CostCenterId, l.Month, l.Amount, ahora, quien));
        var siguiente = NuevaVersion(ejercicio, actual.Version + 1, BudgetStatus.Approved, reason.Trim(), copias.Concat(agregar), ahora, quien);
        actual.Status = BudgetStatus.Superseded;
        actual.UpdatedAt = ahora;
        actual.UpdatedBy = quien;
        db.Budgets.Add(siguiente);
        return Result.Success(siguiente);
    }
}
