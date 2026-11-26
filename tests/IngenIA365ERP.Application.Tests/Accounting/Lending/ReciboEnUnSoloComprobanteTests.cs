using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Accounting.Receipts.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Accounting.Receipts;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// 2026-11-26 (pedido del dueño; <see cref="ReciboEnUnSoloComprobante"/>): el recibo de caja de Recaudo y lo que abona a Cartera en un solo comprobante.
/// El planeador junta la unidad del recibo con las de Cartera del mismo recibo; el comprobante lleva el tipo y el origen del recibo, todas las líneas sin
/// sumar y cada una con su módulo; el contrato mide «habilitada para» con el módulo de cada línea.
/// </summary>
public class ReciboEnUnSoloComprobanteTests
{
    private static IReadOnlyList<MensajeEntrante> Pago(string recibo = "R-0001") =>
        EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("04-pago-por-caja-con-anticipo")).Select(m => m.Payload is PagoAplicadoV1 p
            ? m with { Payload = p with { Collector = p.Collector with { Receipt = p.Collector.Receipt with { Number = recibo } } } }
            : m).ToList();

    private static CollectorRefV1 RecaudoDe(IReadOnlyList<MensajeEntrante> pago) => pago.Select(m => m.Payload).OfType<PagoAplicadoV1>().Single().Collector;

    private static DateOnly FechaDe(IReadOnlyList<MensajeEntrante> pago) => pago[0].Envelope.Origin.OperationDate;

    /// <summary>El <c>ReciboEmitido</c> de la caja que casa con el pago: mismo número, oficina y fecha contable.</summary>
    private static IReadOnlyList<MensajeEntrante> Caja(IReadOnlyList<MensajeEntrante> pago, string? numero = null)
    {
        var recaudo = RecaudoDe(pago);
        var contenido = new ReciboEmitidoV1
        {
            ReceiptPublicId = Guid.NewGuid(), Number = numero ?? recaudo.Receipt.Number, Channel = "CashDesk", ValueDate = FechaDe(pago), TotalApplied = 100m,
            Lines =
            [
                new FactLineV1 { Role = RolesDeRecaudo.Caja, Side = RolesDeRecaudo.Debito, Amount = 100m, Dimensions = new ReceiptsFactDimensionsV1 { BranchCode = "01" } },
                new FactLineV1 { Role = RolesDeRecaudo.PuenteDelModulo, Side = RolesDeRecaudo.Credito, Amount = 100m,
                    Dimensions = new ReceiptsFactDimensionsV1 { BranchCode = "01", TargetModule = "CAR", TargetNature = "Loan" } },
            ],
        };
        return [MatricesDePrueba.Mensaje(ModulosDeOrigen.Recaudo, contenido, ReciboEmitidoV1.Type, sucursal: recaudo.Receipt.OfficePublicId ?? pago[0].Envelope.BranchPublicId, fecha: FechaDe(pago))];
    }

    private static UnidadDeConsumo Unidad(IReadOnlyList<MensajeEntrante> mensajes) =>
        new(IntegrationDestinations.Accounting, mensajes[0].Envelope.Origin.PublicId, mensajes[0].Envelope.OriginEventKey,
            mensajes.Select(m => m.MessageId).ToList(), null, 0) { OriginModule = mensajes[0].OriginModule };

    [Fact]
    public void El_recibo_de_caja_se_junta_con_lo_que_abona_a_Cartera_y_lo_demas_va_como_siempre()
    {
        var a = Pago();
        var b = Pago();
        var otro = Pago("R-0002");
        var caja = Caja(a);
        var cajaSinAbonos = Caja(a, numero: "R-0099");
        var deRecaudo = new[] { cajaSinAbonos, caja }.Select(u => (Unidad(u), u)).ToList();
        var deCartera = new[] { a, otro, b }.Select(u => (Unidad(u), u)).ToList();

        var trabajos = ReciboConSuCaja.Planear(deRecaudo, deCartera);

        var t = trabajos.Should().ContainSingle().Subject;
        t.ClaveDeGrupo.Should().StartWith(ReciboEnUnSoloComprobante.PrefijoDeGrupo).And.Contain("R-0001|");
        t.Unidades.Should().Equal(deRecaudo[1].Item1, deCartera[0].Item1, deCartera[2].Item1);
        ReciboConSuCaja.ClaveDeRecaudo(caja).Should().Be(ReciboConSuCaja.ClaveDeCartera(a));
    }

    [Fact]
    public void Lo_que_no_recauda_la_caja_o_es_de_otra_fecha_no_se_junta()
    {
        var pago = Pago();
        var deNomina = pago.Select(m => m.Payload is PagoAplicadoV1 p ? m with { Payload = p with { Collector = p.Collector with { CollectorCode = "NOM" } } } : m).ToList();
        ReciboConSuCaja.ClaveDeCartera(deNomina).Should().BeNull("sólo se junta lo que recauda la caja de Recaudo");

        var otraFecha = EscenarioContableDeCartera.Copia(pago, fecha: FechaDe(pago).AddDays(1));
        ReciboConSuCaja.ClaveDeCartera(otraFecha).Should().NotBe(ReciboConSuCaja.ClaveDeRecaudo(Caja(pago)));
        ReciboConSuCaja.ClaveDeCartera(EscenarioContableDeCartera.UnidadDe("03-causacion-del-dia")).Should().BeNull();
    }

    [Fact]
    public async Task El_comprobante_lleva_el_tipo_y_el_origen_del_recibo_y_cada_linea_con_su_modulo()
    {
        var e = new EscenarioContableDeCartera();
        var a = Pago();
        var b = Pago();
        var catalogosDeCartera = await e.CatalogosAsync(a, b);
        var abonos = new[] { a, b }.Select(u => ConstructorDeLineasDeCartera.Construir(u.Select(HechoDeCartera.De).ToList(), catalogosDeCartera)).ToList();
        abonos.Should().OnlyContain(x => x.EsValida && !x.SinLineas);

        var cuentaCaja = MatricesDePrueba.Cuenta("11050501");
        var puente = MatricesDePrueba.Cuenta("16603505");
        var matriz = MatricesDePrueba.Matriz(MatricesDeRecaudoYTesoreria.Recaudo,
            [MatricesDePrueba.Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, cuentaCaja, desde: FechaDe(a).AddMonths(-1)),
             MatricesDePrueba.Regla<ReceiptsPostingRule>(RolesDeRecaudo.PuenteDelModulo, puente, desde: FechaDe(a).AddMonths(-1), dimensiones: r => r.TargetModule = "CAR")],
            [cuentaCaja, puente]);
        var mensajeDeCaja = Caja(a)[0];
        var catalogos = new CatalogosDeModulo(matriz, new Dictionary<string, (string, string?)> { [ReciboEmitidoV1.Type] = ("RR", null) }, new Dictionary<Guid, int>(),
            new Dictionary<Guid, int> { [mensajeDeCaja.Envelope.BranchPublicId] = 1 });
        var caja = ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Recaudo, [HechoContableDeModulo.De(mensajeDeCaja)], catalogos);
        caja.EsValida.Should().BeTrue(string.Join(" ", caja.Fallos.Select(f => f.Error.Message)));

        var unido = PostCashReceiptVoucherCommandHandler.Unido(caja, abonos);

        unido.VoucherTypeCode.Should().Be("RR");
        unido.Origin.Should().Be(caja.Request!.Origin);
        unido.Description.Should().StartWith("Recibo R-0001 · ");
        unido.Lines.Should().HaveCount(caja.Request.Lines.Count + abonos.Sum(x => x.Request!.Lines.Count), "no se suman: cada crédito conserva las suyas");
        unido.Lines.Take(caja.Request.Lines.Count).Should().OnlyContain(l => l.Module == ModuloContable.Recaudo);
        unido.Lines.Skip(caja.Request.Lines.Count).Should().OnlyContain(l => l.Module == ModuloContable.Cartera);
    }

    /// <summary>El escenario de Cartera con lo de Recaudo: la caja y el puente (habilitadas para Recaudo), sus reglas y el RR del recibo de caja.</summary>
    private static (EscenarioContableDeCartera E, PostCashReceiptVoucherCommandHandler Handler) ConRecaudo(DateOnly desde)
    {
        var e = new EscenarioContableDeCartera();
        var caja = e.D.Cuenta("11050599", modulos: AccountingModules.Accounting | AccountingModules.Receipts);
        var puente = e.D.Cuenta("16603599", modulos: AccountingModules.Accounting | AccountingModules.Receipts);
        e.D.Db.SaveChanges();
        foreach (var (rol, cuenta, aCartera) in new[] { (RolesDeRecaudo.Caja, caja, false), (RolesDeRecaudo.PuenteDelModulo, puente, true) })
        {
            var regla = new ReceiptsPostingRule { FactType = "*", Role = rol, AccountId = cuenta.Id, ValidFrom = desde.AddMonths(-1), CreatedBy = "test" };
            if (aCartera) regla.TargetModule = "CAR";
            regla.Recalcular(false);
            e.D.Db.ReceiptsPostingRules.Add(regla);
        }
        e.D.Db.ReceiptsVoucherMappings.Add(new ReceiptsVoucherMapping
        {
            FactType = ReciboEmitidoV1.Type, VoucherTypeId = e.D.Db.VoucherTypes.Single(v => v.Code == "RR").Id, CreatedBy = "test",
        });
        e.D.Db.SaveChanges();
        var handler = new PostCashReceiptVoucherCommandHandler(e.D.Db, new MensajesEntrantes(e.D.Db),
            new ConsumoDeModulo(e.D.Db, e.ActorActual, e.D.Clock, e.Emisor), e.Consumo(), e.D.Poster, new ResolutorDeLaMatriz(e.D.Db),
            new Application.Accounting.Lending.Reglas.ResolutorDeReglasDeCartera(e.D.Db), new TiposDeComprobanteDeCartera(e.D.Db), e.D.Clock);
        return (e, handler);
    }

    [Fact]
    public async Task El_recibo_de_caja_y_sus_abonos_quedan_en_un_solo_RR_con_el_puente_a_la_vista()
    {
        var a = Pago();
        var b = Pago();
        var (e, handler) = ConRecaudo(FechaDe(a));
        var totalDeLosAbonos = 0m;
        foreach (var u in new[] { a, b })
        {
            var abono = ConstructorDeLineasDeCartera.Construir(u.Select(HechoDeCartera.De).ToList(), await e.CatalogosAsync(u));
            totalDeLosAbonos += abono.Request!.Lines.Sum(l => l.Debit);
        }
        var caja = Caja(a);
        foreach (var u in new[] { caja, a, b }) e.Emitir(u);

        var r = await handler.Handle(new PostCashReceiptVoucherCommand(ReciboConSuCaja.ClaveDeRecaudo(caja)!, caja.Concat(a).Concat(b).Select(m => m.MessageId).ToList()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Should().HaveCount(3).And.OnlyContain(x => x.Resultado is ResultadoDeConsumo.Processed);
        e.D.Db.ChangeTracker.Clear();
        var documento = await e.D.Db.AccountingDocuments.Include(d => d.Lines).Include(d => d.VoucherType).SingleAsync();
        documento.VoucherType!.Code.Should().Be("RR");
        documento.OriginModule.Should().Be(ModuloContable.Recaudo);
        documento.Description.Should().StartWith("Recibo R-0001 · ");
        documento.TotalDebit.Should().Be(documento.TotalCredit).And.Be(100m + totalDeLosAbonos, "las líneas no se suman ni se netea el puente");
        (await e.D.Db.ReceiptsPostings.ToListAsync()).Should().ContainSingle().Which.AccountingDocumentId.Should().Be(documento.Id);
        (await e.D.Db.LendingPostings.ToListAsync()).Should().HaveCount(a.Count + b.Count).And.OnlyContain(p => p.AccountingDocumentId == documento.Id);
    }

    [Fact]
    public async Task Si_una_parte_no_se_arma_no_se_guarda_nada_y_se_contabiliza_por_modulo()
    {
        var a = Pago();
        var (e, handler) = ConRecaudo(FechaDe(a));
        e.D.Db.ReceiptsVoucherMappings.RemoveRange(e.D.Db.ReceiptsVoucherMappings);
        e.D.Db.SaveChanges();
        var caja = Caja(a);
        foreach (var u in new[] { caja, a }) e.Emitir(u);

        var r = await handler.Handle(new PostCashReceiptVoucherCommand(ReciboConSuCaja.ClaveDeRecaudo(caja)!, caja.Concat(a).Select(m => m.MessageId).ToList()), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(PostCashReceiptVoucherCommand.CodigoNoSeJunta);
        e.D.Db.ChangeTracker.Clear();
        (await e.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await e.D.Db.ReceiptsPostings.CountAsync()).Should().Be(0);
        (await e.D.Db.LendingPostings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_contrato_mide_habilitada_para_con_el_modulo_de_cada_linea()
    {
        var d = new ContabilidadTestData();
        var gasto = d.Cuenta("510506", modulos: AccountingModules.Payroll);
        var deCartera = d.Cuenta("144105", AccountNature.Debit, modulos: AccountingModules.Lending);
        var lineas = new List<PostingLine> { PostingLine.Debito(deCartera.Id, 10m), PostingLine.Credito(gasto.Id, 10m) };
        var request = new PostingRequest("NM", ContabilidadTestData.Marzo15, "Dos módulos", ContabilidadTestData.Nomina(), lineas);

        (await d.Poster.ValidarAsync(request, CancellationToken.None)).Errores.Should().Contain(x => x.Code == "Accounting.Line.AccountNotEnabledForModule");

        var conModulo = request with { Lines = [lineas[0] with { Module = ModuloContable.Cartera }, lineas[1]] };
        (await d.Poster.ValidarAsync(conModulo, CancellationToken.None)).EsValido.Should().BeTrue();

        var manual = new PostingRequest("CG", ContabilidadTestData.Marzo15, "Digitado", ContabilidadTestData.Manual(),
            [PostingLine.Debito(deCartera.Id, 10m) with { Module = ModuloContable.Cartera }, PostingLine.Credito(d.Cuenta("413505", AccountNature.Credit).Id, 10m)]);
        (await d.Poster.ValidarAsync(manual, CancellationToken.None)).Errores.Should().Contain(x => x.Code == "Accounting.Line.AccountNotEnabledForModule",
            "la digitación no puede hablar por otro módulo");
    }
}
