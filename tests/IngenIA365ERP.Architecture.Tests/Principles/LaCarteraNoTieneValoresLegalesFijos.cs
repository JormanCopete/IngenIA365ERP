using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-014 (DT §2.25, T9): ningún valor legal de crédito vive en el programa; usura, índices, SMMLV,
/// topes y porcentajes son valores nacionales o parámetros con vigencia. Molde: <c>LaNominaNoTieneValoresLegalesFijos</c>.
///
/// <para>Admite los literales de calendario y proporción de Nómina más <c>365</c>; <c>7</c> y <c>10</c> sólo como valor
/// del enum de periodicidad (no son decimales). Recorre el motor nuevo desde ya; la fase Fundacional B suma
/// <c>Application/Lending</c> cuando la cartera heredada se retira. La lista de exenciones por archivo nace vacía.</para>
/// </summary>
public class LaCarteraNoTieneValoresLegalesFijos
{
    /// <summary>Carpetas recorridas (relativas a <c>src/</c>).</summary>
    /// <remarks>Desde la fase Fundacional B (T0143) también <c>Application/Lending</c>: lo heredado ya se retiró.</remarks>
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Domain/Lending", "Core/IngenIA365ERP.Application/Lending"];

    /// <summary>Archivos exentos (rutas relativas a la raíz). Nace vacía.</summary>
    private static readonly string[] Exentos =
    [
        // Fase Fundacional B (T0143): los largos de un segmento SMS (153 y 67 caracteres concatenados, GSM-7 y UCS-2) son del
        // protocolo GSM 03.38, no una norma de crédito.
        "src/Core/IngenIA365ERP.Application/Lending/Collections/RenderizadorDePlantillasDeMensaje.cs",
    ];

    private static readonly Regex LiteralDecimal = new(@"(?<![\w.])\d+(?:\.\d+)?[mM]\b", RegexOptions.Compiled);

