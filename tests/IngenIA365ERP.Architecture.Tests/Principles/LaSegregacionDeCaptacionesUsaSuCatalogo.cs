using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Lending.Segregation;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014, US16 (T163; FR-005; api.md §1.7): la segregación de captaciones y aportes nace de un solo catálogo, <see cref="SegregacionDeCaptaciones"/>.
/// <list type="bullet">
/// <item>todo tipo de expediente que declara <c>Application/Lending/Deposits</c> (<c>const string Expediente…</c>) es una constante del catálogo;</item>
/// <item>todo <c>new ExpedienteDeCartera(…)</c> de esa carpeta nombra un expediente del catálogo (o el asunto de las diferencias de la migración, que es de
/// la 013) y, si el expediente cuelga de un recurso ajeno (cargos de una cuenta, devolución del retiro), lo hace con su raíz derivada —sin ella, quien
/// abrió una cuenta quedaba impedido para condonar sus cargos—;</item>
/// <item>ninguna carpeta de captaciones escribe <c>LND_CaseParticipations</c> por su cuenta: sólo <c>RegistroDeIntervenciones</c> (la 013).</item>
/// </list>
/// (nuevo)
/// </summary>
public class LaSegregacionDeCaptacionesUsaSuCatalogo
{
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Application/Lending/Deposits"];

    private static readonly Regex ConstanteDeExpediente = new(@"const\s+string\s+(Expediente\w*)\s*=\s*([^;]+);", RegexOptions.Compiled);

    private static readonly Regex NuevoExpediente = new(@"new\s+ExpedienteDeCartera\(\s*([^,]+),\s*([^;]+?)\)\s*[,;)]", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Los nombres con que la carpeta se refiere a un expediente del catálogo.</summary>
    private static readonly string[] Permitidos =
    [
        "SegregacionDeCaptaciones.", "OpenMemberAccountCommandHandler.Expediente", "SolicitudesDeAportes.Expediente", "RetirosDeAsociado.Expediente",
        "RetirosDeAsociado.ExpedienteDeDevolucion", "RegisterAccountChargeCommandHandler.Expediente", "Expediente",
        // La aceptación de diferencias de la carga de migración: el asunto de acta de la 013, que sirve a las dos naturalezas (research E117).
        "AsuntoDeDiferenciasDeActivacion.ClaseDeExpediente",
    ];

    /// <summary>Los que cuelgan de un recurso ajeno: su raíz es derivada.</summary>
    private static readonly string[] Derivados =
    [
        "RetirosDeAsociado.ExpedienteDeDevolucion", "RegisterAccountChargeCommandHandler.Expediente", "SegregacionDeCaptaciones.CargosDeCuenta",
        "SegregacionDeCaptaciones.DevolucionDelRetiro",
    ];

    [Fact]
    public void Los_tipos_de_expediente_de_captaciones_salen_del_catalogo()
    {
        var raiz = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(Carpetas))
        {
            var fuente = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in ConstanteDeExpediente.Matches(fuente))
                if (!m.Groups[2].Value.Trim().StartsWith("SegregacionDeCaptaciones.", StringComparison.Ordinal))
                    infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {m.Groups[1].Value} = {m.Groups[2].Value.Trim()} no es del catálogo");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Un expediente de captaciones fuera de SegregacionDeCaptaciones (FR-005)");
    }

    [Fact]
    public void Cada_expediente_que_nace_es_del_catalogo_y_los_derivados_usan_su_raiz()
    {
        var raiz = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        var vistos = 0;
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(Carpetas))
        {
            var fuente = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in NuevoExpediente.Matches(fuente))
            {
                vistos++;
                var tipo = m.Groups[1].Value.Trim();
                var recurso = m.Groups[2].Value.Trim();
                var relativo = Path.GetRelativePath(raiz, archivo);
                if (!Permitidos.Any(p => tipo == p || tipo.StartsWith(p, StringComparison.Ordinal) && p.EndsWith('.')))
                    infracciones.Add($"{relativo}: ExpedienteDeCartera({tipo}, …) no nombra un expediente del catálogo");
                if (Derivados.Contains(tipo) && !recurso.Contains("Raiz", StringComparison.Ordinal))
                    infracciones.Add($"{relativo}: ExpedienteDeCartera({tipo}, {recurso}) cuelga de un recurso ajeno y no usa su raíz derivada");
            }
        }
        Assert.True(vistos > 0, "la carpeta de captaciones registra intervenciones (apertura, aportes, retiro)");
        ReglaSobreFuente.SinInfracciones(infracciones, "Un expediente de captaciones fuera del catálogo (FR-005)");
    }

    [Fact]
    public void Nadie_en_captaciones_escribe_las_intervenciones_fuera_del_registro()
    {
        var infracciones = ReglaSobreFuente.ArchivosEn(Carpetas)
            .Where(a => Regex.IsMatch(FuenteSinComentarios.Leer(a), @"CaseParticipations\.(Add|AddRange|Update|Remove)|CaseParticipation\.Registrar\("))
            .Select(a => Path.GetRelativePath(RepoPath.FindRepoRoot(), a))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Captaciones escribe LND_CaseParticipations sin RegistroDeIntervenciones (FR-005)");
    }

    [Fact]
    public void El_catalogo_cubre_los_pares_de_api_md()
    {
        Assert.Equal(9, SegregacionDeCaptaciones.Pares.Count);
        Assert.All(SegregacionDeCaptaciones.Pares, p => Assert.Contains(ReglasDeSegregacion.ReglaFija, f => f.Junta(p.Registra, p.Aprueba)));
    }
}
