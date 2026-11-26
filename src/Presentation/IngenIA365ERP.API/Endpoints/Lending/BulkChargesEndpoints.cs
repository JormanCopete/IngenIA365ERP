using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.BulkCharges;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los cargos masivos de Cartera (2026-11-25, pedido del dueño; feature 026; specs/026-cargos-masivos/contracts/api.md): una corrida crea una obligación de
/// una línea marcada por cada asociado, sin solicitud ni giro, y la contabiliza en un solo comprobante <c>CO</c>. Ver exige <c>Lending.BulkCharges.Read</c>;
/// preparar, recalcular, descartar y confirmar, <c>.Run</c>; anular, <c>.Void</c>. Toda escritura lleva <c>Idempotency-Key</c>. Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class BulkChargesEndpoints : ICarterModule
{
    public const string Ver = "Lending.BulkCharges.Read";
    public const string Correr = "Lending.BulkCharges.Run";
    public const string Anular = "Lending.BulkCharges.Void";

    public sealed record ConfirmarRequest(string ExpectedPreviewSha256);

    public sealed record MotivoRequest(string Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var cargos = LendingGroup.Grupo(app, "/bulk-charges", "Cartera Cargos masivos");

        // HU4: el plano de una corrida en borrador con origen «plano» (GET /plano/template.xlsx y POST /plano/import?mode=review|apply), en su grupo base.
        LendingGroup.Grupo(app, "/bulk-charges/plano", "Cartera Cargos masivos").MapPlantilla(
            () => PlantillaDeCargosMasivos.Definicion, "plantilla-cartera-cargos-masivos", Correr, Correr, "Lending_BulkCharges_File",
            "Lending.Reports.ExportPersonalData", IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingReportExported,
            importar: (modo, archivo, motivo, clave) => new ImportBulkChargeLinesCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });

        cargos.MapGet("/options", async (ISender sender, CancellationToken ct) => await sender.Send(new GetBulkChargeOptionsQuery(), ct))
            .WithName("Lending_BulkCharges_Options")
            .RequirePermission(Ver);

        cargos.MapGet("/", async (Guid? creditLinePublicId, int? period, BulkChargeRunStatus? status, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListBulkChargeRunsQuery(creditLinePublicId, period, status, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_BulkCharges_List")
            .RequirePermission(Ver);

        cargos.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetBulkChargeRunQuery(id), ct))
            .WithName("Lending_BulkCharges_Get")
            .RequirePermission(Ver);

        cargos.MapGet("/{id:guid}/lines", async (Guid id, BulkChargeLineStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListBulkChargeLinesQuery(id, status, page ?? 1, pageSize ?? 100), ct))
            .WithName("Lending_BulkCharges_Lines")
            .RequirePermission(Ver);

        cargos.MapPost("/", async (BulkChargeRunInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreateBulkChargeRunCommand(body) { OperationKey = http.ClaveDeOperacion() }, ct),
                    r => $"{LendingGroup.Prefijo}/bulk-charges/{r.PublicId}"))
            .WithName("Lending_BulkCharges_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Correr);

        cargos.MapPut("/{id:guid}", async (Guid id, BulkChargeRunInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateBulkChargeRunCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_BulkCharges_Update")
            .ConClaveDeOperacion()
            .RequirePermission(Correr);

        cargos.MapPost("/{id:guid}/preview", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateBulkChargeRunCommand(id, null) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_BulkCharges_Preview")
            .ConClaveDeOperacion()
            .RequirePermission(Correr);

        cargos.MapPost("/{id:guid}/discard", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardBulkChargeRunCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_BulkCharges_Discard")
            .ConClaveDeOperacion()
            .RequirePermission(Correr);

        cargos.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmarRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ConfirmBulkChargeRunCommand(id, body.ExpectedPreviewSha256) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_BulkCharges_Confirm")
            .ConClaveDeOperacion()
            .RequirePermission(Correr);

        cargos.MapPost("/{id:guid}/void", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new VoidBulkChargeRunCommand(id, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_BulkCharges_Void")
            .ConClaveDeOperacion()
            .RequirePermission(Anular);

        cargos.MapPost("/{id:guid}/lines/{lineId:guid}/void", async (Guid id, Guid lineId, MotivoRequest body, HttpContext http, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new VoidBulkChargeLineCommand(id, lineId, body.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_BulkCharges_VoidLine")
            .ConClaveDeOperacion()
            .RequirePermission(Anular);
    }
}
