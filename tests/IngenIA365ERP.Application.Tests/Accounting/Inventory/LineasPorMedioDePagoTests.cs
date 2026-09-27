using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T558 (T621, T622; FR-098, SC-024; contracts/contabilidad.md §2.2, §3.3, §3.4): el lado contable de los pagos y de
/// la caja. Cada pago llega a la cuenta de su medio —y, más específica, a la de su punto de venta (peso 16)—; <c>Received</c> va al
/// débito y <c>Refunded</c> al crédito; el tercero es el del pago según la clase del medio; la cuenta de un crédito cruza contra
/// <c>FV</c> + el número de la venta; el movimiento de caja va entre el medio y la caja de destino por <c>ReasonCode</c>, la
/// reclasificación entre el medio que sale y el que entra, y la diferencia de arqueo contra sobrante, faltante (con el cajero) o gasto
/// por tratamiento. Un medio activo sin regla aparece en la completitud y detiene la validación previa.
/// </summary>
public class LineasPorMedioDePagoTests
{
    private readonly EscenarioContable E = new();

    // ------------------------------------------------------------------------------------------------ unidades --

    private static PaymentLineV1 Pago(int linea, string medio, PaymentMeansClass clase, decimal valor, Guid? tercero = null,
        PaymentDirection direccion = PaymentDirection.Received) => new()
    {
        PaymentPublicId = Guid.NewGuid(), LineNumber = linea, PaymentMeansCode = medio, PaymentMeansClass = clase, Direction = direccion,
        Amount = valor, ThirdPartyPersonPublicId = tercero,
    };

