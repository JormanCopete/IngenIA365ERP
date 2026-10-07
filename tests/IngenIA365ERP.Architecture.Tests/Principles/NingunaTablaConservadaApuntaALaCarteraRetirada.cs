using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25 y §7.6, R-07, T0123; data-model parte 3 §13.1–§13.2): ninguna de las 38 tablas conservadas de la cartera
/// heredada ni <c>PAY_SettlementDeductions</c>/<c>PAY_Novelties</c> conserva una FK, una navegación o una columna
/// <c>LoanPortfolioId</c>/<c>CreditLine</c> hacia una tabla de crédito retirada; <c>LND_SiplaGroupParameters.CreditLineId</c> queda
/// como dato requerido, sin FK ni índice. Se revisa el modelo de los dos motores y su snapshot.
/// </summary>
public class NingunaTablaConservadaApuntaALaCarteraRetirada
{
    private static readonly HashSet<string> TablasRetiradas = CarteraHeredada.Retiradas.Select(r => r.Tabla).ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> TablasVigiladas = CarteraHeredada.Conservadas.Select(c => c.Tabla)
        .Append("PAY_SettlementDeductions").Append("PAY_Novelties")
        .ToHashSet(StringComparer.Ordinal);

    private static ApplicationDbContext Contexto(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres
            ? builder.UseNpgsql("Host=modelo-sin-conexion;Database=x;Username=y;Password=z")
            : builder.UseSqlServer("Server=modelo-sin-conexion;Database=x;User Id=y;Password=z;TrustServerCertificate=True");
        return new ApplicationDbContext(builder.Options);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ninguna_conservada_tiene_FK_navegacion_ni_columna_hacia_lo_retirado(bool postgres)
    {
        using var db = Contexto(postgres);
        var vigiladas = db.Model.GetEntityTypes().Where(t => TablasVigiladas.Contains(t.GetTableName() ?? string.Empty)).ToList();
        Assert.Equal(TablasVigiladas.Count, vigiladas.Select(t => t.GetTableName()).Distinct().Count());

        var violaciones = new List<string>();
        foreach (var tipo in vigiladas)
        {
            var tabla = tipo.GetTableName();
            violaciones.AddRange(tipo.GetForeignKeys()
                .Where(fk => fk.PrincipalEntityType.GetTableName() is { } principal
                             && (TablasRetiradas.Contains(principal) || principal.StartsWith("LND_", StringComparison.Ordinal) && !TablasVigiladas.Contains(principal)))
                .Select(fk => $"{tabla}: FK {string.Join("+", fk.Properties.Select(p => p.Name))} → {fk.PrincipalEntityType.GetTableName()}"));
            violaciones.AddRange(tipo.GetNavigations()
                .Where(n => TablasRetiradas.Contains(n.TargetEntityType.GetTableName() ?? string.Empty))
                .Select(n => $"{tabla}: navegación {n.Name}"));
            violaciones.AddRange(tipo.GetProperties()
                .Where(p => p.Name is "LoanPortfolioId" or "CreditLine")
                .Select(p => $"{tabla}: columna {p.Name}"));
        }
        Assert.True(violaciones.Count == 0, "Una tabla conservada apunta a la cartera retirada (R-07): " + string.Join("; ", violaciones));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void La_linea_de_SIPLA_queda_como_dato_requerido_sin_FK_ni_indice(bool postgres)
    {
        using var db = Contexto(postgres);
        var sipla = db.Model.GetEntityTypes().Single(t => t.GetTableName() == "LND_SiplaGroupParameters");
        var linea = sipla.FindProperty("CreditLineId");
        Assert.NotNull(linea);
        Assert.False(linea!.IsNullable, "CreditLineId queda requerida como dato (§7.6 de las transversales).");
        Assert.Empty(sipla.GetForeignKeys().Where(fk => fk.Properties.Any(p => p.Name == "CreditLineId")));
        Assert.Empty(sipla.GetIndexes().Where(i => i.Properties.Any(p => p.Name == "CreditLineId")));
    }

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void El_snapshot_no_tiene_FK_ni_indices_heredados_en_lo_conservado(string proveedor)
    {
        var snapshot = Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure", $"IngenIA365ERP.Persistence.Migrations.{proveedor}",
            "Application", "ApplicationDbContextModelSnapshot.cs");
        var prohibidos = new (Regex, string)[]
        {
            (new Regex(@"FK_LND_SiplaGroupParameters_LND_CreditLineParameters", RegexOptions.Compiled), "FK de SIPLA a la línea heredada"),
            (new Regex(@"IX_LND_SiplaGroupParameters_CreditLineId", RegexOptions.Compiled), "índice de SIPLA a la línea heredada"),
            (new Regex(@"UK_PAY_SettlementDeductions_Termination_Loan\b", RegexOptions.Compiled), "único heredado de la definitiva"),
            (new Regex(@"IX_PAY_SettlementDeductions_LoanPortfolioId", RegexOptions.Compiled), "índice heredado de la definitiva"),
            (new Regex(@"""LoanPortfolioId""", RegexOptions.Compiled), "columna LoanPortfolioId"),
        };
        var infracciones = ReglaSobreFuente.Infracciones([snapshot], prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, $"El snapshot de {proveedor} conserva una llave hacia la cartera retirada (R-07)");
    }
}
