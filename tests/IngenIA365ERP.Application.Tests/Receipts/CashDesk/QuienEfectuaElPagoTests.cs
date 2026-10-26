using FluentAssertions;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, decisión del dueño del 2026-10-24 sobre T0115: quien efectúa el pago (<c>Receipts.Compliance.DeliveredByRequired</c>) es obligatorio cuando el
/// efectivo del recibo llega al umbral de la transacción individual (<c>Sarlaft.UmbralTransaccionIndividual</c>, parámetro con vigencia) y cuando el pagador no es
/// el titular de lo que paga; en lo demás es opcional. Registrarlo con <c>SetCashOperationDeliveredByCommand</c> deja confirmar. Prueba escrita, sin correr.
/// </summary>
public class QuienEfectuaElPagoTests
{
    private readonly CajaConContratos _k = new();

    public QuienEfectuaElPagoTests() =>
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftUmbralTransaccionIndividual, "1000000"); // bajo el tope de bloqueo de la caja C1 (3.000.000)

    [Fact]
    public async Task Bajo_el_umbral_y_pagando_lo_suyo_no_se_exige()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, 100_000)], [_k.Efectivo(100_000)]);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
    }

    [Fact]
    public async Task En_el_umbral_de_efectivo_se_exige_y_registrado_confirma()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, 1_000_000)], [_k.Efectivo(1_000_000)]);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoQuienEfectuaRequerido);
        r.Error.ToString().Should().Contain("CashThreshold");

        var quien = new Person { FirstName = "Pedro", LastName = "Paga", TaxId = "3001" };
        _k.C.E.Db.People.Add(quien);
        _k.C.E.Db.SaveChanges();
        (await new SetCashOperationDeliveredByCommandHandler(_k.C.E.Db, _k.C.E.Alcance)
            .Handle(new SetCashOperationDeliveredByCommand(op.PublicId, quien.PublicId) { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();

        var confirmado = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        confirmado.IsSuccess.Should().BeTrue(confirmado.IsFailure ? confirmado.Error.Code : null);
        confirmado.Value.DeliveredByPersonPublicId.Should().Be(quien.PublicId);
    }

    [Fact]
    public async Task Si_el_pagador_no_es_el_titular_se_exige()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, 100_000)], [_k.Efectivo(100_000)]);
        var titular = new Person { FirstName = "Tito", LastName = "Titular", TaxId = "3002" };
        _k.C.E.Db.People.Add(titular);
        _k.C.E.Db.SaveChanges();
        var operacion = await _k.C.E.Db.CashOperations.SingleAsync(o => o.PublicId == op.PublicId);
        operacion.ItemsJson = OperacionesDeCaja.Json(OperacionesDeCaja.Items(operacion).Select(i => i with { BeneficiaryPersonPublicId = titular.PublicId }).ToList());
        _k.C.E.Db.SaveChanges();

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoQuienEfectuaRequerido);
        r.Error.ToString().Should().Contain("PayerIsNotHolder");
    }
}
