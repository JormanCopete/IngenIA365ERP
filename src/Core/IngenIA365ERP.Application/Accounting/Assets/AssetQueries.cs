using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Assets;

// Consultas de activos (feature 009 E4, US12, FR-080): el inventario con valor original, acumulada, neto en libros y meses que
// faltan; la ficha con las cuotas contabilizadas o reversadas y la proyección; las corridas. Respetan el alcance de sucursal
// (FR-035): quien tiene sucursales asignadas sólo ve los activos de esas sucursales. Nada se lee de saldos guardados: la acumulada
// es la inicial más las cuotas contabilizadas, que son las mismas líneas que el DP llevó al libro.

internal static class ArmadoDeActivos
{
    public static async Task<IReadOnlyList<FixedAssetDto>> ArmarAsync(IApplicationDbContext db, IQueryable<FixedAsset> consulta, CancellationToken ct)
    {
        var activos = await consulta.AsNoTracking()
            .Include(a => a.AssetAccount).Include(a => a.AccumulatedAccount).Include(a => a.ExpenseAccount)
            .Include(a => a.Branch).Include(a => a.CostCenter).Include(a => a.SupplierPerson).Include(a => a.RetireDocument)
            .OrderBy(a => a.Code).ToListAsync(ct);
        var ids = activos.Select(a => a.Id).ToList();
        var historia = (await db.FixedAssetInstallments.AsNoTracking()
                .Where(i => ids.Contains(i.AssetId) && !i.IsDeleted && i.Status == InstallmentStatus.Posted)
                .Select(i => new { i.AssetId, i.Amount, i.Period!.FiscalYear!.Year, i.Period.Month })
                .ToListAsync(ct))
            .GroupBy(i => i.AssetId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<CuotaContabilizada>)g.Select(i => new CuotaContabilizada(new Mes(i.Year, i.Month), i.Amount)).ToList());
        return activos.Select(a => Armar(a, historia.GetValueOrDefault(a.Id) ?? [])).ToList();
    }

    public static FixedAssetDto Armar(FixedAsset a, IReadOnlyCollection<CuotaContabilizada> contabilizadas)
    {
        var calculo = ResolucionDeActivo.ParaCalculo(a);
        var acumulada = CalculoDeDepreciacion.Acumulada(calculo, contabilizadas);
        return new FixedAssetDto(a.PublicId, a.Code, a.Name, a.Kind.ToString(), a.Status.ToString(),
            Cuenta(a.AssetAccount!), a.AccumulatedAccount is null ? null : Cuenta(a.AccumulatedAccount), Cuenta(a.ExpenseAccount!),
            a.Branch!.PublicId, a.Branch.Name, a.CostCenter?.PublicId, a.CostCenter?.Name,
            a.SupplierPerson?.PublicId, a.SupplierPerson is null ? null : Nombre(a.SupplierPerson),
            a.PurchaseDate, a.PurchaseDocument, a.Cost, a.ResidualValue, a.LifeMonths, a.StartDate, a.OpeningAccumulated, a.OpeningElapsedMonths,
            acumulada, a.Cost - acumulada, a.Status == FixedAssetStatus.Retired ? 0 : Math.Max(0, a.LifeMonths - CalculoDeDepreciacion.MesesConsumidos(calculo, contabilizadas)),
            a.RetiredAt, a.RetireReason, a.RetireDocument?.PublicId, contabilizadas.Count > 0);
    }

    private static CuentaDeActivoDto Cuenta(ChartOfAccount c) => new(c.PublicId, c.Code, c.Name);

    public static string Nombre(Person p) => !string.IsNullOrWhiteSpace(p.BusinessName) ? p.BusinessName! : NombreDePersona.Completo(p);

    public static IQueryable<FixedAsset> EnAlcance(IQueryable<FixedAsset> q, AlcanceDeSucursales alcance) =>
        alcance.Restringido ? q.Where(a => alcance.Sucursales.Contains(a.BranchId)) : q;
}

/// <summary>El inventario de activos. <c>Kind</c> y <c>Status</c> filtran; <c>Search</c> busca en código y nombre.</summary>
public sealed record ListFixedAssetsQuery(string? Kind = null, string? Status = null, string? Search = null) : IRequest<Result<IReadOnlyList<FixedAssetDto>>>;

