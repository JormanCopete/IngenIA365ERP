using Carter;
using IngenIA365ERP.API.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

public class CDTReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/cdt")
            .WithTags("CDT Reports")
            .RequireAuthorization();

        // CDT Certificate PDF
        group.MapGet("/certificate/{cdtId:guid}/pdf", async (Guid cdtId, ISender sender) =>
        {
            // TODO: Query CDT by PublicId, build CdtCertificateDto from entity + person + company data
            // var cdt = await sender.Send(new GetCdtByIdQuery(cdtId));
            // var dto = new CdtCertificateDto(...);
            // var pdf = CdtCertificateReport.Generate(dto);
            // return Results.File(pdf, "application/pdf", $"CDT_{dto.CertificateNumber}.pdf");
            return Results.NotFound(new { message = "CDT certificate query not yet implemented" });
        }).WithName("GetCdtCertificatePdf");
    }
}
