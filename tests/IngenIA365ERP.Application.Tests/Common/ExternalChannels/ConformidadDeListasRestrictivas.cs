using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.RestrictiveLists;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.ExternalChannels;

/// <summary>
/// La batería reutilizable de conformidad de las listas restrictivas (feature 013, T0031; T70, T71; contracts/adaptadores-y-archivos.md §3.1,
/// §3.9; DT §7.6), <c>ConformidadDeListasRestrictivas</c> (fijada allí). Cada canal provee una consulta sin coincidencia, una con coincidencia
/// por documento y una posible por nombre. Una consulta no escribe nada: las reglas de §3.1 que le aplican son no lanzar, el dato inválido,
/// el código sellado sin adaptador y la bandera de simulado. (nuevo)
/// </summary>
public abstract class ConformidadDeListasRestrictivas
{
    protected static readonly Guid Cooperativa = Guid.Parse("3f2a1b4c-0000-4000-8000-00000000c091");

    protected abstract IListasRestrictivas Canal { get; }

    protected abstract bool EsSimulado { get; }

    protected abstract ConsultaDeListas SinCoincidencia { get; }

    protected abstract ConsultaDeListas CoincidenciaPorDocumento { get; }

    protected abstract ConsultaDeListas PosibleCoincidenciaPorNombre { get; }

    protected ContextoDeCanalExterno Contexto() => new(Cooperativa, Canal.ChannelCode, null, null, Guid.NewGuid().ToString("N"), true);

    [Fact]
    public async Task Sin_coincidencia_es_NoMatch()
    {
        var r = await Canal.ConsultarAsync(SinCoincidencia, Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, r.Outcome);
        Assert.Equal(ScreeningOutcome.NoMatch, r.Valor!.Resultado);
        Assert.Empty(r.Valor.Coincidencias);
    }

    [Fact]
    public async Task La_coincidencia_por_documento_es_Match()
    {
        var r = await Canal.ConsultarAsync(CoincidenciaPorDocumento, Contexto(), default);

        Assert.Equal(ScreeningOutcome.Match, r.Valor!.Resultado);
        Assert.Contains(r.Valor.Coincidencias, c => c.PorDocumento);
        Assert.NotEmpty(r.Valor.Versiones);
    }

    [Fact]
    public async Task La_posible_por_nombre_es_PossibleMatch()
    {
        var r = await Canal.ConsultarAsync(PosibleCoincidenciaPorNombre, Contexto(), default);

        Assert.Equal(ScreeningOutcome.PossibleMatch, r.Valor!.Resultado);
        Assert.All(r.Valor.Coincidencias, c => Assert.False(c.PorDocumento));
    }

    [Fact]
    public async Task Regla_1_una_consulta_vacia_es_InvalidData_sin_lanzar()
    {
        var r = await Canal.ConsultarAsync(new ConsultaDeListas(Guid.NewGuid(), "CC", "", ""), Contexto(), default);

        Assert.Equal(ExternalOutcome.InvalidData, r.Outcome);
    }

    [Fact]
    public async Task Regla_7_un_codigo_sellado_sin_adaptador_responde_ChannelUnavailable()
    {
        var registro = new ProveedoresDeListas([Canal], new CircuitoSiempreCerrado());

        var r = await registro.Resolver("PROVEEDOR-QUE-NO-ESTA").ConsultarAsync(SinCoincidencia, Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
        Assert.Contains("no está instalado", r.Detalle, StringComparison.Ordinal);
    }

    [Fact]
    public void Regla_6_dos_adaptadores_con_el_mismo_codigo_no_arrancan()
    {
        Assert.Throws<InvalidOperationException>(() => new ProveedoresDeListas([Canal, Canal], new CircuitoSiempreCerrado()));
    }

    [Fact]
    public async Task Todo_resultado_trae_el_ChannelCode_y_Simulado_solo_en_el_simulado()
    {
        foreach (var consulta in new[] { SinCoincidencia, CoincidenciaPorDocumento, PosibleCoincidenciaPorNombre })
        {
            var r = await Canal.ConsultarAsync(consulta, Contexto(), default);
            Assert.Equal(Canal.ChannelCode, r.ChannelCode);
            Assert.Equal(EsSimulado, r.Simulado);
        }
        var prueba = await Canal.ProbarAsync(Contexto(), default);
        Assert.Equal(ExternalOutcome.Ok, prueba.Outcome);
        Assert.Equal(EsSimulado, prueba.Simulado);
    }
}

/// <summary>
/// <c>ListasSimuladas</c> cumple la batería (feature 013, T0031) y la tabla de DT §2.22: 8 <c>PossibleMatch</c>, 9 <c>Match</c>, cualquier
/// otro <c>NoMatch</c>. (nuevo)
/// </summary>
public sealed class ListasSimuladasConformidadTests : ConformidadDeListasRestrictivas
{
    private static ConsultaDeListas Consulta(string documento) => new(Guid.NewGuid(), "CC", documento, "Ana María Pérez");