    private static readonly HashSet<string> Permitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "0m", "1m", "2m", "0.5m", "12m", "15m", "30m", "100m", "360m", "365m", "0.0m", "1.0m",
    };

    [Fact]
    public void La_libranza_esta_en_la_regla()
    {
        // K2 (T0818): los motores puros de la libranza (Domain/Lending/Payroll) y su Application están entre los archivos que se revisan.
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).Select(a => a.Replace('\\', '/')).ToList();
        Assert.Contains(archivos, a => a.EndsWith("Domain/Lending/Payroll/CapacidadDeDescuento.cs", StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith("Domain/Lending/Payroll/NovedadesDeLibranza.cs", StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith("Application/Lending/Payroll/ConveniosDeLibranza.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void La_vivienda_esta_en_las_dos_reglas()
    {
        // K6b (T1053, T1054): la vivienda (Domain/Lending/Housing) se revisa por literales decimales y por los días de la semana y de la década.
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).Select(a => a.Replace('\\', '/')).ToList();
        var delMotor = ReglaSobreFuente.ArchivosEn(CarpetasDelMotor).Select(a => a.Replace('\\', '/')).ToList();
        foreach (var pieza in new[] { "SerieUvr", "ConversionUvr", "ReconstruccionEnUvr", "ReglasDeVivienda" })
        {
            Assert.Contains(archivos, a => a.EndsWith($"Domain/Lending/Housing/{pieza}.cs", StringComparison.Ordinal));
            Assert.Contains(delMotor, a => a.EndsWith($"Domain/Lending/Housing/{pieza}.cs", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Las_captaciones_estan_en_las_dos_reglas()
    {
        // Feature 014 (T020; R26): Domain/Lending/Deposits y Application/Lending/Deposits se revisan por literales decimales (la carpeta de cada
        // capa es recursiva) y los motores de Domain/Lending/Deposits, además, por los días de la semana y de la década.
        Assert.Contains(Carpetas, c => "Core/IngenIA365ERP.Domain/Lending/Deposits".StartsWith(c + "/", StringComparison.Ordinal));
        Assert.Contains(Carpetas, c => "Core/IngenIA365ERP.Application/Lending/Deposits".StartsWith(c + "/", StringComparison.Ordinal));
        Assert.Contains("Core/IngenIA365ERP.Domain/Lending/Deposits", CarpetasDelMotor);
        Assert.DoesNotContain(Exentos, e => e.Contains("/Lending/Deposits/", StringComparison.Ordinal));
    }

    [Fact]
    public void La_cobranza_esta_en_la_regla()
    {
        // K4 (T0932, T0937): los motores puros de la cobranza (Domain/Lending/Collections) están entre los archivos que se revisan.
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).Select(a => a.Replace('\\', '/')).ToList();
        foreach (var motor in new[] { "EtapasDeCobranza", "RepartoDeGestores", "EstadoDePromesa", "CumplimientoDeAcuerdo", "AvisoPrevioPorEpisodio",
                     "VentanaLegalDeContacto", "GastoDeCobranza", "AccionesDeEtapa" })
            Assert.Contains(archivos, a => a.EndsWith($"Domain/Lending/Collections/{motor}.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// K5 (T0994; DT §2.25): los layouts del SICSES y de las centrales son JSON embebidos en <c>Application/Lending/Regulatory/Layouts/</c> —largos,
    /// posiciones y códigos del instructivo, dato versionado con vigencia— y quedan fuera de la regla por ser <c>.json</c>, no por una exención; el
    /// código que los carga y las proyecciones sí se revisan, y ninguna exención por archivo cae en esa carpeta.
    /// </summary>
    [Fact]
    public void Los_layouts_regulatorios_son_dato_y_su_codigo_esta_en_la_regla()
    {
        var root = RepoPath.FindRepoRoot();
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).Select(a => ReglaSobreFuente.Relativo(root, a).Replace('\\', '/')).ToList();
        Assert.Contains(archivos, a => a.EndsWith("Application/Lending/Regulatory/CatalogoDeLayoutsDeCartera.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(archivos, a => a.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Exentos, e => e.Contains("/Lending/Regulatory/", StringComparison.Ordinal));
        var layouts = Path.Combine(root, "src", "Core", "IngenIA365ERP.Application", "Lending", "Regulatory", "Layouts");
        Assert.True(Directory.GetFiles(layouts, "*.json").Length >= 3, "los layouts del SICSES y de las centrales viven como JSON embebidos");
    }

    [Fact]
    public void El_motor_de_Cartera_no_lleva_valores_legales_fijos()
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(Carpetas))
        {
            var relativo = ReglaSobreFuente.Relativo(root, archivo);
            if (Exentos.Contains(relativo) || relativo.Contains("Seeder", StringComparison.OrdinalIgnoreCase)) continue;
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();
                if (ReglaSobreFuente.EsComentario(linea)) continue;
                foreach (Match m in LiteralDecimal.Matches(linea))
                    if (!Permitidos.Contains(m.Value))
                        infracciones.Add($"{relativo}:{i + 1}: literal decimal '{m.Value}'");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Valores legales fijos en el código de Cartera (FR-014): cada uno es un valor nacional o un parámetro");
    }

    /// <summary>
    /// US1 (T0340; CH39, M16): el motor de días, tasas, usura y plan no escribe <c>7</c> ni <c>10</c> como número suelto —ni entero
    /// ni decimal—: los días de la semana y de la década salen del valor de <c>RepaymentPeriodicity</c> (<c>(int)Weekly</c>,
    /// <c>(int)TenDay</c>). Se miran el código sin comentarios y sin el texto de las cadenas.
    /// </summary>
    private static readonly string[] CarpetasDelMotor = [
        "Core/IngenIA365ERP.Domain/Lending/DayCount",
        "Core/IngenIA365ERP.Domain/Lending/Rates",
        "Core/IngenIA365ERP.Domain/Lending/Schedules",
        // US4 (T0480): la causación, la mora, la suspensión y la reconstrucción tampoco (los umbrales de mora y de suspensión son parámetros
        // con vigencia o el Anexo 1 replicado, nunca un número).
        "Core/IngenIA365ERP.Domain/Lending/Accrual",
        "Core/IngenIA365ERP.Domain/Lending/Delinquency",
        "Core/IngenIA365ERP.Domain/Lending/Replay",
        // US5 (T0533–T0535): la prelación, el aplicador de pagos y el tope del prepago (las veces del SMMLV llegan por parámetro).
        "Core/IngenIA365ERP.Domain/Lending/Payments",
        // US9 (T0674): los motores de calificación y deterioro, la política efectiva, el selector y el historial de mora: rangos, porcentajes,
        // tramos de garantía y meses de historial llegan en el Anexo 1, la política y la regla de pérdida esperada, nunca como número.
        "Core/IngenIA365ERP.Domain/Lending/Risk",
        // K6b (T1053, T1054): la vivienda —la UVR, sus conversiones, la reconstrucción en UVR y las reglas de la Ley 546—. Los límites de vivienda,
        // el tope y la UVR llegan como valores nacionales; ninguno es un número del código.
        "Core/IngenIA365ERP.Domain/Lending/Housing",
        // Feature 014 (T020; R26): los motores de captaciones —interés, retención, GMF, CDAT, contractual, cargos, revalorización, fondo de
        // liquidez, inembargabilidad—. Tarifas, umbrales, topes, plazos y porcentajes llegan como parámetros o valores nacionales.
        "Core/IngenIA365ERP.Domain/Lending/Deposits",
    ];

    private static readonly Regex SieteODiez = new(@"(?<![\w.])(7|10)(?:[mM])?(?![\w.])", RegexOptions.Compiled);

    private static readonly Regex Cadena = new("\\$?@?\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);

    /// <summary>
    /// US9 (T0674): los lectores del esquema de la norma (<c>Domain/Lending/Risk/Normativa</c>) leen la forma de los documentos y la validan
    /// (fracciones entre 0 y 1, rangos contiguos, tramos de garantía); no traen ningún valor de la norma, que llega en el contenido replicado. Se
    /// exceptúan por archivo, con su motivo, sólo si algún día necesitan un número del esquema; hoy no necesitan ninguno.
    /// </summary>
    private static readonly Dictionary<string, string> ExentosDeNormativa = new(StringComparer.Ordinal);

    [Fact]
    public void El_riesgo_esta_en_las_dos_reglas_y_la_normativa_sin_exenciones_que_sobren()
    {
        var root = RepoPath.FindRepoRoot();
        var decimales = ReglaSobreFuente.ArchivosEn(Carpetas).Select(a => ReglaSobreFuente.Relativo(root, a)).ToList();
        var delMotor = ReglaSobreFuente.ArchivosEn(CarpetasDelMotor).Select(a => ReglaSobreFuente.Relativo(root, a)).ToList();
        foreach (var pieza in new[] { "MotorDeCalificacion.cs", "MotorDeDeterioroIndividual.cs", "MotorDeDeterioroGeneral.cs", "PoliticaSarcEfectiva.cs",
                     "SelectorDeModeloDeDeterioro.cs", "HistorialDeMora.cs", "MovimientosDelDeterioro.cs", "DeterioroPorConcepto.cs", "Normativa/Anexo1.cs",
                     "Normativa/PoliticaSarc.cs", "Normativa/ReglaDePerdidaEsperada.cs",
                     // K3 (T0873, T0904): el clasificador de novedades, los requisitos del castigo y la insolvencia al corte.
                     "ClasificadorDeNovedad.cs", "RequisitosDeCastigo.cs", "CalificacionDeInsolvencia.cs",
                     // K6a (T1022): la pérdida esperada del Anexo 2. Ningún coeficiente, corte, PI ni PDI vive en el código: llegan en el documento
                     // SarcAnexo2 (el JSON semilla queda fuera de la regla por ser .json, no por una exención).
                     "FuncionLogistica.cs", "MotorDePerdidaEsperada.cs", "HomologacionDeCalificacion.cs", "Normativa/Anexo2.cs" })
        {
            Assert.Contains(decimales, a => a.EndsWith("Lending/Risk/" + pieza, StringComparison.Ordinal));
            Assert.Contains(delMotor, a => a.EndsWith("Lending/Risk/" + pieza, StringComparison.Ordinal));
        }
        Assert.All(ExentosDeNormativa, e => Assert.False(string.IsNullOrWhiteSpace(e.Value), $"{e.Key}: exento sin motivo"));
        Assert.All(ExentosDeNormativa.Keys, k => Assert.Contains("Domain/Lending/Risk/Normativa/", k, StringComparison.Ordinal));
    }

    [Fact]
    public void El_motor_no_escribe_los_dias_de_la_semana_ni_de_la_decada()
    {
        var root = RepoPath.FindRepoRoot();
        var archivos = ReglaSobreFuente.ArchivosEn(CarpetasDelMotor)
            .Concat(ReglaSobreFuente.ArchivosNombrados(["Core/IngenIA365ERP.Domain/Lending/MotorDeCartera.cs"])).ToList();
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Schedules", "FechasDeVencimiento.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Accrual", "ReglaDeSuspension.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Delinquency", "AlturaDeMora.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Replay", "ReconstruccionDeCredito.cs"), StringComparison.Ordinal));

        var infracciones = new List<string>();
        foreach (var archivo in archivos)
        {
            var relativo = ReglaSobreFuente.Relativo(root, archivo);
            if (ExentosDeNormativa.ContainsKey(relativo)) continue;
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();
                if (ReglaSobreFuente.EsComentario(linea)) continue;
                var codigo = Cadena.Replace(linea, "\"\"");
                var comentario = codigo.IndexOf("//", StringComparison.Ordinal);
                if (comentario >= 0) codigo = codigo[..comentario];
                foreach (Match m in SieteODiez.Matches(codigo))
                    infracciones.Add($"{relativo}:{i + 1}: literal '{m.Value}' (use el valor de RepaymentPeriodicity)");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "El motor de Cartera escribe 7 o 10 como literal (CH39)");
    }

    /// <summary>
    /// US13 (T0768; adaptadores-y-archivos.md §8.5): los documentos al deudor no fijan los días de firmeza del paz y salvo ni ningún otro plazo: la
    /// firmeza sale del parámetro <c>Pagos.FirmezaDiasPorClaseDeMedio</c> por <c>FirmezaDelPago</c> y el calendario hábil. Las carpetas de los
    /// documentos están en el recorrido de los literales decimales, y en ellas ningún <c>AddDays</c> lleva otro número que el día siguiente.
    /// </summary>
    [Fact]
    public void Los_documentos_al_deudor_no_fijan_dias_de_firmeza()
    {
        var root = RepoPath.FindRepoRoot();
        var decimales = ReglaSobreFuente.ArchivosEn(Carpetas).Select(a => ReglaSobreFuente.Relativo(root, a)).ToList();
        Assert.Contains(decimales, a => a.EndsWith("Domain/Lending/Documents/VigenciaDeDocumentos.cs", StringComparison.Ordinal));
        Assert.Contains(decimales, a => a.EndsWith("Domain/Lending/Documents/EstadoDeDocumentoEmitido.cs", StringComparison.Ordinal));
        Assert.Contains(decimales, a => a.EndsWith("Application/Lending/Documents/ConstructorDeDocumentosDeCartera.cs", StringComparison.Ordinal));

        var plazoFijo = new Regex(@"AddDays\(\s*(?!1\s*\))\d+\s*\)", RegexOptions.Compiled);
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(["Core/IngenIA365ERP.Domain/Lending/Documents", "Core/IngenIA365ERP.Application/Lending/Documents"]))
        {
            var relativo = ReglaSobreFuente.Relativo(root, archivo);
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();
                if (ReglaSobreFuente.EsComentario(linea)) continue;
                if (plazoFijo.IsMatch(linea)) infracciones.Add($"{relativo}:{i + 1}: plazo en días fijo en el código");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Los documentos al deudor fijan un plazo en días: sale de un parámetro con vigencia");
    }
}
