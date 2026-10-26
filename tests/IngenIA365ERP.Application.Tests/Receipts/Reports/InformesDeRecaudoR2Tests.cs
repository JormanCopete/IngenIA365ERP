using FluentAssertions;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Receipts.Agreements;
using IngenIA365ERP.Application.Receipts.Reports;
using IngenIA365ERP.Application.Tests.Receipts.Agreements;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Reports;

/// <summary>
/// Feature 015, R2, T0437 (FR-152 parte R2): las vistas <c>channels</c> y <c>agreements</c>. La de canales cuadra con los recibos vigentes por canal; la de convenios
/// muestra cada archivo con lo informado, lo aplicado, lo que fue a partidas y su conciliación (resaltado si no está conciliado). Prueba escrita, sin correr. (nuevo)
/// </summary>
public sealed class InformesDeRecaudoR2Tests
{
    [Fact]
    public void Las_dos_vistas_de_R2_estan_en_el_registro()
    {
        VistasDeRecaudo.Todas.Select(v => v.Key).Should().Contain(["channels", "agreements"]);
    }

    [Fact]
    public async Task Canales_y_convenios_cuadran_con_los_recibos_del_archivo()
    {
        var p = new ConveniosDePrueba();
        var hoy = EscenarioDeRecaudo.Hoy;
        var carga = await p.Cargar().Handle(new LoadCollectionFileCommand(p.Convenio.PublicId, "CONVENIO.csv", ConveniosDePrueba.Archivo(
            $"{hoy:yyyy-MM-dd};100000.00;{ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroA)};S1;;",
            $"{hoy:yyyy-MM-dd};50000.00;{ConveniosDePrueba.Referencia("999999")};S2;;")) { OperationKey = Guid.NewGuid() }, default);
        carga.IsSuccess.Should().BeTrue(carga.IsFailure ? carga.Error.Code : null);
        var e = p.B.K.C.E;
        var informes = new ReceiptsReportQueryHandler(e.Db, e.Alcance, e.Reloj);
        var filtros = new FiltrosDeInformeDeRecaudoYTesoreria { From = hoy, To = hoy };

        var canales = await informes.Handle(new ReceiptsReportQuery(VistasDeRecaudo.Canales, filtros), default);
        var convenios = await informes.Handle(new ReceiptsReportQuery(VistasDeRecaudo.Convenios, filtros), default);

        canales.IsSuccess.Should().BeTrue(canales.IsFailure ? canales.Error.Message : null);
        canales.Value.Filas.Single(f => (string?)f.Valores[4] == "Agreement").Valores[2].Should().Be(100_000m);
        convenios.Value.Filas.Single().Valores[6].Should().Be(150_000m, "lo aplicado más lo que fue a partidas");
        convenios.Value.Filas.Single().Resaltada.Should().BeTrue("sin conciliar");
    }
}
