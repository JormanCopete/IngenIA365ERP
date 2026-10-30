using FluentValidation;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Assets;

// Activos fijos y diferidos (feature 009 E4, US12, FR-076..FR-080). El activo guarda costo, residual, vida útil y cuentas; las
// cuotas pendientes no se guardan: las calcula CalculoDeDepreciacion sobre el valor neto en libros, y una cuota sólo existe en
// ACC_FixedAssetInstallments cuando una corrida la contabiliza. La corrida es una por período (UK_ACC_AssetRuns_Period) y deja un
// solo comprobante DP por el contrato; reversarla deja su reversión, marca las cuotas Reversed y da de baja lógica la corrida
// (Status Reversed) para que el mes se pueda volver a correr. La baja genera su propio DP. Todo con origen ACT: lo de Activos
// sólo se reversa desde Activos (Accounting.Document.ModuleOwned).

/// <summary>Lo común de alta y corrección: resolver por PublicId lo que llega y comprobar las cuentas para Activos.</summary>
internal static class ResolucionDeActivo
{
    public sealed record Resuelto(FixedAssetKind Kind, ChartOfAccount Asset, ChartOfAccount? Accumulated, ChartOfAccount Expense, int BranchId,
        int? CostCenterId, int? SupplierPersonId);

    public static FixedAssetKind? Clase(string? kind) =>
        string.Equals(kind, nameof(FixedAssetKind.Asset), StringComparison.OrdinalIgnoreCase) ? FixedAssetKind.Asset
        : string.Equals(kind, nameof(FixedAssetKind.Deferred), StringComparison.OrdinalIgnoreCase) ? FixedAssetKind.Deferred
        : null;

