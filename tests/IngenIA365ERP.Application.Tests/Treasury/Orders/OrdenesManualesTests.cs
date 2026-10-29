using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Treasury.Orders;

/// <summary>
/// Feature 015, T1, T0414 (FR-211, FR-012; US26-3, US26-4): la orden manual sólo por un concepto activo del catálogo cerrado, con soporte, dentro del tope y con la retención
/// que exija (sin ella, se deriva a una causación previa en Contabilidad); la aprueban dos personas distintas de quien prepara y del beneficiario; aprobada, entra a la
/// bandeja con origen <c>Manual</c> y queda lista; sin sus dos aprobaciones no queda lista. Prueba escrita, sin correr.
/// </summary>
public class OrdenesManualesTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly ManualOrderConcept _honorarios;
    private readonly ManualOrderConcept _reintegro;

    public OrdenesManualesTests()
    {
        _honorarios = new ManualOrderConcept { Code = "HONOR", Name = "Honorarios", MaxAmount = 5_000_000m, RequiresWithholding = true, WithholdingNote = "Honorarios 11 %" };
        _reintegro = new ManualOrderConcept { Code = "REINT", Name = "Reintegro de gastos", MaxAmount = 1_000_000m };
        _t.E.Db.ManualOrderConcepts.AddRange(_honorarios, _reintegro);
        _t.E.Db.SaveChanges();
    }

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    private CreateManualPaymentOrderCommandHandler Preparar() => new(_t.E.Db, _t.E.Actor, _t.E.Personas, _t.Cerrojo, _t.Numerador, _t.Parametros, Auditoria, _t.E.Reloj);

    private ApproveManualPaymentOrderCommandHandler Aprobar() => new(_t.E.Db, _t.E.Actor, _t.E.Personas, _t.Cerrojo, _t.Validador, Auditoria, _t.E.Reloj);

    private CreateManualPaymentOrderCommand Orden(ManualOrderConcept c, decimal bruto, decimal retencion = 0m, Guid? soporte = null) =>
        new(c.PublicId, _t.Beneficiaria.PublicId, PaymentForm.Transfer, bruto, retencion, "Pago manual", soporte ?? Guid.NewGuid(), _t.CuentaDeLaBeneficiaria.PublicId)
        {
            OperationKey = Guid.NewGuid(),
        };

    [Fact]
    public async Task Respeta_el_tope_del_concepto_y_deriva_a_causacion_si_falta_la_retencion()
    {
        (await Preparar().Handle(Orden(_reintegro, 2_000_000m), default)).Error.Code.Should().Be(ErroresDeTesoreria.CodigoOrdenManualSobreElTope);
        (await Preparar().Handle(Orden(_honorarios, 1_000_000m), default)).Error.Code.Should().Be(ErroresDeTesoreria.CodigoOrdenManualDerivarACausacion);
        _reintegro.Status = TreasuryCatalogStatus.Inactive;
        await _t.E.Db.SaveChangesAsync();
        (await Preparar().Handle(Orden(_reintegro, 100m), default)).Error.Code.Should().Be(ErroresDeTesoreria.CodigoOrdenManualConceptoInactivo);
    }

    [Fact]
    public void El_soporte_es_obligatorio() =>
        new CreateManualPaymentOrderCommandValidator().Validate(Orden(_reintegro, 100m, soporte: Guid.Empty)).IsValid.Should().BeFalse();

    [Fact]
    public async Task Queda_esperando_dos_aprobaciones_y_mientras_tanto_no_queda_lista()
    {
        var r = await Preparar().Handle(Orden(_honorarios, 1_000_000m, 110_000m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.ApprovalState.Should().Be(ManualOrderApprovalState.PendingFirstApproval);
        r.Value.NetAmount.Should().Be(890_000m);
        var orden = await _t.E.Db.PaymentOrders.SingleAsync();
        orden.Origin.Should().Be(PaymentOrderOrigin.Manual);
        orden.OrderKind.Should().Be("HONOR");
        (await _t.Validador.ValidarAsync(orden, default)).Motivos.Should().Contain(ErroresDeTesoreria.CodigoOrdenManualSinAprobar);
    }

    [Fact]
    public async Task Quien_prepara_no_aprueba_y_la_segunda_aprobacion_es_de_otra_persona()
    {
        var r = await Preparar().Handle(Orden(_reintegro, 500_000m), default);

        var mismo = await Aprobar().Handle(new ApproveManualPaymentOrderCommand(r.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);

        mismo.Error.Code.Should().Be(ErroresDeTesoreria.CodigoOrdenManualAutoaprobada);
    }

    [Fact]
    public async Task Con_las_dos_aprobaciones_entra_a_la_bandeja_lista()
    {
        var r = await Preparar().Handle(Orden(_reintegro, 500_000m), default);
        var orden = await _t.E.Db.PaymentOrders.SingleAsync();
        orden.PreparedByUserId = 9_001;
        orden.FirstApprovedByUserId = 9_002;
        orden.ManualApprovalState = ManualOrderApprovalState.PendingSecondApproval;
        await _t.E.Db.SaveChangesAsync();

        var segunda = await Aprobar().Handle(new ApproveManualPaymentOrderCommand(r.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);

        segunda.IsSuccess.Should().BeTrue(segunda.IsFailure ? segunda.Error.Code : null);
        segunda.Value.ApprovalState.Should().Be(ManualOrderApprovalState.Approved);
        segunda.Value.Status.Should().Be(PaymentOrderState.Ready);
    }

    [Fact]
    public async Task El_beneficiario_no_prepara_su_propia_orden()
    {
        var cajera = await _t.E.Db.Users.SingleAsync(u => u.Id == _t.E.Cajera.Id);
        cajera.PersonId = _t.Beneficiaria.Id;
        await _t.E.Db.SaveChangesAsync();

        var r = await Preparar().Handle(Orden(_reintegro, 100m), default);

        r.Error.Code.Should().Be(ErroresDeTesoreria.CodigoOrdenManualBeneficiarioPrepara);
    }

    [Fact]
    public void El_pago_girado_de_una_orden_con_retencion_lleva_la_retencion_por_pagar()
    {
        var orden = new PaymentOrder { Origin = PaymentOrderOrigin.Manual, WithholdingAmount = 110_000m, NetAmount = 890_000m, Number = "OP-9", OriginOrderNumber = "OP-9" };
        var lineas = EmisionDeTesoreria.LineasDelPago(orden, new DisbursementResult { Amount = 890_000m }, new TreasuryFactDimensionsV1(), Guid.NewGuid());

        lineas.Should().Contain(l => l.Role == RolesDeTesoreriaT1.RetencionEnLaFuentePorPagar && l.Amount == 110_000m);
        lineas.Where(l => l.Side == RolesDeTesoreria.Debito).Sum(l => l.Amount).Should().Be(lineas.Where(l => l.Side == RolesDeTesoreria.Credito).Sum(l => l.Amount));
    }
}
