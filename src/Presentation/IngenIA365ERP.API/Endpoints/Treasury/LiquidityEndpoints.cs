using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.Liquidity;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// La liquidez de T2 (feature 015, fase 43, T0461; FR-137 a FR-139, FR-155; contracts/api.md §13): la posición del día y sus fotos (con el saldo del fondo a Captaciones), el
/// flujo de caja proyectado con sus categorías y lo digitado, y el tablero. **Desviación**: el flujo va en <c>/cash-flow-projections</c>, porque <c>/cash-flow</c> es del
/// flujo heredado hasta su retiro (R-04, <see cref="TreasuryGroup.AreasHeredadas"/>). (nuevo)
/// </summary>
public class LiquidityEndpoints : ICarterModule
{
    public const string Ver = "Treasury.Liquidity.View";
    public const string Administrar = "Treasury.Liquidity.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var l = TreasuryGroup.Grupo(app, "/liquidity", "Tesorería Liquidez");
        l.MapGet("/", async (DateOnly? date, int? days, ISender s, CancellationToken ct) => await s.Send(new GetLiquidityPositionQuery(date, days), ct))
            .WithName("Treasury_Liquidity_Position").RequirePermission(new AlgunPermiso(Ver, Administrar));
        l.MapGet("/snapshots", async (DateOnly? from, DateOnly? to, ISender s, CancellationToken ct) => await s.Send(new ListLiquiditySnapshotsQuery(from, to), ct))
            .WithName("Treasury_Liquidity_Snapshots").RequirePermission(new AlgunPermiso(Ver, Administrar));
        l.MapPost("/snapshots", async (FotoRequest? b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new TakeLiquiditySnapshotCommand(b?.Date) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_Liquidity_TakeSnapshot").ConClaveDeOperacion().RequirePermission(Administrar);

        var f = TreasuryGroup.Grupo(app, "/cash-flow-projections", "Tesorería Flujo de caja");
        f.MapGet("/", async (CashFlowPeriodKind? kind, DateOnly? from, int? periods, ISender s, CancellationToken ct) =>
                await s.Send(new GetCashFlowQuery(kind ?? CashFlowPeriodKind.Week, from, periods), ct))
            .WithName("Treasury_CashFlow_Get").RequirePermission(new AlgunPermiso(Ver, Administrar));
        f.MapGet("/categories", async (ISender s, CancellationToken ct) => await s.Send(new ListCashFlowCategoriesQuery(), ct))
            .WithName("Treasury_CashFlow_Categories").RequirePermission(new AlgunPermiso(Ver, Administrar));
        f.MapPost("/categories", async (CategoriaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SaveCashFlowCategoryCommand(null, b.Code ?? string.Empty, b.Name ?? string.Empty, b.Direction ?? CashFlowDirection.Inflow,
                    b.Source ?? CashFlowCategorySource.Manual, b.SortOrder ?? 0, b.IsActive ?? true) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_CashFlow_CreateCategory").ConClaveDeOperacion().RequirePermission(Administrar);
        f.MapPut("/categories/{id:guid}", async (Guid id, CategoriaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SaveCashFlowCategoryCommand(id, b.Code ?? string.Empty, b.Name ?? string.Empty, b.Direction ?? CashFlowDirection.Inflow,
                    b.Source ?? CashFlowCategorySource.Manual, b.SortOrder ?? 0, b.IsActive ?? true) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_CashFlow_UpdateCategory").ConClaveDeOperacion().RequirePermission(Administrar);
        f.MapPost("/entries", async (ProyeccionRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new SaveCashFlowProjectionCommand(b.CategoryPublicId ?? Guid.Empty, b.PeriodKind ?? CashFlowPeriodKind.Week,
                    b.PeriodStart ?? DateOnly.MinValue, b.Amount ?? 0m, b.Notes) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_CashFlow_SaveEntry").ConClaveDeOperacion().RequirePermission(Administrar);

        var t = TreasuryGroup.Grupo(app, "/dashboard", "Tesorería Tablero");
        t.MapGet("/", async (ISender s, CancellationToken ct) => await s.Send(new GetTreasuryDashboardQuery(), ct))
            .WithName("Treasury_Dashboard").RequirePermission(new AlgunPermiso(Ver, Administrar));
    }

    public sealed record FotoRequest(DateOnly? Date);

    public sealed record CategoriaRequest(string? Code, string? Name, CashFlowDirection? Direction, CashFlowCategorySource? Source, int? SortOrder, bool? IsActive);

    public sealed record ProyeccionRequest(Guid? CategoryPublicId, CashFlowPeriodKind? PeriodKind, DateOnly? PeriodStart, decimal? Amount, string? Notes);
}
