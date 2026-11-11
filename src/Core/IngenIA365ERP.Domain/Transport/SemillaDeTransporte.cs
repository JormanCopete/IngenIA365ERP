using IngenIA365ERP.Domain.Enums.Transport;

namespace IngenIA365ERP.Domain.Transport;

/// <summary>
/// La semilla de los catálogos de Transporte (feature 020, R6; FR-020, FR-024): los valores comunes del transporte en Colombia
/// (Decreto 1079 de 2015 y clases del RUNT), para que la cooperativa no empiece en blanco. Cada cooperativa los renombra, inactiva o
/// complementa; ninguno se borra. Quedan <b>sin semilla</b> la marca, el grupo de rodamiento y el organismo de tránsito, que son de cada
/// cooperativa. Pendiente de revisar por el área de transporte antes de la salida (spec, Supuestos).
/// Cambiar esta lista sube <see cref="Version"/>: la siembra agrega lo que falte y no toca lo que la cooperativa cambió.
/// </summary>
public static class SemillaDeTransporte
{
    public const int Version = 1;

    public static IReadOnlyList<ValorSembrado> Catalogos { get; } =
    [
        // Clases de vehículo (RUNT).
        new(TransportCatalogKind.VehicleClass, "AUTOMOVIL", "Automóvil"),
        new(TransportCatalogKind.VehicleClass, "CAMIONETA", "Camioneta"),
        new(TransportCatalogKind.VehicleClass, "CAMPERO", "Campero"),
        new(TransportCatalogKind.VehicleClass, "MICROBUS", "Microbús"),
        new(TransportCatalogKind.VehicleClass, "BUSETA", "Buseta"),
        new(TransportCatalogKind.VehicleClass, "BUS", "Bus"),
        new(TransportCatalogKind.VehicleClass, "CAMION", "Camión"),
        new(TransportCatalogKind.VehicleClass, "TRACTOCAM", "Tractocamión"),
        new(TransportCatalogKind.VehicleClass, "VOLQUETA", "Volqueta"),
        new(TransportCatalogKind.VehicleClass, "MOTOCICLET", "Motocicleta"),

        // Carrocerías.
        new(TransportCatalogKind.BodyType, "SEDAN", "Sedán"),
        new(TransportCatalogKind.BodyType, "HATCHBACK", "Hatchback"),
        new(TransportCatalogKind.BodyType, "STATION", "Station wagon"),
        new(TransportCatalogKind.BodyType, "CERRADA", "Cerrada"),
        new(TransportCatalogKind.BodyType, "VAN", "Van"),
        new(TransportCatalogKind.BodyType, "DOBLECAB", "Doble cabina"),

        // Combustibles.
        new(TransportCatalogKind.Fuel, "GASOLINA", "Gasolina"),
        new(TransportCatalogKind.Fuel, "DIESEL", "Diésel"),
        new(TransportCatalogKind.Fuel, "GNV", "Gas natural vehicular"),
        new(TransportCatalogKind.Fuel, "GASGNV", "Gasolina y gas natural"),
        new(TransportCatalogKind.Fuel, "ELECTRICO", "Eléctrico"),
        new(TransportCatalogKind.Fuel, "HIBRIDO", "Híbrido"),

        // Modalidades de servicio público (Decreto 1079 de 2015).
        new(TransportCatalogKind.Modality, "INDIVIDUAL", "Individual de pasajeros (taxi)"),
        new(TransportCatalogKind.Modality, "COLECTIVO", "Colectivo metropolitano, distrital y municipal"),
        new(TransportCatalogKind.Modality, "ESPECIAL", "Especial"),
        new(TransportCatalogKind.Modality, "MIXTO", "Mixto"),
        new(TransportCatalogKind.Modality, "CARRETERA", "Pasajeros por carretera"),
        new(TransportCatalogKind.Modality, "CARGA", "Carga"),

        // Clases de servicio.
        new(TransportCatalogKind.ServiceClass, "PUBLICO", "Público"),
        new(TransportCatalogKind.ServiceClass, "PARTICULAR", "Particular"),
        new(TransportCatalogKind.ServiceClass, "OFICIAL", "Oficial"),
        new(TransportCatalogKind.ServiceClass, "DIPLOMATIC", "Diplomático"),

        // Tipos de equipo (FR-008).
        new(TransportCatalogKind.EquipmentType, "TAXIMETRO", "Taxímetro"),
        new(TransportCatalogKind.EquipmentType, "REGISTRA", "Registradora electrónica"),
        new(TransportCatalogKind.EquipmentType, "RADIO", "Radio"),
        new(TransportCatalogKind.EquipmentType, "GPS", "GPS"),
        new(TransportCatalogKind.EquipmentType, "APP", "Usuario de la app"),
    ];

    /// <summary>Los tipos de documento del FR-020, con obligatorio y días de aviso por defecto.</summary>
    public static IReadOnlyList<TipoDeDocumentoSembrado> TiposDeDocumento { get; } =
    [
        new("SOAT", "SOAT", true, 30),
        new("RTM", "Revisión técnico-mecánica y de emisiones", true, 30),
        new("TOP", "Tarjeta de operación", true, 30),
        new("RCC", "Póliza de responsabilidad civil contractual", true, 30),
        new("RCE", "Póliza de responsabilidad civil extracontractual", true, 30),
        new("TODORIESGO", "Póliza todo riesgo", false, 30),
        new("REVPREV", "Revisión preventiva", false, 15),
    ];
}

/// <param name="Kind">El catálogo.</param>
/// <param name="Codigo">Nomenclatura en mayúsculas, sin espacios, hasta 10 caracteres.</param>
public sealed record ValorSembrado(TransportCatalogKind Kind, string Codigo, string Nombre);

public sealed record TipoDeDocumentoSembrado(string Codigo, string Nombre, bool Obligatorio, int DiasDeAviso);
