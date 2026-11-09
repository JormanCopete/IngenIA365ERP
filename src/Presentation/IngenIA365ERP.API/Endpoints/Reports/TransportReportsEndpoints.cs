using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Application.Transport.Reports;
using IngenIA365ERP.Domain.Platform;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// El centro de informes de Transporte (feature 020, T172; R19; FR-054): <c>GET /api/reports/transport</c> es el registro de vistas y cada vista
/// vive en <c>/api/reports/transport/{vista}?format=json|xlsx|pdf</c>, con <c>Transport.Vehicles.View</c> para verla y
/// <c>Transport.Vehicles.Export</c> para exportarla (<c>RequirePermissionWhenExporting</c>: sin él, el mismo 404 que lo inexistente). Está detrás de
/// la puerta del módulo: sin Transporte encendido, 403 <c>Module.NotEnabled</c>. Cada exportación que sale bien deja
/// <c>Transport.Report.Exported</c> en la auditoría. Molde: <c>TreasuryReportsEndpoints</c>.
/// </summary>
public class TransportReportsEndpoints : ICarterModule
{
    public const string Ruta = "/api/reports/transport";
    public const string EventoExportado = "Transport.Report.Exported";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta).WithTags("Transport.Reports").RequireAuthorization().RequireModulo(ModulosHabilitables.Transporte);

        group.MapGet("/", () => Results.Ok(VistasDeTransporte.Vistas))
            .WithName("Reportes_Transporte_Vistas")
            .RequirePermission(TransportPermissions.VehiclesView);

        foreach (var vista in VistasDeTransporte.Vistas)
        {
            group.MapGet($"/{vista.Key}", async ([AsParameters] FiltrosDeInformeDeTransporte filtros, HttpContext http, ISender sender, IServiceProvider servicios,
                    ILogger<TransportReportsEndpoints> logger, CancellationToken ct) =>
                {
                    var resultado = await sender.Send(new TransportReportQuery(vista.Key, filtros), ct);
                    if (EntregaDeInformes.EsExportacion(filtros.Format) && EntregaDeInformes.EsFormatoValido(filtros.Format) && resultado.IsSuccess)
                    {
                        try
                        {
                            await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
                            {
                                Action = EventoExportado,
                                EntityType = "TransportReport",
                                Module = ModuloDeAuditoria.Transport,
                                NewValues = new
                                {
                                    view = vista.Key, format = EntregaDeInformes.Normalizar(filtros.Format), rows = resultado.Value.Filas.Count,
                                    filters = filtros.ParaAuditoria(),
                                },
                            }, ct, false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogWarning(ex, "[Transporte.InformeSinAuditar] La exportación de {Vista} no quedó en la auditoría.", vista.Key);
                        }
                    }
                    return await EntregaDeInformes.EntregarAsync(resultado, filtros.Format, vista.FileName, http);
                })
                .WithName($"Reportes_Transporte_{vista.Key}")
                .WithMetadata(vista)
                .RequirePermission(TransportPermissions.VehiclesView)
                .RequirePermissionWhenExporting(TransportPermissions.VehiclesExport);
        }
    }
}
