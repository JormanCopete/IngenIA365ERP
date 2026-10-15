using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.DebtorEvents;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los eventos del deudor, la reclamación al seguro y la insolvencia (feature 013, K3, US11, T0890; contracts/api.md §11.4, FR-097, FR-098, T102):
/// <list type="bullet">
/// <item><c>/debtor-events</c>: la lista (<c>Lending.Loans.Read</c>) y registrar fallecimiento, retiro de la cooperativa o embargo
/// (<c>Lending.DebtorEvents.Manage</c>); el evento se guarda como bloqueo del deudor y el fallecimiento abre la reclamación;</item>
/// <item><c>/insurance-claims</c>: la lista (K1, <c>Lending.Loans.Read</c>) y cambiar el estado a <c>Denied</c> o <c>Withdrawn</c>
/// (<c>Lending.DebtorEvents.Manage</c>); <c>Paid</c> lo pone sólo el contrato cuando la aseguradora paga;</item>
/// <item><c>/insolvency-proceedings</c>: la lista (K1, <c>Lending.Loans.Read</c>), abrir el proceso y registrar su fracaso
/// (<c>Lending.Insolvency.Manage</c>); el acuerdo es una novedad <c>Restructuring</c> ligada al proceso (§11.1).</item>
/// </list>
/// Toda escritura con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DebtorEventsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Loans.Read";
    public const string GestionarEventos = "Lending.DebtorEvents.Manage";
    public const string GestionarInsolvencia = "Lending.Insolvency.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var raiz = LendingGroup.Grupo(app, string.Empty, "Cartera Eventos del deudor");

        raiz.MapGet("/debtor-events", async (Guid? personPublicId, DebtorBlockReason? kind, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListDebtorEventsQuery(personPublicId, kind, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_DebtorEvents_List")
            .RequirePermission(Leer);

        raiz.MapPost("/debtor-events", async (EventoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterDebtorEventCommand(body.PersonPublicId, body.Kind, body.EventDate,
                    body.SupportAttachmentPublicId, body.InsurerPersonPublicId, body.Notes) { OperationKey = http.ClaveDeOperacion() }, ct),
                    $"/api/lending/debtor-events?personPublicId={body.PersonPublicId}"))
            .WithName("Lending_DebtorEvents_Register")
            .ConClaveDeOperacion()
            .RequirePermission(GestionarEventos);

        raiz.MapGet("/insurance-claims", async (InsuranceClaimStatus? status, Guid? loanPublicId, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListInsuranceClaimsQuery(status, loanPublicId, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_InsuranceClaims_List")
            .RequirePermission(Leer);

        raiz.MapPost("/insurance-claims/{id:guid}/status", async (Guid id, EstadoDeReclamacionRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ChangeInsuranceClaimStatusCommand(id, body.Status, body.Reason ?? string.Empty, body.SupportAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_InsuranceClaims_ChangeStatus")
            .ConClaveDeOperacion()
            .RequirePermission(GestionarEventos);

        raiz.MapGet("/insolvency-proceedings", async (InsolvencyProceedingStatus? status, Guid? personPublicId, int? page, int? pageSize,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new ListInsolvencyProceedingsQuery(status, personPublicId, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Insolvency_List")
            .RequirePermission(Leer);

        raiz.MapPost("/insolvency-proceedings", async (InsolvenciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new OpenInsolvencyProceedingCommand(body.PersonPublicId, body.StartDate, body.Regime,
                    body.Authority ?? string.Empty, body.FileNumber, body.SupportAttachmentPublicId) { OperationKey = http.ClaveDeOperacion() }, ct),
                    p => $"/api/lending/insolvency-proceedings?personPublicId={body.PersonPublicId}"))
            .WithName("Lending_Insolvency_Open")
            .ConClaveDeOperacion()
            .RequirePermission(GestionarInsolvencia);

        raiz.MapPost("/insolvency-proceedings/{id:guid}/failure", async (Guid id, FracasoRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new RecordInsolvencyFailureCommand(id, body.Date, body.Reason ?? string.Empty, body.SupportAttachmentPublicId,
                    body.Regime, body.Authority) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Insolvency_Failure")
            .ConClaveDeOperacion()
            .RequirePermission(GestionarInsolvencia);
    }

    /// <summary>El evento (§11.4): la persona, la clase (<c>Deceased</c>, <c>AssociateWithdrawal</c>, <c>Garnishment</c>), la fecha, el soporte y,
    /// en el fallecimiento, la aseguradora si no es la de la línea.</summary>
    public sealed record EventoRequest(Guid PersonPublicId, DebtorBlockReason Kind, DateOnly EventDate, Guid? SupportAttachmentPublicId,
        Guid? InsurerPersonPublicId, string? Notes);

    /// <summary>El estado nuevo de la reclamación (<c>Denied</c> o <c>Withdrawn</c>), con motivo.</summary>
    public sealed record EstadoDeReclamacionRequest(InsuranceClaimStatus Status, string? Reason, Guid? SupportAttachmentPublicId);

    /// <summary>El proceso de insolvencia (§11.4): la persona, el inicio, el régimen, la autoridad, el radicado y el soporte.</summary>
    public sealed record InsolvenciaRequest(Guid PersonPublicId, DateOnly StartDate, InsolvencyRegime Regime, string? Authority, string? FileNumber,
        Guid? SupportAttachmentPublicId);

    /// <summary>El fracaso del proceso: la fecha, el motivo, el soporte y, en lo migrado, el régimen y la autoridad que faltaban (C64).</summary>
    public sealed record FracasoRequest(DateOnly Date, string? Reason, Guid? SupportAttachmentPublicId, InsolvencyRegime? Regime, string? Authority);
}
