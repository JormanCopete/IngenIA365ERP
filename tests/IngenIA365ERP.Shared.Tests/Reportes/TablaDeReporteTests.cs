using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.Shared.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Reportes;

/// <summary>
/// Feature 009 E2: el pie de <c>TablaDeReporte</c> pinta la fila de totales que arma el servidor, cuya
/// primera celda ya dice «Totales». Cuando la tabla tiene secciones, la celda de la columna «Sección»
/// del pie va vacía, como en <c>ReportesNomina.razor</c> y como en los exportadores; hasta el
/// 2026-09-20 el componente ponía ahí su propio «Totales» y el balance de prueba, el libro diario, la
/// relación de comprobantes y el estado de cuenta mostraban «Totales | Totales». No hay bUnit en el
/// repositorio: se lee el marcado.
/// </summary>
public class TablaDeReporteTests
{
    private static readonly Regex PieConSeccion = new(@"<tfoot>(?<pie>[\s\S]*?)</tfoot>", RegexOptions.Compiled);

    [Theory]
    [InlineData("Components/Reportes/TablaDeReporte.razor")]
    [InlineData("Pages/Reportes/ReportesNomina.razor")]
    public void La_celda_de_seccion_del_pie_va_vacia(string archivo)
    {
        var marcado = File.ReadAllText(Path.Combine(Repositorio.Shared(), archivo));
        var pie = PieConSeccion.Match(marcado);
        pie.Success.Should().BeTrue("la tabla pinta sus totales en un <tfoot>");

        pie.Groups["pie"].Value.Should().MatchRegex(@"@if \(conSeccion\)\s*\{\s*<td></td>\s*\}",
            "la primera celda de la fila de totales que arma el servidor ya dice «Totales»; la de sección va vacía para que pantalla y archivo coincidan");
    }
}
