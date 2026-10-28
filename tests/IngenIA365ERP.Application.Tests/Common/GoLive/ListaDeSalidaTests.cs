using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.GoLive;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Common.GoLive;

/// <summary>
/// Feature 015, T0014 (R-28; FR-227; data-model §16.2): la lista de go/no-go por salida, con condiciones <c>Automatic</c> que aporta cada
/// <see cref="IVerificacionDeSalida"/> y <c>Manual</c> con evidencia. Un rojo nunca se oculta: decidir <c>Go</c> con un rojo exige la excepción
/// aprobada (<c>Platform.GoLive.Override</c>) con motivo; <c>PartialGo</c> exige el escenario adjunto; los rojos levantan
/// <c>Plataforma.GoLiveEnRojo</c>.
/// </summary>
public class ListaDeSalidaTests
{
    private sealed class VerificacionFija(string modulo, params ResultadoDeVerificacion[] resultados) : IVerificacionDeSalida
    {
        public ResultadoDeVerificacion[] Resultados { get; set; } = resultados;
        public string Modulo { get; } = modulo;
        public Task<IReadOnlyList<ResultadoDeVerificacion>> VerificarAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ResultadoDeVerificacion>>(Resultados);
    }

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly VerificacionFija _recaudo = new("REC",
        new ResultadoDeVerificacion("CAJEROS", "Hay cajero y supervisor con sus permisos", true, "2 cajeros, 1 supervisor"),
        new ResultadoDeVerificacion("MATRIZ", "La matriz contable está completa", false, "Faltan 3 combinaciones"));

