using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 y T018 (decisiones-transversales T10, T47, T6, §2.18), FR-083: todo trabajo de fondo
/// que opera sobre datos de una cooperativa lo hace por <c>IEjecutorEnCooperativa</c> —que resuelve
/// la cooperativa, su base y el actor «Proceso de integración»— y escribe sólo por comandos: nunca
/// llama a <c>SaveChangesAsync</c> por su cuenta. Sin eso la auditoría del proceso cae en la base
/// global o sin actor.
///
/// <para>
/// Tres reglas (T018): (1) todo tipo que herede <c>BackgroundService</c> en <c>src/</c> está declarado,
/// o en <see cref="TrabajosDeFondo"/> o en <see cref="ExcepcionesSinCooperativa"/> con su motivo —uno
/// nuevo obliga a decidir—; (2) el que depende de <c>ISender</c>, <c>ITenantDirectory</c> o
/// <c>IApplicationDbContext</c> recibe <c>IEjecutorEnCooperativa</c> y no guarda por su cuenta salvo que
/// esté en <see cref="GuardanPorSuCuenta"/> con su motivo; (3) <c>AddHostedService</c> sólo aparece en
/// <c>API/Program.cs</c> (el DbMigrator no arranca trabajos), salvo el registro de las excepciones
/// existentes. La fase 16 (US7, T448) endurece la regla de guardado para los despachadores de I2.
/// </para>
/// </summary>
public class NingunTrabajoDeFondoOperaSinCooperativa
{
    /// <summary>Trabajos de fondo que operan por cooperativa, como ruta relativa a la raíz.</summary>
    private static readonly string[] TrabajosDeFondo =
    [
        Path.Combine("src", "Presentation", "IngenIA365ERP.API", "Integration", "ProgramadorDeTareas.cs"),
        Path.Combine("src", "Infrastructure", "IngenIA365ERP.Audit", "Services", "AuditOutboxForwarder.cs"),
        Path.Combine("src", "Infrastructure", "IngenIA365ERP.Storage", "Services", "NotificationEmailDispatcher.cs"),
    ];

    /// <summary>
    /// Servicios alojados existentes que no operan por cooperativa, por nombre de tipo y con su motivo.
    /// Pueden heredar <c>BackgroundService</c> sin el ejecutor y registrarse fuera de <c>Program.cs</c>.
    /// </summary>
    private static readonly Dictionary<string, string> ExcepcionesSinCooperativa = new(StringComparer.Ordinal)
    {
        // Crea los índices de las colecciones de auditoría en Mongo al arrancar: no toca ninguna base
        // transaccional ni tiene actor; lo registra el propio módulo de auditoría (AddAuditServices).
        ["AuditIndexBootstrap"] = "índices de Mongo al arrancar; sin base de cooperativa",
        // Migra la base administrativa y cada base de cooperativa con AutoMigrate y levanta
        // DatabaseReadiness: es la condición previa de todo trabajo por cooperativa, no uno de ellos.
        ["DatabaseInitializerHostedService"] = "migraciones al arrancar; precede a toda cooperativa",
        // Vence invitaciones en la base administrativa (AdminDbContext), que es una sola y vive fuera
        // de toda cooperativa (Principio IV).
        ["InvitationExpiryJob"] = "base administrativa, no de cooperativa",
        // Limpia tokens de restablecimiento de contraseña de la identidad central (AdminDbContext).
        ["PasswordResetTokenCleanupJob"] = "base administrativa, no de cooperativa",
    };

    /// <summary>
    /// Trabajos por cooperativa que todavía guardan con <c>SaveChangesAsync</c> propio, con su motivo.
    /// </summary>
    private static readonly Dictionary<string, string> GuardanPorSuCuenta = new(StringComparer.Ordinal)
    {
        // T048: el estado técnico del correo de una notificación (intentos, enviado, error) se guarda
        // directo; un comando dejaría un evento de auditoría cada 15 s por cooperativa. Corre dentro de
        // IEjecutorEnCooperativa, así que la base y el actor son los de la cooperativa.
        ["NotificationEmailDispatcher.cs"] = "estado técnico del correo; un comando auditaría cada pasada",
    };

    private static readonly Regex HeredaDeBackgroundService = new(
        @"\bclass\s+(?<nombre>\w+)\b[^{;]*?:\s*[^{;]*?\bBackgroundService\b", RegexOptions.Compiled);

    private static readonly Regex AgregaServicioAlojado = new(
        @"\bAddHostedService\s*(<\s*(?<tipo>[\w.]+)\s*>)?\s*\((?<args>[^;]*)", RegexOptions.Compiled);

    private static readonly string[] DependenciasDeCooperativa = ["ISender", "ITenantDirectory", "IApplicationDbContext"];

