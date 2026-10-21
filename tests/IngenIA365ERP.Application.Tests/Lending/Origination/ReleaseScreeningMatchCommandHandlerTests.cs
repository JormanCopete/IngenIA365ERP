using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using ResultadoDeLaLista = IngenIA365ERP.Domain.Enums.Compliance.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0376 (quickstart §1.5; T70, C84, C89) y la consulta de listas de Cartera (T0403). Liberar exige
/// <c>Compliance.RestrictiveLists.Release</c> y motivo; quien radicó la solicitud o pidió el cupo → <c>Lending.Segregation.Conflict</c>; un resultado
/// sin coincidencia pendiente → <c>Lending.Screening.NotPending</c>; liberar escribe la liberación una sola vez, levanta el bloqueo
/// <c>RestrictiveListMatch</c> y deja <c>Lending.ScreeningMatch.Released</c>; la lista y el detalle exigen el mismo permiso. La consulta agrega el
/// resultado sin guardar, con el canal sellado; una coincidencia bloquea y alerta; <c>Unavailable</c> y el simulado en producción no cuentan; una
/// coincidencia ya liberada con las mismas versiones nace liberada.
/// </summary>
public sealed class ReleaseScreeningMatchCommandHandlerTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;
    private readonly ListasDePrueba _listas = new();
    private User _oficial = null!;

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync();
        _oficial = _o.UsuarioConPersona("Olga", "9009009");
        _listas.PorDocumento[_o.Ana.TaxId!] = ResultadoDeLaLista.Match;
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private async Task<ScreeningResult> CoincidenciaDeSolicitudAsync()
    {
        var s = await _o.RadicadaAsync();
        var id = await _o.Db.CreditApplications.Where(a => a.PublicId == s.PublicId).Select(a => a.Id).SingleAsync();
        var r = await _listas.ConsultaReal(_o).ConsultarAsync(new PedidoDeConsultaDeListas(_o.Ana.Id, ScreeningContext.Application, id), default);
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
        return r.Resultado;
    }

    private void ComoOficial(User? quien = null)
    {
        _o.G.ActuarComo(quien ?? _oficial);
        _o.G.Conceder("Compliance.RestrictiveLists.Release");
    }

    private Task<Result<ScreeningResultDto>> LiberarAsync(Guid resultado, string motivo = "Homónimo: el documento no coincide") =>
        new ReleaseScreeningMatchCommandHandler(_o.Db, _o.G.Permisos, _o.G.Actor, _o.G.Auditoria, _o.G.Reloj)
            .Handle(new ReleaseScreeningMatchCommand(resultado, motivo) { OperationKey = Guid.NewGuid() }, default);

    // ------------------------------------------------------------------------------------------------------- liberar --

    [Fact]
    public async Task Sin_el_permiso_del_oficial_es_el_404()
    {
        var r = await CoincidenciaDeSolicitudAsync();
        _o.G.ActuarComo(_oficial);
        _o.G.Conceder("Lending.Applications.Read");

        (await LiberarAsync(r.PublicId)).Error.Code.Should().Be("Lending.ScreeningResult.NotFound");
    }

    [Fact]
    public async Task Quien_radico_no_libera()
    {
        var r = await CoincidenciaDeSolicitudAsync();
        ComoOficial(_o.Asesora);

        (await LiberarAsync(r.PublicId)).Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task Quien_pidio_el_cupo_no_libera()
    {
        _o.G.ActuarComo(_o.Asesora);
        var cupo = await _o.Asignar().Handle(new AssignCreditLimitCommand(_o.Beto.PublicId, CreditLimitKind.Consolidated, null, CreditLimitOrigin.Assigned,
            1_000_000m, OriginacionDePrueba.Hoy, null, null, "Cupo del asociado") { OperationKey = Guid.NewGuid() }, default);
        _listas.PorDocumento[_o.Beto.TaxId!] = ResultadoDeLaLista.PossibleMatch;
        var id = await _o.Db.CreditLimits.Where(c => c.PublicId == cupo.Value.PublicId).Select(c => c.Id).SingleAsync();
        var r = await _listas.ConsultaReal(_o).ConsultarAsync(new PedidoDeConsultaDeListas(_o.Beto.Id, ScreeningContext.CreditLimit, CreditLimitId: id), default);
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
        ComoOficial(_o.Asesora);

        (await LiberarAsync(r.Resultado.PublicId)).Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task Liberar_escribe_la_liberacion_levanta_el_bloqueo_y_queda_auditado()
    {
        var r = await CoincidenciaDeSolicitudAsync();
        (await _o.Db.DebtorBlocks.SingleAsync()).Should().Match<DebtorBlock>(b =>
            b.Reason == DebtorBlockReason.RestrictiveListMatch && b.IsAutomatic && b.SourceKey == r.PublicId.ToString() && b.LiftedAt == null);
        ComoOficial();

        var liberado = await LiberarAsync(r.PublicId);

        liberado.IsSuccess.Should().BeTrue(liberado.IsFailure ? $"{liberado.Error.Code}: {liberado.Error.Message}" : string.Empty);
        liberado.Value.Status.Should().Be(EstadosDeListas.Liberado);
        var fila = await _o.Db.ScreeningResults.AsNoTracking().SingleAsync(x => x.PublicId == r.PublicId);
        fila.ReleasedAt.Should().Be(_o.G.UtcNow);
        fila.ReleasedByUserId.Should().Be(_oficial.Id);
        fila.ReleaseReason.Should().Be("Homónimo: el documento no coincide");
        var bloqueo = await _o.Db.DebtorBlocks.AsNoTracking().SingleAsync();
        bloqueo.LiftedAt.Should().NotBeNull();
        bloqueo.LiftedByUserId.Should().Be(_oficial.Id);
        (await _o.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingScreeningMatchReleased);
    }

    [Fact]
    public async Task Lo_que_no_tiene_coincidencia_pendiente_no_se_libera()
    {
        var r = await CoincidenciaDeSolicitudAsync();
        ComoOficial();
        (await LiberarAsync(r.PublicId)).IsSuccess.Should().BeTrue();
        _o.Db.ChangeTracker.Clear();

        (await LiberarAsync(r.PublicId)).Error.Code.Should().Be("Lending.Screening.NotPending", "la liberación se escribe una sola vez");
        var sinCoincidencia = await _o.Db.ScreeningResults.AsNoTracking().FirstAsync(x => x.Outcome == ScreeningOutcome.NoMatch);
        (await LiberarAsync(sinCoincidencia.PublicId)).Error.Code.Should().Be("Lending.Screening.NotPending");
    }

    [Fact]
    public async Task La_lista_y_el_detalle_exigen_el_permiso_del_oficial()
    {
        var r = await CoincidenciaDeSolicitudAsync();
        _o.G.ActuarComo(_oficial);
        _o.G.Conceder("Lending.Applications.Read");
        var lista = new ListScreeningResultsQueryHandler(_o.Db, _o.G.Permisos);
        var detalle = new GetScreeningResultQueryHandler(_o.Db, _o.G.Permisos);

        (await lista.Handle(new ListScreeningResultsQuery(), default)).Error.Code.Should().Be("Generic.NotFound");
        (await detalle.Handle(new GetScreeningResultQuery(r.PublicId), default)).Error.Code.Should().Be("Lending.ScreeningResult.NotFound");

        ComoOficial();
        var bloqueados = (await lista.Handle(new ListScreeningResultsQuery(EstadosDeListas.Bloqueado), default)).Value.Items;
        bloqueados.Should().ContainSingle().Which.Should().Match<ScreeningResultDto>(x =>
            x.PublicId == r.PublicId && x.Outcome == ScreeningOutcome.Match && x.Person.DocumentNumber == _o.Ana.TaxId && x.ChannelCode == "CARGA-PROPIA");
        var visto = (await detalle.Handle(new GetScreeningResultQuery(r.PublicId), default)).Value;
        visto.Matches.Should().ContainSingle().Which.ByDocument.Should().BeTrue();
        visto.ListLoads.Should().ContainSingle().Which.Should().Be(new ScreeningListLoadDto("OFAC", 3));
    }

    // ----------------------------------------------------------------------------------------------- la consulta (T0403) --

    [Fact]
    public async Task La_consulta_no_guarda_sella_el_canal_bloquea_y_alerta()
    {
        var r = await _listas.ConsultaReal(_o).ConsultarAsync(new PedidoDeConsultaDeListas(_o.Ana.Id, ScreeningContext.CreditLimit), default);

        r.Bloquea.Should().BeTrue();
        r.CuentaComoConsulta.Should().BeTrue();
        _o.Db.Entry(r.Resultado).State.Should().Be(EntityState.Added, "agrega el resultado a la unidad de trabajo sin guardar");
        r.Resultado.ChannelCode.Should().Be("CARGA-PROPIA");
        r.Resultado.ListVersions.Should().Contain("OFAC");
        _o.Db.DebtorBlocks.Local.Should().ContainSingle(b => b.Reason == DebtorBlockReason.RestrictiveListMatch);
        _o.L.Levantadas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.CoincidenciaEnListas);
    }

    [Fact]
    public async Task Un_canal_caido_y_el_simulado_en_produccion_no_cuentan_como_consulta()
    {
        _listas.Salida = ExternalOutcome.ChannelUnavailable;
        var caida = await _listas.ConsultaReal(_o).ConsultarAsync(new PedidoDeConsultaDeListas(_o.Ana.Id, ScreeningContext.CreditLimit), default);
        caida.Resultado.Outcome.Should().Be(ScreeningOutcome.Unavailable);
        caida.CuentaComoConsulta.Should().BeFalse();
        caida.Bloquea.Should().BeFalse();

        _o.Opciones.AmbienteDePruebas = false;
        _o.Db.ExternalChannelSettings.Add(new Domain.Entities.Core.ExternalChannelSetting
        {
            Kind = Domain.Enums.ExternalChannels.ExternalChannelKind.RestrictiveLists, ChannelCode = "SIMULADO", ValidFrom = new DateOnly(2026, 1, 1),
            SettingKey = Domain.Entities.Core.ExternalChannelSetting.ClaveDe(Domain.Enums.ExternalChannels.ExternalChannelKind.RestrictiveLists, null),
            Reason = "Sin proveedor todavía",
        });
        await _o.Db.SaveChangesAsync();
        var simuladas = new ListasDePrueba("SIMULADO", simulado: true);
        simuladas.PorDocumento[_o.Ana.TaxId!] = ResultadoDeLaLista.Match;
        var simulada = await simuladas.ConsultaReal(_o).ConsultarAsync(new PedidoDeConsultaDeListas(_o.Ana.Id, ScreeningContext.CreditLimit), default);
        simulada.Resultado.IsSimulated.Should().BeTrue();
        simulada.Resultado.ChannelCode.Should().Be("SIMULADO", "el canal vigente de la cooperativa se sella");
        simulada.CuentaComoConsulta.Should().BeFalse("en producción el simulado no tiene efecto (T73)");
        simulada.Bloquea.Should().BeFalse();
        _o.Db.DebtorBlocks.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task Una_coincidencia_ya_liberada_con_las_mismas_versiones_nace_liberada()
    {
        var r = await CoincidenciaDeSolicitudAsync();
        ComoOficial();
        (await LiberarAsync(r.PublicId)).IsSuccess.Should().BeTrue();
        _o.Db.ChangeTracker.Clear();

        var otra = await _listas.ConsultaReal(_o).ConsultarAsync(new PedidoDeConsultaDeListas(_o.Ana.Id, ScreeningContext.CreditLimit), default);

        otra.Bloquea.Should().BeFalse();
        otra.Resultado.ReleasedAt.Should().NotBeNull();
        _o.Db.DebtorBlocks.Local.Should().BeEmpty();

        _listas.Version = _listas.Version with { Version = 4 };
        (await _listas.ConsultaReal(_o).ConsultarAsync(new PedidoDeConsultaDeListas(_o.Ana.Id, ScreeningContext.CreditLimit), default))
            .Bloquea.Should().BeTrue("con una versión nueva de la lista se revisa otra vez");
    }
}
