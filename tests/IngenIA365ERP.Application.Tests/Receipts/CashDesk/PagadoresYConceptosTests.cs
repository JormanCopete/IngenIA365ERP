using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, T0106 (FR-016, FR-040, FR-050, FR-177, FR-179): el pagador tercero viaja como tercero en los contratos y el recibo sale enmascarado; el
/// pagador ocasional que paga en efectivo sobre el umbral levanta <c>Recaudo.PagadorOcasionalSobreUmbral</c>; el concepto vario con sus reglas (tercero,
/// cruce, factura). Escrita en la fase 6; se corre al final.
/// </summary>
public class PagadoresYConceptosTests
{
    private readonly CajaConContratos _k = new();

    /// <summary>R1 (FR-177, «quién efectúa el pago»): con un pagador que no es el titular, la confirmación exige quién entrega el dinero; aquí, el mismo tercero.</summary>
    private async Task QuienEntregaAsync(Guid operacion, Guid persona) =>
        (await new SetCashOperationDeliveredByCommandHandler(_k.C.E.Db, _k.C.E.Alcance)
            .Handle(new SetCashOperationDeliveredByCommand(operacion, persona) { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();

    private async Task<Person> TerceroAsync(bool asociado = false)
    {
        var p = new Person { FirstName = "Tomás", LastName = "Tercero", TaxId = "3001", IsAssociate = asociado };
        _k.C.E.Db.People.Add(p);
        await _k.C.E.Db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task El_pagador_tercero_viaja_como_tercero_y_el_recibo_sale_enmascarado()
    {
        await _k.AbrirAsync();
        var tercero = await TerceroAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, tercero.PublicId, _k.Asociado.PublicId), default)).Value;
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, 100_000)]), default);
        await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_k.Efectivo(100_000)]), default);
        await QuienEntregaAsync(op.PublicId, tercero.PublicId);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        r.Value.PayerIsThirdParty.Should().BeTrue();
        r.Value.Masked.Should().BeTrue();
        await _k.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.All(p => p.PayerPublicId == tercero.PublicId)), Arg.Any<CancellationToken>());
        RecibosDeCaja.Enmascarar("CR-000123").Should().Be("•••••0123");
    }

    [Fact]
    public async Task El_pagador_ocasional_sobre_el_umbral_avisa_al_oficial()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftUmbralDeclaracionOrigenFondos, "50000");
        await _k.AbrirAsync();
        var tercero = await TerceroAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, tercero.PublicId, _k.Asociado.PublicId), default)).Value;
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, 100_000)]), default);
        await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_k.Efectivo(100_000)]), default);
        await QuienEntregaAsync(op.PublicId, tercero.PublicId);

        var sinDeclaracion = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        sinDeclaracion.Error.Code.Should().Be(ErroresDeRecaudo.CodigoDeclaracionRequerida);

        await _k.Declarar().Handle(new DeclareSourceOfFundsCommand(op.PublicId, "Venta de un vehículo", 100_000, Guid.NewGuid()), default);
        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        await _k.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoPagadorOcasionalSobreUmbral), Arg.Any<CancellationToken>());
        (await _k.C.E.Db.IssuedReceipts.SingleAsync()).SourceOfFundsDeclarationId.Should().NotBeNull();
    }

    [Fact]
    public async Task El_concepto_vario_exige_su_factura_su_cruce_y_su_tercero()
    {
        await _k.AbrirAsync();
        _k.Concepto.RequiresInvoice = true;
        await _k.C.E.Db.SaveChangesAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;

        var sinFactura = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId,
            [new ItemPedidoDto(ApplicationTarget.MiscConcept, 10_000, MiscConceptPublicId: _k.Concepto.PublicId)]), default);
        sinFactura.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        System.Text.Json.JsonSerializer.Serialize(((Application.Common.Models.ErrorConDatos)sinFactura.Error).Data).Should().Contain(ErroresDeRecaudo.CodigoFacturaRequerida);

        _k.Concepto.RequiresInvoice = false;
        _k.Concepto.RequiresCrossDocument = true;
        await _k.C.E.Db.SaveChangesAsync();
        var conCruce = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId,
            [new ItemPedidoDto(ApplicationTarget.MiscConcept, 10_000, MiscConceptPublicId: _k.Concepto.PublicId, CrossDocument: "FAC-9")]), default);
        conCruce.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task El_concepto_vario_se_cobra_sin_tocar_los_contratos()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([new ItemPedidoDto(ApplicationTarget.MiscConcept, 10_000, MiscConceptPublicId: _k.Concepto.PublicId)], [_k.Efectivo(10_000)]);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Value.Applications.Single().MiscConceptCode.Should().Be("CERT");
        await _k.Obligaciones.DidNotReceiveWithAnyArgs().AplicarAsync(default!, default);
        await _k.Cuentas.DidNotReceiveWithAnyArgs().ConsignarAsync(default!, default);
    }
}
