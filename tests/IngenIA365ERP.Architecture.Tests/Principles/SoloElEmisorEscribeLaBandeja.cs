using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T10; T0037): con varios emisores (Inventario, Cartera, Tesorería, Recaudo), la bandeja de salida sigue
/// teniendo un solo escritor, <c>EmisorDeMensajes</c>; ningún módulo construye ni agrega filas de <c>COR_IntegrationMessages</c> por su
/// cuenta (contracts/mensajes.md §2.4). Recorre todo el código de producción de <c>src/</c>; el único exento es el emisor.
/// </summary>
public class SoloElEmisorEscribeLaBandeja
{
    /// <summary>Todo el código de producción (relativo a <c>src/</c>): cualquier módulo puede llegar a emitir.</summary>
    private static readonly string[] Carpetas = ["Core", "Infrastructure", "Presentation"];

    /// <summary>El emisor mismo (ruta relativa a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = ["src/Core/IngenIA365ERP.Application/Common/Integration/EmisorDeMensajes.cs"];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bIntegrationMessages\.(Add|AddRange|Attach|Update)\(", RegexOptions.Compiled), "agrega a la bandeja"),
        (new Regex(@"\bSet<IntegrationMessage>\(\)\.(Add|AddRange|Attach|Update)\(", RegexOptions.Compiled), "agrega a la bandeja"),
        (new Regex(@"\bnew\s+IntegrationMessage\b\s*[({]", RegexOptions.Compiled), "construye un mensaje de la bandeja"),
    ];

    [Fact]
    public void Nadie_fuera_del_emisor_agrega_mensajes()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}Migrations", StringComparison.Ordinal))
            .ToList();
        Assert.True(archivos.Any(a => a.EndsWith("EmisorDeMensajes.cs", StringComparison.Ordinal)), "El recorrido tiene que ver el emisor.");

        var infracciones = ReglaSobreFuente.Infracciones(archivos, Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Un módulo escribe la bandeja fuera de EmisorDeMensajes (T10)");
    }

    [Fact]
    public void El_emisor_si_escribe_la_bandeja()
    {
        // Sin esto la regla pasaría también si el emisor dejara de existir o cambiara de nombre.
        var emisor = ReglaSobreFuente.ArchivosNombrados(["Core/IngenIA365ERP.Application/Common/Integration/EmisorDeMensajes.cs"]).Single();
        var texto = File.ReadAllText(emisor);
        Assert.True(Prohibidos.Any(p => p.Patron.IsMatch(texto)), "EmisorDeMensajes ya no escribe la bandeja: ¿se movió el único escritor?");
    }
}
