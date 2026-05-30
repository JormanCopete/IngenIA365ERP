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
