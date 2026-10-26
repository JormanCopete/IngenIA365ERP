using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Activation;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Activation;

/// <summary>
/// Feature 015, US9, T0295 (FR-147, FR-149, FR-150; D2): la activación <b>completa</b> de Tesorería: cuenta de pagos vinculada, formato o la forma «portal del banco»
/// (FR-213), el cuadre de la puesta en marcha, la chequera si la cooperativa gira cheques y la matriz completa con Contabilidad iniciada; registra
/// <c>TES × MessageDestination × Treasury</c> con <c>OrdenDeGiroEmitida</c> v1 y v2, <c>AnulacionDeOrdenDeGiro</c> v1 y los dos del archivo de desembolsos de
/// Cartera, y <b>nunca</b> una fila <c>ObligationsCollector</c> (la clase <c>ClaseDeRecaudador.Treasury</c> queda sin uso); una versión no adoptada no está en la
/// lista (FR-149: el registro la rechaza con su motivo). Prueba escrita, sin correr.
/// </summary>
public class ActivacionDeTesoreriaTests
{
    private static readonly DateOnly Corte = EscenarioDeRecaudo.Hoy.AddDays(-1);

    private readonly EscenarioDeTesoreria _t = new();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();

    public ActivacionDeTesoreriaTests()
    {
        _t.E.Db.TreasurySetups.Add(new TreasurySetup { Id = TreasurySetup.IdDeLaFilaUnica });
        _t.E.Db.SaveChanges();
        _t.Contabilidad.VinculoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new VinculoDto(ci.ArgAt<Guid>(0), Guid.NewGuid(), "11100501", EscenarioDeRecaudo.Hoy.AddYears(-1)));
        _t.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));
        _t.Contabilidad.CompletitudDeLaMatrizAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new CompletitudContableDeModuloDto(false, false, 100m, 0));
    }

    private TreasuryOpeningSquare Cuadre()
    {
        var c = new TreasuryOpeningSquare
        {
            Kind = OpeningSquareKind.Provisional, Status = OpeningSquareState.ProvisionalApproved, CutoffDate = Corte, RegisteredByUserId = _t.E.Supervisor.Id,
            RegisteredAt = DateTime.UtcNow, Reason = "El definitivo lo cuadra la contadora", DeadlineOn = Corte.AddDays(4),
        };
        _t.E.Db.TreasuryOpeningSquares.Add(c);
        _t.E.Db.SaveChanges();
        return c;
    }

    private ActivateTreasuryCommandHandler Activar() => new(_t.E.Db, _t.E.Actor, _t.Cerrojo, new RequisitosDeActivacionDeTesoreria(_t.E.Db, _t.Contabilidad, _ambiente),
        _t.Parametros, new EscritorDelRegistroDeIntegracion(_t.E.Db, _ambiente, _t.E.Actor, _t.E.Reloj),
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _ambiente, _t.E.Reloj);

    private static IEnumerable<string> Faltan(IngenIA365ERP.Application.Common.Models.Error error)
    {
        error.Code.Should().Be(ActivateTreasuryCommandHandler.CodigoRequisitos);
        var datos = ((IngenIA365ERP.Application.Common.Models.ErrorConDatos)error).Data;
        return (IEnumerable<string>)datos.GetType().GetProperty("missing")!.GetValue(datos)!;
    }

    [Fact]
    public async Task Sin_cuadre_no_activa()
    {
        var r = await Activar().Handle(new ActivateTreasuryCommand { OperationKey = Guid.NewGuid() }, default);

        Faltan(r.Error).Should().Contain(RequisitosDeActivacionDeTesoreria.Cuadre);
    }

    [Fact]
    public async Task Con_el_cuadre_activa_y_registra_solo_el_destino_con_sus_cinco_adopciones()
    {
        var cuadre = Cuadre();

        var r = await Activar().Handle(new ActivateTreasuryCommand { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + " " + r.Error.Message : null);
        r.Value.IsMinimal.Should().BeFalse();
        var activacion = _t.E.Db.TreasuryActivations.Single();
        activacion.OpeningSquareId.Should().Be(cuadre.Id);
        activacion.ActivatedOn.Should().Be(Corte.AddDays(1));
        var tes = _t.E.Db.IntegrationParticipants.Where(p => p.Code == ActivateTreasuryCommandHandler.CodigoDeDestino).ToList();
        tes.Should().ContainSingle();
        tes[0].Role.Should().Be(IntegrationParticipantRole.MessageDestination);
        tes[0].Class.Should().Be(IntegrationParticipantClass.Treasury);
        _t.E.Db.IntegrationParticipants.Should().NotContain(p => p.Role == IntegrationParticipantRole.ObligationsCollector,
            "Tesorería nunca es recaudador (D2): ClaseDeRecaudador.Treasury queda sin uso");
        _t.E.Db.IntegrationParticipantAdoptions.Where(a => a.ParticipantId == tes[0].Id).Select(a => a.ContractName).Should().BeEquivalentTo(
            [OrdenDeGiroEmitidaV1.Type, OrdenDeGiroEmitidaV2.Type, AnulacionDeOrdenDeGiroV1.Type, ArchivoDeDesembolsosGeneradoV1.Type, ArchivoDeDesembolsosAnuladoV1.Type,
             // T2 (fase 42): la dispersión de Nómina y las facturas de proveedor de Comercial.
             Application.Common.Integration.Contracts.Payroll.DispersionDeNominaGeneradaV1.Type,
             Application.Common.Integration.Contracts.Inventory.FacturaDeProveedorPorPagarV1.Type,
             Application.Common.Integration.Contracts.Inventory.AnulacionDeFacturaDeProveedorV1.Type]);
    }

    [Fact]
    public async Task Si_gira_cheques_exige_una_chequera_activa()
    {
        Cuadre();

        var sin = await Activar().Handle(new ActivateTreasuryCommand(OffersChecks: true) { OperationKey = Guid.NewGuid() }, default);
        Faltan(sin.Error).Should().Equal(RequisitosDeActivacionDeTesoreria.Chequera);

        _t.E.Db.Checkbooks.Add(new Checkbook
        {
            BankAccountId = _t.Pagos.Id, FromNumber = 1001, ToNumber = 1100, NextCheckNumber = 1001, CustodianUserId = _t.E.Supervisor.Id,
            RegisteredByUserId = _t.E.Supervisor.Id, RegisteredAt = DateTime.UtcNow,
        });
        _t.E.Db.SaveChanges();
        (await Activar().Handle(new ActivateTreasuryCommand(OffersChecks: true) { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Con_contabilidad_iniciada_exige_la_matriz_completa()
    {
        Cuadre();
        _t.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, null));
        _t.Contabilidad.CompletitudDeLaMatrizAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new CompletitudContableDeModuloDto(true, false, 50m, 7));

        var r = await Activar().Handle(new ActivateTreasuryCommand { OperationKey = Guid.NewGuid() }, default);

        Faltan(r.Error).Should().Equal(RequisitosDeActivacionDeTesoreria.MatrizCompleta);
    }

    [Fact]
    public async Task En_produccion_no_hay_activacion_minima()
    {
        _ambiente.EsProduccion.Returns(true);

        var r = await Activar().Handle(new ActivateTreasuryCommand(Minimal: true) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ActivateTreasuryCommandHandler.CodigoMinimaNoPermitida);
    }
}