    /// <summary>Una venta de un renglón de abarrotes sin impuestos, cobrada con <paramref name="pagos"/>.</summary>
    private static IReadOnlyList<MensajeDeUnidad> Venta(string punto, string numero, Guid? cliente, params PaymentLineV1[] pagos)
    {
        var total = pagos.Sum(p => p.Amount);
        var venta = new VentaFacturadaV1
        {
            PointOfSaleCode = punto,
            CashRegisterCode = "CAJA03",
            Lines = [new SalesAmountLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01", GrossAmount = total, NetAmount = total, DocumentLines = [1] }],
            Payments = pagos,
            Totals = new SalesTotalsV1 { Subtotal = total, Total = total, AmountDue = total },
        };
        var sobre = EscenarioContable.Sobre(VentaFacturadaV1.Type, clase: "SalesInvoice", tipoDoc: "FV", numero: numero, persona: cliente);
        return [new MensajeDeUnidad(sobre with { Payload = venta }, venta)];
    }

    private static IReadOnlyList<MensajeDeUnidad> Movimiento(CashMovementKind tipo, string medio, decimal valor, CashMovementDestination? destino = null,
        string? medioDestino = null, string? puntoDestino = null)
    {
        var m = new MovimientoDeCajaRegistradoV1
        {
            MovementKind = tipo, PaymentMeansCode = medio, PaymentMeansClass = PaymentMeansClass.Cash, DestinationPaymentMeansCode = medioDestino,
            PointOfSaleCode = "PTO01", CashRegisterCode = "CAJA03", CashSessionPublicId = Guid.NewGuid(), Destination = destino,
            DestinationPointOfSaleCode = puntoDestino, Amount = valor, Reason = "Prueba",
        };
        var sobre = EscenarioContable.Sobre(MovimientoDeCajaRegistradoV1.Type, clase: "CashMovement", tipoDoc: "MC", numero: "MC-1");
        return [new MensajeDeUnidad(sobre with { Payload = m }, m)];
    }

    private static IReadOnlyList<MensajeDeUnidad> Arqueo(params (string Medio, decimal Diferencia, CashDifferenceTreatment Tratamiento)[] lineas)
    {
        var a = new DiferenciaDeArqueoAprobadaV1
        {
            PointOfSaleCode = "PTO01", CashRegisterCode = "CAJA03", CashSessionPublicId = Guid.NewGuid(),
            Cashier = new CashierV1 { PersonPublicId = EscenarioContable.Cajero, Name = "Cajero de ensayo" },
            Lines = lineas.Select(l => new CashCountDifferenceLineV1
            {
                PaymentMeansCode = l.Medio, PaymentMeansClass = PaymentMeansClass.Cash, CountMethod = CashCountMethod.PhysicalCount,
                Expected = 100000m, Counted = 100000m + l.Diferencia, Difference = l.Diferencia, Treatment = l.Tratamiento, Reason = "Prueba",
            }).ToList(),
        };
        var sobre = EscenarioContable.Sobre(DiferenciaDeArqueoAprobadaV1.Type, clase: "CashCountDifference", tipoDoc: "ARQ", numero: "AD-1",
            persona: EscenarioContable.Cajero);
        return [new MensajeDeUnidad(sobre with { Payload = a }, a)];
    }

    private async Task<PostingRequest> ConstruirAsync(IReadOnlyList<MensajeDeUnidad> unidad)
    {
        var c = ConstructorDeLineasDeInventario.Construir(unidad, await E.CatalogosAsync(unidad));
        c.Fallos.Select(f => $"{f.Error.Code}: {f.Error.Message}").Should().BeEmpty();
        var validacion = await E.D.Poster.ValidarAsync(c.Request!, default);
        validacion.Errores.Select(x => $"{x.AccountCode}: {x.Code}").Should().BeEmpty("el contrato de la 009 lo acepta");
        return c.Request!;
    }

    private Guid? Tercero(PostingLine l) => l.PersonId is { } id ? E.PersonaPorId[id] : null;

    // ----------------------------------------------------------------------------------------------------- venta --

    [Fact]
    public async Task El_medio_resuelve_por_su_codigo_y_mas_especifico_por_el_punto_de_venta()
    {
        E.AgregarReglas(JsonNode.Parse("""[{ "op": "Venta", "rol": "MedioDePago", "cuenta": "11050502", "medio": "EFECTIVO", "punto": "PTO02" }]""")!.AsArray());

        var enPto01 = await ConstruirAsync(Venta("PTO01", "FV-1", null, Pago(1, "EFECTIVO", PaymentMeansClass.Cash, 1000m)));
        var enPto02 = await ConstruirAsync(Venta("PTO02", "FV-2", null, Pago(1, "EFECTIVO", PaymentMeansClass.Cash, 1000m)));

        enPto01.Lines.Should().ContainSingle(l => l.Debit == 1000m).Which.AccountCode.Should().Be("11050501", "la regla general del medio");
        enPto02.Lines.Should().ContainSingle(l => l.Debit == 1000m).Which.AccountCode.Should().Be("11050502", "la del punto pesa 16 y gana");
    }

    [Fact]
    public async Task Cada_pago_va_a_la_cuenta_de_su_medio_con_el_tercero_de_su_clase()
    {
        var r = await ConstruirAsync(Venta("PTO01", "FV-3", EscenarioContable.Asociado,
            Pago(1, "EFECTIVO", PaymentMeansClass.Cash, 600m),
            Pago(2, "VISARB", PaymentMeansClass.CreditCard, 300m, EscenarioContable.Redeban),
            Pago(3, "CREDASOC", PaymentMeansClass.AssociateCredit, 100m, EscenarioContable.Asociado)));

        var caja = r.Lines.Single(l => l.AccountCode == "11050501");
        caja.Debit.Should().Be(600m);
        caja.PersonId.Should().BeNull("el efectivo no tiene tercero");
        var tarjeta = r.Lines.Single(l => l.AccountCode == "13050501");
        tarjeta.Debit.Should().Be(300m);
        Tercero(tarjeta).Should().Be(EscenarioContable.Redeban, "la tarjeta va contra el adquirente");
        tarjeta.CrossDocumentType.Should().BeNull("la cuenta del adquirente no exige cruce");
        var credito = r.Lines.Single(l => l.AccountCode == "13050502");
        credito.Debit.Should().Be(100m);
        Tercero(credito).Should().Be(EscenarioContable.Asociado, "el crédito va contra el cliente");
        (credito.CrossDocumentType, credito.CrossDocumentNumber).Should().Be(("FV", "FV-3"), "la cuenta provisional cruza contra la venta (T32)");
        r.Lines.Single(l => l.AccountCode == "41350501").Credit.Should().Be(1000m);
    }

    [Fact]
    public async Task Recibido_va_al_debito_y_reintegrado_al_credito()
    {
        var nota = new NotaCreditoEmitidaV1
        {
            PointOfSaleCode = "PTO01",
            Lines = [new SalesAmountLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01", GrossAmount = 400m, NetAmount = 400m, DocumentLines = [1] }],
            Payments =
            [
                Pago(1, "EFECTIVO", PaymentMeansClass.Cash, 300m, direccion: PaymentDirection.Refunded),
                Pago(2, "CREDASOC", PaymentMeansClass.AssociateCredit, 100m, EscenarioContable.Asociado, PaymentDirection.Refunded),
            ],
            Totals = new SalesTotalsV1 { Subtotal = 400m, Total = 400m, AmountDue = 400m },
        };
        var original = new DocumentRefV1 { PublicId = Guid.NewGuid(), DocumentClass = DocumentClass.SalesInvoice, Number = "FV-3" };
        var sobre = EscenarioContable.Sobre(NotaCreditoEmitidaV1.Type, clase: "CreditNote", tipoDoc: "NC", numero: "NC-1",
            persona: EscenarioContable.Asociado, relacionado: original);

        var r = await ConstruirAsync([new MensajeDeUnidad(sobre with { Payload = nota }, nota)]);

        r.Lines.Single(l => l.AccountCode == "11050501").Credit.Should().Be(300m, "el reintegro en efectivo sale de la caja");
        var credito = r.Lines.Single(l => l.AccountCode == "13050502");
        credito.Credit.Should().Be(100m);
        (credito.CrossDocumentType, credito.CrossDocumentNumber).Should().Be(("FV", "FV-3"), "el reintegro a un crédito salda la venta");
        r.Lines.Single(l => l.AccountCode == "41750502").Debit.Should().Be(400m);
    }

    // ------------------------------------------------------------------------------------------------------ caja --

    [Theory]
    [InlineData(CashMovementKind.WithdrawalToSafe, CashMovementDestination.Safe, "11100501")]
    [InlineData(CashMovementKind.WithdrawalForDeposit, CashMovementDestination.Deposit, "11100502")]
    public async Task El_retiro_va_del_medio_a_la_caja_de_destino_por_ReasonCode(CashMovementKind tipo, CashMovementDestination destino, string cuenta)
    {
        var r = await ConstruirAsync(Movimiento(tipo, "EFECTIVO", 50000m, destino));

        r.Lines.Should().HaveCount(2);
        r.Lines.Single(l => l.Debit > 0).Should().Match<PostingLine>(l => l.AccountCode == cuenta && l.Debit == 50000m);
        r.Lines.Single(l => l.Credit > 0).Should().Match<PostingLine>(l => l.AccountCode == "11050501" && l.Credit == 50000m);
    }

    [Fact]
    public async Task El_retiro_a_otra_caja_va_entre_las_cajas_de_los_dos_puntos()
    {
        var r = await ConstruirAsync(Movimiento(CashMovementKind.WithdrawalToRegister, "EFECTIVO", 20000m, CashMovementDestination.Register, puntoDestino: "PTO02"));

        r.Lines.Single(l => l.Debit > 0).AccountCode.Should().Be("11050502", "la caja del punto de destino");
        r.Lines.Single(l => l.Credit > 0).AccountCode.Should().Be("11050501", "la del punto de origen");
    }

    [Fact]
    public async Task La_reclasificacion_va_del_medio_que_sale_al_que_entra()
    {
        var r = await ConstruirAsync(Movimiento(CashMovementKind.ReclassificationBetweenMeans, "EFECTIVO", 7000m, medioDestino: "TRANSF"));

        r.Lines.Single(l => l.Debit > 0).Should().Match<PostingLine>(l => l.AccountCode == "11200501" && l.Debit == 7000m);
        r.Lines.Single(l => l.Credit > 0).Should().Match<PostingLine>(l => l.AccountCode == "11050501" && l.Credit == 7000m);
    }

    [Fact]
    public async Task La_diferencia_de_arqueo_va_por_tratamiento_con_el_cajero_en_el_faltante_a_su_cargo()
    {
        var r = await ConstruirAsync(Arqueo(
            ("EFECTIVO", -8000m, CashDifferenceTreatment.ShortageToCashier),
            ("TRANSF", -2000m, CashDifferenceTreatment.ShortageToExpense),
            ("EFECTIVO", 500m, CashDifferenceTreatment.Surplus)));

        var aCargo = r.Lines.Single(l => l.AccountCode == "13659501");
        aCargo.Debit.Should().Be(8000m);
        Tercero(aCargo).Should().Be(EscenarioContable.Cajero, "el faltante a cargo del cajero lleva su persona");
        r.Lines.Single(l => l.AccountCode == "53959501").Debit.Should().Be(2000m, "el faltante al gasto");
        r.Lines.Single(l => l.AccountCode == "42950501").Credit.Should().Be(500m, "el sobrante");
        r.Lines.Single(l => l.AccountCode == "11200501").Credit.Should().Be(2000m);
        r.Lines.Where(l => l.AccountCode == "11050501").Sum(l => l.Debit - l.Credit).Should().Be(-7500m, "la caja pierde el faltante y gana el sobrante");
    }

    // ----------------------------------------------------------------------------------------- completitud (SC-024) --

    [Fact]
    public async Task Un_medio_activo_sin_regla_aparece_en_la_completitud_y_detiene_la_validacion_previa()
    {
        E.Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CatalogoDeDimensionesDto(
            [new("ABARROTES", "Abarrotes")], [], [new("PTO01", "Principal"), new("PTO02", "Norte")], [], [],
            [new("EFECTIVO", "Efectivo", PaymentMeansClass.Cash), new("NEQUI", "Nequi", PaymentMeansClass.Transfer),
             new("VISARB", "Visa", PaymentMeansClass.CreditCard), new("CREDASOC", "Crédito", PaymentMeansClass.AssociateCredit),
             new("TRANSF", "Transferencia", PaymentMeansClass.Transfer)])));
        var parametros = Substitute.For<ILectorDeParametros>();
        parametros.LeerAsync(default!, default!, default).ReturnsForAnyArgs(Task.FromResult(Result.Failure<ValorDeParametro>(new Error("Parameters.KeyNotFound", "x"))));

        var completitud = (await new InventoryRulesCompletenessQueryHandler(E.D.Db, E.Dimensiones, new TiposDeComprobanteDeInventario(E.D.Db), parametros)
            .Handle(new InventoryRulesCompletenessQuery(new DateOnly(2026, 3, 15)), default)).Value;
        var evaluacion = (await new EvaluateInventoryPostingQueryHandler(E.D.Db, E.D.Poster, new ResolutorDeReglas(E.D.Db), new TiposDeComprobanteDeInventario(E.D.Db), E.D.Clock)
            .Handle(new EvaluateInventoryPostingQuery(Venta("PTO01", "FV-9", null, Pago(1, "NEQUI", PaymentMeansClass.Transfer, 1000m))
                .Select(m => new MensajeContableDto(m.Sobre, m.Contenido!)).ToList()), default)).Value;

        completitud.PaymentMeansWithoutAccount.Select(m => m.PaymentMeansCode).Should().Equal("NEQUI");
        completitud.Summary.ByKind["paymentMeansWithoutAccount"].Should().Be(1);
        evaluacion.IsPostable.Should().BeFalse();
        evaluacion.Errors.Should().ContainSingle(e => e.Rule == "Accounting.InventoryRule.Missing");
    }

