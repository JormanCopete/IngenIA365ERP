using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 028 (E2, T071, T073; FR-023, D4, H1): la cuota enviada en la caja.
/// <list type="bullet">
/// <item>Cartera (contrato real) dice en el listado, con la cuota del período ya enviada, lo que dice <c>Pagos.CuotaEnviadaAlCancelar</c> de la línea (de fábrica
/// «Incluir») y, si esa cuota sigue en su canal sin respuesta y es la causada, lo que cuesta cancelar todo menos ella y lo que ella queda debiendo; sin nada
/// enviado, nada de eso.</item>
/// <item>Las reglas de la caja (contrato simulado) sólo admiten «todo menos la cuota enviada» con la línea en «Excluir» o «Preguntar» y el valor de Cartera; con
/// «Incluir» (de fábrica) la rechazan, y la cancelación total sigue admitida con cualquier valor del parámetro.</item>
/// </list>
/// (nuevo)
/// </summary>
public class CuotaEnviadaEnLaCajaTests
{
    // ------------------------------------------------------------------------------------------------- Cartera: el listado --

    private static readonly DateOnly AMitad = PagosDePrueba.Hoy.AddDays(10);
    private static readonly DateOnly VenceLaPrimera = new(2027, 1, 1);

    private static void AlCancelar(PagosDePrueba p, string valor)
    {
        p.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.PagosCuotaEnviadaAlCancelar, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedBy = "test", CreatedAt = p.G.UtcNow.AddDays(-10), ScopeKind = ParameterScopeKind.CreditLine, ScopeId = p.Credito.CreditLineId,
        });
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();
    }

    /// <summary>La cuota 1 del crédito de Ana en un archivo enviado a la pagaduría, sin respuesta.</summary>
    private static void Enviada(PagosDePrueba p)
    {
        var pagaduria = p.D.O.Persona("Pagaduría", "Municipal", "800100300", null, null);
        var convenio = new Domain.Entities.Lending.Payroll.PayrollAgreement { Code = "MUN", PaymasterPersonId = pagaduria.Id, CreatedBy = "test" };
        p.Db.PayrollAgreements.Add(convenio);
        p.Db.SaveChanges();
        var archivo = new Domain.Entities.Lending.Payroll.PayrollDeductionFile
        {
            PayrollAgreementId = convenio.Id, PayrollAgreementVersionId = 1, PeriodYear = 2027, PeriodMonth = 1, Number = "MUN-000001", OutboundFormatId = 1,
            FileName = "MUN-000001.txt", FileSha256 = "x", GeneratedAt = p.G.UtcNow, CreatedBy = "test",
        };
        archivo.MarcarEnviado(p.G.UtcNow, Guid.NewGuid(), null);
        p.Db.PayrollDeductionFiles.Add(archivo);
        p.Db.SaveChanges();
        p.Db.PayrollDeductionFileLines.Add(new Domain.Entities.Lending.Payroll.PayrollDeductionFileLine
        {
            PayrollDeductionFileId = archivo.Id, LineNumber = 1, LoanId = p.Credito.Id, LoanPayrollLinkId = 1, PersonId = p.Credito.DebtorPersonId,
            InstallmentNumber = 1, DueDate = VenceLaPrimera, CurrentAmount = 150_000m, TotalAmount = 150_000m, LibranzaDate = PagosDePrueba.Hoy, CreatedBy = "test",
        });
        p.Db.SaveChanges();
        p.Db.ChangeTracker.Clear();
    }

    private static async Task<ObligacionListadaDto> ListadoAsync(PagosDePrueba p)
    {
        p.Recaudadores[ClavesDeRecaudo.Codigo] = IntegrationParticipantClass.Receipts;
        var r = await p.Contrato().ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, p.D.O.Ana.PublicId, AMitad), default);
        r.Result.Should().Be(ResultadoDelContrato.Answered, r.Message);
        return r.Value!.Obligations.Single(o => o.ObligationPublicId == p.Credito.PublicId);
    }

    /// <summary>
    /// 2026-11-27 (D13 ajustada por el dueño): la regla con que se deja la cuota la decide el motivo legal, como en el contrato: sin los valores nacionales
    /// (tope desconocido) y dentro del tope legal del prepago (880 SMMLV), el proporcional; fuera del tope (1 SMMLV), tal como se envió.
    /// </summary>
    [Theory]
    [InlineData("sin-tope", "Proporcional", "LegalPrepaymentCapUnknown")]
    [InlineData("dentro-del-tope", "Proporcional", "WithinLegalPrepaymentCap")]
    [InlineData("fuera-del-tope", "TalComoSeEnvio", null)]
    public async Task El_listado_dice_la_regla_de_la_linea_y_lo_que_cuesta_dejar_la_cuota_enviada(string tope, string regla, string? motivo)
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: tope == "sin-tope", topeSmmlv: tope == "fuera-del-tope" ? 1m : 880m);
        AlCancelar(p, ParametrosDeCartera.CuotaExcluir);
        await p.CausarHastaAsync(AMitad);
        Enviada(p);
        var cargado = await p.C.Cargador().CargarAsync(p.Credito.Id, null, AMitad, default);
        var estado = MotorDeCartera.Reconstruir(cargado!.Entrada!, AMitad);
        var proporcional = regla == AplicadorDePagos.ReglaDeLaCuotaDejadaProporcional;
        var esperado = AplicadorDePagos.ValorParaCancelarSalvoCuota(estado, null, cuotaCompleta: false, proporcional: proporcional);
        var talComoSeEnvio = AplicadorDePagos.ValorParaCancelarSalvoCuota(estado, null, cuotaCompleta: false);

        var o = await ListadoAsync(p);

        o.SentInstallment!.InstallmentNumber.Should().Be(1);
        o.SentInstallmentAtPayoff.Should().Be(CuotaEnviadaAlCancelar.Excluir);
        (o.PayoffExceptSentInstallmentAmount, o.SentInstallmentLeftAmount).Should().Be(((decimal?)esperado.ParaCancelar, (decimal?)esperado.QuedaEnLaCuota));
        (o.SentInstallmentLeftRule, o.SentInstallmentLeftRuleReason).Should().Be((regla, motivo));
        o.SentInstallmentAsSentAmount.Should().Be(talComoSeEnvio.QuedaEnLaCuota, "lo que la cuota debería tal como se envió");
        o.PayoffExceptSentInstallmentAmount.Should().Be(talComoSeEnvio.ParaCancelar, "lo que se cancela no cambia con la regla");
        if (proporcional) o.SentInstallmentLeftAmount.Should().BeLessThan(o.SentInstallmentAsSentAmount!.Value, "el proporcional no cobra los días sobre lo cancelado");
        o.AdmittedModalities.Should().Contain(ModalidadDePago.PayoffExceptCurrentInstallment);
        o.PayoffExceptSentInstallmentAmount.Should().BeLessThan(o.PayoffAmount);
    }

    [Fact]
    public async Task De_fabrica_dice_Incluir_y_sin_nada_enviado_no_dice_nada()
    {
        using var p = await PagosDePrueba.CrearAsync(sinTopeDePrepago: true);
        await p.CausarHastaAsync(AMitad);

        var sinEnviar = await ListadoAsync(p);
        (sinEnviar.SentInstallment, sinEnviar.SentInstallmentAtPayoff, sinEnviar.PayoffExceptSentInstallmentAmount)
            .Should().Be(((CuotaEnviadaDto?)null, (string?)null, (decimal?)null));

        Enviada(p);
        var enviada = await ListadoAsync(p);
        enviada.SentInstallmentAtPayoff.Should().Be(CuotaEnviadaAlCancelar.Incluir, "de fábrica la cancelación la cobra");
    }

    // ------------------------------------------------------------------------------------------------- Recaudo: las reglas --

    private readonly CajaConContratos _k = new();

    private void Listado(string? alCancelar, decimal? salvo)
    {
        var o = new ObligacionListadaDto(_k.CreditoA, "CR-A", "CONLIB", "Consumo", RolEnLaObligacion.Debtor, EstadoDeObligacion.Active, 0, 0m, 100_000m, 0m,
            2_000_000m, 0m, 0m, Enum.GetValues<ModalidadDePago>(), [], false, false)
        {
            SentInstallment = new CuotaEnviadaDto(EstadoDeCuotaEnviada.SentWithoutResponse, 1, VenceLaPrimera, "Pagaduría", "MUN", "MUN-000001", "enero de 2027",
                150_000m, 0m, 150_000m, null, "La cuota 1 ya se envió a la pagaduría Pagaduría en el archivo MUN-000001 de enero de 2027 y no ha respondido."),
            SentInstallmentAtPayoff = alCancelar,
            PayoffExceptSentInstallmentAmount = salvo,
            SentInstallmentLeftAmount = salvo is null ? null : 150_000m,
        };
        _k.Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "OK", "",
                new ListadoDeObligacionesDto(EstadoDeCartera.Active, ci.Arg<SolicitudDeListado>().PersonPublicId, EscenarioDeRecaudo.Hoy, [o])));
    }

    private bool _abierta;

    private async Task<Result<CashOperationDto>> GuardarAsync(ModalidadDePago modalidad, decimal valor)
    {
        if (!_abierta) await _k.AbrirAsync();
        _abierta = true;
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value.PublicId;
        return await new UpdateCashOperationItemsCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.Obligaciones, _k.Cuentas, parametros: _k.C.E.Parametros)
            .Handle(new UpdateCashOperationItemsCommand(op, [new ItemPedidoDto(ApplicationTarget.Loan, valor, _k.CreditoA, LoanModality: modalidad)])
                { OperationKey = Guid.NewGuid() }, default);
    }

    [Theory]
    [InlineData(CuotaEnviadaAlCancelar.Excluir)]
    [InlineData(CuotaEnviadaAlCancelar.Preguntar)]
    public async Task Con_Excluir_o_Preguntar_la_caja_admite_dejar_la_cuota_enviada(string alCancelar)
    {
        Listado(alCancelar, 1_850_000m);

        var r = await GuardarAsync(ModalidadDePago.PayoffExceptCurrentInstallment, 1_850_000m);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
    }

    [Fact]
    public async Task Con_Incluir_de_fabrica_la_caja_no_deja_la_cuota_y_la_cancelacion_total_sigue()
    {
        Listado(CuotaEnviadaAlCancelar.Incluir, 1_850_000m);

        var dejarla = await GuardarAsync(ModalidadDePago.PayoffExceptCurrentInstallment, 1_850_000m);
        dejarla.IsFailure.Should().BeTrue();
        JsonSerializer.Serialize(((ErrorConDatos)dejarla.Error).Data).Should().Contain(ErroresDeRecaudo.CodigoModalidadNoAdmitida);

        (await GuardarAsync(ModalidadDePago.FullPayoff, 2_000_000m)).IsSuccess.Should().BeTrue("la cancelación total la cobra, como siempre");
    }

    [Fact]
    public async Task Sin_el_valor_de_Cartera_la_cuota_no_se_deja_aunque_la_linea_lo_pida()
    {
        Listado(CuotaEnviadaAlCancelar.Excluir, null);

        var r = await GuardarAsync(ModalidadDePago.PayoffExceptCurrentInstallment, 1_850_000m);

        r.IsFailure.Should().BeTrue("la cuota ya no está en su canal sin respuesta o no es la causada");
    }
}
