using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0676 (T47, T49; api.md §12.3; data-model §23.1): la orden del cierre mensual sólo encola (202) la corrida <c>MonthlyClose</c>
/// <c>Requested</c> con la persona como quien la ordena (<c>RequestedByKind = Person</c>, origen <c>Orden:{PublicId}</c>), después de revisar los
/// requisitos, cada uno con su código: <c>PreviousMissing</c> (el mensual anterior o, en el primero tras activar, el base aunque sea del mismo mes),
/// <c>AccountingPeriodClosed</c>, <c>AccrualBehind</c>, <c>ReplicationUnavailable</c> (la central no responde, B7), <c>Lending.Process.AlreadyRunning</c>
/// con <c>data.processRunPublicId</c>, y <c>AlreadyClosed</c> con una versión vigente.
/// </summary>
public class OrderMonthlyCloseCommandHandlerTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        await _c.CausarDiciembreAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Encola_la_corrida_Requested_a_nombre_de_la_persona_sin_correrla()
    {
        var r = await _c.OrdenarAsync(2026, 12);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(nameof(LendingProcessRunStatus.Requested));
        var corrida = await _c.Db.LendingProcessRuns.AsNoTracking().SingleAsync(x => x.PublicId == r.Value.ProcessRunPublicId);
        corrida.Should().BeEquivalentTo(new
        {
            Kind = LendingProcessKind.MonthlyClose, PeriodYear = (short?)2026, PeriodMonth = (byte?)12, CloseVersion = (int?)1,
            RequestedByKind = ActorKind.Person, RequestedByUserId = (int?)_c.Jefe.Id, ExecutionOrigin = $"Orden:{_c.Jefe.PublicId:D}",
            InformationCutoffAt = (DateTime?)null,
        }, o => o.ExcludingMissingMembers());
        (await _c.Db.MonthlyCloseLoans.CountAsync()).Should().Be(0, "la orden sólo encola (202)");
    }

    [Fact]
    public async Task Otra_orden_del_mismo_mes_mientras_la_primera_esta_pedida_responde_AlreadyRunning_con_la_corrida()
    {
        var primera = await _c.OrdenarAsync(2026, 12);

        var segunda = await _c.OrdenarAsync(2026, 12);

        segunda.Error.Code.Should().Be(ErroresDeCorridas.CodigoEnCurso);
        GobiernoDePrueba.Datos(segunda.Error).GetProperty("processRunPublicId").GetGuid().Should().Be(primera.Value.ProcessRunPublicId);
    }

    [Fact]
    public async Task Con_el_mes_ya_cerrado_responde_AlreadyClosed()
    {
        var cierre = await _c.CerrarAsync(2026, 12);

        var r = await _c.OrdenarAsync(2026, 12);

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoCierreYaHecho);
        GobiernoDePrueba.Datos(r.Error).GetProperty("monthlyClosePublicId").GetGuid().Should().Be(cierre.PublicId);
    }

    [Fact]
    public async Task Sin_cierre_anterior_responde_PreviousMissing_y_el_mes_siguiente_tambien_hasta_cerrar_el_anterior()
    {
        _c.IrA(new DateOnly(2027, 2, 2));

        (await _c.OrdenarAsync(2027, 1)).Error.Code.Should().Be(ErroresDeRiesgo.CodigoCierreAnteriorFaltante,
            "enero se compara con diciembre, que no se ha cerrado (y el base es de noviembre)");
    }

    [Fact]
    public async Task Sin_ningun_cierre_ni_base_responde_PreviousMissing()
    {
        using var c = await CierreDePrueba.CrearAsync(conBase: false);
        await c.CausarDiciembreAsync();

        (await c.OrdenarAsync(2026, 12)).Error.Code.Should().Be(ErroresDeRiesgo.CodigoCierreAnteriorFaltante);
    }

    [Fact]
    public async Task El_base_de_un_corte_a_mitad_de_mes_es_el_anterior_del_cierre_de_ese_mismo_mes()
    {
        using var c = await CierreDePrueba.CrearAsync(conBase: false);
        await c.BaseAsync(new DateOnly(2026, 12, 15));
        await c.CausarDiciembreAsync();

        var r = await c.OrdenarAsync(2026, 12);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    [Fact]
    public async Task Con_el_mes_cerrado_en_Contabilidad_responde_AccountingPeriodClosed()
    {
        _c.D.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Closed, new DateOnly(2027, 1, 1)))));

        (await _c.OrdenarAsync(2026, 12)).Error.Code.Should().Be(ErroresDeRiesgo.CodigoPeriodoContableCerrado);
    }

    [Fact]
    public async Task Con_la_causacion_atrasada_responde_AccrualBehind()
    {
        using var c = await CierreDePrueba.CrearAsync();
        await c.C.CausarHastaAsync(new DateOnly(2026, 12, 20));
        c.IrA(new DateOnly(2027, 1, 2));

        var r = await c.OrdenarAsync(2026, 12);

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoCausacionAtrasada);
        GobiernoDePrueba.Datos(r.Error).GetProperty("loansBehind").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Antes_de_terminar_el_mes_responde_MonthNotEnded()
    {
        _c.IrA(CierreDePrueba.FinDeDiciembre);

        (await _c.OrdenarAsync(2026, 12)).Error.Code.Should().Be(ErroresDeRiesgo.CodigoMesNoTerminado);
    }

    [Fact]
    public async Task Si_la_central_no_responde_se_niega_con_ReplicationUnavailable_B7()
    {
        var central = Substitute.For<IRegistroCentralDeValoresNacionales>();
        central.LeerDesdeAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("sin red"));
        var replicacion = new ReplicacionDeValoresNacionales(_c.Db, central, [], _c.G.Reloj, NullLogger<ReplicacionDeValoresNacionales>.Instance);

        var r = await _c.OrdenarAsync(2026, 12, replicacion);

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoReplicaNoDisponible);
        r.Error.Message.Should().Contain("sin red");
        (await _c.Db.LendingProcessRuns.CountAsync(x => x.Kind == LendingProcessKind.MonthlyClose)).Should().Be(0);
    }
}

