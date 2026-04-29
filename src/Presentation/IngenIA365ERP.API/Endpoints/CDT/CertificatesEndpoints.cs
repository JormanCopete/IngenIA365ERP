using Carter;
using IngenIA365ERP.Application.CDT.Certificates.Commands.CancelCertificate;
using IngenIA365ERP.Application.CDT.Certificates.Commands.CreateCertificate;
using IngenIA365ERP.Application.CDT.Certificates.Commands.RenewCertificate;
using IngenIA365ERP.Application.CDT.Certificates.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.CDT;

public class CertificatesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cdt/certificates")
            .WithTags("Certificates")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCertificatesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCertificates");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCertificateByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCertificateById");

        group.MapGet("/near-expiry", async (int? daysAhead, ISender sender) =>
        {
            var result = await sender.Send(new GetCertificatesNearExpiryQuery(daysAhead ?? 30));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetCertificatesNearExpiry");

        group.MapPost("/", async (CreateCertificateCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/cdt/certificates/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCertificate");

        group.MapPost("/{id:guid}/renew", async (Guid id, RenewRequest request, ISender sender) =>
        {
            var command = new RenewCertificateCommand
            {
                CertificatePublicId = id,
                RenewWithInterest = request.RenewWithInterest
            };
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("RenewCertificate");

        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelRequest? request, ISender sender) =>
        {
            var command = new CancelCertificateCommand
            {
                CertificatePublicId = id,
                Reason = request?.Reason
            };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("CancelCertificate");
    }
}

// Request DTOs
public record RenewRequest(bool RenewWithInterest);
public record CancelRequest(string? Reason);
