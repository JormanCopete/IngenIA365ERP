using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Payroll;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Receipts.Reports;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Reports;

/// <summary>
/// Feature 015, R1, T0399 (FR-081, FR-087, FR-152, FR-155): las vistas de R1 —<c>consolidated-collection</c>, <c>voidings-with-reissue</c>, <c>delinquent-paymasters</c>,
/// <c>received-checks</c>, <c>cash-transactions</c>, <c>pending-application-load</c>— y el tablero <c>GET /api/receipts/dashboard</c> sin datos personales. El consolidado
/// suma, de sólo lectura, Nómina y el débito automático. Prueba escrita, sin correr.
/// </summary>
public sealed class InformesDeRecaudoR1Tests
{
    private readonly EscenarioDeRecaudo _e = new();
    private readonly IRecaudoDeNominaParaConsolidado _nomina = Substitute.For<IRecaudoDeNominaParaConsolidado>();
    private readonly IDebitoAutomaticoParaConsolidado _debito = Substitute.For<IDebitoAutomaticoParaConsolidado>();

    public InformesDeRecaudoR1Tests()
    {
        _nomina.PorDiaAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new RecaudoDeNominaDelDiaDto(EscenarioDeRecaudo.Hoy, 300_000m, 50_000m, 4)]);
        _debito.PorDiaAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new DebitoAutomaticoDelDiaDto(EscenarioDeRecaudo.Hoy, 120_000m, 2)]);
    }

    private ReceiptsReportQueryHandler Informes() => new(_e.Db, _e.Alcance, _e.Reloj, _nomina, _debito);

    private Task<IngenIA365ERP.Application.Common.Models.Result<TablaExportable>> VistaAsync(string vista) =>
        Informes().Handle(new ReceiptsReportQuery(vista, new FiltrosDeInformeDeRecaudoYTesoreria { From = EscenarioDeRecaudo.Hoy, To = EscenarioDeRecaudo.Hoy }), default);

    [Fact]
    public void Las_seis_vistas_de_R1_estan_en_el_registro()
    {
        VistasDeRecaudo.Todas.Select(v => v.Key).Should().Contain(["consolidated-collection", "voidings-with-reissue", "delinquent-paymasters", "received-checks",
            "cash-transactions", "pending-application-load"]);
    }

    [Fact]
    public async Task El_consolidado_suma_Nomina_y_debito_automatico_de_solo_lectura()
    {
        var r = await VistaAsync(VistasDeRecaudo.Consolidado);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var dia = r.Value.Filas.Single();
        dia.Valores[5].Should().Be(350_000m, "lo descontado por Nómina");
        dia.Valores[6].Should().Be(120_000m, "el débito automático");
        dia.Valores[7].Should().Be(470_000m);
        r.Value.Notas.Should().ContainSingle(n => n.Contains("sólo lectura"));
    }

    [Theory]
    [InlineData("voidings-with-reissue")]
    [InlineData("delinquent-paymasters")]
    [InlineData("received-checks")]
    [InlineData("cash-transactions")]
    [InlineData("pending-application-load")]
    public async Task Cada_vista_de_R1_responde_su_tabla(string vista)
    {
        var r = await VistaAsync(vista);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Columnas.Should().NotBeEmpty();
    }

    [Fact]
    public async Task El_tablero_no_lleva_datos_personales()
    {
        var r = await new GetReceiptsDashboardQueryHandler(_e.Db, _e.Alcance, _e.Reloj, _nomina, _debito).Handle(new GetReceiptsDashboardQuery(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Figures.Should().Contain(f => f.Key == "payroll" && f.Amount == 350_000m);
        typeof(CifraDelTableroDto).GetProperties().Select(p => p.PropertyType).Should().NotContain([typeof(Guid), typeof(Guid?)], "sin personas ni documentos");
        typeof(CifraDelTableroDto).GetProperties().Select(p => p.Name).Should().Equal("Key", "Label", "Amount", "Count");
    }
}
