using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Integration;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Application.Tests.Accounting.Lending;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Application.Tests.Lending.Sustitutos;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payoffs;
using IngenIA365ERP.Domain.Sales.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028, HU7 (T084–T090; R14, E-16, FR-026): la recogida de facturas a crédito de Comercial en el desembolso, del lado de Recaudo. Sobre el escenario de
/// los bancos (<see cref="BancosDePrueba"/>: la asociada, Contabilidad que informa la factura FV-1001 por $73.425) con la aplicación no interactiva y la anulación
/// reales: el recibo sin caja del canal <c>LoanDisbursement</c> con el medio del cruce interno deja la factura en cero (o con el resto, si es parcial), su
/// <c>ReciboEmitido</c> debita la recogida y acredita la cuenta por cobrar con el deudor y la factura —la aplicación que Transporte escucha para marcar pagado el
/// cargo—; sin su tipo de documento o su medio no emite y dice por qué; una factura que cambió no se paga; los cerrojos son los de Recaudo; la anulación es el
/// espejo exacto, sin saldo a favor, y la factura vuelve; desde la caja no se anula. (nuevo)
/// </summary>
public sealed class RecogidaDeFacturasEnRecaudoTests
{
    private const string Factura = "FV-1001";
    private const decimal Saldo = 73_425m;

    private readonly BancosDePrueba _b = new();
    private readonly Guid _documento = Guid.NewGuid();
    private readonly PaymentMeans _medio;

    public RecogidaDeFacturasEnRecaudoTests()
    {
        var e = _b.K.C.E;
        e.Como(e.Cajera);
        _b.Contabilidad.CuentasPorCobrarComercialesAsync(_b.K.Asociado.PublicId, Arg.Any<CancellationToken>())
            .Returns([new CuentaPorCobrarComercialDto(_documento, Factura, EscenarioDeRecaudo.Hoy.AddDays(-10), Saldo)]);
        _medio = e.Medio("RECOGIDA", PaymentMeansClass.InternalOffset, 0m, CashCountMethod.None);
        e.Db.ReceiptDocumentTypes.AddRange(
            new ReceiptDocumentType { Code = "RD", Name = "Recogida en el desembolso", Kind = ReceiptDocumentKind.LoanDisbursementReceipt },
            new ReceiptDocumentType { Code = "AN", Name = "Anulación", Kind = ReceiptDocumentKind.Voiding });
        e.Db.SaveChanges();
    }

    private RecogidaEnRecaudo Recaudo()
    {
        var e = _b.K.C.E;
        return new RecogidaEnRecaudo(e.Db, _b.K.C.Cerrojo, _b.Contabilidad, _b.Aplicacion,
            new AplicacionDeAnulacion(e.Db, _b.K.C.Numerador, _b.K.Obligaciones, _b.K.Cuentas, _b.Emision, e.Reloj), e.Actor, _b.Auditoria, e.Reloj);
    }

    private SolicitudDeRecogidaEnRecaudo Solicitud(decimal valor, Guid? desembolso = null)
    {
        var op = desembolso ?? Guid.NewGuid();
        return new SolicitudDeRecogidaEnRecaudo(RecogidaDeFacturas.ClaveDelRecibo(op), _b.K.Asociado.PublicId, _b.K.C.E.OficinaA.PublicId, EscenarioDeRecaudo.Hoy,
            [new FacturaARecoger(Factura, valor, RecogidaDeFacturas.Clave(op, Factura))], "DES-000001", "CR-NUEVO");
    }

    private Task<IReadOnlyList<CommercialReceivableDto>> PendientesAsync() =>
        CuentasPorCobrarDeRecaudo.PendientesAsync(_b.K.C.E.Db, _b.Contabilidad, _b.K.Asociado.PublicId, default);

