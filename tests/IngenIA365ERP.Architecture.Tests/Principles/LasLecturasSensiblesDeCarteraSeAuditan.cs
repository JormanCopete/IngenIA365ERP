using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-003 y SC-014 (DT §2.25, T67, C89): las lecturas sensibles de la lista cerrada se auditan.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0006) y la llena cada historia con los handlers de su lista (research.md §13); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LasLecturasSensiblesDeCarteraSeAuditan
{
    /// <summary>Handlers de lecturas sensibles (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos = [];

    private static readonly Regex Requerido = new(@"\bAudit\w*\(|\bAuditar\w*\(|LendingAuditEmitter", RegexOptions.Compiled);

    [Fact]
    public void Cada_lectura_sensible_se_audita()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Una lectura sensible de Cartera no se audita (C89)");
    }
}