    public ListaDeSalidaTests()
    {
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 28, 15, 0, 0, DateTimeKind.Utc));
        _actor.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(new Actor(ActorKind.Person, 5, Guid.NewGuid(), Guid.NewGuid(), "Dueño", null, ExecutionChannel.Web, "pruebas", null, null));
        _alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
    }

    private async Task<GoLiveChecklistDto> CrearAsync()
    {
        var r = await new CreateGoLiveChecklistCommandHandler(_db, [_recaudo], _actor, _alertas, _reloj).Handle(
            new CreateGoLiveChecklistCommand("Salida COOFLOPAL", "Producción", new DateOnly(2026, 12, 1),
                [new CondicionManual("Plataforma", "NO-GO-PARCIAL", "El escenario de no-go parcial está escrito (P15)")]),
            CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value;
    }

    private Task<Result<GoLiveChecklistDto>> DecidirAsync(Guid lista, GoLiveChecklistStatus decision, Guid? escenario = null) =>
        new DecideGoLiveCommandHandler(_db, _actor, _alertas, _reloj).Handle(new DecideGoLiveCommand(lista, decision, "Revisada en comité", escenario), CancellationToken.None);

    /// <summary>Segunda ronda del 2026-10-27 (D-h, T0494): el código es único por módulo; el mismo código en dos módulos es válido y dos veces en uno se rechaza.</summary>
    [Fact]
    public async Task El_codigo_es_unico_por_modulo_no_en_toda_la_lista()
    {
        var handler = new CreateGoLiveChecklistCommandHandler(_db, [_recaudo], _actor, _alertas, _reloj);

        var dosModulos = await handler.Handle(new CreateGoLiveChecklistCommand("Salida", "QA", new DateOnly(2026, 12, 1),
            [new CondicionManual("TES", "CAJEROS", "Tesorería tiene sus personas")]), CancellationToken.None);
        dosModulos.IsSuccess.Should().BeTrue(dosModulos.IsFailure ? dosModulos.Error.Code : null);
        dosModulos.Value.Items.Count(i => i.Code == "CAJEROS").Should().Be(2);

        var mismoModulo = await handler.Handle(new CreateGoLiveChecklistCommand("Salida 2", "QA", new DateOnly(2026, 12, 1),
            [new CondicionManual("TES", "FIRMAS", "Firmantes"), new CondicionManual("TES", "FIRMAS", "Repetida en Tesorería")]), CancellationToken.None);
        mismoModulo.Error.Code.Should().Be(ErroresDeGoLive.CodigoCodigoRepetido);
    }

    [Fact]
    public async Task La_lista_trae_las_automaticas_de_los_modulos_y_las_manuales_en_rojo_sin_ocultarlas()
    {
        var lista = await CrearAsync();

        lista.Items.Should().HaveCount(3);
        lista.Items.Single(i => i.Code == "CAJEROS").Status.Should().Be(GoLiveItemStatus.Green);
        lista.Items.Single(i => i.Code == "MATRIZ").Should().Match<GoLiveChecklistItemDto>(i => i.Status == GoLiveItemStatus.Red && i.Kind == GoLiveItemKind.Automatic);
        lista.Items.Single(i => i.Code == "NO-GO-PARCIAL").Should().Match<GoLiveChecklistItemDto>(i => i.Status == GoLiveItemStatus.Red && i.Kind == GoLiveItemKind.Manual);
        lista.RedWithoutOverride.Should().Be(2);
        await _alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.PlataformaGoLiveEnRojo), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Go_con_un_rojo_sin_excepcion_no_se_admite()
    {
        var lista = await CrearAsync();

        var r = await DecidirAsync(lista.PublicId, GoLiveChecklistStatus.Go);

        r.Error.Code.Should().Be(ErroresDeGoLive.CodigoRojoSinExcepcion);
    }

    [Fact]
    public async Task Una_manual_se_marca_con_evidencia_y_un_rojo_se_acepta_con_motivo_para_decidir_Go()
    {
        var lista = await CrearAsync();
        var manual = lista.Items.Single(i => i.Code == "NO-GO-PARCIAL").PublicId;
        var matriz = lista.Items.Single(i => i.Code == "MATRIZ").PublicId;

        var sinEvidencia = await new CheckGoLiveItemCommandHandler(_db, [_recaudo], _actor, _alertas, _reloj)
            .Handle(new CheckGoLiveItemCommand(lista.PublicId, manual, GoLiveItemStatus.Green, null, null), CancellationToken.None);
        sinEvidencia.Error.Code.Should().Be(ErroresDeGoLive.CodigoEvidenciaRequerida);

        await new CheckGoLiveItemCommandHandler(_db, [_recaudo], _actor, _alertas, _reloj)
            .Handle(new CheckGoLiveItemCommand(lista.PublicId, manual, GoLiveItemStatus.Green, "Acta del 27/11", null), CancellationToken.None);
        var excepcion = await new OverrideGoLiveItemCommandHandler(_db, _actor, _reloj)
            .Handle(new OverrideGoLiveItemCommand(lista.PublicId, matriz, "La contadora completa la matriz el 02/12"), CancellationToken.None);
        excepcion.Value.Items.Single(i => i.Code == "MATRIZ").Status.Should().Be(GoLiveItemStatus.Red, "el rojo no se oculta: se acepta");

        var go = await DecidirAsync(lista.PublicId, GoLiveChecklistStatus.Go);
        go.Value.Status.Should().Be(GoLiveChecklistStatus.Go);
        (await DecidirAsync(lista.PublicId, GoLiveChecklistStatus.NoGo)).Error.Code.Should().Be(ErroresDeGoLive.CodigoDecidida);
    }

    [Fact]
    public async Task La_automatica_se_recalcula_con_el_modulo()
    {
        var lista = await CrearAsync();
        _recaudo.Resultados = [new ResultadoDeVerificacion("MATRIZ", "La matriz contable está completa", true, "Completa")];

        var r = await new CheckGoLiveItemCommandHandler(_db, [_recaudo], _actor, _alertas, _reloj)
            .Handle(new CheckGoLiveItemCommand(lista.PublicId, lista.Items.Single(i => i.Code == "MATRIZ").PublicId, null, null, null), CancellationToken.None);

        r.Value.Items.Single(i => i.Code == "MATRIZ").Status.Should().Be(GoLiveItemStatus.Green);
    }

    [Fact]
    public async Task Una_salida_parcial_exige_el_escenario_adjunto()
    {
        var lista = await CrearAsync();

        (await DecidirAsync(lista.PublicId, GoLiveChecklistStatus.PartialGo)).Error.Code.Should().Be(ErroresDeGoLive.CodigoEscenarioRequerido);
        (await DecidirAsync(lista.PublicId, GoLiveChecklistStatus.PartialGo, Guid.NewGuid())).Value.Status.Should().Be(GoLiveChecklistStatus.PartialGo);
    }

    [Fact]
    public async Task Una_excepcion_solo_se_aprueba_sobre_un_rojo()
    {
        var lista = await CrearAsync();
        var verde = lista.Items.Single(i => i.Code == "CAJEROS").PublicId;

        var r = await new OverrideGoLiveItemCommandHandler(_db, _actor, _reloj)
            .Handle(new OverrideGoLiveItemCommand(lista.PublicId, verde, "por si acaso"), CancellationToken.None);

        r.Error.Code.Should().Be(ErroresDeGoLive.CodigoNoEstaEnRojo);
    }
}
