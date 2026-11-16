using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Replay;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 024, fase 1 (pedido del dueño del 2026-11-16): la tasa general de mora (<c>Mora.TasaGeneral</c>). El alta no admite vigencias con fecha
/// pasada ni en un día que la causación ya corrió (<c>Lending.Parameter.BackdatedNotAllowed</c> con la primera fecha admitida) y, como toda tasa, cabe
/// en seis decimales (<c>Lending.Rate.TooPrecise</c>); vacía se admite (vuelve la de la línea). La serie entera llega a la reconstrucción con lo conocido
/// en R, con la vigencia vacía como «sin tasa general», y la reconstrucción la usa para la mora de todos los créditos (el detalle lo fija el caso dorado
/// <c>35-tasa-general-de-mora</c>).
/// </summary>
public class TasaGeneralDeMoraTests
{
    private const string Clave = ParametrosDeCartera.MoraTasaGeneral;

    /// <summary>15:00 UTC = 10:00 en Colombia del 16-11-2026.</summary>
    private static readonly DateTime Ahora = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly Hoy = new(2026, 11, 16);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();

    public TasaGeneralDeMoraTests() => _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

    private sealed class Reloj : IDateTimeService
    {
        public DateTime UtcNow => Ahora;
        public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
    }

    private ReglasDeParametrosDeCartera Reglas() => new(new LimitesDePrueba(), new LectorDeParametros(_db), new Reloj(), _db);

    private Task<Result<AddParameterVersionResponse>> Registrar(string valor, DateOnly desde) =>
        new AddParameterVersionCommandHandler(_db, _permisos, new ResolutorDeAmbitoVacio(),
                new ReglasDeParametrosPorModulo(new ReglasDeParametrosVacias(), [Reglas()]))
            .Handle(new AddParameterVersionCommand(ParametrosDeCartera.Modulo, Clave, ParameterScopeKind.None, null, null, valor, desde,
                "Decisión del consejo", null) { OperationKey = Guid.NewGuid() }, CancellationToken.None);

    private static JsonElement Datos(Error error) => JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    private void Causado(DateOnly hasta)
    {
        _db.LoanControls.Add(new LoanControl { LoanId = 1, LastAccruedDate = hasta.AddDays(-3) });
        _db.LoanControls.Add(new LoanControl { LoanId = 2, LastAccruedDate = hasta });
        _db.SaveChanges();
    }

