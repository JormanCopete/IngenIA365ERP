using FluentAssertions;
using IngenIA365ERP.Application.Lending.Housing;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Housing;

/// <summary>
/// Feature 013, K6b, bloque 2 (T1060; FR-024, D18; api.md §29): lo de vivienda y UVR que exponen las consultas. La simulación de una línea en UVR
/// trae el plan en UVR con la UVR del desembolso y su leyenda, y dice que es de vivienda; en pesos, nada de UVR. El detalle de una línea de vivienda
/// trae el tope y los cinco límites vigentes con su fuente, y lo que falta sin romper la consulta. Valores DE PRUEBA (los de
/// <see cref="LineasDePrueba.ViviendaAsync"/>). (nuevo)
/// </summary>
public sealed class ViviendaEnLasConsultasTests : IDisposable
{
    private readonly LineasDePrueba _l = new();

    public void Dispose() => _l.Dispose();

    [Fact]
    public async Task La_simulacion_en_UVR_expone_el_plan_en_UVR_y_la_de_pesos_no()
    {
        await _l.NormativaAsync();
        await _l.ViviendaAsync();
        await _l.UvrAsync((LineasDePrueba.Hoy, 407.7518m));
        var pesos = await _l.LineaAsync("VIV", LineasDePrueba.Vivienda());
        var uvr = await _l.LineaAsync("VIVUVR", LineasDePrueba.Vivienda(unidad: DenominationUnit.Uvr));

        var enPesos = await SimularAsync(pesos.PublicId);
        enPesos.Uvr.Should().BeNull("una línea en pesos no lleva nada de UVR");
        enPesos.IsHousing.Should().BeTrue();
        enPesos.RulesVersion.Should().Be(VersionesDeReglas.Vivienda);

        var enUvr = await SimularAsync(uvr.PublicId);
        var plan = enUvr.Uvr!;
        plan.UvrValue.Should().Be(407.7518m);
        plan.UvrDate.Should().Be(LineasDePrueba.Hoy);
        plan.Installments.Should().HaveCount(enUvr.Installments.Count);
        plan.AmountUvr.Should().Be(plan.Installments.Sum(c => c.Principal), "el capital en UVR del plan es el monto en UVR");
        plan.ToPayUvr.Should().Be(plan.Installments.Sum(c => c.Total));
        plan.Legend.Should().Contain("UVR").And.Contain(LineasDePrueba.Hoy.ToString("dd/MM/yyyy"));
        enUvr.Installments.Sum(c => c.Principal).Should().Be(100_000_000m, "las cuotas en pesos son la equivalencia del monto");
    }

    [Fact]
    public async Task El_detalle_de_vivienda_trae_el_tope_y_los_limites_con_su_fuente_y_dice_lo_que_falta()
    {
        var sinNada = await CreditLineHousingDto.LeerAsync(new LectorDeVivienda(_l.Valores()), LineasDePrueba.Hoy, default);
        sinNada.RateCap.Should().BeNull();
        sinNada.Limits.Should().BeEmpty();
        sinNada.Missing.Should().HaveCount(2, "falta el tope y faltan los límites: la consulta no se rompe, lo dice");

        await _l.ViviendaAsync(tope: 0.125m);
        var vivienda = await CreditLineHousingDto.LeerAsync(new LectorDeVivienda(_l.Valores()), LineasDePrueba.Hoy, default);
        vivienda.Missing.Should().BeEmpty();
        vivienda.RateCap!.Rate.Should().Be(0.125m);
        vivienda.Limits.Select(x => x.Code).Should().Equal(
            CatalogoDeLimitesLegales.ViviendaPlazoMinimoMeses, CatalogoDeLimitesLegales.ViviendaPlazoMaximoMeses, CatalogoDeLimitesLegales.ViviendaFinanciable,
            CatalogoDeLimitesLegales.ViviendaFinanciableVis, CatalogoDeLimitesLegales.ViviendaCuotaIngreso);
        vivienda.Limits.Single(x => x.Code == CatalogoDeLimitesLegales.ViviendaFinanciableVis).Value.Should().Be(0.80m);
        vivienda.Limits.Should().OnlyContain(x => x.NationalValuePublicId != null, "cada límite dice de qué valor nacional sale");
    }

    private async Task<ScheduleSimulationDto> SimularAsync(Guid linea)
    {
        var entidad = await _l.Db.CreditLines.AsNoTracking().SingleAsync(l => l.PublicId == linea);
        var r = await new SimuladorDeCredito(_l.Db, _l.Reglas()).SimularAsync(entidad, new ScheduleSimulationInput(linea, 100_000_000m, 120),
            PerfilParaLaBanda.Ninguno, LineasDePrueba.Hoy, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return SimulacionEnAlcance.Dto(r.Value, entidad);
    }
}
