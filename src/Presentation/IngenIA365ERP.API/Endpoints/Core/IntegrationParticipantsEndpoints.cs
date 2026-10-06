using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.IntegrationParticipants;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// El registro de recaudadores y destinos de la integración (feature 013, T0078; T16, C35, C36; contracts/api.md §21.1): la lista con
/// <c>Core.IntegrationParticipants.View</c>; el alta (o el cambio de habilitación) y el retiro con <c>Core.IntegrationParticipants.Manage</c>,
/// motivo e <c>Idempotency-Key</c>. Fuera del grupo <c>/api/lending</c>: no aplica el alcance de Cartera. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class IntegrationParticipantsEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Core.IntegrationParticipants.View";
    public const string PermisoDeAdministracion = "Core.IntegrationParticipants.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var registro = app.MapGroup("/api/core/integration-participants")
            .WithTags("Core Registro de integración")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        registro.MapGet("/", async (IntegrationParticipantClass? @class, IntegrationParticipantRole? role, string? destination, DateOnly? activeOn,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new ListIntegrationParticipantsQuery(@class, role, destination, activeOn), ct))
            .WithName("Core_IntegrationParticipants_List")
            .RequirePermission(PermisoDeConsulta);

        registro.MapPost("/", async (RegistroRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RegisterIntegrationParticipantCommand(
                    body.Code ?? string.Empty, body.Class, body.Role, body.Destination, body.IsEnabled, body.IsTestDouble,
                    body.Adoptions?.Select(a => new AdopcionDto(a.Contract ?? string.Empty, a.Version)).ToList() ?? [],
                    body.ValidFrom, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/core/integration-participants/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Core_IntegrationParticipants_Register")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);

        registro.MapPost("/{id:guid}/retire", async (Guid id, RetiroRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new RetireIntegrationParticipantCommand(id, body.ValidTo, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.NoContent() : r;
            })
            .WithName("Core_IntegrationParticipants_Retire")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);
    }

    /// <summary>Una adopción del cuerpo (§21.1, <c>adoptions: [{ contract, version }]</c>).</summary>
    public sealed record AdopcionRequest(string? Contract, int Version);

    /// <summary>El cuerpo del alta (§21.1).</summary>
    public sealed record RegistroRequest(
        string? Code,
        IntegrationParticipantClass Class,
        IntegrationParticipantRole Role,
        string? Destination,
        bool IsEnabled,
        bool IsTestDouble,
        IReadOnlyList<AdopcionRequest>? Adoptions,
        DateOnly ValidFrom,
        string? Reason);

    /// <summary>El cuerpo del retiro (§21.1, <c>{ validTo, reason }</c>).</summary>
    public sealed record RetiroRequest(DateOnly ValidTo, string? Reason);
}
