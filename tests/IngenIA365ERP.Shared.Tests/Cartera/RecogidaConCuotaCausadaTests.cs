using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 028 (E2, T066, T072; HU4): lo que la pantalla lee de la cuota causada —el tratamiento que rige, si el asesor lo elige («Preguntar»), lo que cuesta
/// recoger dejándola, la cuota enviada a la pagaduría o a la nómina y qué pasa con ella—, cómo lo pide al servidor y cómo lo resume, y los textos de la cuota
/// dejada en el desembolso y en el detalle del crédito. (nuevo)
/// </summary>
public class RecogidaConCuotaCausadaTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly Guid Credito = Guid.Parse("33333333-4444-5555-6666-777777777777");

    /// <summary>Un producto con «Preguntar»: 5.000.000 con la cuota, 4.700.000 dejándola (la cuota 7 deberá 312.400), enviada a la pagaduría.</summary>
    private const string ProductoConPreguntar = """
        {
          "kind": 1, "productPublicId": "33333333-4444-5555-6666-777777777777", "number": "65-78925", "line": "CONLIB", "origin": 1, "channel": 3,
          "daysPastDue": 0, "installment": 303683, "eligible": true, "blockReason": null, "admittedModes": [1, 2], "toPayOff": 5000000,
          "breakdown": { "principal": 4900000, "currentInterest": 100000, "interestReturned": 0, "interestCharged": 0, "defaultInterest": 0, "insurance": 0,
                         "administration": 0, "otherCharges": 0, "advance": 0, "creditBalance": 0 },
          "interestMode": "Corrido", "interestModeReason": null,
          "currentInstallment": {
            "number": 7, "dueDate": "2026-12-30", "amount": 312400, "treatment": 1, "choosable": true, "left": 312400, "parameter": "Preguntar",
            "explanation": "La cuota 7 por $312.400 ya se envió …: se recoge completa; …",
            "includedExplanation": "La cuota 7 por $312.400 ya se envió …: se recoge completa; …",
            "leftExplanation": "La cuota 7 por $312.400 ya se envió …: se deja en el crédito y la pagará la pagaduría; …",
            "sent": { "state": 1, "paymaster": "COLPENSIONES", "file": "ADP-000123", "period": "noviembre de 2026", "lineAmount": 312400,
                      "installmentNumber": 7, "agreement": "ADP", "currentAmount": 312400, "pastDueAmount": 0,
                      "text": "La cuota 7 ya se envió a la pagaduría COLPENSIONES en el archivo ADP-000123 de noviembre de 2026 y no ha respondido." }
          },
          "toPayOffExcludingCurrentInstallment": 4700000,
          "breakdownExcludingCurrentInstallment": { "principal": 4700000, "currentInterest": 0, "interestReturned": 0, "interestCharged": 0, "defaultInterest": 0,
                         "insurance": 0, "administration": 0, "otherCharges": 0, "advance": 0, "creditBalance": 0 },
          "inOtherApplication": null, "fingerprint": "sha256:x", "warnings": []
        }
        """;

    private static ProductoParaRecogerDto Producto() => JsonSerializer.Deserialize<ProductoParaRecogerDto>(ProductoConPreguntar, Web)!;

    [Fact]
    public void Lee_la_cuota_causada_con_su_envio_y_lo_que_cuesta_dejarla()
    {
        var p = Producto();

        p.CuotaElegible.Should().BeTrue();
        var c = p.CurrentInstallment!;
        (c.Number, c.Treatment, c.Choosable, c.Left, c.Parameter).Should().Be((7, TextosDeRecogida.CuotaIncluida, true, (decimal?)312_400m, "Preguntar"));
        c.Sent!.Texto.Should().StartWith("La cuota 7 ya se envió a la pagaduría COLPENSIONES");
        c.Sent.EnNominaPropia.Should().BeFalse();
        p.BreakdownExcludingCurrentInstallment!.Total.Should().Be(4_700_000m);
    }

    [Fact]
    public void Con_Preguntar_lo_que_se_recoge_sigue_la_eleccion_del_asesor()
    {
        var p = Producto();

        p.ParaRecoger(null).Should().Be(5_000_000m, "sin elegir, el tratamiento de la línea (incluirla)");
        p.ParaRecoger(TextosDeRecogida.CuotaIncluida).Should().Be(5_000_000m);
        p.ParaRecoger(TextosDeRecogida.CuotaExcluida).Should().Be(4_700_000m);
        p.Desglose(TextosDeRecogida.CuotaExcluida).Principal.Should().Be(4_700_000m);
        p.TratamientoDeLaCuota(TextosDeRecogida.CuotaExcluida).Should().Be(TextosDeRecogida.CuotaExcluida);
        p.CurrentInstallment!.Explicacion(TextosDeRecogida.CuotaExcluida).Should().Contain("se deja en el crédito");
        p.CurrentInstallment.Explicacion(TextosDeRecogida.CuotaIncluida).Should().Contain("se recoge completa");

        var sinElegir = p with { CurrentInstallment = p.CurrentInstallment with { Choosable = false } };
        sinElegir.ParaRecoger(TextosDeRecogida.CuotaExcluida).Should().Be(5_000_000m, "sin «Preguntar» la elección del asesor no cuenta");
        sinElegir.TratamientoDeLaCuota(TextosDeRecogida.CuotaExcluida).Should().Be(TextosDeRecogida.CuotaIncluida);
    }

    [Fact]
    public void Lo_marcado_pide_el_tratamiento_sólo_en_una_recogida_total_y_el_resumen_lo_sigue()
    {
        var p = Producto();
        var dejada = new RecogidaMarcada(Credito, "65-78925", TextosDeRecogida.ModoTotal, null, 4_700_000m, TextosDeRecogida.CuotaExcluida);

        dejada.ComoPedido().InstallmentTreatment.Should().Be(TextosDeRecogida.CuotaExcluida);
        (dejada with { Mode = TextosDeRecogida.ModoParcial, Amount = 100m }).ComoPedido().InstallmentTreatment.Should().BeNull("un parcial no la toca");
        var json = JsonSerializer.Serialize(dejada.ComoPedido(), Web);
        json.Should().Contain("\"installmentTreatment\":2");

        var resumen = SeleccionDeRecogida.Resumir([p], [dejada], 8_000_000m, null);
        (resumen.SelectedTotal, resumen.NetToDisburse).Should().Be((4_700_000m, (decimal?)3_300_000m));
        SeleccionDeRecogida.Resumir([p], [dejada with { InstallmentTreatment = null }], 8_000_000m, null).SelectedTotal.Should().Be(5_000_000m);
    }

    [Fact]
    public void La_solicitud_guardada_abre_la_correccion_con_la_cuota_que_eligio()
    {
        var guardada = new RecogidaDeSolicitudDto(TextosDeRecogida.ClaseCredito, Credito, null, "65-78925", TextosDeRecogida.ModoTotal, null, 4_700_000m,
            new DesgloseDeRecogidaDto(4_700_000m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m), TextosDeRecogida.CuotaExcluida, "Corrido",
            new CuotaCausadaDeRecogidaDto(7, null, 312_400m, TextosDeRecogida.CuotaExcluida, false, null) { Left = 312_400m },
            new DateOnly(2026, 12, 5), DateTime.UtcNow, true, null, 1);

        guardada.ComoMarcada()!.InstallmentTreatment.Should().Be(TextosDeRecogida.CuotaExcluida);
        guardada.ComoPedido().InstallmentTreatment.Should().Be(TextosDeRecogida.CuotaExcluida);
    }

    [Fact]
    public void Los_estados_del_envio_y_las_opciones_se_dicen_en_palabras()
    {
        TextosDeRecogida.EstadoDeEnvio(TextosDeRecogida.EnvioSinRespuesta).Should().Be("Enviada a la pagaduría, sin respuesta");
        TextosDeRecogida.EstadoDeEnvio(TextosDeRecogida.EnvioDescontadaPendienteDeGiro).Should().Contain("pendiente de giro");
        TextosDeRecogida.EstadoDeEnvio(TextosDeRecogida.EnvioNoDescontada).Should().Contain("No descontada");
        TextosDeRecogida.EstadoDeEnvio(TextosDeRecogida.EnvioNominaGenerada).Should().Contain("nómina propia");
        TextosDeRecogida.EstadoDeEnvio(TextosDeRecogida.EnvioNominaCalculada).Should().Contain("borrador");
        TextosDeRecogida.EstadoDeEnvio(TextosDeRecogida.EnvioNominaAprobada).Should().Contain("Aplicada");
        TextosDeRecogida.EstadoDeEnvio(TextosDeRecogida.EnvioNinguno).Should().Be("Sin enviar");
        TextosDeRecogida.OpcionDeCuota(TextosDeRecogida.CuotaIncluida).Should().Be("Incluir la cuota");
        TextosDeRecogida.OpcionDeCuota(TextosDeRecogida.CuotaExcluida).Should().Be("Dejarla a su canal");
        new CuotaEnviadaDto(TextosDeRecogida.EnvioNominaCalculada, null, null, "Noviembre", 1m).EnNominaPropia.Should().BeTrue();
        new CuotaEnviadaDto(TextosDeRecogida.EnvioNoDescontada, "X", "A-1", "noviembre", 1m).Texto.Should().Be("No descontada por la pagaduría");
    }

    [Fact]
    public void La_vista_previa_del_desembolso_dice_la_cuota_que_se_deja()
    {
        var json = """
            { "number": 7, "dueDate": "2026-12-30", "amount": 330000, "treatment": "Exclude", "left": 312400,
              "sent": { "state": 2, "paymaster": "COLPENSIONES", "file": "ADP-000123", "period": "noviembre de 2026", "lineAmount": 312400 },
              "explanation": "La cuota 7 por $312.400 ya la descontó … : se deja en el crédito …" }
            """;
        var cuota = JsonSerializer.Deserialize<CuotaCausadaDelDesembolsoDto>(json, Web)!;

        cuota.SeDeja.Should().BeTrue();
        cuota.Texto.Should().StartWith("Cuota causada 7 (vence el 30/12/2026) por ").And.Contain("312.400").And.EndWith(": se deja a su canal, como se envió.");
        cuota.Sent!.State.Should().Be(TextosDeRecogida.EnvioDescontadaPendienteDeGiro);
        cuota.Explanation.Should().Contain("se deja en el crédito");
        (cuota with { Treatment = "Include" }).Texto.Should().Contain("330.000").And.EndWith(": se incluye en la recogida.");
    }

    [Fact]
    public void El_credito_recogido_dice_la_cuota_dejada_mientras_se_deba()
    {
        var por = new VinculoDeRecogidaDto("Loan", Credito, "CR-000045", new DateOnly(2026, 11, 28), "Total", 4_700_000m, false)
        {
            LeftInstallmentNumber = 7, LeftInstallmentAmount = 312_400m, LeftInstallmentChannel = "PaymasterDeduction",
        };

        por.CuotaDejadaPendiente.Should().BeTrue();
        por.CuotaDejada.Should().StartWith("Cuota dejada a la pagaduría: cuota 7 por ").And.Contain("312.400");
        (por with { LeftInstallmentChannel = "OwnPayroll" }).CuotaDejada.Should().StartWith("Cuota dejada a la nómina:");
        (por with { LeftInstallmentChannel = "Cashier" }).CuotaDejada.Should().StartWith("Cuota dejada a su canal:");
        (por with { LeftInstallmentSettledAt = DateTime.UtcNow }).CuotaDejadaPendiente.Should().BeFalse("ya se pagó");
        (por with { Reversed = true }).CuotaDejadaPendiente.Should().BeFalse("la recogida se reversó");
        (por with { LeftInstallmentNumber = null }).CuotaDejada.Should().BeEmpty();
    }
}
