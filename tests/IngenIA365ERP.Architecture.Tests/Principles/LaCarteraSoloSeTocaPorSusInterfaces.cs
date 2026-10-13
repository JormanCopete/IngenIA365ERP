using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-008 (DT §2.25, T90, C27, C34): Cartera no lee ni escribe datos de otro módulo, ni otro módulo los
/// suyos, salvo por la lista cerrada de interfaces (los cinco puertos de <c>Application.Common.Integration.Lending</c>
/// y los que Cartera consume). Vigila las dos direcciones.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0001) y la llena la fase Fundacional B: los módulos que hoy tocan la
/// cartera heredada entran a <see cref="CarpetasDeOtrosModulos"/> al pasar a los puertos, y las carpetas de Cartera nueva a
/// <see cref="CarpetasDeCartera"/> al nacer. T0120 (sección A.2, Nómina): entran <c>Application/Payroll</c> y
/// <c>Domain/Entities/Payroll</c>, y se fijan los métodos exactos de los cuatro puertos, quién los implementa y que ninguna
/// tabla <c>PAY_</c> tiene FK a <c>LND_</c>. T0120 (secciones A.3 y A.4): entran Personas (<c>Application/Core/People</c>), Comercial
/// (<c>Application/Inventory</c>, <c>Domain/Entities/Inventory</c>, <c>Domain/Inventory</c>) y <c>Application/Savings</c> (vacía hasta la
/// mudanza de T0185; el retiro de asociado ya pasa por la consulta, T0170), y ninguna tabla <c>INV_</c> tiene FK a <c>LND_</c>.
/// Sección B (T0184, T0185, T0190): lo conservado se mudó a <c>Savings</c> y entra al ArchUnit; la exención por nombre de
/// <c>SavingsAccount</c> y la FK heredada de <c>PAY_SettlementDeductions</c> se retiraron con la cartera heredada.</para>
/// </summary>
public class LaCarteraSoloSeTocaPorSusInterfaces
{
    /// <summary>Carpetas de otros módulos (relativas a <c>src/</c>) que no pueden nombrar a Cartera salvo por sus puertos.</summary>
    private static readonly string[] CarpetasDeOtrosModulos =
    [
        "Core/IngenIA365ERP.Application/Payroll",
        "Core/IngenIA365ERP.Domain/Entities/Payroll",
        "Core/IngenIA365ERP.Domain/Payroll",
        "Core/IngenIA365ERP.Application/Core/People",
        "Core/IngenIA365ERP.Application/Inventory",
        "Core/IngenIA365ERP.Application/Savings",
        "Core/IngenIA365ERP.Domain/Entities/Savings",
        "Core/IngenIA365ERP.Domain/Entities/Inventory",
        "Core/IngenIA365ERP.Domain/Inventory",
    ];

    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>) que no pueden nombrar a otros módulos salvo por sus puertos.</summary>
    private static readonly string[] CarpetasDeCartera =
    [
        // US3 (bloque 7C, T0428): el desembolso, su reverso, las garantías, el pagaré y el archivo de desembolsos. La recogida paga por
        // IContratoDeObligaciones; la cuenta de origen llega por IContabilidadParaCartera; el formato y el motor del archivo son de Common.
        "Core/IngenIA365ERP.Application/Lending/Disbursements",
        // US5 (T0526): los pagos (aplicar, reversar, resolver, preferencia) y el contrato real con sus dobles. Nómina llega sólo por
        // IContratoDeObligaciones / IConsultaDeObligaciones; Contabilidad por IContabilidadParaCartera; la mensajería y el registro son de Common.
        "Core/IngenIA365ERP.Application/Lending/Payments",
        "Core/IngenIA365ERP.Application/Lending/Integration",
    ];

    /// <summary>Espacios de otros módulos que el ArchUnit revisa (los de <see cref="CarpetasDeOtrosModulos"/>).</summary>
    private static readonly string[] EspaciosDeOtrosModulos =
    [
        "IngenIA365ERP.Application.Payroll",
        "IngenIA365ERP.Domain.Entities.Payroll",
        "IngenIA365ERP.Domain.Payroll",
        "IngenIA365ERP.Application.Core.People",
        "IngenIA365ERP.Application.Inventory",
        // Feature 013 (T0185): lo conservado de ahorros y aportes, mudado fuera del espacio Lending.
        "IngenIA365ERP.Application.Savings",
        "IngenIA365ERP.Domain.Entities.Savings",
        "IngenIA365ERP.Domain.Entities.Inventory",
        "IngenIA365ERP.Domain.Inventory",
    ];

    private static readonly (Regex Patron, string Que)[] HaciaCartera =
    [
        (new Regex(@"IngenIA365ERP\.(Domain\.Entities|Application)\.Lending\b", RegexOptions.Compiled), "depende de Cartera fuera de Common.Integration.Lending"),
        (new Regex("\"LND_", RegexOptions.Compiled), "nombra una tabla LND_"),
        (new Regex(@"\b(LoanPortfolios|PayrollDeductionEntries|RecaudoDeCredito)\b", RegexOptions.Compiled), "usa la cartera heredada"),
    ];

    private static readonly (Regex Patron, string Que)[] DesdeCartera =
    [
        (new Regex(@"IngenIA365ERP\.Application\.(Inventory|Payroll|Accounting|Core\.People)\b", RegexOptions.Compiled), "depende de otro módulo fuera de sus puertos"),
        (new Regex(@"IngenIA365ERP\.Domain\.Entities\.(Inventory|Payroll|Accounting)\b", RegexOptions.Compiled), "usa entidades de otro módulo"),
        (new Regex("\"(INV|PAY|ACC)_", RegexOptions.Compiled), "nombra una tabla de otro módulo"),
    ];

    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Arquitectura = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build());

    private static string ConHijos(string ns) => $"^{Regex.Escape(ns)}(\\..+)?$";

    /// <summary>Todo espacio <c>*.Lending*</c> salvo los puertos de <c>Application.Common.Integration.Lending</c> y los contratos de mensajes.</summary>
    private const string LendingSalvoPuertos =
        @"^IngenIA365ERP\.(?!Application\.Common\.Integration(\.|$))([\w.]+\.)?Lending(\..+)?$";

    [Fact]
    public void Ningun_otro_modulo_toca_Cartera_salvo_por_sus_puertos()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(CarpetasDeOtrosModulos), HaciaCartera);
        ReglaSobreFuente.SinInfracciones(infracciones, "Otro módulo toca Cartera fuera de Common.Integration.Lending (FR-008, C34)");
    }

    [Fact]
    public void Los_tipos_de_otros_modulos_no_dependen_de_Cartera_salvo_por_sus_puertos()
    {
        // La exención por nombre de SavingsAccount se retiró con la mudanza a Domain.Entities.Savings (T0184).
        var cartera = Types().That().ResideInNamespaceMatching(LendingSalvoPuertos);
        foreach (var ns in EspaciosDeOtrosModulos)
            Types().That().ResideInNamespaceMatching(ConHijos(ns))
                .Should().NotDependOnAny(cartera)
                .Because($"FR-008, T90: {ns} sólo conoce Cartera por Application.Common.Integration.Lending")
                .Check(Arquitectura.Value);
    }

    [Fact]
    public void Cartera_no_toca_otros_modulos_salvo_por_los_puertos_que_consume()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(CarpetasDeCartera), DesdeCartera);
        ReglaSobreFuente.SinInfracciones(infracciones, "Cartera toca otro módulo fuera de sus puertos (FR-008, C34)");
    }

    [Fact]
    public void Los_puertos_que_Cartera_ofrece_declaran_exactamente_sus_metodos()
    {
        // T90, R117: IDimensionesDeCartera lo suma US7.
        Assert.Equal(["EstadoCrediticioAsync", "EstadoDeValidacionAsync"], MetodosDe(typeof(IConsultasDeCartera)));
        Assert.Equal(["LiberarAsync", "ReservarAsync"], MetodosDe(typeof(IReservasDeCartera)));
        Assert.Equal(["CuotasDeNominaAsync", "ObligacionesAsync"], MetodosDe(typeof(IConsultaDeObligaciones)));
        Assert.Equal(["AplicarAsync", "ListarAsync", "ResolverSaldoAFavorAsync", "ReversarAsync", "SimularAsync"], MetodosDe(typeof(IContratoDeObligaciones)));
        Assert.Equal(1, IContratoDeObligaciones.VersionDelContrato);
        foreach (var puerto in new[] { typeof(IConsultasDeCartera), typeof(IReservasDeCartera), typeof(IConsultaDeObligaciones), typeof(IContratoDeObligaciones) })
            Assert.Equal("IngenIA365ERP.Application.Common.Integration.Lending", puerto.Namespace);
    }

    [Fact]
    public void Fuera_de_Cartera_solo_los_provisionales_del_mismo_espacio_implementan_los_puertos()
    {
        // Los dobles viven en tests/ (otro ensamblado). Las implementaciones reales vivirán en Application.Lending.Integration (US5, US6).
        var puertos = new[] { typeof(IConsultasDeCartera), typeof(IReservasDeCartera), typeof(IConsultaDeObligaciones), typeof(IContratoDeObligaciones) };
        var ensamblados = new[] { typeof(IngenIA365ERP.Application.DependencyInjection).Assembly, typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly, typeof(ApplicationDbContext).Assembly };
        var ajenos = ensamblados.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && puertos.Any(p => p.IsAssignableFrom(t)))
            .Where(t => t.Namespace != "IngenIA365ERP.Application.Common.Integration.Lending"
                        && !(t.Namespace ?? string.Empty).StartsWith("IngenIA365ERP.Application.Lending.Integration", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();
        Assert.True(ajenos.Count == 0, "Implementan un puerto de Cartera fuera de su espacio: " + string.Join(", ", ajenos));
    }

    /// <summary>
    /// US5 (T0526): las implementaciones reales son las de Cartera —<c>ContratoDeObligacionesDeCartera</c> y <c>ObligacionesDeCartera</c>— y
    /// <c>DependencyInjection</c> registra ellas, no los provisionales «sin activar» de la fase 3.
    /// </summary>
    [Fact]
    public void Las_implementaciones_reales_del_contrato_y_la_consulta_son_de_Cartera()
    {
        Assert.True(typeof(IContratoDeObligaciones).IsAssignableFrom(typeof(IngenIA365ERP.Application.Lending.Integration.ContratoDeObligacionesDeCartera)));
        Assert.True(typeof(IConsultaDeObligaciones).IsAssignableFrom(typeof(IngenIA365ERP.Application.Lending.Integration.ObligacionesDeCartera)));
        var di = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "DependencyInjection.cs"));
        Assert.Matches(@"IContratoDeObligaciones,\s*[\w.]*ContratoDeObligacionesDeCartera>", di);
        Assert.Matches(@"IConsultaDeObligaciones,\s*[\w.]*ObligacionesDeCartera>", di);
    }

    /// <summary>
    /// US6 (T0577, T0582, T0584): las implementaciones reales de las consultas y las reservas son de Cartera, viven en
    /// <c>Application/Lending/Integration</c> y <c>DependencyInjection</c> las registra en lugar de los provisionales «no habilitada».
    /// </summary>
    [Fact]
    public void Las_consultas_y_las_reservas_reales_son_de_Cartera()
    {
        var consultas = typeof(IngenIA365ERP.Application.Lending.Integration.ConsultasDeCartera);
        var reservas = typeof(IngenIA365ERP.Application.Lending.Integration.ReservasDeCartera);
        Assert.True(typeof(IConsultasDeCartera).IsAssignableFrom(consultas));
        Assert.True(typeof(IReservasDeCartera).IsAssignableFrom(reservas));
        Assert.Equal("IngenIA365ERP.Application.Lending.Integration", consultas.Namespace);
        Assert.Equal("IngenIA365ERP.Application.Lending.Integration", reservas.Namespace);
        var di = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "DependencyInjection.cs"));
        Assert.Matches(@"IConsultasDeCartera,\s*[\w.]*ConsultasDeCartera>", di);
        Assert.Matches(@"IReservasDeCartera,\s*[\w.]*ReservasDeCartera>", di);
    }

    /// <summary>
    /// US6 (T0577; FR-008, C34): Cartera no lee tablas de Comercial —la venta la conoce por el contenido del mensaje—: ningún tipo de
    /// <c>Application.Lending</c> usa entidades de <c>Domain.Entities.Inventory</c> y ningún archivo de Cartera consulta un <c>DbSet</c> de Comercial.
    /// </summary>
    [Fact]
    public void Cartera_no_lee_tablas_de_Comercial()
    {
        const BindingFlags Todo = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var aplicacion = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;
        var infractores = aplicacion.GetTypes()
            .Where(t => (t.Namespace ?? string.Empty).StartsWith("IngenIA365ERP.Application.Lending", StringComparison.Ordinal))
            .SelectMany(t => t.GetFields(Todo).Select(f => f.FieldType)
                .Concat(t.GetMethods(Todo).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
                .Where(x => (x.Namespace ?? string.Empty).StartsWith("IngenIA365ERP.Domain.Entities.Inventory", StringComparison.Ordinal))
                .Select(x => $"{t.FullName} → {x.Name}"))
            .Distinct()
            .ToList();
        Assert.True(infractores.Count == 0, "Cartera usa entidades de Comercial (FR-008): " + string.Join(", ", infractores));

        var fuentes = ReglaSobreFuente.ArchivosEn(CarpetasDeCartera)
            .Where(a => Regex.IsMatch(FuenteSinComentarios.Leer(a), @"\bdb\.(DocumentPayments|InventoryDocuments|LendingObligationResponses|DocumentLines)\b"))
            .ToList();
        Assert.True(fuentes.Count == 0, "Cartera lee tablas INV_: " + string.Join(", ", fuentes));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ninguna_entidad_PAY_ni_INV_tiene_FK_ni_navegacion_hacia_LND(bool postgres)
    {
        // C34: la FK heredada (PAY_SettlementDeductions.LoanPortfolioId) la soltó RetiroDeLaCarteraHeredada (T0190, CH37); ninguna nueva.
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres
            ? builder.UseNpgsql("Host=localhost;Database=x;Username=y;Password=z")
            : builder.UseSqlServer("Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True");
        using var db = new ApplicationDbContext(builder.Options);

        var violaciones = db.Model.GetEntityTypes()
            .Where(t => (t.GetTableName() ?? string.Empty).StartsWith("PAY_", StringComparison.Ordinal)
                        || (t.GetTableName() ?? string.Empty).StartsWith("INV_", StringComparison.Ordinal))
            .SelectMany(t => t.GetForeignKeys()
                .Where(fk => (fk.PrincipalEntityType.GetTableName() ?? string.Empty).StartsWith("LND_", StringComparison.Ordinal))
                .Select(fk => $"{t.GetTableName()}.{string.Join("+", fk.Properties.Select(p => p.Name))}"))
            .ToList();

        Assert.True(violaciones.Count == 0, "Tablas de Nómina o Comercial con FK a Cartera (FR-008, C34): " + string.Join(", ", violaciones));
    }

    private static List<string> MetodosDe(System.Type puerto) => puerto
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName)
        .Select(m => m.Name)
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// US3 (T0428, FR-046, T21): la recogida de créditos propios en el desembolso paga por el contrato (<c>IContratoDeObligaciones</c>, clase
    /// <c>OwnLoanPayoff</c>), igual que cualquier recaudador; no llama un servicio interno de pagos de Cartera.
    /// </summary>
    [Fact]
    public void La_recogida_del_desembolso_paga_por_el_contrato()
    {
        var raiz = RepoPath.FindRepoRoot();
        var recogida = Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending", "Disbursements", "RecogidaEnElDesembolso.cs");
        Assert.True(File.Exists(recogida), "No existe Application/Lending/Disbursements/RecogidaEnElDesembolso.cs (T0444).");
        Assert.Contains("IContratoDeObligaciones", FuenteSinComentarios.Leer(recogida), StringComparison.Ordinal);

        var interno = new Regex(@"\bIngenIA365ERP\.Application\.Lending\.Payments\b|\bAplicadorDePagos\b", RegexOptions.Compiled);
        var infractores = Directory.EnumerateFiles(Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending", "Disbursements"), "*.cs",
                SearchOption.AllDirectories)
            .Where(f => interno.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(raiz, f))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infractores, "El desembolso paga fuera del contrato de obligaciones (FR-046)");
    }
}
