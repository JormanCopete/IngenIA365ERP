using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Application.Transport.Documents;
using IngenIA365ERP.Application.Transport.Imports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// Documentos del vehículo y su tablero (feature 020, US4; contracts/api.md §3, «Documentos»): tipos de documento, registrar y corregir, el
/// tablero de vencidos, por vencer y faltantes, y la plantilla <c>transport.documents</c>. Nada de esto bloquea operaciones (FR-023).
/// </summary>
public sealed class DocumentsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var tipos = TransportGroup.Grupo(app, "/document-types", "Transporte Documentos");
        tipos.MapGet("/", async (bool? includeInactive, ISender s, CancellationToken ct) => await s.Send(new ListDocumentTypesQuery(includeInactive ?? false), ct))
            .WithName("Transport_DocumentTypes_List").RequirePermission(new AlgunPermiso(TransportPermissions.DocumentsView, TransportPermissions.CatalogsManage));
        tipos.MapPost("/", async (TipoRequest b, ISender s, CancellationToken ct) =>
                TransportGroup.Creado(await s.Send(new CreateDocumentTypeCommand(b.Code ?? "", b.Name ?? "", b.IsRequired, b.WarningDays), ct),
                    t => $"/api/transport/document-types/{t.PublicId}"))
            .WithName("Transport_DocumentTypes_Create").RequirePermission(TransportPermissions.CatalogsManage);
        tipos.MapPut("/{id:guid}", async (Guid id, TipoRequest b, ISender s, CancellationToken ct) =>
                await s.Send(new UpdateDocumentTypeCommand(id, b.Name ?? "", b.IsRequired, b.WarningDays, b.IsActive ?? true), ct))
            .WithName("Transport_DocumentTypes_Update").RequirePermission(TransportPermissions.CatalogsManage);

        var vehiculos = TransportGroup.Grupo(app, "/vehicles/{vehicleId:guid}/documents", "Transporte Documentos");
        vehiculos.MapPost("/", async (Guid vehicleId, VehicleDocumentInput b, ISender s, CancellationToken ct) =>
                TransportGroup.Creado(await s.Send(new AddVehicleDocumentCommand(vehicleId, b), ct), d => $"/api/transport/vehicles/{vehicleId}/documents/{d}"))
            .WithName("Transport_VehicleDocuments_Add").RequirePermission(TransportPermissions.DocumentsManage);
        vehiculos.MapPut("/{docId:guid}", async (Guid vehicleId, Guid docId, VehicleDocumentInput b, ISender s, CancellationToken ct) =>
                await s.Send(new CorrectVehicleDocumentCommand(vehicleId, docId, b), ct))
            .WithName("Transport_VehicleDocuments_Correct").RequirePermission(TransportPermissions.DocumentsManage);

        var tablero = TransportGroup.Grupo(app, "/documents", "Transporte Documentos");
        tablero.MapGet("/expirations", async (string? status, int? days, bool? includeCurrent, ISender s, CancellationToken ct) =>
                await s.Send(new GetDocumentExpirationsQuery(status, days, includeCurrent ?? false), ct))
            .WithName("Transport_Documents_Expirations").RequirePermission(TransportPermissions.DocumentsView);
        tablero.MapPlantillaDePlataformaConExistentes(PlantillaDeDocumentosDeVehiculo.Clave, "Transport_Documents",
            (modo, archivo, motivo, existentes, clave) => new ImportVehicleDocumentsCommand(modo, archivo, motivo, existentes) { OperationKey = clave });
    }

    public sealed record TipoRequest(string? Code, string? Name, bool IsRequired, int WarningDays, bool? IsActive);
}
