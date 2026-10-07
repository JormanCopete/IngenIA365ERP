using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-010 (DT §2.25, T35): las configuraciones EF de Cartera fijan la precisión por los alias de
/// <c>PrecisionDeCartera</c>, nunca con números sueltos.
///
/// <para>Llena en la fase Fundacional B (T0143): recorre <c>Persistence/Configurations/Lending</c> (exime <c>PrecisionDeCartera</c>, donde
/// viven los números) y exige, por reflexión, que toda propiedad <c>decimal</c> de <c>Entities/Lending</c> declare un alias en las
/// configuraciones de Cartera.</para>
/// </summary>
public class LosSaldosYTasasDeCarteraTienenSuPrecision
{
    /// <summary>Carpetas de configuraciones EF de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = ["Infrastructure/IngenIA365ERP.Persistence/Configurations/Lending"];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = ["src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Lending/PrecisionDeCartera.cs"];

    private static readonly string[] Alias = ["Monto", "TasaAnual", "TasaEquivalente", "Fraccion", "Puntaje", "ValorUvr"];

    [Fact]
    public void Toda_propiedad_decimal_de_Cartera_declara_su_alias()
    {
        var configuraciones = string.Join("\n", ReglaSobreFuente.ArchivosEn(Carpetas).Select(File.ReadAllText));
        var entidades = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Namespace?.StartsWith("IngenIA365ERP.Domain.Entities.Lending", StringComparison.Ordinal) == true)
            .ToList();
        Assert.NotEmpty(entidades);

        var sinAlias = new List<string>();
        foreach (var entidad in entidades)
        {
            foreach (var propiedad in entidad.GetProperties().Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)))
            {
                var conAlias = new Regex($@"\bProperty\(\s*\w+\s*=>\s*\w+\.{propiedad.Name}\s*\)\s*\.({string.Join("|", Alias)})\(");
                if (!conAlias.IsMatch(configuraciones)) sinAlias.Add($"{entidad.Name}.{propiedad.Name}");
            }
        }
        Assert.True(sinAlias.Count == 0, "Decimales de Cartera sin alias de PrecisionDeCartera (T35):\n  " + string.Join("\n  ", sinAlias));
    }

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\.HasPrecision\(\s*\d", RegexOptions.Compiled), "precisión con números sueltos"),
        (new Regex(@"HasColumnType\(\s*\""(numeric|decimal)\(", RegexOptions.Compiled), "tipo decimal con números sueltos"),
    ];

    [Fact]
    public void Las_configuraciones_usan_los_alias_de_precision()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Una configuración de Cartera fija la precisión sin PrecisionDeCartera (FR-010)");
    }
}
