using FluentAssertions;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using static IngenIA365ERP.Application.Tests.Lending.Regulatory.RegulatoriosDePrueba;

namespace IngenIA365ERP.Application.Tests.Lending.Regulatory;

/// <summary>
/// T0989 (feature 013, K5, US16; T41, D28; adaptadores-y-archivos.md §7.4): la proyección del formato de cartera del SICSES sale del cierre mensual
/// vigente del período —un cierre reabierto no cuenta y su <c>Version + 1</c> sí— con una fila por crédito de sus líneas: el deudor del maestro, los
/// saldos por concepto, los días de mora, la calificación y la subcategoría, el deterioro, la garantía, la reestructuración, la modalidad y el código
/// contable del capital que resuelve Contabilidad. Sin cierre, <c>Lending.MonthlyClose.NotFound</c>. La marca D28 y la normativa sin validar son
/// advertencias y ningún campo del archivo las lleva; el plazo sale del perfil regulatorio.
/// </summary>
public sealed class ProyeccionSicsesCarteraTests : IAsyncLifetime
{
    private RegulatoriosDePrueba _r = null!;

    public async Task InitializeAsync() => _r = await CrearAsync();

    public Task DisposeAsync()
    {
        _r.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Sin_cierre_vigente_del_mes_responde_MonthlyClose_NotFound()
    {
        var r = await _r.Sicses().ProyectarAsync(Anio, Mes, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.MonthlyClose.NotFound");

        // Un cierre reabierto tampoco cuenta.
        var cierre = await _r.CierreAsync([Linea(_r.Credito)]);
        await _r.ReabrirAsync(cierre);
        (await _r.Sicses().ProyectarAsync(Anio, Mes, default)).Error.Code.Should().Be("Lending.MonthlyClose.NotFound");
    }

    [Fact]
    public async Task Sale_de_las_lineas_del_cierre_vigente_una_fila_por_credito()
    {
        var beto = await _r.CreditoAsync("CR-SIC-0002", _r.D.O.Beto, dias: 45);
        var primero = await _r.CierreAsync([Linea(_r.Credito)]);
        await _r.ReabrirAsync(primero);
        await _r.CierreAsync([Linea(_r.Credito, capital: 2_900_000m), Linea(beto, dias: 45, cal: RatingCategory.C, capital: 900_000m, deterioro: 90_000m)],
            version: 2);

        var p = (await _r.Sicses().ProyectarAsync(Anio, Mes, default)).Value;

        p.Cierre.Version.Should().Be(2, "el reabierto no cuenta; su versión siguiente sí");
        p.Creditos.Should().HaveCount(2);
        p.Filas.Should().HaveCount(2).And.OnlyContain(f => f.Registro == FilasDelSicses.Detalle);
        FilaDe(p.Filas, _r.Credito.Number).Valor("Saldos.Capital").Should().Be(2_900_000m, "los saldos son los del cierre vigente");
        var fila = FilaDe(p.Filas, "CR-SIC-0002");
        (fila.Valor("Riesgo.DiasDeMora"), fila.Valor("Riesgo.Categoria"), fila.Valor("Deterioro.Capital"))
            .Should().Be((45, "C", 90_000m));
        p.Totales["Totales.Registros"].Should().Be(2);
        p.Totales["Totales.SaldoDeCapital"].Should().Be(3_800_000m);
        p.Totales["Totales.DeterioroGeneral"].Should().Be(4_321m, "el general del cierre va en los totales, no en la fila");
    }

    [Fact]
    public async Task La_fila_lleva_el_deudor_del_maestro_los_saldos_la_calificacion_el_deterioro_la_garantia_y_la_modalidad()
    {
        _r.Db.Collaterals.Add(new Collateral
        {
            Kind = CollateralKind.Pledge, OwnerPersonId = _r.Ana.Id, LoanId = _r.Credito.Id, Description = "Vehículo", Value = 50_000_000m, AdmissibleValue = 35_000_000m,
            ConstitutedOn = new DateOnly(2026, 11, 1), CreatedBy = "test",
        });
        var ana = _r.Db.People.Single(p => p.Id == _r.Ana.Id);
        ana.TaxId = "1020304050";
        ana.IdType = "C";
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();
        await _r.CierreAsync([Linea(_r.Credito, dias: 100, cal: RatingCategory.E, sub: RatingSubcategory.E1, capital: 2_500_000m, deterioro: 1_250_000m,
            modalidad: "CONSUMO_SIN_LIBRANZA", reestructurado: true, veces: 2, garantia: 35_000_000m, empleado: true)]);

        var p = (await _r.Sicses().ProyectarAsync(Anio, Mes, default)).Value;
        var f = p.Filas.Single();

        (f.Valor("Deudor.TipoDeDocumento"), f.Valor("Deudor.NumeroDeDocumento"), f.Valor("Deudor.EsEmpleado")).Should().Be(("CC", "1020304050", "S"),
            "el deudor sale del maestro al generar, con el tipo de documento traducido");
        (f.Valor("Credito.Numero"), f.Valor("Credito.Modalidad"), f.Valor("Credito.CodigoContable")).Should()
            .Be((_r.Credito.Number, "CONSSINLIB", CuentaDeCapital), "la modalidad del SICSES y la cuenta que resuelve la matriz contable");
        (f.Valor("Saldos.Capital"), f.Valor("Saldos.InteresCorriente"), f.Valor("Saldos.InteresDeMora"), f.Valor("Saldos.OtrosConceptos"))
            .Should().Be((2_500_000m, 12_000m, 1_500m, 3_000m));
        (f.Valor("Riesgo.DiasDeMora"), f.Valor("Riesgo.Categoria"), f.Valor("Riesgo.Subcategoria")).Should().Be((100, "E", "E1"));
        (f.Valor("Deterioro.Capital"), f.Valor("Garantias.Tipo"), f.Valor("Garantias.ValorAdmisible")).Should().Be((1_250_000m, "PR", 35_000_000m));
        (f.Valor("Credito.Reestructurado"), f.Valor("Credito.VecesReestructurado")).Should().Be(("S", 2));
        // Las condiciones y el plan del crédito de Ana (24 cuotas mensuales, desembolsado hoy).
        (f.Valor("Credito.CuotasPactadas"), f.Valor("Credito.PeriodicidadEnDias"), f.Valor("Credito.MontoDesembolsado")).Should().Be((24, 30, 3_000_000m));
        f.Valor("Credito.FechaDeVencimiento").Should().BeOfType<DateOnly>().Which.Should().BeAfter(Corte);
        f.Valor("Credito.ValorDeLaCuota").Should().BeOfType<decimal>().Which.Should().BePositive();
        f.Valor("Credito.TasaEfectivaAnual").Should().BeOfType<decimal>().Which.Scale.Should().BeLessThanOrEqualTo(FilasDelSicses.DecimalesDeLaTasa);
    }

    [Fact]
    public async Task Sin_regla_contable_el_codigo_queda_vacio_y_el_validador_lo_senala()
    {
        _r.ResolverCuentas(false);
        await _r.CierreAsync([Linea(_r.Credito)]);

        var p = (await _r.Preparacion().SicsesAsync(Anio, Mes, default)).Value;

        p.Filas.Single().Valor("Credito.CodigoContable").Should().BeNull();
        p.Escritura.Inconsistencias.Should().Contain(i => i.Ruta == "Credito.CodigoContable" && i.Codigo == "Layout.CampoObligatorioVacio"
                                                          && i.Credito == _r.Credito.Number);
    }

    [Fact]
    public async Task El_modelo_obligatorio_no_disponible_es_advertencia_y_ningun_campo_lo_lleva()
    {
        await _r.CierreAsync([Linea(_r.Credito)], sinModelo: true, pendiente: true);

        var p = (await _r.Sicses().ProyectarAsync(Anio, Mes, default)).Value;

        p.Advertencias.Select(a => a.Code).Should().BeEquivalentTo(
            [AdvertenciasRegulatorias.ModeloObligatorioNoDisponible, AdvertenciasRegulatorias.NormativaPendienteDeValidar]);
        var fila = p.Filas.Single();
        fila.Valores.Keys.Should().NotContain(k => k.Contains("Modelo", StringComparison.OrdinalIgnoreCase) || k.Contains("Obligatori", StringComparison.OrdinalIgnoreCase));
        fila.Valores.Values.OfType<string>().Should().NotContain(v => v.Contains("Modelo", StringComparison.OrdinalIgnoreCase));
        var layout = CatalogoDeLayoutsDeCartera.Embebido.Vigente(RegulatoryFileKind.Sicses, null, Corte)!;
        layout.Layout.Records.SelectMany(r => r.Fields).Should().NotContain(c => c.Path != null && c.Path.Contains("Modelo", StringComparison.OrdinalIgnoreCase),
            "D28 nunca va dentro del archivo");
    }

    [Fact]
    public async Task El_plazo_sale_del_nivel_de_supervision_del_perfil()
    {
        await _r.CierreAsync([Linea(_r.Credito)]);
        await _r.PerfilConSicsesAsync();

        var p = (await _r.Sicses().ProyectarAsync(Anio, Mes, default)).Value;

        p.Plazo.Should().Be(new PlazoDelSicses(1, true, new DateOnly(2027, 1, 20), new DateOnly(2027, 1, 15)));
        p.Advertencias.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_mes_que_el_nivel_no_reporta_lleva_su_advertencia()
    {
        await _r.CierreAsync([Linea(_r.Credito)]);
        await _r.PerfilConSicsesAsync(2, """[ { "supervisionLevel": 2, "cutoffMonths": [3, 6, 9], "dueDays": 30, "warningDays": 5 } ]""");

        var p = (await _r.Sicses().ProyectarAsync(Anio, Mes, default)).Value;

        p.Plazo.Should().Be(new PlazoDelSicses(2, false, null, null));
        p.Advertencias.Should().ContainSingle(a => a.Code == AdvertenciasRegulatorias.MesFueraDeLaPeriodicidad);
    }
}
