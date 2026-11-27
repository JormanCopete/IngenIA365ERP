using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 028 (T042; HU3, FR-013–FR-016, FR-027d; api.md §3): el desembolso ya no manda los créditos que recoge (salen de la solicitud) y manda
/// <c>acknowledgedPayoffDifferences</c>; la vista previa trae por producto lo proyectado, lo actual, lo que se aplica, la diferencia por concepto con sus
/// causas, lo omitido y la cuota causada, y el resumen <c>payoffDifferences</c>; el resultado trae lo aplicado y lo omitido; la garantía y el pagaré de un
/// crédito recogido dicen «Por liberar al girar el crédito …». Las causas, los conceptos y la diferencia con su signo se dicen en palabras. (nuevo)
/// </summary>
public class DesembolsoConRecogidasTests
{
    private static readonly Guid Solicitud = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Credito = Guid.Parse("66666666-7777-8888-9999-000000000000");
    private static readonly Guid Otro = Guid.Parse("77777777-8888-9999-0000-111111111111");
    private static readonly Guid Pago = Guid.Parse("88888888-9999-0000-1111-222222222222");

    private readonly ServidorDeCartera _servidor = new();
    private readonly DesembolsosClient _desembolsos;
    private readonly GarantiasClient _garantias;

    public DesembolsoConRecogidasTests()
    {
        var (http, auth) = _servidor.Sesion();
        _desembolsos = new DesembolsosClient(http, auth);
        _garantias = new GarantiasClient(http, auth);
    }

    // ------------------------------------------------------------------------------------------------------- el pedido --

    [Fact]
    public async Task Desembolsar_no_manda_lo_que_recoge_y_manda_el_reconocimiento_de_las_diferencias()
    {
        string? cuerpo = null;
        _servidor.Responder = r =>
        {
            cuerpo = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return ServidorDeCartera.Json(HttpStatusCode.OK, new { });
        };

        await _desembolsos.DesembolsarAsync(new DesembolsoRequest
        {
            ApplicationPublicId = Solicitud, Form = "Transfer", ExpectedPreviewSha256 = "sha256:abc", AcknowledgedPayoffDifferences = true,
        }, new ClaveDeOperacion());

        using var doc = JsonDocument.Parse(cuerpo!);
        doc.RootElement.TryGetProperty("ownLoanPayoffs", out _).Should().BeFalse("las recogidas salen de la solicitud (D9)");
        doc.RootElement.TryGetProperty("payoffs", out _).Should().BeFalse();
        doc.RootElement.GetProperty("acknowledgedPayoffDifferences").GetBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------------- las respuestas --

    [Fact]
    public async Task La_vista_previa_trae_lo_que_recoge_con_sus_diferencias()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new
        {
            usury = new { rate = 0.2m, usury = 0.25m, passes = true, usuryValuePublicId = (Guid?)null },
            approval = new { expiresAt = "2026-12-31", valid = true },
            collaterals = new { perfected = true, pending = Array.Empty<object>() },
            payoffs = new object[]
            {
                new
                {
                    loanPublicId = Credito, number = "65-78925", toPayOff = 5_098_456.78m, daysPastDue = 0, requiresRestructuring = false, kind = "Loan",
                    productPublicId = Credito, mode = "Total", requestedMode = "Total", projected = 5_123_456.78m, current = 5_098_456.78m,
                    toApply = 5_098_456.78m, difference = -25_000m, causes = new[] { "Payments" }, detail = new Dictionary<string, decimal> { ["principal"] = -25_000m },
                    omitted = false, aboveTolerance = false, interestMode = "Corrido",
                    currentInstallment = new { number = 7, dueDate = "2026-11-30", amount = 312_400m, treatment = "Include", left = (decimal?)null },
                    warnings = new[] { "Cambió desde la proyección" },
                },
                new
                {
                    loanPublicId = Otro, number = "65-11111", toPayOff = 0m, daysPastDue = 0, requiresRestructuring = false, kind = "Loan", productPublicId = Otro,
                    mode = "Total", requestedMode = "Total", projected = 800_000m, current = 0m, toApply = 0m, difference = -800_000m,
                    causes = new[] { "ProductClosed" }, detail = new Dictionary<string, decimal>(), omitted = true, aboveTolerance = false,
                    interestMode = (string?)null, currentInstallment = (object?)null, warnings = Array.Empty<string>(),
                },
            },
            tranche = 1, grossAmount = 8_000_000m, netToDisburse = 2_901_543.22m,
            schedule = new { installmentCount = 24, annualEffectiveRate = 0.2m, installments = Array.Empty<object>() },
            prevalidation = new { applies = false, postable = (bool?)null, responded = true },
            segregation = new { conflict = (string?)null },
            blocking = Array.Empty<string>(), warnings = Array.Empty<string>(), previewSha256 = "sha256:x",
            payoffDifferences = new { count = 2, total = -825_000m, requiresAcknowledgement = true, aboveTolerance = false },
        });