    private void Guardar(string valor, DateOnly desde, DateTime creada, DateOnly? hasta = null)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = Clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = desde, ValidTo = hasta,
            Reason = "prueba", CreatedAt = creada,
        });
        _db.SaveChanges();
    }

    [Fact]
    public void Es_una_clave_general_de_K1_que_admite_vacio_y_arranca_vacia()
    {
        var d = ParametrosDeCartera.Definiciones.Single(x => x.Clave == Clave);
        d.DefectoSeguro.Should().BeEmpty("de fábrica rige la mora de la línea");
        d.AdmiteVacio.Should().BeTrue();
        d.AmbitosAdmitidos.Should().Equal(ParameterScopeKind.None);
        d.DisponibleDesde.Should().Be((Domain.Common.Parametros.EntregaDeModulo)EntregaDeCartera.K1);
    }

    [Fact]
    public async Task No_admite_una_vigencia_con_fecha_pasada_y_dice_desde_cuando_se_puede()
    {
        var r = await Registrar("0.25", Hoy.AddDays(-1));

        r.Error.Code.Should().Be(ErroresDeParametrosDeCartera.CodigoVigenciaHaciaAtras);
        r.Error.Message.Should().Contain("fecha pasada").And.Contain("16/11/2026");
        Datos(r.Error).GetProperty("minimumValidFrom").GetString().Should().Be("2026-11-16");
        _db.ParameterVersions.Should().BeEmpty();

        (await Registrar("0.25", Hoy)).IsSuccess.Should().BeTrue("desde hoy sí, si la causación de hoy no ha corrido");
    }

    [Fact]
    public async Task No_admite_una_vigencia_en_un_dia_que_la_causacion_ya_corrio()
    {
        Causado(Hoy);

        var r = await Registrar("0.25", Hoy);

        r.Error.Code.Should().Be(ErroresDeParametrosDeCartera.CodigoVigenciaHaciaAtras);
        r.Error.Message.Should().Contain("ya corrió hasta el 16/11/2026");
        var datos = Datos(r.Error);
        datos.GetProperty("minimumValidFrom").GetString().Should().Be("2026-11-17");
        datos.GetProperty("lastAccruedDate").GetString().Should().Be("2026-11-16");
        (await Registrar("0.25", Hoy.AddDays(1))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Cabe_en_seis_decimales_como_toda_tasa_y_vacia_vuelve_a_la_de_la_linea()
    {
        (await Registrar("0.2512345", Hoy.AddDays(5))).Error.Code.Should().Be("Lending.Rate.TooPrecise");
        (await Registrar("0.251234", Hoy.AddDays(5))).IsSuccess.Should().BeTrue();
        (await Registrar("", Hoy.AddDays(20))).IsSuccess.Should().BeTrue("vacía: desde ese día rige otra vez la mora de la línea");
        (await Registrar("1.5", Hoy.AddDays(30))).Error.Code.Should().Be("Parameters.ValueNotAllowed", "es una fracción hasta 1");
    }

    [Fact]
    public async Task La_serie_trae_todas_las_vigencias_conocidas_en_R_con_la_vacia_como_sin_tasa_general()
    {
        Guardar("0.24", new DateOnly(2026, 12, 10), new DateTime(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc));
        Guardar("0.35", new DateOnly(2027, 1, 12), new DateTime(2027, 1, 2, 15, 0, 0, DateTimeKind.Utc));
        Guardar("", new DateOnly(2027, 2, 5), new DateTime(2027, 2, 1, 15, 0, 0, DateTimeKind.Utc));

        var todas = await Reglas().TasaGeneralDeMoraAsync(null, default);
        todas.Select(t => (t.ValidFrom, t.Ea)).Should().Equal(
            (new DateOnly(2026, 12, 10), 0.24m), (new DateOnly(2027, 1, 12), 0.35m), (new DateOnly(2027, 2, 5), (decimal?)null));
        todas.Select(t => t.VersionNumber).Should().Equal(1, 2, 3);

        var enEnero = await Reglas().TasaGeneralDeMoraAsync(new DateTime(2027, 1, 5, 0, 0, 0, DateTimeKind.Utc), default);
        enEnero.Select(t => t.ValidFrom).Should().Equal([new DateOnly(2026, 12, 10), new DateOnly(2027, 1, 12)],
            "la vacía se registró después de R: con lo que se sabía entonces, la de 35 % sigue abierta");
    }

    [Fact]
    public async Task Una_vigencia_cerrada_sin_sucesora_deja_de_regir_al_dia_siguiente()
    {
        Guardar("0.24", new DateOnly(2026, 12, 10), new DateTime(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc), hasta: new DateOnly(2027, 1, 31));

        var serie = await Reglas().TasaGeneralDeMoraAsync(null, default);

        serie.Select(t => (t.ValidFrom, t.Ea)).Should().Equal((new DateOnly(2026, 12, 10), 0.24m), (new DateOnly(2027, 2, 1), (decimal?)null));
    }

    [Fact]
    public void Las_series_de_la_modalidad_llevan_la_tasa_general_a_la_entrada_de_la_reconstruccion()
    {
        var general = new List<TasaGeneralDeMora> { new(new DateOnly(2026, 12, 10), 0.24m, Ahora, 1) };
        var series = new SeriesDeModalidad("CONSUMO", null, "CONSUMO", "CONSUMO", Hoy, Hoy.AddDays(60), [], [], [], [], []) { MoraGeneral = general };
        var entrada = new EntradaDeReconstruccion(
            new CondicionesDeCredito(VersionesDeReglas.Vigente, 0.18m, DefaultRateKind.RelativeToCurrent, DayCountConvention.Thirty360,
                RateEquivalence.Compound, RepaymentPeriodicity.Monthly, RedondeoDeCartera.AlCentavo) { FactorDeMora = 1.5m },
            null, [], Ahora);

        series.Aplicar(entrada).MoraGeneral.Should().BeEquivalentTo(general);
    }
}
