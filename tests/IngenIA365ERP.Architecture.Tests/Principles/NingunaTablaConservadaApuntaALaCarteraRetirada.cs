using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, R-07): ninguna tabla conservada (ahorros, aportes) conserva una llave hacia una tabla
/// de crédito retirada; se revisan los dos snapshots de migraciones.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0005) y la llena la fase Fundacional B al retirar; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class NingunaTablaConservadaApuntaALaCarteraRetirada
{
    /// <summary>Nombres de las tablas retiradas.</summary>
    private static readonly string[] Nombres = [];

    /// <summary>Carpetas de migraciones (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [
        "Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql",
        "Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer",
    ];

    [Fact]
    public void Los_snapshots_no_referencian_tablas_retiradas()
    {
        var prohibidos = Nombres
            .Select(n => (new Regex($@"\b{Regex.Escape(n)}\b", RegexOptions.Compiled), $"referencia la tabla retirada {n}"))
            .ToList();
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(Carpetas), prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Una tabla conservada apunta a la cartera retirada (R-07)");
    }
}
