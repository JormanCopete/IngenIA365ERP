using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ResultadoDeLaLista = IngenIA365ERP.Domain.Enums.Compliance.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0381 (C90, I12; adaptadores §3.8): la tarea <c>cartera.re-escaneo-de-listas</c>. Corre cuando hay una versión de la carga de listas
/// sin escanear y sale de inmediato si Cartera no está activada; consulta a los deudores y codeudores vivos; cada hallazgo deja su
/// <c>ScreeningResult</c> <c>ListRescan</c>, el bloqueo automático <c>RestrictiveListMatch</c> (impide originar y vender; el cobro de lo vivo sigue) y
/// la alerta <c>Cartera.CoincidenciaEnListas</c>; una consulta sin coincidencia no deja fila; es idempotente por la versión de la carga.
/// </summary>
public sealed class ReEscaneoDeListasTests : IDisposable
{
    private OriginacionDePrueba _o = null!;
    private readonly ListasDePrueba _listas = new();
    private readonly ICargaDeListasRestrictivas _cargas = Substitute.For<ICargaDeListasRestrictivas>();
    private Loan _credito = null!;

    private async Task ArmarAsync(bool activada = true)
    {
        _o = await OriginacionDePrueba.CrearAsync(activada);
        _credito = _o.Credito(_o.Ana, 5_000_000m, "CR-A", _o.Beto);
        _o.Credito(_o.Dario, 2_000_000m, "CR-D");
        _listas.PorDocumento[_o.Beto.TaxId!] = ResultadoDeLaLista.Match;
        _listas.PorDocumento[_o.Dario.TaxId!] = ResultadoDeLaLista.PossibleMatch;
        _listas.PorDocumento[_o.Carla.TaxId!] = ResultadoDeLaLista.Match;
        Carga(3);
    }

    public void Dispose() => _o?.Dispose();

    private void Carga(int version, bool vacia = false) =>
        _cargas.LeerVigenteAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(vacia ? CargaDeListasRestrictivas.Vacia : new CargaDeListasRestrictivas([new VersionDeListaConsultada("OFAC", version, new DateOnly(2026, 11, 1), null)], []));

    private async Task<ReEscaneoDeListasDto> CorrerAsync()
    {
        var r = await new RescanRestrictiveListsCommandHandler(_o.Db, _listas.ConsultaReal(_o), _cargas, _o.G.Reloj).Handle(new RescanRestrictiveListsCommand(), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        _o.Db.ChangeTracker.Clear();
        return r.Value;
    }

    [Fact]
    public async Task Sin_Cartera_activada_sale_de_inmediato()
    {
        await ArmarAsync(activada: false);

        var r = await CorrerAsync();

        r.Corrio.Should().BeFalse();
        r.Motivo.Should().Be("NotActivated");
        _listas.Consultas.Should().Be(0);
        (await _o.Db.ScreeningResults.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Consulta_a_deudores_y_codeudores_vivos_y_cada_hallazgo_bloquea_y_alerta()
    {
        await ArmarAsync();

        var r = await CorrerAsync();

        r.Corrio.Should().BeTrue();
        r.Consultadas.Should().Be(3, "Ana y Darío como deudores y Beto como codeudor; Carla no tiene créditos");
        r.Hallazgos.Should().Be(2);
        var resultados = await _o.Db.ScreeningResults.ToListAsync();
        resultados.Should().HaveCount(2, "una consulta sin coincidencia no deja fila").And.OnlyContain(x => x.Context == ScreeningContext.ListRescan);
        resultados.Select(x => x.PersonId).Should().BeEquivalentTo([_o.Beto.Id, _o.Dario.Id]);
        var bloqueos = await _o.Db.DebtorBlocks.ToListAsync();
        bloqueos.Should().HaveCount(2).And.OnlyContain(b => b.Reason == DebtorBlockReason.RestrictiveListMatch && b.IsAutomatic && b.LiftedAt == null);
        _o.L.Levantadas.Count(a => a.TypeCode == TiposDeAlerta.CoincidenciaEnListas).Should().Be(2);
        (await _o.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _credito.Id)).Status.Should().Be(LoanStatus.Active, "el cobro de lo vivo no se toca");
    }

    [Fact]
    public async Task Es_idempotente_por_la_version_de_la_carga()
    {
        await ArmarAsync();
        await CorrerAsync();
        var consultas = _listas.Consultas;

        var otra = await CorrerAsync();

        otra.Corrio.Should().BeFalse();
        otra.Motivo.Should().Be("AlreadyScanned");
        _listas.Consultas.Should().Be(consultas);
        (await _o.Db.ScreeningResults.CountAsync()).Should().Be(2);
        (await _o.Db.LendingProcessRuns.SingleAsync()).Should().Match<Domain.Entities.Lending.Integration.LendingProcessRun>(x =>
            x.Kind == LendingProcessKind.ScheduledTask && x.ExecutionOrigin == "Tarea:cartera.re-escaneo-de-listas" && x.ResultSha256 == otra.VersionDeLaCarga);

        Carga(4);
        (await CorrerAsync()).Corrio.Should().BeTrue("una carga nueva se escanea");
        _listas.Consultas.Should().BeGreaterThan(consultas);
    }

    [Fact]
    public async Task Sin_listas_cargadas_no_hace_nada()
    {
        await ArmarAsync();
        Carga(0, vacia: true);

        (await CorrerAsync()).Motivo.Should().Be("NoLists");
        _listas.Consultas.Should().Be(0);
    }

    [Fact]
    public async Task La_tarea_manda_el_comando_solo_con_Cartera_activada()
    {
        var tarea = new ReEscaneoDeListas();
        tarea.Nombre.Should().Be("cartera.re-escaneo-de-listas");
        tarea.DebeCorrer(DateTimeOffset.Now, null).Should().BeTrue();
        tarea.DebeCorrer(DateTimeOffset.Now, DateTimeOffset.Now.AddMinutes(-10)).Should().BeFalse();

        await ArmarAsync(activada: false);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<RescanRestrictiveListsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ReEscaneoDeListasDto(true, null, 0, 0, null)));
        var servicios = new ServiceCollection().AddSingleton<IApplicationDbContext>(_o.Db).AddSingleton(sender).BuildServiceProvider();

        await tarea.EjecutarAsync(servicios, default);
        await sender.DidNotReceive().Send(Arg.Any<RescanRestrictiveListsCommand>(), Arg.Any<CancellationToken>());

        _o.Activar();
        await tarea.EjecutarAsync(servicios, default);
        await sender.Received(1).Send(Arg.Any<RescanRestrictiveListsCommand>(), Arg.Any<CancellationToken>());
    }
}
