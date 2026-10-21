using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// El CDAT (feature 014, US6, T178; contracts/api.md §5 de la 014), sobre <see cref="LendingGroup"/>: <c>/api/lending/term-deposits</c> —lista, calendario de
/// vencimientos sin titulares, detalle, plan, títulos, simulación, constitución, fondeo por traslado (ruta nueva, research E132), instrucción al vencimiento,
/// renovación, simulación y redención—. Leer exige <c>Lending.Accounts.Read</c> (datos del titular) o <c>.View</c> (calendario); constituir y fondear,
/// <c>Lending.TermDeposits.Constitute</c>; la instrucción y la renovación, <c>.Renew</c>; redimir, <c>.Redeem</c>. Toda escritura con
/// <c>Idempotency-Key</c>; la redención que supera la atribución responde <b>202</b> con la solicitud de aprobación y no mueve saldo. La prórroga
/// automática, el pago de intereses del plan, la reclasificación y el vencido por pagar los hace el ciclo diario, sin ruta. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class TermDepositsEndpoints : ICarterModule
{
    public const string Ver = "Lending.Accounts.View";
    public const string Leer = "Lending.Accounts.Read";
    public const string Constituir = "Lending.TermDeposits.Constitute";
    public const string Renovar = "Lending.TermDeposits.Renew";
    public const string Redimir = "Lending.TermDeposits.Redeem";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var cdat = LendingGroup.Grupo(app, "/term-deposits", "Cartera CDAT");

        cdat.MapGet("/", async (MemberAccountStatus? status, Guid? line, Guid? office, DateOnly? maturityFrom, DateOnly? maturityTo, IssueTermBand? issueTermBand,
                    Guid? personPublicId, bool? negotiated, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTermDepositsQuery(status, line, office, maturityFrom, maturityTo, issueTermBand, personPublicId, negotiated, page ?? 1,
                    pageSize ?? 20), ct))
            .WithName("Lending_TermDeposits_List")
            .RequirePermission(Leer);

        cdat.MapGet("/maturity-calendar", async (DateOnly from, DateOnly to, Guid? office, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetTermDepositMaturityCalendarQuery(from, to, office), ct))
            .WithName("Lending_TermDeposits_MaturityCalendar")
            .RequirePermission(Ver);

        cdat.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetTermDepositQuery(id), ct))
            .WithName("Lending_TermDeposits_Get")
            .RequirePermission(Leer);

        cdat.MapGet("/{id:guid}/schedule", async (Guid id, int? version, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetTermDepositScheduleQuery(id, version), ct))
            .WithName("Lending_TermDeposits_Schedule")
            .RequirePermission(Leer);

        cdat.MapGet("/{id:guid}/titles", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTermDepositTitlesQuery(id), ct))
            .WithName("Lending_TermDeposits_Titles")
            .RequirePermission(Leer);

        // ------------------------------------------------------------------------------------------- constitución y fondeo --
        cdat.MapPost("/simulate", async (SimularCdatRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateTermDepositQuery(body.LinePublicId, body.Amount, body.TermDays, body.InterestPeriodicity, body.Timing, body.StartDate,
                    body.RequestedMarginEa, body.HolderPersonPublicId, body.OfficePublicId, body.Channel), ct))
            .WithName("Lending_TermDeposits_Simulate")
            .RequirePermission(Constituir);

        cdat.MapPost("/", async (ConstituteTermDepositInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ConstituteTermDepositCommand(body) { OperationKey = http.ClaveDeOperacion() }, ct),
                    c => $"/api/lending/term-deposits/{c.Account.PublicId}"))
            .WithName("Lending_TermDeposits_Constitute")
            .ConClaveDeOperacion()
            .RequirePermission(Constituir);

        cdat.MapPost("/{id:guid}/fund", async (Guid id, FondearCdatRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new FundTermDepositCommand(id, body.SourceAccountPublicId, body.Signers ?? [], body.ApprovalPublicId, body.Reason)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_TermDeposits_Fund")
            .ConClaveDeOperacion()
            .RequirePermission(Constituir);

        // ------------------------------------------------------------------------------------- vencimiento y renovación --
        cdat.MapPut("/{id:guid}/maturity-instruction", async (Guid id, InstruccionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetTermDepositMaturityInstructionCommand(id, body.Instruction, body.PaymentDestination, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_TermDeposits_SetMaturityInstruction")
            .ConClaveDeOperacion()
            .RequirePermission(Renovar);

        cdat.MapPost("/{id:guid}/renew", async (Guid id, RenewTermDepositInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RenewTermDepositCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_TermDeposits_Renew")
            .ConClaveDeOperacion()
            .RequirePermission(Renovar);

        // ------------------------------------------------------------------------------------------------------ redención --
        cdat.MapPost("/{id:guid}/redeem/simulate", async (Guid id, SimularRedencionRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateTermDepositRedemptionQuery(id, body.Date), ct))
            .WithName("Lending_TermDeposits_SimulateRedemption")
            .RequirePermission(Redimir);

        cdat.MapPost("/{id:guid}/redeem", async (Guid id, RedimirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                Redimido(await sender.Send(new RedeemTermDepositCommand(id, body.Destination, body.ExpectedPreviewSha256, body.Reason ?? string.Empty,
                    body.ApprovalPublicId) { OperationKey = http.ClaveDeOperacion() }, ct), id))
            .WithName("Lending_TermDeposits_Redeem")
            .ConClaveDeOperacion()
            .RequirePermission(Redimir);
    }

    /// <summary>El estado del resultado de una redención que espera un aprobador presente.</summary>
    public const string PendienteDeAprobacion = "PendingApproval";

    /// <summary>201 con la operación si se aplicó; 202 con la solicitud de aprobación si supera la atribución (api.md §1.6).</summary>
    private static object Redimido(Result<TermDepositRedemptionResultDto> r, Guid cdat)
    {
        if (r.IsFailure) return r;
        return r.Value.Status == PendienteDeAprobacion
            ? Results.Accepted($"/api/inventory/approvals/{r.Value.ApprovalPublicId}", r.Value)
            : Results.Created($"/api/lending/accounts/{cdat}/operations/{r.Value.OperationPublicId}", r.Value);
    }

    /// <summary>El cuerpo de la simulación (api.md §5).</summary>
    public sealed record SimularCdatRequest(Guid LinePublicId, decimal Amount, int TermDays, RepaymentPeriodicity InterestPeriodicity, InterestTiming Timing,
        DateOnly? StartDate, decimal? RequestedMarginEa, Guid? HolderPersonPublicId, Guid? OfficePublicId, string? Channel);

    /// <summary>El cuerpo del fondeo por traslado (research E132): la cuenta origen del titular, sus firmantes y la aprobación si la hubo.</summary>
    public sealed record FondearCdatRequest(Guid SourceAccountPublicId, IReadOnlyList<Guid>? Signers, Guid? ApprovalPublicId, string? Reason);

    /// <summary>El cuerpo de la instrucción al vencimiento.</summary>
    public sealed record InstruccionRequest(MaturityInstruction Instruction, TermDepositDestinationInput? PaymentDestination, string? Reason);

    /// <summary>El cuerpo de la simulación de la redención: la fecha.</summary>
    public sealed record SimularRedencionRequest(DateOnly Date);

    /// <summary>El cuerpo de la redención: el destino, la huella de la vista previa, el motivo y la aprobación si la hubo.</summary>
    public sealed record RedimirRequest(TermDepositRedemptionDestinationInput Destination, string? ExpectedPreviewSha256, string? Reason, Guid? ApprovalPublicId);
}
