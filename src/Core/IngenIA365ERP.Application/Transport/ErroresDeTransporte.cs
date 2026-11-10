using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Transport;

namespace IngenIA365ERP.Application.Transport;

/// <summary>
/// Los errores tipados de Transporte (feature 020, data-model §6). Un duplicado nombra el vehículo vivo que tiene el dato
/// (<c>existingPublicId</c>), como <c>Person.TaxIdDuplicate</c>.
/// </summary>
public static class ErroresDeTransporte
{
    public static Error PlacaDuplicada(string placa, Guid existente, string numeroInterno) =>
        new ErrorConDatos("Vehicle.PlateDuplicate", $"La placa {placa} ya la tiene el vehículo vivo con número interno {numeroInterno}.",
            new { plate = placa, existingPublicId = existente, internalNumber = numeroInterno });

    public static Error NumeroInternoDuplicado(string numero, Guid existente, string placa) =>
        new ErrorConDatos("Vehicle.InternalNumberDuplicate", $"El número interno {numero} ya lo tiene el vehículo vivo de placa {placa}.",
            new { internalNumber = numero, existingPublicId = existente, plate = placa });

    public static Error RadioMovilDuplicado(string codigo, Guid existente, string placa) =>
        new ErrorConDatos("Vehicle.RadioCodeDuplicate", $"El código radio-móvil {codigo} ya lo tiene el vehículo vivo de placa {placa}.",
            new { radioCode = codigo, existingPublicId = existente, plate = placa });

    public static Error PlacaInvalida(string? placa) =>
        new("Vehicle.PlateInvalid", $"«{placa}» no es una placa válida: de 5 a 10 letras y números.");

    public static readonly Error PropietarioObligatorio = new("Vehicle.OwnerRequired", "Todo vehículo tiene un propietario.");

    public static Error PropietarioNoEncontrado(Guid persona) =>
        new("Vehicle.OwnerNotFound", $"No existe la persona {persona:D} para ser propietaria.");

    public static Error VehiculoNoEncontrado(Guid vehiculo) =>
        new("Vehicle.NotFound", $"No existe el vehículo {vehiculo:D}.");

    public static Error VehiculoNoEncontradoPorPlaca(string placa) =>
        new("Vehicle.NotFound", $"No hay un vehículo vivo con la placa {placa}.");

    public static Error Retirado(string placa) =>
        new("Vehicle.Retired", $"El vehículo {placa} está retirado: sólo se consulta. El reingreso es un vehículo nuevo.");

    public static Error FechaAnteriorAlUltimoCambio(DateOnly fecha, DateOnly ultimo) =>
        new ErrorConDatos("Vehicle.EffectiveDateBeforeLastChange",
            $"La fecha efectiva {fecha:dd/MM/yyyy} es anterior al último cambio del mismo tipo ({ultimo:dd/MM/yyyy}).",
            new { effectiveDate = fecha, lastChange = ultimo });

    public static Error MismoEstado(VehicleStatus estado) =>
        new("Vehicle.SameStatus", $"El vehículo ya está {(estado == VehicleStatus.Active ? "activo" : "inactivo")}.");

    public static readonly Error MismoPropietario = new("Vehicle.SameOwner", "La persona ya es la propietaria del vehículo.");

    public static Error CatalogoDeOtroTipo(string campo, TransportCatalogKind esperado) =>
        new ErrorConDatos("Vehicle.CatalogKindMismatch", $"El valor de «{campo}» no es del catálogo {NombreDelCatalogo(esperado)}.",
            new { field = campo, expected = esperado.ToString() });

    public static Error CatalogoNoEncontrado(string campo) =>
        new ErrorConDatos("Transport.CatalogItemNotFound", $"El valor de «{campo}» no existe o está inactivo.", new { field = campo });

    public static Error CodigoDeCatalogoDuplicado(TransportCatalogKind tipo, string codigo, string nombre) =>
        new ErrorConDatos("Catalogo.CodigoDuplicado", $"El código {codigo} ya existe en {NombreDelCatalogo(tipo)}: «{nombre}».",
            new { kind = tipo.ToString(), code = codigo, existingName = nombre });

    public static Error CatalogoNoEncontradoPorId(Guid id) => new("Transport.CatalogItemNotFound", $"No existe el valor de catálogo {id:D}.");

    public static string NombreDelCatalogo(TransportCatalogKind tipo) => tipo switch
    {
        TransportCatalogKind.Brand => "marcas",
        TransportCatalogKind.VehicleClass => "clases de vehículo",
        TransportCatalogKind.BodyType => "carrocerías",
        TransportCatalogKind.Fuel => "combustibles",
        TransportCatalogKind.Modality => "modalidades",
        TransportCatalogKind.ServiceClass => "clases de servicio",
        TransportCatalogKind.TransitAuthority => "organismos de tránsito",
        TransportCatalogKind.RollingGroup => "grupos de rodamiento",
        TransportCatalogKind.EquipmentType => "tipos de equipo",
        _ => tipo.ToString(),
    };
}