public sealed class ListFixedAssetsQueryValidator : AbstractValidator<ListFixedAssetsQuery>
{
    public ListFixedAssetsQueryValidator() => RuleFor(x => x.Search).MaximumLength(100);
}

public sealed class ListFixedAssetsQueryHandler(IApplicationDbContext db, IUserBranchScope alcance) : IRequestHandler<ListFixedAssetsQuery, Result<IReadOnlyList<FixedAssetDto>>>
{
    public async Task<Result<IReadOnlyList<FixedAssetDto>>> Handle(ListFixedAssetsQuery request, CancellationToken ct)
    {
        var q = ArmadoDeActivos.EnAlcance(db.FixedAssets.Where(a => !a.IsDeleted), await alcance.ObtenerAsync(ct));
        if (ResolucionDeActivo.Clase(request.Kind) is { } clase) q = q.Where(a => a.Kind == clase);
        if (Enum.TryParse<FixedAssetStatus>(request.Status, ignoreCase: true, out var estado)) q = q.Where(a => a.Status == estado);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var texto = request.Search.Trim().ToUpperInvariant();
            q = q.Where(a => a.Code.ToUpper().Contains(texto) || a.Name.ToUpper().Contains(texto));
        }
        return Result.Success(await ArmadoDeActivos.ArmarAsync(db, q, ct));
    }
}

/// <summary>La ficha de un activo: datos, cuotas (contabilizadas y reversadas, con su comprobante) y la proyección de las que faltan.</summary>
public sealed record GetFixedAssetQuery(Guid PublicId) : IRequest<Result<FixedAssetDetailDto>>;

