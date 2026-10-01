using System.Text.Json;
using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Core.PaymentMeans;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// El catálogo de medios de pago de Core (feature 012; contracts/api.md §22.1–§22.2; T25, T627): medios
/// (<c>/api/core/payment-means</c>, con la plantilla 11 y su importación), franquicias (<c>/card-networks</c>), adquirentes
/// (<c>/card-acquirers</c>), datáfonos de cobro (<c>/card-terminals</c>) y denominaciones (<c>/cash-denominations</c>). Consulta y
/// plantilla vacía con <c>Core.PaymentMeans.View</c>; escritura e importación con <c>Core.PaymentMeans.Manage</c> e
/// <c>Idempotency-Key</c>. Dónde se ofrece cada medio es del módulo comercial (<c>/api/inventory/payment-means/{id}/availability</c>,
/// en <see cref="Inventory.PointsOfSaleEndpoints"/>). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PaymentMeansEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Core.PaymentMeans.View";
    public const string PermisoDeEscritura = "Core.PaymentMeans.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Medios(app);
        Franquicias(app);
        Adquirentes(app);
        Datafonos(app);
        Denominaciones(app);
    }

    private static void Medios(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/payment-means")
            .WithTags("Core Payment Means")
            .RequireAuthorization();

        group.MapGet("/", async (PaymentMeansClass? @class, bool? active, DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPaymentMeansQuery(@class, active, asOf), ct))
            .WithName("Core_PaymentMeans_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPaymentMeansQuery(id), ct))
            .WithName("Core_PaymentMeans_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (PaymentMeansInput body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreatePaymentMeansCommand(body) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/core/payment-means/{result.Value}", new { paymentMeansPublicId = result.Value })
                    : result;
            })
            .WithName("Core_PaymentMeans_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        // El cuerpo es PaymentMeansInput más «reason» obligatorio (IConMotivo, §22.1): se lee dos veces del mismo JSON.
        group.MapPut("/{id:guid}", async (Guid id, JsonElement body, HttpContext http, ISender sender, IOptions<JsonOptions> json, CancellationToken ct) =>
            {
                var opciones = json.Value.SerializerOptions;
                var medio = body.Deserialize<PaymentMeansInput>(opciones) ?? new PaymentMeansInput();
                var motivo = body.Deserialize<ConMotivoRequest>(opciones)?.Reason ?? string.Empty;
                return await sender.Send(new UpdatePaymentMeansCommand(id, medio, motivo) { OperationKey = http.ClaveDeOperacion() }, ct);
            })
            .WithName("Core_PaymentMeans_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeletePaymentMeansCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_PaymentMeans_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        // Plantilla 11 (contracts/plantillas.md §11): descarga vacía desde I1 (T238), importación desde I3 (T627).
        group.MapPlantilla(PermisoDeConsulta, PermisoDeEscritura, CatalogoDePlantillas.MediosDePagoClave, "Core_PaymentMeans",
            importar: (modo, archivo, motivo, clave) => new ImportPaymentMeansCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });
    }

    private static void Franquicias(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/card-networks")
            .WithTags("Core Card Networks")
            .RequireAuthorization();

        group.MapGet("/", async (bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCardNetworksQuery(active), ct))
            .WithName("Core_CardNetworks_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCardNetworkQuery(id), ct))
            .WithName("Core_CardNetworks_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (FranquiciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCardNetworkCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.CardKind, body.IsActive ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/core/card-networks/{result.Value.CardNetworkPublicId}", result.Value) : result;
            })
            .WithName("Core_CardNetworks_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}", async (Guid id, FranquiciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateCardNetworkCommand(id, body.Name ?? string.Empty, body.CardKind, body.IsActive ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Core_CardNetworks_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteCardNetworkCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_CardNetworks_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);
    }

    private static void Adquirentes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/card-acquirers")
            .WithTags("Core Card Acquirers")
            .RequireAuthorization();

        group.MapGet("/", async (bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCardAcquirersQuery(active), ct))
            .WithName("Core_CardAcquirers_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCardAcquirerQuery(id), ct))
            .WithName("Core_CardAcquirers_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (AdquirenteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCardAcquirerCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.PersonPublicId, body.IsActive ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/core/card-acquirers/{result.Value.CardAcquirerPublicId}", result.Value) : result;
            })
            .WithName("Core_CardAcquirers_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}", async (Guid id, AdquirenteRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateCardAcquirerCommand(id, body.Name ?? string.Empty, body.PersonPublicId, body.IsActive ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Core_CardAcquirers_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteCardAcquirerCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_CardAcquirers_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);
    }

    private static void Datafonos(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/card-terminals")
            .WithTags("Core Card Terminals")
            .RequireAuthorization();

        group.MapGet("/", async (Guid? cardAcquirerPublicId, bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCardTerminalsQuery(cardAcquirerPublicId, active), ct))
            .WithName("Core_CardTerminals_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCardTerminalQuery(id), ct))
            .WithName("Core_CardTerminals_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (DatafonoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCardTerminalCommand(
                    body.Code ?? string.Empty, body.CardAcquirerPublicId ?? Guid.Empty, body.Serial, body.Description, body.IsActive ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/core/card-terminals/{result.Value.CardTerminalPublicId}", result.Value) : result;
            })
            .WithName("Core_CardTerminals_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}", async (Guid id, DatafonoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateCardTerminalCommand(id, body.CardAcquirerPublicId ?? Guid.Empty, body.Serial, body.Description, body.IsActive ?? true)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Core_CardTerminals_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteCardTerminalCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_CardTerminals_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);
    }

    private static void Denominaciones(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/cash-denominations")
            .WithTags("Core Cash Denominations")
            .RequireAuthorization();

        group.MapGet("/", async (DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCashDenominationsQuery(asOf), ct))
            .WithName("Core_CashDenominations_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCashDenominationQuery(id), ct))
            .WithName("Core_CashDenominations_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (DenominacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCashDenominationCommand(
                    body.Kind, body.Value, body.ValidFrom ?? DateOnly.MinValue, body.ValidTo, body.DisplayOrder ?? 0, body.RetiresPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/core/cash-denominations/{result.Value.CashDenominationPublicId}", result.Value)
                    : result;
            })
            .WithName("Core_CashDenominations_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}", async (Guid id, DenominacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateCashDenominationCommand(id, body.DisplayOrder ?? 0, body.IsActive ?? true, body.ValidTo)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Core_CashDenominations_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteCashDenominationCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Core_CashDenominations_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);
    }

    /// <summary>El motivo que acompaña al <see cref="PaymentMeansInput"/> en la edición (§22.1).</summary>
    public sealed record ConMotivoRequest(string? Reason);

    /// <summary>El cuerpo de una franquicia (§22.2). En la edición el código se ignora.</summary>
    public sealed record FranquiciaRequest(string? Code, string? Name, CardKind CardKind, bool? IsActive);

    /// <summary>El cuerpo de un adquirente (§22.2). En la edición el código se ignora.</summary>
    public sealed record AdquirenteRequest(string? Code, string? Name, Guid? PersonPublicId, bool? IsActive);

    /// <summary>El cuerpo de un datáfono de cobro (§22.2). En la edición el código se ignora.</summary>
    public sealed record DatafonoRequest(string? Code, Guid? CardAcquirerPublicId, string? Serial, string? Description, bool? IsActive);

    /// <summary>
    /// El cuerpo de una denominación (§22.2). El alta usa <c>kind</c>, <c>value</c>, <c>validFrom</c>, <c>validTo</c>, <c>displayOrder</c>
    /// y <c>retiresPublicId</c> (la que reemplaza); la edición sólo <c>displayOrder</c>, <c>isActive</c> y <c>validTo</c>.
    /// </summary>
    public sealed record DenominacionRequest(
        CashDenominationKind Kind, decimal Value, DateOnly? ValidFrom, DateOnly? ValidTo, short? DisplayOrder, bool? IsActive, Guid? RetiresPublicId);
}
