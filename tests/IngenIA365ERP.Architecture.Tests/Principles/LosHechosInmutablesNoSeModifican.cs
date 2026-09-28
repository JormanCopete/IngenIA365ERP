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
        // US11 (T389): las capturas de un conteo físico sólo se agregan (una corrección es otra captura negativa).
        "CountCapture",
        // US7, entrega I2 (T476, T479): el intento de entrega y el recibo contable de un mensaje de Inventario.
        "IntegrationDeliveryAttempt",
        "InventoryPosting",
        // US8, entrega I4 (T671; data-model §18, §26): la versión de un documento electrónico y cada intento ante el canal.
        "ElectronicDocumentVersion",
        "ElectronicDocumentTransmission",
        // US16, entrega I5 (T825; data-model §3.5): el consumo de una capa PEPS. La capa (CostLayer) es proyección y no está aquí:
        // la protege NadieEscribeElKardexFueraDelRegistro.
        "LayerConsumption",
    ];

    /// <summary>
    /// T671 (data-model §18, §26): las propiedades que un hecho admite escribir una sola vez (nulo → valor, <c>[EscrituraUnica]</c>).
    /// En la versión de un documento electrónico son exactamente los cuatro artefactos, que llegan después de numerar; la transmisión no
    /// admite ninguna.
    /// </summary>
    private static readonly Dictionary<Type, string[]> EscrituraUnicaAdmitida = new()
    {
        [typeof(IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion)] =
        [
            "CanonicalAttachmentPublicId", "SignedXmlAttachmentPublicId", "AttachedDocumentAttachmentPublicId", "GraphicPdfAttachmentPublicId",
        ],
        [typeof(IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentTransmission)] = [],
    };

    [Fact]
    public void La_version_y_la_transmision_electronicas_son_hechos_con_escritura_unica_solo_en_los_artefactos()
    {
        var infractores = new List<string>();
        foreach (var (tipo, admitidas) in EscrituraUnicaAdmitida)
        {
            if (!typeof(IngenIA365ERP.Domain.Common.IHechoInmutable).IsAssignableFrom(tipo))
                infractores.Add($"{tipo.Name} no es IHechoInmutable");

            var conEscrituraUnica = tipo.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.IsDefined(typeof(IngenIA365ERP.Domain.Common.EscrituraUnicaAttribute), inherit: true))
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (!conEscrituraUnica.SequenceEqual(admitidas.OrderBy(n => n, StringComparer.Ordinal)))
                infractores.Add($"{tipo.Name}: [EscrituraUnica] en {{{string.Join(", ", conEscrituraUnica)}}}; se admite sólo {{{string.Join(", ", admitidas)}}}");

            foreach (var p in tipo.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                         .Where(p => p.DeclaringType == tipo && p.SetMethod is { IsPublic: true }))
            {
                var esInit = p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                    .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
                if (!esInit) infractores.Add($"{tipo.Name}.{p.Name} tiene set público");
            }
        }

        Assert.True(infractores.Count == 0,
            "Los hechos del documento electrónico sólo admiten nulo → valor en sus artefactos (T671, Principio XI):\n  " + string.Join("\n  ", infractores));
    }

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
        // I5 (T825): los consumos de capa son hechos; nadie los borra ni los actualiza en bloque.
        Assert.Contains("LayerConsumptions", conjuntos);

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

    /// <summary>
    /// I5, T825 (FR-043; data-model §3.5): el consumo de capa lleva el marcador, así la guarda del guardado rechaza cualquier cambio o baja
    /// después de insertarlo; y la capa no, porque es proyección (su restante se mueve con cada salida y la reconstrucción la corrige).
    /// </summary>
    [Fact]
    public void El_consumo_de_capa_es_un_hecho_y_la_capa_es_proyeccion()
    {
        var hecho = typeof(IngenIA365ERP.Domain.Common.IHechoInmutable);
        Assert.True(hecho.IsAssignableFrom(typeof(IngenIA365ERP.Domain.Entities.Inventory.Transactions.LayerConsumption)),
            "LayerConsumption tiene que ser IHechoInmutable (T825).");
        Assert.False(hecho.IsAssignableFrom(typeof(IngenIA365ERP.Domain.Entities.Inventory.Projections.CostLayer)),
            "CostLayer es proyección del kardex, no un hecho (data-model §3.5).");
        var conSetPublico = typeof(IngenIA365ERP.Domain.Entities.Inventory.Transactions.LayerConsumption)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(p => p.SetMethod is { IsPublic: true }
                && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"))
            .Select(p => p.Name).ToList();
        Assert.True(conSetPublico.Count == 0, "LayerConsumption con set público: " + string.Join(", ", conSetPublico));
    }

    private static readonly Regex SetPublico =new(@"public\s+[^;{=]+\{\s*get;\s*set;", RegexOptions.Compiled);

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
