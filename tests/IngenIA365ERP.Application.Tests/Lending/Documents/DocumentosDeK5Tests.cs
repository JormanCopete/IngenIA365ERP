using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, K5, T0966 (FR-131, FR-132; api.md §15; data-model §27.1): el extracto y el certificado tributario con los servicios reales sobre
/// el escenario de <see cref="DocumentosDePrueba"/> —el crédito de Ana desembolsado el martes 1 de diciembre de 2026—. Antes de K5,
/// <c>Lending.Document.KindNotAvailable</c> con <c>availableFrom = "K5"</c>; con K5, el extracto sin período, al revés, que termina después de hoy o de
/// más de cinco años es <c>Lending.Document.PeriodInvalid</c>; el certificado del año en curso o futuro, <c>Lending.Document.TaxYearNotClosed</c>; sin
/// créditos en el año, <c>Lending.Document.NothingToCertify</c>. Emitidos: número con el prefijo de su clase (<c>EX-</c>, <c>CT-</c>), período o año,
/// créditos, totales, <c>RulesVersion</c> y huella sellados, y con correo el mensaje <c>DocumentDelivery</c>.
/// </summary>
public sealed class DocumentosDeK5Tests : IAsyncLifetime
{
    private static readonly DateOnly Desembolso = Payments.PagosDePrueba.Hoy;
    private static readonly DateOnly DiaDelPago = new(2026, 12, 20);
    private static readonly DateOnly Enero = new(2027, 1, 12);

    private DocumentosDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DocumentosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private static JsonElement Datos(Application.Common.Models.Error e) => Disbursements.DesembolsoDePrueba.Datos(e);

