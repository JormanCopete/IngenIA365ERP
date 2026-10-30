using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0002 (R-33; FR-170; data-model §0.4): todo valor de Recaudo y Tesorería tiene su precisión por alias —<c>Monto</c> (18,2),
/// <c>Tarifa</c> (9,6), <c>Facial</c> (18,2)— de <c>PrecisionDeRecaudo</c> y <c>PrecisionDeTesoreria</c>; la cantidad de billetes y monedas
/// es <c>int</c>. Nada en coma flotante (TR38) y ninguna configuración escribe la precisión a mano. Molde:
/// <see cref="LosSaldosYTasasDeCarteraTienenSuPrecision"/>.
///
/// <para>
/// Árbol que recorre: <c>Persistence/Configurations/{Receipts,Treasury}</c> (sin las configuraciones heredadas de la raíz de
/// <c>Configurations/Treasury</c>, R-04) y, por reflexión, las entidades de <c>Domain.Entities.Receipts</c> y de los hijos de
/// <c>Domain.Entities.Treasury</c>. <see cref="Exentos"/> nace vacía; la fase 2 (T0037) suma los dos archivos de alias, donde viven los
/// números.
/// </para>
/// </summary>
public class LasCantidadesYMontosDeCajaTienenSuPrecision
{
    private static readonly string[] Carpetas =
    [
        "Infrastructure/IngenIA365ERP.Persistence/Configurations/Receipts",
        "Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury",
    ];

    /// <summary>Archivos exentos (relativos a la raíz, barras normales): los de los alias.</summary>
    private static readonly string[] Exentos =
    [
        // Fase 2 (T0037).
        "src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Receipts/PrecisionDeRecaudo.cs",
        "src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury/PrecisionDeTesoreria.cs",
    ];

    private static readonly string[] Alias = ["Monto", "Tarifa", "Facial"];

    private static IEnumerable<string> Configuraciones() =>
        ReglaSobreFuente.ArchivosEn(Carpetas);

    [Fact]
    public void Toda_propiedad_decimal_declara_su_alias()
    {
        var configuraciones = string.Join("\n", Configuraciones().Select(File.ReadAllText));
        var entidades = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && (RecaudoYTesoreria.EsDeRecaudo(t.Namespace) || RecaudoYTesoreria.EsDeTesoreria(t.Namespace))
                        && t.Namespace!.StartsWith("IngenIA365ERP.Domain.Entities.", StringComparison.Ordinal))
            .ToList();

        var sinAlias = new List<string>();
        foreach (var entidad in entidades)
        {
            foreach (var propiedad in entidad.GetProperties())
            {
                if (propiedad.PropertyType == typeof(double) || propiedad.PropertyType == typeof(float)
                    || propiedad.PropertyType == typeof(double?) || propiedad.PropertyType == typeof(float?))
                    sinAlias.Add($"{entidad.Name}.{propiedad.Name}: coma flotante");
                if (propiedad.PropertyType != typeof(decimal) && propiedad.PropertyType != typeof(decimal?)) continue;
                var conAlias = new Regex($@"\bProperty\(\s*\w+\s*=>\s*\w+\.{propiedad.Name}\s*\)\s*\.({string.Join("|", Alias)})\(");
                if (!conAlias.IsMatch(configuraciones)) sinAlias.Add($"{entidad.Name}.{propiedad.Name}");
            }
        }
        ReglaSobreFuente.SinInfracciones(sinAlias, "Valores de Recaudo o Tesorería sin alias de precisión (R-33, data-model §0.4)");
    }

    [Fact]
    public void Ninguna_configuracion_escribe_la_precision_a_mano()
    {
        var infracciones = ReglaSobreFuente.Infracciones(Configuraciones(),
            [(new Regex(@"\bHasPrecision\(|\bHasColumnType\(\s*""(decimal|numeric)", RegexOptions.Compiled), "precisión a mano")],
            relativo => Exentos.Contains(relativo, StringComparer.Ordinal));
        ReglaSobreFuente.SinInfracciones(infracciones, "Una configuración de Recaudo o Tesorería fija la precisión sin alias (R-33)");
    }
}
