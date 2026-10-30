using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Receipts.Activation;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Tests.Receipts.Sessions;
using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Activation;

/// <summary>
/// Feature 015, US9, T0294 (R-26, R-27; FR-147, FR-150, FR-159, FR-176, FR-185): la activación <b>completa</b> de Recaudo. Exige, además de los básicos, el cuadre de la
/// puesta en marcha (definitivo o provisional aprobado), un cajero y un supervisor con los permisos de FR-176, el mes contable abierto, la matriz completa con
/// Contabilidad iniciada (salvo «no pasa») y la hora de corte antes del ciclo de Cartera; sin uno, <c>Receipts.Activation.RequirementsMissing</c> con
/// <c>data.missing[]</c>. Rige desde el día siguiente al corte del cuadre y, en el mismo guardado, registra <c>REC × ObligationsCollector</c> con
/// <c>ProveedorDeObligaciones</c> y <c>ProveedorDeCuentas</c> v1 y <c>REC × MessageDestination × Receipts</c> con sus cuatro adopciones. En producción no hay
/// activación mínima. Prueba escrita, sin correr.
/// </summary>
public class ActivacionDeRecaudoTests
{
    private static readonly DateOnly Corte = EscenarioDeRecaudo.Hoy.AddDays(-1);

    private readonly CajaDePrueba _c = new();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();
    private readonly IAutoridadDeOtroAprobador _permisos = Substitute.For<IAutoridadDeOtroAprobador>();
    private TimeOnly _inicioDelCiclo = ReglasDeParametrosDeRecaudo.InicioPorDefecto;

