using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, desviación III y X (T101): los métodos de escritura de los puertos que corren fuera de MediatR
/// (<c>IContratoDeObligaciones</c>, <c>IReservasDeCartera</c>) ejecutan su validador y escriben su evento.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0001) y la llena US5 y US6 con los archivos de sus implementaciones; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LosPuertosDeCarteraValidanYAuditanSusEscrituras
{
    /// <summary>Implementaciones de puertos de escritura (relativas a <c>src/</c>).</summary>
    private static readonly string[] Archivos = [];

    private static readonly Regex Valida = new(@"\bValidateAsync\(|\bValidar\w*\(", RegexOptions.Compiled);
    private static readonly Regex Audita = new(@"\bAudit\w*\(|\bAuditar\w*\(", RegexOptions.Compiled);

    [Fact]
    public void Cada_puerto_de_escritura_valida_y_audita()
    {
        var archivos = ReglaSobreFuente.ArchivosNombrados(Archivos).ToList();
        var faltantes = ReglaSobreFuente.Faltantes(archivos, Valida).Select(a => a + ": no valida")
            .Concat(ReglaSobreFuente.Faltantes(archivos, Audita).Select(a => a + ": no audita"))
            .ToList();
        ReglaSobreFuente.SinInfracciones(faltantes, "Un puerto de Cartera escribe sin validar o sin auditar (T101)");
    }
}
