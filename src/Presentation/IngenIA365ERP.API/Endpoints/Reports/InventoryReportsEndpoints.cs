using Carter;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Inventory.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

public class InventoryReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/inventory")
            .WithTags("Inventory Reports")
            .RequireAuthorization();

        // Inventory Valuation
        group.MapGet("/valuation", async (Guid? warehouseId, int? groupId, ISender sender) =>
        {
            var result = await sender.Send(new GetInventoryValuationQuery(warehouseId, groupId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetInventoryValuation");

        group.MapGet("/valuation/pdf", async (Guid? warehouseId, int? groupId, ISender sender) =>
        {
            var result = await sender.Send(new GetInventoryValuationQuery(warehouseId, groupId));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            var pdf = InventoryValuationReport.Generate(result.Value);
            return Results.File(pdf, "application/pdf", $"InventarioValorizado_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }).WithName("GetInventoryValuationPdf");

        // Invoice Print
        group.MapGet("/invoice/{invoiceId:guid}/pdf", async (Guid invoiceId, ISender sender) =>
        {
            // TODO: Query invoice by PublicId, build InvoicePrintDto from entity + items, generate PDF
            // var dto = new InvoicePrintDto(...);
            // var pdf = InvoicePrintReport.Generate(dto);
            // return Results.File(pdf, "application/pdf", $"Factura_{dto.InvoiceNumber}.pdf");
            return Results.NotFound(new { message = "Invoice query not yet implemented" });
        }).WithName("GetInvoicePdf");
    }
}
