using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Persistence.DbContext;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T3, R125, enmienda A10 «propuesta por defecto: sí», C89; T0124): el espacio de nombres <c>Lending</c> es
/// sólo de la Cartera nueva. Lo conservado de ahorros y aportes vive en <c>Domain.Entities.Savings</c>, sus configuraciones en
/// <c>Configurations/Savings</c>, sus handlers en <c>Application.Savings</c> y sus rutas en <c>Endpoints/Savings</c> (<c>/api/savings/*</c>)
/// y <c>SavingsReportsEndpoints</c> (<c>/api/reports/savings/*</c>); ningún tipo de <c>*.Lending*</c> toca un <c>DbSet</c> de lo
/// conservado y <c>/api/lending/*</c> y <c>/api/reports/lending</c> no publican nada de ello.
/// </summary>
public class ElEspacioLendingEsSoloCartera
{
    /// <summary>Las carpetas de handlers conservadas (T0185), ahora bajo <c>Application/Savings</c>.</summary>
    private static readonly string[] HandlersConservados =
    [
        "SavingsAccounts", "SavingsParameters", "SavingsInterest", "Contributions", "Statements", "Withdrawals",
        "WithdrawalStatuses", "Zones", "SubZones", "ZoneTypes", "SiplaParameters", "TransactionCodes", "Reports",
    ];

    /// <summary>Los sufijos de las 12 familias de rutas mudadas (api.md §25).</summary>
    private static readonly string[] FamiliasConservadas =
    [
        "contributions", "savings-accounts", "savings-interest-liquidation", "savings-parameters", "sipla-parameters", "statements",
        "sub-zones", "transaction-codes", "withdrawal-statuses", "withdrawals", "zone-types", "zones",
    ];

    private static string Src => Path.Combine(RepoPath.FindRepoRoot(), "src");

    [Fact]
    public void Las_38_entidades_conservadas_viven_en_Savings()
    {
        var tipos = typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly.GetTypes()
            .ToLookup(t => t.Name, t => t.Namespace ?? string.Empty, StringComparer.Ordinal);
        var infracciones = CarteraHeredada.Conservadas
            .Where(c => !tipos[c.Entidad].Contains("IngenIA365ERP.Domain.Entities.Savings"))
            .Select(c => $"{c.Entidad}: no está en IngenIA365ERP.Domain.Entities.Savings")
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Lo conservado sigue fuera de Savings (T3, A10)");
    }

