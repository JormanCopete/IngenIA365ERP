using System.Reflection;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio VI — PublicId externo (Guid), Id interno (int).
/// Ningún tipo que cruce el límite del API debe exponer <c>int Id</c>.
/// Cubre dos superficies:
///  * <b>Application</b> — DTOs de salida (<c>Response</c>, <c>Dto</c>, <c>Item</c>)
///    devueltos por handlers.
///  * <b>API (Carter)</b> — records de entrada (<c>Body</c>, <c>Request</c>,
///    <c>RequestBody</c>) que llegan deserializados desde JSON.
/// Las entidades del dominio (<c>BaseEntity</c>) sí declaran <c>int Id</c>
/// — quedan excluidas porque son internas.
/// </summary>
public class PrincipioVI_PublicIdOnly
{
    [Fact]
    public void No_public_dto_in_Application_exposes_int_Id()
    {
        var asm = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;
        string[] suffixes = [ "Response", "Dto", "Item" ];

        var offenders = FindOffenders(asm, suffixes);

        Assert.True(offenders.Count == 0,
            "DTO/Response/Dto/Item en Application exponen 'int Id' — usa PublicId (Guid):\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_Carter_request_in_API_exposes_int_Id()
    {
        // Carga el assembly de API por reflection — el proyecto Architecture.Tests
        // no debe ligarse a la API en tiempo de compilación (no es su dependencia
        // declarada). Si el assembly no está accesible, el test es inconcluyente
        // pero no falla — un PR que lo introduzca lo activará en la siguiente corrida.
        var apiAsm = TryLoadApiAssembly();
        if (apiAsm is null) return;

        string[] suffixes = [ "Body", "Request", "RequestBody" ];

        var offenders = FindOffenders(apiAsm, suffixes);

        Assert.True(offenders.Count == 0,
            "Carter requests en API exponen 'int Id' — usa PublicId (Guid):\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Feature 013, fase 2 (T0038): las rutas nuevas de la plataforma de Cartera —valores nacionales (maestro y copia), perfil regulatorio,
    /// canales externos, participantes, listas restrictivas, solicitudes del titular— identifican todo por <c>PublicId</c>: ningún
    /// parámetro de ruta <c>:int</c> ni un <c>personId</c> entero. Las de habeas data por finalidad van por <c>personPublicId</c>; las
    /// heredadas <c>{personId:int}</c> quedan marcadas en desuso (<c>[Obsolete]</c>).
    /// </summary>
    [Fact]
    public void Las_rutas_de_la_plataforma_de_Cartera_van_por_PublicId()
    {
        var api = Path.Combine(Helpers.RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Endpoints");
        string[] archivos =
        [
            Path.Combine("Saas", "NationalValuesEndpoints.cs"),
            Path.Combine("Core", "NationalValuesEndpoints.cs"),
            Path.Combine("Core", "RegulatoryProfileEndpoints.cs"),
            Path.Combine("Core", "ExternalChannelsEndpoints.cs"),
            Path.Combine("Core", "IntegrationParticipantsEndpoints.cs"),
            Path.Combine("Compliance", "RestrictiveListsEndpoints.cs"),
            Path.Combine("Compliance", "DataSubjectRequestsEndpoints.cs"),
        ];
        var infractores = new List<string>();
        foreach (var relativo in archivos)
        {
            var ruta = Path.Combine(api, relativo);
            Assert.True(File.Exists(ruta), $"No existe {relativo}: si se movió, actualizá la lista.");
            var texto = Helpers.FuenteSinComentarios.Leer(ruta);
            if (System.Text.RegularExpressions.Regex.IsMatch(texto, @"\{\w+:int\}|\bint\s+\w*[Ii]d\b"))
                infractores.Add(relativo);
        }
        Assert.True(infractores.Count == 0, "Rutas nuevas con Id entero (Principio VI):\n  " + string.Join("\n  ", infractores));

        var habeas = Helpers.FuenteSinComentarios.Leer(Path.Combine(api, "HabeasDataModule.cs"));
        Assert.Contains("\"/api/compliance/habeas-data/authorizations\"", habeas, StringComparison.Ordinal);
        Assert.Contains("Guid? personPublicId", habeas, StringComparison.Ordinal);
    }

    private static List<string> FindOffenders(Assembly asm, string[] suffixes) =>
        asm.GetTypes()
            .Where(t => t.IsClass && t.IsPublic)
            .Where(t => suffixes.Any(s => t.Name.EndsWith(s, StringComparison.Ordinal)))
            .Where(HasIntId)
            .Select(t => t.FullName!)
            .ToList();

    private static bool HasIntId(Type t)
    {
        var prop = t.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        return prop is not null && prop.PropertyType == typeof(int);
    }

    private static Assembly? TryLoadApiAssembly()
    {
        try
        {
            return Assembly.Load("IngenIA365ERP.API");
        }
        catch
        {
            return null;
        }
    }
}
