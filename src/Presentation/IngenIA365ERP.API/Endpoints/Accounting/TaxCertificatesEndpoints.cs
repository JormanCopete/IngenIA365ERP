using Carter;
using IngenIA365ERP.Application.Accounting.TaxCertificates.Commands.GenerateTaxCertificates;
using IngenIA365ERP.Application.Accounting.TaxCertificates.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class TaxCertificatesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/tax-certificates")
            .WithTags("TaxCertificates")
            .RequireAuthorization();

        group.MapPost("/generate/{year:int}", async (int year, ISender sender) =>
        {
            var result = await sender.Send(new GenerateTaxCertificatesCommand(year));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GenerateTaxCertificates");

        group.MapGet("/{personId:guid}/{year:int}", async (Guid personId, int year, ISender sender) =>
        {
            var result = await sender.Send(new GetTaxCertificateQuery(personId, year));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetTaxCertificate");

        group.MapGet("/{year:int}", async (int year, ISender sender) =>
        {
            var result = await sender.Send(new ListTaxCertificatesQuery(year));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListTaxCertificates");
    }
}