    public static async Task<Result<Resuelto>> ResolverAsync(IApplicationDbContext db, AccountEligibility cuentas, FixedAssetInput input, CancellationToken ct)
    {
        if (Clase(input.Kind) is not { } clase) return Result.Failure<Resuelto>(AssetErrors.KindUnknown);
        if (clase == FixedAssetKind.Asset && input.AccumulatedAccountPublicId is null) return Result.Failure<Resuelto>(AssetErrors.AccumulatedRequired);
        if (input.ResidualValue > input.Cost) return Result.Failure<Resuelto>(AssetErrors.ResidualTooHigh);
        if (input.OpeningAccumulated > input.Cost - input.ResidualValue) return Result.Failure<Resuelto>(AssetErrors.OpeningTooHigh);
        if (input.OpeningElapsedMonths > input.LifeMonths) return Result.Failure<Resuelto>(AssetErrors.OpeningMonthsTooHigh);
        // Se comparan meses: la depreciación se cuenta por mes y la pantalla manda el día 1 del primer mes.
        if (Mes.De(input.StartDate) < Mes.De(input.PurchaseDate)) return Result.Failure<Resuelto>(AssetErrors.StartBeforePurchase);

        var activo = await cuentas.ResolverPorPublicIdAsync(input.AssetAccountPublicId, ModuloContable.Activos, ct);
        if (activo.IsFailure) return Result.Failure<Resuelto>(activo.Error);
        ChartOfAccount? acumulada = null;
        if (input.AccumulatedAccountPublicId is { } idAcumulada)
        {
            var r = await cuentas.ResolverPorPublicIdAsync(idAcumulada, ModuloContable.Activos, ct);
            if (r.IsFailure) return Result.Failure<Resuelto>(r.Error);
            acumulada = r.Value;
        }
        var gasto = await cuentas.ResolverPorPublicIdAsync(input.ExpenseAccountPublicId, ModuloContable.Activos, ct);
        if (gasto.IsFailure) return Result.Failure<Resuelto>(gasto.Error);

        var sucursal = await db.Branches.AsNoTracking().Where(b => b.PublicId == input.BranchPublicId && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (sucursal is null) return Result.Failure<Resuelto>(AssetErrors.BranchNotFound);
        int? centro = null;
        if (input.CostCenterPublicId is { } idCentro)
        {
            centro = await db.CostCenters.AsNoTracking().Where(c => c.PublicId == idCentro && !c.IsDeleted).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            if (centro is null) return Result.Failure<Resuelto>(AssetErrors.CostCenterNotFound);
        }
        int? proveedor = null;
        if (input.SupplierPersonPublicId is { } idProveedor)
        {
            proveedor = await db.People.AsNoTracking().Where(p => p.PublicId == idProveedor && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (proveedor is null) return Result.Failure<Resuelto>(AssetErrors.SupplierNotFound);
        }
        return Result.Success(new Resuelto(clase, activo.Value, acumulada, gasto.Value, sucursal.Value, centro, proveedor));
    }

    public static ActivoParaCalculo ParaCalculo(FixedAsset a) =>
        new(a.Cost, a.ResidualValue, a.LifeMonths, a.OpeningAccumulated, a.OpeningElapsedMonths, Mes.De(a.StartDate));

    /// <summary>Las cuotas contabilizadas y no reversadas del activo, con el mes de su período.</summary>
    public static async Task<IReadOnlyList<CuotaContabilizada>> ContabilizadasAsync(IApplicationDbContext db, int assetId, CancellationToken ct) =>
        (await db.FixedAssetInstallments.AsNoTracking()
            .Where(i => i.AssetId == assetId && !i.IsDeleted && i.Status == InstallmentStatus.Posted)
            .Select(i => new { i.Amount, i.Period!.FiscalYear!.Year, i.Period.Month })
            .ToListAsync(ct))
        .Select(i => new CuotaContabilizada(new Mes(i.Year, i.Month), i.Amount))
        .ToList();
}

/// <summary>Las reglas de validación del cuerpo, compartidas por alta y corrección.</summary>
public sealed class FixedAssetInputValidator : AbstractValidator<FixedAssetInput>
{
    public FixedAssetInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indique el código del activo.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre del activo.").MaximumLength(150);
        RuleFor(x => x.Kind).NotEmpty().WithMessage("Indique si es activo fijo o diferido.");
        RuleFor(x => x.PurchaseDocument).MaximumLength(60);
        RuleFor(x => x.Cost).GreaterThan(0m).WithMessage("El costo debe ser mayor que cero.").PrecisionScale(18, 2, true);
        RuleFor(x => x.ResidualValue).GreaterThanOrEqualTo(0m).WithMessage("El valor residual no puede ser negativo.").PrecisionScale(18, 2, true);
        RuleFor(x => x.LifeMonths).GreaterThan(0).WithMessage("La vida útil debe ser de al menos un mes.").LessThanOrEqualTo(1200);
        RuleFor(x => x.OpeningAccumulated).GreaterThanOrEqualTo(0m).PrecisionScale(18, 2, true);
        RuleFor(x => x.OpeningElapsedMonths).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AssetAccountPublicId).NotEmpty().WithMessage("Indique la cuenta del activo.");
        RuleFor(x => x.ExpenseAccountPublicId).NotEmpty().WithMessage("Indique la cuenta de gasto.");
        RuleFor(x => x.BranchPublicId).NotEmpty().WithMessage("Indique la sucursal.");
    }
}

// --------------------------------------------------------------------------------- registrar --

/// <summary>Registra un activo fijo o un diferido. No contabiliza nada: la compra ya está en el libro (por el módulo o la apertura).</summary>
public sealed record RegisterFixedAssetCommand(FixedAssetInput Asset) : IRequest<Result<Guid>>;

public sealed class RegisterFixedAssetCommandValidator : AbstractValidator<RegisterFixedAssetCommand>
{
    public RegisterFixedAssetCommandValidator() => RuleFor(x => x.Asset).NotNull().SetValidator(new FixedAssetInputValidator());
}

