using Carter;

namespace IngenIA365ERP.API.Endpoints;

public class HealthEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health/details", () =>
        {
            return Results.Ok(new
            {
                Status = "Healthy",
                Application = "IngenIA365ERP",
                Version = "1.0.0",
                Timestamp = DateTime.UtcNow,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            });
        })
        .WithName("HealthDetails")
        .WithTags("Health")
        .Produces(200);
    }
}