        var r = await _desembolsos.VistaPreviaAsync(new DesembolsoRequest { ApplicationPublicId = Solicitud });

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = r.Value!;
        var recogida = vista.Payoffs[0];
        (recogida.Projected, recogida.Current, recogida.ToApply, recogida.Difference).Should().Be((5_123_456.78m, 5_098_456.78m, 5_098_456.78m, -25_000m));
        recogida.Causes.Should().Equal("Payments");
        recogida.Detail.Should().ContainKey("principal").WhoseValue.Should().Be(-25_000m);
        recogida.CurrentInstallment!.Number.Should().Be(7);
        recogida.InterestMode.Should().Be("Corrido");
        vista.Payoffs[1].Omitted.Should().BeTrue();
        vista.Recogido.Should().Be(5_098_456.78m, "lo omitido no se recoge");
        vista.PayoffDifferences.Should().Be(new DiferenciasDeRecogidasDto(2, -825_000m, true, false));
    }

    [Fact]
    public async Task El_resultado_trae_lo_aplicado_por_producto_y_lo_omitido()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new
        {
            loanPublicId = Otro, loanNumber = "CR-000200", operationPublicId = Guid.NewGuid(), operationNumber = "DES-1", tranche = 1, grossAmount = 8_000_000m,
            payoffs = new[]
            {
                new
                {
                    loanPublicId = Credito, number = "65-78925", applied = 5_098_456.78m, receiptOperationPublicId = Pago, kind = "Loan", mode = "Total",
                    projected = (decimal?)5_123_456.78m, current = 5_098_456.78m, difference = (decimal?)-25_000m, causes = new[] { "Payments" },
                },
            },
            netToDisburse = 2_901_543.22m,
            order = new { publicId = Guid.NewGuid(), number = "OG-1", status = "Issued" },
            scheduleVersion = 1, messages = Array.Empty<object>(), warnings = Array.Empty<string>(),
            omittedPayoffs = new[] { new { loanPublicId = Otro, number = "65-11111", projected = (decimal?)800_000m, reason = "Ya estaba cancelado" } },
        });

        var r = await _desembolsos.DesembolsarAsync(new DesembolsoRequest { ApplicationPublicId = Solicitud }, new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var hecho = r.Value!;
        var aplicada = hecho.Payoffs.Single();
        (aplicada.Applied, aplicada.ReceiptOperationPublicId, aplicada.Mode, aplicada.Projected, aplicada.Difference)
            .Should().Be((5_098_456.78m, Pago, "Total", (decimal?)5_123_456.78m, (decimal?)-25_000m));
        aplicada.Causes.Should().Equal("Payments");
        hecho.Recogido.Should().Be(5_098_456.78m);
        hecho.OmittedPayoffs.Should().ContainSingle().Which.Should().Be(new RecogidaOmitidaDto(Otro, "65-11111", 800_000m, "Ya estaba cancelado"));
    }

    [Fact]
    public async Task La_garantia_y_el_pagare_del_credito_recogido_dicen_que_se_liberan_al_girar()
    {
        const string aviso = "Por liberar al girar el crédito CR-000200";
        _servidor.Responder = r => r.RequestUri!.AbsolutePath.EndsWith("/collaterals", StringComparison.Ordinal)
            ? ServidorDeCartera.Json(HttpStatusCode.OK, new
            {
                items = new[]
                {
                    new
                    {
                        publicId = Guid.NewGuid(), kind = "Pledge", status = "Valid", owner = new { kind = "Loan", publicId = Credito, number = "65-78925" },
                        ownerPersonPublicId = Guid.NewGuid(), description = "Moto", value = 6_000_000m, admissibleValue = 4_200_000m, constitutedOn = "2026-01-15",
                        validUntil = (string?)null, policyNumber = (string?)null, policyExpiresOn = (string?)null, fundCoverage = (decimal?)null, releaseNotice = aviso,
                    },
                },
                page = 1, pageSize = 20, totalCount = 1,
            })
            : ServidorDeCartera.Json(HttpStatusCode.OK, new
            {
                items = new[]
                {
                    new
                    {
                        publicId = Guid.NewGuid(), form = "Physical", status = "InCustody", owner = new { kind = "Loan", publicId = Credito, number = "65-78925" },
                        number = "P-1", channelCode = (string?)null, externalNoteId = (string?)null, simulated = false, perfected = true, custody = "Caja fuerte",
                        releaseNotice = aviso,
                    },
                },
                page = 1, pageSize = 20, totalCount = 1,
            });

        var garantias = await _garantias.GarantiasAsync(credito: Credito);
        var pagares = await _garantias.PagaresAsync(credito: Credito);

        garantias.Value!.Items.Single().ReleaseNotice.Should().Be(aviso);
        pagares.Value!.Items.Single().ReleaseNotice.Should().Be(aviso);
    }

    // ----------------------------------------------------------------------------------------------------------- textos --

    [Theory]
    [InlineData("Payments", "Pagos recibidos")]
    [InlineData("Accrual", "Causación")]
    [InlineData("Arrears", "Mora")]
    [InlineData("CurrentInstallment", "Cuota causada")]
    [InlineData("Parameters", "Parámetros")]
    [InlineData("ProductClosed", "Ya cancelado")]
    [InlineData("Other", "Otros movimientos")]
    [InlineData(null, "—")]
    public void Cada_causa_se_dice_en_palabras(string? causa, string texto) => TextosDeDesembolso.CausaDeDiferencia(causa).Should().Be(texto);

    [Fact]
    public void Las_causas_van_juntas_y_sin_causa_es_una_raya()
    {
        TextosDeDesembolso.Causas(["Payments", "Accrual"]).Should().Be("Pagos recibidos, Causación");
        TextosDeDesembolso.Causas([]).Should().Be("—");
        TextosDeDesembolso.Causas(null).Should().Be("—");
    }

    [Theory]
    [InlineData("principal", "Capital")]
    [InlineData("currentInterest", "Interés corriente")]
    [InlineData("interestReturned", "Interés devuelto por proporcionales")]
    [InlineData("defaultInterest", "Interés de mora")]
    [InlineData("advance", "Anticipo a favor")]
    public void Cada_concepto_de_la_diferencia_se_dice_en_palabras(string concepto, string texto) =>
        TextosDeDesembolso.ConceptoDeDiferencia(concepto).Should().Be(texto);

    [Fact]
    public void La_diferencia_lleva_su_signo_y_la_clase_que_dice_a_favor_de_quien()
    {
        TextosDeDesembolso.Diferencia(25_000m).Should().Be("+" + TextosDeOriginacion.Pesos(25_000m));
        TextosDeDesembolso.Diferencia(-25_000m).Should().Be("-" + TextosDeOriginacion.Pesos(25_000m));
        TextosDeDesembolso.Diferencia(0m).Should().Be(TextosDeOriginacion.Pesos(0m));
        TextosDeDesembolso.Diferencia(null).Should().Be("—", "sin proyección no hay con qué comparar");
        TextosDeDesembolso.ClaseDeDiferencia(25_000m).Should().Be("diferencia-en-contra", "recoger cuesta más: el asociado recibe menos");
        TextosDeDesembolso.ClaseDeDiferencia(-25_000m).Should().Be("diferencia-a-favor");
        TextosDeDesembolso.ClaseDeDiferencia(0m).Should().BeEmpty();
    }

    [Fact]
    public void El_estado_de_cada_producto_dice_si_se_cancela_se_abona_se_omite_o_se_bloquea()
    {
        var total = new RecogidaPropuestaDto(Credito, "65-1", 100m, 0, false);
        TextosDeDesembolso.EstadoDeRecogida(total).Should().Be("Se cancela");
        TextosDeDesembolso.EstadoDeRecogida(total with { Mode = "Partial" }).Should().Be("Se abona");
        TextosDeDesembolso.EstadoDeRecogida(total with { Omitted = true }).Should().Be("Ya cancelado: no se recoge");
        TextosDeDesembolso.EstadoDeRecogida(new RecogidaPropuestaDto(Credito, "65-1", 100m, 40, true)).Should().Be("Requiere reestructuración");
        TextosDeDesembolso.EstadoDeRecogida(total with { AboveTolerance = true }).Should().Be("Pasa de la tolerancia");
        TextosDeDesembolso.ModoDeRecogida("Partial").Should().Be("Parcial (abono a capital)");
        TextosDeDesembolso.Bloqueo("Lending.Payoff.DifferenceAboveTolerance").Should().Contain("devuelva la solicitud");
    }
}
