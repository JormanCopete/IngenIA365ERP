using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-008 (DT §2.25, T90, C27, C34): Cartera no lee ni escribe datos de otro módulo, ni otro módulo los
/// suyos, salvo por la lista cerrada de interfaces (los cinco puertos de <c>Application.Common.Integration.Lending</c>
/// y los que Cartera consume). Vigila las dos direcciones.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0001) y la llena la fase Fundacional B: los módulos que hoy tocan la cartera heredada (Nómina, Personas, Comercial, Contabilidad) entran a <see cref="CarpetasDeOtrosModulos"/> al pasar a los puertos, y las carpetas de Cartera nueva a <see cref="CarpetasDeCartera"/> al nacer; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LaCarteraSoloSeTocaPorSusInterfaces
{
    /// <summary>Carpetas de otros módulos (relativas a <c>src/</c>) que no pueden nombrar a Cartera salvo por sus puertos.</summary>
    private static readonly string[] CarpetasDeOtrosModulos = [];

    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>) que no pueden nombrar a otros módulos salvo por sus puertos.</summary>
    private static readonly string[] CarpetasDeCartera = [];

    private static readonly (Regex Patron, string Que)[] HaciaCartera =
    [
        (new Regex(@"IngenIA365ERP\.(Domain\.Entities|Application)\.Lending\b", RegexOptions.Compiled), "depende de Cartera fuera de Common.Integration.Lending"),
        (new Regex("\"LND_", RegexOptions.Compiled), "nombra una tabla LND_"),
    ];

    private static readonly (Regex Patron, string Que)[] DesdeCartera =
    [
        (new Regex(@"IngenIA365ERP\.Application\.(Inventory|Payroll|Accounting|Core\.People)\b", RegexOptions.Compiled), "depende de otro módulo fuera de sus puertos"),
        (new Regex(@"IngenIA365ERP\.Domain\.Entities\.(Inventory|Payroll|Accounting)\b", RegexOptions.Compiled), "usa entidades de otro módulo"),
        (new Regex("\"(INV|PAY|ACC)_", RegexOptions.Compiled), "nombra una tabla de otro módulo"),
    ];

    [Fact]
    public void Ningun_otro_modulo_toca_Cartera_salvo_por_sus_puertos()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(CarpetasDeOtrosModulos), HaciaCartera);
        ReglaSobreFuente.SinInfracciones(infracciones, "Otro módulo toca Cartera fuera de Common.Integration.Lending (FR-008, C34)");
    }

    [Fact]
    public void Cartera_no_toca_otros_modulos_salvo_por_los_puertos_que_consume()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(CarpetasDeCartera), DesdeCartera);
        ReglaSobreFuente.SinInfracciones(infracciones, "Cartera toca otro módulo fuera de sus puertos (FR-008, C34)");
    }
}
