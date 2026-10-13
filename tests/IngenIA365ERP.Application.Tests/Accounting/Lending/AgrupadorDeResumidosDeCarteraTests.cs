using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0617 (T0648; contracts/contabilidad.md §10.3 y §10.4; C07, C09): cómo se consumen las entregas de Cartera de una pasada.
/// Resumido <c>PorDia</c> = un grupo por fecha contable y <b>tipo de comprobante</b> (C09), con la sucursal en cada línea; <c>PorDiaYSucursal</c>
/// suma la sucursal a la clave; <c>PorHecho</c> (C07), los informativos y lo que no trae clave de Cartera van una unidad por trabajo.
/// </summary>
public class AgrupadorDeResumidosDeCarteraTests
{
    private static IReadOnlyList<MensajeEntrante> ConHorario(IReadOnlyList<MensajeEntrante> unidad, string? horario, Guid? lote = null) =>
        unidad.Select(m => m with { Entrega = m.Entrega with { ScheduleKey = horario, Status = DeliveryStatus.InBatch, BatchPublicId = lote } }).ToList();

    private static IReadOnlyList<MensajeEntrante> Parte(string caso, string granularidad = ClavesDeLote.GranularidadPorDia, Guid? origen = null)
    {
        var unidad = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe(caso), origen);
        return ConHorario(unidad, EscenarioContableDeCartera.Horario(unidad[0].Type, granularidad));
    }

    [Fact]
    public void Con_PorDia_la_causacion_del_dia_es_un_grupo_por_fecha_y_tipo_de_comprobante()
    {
        var entregas = new[] { Parte("03-causacion-del-dia"), Parte("25-mora-causada"), Parte("26-seguro-causado-con-la-aseguradora"), Parte("12-causacion-en-suspenso") }
            .SelectMany(u => u).ToList();

        var trabajos = AgrupadorDeResumidosDeCartera.Planear(entregas, IntegrationDestinations.Accounting);

        trabajos.Should().ContainSingle();
        trabajos[0].ClaveDeGrupo.Should().Be("2026-11-14|CA", "«fecha contable + tipo» es el tipo de comprobante (C09), no el del hecho");
        trabajos[0].Unidades.Should().HaveCount(4).And.OnlyContain(u => u.OriginModule == ModulosDeOrigen.Cartera);
    }

    [Fact]
    public void Con_PorDiaYSucursal_la_sucursal_entra_en_la_clave()
    {
        var principal = Parte("03-causacion-del-dia", ClavesDeLote.GranularidadPorDiaYSucursal);
        var norte = Parte("25-mora-causada", ClavesDeLote.GranularidadPorDiaYSucursal)
            .Select(m => m with { Envelope = m.Envelope with { BranchPublicId = Guid.Parse("c8e2f3a4-1b5c-4d6e-9f70-2b3c4d5e6f71") } }).ToList();

        var trabajos = AgrupadorDeResumidosDeCartera.Planear([.. principal, .. norte], IntegrationDestinations.Accounting);

        trabajos.Select(t => t.ClaveDeGrupo).Should().Equal(
            "2026-11-14|CA|b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f",
            "2026-11-14|CA|c8e2f3a4-1b5c-4d6e-9f70-2b3c4d5e6f71");
    }

    [Fact]
    public void Dias_y_tipos_distintos_son_grupos_distintos()
    {
        var otroDia = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("03-causacion-del-dia"), fecha: new DateOnly(2026, 11, 15));
        var dcc = Parte("17-la-deudora-pasa-a-ser-empleada");
        var entregas = new[] { Parte("03-causacion-del-dia"), ConHorario(otroDia, EscenarioContableDeCartera.Horario("InteresCausado")), dcc }.SelectMany(u => u).ToList();

        AgrupadorDeResumidosDeCartera.Planear(entregas, IntegrationDestinations.Accounting).Select(t => t.ClaveDeGrupo)
            .Should().Equal("2026-11-14|CA", "2026-11-15|CA", "2026-12-02|CN");
    }

    [Fact]
    public void Con_PorHecho_cada_operacion_es_una_unidad_sola()
    {
        var entregas = new[] { Parte("03-causacion-del-dia", ClavesDeLote.GranularidadPorHecho), Parte("25-mora-causada", ClavesDeLote.GranularidadPorHecho) }
            .SelectMany(u => u).ToList();

        var trabajos = AgrupadorDeResumidosDeCartera.Planear(entregas, IntegrationDestinations.Accounting);

        trabajos.Should().HaveCount(2).And.OnlyContain(t => t.ClaveDeGrupo == null && t.Unidades.Count == 1, "C07: un comprobante por operación");
    }

    [Fact]
    public void Los_informativos_y_lo_que_no_trae_clave_de_Cartera_van_solos()
    {
        var informativo = Parte("46-causacion-suspendida-informativo");
        var sinClave = ConHorario(EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("25-mora-causada")), null);

        var trabajos = AgrupadorDeResumidosDeCartera.Planear([.. informativo, .. sinClave], IntegrationDestinations.Accounting);

        trabajos.Should().HaveCount(2).And.OnlyContain(t => t.ClaveDeGrupo == null);
    }

    [Fact]
    public void Una_unidad_es_el_pago_con_sus_acompanantes()
    {
        var pago = ConHorario(EscenarioContableDeCartera.UnidadDe("05-pago-con-anticipo-suspenso-y-saldo-a-favor-en-un-RC"), null);

        var unidades = AgrupadorDeResumidosDeCartera.Unidades(pago, IntegrationDestinations.Accounting);

        unidades.Should().ContainSingle().Which.Unidad.MessagePublicIds.Should().HaveCount(4);
        unidades[0].Unidad.OriginModule.Should().Be(ModulosDeOrigen.Cartera);
    }
}
