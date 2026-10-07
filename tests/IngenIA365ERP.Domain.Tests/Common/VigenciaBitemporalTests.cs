using FluentAssertions;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Tests.Common;

/// <summary>
/// Feature 013, T0018 (T38, R48; quickstart §1.4; SC-001): la regla única de lectura de los valores nacionales a la fecha
/// D con la información conocida hasta R. Entran las versiones conocidas en R que cubren D y que ninguna corrección
/// conocida en R reemplazó; gana el <c>ValidFrom</c> más reciente y, si empatan, la <c>Sequence</c> mayor. El
/// <c>ValidTo</c> implícito sale del sucesor conocido en R; el explícito (usura del período, festivo) se respeta.
/// </summary>
public class VigenciaBitemporalTests
{
    private sealed record Version(
        string Nombre,
        DateOnly ValidFrom,
        DateOnly? ValidTo,
        long Sequence,
        DateTime ConocidaEn,
        Guid PublicId,
        Guid? SupersedesPublicId = null) : IVersionBitemporal;

    private static readonly DateTime Enero = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Marzo = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Mayo = new(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Hoy = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static Version V(string nombre, string desde, string? hasta, long seq, DateTime conocida, Guid? reemplaza = null) =>
        new(nombre, DateOnly.Parse(desde), hasta is null ? null : DateOnly.Parse(hasta), seq, conocida, Guid.NewGuid(), reemplaza);

    [Fact]
    public void Toma_la_version_conocida_en_R_que_cubre_D()
    {
        var v2025 = V("2025", "2025-01-01", null, 1, Enero.AddYears(-1));
        var v2026 = V("2026", "2026-01-01", null, 2, Enero);

        VigenciaBitemporal.Vigente([v2025, v2026], new DateOnly(2026, 2, 1), Hoy)!.Nombre.Should().Be("2026");
        VigenciaBitemporal.Vigente([v2025, v2026], new DateOnly(2025, 6, 1), Hoy)!.Nombre.Should().Be("2025",
            "el sucesor conocido en R cierra la vigencia de 2025 la víspera de su ValidFrom");
    }

    [Fact]
    public void Una_version_conocida_despues_de_R_no_cuenta()
    {
        var v2025 = V("2025", "2025-01-01", null, 1, Enero.AddYears(-1));
        var v2026 = V("2026", "2026-01-01", null, 2, Marzo);

        VigenciaBitemporal.Vigente([v2025, v2026], new DateOnly(2026, 2, 1), Enero)!.Nombre.Should().Be("2025",
            "en enero todavía no se sabía de la versión 2026: la de 2025 seguía abierta");
    }

    [Fact]
    public void Una_correccion_conocida_despues_de_R_no_reemplaza_al_original()
    {
        var original = V("original", "2026-03-01", "2026-03-31", 10, Marzo);
        var correccion = V("corrección", "2026-03-01", "2026-03-31", 11, Mayo, original.PublicId);
        var marzo10 = new DateOnly(2026, 3, 10);

        VigenciaBitemporal.Vigente([original, correccion], marzo10, Marzo.AddDays(1))!.Nombre.Should().Be("original");
        VigenciaBitemporal.Vigente([original, correccion], marzo10, Hoy)!.Nombre.Should().Be("corrección");
    }

    [Fact]
    public void La_correccion_reemplaza_aunque_cambie_la_fecha_desde()
    {
        var original = V("original", "2026-03-01", null, 10, Marzo);
        var correccion = V("corrección", "2026-03-05", null, 11, Mayo, original.PublicId);

        VigenciaBitemporal.Vigente([original, correccion], new DateOnly(2026, 3, 2), Hoy).Should().BeNull(
            "el original ya no cuenta y la corrección empieza el 5");
        VigenciaBitemporal.Vigente([original, correccion], new DateOnly(2026, 3, 6), Hoy)!.Nombre.Should().Be("corrección");
    }

    [Fact]
    public void La_correccion_de_una_correccion_reemplaza_a_la_correccion()
    {
        var original = V("original", "2026-01-01", null, 1, Enero);
        var primera = V("primera", "2026-01-01", null, 2, Marzo, original.PublicId);
        var segunda = V("segunda", "2026-01-01", null, 3, Mayo, primera.PublicId);
        var d = new DateOnly(2026, 2, 1);

        VigenciaBitemporal.Vigente([original, primera, segunda], d, Hoy)!.Nombre.Should().Be("segunda");
        VigenciaBitemporal.Vigente([original, primera, segunda], d, Marzo)!.Nombre.Should().Be("primera");
        VigenciaBitemporal.Vigente([original, primera, segunda], d, Enero)!.Nombre.Should().Be("original");
    }

    [Fact]
    public void Con_el_mismo_ValidFrom_gana_la_Sequence_mayor()
    {
        var a = V("a", "2026-01-01", null, 7, Enero);
        var b = V("b", "2026-01-01", null, 9, Enero);
        var c = V("c", "2026-01-01", null, 8, Enero);

        VigenciaBitemporal.Vigente([a, b, c], new DateOnly(2026, 6, 1), Hoy)!.Nombre.Should().Be("b");
    }

    [Fact]
    public void El_ValidTo_explicito_se_respeta_aunque_no_haya_sucesor()
    {
        var usuraOctubre = V("octubre", "2026-10-01", "2026-10-31", 20, Hoy.AddDays(-10));

        VigenciaBitemporal.Vigente([usuraOctubre], new DateOnly(2026, 10, 31), Hoy)!.Nombre.Should().Be("octubre");
        VigenciaBitemporal.Vigente([usuraOctubre], new DateOnly(2026, 11, 1), Hoy).Should().BeNull(
            "la usura certificada vence con su período: noviembre sin certificar es un faltante, no octubre");
    }

    [Fact]
    public void Un_festivo_cubre_solo_su_dia()
    {
        var festivo = V("Todos los Santos", "2026-11-02", "2026-11-02", 30, Enero);

        VigenciaBitemporal.Vigente([festivo], new DateOnly(2026, 11, 2), Hoy).Should().NotBeNull();
        VigenciaBitemporal.Vigente([festivo], new DateOnly(2026, 11, 1), Hoy).Should().BeNull();
        VigenciaBitemporal.Vigente([festivo], new DateOnly(2026, 11, 3), Hoy).Should().BeNull();
    }

    [Fact]
    public void Una_version_anterior_no_revive_si_el_sucesor_vencio_con_su_ValidTo()
    {
        var abierta = V("abierta", "2026-01-01", null, 1, Enero);
        var acotada = V("acotada", "2026-03-01", "2026-03-31", 2, Enero);

        VigenciaBitemporal.Vigente([abierta, acotada], new DateOnly(2026, 2, 15), Hoy)!.Nombre.Should().Be("abierta");
        VigenciaBitemporal.Vigente([abierta, acotada], new DateOnly(2026, 4, 15), Hoy).Should().BeNull(
            "el sucesor conocido cierra la abierta la víspera del 1 de marzo, y el sucesor vence el 31");
    }

    [Fact]
    public void Sin_version_que_cubra_D_devuelve_nulo()
    {
        VigenciaBitemporal.Vigente(Array.Empty<Version>(), new DateOnly(2026, 1, 1), Hoy).Should().BeNull();
        var futura = V("futura", "2027-01-01", null, 1, Enero);
        VigenciaBitemporal.Vigente([futura], new DateOnly(2026, 12, 31), Hoy).Should().BeNull();
    }

    [Fact]
    public void R_es_inclusivo()
    {
        var v = V("v", "2026-01-01", null, 1, Marzo);

        VigenciaBitemporal.Vigente([v], new DateOnly(2026, 6, 1), Marzo).Should().NotBeNull("conocida exactamente en R cuenta");
        VigenciaBitemporal.Vigente([v], new DateOnly(2026, 6, 1), Marzo.AddTicks(-1)).Should().BeNull();
    }

    // ------------------------------------------------- versiones propias de Cartera (feature 013, T0131; data-model §0.6) --

    private sealed record VersionDeCartera(string Nombre, DateOnly ValidFrom, int VersionNumber, DateTime RegisteredAt) : IVersionDeCartera;

    private static VersionDeCartera C(string nombre, string desde, int numero, DateTime registrada) =>
        new(nombre, DateOnly.Parse(desde), numero, registrada);

    [Fact]
    public void Cartera_toma_el_ValidFrom_mas_reciente_registrado_hasta_R()
    {
        var v1 = C("v1", "2026-01-01", 1, Enero);
        var v2 = C("v2", "2026-04-01", 2, Marzo);

        VigenciaBitemporal.VigenteDeCartera([v1, v2], new DateOnly(2026, 3, 31), Hoy)!.Nombre.Should().Be("v1",
            "sin ValidTo guardado, v1 rige hasta la víspera del ValidFrom de v2");
        VigenciaBitemporal.VigenteDeCartera([v1, v2], new DateOnly(2026, 4, 1), Hoy)!.Nombre.Should().Be("v2");
    }

    [Fact]
    public void Cartera_desempata_el_mismo_ValidFrom_por_el_VersionNumber_mayor()
    {
        var primera = C("primera", "2026-05-01", 3, Marzo);
        var segunda = C("segunda", "2026-05-01", 4, Mayo);

        VigenciaBitemporal.VigenteDeCartera([segunda, primera], new DateOnly(2026, 6, 1), Hoy)!.Nombre.Should().Be("segunda");
        VigenciaBitemporal.VigenteDeCartera([segunda, primera], new DateOnly(2026, 6, 1), Marzo)!.Nombre.Should().Be("primera",
            "en marzo todavía no se había registrado la versión 4");
    }

    [Fact]
    public void Cartera_no_cuenta_una_version_registrada_despues_de_R_aunque_su_ValidFrom_sea_anterior()
    {
        var vieja = C("vieja", "2026-01-01", 1, Enero);
        var retroactiva = C("retroactiva", "2026-02-01", 2, Mayo);

        VigenciaBitemporal.VigenteDeCartera([vieja, retroactiva], new DateOnly(2026, 3, 1), Marzo)!.Nombre.Should().Be("vieja");
        VigenciaBitemporal.VigenteDeCartera([vieja, retroactiva], new DateOnly(2026, 3, 1), Hoy)!.Nombre.Should().Be("retroactiva");
    }

    [Fact]
    public void Cartera_sin_version_que_cumpla_devuelve_nulo_y_R_es_inclusivo()
    {
        var v = C("v", "2026-07-01", 1, Marzo);

        VigenciaBitemporal.VigenteDeCartera([v], new DateOnly(2026, 6, 30), Hoy).Should().BeNull("todavía no rige");
        VigenciaBitemporal.VigenteDeCartera([v], new DateOnly(2026, 7, 1), Marzo).Should().NotBeNull("registrada exactamente en R cuenta");
        VigenciaBitemporal.VigenteDeCartera([v], new DateOnly(2026, 7, 1), Marzo.AddTicks(-1)).Should().BeNull();
        VigenciaBitemporal.VigenteDeCartera(Array.Empty<VersionDeCartera>(), new DateOnly(2026, 7, 1), Hoy).Should().BeNull();
    }
}
