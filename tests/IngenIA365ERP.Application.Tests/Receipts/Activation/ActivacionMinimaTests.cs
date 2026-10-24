using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Activation;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Receipts.Sessions;
using IngenIA365ERP.Application.Tests.Treasury;
using IngenIA365ERP.Application.Treasury.Activation;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Activation;

/// <summary>
/// Feature 015, T0183a (R-40; TR42; FR-147, FR-150): con los requisitos básicos (Recaudo: punto, caja con cajero, efectivo y transferencia, tipos de documento;
/// Tesorería: una cuenta de pagos vinculada; el formato o la forma «portal del banco») <c>ActivateReceiptsCommand</c> y <c>ActivateTreasuryCommand</c> dejan el
/// módulo activo, guardan su activación con <c>IsMinimal</c> y registran en el mismo guardado <c>REC × ObligationsCollector</c>, <c>REC × Receipts</c> y
/// <c>TES × Treasury</c> (sin fila <c>ObligationsCollector</c> de Tesorería); sin un requisito, <c>RequirementsMissing</c> con <c>data.missing[]</c>; con la fila del
/// doble <c>TES</c> vigente, se cierra antes. Prueba escrita, sin correr.
/// </summary>
public class ActivacionMinimaTests
{
    private readonly CajaDePrueba _c = new();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();

    private EscritorDelRegistroDeIntegracion Registro => new(_c.E.Db, _ambiente, _c.E.Actor, _c.E.Reloj);

    private ActivateReceiptsCommandHandler ActivarRecaudo() => new(_c.E.Db, _c.E.Actor, _c.Cerrojo,
        new RequisitosDeActivacionDeRecaudo(_c.E.Db, _ambiente, Substitute.For<IngenIA365ERP.Application.Common.Integration.Accounting.IContabilidadParaRecaudo>(),
            Substitute.For<IngenIA365ERP.Application.Common.Approvals.IAutoridadDeOtroAprobador>(), _c.E.Parametros, []),
        _c.E.Parametros, Registro, new AuditoriaDeRecaudo(_c.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance), _ambiente, _c.E.Reloj);

    public ActivacionMinimaTests()
    {
        _c.E.Db.ReceiptsSetups.Add(new ReceiptsSetup { Id = ReceiptsSetup.IdDeLaFilaUnica });
        _c.E.Db.SaveChanges();
    }

    private void TiposDeDocumento()
    {
        _c.E.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RC", Name = "Recibo de caja", Kind = ReceiptDocumentKind.CashReceipt });
        _c.E.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "AN", Name = "Anulación", Kind = ReceiptDocumentKind.Voiding });
        _c.E.Db.SaveChanges();
    }

    [Fact]
    public async Task Sin_un_requisito_basico_no_activa_y_dice_cual()
    {
        var r = await ActivarRecaudo().Handle(new ActivateReceiptsCommand(Minimal: true) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoRequisitosDeActivacion);
        var datos = ((IngenIA365ERP.Application.Common.Models.ErrorConDatos)r.Error).Data;
        ((IEnumerable<string>)datos.GetType().GetProperty("missing")!.GetValue(datos)!).Should().Contain(RequisitosDeActivacionDeRecaudo.TiposDeDocumento);
    }

    [Fact]
    public async Task Con_los_requisitos_activa_Recaudo_minima_y_registra_al_recaudador_y_su_destino()
    {
        TiposDeDocumento();

        var r = await ActivarRecaudo().Handle(new ActivateReceiptsCommand(Minimal: true) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + " " + r.Error.Message : null);
        r.Value.IsMinimal.Should().BeTrue();
        _c.E.Db.ReceiptsSetups.Single().Status.Should().Be(ReceiptsModuleStatus.Active);
        _c.E.Db.ReceiptsActivations.Single().IsMinimal.Should().BeTrue();
        var filas = _c.E.Db.IntegrationParticipants.Where(p => p.Code == ClavesDeRecaudo.Codigo).ToList();
        filas.Should().Contain(p => p.Role == IntegrationParticipantRole.ObligationsCollector && p.Class == IntegrationParticipantClass.Receipts);
        filas.Should().Contain(p => p.Role == IntegrationParticipantRole.MessageDestination && p.Destination == IntegrationDestinations.Receipts);
    }

    [Fact]
    public async Task Tesoreria_cierra_la_fila_del_doble_y_registra_solo_su_destino()
    {
        var t = new EscenarioDeTesoreria();
        t.E.Db.TreasurySetups.Add(new TreasurySetup { Id = TreasurySetup.IdDeLaFilaUnica });
        t.Contabilidad.VinculoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new IngenIA365ERP.Application.Common.Integration.Accounting.VinculoDto(ci.ArgAt<Guid>(0), Guid.NewGuid(), "11100501", EscenarioDeRecaudo.Hoy.AddYears(-1)));
        t.E.Db.IntegrationParticipants.Add(new IntegrationParticipant
        {
            Code = "TES", Class = IntegrationParticipantClass.Test, Role = IntegrationParticipantRole.MessageDestination, Destination = IntegrationDestinations.Treasury,
            IsTestDouble = true, IsEnabled = true, ValidFrom = EscenarioDeRecaudo.Hoy.AddMonths(-2), Reason = "doble",
        });
        t.E.Db.SaveChanges();
        var registro = new EscritorDelRegistroDeIntegracion(t.E.Db, _ambiente, t.E.Actor, t.E.Reloj);
        var handler = new ActivateTreasuryCommandHandler(t.E.Db, t.E.Actor, t.Cerrojo, new RequisitosDeActivacionDeTesoreria(t.E.Db, t.Contabilidad, _ambiente), t.Parametros,
            registro, new AuditoriaDeTesoreria(t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _ambiente, t.E.Reloj);

        var r = await handler.Handle(new ActivateTreasuryCommand(Minimal: true) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + " " + r.Error.Message : null);
        t.E.Db.TreasurySetups.Single().Status.Should().Be(TreasuryModuleStatus.Active);
        t.E.Db.TreasuryActivations.Single().IsMinimal.Should().BeTrue();
        var tes = t.E.Db.IntegrationParticipants.Where(p => p.Code == "TES").ToList();
        tes.Single(p => p.IsTestDouble).ValidTo.Should().NotBeNull();
        tes.Single(p => !p.IsTestDouble).Class.Should().Be(IntegrationParticipantClass.Treasury);
        t.E.Db.IntegrationParticipants.Should().NotContain(p => p.Code == "TES" && p.Role == IntegrationParticipantRole.ObligationsCollector);
    }
}
