using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Tests.Lending.Sessions;

namespace IngenIA365ERP.Application.Tests.Lending.Reports;

/// <summary>
/// T0771 (feature 013, US15, K1; contracts/api.md §17): los filtros comunes del centro de informes de Cartera. Un rango al revés es
/// <c>Lending.Report.RangeInvalid</c> y uno de cinco años o más, <c>.RangeTooLong</c>; un filtro propio fuera de su lista (<c>groupBy</c>,
/// <c>by</c>, <c>dateKind</c>) es <c>.FilterInvalid</c> con los admitidos; sin <c>asOf</c> el corte es hoy en la fecha local; y una sucursal o una
/// línea fuera del alcance dan un resultado vacío, nunca otra oficina.
/// </summary>
public class FiltrosDeInformeDeCarteraTests
{
    private static readonly DateOnly Hoy = InformesDePrueba.Hoy;

    [Fact]
    public void Un_rango_al_reves_es_RangeInvalid()
    {
        var r = new FiltrosDeInformeDeCartera { From = new DateOnly(2026, 12, 10), To = new DateOnly(2026, 12, 1) }.ValidarRango(Hoy);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.Report.RangeInvalid");
        r.Error.Message.Should().Be("La fecha inicial es posterior a la final.");
    }

    [Theory]
    [InlineData("2021-12-02", "2026-12-01", true)]
    [InlineData("2021-12-01", "2026-12-01", false)]
    [InlineData("2019-01-01", "2026-12-01", false)]
    public void El_rango_es_de_menos_de_cinco_anios(string desde, string hasta, bool valido)
    {
        var r = new FiltrosDeInformeDeCartera { From = DateOnly.Parse(desde), To = DateOnly.Parse(hasta) }.ValidarRango(Hoy);

        r.IsSuccess.Should().Be(valido);
        if (!valido)
        {
            r.Error.Code.Should().Be("Lending.Report.RangeTooLong");
            r.Error.Message.Should().Contain("5 años");
        }
    }

    [Fact]
    public void Sin_fechas_el_rango_es_el_mes_en_curso_y_el_corte_es_hoy()
    {
        var f = new FiltrosDeInformeDeCartera();

        f.Desde(Hoy).Should().Be(new DateOnly(2026, 12, 1));
        f.Hasta(Hoy).Should().Be(Hoy);
        f.ALaFecha(Hoy).Should().Be(Hoy);
        f.ValidarRango(Hoy).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("groupBy", "producto")]
    [InlineData("by", "cliente")]
    [InlineData("dateKind", "cuando")]
    public void Un_filtro_propio_fuera_de_su_lista_es_FilterInvalid_con_los_admitidos(string filtro, string valor)
    {
        var r = FiltrosDeInformeDeCartera.Elegir(filtro, valor, ["line", "office"], "line");

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.Report.FilterInvalid");
        r.Error.Message.Should().Contain($"«{valor}» no es un valor de {filtro}").And.Contain("line, office");
        var datos = GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("filter").GetString().Should().Be(filtro);
        datos.GetProperty("allowed").EnumerateArray().Select(a => a.GetString()).Should().Equal("line", "office");
        FiltrosDeInformeDeCartera.Elegir(filtro, "OFFICE", ["line", "office"], "line").Value.Should().Be("office", "sin distinguir mayúsculas");
        FiltrosDeInformeDeCartera.Elegir(filtro, null, ["line", "office"], "line").Value.Should().Be("line", "sin valor rige el defecto");
    }

