using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Parameters;

/// <summary>
/// Límites legales <c>LIMITE</c> de mentiras: por código, una lista de (desde, valor, fuente); sin valor a la fecha responde
/// <c>NationalValues.Missing</c>, como el lector real. (nuevo)
/// </summary>
public sealed class LimitesDePrueba : IValoresNacionales
{
    private readonly Dictionary<string, List<(DateOnly Desde, decimal Valor, string Fuente)>> _limites = new(StringComparer.Ordinal);

    public LimitesDePrueba Con(string codigo, decimal valor, DateOnly? desde = null, string fuente = "Norma de prueba")
    {
        if (!_limites.TryGetValue(codigo, out var lista)) _limites[codigo] = lista = [];
        lista.Add((desde ?? new DateOnly(2020, 1, 1), valor, fuente));
        return this;
    }

    public Task<Result<ValorNacionalVigente>> LeerAsync(string codigo, string clave, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        var vigente = _limites.GetValueOrDefault(clave)?.Where(l => l.Desde <= fecha).OrderByDescending(l => l.Desde).Cast<(DateOnly, decimal, string)?>().FirstOrDefault();
        if (codigo != CodigosDeValorNacional.Limite || vigente is not { } v)
            return Task.FromResult(Result.Failure<ValorNacionalVigente>(new Error("NationalValues.Missing", $"Falta {codigo} {clave} para el {fecha:yyyy-MM-dd}.")));
        return Task.FromResult(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), codigo, clave, v.Item2, null, null,
            v.Item1, null, null, v.Item3, 1, DateTime.UnixEpoch, EstadoDeRevision.PendienteDeValidar)));
    }

    public Task<Result<ValorNacionalVigente>> LeerUltimoPublicadoAsync(string codigo, string clave, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<SerieDeValorNacional> LeerSerieAsync(string codigo, string clave, DateOnly desde, DateOnly hasta, DateTime? informacionHasta = null, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

/// <summary>
/// T0027 (feature 013; decisiones-transversales T42, T43, C10; data-model parte 3 §12.1, §12.5; quickstart §1.5 «Parámetros»): las reglas
/// de los parámetros de Cartera (<c>ReglasDeParametrosDeCartera</c>), corridas por el alta de la plataforma
/// (<c>AddParameterVersionCommand</c>) a través de <c>ReglasDeParametrosPorModulo</c>. Una vigencia menos estricta que el límite
/// legal vigente en su <c>ValidFrom</c> es <c>Lending.Parameter.BelowLegalFloor</c> con el límite y su fuente; al leer rige lo más
/// estricto; <c>Contabilidad.DiferirComisiones = true</c> es <c>Lending.Parameter.NotSupportedYet</c>.
/// </summary>
public class ReglasDeParametrosDeCarteraTests
{
    private const string Lnd = ParametrosDeCartera.Modulo;
    private static readonly DateOnly Desde = new(2026, 11, 1);
    private static readonly DateOnly Hoy = new(2026, 12, 15);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly LimitesDePrueba _limites = new LimitesDePrueba()
        .Con(CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes, 7, fuente: "Ley 2300 de 2023")
        .Con(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 19, fuente: "Ley 2300 de 2023")
        .Con(CatalogoDeLimitesLegales.CobranzaContactosDia, 1, fuente: "Ley 2300 de 2023")
        .Con(CatalogoDeLimitesLegales.ConcentracionIndividual, 0.10m, fuente: "Decreto 961 de 2018")
        .Con(CatalogoDeLimitesLegales.MoraSobreInteresesPermitida, 0, fuente: "Código de Comercio");

    public ReglasDeParametrosDeCarteraTests() =>
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

    private ReglasDeParametrosDeCartera Reglas() => new(_limites, new LectorDeParametros(_db));

    private AddParameterVersionCommandHandler Alta() => new(_db, _permisos, new ResolutorDeAmbitoVacio(),
        new ReglasDeParametrosPorModulo(new ReglasDeParametrosVacias(), [Reglas()]));

    private Task<Result<AddParameterVersionResponse>> Registrar(string clave, string valor, string? fuente = "Acta 12 del consejo") =>
        Alta().Handle(new AddParameterVersionCommand(Lnd, clave, ParameterScopeKind.None, null, null, valor, Desde, "Política aprobada", fuente)
            { OperationKey = Guid.NewGuid() }, CancellationToken.None);

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    private void Guardar(string clave, string valor)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = Lnd, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = Desde, Reason = "prueba",
        });
        _db.SaveChanges();
    }

    // --------------------------------------------------------------------------------------------- al registrar --

    [Theory]
    [InlineData(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "06:30-19:00", CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes)]
    [InlineData(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "07:00-20:00", CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes)]
    [InlineData(ParametrosDeCartera.CobranzaContactosPorDia, "2", CatalogoDeLimitesLegales.CobranzaContactosDia)]
    [InlineData(ParametrosDeCartera.ConcentracionLimiteIndividual, "0.15", CatalogoDeLimitesLegales.ConcentracionIndividual)]
    [InlineData(ParametrosDeCartera.MoraSobreIntereses, "true", CatalogoDeLimitesLegales.MoraSobreInteresesPermitida)]
    public async Task Una_vigencia_menos_estricta_que_el_limite_es_BelowLegalFloor_con_el_limite_y_su_fuente(string clave, string valor, string limite)
    {
        var r = await Registrar(clave, valor);

        r.Error.Code.Should().Be(ErroresDeParametrosDeCartera.CodigoMenosEstrictoQueLaLey);
        var datos = Datos(r.Error);
        datos.GetProperty("limitCode").GetString().Should().Be(limite);
        datos.GetProperty("source").GetString().Should().NotBeNullOrWhiteSpace();
        r.Error.Message.Should().Contain(limite).And.Contain("sólo puede ser más estricta");
        (await _db.ParameterVersions.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "08:00-18:00")]
    [InlineData(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "07:00-19:00")]
    [InlineData(ParametrosDeCartera.CobranzaContactosPorDia, "1")]
    [InlineData(ParametrosDeCartera.ConcentracionLimiteIndividual, "0.08")]
    [InlineData(ParametrosDeCartera.ConcentracionLimiteIndividual, "")]
    [InlineData(ParametrosDeCartera.MoraSobreIntereses, "false")]
    public async Task Igual_o_mas_estricta_que_el_limite_se_registra(string clave, string valor)
    {
        var r = await Registrar(clave, valor);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        (await _db.ParameterVersions.SingleAsync()).Module.Should().Be(Lnd);
    }

    [Fact]
    public async Task Se_compara_con_el_limite_vigente_en_el_ValidFrom_de_la_vigencia()
    {
        // El límite se endurece a 1 de diciembre: una vigencia desde noviembre se juzga con el de noviembre.
        _limites.Con(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 18, new DateOnly(2026, 12, 1));

        (await Registrar(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "07:00-18:30")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_el_limite_replicado_no_se_registra()
    {
        var sinLimites = new AddParameterVersionCommandHandler(_db, _permisos, new ResolutorDeAmbitoVacio(),
            new ReglasDeParametrosPorModulo(new ReglasDeParametrosVacias(), [new ReglasDeParametrosDeCartera(new LimitesDePrueba(), new LectorDeParametros(_db))]));

        var r = await sinLimites.Handle(new AddParameterVersionCommand(Lnd, ParametrosDeCartera.CobranzaContactosPorDia, ParameterScopeKind.None,
            null, null, "1", Desde, "Política", "Acta 12") { OperationKey = Guid.NewGuid() }, CancellationToken.None);

        r.Error.Code.Should().Be("NationalValues.Missing");
    }

    [Fact]
    public async Task Diferir_comisiones_en_true_es_NotSupportedYet_y_false_se_acepta()
    {
        var si = await Registrar(ParametrosDeCartera.ContabilidadDiferirComisiones, "true", fuente: null);

        si.Error.Code.Should().Be(ErroresDeParametrosDeCartera.CodigoNoSoportadoTodavia);
        si.Error.Message.Should().Be("La cooperativa todavía no puede diferir comisiones y costos de originación: ninguna entrega lo implementa.");
        var datos = Datos(si.Error);
        datos.GetProperty("key").GetString().Should().Be(ParametrosDeCartera.ContabilidadDiferirComisiones);
        datos.GetProperty("availableFrom").ValueKind.Should().Be(JsonValueKind.Null);

        (await Registrar(ParametrosDeCartera.ContabilidadDiferirComisiones, "false", fuente: null)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Los_rangos_de_mora_de_las_centrales_exigen_su_regla_ademas_de_su_forma()
    {
        // Decisión del dueño del 2026-10-16: la forma la revisa el patrón; los límites crecientes y el rango negativo existente, la regla de Cartera.
        var mal = await Registrar(ParametrosDeCartera.CentralesRangosDeMora, "*=60,30|1", fuente: null);

        mal.Error.Code.Should().Be(ErroresDeParametrosDeCartera.CodigoRangosDeMoraInvalidos);
        mal.Error.Message.Should().Contain("orden creciente");
        Datos(mal.Error).GetProperty("key").GetString().Should().Be(ParametrosDeCartera.CentralesRangosDeMora);
        (await Registrar(ParametrosDeCartera.CentralesRangosDeMora, "*=30,60|4", fuente: null)).Error.Code
            .Should().Be(ErroresDeParametrosDeCartera.CodigoRangosDeMoraInvalidos);

        (await Registrar(ParametrosDeCartera.CentralesRangosDeMora, "*=30,60,90|2;VIVIENDA=60,120|1", fuente: null)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Las_reglas_de_Cartera_no_corren_para_otros_modulos_ni_las_de_la_plataforma_para_Cartera()
    {
        var plataforma = Substitute.For<IReglasDeParametros>();
        plataforma.EvaluarAsync(Arg.Any<AltaDeParametro>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DecisionDeReglasDeParametro>(new Error("Parameters.ValidFromInClosedPeriod", "período de inventario cerrado")));
        var reglas = new ReglasDeParametrosPorModulo(plataforma, [Reglas()]);
        var handler = new AddParameterVersionCommandHandler(_db, _permisos, new ResolutorDeAmbitoVacio(), reglas);

        var cartera = await handler.Handle(new AddParameterVersionCommand(Lnd, ParametrosDeCartera.MensajesReintentos, ParameterScopeKind.None,
            null, null, "4", Desde, "Política", null) { OperationKey = Guid.NewGuid() }, CancellationToken.None);
        var inventario = await handler.Handle(new AddParameterVersionCommand("INV", "Existencias.StockNegativoPermitido", ParameterScopeKind.None,
            null, null, "true", Desde, "Política", null) { OperationKey = Guid.NewGuid() }, CancellationToken.None);

        cartera.IsSuccess.Should().BeTrue("el período de inventario cerrado no es asunto de Cartera");
        inventario.Error.Code.Should().Be("Parameters.ValidFromInClosedPeriod");
    }

    // ------------------------------------------------------------------------------------------------- al leer --

    [Fact]
    public async Task Al_leer_rige_lo_mas_estricto_entre_la_cooperativa_y_el_limite_vigente()
    {
        // Guardado antes de que la norma se endureciera (la réplica no reescribe parámetros, alerta).
        Guardar(ParametrosDeCartera.CobranzaContactosPorDia, "3");

        var r = await Reglas().LeerAsync(ParametrosDeCartera.CobranzaContactosPorDia, Hoy);

        r.Value.Como<int>().Should().Be(1);
        r.Value.DeLaCooperativa.Como<int>().Should().Be(3);
        r.Value.RigeLaLey.Should().BeTrue();
        r.Value.Limites.Should().ContainSingle().Which.Fuente.Should().Be("Ley 2300 de 2023");
    }

    [Fact]
    public async Task Si_la_cooperativa_es_mas_estricta_rige_la_cooperativa()
    {
        Guardar(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "08:00-17:30");

        var r = await Reglas().LeerAsync(ParametrosDeCartera.CobranzaHorarioLunesAViernes, Hoy);

        r.Value.Texto.Should().Be("08:00-17:30");
        r.Value.RigeLaLey.Should().BeFalse();
    }

    [Fact]
    public async Task Un_horario_se_recorta_por_la_punta_que_cada_limite_acota()
    {
        Guardar(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "07:30-19:00");
        _limites.Con(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 17.5m, new DateOnly(2026, 12, 1));

        var r = await Reglas().LeerAsync(ParametrosDeCartera.CobranzaHorarioLunesAViernes, Hoy);

        r.Value.Texto.Should().Be("07:30-17:30");
    }

    [Fact]
    public async Task Vacio_significa_que_rige_el_limite()
    {
        var r = await Reglas().LeerAsync(ParametrosDeCartera.ConcentracionLimiteIndividual, Hoy);

        r.Value.DeLaCooperativa.EsDefecto.Should().BeTrue();
        r.Value.Como<decimal>().Should().Be(0.10m);
        r.Value.RigeLaLey.Should().BeTrue();
    }

    [Fact]
    public async Task Una_clave_sin_limite_se_lee_tal_cual()
    {
        var r = await Reglas().LeerAsync(ParametrosDeCartera.BloqueosDiasDeMoraParaBloquear, Hoy);

        r.Value.Como<int>().Should().Be(30);
        r.Value.Limites.Should().BeEmpty();
    }
}
