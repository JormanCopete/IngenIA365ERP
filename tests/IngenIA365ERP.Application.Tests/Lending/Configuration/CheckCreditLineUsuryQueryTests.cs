using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0335 (US1; api.md §3.1 <c>usury-check</c>; T28, R36): la consulta de usura de una línea no escribe y devuelve la efectiva
/// con y sin cargos, la usura y su valor nacional, la holgura, los cargos que computan y la explicación; con un monto en una banda usa la
/// tasa de la banda; sin usura del día, <c>NationalValues.Missing</c>; fuera del alcance de líneas, el 404 de la línea. La lista de líneas
/// trae la holgura de la versión vigente y sólo las líneas del alcance.
/// </summary>
public sealed class CheckCreditLineUsuryQueryTests : IDisposable
{
    private readonly LineasDePrueba _l = new();

    public void Dispose() => _l.Dispose();

    [Fact]
    public async Task No_escribe_y_devuelve_efectiva_usura_holgura_cargos_y_explicacion()
    {
        await _l.NormativaAsync(0.25m);
        var linea = await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.20m) with { Insurances = [_l.Seguro(0.001m, retenida: 0.5m)] });
        var antes = await _l.Db.CreditLineVersions.CountAsync();

        var r = await _l.Consultar().Handle(new CheckCreditLineUsuryQuery(linea.PublicId, Amount: 10_000_000m, TermInstallments: 24), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Passes.Should().BeTrue();
        r.Value.UsuryRate.Should().Be(0.25m);
        r.Value.UsuryKey.Should().Be(LineasDePrueba.ClaveConsumo);
        r.Value.NationalValuePublicId.Should().NotBeNull();
        r.Value.EffectiveRate.Should().BeGreaterThan(r.Value.EffectiveRateWithoutCharges, "el seguro que retiene computa");
        r.Value.ChargesSpread.Should().Be(r.Value.EffectiveRate - r.Value.EffectiveRateWithoutCharges);
        r.Value.Headroom.Should().Be(0.25m - r.Value.EffectiveRate);
        r.Value.ComputableCharges.Should().ContainSingle().Which.Code.Should().Be("VIDA");
        r.Value.Explanation.Should().NotBeEmpty();
        (await _l.Db.CreditLineVersions.CountAsync()).Should().Be(antes);
        _l.Db.ChangeTracker.HasChanges().Should().BeFalse("la consulta no escribe");
    }

    [Fact]
    public async Task Con_un_monto_dentro_de_una_banda_usa_la_tasa_de_la_banda()
    {
        await _l.NormativaAsync(0.25m);
        var linea = await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.20m) with
        {
            Rates = LineasDePrueba.Version().Rates with { Bands = [new RateBandInput(0m, 5_000_000m, EffectiveAnnual: 0.24m)] },
        });

        var r = await _l.Consultar().Handle(new CheckCreditLineUsuryQuery(linea.PublicId, Amount: 2_000_000m, TermInstallments: 12), default);

        r.Value.Rate.Should().Be(0.24m);
        r.Value.Field.Should().Be("rateBands[1].annualRate");
    }

    [Fact]
    public async Task Sin_usura_del_dia_es_NationalValues_Missing()
    {
        await _l.NormativaAsync(0.25m);
        var linea = await _l.LineaAsync("CONLIB");

        var r = await _l.Consultar().Handle(new CheckCreditLineUsuryQuery(linea.PublicId, new DateOnly(2026, 11, 1)), default);

        r.Error.Code.Should().Be("NationalValues.Missing");
    }

    [Fact]
    public async Task Fuera_del_alcance_de_lineas_es_el_404_de_la_linea()
    {
        await _l.NormativaAsync(0.25m);
        var linea = await _l.LineaAsync("CONLIB");
        _l.G.AlcanceActual = AlcanceDeCartera.Vacio;

        var r = await _l.Consultar().Handle(new CheckCreditLineUsuryQuery(linea.PublicId), default);

        r.Error.Code.Should().Be("Lending.CreditLine.NotFound");
    }

    [Fact]
    public async Task La_lista_trae_la_holgura_de_la_vigente_y_solo_las_lineas_del_alcance()
    {
        await _l.NormativaAsync(0.25m);
        var conlib = await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.20m));
        await _l.LineaAsync("VARIBR", LineasDePrueba.Version(0.22m));
        var id = (await _l.Db.CreditLines.SingleAsync(l => l.Code == "CONLIB")).Id;

        var todas = await _l.Listar().Handle(new ListCreditLinesQuery(), default);
        _l.G.AlcanceActual = new AlcanceDeCartera(true, new HashSet<int>(), null, false, new HashSet<int> { id });
        var delAlcance = await _l.Listar().Handle(new ListCreditLinesQuery(), default);

        todas.Value.Select(l => l.Code).Should().Equal("CONLIB", "VARIBR");
        var holgura = todas.Value[0].CurrentVersion!.UsuryHeadroom;
        holgura.Should().NotBeNull();
        holgura!.Value.Should().BeApproximately(0.05m, 0.001m);
        delAlcance.Value.Should().ContainSingle().Which.PublicId.Should().Be(conlib.PublicId);
    }

    [Fact]
    public async Task La_lista_no_trae_las_cerradas_salvo_que_se_pidan()
    {
        await _l.NormativaAsync(0.25m);
        var linea = await _l.LineaAsync("CONLIB");
        await _l.Cerrar().Handle(new CloseCreditLineCommand(linea.PublicId, "Se reemplaza") { OperationKey = Guid.NewGuid() }, default);

        (await _l.Listar().Handle(new ListCreditLinesQuery(), default)).Value.Should().BeEmpty();
        (await _l.Listar().Handle(new ListCreditLinesQuery(IncludeClosed: true), default)).Value.Should().ContainSingle()
            .Which.Status.Should().Be(CreditLineStatus.Closed);
    }
}
