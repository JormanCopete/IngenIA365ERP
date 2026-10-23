using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0003 (R-04; FR-161; data-model §24): la Tesorería heredada (<c>TRS_Checks</c>, <c>TRS_Concepts</c>, <c>TRS_Invoices</c>)
/// convive sin cambios hasta T2 y después se retira por la migración destructiva con guarda <c>RetiroDeLaTesoreriaHeredada</c>. Dos reglas:
/// mientras viva, <b>nada nuevo la usa</b> (la Tesorería nueva y Recaudo no nombran tablas <c>TRS_</c>); y una vez retirada, <b>no vuelve</b>
/// (ninguna configuración mapea las tablas de <see cref="Retiradas"/>). Molde: <see cref="LaCarteraHeredadaNoVuelve"/>.
///
/// <para>
/// Árbol que recorre: <see cref="RecaudoYTesoreria.CarpetasDeRecaudo"/>, <see cref="RecaudoYTesoreria.CarpetasDeTesoreria"/> sin lo heredado,
/// y todas las configuraciones EF. <see cref="Retiradas"/> nace vacía y la llena la fase 46, la del retiro.
/// </para>
/// </summary>
public class LaTesoreriaHeredadaNoVuelve
{
    /// <summary>Las tablas <c>TRS_</c> ya retiradas (se llena en la fase 46).</summary>
    private static readonly string[] Retiradas =
    [
    ];

    [Fact]
    public void Lo_nuevo_no_usa_las_tablas_heredadas()
    {
        var root = RepoPath.FindRepoRoot();
        var trs = new Regex("\"TRS_", RegexOptions.Compiled);
        var infracciones = RecaudoYTesoreria.ArchivosDeRecaudo().Concat(RecaudoYTesoreria.ArchivosDeTesoreria())
            .Where(a => trs.IsMatch(FuenteSinComentarios.Leer(a)))
            .Select(a => ReglaSobreFuente.Relativo(root, a))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Recaudo o la Tesorería nueva usan una tabla TRS_ heredada (R-04)");
    }

    [Fact]
    public void Ninguna_configuracion_mapea_una_tabla_retirada()
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var tabla in Retiradas)
        {
            var mapea = new Regex($@"ToTable\(\s*""{Regex.Escape(tabla)}""", RegexOptions.Compiled);
            infracciones.AddRange(ReglaSobreFuente.ArchivosEn(["Infrastructure/IngenIA365ERP.Persistence/Configurations"])
                .Where(a => mapea.IsMatch(FuenteSinComentarios.Leer(a)))
                .Select(a => $"{ReglaSobreFuente.Relativo(root, a)}: mapea {tabla}"));
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Una tabla de la Tesorería heredada volvió al modelo (FR-161)");
    }
}