public sealed class RegisterFixedAssetCommandHandler(IApplicationDbContext db, AccountEligibility cuentas, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<RegisterFixedAssetCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterFixedAssetCommand request, CancellationToken ct)
    {
        var input = request.Asset;
        var resuelto = await ResolucionDeActivo.ResolverAsync(db, cuentas, input, ct);
        if (resuelto.IsFailure) return Result.Failure<Guid>(resuelto.Error);
        var codigo = CodigoDeCatalogo.Normalizar(input.Code)!;
        var repetido = await db.FixedAssets.AsNoTracking().Where(a => a.Code == codigo && !a.IsDeleted).Select(a => new { a.Name, a.PublicId }).FirstOrDefaultAsync(ct);
        if (repetido is not null) return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un activo", codigo, repetido.Name, repetido.PublicId));

        var r = resuelto.Value;
        var activo = new FixedAsset
        {
            Code = codigo,
            Name = input.Name.Trim(),
            Kind = r.Kind,
            AssetAccountId = r.Asset.Id,
            AccumulatedAccountId = r.Accumulated?.Id,
            ExpenseAccountId = r.Expense.Id,
            BranchId = r.BranchId,
            CostCenterId = r.CostCenterId,
            SupplierPersonId = r.SupplierPersonId,
            PurchaseDate = input.PurchaseDate,
            PurchaseDocument = string.IsNullOrWhiteSpace(input.PurchaseDocument) ? null : input.PurchaseDocument.Trim(),
            Cost = input.Cost,
            ResidualValue = input.ResidualValue,
            LifeMonths = input.LifeMonths,
            StartDate = input.StartDate,
            OpeningAccumulated = input.OpeningAccumulated,
            OpeningElapsedMonths = input.OpeningElapsedMonths,
            Status = input.OpeningAccumulated >= input.Cost - input.ResidualValue ? FixedAssetStatus.FullyDepreciated : FixedAssetStatus.Active,
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName ?? "system",
        };
        db.FixedAssets.Add(activo);
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Asset.Registered", nameof(FixedAsset), activo.PublicId, null,
            new { activo.Code, activo.Name, kind = activo.Kind.ToString(), activo.Cost, activo.ResidualValue, activo.LifeMonths, activo.StartDate, activo.OpeningAccumulated, activo.OpeningElapsedMonths }, ct);
        return Result.Success(activo.PublicId);
    }
}

// --------------------------------------------------------------------------------- corregir --

/// <summary>
/// Corrige un activo. Sin cuotas contabilizadas, todo. Con cuotas, sólo nombre, documento, proveedor, sucursal, centro, vida útil y
/// residual —los dos últimos hacia adelante, sobre el valor neto en libros (FR-078)—; cambiar cuentas, costo, inicio o saldo inicial
/// responde <c>Accounting.Assets.Locked</c>.
/// </summary>
public sealed record UpdateFixedAssetCommand(Guid PublicId, FixedAssetInput Asset, string? Reason) : IRequest<Result>;

public sealed class UpdateFixedAssetCommandValidator : AbstractValidator<UpdateFixedAssetCommand>
{
    public UpdateFixedAssetCommandValidator()
    {
        RuleFor(x => x.Asset).NotNull().SetValidator(new FixedAssetInputValidator());
        RuleFor(x => x.Reason).MaximumLength(200);
    }
}

