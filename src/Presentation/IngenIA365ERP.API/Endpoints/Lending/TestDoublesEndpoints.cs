using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los dobles de prueba de Cartera (feature 013, US3, T0457; contracts/api.md §20, FR-089, T20): <b>sólo se mapean</b> con
/// <c>Lending:TestDoubles:Enabled = true</c> en un despliegue que no es de producción —el mismo criterio con que
/// <c>DoblesDePruebaDeCartera</c> registra <see cref="TesoreriaDePrueba"/>—; en producción no existen (404). Todas con
/// <c>Lending.TestDoubles.Operate</c>: <c>GET /</c> dice qué dobles hay (la pantalla y el menú lo preguntan), la tesorería de prueba lista
/// las órdenes y anulaciones recibidas y emite el <c>ResultadoDelGiro</c> de una orden como <c>TES</c> (con <c>Idempotency-Key</c>), con la
/// persona que prueba como usuario de origen. US5 suma el recaudador de prueba. (nuevo)
/// </summary>
public class TestDoublesEndpoints : ICarterModule
{
    public const string Operar = "Lending.TestDoubles.Operate";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var configuracion = app.ServiceProvider.GetRequiredService<IConfiguration>();
        var ambiente = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!Publicados(configuracion, ambiente)) return;

        var dobles = LendingGroup.Grupo(app, "/test-doubles", "Cartera Dobles de prueba");

        dobles.MapGet("/", () => Results.Ok(new DoblesDisponiblesDto(Treasury: true, Collector: false)))
            .WithName("Lending_TestDoubles_Available")
            .RequirePermission(Operar);

        dobles.MapGet("/treasury/orders", async (DisbursementOrderStatus? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTestTreasuryOrdersQuery(status), ct))
            .WithName("Lending_TestDoubles_TreasuryOrders")
            .RequirePermission(Operar);

        dobles.MapPost("/treasury/orders/{orderPublicId:guid}/results", async (Guid orderPublicId, ResultadoRequest body, HttpContext http,
                IDateTimeService reloj, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new EmitTestDisbursementResultCommand(orderPublicId, body.Outcome ?? string.Empty,
                    body.Reference ?? string.Empty, body.Date ?? reloj.HoyLocal, body.Amount, body.Reason, body.CheckNumber)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/test-doubles/treasury/orders?orderPublicId={orderPublicId}"))
            .WithName("Lending_TestDoubles_TreasuryResult")
            .ConClaveDeOperacion()
            .RequirePermission(Operar);
    }

    /// <summary>Si las rutas se publican: pedidas por configuración y fuera de producción (la guarda que registra los dobles).</summary>
    public static bool Publicados(IConfiguration configuracion, IHostEnvironment ambiente) =>
        configuracion.GetValue(DoblesDePruebaDeCarteraEnDi.ClaveDeConfiguracion, false) && !ambiente.IsProduction();

    /// <summary>Qué dobles hay en este despliegue.</summary>
    public sealed record DoblesDisponiblesDto(bool Treasury, bool Collector);

    /// <summary>
    /// El resultado del giro (§20): <c>Disbursed</c>, <c>Rejected</c>, <c>CheckNotCashed</c> o <c>Voided</c>, la referencia de Tesorería, la
    /// fecha (hoy si no viene), el valor (el neto de la orden si no viene; otro valor prueba <c>InvalidResult</c>), el motivo y el cheque.
    /// </summary>
    public sealed record ResultadoRequest(string? Outcome, string? Reference, DateOnly? Date, decimal? Amount, string? Reason, string? CheckNumber);
}
