using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Receipts.Activation;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Tests.Receipts.Sessions;
using IngenIA365ERP.Application.Treasury.Activation;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using NSubstitute;
using Xunit;
using RequisitoDeCartera = IngenIA365ERP.Application.Lending.Activation.RequisitoDeActivacionDto;

namespace IngenIA365ERP.Application.Tests.Receipts.Activation;

/// <summary>
/// Feature 015, US24, T0317 (R-28; FR-227): las condiciones automáticas de la lista de go/no-go. Recaudo aporta sus requisitos de activación (<c>REQ.*</c>, entre ellos la
/// matriz contable al 100 %), el recibo de prueba por punto y medio y por concepto, el segundo factor de cajeros y supervisores y los alcances de los cajeros; Tesorería,
/// sus requisitos, el giro de prueba por cuenta y por forma y el segundo factor de los aprobadores; Cartera y Aportes, sus requisitos de activación (o «activado»). Los
/// ítems manuales (cronograma, escenarios de no-go parcial con su plan de reversa, decisión) los crea la lista con evidencia y los prueba <c>ListaDeSalidaTests</c>.
/// Prueba escrita, sin correr.
/// </summary>
public class VerificacionesDeSalidaTests
{
    private readonly CajaDePrueba _c = new();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();
    private readonly IAutoridadDeOtroAprobador _permisos = Substitute.For<IAutoridadDeOtroAprobador>();

