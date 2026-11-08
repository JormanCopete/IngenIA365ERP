using IngenIA365ERP.Application.Common.Imports;

namespace IngenIA365ERP.Application.Transport.Imports;

/// <summary>
/// Las plantillas de Transporte (feature 020, R8; FR-025 a FR-027): vehículos, cambios de estado y propietario, documentos, conductores
/// y suscripciones. Entran al catálogo de plantillas de plataforma (<c>CatalogoDePlantillasDePlataforma</c>), que les da la mecánica común
/// de la 016: revisar sin guardar, aplicar todo o nada, <c>?existing=update|keep</c>, errores por fila y columna. Ninguna crea personas, y
/// todas aplican las reglas del alta unitaria (<c>LasPlantillasDeTransporteUsanElAltaUnitaria</c>).
/// </summary>
public static class CatalogoDePlantillasDeTransporte
{
    public static IReadOnlyList<PlantillaDePlataforma> Todas { get; } =
    [
        new(PlantillaDeVehiculos.Definicion, "/api/transport/vehicles", nameof(ImportVehiclesCommand),
            TransportPermissions.VehiclesView, TransportPermissions.VehiclesImport),
        new(PlantillaDeCambiosDeVehiculo.Definicion, "/api/transport/vehicles/changes", nameof(ImportVehicleChangesCommand),
            TransportPermissions.VehiclesView, TransportPermissions.VehiclesImport),
        new(PlantillaDeDocumentosDeVehiculo.Definicion, "/api/transport/documents", nameof(ImportVehicleDocumentsCommand),
            TransportPermissions.DocumentsView, TransportPermissions.DocumentsManage),
        new(PlantillaDeConductores.Definicion, "/api/transport/drivers", nameof(ImportDriversCommand),
            TransportPermissions.DriversView, TransportPermissions.DriversManage),
    ];
}