    protected override IListasRestrictivas Canal { get; } = new ListasSimuladas(Options.Create(new ExternalChannelsOptions()));

    protected override bool EsSimulado => true;

    protected override ConsultaDeListas SinCoincidencia => Consulta("52000001");

    protected override ConsultaDeListas CoincidenciaPorDocumento => Consulta("52000009");

    protected override ConsultaDeListas PosibleCoincidenciaPorNombre => Consulta("52000008");

    [Theory]
    [InlineData("1", ScreeningOutcome.NoMatch)]
    [InlineData("2", ScreeningOutcome.NoMatch)]
    [InlineData("3", ScreeningOutcome.NoMatch)]
    [InlineData("5", ScreeningOutcome.NoMatch)]
    [InlineData("8", ScreeningOutcome.PossibleMatch)]
    [InlineData("9", ScreeningOutcome.Match)]
    [InlineData("0", ScreeningOutcome.NoMatch)]
    public async Task El_ultimo_digito_del_documento_decide(string digito, ScreeningOutcome esperado)
    {
        var r = await Canal.ConsultarAsync(Consulta("1020304" + digito), Contexto(), default);

        Assert.Equal(ExternalOutcome.Ok, r.Outcome);
        Assert.Equal(esperado, r.Valor!.Resultado);
    }

    [Fact]
    public async Task Sin_respuesta_a_tiempo_es_ChannelUnavailable()
    {
        var opciones = new ExternalChannelsOptions();
        opciones.Channels[CodigosDeCanalExterno.Simulado] = new OpcionesDeCanalExterno { TotalTimeoutSeconds = 1, DelayMilliseconds = 1_200 };
        var lento = new ListasSimuladas(Options.Create(opciones));

        var r = await lento.ConsultarAsync(Consulta("52000009"), Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
    }
}

/// <summary>
/// <c>CanalDeListasCargadas</c> (<c>CARGA-PROPIA</c>) cumple la batería sobre una carga en memoria (feature 013, T0031; data-model §4.8):
/// documento normalizado exacto → <c>Match</c>; nombre (o alias) sobre el umbral <c>Listas.UmbralDeCoincidenciaDeNombre</c> →
/// <c>PossibleMatch</c>; no es simulado. (nuevo)
/// </summary>
public sealed class CanalDeListasCargadasConformidadTests : ConformidadDeListasRestrictivas
{
    private static readonly VersionDeListaConsultada Ofac = new("OFAC", 3, new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 30));
    private static readonly VersionDeListaConsultada Propia = new("PROPIA", 1, new DateOnly(2026, 1, 1), null);

    private static EntradaDeListaRestrictiva Entrada(VersionDeListaConsultada lista, string? documento, string nombre, params string[] alias) =>
        new(lista.ListCode, lista.Version, RestrictiveListEntityKind.Person, documento is null ? null : "CC",
            documento is null ? null : CoincidenciaDeNombres.NormalizarDocumento(documento), nombre, CoincidenciaDeNombres.Normalizar(nombre),
            alias.Select(CoincidenciaDeNombres.Normalizar).ToList(), "SDN");

    private sealed class CargaEnMemoria(CargaDeListasRestrictivas carga) : ICargaDeListasRestrictivas
    {
        public Task<CargaDeListasRestrictivas> LeerVigenteAsync(DateOnly fecha, CancellationToken ct) => Task.FromResult(carga);
    }

    private static readonly CargaDeListasRestrictivas Carga = new([Ofac, Propia],
    [
        Entrada(Ofac, "79.123.456", "Pedro Antonio Gómez Ruiz", "El Pájaro"),
        Entrada(Propia, null, "José Fernando Restrepo Díaz"),
    ]);

