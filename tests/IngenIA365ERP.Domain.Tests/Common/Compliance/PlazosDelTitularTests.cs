using FluentAssertions;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;

namespace IngenIA365ERP.Domain.Tests.Common.Compliance;

/// <summary>
/// Feature 013, T0020 (T69, S10; data-model parte 3 §4.9, §12.5; SC-012): los plazos de las solicitudes del titular en
/// días hábiles nacionales. Los días llegan como límites legales (<c>TITULAR_CONSULTA_DIAS</c>,
/// <c>TITULAR_CONSULTA_PRORROGA_DIAS</c>, <c>TITULAR_RECLAMO_DIAS</c>, <c>TITULAR_RECLAMO_PRORROGA_DIAS</c>), nunca están
/// en el código; la consulta usa el de consulta y las demás clases el de reclamo. La prórroga se concede una sola vez.
/// Calendario de lunes a viernes con los festivos de la Ley 51 (el lunes 2 y el lunes 16 de noviembre de 2026 lo son).
/// </summary>
public class PlazosDelTitularTests
{
    private static readonly LimitesDelTitular Limites = new(ConsultaDias: 10, ConsultaProrrogaDias: 5, ReclamoDias: 15, ReclamoProrrogaDias: 8);

    private static readonly CalendarioHabil Calendario = new(
        FestivosLey51.DelAño(2026).Concat(FestivosLey51.DelAño(2027)).Select(f => f.Fecha),
        [DayOfWeek.Saturday, DayOfWeek.Sunday]);

    private static readonly DateOnly ViernesTreinta = new(2026, 10, 30);

    [Fact]
    public void La_consulta_vence_a_los_dias_habiles_de_consulta()
    {
        // Del 3 al 6 (4), del 9 al 13 (9) y el 17 (10): el lunes 2 y el lunes 16 son festivos.
        PlazosDelTitular.Vencimiento(DataSubjectRequestKind.Query, ViernesTreinta, Limites, Calendario)
            .Should().Be(new DateOnly(2026, 11, 17));
    }

    [Theory]
    [InlineData(DataSubjectRequestKind.Update)]
    [InlineData(DataSubjectRequestKind.Rectification)]
    [InlineData(DataSubjectRequestKind.Suppression)]
    [InlineData(DataSubjectRequestKind.Revocation)]
    public void Las_demas_clases_vencen_con_el_plazo_de_reclamo(DataSubjectRequestKind clase)
    {
        PlazosDelTitular.Vencimiento(clase, ViernesTreinta, Limites, Calendario).Should().Be(new DateOnly(2026, 11, 24));
    }

    [Fact]
    public void La_prorroga_de_una_consulta_suma_sus_dias_habiles_al_vencimiento()
    {
        PlazosDelTitular.Prorroga(DataSubjectRequestKind.Query, new DateOnly(2026, 11, 17), yaProrrogada: false, Limites, Calendario)
            .Should().Be(new DateOnly(2026, 11, 24));
    }

    [Fact]
    public void La_prorroga_de_un_reclamo_suma_sus_dias_habiles_al_vencimiento()
    {
        PlazosDelTitular.Prorroga(DataSubjectRequestKind.Revocation, new DateOnly(2026, 11, 24), yaProrrogada: false, Limites, Calendario)
            .Should().Be(new DateOnly(2026, 12, 4));
    }

    [Fact]
    public void La_prorroga_se_concede_una_sola_vez()
    {
        var acto = () => PlazosDelTitular.Prorroga(DataSubjectRequestKind.Query, new DateOnly(2026, 11, 24), yaProrrogada: true, Limites, Calendario);
        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Recibida_en_un_dia_no_habil_cuenta_desde_el_siguiente_habil()
    {
        // Sábado 31: el primer día que cuenta es el martes 3, igual que si hubiera llegado el viernes.
        PlazosDelTitular.Vencimiento(DataSubjectRequestKind.Query, new DateOnly(2026, 10, 31), Limites, Calendario)
            .Should().Be(new DateOnly(2026, 11, 17));
    }

    [Fact]
    public void Un_limite_sin_dias_se_rechaza()
    {
        var acto = () => PlazosDelTitular.Vencimiento(DataSubjectRequestKind.Query, ViernesTreinta, Limites with { ConsultaDias = 0 }, Calendario);
        acto.Should().Throw<ArgumentOutOfRangeException>();
    }
}
