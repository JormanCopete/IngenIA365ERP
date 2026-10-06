using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-004 y SC-015 (DT §2.25, T62): toda consulta de Cartera con datos de deudores filtra por el
/// alcance de oficina y línea del usuario, y falla cerrado.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0006) y la llena cada historia con sus handlers de consulta; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LasConsultasDeCarteraRespetanElAlcance
{
    /// <summary>Handlers de consulta de Cartera (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos = [];

    private static readonly Regex Requerido = new(@"AlcanceDeCartera", RegexOptions.Compiled);

    [Fact]
    public void Cada_consulta_filtra_por_el_alcance()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Una consulta de Cartera no filtra por el alcance (SC-015)");
    }
}
