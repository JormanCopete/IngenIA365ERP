using System.Text.RegularExpressions;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using IngenIA365ERP.Architecture.Tests.Helpers;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014, T037 (research R09, R29; FR-008, FR-086–FR-093; contrato-de-cuentas §1): Nómina, Recaudo y Tesorería tocan las cuentas de captación y de
/// aportes <b>sólo</b> por <c>IContratoDeCuentas</c> (y las consultas por <c>IConsultasDeCartera.ProductosDeCaptacionAsync</c> e
/// <c>IConsultaDeAportes</c>, todos en <c>Common.Integration.Lending</c>): ninguno nombra las entidades, el espacio de captaciones, el registro del libro
/// de cuentas ni las tablas <c>LND_</c> de cuentas, ni depende de esos tipos. Recaudo todavía no existe en el código: su carpeta está en la lista y la
/// regla la vigila desde que nazca. La regla de la 013 (<c>LaCarteraSoloSeTocaPorSusInterfaces</c>) ya cubre lo demás de Cartera.
/// </summary>
public class LasCuentasSeTocanSoloPorElContrato
{
    /// <summary>Carpetas de Nómina, Recaudo y Tesorería (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas =
    [
        "Core/IngenIA365ERP.Application/Payroll",
        "Core/IngenIA365ERP.Domain/Payroll",
        "Core/IngenIA365ERP.Domain/Entities/Payroll",
        "Presentation/IngenIA365ERP.API/Endpoints/Payroll",
        "Core/IngenIA365ERP.Application/Receipts",
        "Core/IngenIA365ERP.Domain/Entities/Receipts",
        "Core/IngenIA365ERP.Application/Treasury",
        "Core/IngenIA365ERP.Domain/Entities/Treasury",
        "Presentation/IngenIA365ERP.API/Endpoints/Treasury",
    ];

    private static readonly (Regex Patron, string Que)[] Prohibidos =
    [
        (new Regex(@"IngenIA365ERP\.(Domain\.Entities|Domain|Application)\.Lending\.Deposits\b", RegexOptions.Compiled), "depende del espacio de captaciones"),
        (new Regex(@"\b(MemberAccount\w*|AccountPayoutOrder|RegistroDeCuentas|MotorDeCaptaciones|ReconstruccionDeCuenta)\b", RegexOptions.Compiled),
            "nombra una entidad o un escritor de las cuentas"),
        (new Regex(@"""LND_(MemberAccount|AccountInstallment|AccountPayoutOrder|AccountHold|AccountCharge|Garnishment|InterestSettlement)\w*""", RegexOptions.Compiled),
            "nombra una tabla de cuentas"),
        (new Regex(@"\bdb\.(MemberAccount\w*|AccountPayoutOrders|AccountInstallments)\b", RegexOptions.Compiled), "lee o escribe las cuentas por el contexto"),
    ];

    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Arquitectura = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build());

    [Fact]
    public void Nomina_Recaudo_y_Tesoreria_no_nombran_las_cuentas_salvo_por_el_contrato()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        Assert.True(archivos.Count > 0, "No se encontraron los archivos de Nómina ni de Tesorería: ¿cambió la estructura de carpetas?");
        var infracciones = ReglaSobreFuente.Infracciones(archivos, Prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Nómina, Recaudo o Tesorería tocan las cuentas fuera de IContratoDeCuentas (R09, FR-008)");
    }

    [Fact]
    public void Los_tipos_de_Nomina_y_Tesoreria_no_dependen_de_las_captaciones()
    {
        var captaciones = Types().That().ResideInNamespaceMatching(@"^IngenIA365ERP\.(Domain(\.Entities)?|Application)\.Lending\.Deposits(\..+)?$");
        foreach (var ns in new[] { "IngenIA365ERP.Application.Payroll", "IngenIA365ERP.Domain.Payroll", "IngenIA365ERP.Domain.Entities.Payroll",
                     "IngenIA365ERP.Application.Treasury", "IngenIA365ERP.Domain.Entities.Treasury" })
            Types().That().ResideInNamespaceMatching($"^{Regex.Escape(ns)}(\\..+)?$")
                .Should().NotDependOnAny(captaciones)
                .Because($"R09, FR-008: {ns} toca las cuentas sólo por IContratoDeCuentas")
                .Check(Arquitectura.Value);
    }

    /// <summary>El contrato vive en el espacio de los puertos, que es lo único de Cartera que esos módulos pueden nombrar.</summary>
    [Fact]
    public void El_contrato_de_cuentas_es_un_puerto_de_Common_Integration_Lending()
    {
        Assert.Equal("IngenIA365ERP.Application.Common.Integration.Lending", typeof(IngenIA365ERP.Application.Common.Integration.Lending.IContratoDeCuentas).Namespace);
        Assert.True(typeof(IngenIA365ERP.Application.Common.Integration.Lending.IContratoDeCuentas).IsInterface);
    }
}
