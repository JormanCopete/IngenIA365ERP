using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 020 (T066; R8; FR-027): las plantillas de Transporte aplican las mismas reglas que la operación una a una. No arman vehículos,
/// conductores, suscripciones ni personas por su cuenta: pasan por <c>VehicleRegistrar</c>, <c>CambiosDeVehiculo</c>, <c>RolDeConductor</c>,
/// <c>RegistrarDocumento</c> y <c>SuscripcionDeServicio</c>. Y ninguna crea personas (FR-013): un propietario o conductor inexistente es un
/// error de fila. Gemela de las reglas de las plantillas de asociados de la 016.
/// </summary>
public class LasPlantillasDeTransporteUsanElAltaUnitaria
{
    private static readonly string[] Prohibidos =
    [
        "new Vehicle(", "new Vehicle {", "Vehicle.Registrar(",
        "new Driver(", "new Driver {", "new DriverAssignment",
        "new VehicleSubscription", "new SubscriptionPrice", "new VehicleDocument",
        "new Person(", "new Person {", "People.Add(", "PersonFactory",
        "new VehicleEvent",
    ];

    private static string Carpeta => Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "Transport", "Imports");

    public static TheoryData<string> LasPlantillas =>
        [.. (Directory.Exists(Carpeta) ? Directory.EnumerateFiles(Carpeta, "*.cs") : []).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(LasPlantillas))]
    public void La_plantilla_no_arma_entidades_por_su_cuenta(string archivo)
    {
        var fuente = FuenteSinComentarios.Leer(Path.Combine(Carpeta, archivo));
        var encontrados = Prohibidos.Where(p => fuente.Contains(p, StringComparison.Ordinal)).ToList();
        Assert.True(encontrados.Count == 0,
            $"{archivo} arma entidades por su cuenta ({string.Join(", ", encontrados)}): use el alta unitaria (VehicleRegistrar, CambiosDeVehiculo, RolDeConductor, RegistrarDocumento, SuscripcionDeServicio).");
    }
}
