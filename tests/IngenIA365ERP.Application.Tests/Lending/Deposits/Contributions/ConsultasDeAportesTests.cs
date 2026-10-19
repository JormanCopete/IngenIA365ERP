using FluentAssertions;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;

/// <summary>
/// Feature 014, US3 (T093; api.md §7): el resumen de aportes de una persona —el mismo saldo de <c>IConsultaDeAportes</c>, el desglose por clase y las
/// cuotas vencidas— y las cuotas exigibles por estado y período, en el alcance de quien pregunta. (nuevo)
/// </summary>
public sealed class ConsultasDeAportesTests : IDisposable
{
    private static readonly DateOnly Enero10 = new(2027, 1, 10);
    private readonly AportesDePrueba _a = new();

    public void Dispose() => _a.Dispose();

    [Fact]
    public async Task El_resumen_da_el_saldo_de_la_consulta_de_aportes_por_clase_y_las_cuotas_vencidas()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 30_000m));

        var r = await new GetContributionsSummaryQueryHandler(_a.Db, _a.Consulta(), _a.G.Alcance, _a.G.Auditoria, _a.G.Reloj)
            .Handle(new GetContributionsSummaryQuery(_a.C.Ana.PublicId, Enero10), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Available.Should().BeTrue();
        r.Value.Total.Should().Be(30_000m);
        (r.Value.ByClass.Minimum + r.Value.ByClass.Ordinary + r.Value.ByClass.Extraordinary + r.Value.ByClass.Revalued).Should().Be(r.Value.Total,
            "el desglose por clase suma el mismo saldo");
        r.Value.InstallmentsInArrears.Select(q => (q.Period, q.Pending)).Should().Equal(("2026-12", 20_000m), ("2027-01", 50_000m));
        r.Value.AccountPublicIds.Should().Contain(cuenta.PublicId);
    }

    [Fact]
    public async Task Las_cuotas_se_filtran_por_estado_y_periodo()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 30_000m));

        var todas = await new ListAccountInstallmentsQueryHandler(_a.Db, _a.G.Alcance)
            .Handle(new ListAccountInstallmentsQuery(_a.C.Ana.PublicId), default);
        todas.Value.Items.Select(i => (i.Period, i.Status)).Should().Equal(("2026-12", "PartiallyPaid"), ("2027-01", "Pending"));
        todas.Value.Items[0].Paid.Should().Be(30_000m);

        var pendientes = await new ListAccountInstallmentsQueryHandler(_a.Db, _a.G.Alcance)
            .Handle(new ListAccountInstallmentsQuery(_a.C.Ana.PublicId, EstadoDeCuotaDeCuenta.Pending), default);
        pendientes.Value.Items.Should().ContainSingle(i => i.Period == "2027-01");

        var deDiciembre = await new ListAccountInstallmentsQueryHandler(_a.Db, _a.G.Alcance)
            .Handle(new ListAccountInstallmentsQuery(_a.C.Ana.PublicId, Period: "2026-12"), default);
        deDiciembre.Value.Items.Should().ContainSingle().Which.AccountNumber.Should().Be(cuenta.Number);
    }
}
