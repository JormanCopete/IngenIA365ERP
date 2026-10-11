using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los bloqueos y embargos del deudor (feature 013, US2, T0408; contracts/api.md §7.2, FR-060): la lista (<c>Lending.Loans.Read</c>),
/// registrar uno manual o un embargo y levantarlo con motivo (<c>Lending.Blocks.Manage</c>, con <c>Idempotency-Key</c>). Un bloqueo automático
/// no se levanta a mano (<c>Lending.Block.Automatic</c> dice cómo se levanta solo). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class BlocksEndpoints : ICarterModule
{
    public const string Leer = "Lending.Loans.Read";
    public const string Administrar = "Lending.Blocks.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var bloqueos = LendingGroup.Grupo(app, "/blocks", "Cartera Bloqueos");

        bloqueos.MapGet("/", async (Guid? personPublicId, DebtorBlockReason? kind, bool? active, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListDebtorBlocksQuery(personPublicId, kind, active, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Blocks_List")
            .RequirePermission(Leer);

        bloqueos.MapPost("/", async (BloqueoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterDebtorBlockCommand(
                    body.PersonPublicId, body.Kind, body.Reason ?? string.Empty, body.SupportAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/blocks?personPublicId={body.PersonPublicId}"))
            .WithName("Lending_Blocks_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        bloqueos.MapPost("/{id:guid}/lift", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new LiftDebtorBlockCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_Blocks_Lift")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El bloqueo (§7.2): sólo <c>Manual</c> o <c>Garnishment</c>, con motivo y soporte opcional.</summary>
    public sealed record BloqueoRequest(Guid PersonPublicId, DebtorBlockReason Kind, string? Reason, Guid? SupportAttachmentPublicId);

    /// <summary>El motivo del levantamiento.</summary>
    public sealed record MotivoRequest(string? Reason);
}
