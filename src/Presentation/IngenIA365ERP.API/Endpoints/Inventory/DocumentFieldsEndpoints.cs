using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Sales.CamposAdicionales;
using IngenIA365ERP.Domain.Entities.Inventory.Sales;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Los campos adicionales de venta (2026-11-15, pedido del dueño: la placa del vehículo en COOFLOPAL). Las definiciones las administra quien
/// administra los parámetros (<c>Inventory.Parameters.Manage</c>) y las lee quien vende (<c>Inventory.Sales.View</c>); los valores de un
/// documento en borrador los escribe quien crea ventas (<c>Inventory.Sales.Create</c>). Toda escritura con <c>Idempotency-Key</c>.
/// </summary>
public class DocumentFieldsEndpoints : ICarterModule
{
    private const string Ver = "Inventory.Sales.View";
    private const string Administrar = "Inventory.Parameters.Manage";
    private const string Registrar = "Inventory.Sales.Create";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var campos = app.MapGroup("/api/inventory/document-fields").WithTags("DocumentFields").RequireAuthorization();

        campos.MapGet("/", async (bool? activeOnly, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDocumentFieldsQuery(activeOnly ?? false), ct))
            .WithName("Inventory_DocumentFields_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        campos.MapPost("/", async (CampoAdicionalRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(body.Comando(null) with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_DocumentFields_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        campos.MapPut("/{id:guid}", async (Guid id, CampoAdicionalRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(body.Comando(id) with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_DocumentFields_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        var valores = app.MapGroup("/api/inventory/documents/{id:guid}/fields").WithTags("DocumentFields").RequireAuthorization();

        valores.MapGet("/", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetDocumentFieldValuesQuery(id), ct))
            .WithName("Inventory_DocumentFields_Values").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        valores.MapPut("/", async (Guid id, ValoresDeCamposRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetDocumentFieldValuesCommand(id, body.Values ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_DocumentFields_SetValues").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Registrar);
    }

    public sealed record CampoAdicionalRequest(string Code, string Name, DocumentFieldType DataType, int MaxLength, bool IsRequired, bool ShowInSales,
        bool ShowInQuotes, bool PrintOnInvoice, int SortOrder, bool IsActive)
    {
        public SaveDocumentFieldCommand Comando(Guid? id) =>
            new(id, Code, Name, DataType, MaxLength, IsRequired, ShowInSales, ShowInQuotes, PrintOnInvoice, SortOrder, IsActive);
    }

    public sealed record ValoresDeCamposRequest(IReadOnlyList<DocumentFieldValueInput>? Values);
}
