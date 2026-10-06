using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.ExternalChannels;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// Los canales externos de la cooperativa (feature 013, T0080; T71–T73; contracts/api.md §21.2): la lista con <c>Core.ExternalChannels.View</c>;
/// configurar y verificar con <c>Core.ExternalChannels.Manage</c> e <c>Idempotency-Key</c> (configurar, además, con motivo). Fuera del grupo
/// <c>/api/lending</c>: no aplica el alcance de Cartera. La base sólo guarda la referencia a la credencial; la credencial vive en el Secret
/// <c>erp-canales-externos</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ExternalChannelsEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Core.ExternalChannels.View";
    public const string PermisoDeAdministracion = "Core.ExternalChannels.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var canales = app.MapGroup("/api/core/external-channels")
            .WithTags("Core Canales externos")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        canales.MapGet("/", async (ExternalChannelKind? kind, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListExternalChannelSettingsQuery(kind), ct))
            .WithName("Core_ExternalChannels_List")
            .RequirePermission(PermisoDeConsulta);

        canales.MapPost("/", async (ConfiguracionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new ConfigureExternalChannelCommand(
                    body.Kind, body.BureauCode, body.ChannelCode ?? string.Empty, body.ValidFrom, body.CredentialKey, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/core/external-channels/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Core_ExternalChannels_Configure")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);

        canales.MapPost("/{id:guid}/verify", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VerifyExternalChannelCredentialCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_ExternalChannels_Verify")
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeAdministracion);
    }

    /// <summary>El cuerpo de la configuración (§21.2, <c>{ kind, bureauCode?, channelCode, validFrom, credentialKey?, reason }</c>).</summary>
    public sealed record ConfiguracionRequest(
        ExternalChannelKind Kind,
        string? BureauCode,
        string? ChannelCode,
        DateOnly ValidFrom,
        string? CredentialKey,
        string? Reason);
}