public sealed class UpdateFixedAssetCommandHandler(IApplicationDbContext db, AccountEligibility cuentas, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<UpdateFixedAssetCommand, Result>
{
    public async Task<Result> Handle(UpdateFixedAssetCommand request, CancellationToken ct)
    {
        var activo = await db.FixedAssets.FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);
        if (activo is null) return Result.Failure(AssetErrors.NotFound);
        if (activo.Status == FixedAssetStatus.Retired) return Result.Failure(AssetErrors.Retired);
        var input = request.Asset;
        var resuelto = await ResolucionDeActivo.ResolverAsync(db, cuentas, input, ct);
        if (resuelto.IsFailure) return Result.Failure(resuelto.Error);
        var r = resuelto.Value;

        var codigo = CodigoDeCatalogo.Normalizar(input.Code)!;
        if (codigo != activo.Code)
        {
            var repetido = await db.FixedAssets.AsNoTracking().Where(a => a.Code == codigo && a.Id != activo.Id && !a.IsDeleted).Select(a => new { a.Name, a.PublicId }).FirstOrDefaultAsync(ct);
            if (repetido is not null) return Result.Failure(CodigoDeCatalogo.Duplicado("un activo", codigo, repetido.Name, repetido.PublicId));
        }

        var contabilizadas = await ResolucionDeActivo.ContabilizadasAsync(db, activo.Id, ct);
        if (contabilizadas.Count > 0)
        {
            var bloqueado = r.Kind != activo.Kind || r.Asset.Id != activo.AssetAccountId || r.Accumulated?.Id != activo.AccumulatedAccountId
                            || r.Expense.Id != activo.ExpenseAccountId || input.Cost != activo.Cost || input.StartDate != activo.StartDate
                            || input.OpeningAccumulated != activo.OpeningAccumulated || input.OpeningElapsedMonths != activo.OpeningElapsedMonths;
            if (bloqueado) return Result.Failure(AssetErrors.Locked);
            var actual = ResolucionDeActivo.ParaCalculo(activo);
            if (input.LifeMonths < CalculoDeDepreciacion.MesesConsumidos(actual, contabilizadas)) return Result.Failure(AssetErrors.LifeTooShort);
            if (input.ResidualValue > activo.Cost - CalculoDeDepreciacion.Acumulada(actual, contabilizadas)) return Result.Failure(AssetErrors.ResidualAboveNet);
        }

        var antes = new { activo.Code, activo.Name, activo.LifeMonths, activo.ResidualValue, activo.Cost, activo.StartDate, activo.BranchId, activo.CostCenterId };
        activo.Code = codigo;
        activo.Name = input.Name.Trim();
        activo.Kind = r.Kind;
        activo.AssetAccountId = r.Asset.Id;
        activo.AccumulatedAccountId = r.Accumulated?.Id;
        activo.ExpenseAccountId = r.Expense.Id;
        activo.BranchId = r.BranchId;
        activo.CostCenterId = r.CostCenterId;
        activo.SupplierPersonId = r.SupplierPersonId;
        activo.PurchaseDate = input.PurchaseDate;
        activo.PurchaseDocument = string.IsNullOrWhiteSpace(input.PurchaseDocument) ? null : input.PurchaseDocument.Trim();
        activo.Cost = input.Cost;
        activo.ResidualValue = input.ResidualValue;
        activo.LifeMonths = input.LifeMonths;
        activo.StartDate = input.StartDate;
        activo.OpeningAccumulated = input.OpeningAccumulated;
        activo.OpeningElapsedMonths = input.OpeningElapsedMonths;
        activo.Status = CalculoDeDepreciacion.PorDepreciar(ResolucionDeActivo.ParaCalculo(activo), contabilizadas) <= 0m
            ? FixedAssetStatus.FullyDepreciated : FixedAssetStatus.Active;
        activo.UpdatedAt = clock.UtcNow;
        activo.UpdatedBy = user.UserName ?? "system";
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Asset.Updated", nameof(FixedAsset), activo.PublicId, antes,
            new { activo.Code, activo.Name, activo.LifeMonths, activo.ResidualValue, activo.Cost, activo.StartDate, activo.BranchId, activo.CostCenterId, reason = request.Reason?.Trim() }, ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------------- correr el mes --

/// <summary>Lo común de la corrida y su vista previa: qué activos tienen cuota en el período y cuánto.</summary>
internal static class CuotasDelPeriodo
{
    public sealed record Cuota(FixedAsset Activo, decimal Amount, int Months, decimal PendienteAntes);

    public static async Task<IReadOnlyList<Cuota>> CalcularAsync(IApplicationDbContext db, Mes mes, bool seguimiento, CancellationToken ct)
    {
        var fin = new DateOnly(mes.Year, mes.Month, 1).AddMonths(1).AddDays(-1);
        var consulta = db.FixedAssets.Where(a => !a.IsDeleted && a.Status == FixedAssetStatus.Active && a.StartDate <= fin).OrderBy(a => a.Code);
        var activos = seguimiento ? await consulta.ToListAsync(ct) : await consulta.AsNoTracking().ToListAsync(ct);
        var ids = activos.Select(a => a.Id).ToList();
        var historia = (await db.FixedAssetInstallments.AsNoTracking()
                .Where(i => ids.Contains(i.AssetId) && !i.IsDeleted && i.Status == InstallmentStatus.Posted)
                .Select(i => new { i.AssetId, i.Amount, i.Period!.FiscalYear!.Year, i.Period.Month })
                .ToListAsync(ct))
            .GroupBy(i => i.AssetId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<CuotaContabilizada>)g.Select(i => new CuotaContabilizada(new Mes(i.Year, i.Month), i.Amount)).ToList());

        var cuotas = new List<Cuota>();
        foreach (var a in activos)
        {
            var contabilizadas = historia.GetValueOrDefault(a.Id) ?? [];
            var calculo = ResolucionDeActivo.ParaCalculo(a);
            var valor = CalculoDeDepreciacion.CuotaDe(calculo, contabilizadas, mes);
            if (valor > 0m)
                cuotas.Add(new Cuota(a, valor, mes.MesesDesde(CalculoDeDepreciacion.UltimoMesCubierto(calculo, contabilizadas)),
                    CalculoDeDepreciacion.PorDepreciar(calculo, contabilizadas)));
        }
        return cuotas;
    }

    public static Task<AccountingPeriod?> PeriodoAsync(IApplicationDbContext db, int year, int month, CancellationToken ct) =>
        db.AccountingPeriods.Include(p => p.FiscalYear).FirstOrDefaultAsync(p => !p.IsDeleted && p.FiscalYear!.Year == year && p.Month == month, ct);

    /// <summary>El mes de la última corrida vigente (sin reversar), si la hay.</summary>
    public static async Task<Mes?> UltimaCorridaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var ultima = await db.AssetRuns.AsNoTracking().Where(r => !r.IsDeleted && r.Status == AssetRunStatus.Posted)
            .Select(r => new { r.Period!.FiscalYear!.Year, r.Period.Month })
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).FirstOrDefaultAsync(ct);
        return ultima is null ? null : new Mes(ultima.Year, ultima.Month);
    }

    /// <summary>El tercero de una línea: el proveedor del activo, sólo si la cuenta lo exige.</summary>
    public static int? Tercero(ChartOfAccount cuenta, FixedAsset a) => cuenta.RequiresThirdParty ? a.SupplierPersonId : null;
}

