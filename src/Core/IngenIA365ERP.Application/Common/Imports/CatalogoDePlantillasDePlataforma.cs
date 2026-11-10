using IngenIA365ERP.Application.Compliance.RestrictiveLists;
using IngenIA365ERP.Application.Core.Associates.Imports;
using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Saas.NationalValues;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Una plantilla de plataforma (feature 013, T82, T0089; contracts/plantillas.md §0.7, §0.8): su definición, dónde se publica, su comando
/// y sus permisos. No lleva número de orden de carga: no es de la parametrización del comercio. (nuevo)
/// </summary>
/// <param name="PermisoDeDescarga">El de consulta del área; nulo donde lo protege el maestro (<c>RequireMasterAdminAttribute</c>).</param>
/// <param name="PermisoDeImportacion">El de importar; nulo donde lo protege el maestro.</param>
public sealed record PlantillaDePlataforma(
    DefinicionDePlantilla Definicion,
    string RutaBase,
    string Comando,
    string PermisoDeDescarga,
    string? PermisoDeImportacion)
{
    public string Clave => Definicion.Clave;

    public string Nombre => Definicion.Nombre;

    /// <summary>El nombre del libro que se descarga: <c>plantilla-{clave con guiones}</c>.</summary>
    public string NombreDelLibro => $"plantilla-{Clave.Replace('.', '-')}";
}

/// <summary>
/// Las plantillas de plataforma de la 013 (T82, T0089; contracts/plantillas.md §0.7 filas 4 y 10): las listas restrictivas
/// (<c>compliance.restrictive-lists</c>, módulo de auditoría <c>Compliance</c>) y los valores nacionales del registro central
/// (<c>saas.national-values</c>, sólo el maestro con segundo factor: su ruta la protege <c>RequireMasterAdminAttribute</c>, sin código de
/// permiso, y su comando escribe la base administrativa con su propia transacción, C82). La plantilla de la 012 sigue en
/// <c>CatalogoDePlantillas</c>, con su número de orden. (nuevo)
/// </summary>
public static class CatalogoDePlantillasDePlataforma
{
    /// <summary>El «permiso» con que se lista lo que protege el maestro: no es un código del catálogo de permisos.</summary>
    public const string SoloElMaestro = "RequireMasterAdminAttribute";

    public static IReadOnlyList<PlantillaDePlataforma> Todas { get; } =
    [
        new(PlantillaDeListasRestrictivas.Definicion, "/api/compliance/restrictive-lists", nameof(ImportRestrictiveListCommand),
            "Compliance.RestrictiveLists.View", "Compliance.RestrictiveLists.Load"),
        new(PlantillaDeValoresNacionales.Definicion, "/api/saas/national-values", nameof(ImportNationalValuesCommand),
            SoloElMaestro, null),
        // Feature 016 (US1): personas y terceros de la implantación, con las reglas del alta unitaria.
        new(PlantillaDePersonas.Definicion, "/api/core/people", nameof(ImportPeopleCommand), "Core.People.View", "Core.People.Create"),
        // Feature 016 (US3): asociados sobre personas que ya existen, por el camino del alta unitaria.
        new(PlantillaDeAsociados.Definicion, "/api/core/associates", nameof(ImportAssociatesCommand), "Core.Associates.View", "Core.Associates.Create"),
        // Feature 016: la actualización masiva de productos. Descargarla trae los datos actuales, por eso va con el permiso de exportar.
        new(PlantillaDeActualizacionDeProductos.Definicion, "/api/inventory/products/bulk-update", nameof(ImportProductUpdatesCommand),
            "Inventory.Catalog.Export", "Inventory.Catalog.Import"),
        // Feature 020 (R8): las plantillas de Transporte (vehículos, cambios, documentos, conductores y suscripciones).
        .. Transport.Imports.CatalogoDePlantillasDeTransporte.Todas,
    ];

    /// <summary>La plantilla por su clave; lanza si no es de este catálogo.</summary>
    public static PlantillaDePlataforma Por(string clave) =>
        Todas.FirstOrDefault(p => string.Equals(p.Clave, clave, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"No hay una plantilla «{clave}» en CatalogoDePlantillasDePlataforma.", nameof(clave));
}
