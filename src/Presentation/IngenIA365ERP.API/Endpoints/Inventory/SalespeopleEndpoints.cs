using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.CreateSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.DeleteSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.UpdateSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Import;
using IngenIA365ERP.Application.Inventory.Salespeople.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Vendedores (feature 012, T428; FR-031, FR-092; contracts/api.md §31): el rol vendedor de una persona del maestro. Se da
/// (o se restaura) y se retira sólo por la operación que escribe a la vez <c>INV_Salespeople</c> y la marca
/// <c>IsSalesperson</c>; la persona no se crea aquí. Lecturas con <c>Inventory.Salespeople.View</c>, escrituras con
/// <c>Inventory.Salespeople.Manage</c> y <c>Idempotency-Key</c>; la plantilla 9 con datos exige además
/// <c>Inventory.Reports.ExportPersonalData</c> (son personas). Las rutas del módulo heredado (<c>GET /{id}</c>,
/// <c>DELETE /{id}</c>) se retiraron sin alias. Cada ruta sólo reenvía al <see cref="ISender"/>.
/// </summary>
public class SalespeopleEndpoints : ICarterModule
{
    private const string Ver = "Inventory.Salespeople.View";
    private const string Administrar = "Inventory.Salespeople.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/salespeople")
            .WithTags("Salespeople")
            .RequireAuthorization();

        group.MapGet("/", async (string? search, bool? includeRetired, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListSalespeopleQuery(search, includeRetired ?? false, page ?? 1, pageSize ?? 20), ct))
            .WithName("Inventory_Salespeople_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(Ver);

        group.MapPost("/", async (CrearVendedorRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateSalespersonCommand
                {
                    PersonPublicId = body.PersonPublicId,
                    SalespersonType = body.SalespersonType,
                    AppliesCommission = body.AppliesCommission,
                    Reason = body.Reason ?? string.Empty,
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/salespeople/{r.Value.SalespersonPublicId}", r.Value) : r;
            })
            .WithName("Inventory_Salespeople_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        group.MapPut("/{id:guid}", async (Guid id, ActualizarVendedorRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateSalespersonCommand
                {
                    PublicId = id,
                    SalespersonType = body.SalespersonType,
                    AppliesCommission = body.AppliesCommission,
                    Reason = body.Reason ?? string.Empty,
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Salespeople_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        group.MapPost("/{id:guid}/retire", async (Guid id, RetirarVendedorRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteSalespersonCommand(id, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Salespeople_Retire")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        group.MapPlantilla(Ver, Administrar, CatalogoDePlantillas.VendedoresClave, "Inventory_Salespeople",
            importar: (modo, archivo, motivo, clave) => new ImportSalespeopleCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
            datos: () => new GetSalespeopleTemplateDataQuery(),
            datosPersonales: true);
    }

    /// <summary>El alta (§31). <c>appliesCommission</c> nulo: no en una nueva, lo que tenía en una restaurada.</summary>
    public sealed record CrearVendedorRequest(Guid PersonPublicId, int? SalespersonType, bool? AppliesCommission, string? Reason);

    public sealed record ActualizarVendedorRequest(int? SalespersonType, bool AppliesCommission, string? Reason);

    public sealed record RetirarVendedorRequest(string? Reason);
}
