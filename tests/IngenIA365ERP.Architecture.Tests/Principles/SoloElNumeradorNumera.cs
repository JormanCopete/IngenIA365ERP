using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T16, §2.18), FR-038: los consecutivos son por tipo de
/// documento y prefijo y los asigna <b>un solo mecanismo</b>, el numerador, que incrementa la fila
/// de numeración (<c>INV_DocumentSequences.NextValue</c> o la resolución DIAN
/// <c>LastIssuedNumber</c>) dentro de la transacción de confirmación. Otro código que la toque
/// produce saltos o números repetidos.
///
/// <para>
/// Esqueleto del Setup: <see cref="PropiedadesDeNumeracion"/> empieza vacía y la prueba afirma la
/// regla sobre cada elemento; con la lista vacía pasa porque no hay nada que violar, no por un
/// <c>return</c> temprano. La llena la base de inventario (fase 3) con <c>NextValue</c> y la entrega
/// I4 (US8) con <c>LastIssuedNumber</c>; <see cref="NumeradoresAutorizados"/> nombra los archivos
/// que sí pueden escribirlas.
/// </para>
/// </summary>
public class SoloElNumeradorNumera
{
    /// <summary>Propiedades de las filas de numeración. Las agrega el bloque que crea cada una.</summary>
    private static readonly string[] PropiedadesDeNumeracion = [];

    /// <summary>Nombres de archivo (sin ruta) que pueden escribirlas.</summary>
    private static readonly string[] NumeradoresAutorizados = ["Numerador.cs"];

    [Fact]
    public void Solo_el_numerador_escribe_los_consecutivos()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var propiedad in PropiedadesDeNumeracion)
        {
            // Asignación, suma compuesta o incremento sobre la propiedad (no su declaración ni su lectura).
            var escritura = new Regex($@"\.{Regex.Escape(propiedad)}\s*(=(?!=)|\+=|-=|\+\+|--)|(\+\+|--)\s*\w+\.{Regex.Escape(propiedad)}\b", RegexOptions.Compiled);
            foreach (var (archivo, texto) in fuentes)
            {
                if (NumeradoresAutorizados.Contains(Path.GetFileName(archivo), StringComparer.Ordinal)) continue;
                if (escritura.IsMatch(texto))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: escribe {propiedad} fuera del numerador");
            }
        }

        Assert.True(infractores.Count == 0,
            "Consecutivos escritos fuera del numerador (FR-038, T16):\n  " + string.Join("\n  ", infractores));
    }
}