    [Fact]
    public void Un_medio_con_reglas_solo_de_algunos_puntos_queda_sin_cuenta_en_los_demas_y_una_regla_con_codigos_inexistentes_avisa()
    {
        var catalogo = new CatalogoDeDimensionesDto([], [], [new("PTO01", "Principal"), new("PTO02", "Norte")], [], [],
            [new("EFECTIVO", "Efectivo", PaymentMeansClass.Cash)]);
        var reglas = new[]
        {
            new Domain.Entities.Accounting.Inventory.InventoryPostingRule("Venta", "MedioDePago", 1, new DateOnly(2026, 1, 1), null, null, "PTO02", "EFECTIVO",
                null, null, null, null, null, null, "prueba"),
            new Domain.Entities.Accounting.Inventory.InventoryPostingRule("Venta", "MedioDePago", 1, new DateOnly(2026, 1, 1), null, null, "PTO09", "BITCOIN",
                null, null, null, null, null, null, "prueba"),
        };

        var sinCuenta = InventoryRulesCompletenessQueryHandler.MediosSinCuenta(catalogo, reglas);
        var avisos = InventoryRulesCompletenessQueryHandler.DimensionesInexistentes(catalogo, reglas, new Dictionary<int, Domain.Entities.Accounting.ChartOfAccount>()).ToList();

        sinCuenta.Should().ContainSingle().Which.Should().Be(new MedioSinCuentaDto("EFECTIVO", "Efectivo", "PTO01"));
        avisos.Select(a => a.Kind).Should().BeEquivalentTo(
            [InventoryRulesCompletenessQueryHandler.AvisoMedioInexistente, InventoryRulesCompletenessQueryHandler.AvisoPuntoInexistente]);
    }
}
