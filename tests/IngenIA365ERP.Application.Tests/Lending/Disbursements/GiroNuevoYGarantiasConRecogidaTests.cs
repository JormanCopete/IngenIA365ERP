using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Disbursements.Collaterals;
using IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Application.Tests.Lending.Sustitutos;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 028 (T038, T039, T044; FR-027c, FR-027d, D12; H6, H7): con el contrato de obligaciones real (<see cref="PagosDePrueba"/>), el crédito nuevo de 5.000.000
/// recoge el crédito de Ana.
/// <list type="bullet">
/// <item><b>Giro nuevo</b>: tras un giro rechazado, el hecho <c>CreditoDesembolsado</c> y la <c>OrdenDeGiroEmitida</c> nuevos llevan las mismas recogidas
/// —los pagos <c>CAR</c> vivos del primer desembolso del tramo— y el mismo neto que la orden; también en un segundo giro nuevo, y sin la recogida que ya no
/// está viva.</item>
/// <item><b>Garantías y pagaré del recogido</b>: no se liberan (ni se anula el pagaré) mientras el crédito nuevo no esté girado
/// (<c>Lending.Collateral.PendingNewLoanDisbursement</c>, «Por liberar al girar el crédito …» en el detalle); se liberan cuando Tesorería informa la orden
/// <c>Disbursed</c>; si el crédito nuevo se anula, la recogida se reversa y el recogido vuelve a estar vivo con ellas; lo cancelado por caja libera como
/// antes; y un tramo sin orden de giro queda en firme al desembolsarse.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class GiroNuevoYGarantiasConRecogidaTests
{
    private const decimal Bruto = 5_000_000m;

    // ------------------------------------------------------------------------------------------------------------ piezas --

    /// <summary>La solicitud de 5.000.000 de Ana que recoge su crédito, aprobada y desembolsada por transferencia con el contrato real.</summary>
    private static async Task<DisbursementResultDto> DesembolsadoConRecogidaAsync(PagosDePrueba p)
    {
        var s = await p.D.AprobadaAsync(p.D.O.Solicitud(monto: Bruto) with { Payoffs = [DesembolsoConDiferenciasTests.Total(p.Credito)] });
        var d = await p.D.DesembolsadoAsync(p.D.Entrada(s) with { AcknowledgedPayoffDifferences = true }, p.Contrato());
        d.Payoffs.Should().ContainSingle();
        (await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.PaidOff);
        return d;
    }

    /// <summary>Tesorería (el doble de prueba) informa el resultado de la orden y Cartera lo consume.</summary>
    private static async Task ResultadoAsync(PagosDePrueba p, Guid orden, string resultado)
    {
        var emitido = await p.D.EmitirResultado().Handle(new EmitTestDisbursementResultCommand(orden, resultado, "TRF-9", p.G.HoyLocal)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        emitido.IsSuccess.Should().BeTrue(emitido.IsFailure ? $"{emitido.Error.Code}: {emitido.Error.Message}" : string.Empty);
        p.Db.ChangeTracker.Clear();
        var consumido = await p.D.Consumir().Handle(new ConsumeTreasuryResultCommand([emitido.Value.MessagePublicId]), default);
        consumido.IsSuccess.Should().BeTrue(consumido.IsFailure ? consumido.Error.Message : string.Empty);
        consumido.Value.Should().BeOfType<ResultadoDeConsumo.Processed>();
        p.Db.ChangeTracker.Clear();
    }

    private static async Task<DisbursementOrderDto> GiroNuevoAsync(PagosDePrueba p, Guid orden)
    {
        var r = await p.D.GirarDeNuevo().Handle(new ReissueDisbursementOrderCommand(orden, DisbursementForm.Transfer,
            new BeneficiaryInput(p.D.O.Ana.PublicId, p.D.Banco.PublicId, BeneficiaryInput.Corriente, "777"), "Cuenta corregida") { OperationKey = Guid.NewGuid() }, default);
        p.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private static async Task<JsonElement> UltimoAsync(PagosDePrueba p, string tipo)
    {
        var m = await p.Db.IntegrationMessages.AsNoTracking().Where(x => x.Type == tipo).OrderByDescending(x => x.Id).FirstAsync();
        return JsonDocument.Parse(m.PayloadJson).RootElement.Clone();
    }

    private static async Task<Collateral> GarantiaDelRecogidoAsync(PagosDePrueba p)
    {
        var g = new Collateral
        {
            Kind = CollateralKind.Pledge, OwnerPersonId = p.D.O.Ana.Id, LoanId = p.Credito.Id, Description = "Motocicleta placa XYZ12A", Value = 6_000_000m,
            AdmissibleValue = 4_200_000m, Status = CollateralStatus.Valid, ConstitutedOn = new DateOnly(2026, 1, 15), CreatedBy = "test",
        };
        p.Db.Collaterals.Add(g);
        p.Db.PromissoryNotes.Add(new PromissoryNote
        {
            Kind = PromissoryNoteKind.Physical, LoanId = p.Credito.Id, Number = "P-RECOGIDO", IssuedOn = new DateOnly(2026, 1, 15),
            Status = PromissoryNoteStatus.InCustody, PhysicalLocation = "Caja fuerte", CreatedBy = "test",
        });
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();
        return g;
    }

    private static ReleaseCollateralCommandHandler Liberar(PagosDePrueba p) =>
        new(p.Db, p.D.Emision(), p.D.FechaContable(), p.G.Alcance, p.G.Auditoria, p.G.Actor, p.G.Reloj);

    private static VoidPromissoryNoteCommandHandler AnularPagare(PagosDePrueba p) =>
        new(p.Db, new DepositoDelPagare(p.Db, Substitute.For<IDepositosDeValores>(), Substitute.For<ICredencialesDeCanalExterno>(),
            Substitute.For<ICurrentTenantService>(), Options.Create(p.D.O.Opciones)), p.G.Alcance, p.G.Auditoria, p.G.Reloj);

    private static async Task<IngenIA365ERP.Application.Common.Models.Result<CollateralDto>> LiberarAsync(PagosDePrueba p, Guid garantia)
    {
        var r = await Liberar(p).Handle(new ReleaseCollateralCommand(garantia, "El crédito quedó cancelado") { OperationKey = Guid.NewGuid() }, default);
        p.Db.ChangeTracker.Clear();
        return r;
    }

    private static async Task<IngenIA365ERP.Application.Common.Models.Result<PromissoryNoteDto>> AnularElPagareAsync(PagosDePrueba p)
    {
        var pagare = await p.Db.PromissoryNotes.AsNoTracking().SingleAsync(x => x.LoanId == p.Credito.Id);
        var r = await AnularPagare(p).Handle(new VoidPromissoryNoteCommand(pagare.PublicId, "Se devuelve al deudor") { OperationKey = Guid.NewGuid() }, default);
        p.Db.ChangeTracker.Clear();
        return r;
    }

    private static async Task<CollateralDto> EnElDetalleAsync(PagosDePrueba p)
    {
        var r = await new ListCollateralsQueryHandler(p.Db, p.G.Alcance, p.G.Reloj).Handle(new ListCollateralsQuery(LoanPublicId: p.Credito.PublicId), default);
        r.IsSuccess.Should().BeTrue();
        return r.Value.Items.Should().ContainSingle().Subject;
    }

    private static async Task<PromissoryNoteDto> PagareEnElDetalleAsync(PagosDePrueba p)
    {
        var filas = await p.Db.PromissoryNotes.AsNoTracking().Where(x => x.LoanId == p.Credito.Id).ToListAsync();
        return (await DepositoDelPagare.DtosAsync(p.Db, null, filas, default)).Should().ContainSingle().Subject;
    }

    // ---------------------------------------------------------------------------------------------------- giro nuevo (T038) --

    [Fact]
    public async Task El_giro_nuevo_lleva_las_recogidas_del_desembolso_y_el_mismo_neto_que_su_orden()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var d = await DesembolsadoConRecogidaAsync(p);
        var recogida = d.Payoffs.Single();
        await ResultadoAsync(p, d.Order!.PublicId, "Rejected");

        var giro = await GiroNuevoAsync(p, d.Order.PublicId);

        (giro.GrossAmount, giro.PayoffAmount, giro.NetAmount).Should().Be((Bruto, recogida.Applied, Bruto - recogida.Applied),
            "la orden nueva descuenta lo aplicado a la recogida, como la primera");
        giro.NetAmount.Should().Be(d.NetToDisburse);
        var hecho = await UltimoAsync(p, CreditoDesembolsadoV1.Type);
        var deduccion = hecho.GetProperty("payoffDeductions").EnumerateArray().Should().ContainSingle().Subject;
        deduccion.GetProperty("loan").GetProperty("loanPublicId").GetGuid().Should().Be(p.Credito.PublicId);
        deduccion.GetProperty("operationPublicId").GetGuid().Should().Be(recogida.ReceiptOperationPublicId, "es el mismo pago CAR que aplicó la recogida");
        deduccion.GetProperty("amount").GetDecimal().Should().Be(recogida.Applied);
        hecho.GetProperty("grossAmount").GetDecimal().Should().Be(Bruto);
        hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(giro.NetAmount, "el hecho y la orden dicen el mismo neto (H6)");
        hecho.GetProperty("disbursementOrderPublicId").GetGuid().Should().Be(giro.PublicId);
        var orden = await UltimoAsync(p, OrdenDeGiroEmitidaV1.Type);
        orden.GetProperty("orderPublicId").GetGuid().Should().Be(giro.PublicId);
        orden.GetProperty("payoffDeductions").EnumerateArray().Single().GetProperty("amount").GetDecimal().Should().Be(recogida.Applied);
        orden.GetProperty("netAmount").GetDecimal().Should().Be(giro.NetAmount);
    }

    [Fact]
    public async Task Un_segundo_giro_nuevo_sigue_llevando_las_recogidas_del_primer_desembolso()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var d = await DesembolsadoConRecogidaAsync(p);
        await ResultadoAsync(p, d.Order!.PublicId, "Rejected");
        var primero = await GiroNuevoAsync(p, d.Order.PublicId);
        await ResultadoAsync(p, primero.PublicId, "Rejected");

        var segundo = await GiroNuevoAsync(p, primero.PublicId);

        segundo.PreviousOrderNumber.Should().Be(primero.Number);
        (segundo.PayoffAmount, segundo.NetAmount).Should().Be((d.Payoffs.Single().Applied, d.NetToDisburse),
            "la clave CAR es la del primer desembolso del tramo, no la del giro reversado que se reemplaza");
        var hecho = await UltimoAsync(p, CreditoDesembolsadoV1.Type);
        hecho.GetProperty("payoffDeductions").GetArrayLength().Should().Be(1);
        hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(segundo.NetAmount);
    }

    [Fact]
    public async Task Una_recogida_que_ya_no_esta_viva_no_se_descuenta_en_el_giro_nuevo()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var d = await DesembolsadoConRecogidaAsync(p);
        await ResultadoAsync(p, d.Order!.PublicId, "Rejected");
        // Un pago CAR reversado (lo que deja el reverso de la recogida) ya no cuenta como recogido.
        var pago = await p.Db.LoanOperations.SingleAsync(o => o.PublicId == d.Payoffs.Single().ReceiptOperationPublicId);
        pago.ReversedByOperationId = pago.Id;
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();

        var giro = await GiroNuevoAsync(p, d.Order.PublicId);

        (giro.PayoffAmount, giro.NetAmount).Should().Be((0m, Bruto));
        var hecho = await UltimoAsync(p, CreditoDesembolsadoV1.Type);
        hecho.GetProperty("payoffDeductions").GetArrayLength().Should().Be(0);
        hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(Bruto);
    }

    [Fact]
    public void La_clave_de_la_recogida_dice_su_desembolso_y_ninguna_otra_lo_dice()
    {
        var desembolso = Guid.NewGuid();
        var credito = Guid.NewGuid();
        var clave = RecogidaEnElDesembolso.Clave(desembolso, credito);

        RecogidaEnElDesembolso.DesembolsoDeLaClave(clave).Should().Be(desembolso);
        RecogidaEnElDesembolso.DesembolsoDeLaClave($"{clave}:reverso").Should().BeNull("el reverso no es la recogida");
        RecogidaEnElDesembolso.DesembolsoDeLaClave($"NOM:{desembolso:N}:{credito:N}").Should().BeNull();
        RecogidaEnElDesembolso.DesembolsoDeLaClave($"CAR:{desembolso:N}").Should().BeNull();
        RecogidaEnElDesembolso.DesembolsoDeLaClave(null).Should().BeNull();
    }

    // ------------------------------------------------------------------------------------- garantías del recogido (T039) --

    [Fact]
    public async Task Las_garantias_y_el_pagare_del_recogido_se_liberan_cuando_el_giro_del_credito_nuevo_queda_pagado()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var garantia = await GarantiaDelRecogidoAsync(p);
        var d = await DesembolsadoConRecogidaAsync(p);

        var antes = await LiberarAsync(p, garantia.PublicId);
        var pagareAntes = await AnularElPagareAsync(p);
        var enElDetalle = await EnElDetalleAsync(p);
        var pagareEnElDetalle = await PagareEnElDetalleAsync(p);

        antes.Error.Code.Should().Be(ErroresDeGarantias.CodigoPorLiberarAlGirar, "el cruce interno está en firme, pero el crédito nuevo no se ha girado");
        antes.Error.Message.Should().StartWith($"Por liberar al girar el crédito {d.LoanNumber}");
        var datos = DesembolsoDePrueba.Datos(antes.Error);
        (datos.GetProperty("newLoanNumber").GetString(), datos.GetProperty("newLoanPublicId").GetGuid()).Should().Be((d.LoanNumber, d.LoanPublicId));
        pagareAntes.Error.Code.Should().Be(ErroresDeGarantias.CodigoPorLiberarAlGirar, "el pagaré sigue la misma regla");
        enElDetalle.ReleaseNotice.Should().Be($"Por liberar al girar el crédito {d.LoanNumber}");
        pagareEnElDetalle.ReleaseNotice.Should().Be($"Por liberar al girar el crédito {d.LoanNumber}");

        await ResultadoAsync(p, d.Order!.PublicId, "Disbursed");
        var liberada = await LiberarAsync(p, garantia.PublicId);
        var anulado = await AnularElPagareAsync(p);

        liberada.IsSuccess.Should().BeTrue(liberada.IsFailure ? $"{liberada.Error.Code}: {liberada.Error.Message}" : string.Empty);
        liberada.Value.Status.Should().Be("Released");
        liberada.Value.ReleaseNotice.Should().BeNull();
        var fila = await p.Db.Collaterals.AsNoTracking().SingleAsync(x => x.Id == garantia.Id);
        var pago = await p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == d.Payoffs.Single().ReceiptOperationPublicId);
        fila.ReleaseOperationId.Should().Be(pago.Id, "la liberación anota el pago de la recogida que canceló el crédito");
        anulado.IsSuccess.Should().BeTrue(anulado.IsFailure ? $"{anulado.Error.Code}: {anulado.Error.Message}" : string.Empty);
        anulado.Value.Status.Should().Be("Voided");
    }

    [Fact]
    public async Task Si_el_credito_nuevo_se_anula_antes_del_giro_las_garantias_del_recogido_no_se_liberan()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var garantia = await GarantiaDelRecogidoAsync(p);
        var d = await DesembolsadoConRecogidaAsync(p);
        await ResultadoAsync(p, d.Order!.PublicId, "Rejected");

        var rechazado = await LiberarAsync(p, garantia.PublicId);
        rechazado.Error.Code.Should().Be(ErroresDeGarantias.CodigoPorLiberarAlGirar, "un giro rechazado no es un giro pagado");

        var anulado = await new VoidPendingLoanCommandHandler(p.Db, p.Contrato(), p.D.O.Cerrojo, p.G.Alcance, p.G.Auditoria, p.G.Reloj)
            .Handle(new VoidPendingLoanCommand(d.LoanPublicId, "El asociado desistió") { OperationKey = Guid.NewGuid() }, default);
        p.Db.ChangeTracker.Clear();
        anulado.IsSuccess.Should().BeTrue(anulado.IsFailure ? $"{anulado.Error.Code}: {anulado.Error.Message}" : string.Empty);
        (await p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == p.Credito.Id)).Status.Should().Be(LoanStatus.Active, "la recogida se reversó (C11)");

        var despues = await LiberarAsync(p, garantia.PublicId);

        despues.Error.Code.Should().Be(ErroresDeGarantias.CodigoCreditoNoCancelado, "el recogido vuelve a estar vivo con sus garantías");
        (await p.Db.Collaterals.AsNoTracking().SingleAsync(x => x.Id == garantia.Id)).Status.Should().Be(CollateralStatus.Valid);
        (await EnElDetalleAsync(p)).ReleaseNotice.Should().BeNull();
    }

    [Fact]
    public async Task Un_credito_cancelado_por_caja_libera_como_antes()
    {
        // La cancelación en caja mide el tope legal del prepago: el escenario siembra el SMMLV y el límite.
        using var p = await PagosDePrueba.CrearAsync();
        var garantia = await GarantiaDelRecogidoAsync(p);
        var listado = await p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, p.D.O.Ana.PublicId, p.G.HoyLocal), default);
        p.Db.ChangeTracker.Clear();
        var pago = await p.AplicadoAsync(p.Pago(ModalidadDePago.FullPayoff, listado.Value!.Obligations.Single().PayoffAmount));
        pago.PaidOff.Should().BeTrue();
        var firmeza = (await p.OperacionAsync(pago.OperationPublicId)).FinalityDate!.Value;

        var antes = await LiberarAsync(p, garantia.PublicId);
        var enElDetalle = await EnElDetalleAsync(p);
        p.G.HoyLocal = firmeza;
        var liberada = await LiberarAsync(p, garantia.PublicId);

        antes.Error.Code.Should().Be(ErroresDeGarantias.CodigoSinFirmeza, "el pago en efectivo espera su firmeza, como siempre");
        enElDetalle.ReleaseNotice.Should().BeNull("no lo recogió ningún desembolso");
        liberada.IsSuccess.Should().BeTrue(liberada.IsFailure ? $"{liberada.Error.Code}: {liberada.Error.Message}" : string.Empty);
        liberada.Value.Status.Should().Be("Released");
    }

    [Fact]
    public async Task Un_tramo_sin_orden_de_giro_queda_en_firme_al_desembolsarse_y_con_orden_espera_el_giro()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        var garantia = await GarantiaDelRecogidoAsync(p);
        var hoy = p.G.HoyLocal;
        // Un crédito nuevo con su tramo registrado y, todavía, sin orden; su recogida canceló el crédito de Ana con el cruce interno (en firme hoy).
        var nuevo = p.D.O.Credito(p.D.O.Ana, Bruto, "CR-NUEVO");
        var desembolso = new LoanOperation
        {
            LoanId = nuevo.Id, Kind = LoanOperationKind.Disbursement, Number = "DES-NUEVO", Sequence = 1, ValueDate = hoy, AccountingDate = hoy,
            RegisteredAt = p.G.UtcNow, Amount = Bruto, TrancheNumber = 1, DisbursementForm = DisbursementForm.Transfer, Payload = "{}", InputsSha256 = "x",
            RulesVersion = "LND-2026.1",
        };
        p.Db.LoanOperations.Add(desembolso);
        p.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = p.Credito.Id, Kind = LoanOperationKind.Payment, Number = "PAG-RECOGIDA", Sequence = 99, ValueDate = hoy, AccountingDate = hoy,
            RegisteredAt = p.G.UtcNow, Amount = 1_000_000m, CollectorCode = "CAR", CollectorClass = nameof(ClaseDeRecaudador.OwnLoanPayoff),
            IdempotencyKey = RecogidaEnElDesembolso.Clave(desembolso.PublicId, p.Credito.PublicId), FinalityDate = hoy, Payload = "{}", InputsSha256 = "x",
            RulesVersion = "LND-2026.1",
        });
        var recogido = await p.Db.Loans.SingleAsync(l => l.Id == p.Credito.Id);
        recogido.Status = LoanStatus.PaidOff;
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();

        var sinOrden = await GarantiasDeCartera.PorLiberarAlGirarAsync(p.Db, [p.Credito.Id], default);

        p.Db.DisbursementOrders.Add(new DisbursementOrder
        {
            LoanId = nuevo.Id, DisbursementOperationId = desembolso.Id, Number = "OG-NUEVO", Form = DisbursementForm.Transfer, BeneficiaryPersonId = p.D.O.Ana.Id,
            BeneficiaryTaxIdType = "CC", BeneficiaryTaxId = "1", BeneficiaryName = "ANA", GrossAmount = Bruto, PayoffAmount = 1_000_000m, NetAmount = 4_000_000m,
            ScheduledDate = hoy, IssuedAt = p.G.UtcNow, IssuedByUserId = p.D.Cajera.Id, ResultDueOn = hoy.AddDays(5), CreatedBy = "test",
        });
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();
        var conOrden = await LiberarAsync(p, garantia.PublicId);

        sinOrden.Should().BeEmpty("sin orden de giro, el crédito nuevo queda girado al desembolsarse");
        conOrden.Error.Code.Should().Be(ErroresDeGarantias.CodigoPorLiberarAlGirar);
        DesembolsoDePrueba.Datos(conOrden.Error).GetProperty("newLoanNumber").GetString().Should().Be("CR-NUEVO");
    }
}
