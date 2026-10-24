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
///
/// <para>Feature 014 (T043; research R29; contrato-de-cuentas §1): <b>seis</b> puertos ofrecidos —<c>IConsultasDeCartera</c> (con su tercer método,
/// <c>ProductosDeCaptacionAsync</c>), <c>IReservasDeCartera</c>, <c>IConsultaDeObligaciones</c>, <c>IContratoDeObligaciones</c>,
/// <c>IDimensionesDeCartera</c> e <c>IContratoDeCuentas</c>— más <c>IConsultaDeAportes</c>, que se mudó de <c>Common.Integration.Savings</c> y pasó de
/// consumido a ofrecido. Sin excepciones por nombre. Las captaciones (<c>Application/Lending/Deposits</c>) entran a las carpetas de Cartera.</para>
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
        // US7 (T0608): el consumidor contable de Cartera y lo común del destino Accounting conocen a Cartera sólo por IDimensionesDeCartera
        // y por la bandeja (el contenido de los mensajes, en Common.Integration.Contracts.Lending).
        "Core/IngenIA365ERP.Application/Accounting/Lending",
        "Core/IngenIA365ERP.Application/Accounting/Integration",
        // Feature 015, US3 (T0107): Recaudo conoce a Cartera y Captaciones sólo por IContratoDeObligaciones e IContratoDeCuentas.
        "Core/IngenIA365ERP.Application/Receipts",
        "Core/IngenIA365ERP.Domain/Entities/Receipts",
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
        // Feature 014 (T043): las captaciones y aportes. Nómina, Recaudo y Tesorería llegan sólo por IContratoDeCuentas; Contabilidad, por la bandeja.
        "Core/IngenIA365ERP.Application/Lending/Deposits",
        "Core/IngenIA365ERP.Domain/Lending/Deposits",
        "Core/IngenIA365ERP.Domain/Entities/Lending/Deposits",
    ];

    /// <summary>Espacios de otros módulos que el ArchUnit revisa (los de <see cref="CarpetasDeOtrosModulos"/>).</summary>
    private static readonly string[] EspaciosDeOtrosModulos =
    [
        "IngenIA365ERP.Application.Payroll",
        "IngenIA365ERP.Domain.Entities.Payroll",
        "IngenIA365ERP.Domain.Payroll",
        "IngenIA365ERP.Application.Core.People",
        "IngenIA365ERP.Application.Inventory",
        // Feature 013 (T0185) vigilaba aquí lo conservado de ahorros y aportes (Application.Savings, Domain.Entities.Savings); la
        // feature 014 lo retiró (T007, R28) y las captaciones son Cartera (Lending.Deposits).
        "IngenIA365ERP.Domain.Entities.Inventory",
        "IngenIA365ERP.Domain.Inventory",
        // Feature 015, US3 (T0107).
        "IngenIA365ERP.Application.Receipts",
        "IngenIA365ERP.Domain.Entities.Receipts",
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

    /// <summary>
    /// Todo espacio <c>*.Lending*</c> salvo los puertos de <c>Application.Common.Integration.Lending</c> y los contratos de mensajes, y salvo el
    /// consumidor contable de Cartera y sus tablas (<c>Application.Accounting.Lending</c>, <c>Domain.Entities.Accounting.Lending</c>, US7), que
    /// son de Contabilidad aunque se llamen así.
    /// </summary>
    private const string LendingSalvoPuertos =
        @"^IngenIA365ERP\.(?!Application\.Common\.Integration(\.|$))(?!(Application|Domain(\.Entities)?)\.Accounting(\.|$))([\w.]+\.)?Lending(\..+)?$";

    [Fact]
    public void Ningun_otro_modulo_toca_Cartera_salvo_por_sus_puertos()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(CarpetasDeOtrosModulos), HaciaCartera);
        ReglaSobreFuente.SinInfracciones(infracciones, "Otro módulo toca Cartera fuera de Common.Integration.Lending (FR-008, C34)");
    }

    [Fact]
    public void Los_tipos_de_otros_modulos_no_dependen_de_Cartera_salvo_por_sus_puertos()
    {
        // La exención por nombre de SavingsAccount se retiró con la mudanza a Domain.Entities.Savings (T0184); la 014 retiró la entidad.
        var cartera = Types().That().ResideInNamespaceMatching(LendingSalvoPuertos);
        foreach (var ns in EspaciosDeOtrosModulos)
            Types().That().ResideInNamespaceMatching(ConHijos(ns))
                .Should().NotDependOnAny(cartera)
                .Because($"FR-008, T90: {ns} sólo conoce Cartera por Application.Common.Integration.Lending")
                .Check(Arquitectura.Value);
    }

    /// <summary>
    /// US7 (T0608): el consumidor contable de Cartera y lo común del destino <c>Accounting</c> conocen a Cartera sólo por
    /// <c>IDimensionesDeCartera</c> y por el contenido de los mensajes (<c>Application.Common.Integration</c>). Los nombres de los conceptos
    /// (<c>Domain.Enums.Lending.LoanConcept</c>) son el vocabulario de esos mensajes y se admiten; las entidades y la aplicación de Cartera, no.
    /// </summary>
    [Fact]
    public void El_lado_contable_no_depende_de_Cartera_salvo_por_su_puerto_y_los_mensajes()
    {
        const string carteraSalvoPuertosYVocabulario =
            @"^IngenIA365ERP\.(?!Application\.Common\.Integration(\.|$))(?!(Application|Domain(\.Entities)?)\.Accounting(\.|$))(?!Domain\.Enums\.Lending(\.|$))([\w.]+\.)?Lending(\..+)?$";
        var cartera = Types().That().ResideInNamespaceMatching(carteraSalvoPuertosYVocabulario);
        foreach (var ns in new[] { "IngenIA365ERP.Application.Accounting.Lending", "IngenIA365ERP.Application.Accounting.Integration" })
            Types().That().ResideInNamespaceMatching(ConHijos(ns))
                .Should().NotDependOnAny(cartera)
                .Because($"T0608, C27: {ns} conoce a Cartera por IDimensionesDeCartera y por los mensajes")
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
        // T90, R117; US7 (T0608, enmienda A1, C27): IDimensionesDeCartera, el que Contabilidad usa para la matriz y la completitud.
        // Feature 014 (T043; FR-095): el tercer método de las consultas.
        Assert.Equal(["EstadoCrediticioAsync", "EstadoDeValidacionAsync", "ProductosDeCaptacionAsync"], MetodosDe(typeof(IConsultasDeCartera)));
        Assert.Equal(["CatalogoAsync", "CombinacionesEnUsoAsync"], MetodosDe(typeof(IDimensionesDeCartera)));
        Assert.Equal(["LiberarAsync", "ReservarAsync"], MetodosDe(typeof(IReservasDeCartera)));
        Assert.Equal(["CuotasDeNominaAsync", "ObligacionesAsync"], MetodosDe(typeof(IConsultaDeObligaciones)));
        Assert.Equal(["AplicarAsync", "ListarAsync", "ResolverSaldoAFavorAsync", "ReversarAsync", "SimularAsync"], MetodosDe(typeof(IContratoDeObligaciones)));
        // Feature 015, R1 (E-01 a, E-02 a; contratos-v2.md §1, §3.1): las v2 conviven con las v1.
        Assert.Equal(2, IContratoDeObligaciones.VersionDelContrato);
        // Feature 014 (T039, T041; R09, R20): el contrato de cuentas con sus siete métodos y la consulta de aportes en su versión 2.
        Assert.Equal(["ConsignarAsync", "ConsultarAsync", "ListarExigiblesAsync", "RetirarAsync", "ReversarAsync", "SimularAsync", "TrasladarAsync"],
            MetodosDe(typeof(IContratoDeCuentas)));
        Assert.Equal(2, IContratoDeCuentas.VersionDelContrato);
        Assert.Equal([1, 2], IngenIA365ERP.Application.Common.Integration.ContratosDeIntegracion.VersionesDelContratoDeObligaciones);
        Assert.Equal([1, 2], IngenIA365ERP.Application.Common.Integration.ContratosDeIntegracion.VersionesDelContratoDeCuentas);
        // Feature 015, R1 (E-01 b; contratos-v2.md §2.1): la consulta de la carga aplicada sin activar.
        Assert.Equal(["PersonaAsync"], MetodosDe(typeof(IConsultaDeCargaSinActivar)));
        Assert.Equal(["SaldosAsync"], MetodosDe(typeof(IConsultaDeAportes)));
        Assert.Equal(2, IConsultaDeAportes.VersionDelContrato);
        foreach (var puerto in PuertosOfrecidos)
            Assert.Equal("IngenIA365ERP.Application.Common.Integration.Lending", puerto.Namespace);
        // Feature 015 (decisión del dueño del 2026-10-24, T0113): la búsqueda de una obligación por su número, de sólo lectura.
        Assert.Equal(["PorNumeroAsync"], MetodosDe(typeof(IBusquedaDeObligaciones)));
        // Feature 015, R1 (T0400): el débito automático del día, de sólo lectura.
        Assert.Equal(["PorDiaAsync"], MetodosDe(typeof(IDebitoAutomaticoParaConsolidado)));
        Assert.Equal(10, PuertosOfrecidos.Length);
    }

    [Fact]
    public void Fuera_de_Cartera_solo_los_provisionales_del_mismo_espacio_implementan_los_puertos()
    {
        // Los dobles viven en tests/ (otro ensamblado). Las implementaciones reales vivirán en Application.Lending.Integration (US5, US6).
        var puertos = PuertosOfrecidos;
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

    /// <summary>
    /// US8 (bloque 13C, T0728; C34): la migración y la activación no leen <c>PAY_</c> ni <c>INV_</c>. El aviso de <c>DESC_CARTERA</c> lo pregunta la
    /// pantalla a Nómina (<c>GET /api/payroll/concept-definitions/DESC_CARTERA/accounts</c>); las ventas acumuladas llegan por la bandeja, sin
    /// consumirlas. Ningún tipo de <c>Application.Lending.Activation</c> ni <c>.Migration</c> usa entidades de Nómina o de Comercial, y ningún archivo
    /// de esas carpetas consulta sus conjuntos.
    /// </summary>
    [Fact]
    public void La_activacion_no_lee_Nomina_ni_Comercial()
    {
        const BindingFlags Todo = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        string[] espacios = ["IngenIA365ERP.Application.Lending.Activation", "IngenIA365ERP.Application.Lending.Migration"];
        var infractores = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => espacios.Any(e => (t.Namespace ?? string.Empty).StartsWith(e, StringComparison.Ordinal)))
            .SelectMany(t => t.GetFields(Todo).Select(f => f.FieldType)
                .Concat(t.GetMethods(Todo).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)))
                .Where(x => (x.Namespace ?? string.Empty).StartsWith("IngenIA365ERP.Domain.Entities.Payroll", StringComparison.Ordinal)
                            || (x.Namespace ?? string.Empty).StartsWith("IngenIA365ERP.Domain.Entities.Inventory", StringComparison.Ordinal))
                .Select(x => $"{t.FullName} → {x.Name}"))
            .Distinct()
            .ToList();
        Assert.True(infractores.Count == 0, "La activación usa entidades de Nómina o de Comercial (C34): " + string.Join(", ", infractores));

        var carpetas = new[] { "Core/IngenIA365ERP.Application/Lending/Activation", "Core/IngenIA365ERP.Application/Lending/Migration" };
        var fuentes = ReglaSobreFuente.ArchivosEn(carpetas)
            .Where(a => Regex.IsMatch(FuenteSinComentarios.Leer(a),
                @"\bdb\.(Employees|Payroll\w*|PayPeriods|ConceptDefinition\w*|DocumentPayments|InventoryDocuments|LendingObligationResponses|DocumentLines)\b"))
            .ToList();
        Assert.True(fuentes.Count == 0, "La activación lee tablas PAY_ o INV_: " + string.Join(", ", fuentes));
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

    /// <summary>
    /// US7 (T0608, T0657): la implementación real de <c>IDimensionesDeCartera</c> es de Cartera (<c>Application/Lending/Integration</c>) y
    /// <c>DependencyInjection</c> la registra.
    /// </summary>
    [Fact]
    public void Las_dimensiones_que_Contabilidad_pregunta_las_da_Cartera()
    {
        var dimensiones = typeof(IngenIA365ERP.Application.Lending.Integration.DimensionesDeCartera);
        Assert.True(typeof(IDimensionesDeCartera).IsAssignableFrom(dimensiones));
        Assert.Equal("IngenIA365ERP.Application.Lending.Integration", dimensiones.Namespace);
        var di = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "DependencyInjection.cs"));
        Assert.Matches(@"IDimensionesDeCartera,\s*[\w.]*DimensionesDeCartera>", di);
    }

    /// <summary>Los seis puertos que la Cartera ofrece y la consulta de aportes (feature 014, T043).</summary>
    private static readonly System.Type[] PuertosOfrecidos =
    [
        typeof(IConsultasDeCartera), typeof(IReservasDeCartera), typeof(IConsultaDeObligaciones), typeof(IContratoDeObligaciones),
        typeof(IDimensionesDeCartera), typeof(IContratoDeCuentas), typeof(IConsultaDeAportes),
        // Feature 015 (decisión del dueño del 2026-10-24, T0113): la búsqueda por número que usa la caja de Recaudo.
        typeof(IBusquedaDeObligaciones),
        // Feature 015, R1 (E-01 b, T0350): la consulta de la carga aplicada sin activar, para el recaudo pendiente de aplicar.
        typeof(IConsultaDeCargaSinActivar),
        // Feature 015, R1 (T0400; D26): la lectura del débito automático para el consolidado de Recaudo.
        typeof(IDebitoAutomaticoParaConsolidado),
    ];

    /// <summary>
    /// Feature 014 (T040, T041, T083, T084; contrato-de-cuentas §3.13, §4; research E33): <c>DependencyInjection</c> registra el contrato de cuentas y la
    /// consulta de aportes <b>reales</b> de la Cartera, que responden «no habilitado» / «no disponible» por naturaleza mientras no esté activada (las
    /// implementaciones «sin activar» y «no disponible» siguen existiendo para las pruebas); la carpeta <c>Common/Integration/Savings</c> ya no existe.
    /// </summary>
    [Fact]
    public void El_contrato_de_cuentas_y_la_consulta_de_aportes_son_los_reales_de_la_Cartera()
    {
        var sinActivar = typeof(IngenIA365ERP.Application.Lending.Integration.ContratoDeCuentasSinActivar);
        Assert.True(typeof(IContratoDeCuentas).IsAssignableFrom(sinActivar));
        Assert.True(typeof(IConsultaDeAportes).IsAssignableFrom(typeof(ConsultaDeAportesNoDisponible)));
        var raiz = RepoPath.FindRepoRoot();
        var di = FuenteSinComentarios.Leer(Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "DependencyInjection.cs"));
        Assert.Matches(@"IContratoDeCuentas>\(sp\s*=>\s*sp\.GetRequiredService<[\w.]*ContratoDeCuentasDeCartera>\(\)\)", di);
        Assert.Matches(@"IConsultaDeAportes,\s*[\w.]*ConsultaDeAportesDeCartera>", di);
        Assert.True(typeof(IContratoDeCuentas).IsAssignableFrom(typeof(IngenIA365ERP.Application.Lending.Integration.ContratoDeCuentasDeCartera)));
        Assert.True(typeof(IConsultaDeAportes).IsAssignableFrom(typeof(IngenIA365ERP.Application.Lending.Integration.ConsultaDeAportesDeCartera)));
        var savings = Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Common", "Integration", "Savings");
        Assert.False(Directory.Exists(savings) && Directory.EnumerateFiles(savings, "*.cs", SearchOption.AllDirectories).Any(),
            "IConsultaDeAportes se mudó a Common/Integration/Lending (R20).");
        Assert.DoesNotContain(typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes(),
            t => (t.Namespace ?? string.Empty).StartsWith("IngenIA365ERP.Application.Common.Integration.Savings", StringComparison.Ordinal));
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
