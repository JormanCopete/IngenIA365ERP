using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Regulatory.Captaciones;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending.Deposits;

/// <summary>
/// Los formatos regulatorios de captaciones (feature 014, US17, T221; contracts/api.md §18.2; FR-111, FR-112; research §25), sobre <see cref="LendingGroup"/>:
/// <c>POST /api/lending/regulatory/deposit-formats/{formatCode}/validate</c> (consulta por POST, sin clave: no guarda) y <c>/generate</c> (con
/// <c>Idempotency-Key</c>), ambos con <c>Lending.RegulatoryReports.Generate</c>. <c>{formatCode}</c>: <c>sicses-captaciones</c>, <c>sicses-cotitulares</c>,
/// <c>sicses-aportes</c>, <c>sicses-saldos-diarios</c>, <c>sicses-fondo-liquidez</c> (con <c>month</c>), <c>exogena-1019</c> y <c>exogena-1020</c> (sólo
/// <c>year</c>). Una generación con bloqueantes deja la versión <c>Validated</c> sin archivo y responde 422 <c>Lending.Regulatory.BlockingIssues</c>, como la
/// 013. Listar, leer, descargar y marcar cargado son las rutas <c>/files</c> de la 013 (filtro nuevo <c>format</c>). (nuevo)
/// </summary>
public class DepositRegulatoryEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var formatos = LendingGroup.Grupo(app, "/regulatory/deposit-formats", "Cartera Regulatorios de captaciones");

        formatos.MapPost("/{formatCode}/validate", async (string formatCode, PeriodoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ValidateDepositRegulatoryFileQuery(formatCode, body.Year, body.Month), ct))
            .WithName("Lending_Regulatory_DepositFormats_Validate")
            .RequirePermission(Lending.RegulatoryEndpoints.GenerarSicses);

        formatos.MapPost("/{formatCode}/generate", async (string formatCode, GenerarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                Lending.RegulatoryEndpoints.Generado(await sender.Send(
                    new GenerateDepositRegulatoryFileCommand(formatCode, body.Year, body.Month, body.AcknowledgeWarnings)
                    {
                        OperationKey = http.ClaveDeOperacion(),
                    }, ct)))
            .WithName("Lending_Regulatory_DepositFormats_Generate")
            .ConClaveDeOperacion()
            .RequirePermission(Lending.RegulatoryEndpoints.GenerarSicses);
    }

    /// <summary>El período: el año y, en el SICSES, el mes.</summary>
    public sealed record PeriodoRequest(int Year, int? Month);

    /// <summary>Generar: el período y si se reconocen las advertencias («formato sin cotejar»…).</summary>
    public sealed record GenerarRequest(int Year, int? Month, bool AcknowledgeWarnings);
}
