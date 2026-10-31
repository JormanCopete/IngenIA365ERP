using FluentValidation;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Assets;

/// <summary>
/// La vista <c>assets</c> del centro de informes (feature 009 E4, US12, FR-080): el inventario de activos y diferidos con valor
/// original, residual, acumulada, neto en libros y meses que faltan, por sucursal, como <see cref="TablaExportable"/>. Del filtro
/// común sólo toma la sucursal y el centro; el alcance de sucursal se aplica siempre. <c>_activo</c> abre la ficha.
/// </summary>
public sealed record FixedAssetsReportQuery(FiltrosDeInforme Filtros, bool IncludeRetired = false) : IRequest<Result<TablaExportable>>;

public sealed class FixedAssetsReportQueryValidator : AbstractValidator<FixedAssetsReportQuery>
{
    public FixedAssetsReportQueryValidator() => RuleFor(x => x.Filtros).NotNull();
}

public sealed class FixedAssetsReportQueryHandler(IApplicationDbContext db, IUserBranchScope alcance, AccountingAuditEmitter audit)
    : IRequestHandler<FixedAssetsReportQuery, Result<TablaExportable>>
{
    public const string Vista = "assets";

    private static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Código"), new("Nombre"), new("Clase"), new("Estado"), new("Cuenta del activo"), new("Inicio", TipoDeColumna.Fecha),
        new("Costo", TipoDeColumna.Moneda), new("Residual", TipoDeColumna.Moneda), new("Vida útil (meses)", TipoDeColumna.Entero),
        new("Acumulada", TipoDeColumna.Moneda), new("Neto en libros", TipoDeColumna.Moneda), new("Meses por depreciar", TipoDeColumna.Entero),
        new("Activo", Clave: "_activo"),
    ];

    public async Task<Result<TablaExportable>> Handle(FixedAssetsReportQuery request, CancellationToken ct)
    {
        var q = ArmadoDeActivos.EnAlcance(db.FixedAssets.Where(a => !a.IsDeleted), await alcance.ObtenerAsync(ct));
        if (!request.IncludeRetired) q = q.Where(a => a.Status != FixedAssetStatus.Retired);
        if (request.Filtros.Branch is { } sucursal) q = q.Where(a => a.Branch!.PublicId == sucursal);
        if (request.Filtros.CostCenter is { } centro) q = q.Where(a => a.CostCenter != null && a.CostCenter.PublicId == centro);
        var activos = await ArmadoDeActivos.ArmarAsync(db, q, ct);

        var filas = activos
            .OrderBy(a => a.BranchName).ThenBy(a => a.Code)
            .Select(a => new FilaExportable(
            [
                a.Code, a.Name, a.Kind == nameof(FixedAssetKind.Asset) ? "Activo fijo" : "Diferido", Estado(a.Status),
                $"{a.AssetAccount.Code} {a.AssetAccount.Name}", a.StartDate,
                a.Cost, a.ResidualValue, a.LifeMonths, a.Accumulated, a.NetBookValue, a.RemainingMonths,
                a.PublicId,
            ], Seccion: a.BranchName))
            .ToList();
        var totales = new FilaExportable(
        [
            "Total", null, null, null, null, null,
            activos.Sum(a => a.Cost), activos.Sum(a => a.ResidualValue), null, activos.Sum(a => a.Accumulated), activos.Sum(a => a.NetBookValue), null, null,
        ]);
        var notas = new List<string>
        {
            "Acumulada = la que el activo traía al entrar al ERP más las cuotas contabilizadas por las corridas mensuales (no hay saldos guardados).",
            "Las cuotas que faltan las calcula el sistema en línea recta sobre el valor neto en libros: cambiar vida útil o residual sólo mueve las siguientes.",
        };
        var tabla = new TablaExportable("Activos fijos y diferidos", request.IncludeRetired ? "Incluye los dados de baja" : "Activos vigentes", Columnas, filas, totales, notas);
        if (request.Filtros.EsExportacion)
            await audit.EmitirExportacionAsync(Vista, new { filtros = request.Filtros, request.IncludeRetired }, request.Filtros.FormatoNormalizado, filas.Count, ct);
        return Result.Success(tabla);
    }

    private static string Estado(string status) => status switch
    {
        nameof(FixedAssetStatus.Active) => "Vigente",
        nameof(FixedAssetStatus.FullyDepreciated) => "Totalmente depreciado",
        nameof(FixedAssetStatus.Retired) => "Dado de baja",
        _ => status,
    };
}
