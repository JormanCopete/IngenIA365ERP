using ArchUnitNET.Loader;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0001 (R-22, R-37; FR-004; contracts/interfaces.md §3; contracts/contabilidad.md §1): la Tesorería nueva nunca conoce una
/// cuenta ni escribe asientos. El vínculo de la cuenta bancaria propia con su auxiliar vive en Contabilidad (<c>ACC_OwnBankAccountLinks</c>)
/// y Tesorería lo pide por <c>IContabilidadParaTesoreria</c>; lo que cruza, cruza por hechos <c>TES → Accounting</c>. Molde:
/// <see cref="CarteraNoConoceContabilidad"/>; las comprobaciones son las de <see cref="ReglaContable"/>.
///
/// <para>
/// Árbol que recorre: <see cref="RecaudoYTesoreria.NamespacesDeTesoreria"/> y <see cref="RecaudoYTesoreria.CarpetasDeTesoreria"/>, <b>sin lo
/// heredado</b> (la raíz de <c>Domain/Entities/Treasury</c> y <c>Application/Treasury/{Checks, Invoices, TreasuryConcepts}</c>, R-04), que
/// sigue como está hasta su retiro. Las listas nacen vacías en el Setup: <see cref="Puertos"/> la llena la fase 2 (T0041) con el puerto y
/// sus ocho métodos, y <see cref="ConsumidoresContables"/> la fase 16 (US8) con <c>Application.Accounting.Treasury</c>.
/// </para>
/// </summary>
public class TesoreriaNoConoceContabilidad
{
    private const string Prohibido = @"^IngenIA365ERP\.(?!Application\.Common\.Integration\.Accounting(\.|$))([\w.]+\.)?Accounting(\..+)?$";

    /// <summary>Los puertos contables de Tesorería y exactamente sus métodos.</summary>
    private static readonly (string Tipo, string[] Metodos)[] Puertos =
    [
        // Fase 2 (T0041; interfaces.md §3).
        ("IngenIA365ERP.Application.Common.Integration.Accounting.IContabilidadParaTesoreria",
            ["CuentasBancariasDelPlanAsync", "CuentasPorPagarAsync", "EvaluarAsync", "MovimientosDeCuentaBancariaAsync", "SaldoContableAsync",
             "SaldoEnLibrosAsync", "VincularCuentaBancariaAsync", "VinculoAsync"]),
    ];

    /// <summary>Los espacios del consumidor contable de Tesorería.</summary>
    private static readonly string[] ConsumidoresContables =
    [
    ];

    /// <summary>Los espacios de la Tesorería nueva cuya dependencia mira la inversa (lo heredado no viaja en mensajes).</summary>
    private static readonly string[] EspaciosNuevos = ["IngenIA365ERP.Domain.Treasury", "IngenIA365ERP.Domain.Entities.Treasury.Activation"];

    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Arquitectura = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build());

    [Fact]
    public void Tesoreria_no_depende_de_Contabilidad_salvo_su_puerto() =>
        ReglaContable.SinDependencia(Arquitectura.Value, RecaudoYTesoreria.NamespacesDeTesoreria, RecaudoYTesoreria.EsDeTesoreria, Prohibido,
            "FR-004, R-22: Tesorería pregunta a Contabilidad sólo por IContabilidadParaTesoreria y le avisa por hechos");

    [Fact]
    public void Tesoreria_no_escribe_asientos_ni_conoce_cuentas() =>
        ReglaContable.SinNombresContables(RecaudoYTesoreria.ArchivosDeTesoreria(), "Tesorería conoce Contabilidad fuera de su puerto (FR-004, E-04)");

    [Fact]
    public void Cada_puerto_contable_declara_exactamente_sus_metodos() =>
        ReglaContable.MetodosExactos(Puertos);

    [Fact]
    public void El_consumidor_contable_no_depende_de_Tesoreria() =>
        ReglaContable.ConsumidorSinDependencia(Arquitectura.Value, ConsumidoresContables, EspaciosNuevos,
            "R-22: Contabilidad lee lo que Tesorería manda en los mensajes y le pregunta por IDimensionesDeTesoreria, no sus tablas ni su código");
}