    public ActivacionDeRecaudoTests()
    {
        _c.E.Db.ReceiptsSetups.Add(new ReceiptsSetup { Id = ReceiptsSetup.IdDeLaFilaUnica });
        _c.E.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RC", Name = "Recibo de caja", Kind = ReceiptDocumentKind.CashReceipt });
        _c.E.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "AN", Name = "Anulación", Kind = ReceiptDocumentKind.Voiding });
        _c.E.Db.SaveChanges();
        _c.Contabilidad.CompletitudDeLaMatrizAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new CompletitudContableDeModuloDto(false, false, 100m, 0));
        Permisos(_c.E.Cajera.Id, RequisitosDeActivacionDeRecaudo.PermisosDelCajero);
        Permisos(_c.E.Supervisor.Id, RequisitosDeActivacionDeRecaudo.PermisosDelSupervisor);
    }

    private void Permisos(int usuario, IEnumerable<string> permisos)
    {
        foreach (var p in permisos) _permisos.TienePermisoAsync(usuario, p, Arg.Any<CancellationToken>()).Returns(true);
    }

    private ReceiptsOpeningSquare Cuadre(OpeningSquareKind clase = OpeningSquareKind.Provisional, OpeningSquareState estado = OpeningSquareState.ProvisionalApproved)
    {
        var c = new ReceiptsOpeningSquare
        {
            Kind = clase, Status = estado, CutoffDate = Corte, RegisteredByUserId = _c.E.Supervisor.Id, RegisteredAt = DateTime.UtcNow,
            Reason = clase == OpeningSquareKind.Provisional ? "La contadora cuadra el 04/12" : null, DeadlineOn = clase == OpeningSquareKind.Provisional ? Corte.AddDays(4) : null,
        };
        _c.E.Db.ReceiptsOpeningSquares.Add(c);
        _c.E.Db.SaveChanges();
        return c;
    }

    private RequisitosDeActivacionDeRecaudo Requisitos() =>
        new(_c.E.Db, _ambiente, _c.Contabilidad, _permisos, _c.E.Parametros, [new ReglasDeParametrosDeRecaudo(_inicioDelCiclo)]);

    private ActivateReceiptsCommandHandler Activar() => new(_c.E.Db, _c.E.Actor, _c.Cerrojo, Requisitos(), _c.E.Parametros,
        new EscritorDelRegistroDeIntegracion(_c.E.Db, _ambiente, _c.E.Actor, _c.E.Reloj), new AuditoriaDeRecaudo(_c.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance),
        _ambiente, _c.E.Reloj);

    private static IEnumerable<string> Faltan(IngenIA365ERP.Application.Common.Models.Error error)
    {
        error.Code.Should().Be(ErroresDeRecaudo.CodigoRequisitosDeActivacion);
        var datos = ((IngenIA365ERP.Application.Common.Models.ErrorConDatos)error).Data;
        return (IEnumerable<string>)datos.GetType().GetProperty("missing")!.GetValue(datos)!;
    }

    [Fact]
    public async Task Sin_cuadre_de_la_puesta_en_marcha_la_completa_no_activa()
    {
        var r = await Activar().Handle(new ActivateReceiptsCommand { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
        Faltan(r.Error).Should().Contain(RequisitosDeActivacionDeRecaudo.Cuadre);
        _c.E.Db.ReceiptsSetups.Single().Status.Should().NotBe(ReceiptsModuleStatus.Active);
        _c.E.Db.IntegrationParticipants.Should().BeEmpty("antes de activar Recaudo no está en el registro y sus mensajes quedan pendientes en la bandeja");
    }

    [Theory]
    [InlineData(OpeningSquareKind.Provisional, OpeningSquareState.ProvisionalApproved)]
    [InlineData(OpeningSquareKind.Definitive, OpeningSquareState.Squared)]
    [InlineData(OpeningSquareKind.Definitive, OpeningSquareState.AcceptedWithMinutes)]
    public async Task Con_un_cuadre_aprobado_activa_desde_el_dia_siguiente_al_corte_y_registra_REC(OpeningSquareKind clase, OpeningSquareState estado)
    {
        var cuadre = Cuadre(clase, estado);

        var r = await Activar().Handle(new ActivateReceiptsCommand { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + " " + r.Error.Message : null);
        r.Value.IsMinimal.Should().BeFalse();
        r.Value.OpeningSquarePublicId.Should().Be(cuadre.PublicId);
        r.Value.ActivatedOn.Should().Be(Corte.AddDays(1), "rige desde el día siguiente al corte (FR-159)");
        var activacion = _c.E.Db.ReceiptsActivations.Single();
        activacion.IsMinimal.Should().BeFalse();
        activacion.OpeningSquareId.Should().Be(cuadre.Id);
        activacion.CutoffDate.Should().Be(Corte);
        activacion.RequirementsJson.Should().Contain(RequisitosDeActivacionDeRecaudo.SupervisorConPermisos);

        var filas = _c.E.Db.IntegrationParticipants.Where(p => p.Code == ClavesDeRecaudo.Codigo).ToList();
        var recaudador = filas.Single(p => p.Role == IntegrationParticipantRole.ObligationsCollector);
        recaudador.Class.Should().Be(IntegrationParticipantClass.Receipts);
        recaudador.IsTestDouble.Should().BeFalse();
        var destino = filas.Single(p => p.Role == IntegrationParticipantRole.MessageDestination);
        destino.Destination.Should().Be(IntegrationDestinations.Receipts);
        var adopciones = _c.E.Db.IntegrationParticipantAdoptions.ToList();
        adopciones.Where(a => a.ParticipantId == recaudador.Id).Select(a => (a.ContractName, (int)a.Version)).Should()
            .BeEquivalentTo(ActivateReceiptsCommandHandler.VersionesDelRecaudador.Select(v => (v.Contrato, v.Version)));
        adopciones.Where(a => a.ParticipantId == destino.Id).Should().HaveCount(4, "pago reversado, consignación reversada y descuentos por pagaduría v1 y v2");
    }

    [Fact]
    public async Task Sin_supervisor_con_los_permisos_de_FR_176_no_activa()
    {
        Cuadre();
        _permisos.TienePermisoAsync(_c.E.Supervisor.Id, "Receipts.Day.Close", Arg.Any<CancellationToken>()).Returns(false);

        var r = await Activar().Handle(new ActivateReceiptsCommand { OperationKey = Guid.NewGuid() }, default);

        Faltan(r.Error).Should().Equal(RequisitosDeActivacionDeRecaudo.SupervisorConPermisos);
    }

    [Fact]
    public async Task Sin_un_cajero_asignado_con_sus_permisos_no_activa()
    {
        Cuadre();
        _permisos.TienePermisoAsync(Arg.Any<int>(), "Receipts.Receipt.Register", Arg.Any<CancellationToken>()).Returns(false);

        var r = await Activar().Handle(new ActivateReceiptsCommand { OperationKey = Guid.NewGuid() }, default);

        Faltan(r.Error).Should().Contain(RequisitosDeActivacionDeRecaudo.CajeroConPermisos);
    }

    [Fact]
    public async Task Con_contabilidad_iniciada_exige_la_matriz_completa_salvo_que_no_pase()
    {
        Cuadre();
        _c.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, null));
        _c.Contabilidad.CompletitudDeLaMatrizAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new CompletitudContableDeModuloDto(true, false, 80m, 3));

        var r = await Activar().Handle(new ActivateReceiptsCommand { OperationKey = Guid.NewGuid() }, default);
        Faltan(r.Error).Should().Equal(RequisitosDeActivacionDeRecaudo.MatrizCompleta);

        _c.Contabilidad.CompletitudDeLaMatrizAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new CompletitudContableDeModuloDto(true, true, 80m, 3));
        (await Activar().Handle(new ActivateReceiptsCommand { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue("en «no pasa» la matriz no se exige");
    }

    [Fact]
    public async Task Con_el_mes_de_la_activacion_cerrado_no_activa()
    {
        Cuadre();
        _c.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Closed, null));

        var r = await Activar().Handle(new ActivateReceiptsCommand { OperationKey = Guid.NewGuid() }, default);

        Faltan(r.Error).Should().Contain(RequisitosDeActivacionDeRecaudo.MesAbierto);
    }

    [Fact]
    public async Task Una_hora_de_corte_despues_del_ciclo_de_Cartera_no_activa()
    {
        Cuadre();
        _inicioDelCiclo = new TimeOnly(20, 0);
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, "21:00");

        var r = await Activar().Handle(new ActivateReceiptsCommand { OperationKey = Guid.NewGuid() }, default);

        Faltan(r.Error).Should().Contain(RequisitosDeActivacionDeRecaudo.HoraDeCorte);
    }

    [Fact]
    public async Task En_produccion_no_hay_activacion_minima()
    {
        _ambiente.EsProduccion.Returns(true);

        var r = await Activar().Handle(new ActivateReceiptsCommand(Minimal: true) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ActivateReceiptsCommandHandler.CodigoMinimaNoPermitida);
    }

    [Fact]
    public async Task Los_requisitos_dicen_que_cuadre_tomaria_la_activacion()
    {
        var cuadre = Cuadre(OpeningSquareKind.Definitive, OpeningSquareState.Squared);

        var r = await new GetReceiptsActivationRequirementsQueryHandler(_c.E.Db, Requisitos(), _c.E.Reloj).Handle(new GetReceiptsActivationRequirementsQuery(), default);

        r.Value.CanActivate.Should().BeTrue();
        r.Value.OpeningSquarePublicId.Should().Be(cuadre.PublicId);
        r.Value.OpeningSquareKind.Should().Be("Definitive");
        r.Value.CutoffDate.Should().Be(Corte);
    }
}
