using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Deposits.Reports;
using IngenIA365ERP.Application.Lending.Reports;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;

/// <summary>
/// Feature 014, US3 (T088, T092; api.md §18.1): las vistas <c>contributions-by-member</c> e <c>installments-in-arrears</c> del centro de informes, con
/// datos del titular (exigen <c>Lending.Accounts.Read</c>), con el mismo saldo de la consulta de aportes y las cuotas vencidas con su motivo. (nuevo)
/// </summary>
public sealed class ReportesDeAportesTests : IDisposable
{
    private static readonly DateOnly Enero10 = new(2027, 1, 10);
    private readonly AportesDePrueba _a = new();

    public void Dispose() => _a.Dispose();

    [Fact]
    public async Task Los_aportes_por_asociado_suman_el_libro_por_clase_y_cuentan_las_cuotas_en_mora()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 30_000m));

        var r = await new ContributionsByMemberReportQueryHandler(_a.Db, _a.G.Alcance, _a.G.Reloj)
            .Handle(new ContributionsByMemberReportQuery(new FiltrosDeInformeDeCartera { AsOf = Enero10 }), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var fila = r.Value.Filas.Should().ContainSingle().Subject;
        fila.Valores[0].Should().Be("Ana Titular");
        (fila.Valores[2], fila.Valores[6], fila.Valores[7]).Should().Be((30_000m, 30_000m, 2), "el mínimo primero; las dos cuotas siguen en mora con saldo");
        ContributionsByMemberReportQueryHandler.Vista.RequiredPermission.Should().Be("Lending.Accounts.Read");
        ContributionsByMemberReportQueryHandler.Vista.PersonalData.Should().BeTrue();

        var porClase = await new ContributionsByMemberReportQueryHandler(_a.Db, _a.G.Alcance, _a.G.Reloj)
            .Handle(new ContributionsByMemberReportQuery(new FiltrosDeInformeDeCartera { AsOf = Enero10 }, "Extraordinary"), default);
        porClase.Value.Filas.Should().BeEmpty("nadie tiene extraordinarios");
        var mala = await new ContributionsByMemberReportQueryHandler(_a.Db, _a.G.Alcance, _a.G.Reloj)
            .Handle(new ContributionsByMemberReportQuery(new FiltrosDeInformeDeCartera(), "Otra"), default);
        mala.Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
    }

    [Fact]
    public async Task Las_cuotas_en_mora_traen_lo_pendiente_y_el_motivo_del_no_descuento()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 30_000m));

        var r = await new InstallmentsInArrearsReportQueryHandler(_a.Db, _a.G.Alcance, _a.G.Reloj)
            .Handle(new InstallmentsInArrearsReportQuery(new FiltrosDeInformeDeCartera { AsOf = Enero10 }), default);

        r.Value.Filas.Select(f => (f.Valores[2], f.Valores[4])).Should().Equal(("2026-12", (object?)20_000m), ("2027-01", (object?)50_000m));
        r.Value.Totales!.Valores[4].Should().Be(70_000m);
    }
}
