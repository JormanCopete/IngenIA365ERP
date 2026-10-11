using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §7.6): ningún texto visible de Cartera muestra códigos técnicos ni el nombre del sistema
/// anterior. Molde: <c>TextosDeInventarioTests</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0007) y la llena cada historia con sus archivos de textos y pantallas (<c>Services/Cartera/TextosDeCartera.cs</c>, <c>Pages/Cartera</c>); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class TextosDeCarteraTests
{
    /// <summary>Archivos de textos y pantallas de Cartera (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos =
    [
        (new Regex("SOLIDO", RegexOptions.Compiled | RegexOptions.IgnoreCase), "nombra el sistema anterior"),
        (new Regex("\"Lending\\.[A-Z]\\w*\\.[A-Z]\\w*\"", RegexOptions.Compiled), "muestra un código de error como texto"),
    ];

    /// <summary>
    /// US17 (T0265): los archivos de errores de esta fase. Ahí los códigos son el contrato (<c>Lending.Session.QuorumNotMet</c>), así que la
    /// regla mira los demás textos —los mensajes que lee la persona—: ninguno lleva un código ni el nombre del sistema anterior.
    /// </summary>
    private static readonly string[] ArchivosDeErrores =
    [
        "Core/IngenIA365ERP.Application/Lending/Common/ErroresDeGobierno.cs",
        "Core/IngenIA365ERP.Application/Lending/Collections/ErroresDeMensajesAlDeudor.cs",
        // US1 (T0341): líneas, usura, puntaje y equivalencias; y lo que alerta la réplica.
        "Core/IngenIA365ERP.Application/Lending/Configuration/ErroresDeLineasDeCredito.cs",
        "Core/IngenIA365ERP.Application/Lending/Configuration/EfectoDeReplicacionUsuraEnLineas.cs",
        "Core/IngenIA365ERP.Domain/Lending/Scoring/ModeloDePuntaje.cs",
    ];

    private static readonly Regex Literal = new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    private static readonly Regex SoloCodigo = new(@"^[A-Za-z]+(\.[A-Za-z0-9]+)+$", RegexOptions.Compiled);

    private static readonly Regex CodigoEnTexto = new(@"\b(Lending|Approvals|Generic|Catalogo|Core)\.[A-Z]\w*\.[A-Z]\w*", RegexOptions.Compiled);

    [Fact]
    public void Los_mensajes_de_error_de_Cartera_no_muestran_codigos_ni_el_sistema_anterior()
    {
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosNombrados(ArchivosDeErrores))
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in Literal.Matches(texto))
            {
                var valor = m.Groups[1].Value;
                if (SoloCodigo.IsMatch(valor)) continue;
                if (valor.Contains("SOLIDO", StringComparison.OrdinalIgnoreCase)) infracciones.Add($"{Path.GetFileName(archivo)}: «{valor}» nombra el sistema anterior");
                if (CodigoEnTexto.IsMatch(valor)) infracciones.Add($"{Path.GetFileName(archivo)}: «{valor}» muestra un código");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Mensajes de error de Cartera con códigos o el nombre del sistema anterior (§7.6)");
    }

    [Fact]
    public void Los_textos_de_Cartera_no_muestran_codigos_ni_el_sistema_anterior()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosNombrados(Archivos), Prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Textos de Cartera con códigos técnicos o el nombre del sistema anterior (§7.6)");
    }
}
