using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Accounting.Lending;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

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

    // --------------------------------------------------------------------------- mitad contable con el adaptador real (US7, T0627) --

    /// <summary>
    /// El adaptador real (<see cref="ContabilidadParaCartera"/>) con su consulta de estado sobre la base de pruebas: contabilidad iniciada con el
    /// ejercicio 2026; los meses en <paramref name="cerrados"/> cerrados, el resto abierto; 2027 no existe.
    /// </summary>
    private static (FechaContable Fecha, TestApplicationDbContext Db) ConAdaptadorReal(IAlertas alertas, params byte[] cerrados)
    {
        var db = TestDbContextFactory.Create();
        db.AccountingSetups.Add(new AccountingSetup { NiifGroup = 2, MovementLevel = 5, FirstFiscalYear = 2026, InitializedBy = "test", CreatedBy = "test" });
        var ejercicio = new FiscalYear { Year = 2026, CreatedBy = "test" };
        db.FiscalYears.Add(ejercicio);
        for (byte mes = 1; mes <= 12; mes++)
        {
            var inicio = new DateOnly(2026, mes, 1);
            db.AccountingPeriods.Add(new AccountingPeriod
            {
                FiscalYear = ejercicio, Month = mes, StartDate = inicio, EndDate = inicio.AddMonths(1).AddDays(-1),
                Status = cerrados.Contains(mes) ? PeriodStatus.Closed : PeriodStatus.Open, CreatedBy = "test",
            });
        }
        db.SaveChanges();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetAccountingPeriodStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => new GetAccountingPeriodStatusQueryHandler(db).Handle(ci.Arg<GetAccountingPeriodStatusQuery>(), ci.Arg<CancellationToken>()));
        return (new FechaContable(new ContabilidadParaCartera(sender), alertas), db);
    }

    [Fact]
    public async Task Con_el_adaptador_real_la_fecha_valor_en_mes_abierto_es_la_misma()
    {
        var (fecha, db) = ConAdaptadorReal(_alertas, 1, 2);
        using var _ = db;

        (await fecha.CalcularAsync(new DateOnly(2026, 4, 20), default)).Value.Should().Be(new DateOnly(2026, 4, 20));
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_el_adaptador_real_un_mes_cerrado_lleva_al_primer_dia_del_primer_mes_abierto()
    {
        var (fecha, db) = ConAdaptadorReal(_alertas, 1, 2, 3);
        using var _ = db;

        (await fecha.CalcularAsync(new DateOnly(2026, 2, 10), default)).Value.Should().Be(new DateOnly(2026, 4, 1));
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_el_adaptador_real_un_mes_inexistente_se_queda_en_el_y_el_consumidor_reintenta()
    {
        var (fecha, db) = ConAdaptadorReal(_alertas);
        using var _ = db;

        (await fecha.CalcularAsync(new DateOnly(2027, 1, 20), default)).Value.Should().Be(new DateOnly(2027, 1, 20));
        _levantadas.Should().ContainSingle().Which.DedupKey.Should().Be("Contabilidad.PeriodoInexistente:2027-01");
    }

    [Fact]
    public async Task Con_el_adaptador_real_diciembre_cerrado_sin_el_ejercicio_siguiente_se_queda_en_enero_y_alerta()
    {
        var (fecha, db) = ConAdaptadorReal(_alertas, 12);
        using var _ = db;

        (await fecha.CalcularAsync(new DateOnly(2026, 12, 20), default)).Value.Should().Be(new DateOnly(2027, 1, 1));
        _levantadas.Should().ContainSingle().Which.DedupKey.Should().Be("Contabilidad.PeriodoInexistente:2027-01",
            "el primer mes «abierto» todavía no existe: no se salta y se avisa a quien abre períodos");
    }

    [Fact]
    public async Task Con_el_adaptador_real_si_la_consulta_falla_falla_la_operacion_sin_fecha_supuesta()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetAccountingPeriodStatusQuery>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("base caída"));
        var fecha = new FechaContable(new ContabilidadParaCartera(sender), _alertas);

        var calcular = () => fecha.CalcularAsync(Valor, default);

        await calcular.Should().ThrowAsync<InvalidOperationException>("Principio IX: sin fecha supuesta, la operación falla entera");
    }

    // --------------------------------------------------------------------------------------- mitad de Cartera (US9, T0675, T0711) --

    /// <summary>Un cierre de Cartera del mes en la base de pruebas; reabierto si <paramref name="reabierto"/>.</summary>
    private static MonthlyClose Cierre(TestApplicationDbContext db, int anio, int mes, MonthlyCloseKind clase = MonthlyCloseKind.Monthly, bool reabierto = false)
    {
        var fin = MonthlyClose.UltimoDiaDe(anio, mes);
        var cierre = new MonthlyClose
        {
            Kind = clase, PeriodYear = (short)anio, PeriodMonth = (byte)mes, Version = 1, CloseDate = fin, InformationCutoffAt = DateTime.UtcNow,
            DocumentsUsedJson = "[]", ModelBySegmentJson = "[]", TotalsJson = "{}", LinesSha256 = new string('0', 64), OrderedAt = DateTime.UtcNow,
            ClosedAt = DateTime.UtcNow,
        };
        db.MonthlyCloses.Add(cierre);
        db.SaveChanges();
        if (reabierto)
        {
            db.MonthlyCloseReopenings.Add(new MonthlyCloseReopening { MonthlyCloseId = cierre.Id, ProcessRunId = 1, Reason = "Ajuste", ReopenedByUserId = 1, ReopenedAt = DateTime.UtcNow });
            db.SaveChanges();
        }
        return cierre;
    }

    [Fact]
    public async Task Un_mes_que_Cartera_ya_cerro_se_salta_al_primer_dia_del_siguiente_sin_cierre()
    {
        Estado(true, EstadoDePeriodoContable.Open);
        using var db = TestDbContextFactory.Create();
        Cierre(db, 2026, 12);
        Cierre(db, 2027, 1);

        var fecha = await new FechaContable(_contabilidad, _alertas, db).CalcularAsync(Valor, default);

        fecha.Value.Should().Be(new DateOnly(2027, 2, 1), "diciembre y enero tienen cierre de Cartera vigente (T36, FR-078)");
    }

    [Fact]
    public async Task Un_cierre_reabierto_o_el_base_no_cuentan()
    {
        Estado(true, EstadoDePeriodoContable.Open);
        using var db = TestDbContextFactory.Create();
        Cierre(db, 2026, 12, reabierto: true);
        Cierre(db, 2026, 12, MonthlyCloseKind.Base);

        (await new FechaContable(_contabilidad, _alertas, db).CalcularAsync(Valor, default)).Value.Should().Be(Valor);
    }

    [Fact]
    public async Task La_causacion_y_los_hechos_del_cierre_no_saltan_el_mes_cerrado_por_Cartera()
    {
        Estado(true, EstadoDePeriodoContable.Open);
        using var db = TestDbContextFactory.Create();
        Cierre(db, 2026, 12);

        (await new FechaContable(_contabilidad, _alertas, db).CalcularAsync(Valor, conCierreDeCartera: false, default)).Value.Should().Be(Valor);
    }

    [Fact]
    public async Task Si_Contabilidad_lleva_la_fecha_a_un_mes_que_Cartera_cerro_se_vuelve_a_mirar()
    {
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var f = ci.Arg<DateOnly>();
            return f.Month == 12 && f.Year == 2026
                ? Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Closed, new DateOnly(2027, 1, 1)))
                : Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, f));
        });
        using var db = TestDbContextFactory.Create();
        Cierre(db, 2027, 1);

        (await new FechaContable(_contabilidad, _alertas, db).CalcularAsync(Valor, default)).Value.Should().Be(new DateOnly(2027, 2, 1));
    }
}
