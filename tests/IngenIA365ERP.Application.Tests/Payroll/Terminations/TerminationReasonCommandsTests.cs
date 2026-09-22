using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Terminations;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Terminations;

/// <summary>
/// Revisión de N1 (feature 010): un motivo propio de la cooperativa no genera indemnización —la marca la
/// pone la ley y sólo la lleva <c>DESP_SINJC</c> sembrado— y eso lo dice el <b>handler</b> con su código
/// (422 <c>Payroll.Termination.ReasonSeverancePayReserved</c>, contracts/api.md §3.6). El validador no
/// lo mira: cuando lo miraba, el pipeline respondía 400 <c>Validation.Invalid</c>, el código nunca llegaba
/// al cliente y la e2e toleraba «400 o 422» para no decidirlo.
/// </summary>
public class TerminationReasonCommandsTests
{
    [Fact]
    public async Task Un_motivo_propio_con_indemnizacion_lo_rechaza_el_handler_con_su_codigo_no_el_validador()
    {
        var d = new NominaTestData();
        var comando = new CreateTerminationReasonCommand("RETIRO_X", "Retiro propio", GeneratesSeverancePay: true, RequiresContractEndDate: false);

        new CreateTerminationReasonCommandValidator().Validate(comando).IsValid
            .Should().BeTrue("el validador deja pasar la marca para que el handler responda con el código del contrato");

        var r = await new CreateTerminationReasonCommandHandler(d.Db, d.Clock, d.User).Handle(comando, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Termination.ReasonSeverancePayReserved");
        (await d.Db.TerminationReasons.AnyAsync(m => m.Code == "RETIRO_X")).Should().BeFalse();
    }

    [Fact]
    public async Task Un_motivo_propio_sin_indemnizacion_se_crea_normalizando_el_codigo()
    {
        var d = new NominaTestData();

        var r = await new CreateTerminationReasonCommandHandler(d.Db, d.Clock, d.User)
            .Handle(new CreateTerminationReasonCommand("retiro_y", "Retiro propio", GeneratesSeverancePay: false, RequiresContractEndDate: false), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var motivo = await d.Db.TerminationReasons.SingleAsync(m => m.PublicId == r.Value);
        motivo.Code.Should().Be("RETIRO_Y");
        motivo.GeneratesSeverancePay.Should().BeFalse();
        motivo.IsSeeded.Should().BeFalse();
    }
}
