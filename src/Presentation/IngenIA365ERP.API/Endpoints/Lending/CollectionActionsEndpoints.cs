using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Collections.Actions;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las gestiones de cobranza y las promesas de pago (feature 013, K4, US12, T0956; contracts/api.md §14.6, FR-119): la lista de gestiones y de promesas
/// (<c>Lending.Collections.Read</c>), registrar una gestión con su promesa opcional y anularla con motivo (<c>Lending.Collections.RecordActivity</c>,
/// con <c>Idempotency-Key</c>). Sólo deudor o codeudor; la ventana legal de la Ley 2300 se evalúa y se sella con la gestión. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class CollectionActionsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Collections.Read";
    public const string Registrar = "Lending.Collections.RecordActivity";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var gestiones = LendingGroup.Grupo(app, "/collection-actions", "Cartera Gestiones de cobranza");

        gestiones.MapGet("/", async ([AsParameters] ListCollectionActionsQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_CollectionActions_List")
            .RequirePermission(Leer);

        gestiones.MapPost("/", async (GestionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RecordCollectionActionCommand(body.LoanPublicId, body.At, body.Channel, body.ContactedPersonPublicId,
                    body.IsDirectContact, body.ResultCode, body.Notes, body.Promise)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), g => $"/api/lending/collection-actions/{g.PublicId}"))
            .WithName("Lending_CollectionActions_Record")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        gestiones.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidCollectionActionCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_CollectionActions_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Registrar);

        var promesas = LendingGroup.Grupo(app, "/promises", "Cartera Promesas de pago");

        promesas.MapGet("/", async ([AsParameters] ListPaymentPromisesQuery query, ISender sender, CancellationToken ct) => await sender.Send(query, ct))
            .WithName("Lending_Promises_List")
            .RequirePermission(Leer);
    }

    /// <summary>El cuerpo de una gestión (§14.6).</summary>
    public sealed record GestionRequest(
        Guid LoanPublicId,
        DateTimeOffset At,
        CollectionActionChannel Channel,
        Guid ContactedPersonPublicId,
        bool IsDirectContact,
        CollectionActionResult ResultCode,
        string? Notes,
        PromiseInput? Promise);

    /// <summary>Un motivo.</summary>
    public sealed record MotivoRequest(string? Reason);
}
