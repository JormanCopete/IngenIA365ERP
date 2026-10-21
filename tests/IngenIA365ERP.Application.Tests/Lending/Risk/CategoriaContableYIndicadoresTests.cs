using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0682 (C08, C106; contabilidad.md §9.3; data-model §23.2): la categoría contable de un crédito para las dimensiones de sus
/// hechos es la de su línea en el <b>último cierre vigente anterior</b> a la fecha —el base cuenta, el reabierto no—; sin línea (o sin cierre),
/// la de crédito nuevo (<c>A</c>, <c>NewLoan</c>). <see cref="CategoriaContableDeCartera.AlCorteAsync"/> incluye el cierre fechado ese mismo día
/// (lo que usa la consulta de la calificación oficial).
/// </summary>
public class CategoriaContableDeCarteraTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync() => _c = await CierreDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private CategoriaContableDeCartera Categoria() => new(_c.Db);

    [Fact]
    public async Task Sin_base_de_datos_o_sin_linea_es_la_de_credito_nuevo()
    {
        (await new CategoriaContableDeCartera().VigenteAsync(_c.Credito.Id, CierreDePrueba.Hoy, default)).Should().Be(CategoriaContableVigente.DeCreditoNuevo);
        (await Categoria().VigenteAsync(_c.Credito.Id, CierreDePrueba.Hoy, default)).Should().Be(CategoriaContableVigente.DeCreditoNuevo,
            "el base existe pero no trae línea del crédito");
    }

    [Fact]
    public async Task Con_linea_en_el_base_rige_su_categoria_contable_desde_el_dia_siguiente_al_corte()
    {
        await _c.LineaDelBaseAsync(RatingCategory.C, deterioroDeCapital: 100_000m);

        var vigente = await Categoria().VigenteAsync(_c.Credito.Id, CierreDePrueba.Hoy, default);
        var elMismoDia = await Categoria().VigenteAsync(_c.Credito.Id, CierreDePrueba.CorteDeLaActivacion, default);
        var alCorte = await Categoria().AlCorteAsync([_c.Credito.Id], CierreDePrueba.CorteDeLaActivacion, default);

        (vigente.Category, vigente.Cause, vigente.MonthlyClosePublicId).Should().Be(("C", RatingCause.Arrears, (Guid?)_c.Base!.PublicId));
        elMismoDia.Should().Be(CategoriaContableVigente.DeCreditoNuevo, "el cierre del día rige para los hechos del día siguiente");
        alCorte[_c.Credito.Id].Category.Should().Be("C", "al corte se incluye el cierre de ese mismo día");
    }

    [Fact]
    public async Task El_ultimo_cierre_vigente_es_el_mensual_mas_reciente_y_no_el_reabierto()
    {
        await _c.LineaDelBaseAsync(RatingCategory.C, deterioroDeCapital: 100_000m);
        await _c.CausarDiciembreAsync();
        var diciembre = await _c.CerrarAsync(2026, 12);
        var enero = new DateOnly(2027, 1, 2);

        (await CategoriaContableDeCartera.UltimoCierreVigenteAsync(_c.Db, enero, default))!.Id.Should().Be(diciembre.Id);
        var linea = CategoriaContableDeCartera.LineasDe(_c.Db, diciembre).Single(l => l.LoanId == _c.Credito.Id);
        (await Categoria().VigenteAsync(_c.Credito.Id, enero, default)).Category.Should().Be(linea.AccountingRating.ToString());

        await _c.ReabrirAsync(diciembre);

        (await CategoriaContableDeCartera.UltimoCierreVigenteAsync(_c.Db, enero, default))!.Id.Should().Be(_c.Base!.Id, "el reabierto no cuenta");
        (await Categoria().VigenteAsync(_c.Credito.Id, enero, default)).Category.Should().Be("C");
    }
}