/// <summary>
/// Feature 013, US9, T0676 (T47; api.md §12.3): la preparación del cierre de un mes lista los cinco requisitos con su detalle y no escribe; el bloqueo
/// previo (mes sin terminar, ya cerrado, Cartera sin activar) sale aparte con su código.
/// </summary>
public class GetMonthlyCloseReadinessQueryTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        await _c.CausarDiciembreAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private Task<Result<MonthlyCloseReadinessDto>> LeerAsync(int anio, int mes) =>
        new GetMonthlyCloseReadinessQueryHandler(_c.Requisitos()).Handle(new GetMonthlyCloseReadinessQuery(anio, mes), default);

    [Fact]
    public async Task Lista_los_cinco_requisitos_cumplidos_y_la_version_que_tocaria()
    {
        var r = await LeerAsync(2026, 12);

        r.Value.Ready.Should().BeTrue();
        r.Value.Version.Should().Be(1);
        r.Value.Requirements.Select(x => x.Code).Should().Equal(RequisitosDelCierreMensual.PreviousClose, RequisitosDelCierreMensual.AccountingPeriodOpen,
            RequisitosDelCierreMensual.AccrualUpToDate, RequisitosDelCierreMensual.ReplicaSynced, RequisitosDelCierreMensual.NoRunInProgress);
        r.Value.Requirements.Should().OnlyContain(x => x.Met && x.Detail.Length > 0);
        r.Value.Requirements[0].Detail.Should().Contain("cierre base");
    }

    [Fact]
    public async Task Con_una_corrida_en_curso_NoRunInProgress_no_se_cumple_y_no_escribe()
    {
        await _c.OrdenarAsync(2026, 12);
        var corridas = await _c.Db.LendingProcessRuns.CountAsync();

        var r = await LeerAsync(2026, 12);

        r.Value.Ready.Should().BeFalse();
        r.Value.Requirements.Single(x => x.Code == RequisitosDelCierreMensual.NoRunInProgress).Met.Should().BeFalse();
        (await _c.Db.LendingProcessRuns.CountAsync()).Should().Be(corridas);
    }

    [Fact]
    public async Task Un_mes_que_no_termina_sale_bloqueado_con_su_codigo()
    {
        var r = await LeerAsync(2027, 1);

        (r.Value.Ready, r.Value.BlockedCode).Should().Be((false, ErroresDeRiesgo.CodigoMesNoTerminado));
    }
}
