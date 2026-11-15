using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// 2026-11-12 (revisión comercial, pedido del dueño): los atajos del POS se ejecutan con su tecla o con su botón de la barra, y los dos
/// salen de UNA tabla de comandos (<c>CrearComandos</c> en PuntoDeVenta, sobre <c>TablaDeComandos</c>). Antes había un <c>switch</c> de
/// teclas en la pantalla y una lista de texto aparte en AtajosDelPos, y nada impedía que dijeran cosas distintas. Esta prueba fija que
/// (1) no vuelve un segundo despachador de teclas en la pantalla, (2) AtajosDelPos no tiene su propia lista y (3) cada tecla con acción
/// de la tabla la reserva pos.js —una tecla que pos.js no reserva nunca llega a la pantalla: el navegador hace lo suyo (F-teclas) y el
/// botón sería el único camino—. Es una prueba sobre el fuente porque no hay pruebas de navegador en el repositorio.
/// </summary>
public class ElPosDespachaUnaSolaTablaDeComandos
{
    private static string Shared => Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    private static string Pantalla => File.ReadAllText(Path.Combine(Shared, "Pages", "Pos", "PuntoDeVenta.razor"));

    private static string PosJs => File.ReadAllText(Path.Combine(Shared, "wwwroot", "js", "pos.js"));

    /// <summary>Las teclas con acción de la tabla: <c>new("F2", "buscar", …)</c> dentro de <c>CrearComandos</c> (las pistas van con <c>Pista(</c>).</summary>
    private static List<string> TeclasDeLaTabla()
    {
        var fuente = Pantalla;
        var inicio = fuente.IndexOf("TablaDeComandos CrearComandos()", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "PuntoDeVenta.razor ya no arma su tabla en CrearComandos(): actualizá esta prueba.");
        var fin = fuente.IndexOf("]);", inicio, StringComparison.Ordinal);
        var tabla = fuente[inicio..fin];
        return Regex.Matches(tabla, @"\bnew\(""(?<tecla>[^""]+)"",\s*""").Select(m => m.Groups["tecla"].Value).ToList();
    }

    private static HashSet<string> Conjunto(string nombre)
    {
        var m = Regex.Match(PosJs, nombre + @"\s*=\s*new Set\(\[(?<items>[^\]]*)\]\)");
        Assert.True(m.Success, $"pos.js ya no declara {nombre}: actualizá esta prueba.");
        return Regex.Matches(m.Groups["items"].Value, @"'(?<t>[^']+)'").Select(x => x.Groups["t"].Value).ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void La_pantalla_no_tiene_otro_despachador_de_teclas()
    {
        var fuente = Pantalla;
        // Enter no entra: la lista de suspendidas tiene sus propias teclas (↑ ↓ Enter) y no es un despachador del POS.
        Assert.DoesNotMatch(@"case\s+""(F\d{1,2}|Escape|Delete|Ctrl\+Alt\+[A-Z])""", fuente);
        Assert.Contains("_comandos.EjecutarAsync(tecla)", fuente, StringComparison.Ordinal);
        Assert.Contains("<AtajosDelPos Comandos=\"@_comandos\"", fuente, StringComparison.Ordinal);
    }

    [Fact]
    public void Los_atajos_del_POS_no_tienen_una_lista_propia()
    {
        var atajos = File.ReadAllText(Path.Combine(Shared, "Components", "Pos", "AtajosDelPos.razor"));
        Assert.DoesNotContain("static readonly", atajos, StringComparison.Ordinal);
        Assert.Contains("Comandos=\"@Comandos\"", atajos, StringComparison.Ordinal);
    }

    [Fact]
    public void Cada_tecla_de_la_tabla_la_reserva_pos_js()
    {
        var teclas = TeclasDeLaTabla();
        Assert.True(teclas.Count >= 10, "La tabla de comandos del POS quedó casi vacía: " + string.Join(", ", teclas));
        Assert.Equal(teclas.Count, teclas.Distinct(StringComparer.Ordinal).Count());

        var reservadas = Conjunto("RESERVADAS");
        var conCtrlAlt = Conjunto("CON_CTRL_ALT");
        var sinReservar = teclas.Where(t => t switch
        {
            // Enter no es reservada: pos.js la avisa sólo con la tirilla en pantalla (entregada) y fuera de controles y diálogos.
            "Enter" => !PosJs.Contains("invokeMethodAsync('Tecla', 'Enter')", StringComparison.Ordinal),
            _ when t.StartsWith("Ctrl+Alt+", StringComparison.Ordinal) => !conCtrlAlt.Contains(t["Ctrl+Alt+".Length..].ToLowerInvariant()),
            _ => !reservadas.Contains(t),
        }).ToList();

        Assert.True(sinReservar.Count == 0, "Teclas de la tabla de comandos del POS que pos.js no reserva (no llegarían a la pantalla): " + string.Join(", ", sinReservar));
    }
}
