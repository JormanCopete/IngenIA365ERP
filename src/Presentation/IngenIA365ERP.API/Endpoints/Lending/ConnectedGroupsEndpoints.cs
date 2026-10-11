using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Origination.ConnectedGroups;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los grupos conectados (feature 013, US2, T0393; FR-041, SC-022; DT §7.10 C112): ver (auditado como <c>Lending.RelatedParty.Viewed</c>, con
/// <c>Lending.RelatedParties.Read</c>, como los vinculados), registrar un grupo con su fundamento, agregar y retirar miembros y terminarlo con
/// motivo (<c>Lending.RelatedParties.Manage</c>, también en el handler, e <c>Idempotency-Key</c>). La concentración se mide por deudor y por
/// grupo con el mismo límite. Es la pestaña «Grupos conectados» de <c>/cartera/vinculados</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class ConnectedGroupsEndpoints : ICarterModule
{
    public const string Leer = RelatedPartiesEndpoints.Leer;
    public const string Administrar = RelatedPartiesEndpoints.Administrar;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var grupos = LendingGroup.Grupo(app, "/connected-groups", "Cartera Grupos conectados");

        grupos.MapGet("/", async (Guid? personPublicId, bool? includeEnded, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListConnectedGroupsQuery(personPublicId, includeEnded ?? false), ct))
            .WithName("Lending_ConnectedGroups_List")
            .RequirePermission(Leer);

        grupos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetConnectedGroupQuery(id), ct))
            .WithName("Lending_ConnectedGroups_Get")
            .RequirePermission(Leer);

        grupos.MapPost("/", async (RegistrarGrupoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new RegisterConnectedGroupCommand(
                    body.Name ?? string.Empty, body.Basis, body.Grounds ?? string.Empty, body.MemberPublicIds ?? [], body.SupportAttachmentPublicId,
                    body.ValidFrom)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), g => $"/api/lending/connected-groups/{g.PublicId}"))
            .WithName("Lending_ConnectedGroups_Register")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        grupos.MapPost("/{id:guid}/members", async (Guid id, MiembroRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new AddConnectedGroupMemberCommand(id, body.PersonPublicId, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_ConnectedGroups_AddMember")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        grupos.MapPost("/{id:guid}/members/{personId:guid}/end", async (Guid id, Guid personId, MotivoRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new EndConnectedGroupMemberCommand(id, personId, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_ConnectedGroups_EndMember")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        grupos.MapPost("/{id:guid}/end", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new EndConnectedGroupCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_ConnectedGroups_End")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El alta de un grupo: nombre, fundamento con su texto, al menos dos miembros, soporte opcional y desde cuándo rige.</summary>
    public sealed record RegistrarGrupoRequest(
        string? Name,
        ConnectedGroupBasis Basis,
        string? Grounds,
        IReadOnlyList<Guid>? MemberPublicIds,
        Guid? SupportAttachmentPublicId,
        DateOnly? ValidFrom);

    /// <summary>Un miembro nuevo con el motivo.</summary>
    public sealed record MiembroRequest(Guid PersonPublicId, string? Reason);

    /// <summary>El motivo de un retiro o de la terminación.</summary>
    public sealed record MotivoRequest(string? Reason);
}
