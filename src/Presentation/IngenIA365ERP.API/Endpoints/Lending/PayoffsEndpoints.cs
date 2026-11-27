using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Payoffs;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// La recogida de créditos y productos con el desembolso de un crédito nuevo (2026-11-27, pedido del dueño; feature 028, HU1, T016 y T017; api.md §1):
/// <list type="bullet">
/// <item><c>POST /api/lending/payoffs/candidates</c> (<c>Lending.Payoffs.Read</c>): los productos del deudor liquidados a la fecha proyectada de desembolso
/// con la información hasta R, si se pueden recoger y el resumen de lo marcado (<see cref="GetPayoffCandidatesQuery"/>).</item>
/// <item><c>POST /api/lending/payoffs/candidates/pdf</c> (<c>Lending.Payoffs.Read</c>): la misma simulación en PDF con el membrete
/// (<see cref="SimulacionDeRecogidaReport"/>), como la simulación de cancelación del estado de cuenta; queda en la auditoría de exportaciones.</item>
/// </list>
/// Las dos son consultas por POST por el tamaño del cuerpo: no escriben y van sin <c>Idempotency-Key</c> (§1.5). Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class PayoffsEndpoints : ICarterModule
{
    public const string Leer = "Lending.Payoffs.Read";

    /// <summary>La clave con que la simulación impresa queda en la auditoría de exportaciones (no es una vista del centro de informes).</summary>
    public const string ClaveDeLaSimulacion = "payoff-candidates-simulation";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var recogidas = LendingGroup.Grupo(app, "/payoffs", "Cartera Recogidas");

        recogidas.MapPost("/candidates", async (CandidatosRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(body.Consulta(), ct))
            .WithName("Lending_Payoffs_Candidates")
            .RequirePermission(Leer);

        recogidas.MapPost("/candidates/pdf", async (CandidatosRequest body, ISender sender, LendingAuditEmitter auditoria, IDateTimeService reloj,
                CancellationToken ct) =>
            {
                var leida = await sender.Send(body.Consulta() with { ForPrint = true }, ct);
                if (leida.IsFailure) return (object)leida;
                var s = leida.Value;
                var pdf = SimulacionDeRecogidaReport.Generate(s, body.Decimals ?? false, reloj.AhoraLocal);
                var marcados = s.Products.Where(p => p.Selected).Select(p => p.ProductPublicId).ToList();
                await auditoria.EmitirExportacionAsync(ClaveDeLaSimulacion,
                    new { person = body.PersonPublicId, date = s.Date, recordedAsOf = s.RecordedAsOf, products = marcados }, "pdf",
                    marcados.Count == 0 ? s.Products.Count : marcados.Count, ct);
                return Results.File(pdf, "application/pdf", $"{SimulacionDeRecogidaReport.NombreDelArchivo(s)}.pdf");
            })
            .WithName("Lending_Payoffs_Candidates_Pdf")
            .RequirePermission(Leer);
    }

    /// <summary>
    /// El cuerpo de las dos rutas (api.md §1): el deudor, la línea del crédito nuevo, la fecha proyectada de desembolso (D), hasta cuándo se toma la
    /// información (R), lo marcado, el crédito nuevo, la solicitud que se corrige y, para el PDF, si los valores van con centavos.
    /// </summary>
    public sealed record CandidatosRequest(
        Guid PersonPublicId,
        Guid? CreditLinePublicId = null,
        DateOnly? ProjectedDisbursementDate = null,
        DateTime? RecordedAsOf = null,
        IReadOnlyList<PayoffInput>? Selected = null,
        PayoffNewLoanInput? NewLoan = null,
        Guid? ExcludeApplicationPublicId = null,
        bool? Decimals = null)
    {
        public GetPayoffCandidatesQuery Consulta() =>
            new(PersonPublicId, CreditLinePublicId, ProjectedDisbursementDate, RecordedAsOf, Selected, NewLoan, ExcludeApplicationPublicId);
    }
}