/// <summary>
/// Feature 013, US9, T0682 (D19, C33; FR-100, FR-103): el lector único de los indicadores de la entidad. Disponibles = patrimonio técnico positivo
/// y solvencia, los dos con corte ≤ la fecha y no más viejos que <c>Contabilidad.IndicadoresVigenciaMeses</c>; si Contabilidad no responde, «no
/// disponible» (lo conservador), nunca una cifra supuesta; con pérdidas acumuladas, los aportes no se descuentan.
/// </summary>
public class LectorDeIndicadoresDeLaEntidadTests : IAsyncLifetime
{
    private static readonly DateOnly Fecha = new(2026, 12, 31);
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync() => _c = await CierreDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private static IndicadorRegistradoDto Indicador(decimal valor, DateOnly corte) => new(Guid.NewGuid(), valor, "COP", corte, "Contabilidad");

    private void Responder(IndicadorRegistradoDto? patrimonio, IndicadorRegistradoDto? solvencia, decimal perdidas = 0m) =>
        _c.D.Contabilidad.IndicadoresDeLaEntidadAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(new IndicadoresDeLaEntidadDto(ci.Arg<DateOnly>(), 0m, perdidas, patrimonio, solvencia))));

    private Task<IndicadoresDeCartera> LeerAsync() => new LectorDeIndicadoresDeLaEntidad(_c.D.Contabilidad, _c.D.Parametros()).LeerAsync(Fecha, default);

    [Fact]
    public async Task Con_los_dos_vigentes_estan_disponibles_y_los_aportes_cuentan()
    {
        Responder(Indicador(1_000_000_000m, new DateOnly(2026, 11, 30)), Indicador(0.12m, new DateOnly(2026, 11, 30)));

        var i = await LeerAsync();

        i.Disponibles.Should().BeTrue();
        i.VigenciaMeses.Should().Be(6);
        i.ContextoDeAportes(excepcionDeArrastre: true, descuentoEnDeterioro: false).Should().Be(new IngenIA365ERP.Domain.Lending.Risk.ContextoDeAportes(true, false, true));
    }

    [Fact]
    public async Task Un_indicador_mas_viejo_que_la_vigencia_o_un_patrimonio_no_positivo_no_esta_disponible()
    {
        Responder(Indicador(1_000_000_000m, new DateOnly(2026, 5, 31)), Indicador(0.12m, new DateOnly(2026, 11, 30)));
        (await LeerAsync()).PatrimonioTecnicoVigente.Should().BeFalse("de hace más de seis meses (C33)");

        Responder(Indicador(0m, new DateOnly(2026, 11, 30)), Indicador(0.12m, new DateOnly(2026, 11, 30)));
        (await LeerAsync()).Disponibles.Should().BeFalse();

        Responder(Indicador(1_000_000_000m, new DateOnly(2026, 11, 30)), null);
        (await LeerAsync()).Disponibles.Should().BeFalse("sin solvencia, lo conservador");
    }

    [Fact]
    public async Task Con_perdidas_acumuladas_los_aportes_no_se_descuentan()
    {
        Responder(Indicador(1_000_000_000m, new DateOnly(2026, 11, 30)), Indicador(0.12m, new DateOnly(2026, 11, 30)), perdidas: 5_000_000m);

        var i = await LeerAsync();

        (i.Disponibles, i.ConPerdidas).Should().Be((true, true));
        i.ContextoDeAportes(excepcionDeArrastre: true, descuentoEnDeterioro: true).Should().Be(new IngenIA365ERP.Domain.Lending.Risk.ContextoDeAportes(true, true, false));
    }

    [Fact]
    public async Task Si_Contabilidad_no_responde_no_hay_indicadores_disponibles()
    {
        _c.D.Contabilidad.IndicadoresDeLaEntidadAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure<IndicadoresDeLaEntidadDto>(new Error("Accounting.Unavailable", "sin respuesta"))));

        var i = await LeerAsync();

        (i.PatrimonioTecnico, i.Solvencia, i.Disponibles).Should().Be(((IndicadorRegistradoDto?)null, (IndicadorRegistradoDto?)null, false));
    }
}
