using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.ElectronicInvoicing;

/// <summary>
/// Las resoluciones de numeración DIAN (feature 012, I4, T745; contracts/api.md §24.2; contracts/dian.md §9): la lista con el estado calculado
/// (<c>GET /resolutions?kind=&amp;environment=&amp;status=</c>), el detalle con los documentos numerados por mes (<c>GET /resolutions/{id}</c>),
/// el alta (<c>POST</c> → 201), la corrección mientras no haya números emitidos (<c>PUT /{id}</c>; con números sólo retirarla,
/// <c>Resolution.InUse</c>) y la asociación del prefijo a un canal desde una fecha (<c>POST /{id}/channels</c>). La clave técnica viaja
/// sólo de entrada: toda respuesta la devuelve enmascarada (<c>••••ab12</c>). <c>ElectronicInvoicing.Resolutions.View|Manage</c>; toda
/// escritura con <c>Idempotency-Key</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ResolutionsEndpoints : ICarterModule
{
    public const string Ruta = "/api/electronic-invoicing/resolutions";
    private const string Ver = "ElectronicInvoicing.Resolutions.View";
    private const string Administrar = "ElectronicInvoicing.Resolutions.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta)
            .WithTags("Electronic Invoicing Resolutions")
            .RequireAuthorization();

        group.MapGet("/", async (ResolutionKind? kind, DianEnvironment? environment, string? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListNumberingResolutionsQuery(kind, environment, status), ct))
            .WithName("ElectronicInvoicing_Resolutions_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetNumberingResolutionQuery(id), ct))
            .WithName("ElectronicInvoicing_Resolutions_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapPost("/", async (RegisterNumberingResolutionCommand body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(body with { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/{result.Value.ResolutionPublicId}", result.Value) : result;
            })
            .WithName("ElectronicInvoicing_Resolutions_Register")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        group.MapPut("/{id:guid}", async (Guid id, UpdateNumberingResolutionCommand body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(body with { ResolutionPublicId = id, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("ElectronicInvoicing_Resolutions_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        group.MapPost("/{id:guid}/channels", async (Guid id, LinkResolutionToChannelCommand body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(body with { ResolutionPublicId = id, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("ElectronicInvoicing_Resolutions_LinkChannel")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);
    }
}
