using Carter;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Accounting.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

public class AccountingReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/accounting")
            .WithTags("Accounting Reports")
            .RequireAuthorization();

        // Balance Sheet
        group.MapGet("/balance-sheet", async (int year, int month, Guid? branchId, int? comparisonYear, ISender sender) =>
        {
            var result = await sender.Send(new GetBalanceSheetQuery(year, month, branchId, comparisonYear));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetBalanceSheet");

        group.MapGet("/balance-sheet/pdf", async (int year, int month, Guid? branchId, int? comparisonYear, ISender sender) =>
        {
            var result = await sender.Send(new GetBalanceSheetQuery(year, month, branchId, comparisonYear));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            var pdf = BalanceSheetReport.Generate(result.Value);
            return Results.File(pdf, "application/pdf", $"BalanceGeneral_{year}_{month:D2}.pdf");
        }).WithName("GetBalanceSheetPdf");

        // Income Statement
        group.MapGet("/income-statement", async (int year, int month, Guid? branchId, int? comparisonYear, ISender sender) =>
        {
            var result = await sender.Send(new GetIncomeStatementQuery(year, month, branchId, comparisonYear));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetIncomeStatement");

        group.MapGet("/income-statement/pdf", async (int year, int month, Guid? branchId, int? comparisonYear, ISender sender) =>
        {
            var result = await sender.Send(new GetIncomeStatementQuery(year, month, branchId, comparisonYear));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            var pdf = IncomeStatementReport.Generate(result.Value);
            return Results.File(pdf, "application/pdf", $"EstadoResultados_{year}_{month:D2}.pdf");
        }).WithName("GetIncomeStatementPdf");

        // General Ledger
        group.MapGet("/general-ledger", async (DateTime dateFrom, DateTime dateTo, string? accountFrom, string? accountTo, Guid? branchId, ISender sender) =>
        {
            var result = await sender.Send(new GetGeneralLedgerQuery(dateFrom, dateTo, accountFrom, accountTo, branchId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetGeneralLedger");

        group.MapGet("/general-ledger/pdf", async (DateTime dateFrom, DateTime dateTo, string? accountFrom, string? accountTo, Guid? branchId, ISender sender) =>
        {
            var result = await sender.Send(new GetGeneralLedgerQuery(dateFrom, dateTo, accountFrom, accountTo, branchId));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            var pdf = GeneralLedgerReport.Generate(result.Value);
            return Results.File(pdf, "application/pdf", $"LibroMayor_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf");
        }).WithName("GetGeneralLedgerPdf");

        // Voucher Print
        group.MapGet("/voucher/{id:guid}/pdf", async (Guid id, ISender sender) =>
        {
            // TODO: Implement voucher query by PublicId, build VoucherPrintDto, generate PDF
            return Results.NotFound(new { message = "Voucher query not yet implemented" });
        }).WithName("GetVoucherPdf");

        // Withholding Certificate
        group.MapGet("/withholding-certificate/{personId:guid}/{year:int}/pdf", async (Guid personId, int year, ISender sender) =>
        {
            // TODO: Implement withholding certificate query, build WithholdingCertificateDto, generate PDF
            return Results.NotFound(new { message = "Withholding certificate query not yet implemented" });
        }).WithName("GetWithholdingCertificatePdf");
    }
}
