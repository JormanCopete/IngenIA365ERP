using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 008 (FR-006, SC-003; constitución, Principio V): la persona se escribe desde la
/// interfaz en UN solo sitio —el diálogo compartido de <c>Components/Personas</c>— y el
/// formulario existe una vez. Hasta el 2026-09-13 el diálogo estaba copiado a mano en Personas,
/// Empleados y Asociados, con catálogos distintos, y Empleados/Asociados hacían un <c>PUT</c> de la
/// persona con sólo su bandera que apagaba las demás. Esta prueba impide que vuelva a pasar.
///
/// <para>
/// También fija que el buscador de personas es <c>PersonSearchPicker</c> (constitución: nunca
/// buscadores ad-hoc). Los que quedan de antes están en una lista explícita que la fase 2 vacía;
/// agregar uno nuevo a la lista es una decisión, no un descuido.
/// </para>
/// </summary>
public class LaPersonaSeEscribeEnUnSoloSitio
{
    private static readonly string Shared = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");

    /// <summary>Lo único que puede escribir la persona desde la interfaz.</summary>
    private static readonly string[] EscritoresPermitidos =
    [
        Path.Combine("Components", "Personas", "PersonaDialog.razor"),
        Path.Combine("Services", "Core", "PersonasClient.cs"),
    ];

    /// <summary>Buscadores ad-hoc heredados de antes de la 008: los migra la fase 2 a <c>PersonSearchPicker</c>.</summary>
    private static readonly string[] BuscadoresAdHocHeredados =
    [
        Path.Combine("Pages", "CarteraFinanciera", "Recaudos.razor"),
        Path.Combine("Pages", "CarteraFinanciera", "SolicitudCredito.razor"),
    ];

    private static readonly Regex EscribePersona = new(
        @"(PostAsJsonAsync|PutAsJsonAsync|CrearAsync|ActualizarAsync)\s*(<[^>]+>)?\s*\(\s*\$?""/?api/core/people(/|""|\{)",
        RegexOptions.Compiled);

    private static readonly Regex LlamaAlBuscador = new(@"api/core/people/search", RegexOptions.Compiled);

