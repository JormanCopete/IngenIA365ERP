using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El pagaré (feature 013, US3, T0457; contracts/api.md §6.2, FR-044, T71–T73): la lista (<c>Lending.Guarantees.Read</c>) y, con
/// <c>Lending.PromissoryNotes.Manage</c>, crear (físico o desmaterializado por el depósito de valores vigente, que queda sellado), actualizar el
/// estado consultando al depósito (<c>POST /{id}/refresh</c>: sin clave, sólo trae lo que el depósito ya sabe; la tarea <c>cartera.pagares</c>
/// hace lo mismo sola), la custodia del físico, endosar y anular. Las demás escrituras llevan <c>Idempotency-Key</c>. En producción un pagaré
/// del canal <c>SIMULADO</c> nunca queda perfeccionado (T73). (nuevo)
/// </summary>
public class PromissoryNotesEndpoints : ICarterModule
{
    public const string Administrar = "Lending.PromissoryNotes.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var pagares = LendingGroup.Grupo(app, "/promissory-notes", "Cartera Pagarés");

        pagares.MapGet("/", async (Guid? loanPublicId, Guid? applicationPublicId, PromissoryNoteStatus? status, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListPromissoryNotesQuery(loanPublicId, applicationPublicId, status, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_PromissoryNotes_List")
            .RequirePermission(CollateralsEndpoints.Leer);

        pagares.MapPost("/", async (PagareRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreatePromissoryNoteCommand(body.OwnerKind ?? string.Empty, body.OwnerPublicId, body.Form,
                    body.PhysicalNumber, body.CustodyLocation)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), p => $"/api/lending/promissory-notes?applicationPublicId={body.OwnerPublicId}"))
            .WithName("Lending_PromissoryNotes_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        pagares.MapPost("/{id:guid}/refresh", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new RefreshPromissoryNoteStatusCommand(id), ct))
            .WithName("Lending_PromissoryNotes_Refresh")
            .RequirePermission(Administrar);

        pagares.MapPost("/{id:guid}/physical-custody", async (Guid id, CustodiaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RegisterPhysicalPromissoryNoteCustodyCommand(id, body.SignedAt, body.CustodyLocation ?? string.Empty,
                    body.AttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_PromissoryNotes_PhysicalCustody")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        pagares.MapPost("/{id:guid}/endorse", async (Guid id, EndosoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new EndorsePromissoryNoteCommand(id, body.EndorseePersonPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_PromissoryNotes_Endorse")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        pagares.MapPost("/{id:guid}/void", async (Guid id, EndosoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidPromissoryNoteCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_PromissoryNotes_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El pagaré (§6.2): el dueño (<c>Application</c> o <c>Loan</c>), la forma y, el físico, su número y custodia.</summary>
    public sealed record PagareRequest(string? OwnerKind, Guid OwnerPublicId, PromissoryNoteKind Form, string? PhysicalNumber, string? CustodyLocation);

    /// <summary>La custodia del pagaré físico firmado, con el adjunto del pagaré escaneado.</summary>
    public sealed record CustodiaRequest(DateTime SignedAt, string? CustodyLocation, Guid AttachmentPublicId);

    /// <summary>El endoso (con endosatario) o la anulación, con motivo.</summary>
    public sealed record EndosoRequest(Guid? EndorseePersonPublicId, string? Reason);
}