    public VerificacionesDeSalidaTests()
    {
        _c.E.Db.ReceiptsSetups.Add(new ReceiptsSetup { Id = ReceiptsSetup.IdDeLaFilaUnica });
        _c.E.Db.SaveChanges();
        _c.Contabilidad.CompletitudDeLaMatrizAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new CompletitudContableDeModuloDto(true, false, 80m, 3));
        foreach (var p in RequisitosDeActivacionDeRecaudo.PermisosDelSupervisor)
            _permisos.TienePermisoAsync(_c.E.Supervisor.Id, p, Arg.Any<CancellationToken>()).Returns(true);
    }

    private VerificacionDeSalidaDeRecaudo Recaudo() => new(_c.E.Db,
        new RequisitosDeActivacionDeRecaudo(_c.E.Db, _ambiente, _c.Contabilidad, _permisos, _c.E.Parametros, [new ReglasDeParametrosDeRecaudo(ReglasDeParametrosDeRecaudo.InicioPorDefecto)]),
        _permisos, _c.E.Reloj);

    [Fact]
    public async Task Recaudo_aporta_sus_requisitos_y_sus_condiciones_de_salida()
    {
        var r = await Recaudo().VerificarAsync(default);

        r.Select(x => x.Code).Should().Contain([
            VerificacionDeSalidaDeRecaudo.PrefijoDeRequisito + RequisitosDeActivacionDeRecaudo.MatrizCompleta, VerificacionDeSalidaDeRecaudo.PrefijoDeRequisito + RequisitosDeActivacionDeRecaudo.Cuadre,
            VerificacionDeSalidaDeRecaudo.ReciboDePruebaPorPuntoYMedio, VerificacionDeSalidaDeRecaudo.ReciboDePruebaPorConcepto,
            VerificacionDeSalidaDeRecaudo.SegundoFactorDeCajerosYSupervisores, VerificacionDeSalidaDeRecaudo.AlcancesAsignados,
        ]);
        r.Single(x => x.Code == VerificacionDeSalidaDeRecaudo.PrefijoDeRequisito + RequisitosDeActivacionDeRecaudo.MatrizCompleta).Ok.Should().BeFalse("la matriz está al 80 %");
        r.Single(x => x.Code == VerificacionDeSalidaDeRecaudo.ReciboDePruebaPorPuntoYMedio).Ok.Should().BeFalse("ningún medio ofrecido tiene su recibo de prueba");
        r.Single(x => x.Code == VerificacionDeSalidaDeRecaudo.ReciboDePruebaPorPuntoYMedio).Evidence.Should().Contain("P1/EF").And.Contain("P1/TR");
    }

    [Fact]
    public async Task El_segundo_factor_de_cajeros_y_supervisores_se_exige_a_todos()
    {
        foreach (var u in new[] { _c.E.Cajera, _c.E.OtroCajero, _c.E.Supervisor }) u.IsMfaEnabled = true;
        _c.E.Db.SaveChanges();
        (await Recaudo().VerificarAsync(default)).Single(x => x.Code == VerificacionDeSalidaDeRecaudo.SegundoFactorDeCajerosYSupervisores).Ok.Should().BeTrue();

        _c.E.OtroCajero.IsMfaEnabled = false;
        _c.E.Db.SaveChanges();
        var r = (await Recaudo().VerificarAsync(default)).Single(x => x.Code == VerificacionDeSalidaDeRecaudo.SegundoFactorDeCajerosYSupervisores);
        r.Ok.Should().BeFalse();
        r.Evidence.Should().Contain(_c.E.OtroCajero.Username);
    }

    [Fact]
    public async Task Sin_conceptos_propios_la_condicion_del_concepto_queda_en_verde_y_con_uno_sin_recibo_en_rojo()
    {
        (await Recaudo().VerificarAsync(default)).Single(x => x.Code == VerificacionDeSalidaDeRecaudo.ReciboDePruebaPorConcepto).Ok.Should().BeTrue();

        _c.E.Db.MiscConcepts.Add(new MiscConcept { Code = "CUOTAADM", Name = "Cuota de administración", Status = ReceiptsCatalogStatus.Active });
        _c.E.Db.SaveChanges();
        var r = (await Recaudo().VerificarAsync(default)).Single(x => x.Code == VerificacionDeSalidaDeRecaudo.ReciboDePruebaPorConcepto);
        r.Ok.Should().BeFalse();
        r.Evidence.Should().Contain("CUOTAADM");
    }

    [Fact]
    public async Task Tesoreria_sin_cuentas_de_pagos_queda_en_rojo_en_el_giro_de_prueba_y_en_sus_requisitos()
    {
        var contabilidad = Substitute.For<IContabilidadParaTesoreria>();
        contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));
        var verificacion = new VerificacionDeSalidaDeTesoreria(_c.E.Db, new RequisitosDeActivacionDeTesoreria(_c.E.Db, contabilidad, _ambiente), _permisos, _c.E.Reloj);

        var r = await verificacion.VerificarAsync(default);

        verificacion.Modulo.Should().Be("TES");
        r.Single(x => x.Code == VerificacionDeSalidaDeTesoreria.GiroDePruebaPorCuenta).Ok.Should().BeFalse();
        r.Single(x => x.Code == VerificacionDeSalidaDeTesoreria.GiroDePruebaPorForma).Ok.Should().BeFalse("no hay ningún pago por transferencia");
        r.Single(x => x.Code == VerificacionDeSalidaDeTesoreria.SegundoFactorDeAprobadores).Ok.Should().BeFalse("nadie aprueba lotes");
        r.Should().Contain(x => x.Code.StartsWith(VerificacionDeSalidaDeTesoreria.PrefijoDeRequisito) && !x.Ok);
    }

    /// <summary>
    /// Corrida e2e del 2026-10-27: el índice único de los ítems es lista × código, así que los requisitos de Recaudo y de Tesorería (que comparten códigos como
    /// <c>OpeningSquare</c>) llevan el módulo en el prefijo; con el mismo <c>REQ.</c> crear la lista caía en la llave duplicada.
    /// </summary>
    [Fact]
    public void Los_requisitos_de_Recaudo_y_de_Tesoreria_no_comparten_codigo_en_la_lista()
    {
        VerificacionDeSalidaDeRecaudo.PrefijoDeRequisito.Should().NotBe(VerificacionDeSalidaDeTesoreria.PrefijoDeRequisito);
        VerificacionDeSalidaDeRecaudo.PrefijoDeRequisito.Should().StartWith("REC.");
        VerificacionDeSalidaDeTesoreria.PrefijoDeRequisito.Should().StartWith("TES.");
    }

    [Fact]
    public async Task Cartera_y_aportes_activados_quedan_en_verde()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetLendingActivationStatusQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new ActivationReadinessDto(
            GetLendingActivationStatusQueryHandler.Activada, new DateOnly(2026, 12, 1), Guid.NewGuid(), DateTime.UtcNow, null, null, null, false, [])));
        sender.Send(Arg.Any<GetDepositsActivationStatusQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new DepositsActivationStatusDto(
            LineNature.Contribution, "Activated", new DateOnly(2026, 12, 1), Guid.NewGuid(), null, null, null, false, [], [])));

        var r = await new VerificacionDeSalidaDeCartera(sender).VerificarAsync(default);

        r.Should().HaveCount(2).And.OnlyContain(x => x.Ok);
    }

    [Fact]
    public async Task Cartera_sin_activar_lista_sus_requisitos_con_el_recaudador_y_la_tesoreria_reales()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetLendingActivationStatusQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new ActivationReadinessDto(
            GetLendingActivationStatusQueryHandler.SinActivar, null, null, null, null, null, null, false,
            [
                new RequisitoDeCartera(RequisitosDeActivacion.RecaudadorRegistrado, false, "Falta el recaudador real.", null, EstadosDeRequisito.Missing),
                new RequisitoDeCartera(RequisitosDeActivacion.TesoreriaRegistrada, true, "Tesorería registrada.", null),
                new RequisitoDeCartera("ExpectedLossAvailable", false, "No aplica.", null, EstadosDeRequisito.NotApplicable),
            ])));
        sender.Send(Arg.Any<GetDepositsActivationStatusQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new DepositsActivationStatusDto(
            LineNature.Contribution, "NotActivated", null, null, null, null, null, false, [], [])));

        var r = await new VerificacionDeSalidaDeCartera(sender).VerificarAsync(default);

        r.Single(x => x.Code == "CAR." + RequisitosDeActivacion.RecaudadorRegistrado).Ok.Should().BeFalse();
        r.Single(x => x.Code == "CAR." + RequisitosDeActivacion.TesoreriaRegistrada).Ok.Should().BeTrue();
        r.Single(x => x.Code == "CAR.ExpectedLossAvailable").Ok.Should().BeTrue("un requisito que no aplica cuenta en verde");
    }
}
