using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Origination;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El simulador (feature 013, US2, T0408; contracts/api.md §4.1, FR-034, T29): <c>/simulations/schedule</c> (el plan cuota a cuota, sin
/// registrar nada), <c>/schedule/pdf</c> (el mismo cuerpo en PDF, sin número ni código, con la leyenda «Simulación – no constituye aprobación»)
/// y <c>/schedule/send</c> (un mensaje informativo por correo con el PDF a la persona simulada). Todo con <c>Lending.Simulations.Run</c>. Las
/// dos primeras son consultas por POST por el tamaño del cuerpo y van <b>sin</b> clave (§1.5); el envío escribe la cola y la exige. Cada ruta
/// sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class SimulationsEndpoints : ICarterModule
{
    public const string Simular = "Lending.Simulations.Run";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var simulaciones = LendingGroup.Grupo(app, "/simulations", "Cartera Simulador");

        simulaciones.MapPost("/schedule", async (ScheduleSimulationInput body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateRepaymentScheduleQuery(body), ct))
            .WithName("Lending_Simulations_Schedule")
            .RequirePermission(Simular);

        simulaciones.MapPost("/schedule/pdf", async (ScheduleSimulationInput body, ISender sender, CancellationToken ct) =>
            {
                var pdf = await sender.Send(new RenderScheduleSimulationPdfQuery(body), ct);
                return pdf.IsSuccess ? (object)Results.File(pdf.Value.Contenido, "application/pdf", pdf.Value.NombreArchivo) : pdf;
            })
            .WithName("Lending_Simulations_SchedulePdf")
            .RequirePermission(Simular);

        simulaciones.MapPost("/schedule/send", async (EnviarSimulacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new SendScheduleSimulationCommand(body.Simulation!, body.RecipientPersonPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), m => $"/api/lending/debtor-messages/{m.DebtorMessagePublicId}"))
            .WithName("Lending_Simulations_Send")
            .ConClaveDeOperacion()
            .RequirePermission(Simular);
    }

    /// <summary>El envío (§4.1): la simulación y la persona que la recibe.</summary>
    public sealed record EnviarSimulacionRequest(ScheduleSimulationInput? Simulation, Guid RecipientPersonPublicId);
}
