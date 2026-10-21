using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 013, US3, T0418 (mensajes.md §3, §6, §7, §10.1, §11.3; interfaces.md §4.1–§4.2; T13, C10, C15): sobre la bandeja real
/// (<c>EmisorDeMensajes</c> con el catálogo agregado), un tramo emite en el mismo guardado <c>CreditoDesembolsado</c> (origen la operación, clase
/// <c>LoanDisbursement</c>, clave <c>Confirmation</c>, tercero el deudor) y <c>OrdenDeGiroEmitida</c> (origen la orden, <c>Treasury</c>, entrega
/// <c>Always</c>, tercero el beneficiario, <c>PaidByFile</c> como se pidió); el sobre lleva la fecha contable y la sucursal contable de la oficina;
/// el contenido, el tramo, las recogidas, el neto y el plan, sin cuentas contables; con A3, la baja de <c>CreditoAprobadoSinDesembolsar</c> aparte.
/// Lo origina la persona que desembolsa.
/// </summary>
public sealed class EmisionDelDesembolsoTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DesembolsoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private static JsonElement Contenido(Domain.Entities.Integration.Transactions.IntegrationMessage m) => JsonDocument.Parse(m.PayloadJson).RootElement;

    [Fact]
    public async Task Una_transferencia_emite_el_desembolso_y_la_orden_en_el_mismo_guardado()
    {
        var s = await _d.AprobadaAsync();

        var r = await _d.DesembolsadoAsync(_d.Entrada(s) with { PaidByFile = true });

        var mensajes = await _d.Db.IntegrationMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        var desembolso = mensajes.Single(m => m.Type == CreditoDesembolsadoV1.Type);
        var orden = mensajes.Single(m => m.Type == OrdenDeGiroEmitidaV1.Type);
        r.Messages.Select(m => m.Type).Should().BeEquivalentTo(CreditoDesembolsadoV1.Type, OrdenDeGiroEmitidaV1.Type);

        (desembolso.OriginModule, desembolso.OriginPublicId, desembolso.OriginDocumentClass, desembolso.OriginEventKey).Should()
            .Be((ModulosDeOrigen.Cartera, r.OperationPublicId, EmisionDeCartera.ClaseDesembolso, ClavesDeEvento.Confirmacion));
        desembolso.OriginNumber.Should().Be(r.OperationNumber);
        (orden.OriginPublicId, orden.OriginDocumentClass, orden.OriginEventKey).Should()
            .Be((r.Order!.PublicId, EmisionDeCartera.ClaseOrdenDeGiro, ClavesDeEvento.Confirmacion));
        var entregaDeLaOrden = await _d.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(e => e.MessageId == orden.Id);
        (entregaDeLaOrden.Destination, entregaDeLaOrden.Mode).Should().Be((IntegrationDestinations.Treasury, DeliveryMode.Always));
        Contenido(orden).GetProperty("paidByFile").GetBoolean().Should().BeTrue();
        (await _d.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(e => e.MessageId == desembolso.Id)).Destination
            .Should().Be(IntegrationDestinations.Accounting);
    }

    [Fact]
    public async Task El_sobre_lleva_fecha_contable_sucursal_de_la_oficina_y_el_tercero_de_cada_mensaje()
    {
        var s = await _d.AprobadaAsync();

        await _d.DesembolsadoAsync(_d.Entrada(s));

        var mensajes = await _d.Db.IntegrationMessages.AsNoTracking().ToListAsync();
        mensajes.Should().OnlyContain(m => m.OperationDate == DesembolsoDePrueba.Hoy && m.BranchPublicId == _d.O.Norte.PublicId);
        mensajes.Single(m => m.Type == CreditoDesembolsadoV1.Type).PersonPublicId.Should().Be(_d.O.Ana.PublicId);
        mensajes.Single(m => m.Type == OrdenDeGiroEmitidaV1.Type).PersonPublicId.Should().Be(_d.O.Ana.PublicId);
        mensajes.Should().OnlyContain(m => m.OriginActorKind == ActorKind.Person && m.OriginUserCentralId == _d.Cajera.CentralUserId,
            "lo origina la persona que desembolsa, sin proceso");
    }

    [Fact]
    public async Task El_contenido_lleva_tramo_recogidas_neto_y_plan_sin_cuentas_contables()
    {
        var s = await _d.AprobadaAsync();

        await _d.DesembolsadoAsync(_d.Entrada(s));

        var c = Contenido(await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoDesembolsadoV1.Type));
        c.GetProperty("tranche").GetProperty("number").GetInt32().Should().Be(1);
        c.GetProperty("tranche").GetProperty("isLast").GetBoolean().Should().BeTrue();
        c.GetProperty("payoffDeductions").GetArrayLength().Should().Be(0);
        c.GetProperty("grossAmount").GetDecimal().Should().Be(3_000_000m);
        c.GetProperty("netToDisburse").GetDecimal().Should().Be(3_000_000m);
        c.GetProperty("disbursementForm").GetString().Should().Be("Transfer");
        c.GetProperty("plan").GetProperty("installmentCount").GetInt32().Should().Be(24);
        c.GetProperty("amounts")[0].GetProperty("reasonCode").GetString().Should().Be("Transfer");
        c.GetRawText().Should().NotContainAny("accountCode", "\"account\"");
    }

    [Fact]
    public async Task Un_giro_a_tercero_sella_otro_beneficiario_y_un_cheque_va_sin_cuenta()
    {
        var s = await _d.AprobadaAsync();

        var r = await _d.DesembolsadoAsync(_d.Entrada(s) with
        {
            Form = DisbursementForm.ThirdPartyPayment,
            Beneficiary = new BeneficiaryInput(_d.O.Carla.PublicId, _d.Banco.PublicId, BeneficiaryInput.Corriente, "999"),
        });

        var orden = await _d.Db.DisbursementOrders.AsNoTracking().SingleAsync();
        orden.BeneficiaryPersonId.Should().Be(_d.O.Carla.Id);
        orden.PaidByFile.Should().BeFalse("sólo una transferencia sale en archivo");
        var giro = Contenido(await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == OrdenDeGiroEmitidaV1.Type));
        giro.GetProperty("payee").GetProperty("personPublicId").GetGuid().Should().Be(_d.O.Carla.PublicId);
        r.Order.Should().NotBeNull();

        using var d2 = await DesembolsoDePrueba.CrearAsync();
        var s2 = await d2.AprobadaAsync();
        await d2.DesembolsadoAsync(d2.Entrada(s2) with { Form = DisbursementForm.Check, Beneficiary = null });
        var cheque = await d2.Db.DisbursementOrders.AsNoTracking().SingleAsync();
        (cheque.DestinationBankId, cheque.DestinationAccountNumber, cheque.Form).Should().Be(((int?)null, (string?)null, DisbursementForm.Check));
    }

    [Fact]
    public async Task Con_A3_el_tramo_emite_aparte_la_baja_del_aprobado_sin_desembolsar()
    {
        var tipo = new LendingFactType
        {
            Code = CreditoAprobadoSinDesembolsarV1.Type, ShortCode = "CAD", Name = "Aprobado sin desembolsar", DefaultMode = ModoDePasoDeCartera.NoPasa,
        };
        _d.Db.LendingFactTypes.Add(tipo);
        await _d.Db.SaveChangesAsync();
        var s = await _d.AprobadaAsync();

        var r = await _d.DesembolsadoAsync(_d.Entrada(s));

        var baja = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoAprobadoSinDesembolsarV1.Type);
        (baja.OriginPublicId, baja.OriginEventKey).Should().Be((r.OperationPublicId, ClavesDeEvento.ConfirmacionPor(tipo.PublicId)));
        (await _d.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(e => e.MessageId == baja.Id)).Mode.Should().Be(DeliveryMode.NotPosted);
        var c = Contenido(baja);
        c.GetProperty("movement").GetString().Should().Be("Disbursed");
        c.GetProperty("amounts")[0].GetProperty("amount").GetDecimal().Should().Be(-3_000_000m);
    }
}