/// <summary>
/// Corre la depreciación y amortización de un mes (FR-077): un solo comprobante <c>DP</c> fechado al fin del período, por el contrato,
/// con dos líneas por activo (gasto contra acumulada; en un diferido sin acumulada, contra su propia cuenta). Única por período.
/// </summary>
public sealed record RunAssetDepreciationCommand(int Year, int Month) : IRequest<Result<AssetRunDto>>, IReintentableAnteConcurrencia;

public sealed class RunAssetDepreciationCommandValidator : AbstractValidator<RunAssetDepreciationCommand>
{
    public RunAssetDepreciationCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class RunAssetDepreciationCommandHandler(IApplicationDbContext db, AccountingPoster poster, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<RunAssetDepreciationCommand, Result<AssetRunDto>>
{
    public const string TipoDeComprobante = "DP";

    public async Task<Result<AssetRunDto>> Handle(RunAssetDepreciationCommand request, CancellationToken ct)
    {
        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct)) return Result.Failure<AssetRunDto>(AssetErrors.AccountingNotInitialized);
        var periodo = await CuotasDelPeriodo.PeriodoAsync(db, request.Year, request.Month, ct);
        if (periodo is null) return Result.Failure<AssetRunDto>(AssetErrors.PeriodNotFound);
        if (periodo.Status != PeriodStatus.Open) return Result.Failure<AssetRunDto>(AssetErrors.PeriodClosed);
        if (await db.AssetRuns.AnyAsync(r => r.PeriodId == periodo.Id && !r.IsDeleted, ct)) return Result.Failure<AssetRunDto>(AssetErrors.AlreadyRun);
        var mes = new Mes(request.Year, request.Month);
        if (await CuotasDelPeriodo.UltimaCorridaAsync(db, ct) is { } ultima && ultima > mes) return Result.Failure<AssetRunDto>(AssetErrors.EarlierThanLastRun);

        var cuotas = await CuotasDelPeriodo.CalcularAsync(db, mes, seguimiento: true, ct);
        if (cuotas.Count == 0) return Result.Failure<AssetRunDto>(AssetErrors.NothingToRun);

        var idsDeCuenta = cuotas.SelectMany(c => new[] { c.Activo.ExpenseAccountId, c.Activo.AccumulatedAccountId ?? c.Activo.AssetAccountId }).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(c => idsDeCuenta.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var corrida = new AssetRun { PeriodId = periodo.Id, ExecutedAt = ahora, ExecutedBy = quien, Status = AssetRunStatus.Posted, CreatedAt = ahora, CreatedBy = quien };
        var lineas = new List<PostingLine>();
        foreach (var c in cuotas)
        {
            var a = c.Activo;
            var gasto = cuentas[a.ExpenseAccountId];
            var abono = cuentas[a.AccumulatedAccountId ?? a.AssetAccountId];
            var verbo = a.Kind == FixedAssetKind.Asset ? "Depreciación" : "Amortización";
            var detalle = Recortar($"{verbo} {a.Code} {a.Name}{(c.Months > 1 ? $" ({c.Months} meses)" : string.Empty)}");
            lineas.Add(new PostingLine { AccountId = gasto.Id, Debit = c.Amount, Detail = detalle, BranchId = a.BranchId, CostCenterId = a.CostCenterId, PersonId = CuotasDelPeriodo.Tercero(gasto, a) });
            lineas.Add(new PostingLine { AccountId = abono.Id, Credit = c.Amount, Detail = detalle, BranchId = a.BranchId, CostCenterId = a.CostCenterId, PersonId = CuotasDelPeriodo.Tercero(abono, a) });
        }

        var solicitud = new PostingRequest(TipoDeComprobante, periodo.EndDate, $"Depreciación y amortización de activos {mes}",
            new AccountingOrigin(ModuloContable.Activos, nameof(AssetRun), corrida.PublicId), lineas);
        var documento = await poster.PrepareAsync(solicitud, ct);
        if (documento.IsFailure) return Result.Failure<AssetRunDto>(documento.Error);

        corrida.Document = documento.Value;
        db.AssetRuns.Add(corrida);
        foreach (var c in cuotas)
        {
            db.FixedAssetInstallments.Add(new FixedAssetInstallment
            {
                AssetId = c.Activo.Id, PeriodId = periodo.Id, Amount = c.Amount, Status = InstallmentStatus.Posted, Document = documento.Value,
                CreatedAt = ahora, CreatedBy = quien,
            });
            // Lo que queda por depreciar después de esta cuota: si se agota, el activo queda totalmente depreciado (FR-080).
            if (c.Amount >= c.PendienteAntes)
            {
                c.Activo.Status = FixedAssetStatus.FullyDepreciated;
                c.Activo.UpdatedAt = ahora;
                c.Activo.UpdatedBy = quien;
            }
        }
        await db.SaveChangesAsync(ct);
        var total = cuotas.Sum(c => c.Amount);
        await audit.EmitAsync("Accounting.AssetRun.Posted", nameof(AssetRun), corrida.PublicId, null,
            new { period = mes.ToString(), assets = cuotas.Count, total, document = $"{TipoDeComprobante}-{documento.Value.Number}" }, ct);
        return Result.Success(new AssetRunDto(corrida.PublicId, mes.Year, mes.Month, corrida.Status.ToString(), documento.Value.PublicId,
            $"{TipoDeComprobante}-{documento.Value.Number}", cuotas.Count, total, ahora, quien, null, null));
    }

    internal static string Recortar(string texto) => texto.Length <= 200 ? texto : texto[..200];
}

// ------------------------------------------------------------------------- reversar la corrida --

/// <summary>
/// Reversa la última corrida vigente, en su período (que debe seguir abierto): reversión del <c>DP</c> por el contrato, cuotas
/// <c>Reversed</c>, activos de nuevo activos y la corrida dada de baja lógica con <c>Status = Reversed</c>, para poder correr el mes otra vez.
/// </summary>
public sealed record ReverseAssetRunCommand(Guid RunPublicId, string Reason) : IRequest<Result<AssetRunDto>>, IReintentableAnteConcurrencia;

public sealed class ReverseAssetRunCommandValidator : AbstractValidator<ReverseAssetRunCommand>
{
    public ReverseAssetRunCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo.").MaximumLength(200);
}

public sealed class ReverseAssetRunCommandHandler(IApplicationDbContext db, AccountingPoster poster, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<ReverseAssetRunCommand, Result<AssetRunDto>>
{
    public async Task<Result<AssetRunDto>> Handle(ReverseAssetRunCommand request, CancellationToken ct)
    {
        var corrida = await db.AssetRuns.Include(r => r.Period).ThenInclude(p => p!.FiscalYear)
            .FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId && !r.IsDeleted, ct);
        if (corrida is null) return Result.Failure<AssetRunDto>(AssetErrors.RunNotFound);
        if (corrida.Status != AssetRunStatus.Posted) return Result.Failure<AssetRunDto>(AssetErrors.RunReversed);
        var periodo = corrida.Period!;
        if (periodo.Status != PeriodStatus.Open) return Result.Failure<AssetRunDto>(AssetErrors.PeriodClosed);
        var mes = new Mes(periodo.FiscalYear!.Year, periodo.Month);
        if (await CuotasDelPeriodo.UltimaCorridaAsync(db, ct) is { } ultima && ultima > mes) return Result.Failure<AssetRunDto>(AssetErrors.NotLastRun);

        var original = await db.AccountingDocuments.Include(d => d.Lines).Include(d => d.VoucherType).FirstAsync(d => d.Id == corrida.DocumentId, ct);
        var reversion = await poster.PrepareReversalAsync(original, original.Date, request.Reason, new AccountingOrigin(ModuloContable.Activos, nameof(AssetRun), corrida.PublicId), ct);
        if (reversion.IsFailure) return Result.Failure<AssetRunDto>(reversion.Error);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var cuotas = await db.FixedAssetInstallments.Include(i => i.Asset)
            .Where(i => i.DocumentId == corrida.DocumentId && !i.IsDeleted && i.Status == InstallmentStatus.Posted).ToListAsync(ct);
        foreach (var i in cuotas)
        {
            i.Status = InstallmentStatus.Reversed;
            i.UpdatedAt = ahora;
            i.UpdatedBy = quien;
            // Una cuota reversada deja el par (activo, período) libre para la próxima corrida del mes (UK filtrada por IsDeleted).
            i.IsDeleted = true;
            i.DeletedAt = ahora;
            i.DeletedBy = quien;
            if (i.Asset is { Status: FixedAssetStatus.FullyDepreciated } a)
            {
                a.Status = FixedAssetStatus.Active;
                a.UpdatedAt = ahora;
                a.UpdatedBy = quien;
            }
        }
        corrida.Status = AssetRunStatus.Reversed;
        corrida.ReversalDocument = reversion.Value;
        corrida.IsDeleted = true;
        corrida.DeletedAt = ahora;
        corrida.DeletedBy = quien;
        corrida.UpdatedAt = ahora;
        corrida.UpdatedBy = quien;
        await db.SaveChangesAsync(ct);
        var total = cuotas.Sum(c => c.Amount);
        var tipo = original.VoucherType?.Code ?? RunAssetDepreciationCommandHandler.TipoDeComprobante;
        await audit.EmitAsync("Accounting.AssetRun.Reversed", nameof(AssetRun), corrida.PublicId, new { status = AssetRunStatus.Posted.ToString() },
            new { period = mes.ToString(), reason = request.Reason.Trim(), reversal = $"{tipo}-{reversion.Value.Number}" }, ct);
        return Result.Success(new AssetRunDto(corrida.PublicId, mes.Year, mes.Month, corrida.Status.ToString(), original.PublicId, $"{tipo}-{original.Number}",
            cuotas.Count, total, corrida.ExecutedAt, corrida.ExecutedBy, reversion.Value.PublicId, $"{tipo}-{reversion.Value.Number}"));
    }
}

// ------------------------------------------------------------------------------------- baja --

/// <summary>
/// Da de baja o vende un activo (FR-079): un <c>DP</c> fechado en la baja que cancela el costo y la acumulada contra la cuenta indicada
/// (pérdida en baja, cuenta por cobrar de la venta…), por el valor neto en libros. La depreciación del mes de la baja se corre antes.
/// </summary>
public sealed record RetireFixedAssetCommand(Guid PublicId, DateOnly Date, Guid CounterAccountPublicId, string Reason) : IRequest<Result<Guid>>, IReintentableAnteConcurrencia;

public sealed class RetireFixedAssetCommandValidator : AbstractValidator<RetireFixedAssetCommand>
{
    public RetireFixedAssetCommandValidator()
    {
        RuleFor(x => x.CounterAccountPublicId).NotEmpty().WithMessage("Indique la cuenta contra la que se da de baja.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo de la baja.").MaximumLength(200);
    }
}

public sealed class RetireFixedAssetCommandHandler(IApplicationDbContext db, AccountingPoster poster, AccountEligibility elegibilidad, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<RetireFixedAssetCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RetireFixedAssetCommand request, CancellationToken ct)
    {
        var activo = await db.FixedAssets.FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);
        if (activo is null) return Result.Failure<Guid>(AssetErrors.NotFound);
        if (activo.Status == FixedAssetStatus.Retired) return Result.Failure<Guid>(AssetErrors.Retired);
        var contrapartida = await elegibilidad.ResolverPorPublicIdAsync(request.CounterAccountPublicId, ModuloContable.Activos, ct);
        if (contrapartida.IsFailure) return Result.Failure<Guid>(contrapartida.Error);

