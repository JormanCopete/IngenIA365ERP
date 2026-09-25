using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 (decisiones-transversales T40, §2.18), FR-063: las credenciales de facturación
/// electrónica (certificado de firma, su clave, el token del proveedor) viven en el Secret
/// <c>erp-fe-credenciales</c> y se leen por <c>CredencialesEnArchivo</c> con una ruta derivada sólo
/// de la cooperativa resuelta. Nunca entran a la base: ni como entidad, ni como <c>DbSet</c>, ni en
/// una configuración EF. Una copia de la base no debe bastar para firmar a nombre de la cooperativa.
///
/// <para>
/// Esqueleto del Setup: <see cref="TiposDeCredencial"/> empieza vacía y la prueba afirma la regla
/// sobre cada elemento; con la lista vacía pasa porque no hay nada que violar, no por un
/// <c>return</c> temprano. La llena la fase 15 (US8, entrega I4) al crear los tipos de credencial.
/// </para>
/// </summary>
public class LasCredencialesDeFacturacionNoTocanLaBase
{
    /// <summary>Nombres de tipo de las credenciales de facturación. Los agrega la entrega I4.</summary>
    private static readonly string[] TiposDeCredencial = [];

    [Fact]
    public void Ninguna_credencial_de_facturacion_se_mapea_a_la_base()
    {
        var root = RepoPath.FindRepoRoot();
        var persistencia = RepoPath.ProductionCSharpFiles()
            .Where(f => f.Replace('\\', '/').Contains("/IngenIA365ERP.Persistence", StringComparison.Ordinal)
                     || f.Replace('\\', '/').Contains("/IngenIA365ERP.Domain/Entities/", StringComparison.Ordinal)
                     || Path.GetFileName(f) == "IApplicationDbContext.cs")
            .ToList();
        var infractores = new List<string>();

        foreach (var tipo in TiposDeCredencial)
        {
            var mapeo = new Regex(
                $@"DbSet<{Regex.Escape(tipo)}>|IEntityTypeConfiguration<{Regex.Escape(tipo)}>|Entity<{Regex.Escape(tipo)}>|\bclass\s+{Regex.Escape(tipo)}\b",
                RegexOptions.Compiled);
            foreach (var archivo in persistencia)
            {
                if (mapeo.IsMatch(File.ReadAllText(archivo)))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: {tipo} llega a la base");
            }
        }

        Assert.True(infractores.Count == 0,
            "Credenciales de facturación electrónica en la base (FR-063, T40):\n  " + string.Join("\n  ", infractores));
    }
}
