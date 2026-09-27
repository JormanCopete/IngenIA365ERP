using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// La venta en el punto de venta (feature 012; contracts/api.md §20.2; T630): <c>/api/inventory/pos</c> con la búsqueda exacta del
/// lector (<c>lookup</c>), la venta en borrador ligada a la sesión (<c>drafts</c>, <c>drafts/{id}</c>), sus líneas, suspender,
/// recuperar, descartar y cobrar (<c>checkout</c>). Todo con <c>Inventory.Pos.Sell</c>; toda escritura con <c>Idempotency-Key</c>
/// —un cobro repetido con la misma clave devuelve el mismo resultado, nunca una segunda venta (SC-002)—. Los comandos implementan
/// <c>IOperacionDePuntoDeVenta</c> y se auditan con canal <c>pos</c>. Los 422 que dependen del cuerpo (un medio de crédito sin
/// <c>SellOnCredit</c>) los responde el handler, no el filtro. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PosEndpoints : ICarterModule
{
    public const string PermisoDeVenta = "Inventory.Pos.Sell";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/pos")
            .WithTags("Inventory Point of Sale")
            .RequireAuthorization();

        group.MapGet("/lookup", async (string? code, Guid? cashSession, ISender sender, CancellationToken ct) =>
                await sender.Send(new LookupPosProductQuery(code ?? string.Empty, cashSession ?? Guid.Empty), ct))
            .WithName("Inventory_Pos_Lookup")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeVenta);

        group.MapGet("/drafts", async (Guid? cashSession, Guid? pointOfSale, bool? suspended, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPosDraftsQuery(cashSession, pointOfSale, suspended), ct))
            .WithName("Inventory_Pos_Drafts_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeVenta);

        group.MapGet("/drafts/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPosDraftQuery(id), ct))
            .WithName("Inventory_Pos_Drafts_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeVenta);

        group.MapPost("/drafts", async (AbrirVentaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreatePosDraftCommand(body.CashSessionPublicId ?? Guid.Empty, body.FirstLine)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/inventory/pos/drafts/{result.Value.DraftPublicId}", result.Value) : result;
            })
            .WithName("Inventory_Pos_Drafts_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);

        group.MapPatch("/drafts/{id:guid}", async (Guid id, CabeceraDeVentaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdatePosDraftCommand(
                    id, body.CustomerPersonPublicId, body.ClearCustomer ?? false, body.SalespersonPublicId, body.ClearSalesperson ?? false,
                    body.Role, body.DocumentDiscount, body.Notes)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Pos_Drafts_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);

        group.MapPost("/drafts/{id:guid}/lines", async (Guid id, LecturaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new AddPosLineCommand(id, body.Code, body.ProductPublicId, body.UnitPublicId, body.Quantity)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Pos_Lines_Add")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);

        group.MapPatch("/drafts/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, LineaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdatePosLineCommand(id, lineId, body.Quantity, body.UnitPrice, body.Discount, body.ClearUnitPrice ?? false)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Pos_Lines_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);

        group.MapDelete("/drafts/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RemovePosLineCommand(id, lineId) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Pos_Lines_Remove")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);

        group.MapPost("/drafts/{id:guid}/suspend", async (Guid id, SuspenderRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SuspendPosDraftCommand(id, body?.Label) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Pos_Drafts_Suspend")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);

        group.MapPost("/drafts/{id:guid}/resume", async (Guid id, RecuperarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ResumePosDraftCommand(id, body.CashSessionPublicId ?? Guid.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Pos_Drafts_Resume")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);

        group.MapPost("/drafts/{id:guid}/discard", async (Guid id, MotivoDePosRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardPosDraftCommand(id, body?.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Pos_Drafts_Discard")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);

        group.MapPost("/drafts/{id:guid}/checkout", async (Guid id, CobroRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CheckoutPosDraftCommand(id, body.Payments ?? [], body.ExpectedAmountDue, body.SendEmailTo)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Pos_Drafts_Checkout")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeVenta);
    }

    /// <summary><c>POST /pos/drafts</c>: la sesión abierta del usuario y, si la hay, la primera lectura.</summary>
    public sealed record AbrirVentaRequest(Guid? CashSessionPublicId, PosFirstLineInput? FirstLine);

    /// <summary><c>PATCH /pos/drafts/{id}</c>: cliente, vendedor, rol del documento, descuento por total y notas.</summary>
    public sealed record CabeceraDeVentaRequest(
        Guid? CustomerPersonPublicId, bool? ClearCustomer, Guid? SalespersonPublicId, bool? ClearSalesperson, CashRegisterDocumentRole? Role,
        PosDiscountInput? DocumentDiscount, string? Notes);

    /// <summary>Una lectura: por código (el de empaque trae su unidad; «3*» multiplica) o por producto y unidad.</summary>
    public sealed record LecturaRequest(string? Code, Guid? ProductPublicId, Guid? UnitPublicId, decimal? Quantity);

    /// <summary>El cambio de una línea: cantidad, precio digitado (o volver al de la lista) y descuento.</summary>
    public sealed record LineaRequest(decimal? Quantity, decimal? UnitPrice, PosDiscountInput? Discount, bool? ClearUnitPrice);

    /// <summary>El rótulo con que se suspende la venta.</summary>
    public sealed record SuspenderRequest(string? Label);

    /// <summary>La sesión abierta del usuario en la que se recupera la venta suspendida.</summary>
    public sealed record RecuperarRequest(Guid? CashSessionPublicId);

    /// <summary>El motivo de descartar la venta.</summary>
    public sealed record MotivoDePosRequest(string? Reason);

    /// <summary><c>POST /pos/drafts/{id}/checkout</c>: los pagos, lo que la persona vio a pagar y el correo del comprador.</summary>
    public sealed record CobroRequest(IReadOnlyList<DocumentPaymentInput>? Payments, decimal ExpectedAmountDue, string? SendEmailTo);
}