    [Fact]
    public void Sus_configuraciones_viven_en_Configurations_Savings()
    {
        var configuraciones = typeof(ApplicationDbContext).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<>))
                .Select(i => (Configuracion: t, Entidad: i.GetGenericArguments()[0])))
            .ToList();
        var conservadas = CarteraHeredada.Conservadas.Select(c => c.Entidad).ToHashSet(StringComparer.Ordinal);

        var mal = configuraciones
            .Where(c => conservadas.Contains(c.Entidad.Name) && c.Entidad.Namespace == "IngenIA365ERP.Domain.Entities.Savings")
            .Where(c => c.Configuracion.Namespace != "IngenIA365ERP.Persistence.Configurations.Savings")
            .Select(c => c.Configuracion.FullName!)
            .ToList();
        Assert.True(mal.Count == 0, "Configuraciones de lo conservado fuera de Configurations/Savings: " + string.Join(", ", mal));
        Assert.Equal(38, configuraciones.Count(c => c.Entidad.Namespace == "IngenIA365ERP.Domain.Entities.Savings"));
        Assert.Empty(configuraciones.Where(c => (c.Configuracion.Namespace ?? string.Empty).EndsWith(".Configurations.Lending", StringComparison.Ordinal)
                                                && c.Entidad.Namespace == "IngenIA365ERP.Domain.Entities.Savings"));
    }

    [Fact]
    public void Sus_handlers_viven_en_Application_Savings_y_no_quedan_en_Lending()
    {
        var savings = Path.Combine(Src, "Core", "IngenIA365ERP.Application", "Savings");
        var lending = Path.Combine(Src, "Core", "IngenIA365ERP.Application", "Lending");
        foreach (var carpeta in HandlersConservados)
        {
            Assert.True(Directory.Exists(Path.Combine(savings, carpeta)), $"Falta Application/Savings/{carpeta} (T0185).");
            Assert.False(Directory.Exists(Path.Combine(lending, carpeta)) && Directory.EnumerateFiles(Path.Combine(lending, carpeta), "*.cs", SearchOption.AllDirectories).Any(),
                $"Application/Lending/{carpeta} sigue con código: lo conservado vive en Savings.");
        }

        var tipos = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => (t.Namespace ?? string.Empty).StartsWith("IngenIA365ERP.Application.Savings", StringComparison.Ordinal));
        Assert.NotEmpty(tipos);
    }

    [Fact]
    public void Ningun_tipo_de_Lending_toca_lo_conservado()
    {
        // Los DbSet de las 38 y su espacio de entidades no se nombran desde Cartera.
        var dbSets = typeof(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType
                        && p.PropertyType.GetGenericArguments()[0].Namespace == "IngenIA365ERP.Domain.Entities.Savings")
            .Select(p => p.Name)
            .ToList();
        var prohibidos = dbSets
            .Select(n => (new Regex($@"\.{Regex.Escape(n)}\b", RegexOptions.Compiled), $"usa el DbSet conservado {n}"))
            .Append((new Regex(@"IngenIA365ERP\.Domain\.Entities\.Savings\b", RegexOptions.Compiled), "usa entidades de ahorros y aportes"))
            .Append((new Regex(@"IngenIA365ERP\.Application\.Savings\b", RegexOptions.Compiled), "usa handlers de ahorros y aportes"))
            .ToList();
        var carpetas = new[]
        {
            "Core/IngenIA365ERP.Application/Lending",
            "Core/IngenIA365ERP.Domain/Entities/Lending",
            "Core/IngenIA365ERP.Domain/Lending",
            "Infrastructure/IngenIA365ERP.Persistence/Configurations/Lending",
            "Presentation/IngenIA365ERP.API/Endpoints/Lending",
        };
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(carpetas), prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Cartera toca lo conservado de ahorros y aportes (T3, R125)");
    }

    [Fact]
    public void Las_rutas_conservadas_viven_en_api_savings_y_no_en_api_lending()
    {
        var endpoints = Path.Combine(Src, "Presentation", "IngenIA365ERP.API", "Endpoints");
        var savings = Directory.EnumerateFiles(Path.Combine(endpoints, "Savings"), "*.cs").ToList();
        Assert.Equal(12, savings.Count);
        foreach (var archivo in savings)
        {
            var fuente = FuenteSinComentarios.Leer(archivo);
            Assert.Matches(@"MapGroup\(""/api/savings/[a-z-]+""\)", fuente);
            Assert.Contains("namespace IngenIA365ERP.API.Endpoints.Savings;", fuente, StringComparison.Ordinal);
        }

        var informes = FuenteSinComentarios.Leer(Path.Combine(endpoints, "Reports", "SavingsReportsEndpoints.cs"));
        Assert.Contains("MapGroup(\"/api/reports/savings\")", informes, StringComparison.Ordinal);
        Assert.Contains("\"/person-portfolio/{personId:guid}\"", informes, StringComparison.Ordinal);
        Assert.Contains("\"/savings-statement/{accountId:guid}/pdf\"", informes, StringComparison.Ordinal);

        // Ningún archivo publica una familia conservada bajo /api/lending, ni nada bajo /api/reports/lending.
        var prohibidos = FamiliasConservadas
            .Select(f => (new Regex($@"""/api/lending/{Regex.Escape(f)}\b", RegexOptions.Compiled), $"publica /api/lending/{f}"))
            .Append((new Regex(@"""/api/reports/lending\b", RegexOptions.Compiled), "publica /api/reports/lending"))
            .ToList();
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(["Presentation/IngenIA365ERP.API"]), prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Lo conservado vuelve a /api/lending (api.md §25)");
    }
}