    private async Task<T> HechoAsync<T>(string tipo) =>
        JsonSerializer.Deserialize<T>((await _b.K.C.E.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == tipo)).PayloadJson,
            OpcionesDeMensajes.Opciones)!;

    [Fact]
    public async Task HU7_el_recibo_sin_caja_deja_la_factura_en_cero_y_emite_ReciboEmitido_como_la_caja()
    {
        var desembolso = Guid.NewGuid();

        var r = await Recaudo().AplicarAsync(Solicitud(Saldo, desembolso), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Invoices.Should().ContainSingle().Which.Should().Be(new FacturaRecogidaEnRecaudo(Factura, Saldo, RecogidaDeFacturas.Clave(desembolso, Factura)));
        var recibo = await _b.K.C.E.Db.IssuedReceipts.AsNoTracking().SingleAsync();
        (recibo.PublicId, recibo.Number, recibo.Channel, recibo.CashSessionId, recibo.TotalApplied)
            .Should().Be((r.Value.ReceiptPublicId, r.Value.Number, ReceiptChannel.LoanDisbursement, (int?)null, Saldo));
        (await _b.K.C.E.Db.ReceiptDocumentTypes.SingleAsync(t => t.Id == recibo.DocumentTypeId)).Kind.Should().Be(ReceiptDocumentKind.LoanDisbursementReceipt);
        var medio = await _b.K.C.E.Db.ReceiptMeans.AsNoTracking().SingleAsync();
        (medio.PaymentMeansId, medio.MeansClass, medio.Reference).Should().Be((_medio.Id, PaymentMeansClass.InternalOffset, "DES-000001 · CR-NUEVO"));
        (await _b.K.C.E.Db.CommercialReceivableApplications.SingleAsync()).Amount.Should().Be(Saldo);
        (await PendientesAsync()).Should().BeEmpty("la factura quedó en cero");
        _b.K.C.E.Db.PayerCreditBalances.Should().BeEmpty("no entró dinero que pueda sobrar");

        // El hecho es el de la caja: debita la recogida (el puente de Cartera) y acredita la cuenta por cobrar, los dos con el deudor y la factura.
        var hecho = await HechoAsync<ReciboEmitidoV1>(ReciboEmitidoV1.Type);
        hecho.Lines.Select(l => (l.Role, l.Side, l.Amount, l.ThirdPartyPersonPublicId, l.CrossDocument)).Should().BeEquivalentTo(new[]
        {
            (RolesDeRecaudo.RecogidaEnElDesembolso, RolesDeRecaudo.Debito, Saldo, (Guid?)_b.K.Asociado.PublicId, (string?)Factura),
            (RolesDeRecaudo.CuentasPorCobrarComerciales, RolesDeRecaudo.Credito, Saldo, (Guid?)_b.K.Asociado.PublicId, (string?)Factura),
        });
        // Transporte marca pagado el cargo de la factura con esta aplicación (Target CommercialReceivable y el número de la factura).
        hecho.Applications.Should().ContainSingle().Which.Should().Match<AplicacionDeReciboV1>(a =>
            a.Target == nameof(ApplicationTarget.CommercialReceivable) && a.ProductNumber == Factura && a.Amount == Saldo);
        CatalogoDeMensajes.Buscar(ReciboEmitidoV1.Type, 1)!.DestinosOpcionales.Should().Contain(IntegrationDestinations.Transport);
    }

    [Fact]
    public async Task HU7_1_parcial_aplica_ese_valor_y_la_factura_queda_con_el_resto()
    {
        var r = await Recaudo().AplicarAsync(Solicitud(50_000m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Invoices.Single().Applied.Should().Be(50_000m);
        (await PendientesAsync()).Should().ContainSingle().Which.Pending.Should().Be(Saldo - 50_000m);
    }

    [Fact]
    public async Task HU7_2_sin_el_tipo_de_documento_o_sin_el_medio_no_emite_y_dice_por_que()
    {
        var tipo = await _b.K.C.E.Db.ReceiptDocumentTypes.SingleAsync(t => t.Kind == ReceiptDocumentKind.LoanDisbursementReceipt);
        tipo.Status = ReceiptsCatalogStatus.Inactive;
        await _b.K.C.E.Db.SaveChangesAsync();

        var sinTipo = await Recaudo().AplicarAsync(Solicitud(Saldo), default);
        (await Recaudo().PendientesAsync(_b.K.Asociado.PublicId, default)).NotReadyReason.Should().Contain("Recogida en el desembolso");

        tipo.Status = ReceiptsCatalogStatus.Active;
        _medio.IsActive = false;
        await _b.K.C.E.Db.SaveChangesAsync();
        var sinMedio = await Recaudo().AplicarAsync(Solicitud(Saldo), default);

        sinTipo.Error.Code.Should().Be(RecogidaEnRecaudo.CodigoSinConfigurar);
        sinTipo.Error.Message.Should().Contain("Tipos de documento");
        sinMedio.Error.Code.Should().Be(RecogidaEnRecaudo.CodigoSinConfigurar);
        (await _b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_factura_cuyo_saldo_ya_no_alcanza_no_se_paga()
    {
        var r = await Recaudo().AplicarAsync(Solicitud(Saldo + 1m), default);

        r.Error.Code.Should().Be(RecogidaEnRecaudo.CodigoFacturaCambio);
        (await _b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Los_cerrojos_son_los_de_Recaudo_la_configuracion_el_pagador_y_al_anular_el_recibo()
    {
        var aplicado = await Recaudo().AplicarAsync(Solicitud(Saldo), default);
        _b.K.C.Cerrojo.ClearReceivedCalls();

        await Recaudo().TomarCerrojosAsync(_b.K.Asociado.PublicId, [aplicado.Value.ReceiptPublicId], default);

        Received.InOrder(() =>
        {
            _b.K.C.Cerrojo.TomarSetupAsync(false, Arg.Any<CancellationToken>());
            _b.K.C.Cerrojo.BloquearPagadoresAsync(Arg.Is<IReadOnlyCollection<int>>(l => l.Single() == _b.K.Asociado.Id), Arg.Any<CancellationToken>());
            _b.K.C.Cerrojo.BloquearAsync(Application.Receipts.Common.NivelDeRecaudo.Origenes, Application.Receipts.Common.TablasDeRecaudo.Recibos,
                Arg.Any<IReadOnlyCollection<long>>(), true, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task La_anulacion_es_el_espejo_del_recibo_sin_saldo_a_favor_y_la_factura_vuelve()
    {
        var aplicado = await Recaudo().AplicarAsync(Solicitud(Saldo), default);
        _b.K.C.E.Db.ChangeTracker.Clear();

        var r = await Recaudo().ReversarAsync(aplicado.Value.ReceiptPublicId, "El asociado desistió", default);
        var repetido = await Recaudo().ReversarAsync(aplicado.Value.ReceiptPublicId, "El asociado desistió", default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        repetido.Value.VoidingPublicId.Should().Be(r.Value.VoidingPublicId, "repetida, devuelve la anulación que ya tiene");
        (await _b.K.C.E.Db.IssuedReceipts.AsNoTracking().SingleAsync()).Status.Should().Be(ReceiptState.Voided);
        _b.K.C.E.Db.PayerCreditBalances.Should().BeEmpty("no entró dinero: no nace saldo a favor");
        (await PendientesAsync()).Should().ContainSingle().Which.Pending.Should().Be(Saldo, "la factura vuelve a quedar con su saldo");
        var anulado = await HechoAsync<ReciboAnuladoV1>(ReciboAnuladoV1.Type);
        anulado.Lines.Select(l => (l.Role, l.Side, l.Amount, l.CrossDocument)).Should().BeEquivalentTo(new[]
        {
            (RolesDeRecaudo.CuentasPorCobrarComerciales, RolesDeRecaudo.Debito, Saldo, (string?)Factura),
            (RolesDeRecaudo.RecogidaEnElDesembolso, RolesDeRecaudo.Credito, Saldo, (string?)Factura),
        });
    }

    [Fact]
    public void El_medio_del_cruce_interno_nunca_se_ofrece_al_cobrar()
    {
        var medio = new MedioOfrecible(1, "RECOGIDA", "Recogida en el desembolso", PaymentMeansClass.InternalOffset, 999, null, true, new DateOnly(2026, 1, 1), null,
            true, [], true, [], true, []);

        DisponibilidadDeMedio.SeOfrece(medio, new CasoDeCobro(EscenarioDeRecaudo.Hoy, 1, 1, 1, false, true)).Should().BeFalse();
        ClasesDeMedio.ArqueoPorDefecto(PaymentMeansClass.InternalOffset).Should().Be(CashCountMethod.None);
    }
}

/// <summary>
/// Feature 028, HU7 (T084–T090; R14, E-16, FR-026, FR-027): la recogida de facturas a crédito de Comercial en el desembolso, del lado de Cartera. Sobre el escenario
/// del desembolso (<see cref="DesembolsoDePrueba"/>) con el puerto de Recaudo doble (la factura FV-1001 por $73.425): de fábrica se ven sin poder marcarse; con
/// <c>FacturasComerciales</c> admitidas, total o parcial; el desembolso toma los cerrojos de Recaudo antes que los suyos, paga por el puerto con la clave
/// <c>CAR:{desembolso:N}:{factura}</c>, descuenta lo aplicado del neto, guarda su fila con el recibo y lleva la factura al hecho; si Recaudo no emite el recibo, el
/// desembolso no existe; la anulación del crédito nuevo anula el recibo antes que nada; el comprobante del desembolso acredita el puente por la factura y la
/// completitud avisa si las dos reglas no dan la misma cuenta. (nuevo)
/// </summary>
public sealed class RecogidaDeFacturasTests : IAsyncLifetime
{
    private const string Factura = "FV-1001";
    private const decimal Saldo = 73_425m;
    private const string Admite = "Creditos:Total,Parcial;FacturasComerciales:Total,Parcial";

    private DesembolsoDePrueba _d = null!;
    private readonly IRecogidaEnRecaudo _recaudo = Substitute.For<IRecogidaEnRecaudo>();
    private readonly Guid _recibo = Guid.NewGuid();
    private readonly List<string> _orden = [];

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _d.O.Recaudo = _recaudo;
        _recaudo.PendientesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new FacturasParaRecoger(
            [new FacturaPendienteEnRecaudo(Factura, Guid.NewGuid(), DesembolsoDePrueba.Hoy.AddDays(-10), Saldo)], null));
        _recaudo.TomarCerrojosAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask).AndDoes(_ => _orden.Add("Recaudo"));
        _d.O.Cerrojo.When(c => c.TomarSetupAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())).Do(_ => _orden.Add("Cartera"));
        _recaudo.AplicarAsync(Arg.Any<SolicitudDeRecogidaEnRecaudo>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeRecogidaEnRecaudo>();
            return Result.Success(new ReciboDeLaRecogida(_recibo, "RD-000001", s.Invoices.Select(f => new FacturaRecogidaEnRecaudo(f.InvoiceNumber, f.Amount, f.Key)).ToList()));
        });
        _recaudo.ReversarAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ReciboDeLaRecogidaAnulado(_recibo, Guid.NewGuid(), "AN-000001")));
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private static PayoffInput Total() => new(PayoffProductKind.CommercialReceivable, null, PayoffMode.Total, Reference: Factura);

    private static PayoffInput Parcial(decimal valor) => new(PayoffProductKind.CommercialReceivable, null, PayoffMode.Partial, valor, Factura);

    private void Admitir(string valor = Admite)
    {
        _d.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.RecogidaProductosAdmitidos, Value = valor, ScopeKind = ParameterScopeKind.None,
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test", CreatedAt = _d.G.UtcNow.AddDays(-10),
        });
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
    }

    private GetPayoffCandidatesQueryHandler Candidatos()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ListLoansQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ListLoansQueryHandler(_d.Db, _d.G.Alcance).Handle(ci.Arg<ListLoansQuery>(), ci.Arg<CancellationToken>()));
        return new GetPayoffCandidatesQueryHandler(_d.Db, sender, _d.LiquidacionDeRecogida(), _d.O.Capacidad(), _d.Parametros(), _d.G.Alcance, _d.G.Auditoria,
            _d.G.Reloj, Substitute.For<IBlobStore>(), facturas: _d.Facturas());
    }

    private async Task<PayoffCandidatesDto> CandidatosAsync(params PayoffInput[] marcados)
    {
        var r = await Candidatos().Handle(new GetPayoffCandidatesQuery(_d.O.Ana.PublicId, Selected: marcados.Length == 0 ? null : marcados,
            NewLoan: new PayoffNewLoanInput(1_000_000m)), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _d.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private async Task<DisbursementResultDto> DesembolsadoConAsync(PayoffInput recogida)
    {
        var s = await _d.AprobadaAsync(_d.O.Solicitud() with { Payoffs = [recogida] });
        _orden.Clear();
        return await _d.DesembolsadoAsync(_d.Entrada(s) with { AcknowledgedPayoffDifferences = true });
    }

    // ------------------------------------------------------------------------------------------------- T087 candidatos --

    [Fact]
    public async Task HU7_de_fabrica_las_facturas_se_ven_sin_poder_marcarse()
    {
        var r = await CandidatosAsync();

        var f = r.Products.Should().ContainSingle(p => p.Kind == PayoffProductKind.CommercialReceivable).Subject;
        (f.Reference, f.Number, f.ToPayOff, f.Eligible, f.BlockReason).Should().Be((Factura, Factura, Saldo, false, ErroresDeRecogida.MotivoNoAdmitido));
        f.BlockMessage.Should().Contain("no recoge facturas a crédito");
        f.ProductPublicId.Should().Be(FacturaRecogible.IdentificadorDe(Factura));
        // El mismo que calcula la pantalla (IdentificadoresDeRecogida.DeFactura en Shared, con el mismo valor fijado en su prueba).
        FacturaRecogible.IdentificadorDe(Factura).Should().Be(Guid.Parse("6a85d6f9-d2a5-f33d-a7d1-496853c57e16"));
    }

    [Fact]
    public async Task HU7_admitidas_se_marcan_total_o_parcial_y_el_resumen_lo_cuenta()
    {
        Admitir();

        var total = await CandidatosAsync(Total());
        var parcial = await CandidatosAsync(Parcial(50_000m));

        var f = total.Products.Single(p => p.Kind == PayoffProductKind.CommercialReceivable);
        (f.Eligible, f.AdmittedModes).Should().BeEquivalentTo((true, new[] { PayoffMode.Total, PayoffMode.Partial }));
        (total.Summary.SelectedTotal, total.Summary.NetToDisburse).Should().Be((Saldo, 1_000_000m - Saldo));
        parcial.Summary.SelectedTotal.Should().Be(50_000m);
    }

    [Fact]
    public async Task HU7_sin_Recaudo_listo_la_factura_no_se_marca_y_se_dice_por_que()
    {
        Admitir();
        _recaudo.PendientesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new FacturasParaRecoger(
            [new FacturaPendienteEnRecaudo(Factura, null, DesembolsoDePrueba.Hoy, Saldo)], "Recaudo no tiene un tipo de documento activo."));

        var f = (await CandidatosAsync()).Products.Single(p => p.Kind == PayoffProductKind.CommercialReceivable);

        (f.Eligible, f.BlockReason).Should().Be((false, ErroresDeRecogida.MotivoRecaudoSinConfigurar));
        f.BlockMessage.Should().Contain("tipo de documento");
    }

    // ------------------------------------------------------------------------------------------------- T087 desembolso --

    [Fact]
    public async Task HU7_el_desembolso_paga_la_factura_por_Recaudo_con_sus_cerrojos_primero_y_la_descuenta_del_neto()
    {
        Admitir();

        var d = await DesembolsadoConAsync(Total());

        _orden.Should().StartWith(["Recaudo", "Cartera"], "los cerrojos de Recaudo van antes que los de Cartera (E-16)");
        await _recaudo.Received(1).AplicarAsync(Arg.Is<SolicitudDeRecogidaEnRecaudo>(s => s.DebtorPersonPublicId == _d.O.Ana.PublicId
            && s.Key == RecogidaDeFacturas.ClaveDelRecibo(d.OperationPublicId) && s.DisbursementNumber == d.OperationNumber && s.NewLoanNumber == d.LoanNumber
            && s.Invoices.Single().InvoiceNumber == Factura && s.Invoices.Single().Amount == Saldo
            && s.Invoices.Single().Key == $"CAR:{d.OperationPublicId:N}:{Factura}"), Arg.Any<CancellationToken>());
        d.NetToDisburse.Should().Be(d.GrossAmount - Saldo, "la orden sale por el bruto menos lo aplicado");
        var aplicada = d.Payoffs.Should().ContainSingle().Subject;
        (aplicada.Kind, aplicada.Reference, aplicada.Applied, aplicada.ReceiptPublicId, aplicada.ReceiptNumber)
            .Should().Be((nameof(PayoffProductKind.CommercialReceivable), Factura, Saldo, (Guid?)_recibo, "RD-000001"));
        var fila = await _d.Db.DisbursementPayoffs.AsNoTracking().SingleAsync();
        (fila.ProductKind, fila.CommercialReceivableReference, fila.AppliedAmount, fila.ReceiptPublicId, fila.Mode)
            .Should().Be((PayoffProductKind.CommercialReceivable, Factura, Saldo, (Guid?)_recibo, PayoffMode.Total));
        var ds = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoDesembolsadoV1.Type);
        var hecho = JsonSerializer.Deserialize<CreditoDesembolsadoV1>(ds.PayloadJson, OpcionesDeMensajes.Opciones)!;
        var recogida = hecho.PayoffDeductions.Should().ContainSingle().Subject;
        (recogida.ClaseDeProducto(), recogida.ReceivableRef?.Number, recogida.OperationPublicId, recogida.Amount, recogida.PayerPersonPublicId)
            .Should().Be((PayoffDeductionV1.FacturaComercial, Factura, _recibo, Saldo, (Guid?)_d.O.Ana.PublicId));
        hecho.NetToDisburse.Should().Be(d.GrossAmount - Saldo);
        (await RecogidaEnElDesembolso.VivasAsync(_d.Db, [d.OperationPublicId], default)).Should().ContainSingle(
            r => r.ClaseDeProducto() == PayoffDeductionV1.FacturaComercial && r.Amount == Saldo, "el giro nuevo la vuelve a descontar (H6)");
    }

    [Fact]
    public async Task HU7_1_una_recogida_parcial_paga_ese_valor()
    {
        Admitir();

        var d = await DesembolsadoConAsync(Parcial(50_000m));

        await _recaudo.Received(1).AplicarAsync(Arg.Is<SolicitudDeRecogidaEnRecaudo>(s => s.Invoices.Single().Amount == 50_000m), Arg.Any<CancellationToken>());
        d.NetToDisburse.Should().Be(d.GrossAmount - 50_000m);
        (await _d.Db.DisbursementPayoffs.AsNoTracking().SingleAsync()).Mode.Should().Be(PayoffMode.Partial);
    }

    [Fact]
    public async Task HU7_2_si_Recaudo_no_emite_el_recibo_el_desembolso_no_existe_y_se_dice_por_que()
    {
        Admitir();
        var s = await _d.AprobadaAsync(_d.O.Solicitud() with { Payoffs = [Total()] });
        var creditos = await _d.Db.Loans.CountAsync();
        _recaudo.AplicarAsync(Arg.Any<SolicitudDeRecogidaEnRecaudo>(), Arg.Any<CancellationToken>()).Returns(
            Result.Failure<ReciboDeLaRecogida>(new Error(RecogidaEnRecaudo.CodigoSinConfigurar, "Recaudo no tiene un tipo de documento activo.")));

        var r = await _d.DesembolsarAsync(_d.Entrada(s));

        r.IsFailure.Should().BeTrue();
        (r.Error.Code, r.Error.Message).Should().Be((RecogidaEnRecaudo.CodigoSinConfigurar, "Recaudo no tiene un tipo de documento activo."));
        (await _d.Db.Loans.CountAsync()).Should().Be(creditos, "el crédito nuevo no existe");
        (await _d.Db.DisbursementPayoffs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_factura_ya_pagada_en_la_caja_se_omite_y_el_neto_sube()
    {
        Admitir();
        var s = await _d.AprobadaAsync(_d.O.Solicitud() with { Payoffs = [Total()] });
        _recaudo.PendientesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(FacturasParaRecoger.Ninguna);

        var vista = await _d.VistaPrevia().Handle(new PreviewDisbursementQuery(_d.Entrada(s)), default);
        var d = await _d.DesembolsadoAsync(_d.Entrada(s) with { AcknowledgedPayoffDifferences = true });

        var omitida = vista.Value.Payoffs.Should().ContainSingle().Subject;
        (omitida.Kind, omitida.Omitted, omitida.Reference).Should().Be((nameof(PayoffProductKind.CommercialReceivable), true, Factura));
        vista.Value.PayoffDifferences.RequiresAcknowledgement.Should().BeTrue();
        d.NetToDisburse.Should().Be(d.GrossAmount);
        d.OmittedPayoffs.Should().ContainSingle().Which.Reference.Should().Be(Factura);
        await _recaudo.DidNotReceive().AplicarAsync(Arg.Any<SolicitudDeRecogidaEnRecaudo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HU7_la_anulacion_del_credito_nuevo_anula_el_recibo_antes_que_nada_y_marca_la_fila()
    {
        Admitir();
        var d = await DesembolsadoConAsync(Total());
        var emitido = await _d.EmitirResultado().Handle(new EmitTestDisbursementResultCommand(d.Order!.PublicId, "Rejected", "TRF-9", DesembolsoDePrueba.Hoy)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _d.Db.ChangeTracker.Clear();
        (await _d.Consumir().Handle(new ConsumeTreasuryResultCommand([emitido.Value.MessagePublicId]), default)).IsSuccess.Should().BeTrue();
        _d.Db.ChangeTracker.Clear();
        _orden.Clear();

        var r = await _d.AnularCredito().Handle(new VoidPendingLoanCommand(d.LoanPublicId, "El asociado desistió") { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _orden.Should().StartWith(["Recaudo", "Cartera"]);
        await _recaudo.Received(1).ReversarAsync(_recibo, "El asociado desistió", Arg.Any<CancellationToken>());
        var fila = await _d.Db.DisbursementPayoffs.SingleAsync();
        fila.ReversalKey.Should().Be($"CAR:{d.OperationPublicId:N}:{Factura}:reverso");
        (await RecogidaEnElDesembolso.VivasAsync(_d.Db, [d.OperationPublicId], default)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------------- T088 contabilidad --

    [Fact]
    public void T088_el_comprobante_del_desembolso_acredita_el_puente_por_la_factura_con_el_deudor()
    {
        var deudor = Guid.NewGuid();
        var sucursal = Guid.NewGuid();
        var hecho = new HechoDeCartera(new IntegrationEnvelopeV1
        {
            MessageId = Guid.NewGuid(), Type = CreditoDesembolsadoV1.Type, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
            OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = sucursal,
        }, new CreditoDesembolsadoV1
        {
            Loan = new LoanRefV1 { LoanPublicId = Guid.NewGuid(), LoanNumber = "CR-NUEVO" },
            DebtorPersonPublicId = deudor,
            ValueDate = DesembolsoDePrueba.Hoy,
            Dimensions = new LoanFactDimensionsV1
            {
                CreditLineCode = "CONLIB", ModalityCode = "CONSUMO", RiskCategory = "A", RiskSubcategory = "None", BranchPublicId = sucursal,
                ImpairmentModel = "ArrearsAnnex1", RiskSegment = "CONSUMO", ValueUnit = "Pesos",
            },
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = 1_000_000m, ReasonCode = nameof(DisbursementForm.Transfer) }],
            Tranche = new TrancheV1 { Number = 1, IsLast = true },
            DisbursementForm = nameof(DisbursementForm.Transfer),
            PayeePersonPublicId = deudor,
            GrossAmount = 1_000_000m,
            PayoffDeductions = [RecogidaDeFacturas.ComoDeduccion(new RecogidaDeFacturaAplicada(new RecogidaDeFacturaEvaluada(Factura, PayoffMode.Total, Saldo),
                "CAR:x:FV-1001", _recibo, "RD-000001", Saldo), sucursal)],
            NetToDisburse = 1_000_000m - Saldo,
        }, Application.Lending.Integration.EmisionDeCartera.ClaseDesembolso);

        var d = ConstructorDeLineasDeCartera.Desglosar([hecho]);

        d.Fallos.Should().BeEmpty("la factura ya se sabe asentar (sin PayoffKindNotSupported)");
        var puente = d.Partidas.Should().ContainSingle(p => p.Rol == R.RecaudoPorAplicar).Subject;
        (puente.DebitoSiPositivo, puente.Importe, puente.Factura, puente.Tercero, puente.Valores.CollectorClass, puente.Valores.BranchPublicId)
            .Should().Be((false, Saldo, Factura, (Guid?)deudor, ConstructorDeLineasDeCartera.ClaseDeLaRecogidaPropia, sucursal));
        d.Partidas.Where(p => p.Rol == R.DesembolsoPorGirar).Sum(p => p.Importe).Should().Be(1_000_000m - Saldo, "«desembolso por girar», sólo el neto");
    }

    [Fact]
    public async Task T088_la_completitud_avisa_si_la_regla_de_Recaudo_y_el_puente_de_Cartera_no_dan_la_misma_cuenta()
    {
        var e = new EscenarioContableDeCartera();
        var registro = Substitute.For<IRegistroDeIntegracion>();
        registro.QueCuentanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<ParticipanteVigente>>([]));
        var catalogo = await e.Dimensiones.CatalogoAsync(default);
        e.Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(catalogo with { PayoffCommercialReceivablesAdmitted = true });
        var fecha = new DateOnly(2026, 11, 30);
        async Task<IReadOnlyList<AvisoDeCompletitudDeCarteraDto>> AvisosAsync() =>
            (await new GetLendingRulesCompletenessQueryHandler(e.D.Db, e.Dimensiones, registro, new TiposDeComprobanteDeCartera(e.D.Db))
                .Handle(new GetLendingRulesCompletenessQuery(fecha), default)).Value.Warnings
            .Where(a => a.Code == GetLendingRulesCompletenessQueryHandler.AvisoPuenteDeFacturasCruzado).ToList();
        var puente = await e.D.Db.LendingPostingRules.AsNoTracking().FirstAsync(r => r.Role == R.RecaudoPorAplicar && r.CollectorClass == "OwnLoanPayoff");
        var otra = await e.D.Db.ChartOfAccounts.AsNoTracking().FirstAsync(a => a.Id != puente.AccountId);

        var sinRegla = await AvisosAsync();
        var regla = new ReceiptsPostingRule
        {
            Role = RolesDeRecaudo.RecogidaEnElDesembolso, AccountId = otra.Id, ValidFrom = new DateOnly(2026, 1, 1), CreatedBy = "test",
        };
        e.D.Db.ReceiptsPostingRules.Add(regla);
        await e.D.Db.SaveChangesAsync();
        var distintas = await AvisosAsync();
        regla.AccountId = puente.AccountId;
        await e.D.Db.SaveChangesAsync();
        var iguales = await AvisosAsync();

        sinRegla.Should().ContainSingle().Which.Message.Should().Contain("no tiene regla para el rol «Recogida en el desembolso»");
        distintas.Should().ContainSingle().Which.Message.Should().Contain("no cuadra sola");
        iguales.Should().BeEmpty("las dos reglas dan la misma cuenta: el puente queda en cero");
    }
}
