using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-147 y A7 (DT §2.25, T93): Nómina conserva una proyección del SMMLV, la UVT y los festivos que
/// sólo escribe la réplica. Reemplaza en nombre a <c>SoloLaReplicaEscribeElSalarioMinimo</c> de los informes, que
/// nunca existió en el código.
///
/// <para>
/// Llenada en la fase 2 (T0036). Nómina sigue escribiendo festivos (los <c>Manual</c> y <c>Decreed</c>) y parámetros legales (todos los
/// demás), así que la regla no es «nadie agrega a la tabla» sino: (1) sólo <c>EfectoDeReplicacionEnNomina</c> crea un festivo con origen
/// <c>National</c> o un parámetro con código <c>SMMLV</c>/<c>UVT</c>; (2) la semilla de parámetros (Order 71) ya no trae esos dos códigos y
/// la de festivos (Order 74) ya no existe; (3) <c>AddLegalParameterVersionCommand</c> los rechaza
/// (<c>Payroll.LegalParameter.NationalValue</c>).
/// </para>
/// </summary>
public class SoloLaReplicaEscribeLosValoresNacionalesDeNomina
{
    /// <summary>Carpetas revisadas (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas =
    [
        "Core/IngenIA365ERP.Application",
        "Presentation",
        "Infrastructure/IngenIA365ERP.Persistence/Seeding",
        "Infrastructure/IngenIA365ERP.Persistence/Services",
    ];

    /// <summary>Archivos exentos: la réplica (rutas relativas a la raíz).</summary>
    private static readonly string[] Exentos =
    [
        "src/Core/IngenIA365ERP.Application/Payroll/NationalValues/EfectoDeReplicacionEnNomina.cs",
    ];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bOrigin\s*=\s*HolidayOrigin\.National\b", RegexOptions.Compiled), "crea un festivo nacional"),
        (new Regex(@"\bCode\s*=\s*(LegalParameterCodes\.(Smmlv|Uvt)|""(SMMLV|UVT)"")\s*[,}]", RegexOptions.Compiled), "crea el SMMLV o la UVT"),
    ];

    [Fact]
    public void Solo_la_replica_escribe_la_proyeccion_de_Nomina()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Alguien fuera de la réplica escribe la proyección nacional de Nómina (A7)");
    }

    [Fact]
    public void La_replica_si_escribe_la_proyeccion()
    {
        var texto = File.ReadAllText(Path.Combine(RepoPath.FindRepoRoot(), Exentos[0]));
        Assert.Matches(Prohibidos[0].Patron, texto);
        Assert.Contains("PayrollLegalParameters.Add", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Las_semillas_de_Nomina_ya_no_traen_los_valores_nacionales()
    {
        var codigos = IngenIA365ERP.Persistence.Seeding.Parametric.PayrollLegalParametersSeeder.Catalogo().Select(p => p.Code).ToList();
        Assert.DoesNotContain(LegalParameterCodes.Smmlv, codigos);
        Assert.DoesNotContain(LegalParameterCodes.Uvt, codigos);
        Assert.NotEmpty(codigos);

        var semillas = Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure", "IngenIA365ERP.Persistence", "Seeding");
        Assert.Empty(Directory.EnumerateFiles(semillas, "HolidaysSeeder.cs", SearchOption.AllDirectories));
    }

    [Fact]
    public void Nomina_rechaza_registrar_el_SMMLV_y_la_UVT()
    {
        var comando = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "Payroll",
            "LegalParameters", "LegalParameterCommands.cs"));
        Assert.Contains("Payroll.LegalParameter.NationalValue", comando, StringComparison.Ordinal);
        Assert.Contains("EfectoDeReplicacionEnNomina.CodigosProyectados", comando, StringComparison.Ordinal);
        Assert.True(IngenIA365ERP.Application.Payroll.NationalValues.EfectoDeReplicacionEnNomina.CodigosProyectados
            .SetEquals([LegalParameterCodes.Smmlv, LegalParameterCodes.Uvt]), "La réplica proyecta exactamente el SMMLV y la UVT.");
    }
}
