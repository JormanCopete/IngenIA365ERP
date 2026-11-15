using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.Application.Transport.Imports;

namespace IngenIA365ERP.API.Endpoints.Transport;

/// <summary>
/// Las rutas de las plantillas de Transporte (feature 020, R8; contracts/api.md §3): <c>GET …/template.xlsx</c> y <c>POST …/import?mode=review|apply
/// &amp;existing=update|keep</c>, con la mecánica común de la 016 (<see cref="RutasDePlantilla"/>). Cuelgan de los grupos de Transporte, así
/// que pasan por la puerta del módulo.
/// </summary>
internal static class TransportImports
{
    /// <summary><c>/vehicles/template.xlsx</c>, <c>/vehicles/import</c>, <c>/vehicles/changes/template.xlsx</c> y <c>/vehicles/changes/import</c>.</summary>
    public static void MapPlantillasDeVehiculos(RouteGroupBuilder vehiculos)
    {
        vehiculos.MapPlantillaDePlataformaConExistentes(PlantillaDeVehiculos.Clave, "Transport_Vehicles",
            (modo, archivo, motivo, existentes, clave) => new ImportVehiclesCommand(modo, archivo, motivo, existentes) { OperationKey = clave });

        var cambios = vehiculos.MapGroup("/changes");
        cambios.MapPlantillaDePlataformaConExistentes(PlantillaDeCambiosDeVehiculo.Clave, "Transport_VehicleChanges",
            (modo, archivo, motivo, existentes, clave) => new ImportVehicleChangesCommand(modo, archivo, motivo, existentes) { OperationKey = clave });
    }
}
