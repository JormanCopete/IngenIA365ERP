using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T10, T47, T6, §2.18), FR-083: todo trabajo de fondo
/// que opera sobre datos de una cooperativa lo hace por <c>IEjecutorEnCooperativa</c> —que resuelve
/// la cooperativa, su base y el actor «Proceso de integración»— y escribe sólo por comandos: nunca
/// llama a <c>SaveChangesAsync</c> por su cuenta. Sin eso la auditoría del proceso cae en la base
/// global o sin actor.
///
/// <para>
/// Esqueleto del Setup: <see cref="TrabajosDeFondo"/> (archivos relativos a la raíz del repositorio)
/// empieza vacía y la prueba afirma la regla sobre cada elemento; con la lista vacía pasa porque no
/// hay nada que violar, no por un <c>return</c> temprano. La llena la fase 2 (plataforma, T018), que
/// además agrega el recorrido de todo <c>BackgroundService</c>, la regla de <c>AddHostedService</c>
/// sólo en <c>API/Program.cs</c> y las excepciones existentes con su motivo.
/// </para>
/// </summary>
public class NingunTrabajoDeFondoOperaSinCooperativa
{
    /// <summary>Trabajos de fondo que operan por cooperativa, como ruta relativa a la raíz. Los agrega la plataforma.</summary>
    private static readonly string[] TrabajosDeFondo = [];

    [Fact]
    public void Cada_trabajo_de_fondo_pasa_por_el_ejecutor_y_no_guarda_por_su_cuenta()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var relativo in TrabajosDeFondo)
        {
            var archivo = Path.Combine(root, relativo);
            Assert.True(File.Exists(archivo), $"No existe {relativo}: si el trabajo se movió, actualizá TrabajosDeFondo.");
            var texto = File.ReadAllText(archivo);

            if (!texto.Contains("IEjecutorEnCooperativa", StringComparison.Ordinal))
                infractores.Add($"{relativo}: no recibe IEjecutorEnCooperativa");
            if (texto.Contains("SaveChangesAsync", StringComparison.Ordinal))
                infractores.Add($"{relativo}: llama a SaveChangesAsync (escribe sólo por comandos)");
        }

        Assert.True(infractores.Count == 0,
            "Trabajos de fondo que operan sin cooperativa resuelta (FR-083, T10, T47):\n  " + string.Join("\n  ", infractores));
    }
}