        var calculo = ResolucionDeActivo.ParaCalculo(activo);
        var contabilizadas = await ResolucionDeActivo.ContabilizadasAsync(db, activo.Id, ct);
        if (contabilizadas.Count > 0 && CalculoDeDepreciacion.UltimoMesCubierto(calculo, contabilizadas) > Mes.De(request.Date))
            return Result.Failure<Guid>(AssetErrors.RetireDateBeforeLastInstallment);
        var acumulada = CalculoDeDepreciacion.Acumulada(calculo, contabilizadas);
        var neto = activo.Cost - acumulada;

        var ids = new[] { activo.AssetAccountId, activo.AccumulatedAccountId ?? activo.AssetAccountId, contrapartida.Value.Id };
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var detalle = RunAssetDepreciationCommandHandler.Recortar($"Baja de {activo.Code} {activo.Name}: {request.Reason.Trim()}");
        PostingLine Linea(int cuenta, decimal debito, decimal credito) => new()
        {
            AccountId = cuenta, Debit = debito, Credit = credito, Detail = detalle, BranchId = activo.BranchId, CostCenterId = activo.CostCenterId,
            PersonId = CuotasDelPeriodo.Tercero(cuentas[cuenta], activo),
        };
        var lineas = new List<PostingLine>();
        if (activo.AccumulatedAccountId is { } cuentaAcumulada)
        {
            // Activo fijo (o diferido con acumulada): sale el costo entero; la acumulada se cancela y el neto va a la contrapartida.
            if (acumulada > 0m) lineas.Add(Linea(cuentaAcumulada, acumulada, 0m));
            if (neto > 0m) lineas.Add(Linea(contrapartida.Value.Id, neto, 0m));
            lineas.Add(Linea(activo.AssetAccountId, 0m, activo.Cost));
        }
        else
        {
            // Diferido amortizado contra su propia cuenta: en ella sólo queda el neto.
            if (neto <= 0m) lineas.Clear();
            else
            {
                lineas.Add(Linea(contrapartida.Value.Id, neto, 0m));
                lineas.Add(Linea(activo.AssetAccountId, 0m, neto));
            }
        }

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        AccountingDocument? documento = null;
        if (lineas.Count > 0)
        {
            var solicitud = new PostingRequest(RunAssetDepreciationCommandHandler.TipoDeComprobante, request.Date, RunAssetDepreciationCommandHandler.Recortar($"Baja del activo {activo.Code} {activo.Name}"),
                new AccountingOrigin(ModuloContable.Activos, nameof(FixedAsset), activo.PublicId), lineas);
            var r = await poster.PrepareAsync(solicitud, ct);
            if (r.IsFailure) return Result.Failure<Guid>(r.Error);
            documento = r.Value;
        }

        activo.Status = FixedAssetStatus.Retired;
        activo.RetiredAt = request.Date;
        activo.RetireReason = request.Reason.Trim();
        activo.RetireCounterAccountId = contrapartida.Value.Id;
        activo.RetireDocument = documento;
        activo.UpdatedAt = ahora;
        activo.UpdatedBy = quien;
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Asset.Retired", nameof(FixedAsset), activo.PublicId, null,
            new { activo.Code, date = request.Date, reason = activo.RetireReason, cost = activo.Cost, accumulated = acumulada, net = neto, document = documento?.Number }, ct);
        return Result.Success(documento?.PublicId ?? Guid.Empty);
    }
}
