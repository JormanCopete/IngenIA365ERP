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
    ];

    private static readonly Regex SieteODiez = new(@"(?<![\w.])(7|10)(?:[mM])?(?![\w.])", RegexOptions.Compiled);

    private static readonly Regex Cadena = new("\\$?@?\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);

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
}
