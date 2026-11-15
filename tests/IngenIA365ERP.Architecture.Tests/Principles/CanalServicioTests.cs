using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 021 (T021; constitución v2.1.0, «Sistemas satélite», regla 2; research R2 y R3): la API de servicio del canal digital
/// es una puerta aparte. Su esquema no sale de <c>/api/canal/v1</c>, ninguna ruta de servicio acepta el JWT de usuarios, y el
/// actor «asociado por canal» lo construye sólo el resolvedor del actor de la API —ningún handler se fabrica un asociado—.
/// </summary>
public class CanalServicioTests
{
    private static readonly Regex UsaElEsquemaDelCanal = new(@"PeticionDelCanal\.(Politica|Esquema)\b", RegexOptions.Compiled);

    private static readonly Regex FabricaAsociado = new(@"\bActor\.AsociadoPorCanal\(", RegexOptions.Compiled);

    /// <summary>Los archivos que pueden nombrar el esquema o la política del canal.</summary>
    private static readonly string[] PuedenUsarElEsquema =
    [
        "Presentation/IngenIA365ERP.API/Security/CanalServicio/",
        "Presentation/IngenIA365ERP.API/Endpoints/CanalServicio/",
    ];

    /// <summary>Los únicos archivos que construyen el actor asociado.</summary>
    private static readonly string[] PuedenFabricarAsociado =
    [
        "Presentation/IngenIA365ERP.API/Services/ActorDeLaPeticion.cs",
    ];

    [Fact]
    public void El_esquema_del_canal_solo_lo_usan_su_autenticacion_y_su_modulo()
    {
        var infracciones = ReglaSobreFuente.ArchivosEn(["Presentation", "Core", "Infrastructure"])
            .Where(a => !Permitido(a, PuedenUsarElEsquema))
            .Where(a => UsaElEsquemaDelCanal.IsMatch(FuenteSinComentarios.Leer(a)))
            .Select(a => ReglaSobreFuente.Relativo(RepoPath.FindRepoRoot(), a))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones,
            "El esquema de servicio del canal se usa fuera de /api/canal/v1 (sólo Security/CanalServicio y Endpoints/CanalServicio)");
    }

    [Fact]
    public void Las_rutas_de_servicio_exigen_la_politica_del_canal()
    {
        var modulos = ReglaSobreFuente.ArchivosEn(["Presentation/IngenIA365ERP.API/Endpoints/CanalServicio"]).ToList();
        Assert.NotEmpty(modulos);
        var sinPolitica = modulos
            .Where(a => FuenteSinComentarios.Leer(a).Contains("MapGroup(", StringComparison.Ordinal))
            .Where(a => !FuenteSinComentarios.Leer(a).Contains("RequireAuthorization(PeticionDelCanal.Politica)", StringComparison.Ordinal))
            .Select(a => ReglaSobreFuente.Relativo(RepoPath.FindRepoRoot(), a))
            .ToList();
        ReglaSobreFuente.SinInfracciones(sinPolitica, "Un grupo de la API de servicio no exige la política del canal");

        var fuera = ReglaSobreFuente.ArchivosEn(["Presentation/IngenIA365ERP.API/Endpoints"])
            .Where(a => !Permitido(a, PuedenUsarElEsquema))
            .Where(a => Regex.IsMatch(FuenteSinComentarios.Leer(a), @"[""']/api/canal/v1"))
            .Select(a => ReglaSobreFuente.Relativo(RepoPath.FindRepoRoot(), a))
            .ToList();
        ReglaSobreFuente.SinInfracciones(fuera, "Hay rutas /api/canal/v1 fuera de Endpoints/CanalServicio");
    }

    [Fact]
    public void Solo_el_resolvedor_del_actor_fabrica_al_asociado_por_canal()
    {
        var infracciones = ReglaSobreFuente.ArchivosEn(["Presentation", "Core", "Infrastructure"])
            .Where(a => !Permitido(a, PuedenFabricarAsociado))
            .Where(a => !a.EndsWith(Path.Combine("Common", "Execution", "Actor.cs"), StringComparison.OrdinalIgnoreCase))
            .Where(a => FabricaAsociado.IsMatch(FuenteSinComentarios.Leer(a)))
            .Select(a => ReglaSobreFuente.Relativo(RepoPath.FindRepoRoot(), a))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones,
            "Se construye Actor.AsociadoPorCanal fuera de ActorDeLaPeticion (research R3: ningún handler se fabrica un asociado)");
    }

    private static bool Permitido(string archivo, IEnumerable<string> permitidos)
    {
        var normalizado = archivo.Replace('\\', '/');
        return permitidos.Any(p => normalizado.Contains("/src/" + p, StringComparison.OrdinalIgnoreCase));
    }
}
