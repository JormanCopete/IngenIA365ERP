using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T60, C84): un código <c>Lending.*.View</c> no da acceso a datos personales; la bandeja,
/// los hechos y los archivos regulatorios van con <c>Read</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0006) y la llena cada historia con sus rutas que devuelven datos de deudores y el permiso que exigen; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LosCodigosViewDeCarteraNoTienenDatosPersonales
{
    /// <summary>Rutas que devuelven datos personales y el permiso que exigen.</summary>
    private static readonly (string Ruta, string Permiso)[] RutasConDatosPersonales = [];

    [Fact]
    public void Ninguna_ruta_con_datos_personales_se_abre_con_View()
    {
        var infracciones = RutasConDatosPersonales
            .Where(r => r.Permiso.EndsWith(".View", StringComparison.Ordinal))
            .Select(r => $"{r.Ruta}: {r.Permiso}")
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Una ruta con datos personales se abre con un código View (T60)");
    }
}
