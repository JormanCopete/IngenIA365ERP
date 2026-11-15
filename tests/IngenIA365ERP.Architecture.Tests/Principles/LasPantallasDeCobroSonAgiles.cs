using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 022 (T073; FR-011, FR-040): en las tres pantallas de cobro de más uso —Caja de Recaudo, POS y factura de oficina— cada
/// campo explica con F1 cómo se usa (<c>data-ayuda</c> o el parámetro <c>Ayuda</c>), y los pagos se arman sólo en el panel único de
/// medios: ninguna pantalla vuelve a tener su propio formulario de medios ni el panel retirado.
/// </summary>
public class LasPantallasDeCobroSonAgiles
{
    private static readonly string[] Pantallas = ["Pages/Recaudo/Caja.razor", "Pages/Pos/PuntoDeVenta.razor", "Pages/Ventas/NuevaFactura.razor"];
    private static readonly Regex Campo = new(@"<(input|select|textarea|CampoDeValor)\b", RegexOptions.Compiled);

    private static string Shared => Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    [Fact]
    public void Cada_campo_de_las_tres_pantallas_tiene_su_ayuda()
    {
        var faltan = new List<string>();
        foreach (var pantalla in Pantallas)
        {
            var fuente = File.ReadAllText(Path.Combine(Shared, pantalla));
            foreach (Match m in Campo.Matches(fuente))
            {
                var fin = fuente.IndexOf('>', m.Index);
                while (fin > 0 && fuente[fin - 1] == '=') fin = fuente.IndexOf('>', fin + 1);
                var etiqueta = fuente[m.Index..(fin < 0 ? fuente.Length : fin)];
                if (etiqueta.Contains("type=\"file\"", StringComparison.Ordinal)) continue;
                if (!etiqueta.Contains("data-ayuda", StringComparison.Ordinal) && !etiqueta.Contains("Ayuda=", StringComparison.Ordinal))
                    faltan.Add($"{pantalla}:{fuente[..m.Index].Count(c => c == '\n') + 1}");
            }
        }

        Assert.True(faltan.Count == 0, "Campos sin ayuda F1 (data-ayuda o Ayuda=): " + string.Join(", ", faltan));
    }

    [Fact]
    public void Los_pagos_se_arman_solo_en_el_panel_unico()
    {
        foreach (var pantalla in Pantallas.Append("Pages/Ventas/NuevaNotaCredito.razor").Append("Components/Ventas/EditorDelCiclo.razor"))
        {
            var fuente = File.ReadAllText(Path.Combine(Shared, pantalla));
            Assert.Contains("<PanelDeMediosDePago", fuente, StringComparison.Ordinal);
            Assert.DoesNotContain("<PanelDeCobro", fuente, StringComparison.Ordinal);
            Assert.DoesNotContain("AgregarMedio", fuente, StringComparison.Ordinal);
        }
        Assert.False(File.Exists(Path.Combine(Shared, "Components", "Pos", "PanelDeCobro.razor")), "PanelDeCobro se retiró en la feature 022.");
    }
}