    private static readonly string ProgramDeLaApi = Path.Combine("src", "Presentation", "IngenIA365ERP.API", "Program.cs");

    [Fact]
    public void Cada_trabajo_de_fondo_pasa_por_el_ejecutor_y_no_guarda_por_su_cuenta()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var relativo in TrabajosDeFondo)
        {
            var archivo = Path.Combine(root, relativo);
            Assert.True(File.Exists(archivo), $"No existe {relativo}: si el trabajo se movió, actualizá TrabajosDeFondo.");
            var texto = FuenteSinComentarios.Leer(archivo);

            if (!texto.Contains("IEjecutorEnCooperativa", StringComparison.Ordinal))
                infractores.Add($"{relativo}: no recibe IEjecutorEnCooperativa");
            if (texto.Contains("SaveChangesAsync", StringComparison.Ordinal)
                && !GuardanPorSuCuenta.ContainsKey(Path.GetFileName(relativo)))
                infractores.Add($"{relativo}: llama a SaveChangesAsync (escribe sólo por comandos)");
        }

        Assert.True(infractores.Count == 0,
            "Trabajos de fondo que operan sin cooperativa resuelta (FR-083, T10, T47):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Todo_BackgroundService_esta_declarado_y_el_que_toca_una_cooperativa_usa_el_ejecutor()
    {
        var root = RepoPath.FindRepoRoot();
        var declarados = TrabajosDeFondo.Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        var infractores = new List<string>();
        var encontrados = new HashSet<string>(StringComparer.Ordinal);

        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in HeredaDeBackgroundService.Matches(texto))
            {
                var nombre = m.Groups["nombre"].Value;
                var relativo = Path.GetRelativePath(root, archivo);
                encontrados.Add(nombre);

                if (ExcepcionesSinCooperativa.ContainsKey(nombre)) continue;
                if (!declarados.Contains(nombre))
                {
                    infractores.Add($"{relativo}: {nombre} hereda BackgroundService y no está en TrabajosDeFondo ni en las excepciones");
                    continue;
                }
                var dependeDeCooperativa = DependenciasDeCooperativa.Any(d => Regex.IsMatch(texto, $@"\b{d}\b"));
                if (dependeDeCooperativa && !texto.Contains("IEjecutorEnCooperativa", StringComparison.Ordinal))
                    infractores.Add($"{relativo}: {nombre} toca datos de cooperativa sin IEjecutorEnCooperativa");
            }
        }

        // La lista explícita no puede quedar desfasada del recorrido: si el patrón dejara de reconocer
        // un trabajo declarado, la regla no estaría mirando nada.
        foreach (var declarado in declarados.Where(d => !encontrados.Contains(d)))
            infractores.Add($"{declarado}: está en TrabajosDeFondo pero el recorrido no lo reconoce como BackgroundService");

        Assert.True(infractores.Count == 0,
            "Servicios de fondo sin decisión o sin ejecutor por cooperativa (FR-083, T10):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void AddHostedService_solo_en_el_Program_de_la_API()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();
        var enProgram = 0;

        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            var relativo = Path.GetRelativePath(root, archivo);
            foreach (Match m in AgregaServicioAlojado.Matches(texto))
            {
                if (string.Equals(relativo, ProgramDeLaApi, StringComparison.OrdinalIgnoreCase))
                {
                    enProgram++;
                    continue;
                }
                var tipo = m.Groups["tipo"].Success ? m.Groups["tipo"].Value.Split('.').Last() : null;
                if (tipo is not null && ExcepcionesSinCooperativa.ContainsKey(tipo)) continue;
                infractores.Add($"{relativo}: AddHostedService{(tipo is null ? "" : $"<{tipo}>")} fuera de API/Program.cs");
            }
        }

        Assert.True(enProgram > 0, "API/Program.cs no registra ningún AddHostedService: ¿cambió la forma de registrarlos?");
        Assert.True(infractores.Count == 0,
            "Los trabajos de fondo se registran sólo en API/Program.cs (T10, T47):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Las_excepciones_siguen_existiendo()
    {
        // Si una excepción desaparece, la lista se limpia; nada debe quedar exceptuado en silencio.
        var textos = RepoPath.ProductionCSharpFiles().Select(FuenteSinComentarios.Leer).ToList();
        foreach (var nombre in ExcepcionesSinCooperativa.Keys)
            Assert.True(textos.Any(t => Regex.IsMatch(t, $@"\bclass\s+{nombre}\b")),
                $"{nombre} ya no existe: quitálo de ExcepcionesSinCooperativa.");
        foreach (var archivo in GuardanPorSuCuenta.Keys)
            Assert.True(TrabajosDeFondo.Any(t => Path.GetFileName(t) == archivo),
                $"{archivo} está en GuardanPorSuCuenta pero no en TrabajosDeFondo.");
    }
}