    private static CanalDeListasCargadas Crear(ICargaDeListasRestrictivas carga, decimal? umbral = null)
    {
        var parametros = Substitute.For<ILectorDeParametros>();
        parametros.LeerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<IngenIA365ERP.Domain.Enums.Parameters.ParameterScopeKind>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ValorDeParametro>("Parameters.KeyNotFound", "sin parámetro"));
        if (umbral is { } u)
        {
            var definicion = IngenIA365ERP.Domain.Lending.Parameters.ParametrosDeCartera.Definiciones
                .First(d => d.Clave == IngenIA365ERP.Domain.Lending.Parameters.ParametrosDeCartera.ListasUmbralDeCoincidenciaDeNombre);
            parametros.LeerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<IngenIA365ERP.Domain.Enums.Parameters.ParameterScopeKind>(),
                    Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new ValorDeParametro(definicion, u.ToString(System.Globalization.CultureInfo.InvariantCulture), u, null)));
        }
        var reloj = Substitute.For<IDateTimeService>();
        reloj.HoyLocal.Returns(new DateOnly(2026, 12, 5));
        return new CanalDeListasCargadas(carga, parametros, reloj, NullLogger<CanalDeListasCargadas>.Instance);
    }

    protected override IListasRestrictivas Canal { get; } = Crear(new CargaEnMemoria(Carga));

    protected override bool EsSimulado => false;

    protected override ConsultaDeListas SinCoincidencia => new(Guid.NewGuid(), "CC", "52000001", "Ana María Pérez");

    protected override ConsultaDeListas CoincidenciaPorDocumento => new(Guid.NewGuid(), "CC", "0079123456", "Otro Nombre Cualquiera");

    protected override ConsultaDeListas PosibleCoincidenciaPorNombre => new(Guid.NewGuid(), "CC", "11111111", "JOSE FERNANDO RESTREPO DIAZ");

    [Fact]
    public async Task El_nombre_en_otro_orden_y_sin_tildes_coincide_y_el_alias_tambien()
    {
        var porOrden = await Canal.ConsultarAsync(new ConsultaDeListas(Guid.NewGuid(), "CC", "1", "Restrepo Díaz, José Fernando"), Contexto(), default);
        var porAlias = await Canal.ConsultarAsync(new ConsultaDeListas(Guid.NewGuid(), "CC", "2", "el pajaro"), Contexto(), default);

        Assert.Equal(ScreeningOutcome.PossibleMatch, porOrden.Valor!.Resultado);
        Assert.Equal(ScreeningOutcome.PossibleMatch, porAlias.Valor!.Resultado);
        Assert.Equal("OFAC", porAlias.Valor.Coincidencias.Single().ListCode);
    }

    [Fact]
    public async Task El_umbral_sale_del_parametro_de_la_cooperativa()
    {
        var parecido = new ConsultaDeListas(Guid.NewGuid(), "CC", "3", "Jose Fernando Restrepo");

        var conDefecto = await Canal.ConsultarAsync(parecido, Contexto(), default);
        var laxo = await Crear(new CargaEnMemoria(Carga), umbral: 0.5m).ConsultarAsync(parecido, Contexto(), default);

        Assert.Equal(ScreeningOutcome.NoMatch, conDefecto.Valor!.Resultado);
        Assert.Equal(ScreeningOutcome.PossibleMatch, laxo.Valor!.Resultado);
    }

    [Fact]
    public async Task Informa_las_versiones_consultadas()
    {
        var r = await Canal.ConsultarAsync(SinCoincidencia, Contexto(), default);

        Assert.Equal(["OFAC", "PROPIA"], r.Valor!.Versiones.Select(v => v.ListCode));
    }

    [Fact]
    public async Task Sin_listas_cargadas_es_NoMatch_sin_versiones()
    {
        var r = await Crear(new SinCargaDeListasRestrictivas()).ConsultarAsync(CoincidenciaPorDocumento, Contexto(), default);

        Assert.Equal(ScreeningOutcome.NoMatch, r.Valor!.Resultado);
        Assert.Empty(r.Valor.Versiones);
    }

    [Fact]
    public async Task Una_carga_ilegible_es_ChannelUnavailable_sin_lanzar()
    {
        var rota = Substitute.For<ICargaDeListasRestrictivas>();
        rota.LeerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns<Task<CargaDeListasRestrictivas>>(_ => throw new InvalidOperationException("base caída"));

        var r = await Crear(rota).ConsultarAsync(SinCoincidencia, Contexto(), default);

        Assert.Equal(ExternalOutcome.ChannelUnavailable, r.Outcome);
        Assert.False(r.Simulado);
    }
}
