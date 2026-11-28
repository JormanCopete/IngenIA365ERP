using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Recaudo;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Recaudo;

/// <summary>
/// Feature 028 (E2, T071, T073; FR-023, D4): la cuota enviada en la caja de Shared (<see cref="CuotaEnviadaEnLaCaja"/>). «Todo para cancelar (F7)» propone todo
/// menos la cuota enviada sólo con «Excluir»; con «Preguntar» propone la cancelación y el cajero la deja; con «Incluir» (de fábrica) la cancelación la cobra con
/// el aviso. Sin el valor de Cartera o sin la modalidad admitida no se ofrece. El listado del contrato llega con los campos nuevos. (nuevo)
/// </summary>
public class CuotaEnviadaEnLaCajaTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static ObligacionPagableDto Credito(string? alCancelar, decimal? salvo = 1_850_000m, bool enviada = true, bool nomina = false,
        IReadOnlyList<int>? admitidas = null) =>
        new(Guid.NewGuid(), "CR-1", "Consumo", 1, 0, 0m, 100_000m, 0m, 2_000_000m, admitidas ?? [1, 2, 3, 4, 5, 6, 7, 8], [])
        {
            SentInstallment = enviada
                ? new CuotaEnviadaDeLaCajaDto(nomina ? 5 : 1, 7, new DateOnly(2027, 1, 1), "Secretaría", "MUN", "MUN-000001", "enero de 2027", 150_000m, 0m,
                    150_000m, null, "La cuota 7 ya se envió a la pagaduría Secretaría en el archivo MUN-000001 de enero de 2027 y no ha respondido.")
                : null,
            SentInstallmentAtPayoff = alCancelar,
            PayoffExceptSentInstallmentAmount = salvo,
            SentInstallmentLeftAmount = salvo is null ? null : 150_000m,
        };

    [Fact]
    public void De_fabrica_F7_cancela_todo_y_avisa_que_la_cuota_va_en_su_canal()
    {
        var o = Credito(CuotaEnviadaEnLaCaja.Incluir);

        CuotaEnviadaEnLaCaja.ParaCancelar(o).Should().Be((CuotaEnviadaEnLaCaja.Cancelacion, 2_000_000m));
        CuotaEnviadaEnLaCaja.OfreceDejarla(o).Should().BeFalse();
        CuotaEnviadaEnLaCaja.Aviso(o, CuotaEnviadaEnLaCaja.Cancelacion).Should().Contain("ya se envió").And.Contain("La cancelación la cobra")
            .And.Contain("saldo de la pagaduría").And.NotContain("Puede dejarla");
    }

    [Fact]
    public void Con_Excluir_F7_propone_todo_menos_la_cuota_enviada()
    {
        var o = Credito(CuotaEnviadaEnLaCaja.Excluir);

        CuotaEnviadaEnLaCaja.ParaCancelar(o).Should().Be((CuotaEnviadaEnLaCaja.SalvoLaCuota, 1_850_000m));
        CuotaEnviadaEnLaCaja.LaPropone(o).Should().BeTrue();
        CuotaEnviadaEnLaCaja.ElCajeroElige(o).Should().BeFalse();
        CuotaEnviadaEnLaCaja.Aviso(o, CuotaEnviadaEnLaCaja.SalvoLaCuota).Should().Contain("todo menos la cuota 7").And.Contain("la pague la pagaduría");
    }

    [Fact]
    public void Con_Preguntar_F7_propone_la_cancelacion_y_el_cajero_puede_dejarla()
    {
        var o = Credito(CuotaEnviadaEnLaCaja.Preguntar);

        CuotaEnviadaEnLaCaja.ParaCancelar(o).Should().Be((CuotaEnviadaEnLaCaja.Cancelacion, 2_000_000m));
        CuotaEnviadaEnLaCaja.ElCajeroElige(o).Should().BeTrue();
        CuotaEnviadaEnLaCaja.AlDejarla(o, dejarla: true).Should().Be((CuotaEnviadaEnLaCaja.SalvoLaCuota, 1_850_000m));
        CuotaEnviadaEnLaCaja.AlDejarla(o, dejarla: false).Should().Be((CuotaEnviadaEnLaCaja.Cancelacion, 2_000_000m));
        CuotaEnviadaEnLaCaja.Aviso(o, CuotaEnviadaEnLaCaja.Cancelacion).Should().Contain("Puede dejarla");
    }

    [Fact]
    public void Sin_el_valor_de_Cartera_sin_la_modalidad_o_sin_nada_enviado_no_se_ofrece()
    {
        CuotaEnviadaEnLaCaja.OfreceDejarla(Credito(CuotaEnviadaEnLaCaja.Excluir, salvo: null)).Should().BeFalse();
        CuotaEnviadaEnLaCaja.OfreceDejarla(Credito(CuotaEnviadaEnLaCaja.Excluir, admitidas: [1, 2, 3])).Should().BeFalse();
        var sinEnviar = Credito(CuotaEnviadaEnLaCaja.Excluir, enviada: false);
        CuotaEnviadaEnLaCaja.OfreceDejarla(sinEnviar).Should().BeFalse();
        CuotaEnviadaEnLaCaja.Aviso(sinEnviar, CuotaEnviadaEnLaCaja.Cancelacion).Should().BeNull();
        CuotaEnviadaEnLaCaja.Aviso(Credito(CuotaEnviadaEnLaCaja.Incluir), 3).Should().BeNull("sólo se avisa al cancelar");
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T062a; D13 ajustada por el dueño): dentro del tope legal del prepago Cartera deja la cuota con el interés proporcional; el aviso lo
    /// dice, con lo que sobraría si su canal la paga completa (la pagaduría: saldo de la pagaduría; la nómina: vuelve al neto del empleado).
    /// </summary>
    [Fact]
    public void Con_el_proporcional_el_aviso_dice_la_regla_y_lo_que_sobraria()
    {
        var o = Credito(CuotaEnviadaEnLaCaja.Excluir) with
        {
            SentInstallmentLeftAmount = 147_000m, SentInstallmentLeftRule = CuotaEnviadaEnLaCaja.Proporcional,
            SentInstallmentLeftRuleReason = "WithinLegalPrepaymentCap", SentInstallmentAsSentAmount = 150_000m,
        };

        CuotaEnviadaEnLaCaja.Aviso(o, CuotaEnviadaEnLaCaja.SalvoLaCuota).Should()
            .Contain("con el interés proporcional (dentro del tope legal del prepago sin penalidad, Ley 1555 de 2012)")
            .And.Contain("147.000").And.Contain("150.000").And.Contain("3.000").And.Contain("saldo de la pagaduría").And.NotContain("tal como se envió");
        var nomina = Credito(CuotaEnviadaEnLaCaja.Excluir, nomina: true) with
        {
            SentInstallmentLeftAmount = 147_000m, SentInstallmentLeftRule = CuotaEnviadaEnLaCaja.Proporcional, SentInstallmentAsSentAmount = 150_000m,
        };
        CuotaEnviadaEnLaCaja.Aviso(nomina, CuotaEnviadaEnLaCaja.SalvoLaCuota).Should().Contain("vuelven al neto del empleado");
        CuotaEnviadaEnLaCaja.Aviso(Credito(CuotaEnviadaEnLaCaja.Excluir), CuotaEnviadaEnLaCaja.SalvoLaCuota).Should()
            .Contain("tal como se envió", "fuera del tope legal, como siempre");
    }

    [Fact]
    public void En_la_nomina_propia_el_aviso_dice_que_el_descuento_baja_a_cero()
    {
        var o = Credito(CuotaEnviadaEnLaCaja.Incluir, nomina: true);

        CuotaEnviadaEnLaCaja.Aviso(o, CuotaEnviadaEnLaCaja.Cancelacion).Should().Contain("baja a cero");
        CuotaEnviadaEnLaCaja.Aviso(Credito(CuotaEnviadaEnLaCaja.Excluir, nomina: true), CuotaEnviadaEnLaCaja.SalvoLaCuota).Should().Contain("la pague la nómina");
    }

    [Fact]
    public void El_listado_del_contrato_llega_con_la_cuota_enviada_y_su_regla()
    {
        const string json = """
            {"obligations":[{"obligationPublicId":"0f8fad5b-d9cb-469f-a165-70867728950e","number":"CR-1","creditLineCode":"CONLIB","creditLineName":"Consumo",
            "role":1,"status":1,"daysPastDue":0,"dueToday":0,"cycleInstallment":100000,"pastDue":0,"payoffAmount":2000000,"advance":0,"creditBalance":0,
            "admittedModalities":[1,2,3,4,5,6,7,8],"blockReasons":[],"hasPaymentAgreement":false,"validationFailed":false,
            "sentInstallment":{"state":1,"installmentNumber":7,"dueDate":"2027-01-01","paymaster":"Secretaría","agreement":"MUN","file":"MUN-000001",
            "period":"enero de 2027","currentAmount":150000,"pastDueAmount":0,"totalAmount":150000,"payrollRunPublicId":null,"explanation":"La cuota 7 ya se envió."},
            "sentInstallmentAtPayoff":"Excluir","payoffExceptSentInstallmentAmount":1850000,"sentInstallmentLeftAmount":150000,
            "sentInstallmentLeftRule":"Proporcional","sentInstallmentLeftRuleReason":"WithinLegalPrepaymentCap","sentInstallmentAsSentAmount":152000}],"lendingStatus":2}
            """;

        var listado = JsonSerializer.Deserialize<ListadoPagableDto>(json, Json)!;

        var o = listado.Obligations.Should().ContainSingle().Subject;
        (o.SentInstallment!.State, o.SentInstallment.InstallmentNumber, o.SentInstallment.File).Should().Be((1, (int?)7, "MUN-000001"));
        (o.SentInstallmentAtPayoff, o.PayoffExceptSentInstallmentAmount, o.SentInstallmentLeftAmount).Should().Be(("Excluir", (decimal?)1_850_000m, (decimal?)150_000m));
        (o.SentInstallmentLeftRule, o.SentInstallmentLeftRuleReason, o.SentInstallmentAsSentAmount)
            .Should().Be(("Proporcional", "WithinLegalPrepaymentCap", (decimal?)152_000m), "la regla con que Cartera deja la cuota (D13 ajustada)");
        CuotaEnviadaEnLaCaja.LaPropone(o).Should().BeTrue();
        CajaClient.Modalidad(8).Should().Be("Todo menos la cuota enviada");
        CajaClient.AyudaDeModalidad(8).Should().Contain("tal como se envió");
    }
}
