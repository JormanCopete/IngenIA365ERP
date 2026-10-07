using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-009 y FR-149 (DT §2.25, T21): toda escritura de pantalla de Cartera lleva <c>Idempotency-Key</c>;
/// cada archivo de rutas de escritura de Cartera la exige.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0002) y la llena cada historia con sus archivos de rutas en <c>API/Endpoints/Lending</c> (los de hoy son de la cartera heredada); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LosComandosDeCarteraLlevanClave
{
    /// <summary>Archivos de rutas de escritura de Cartera (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // Fase 3 (T0234, C107): la migración y la activación en su forma mínima; US8 suma sus demás rutas.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/MigrationEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ActivationEndpoints.cs",
    ];

    private static readonly Regex Requerido = new(@"Idempotency|ConClave|RequiereClave", RegexOptions.Compiled);

    [Fact]
    public void Cada_archivo_de_rutas_de_escritura_exige_la_clave()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Un archivo de rutas de Cartera no exige Idempotency-Key (FR-009)");
    }
}
