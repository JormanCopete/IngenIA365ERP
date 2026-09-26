using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T18, §2.18), FR-002 y FR-008: las entidades que son
/// <b>hechos</b> (<c>IHechoInmutable</c>: línea de kardex, consumo de capa, mensaje, dependencia,
/// intento, decisión de aprobación, <c>InventoryPosting</c>, instantánea de tercero, línea de
/// impuesto, versión y transmisión electrónica, ancla) no cambian después de insertarse. En el
/// fuente eso se ve en que no tienen <c>set</c> público: sólo <c>init</c> o <c>private set</c>. En
/// ejecución lo hace cumplir <c>ApplicationDbContext.SaveChangesAsync</c>.
///
/// <para>
/// Esqueleto del Setup: <see cref="Hechos"/> (nombres de tipo) empieza vacía y la prueba afirma la
/// regla sobre cada elemento; con la lista vacía pasa porque no hay nada que violar, no por un
/// <c>return</c> temprano. La llena el bloque que crea cada entidad (plataforma, fase 2; base de
/// inventario, fase 3; y las historias que agregan hechos).
/// </para>
/// </summary>
public class LosHechosInmutablesNoSeModifican
{
    /// <summary>Nombres de tipo de las entidades que son hechos inmutables. Los agrega el bloque que las crea.</summary>
    private static readonly string[] Hechos =
    [
        // Plataforma, bandeja de salida (T073): el mensaje y su arista sólo tienen init.
        "IntegrationMessage",
        "IntegrationMessageDependency",
        // Base de inventario, fase 3 (T133): la copia fiscal de la contraparte y la foto tributaria.
        "DocumentPartySnapshot",
        "DocumentTaxLine",
        // US2 (T245): el hecho del kardex y el historial del grupo contable de un producto (US1, T201).
        "KardexEntry",
        "ProductAccountingGroupChange",
    ];

    /// <summary>
    /// T117 (T18): la guarda de <c>ApplicationDbContext.SaveChangesAsync</c> no tiene lista que mantener —cubre a todo
    /// tipo por el marcador—, así que lo que se fija es que exista, que el guardado la llame antes de escribir y que mire
    /// los dos marcadores. Si alguien la cambia por una lista, esta prueba tiene que cambiar con ella.
    /// </summary>
    [Fact]
    public void El_guardado_hace_cumplir_los_dos_marcadores_para_todo_tipo()
    {
        var root = RepoPath.FindRepoRoot();
        var carpeta = Path.Combine(root, "src", "Infrastructure", "IngenIA365ERP.Persistence", "DbContext");
        var contexto = File.ReadAllText(Path.Combine(carpeta, "ApplicationDbContext.cs"));
        var guarda = File.ReadAllText(Path.Combine(carpeta, "GuardaDeInmutabilidad.cs"));

        Assert.Matches(@"SaveChangesAsync\(CancellationToken[^)]*\)\s*\{\s*(//[^\n]*\n\s*)*await GuardaDeInmutabilidad\.VerificarAsync\(this", contexto);
        Assert.Contains("case IHechoInmutable", guarda);
        Assert.Contains("case IInmutableTrasConfirmar", guarda);

        var marcados = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && (typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(t)
                    || typeof(IngenIA365ERP.Domain.Common.IInmutableTrasConfirmar).IsAssignableFrom(t)))
            .ToList();
        Assert.NotEmpty(marcados);
        Assert.All(marcados.Where(t => typeof(IngenIA365ERP.Domain.Common.IInmutableTrasConfirmar).IsAssignableFrom(t)),
            t => Assert.NotNull(t.GetProperty(nameof(IngenIA365ERP.Domain.Common.IInmutableTrasConfirmar.Status))));
    }

    /// <summary>
    /// T117 (T18): fuera de su creación, ningún archivo de <c>Application</c> llama <c>Remove</c>, <c>RemoveRange</c>,
    /// <c>Update</c> ni <c>UpdateRange</c> sobre el <c>DbSet</c> de un hecho o de un documento que se fija al confirmar.
    /// </summary>
    [Fact]
    public void Application_no_borra_ni_actualiza_en_bloque_hechos_ni_documentos()
    {
        var root = RepoPath.FindRepoRoot();
        var conjuntos = typeof(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType)
            .Where(p =>
            {
                var entidad = p.PropertyType.GetGenericArguments()[0];
                return typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(entidad)
                    || typeof(IngenIA365ERP.Domain.Common.IInmutableTrasConfirmar).IsAssignableFrom(entidad);
            })
            .Select(p => p.Name)
            .ToList();
        Assert.Contains("DocumentTaxLines", conjuntos);
        Assert.Contains("InventoryDocuments", conjuntos);

        var infractores = new List<string>();
        foreach (var archivo in RepoPath.ProductionCSharpFiles()
                     .Where(f => f.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Application{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var texto = File.ReadAllText(archivo);
            foreach (var conjunto in conjuntos)
            {
                if (Regex.IsMatch(texto, $@"\.{Regex.Escape(conjunto)}\s*\.\s*(Remove|RemoveRange|Update|UpdateRange|ExecuteDelete|ExecuteDeleteAsync|ExecuteUpdate|ExecuteUpdateAsync)\b"))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: {conjunto}");
            }
        }

        Assert.True(infractores.Count == 0,
            "Hechos o documentos borrados o actualizados en bloque (Principio XI, T18):\n  " + string.Join("\n  ", infractores));
    }

    private static readonly Regex SetPublico = new(@"public\s+[^;{=]+\{\s*get;\s*set;", RegexOptions.Compiled);

    [Fact]
    public void Ningun_hecho_expone_un_set_publico()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var hecho in Hechos)
        {
            var declaracion = new Regex($@"\bclass\s+{Regex.Escape(hecho)}\b", RegexOptions.Compiled);
            var (archivo, texto) = fuentes.FirstOrDefault(f => declaracion.IsMatch(f.Texto));

            if (archivo is null)
                infractores.Add($"{hecho}: no se encontró su declaración (si se renombró, actualizá Hechos)");
            else if (SetPublico.IsMatch(texto))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {hecho} tiene una propiedad con set público");
        }

        Assert.True(infractores.Count == 0,
            "Hechos inmutables que se pueden modificar (FR-002, Principio XI):\n  " + string.Join("\n  ", infractores));
    }
}
