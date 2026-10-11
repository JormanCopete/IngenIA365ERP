using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Integration;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US3, T0427 (nombre de quickstart §1.5; T36, FR-012; contabilidad.md §8): la fecha contable de un hecho es la fecha valor si su mes
/// está abierto en Contabilidad; si está cerrado, el primer día del primer mes abierto; un mes que no existe (<c>PeriodNotFound</c>) no se salta
/// ni se inventa: la fecha queda en él y se levanta <c>Contabilidad.PeriodoInexistente</c>. Sin contabilidad iniciada, la fecha valor. US7 y US9 la
/// amplían (los cierres de Cartera). Sobre un doble de <see cref="IContabilidadParaCartera"/>.
/// </summary>
public sealed class FechaContableTests
{
    private static readonly DateOnly Valor = new(2026, 12, 17);

    private readonly IContabilidadParaCartera _contabilidad = Substitute.For<IContabilidadParaCartera>();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly List<AlertaALevantar> _levantadas = [];

    public FechaContableTests() =>
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(_levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));

    private void Estado(bool iniciada, EstadoDePeriodoContable estado, DateOnly? primerAbierto = null) =>
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EstadoDelPeriodoDto(iniciada, estado, primerAbierto)));

    private Task<Result<DateOnly>> CalcularAsync() => new FechaContable(_contabilidad, _alertas).CalcularAsync(Valor, default);

    [Fact]
    public async Task Con_el_mes_abierto_es_la_fecha_valor()
    {
        Estado(true, EstadoDePeriodoContable.Open, Valor);

        (await CalcularAsync()).Value.Should().Be(Valor);
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_el_mes_cerrado_es_el_primer_dia_del_primer_mes_abierto()
    {
        Estado(true, EstadoDePeriodoContable.Closed, new DateOnly(2027, 2, 1));

        (await CalcularAsync()).Value.Should().Be(new DateOnly(2027, 2, 1));
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_mes_inexistente_no_se_salta_y_levanta_la_alerta_de_periodo_inexistente()
    {
        Estado(true, EstadoDePeriodoContable.NotFound);

        (await CalcularAsync()).Value.Should().Be(Valor, "la fecha queda en el mes que no existe: no se inventa otra");
        var alerta = _levantadas.Should().ContainSingle().Subject;
        alerta.TypeCode.Should().Be(TiposDeAlerta.PeriodoInexistente);
        alerta.DedupKey.Should().Be("Contabilidad.PeriodoInexistente:2026-12");
        alerta.RecipientPermissions.Should().Contain(FechaContable.PermisoDePeriodos);
    }

    [Fact]
    public async Task Cerrado_sin_ningun_mes_abierto_despues_queda_en_el_siguiente_con_la_alerta()
    {
        Estado(true, EstadoDePeriodoContable.Closed);

        (await CalcularAsync()).Value.Should().Be(new DateOnly(2027, 1, 1));
        _levantadas.Should().ContainSingle().Which.DedupKey.Should().Be("Contabilidad.PeriodoInexistente:2027-01");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_es_la_fecha_valor_sin_alerta()
    {
        Estado(false, EstadoDePeriodoContable.NotFound);

        (await CalcularAsync()).Value.Should().Be(Valor);
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_Contabilidad_falla_falla_la_operacion_sin_fecha_supuesta()
    {
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<EstadoDelPeriodoDto>(new Error("Accounting.Unavailable", "sin respuesta")));

        (await CalcularAsync()).Error.Code.Should().Be("Accounting.Unavailable");
    }
}
