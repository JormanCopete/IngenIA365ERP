using FluentAssertions;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Common.NationalValues;

namespace IngenIA365ERP.Application.Tests.Lending.Engine;

/// <summary>
/// Feature 013, US4, T0470 (T38, T40, T44; DT §2.9): <see cref="SeriesRegulatoriasDeCartera"/> lee la usura sobre
/// <see cref="IValoresNacionales.LeerSerieAsync"/> <b>una vez por corrida</b> (un rango que cabe en lo leído no vuelve a la base), con la modalidad
/// certificada que el catálogo vigente asigna a la combinación (T44), los festivos nacionales del calendario del art. 829, el umbral de suspensión
/// del Anexo 1 y la tolerancia; lo conocido en R (<c>VigenciaBitemporal</c>, T38); y un día sin usura responde <c>NationalValues.Missing</c>.
/// </summary>
public class SeriesRegulatoriasDeCarteraTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync() => _c = await CausacionDePrueba.CrearAsync(conDesembolso: false);

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>El lector real de valores nacionales que cuenta cuántas veces se lee una serie.</summary>
    private sealed class ValoresQueCuentan(IValoresNacionales real) : IValoresNacionales
    {
        public List<(string Codigo, string Clave, DateOnly Desde, DateOnly Hasta, DateTime? R)> Series { get; } = [];

        public Task<Result<ValorNacionalVigente>> LeerAsync(string codigo, string clave, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default) =>
            real.LeerAsync(codigo, clave, fecha, informacionHasta, ct);

        public Task<Result<ValorNacionalVigente>> LeerUltimoPublicadoAsync(string codigo, string clave, DateOnly fecha, DateTime? informacionHasta = null,
            CancellationToken ct = default) => real.LeerUltimoPublicadoAsync(codigo, clave, fecha, informacionHasta, ct);

        public Task<SerieDeValorNacional> LeerSerieAsync(string codigo, string clave, DateOnly desde, DateOnly hasta, DateTime? informacionHasta = null,
            CancellationToken ct = default)
        {
            Series.Add((codigo, clave, desde, hasta, informacionHasta));
            return real.LeerSerieAsync(codigo, clave, desde, hasta, informacionHasta, ct);
        }
    }

    private (SeriesRegulatoriasDeCartera Series, ValoresQueCuentan Valores) Lector()
    {
        var valores = new ValoresQueCuentan(_c.D.O.L.Valores());
        return (new SeriesRegulatoriasDeCartera(valores, _c.D.O.L.Normativa(), new CalendarioDeDiasHabiles(_c.Db), _c.D.Parametros()), valores);
    }

    [Fact]
    public async Task Lee_la_usura_de_la_modalidad_certificada_una_vez_por_corrida()
    {
        var (lector, valores) = Lector();

        var primera = await lector.LeerAsync("CONSUMO", "SinLibranza", Hoy, Hoy.AddDays(30), null, default);
        var dentro = await lector.LeerAsync("CONSUMO", "SinLibranza", Hoy.AddDays(5), Hoy.AddDays(20), null, default);

        primera.IsSuccess.Should().BeTrue();
        dentro.Value.Should().BeSameAs(primera.Value, "un rango que cabe en lo leído no vuelve a la base");
        valores.Series.Should().ContainSingle().Which.Should().Be((CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, Hoy, Hoy.AddDays(30), (DateTime?)null));
        primera.Value.ClaveDeUsura.Should().Be(LineasDePrueba.ClaveConsumo, "el catálogo vigente asigna CONSUMO_Y_ORDINARIO a CONSUMO/SinLibranza (T44)");
        primera.Value.Usura.Should().ContainSingle().Which.Ea.Should().Be(0.25m);
        primera.Value.DiasSinUsura.Should().BeEmpty();

        await lector.LeerAsync("CONSUMO", "SinLibranza", Hoy, Hoy.AddDays(60), null, default);
        valores.Series.Should().HaveCount(2, "un rango más largo vuelve a leer, una vez");
    }

    [Fact]
    public async Task Lee_con_lo_conocido_en_R()
    {
        var original = _c.Db.NationalValues.Single(v => v.Code == CodigosDeValorNacional.Usura && v.Key == LineasDePrueba.ClaveConsumo);
        var r = _c.D.G.UtcNow.AddHours(-1);
        await CopiasDePrueba.SembrarAsync(_c.Db, _c.D.G.UtcNow,
            CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, Hoy, 0.24m, _c.D.G.UtcNow, corrige: original.SourcePublicId, unidad: "EA"));
        _c.Db.ChangeTracker.Clear();
        var (lector, valores) = Lector();

        var antes = await lector.LeerAsync("CONSUMO", "SinLibranza", Hoy, Hoy.AddDays(3), r, default);
        var ahora = await lector.LeerAsync("CONSUMO", "SinLibranza", Hoy, Hoy.AddDays(3), null, default);

        valores.Series.Select(x => x.R).Should().Equal(r, null);
        antes.Value.Usura.Should().ContainSingle().Which.Ea.Should().Be(0.25m, "con R anterior a la corrección rige la original (T38)");
        ahora.Value.Usura.Should().ContainSingle().Which.Ea.Should().Be(0.24m);
    }

    [Fact]
    public async Task Los_festivos_salen_del_calendario_nacional_y_un_dia_sin_usura_es_NationalValues_Missing()
    {
        await _c.FestivoAsync(new DateOnly(2026, 12, 8));
        using var corta = await CausacionDePrueba.CrearAsync(usuraHasta: Hoy.AddDays(2), conDesembolso: false);
        var series = new SeriesRegulatoriasDeCartera(corta.D.O.L.Valores(), corta.D.O.L.Normativa(), new CalendarioDeDiasHabiles(corta.Db), corta.D.Parametros());
        var (lector, _) = Lector();

        var conFestivo = await lector.LeerAsync("CONSUMO", "SinLibranza", Hoy, Hoy.AddDays(10), null, default);
        var sinUsura = await series.LeerAsync("CONSUMO", "SinLibranza", Hoy, Hoy.AddDays(5), null, default);

        conFestivo.Value.Festivos.Should().Contain(new DateOnly(2026, 12, 8)).And.NotContain(d => d.DayOfWeek == DayOfWeek.Sunday,
            "el domingo ya es de descanso en el art. 829");
        sinUsura.Value.DiasSinUsura.Should().Equal(Hoy.AddDays(3), Hoy.AddDays(4), Hoy.AddDays(5));
        sinUsura.Value.UsuraDe(Hoy.AddDays(4)).Error.Code.Should().Be("NationalValues.Missing");
        sinUsura.Value.UsuraDe(Hoy.AddDays(1)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Una_modalidad_que_no_esta_en_el_catalogo_es_NationalValues_Missing_y_el_umbral_sale_del_Anexo_1()
    {
        var (lector, _) = Lector();
        (await lector.LeerAsync("INEXISTENTE", null, Hoy, Hoy.AddDays(1), null, default)).Error.Code.Should().Be("NationalValues.Missing");

        (await lector.LeerAsync("CONSUMO", "SinLibranza", Hoy, Hoy.AddDays(1), null, default)).Value.UmbralesDeSuspension.Should().BeEmpty(
            "sin Anexo 1 replicado no hay umbral");
        await _c.AnexoAsync(new DateOnly(2026, 1, 1));
        var (otro, _) = Lector();
        var conAnexo = await otro.LeerAsync("CONSUMO", "SinLibranza", Hoy, Hoy.AddDays(1), null, default);
        conAnexo.Value.UmbralesDeSuspension.Should().ContainSingle().Which.Valor.Should().Be(61, "consumo suspende desde C, que empieza en 61 días");
        conAnexo.Value.SegmentoSarc.Should().Be("CONSUMO");
        conAnexo.Value.Tolerancias.Should().ContainSingle().Which.Valor.Should().Be(0m, "el defecto de Mora.ToleranciaDeSaldoVencido");
    }
}
