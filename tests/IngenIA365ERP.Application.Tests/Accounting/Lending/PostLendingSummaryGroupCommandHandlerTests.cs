using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0617 (T0648; contracts/contabilidad.md §10.4; FR-128, US7-3): el resumido de la causación diaria. Un <c>CA</c> por día con el
/// interés, la mora, el seguro y el suspenso, cada línea en su sucursal; débitos y créditos sin netear; las cuentas que exigen tercero o cruce
/// conservan una línea por crédito con su deudor y <c>PG</c> + número; origen <c>LendingPostingBatch</c>, «registrado por» nulo, un recibo por
/// hecho con el mismo comprobante y el lote. Una parte que falla queda <c>Rejected</c> y sus dependientes esperan sin tumbar el grupo; repetir
/// el grupo no crea nada.
/// </summary>
public class PostLendingSummaryGroupCommandHandlerTests
{
    private readonly EscenarioContableDeCartera E = new();

    private IReadOnlyList<MensajeEntrante> Emitida(IReadOnlyList<MensajeEntrante> unidad, IntegrationBatch lote)
    {
        E.Emitir(unidad, DeliveryStatus.InBatch, lote, EscenarioContableDeCartera.Horario(unidad[0].Type));
        return unidad;
    }

    private IReadOnlyList<MensajeEntrante> Parte(string caso, IntegrationBatch lote) =>
        Emitida(EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe(caso)), lote);

    private async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(IntegrationBatch lote, params IReadOnlyList<MensajeEntrante>[] unidades)
    {
        var r = await E.Resumido().Handle(new PostLendingSummaryGroupCommand(lote.PublicId, "2026-11-14|CA",
            unidades.SelectMany(u => u).Select(m => m.MessageId).ToList()), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        E.D.Db.ChangeTracker.Clear();
        return r.Value;
    }

    [Fact]
    public async Task La_causacion_del_dia_sale_en_un_CA_con_una_linea_por_credito_donde_la_cuenta_lo_exige()
    {
        var lote = E.Lote();
        var interes = Parte("03-causacion-del-dia", lote);
        var mora = Parte("25-mora-causada", lote);
        var seguro = Parte("26-seguro-causado-con-la-aseguradora", lote);
        var suspenso = Parte("12-causacion-en-suspenso", lote);

        var resultados = await ConsumirAsync(lote, interes, mora, seguro, suspenso);

        resultados.Should().HaveCount(4).And.OnlyContain(r => r.Resultado is ResultadoDeConsumo.Processed);
        var documento = await E.D.Db.AccountingDocuments.Include(d => d.Lines).ThenInclude(l => l.Account).SingleAsync();
        documento.Date.Should().Be(new DateOnly(2026, 11, 14), "la fecha contable del grupo, nunca la del lote");
        documento.Kind.Should().Be(DocumentKind.Regular);
        documento.OriginModule.Should().Be("CAR");
        documento.SourceType.Should().Be(OrigenesDeCartera.LoteResumido);
        documento.SourcePublicId.Should().Be(lote.PublicId);
        documento.RegisteredBy.Should().Be(E.D.User.UserName, "un resumido no tiene usuario de origen (§7.3)");
        documento.Description.Should().Be("Lote 1 · CA · 2026-11-14 · 3 créditos");
        documento.TotalDebit.Should().Be(documento.TotalCredit).And.Be(4302.38m + 2150.10m + 850.25m + 1500m + 3000m);

        var lineas = documento.Lines.Select(l => (l.Account!.Code, l.Debit, l.Credit, l.CrossDocumentNumber)).ToList();
        lineas.Should().Contain(("14430501", 4302.38m, 0m, "000123")).And.Contain(("14430501", 2150.10m, 0m, "000124"),
            "la cuenta de interés exige tercero y cruce: una línea por crédito");
        lineas.Should().ContainSingle(l => l.Code == "41502501").Which.Credit.Should().Be(6452.48m, "el ingreso sin tercero se suma");
        lineas.Should().ContainSingle(l => l.Code == "81150501").Which.Debit.Should().Be(3000m);
        documento.Lines.Should().OnlyContain(l => l.BranchId == E.D.Principal.Id, "cada línea en su sucursal");

        var recibos = await E.D.Db.LendingPostings.ToListAsync();
        recibos.Should().HaveCount(4).And.OnlyContain(p => p.AccountingDocumentId == documento.Id && p.BatchPublicId == lote.PublicId);
    }

    /// <summary>
    /// Defecto del 2026-10-13 (medición de T0633): si la cuenta exige tercero pero no cruce, dos créditos del mismo deudor se juntaban en una línea.
    /// La línea por crédito de §10.4 no depende de que la cuenta pida cruce.
    /// </summary>
    [Fact]
    public void Con_tercero_y_sin_cruce_sigue_siendo_una_linea_por_credito()
    {
        var tipo = new TipoDeLaUnidadDeCartera("InteresCausado", null, 1, "CA", null, null);
        ConstruccionDeCartera Una(string credito, decimal valor) => new(
            new PostingRequest("CA", new DateOnly(2026, 11, 14), "x", new AccountingOrigin("CAR", "LendingIssuedDocument", Guid.NewGuid()),
            [
                new PostingLine { AccountCode = "14430501", Debit = valor, PersonId = 7, BranchId = 1 },
                new PostingLine { AccountCode = "41502501", Credit = valor, BranchId = 1 },
            ]),
            tipo,
            [
                new LineaDeCartera(["InteresCausado"], "InteresPorCobrar", "CurrentInterest", [credito], "14430501", "Interés", ConservaDetalle: true),
                new LineaDeCartera(["InteresCausado"], "Ingreso", "CurrentInterest", [credito], "41502501", "Ingreso", ConservaDetalle: false),
            ],
            [], new DateOnly(2026, 11, 14), null!);

        var resumen = PostLendingSummaryGroupCommandHandler.Resumen([Una("000123", 100m), Una("000124", 50m)], Guid.NewGuid(), 1);

        resumen.Lines.Where(l => l.AccountCode == "14430501").Select(l => l.Debit)
            .Should().BeEquivalentTo([100m, 50m], "una línea por crédito aunque los dos sean del mismo tercero y la cuenta no pida cruce");
        resumen.Lines.Should().ContainSingle(l => l.AccountCode == "41502501").Which.Credit.Should().Be(150m);
    }

    [Fact]
    public async Task Debitos_y_creditos_de_una_misma_cuenta_no_se_netean()
    {
        var lote = E.Lote();
        var interes = Parte("03-causacion-del-dia", lote);
        var reverso = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("22-usura-corregida-ajuste-de-causacion"), fecha: new DateOnly(2026, 11, 14));
        Emitida(reverso, lote);

        await ConsumirAsync(lote, interes, reverso);

        var ingreso = await E.D.Db.JournalEntries.Where(l => l.Account!.Code == "41502501").ToListAsync();
        ingreso.Should().HaveCount(2, "débitos y créditos por separado, sin netear (§10.4)");
        ingreso.Sum(l => l.Credit).Should().Be(6452.48m);
        ingreso.Sum(l => l.Debit).Should().Be(3000m);
    }

    [Fact]
    public async Task Una_parte_que_falla_queda_rechazada_y_sus_dependientes_esperan_sin_tumbar_el_grupo()
    {
        var lote = E.Lote();
        var buena = Parte("03-causacion-del-dia", lote);
        var caso = EscenarioContableDeCartera.Caso("25-mora-causada");
        caso["mensajes"]![0]!["payload"]!["lines"]![0]!["dimensions"]!["riskCategory"] = "E";
        var mala = Emitida(EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.Unidad(caso)), lote);
        var dependiente = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("26-seguro-causado-con-la-aseguradora"))
            .Select(m => m with { Envelope = m.Envelope with { Related = new DocumentRefV1 { PublicId = mala[0].Envelope.Origin.PublicId, Number = "X" } } })
            .ToList();
        Emitida(dependiente, lote);

        var resultados = await ConsumirAsync(lote, buena, mala, dependiente);

        resultados[0].Resultado.Should().BeOfType<ResultadoDeConsumo.Processed>();
        resultados[1].Resultado.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.LendingRule.Missing");
        resultados[2].Resultado.Should().BeOfType<ResultadoDeConsumo.Retry>().Which.Code.Should().Be("Accounting.LendingFact.WaitingForOriginal");
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(1);
        (await E.D.Db.LendingPostings.CountAsync()).Should().Be(1, "sólo la parte buena deja recibo");
    }

    [Fact]
    public async Task Repetir_el_grupo_no_crea_nada()
    {
        var lote = E.Lote();
        var interes = Parte("03-causacion-del-dia", lote);
        var mora = Parte("25-mora-causada", lote);
        var primero = await ConsumirAsync(lote, interes, mora);

        var segundo = await ConsumirAsync(lote, interes, mora);

        segundo.Should().OnlyContain(r => r.Resultado is ResultadoDeConsumo.AlreadyProcessed);
        ((ResultadoDeConsumo.AlreadyProcessed)segundo[0].Resultado).AccountingDocumentPublicId
            .Should().Be(((ResultadoDeConsumo.Processed)primero[0].Resultado).AccountingDocumentPublicId);
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(1);
        (await E.D.Db.LendingPostings.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Sin_lineas_cada_parte_deja_su_recibo_y_el_grupo_sigue()
    {
        var lote = E.Lote();
        var interes = Parte("03-causacion-del-dia", lote);
        var mismas = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("39-traslado-que-resuelve-las-mismas-cuentas"), fecha: new DateOnly(2026, 11, 14));
        Emitida(mismas, lote);

        var resultados = await ConsumirAsync(lote, interes, mismas);

        resultados[1].Resultado.Should().Be(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.SameAccounts));
        (await E.D.Db.LendingPostings.CountAsync(p => p.NoVoucherReason == "SameAccounts")).Should().Be(1);
    }
}
