using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, T0102 (FR-037): lo que una persona puede pagar sale de los contratos —las obligaciones por <c>ListarAsync</c> con exigible, cuota, vencido,
/// total para cancelar, modalidades y bloqueos; las cuentas por <c>ConsultarAsync</c> y lo exigible por <c>ListarExigiblesAsync</c>— como el recaudador
/// <c>REC</c>, más los conceptos varios de ingreso; la persona se busca por documento o código; la lectura se audita. Escrita en la fase 6; se corre al final.
/// </summary>
public class LoQuePuedePagarTests
{
    private readonly CajaConContratos _k = new();

    private void ConObligacion()
    {
        _k.Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "OK", "", new ListadoDeObligacionesDto(EstadoDeCartera.Active,
                ci.Arg<SolicitudDeListado>().PersonPublicId, EscenarioDeRecaudo.Hoy,
                [new ObligacionListadaDto(_k.CreditoA, "CR-1", "CONS", "Consumo", RolEnLaObligacion.Debtor, EstadoDeObligacion.Active, 5, 120_000, 100_000, 20_000,
                    2_000_000, 0, 0, [ModalidadDePago.CycleInstallment, ModalidadDePago.ExtraordinaryPrincipal], [], false, false)])));
        _k.Cuentas.ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(ResultadoDelContrato.Answered, "OK", "", new ExigiblesDeCuentasDto(EstadoDeCartera.Active,
                EstadoDeCartera.Active, [], [])));
    }

    [Fact]
    public async Task Trae_obligaciones_cuentas_exigibles_y_conceptos_como_el_recaudador_de_Recaudo()
    {
        ConObligacion();

        var r = await _k.Pagables().Handle(new GetPayablesQuery(Document: "2001"), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.PersonPublicId.Should().Be(_k.Asociado.PublicId);
        r.Value.Obligations!.Obligations.Single().CycleInstallment.Should().Be(100_000);
        r.Value.MiscConcepts.Should().ContainSingle(c => c.Code == "CERT");
        await _k.Obligaciones.Received(1).ListarAsync(Arg.Is<SolicitudDeListado>(s => s.Collector.Code == "REC" && s.Collector.Class == ClaseDeRecaudador.Receipts),
            Arg.Any<CancellationToken>());
        await _k.Cuentas.Received(1).ConsultarAsync(Arg.Any<SolicitudDeConsultaDeCuentas>(), Arg.Any<CancellationToken>());
        await _k.Cuentas.Received(1).ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_rechazo_del_modulo_queda_como_aviso_sin_perder_lo_demas()
    {
        _k.Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Rejected, "Lending.NotActivated", "Cartera no está activada.", null));
        _k.Cuentas.ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(ResultadoDelContrato.Answered, "OK", "", null));

        var r = await _k.Pagables().Handle(new GetPayablesQuery(_k.Asociado.PublicId), default);

        r.Value.Notices.Should().Contain("Cartera no está activada.");
        r.Value.Obligations.Should().BeNull();
    }

    [Fact]
    public async Task La_persona_que_no_existe_responde_su_codigo()
    {
        var r = await _k.Pagables().Handle(new GetPayablesQuery(Document: "nadie"), default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoPersonaNoEncontrada);
    }

    [Fact]
    public async Task Los_conceptos_del_sistema_y_los_de_egreso_no_se_ofrecen()
    {
        ConObligacion();
        _k.C.E.Db.MiscConcepts.AddRange(
            new MiscConcept { Code = "SOBRANTE", Name = "Sobrantes", IsSystem = true },
            new MiscConcept { Code = "EGRESO", Name = "Egreso", Direction = MiscConceptDirection.Expense });
        await _k.C.E.Db.SaveChangesAsync();

        var r = await _k.Pagables().Handle(new GetPayablesQuery(_k.Asociado.PublicId), default);

        r.Value.MiscConcepts.Select(c => c.Code).Should().BeEquivalentTo(["CERT"]);
    }
}