    /// <summary>Causa hasta el 20 de diciembre, cancela el crédito ese día (el sobrante vuelve al recaudador) y lleva el reloj a enero de 2027.</summary>
    private async Task<decimal> CanceladoEnDiciembreAsync()
    {
        await _d.P.CausarHastaAsync(DiaDelPago);
        await _d.P.AplicadoAsync(_d.P.Pago(ModalidadDePago.FullPayoff, 3_200_000m));
        _d.Db.ChangeTracker.Clear();
        var intereses = -await _d.Db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == _d.Credito.Id && (t.Kind == LoanTransactionKind.PaymentAllocation || t.Kind == LoanTransactionKind.PaymentReallocation)
                        && (t.Concept == LoanConcept.CurrentInterest || t.Concept == LoanConcept.DefaultInterest
                            || t.Concept == LoanConcept.CurrentInterestInSuspense || t.Concept == LoanConcept.DefaultInterestInSuspense))
            .SumAsync(t => t.Amount);
        _d.P.C.Ir(Enero);
        return intereses;
    }

    // --------------------------------------------------------------------------------------------------------- entrega --

    [Theory]
    [InlineData(IssuedDocumentKind.Statement)]
    [InlineData(IssuedDocumentKind.TaxCertificate)]
    public async Task Antes_de_K5_el_extracto_y_el_certificado_tributario_no_estan_disponibles(IssuedDocumentKind clase)
    {
        _d.EntregaDeCartera = EntregaDeCartera.K4;
        var pedido = clase == IssuedDocumentKind.Statement
            ? DocumentosDePrueba.Extracto(_d.Ana.PublicId, _d.Credito.PublicId, Desembolso, Desembolso)
            : DocumentosDePrueba.CertificadoTributario(_d.Ana.PublicId, 2025);

        var r = await _d.EmitirAsync(pedido);

        r.Error.Code.Should().Be("Lending.Document.KindNotAvailable");
        Datos(r.Error).GetProperty("availableFrom").GetString().Should().Be("K5");
        r.Error.Message.Should().Be("Este documento estará disponible en una entrega posterior de Cartera.");
    }

    [Fact]
    public void Con_K5_vigente_las_seis_clases_se_emiten()
    {
        CatalogoDeCartera.EntregaVigente.Should().Be(EntregaDeCartera.K5);
        Enum.GetValues<IssuedDocumentKind>().Should().OnlyContain(c => PrefijosDeDocumentosDeCartera.Disponible(c, CatalogoDeCartera.EntregaVigente));
        PrefijosDeDocumentosDeCartera.Disponible(IssuedDocumentKind.Statement, EntregaDeCartera.K4).Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------------- validaciones --

    [Fact]
    public async Task El_extracto_exige_un_periodo_valido_de_hasta_cinco_años()
    {
        var hoy = _d.G.HoyLocal;
        var sinPeriodo = await _d.EmitirAsync(DocumentosDePrueba.Extracto(_d.Ana.PublicId, _d.Credito.PublicId, null, null));
        sinPeriodo.Error.Code.Should().Be("Lending.Document.PeriodInvalid");

        var alReves = await _d.EmitirAsync(DocumentosDePrueba.Extracto(_d.Ana.PublicId, _d.Credito.PublicId, hoy, hoy.AddDays(-1)));
        alReves.Error.Code.Should().Be("Lending.Document.PeriodInvalid");

        var largo = await _d.EmitirAsync(DocumentosDePrueba.Extracto(_d.Ana.PublicId, _d.Credito.PublicId, hoy.AddYears(-5), hoy));
        largo.Error.Code.Should().Be("Lending.Document.PeriodInvalid");
        Datos(largo.Error).GetProperty("maxYears").GetInt32().Should().Be(5);

        var futuro = await _d.EmitirAsync(DocumentosDePrueba.Extracto(_d.Ana.PublicId, _d.Credito.PublicId, hoy, hoy.AddDays(1)));
        futuro.Error.Code.Should().Be("Lending.Document.PeriodInvalid");
        futuro.Error.Message.Should().Contain("después de hoy");

        _d.Subidas.Should().BeEmpty("nada se pinta ni se sube");
        (await _d.Db.IssuedLendingDocuments.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(2026)]
    [InlineData(2027)]
    public async Task El_certificado_tributario_del_año_en_curso_o_futuro_se_rechaza(short año)
    {
        var r = await _d.EmitirAsync(DocumentosDePrueba.CertificadoTributario(_d.Ana.PublicId, año));

        r.Error.Code.Should().Be("Lending.Document.TaxYearNotClosed");
        Datos(r.Error).GetProperty("year").GetInt32().Should().Be(año);
        r.Error.Message.Should().Contain($"1 de enero de {año + 1}");
    }

    [Fact]
    public async Task Sin_creditos_en_el_año_no_hay_nada_que_certificar()
    {
        var beto = await _d.EmitirAsync(DocumentosDePrueba.CertificadoTributario(_d.Beto.PublicId, 2025));
        beto.Error.Code.Should().Be("Lending.Document.NothingToCertify");

        var anaAntesDelDesembolso = await _d.EmitirAsync(DocumentosDePrueba.CertificadoTributario(_d.Ana.PublicId, 2025));
        anaAntesDelDesembolso.Error.Code.Should().Be("Lending.Document.NothingToCertify", "el crédito nació en 2026");
        _d.Subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_extracto_posterior_a_la_cancelacion_sin_movimientos_no_se_emite()
    {
        await CanceladoEnDiciembreAsync();

        var r = await _d.EmitirAsync(DocumentosDePrueba.Extracto(_d.Ana.PublicId, _d.Credito.PublicId, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 10)));

        r.Error.Code.Should().Be("Lending.Document.NothingToCertify");
    }

    // ---------------------------------------------------------------------------------------------------------- emitir --

    [Fact]
    public async Task El_extracto_del_mes_trae_desembolso_pago_y_causacion_y_sella_su_huella()
    {
        await CanceladoEnDiciembreAsync();

        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Extracto(_d.Ana.PublicId, _d.Credito.PublicId, new DateOnly(2026, 12, 1),
            new DateOnly(2026, 12, 31)));

        emitido.Number.Should().StartWith("EX-");
        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        documento.Kind.Should().Be(IssuedDocumentKind.Statement);
        documento.PeriodFrom.Should().Be(new DateOnly(2026, 12, 1));
        documento.PeriodTo.Should().Be(new DateOnly(2026, 12, 31));
        documento.TaxYear.Should().BeNull();
        documento.SettlementSha256.Should().MatchRegex("^[0-9a-f]{64}$", "la huella del estado al último día del período");
        documento.RulesVersion.Should().Be(VersionesDeReglas.Vigente);
        var secuencia = await _d.Db.LendingNumberSequences.AsNoTracking()
            .SingleAsync(s => s.Kind == LendingNumberKind.IssuedDocument && s.ScopeKey == nameof(IssuedDocumentKind.Statement));
        secuencia.Prefix.Should().Be("EX-");

        using (var datos = JsonDocument.Parse(documento.EssentialDataJson))
        {
            datos.RootElement.GetProperty("periodFrom").GetString().Should().Be("2026-12-01");
            datos.RootElement.GetProperty("periodTo").GetString().Should().Be("2026-12-31");
            datos.RootElement.GetProperty("openingBalance").GetDecimal().Should().Be(0m);
            datos.RootElement.GetProperty("closingBalance").GetDecimal().Should().Be(0m);
        }
        var cuerpo = _d.Pintados.Last().Cuerpo.Should().BeOfType<CuerpoDeExtracto>().Subject;
        cuerpo.Lineas.Select(l => l.Descripcion).Should().Contain(d => d.StartsWith("Desembolso")).And.Contain(d => d.StartsWith("Pago"))
            .And.Contain("Causación de diciembre de 2026");
        cuerpo.Lineas.Select(l => l.Fecha).Should().BeInAscendingOrder();
        cuerpo.TotalInicial.Should().Be(0m);
        cuerpo.TotalFinal.Should().Be(0m);
        cuerpo.Lineas.Sum(l => l.Total).Should().Be(0m, "saldo inicial + movimientos = saldo final");
        _d.Pintados.Last().Encabezado.Leyendas.Should().Contain(ModelosDeDocumentoDeCartera.LeyendaDelExtracto);

        var linea = await _d.Db.IssuedDocumentLoans.AsNoTracking().SingleAsync(x => x.IssuedDocumentId == documento.Id);
        linea.LoanId.Should().Be(_d.Credito.Id);
        linea.LastOperationId.Should().NotBeNull();
        linea.LastTransactionId.Should().NotBeNull();
    }

    [Fact]
    public async Task El_certificado_tributario_del_año_anterior_trae_los_intereses_pagados_y_el_saldo_al_31_de_diciembre()
    {
        var intereses = await CanceladoEnDiciembreAsync();
        intereses.Should().BeGreaterThan(0m, "diecinueve días de interés se pagaron con la cancelación");

        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.CertificadoTributario(_d.Ana.PublicId, null, DocumentDeliveryMode.Email));

        emitido.Number.Should().StartWith("CT-");
        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        documento.TaxYear.Should().Be((short)2026, "sin año, el anterior");
        documento.AsOfDate.Should().Be(new DateOnly(2026, 12, 31));
        documento.PeriodFrom.Should().BeNull();
        documento.RulesVersion.Should().Be(VersionesDeReglas.Vigente);
        using (var datos = JsonDocument.Parse(documento.EssentialDataJson))
        {
            datos.RootElement.GetProperty("year").GetInt32().Should().Be(2026);
            datos.RootElement.GetProperty("totalInterestPaid").GetDecimal().Should().Be(intereses);
            var credito = datos.RootElement.GetProperty("loans").EnumerateArray().Should().ContainSingle().Subject;
            credito.GetProperty("number").GetString().Should().Be(_d.Credito.Number);
            credito.GetProperty("balanceAtYearEnd").GetDecimal().Should().Be(0m, "cancelado en el año aparece con saldo cero");
            credito.GetProperty("housing").GetBoolean().Should().BeFalse();
        }
        var cuerpo = _d.Pintados.Last().Cuerpo.Should().BeOfType<CuerpoDeCertificadoTributario>().Subject;
        cuerpo.Año.Should().Be(2026);
        cuerpo.Creditos.Should().ContainSingle().Which.InteresesPagados.Should().Be(intereses);
        cuerpo.Vivienda.Should().BeEmpty("vacía hasta K6b");
        _d.Pintados.Last().Encabezado.Leyendas.Should().Contain(CuerpoDePazYSalvo.LeyendaDeAlcance)
            .And.Contain(ModelosDeDocumentoDeCartera.LeyendaDeInteresesPagados);

        var linea = await _d.Db.IssuedDocumentLoans.AsNoTracking().SingleAsync(x => x.IssuedDocumentId == documento.Id);
        linea.BalanceAtCutoff.Should().Be(0m);
        var mensaje = await _d.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.IssuedDocumentId == documento.Id);
        mensaje.Purpose.Should().Be(DebtorMessagePurpose.DocumentDelivery);
        mensaje.PublicId.Should().Be(emitido.DebtorMessagePublicId!.Value);
        mensaje.RenderedBody.Should().Contain("Certificado tributario");
    }

    [Fact]
    public async Task La_misma_clave_devuelve_el_mismo_certificado()
    {
        await CanceladoEnDiciembreAsync();
        var pedido = DocumentosDePrueba.CertificadoTributario(_d.Ana.PublicId, 2026);

        var primero = await _d.ConClaveAsync(pedido);
        var segundo = await _d.ConClaveAsync(pedido);

        primero.IsSuccess.Should().BeTrue(primero.IsFailure ? primero.Error.Message : string.Empty);
        segundo.Value.DocumentPublicId.Should().Be(primero.Value.DocumentPublicId);
        (await _d.Db.IssuedLendingDocuments.CountAsync(d => d.Kind == IssuedDocumentKind.TaxCertificate)).Should().Be(1);
    }

    // ------------------------------------------------------------ decisiones del dueño del 2026-10-16 (certificado tributario) --

    private static readonly LoanConcept[] ConceptosDeInteres =
        [LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense];

    /// <summary>Lo que el libro real aplicó a intereses en el año, por clase de fila (positivo).</summary>
    private async Task<decimal> InteresesDelLibroAsync(int año, params LoanTransactionKind[] clases)
    {
        var desde = new DateOnly(año, 1, 1);
        var hasta = new DateOnly(año, 12, 31);
        var filas = await _d.Db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == _d.Credito.Id && clases.Contains(t.Kind) && ConceptosDeInteres.Contains(t.Concept)).ToListAsync();
        return -filas.Where(t => t.ValueDate >= desde && t.ValueDate <= hasta).Sum(t => t.Amount);
    }

    private async Task<JsonElement> CreditoDelCertificadoAsync(short año)
    {
        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.CertificadoTributario(_d.Ana.PublicId, año));
        var documento = await _d.DocumentoAsync(emitido.DocumentPublicId);
        using var datos = JsonDocument.Parse(documento.EssentialDataJson);
        return datos.RootElement.Clone();
    }

    [Fact]
    public async Task El_interes_que_cubre_un_anticipo_ingresado_en_el_año_cuenta_en_el_certificado()
    {
        // La cuota del 1 de enero de 2027 vence sin pagarse; el 15 de enero un pago la cubre y deja anticipo, que cubre parte de la de febrero.
        await _d.P.CausarHastaAsync(new DateOnly(2027, 1, 15));
        await _d.P.AplicadoAsync(_d.P.Pago(ModalidadDePago.Advance, 250_000m));
        await _d.P.CausarHastaAsync(new DateOnly(2027, 2, 2));
        var delPago = await InteresesDelLibroAsync(2027, LoanTransactionKind.PaymentAllocation, LoanTransactionKind.PaymentReallocation);
        var delAnticipo = await InteresesDelLibroAsync(2027, LoanTransactionKind.AdvanceApplied);
        delPago.Should().BeGreaterThan(0m);
        delAnticipo.Should().BeGreaterThan(0m, "el anticipo de enero cubrió el interés de la cuota de febrero");
        _d.P.C.Ir(new DateOnly(2028, 1, 12));

        var datos = await CreditoDelCertificadoAsync(2027);

        datos.GetProperty("totalInterestPaid").GetDecimal().Should().Be(delPago + delAnticipo, "el anticipo ingresó en 2027 y se aplicó en 2027");
        datos.GetProperty("loans")[0].GetProperty("interestPaidWithAdvance").GetDecimal().Should().Be(delAnticipo);
    }

    [Fact]
    public async Task El_interes_que_cubre_un_anticipo_ingresado_el_año_anterior_no_cuenta_aunque_se_aplique_en_el_año()
    {
        await _d.P.CausarHastaAsync(DiaDelPago);
        await _d.P.AplicadoAsync(_d.P.Pago(ModalidadDePago.Advance, 100_000m));
        await _d.P.CausarHastaAsync(new DateOnly(2027, 1, 2));
        (await InteresesDelLibroAsync(2027, LoanTransactionKind.AdvanceApplied)).Should()
            .BeGreaterThan(0m, "el anticipo de diciembre cubrió el interés de la cuota de enero");
        _d.P.C.Ir(new DateOnly(2028, 1, 12));

        var datos = await CreditoDelCertificadoAsync(2027);

        datos.GetProperty("totalInterestPaid").GetDecimal().Should().Be(0m, "el anticipo ingresó en 2026 y en 2027 no hubo pagos");
        datos.GetProperty("loans")[0].GetProperty("interestPaidWithAdvance").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task El_saldo_al_31_de_diciembre_por_defecto_lleva_capital_e_intereses()
    {
        _d.P.C.Ir(Enero);

        var datos = await CreditoDelCertificadoAsync(2026);

        datos.GetProperty("balanceIncludes").GetString().Should().Be(nameof(ConceptosDelSaldoCertificado.Todos));
        datos.GetProperty("loans")[0].GetProperty("balanceAtYearEnd").GetDecimal().Should()
            .BeGreaterThan(3_000_000m, "capital más el interés causado en diciembre");
        var cuerpo = _d.Pintados.Last().Cuerpo.Should().BeOfType<CuerpoDeCertificadoTributario>().Subject;
        cuerpo.Creditos.Single().SaldoDeIntereses.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task El_saldo_al_31_de_diciembre_lleva_los_conceptos_que_configura_la_cooperativa()
    {
        await _d.ParametroAsync(ParametrosDeCartera.DocumentosCertificadoTributarioSaldoIncluye, "Capital");
        _d.P.C.Ir(Enero);

        var datos = await CreditoDelCertificadoAsync(2026);

        datos.GetProperty("balanceIncludes").GetString().Should().Be(nameof(ConceptosDelSaldoCertificado.Capital));
        datos.GetProperty("loans")[0].GetProperty("balanceAtYearEnd").GetDecimal().Should().Be(3_000_000m, "sólo el capital, sin pagos");
        var cuerpo = _d.Pintados.Last().Cuerpo.Should().BeOfType<CuerpoDeCertificadoTributario>().Subject;
        cuerpo.Creditos.Single().SaldoDeIntereses.Should().Be(0m);
        cuerpo.TotalSaldo.Should().Be(3_000_000m);
    }
}
