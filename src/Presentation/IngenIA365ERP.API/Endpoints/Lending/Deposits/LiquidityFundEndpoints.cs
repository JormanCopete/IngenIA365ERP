using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Deposits.Liquidity;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// El fondo de liquidez y el riesgo de liquidez (feature 014, US15, T217; contracts/api.md §15; FR-025, FR-109; research §24), sobre <see cref="LendingGroup"/>:
/// <c>/api/lending/liquidity-fund</c> —la posición a una fecha, la medición del riesgo (IRL o brecha; nueva en contracts), las fotos y lo constituido
/// (<c>Lending.LiquidityFund.View</c>); tomar la foto, registrar una inversión y terminarla (<c>Lending.LiquidityFund.Manage</c>)—. Sin datos de titulares; los
/// depósitos asegurados son la vista <c>insured-deposits</c> del centro de informes. Toda escritura con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class LiquidityFundEndpoints : ICarterModule
{
    public const string Ver = "Lending.LiquidityFund.View";
    public const string Administrar = "Lending.LiquidityFund.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var fondo = LendingGroup.Grupo(app, "/liquidity-fund", "Cartera Fondo de liquidez");

        fondo.MapGet("/", async (DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLiquidityFundPositionQuery(date), ct))
            .WithName("Lending_LiquidityFund_Position")
            .RequirePermission(Ver);

        fondo.MapGet("/risk", async (DateOnly? date, MedidaDeRiesgoDeLiquidez? measure, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetLiquidityRiskQuery(date, measure ?? MedidaDeRiesgoDeLiquidez.Irl), ct))
            .WithName("Lending_LiquidityFund_Risk")
            .RequirePermission(Ver);

        fondo.MapGet("/snapshots", async (DateOnly? from, DateOnly? to, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLiquidityFundSnapshotsQuery(from, to), ct))
            .WithName("Lending_LiquidityFund_Snapshots")
            .RequirePermission(Ver);

        fondo.MapPost("/snapshots", async (FotoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new TakeLiquidityFundSnapshotCommand(body.Date, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_LiquidityFund_TakeSnapshot")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        fondo.MapGet("/holdings", async (DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLiquidityFundHoldingsQuery(date), ct))
            .WithName("Lending_LiquidityFund_Holdings")
            .RequirePermission(Ver);

        fondo.MapPost("/holdings", async (InversionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RegisterLiquidityFundHoldingCommand(body.Instrument, body.EntityPersonPublicId, body.Reference ?? string.Empty, body.Amount,
                    body.ConstitutedOn, body.MaturityDate, body.SupportAttachmentPublicId, body.Note, body.PublicId, body.TermTitleKind, body.AccruedInterest ?? 0m,
                    body.CapitalizesInterest ?? false)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_LiquidityFund_RegisterHolding")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        fondo.MapPost("/holdings/{holdingId:guid}/end", async (Guid holdingId, TerminarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new EndLiquidityFundHoldingCommand(holdingId, body.EndedOn, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() },
                    ct))
            .WithName("Lending_LiquidityFund_EndHolding")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo de <c>POST /snapshots</c>: la fecha (hoy si no viene) y el motivo opcional.</summary>
    public sealed record FotoRequest(DateOnly? Date, string? Reason);

    /// <summary>
    /// El cuerpo de <c>POST /holdings</c> (api.md §15, con <c>reference</c> y el <c>publicId</c> que la pantalla reservó para el soporte, research E210).
    /// </summary>
    public sealed record InversionRequest(
        LiquidityHoldingKind Instrument,
        Guid EntityPersonPublicId,
        string? Reference,
        decimal Amount,
        DateOnly ConstitutedOn,
        DateOnly? MaturityDate,
        Guid? SupportAttachmentPublicId,
        string? Note,
        Guid? PublicId,
        string? TermTitleKind = null,
        decimal? AccruedInterest = null,
        bool? CapitalizesInterest = null);

    /// <summary>El cuerpo de <c>POST /holdings/{holdingId}/end</c>.</summary>
    public sealed record TerminarRequest(DateOnly EndedOn, string? Reason);
}