public sealed class GetFixedAssetQueryValidator : AbstractValidator<GetFixedAssetQuery>
{
    public GetFixedAssetQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class GetFixedAssetQueryHandler(IApplicationDbContext db, IUserBranchScope alcance) : IRequestHandler<GetFixedAssetQuery, Result<FixedAssetDetailDto>>
{
    public async Task<Result<FixedAssetDetailDto>> Handle(GetFixedAssetQuery request, CancellationToken ct)
    {
        var q = ArmadoDeActivos.EnAlcance(db.FixedAssets.Where(a => a.PublicId == request.PublicId && !a.IsDeleted), await alcance.ObtenerAsync(ct));
        var activo = (await ArmadoDeActivos.ArmarAsync(db, q, ct)).FirstOrDefault();
        if (activo is null) return Result.Failure<FixedAssetDetailDto>(AssetErrors.NotFound);

        var entidad = await db.FixedAssets.AsNoTracking().FirstAsync(a => a.PublicId == request.PublicId, ct);
        // Las reversadas quedan de baja lógica (para liberar el período); se muestran igual, con su estado.
        var cuotas = await db.FixedAssetInstallments.AsNoTracking()
            .Where(i => i.AssetId == entidad.Id && (!i.IsDeleted || i.Status == InstallmentStatus.Reversed))
            .Select(i => new { i.Period!.FiscalYear!.Year, i.Period.Month, i.Amount, i.Status, Doc = i.Document == null ? null : new { i.Document.PublicId, i.Document.Number, Codigo = i.Document.VoucherType!.Code } })
            .ToListAsync(ct);
        var filas = cuotas.OrderBy(c => c.Year).ThenBy(c => c.Month).ThenBy(c => c.Status)
            .Select(c => new CuotaDeActivoDto(c.Year, c.Month, c.Amount, c.Status.ToString(), c.Doc?.PublicId, c.Doc is null ? null : $"{c.Doc.Codigo}-{c.Doc.Number}"))
            .ToList();
        var contabilizadas = cuotas.Where(c => c.Status == InstallmentStatus.Posted).Select(c => new CuotaContabilizada(new Mes(c.Year, c.Month), c.Amount)).ToList();
        var proyeccion = entidad.Status == FixedAssetStatus.Retired
            ? []
            : CalculoDeDepreciacion.Proyeccion(ResolucionDeActivo.ParaCalculo(entidad), contabilizadas)
                .Select(p => new CuotaProyectadaDto(p.Mes.Year, p.Mes.Month, p.Amount, p.Accumulated, p.NetBookValue)).ToList();
        return Result.Success(new FixedAssetDetailDto(activo, filas, proyeccion));
    }
}

/// <summary>Las corridas, de la más reciente a la más antigua, también las reversadas.</summary>
public sealed record ListAssetRunsQuery(int? Year = null) : IRequest<Result<IReadOnlyList<AssetRunDto>>>;

public sealed class ListAssetRunsQueryValidator : AbstractValidator<ListAssetRunsQuery>;

public sealed class ListAssetRunsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListAssetRunsQuery, Result<IReadOnlyList<AssetRunDto>>>
{
    public async Task<Result<IReadOnlyList<AssetRunDto>>> Handle(ListAssetRunsQuery request, CancellationToken ct)
    {
        var q = db.AssetRuns.AsNoTracking().Where(r => !r.IsDeleted || r.Status == AssetRunStatus.Reversed);
        if (request.Year is { } year) q = q.Where(r => r.Period!.FiscalYear!.Year == year);
        var corridas = await q.Select(r => new
            {
                r.PublicId, r.Period!.FiscalYear!.Year, r.Period.Month, r.Status, r.ExecutedAt, r.ExecutedBy, r.DocumentId,
                Doc = new { r.Document!.PublicId, r.Document.Number, Codigo = r.Document.VoucherType!.Code },
                Rev = r.ReversalDocument == null ? null : new { r.ReversalDocument.PublicId, r.ReversalDocument.Number },
            })
            .ToListAsync(ct);
        var documentos = corridas.Select(c => (long?)c.DocumentId).ToList();
        var totales = (await db.FixedAssetInstallments.AsNoTracking().Where(i => documentos.Contains(i.DocumentId))
                .GroupBy(i => i.DocumentId).Select(g => new { DocumentId = g.Key, Cuenta = g.Count(), Total = g.Sum(i => i.Amount) }).ToListAsync(ct))
            .ToDictionary(t => t.DocumentId!.Value);
        return Result.Success<IReadOnlyList<AssetRunDto>>(corridas
            .OrderByDescending(c => c.Year).ThenByDescending(c => c.Month).ThenByDescending(c => c.ExecutedAt)
            .Select(c =>
            {
                var t = totales.GetValueOrDefault(c.DocumentId);
                return new AssetRunDto(c.PublicId, c.Year, c.Month, c.Status.ToString(), c.Doc.PublicId, $"{c.Doc.Codigo}-{c.Doc.Number}",
                    t?.Cuenta ?? 0, t?.Total ?? 0m, c.ExecutedAt, c.ExecutedBy, c.Rev?.PublicId, c.Rev is null ? null : $"{c.Doc.Codigo}-{c.Rev.Number}");
            }).ToList());
    }
}

/// <summary>Lo que dejaría la corrida de un mes, sin contabilizar: activo por activo, cuota y meses que cubre.</summary>
public sealed record PreviewAssetRunQuery(int Year, int Month) : IRequest<Result<VistaPreviaDeCorridaDto>>;

public sealed class PreviewAssetRunQueryValidator : AbstractValidator<PreviewAssetRunQuery>
{
    public PreviewAssetRunQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class PreviewAssetRunQueryHandler(IApplicationDbContext db) : IRequestHandler<PreviewAssetRunQuery, Result<VistaPreviaDeCorridaDto>>
{
    public async Task<Result<VistaPreviaDeCorridaDto>> Handle(PreviewAssetRunQuery request, CancellationToken ct)
    {
        var periodo = await CuotasDelPeriodo.PeriodoAsync(db, request.Year, request.Month, ct);
        var corrida = periodo is not null && await db.AssetRuns.AsNoTracking().AnyAsync(r => r.PeriodId == periodo.Id && !r.IsDeleted, ct);
        var cuotas = corrida ? [] : await CuotasDelPeriodo.CalcularAsync(db, new Mes(request.Year, request.Month), seguimiento: false, ct);
        var lineas = cuotas.Select(c => new CuotaDeLaCorridaDto(c.Activo.PublicId, c.Activo.Code, c.Activo.Name, c.Activo.Kind.ToString(), c.Amount, c.Months)).ToList();
        return Result.Success(new VistaPreviaDeCorridaDto(request.Year, request.Month, corrida, lineas, lineas.Sum(l => l.Amount)));
    }
}