    [Fact]
    public async Task Las_vistas_rechazan_su_filtro_propio_con_los_admitidos()
    {
        using var e = await InformesDePrueba.CrearAsync();
        var f = new FiltrosDeInformeDeCartera();

        var colocacion = await e.Colocacion().Handle(new PlacementReportQuery(f, "producto"), default);
        var recaudo = await e.Recaudo().Handle(new ReceiptsReportQuery(f, "office"), default);
        var movimientos = await e.Movimientos().Handle(new TransactionsReportQuery(f, DateKind: "cuando"), default);
        var edades = await e.Edades().Handle(new AgingReportQuery(f, "90,30"), default);

        colocacion.Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        colocacion.Error.Message.Should().Contain("line, office, advisor, month");
        recaudo.Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        recaudo.Error.Message.Should().Contain("line, collector, modality, meansClass");
        movimientos.Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        movimientos.Error.Message.Should().Contain("value, accounting, registered");
        edades.Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo, "los rangos van en orden creciente");
    }

    [Fact]
    public async Task Sin_asOf_el_corte_es_hoy_en_la_fecha_local()
    {
        using var e = await InformesDePrueba.CrearAsync();
        // El corte sale de IDateTimeService.HoyLocal (la fecha de Colombia), no de la fecha UTC.
        e.G.UtcNow = Hoy.AddDays(1).ToDateTime(new TimeOnly(2, 0), DateTimeKind.Utc);
        e.G.HoyLocal = Hoy;

        var t = InformesDePrueba.Tabla(await e.Edades().Handle(new AgingReportQuery(new FiltrosDeInformeDeCartera()), default));

        t.Subtitulo.Should().StartWith($"Al {Hoy:yyyy-MM-dd}");
        t.Filas.Should().ContainSingle().Which.Valores[0].Should().Be(e.Credito.Number);
    }

    [Fact]
    public async Task Una_sucursal_o_una_linea_fuera_del_alcance_dan_un_resultado_vacio_nunca_otra_oficina()
    {
        using var e = await InformesDePrueba.CrearAsync();
        var sucursal = e.Db.Branches.Single(b => b.Id == e.Credito.BranchId).PublicId;
        var linea = e.Db.CreditLines.Single(l => l.Id == e.Credito.CreditLineId).PublicId;

        // Con todo el alcance y la sucursal del crédito, el crédito aparece.
        var conAlcance = InformesDePrueba.Tabla(await e.Colocacion().Handle(new PlacementReportQuery(new FiltrosDeInformeDeCartera { Branch = sucursal }), default));
        conAlcance.Filas.Should().ContainSingle();

        // Con el alcance de otra oficina, pedir la sucursal del crédito no lo trae ni trae otra cosa.
        e.G.AlcanceActual = InformesDePrueba.OtraOficina(e.Credito.BranchId);
        var fuera = InformesDePrueba.Tabla(await e.Colocacion().Handle(new PlacementReportQuery(new FiltrosDeInformeDeCartera { Branch = sucursal }), default));
        fuera.Filas.Should().BeEmpty();
        InformesDePrueba.Tabla(await e.Edades().Handle(new AgingReportQuery(new FiltrosDeInformeDeCartera { Branch = sucursal }), default)).Filas.Should().BeEmpty();

        // Una línea fuera del alcance de líneas: vacío.
        e.G.AlcanceActual = new AlcanceDeCartera(true, new HashSet<int>(), null, false, new HashSet<int> { e.Credito.CreditLineId + 1000 });
        InformesDePrueba.Tabla(await e.Movimientos().Handle(new TransactionsReportQuery(new FiltrosDeInformeDeCartera { Line = linea }), default))
            .Filas.Should().BeEmpty();

        // Una sucursal que no existe: vacío, no «todas».
        e.G.AlcanceActual = AlcanceDeCartera.Total;
        InformesDePrueba.Tabla(await e.Colocacion().Handle(new PlacementReportQuery(new FiltrosDeInformeDeCartera { Branch = Guid.NewGuid() }), default))
            .Filas.Should().BeEmpty();
    }

    [Fact]
    public void Para_la_auditoria_viaja_solo_lo_que_vino()
    {
        var linea = Guid.NewGuid();
        var d = new FiltrosDeInformeDeCartera { AsOf = new DateOnly(2026, 12, 1), Line = linea, Modality = "CONSUMO", Format = "xlsx" }.ParaAuditoria();

        d.Should().ContainKey("asOf").WhoseValue.Should().Be("2026-12-01");
        d.Should().ContainKey("line").WhoseValue.Should().Be(linea.ToString());
        d.Should().ContainKey("modality").WhoseValue.Should().Be("CONSUMO");
        d.Should().NotContainKey("from").And.NotContainKey("branch").And.NotContainKey("format");
    }
}
