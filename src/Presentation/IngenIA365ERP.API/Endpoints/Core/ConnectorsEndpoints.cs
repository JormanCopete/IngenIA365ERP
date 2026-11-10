using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.Connectors;
using IngenIA365ERP.Application.Transport;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// Los conectores externos de la cooperativa (feature 020, US8; R16; contracts/api.md §2): los que el maestro le habilitó, con
/// <c>Core.Connectors.View</c>; poner las credenciales y probar la conexión con <c>Core.Connectors.Configure</c>. Ninguna respuesta lleva el
/// contenido de una credencial ni el token del sistema externo. Cada ruta sólo reenvía al <see cref="ISender"/>.
/// </summary>
public sealed class ConnectorsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/core/connectors")
            .WithTags("Core Conectores externos")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        g.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new ListTenantConnectorsQuery(), ct))
            .WithName("Core_Connectors_List")
            .RequirePermission(TransportPermissions.ConnectorsView);

        g.MapPut("/{id:guid}/credentials", async (Guid id, CredencialesRequest body, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new SetConnectorCredentialsCommand(id, body.Fields ?? new Dictionary<string, string>()), ct);
                return r.IsSuccess ? Results.NoContent() : (object)r;
            })
            .WithName("Core_Connectors_SetCredentials")
            .RequirePermission(TransportPermissions.ConnectorsConfigure);

        g.MapPost("/{id:guid}/test", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new TestConnectorCommand(id), ct))
            .WithName("Core_Connectors_Test")
            .RequirePermission(TransportPermissions.ConnectorsConfigure);
    }

    /// <summary><c>{ fields: { username, password } }</c>: un campo vacío conserva el valor anterior.</summary>
    public sealed record CredencialesRequest(Dictionary<string, string>? Fields);
}
