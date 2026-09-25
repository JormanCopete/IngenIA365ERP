using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T19, §2.18), FR-017: cantidades, costos, precios,
/// montos y tarifas se guardan en decimal exacto con la precisión de
/// <c>PrecisionDeInventario</c> —cantidad (18,4), factor (18,6), costo y precio unitario (18,6),
/// monto (18,2), tarifa como fracción (9,6)—. La convención global (18,2) trunca un costo promedio
/// o una tarifa de ICA por mil sin avisar.
///
/// <para>
/// Esqueleto del Setup: <see cref="Columnas"/> empieza vacía y la prueba afirma la regla sobre cada
/// elemento; con la lista vacía pasa porque no hay nada que violar, no por un <c>return</c>
/// temprano. La llena el bloque que configura cada entidad (base de inventario, fase 3, y las
/// historias siguientes): el archivo de configuración EF (relativo a la raíz), la propiedad y el
/// ayudante de <c>PrecisionDeInventario</c> que debe usar.
/// </para>
/// </summary>
public class LasCantidadesYCostosTienenSuPrecision
{
    /// <summary>(configuración EF relativa a la raíz, propiedad, ayudante de precisión). Los agrega cada bloque.</summary>
    private static readonly (string Configuracion, string Propiedad, string Precision)[] Columnas = [];

    [Fact]
    public void Cada_columna_de_cantidad_o_costo_usa_su_precision()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var (configuracion, propiedad, precision) in Columnas)
        {
            var archivo = Path.Combine(root, configuracion);
            Assert.True(File.Exists(archivo), $"No existe {configuracion}: si la configuración se movió, actualizá Columnas.");
            var texto = File.ReadAllText(archivo);

            // La propiedad y, en la misma cadena fluida (antes del ';'), su ayudante de precisión.
            var mapeo = new Regex($@"\.{Regex.Escape(propiedad)}\b[^;]*\b{Regex.Escape(precision)}\b", RegexOptions.Compiled);
            if (!mapeo.IsMatch(texto))
                infractores.Add($"{configuracion}: {propiedad} no usa PrecisionDeInventario.{precision}");
        }

        Assert.True(infractores.Count == 0,
            "Columnas de inventario sin su precisión (FR-017, T19):\n  " + string.Join("\n  ", infractores));
    }
}
