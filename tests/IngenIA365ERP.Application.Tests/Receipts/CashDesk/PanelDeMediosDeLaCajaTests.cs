using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 022 (T032, T033; plan §8.5–§8.6): la sesión de la caja trae lo que el panel único de medios necesita sin el catálogo —tecla rápida, vueltas,
/// referencia, orden y los datáfonos activos del adquirente de cada tarjeta— y la tarjeta en Recaudo guarda los cuatro últimos dígitos, opcionales y
/// exactamente cuatro números (nunca el número completo).
/// </summary>
public class PanelDeMediosDeLaCajaTests
{
    private readonly CajaConContratos _k = new();

    private (PaymentMeans Tarjeta, CardTerminal Activo) TarjetaOfrecida()
    {
        var e = _k.C.E;
        var adquirente = new CardAcquirer { Code = "CRB", Name = "Credibanco" };
        var otro = new CardAcquirer { Code = "RBM", Name = "Redeban" };
        e.Db.CardAcquirers.AddRange(adquirente, otro);
        e.Db.SaveChanges();
        var activo = new CardTerminal { CardAcquirerId = adquirente.Id, Code = "D-01" };
        e.Db.CardTerminals.AddRange(activo,
            new CardTerminal { CardAcquirerId = adquirente.Id, Code = "D-02", IsActive = false },
            new CardTerminal { CardAcquirerId = otro.Id, Code = "D-99" });
        var tarjeta = e.Medio("TD", PaymentMeansClass.DebitCard, 0);
        tarjeta.CardAcquirerId = adquirente.Id;
        tarjeta.QuickKey = "T";
        tarjeta.RequiresReference = true;
        tarjeta.ReferenceKind = PaymentReferenceKind.Approval;
        tarjeta.DisplayOrder = 2;
        _k.C.Efectivo.QuickKey = "E";
        e.Db.SaveChanges();
        e.Ofrecer(_k.C.Punto, tarjeta);
        return (tarjeta, activo);
    }

    [Fact]
    public async Task La_sesion_trae_tecla_vueltas_referencia_orden_y_los_datafonos_del_adquirente()
    {
        var (tarjeta, activo) = TarjetaOfrecida();

        await _k.AbrirAsync();

        var medios = _k.Sesion.Means;
        var td = medios.Single(m => m.PaymentMeansPublicId == tarjeta.PublicId);
        td.QuickKey.Should().Be("T");
        td.RequiresReference.Should().BeTrue();
        td.ReferenceKind.Should().Be(PaymentReferenceKind.Approval);
        td.DisplayOrder.Should().Be(2);
        td.AllowsChange.Should().BeFalse();
        td.CardTerminals!.Should().ContainSingle().Which.Should().Be(new IngenIA365ERP.Application.Receipts.Sessions.DatafonoDeSesionDto(activo.PublicId, "D-01", "CRB"));

        var ef = medios.Single(m => m.PaymentMeansPublicId == _k.C.Efectivo.PublicId);
        ef.QuickKey.Should().Be("E");
        ef.AllowsChange.Should().BeTrue();
        ef.CardTerminals.Should().BeNull("sólo las tarjetas llevan datáfonos");
    }

    [Fact]
    public void Los_ultimos_cuatro_son_exactamente_cuatro_digitos()
    {
        var v = new UpdateCashOperationMeansCommandValidator();
        UpdateCashOperationMeansCommand Con(string? last4) => new(Guid.NewGuid(), [new MedioDeCajaDto(Guid.NewGuid(), 1_000, Last4: last4)]);

        v.TestValidate(Con("1234")).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(Con(null)).ShouldNotHaveAnyValidationErrors();
        v.TestValidate(Con("123")).IsValid.Should().BeFalse();
        v.TestValidate(Con("12a4")).IsValid.Should().BeFalse();
        v.TestValidate(Con("4111111111111111")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task La_tarjeta_guarda_los_cuatro_ultimos_y_rechaza_lo_que_no_lo_es()
    {
        var (tarjeta, activo) = TarjetaOfrecida();
        await _k.AbrirAsync();
        MedioDeCajaDto Tarjeta(string? last4) => new(tarjeta.PublicId, 100_000, CardTerminalPublicId: activo.PublicId, TerminalBatch: "L-7",
            ApprovalCode: "A-123", Last4: last4);

        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, 100_000)]), default);

        // La validación de la caja (sin pasar por el validador) también lo rechaza: «12 4» no son cuatro dígitos.
        var mal = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [Tarjeta("12 4")]), default);
        mal.Error.Code.Should().Be(ErroresDeRecaudo.CodigoUltimosCuatroInvalidos);

        var bien = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [Tarjeta("0042")]), default);
        bien.IsSuccess.Should().BeTrue(bien.IsFailure ? bien.Error.Code + " " + bien.Error.Message : "");
        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + " " + r.Error.Message : "");
        (await _k.C.E.Db.ReceiptMeans.SingleAsync()).Last4.Should().Be("0042");
    }
}
