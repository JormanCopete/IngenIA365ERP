using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los órganos de decisión (feature 013, T0313, T64; contracts/api.md §3.6): consejo, junta, comités y gerencia, con sus reglas de quórum y
/// mayoría versionadas y sus miembros con vigencia. Leer exige <c>Lending.GoverningBodies.View</c> (de los miembros sólo nombre y cargo, C84);
/// toda escritura <c>Lending.GoverningBodies.Manage</c> con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class GoverningBodiesEndpoints : ICarterModule
{
    public const string Ver = "Lending.GoverningBodies.View";
    public const string Administrar = "Lending.GoverningBodies.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var organos = LendingGroup.Grupo(app, "/governing-bodies", "Cartera Órganos");

        organos.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListGoverningBodiesQuery(includeInactive ?? false), ct))
            .WithName("Lending_GoverningBodies_List")
            .RequirePermission(Ver);

        organos.MapGet("/{id:guid}", async (Guid id, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetGoverningBodyQuery(id, date), ct))
            .WithName("Lending_GoverningBodies_Get")
            .RequirePermission(Ver);

        organos.MapPost("/", async (CrearOrganoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateGoverningBodyCommand(
                    body.Code ?? string.Empty, body.Name ?? string.Empty, body.Kind, body.DecidesCredit, body.MinutesPrefix ?? string.Empty, body.Rules!)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), o => $"/api/lending/governing-bodies/{o.PublicId}"))
            .WithName("Lending_GoverningBodies_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        organos.MapPost("/{id:guid}/rules", async (Guid id, ReglasDeOrganoInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddGoverningBodyRulesVersionCommand(id, body)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/governing-bodies/{id}"))
            .WithName("Lending_GoverningBodies_AddRules")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        organos.MapPost("/{id:guid}/members", async (Guid id, NombrarMiembroRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddGoverningBodyMemberCommand(
                    id, body.PersonPublicId, body.Position, body.Votes, body.ValidFrom, body.AlternateForMemberPublicId, body.AppointmentNotes,
                    body.AppointmentAttachmentPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/governing-bodies/{id}"))
            .WithName("Lending_GoverningBodies_AddMember")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        organos.MapPost("/{id:guid}/members/{memberId:guid}/end", async (Guid id, Guid memberId, TerminarRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new EndGoverningBodyMemberCommand(id, memberId, body.ValidTo, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_GoverningBodies_EndMember")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        organos.MapPost("/{id:guid}/deactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeactivateGoverningBodyCommand(id, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_GoverningBodies_Deactivate")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }

    /// <summary>El cuerpo del alta (§3.6): el órgano con su primera versión de reglas.</summary>
    public sealed record CrearOrganoRequest(
        string? Code,
        string? Name,
        GoverningBodyKind Kind,
        bool DecidesCredit,
        string? MinutesPrefix,
        ReglasDeOrganoInput? Rules);

    /// <summary>El cuerpo del nombramiento (§3.6); el suplente nombra a su principal.</summary>
    public sealed record NombrarMiembroRequest(
        Guid PersonPublicId,
        BodyMemberRole Position,
        bool Votes,
        DateOnly ValidFrom,
        Guid? AlternateForMemberPublicId,
        string? AppointmentNotes,
        Guid? AppointmentAttachmentPublicId);

    /// <summary>El fin de una vigencia con su motivo.</summary>
    public sealed record TerminarRequest(DateOnly ValidTo, string? Reason);

    /// <summary>Un motivo.</summary>
    public sealed record MotivoRequest(string? Reason);
}