    private static IEnumerable<string> Fuentes() =>
        Directory.EnumerateFiles(Shared, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(Shared, "Services"), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string Relativo(string f) => Path.GetRelativePath(Shared, f);

    [Fact]
    public void Solo_el_dialogo_compartido_escribe_la_persona()
    {
        var escritores = Fuentes()
            .Where(f => EscribePersona.IsMatch(File.ReadAllText(f)))
            .Select(Relativo)
            .Where(r => !EscritoresPermitidos.Contains(r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(escritores.Count == 0,
            "Estos archivos escriben la persona (POST/PUT a /api/core/people) fuera del diálogo compartido " +
            "(feature 008, FR-006; Principio V):\n  " + string.Join("\n  ", escritores));
    }

    [Fact]
    public void Ninguna_pantalla_declara_su_propio_modelo_de_persona()
    {
        var copias = Fuentes()
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bclass\s+(PersonModel|PersonEditDto)\b"))
            .Select(Relativo)
            .ToList();

        Assert.True(copias.Count == 0,
            "El formulario de persona vive en PersonaFormularioModelo; estas pantallas volvieron a copiarlo:\n  " +
            string.Join("\n  ", copias));
    }

    [Fact]
    public void El_buscador_de_personas_es_PersonSearchPicker()
    {
        var picker = Path.Combine("Components", "Shared", "PersonSearchPicker.razor");
        var adHoc = Fuentes()
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(f => LlamaAlBuscador.IsMatch(File.ReadAllText(f)))
            .Select(Relativo)
            .Where(r => !r.Equals(picker, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var nuevos = adHoc.Except(BuscadoresAdHocHeredados, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(nuevos.Count == 0,
            "Buscadores de personas ad-hoc nuevos (la constitución manda usar <PersonSearchPicker>):\n  " +
            string.Join("\n  ", nuevos));

        // La lista de heredados sólo puede achicarse: si uno ya migró, hay que sacarlo de ahí.
        var yaMigrados = BuscadoresAdHocHeredados.Except(adHoc, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(yaMigrados.Count == 0,
            "Estos buscadores ya no son ad-hoc: sacalos de BuscadoresAdHocHeredados para que la lista siga siendo cierta:\n  " +
            string.Join("\n  ", yaMigrados));
    }

    /// <summary>
    /// Feature 012 (T46, T175): la autorización de datos al crear se captura en el mismo sitio que la persona. Sólo el
    /// diálogo compartido pide la política vigente y arma <c>AutorizacionAlCrearDto</c>; cada pantalla que da de alta
    /// (POS, Compras) lo abre con <c>CapturarAutorizacion</c> en vez de copiar el aviso.
    /// </summary>
    [Fact]
    public void Solo_el_dialogo_compartido_captura_la_autorizacion_al_crear()
    {
        string[] permitidos =
        [
            Path.Combine("Components", "Personas", "PersonaDialog.razor"),
            Path.Combine("Services", "Core", "PersonasClient.cs"),
            Path.Combine("Services", "Core", "PersonasDtos.cs"),
        ];
        var capturan = Fuentes()
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"habeas-data/policies/current|\bnew\s+AutorizacionAlCrearDto\b|\bPoliticaDeDatosVigenteAsync\b"))
            .Select(Relativo)
            .Where(r => !permitidos.Contains(r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(capturan.Count == 0,
            "La autorización de datos al crear se captura sólo en PersonaDialog (CapturarAutorizacion; feature 012, T46):\n  " +
            string.Join("\n  ", capturan));
    }

    /// <summary>
    /// Feature 012 (T46, T175): el registro de consentimientos lo escriben el alta con autorización
    /// (<c>AutorizacionDeDatos</c>) y los dos comandos de Habeas Data; nadie más agrega filas a
    /// <c>CMP_HabeasDataConsents</c>.
    /// </summary>
    [Fact]
    public void Solo_la_autorizacion_y_Habeas_Data_escriben_consentimientos()
    {
        var application = Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application");
        string[] permitidos =
        [
            Path.Combine("Compliance", "HabeasData", "AutorizacionDeDatos.cs"),
            Path.Combine("Compliance", "HabeasData", "AcceptConsent", "AcceptConsentCommand.cs"),
            Path.Combine("Compliance", "HabeasData", "RevokeConsent", "RevokeConsentCommand.cs"),
        ];
        var escritores = Directory.EnumerateFiles(application, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"HabeasDataConsents\s*\.\s*(Add|AddRange)\b|new\s+HabeasDataConsent\b"))
            .Select(f => Path.GetRelativePath(application, f))
            .Where(r => !permitidos.Contains(r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(escritores.Count == 0,
            "Estos archivos escriben consentimientos de Habeas Data fuera de AutorizacionDeDatos y los comandos de Habeas Data:\n  " +
            string.Join("\n  ", escritores));
    }

    [Fact]
    public void Los_modulos_ya_no_pisan_las_banderas_con_un_PUT_de_persona()
    {
        // La forma concreta del bug: mandar IsEmployee/IsAssociate en un PUT de la persona desde
        // un módulo. El contrato del servidor ya no las acepta; esto evita que alguien las vuelva a
        // escribir en el cliente creyendo que hacen algo.
        var culpables = Fuentes()
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            // Asignación en C# (inicializador o sentencia), no el filtro «&IsEmployee=true» de una URL.
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\b(IsEmployee|IsAssociate|IsSalesperson)\s*=\s*true\s*[,;}]"))
            .Select(Relativo)
            .ToList();

        Assert.True(culpables.Count == 0,
            "Pantallas que intentan encender una bandera derivada desde el cliente (la escribe el handler de la fila hija):\n  " +
            string.Join("\n  ", culpables));
    }
}
