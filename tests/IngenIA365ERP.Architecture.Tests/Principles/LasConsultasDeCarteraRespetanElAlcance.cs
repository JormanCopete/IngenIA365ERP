using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-004 y SC-015 (DT §2.25, T62): toda consulta de Cartera con datos de deudores filtra por el
/// alcance de oficina y línea del usuario, y falla cerrado.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0006) y la llena cada historia con sus handlers de consulta; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
///
/// <para>
/// US17 (T0259, T0273) la llena y la cierra: cada consulta de <c>Application/Lending</c> (un <c>record …Query</c>) o está en un archivo que
/// aplica el alcance (<see cref="Archivos"/>) o está en <see cref="Exceptuadas"/> con su motivo. Una consulta nueva que no está en ninguna de
/// las dos rompe la prueba: quien la escribe decide si filtra o por qué no.
/// </para>
/// </summary>
public class LasConsultasDeCarteraRespetanElAlcance
{
    /// <summary>Handlers de consulta de Cartera (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // US17 (T0259): reclamos sobre el reporte por la oficina del crédito; la vista previa de la matriz por la línea; el alcance de un
        // usuario (las líneas que asigna quien asigna); la bandeja de alertas de Cartera (C68).
        "Core/IngenIA365ERP.Application/Lending/Collections/ListCreditReportClaimsQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Attributions/AttributionMatrixCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Common/LendingUserScopeCommands.cs",
        "Core/IngenIA365ERP.Application/Common/Alerts/VisibilidadDeAlertas.cs",
        // US17 (T0273): la cola de mensajes al deudor y la vista previa de una plantilla con un crédito de ejemplo.
        "Core/IngenIA365ERP.Application/Lending/Collections/DebtorMessageQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Collections/MessageTemplateQueries.cs",
    ];

    /// <summary>Consultas de <c>Application/Lending</c> que no filtran por oficina ni línea, con el motivo.</summary>
    private static readonly Dictionary<string, string> Exceptuadas = new(StringComparer.Ordinal)
    {
        // Catálogos y configuración sin oficina (T0259).
        ["ListAttributionMatricesQuery"] = "catálogo de la cooperativa: la matriz no tiene oficina",
        ["GetAttributionMatrixQuery"] = "catálogo de la cooperativa: la matriz no tiene oficina",
        ["ListGoverningBodiesQuery"] = "catálogo de la cooperativa: los órganos no tienen oficina",
        ["GetGoverningBodyQuery"] = "catálogo de la cooperativa: los órganos no tienen oficina",
        ["ListLendingTemplatesQuery"] = "registro de plantillas vacías: no trae datos",
        ["GetLendingTemplateQuery"] = "definición de una plantilla vacía: no trae datos",
        ["ListMessageTemplatesQuery"] = "plantillas de mensaje: textos de la cooperativa, sin deudores",
        ["GetMessageTemplateHistoryQuery"] = "plantillas de mensaje: textos de la cooperativa, sin deudores",
        // Actas y vinculados (T0259, decisión del bloque F): el acta es del órgano y los vinculados son personas por su vínculo con la
        // cooperativa; ninguno tiene oficina. Las dos lecturas quedan auditadas (LasLecturasSensiblesDeCarteraSeAuditan) y el alcance de cada
        // sujeto del acta lo aplica su historia cuando lo describe (IAsuntoDeActa).
        ["ListSessionsQuery"] = "el acta es del órgano, no de una oficina",
        ["GetSessionQuery"] = "el acta es del órgano, no de una oficina (lectura auditada)",
        ["ListSessionItemsQuery"] = "puntos de actas cerradas del órgano, no de una oficina",
        ["GetSessionMinutesPdfQuery"] = "el acta es del órgano, no de una oficina (lectura auditada)",
        ["ListRelatedPartiesQuery"] = "los vinculados son personas por su vínculo con la cooperativa, sin oficina (lectura auditada)",
        // Migración y activación (fase 3): de toda la cooperativa, con Lending.Migration.Load o Lending.Activation.Execute.
        ["ListMigrationLoadsQuery"] = "la carga de migración es de toda la cooperativa",
        ["GetMigrationLoadQuery"] = "la carga de migración es de toda la cooperativa",
        ["GetLendingActivationStatusQuery"] = "la activación es de toda la cooperativa",
    };

    private static readonly Regex Requerido = new(@"AlcanceDeCartera", RegexOptions.Compiled);

    private static readonly Regex Consulta = new(@"public\s+sealed\s+record\s+(\w+Query)\b", RegexOptions.Compiled);

    [Fact]
    public void Cada_consulta_filtra_por_el_alcance()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Una consulta de Cartera no filtra por el alcance (SC-015)");
    }

    [Fact]
    public void Cada_consulta_de_Cartera_filtra_o_esta_exceptuada_con_motivo()
    {
        var raiz = RepoPath.FindRepoRoot();
        var conAlcance = Archivos.Select(a => Path.GetFullPath(Path.Combine(raiz, "src", a))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var carpeta = Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending");

        var encontradas = new HashSet<string>(StringComparer.Ordinal);
        var infracciones = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs", SearchOption.AllDirectories))
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in Consulta.Matches(texto))
            {
                var nombre = m.Groups[1].Value;
                encontradas.Add(nombre);
                if (conAlcance.Contains(Path.GetFullPath(archivo)) || Exceptuadas.ContainsKey(nombre)) continue;
                infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {nombre} no aplica el alcance ni está exceptuada con motivo");
            }
        }

        foreach (var (nombre, motivo) in Exceptuadas)
        {
            if (string.IsNullOrWhiteSpace(motivo)) infracciones.Add($"{nombre}: exceptuada sin motivo");
            if (!encontradas.Contains(nombre)) infracciones.Add($"{nombre}: exceptuada pero ya no existe en Application/Lending (quitála de la lista)");
        }

        ReglaSobreFuente.SinInfracciones(infracciones, "Consultas de Cartera sin alcance ni excepción (FR-004, SC-015)");
    }
}
